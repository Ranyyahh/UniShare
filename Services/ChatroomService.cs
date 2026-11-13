using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using MongoDB.Bson;
using MongoDB.Driver;
using UniShare.Models;

namespace UniShare.Services
{
    public static class ChatroomService
    {
        private static readonly Lazy<bool> IndexesReady = new Lazy<bool>(() =>
        {
            MongoDatabase.Conversations.Indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("itemId").Ascending("participantA").Ascending("participantB"),
                new CreateIndexOptions { Unique = true, Name = "item_participants_unique" }));
            MongoDatabase.Messages.Indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("conversationId").Ascending("createdAt"),
                new CreateIndexOptions { Name = "conversation_created" }));
            MongoDatabase.Messages.Indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("conversationId").Ascending("readBy"),
                new CreateIndexOptions { Name = "conversation_readby" }));
            return true;
        });

        public static ObjectId? StartOrFind(ObjectId currentUserId, ObjectId ownerId, ObjectId itemId)
        {
            EnsureIndexes();
            if (currentUserId == ownerId) return null;
            var item = MongoDatabase.Items.Find(x => x.Id == itemId && x.OwnerId == ownerId).FirstOrDefault();
            if (item == null) return null;
            var participants = new[] { currentUserId, ownerId }.OrderBy(x => x.ToString(), StringComparer.Ordinal).ToArray();
            var filter = Builders<BsonDocument>.Filter.Eq("itemId", itemId) &
                         Builders<BsonDocument>.Filter.Eq("participantA", participants[0]) &
                         Builders<BsonDocument>.Filter.Eq("participantB", participants[1]);
            var found = MongoDatabase.Conversations.Find(filter).FirstOrDefault();
            if (found != null) return found.GetValue("_id").AsObjectId;

            var now = DateTime.UtcNow;
            var conversation = new BsonDocument
            {
                { "_id", ObjectId.GenerateNewId() }, { "itemId", itemId },
                { "participantA", participants[0] }, { "participantB", participants[1] },
                { "createdAt", now }, { "updatedAt", now }, { "lastMessage", "" },
                { "lastMessageAt", BsonNull.Value }, { "lastMessageBy", BsonNull.Value }
            };
            try { MongoDatabase.Conversations.InsertOne(conversation); return conversation["_id"].AsObjectId; }
            catch (MongoWriteException ex) when (ex.WriteError != null && ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                found = MongoDatabase.Conversations.Find(filter).FirstOrDefault();
                return found == null ? (ObjectId?)null : found.GetValue("_id").AsObjectId;
            }
        }

        public static List<ChatConversationViewModel> GetConversations(ObjectId userId)
        {
            EnsureIndexes();
            var filter = Builders<BsonDocument>.Filter.Eq("participantA", userId) | Builders<BsonDocument>.Filter.Eq("participantB", userId);
            var conversations = MongoDatabase.Conversations.Find(filter).Sort(Builders<BsonDocument>.Sort.Descending("updatedAt")).ToList();
            var result = new List<ChatConversationViewModel>();
            foreach (var conversation in conversations)
            {
                ObjectId otherId;
                if (!TryOtherParticipant(conversation, userId, out otherId)) continue;
                var person = MongoDatabase.Users.Find(x => x.Id == otherId).FirstOrDefault();
                var itemIdValue = conversation.GetValue("itemId", BsonNull.Value);
                var item = itemIdValue.IsObjectId ? MongoDatabase.Items.Find(x => x.Id == itemIdValue.AsObjectId).FirstOrDefault() : null;
                var id = conversation.GetValue("_id", BsonNull.Value);
                if (!id.IsObjectId || person == null) continue;
                var lastMessage = conversation.GetValue("lastMessage", "").ToString();
                var lastAt = ReadDate(conversation, "lastMessageAt");
                var unreadFilter = Builders<BsonDocument>.Filter.Eq("conversationId", id) &
                                   Builders<BsonDocument>.Filter.Ne("senderId", userId) &
                                   Builders<BsonDocument>.Filter.Not(Builders<BsonDocument>.Filter.AnyEq("readBy", userId));
                var unread = MongoDatabase.Messages.Find(unreadFilter).Limit(1).Any();
                result.Add(new ChatConversationViewModel
                {
                    Id = id.AsObjectId.ToString(), With = FullName(person), Initials = Initials(person),
                    ItemName = item == null ? "Rental item" : item.Name,
                    LastMessage = lastMessage, Time = RelativeTime(lastAt), Unread = unread,
                    ItemId = item == null ? "" : item.Id.ToString(),
                    ItemImage = item == null || item.Photos == null || item.Photos.Count == 0 ? "" : MarketplaceService.ToImageUrl(item.Photos[0]),
                    ItemUrl = item == null ? "" : VirtualPathUtility.ToAbsolute("~/BrowseItems/ItemDetails/" + item.Id),
                    ItemPrice = item == null ? "" : FormatPrice(item),
                    RentalUrl = VirtualPathUtility.ToAbsolute("~/BorrowedItems")
                });
            }
            return result;
        }

        public static ChatThreadViewModel GetThread(ObjectId userId, string conversationId)
        {
            EnsureIndexes();
            ObjectId id;
            if (!ObjectId.TryParse(conversationId, out id)) return null;
            var conversation = FindParticipantConversation(id, userId);
            if (conversation == null) return null;
            ObjectId otherId;
            if (!TryOtherParticipant(conversation, userId, out otherId)) return null;
            var person = MongoDatabase.Users.Find(x => x.Id == otherId).FirstOrDefault();
            var itemValue = conversation.GetValue("itemId", BsonNull.Value);
            var item = itemValue.IsObjectId ? MongoDatabase.Items.Find(x => x.Id == itemValue.AsObjectId).FirstOrDefault() : null;
            var messages = MongoDatabase.Messages.Find(Builders<BsonDocument>.Filter.Eq("conversationId", id))
                .Sort(Builders<BsonDocument>.Sort.Descending("createdAt")).Limit(500).ToList();
            messages.Reverse();
            var now = DateTime.UtcNow;
            MongoDatabase.Messages.UpdateMany(
                Builders<BsonDocument>.Filter.Eq("conversationId", id) & Builders<BsonDocument>.Filter.Ne("senderId", userId),
                Builders<BsonDocument>.Update.AddToSet("readBy", userId));

            return new ChatThreadViewModel
            {
                Id = id.ToString(), With = person == null ? "Student" : FullName(person),
                Initials = person == null ? "ST" : Initials(person), ItemName = item == null ? "Rental item" : item.Name,
                ItemId = item == null ? "" : item.Id.ToString(),
                ItemImage = item == null || item.Photos == null || item.Photos.Count == 0 ? "" : MarketplaceService.ToImageUrl(item.Photos[0]),
                ItemUrl = item == null ? "" : VirtualPathUtility.ToAbsolute("~/BrowseItems/ItemDetails/" + item.Id),
                ItemPrice = item == null ? "" : FormatPrice(item), RentalUrl = VirtualPathUtility.ToAbsolute("~/BorrowedItems"),
                Messages = messages.Select(m => new ChatMessageViewModel
                {
                    Id = m.GetValue("_id", BsonNull.Value).ToString(),
                    Mine = m.GetValue("senderId", BsonNull.Value).IsObjectId && m["senderId"].AsObjectId == userId,
                    Text = m.GetValue("text", "").ToString(),
                    CreatedAt = ReadDate(m, "createdAt") ?? now,
                    Time = (ReadDate(m, "createdAt") ?? now).ToLocalTime().ToString("h:mm tt", CultureInfo.InvariantCulture),
                    Day = (ReadDate(m, "createdAt") ?? now).ToLocalTime().ToString("MMM d, yyyy", CultureInfo.InvariantCulture)
                }).ToList()
            };
        }

        public static ChatMessageViewModel Send(ObjectId userId, string conversationId, string text)
        {
            EnsureIndexes();
            ObjectId id;
            text = (text ?? "").Trim();
            if (!ObjectId.TryParse(conversationId, out id) || text.Length == 0 || text.Length > 500 || FindParticipantConversation(id, userId) == null) return null;
            var now = DateTime.UtcNow;
            var message = new BsonDocument
            {
                { "_id", ObjectId.GenerateNewId() }, { "conversationId", id }, { "senderId", userId },
                { "text", text }, { "readBy", new BsonArray { userId } }, { "createdAt", now }
            };
            MongoDatabase.Messages.InsertOne(message);
            MongoDatabase.Conversations.UpdateOne(Builders<BsonDocument>.Filter.Eq("_id", id),
                Builders<BsonDocument>.Update.Set("lastMessage", text).Set("lastMessageAt", now)
                    .Set("lastMessageBy", userId).Set("updatedAt", now));
            return new ChatMessageViewModel { Id = message["_id"].AsObjectId.ToString(), Mine = true, Text = text, CreatedAt = now, Time = now.ToLocalTime().ToString("h:mm tt", CultureInfo.InvariantCulture), Day = now.ToLocalTime().ToString("MMM d, yyyy", CultureInfo.InvariantCulture) };
        }

        private static BsonDocument FindParticipantConversation(ObjectId id, ObjectId userId)
        {
            return MongoDatabase.Conversations.Find(Builders<BsonDocument>.Filter.Eq("_id", id) &
                (Builders<BsonDocument>.Filter.Eq("participantA", userId) | Builders<BsonDocument>.Filter.Eq("participantB", userId))).FirstOrDefault();
        }

        private static bool TryOtherParticipant(BsonDocument doc, ObjectId userId, out ObjectId otherId)
        {
            otherId = ObjectId.Empty;
            var a = doc.GetValue("participantA", BsonNull.Value);
            var b = doc.GetValue("participantB", BsonNull.Value);
            if (!a.IsObjectId || !b.IsObjectId) return false;
            if (a.AsObjectId == userId) otherId = b.AsObjectId;
            else if (b.AsObjectId == userId) otherId = a.AsObjectId;
            else return false;
            return true;
        }

        private static string FullName(UserDocument user)
        {
            return string.Join(" ", new[] { user.FirstName, user.MiddleName, user.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
        }

        private static string Initials(UserDocument user)
        {
            return string.Concat(new[] { user.FirstName, user.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => char.ToUpperInvariant(x.Trim()[0])));
        }

        private static string FormatPrice(ItemDocument item)
        {
            var amount = item.RentalDetails == null ? 0 : item.RentalDetails.HourlyRate;
            return amount > 0 ? "₱" + amount.ToString("0.##", CultureInfo.InvariantCulture) : "";
        }

        private static DateTime? ReadDate(BsonDocument doc, string field)
        {
            var value = doc.GetValue(field, BsonNull.Value);
            return value.IsValidDateTime ? (DateTime?)value.ToUniversalTime() : null;
        }

        private static string RelativeTime(DateTime? date)
        {
            if (!date.HasValue) return "";
            var local = date.Value.ToLocalTime();
            if (local.Date == DateTime.Now.Date) return local.ToString("h:mm tt", CultureInfo.InvariantCulture);
            if (local.Date == DateTime.Now.Date.AddDays(-1)) return "Yesterday";
            return local.ToString("MMM d", CultureInfo.InvariantCulture);
        }

        private static void EnsureIndexes() { var ready = IndexesReady.Value; }
    }
}
