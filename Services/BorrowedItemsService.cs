using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using UniShare.Models;

namespace UniShare.Services
{
    /// <summary>Database-backed borrower workflows. Booking and rental documents remain in their existing collections.</summary>
    public static class BorrowedItemsService
    {
        private static readonly string[] DisplayableStatuses = { "ACCEPTED", "APPROVED", "ACTIVE", "ONGOING", "OVERDUE", "RETURN_REQUESTED", "RETURN PENDING", "COMPLETED", "COMPLETE", "RETURNED", "DISPUTED", "CANCELLED", "CANCELED", "DECLINED", "REJECTED", "EXPIRED" };
        private static readonly string[] AwaitingHandover = { "ACCEPTED", "APPROVED" };

        public static List<BorrowedItem> GetForBorrower(ObjectId borrowerId)
        {
            BookingService.ExpirePending();
            RefreshOverdue(borrowerId);
            var borrowerFilter = Builders<BsonDocument>.Filter.Eq("borrowerId", borrowerId) |
                                 Builders<BsonDocument>.Filter.Eq("renterId", borrowerId) |
                                 Builders<BsonDocument>.Filter.Eq("borrowerId", borrowerId.ToString()) |
                                 Builders<BsonDocument>.Filter.Eq("renterId", borrowerId.ToString());
            var bookings = MongoDatabase.Bookings.Find(borrowerFilter).ToList()
                .Where(IsDisplayable).ToList();
            var bookingIds = new HashSet<string>(bookings.Select(x => x.GetValue("_id", BsonNull.Value).ToString()), StringComparer.OrdinalIgnoreCase);
            var rentals = MongoDatabase.Rentals.Find(borrowerFilter).ToList()
                .Where(IsDisplayable)
                .Where(x => !bookingIds.Contains(x.GetValue("bookingId", BsonNull.Value).ToString()))
                .ToList();
            return bookings.Select(x => Map(x, borrowerId)).Concat(rentals.Select(x => Map(x, borrowerId)))
                .OrderByDescending(x => x.RequestedOn ?? x.Start).ToList();
        }

        public static BorrowedItem GetDetails(ObjectId borrowerId, string recordId)
        {
            RefreshOverdue(borrowerId);
            ObjectId id;
            if (!ObjectId.TryParse(recordId, out id)) return null;
            var booking = FindOwned(MongoDatabase.Bookings, id, borrowerId);
            if (booking != null) return Map(booking, borrowerId);
            var rental = FindOwned(MongoDatabase.Rentals, id, borrowerId);
            return rental == null ? null : Map(rental, borrowerId);
        }

        public static bool ConfirmHandover(ObjectId borrowerId, string recordId)
        {
            BsonDocument doc; bool isBooking;
            if (!TryFind(borrowerId, recordId, out doc, out isBooking)) return false;
            var status = Status(doc);
            var canConfirmLegacyPending = status == "PENDING" && (doc.Contains("approvedAt") || doc.Contains("acceptedAt"));
            if (!AwaitingHandover.Contains(status) && !canConfirmLegacyPending) return false;
            var borrowerAlreadyConfirmed = doc.GetValue("borrowerHandoverConfirmed", doc.GetValue("handoverConfirmed", false)).ToBoolean();
            if (borrowerAlreadyConfirmed) return false;
            var now = DateTime.UtcNow;
            var ownerConfirmed = doc.GetValue("ownerHandoverConfirmed", false).ToBoolean();
            var update = Builders<BsonDocument>.Update.Set("borrowerHandoverConfirmed", true).Set("borrowerHandoverConfirmedAt", now).Set("updatedAt", now);
            if (ownerConfirmed)
                update = update.Set("status", "ONGOING").Set("handoverConfirmed", true).Set("handoverConfirmedAt", now).Set("handoverAt", now);
            UpdateRecord(doc, isBooking, borrowerId, update);
            UpdateLinkedRental(doc, borrowerId, update);
            return true;
        }

