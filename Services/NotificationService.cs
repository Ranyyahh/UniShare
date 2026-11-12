using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using UniShare.Models;

namespace UniShare.Services
{
    public static class NotificationService
    {
        public static List<NotificationItem> GetForUser(ObjectId userId)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("borrowerId", userId) |
                         Builders<BsonDocument>.Filter.Eq("ownerId", userId) |
                         Builders<BsonDocument>.Filter.Eq("borrowerId", userId.ToString()) |
                         Builders<BsonDocument>.Filter.Eq("ownerId", userId.ToString());
            var bookings = MongoDatabase.Bookings.Find(filter).SortByDescending(x => x["createdAt"]).Limit(40).ToList();
            var keys = bookings.Select(x => EventKey(x)).ToList();
            var readKeys = keys.Count == 0 ? new HashSet<string>() : new HashSet<string>(MongoDatabase.NotificationReads
                .Find(Builders<BsonDocument>.Filter.Eq("userId", userId.ToString()) & Builders<BsonDocument>.Filter.In("eventKey", keys))
                .ToList().Where(x => x.GetValue("isRead", false).ToBoolean()).Select(x => x.GetValue("eventKey", "").ToString()));

            return bookings.Select((booking, index) =>
            {
                var owner = booking.GetValue("ownerId", BsonNull.Value).ToString() == userId.ToString();
                var itemName = ResolveItemName(booking);
                var status = booking.GetValue("status", "PENDING").ToString().Trim().ToUpperInvariant();
                var created = ReadDate(booking, "updatedAt") ?? ReadDate(booking, "createdAt") ?? DateTime.UtcNow;
                string title, body;
                switch (status)
                {
                    case "PENDING": case "REQUESTED":
                        title = owner ? "New rental request" : "Rental request submitted";
                        body = owner ? BorrowerName(booking) + " requested to rent " + itemName + "." : "Your request to rent " + itemName + " is awaiting the owner.";
                        break;
                    case "ACCEPTED": case "APPROVED":
                        title = "Rental request accepted"; body = "The request for " + itemName + " was accepted."; break;
                    case "DECLINED": case "REJECTED":
                        title = "Rental request declined"; body = "The request for " + itemName + " was declined."; break;
                    case "ONGOING": case "ACTIVE":
                        title = "Rental started"; body = "The rental for " + itemName + " is now active."; break;
                    case "RETURN_REQUESTED": case "RETURN PENDING":
                        title = "Return requested"; body = "A return was requested for " + itemName + "."; break;
                    case "COMPLETED": case "COMPLETE": case "RETURNED":
                        title = "Rental completed"; body = "The rental for " + itemName + " has been completed."; break;
                    case "DISPUTED":
                        title = "Rental issue reported"; body = "A reported issue needs attention for " + itemName + "."; break;
                    case "CANCELLED": case "CANCELED": case "EXPIRED":
                        title = "Rental request updated"; body = "The request for " + itemName + " is " + status.ToLowerInvariant() + "."; break;
                    default:
                        title = "Rental update"; body = "There is an update for " + itemName + "."; break;
                }
                return new NotificationItem
                {
                    Id = index + 1, EventKey = keys[index], Title = title, Body = body,
                    Time = created.ToLocalTime().ToString("MMM d, h:mm tt", CultureInfo.InvariantCulture),
                    Unread = !readKeys.Contains(keys[index]), LinkUrl = "/MyRentals/Index"
                };
            }).ToList();
        }

        public static void MarkAllRead(ObjectId userId)
        {
            foreach (var item in GetForUser(userId))
            {
                var filter = Builders<BsonDocument>.Filter.Eq("userId", userId.ToString()) & Builders<BsonDocument>.Filter.Eq("eventKey", item.EventKey);
                MongoDatabase.NotificationReads.UpdateOne(filter,
                    Builders<BsonDocument>.Update.Set("isRead", true).Set("readAt", DateTime.UtcNow), new UpdateOptions { IsUpsert = true });
            }
        }

        private static string EventKey(BsonDocument booking)
        {
            return booking.GetValue("_id", ObjectId.Empty).ToString() + ":" +
                   booking.GetValue("status", "PENDING").ToString().Trim().ToUpperInvariant() + ":" +
                   (ReadDate(booking, "updatedAt") ?? ReadDate(booking, "createdAt") ?? DateTime.MinValue).Ticks;
        }

        private static DateTime? ReadDate(BsonDocument document, string key)
        {
            var value = document.GetValue(key, BsonNull.Value);
            return value.IsValidDateTime ? (DateTime?)value.ToUniversalTime() : null;
        }

        private static string ResolveItemName(BsonDocument booking)
        {
            var id = booking.GetValue("itemId", BsonNull.Value);
            if (id.IsObjectId)
            {
                var item = MongoDatabase.Items.Find(x => x.Id == id.AsObjectId).FirstOrDefault();
                if (item != null && !string.IsNullOrWhiteSpace(item.Name)) return item.Name;
            }
            return booking.GetValue("itemName", "the item").ToString();
        }

        private static string BorrowerName(BsonDocument booking)
        {
            var id = booking.GetValue("borrowerId", BsonNull.Value);
            ObjectId borrowerId;
            if (id.IsObjectId) borrowerId = id.AsObjectId;
            else if (!ObjectId.TryParse(id.ToString(), out borrowerId)) return "A student";
            var user = MongoDatabase.Users.Find(x => x.Id == borrowerId).FirstOrDefault();
            return user == null ? "A student" : (user.FirstName + " " + user.LastName).Trim();
        }
    }
}
