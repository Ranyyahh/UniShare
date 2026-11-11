using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using UniShare.Models;

namespace UniShare.Services
{
    /// <summary>Booking persistence and state transitions for the MongoDB-backed MVC app.</summary>
    public static class BookingService
    {
        private static readonly string[] NonBlockingStatuses = { "CANCELLED", "CANCELED", "DECLINED", "EXPIRED", "REJECTED" };
        private static readonly string[] AcceptedStatuses = { "ACCEPTED", "ACTIVE", "ONGOING", "OVERDUE" };

        public static BsonDocument Submit(ObjectId renterId, string itemId, DateTime handoverDate, string handoverTime, int durationHours, string notes)
        {
            ExpirePending();
            ObjectId parsedItemId;
            if (!ObjectId.TryParse(itemId, out parsedItemId)) throw new BookingException("The selected item is invalid.");
            if (durationHours != 1 && durationHours != 2 && durationHours != 3 && durationHours != 4 && durationHours != 6 && durationHours != 8 && durationHours != 12 && durationHours != 24)
                throw new BookingException("Choose one of the available rental durations.");
            DateTime time;
            if (!DateTime.TryParseExact(handoverTime, new[] { "hh:mm tt", "h:mm tt" }, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out time)) throw new BookingException("Choose a valid handover time.");
            var start = handoverDate.Date.Add(time.TimeOfDay);
            if (start <= DateTime.Now) throw new BookingException("The handover schedule must be in the future.");

            var item = MarketplaceService.GetItem(itemId);
            if (item == null || item.OwnerId == renterId || !string.Equals(item.ApprovalStatus, "APPROVED", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(item.AvailabilityStatus, "AVAILABLE", StringComparison.OrdinalIgnoreCase))
                throw new BookingException("This item is not available for rental.");
            var end = start.AddHours(durationHours);
            var rate = item.RentalDetails == null ? 0 : item.RentalDetails.HourlyRate;
            if (rate <= 0 && item.RentalDetails != null && item.RentalDetails.PricingUnitHours > 0)
                rate = item.RentalDetails.PricePerDay / 24d;
            if (rate <= 0) throw new BookingException("The owner has not configured a valid rental rate.");
            EnsureLockIndex();
            var lockToken = ObjectId.GenerateNewId();
            AcquireItemLock(item.Id, lockToken);
            try
            {
            if (HasAcceptedConflict(item.Id, start, end)) throw new BookingException("That schedule is already booked or temporarily reserved. Choose another date or time.");
            var now = DateTime.UtcNow;
            var doc = new BsonDocument {
                { "_id", ObjectId.GenerateNewId() }, { "requestId", "REQ-" + now.ToString("yyyyMMdd") + "-" + ObjectId.GenerateNewId().ToString().Substring(18).ToUpperInvariant() },
                { "itemId", item.Id }, { "borrowerId", renterId }, { "ownerId", item.OwnerId }, { "startDate", start }, { "endDate", end },
                { "handoverDate", start }, { "handoverTime", handoverTime }, { "durationHours", durationHours }, { "duration", durationHours + " hours" },
                { "estimatedFee", Math.Round(rate * durationHours, 2, MidpointRounding.AwayFromZero) }, { "status", "PENDING" },
                { "createdAt", now }, { "expiresAt", now.AddHours(24) }, { "notes", (notes ?? "").Trim() }, { "itemName", item.Name }
            };
            MongoDatabase.Bookings.InsertOne(doc);
            return doc;
            }
            finally { MongoDatabase.BookingLocks.DeleteOne(Builders<BsonDocument>.Filter.Eq("_id", item.Id) & Builders<BsonDocument>.Filter.Eq("token", lockToken)); }
        }

        public static void ExpirePending()
        {
            var now = DateTime.UtcNow;
            MongoDatabase.Bookings.UpdateMany(
                Builders<BsonDocument>.Filter.In("status", new[] { "PENDING", "REQUESTED" }) &
                (Builders<BsonDocument>.Filter.Lte("expiresAt", now) | Builders<BsonDocument>.Filter.Lte("createdAt", now.AddHours(-24))),
                Builders<BsonDocument>.Update.Set("status", "EXPIRED").Set("expiredAt", now));
        }

        public static List<BsonDocument> ForRenter(ObjectId id) { ExpirePending(); return MongoDatabase.Bookings.Find(Builders<BsonDocument>.Filter.Eq("borrowerId", id)).SortByDescending(x => x["createdAt"]).ToList(); }
        public static List<BsonDocument> ForOwner(ObjectId id) { ExpirePending(); return MongoDatabase.Bookings.Find(Builders<BsonDocument>.Filter.Eq("ownerId", id)).SortByDescending(x => x["createdAt"]).ToList(); }
        public static BsonDocument GetForParticipant(string id, ObjectId userId)
        {
            ExpirePending(); ObjectId oid;
            var idFilter = ObjectId.TryParse(id, out oid)
                ? Builders<BsonDocument>.Filter.Eq("_id", oid)
                : Builders<BsonDocument>.Filter.Eq("requestId", id);
            return MongoDatabase.Bookings.Find(idFilter &
                (Builders<BsonDocument>.Filter.Eq("borrowerId", userId) | Builders<BsonDocument>.Filter.Eq("ownerId", userId))).FirstOrDefault();
        }

        public static bool Decide(ObjectId bookingId, ObjectId ownerId, bool accept, string reason, string notes)
        {
            EnsureLockIndex();
            var lockId = ObjectId.GenerateNewId();
            var now = DateTime.UtcNow;
            BsonDocument booking = null;
            var ownerFilter = Builders<BsonDocument>.Filter.Eq("_id", bookingId) & Builders<BsonDocument>.Filter.Eq("ownerId", ownerId) & Builders<BsonDocument>.Filter.In("status", new[] { "PENDING", "REQUESTED" });
            booking = MongoDatabase.Bookings.Find(ownerFilter).FirstOrDefault();
            if (booking == null) return false;
            if (!accept && string.IsNullOrWhiteSpace(reason)) return false;
            if (IsExpired(booking, now)) { ExpirePending(); return false; }
            var itemId = booking.GetValue("itemId", BsonNull.Value);
            if (!itemId.IsObjectId) return false;
            var ownedItem = MongoDatabase.Items.Find(x => x.Id == itemId.AsObjectId && x.OwnerId == ownerId).FirstOrDefault();
            if (ownedItem == null) return false;
            try { AcquireItemLock(itemId.AsObjectId, lockId); }
            catch (BookingException) { return false; }
            try
            {
                booking = MongoDatabase.Bookings.Find(ownerFilter).FirstOrDefault();
                if (booking == null || IsExpired(booking, DateTime.UtcNow)) { ExpirePending(); return false; }
                var start = Date(booking, "startDate", Date(booking, "handoverDate", DateTime.MinValue));
                var end = Date(booking, "endDate", start.AddHours(Number(booking, "durationHours", 1)));
                if (accept && HasAcceptedConflict(itemId.AsObjectId, start, end, bookingId)) return false;
                var update = accept
                    ? Builders<BsonDocument>.Update.Set("status", "ACCEPTED").Set("acceptedAt", DateTime.UtcNow)
                    : Builders<BsonDocument>.Update.Set("status", "DECLINED").Set("declinedAt", DateTime.UtcNow).Set("declineReason", (reason ?? "").Trim()).Set("declineNotes", (notes ?? "").Trim());
                return MongoDatabase.Bookings.UpdateOne(ownerFilter, update).ModifiedCount == 1;
            }
            finally { MongoDatabase.BookingLocks.DeleteOne(Builders<BsonDocument>.Filter.Eq("_id", itemId) & Builders<BsonDocument>.Filter.Eq("token", lockId)); }
        }

        public static bool Cancel(ObjectId bookingId, ObjectId renterId)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("_id", bookingId) & Builders<BsonDocument>.Filter.Eq("borrowerId", renterId) &
                Builders<BsonDocument>.Filter.In("status", new[] { "PENDING", "REQUESTED" });
            return MongoDatabase.Bookings.UpdateOne(filter, Builders<BsonDocument>.Update.Set("status", "CANCELLED").Set("cancelledAt", DateTime.UtcNow)).ModifiedCount == 1;
        }

