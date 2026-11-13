using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
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
            var start = DateTime.SpecifyKind(handoverDate.Date.Add(time.TimeOfDay), DateTimeKind.Local);
            if (start <= DateTime.Now) throw new BookingException("The handover schedule must be in the future.");

            var item = MarketplaceService.GetItem(itemId);
            if (item == null || item.OwnerId == renterId || !string.Equals(item.ApprovalStatus, "APPROVED", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(item.AvailabilityStatus, "AVAILABLE", StringComparison.OrdinalIgnoreCase))
                throw new BookingException("This item is not available for rental.");
            if (!IsWithinListingSchedule(item, start))
                throw new BookingException("Choose a day and handover time within the owner's availability schedule.");
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
                { "estimatedFee", Math.Round(rate * durationHours, 2, MidpointRounding.AwayFromZero) }, { "status", "PENDING" }, { "lateGraceMinutes", 30 },
                { "createdAt", now }, { "expiresAt", now.AddHours(24) }, { "notes", (notes ?? "").Trim() }, { "itemName", item.Name }
            };
            MongoDatabase.Bookings.InsertOne(doc);
            // Create the unpaid ledger entry with the request so payment history and
            // booking state stay linked from the start of the transaction.
            MongoDatabase.Payments.InsertOne(new BsonDocument {
                { "_id", ObjectId.GenerateNewId() }, { "bookingId", doc["_id"] }, { "payerId", renterId },
                { "ownerId", item.OwnerId }, { "amount", doc["estimatedFee"] }, { "paymentMethod", "CASH" },
                { "status", "UNPAID" }, { "createdAt", now }, { "updatedAt", now }
            });
            MongoDatabase.Bookings.UpdateOne(Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]),
                Builders<BsonDocument>.Update.Set("paymentMethod", "CASH"));
            return doc;
            }
            finally { MongoDatabase.BookingLocks.DeleteOne(Builders<BsonDocument>.Filter.Eq("_id", item.Id) & Builders<BsonDocument>.Filter.Eq("token", lockToken)); }
        }

        public static void ExpirePending()
        {
            var now = DateTime.UtcNow;
            var expiredFilter = Builders<BsonDocument>.Filter.In("status", new[] { "PENDING", "REQUESTED" }) &
                (Builders<BsonDocument>.Filter.Lte("expiresAt", now) | Builders<BsonDocument>.Filter.Lte("createdAt", now.AddHours(-24)));
            var expiredIds = MongoDatabase.Bookings.Find(expiredFilter).Project(x => x["_id"]).ToList();
            if (expiredIds.Count == 0) return;
            MongoDatabase.Bookings.UpdateMany(expiredFilter,
                Builders<BsonDocument>.Update.Set("status", "EXPIRED").Set("expiredAt", now).Set("updatedAt", now));
            MongoDatabase.Payments.UpdateMany(Builders<BsonDocument>.Filter.In("bookingId", expiredIds) & Builders<BsonDocument>.Filter.In("status", new[] { "UNPAID", "PENDING" }),
                Builders<BsonDocument>.Update.Set("voidedAt", now).Set("voidReason", "BOOKING_EXPIRED").Set("updatedAt", now));
        }

        public static List<string> GetUnavailableHandoverSlots(ObjectId itemId, DateTime date, int durationHours)
        {
            if (!new[] { 1, 2, 3, 4, 6, 8, 12, 24 }.Contains(durationHours)) throw new BookingException("Choose one of the available rental durations.");
            ExpirePending();
            var bookings = MongoDatabase.Bookings.Find(Builders<BsonDocument>.Filter.Eq("itemId", itemId)).ToList();
            var item = MongoDatabase.Items.Find(x => x.Id == itemId).FirstOrDefault();
            var unavailable = new List<string>();
            if (item == null) return unavailable;
            foreach (var slot in GetHandoverSlots(item))
            {
                DateTime time;
                if (!DateTime.TryParseExact(slot, new[] { "hh:mm tt", "h:mm tt" }, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out time)) continue;
                var start = date.Date.Add(time.TimeOfDay);
                var end = start.AddHours(durationHours);
                if (start <= DateTime.Now || item == null || !IsWithinListingSchedule(item, start) ||
                    bookings.Any(x => BlocksAvailability(x, DateTime.UtcNow) && Overlaps(x, start, end))) unavailable.Add(slot);
            }
            return unavailable;
        }

        public static List<string> GetHandoverSlots(ItemDocument item)
        {
            var slots = new List<string>();
            if (item == null) return slots;
            TimeSpan earliest, latest;
            if (!TimeSpan.TryParse(item.EarliestHandoverTime, CultureInfo.InvariantCulture, out earliest)) earliest = new TimeSpan(9, 0, 0);
            if (!TimeSpan.TryParse(item.LatestReturnTime, CultureInfo.InvariantCulture, out latest)) latest = new TimeSpan(16, 0, 0);
            if (latest < earliest) return slots;

            // Offer handover times on the half-hour so owner schedules such as 8:30 AM
            // are usable instead of being forced into a hardcoded 9 AM–4 PM window.
            for (var time = earliest; time <= latest; time = time.Add(TimeSpan.FromMinutes(30)))
                slots.Add(DateTime.Today.Add(time).ToString("hh:mm tt", CultureInfo.InvariantCulture));
            return slots;
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
            var borrowerId = booking.GetValue("borrowerId", BsonNull.Value);
            if ((borrowerId.IsObjectId && borrowerId.AsObjectId == ownerId) ||
                (borrowerId.IsString && string.Equals(borrowerId.AsString, ownerId.ToString(), StringComparison.OrdinalIgnoreCase))) return false;
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
                var changed = MongoDatabase.Bookings.UpdateOne(ownerFilter, update).ModifiedCount == 1;
                if (changed && !accept)
                    MongoDatabase.Payments.UpdateMany(Builders<BsonDocument>.Filter.Eq("bookingId", bookingId) & Builders<BsonDocument>.Filter.In("status", new[] { "UNPAID", "PENDING" }),
                        Builders<BsonDocument>.Update.Set("voidedAt", now).Set("voidReason", "BOOKING_DECLINED").Set("updatedAt", now));
                if (changed)
                    AdminAccountabilityService.LogActivity(ownerId, accept ? "BOOKING_ACCEPTED" : "BOOKING_DECLINED", "BOOKING", bookingId,
                        new BsonDocument { { "reason", (reason ?? "").Trim() }, { "notes", (notes ?? "").Trim() } });
                return changed;
            }
            finally { MongoDatabase.BookingLocks.DeleteOne(Builders<BsonDocument>.Filter.Eq("_id", itemId) & Builders<BsonDocument>.Filter.Eq("token", lockId)); }
        }

        public static bool Cancel(ObjectId bookingId, ObjectId renterId)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("_id", bookingId) & Builders<BsonDocument>.Filter.Eq("borrowerId", renterId) &
                Builders<BsonDocument>.Filter.In("status", new[] { "PENDING", "REQUESTED" });
            var now = DateTime.UtcNow;
            var cancelled = MongoDatabase.Bookings.UpdateOne(filter, Builders<BsonDocument>.Update.Set("status", "CANCELLED").Set("cancelledAt", now).Set("updatedAt", now)).ModifiedCount == 1;
            if (cancelled)
            {
                MongoDatabase.Payments.UpdateMany(Builders<BsonDocument>.Filter.Eq("bookingId", bookingId) & Builders<BsonDocument>.Filter.In("status", new[] { "UNPAID", "PENDING" }),
                    Builders<BsonDocument>.Update.Set("voidedAt", now).Set("voidReason", "BOOKING_CANCELLED").Set("updatedAt", now));
                AdminAccountabilityService.LogActivity(renterId, "BOOKING_CANCELLED", "BOOKING", bookingId, new BsonDocument());
            }
            return cancelled;
        }

        private static bool HasAcceptedConflict(ObjectId itemId, DateTime start, DateTime end, ObjectId? except = null)
        {
            var f = Builders<BsonDocument>.Filter.Eq("itemId", itemId) & Builders<BsonDocument>.Filter.Nin("status", NonBlockingStatuses);
            if (except.HasValue) f &= Builders<BsonDocument>.Filter.Ne("_id", except.Value);
            var now = DateTime.UtcNow;
            return MongoDatabase.Bookings.Find(f).ToList().Any(x => BlocksAvailability(x, now) && Overlaps(x, start, end));
        }
        private static bool BlocksAvailability(BsonDocument booking, DateTime now)
        {
            var status = booking.GetValue("status", "").ToString().Trim().ToUpperInvariant();
            if (NonBlockingStatuses.Contains(status)) return false;
            if (AcceptedStatuses.Contains(status)) return true;
            return (status == "PENDING" || status == "REQUESTED") && !IsExpired(booking, now);
        }
        private static bool Overlaps(BsonDocument booking, DateTime start, DateTime end)
        {
            var otherStart = Date(booking, "startDate", Date(booking, "handoverDate", DateTime.MinValue)).ToLocalTime();
            var otherEnd = Date(booking, "endDate", otherStart.AddHours(Number(booking, "durationHours", 1))).ToLocalTime();
            var localStart = start.Kind == DateTimeKind.Utc ? start.ToLocalTime() : start;
            var localEnd = end.Kind == DateTimeKind.Utc ? end.ToLocalTime() : end;
            return otherStart < localEnd && otherEnd > localStart;
        }
        private static bool IsWithinListingSchedule(ItemDocument item, DateTime start)
        {
            var availableDays = item.AvailableDays ?? new List<string>();
            if (availableDays.Count > 0 && !availableDays.Any(x => String.Equals(x, start.ToString("dddd", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase))) return false;
            DateTime earliest, latest;
            if (!String.IsNullOrWhiteSpace(item.EarliestHandoverTime) &&
                DateTime.TryParseExact(item.EarliestHandoverTime, new[] { "hh:mm tt", "h:mm tt" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out earliest) &&
                start.TimeOfDay < earliest.TimeOfDay) return false;
            if (!String.IsNullOrWhiteSpace(item.LatestReturnTime) &&
                DateTime.TryParseExact(item.LatestReturnTime, new[] { "hh:mm tt", "h:mm tt" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out latest) &&
                start.TimeOfDay > latest.TimeOfDay) return false;
            return true;
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