        public static bool ConfirmHandoverByOwner(ObjectId ownerId, string recordId)
        {
            ObjectId id;
            if (!ObjectId.TryParse(recordId, out id)) return false;
            var ownerFilter = Builders<BsonDocument>.Filter.Eq("ownerId", ownerId);
            var allowed = Builders<BsonDocument>.Filter.In("status", new[] { "ACCEPTED", "APPROVED", "ONGOING", "ACTIVE" });
            var doc = MongoDatabase.Bookings.Find(Builders<BsonDocument>.Filter.Eq("_id", id) & ownerFilter & allowed).FirstOrDefault();
            var isBooking = doc != null;
            if (doc == null) doc = MongoDatabase.Rentals.Find(Builders<BsonDocument>.Filter.Eq("_id", id) & ownerFilter & allowed).FirstOrDefault();
            if (doc == null || doc.GetValue("ownerHandoverConfirmed", false).ToBoolean()) return false;

            var now = DateTime.UtcNow;
            var borrowerConfirmed = doc.GetValue("borrowerHandoverConfirmed", doc.GetValue("handoverConfirmed", false)).ToBoolean();
            var update = Builders<BsonDocument>.Update.Set("ownerHandoverConfirmed", true).Set("ownerHandoverConfirmedAt", now).Set("updatedAt", now);
            if (borrowerConfirmed)
                update = update.Set("status", "ONGOING").Set("handoverConfirmed", true).Set("handoverConfirmedAt", now).Set("handoverAt", now);
            var collection = isBooking ? MongoDatabase.Bookings : MongoDatabase.Rentals;
            var ownerNotConfirmed = Builders<BsonDocument>.Filter.Eq("ownerHandoverConfirmed", false) | Builders<BsonDocument>.Filter.Exists("ownerHandoverConfirmed", false);
            var result = collection.UpdateOne(Builders<BsonDocument>.Filter.Eq("_id", id) & ownerFilter & allowed & ownerNotConfirmed, update);
            if (result.ModifiedCount != 1) return false;
            UpdateLinkedRental(doc, ownerId, update);
            return true;
        }

        public static bool RequestReturn(ObjectId borrowerId, string recordId)
        {
            BsonDocument doc; bool isBooking;
            if (!TryFind(borrowerId, recordId, out doc, out isBooking) || (Status(doc) != "ONGOING" && Status(doc) != "OVERDUE") || !doc.GetValue("handoverConfirmed", false).ToBoolean()) return false;
            var now = DateTime.UtcNow;
            UpdateRecord(doc, isBooking, borrowerId, Builders<BsonDocument>.Update.Set("status", "RETURN_REQUESTED").Set("returnRequestedAt", now).Set("updatedAt", now));
            UpdateLinkedRental(doc, borrowerId, Builders<BsonDocument>.Update.Set("status", "RETURN_REQUESTED").Set("returnRequestedAt", now).Set("updatedAt", now));
            return true;
        }