        private static bool HasAcceptedConflict(ObjectId itemId, DateTime start, DateTime end, ObjectId? except = null)
        {
            var f = Builders<BsonDocument>.Filter.Eq("itemId", itemId) & Builders<BsonDocument>.Filter.Nin("status", NonBlockingStatuses);
            if (except.HasValue) f &= Builders<BsonDocument>.Filter.Ne("_id", except.Value);
            return MongoDatabase.Bookings.Find(f).ToList().Any(x =>
            {
                var status = x.GetValue("status", "").ToString().ToUpperInvariant();
                if (!AcceptedStatuses.Contains(status) && !((status == "PENDING" || status == "REQUESTED") && !IsExpired(x, DateTime.UtcNow))) return false;
                var otherStart = Date(x, "startDate", Date(x, "handoverDate", DateTime.MinValue));
                var otherEnd = Date(x, "endDate", otherStart.AddHours(Number(x, "durationHours", 1)));
                // The current request form disables a whole calendar date if any reservation touches it.
                return otherStart.Date < end.Date && otherEnd.Date >= start.Date;
            });
        }
        private static bool IsExpired(BsonDocument d, DateTime now) { return Date(d, "expiresAt", Date(d, "createdAt", now).AddHours(24)) <= now; }
        private static DateTime Date(BsonDocument d, string key, DateTime fallback) { var v = d.GetValue(key, BsonNull.Value); return v.IsValidDateTime ? v.ToUniversalTime() : fallback; }
        private static int Number(BsonDocument d, string key, int fallback) { var v = d.GetValue(key, BsonNull.Value); return v.IsNumeric ? v.ToInt32() : fallback; }
        private static void EnsureLockIndex()
        {
            // MongoDB creates a unique _id index automatically; explicitly setting unique on it
            // is rejected by some server versions.
            MongoDatabase.BookingLocks.Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("expiresAt"), new CreateIndexOptions { ExpireAfter = TimeSpan.Zero }));
        }
        private static void AcquireItemLock(ObjectId itemId, ObjectId token)
        {
            var now = DateTime.UtcNow;
            MongoDatabase.BookingLocks.DeleteOne(Builders<BsonDocument>.Filter.Eq("_id", itemId) & Builders<BsonDocument>.Filter.Lte("expiresAt", now));
            try { MongoDatabase.BookingLocks.InsertOne(new BsonDocument { { "_id", itemId }, { "token", token }, { "expiresAt", now.AddSeconds(30) } }); }
            catch (MongoWriteException) { throw new BookingException("Another request is updating this item schedule. Please try again."); }
        }
    }

    public class BookingException : Exception { public BookingException(string message) : base(message) { } }
}
