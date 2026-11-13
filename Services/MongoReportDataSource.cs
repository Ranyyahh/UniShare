using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using UniShare.Controllers;
using UniShare.Models;

namespace UniShare.Services
{
    /// <summary>Reads personalized report rows from the authoritative bookings collection.</summary>
    public sealed class MongoReportDataSource : IReportDataSource
    {
        public bool IsAvailable { get { return true; } }
        public bool IsSampleData { get { return false; } }
        public string UnavailableReason { get { return null; } }

        public IList<RentalTransactionRecord> GetOwnerRentals(string ownerKey, DateTime fromInclusive, DateTime toExclusive)
        {
            ObjectId ownerId;
            if (!ObjectId.TryParse(ownerKey, out ownerId)) return new List<RentalTransactionRecord>();
            var ownedItemIds = new HashSet<string>(MongoDatabase.Items.Find(x => x.OwnerId == ownerId).ToList().Select(x => x.Id.ToString()), StringComparer.OrdinalIgnoreCase);
            return MongoDatabase.Bookings.Find(Builders<BsonDocument>.Filter.Eq("ownerId", ownerId)).ToList()
                .Select(ToRental)
                .Where(x => ownedItemIds.Contains(x.ItemId))
                .Where(x => InRange(x.RequestedAt, fromInclusive, toExclusive) ||
                            (x.CompletedAt.HasValue && InRange(x.CompletedAt.Value, fromInclusive, toExclusive)))
                .GroupBy(x => x.TransactionId, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
        }

        public IList<BorrowingTransactionRecord> GetBorrowings(string borrowerKey, DateTime fromInclusive, DateTime toExclusive)
        {
            ObjectId borrowerId;
            if (!ObjectId.TryParse(borrowerKey, out borrowerId)) return new List<BorrowingTransactionRecord>();
            var borrowerFilter = Builders<BsonDocument>.Filter.Eq("borrowerId", borrowerId) |
                                 Builders<BsonDocument>.Filter.Eq("renterId", borrowerId) |
                                 Builders<BsonDocument>.Filter.Eq("borrowerId", borrowerId.ToString()) |
                                 Builders<BsonDocument>.Filter.Eq("renterId", borrowerId.ToString());
            var documents = MongoDatabase.Bookings.Find(borrowerFilter).ToList();
            var bookingIds = documents.Select(b => b.GetValue("_id", BsonNull.Value)).Where(v => v.IsObjectId).Select(v => v.AsObjectId).ToList();
            var payments = bookingIds.Count == 0 ? new List<BsonDocument>() : MongoDatabase.Payments.Find(
                Builders<BsonDocument>.Filter.In("bookingId", bookingIds) &
                Builders<BsonDocument>.Filter.In("status", new[] { "PAID", "DISPUTED" })).ToList();
            var paymentsByBooking = payments.Where(p => p.GetValue("bookingId", BsonNull.Value).IsObjectId)
                .GroupBy(p => p["bookingId"].AsObjectId.ToString(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(p => Date(p, "paidAt")).First(), StringComparer.OrdinalIgnoreCase);
            var itemIds = documents.Select(b => b.GetValue("itemId", BsonNull.Value))
                .Where(v => v.IsObjectId).Select(v => v.AsObjectId).Distinct().ToList();
            var itemOwners = itemIds.Count == 0 ? new Dictionary<string, ObjectId>() :
                MongoDatabase.Items.Find(Builders<UniShare.Models.ItemDocument>.Filter.In(x => x.Id, itemIds)).ToList()
                    .ToDictionary(x => x.Id.ToString(), x => x.OwnerId, StringComparer.OrdinalIgnoreCase);
            return documents.Select(b =>
                {
                    BsonDocument payment;
                    return ToBorrowing(b, paymentsByBooking.TryGetValue(b.GetValue("_id", BsonNull.Value).ToString(), out payment) ? payment : null);
                })
                .Where(x => itemOwners.ContainsKey(x.ItemId) && itemOwners[x.ItemId] != borrowerId)
                .Where(x => InRange(x.RequestedAt, fromInclusive, toExclusive) ||
                            (x.PaidAt.HasValue && InRange(x.PaidAt.Value, fromInclusive, toExclusive)))
                .GroupBy(x => x.TransactionId, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
        }

        private static RentalTransactionRecord ToRental(BsonDocument booking)
        {
            var ownerId = booking.GetValue("ownerId", BsonNull.Value);
            var completedAt = Date(booking, "completedAt") ?? Date(booking, "returnedAt");
            return new RentalTransactionRecord
            {
                TransactionId = Id(booking),
                ItemId = booking.GetValue("itemId", "").ToString(),
                ItemName = booking.GetValue("itemName", "Item").ToString(),
                OwnerKey = ownerId.IsObjectId ? ownerId.AsObjectId.ToString() : ownerId.ToString(),
                Quantity = 1,
                Amount = Amount(booking),
                Status = DisplayStatus(booking.GetValue("status", "").ToString()),
                RequestedAt = Date(booking, "createdAt") ?? Date(booking, "requestedAt") ?? Date(booking, "startDate") ?? DateTime.MinValue,
                CompletedAt = completedAt
            };
        }

        private static BorrowingTransactionRecord ToBorrowing(BsonDocument booking, BsonDocument payment)
        {
            var borrowerId = booking.GetValue("borrowerId", booking.GetValue("renterId", BsonNull.Value));
            var paidAt = payment == null ? (DateTime?)null : Date(payment, "paidAt");
            var paidAmount = payment == null ? double.NaN : Number(payment, "amount", double.NaN);
            return new BorrowingTransactionRecord
            {
                TransactionId = Id(booking),
                ItemId = booking.GetValue("itemId", "").ToString(),
                ItemName = booking.GetValue("itemName", "Item").ToString(),
                BorrowerKey = borrowerId.IsObjectId ? borrowerId.AsObjectId.ToString() : borrowerId.ToString(),
                Quantity = 1,
                Status = DisplayStatus(booking.GetValue("status", "").ToString()),
                RequestedAt = Date(booking, "createdAt") ?? Date(booking, "requestedAt") ?? Date(booking, "startDate") ?? DateTime.MinValue,
                EstimatedAmount = Amount(booking),
                AmountPaid = paidAt.HasValue && !double.IsNaN(paidAmount) ? (decimal?)Convert.ToDecimal(paidAmount) : null,
                PaidAt = paidAt
            };
        }

        private static string Id(BsonDocument doc)
        {
            return doc.GetValue("requestId", doc.GetValue("_id", BsonNull.Value)).ToString();
        }

        private static decimal Amount(BsonDocument doc)
        {
            return Convert.ToDecimal(Number(doc, "estimatedFee", Number(doc, "rentalFee", Number(doc, "totalFee", 0))));
        }

        private static double Number(BsonDocument doc, string field, double fallback)
        {
            var value = doc.GetValue(field, BsonNull.Value);
            return value.IsNumeric ? value.ToDouble() : fallback;
        }

        private static DateTime? Date(BsonDocument doc, string field)
        {
            var value = doc.GetValue(field, BsonNull.Value);
            return value.IsValidDateTime ? (DateTime?)value.ToUniversalTime().ToLocalTime() : null;
        }

        private static bool InRange(DateTime value, DateTime from, DateTime toExclusive)
        {
            return value >= from && value < toExclusive;
        }

        private static string DisplayStatus(string status)
        {
            var normalized = (status ?? "").Trim().ToUpperInvariant();
            if (normalized == "PENDING" || normalized == "REQUESTED") return "Pending";
            if (normalized == "ACCEPTED" || normalized == "APPROVED") return "Accepted";
            if (normalized == "ACTIVE" || normalized == "ONGOING" || normalized == "OVERDUE" || normalized == "RETURN_REQUESTED" || normalized == "RETURN PENDING") return "Active";
            if (normalized == "COMPLETED" || normalized == "COMPLETE" || normalized == "RETURNED") return "Completed";
            if (normalized == "DECLINED" || normalized == "REJECTED") return "Rejected";
            if (normalized == "CANCELLED" || normalized == "CANCELED" || normalized == "EXPIRED") return "Cancelled";
            if (normalized == "DISPUTED") return "Disputed";
            return string.IsNullOrEmpty(normalized) ? "Unknown" : normalized;
        }
    }
}
