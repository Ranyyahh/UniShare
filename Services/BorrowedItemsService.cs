using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;
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
        private static readonly Lazy<bool> LifecycleIndexesReady = new Lazy<bool>(() =>
        {
            MongoDatabase.Handovers.Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("bookingId"), new CreateIndexOptions { Unique = true, Name = "bookingId_unique" }));
            MongoDatabase.Returns.Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("bookingId"), new CreateIndexOptions { Unique = true, Name = "bookingId_unique" }));
            MongoDatabase.ConditionRecords.Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("bookingId").Ascending("phase").Ascending("createdAt")));
            return true;
        });

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

        public static List<BorrowedItem> GetForOwner(ObjectId ownerId)
        {
            BookingService.ExpirePending();
            RefreshOverdue(ownerId);
            var filter = Builders<BsonDocument>.Filter.Eq("ownerId", ownerId) |
                         Builders<BsonDocument>.Filter.Eq("ownerId", ownerId.ToString());
            var bookings = MongoDatabase.Bookings.Find(filter).ToList().Where(IsDisplayable).ToList();
            var bookingIds = new HashSet<string>(bookings.Select(x => x.GetValue("_id", BsonNull.Value).ToString()), StringComparer.OrdinalIgnoreCase);
            var rentals = MongoDatabase.Rentals.Find(filter).ToList().Where(IsDisplayable)
                .Where(x => !bookingIds.Contains(x.GetValue("bookingId", BsonNull.Value).ToString())).ToList();
            return bookings.Select(x => Map(x, ownerId, true)).Concat(rentals.Select(x => Map(x, ownerId, true)))
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

        public static ReturnInspectionViewModel GetReturnInspection(
        ObjectId ownerId,
        string recordId)
        {
            ObjectId bookingId;

            if (!ObjectId.TryParse(recordId, out bookingId))
                return null;

            // Verify that the current user owns this booking.
            var booking = MongoDatabase.Bookings.Find(
                Builders<BsonDocument>.Filter.Eq("_id", bookingId) &
                Builders<BsonDocument>.Filter.Eq("ownerId", ownerId)
            ).FirstOrDefault();

            if (booking == null)
                return null;

            // Retrieve the return record.
            var returnDoc = MongoDatabase.Returns.Find(
                Builders<BsonDocument>.Filter.Eq("bookingId", bookingId)
            ).FirstOrDefault();

            if (returnDoc == null ||
                !returnDoc.GetValue("inspectionRequired", false).ToBoolean())
            {
                return null;
            }

            // Retrieve the item.
            var itemValue = booking.GetValue("itemId", BsonNull.Value);

            if (!itemValue.IsObjectId)
                return null;

            var item = MongoDatabase.Items.Find(
                x => x.Id == itemValue.AsObjectId
            ).FirstOrDefault();

            if (item == null)
                return null;

            // Retrieve the latest BORROWER return condition.
            var borrowerCondition = MongoDatabase.ConditionRecords.Find(
                Builders<BsonDocument>.Filter.Eq("bookingId", bookingId) &
                Builders<BsonDocument>.Filter.Eq("phase", "RETURN") &
                Builders<BsonDocument>.Filter.Eq("participantRole", "BORROWER")
            ).SortByDescending(x => x["createdAt"]).FirstOrDefault();

            // Retrieve the owner's original handover condition.
            var handoverCondition = MongoDatabase.ConditionRecords.Find(
                Builders<BsonDocument>.Filter.Eq("bookingId", bookingId) &
                Builders<BsonDocument>.Filter.Eq("phase", "HANDOVER") &
                Builders<BsonDocument>.Filter.Eq("participantRole", "OWNER")
            ).SortByDescending(x => x["createdAt"]).FirstOrDefault();

            return new ReturnInspectionViewModel
            {
                BookingId = bookingId.ToString(),
                ReturnId = returnDoc.GetValue("_id", BsonNull.Value).ToString(),
                ItemId = item.Id.ToString(),

                ItemName = item.Name,
                ItemCategory = item.Category,
                ItemConditionBefore = handoverCondition == null
                    ? item.Condition
                    : handoverCondition.GetValue("condition", item.Condition ?? "").ToString(),

                ItemPhotos = item.Photos ?? new List<string>(),

                HandoverConditionNotes = handoverCondition == null
                    ? ""
                    : handoverCondition.GetValue("notes", "").ToString(),

                HandoverConditionPhotos = handoverCondition == null
                    ? new List<string>()
                    : handoverCondition.GetValue("photos", new BsonArray())
                        .AsBsonArray.Select(x => x.ToString()).ToList(),

                BorrowerConditionAfter = borrowerCondition == null
                    ? ""
                    : borrowerCondition.GetValue("condition", "").ToString(),

                BorrowerConditionNotes = borrowerCondition == null
                    ? ""
                    : borrowerCondition.GetValue("notes", "").ToString(),

                BorrowerConditionPhotos = borrowerCondition == null
                    ? new List<string>()
                    : borrowerCondition.GetValue("photos", new BsonArray())
                        .AsBsonArray.Select(x => x.ToString()).ToList(),

                ReturnStatus = returnDoc.GetValue("status", "").ToString(),

                ReturnRequestedAt =
                    ReadDate(returnDoc, "requestedAt") ??
                    ReadDate(booking, "returnRequestedAt"),

                InspectionDate = ReadDate(returnDoc, "confirmedAt"),

                InspectionRequired = true
            };
        }

        public static bool ConfirmHandover(ObjectId borrowerId, string recordId)
        {
            EnsureLifecycleIndexes();
            BsonDocument doc; bool isBooking;
            if (!TryFind(borrowerId, recordId, out doc, out isBooking)) return false;
            var status = Status(doc);
            var canConfirmLegacyPending = status == "PENDING" && (doc.Contains("approvedAt") || doc.Contains("acceptedAt"));
            if (!AwaitingHandover.Contains(status) && !canConfirmLegacyPending) return false;
            var borrowerAlreadyConfirmed = doc.GetValue("borrowerHandoverConfirmed", doc.GetValue("handoverConfirmed", false)).ToBoolean();
            if (borrowerAlreadyConfirmed) return false;
            var now = DateTime.UtcNow;
            var update = Builders<BsonDocument>.Update.Set("borrowerHandoverConfirmed", true).Set("borrowerHandoverConfirmedAt", now).Set("updatedAt", now);
            UpdateRecord(doc, isBooking, borrowerId, update);
            UpdateLinkedRental(doc, borrowerId, update);
            SaveHandover(doc, borrowerId, "BORROWER", now);
            AdminAccountabilityService.LogActivity(borrowerId, "HANDOVER_CONFIRMED", "BOOKING", ReadBookingId(doc), new BsonDocument { { "party", "BORROWER" } });
            ActivateIfBothConfirmed(doc.GetValue("_id", BsonNull.Value), isBooking);
            return true;
        }

        public static bool ConfirmHandoverByOwner(ObjectId ownerId, string recordId)
        {
            EnsureLifecycleIndexes();
            ObjectId id;
            if (!ObjectId.TryParse(recordId, out id)) return false;
            var ownerFilter = Builders<BsonDocument>.Filter.Eq("ownerId", ownerId);
            var allowed = Builders<BsonDocument>.Filter.In("status", new[] { "ACCEPTED", "APPROVED", "ONGOING", "ACTIVE" });
            var doc = MongoDatabase.Bookings.Find(Builders<BsonDocument>.Filter.Eq("_id", id) & ownerFilter & allowed).FirstOrDefault();
            var isBooking = doc != null;
            if (doc == null) doc = MongoDatabase.Rentals.Find(Builders<BsonDocument>.Filter.Eq("_id", id) & ownerFilter & allowed).FirstOrDefault();
            if (doc == null || doc.GetValue("ownerHandoverConfirmed", false).ToBoolean()) return false;

            var now = DateTime.UtcNow;
            var update = Builders<BsonDocument>.Update.Set("ownerHandoverConfirmed", true).Set("ownerHandoverConfirmedAt", now).Set("updatedAt", now);
            var collection = isBooking ? MongoDatabase.Bookings : MongoDatabase.Rentals;
            var ownerNotConfirmed = Builders<BsonDocument>.Filter.Eq("ownerHandoverConfirmed", false) | Builders<BsonDocument>.Filter.Exists("ownerHandoverConfirmed", false);
            var result = collection.UpdateOne(Builders<BsonDocument>.Filter.Eq("_id", id) & ownerFilter & allowed & ownerNotConfirmed, update);
            if (result.ModifiedCount != 1) return false;
            UpdateLinkedRental(doc, ownerId, update);
            SaveHandover(doc, ownerId, "OWNER", now);
            AdminAccountabilityService.LogActivity(ownerId, "HANDOVER_CONFIRMED", "BOOKING", ReadBookingId(doc), new BsonDocument { { "party", "OWNER" } });
            ActivateIfBothConfirmed(doc.GetValue("_id", BsonNull.Value), isBooking);
            return true;
        }

        private static void ActivateIfBothConfirmed(BsonValue id, bool isBooking)
        {
            if (!id.IsObjectId) return;
            var collection = isBooking ? MongoDatabase.Bookings : MongoDatabase.Rentals;
            var doc = collection.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstOrDefault();
            if (doc == null || !doc.GetValue("borrowerHandoverConfirmed", doc.GetValue("handoverConfirmed", false)).ToBoolean() ||
                !doc.GetValue("ownerHandoverConfirmed", false).ToBoolean()) return;

            var now = DateTime.UtcNow;
            var eligible = Builders<BsonDocument>.Filter.Eq("_id", id) &
                Builders<BsonDocument>.Filter.In("status", new[] { "ACCEPTED", "APPROVED", "PENDING", "ACTIVE" }) &
                Builders<BsonDocument>.Filter.Eq("borrowerHandoverConfirmed", true) &
                Builders<BsonDocument>.Filter.Eq("ownerHandoverConfirmed", true);
            var update = Builders<BsonDocument>.Update.Set("status", "ONGOING").Set("handoverConfirmed", true)
                .Set("handoverConfirmedAt", now).Set("handoverAt", now).Set("updatedAt", now);
            if (collection.UpdateOne(eligible, update).ModifiedCount != 1) return;

            var itemValue = doc.GetValue("itemId", BsonNull.Value);
            if (itemValue.IsObjectId)
                MongoDatabase.Items.UpdateOne(x => x.Id == itemValue.AsObjectId,
                    Builders<ItemDocument>.Update.Set(x => x.AvailabilityStatus, "RENTED").Set(x => x.UpdatedAt, now));

            var bookingId = doc.GetValue("bookingId", id);
            if (bookingId.IsObjectId)
                MongoDatabase.Handovers.UpdateOne(Builders<BsonDocument>.Filter.Eq("bookingId", bookingId),
                    Builders<BsonDocument>.Update.SetOnInsert("_id", ObjectId.GenerateNewId()).SetOnInsert("bookingId", bookingId)
                        .SetOnInsert("scheduledAt", doc.GetValue("handoverDate", doc.GetValue("startDate", BsonNull.Value)))
                        .Set("status", "COMPLETED").Set("ownerConfirmed", true).Set("borrowerConfirmed", true)
                        .Set("actualHandoverAt", now).Set("updatedAt", now).SetOnInsert("createdAt", now),
                    new UpdateOptions { IsUpsert = true });

            if (bookingId.IsObjectId)
            {
                MongoDatabase.Rentals.UpdateOne(Builders<BsonDocument>.Filter.Eq("bookingId", bookingId) &
                    Builders<BsonDocument>.Filter.Eq("borrowerHandoverConfirmed", true) &
                    Builders<BsonDocument>.Filter.Eq("ownerHandoverConfirmed", true), update);
            }
        }




        public static bool RequestReturn(ObjectId actorId, string recordId)
        {
            EnsureLifecycleIndexes();

            BsonDocument doc;
            bool isBooking;

            if (!TryFindParticipant(actorId, recordId, out doc, out isBooking))
                return false;

            var status = Status(doc);

            if ((status != "ONGOING" && status != "OVERDUE") ||
                !doc.GetValue("handoverConfirmed", false).ToBoolean())
                return false;

            var now = DateTime.UtcNow;

            var collection = isBooking
                ? MongoDatabase.Bookings
                : MongoDatabase.Rentals;

            var filter =
                Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]) &
                ParticipantFilter(actorId) &
                Builders<BsonDocument>.Filter.Eq(
                    "status", doc.GetValue("status", ""));

            var update = Builders<BsonDocument>.Update
                .Set("status", "RETURN_REQUESTED")
                .Set("returnRequestedAt", now)
                .Set("returnRequestedBy", actorId)
                .Set("updatedAt", now);

            if (collection.UpdateOne(filter, update).ModifiedCount != 1)
                return false;

            var bookingId = doc.GetValue(
                "bookingId",
                doc.GetValue("_id", BsonNull.Value));

            if (bookingId.IsObjectId)
            {
                var linkedFilter =
                    Builders<BsonDocument>.Filter.Eq("bookingId", bookingId) &
                    ParticipantFilter(actorId);

                MongoDatabase.Rentals.UpdateOne(
                    linkedFilter,
                    update);

                var borrowerValue = doc.GetValue(
                    "borrowerId",
                    doc.GetValue("renterId", BsonNull.Value));

                MongoDatabase.Returns.UpdateOne(
                    Builders<BsonDocument>.Filter.Eq("bookingId", bookingId),
                    Builders<BsonDocument>.Update
                        .SetOnInsert("_id", ObjectId.GenerateNewId())
                        .SetOnInsert("bookingId", bookingId)
                        .SetOnInsert("rentalId", doc.GetValue("_id", BsonNull.Value))
                        .SetOnInsert("borrowerId", borrowerValue)
                        .SetOnInsert("createdAt", now)
                        .Set("status", "RETURN_REQUESTED")
                        .Set("requestedAt", now)
                        .Set("requestedBy", actorId)
                        .Set("updatedAt", now),
                    new UpdateOptions { IsUpsert = true });
            }

            AdminAccountabilityService.LogActivity(
                actorId,
                "RETURN_REQUESTED",
                "BOOKING",
                bookingId.IsObjectId ? bookingId.AsObjectId : doc["_id"].AsObjectId,
                new BsonDocument());

            return true;
        }



        public static bool ConfirmReturnByOwner(ObjectId ownerId, string recordId)
        {
            EnsureLifecycleIndexes();

            ObjectId id;
            if (!ObjectId.TryParse(recordId, out id))
                return false;

            var ownerFilter = Builders<BsonDocument>.Filter.Eq("ownerId", ownerId);

            var returnStatuses = Builders<BsonDocument>.Filter.In(
                "status",
                new[] { "RETURN_REQUESTED", "RETURN PENDING", "OVERDUE" }
            );

            var doc = MongoDatabase.Bookings.Find(
                Builders<BsonDocument>.Filter.Eq("_id", id) &
                ownerFilter &
                returnStatuses
            ).FirstOrDefault();

            var isBooking = doc != null;

            if (doc == null)
            {
                doc = MongoDatabase.Rentals.Find(
                    Builders<BsonDocument>.Filter.Eq("_id", id) &
                    ownerFilter &
                    returnStatuses
                ).FirstOrDefault();
            }

            if (doc == null)
                return false;

            var bookingId = doc.GetValue(
                "bookingId",
                doc.GetValue("_id", BsonNull.Value)
            );

            if (!bookingId.IsObjectId)
                return false;

            // Only the borrower's RETURN condition record is required.
            var borrowerCondition = MongoDatabase.ConditionRecords.Find(
                Builders<BsonDocument>.Filter.Eq("bookingId", bookingId) &
                Builders<BsonDocument>.Filter.Eq("phase", "RETURN") &
                Builders<BsonDocument>.Filter.Eq("participantRole", "BORROWER")
            )
            .SortByDescending(x => x["createdAt"])
            .FirstOrDefault();

            if (borrowerCondition == null)
            {
                System.Diagnostics.Debug.WriteLine("===== RETURN CONFIRMATION FAILED =====");
                System.Diagnostics.Debug.WriteLine("Booking ID: " + bookingId);
                System.Diagnostics.Debug.WriteLine("Borrower Condition Found: false");
                System.Diagnostics.Debug.WriteLine("======================================");

                return false;
            }

            // A condition of FAIR, POOR, or DAMAGED requires inspection.
            var borrowerConditionValue = borrowerCondition
                .GetValue("condition", "")
                .ToString()
                .ToUpperInvariant();

            var requiresInspection =
                new[] { "FAIR", "POOR", "DAMAGED" }
                .Contains(borrowerConditionValue);

            var now = DateTime.UtcNow;

            if (requiresInspection)
            {
                // Record the return for inspection.
                // Do not complete the rental or make the item available yet.
                MongoDatabase.Returns.UpdateOne(
                    Builders<BsonDocument>.Filter.Eq("bookingId", bookingId),
                    Builders<BsonDocument>.Update
                        .SetOnInsert("_id", ObjectId.GenerateNewId())
                        .SetOnInsert("bookingId", bookingId)
                        .Set("status", "INSPECTION_REQUIRED")
                        .Set("damageReported", true)
                        .Set("inspectionRequired", true)
                        .Set("confirmedBy", ownerId)
                        .Set("confirmedAt", now)
                        .Set("updatedAt", now)
                        .SetOnInsert("createdAt", now),
                    new UpdateOptions { IsUpsert = true }
                );

                AdminAccountabilityService.LogActivity(
                    ownerId,
                    "RETURN_REQUIRES_INSPECTION",
                    "BOOKING",
                    bookingId.AsObjectId,
                    new BsonDocument()
                );

                return true;
            }

            // No inspection issue: complete the rental.
            var update = Builders<BsonDocument>.Update
                .Set("status", "COMPLETED")
                .Set("returnedAt", now)
                .Set("completedAt", now)
                .Set("updatedAt", now);

            var filter =
                Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]) &
                Builders<BsonDocument>.Filter.Eq("ownerId", ownerId) &
                Builders<BsonDocument>.Filter.Eq("status", doc["status"]);

            var result = isBooking
                ? MongoDatabase.Bookings.UpdateOne(filter, update)
                : MongoDatabase.Rentals.UpdateOne(filter, update);

            if (result.ModifiedCount != 1)
            {
                System.Diagnostics.Debug.WriteLine("===== BOOKING UPDATE FAILED =====");
                System.Diagnostics.Debug.WriteLine("Booking ID: " + bookingId);
                System.Diagnostics.Debug.WriteLine("Matched Count: " + result.MatchedCount);
                System.Diagnostics.Debug.WriteLine("Modified Count: " + result.ModifiedCount);
                System.Diagnostics.Debug.WriteLine("Is Booking: " + isBooking);
                System.Diagnostics.Debug.WriteLine("=================================");

                return false;
            }

            UpdateLinkedRental(doc, ownerId, update);

            PaymentFeeService.AssessLateFee(doc, now);

            MongoDatabase.Returns.UpdateOne(
                Builders<BsonDocument>.Filter.Eq("bookingId", bookingId),
                Builders<BsonDocument>.Update
                    .SetOnInsert("_id", ObjectId.GenerateNewId())
                    .SetOnInsert("bookingId", bookingId)
                    .Set("status", "COMPLETED")
                    .Set("damageReported", false)
                    .Set("confirmedBy", ownerId)
                    .Set("confirmedAt", now)
                    .Set("returnedAt", now)
                    .Set("updatedAt", now)
                    .SetOnInsert("createdAt", now),
                new UpdateOptions { IsUpsert = true }
            );

            var itemValue = doc.GetValue("itemId", BsonNull.Value);

            if (itemValue.IsObjectId)
            {
                var itemId = itemValue.AsObjectId;

                var itemBefore = MongoDatabase.Items
                    .Find(x => x.Id == itemId)
                    .FirstOrDefault();

                System.Diagnostics.Debug.WriteLine("===== ITEM AVAILABILITY DEBUG =====");
                System.Diagnostics.Debug.WriteLine("Item ID: " + itemId);

                if (itemBefore == null)
                {
                    System.Diagnostics.Debug.WriteLine("Item not found.");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine(
                        "Availability Before: " + itemBefore.AvailabilityStatus
                    );

                    var itemUpdateResult = MongoDatabase.Items.UpdateOne(
                        x => x.Id == itemId &&
                             x.AvailabilityStatus == "RENTED",
                        Builders<ItemDocument>.Update
                            .Set(x => x.AvailabilityStatus, "AVAILABLE")
                            .Set(x => x.UpdatedAt, now)
                    );

                    System.Diagnostics.Debug.WriteLine(
                        "Matched Count: " + itemUpdateResult.MatchedCount
                    );

                    System.Diagnostics.Debug.WriteLine(
                        "Modified Count: " + itemUpdateResult.ModifiedCount
                    );

                    var itemAfter = MongoDatabase.Items
                        .Find(x => x.Id == itemId)
                        .FirstOrDefault();

                    System.Diagnostics.Debug.WriteLine(
                        "Availability After: " +
                        (itemAfter == null ? "ITEM NOT FOUND" : itemAfter.AvailabilityStatus)
                    );
                }

                System.Diagnostics.Debug.WriteLine("==================================");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine(
                    "The booking itemId is not an ObjectId."
                );
            }

            AdminAccountabilityService.LogActivity(
                ownerId,
                "RETURN_CONFIRMED",
                "BOOKING",
                bookingId.AsObjectId,
                new BsonDocument()
            );

            return true;
        }



        public static bool FinalizeInspectedReturn(ObjectId ownerId, string recordId)
        {
            EnsureLifecycleIndexes();

            ObjectId id;

            if (!ObjectId.TryParse(recordId, out id))
                return false;

            var ownerFilter =
                Builders<BsonDocument>.Filter.Eq("ownerId", ownerId);

            var returnStatuses =
                Builders<BsonDocument>.Filter.In(
                    "status",
                    new[] { "RETURN_REQUESTED", "RETURN PENDING", "OVERDUE" }
                );

            // Find the booking first.
            var doc = MongoDatabase.Bookings.Find(
                Builders<BsonDocument>.Filter.Eq("_id", id) &
                ownerFilter &
                returnStatuses
            ).FirstOrDefault();

            var isBooking = doc != null;

            // If it is not in Bookings, check Rentals.
            if (doc == null)
            {
                doc = MongoDatabase.Rentals.Find(
                    Builders<BsonDocument>.Filter.Eq("_id", id) &
                    ownerFilter &
                    returnStatuses
                ).FirstOrDefault();
            }

            if (doc == null)
                return false;

            var bookingId = doc.GetValue(
                "bookingId",
                doc.GetValue("_id", BsonNull.Value)
            );

            if (!bookingId.IsObjectId)
                return false;

            // Only allow finalization after an inspection has been required.
            var returnDoc = MongoDatabase.Returns.Find(
                Builders<BsonDocument>.Filter.Eq("bookingId", bookingId) &
                Builders<BsonDocument>.Filter.Eq("status", "INSPECTION_REQUIRED") &
                Builders<BsonDocument>.Filter.Eq("inspectionRequired", true)
            ).FirstOrDefault();

            if (returnDoc == null)
                return false;

            // Verify that the borrower has a return condition record.
            var borrowerValue = doc.GetValue("borrowerId", BsonNull.Value);

            if (!borrowerValue.IsObjectId)
                return false;

            var borrowerCondition = MongoDatabase.ConditionRecords.Find(
                Builders<BsonDocument>.Filter.Eq("bookingId", bookingId) &
                Builders<BsonDocument>.Filter.Eq("phase", "RETURN") &
                Builders<BsonDocument>.Filter.Eq("participantRole", "BORROWER") &
                Builders<BsonDocument>.Filter.Eq("recordedBy", borrowerValue.AsObjectId)
            )
            .SortByDescending(x => x["updatedAt"])
            .FirstOrDefault();

            if (borrowerCondition == null)
                return false;

            var condition = borrowerCondition
                .GetValue("condition", "")
                .ToString()
                .ToUpperInvariant();

            // This method is specifically for conditions requiring inspection.
            if (!new[] { "FAIR", "POOR", "DAMAGED" }.Contains(condition))
                return false;

            var now = DateTime.UtcNow;

            var update = Builders<BsonDocument>.Update
                .Set("status", "COMPLETED")
                .Set("returnedAt", now)
                .Set("completedAt", now)
                .Set("updatedAt", now);

            var bookingFilter =
                Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]) &
                ownerFilter &
                returnStatuses;

            var collection = isBooking
                ? MongoDatabase.Bookings
                : MongoDatabase.Rentals;

            var result = collection.UpdateOne(bookingFilter, update);

            if (result.ModifiedCount != 1)
                return false;

            // Update the linked rental when the main record was a booking.
            if (isBooking)
            {
                UpdateLinkedRental(doc, ownerId, update);
            }

            PaymentFeeService.AssessLateFee(doc, now);

            // Finalize the return inspection.
            MongoDatabase.Returns.UpdateOne(
                Builders<BsonDocument>.Filter.Eq("bookingId", bookingId) &
                Builders<BsonDocument>.Filter.Eq("status", "INSPECTION_REQUIRED"),
                Builders<BsonDocument>.Update
                    .Set("status", "COMPLETED")
                    .Set("inspectionRequired", false)
                    .Set("inspectionCompletedBy", ownerId)
                    .Set("inspectionCompletedAt", now)
                    .Set("damageReported", false)
                    .Set("confirmedBy", ownerId)
                    .Set("confirmedAt", now)
                    .Set("returnedAt", now)
                    .Set("updatedAt", now)
            );

            // Make the item available again.
            var itemValue = doc.GetValue("itemId", BsonNull.Value);

            if (itemValue.IsObjectId)
            {
                var itemId = itemValue.AsObjectId;

                MongoDatabase.Items.UpdateOne(
                    x => x.Id == itemId &&
                         x.AvailabilityStatus == "RENTED",
                    Builders<ItemDocument>.Update
                        .Set(x => x.AvailabilityStatus, "AVAILABLE")
                        .Set(x => x.UpdatedAt, now)
                );
            }

            AdminAccountabilityService.LogActivity(
                ownerId,
                "RETURN_INSPECTION_COMPLETED",
                "BOOKING",
                bookingId.AsObjectId,
                new BsonDocument()
            );

            return true;
        }

        public static bool RecordCondition(
            ObjectId actorId,
            string recordId,
            string phase,
            string condition,
            string notes,
            IList<string> photos)
        {
            EnsureLifecycleIndexes();

            ObjectId id;

            phase = (phase ?? "").Trim().ToUpperInvariant();
            condition = (condition ?? "").Trim().ToUpperInvariant();

            if (!ObjectId.TryParse(recordId, out id) ||
                (phase != "HANDOVER" && phase != "RETURN") ||
                !new[] { "EXCELLENT", "GOOD", "FAIR", "POOR", "DAMAGED" }.Contains(condition) ||
                (notes ?? "").Length > 1000 ||
                photos == null ||
                photos.Count > 5)
            {
                return false;
            }

            var booking = MongoDatabase.Bookings
                .Find(Builders<BsonDocument>.Filter.Eq("_id", id))
                .FirstOrDefault();

            if (booking == null) return false;

            var ownerValue = booking.GetValue("ownerId", BsonNull.Value);
            var borrowerValue = booking.GetValue("borrowerId", BsonNull.Value);

            var isOwner = ownerValue.IsObjectId && ownerValue.AsObjectId == actorId;
            var isBorrower = borrowerValue.IsObjectId && borrowerValue.AsObjectId == actorId;

            // Only the booking owner or borrower can submit condition records.
            if (!isOwner && !isBorrower) return false;

            // RETURN condition records can only be submitted by the borrower.
            // HANDOVER condition records remain available to both participants.
            // RETURN condition records can only be submitted by the borrower.
            if (phase == "RETURN" && !isBorrower)
                return false;

            var status = Status(booking);

            if (phase == "HANDOVER" &&
                status != "ACCEPTED" &&
                status != "ONGOING" &&
                status != "ACTIVE")
            {
                return false;
            }

            if (phase == "RETURN")
            {
                // Borrower may submit or edit the return condition
                // only while the return is awaiting the owner's decision.
                var isReturnStatus =
                    status == "RETURN_REQUESTED" ||
                    status == "RETURN PENDING" ||
                    status == "OVERDUE";

                if (!isReturnStatus)
                    return false;
            }

            if (photos.Any(x => !IsSavedConditionPhoto(x)))
                return false;

            var now = DateTime.UtcNow;

            // Save or update the participant's condition record.
            var conditionFilter =
            Builders<BsonDocument>.Filter.Eq("bookingId", id) &
            Builders<BsonDocument>.Filter.Eq("phase", phase) &
            Builders<BsonDocument>.Filter.Eq("recordedBy", actorId) &
            Builders<BsonDocument>.Filter.Eq(
                "participantRole",
                isOwner ? "OWNER" : "BORROWER"
            );

            var conditionUpdate = Builders<BsonDocument>.Update
            .SetOnInsert("_id", ObjectId.GenerateNewId())
            .SetOnInsert("bookingId", id)
            .SetOnInsert("itemId", booking.GetValue("itemId", BsonNull.Value))
            .SetOnInsert("recordedBy", actorId)
            .SetOnInsert("participantRole", isOwner ? "OWNER" : "BORROWER")
            .SetOnInsert("phase", phase)
            .SetOnInsert("createdAt", now)
            .Set("condition", condition)
            .Set("notes", (notes ?? "").Trim())
            .Set("updatedAt", now);

            // Preserve existing RETURN photos when no new photos are uploaded.
            // HANDOVER behavior remains unchanged.
            if (phase != "RETURN" || photos.Count > 0)
            {
                conditionUpdate = Builders<BsonDocument>.Update.Combine(
                    conditionUpdate,
                    Builders<BsonDocument>.Update.Set(
                        "photos",
                        new BsonArray(photos.Select(x => (BsonValue)x))
                    )
                );
            }

            MongoDatabase.ConditionRecords.UpdateOne(
                conditionFilter,
                conditionUpdate,
                new UpdateOptions { IsUpsert = true }
            );

            if (phase == "RETURN")
            {
                // RETURN condition details are always submitted by the borrower.
                const string rolePrefix = "borrower";

                var returnUpdate = Builders<BsonDocument>.Update
                    .SetOnInsert("_id", ObjectId.GenerateNewId())
                    .SetOnInsert("bookingId", id)
                    .SetOnInsert("borrowerId", borrowerValue)
                    .SetOnInsert("createdAt", now)
                    .Set(rolePrefix + "ConditionAfter", condition)
                    .Set(rolePrefix + "ConditionNotes", (notes ?? "").Trim())
                    .Set(rolePrefix + "ConditionAt", now)
                    .Set("conditionAfter", condition)
                    .Set("conditionNotes", (notes ?? "").Trim())
                    .Set("conditionRecordedAt", now)
                    .Set("updatedAt", now);

                // Only replace stored photos when new photos are provided.
                if (photos.Count > 0)
                {
                    var photoArray = new BsonArray(
                        photos.Select(x => (BsonValue)x)
                    );

                    returnUpdate = Builders<BsonDocument>.Update.Combine(
                        returnUpdate,
                        Builders<BsonDocument>.Update
                            .Set(rolePrefix + "ConditionPhotos", photoArray)
                            .Set("conditionPhotos", photoArray)
                    );
                }

                MongoDatabase.Returns.UpdateOne(
                    Builders<BsonDocument>.Filter.Eq("bookingId", id),
                    returnUpdate,
                    new UpdateOptions { IsUpsert = true }
                );
            }
            else
            {
                // HANDOVER condition records can still be submitted by either participant.
                var rolePrefix = isOwner ? "owner" : "borrower";

                MongoDatabase.Handovers.UpdateOne(
                    Builders<BsonDocument>.Filter.Eq("bookingId", id),
                    Builders<BsonDocument>.Update
                        .SetOnInsert("_id", ObjectId.GenerateNewId())
                        .SetOnInsert("bookingId", id)
                        .SetOnInsert("ownerId", ownerValue)
                        .SetOnInsert("borrowerId", borrowerValue)
                        .SetOnInsert("createdAt", now)
                        .Set(rolePrefix + "ConditionBefore", condition)
                        .Set(rolePrefix + "ConditionPhotos",
                            new BsonArray(photos.Select(x => (BsonValue)x)))
                        .Set(rolePrefix + "ConditionNotes", (notes ?? "").Trim())
                        .Set(rolePrefix + "ConditionAt", now)
                        .Set("conditionBefore", condition)
                        .Set("conditionPhotos",
                            new BsonArray(photos.Select(x => (BsonValue)x)))
                        .Set("conditionNotes", (notes ?? "").Trim())
                        .Set("updatedAt", now),
                    new UpdateOptions { IsUpsert = true }
                );
            }

            return true;
        }



        private static void SaveHandover(BsonDocument booking, ObjectId actorId, string role, DateTime at)
        {
            var bookingId = booking.GetValue("bookingId", booking.GetValue("_id", BsonNull.Value));
            if (!bookingId.IsObjectId) return;
            var confirmedField = role == "OWNER" ? "ownerConfirmed" : "borrowerConfirmed";
            MongoDatabase.Handovers.UpdateOne(Builders<BsonDocument>.Filter.Eq("bookingId", bookingId),
                Builders<BsonDocument>.Update.SetOnInsert("_id", ObjectId.GenerateNewId()).SetOnInsert("bookingId", bookingId)
                    .SetOnInsert("ownerId", booking.GetValue("ownerId", BsonNull.Value)).SetOnInsert("borrowerId", booking.GetValue("borrowerId", BsonNull.Value))
                    .SetOnInsert("scheduledAt", booking.GetValue("handoverDate", booking.GetValue("startDate", BsonNull.Value)))
                    .Set(confirmedField, true).Set(role == "OWNER" ? "ownerConfirmedAt" : "borrowerConfirmedAt", at)
                    .Set("updatedAt", at).SetOnInsert("createdAt", at), new UpdateOptions { IsUpsert = true });
        }

        private static ObjectId? ReadBookingId(BsonDocument doc)
        {
            var value = doc.GetValue("bookingId", doc.GetValue("_id", BsonNull.Value));
            return value.IsObjectId ? (ObjectId?)value.AsObjectId : null;
        }

        private static void EnsureLifecycleIndexes() { var initialized = LifecycleIndexesReady.Value; }

        private static bool IsSavedConditionPhoto(string photo)
        {
            if (String.IsNullOrWhiteSpace(photo) || photo.Length > 80 || photo.Contains("/") || photo.Contains("\\")) return false;
            var extension = Path.GetExtension(photo).ToLowerInvariant();
            Guid id;
            if ((extension != ".jpg" && extension != ".jpeg" && extension != ".png") ||
                !Guid.TryParseExact(Path.GetFileNameWithoutExtension(photo), "N", out id)) return false;
            var context = HttpContext.Current;
            return context != null && File.Exists(context.Server.MapPath("~/Uploads/" + photo));
        }

        public static bool SubmitDispute(ObjectId participantId, string recordId, string reason, string details)
        {
            ObjectId id;
            if (!ObjectId.TryParse(recordId, out id)) return false;
            var doc = MongoDatabase.Bookings.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstOrDefault();
            var isBooking = doc != null;
            if (doc == null) doc = MongoDatabase.Rentals.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstOrDefault();
            if (doc == null) return false;
            var ownerValue = doc.GetValue("ownerId", BsonNull.Value);
            var borrowerValue = doc.GetValue("borrowerId", doc.GetValue("renterId", BsonNull.Value));
            if (!(ownerValue.IsObjectId && ownerValue.AsObjectId == participantId) &&
                !(borrowerValue.IsObjectId && borrowerValue.AsObjectId == participantId)) return false;
            if (Status(doc) != "ONGOING" && Status(doc) != "OVERDUE" && Status(doc) != "ACCEPTED" && Status(doc) != "APPROVED" &&
                Status(doc) != "RETURN_REQUESTED" && Status(doc) != "RETURN PENDING") return false;
            if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 100 || (details ?? "").Length > 400) return false;
            var now = DateTime.UtcNow;
            var text = string.IsNullOrWhiteSpace(details) ? reason.Trim() : reason.Trim() + ": " + details.Trim();
            var update = Builders<BsonDocument>.Update.Set("status", "DISPUTED").Set("disputeReason", text)
                .Set("disputeCategory", reason.Trim()).Set("disputeDetails", (details ?? "").Trim()).Set("disputedAt", now).Set("updatedAt", now);
            var collection = isBooking ? MongoDatabase.Bookings : MongoDatabase.Rentals;
            var participantFilter = Builders<BsonDocument>.Filter.Eq("ownerId", participantId) |
                Builders<BsonDocument>.Filter.Eq("borrowerId", participantId) | Builders<BsonDocument>.Filter.Eq("renterId", participantId);
            if (collection.UpdateOne(Builders<BsonDocument>.Filter.Eq("_id", id) & participantFilter &
                Builders<BsonDocument>.Filter.In("status", new[] { "ONGOING", "OVERDUE", "ACCEPTED", "APPROVED", "RETURN_REQUESTED", "RETURN PENDING" }), update).ModifiedCount != 1) return false;
            UpdateLinkedRental(doc, participantId, update);
            AdminAccountabilityService.RecordDispute(doc, participantId, reason.Trim(), (details ?? "").Trim());
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
        public static void RefreshOverdue(ObjectId userId)
        {
            var users = Builders<BsonDocument>.Filter.Eq("borrowerId", userId) | Builders<BsonDocument>.Filter.Eq("renterId", userId) |
                        Builders<BsonDocument>.Filter.Eq("ownerId", userId) | Builders<BsonDocument>.Filter.Eq("borrowerId", userId.ToString()) |
                        Builders<BsonDocument>.Filter.Eq("renterId", userId.ToString()) | Builders<BsonDocument>.Filter.Eq("ownerId", userId.ToString());
            var active = users & Builders<BsonDocument>.Filter.In("status", new[] { "ONGOING", "ACTIVE" });
            var now = DateTime.UtcNow;
            foreach (var collection in new[] { MongoDatabase.Bookings, MongoDatabase.Rentals })
            {
                foreach (var doc in collection.Find(active).ToList())
                {
                    var start = ReadDate(doc, "handoverAt") ?? ReadDate(doc, "handoverConfirmedAt") ?? ReadDate(doc, "startDate") ?? ReadDate(doc, "handoverDate");
                    var graceMinutesValue = doc.GetValue("lateGraceMinutes", 30);
                    var graceMinutes = graceMinutesValue.IsNumeric ? Math.Max(0, Math.Min(60, graceMinutesValue.ToInt32())) : 30;
                    if (!start.HasValue || start.Value.AddHours(ReadHours(doc)).AddMinutes(graceMinutes) > now) continue;
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

        private static FilterDefinition<BsonDocument> ParticipantFilter(ObjectId actorId)
        {
            var actorString = actorId.ToString();

            return
                Builders<BsonDocument>.Filter.Eq("ownerId", actorId) |
                Builders<BsonDocument>.Filter.Eq("borrowerId", actorId) |
                Builders<BsonDocument>.Filter.Eq("renterId", actorId) |
                Builders<BsonDocument>.Filter.Eq("ownerId", actorString) |
                Builders<BsonDocument>.Filter.Eq("borrowerId", actorString) |
                Builders<BsonDocument>.Filter.Eq("renterId", actorString);
        }



        private static bool TryFindParticipant(
            ObjectId actorId,
            string recordId,
            out BsonDocument doc,
            out bool isBooking)
        {
            ObjectId id;
            doc = null;
            isBooking = false;

            if (!ObjectId.TryParse(recordId, out id))
                return false;

            var participantFilter = ParticipantFilter(actorId);

            doc = MongoDatabase.Bookings.Find(
                Builders<BsonDocument>.Filter.Eq("_id", id) & participantFilter
            ).FirstOrDefault();

            if (doc != null)
            {
                isBooking = true;
                return true;
            }

            doc = MongoDatabase.Rentals.Find(
                Builders<BsonDocument>.Filter.Eq("_id", id) & participantFilter
            ).FirstOrDefault();

            return doc != null;
        }

        private static void UpdateParticipantRecord(
            BsonDocument doc,
            bool isBooking,
            ObjectId actorId,
            UpdateDefinition<BsonDocument> update)
        {
            var collection = isBooking
                ? MongoDatabase.Bookings
                : MongoDatabase.Rentals;

            var filter =
                Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]) &
                ParticipantFilter(actorId) &
                Builders<BsonDocument>.Filter.Eq(
                    "status",
                    doc.GetValue("status", "")
                );

            if (collection.UpdateOne(filter, update).ModifiedCount != 1)
                throw new InvalidOperationException(
                    "The borrowing record changed. Refresh and try again."
                );
        }

        private static void UpdateLinkedRentalForParticipant(
            BsonDocument doc,
            ObjectId actorId,
            UpdateDefinition<BsonDocument> update)
        {
            var id = doc.GetValue("_id", BsonNull.Value);
            var bookingId = doc.GetValue("bookingId", id);

            if (!bookingId.IsObjectId)
                return;

            var filter =
                Builders<BsonDocument>.Filter.Eq("bookingId", bookingId) &
                ParticipantFilter(actorId);

            MongoDatabase.Rentals.UpdateOne(filter, update);
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

        private static BorrowedItem Map(BsonDocument doc, ObjectId borrowerId, bool ownerView = false)
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
            UserDocument borrower = null;
            if (!parsedBorrowerId.Equals(ObjectId.Empty))
            {
                borrower = MongoDatabase.Users.Find(x => x.Id == parsedBorrowerId).FirstOrDefault();
                if (string.IsNullOrWhiteSpace(borrowerName)) borrowerName = borrower == null ? "Student" : (borrower.FirstName + " " + borrower.LastName).Trim();
            }
            var image = item != null && item.Photos != null && item.Photos.Count > 0 ? MarketplaceService.ToImageUrl(item.Photos[0]) : null;
            var location = item == null ? "" : MarketplaceService.FormatLocation(item.Location);
            var itemName = item == null ? doc.GetValue("itemName", "Item").ToString() : item.Name;
            var period = start.ToLocalTime().ToString("MMM d, h:mm tt", CultureInfo.InvariantCulture) + " – " + end.ToLocalTime().ToString("h:mm tt", CultureInfo.InvariantCulture);
            var tx = doc.GetValue("transactionId", doc.GetValue("requestId", "#UNI-" + idValue.ToString().Substring(Math.Max(0, idValue.ToString().Length - 6)).ToUpperInvariant())).ToString();
            var theme = status == "DISPUTED" ? "mint" : status == "COMPLETED" || status == "COMPLETE" ? "peach" : "indigo";
            var record = new BorrowedItem
            {
                RecordId = idValue.ToString(), OwnerId = !ratingOwnerId.Equals(ObjectId.Empty) ? ratingOwnerId.ToString() : "", BorrowerId = !parsedBorrowerId.Equals(ObjectId.Empty) ? parsedBorrowerId.ToString() : "", Borrower = borrowerName, BorrowerInitials = Initials(borrowerName), BorrowerProgram = borrower == null ? doc.GetValue("borrowerProgram", "").ToString() : ((borrower.Program ?? "") + (string.IsNullOrWhiteSpace(borrower.YearLevel) ? "" : " " + borrower.YearLevel)), IsOwnerView = ownerView, ItemId = itemIdValue.IsObjectId ? itemIdValue.AsObjectId.ToString() : itemIdValue.ToString(), ItemName = itemName, Category = item == null ? doc.GetValue("category", "Other Items").ToString() : item.Category,
                Lender = ownerName, LenderInitials = Initials(ownerName), LenderProgram = owner == null ? doc.GetValue("ownerProgram", "").ToString() : ((owner.Program ?? "") + (string.IsNullOrWhiteSpace(owner.YearLevel) ? "" : " " + owner.YearLevel)),
                PricePerHour = (decimal)price, PaidAmount = (decimal)fee, Start = start.ToLocalTime(), End = end.ToLocalTime(), PickupPoint = doc.GetValue("handoverLocation", location).ToString(),
                PaymentMethod = "Cash", TransactionId = tx, About = item == null ? doc.GetValue("description", "").ToString() : item.Description,
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