        public static bool ConfirmReturnByOwner(ObjectId ownerId, string recordId)
        {
            ObjectId id;
            if (!ObjectId.TryParse(recordId, out id)) return false;
            var doc = MongoDatabase.Bookings.Find(Builders<BsonDocument>.Filter.Eq("_id", id) & Builders<BsonDocument>.Filter.Eq("ownerId", ownerId) & Builders<BsonDocument>.Filter.In("status", new[] { "RETURN_REQUESTED", "RETURN PENDING" })).FirstOrDefault();
            var isBooking = doc != null;
            if (doc == null) doc = MongoDatabase.Rentals.Find(Builders<BsonDocument>.Filter.Eq("_id", id) & Builders<BsonDocument>.Filter.Eq("ownerId", ownerId) & Builders<BsonDocument>.Filter.In("status", new[] { "RETURN_REQUESTED", "RETURN PENDING" })).FirstOrDefault();
            if (doc == null) return false;
            var now = DateTime.UtcNow;
            var update = Builders<BsonDocument>.Update.Set("status", "COMPLETED").Set("returnedAt", now).Set("completedAt", now).Set("updatedAt", now);
            if (isBooking) MongoDatabase.Bookings.UpdateOne(Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]) & Builders<BsonDocument>.Filter.Eq("ownerId", ownerId) & Builders<BsonDocument>.Filter.Eq("status", doc["status"]), update);
            else MongoDatabase.Rentals.UpdateOne(Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]) & Builders<BsonDocument>.Filter.Eq("ownerId", ownerId) & Builders<BsonDocument>.Filter.Eq("status", doc["status"]), update);
            UpdateLinkedRental(doc, ownerId, update);
            return true;
        }

        public static bool SubmitDispute(ObjectId borrowerId, string recordId, string reason, string details)
        {
            BsonDocument doc; bool isBooking;
            if (!TryFind(borrowerId, recordId, out doc, out isBooking)) return false;
            if (Status(doc) != "ONGOING" && Status(doc) != "OVERDUE" && Status(doc) != "ACCEPTED" && Status(doc) != "APPROVED") return false;
            if (string.IsNullOrWhiteSpace(reason)) return false;
            var now = DateTime.UtcNow;
            var text = string.IsNullOrWhiteSpace(details) ? reason.Trim() : reason.Trim() + ": " + details.Trim();
            var update = Builders<BsonDocument>.Update.Set("status", "DISPUTED").Set("disputeReason", text)
                .Set("disputeCategory", reason.Trim()).Set("disputeDetails", (details ?? "").Trim()).Set("disputedAt", now).Set("updatedAt", now);
            UpdateRecord(doc, isBooking, borrowerId, update);
            UpdateLinkedRental(doc, borrowerId, update);
            return true;
        }

        public static bool SubmitRating(ObjectId borrowerId, string recordId, int itemRating, int ownerRating, string review)
        {
            BsonDocument doc; bool isBooking;
            if (!TryFind(borrowerId, recordId, out doc, out isBooking) || (Status(doc) != "COMPLETED" && Status(doc) != "COMPLETE" && Status(doc) != "RETURNED")) return false;
            if (itemRating < 1 || itemRating > 5 || ownerRating < 1 || ownerRating > 5 || doc.Contains("itemRating") || doc.Contains("ownerRating")) return false;
            var itemId = doc.GetValue("itemId", BsonNull.Value);
            var ownerId = doc.GetValue("ownerId", BsonNull.Value);
            if (!itemId.IsObjectId || !ownerId.IsObjectId) return false;
            var bookingId = doc.GetValue("_id", BsonNull.Value);
            if (bookingId.IsObjectId && MongoDatabase.Ratings.Find(Builders<BsonDocument>.Filter.Eq("bookingId", bookingId) & Builders<BsonDocument>.Filter.Eq("reviewerId", borrowerId)).Limit(1).Any()) return false;
            var now = DateTime.UtcNow;
            var update = Builders<BsonDocument>.Update.Set("itemRating", itemRating).Set("ownerRating", ownerRating).Set("review", (review ?? "").Trim()).Set("ratedAt", now).Set("updatedAt", now);
            var collection = isBooking ? MongoDatabase.Bookings : MongoDatabase.Rentals;
            var borrowerFilter = Builders<BsonDocument>.Filter.Eq("borrowerId", borrowerId) | Builders<BsonDocument>.Filter.Eq("renterId", borrowerId) |
                                 Builders<BsonDocument>.Filter.Eq("borrowerId", borrowerId.ToString()) | Builders<BsonDocument>.Filter.Eq("renterId", borrowerId.ToString());
            var statusFilter = Builders<BsonDocument>.Filter.In("status", new[] { "COMPLETED", "COMPLETE", "RETURNED" });
            var unratedFilter = Builders<BsonDocument>.Filter.Exists("itemRating", false) & Builders<BsonDocument>.Filter.Exists("ownerRating", false);
            if (collection.UpdateOne(Builders<BsonDocument>.Filter.Eq("_id", bookingId) & borrowerFilter & statusFilter & unratedFilter, update).ModifiedCount != 1) return false;
            var rating = new BsonDocument { { "_id", ObjectId.GenerateNewId() }, { "bookingId", bookingId },
                { "itemId", itemId }, { "reviewerId", borrowerId }, { "reviewedUserId", ownerId }, { "itemRating", itemRating },
                { "ownerRating", ownerRating }, { "rating", ownerRating }, { "review", (review ?? "").Trim() }, { "createdAt", now } };
            MongoDatabase.Ratings.InsertOne(rating);
            UpdateLinkedRental(doc, borrowerId, update);
            return true;
        }

        private static bool IsDisplayable(BsonDocument x)
        {
            var status = Status(x);
            return DisplayableStatuses.Contains(status) || status == "PENDING" || status == "REQUESTED";
        }
        private static void RefreshOverdue(ObjectId borrowerId)
        {
            var users = Builders<BsonDocument>.Filter.Eq("borrowerId", borrowerId) | Builders<BsonDocument>.Filter.Eq("renterId", borrowerId) |
                        Builders<BsonDocument>.Filter.Eq("borrowerId", borrowerId.ToString()) | Builders<BsonDocument>.Filter.Eq("renterId", borrowerId.ToString());
            var active = users & Builders<BsonDocument>.Filter.In("status", new[] { "ONGOING", "ACTIVE" });
            var now = DateTime.UtcNow;
            foreach (var collection in new[] { MongoDatabase.Bookings, MongoDatabase.Rentals })
            {
                foreach (var doc in collection.Find(active).ToList())
                {
                    var start = ReadDate(doc, "handoverAt") ?? ReadDate(doc, "handoverConfirmedAt") ?? ReadDate(doc, "startDate") ?? ReadDate(doc, "handoverDate");
                    if (!start.HasValue || start.Value.AddHours(ReadHours(doc)) > now) continue;
                    collection.UpdateOne(Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]) & Builders<BsonDocument>.Filter.Eq("status", doc.GetValue("status", "")),
                        Builders<BsonDocument>.Update.Set("status", "OVERDUE").Set("overdueAt", now).Set("updatedAt", now));
                }
            }
        }
        private static string Status(BsonDocument x) { return x.GetValue("status", "").ToString().Trim().ToUpperInvariant(); }
        private static BsonDocument FindOwned(IMongoCollection<BsonDocument> collection, ObjectId id, ObjectId borrowerId)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("_id", id) &
                (Builders<BsonDocument>.Filter.Eq("borrowerId", borrowerId) | Builders<BsonDocument>.Filter.Eq("renterId", borrowerId) |
                 Builders<BsonDocument>.Filter.Eq("borrowerId", borrowerId.ToString()) | Builders<BsonDocument>.Filter.Eq("renterId", borrowerId.ToString()));
            return collection.Find(filter).FirstOrDefault();
        }
        private static bool TryFind(ObjectId borrowerId, string recordId, out BsonDocument doc, out bool isBooking)
        {
            ObjectId id;
            doc = null; isBooking = false;
            if (!ObjectId.TryParse(recordId, out id)) return false;
            doc = FindOwned(MongoDatabase.Bookings, id, borrowerId);
            if (doc != null) { isBooking = true; return true; }
            doc = FindOwned(MongoDatabase.Rentals, id, borrowerId);
            return doc != null;
        }
        private static void UpdateRecord(BsonDocument doc, bool isBooking, ObjectId borrowerId, UpdateDefinition<BsonDocument> update)
        {
            var collection = isBooking ? MongoDatabase.Bookings : MongoDatabase.Rentals;
            var filter = Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]) &
                (Builders<BsonDocument>.Filter.Eq("borrowerId", borrowerId) | Builders<BsonDocument>.Filter.Eq("renterId", borrowerId) |
                 Builders<BsonDocument>.Filter.Eq("borrowerId", borrowerId.ToString()) | Builders<BsonDocument>.Filter.Eq("renterId", borrowerId.ToString())) &
                Builders<BsonDocument>.Filter.Eq("status", doc.GetValue("status", ""));
            if (collection.UpdateOne(filter, update).ModifiedCount != 1) throw new InvalidOperationException("The borrowing record changed. Refresh and try again.");
        }
        private static void UpdateLinkedRental(BsonDocument doc, ObjectId actingUserId, UpdateDefinition<BsonDocument> update)
        {
            var id = doc.GetValue("_id", BsonNull.Value);
            var bookingId = doc.GetValue("bookingId", id);
            if (!bookingId.IsObjectId) return;
            var filter = Builders<BsonDocument>.Filter.Eq("bookingId", bookingId) &
                (Builders<BsonDocument>.Filter.Eq("borrowerId", actingUserId) | Builders<BsonDocument>.Filter.Eq("ownerId", actingUserId));
            MongoDatabase.Rentals.UpdateOne(filter, update);
        }

        private static BorrowedItem Map(BsonDocument doc, ObjectId borrowerId)
        {
            var idValue = doc.GetValue("_id", BsonNull.Value);
            var itemIdValue = doc.GetValue("itemId", BsonNull.Value);
            ObjectId parsedItemId;
            var item = itemIdValue.IsObjectId ? MongoDatabase.Items.Find(x => x.Id == itemIdValue.AsObjectId).FirstOrDefault() :
                ObjectId.TryParse(itemIdValue.ToString(), out parsedItemId) ? MongoDatabase.Items.Find(x => x.Id == parsedItemId).FirstOrDefault() : null;
            var ownerValue = doc.GetValue("ownerId", item == null ? BsonNull.Value : (BsonValue)item.OwnerId);
            ObjectId parsedOwnerId = ObjectId.Empty;
            var owner = ownerValue.IsObjectId ? MongoDatabase.Users.Find(x => x.Id == ownerValue.AsObjectId).FirstOrDefault() :
                ObjectId.TryParse(ownerValue.ToString(), out parsedOwnerId) ? MongoDatabase.Users.Find(x => x.Id == parsedOwnerId).FirstOrDefault() : null;
            var ownerName = owner == null ? doc.GetValue("ownerName", "Lender").ToString() : (owner.FirstName + " " + owner.LastName).Trim();
            var ratingOwnerId = ownerValue.IsObjectId ? ownerValue.AsObjectId : parsedOwnerId;
            var ownerRating = !ratingOwnerId.Equals(ObjectId.Empty) ? MarketplaceService.GetOwnerRatingSummary(ratingOwnerId).Item1 : null;
            var start = ReadDate(doc, "startDate") ?? ReadDate(doc, "handoverDate") ?? ReadDate(doc, "handoverAt") ?? DateTime.Now;
            var end = ReadDate(doc, "endDate") ?? ReadDate(doc, "expectedReturnDate") ?? start.AddHours(ReadHours(doc));
            var actualHandover = ReadDate(doc, "handoverAt") ?? ReadDate(doc, "handoverConfirmedAt");
            var status = Status(doc);
            var borrowerHandoverConfirmed = doc.GetValue("borrowerHandoverConfirmed", doc.GetValue("handoverConfirmed", false)).ToBoolean();
            var ownerHandoverConfirmed = doc.GetValue("ownerHandoverConfirmed", false).ToBoolean();
            var handoverConfirmed = (borrowerHandoverConfirmed && ownerHandoverConfirmed) ||
                (!doc.Contains("borrowerHandoverConfirmed") && !doc.Contains("ownerHandoverConfirmed") &&
                 (doc.GetValue("handoverConfirmed", false).ToBoolean() || new[] { "ONGOING", "ACTIVE", "OVERDUE", "RETURN_REQUESTED", "RETURN PENDING", "COMPLETED", "COMPLETE", "RETURNED", "DISPUTED" }.Contains(status)));
            var legacyAcceptedPending = status == "PENDING" && (doc.Contains("approvedAt") || doc.Contains("acceptedAt"));
            var displayStatus = status == "PENDING" || status == "REQUESTED" || status == "ACCEPTED" || status == "APPROVED" ?
                    (handoverConfirmed ? "Ongoing" : "Pending") :
                status == "ACTIVE" || status == "ONGOING" || status == "OVERDUE" ? "Ongoing" :
                status == "COMPLETE" || status == "COMPLETED" || status == "RETURNED" ? "Completed" :
                status == "RETURN_REQUESTED" || status == "RETURN PENDING" ? "Return Requested" :
                status == "CANCELLED" || status == "CANCELED" || status == "EXPIRED" ? "Cancelled" :
                status == "DECLINED" ? "Declined" : status == "REJECTED" ? "Rejected" : "Disputed";
            var details = item == null ? null : item.RentalDetails;
            var hours = Math.Max(1, (int)Math.Round((end - start).TotalHours));
            var disputeAt = ReadDate(doc, "disputedAt");
            var returnedAt = ReadDate(doc, "returnedAt") ?? ReadDate(doc, "actualReturnAt");
            var timerStart = actualHandover ?? start;
            var dueAt = timerStart.AddHours(hours);
            var timerSnapshot = status == "DISPUTED" ? disputeAt : status == "COMPLETED" || status == "COMPLETE" || status == "RETURNED" ? returnedAt : null;
            var lateMilliseconds = timerSnapshot.HasValue && timerSnapshot.Value > dueAt ? (double?)((timerSnapshot.Value - dueAt).TotalMilliseconds) : null;
            var pausedMilliseconds = status == "DISPUTED" && disputeAt.HasValue ? (double?)Math.Max(0, (dueAt - disputeAt.Value).TotalMilliseconds) : null;
            var price = ReadNumber(doc, "pricePerHour", details != null && details.HourlyRate > 0 ? details.HourlyRate : 0d);
            var fee = ReadNumber(doc, "estimatedFee", ReadNumber(doc, "rentalFee", ReadNumber(doc, "totalFee", price * hours)));
            if (price <= 0 && hours > 0) price = fee / hours;
            var borrowerName = doc.GetValue("borrowerName", "").ToString();
            var borrowerIdValue = doc.GetValue("borrowerId", doc.GetValue("renterId", BsonNull.Value));
            ObjectId parsedBorrowerId;
            if (!borrowerIdValue.IsObjectId) ObjectId.TryParse(borrowerIdValue.ToString(), out parsedBorrowerId);
            else parsedBorrowerId = borrowerIdValue.AsObjectId;
            if (string.IsNullOrWhiteSpace(borrowerName) && !parsedBorrowerId.Equals(ObjectId.Empty))
            {
                var borrower = MongoDatabase.Users.Find(x => x.Id == parsedBorrowerId).FirstOrDefault();
                borrowerName = borrower == null ? "Student" : (borrower.FirstName + " " + borrower.LastName).Trim();
            }
            var image = item != null && item.Photos != null && item.Photos.Count > 0 ? MarketplaceService.ToImageUrl(item.Photos[0]) : null;
            var location = item == null ? "" : MarketplaceService.FormatLocation(item.Location);
            var itemName = item == null ? doc.GetValue("itemName", "Item").ToString() : item.Name;
            var period = start.ToLocalTime().ToString("MMM d, h:mm tt", CultureInfo.InvariantCulture) + " – " + end.ToLocalTime().ToString("h:mm tt", CultureInfo.InvariantCulture);
            var tx = doc.GetValue("transactionId", doc.GetValue("requestId", "#UNI-" + idValue.ToString().Substring(Math.Max(0, idValue.ToString().Length - 6)).ToUpperInvariant())).ToString();
            var theme = status == "DISPUTED" ? "mint" : status == "COMPLETED" || status == "COMPLETE" ? "peach" : "indigo";
            var record = new BorrowedItem
            {
                RecordId = idValue.ToString(), OwnerId = !ratingOwnerId.Equals(ObjectId.Empty) ? ratingOwnerId.ToString() : "", ItemId = itemIdValue.IsObjectId ? itemIdValue.AsObjectId.ToString() : itemIdValue.ToString(), ItemName = itemName, Category = item == null ? doc.GetValue("category", "Other Items").ToString() : item.Category,
                Lender = ownerName, LenderInitials = Initials(ownerName), LenderProgram = owner == null ? doc.GetValue("ownerProgram", "").ToString() : ((owner.Program ?? "") + (string.IsNullOrWhiteSpace(owner.YearLevel) ? "" : " " + owner.YearLevel)),
                PricePerHour = (decimal)price, PaidAmount = (decimal)fee, Start = start.ToLocalTime(), End = end.ToLocalTime(), PickupPoint = doc.GetValue("handoverLocation", location).ToString(),
                PaymentMethod = doc.GetValue("paymentMethod", "—").ToString(), TransactionId = tx, About = item == null ? doc.GetValue("description", "").ToString() : item.Description,
                Status = displayStatus, HandoverConfirmed = handoverConfirmed,
                BorrowerHandoverConfirmed = borrowerHandoverConfirmed, OwnerHandoverConfirmed = ownerHandoverConfirmed,
                CanConfirmHandover = (AwaitingHandover.Contains(status) || legacyAcceptedPending) && !borrowerHandoverConfirmed && !handoverConfirmed,
                Rating = ReadNullableInt(doc, "itemRating"), ItemRating = ReadNullableDouble(doc, "itemRating"), OwnerRating = ownerRating,
                Review = doc.GetValue("review", "").ToString(), DisputeReason = doc.GetValue("disputeReason", "").ToString(), Theme = theme,
                RequestedOn = ReadDate(doc, "createdAt") ?? ReadDate(doc, "requestedAt"), ApprovedOn = ReadDate(doc, "acceptedAt") ?? ReadDate(doc, "approvedAt"),
                HandoverOn = actualHandover, HandoverAt = actualHandover,
                CompletedOn = ReadDate(doc, "completedAt") ?? ReadDate(doc, "returnedAt") ?? ReadDate(doc, "actualReturnAt"), ReturnedOn = (ReadDate(doc, "returnedAt") ?? ReadDate(doc, "actualReturnAt"))?.ToLocalTime().ToString("MMM d, h:mm tt"),
                ReturnRequested = status == "RETURN_REQUESTED" || status == "RETURN PENDING", ReturnRequestedOn = ReadDate(doc, "returnRequestedAt")?.ToLocalTime().ToString("MMM d, h:mm tt"),
                ImageUrl = image, LateMilliseconds = lateMilliseconds, PausedMilliseconds = pausedMilliseconds, DisputedAt = disputeAt
            };
            return record;
        }

        private static DateTime? ReadDate(BsonDocument doc, string name)
        {
            var v = doc.GetValue(name, BsonNull.Value);
            return v.IsValidDateTime ? (DateTime?)v.ToUniversalTime() : null;
        }
        private static double ReadNumber(BsonDocument doc, string name, double fallback) { var v = doc.GetValue(name, BsonNull.Value); return v.IsNumeric ? v.ToDouble() : fallback; }
        private static int ReadHours(BsonDocument doc)
        {
            var n = ReadNumber(doc, "durationHours", ReadNumber(doc, "hours", 0));
            if (n > 0) return (int)Math.Ceiling(n);
            var text = doc.GetValue("duration", "").ToString();
            int parsed; return int.TryParse(new string(text.TakeWhile(char.IsDigit).ToArray()), out parsed) && parsed > 0 ? parsed : 2;
        }
        private static int? ReadNullableInt(BsonDocument doc, string name) { var v = doc.GetValue(name, BsonNull.Value); return v.IsNumeric ? (int?)v.ToInt32() : null; }
        private static double? ReadNullableDouble(BsonDocument doc, string name) { var v = doc.GetValue(name, BsonNull.Value); return v.IsNumeric ? (double?)v.ToDouble() : null; }
        private static string Initials(string name) { return string.Concat((name ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Take(2).Select(x => char.ToUpperInvariant(x[0]))); }
    }
}
