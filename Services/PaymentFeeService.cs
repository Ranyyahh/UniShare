using System;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;

namespace UniShare.Services
{
    /// <summary>Payment and separately tracked late-fee records for a booking.</summary>
    public static class PaymentFeeService
    {
        private static readonly string[] ClosedBookingStatuses = { "CANCELLED", "CANCELED", "DECLINED", "REJECTED", "EXPIRED" };
        private static readonly Lazy<bool> IndexesReady = new Lazy<bool>(() =>
        {
            MongoDatabase.Payments.Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("bookingId"), new CreateIndexOptions { Unique = true, Name = "bookingId_unique" }));
            MongoDatabase.LateFees.Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("bookingId"), new CreateIndexOptions { Unique = true, Name = "bookingId_unique" }));
            return true;
        });

        public static BsonDocument GetPayment(string bookingId, ObjectId participantId)
        {
            EnsureIndexes();
            ObjectId id;
            if (!ObjectId.TryParse(bookingId, out id)) return null;
            var booking = GetBooking(id, participantId);
            return booking == null ? null : MongoDatabase.Payments.Find(Builders<BsonDocument>.Filter.Eq("bookingId", id)).FirstOrDefault();
        }

        public static bool SelectMethod(string bookingId, ObjectId borrowerId)
        {
            EnsureIndexes();
            ObjectId id;
            if (!ObjectId.TryParse(bookingId, out id)) return false;
            var booking = MongoDatabase.Bookings.Find(Builders<BsonDocument>.Filter.Eq("_id", id) &
                Builders<BsonDocument>.Filter.Eq("borrowerId", borrowerId)).FirstOrDefault();
            if (booking == null || ClosedBookingStatuses.Contains(Status(booking))) return false;
            var now = DateTime.UtcNow;
            var amount = Number(booking, "estimatedFee", Number(booking, "rentalFee", Number(booking, "totalFee", 0d)));
            var filter = Builders<BsonDocument>.Filter.Eq("bookingId", id);
            var existing = MongoDatabase.Payments.Find(filter).FirstOrDefault();
            if (existing != null && Status(existing) != "UNPAID" && Status(existing) != "PENDING") return false;
            var update = Builders<BsonDocument>.Update.Set("bookingId", id).Set("payerId", borrowerId)
                .Set("ownerId", booking.GetValue("ownerId", BsonNull.Value)).Set("amount", amount)
                .Set("paymentMethod", "CASH").Unset("transactionReference")
                .Set("status", "UNPAID").Set("updatedAt", now).SetOnInsert("createdAt", now);
            MongoDatabase.Payments.UpdateOne(filter, update, new UpdateOptions { IsUpsert = true });
            MongoDatabase.Bookings.UpdateOne(Builders<BsonDocument>.Filter.Eq("_id", id),
                Builders<BsonDocument>.Update.Set("paymentMethod", "CASH").Set("updatedAt", now));
            AdminAccountabilityService.LogActivity(borrowerId, "PAYMENT_METHOD_SELECTED", "PAYMENT", id,
                new BsonDocument { { "method", "CASH" } });
            return true;
        }

        public static bool ConfirmReceipt(string bookingId, ObjectId ownerId)
        {
            EnsureIndexes();
            ObjectId id;
            if (!ObjectId.TryParse(bookingId, out id)) return false;
            var booking = MongoDatabase.Bookings.Find(Builders<BsonDocument>.Filter.Eq("_id", id) & Builders<BsonDocument>.Filter.Eq("ownerId", ownerId)).FirstOrDefault();
            if (booking == null || ClosedBookingStatuses.Contains(Status(booking))) return false;
            var payment = MongoDatabase.Payments.Find(Builders<BsonDocument>.Filter.Eq("bookingId", id) & Builders<BsonDocument>.Filter.In("status", new[] { "UNPAID", "PENDING" })).FirstOrDefault();
            if (payment == null || String.IsNullOrWhiteSpace(payment.GetValue("paymentMethod", "").ToString())) return false;
            var now = DateTime.UtcNow;
            var changed = MongoDatabase.Payments.UpdateOne(Builders<BsonDocument>.Filter.Eq("_id", payment["_id"]) &
                Builders<BsonDocument>.Filter.In("status", new[] { "UNPAID", "PENDING" }),
                Builders<BsonDocument>.Update.Set("status", "PAID").Set("paidAt", now).Set("confirmedBy", ownerId).Set("confirmedAt", now).Set("updatedAt", now)).ModifiedCount == 1;
            if (changed) AdminAccountabilityService.LogActivity(ownerId, "PAYMENT_RECEIPT_CONFIRMED", "PAYMENT", id, new BsonDocument());
            return changed;
        }

        public static bool SetDisputed(string bookingId, ObjectId participantId, string reason)
        {
            EnsureIndexes();
            ObjectId id;
            if (!ObjectId.TryParse(bookingId, out id) || String.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500) return false;
            if (GetBooking(id, participantId) == null) return false;
            var now = DateTime.UtcNow;
            var changed = MongoDatabase.Payments.UpdateOne(Builders<BsonDocument>.Filter.Eq("bookingId", id) & Builders<BsonDocument>.Filter.Eq("status", "PAID"),
                Builders<BsonDocument>.Update.Set("status", "DISPUTED").Set("disputeReason", reason.Trim()).Set("disputedAt", now).Set("updatedAt", now)).ModifiedCount == 1;
            if (changed) AdminAccountabilityService.LogActivity(participantId, "PAYMENT_DISPUTED", "PAYMENT", id, new BsonDocument { { "reason", reason.Trim() } });
            return changed;
        }

        public static bool Refund(string bookingId, ObjectId adminId, string reason)
        {
            EnsureIndexes();
            ObjectId id;
            if (!ObjectId.TryParse(bookingId, out id) || String.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500) return false;
            var now = DateTime.UtcNow;
            var changed = MongoDatabase.Payments.UpdateOne(Builders<BsonDocument>.Filter.Eq("bookingId", id) &
                Builders<BsonDocument>.Filter.In("status", new[] { "PAID", "DISPUTED" }),
                Builders<BsonDocument>.Update.Set("status", "REFUNDED").Set("refundReason", reason.Trim())
                    .Set("refundedAt", now).Set("refundedBy", adminId).Set("updatedAt", now)).ModifiedCount == 1;
            if (changed) AdminAccountabilityService.LogActivity(adminId, "PAYMENT_REFUNDED", "PAYMENT", id, new BsonDocument { { "reason", reason.Trim() } });
            return changed;
        }

        public static bool FinalizeLateFee(string bookingId, ObjectId actorId, bool isAdmin, decimal finalAmount, string notes)
        {
            EnsureIndexes();
            ObjectId id;
            if (!ObjectId.TryParse(bookingId, out id) || finalAmount < 0m || finalAmount > 100000m || (notes ?? "").Length > 1000) return false;
            var booking = MongoDatabase.Bookings.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstOrDefault();
            if (booking == null) return false;
            var ownerValue = booking.GetValue("ownerId", BsonNull.Value);
            if (!isAdmin && (!ownerValue.IsObjectId || ownerValue.AsObjectId != actorId)) return false;
            var now = DateTime.UtcNow;
            var result = MongoDatabase.LateFees.UpdateOne(Builders<BsonDocument>.Filter.Eq("bookingId", id) & Builders<BsonDocument>.Filter.Eq("status", "ESTIMATED"),
                Builders<BsonDocument>.Update.Set("finalAmount", (double)finalAmount).Set("status", "FINALIZED")
                    .Set("finalizedBy", actorId).Set("finalizedAt", now).Set("finalizationNotes", (notes ?? "").Trim()).Set("updatedAt", now));
            if (result.ModifiedCount == 1)
                AdminAccountabilityService.LogActivity(actorId, "LATE_FEE_FINALIZED", "LATE_FEE", id,
                    new BsonDocument { { "amount", (double)finalAmount }, { "actorRole", isAdmin ? "ADMIN" : "OWNER" } });
            return result.ModifiedCount == 1;
        }

        /// <summary>Stores a one-time late-fee assessment after the owner confirms return.</summary>
        public static void AssessLateFee(BsonDocument booking, DateTime returnedAt)
        {
            EnsureIndexes();
            var idValue = booking.GetValue("_id", BsonNull.Value);
            if (!idValue.IsObjectId) return;
            var bookingId = idValue.AsObjectId;
            var scheduledStart = Date(booking, "handoverAt", Date(booking, "startDate", DateTime.UtcNow));
            var durationHours = Math.Max(1, Number(booking, "durationHours", 1));
            var dueAt = scheduledStart.AddHours(durationHours);
            var graceMinutes = Math.Max(0, Math.Min(60, Number(booking, "lateGraceMinutes", 30)));
            var chargeableMinutes = Math.Max(0, (returnedAt - dueAt.AddMinutes(graceMinutes)).TotalMinutes);
            var billableHours = chargeableMinutes <= 0 ? 0 : (int)Math.Ceiling(chargeableMinutes / 60d);
            var itemValue = booking.GetValue("itemId", BsonNull.Value);
            var item = itemValue.IsObjectId ? MongoDatabase.Items.Find(x => x.Id == itemValue.AsObjectId).FirstOrDefault() : null;
            var hourlyRate = item == null || item.RentalDetails == null ? 0d : item.RentalDetails.HourlyRate;
            var fee = Math.Round(hourlyRate * billableHours, 2, MidpointRounding.AwayFromZero);
            var now = DateTime.UtcNow;
            MongoDatabase.LateFees.UpdateOne(Builders<BsonDocument>.Filter.Eq("bookingId", bookingId),
                Builders<BsonDocument>.Update.SetOnInsert("bookingId", bookingId).SetOnInsert("ownerId", booking.GetValue("ownerId", BsonNull.Value))
                    .SetOnInsert("borrowerId", booking.GetValue("borrowerId", BsonNull.Value)).SetOnInsert("dueAt", dueAt)
                    .SetOnInsert("returnedAt", returnedAt).SetOnInsert("graceMinutes", graceMinutes)
                    .SetOnInsert("chargeableMinutes", chargeableMinutes).SetOnInsert("billableHours", billableHours)
                    .SetOnInsert("hourlyRate", hourlyRate).SetOnInsert("amount", fee).SetOnInsert("status", "ESTIMATED")
                    .SetOnInsert("createdAt", now).Set("updatedAt", now), new UpdateOptions { IsUpsert = true });
        }

        private static BsonDocument GetBooking(ObjectId id, ObjectId participantId)
        {
            return MongoDatabase.Bookings.Find(Builders<BsonDocument>.Filter.Eq("_id", id) &
                (Builders<BsonDocument>.Filter.Eq("ownerId", participantId) | Builders<BsonDocument>.Filter.Eq("borrowerId", participantId))).FirstOrDefault();
        }
        private static string Status(BsonDocument doc) { return doc.GetValue("status", "").ToString().Trim().ToUpperInvariant(); }
        private static double Number(BsonDocument doc, string field, double fallback) { var value = doc.GetValue(field, BsonNull.Value); return value.IsNumeric ? value.ToDouble() : fallback; }
        private static int Number(BsonDocument doc, string field, int fallback) { var value = doc.GetValue(field, BsonNull.Value); return value.IsNumeric ? value.ToInt32() : fallback; }
        private static DateTime Date(BsonDocument doc, string field, DateTime fallback) { var value = doc.GetValue(field, BsonNull.Value); return value.IsValidDateTime ? value.ToUniversalTime() : fallback; }
        private static void EnsureIndexes() { var initialized = IndexesReady.Value; }
    }
}
