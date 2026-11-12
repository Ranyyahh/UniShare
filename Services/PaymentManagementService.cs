using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using UniShare.Models;

namespace UniShare.Services
{
    /// <summary>Payment records and late-fee calculations persisted in the payments and lateFees collections.</summary>
    public static class PaymentManagementService
    {
        private const int DefaultGracePeriodMinutes = 30;
        private static readonly string[] ClosedBookingStatuses = { "CANCELLED", "CANCELED", "DECLINED", "REJECTED", "EXPIRED" };
        private static readonly object IndexLock = new object();
        private static bool _indexesReady;

        public static PaymentDetailsViewModel GetPaymentDetails(string bookingId, ObjectId userId)
        {
            EnsureIndexes();
            var booking = GetParticipantBooking(bookingId, userId);
            if (booking == null) return null;

            var ownerId = ReadId(booking, "ownerId");
            var borrowerId = ReadId(booking, "borrowerId", "renterId");
            var isOwner = ownerId == userId;
            var rentalFee = GetRentalFee(booking);
            var rental = GetRentalForBooking(booking.GetValue("_id", BsonNull.Value));
            var lateFee = rental == null ? null : RefreshLateFeeEstimate(booking, rental);
            var bookingStatus = booking.GetValue("status", "UNKNOWN").ToString().ToUpperInvariant();
            var durationHours = ReadInt(booking, "durationHours", 0);
            if (durationHours <= 0)
            {
                var start = ReadDate(booking, "startDate") ?? ReadDate(booking, "handoverDate");
                var end = ReadDate(booking, "endDate");
                durationHours = start.HasValue && end.HasValue ? Math.Max(1, (int)Math.Ceiling((end.Value - start.Value).TotalHours)) : 1;
            }
            var hourlyRate = ReadMoney(booking, "hourlyRate", 0m);
            if (hourlyRate <= 0 && durationHours > 0) hourlyRate = decimal.Round(rentalFee / durationHours, 2, MidpointRounding.AwayFromZero);
            var paymentDocs = MongoDatabase.Payments.Find(Builders<BsonDocument>.Filter.Eq("bookingId", booking["_id"])).ToList()
                .OrderByDescending(x => ReadDate(x, "createdAt") ?? DateTime.MinValue).ToList();
            var itemName = ReadItemName(booking);
            var otherPartyId = isOwner ? borrowerId : ownerId;
            var otherParty = ReadUserName(otherPartyId);

            return new PaymentDetailsViewModel
            {
                BookingId = booking["_id"].ToString(),
                RentalId = rental == null ? null : rental.GetValue("_id", BsonNull.Value).ToString(),
                RequestId = booking.GetValue("requestId", booking["_id"].ToString()).ToString(),
                ItemName = itemName,
                BookingStatus = bookingStatus,
                IsOwner = isOwner,
                CanSubmitRentalPayment = new[] { "ACCEPTED", "CONFIRMED", "ACTIVE", "ONGOING", "OVERDUE", "RETURN_REQUESTED", "RETURN PENDING", "COMPLETED", "COMPLETE", "RETURNED" }.Contains(bookingStatus),
                DurationHours = durationHours,
                HourlyRate = hourlyRate,
                RentalFee = rentalFee,
                EstimatedLateFee = lateFee == null ? 0m : ReadMoney(lateFee, "estimatedAmount"),
                FinalizedLateFee = lateFee == null ? (decimal?)null : ReadNullableMoney(lateFee, "finalizedAmount"),
                TotalDue = rentalFee + (lateFee == null ? 0m : ReadNullableMoney(lateFee, "finalizedAmount") ?? ReadMoney(lateFee, "estimatedAmount")),
                LateFee = lateFee == null ? null : MapLateFee(lateFee, rental, booking),
                AvailablePaymentMethods = new List<string> { "CASH", "GCASH" },
                Payments = paymentDocs.Select(x => MapPayment(x, booking, userId)).ToList()
            };
        }

        public static LateFeeDetailsViewModel GetLateFeeDetails(string rentalId, ObjectId userId)
        {
            EnsureIndexes();
            ObjectId id;
            if (!ObjectId.TryParse(rentalId, out id)) return null;
            var rental = MongoDatabase.Rentals.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstOrDefault();
            if (rental == null) rental = MongoDatabase.Rentals.Find(Builders<BsonDocument>.Filter.Eq("bookingId", id)).FirstOrDefault();
            if (rental == null) return null;

            var bookingIdValue = rental.GetValue("bookingId", rental.GetValue("_id", BsonNull.Value));
            var booking = FindBookingById(bookingIdValue);
            if (booking == null || !IsParticipant(booking, userId)) return null;
            return MapLateFee(RefreshLateFeeEstimate(booking, rental), rental, booking);
        }

        public static List<PaymentRecordViewModel> GetHistory(ObjectId userId)
        {
            EnsureIndexes();
            var filter = Builders<BsonDocument>.Filter.Eq("payerId", userId) | Builders<BsonDocument>.Filter.Eq("ownerId", userId) |
                         Builders<BsonDocument>.Filter.Eq("payerId", userId.ToString()) | Builders<BsonDocument>.Filter.Eq("ownerId", userId.ToString());
            var docs = MongoDatabase.Payments.Find(filter).ToList();

            // Legacy payment records may not have ownerId yet. Include them through their booking relationship.
            var ownedBookingIds = MongoDatabase.Bookings.Find(Builders<BsonDocument>.Filter.Eq("ownerId", userId))
                .ToList().Select(x => x.GetValue("_id", BsonNull.Value)).Where(x => x.IsObjectId).Select(x => x.AsObjectId).ToList();
            if (ownedBookingIds.Count > 0)
            {
                var legacy = MongoDatabase.Payments.Find(Builders<BsonDocument>.Filter.In("bookingId", ownedBookingIds)).ToList();
                var known = new HashSet<string>(docs.Select(x => x.GetValue("_id", BsonNull.Value).ToString()), StringComparer.OrdinalIgnoreCase);
                docs.AddRange(legacy.Where(x => !known.Contains(x.GetValue("_id", BsonNull.Value).ToString())));
            }

            return docs.Select(x =>
            {
                var booking = FindBookingById(x.GetValue("bookingId", BsonNull.Value));
                return MapPayment(x, booking, userId);
            }).OrderByDescending(x => x.CreatedAt).ToList();
        }

        public static List<TransactionViewModel> GetAdminHistory()
        {
            EnsureIndexes();
            return MongoDatabase.Payments.Find(FilterDefinition<BsonDocument>.Empty).ToList()
                .OrderByDescending(x => ReadDate(x, "paymentSubmittedAt") ?? ReadDate(x, "createdAt") ?? DateTime.MinValue)
                .Select(MapAdminPayment).ToList();
        }

        public static TransactionViewModel GetAdminPaymentDetails(string paymentId)
        {
            EnsureIndexes();
            ObjectId id;
            if (!ObjectId.TryParse(paymentId, out id)) return null;
            var payment = MongoDatabase.Payments.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstOrDefault();
            return payment == null ? null : MapAdminPayment(payment);
        }

        private static TransactionViewModel MapAdminPayment(BsonDocument payment)
        {
            var booking = FindBookingById(payment.GetValue("bookingId", BsonNull.Value));
            var payerId = ReadId(payment, "payerId");
            var ownerId = ReadId(payment, "ownerId");
            if (booking != null)
            {
                if (payerId == ObjectId.Empty) payerId = ReadId(booking, "borrowerId", "renterId");
                if (ownerId == ObjectId.Empty) ownerId = ReadId(booking, "ownerId");
            }
            var borrower = payerId == ObjectId.Empty ? null : MongoDatabase.Users.Find(x => x.Id == payerId).FirstOrDefault();
            var owner = ownerId == ObjectId.Empty ? null : MongoDatabase.Users.Find(x => x.Id == ownerId).FirstOrDefault();
            var amount = ReadMoney(payment, "amount");
            var chargeType = NormalizeChargeType(payment.GetValue("chargeType", "RENTAL_FEE").ToString());
            var status = NormalizePaymentStatus(payment.GetValue("status", "UNPAID").ToString());
            var submittedAt = ReadDate(payment, "paymentSubmittedAt") ?? ReadDate(payment, "reportedPaidAt");
            var createdAt = submittedAt ?? ReadDate(payment, "createdAt") ?? DateTime.MinValue;
            var method = NormalizePaymentMethodForDisplay(payment.GetValue("paymentMethod", payment.GetValue("method", "")).ToString());
            var bookingValue = payment.GetValue("bookingId", BsonNull.Value);
            var requestId = booking == null ? bookingValue.ToString() : booking.GetValue("requestId", bookingValue).ToString();
            return new TransactionViewModel
            {
                TransactionId = payment.GetValue("_id", BsonNull.Value).ToString(),
                ChargeType = chargeType,
                BookingId = requestId,
                DateLogged = createdAt == DateTime.MinValue ? "—" : createdAt.ToLocalTime().ToString("MMM dd, yyyy - hh:mm tt", CultureInfo.InvariantCulture),
                DateRange = GetDateRange(createdAt),
                ItemName = booking == null ? "Rental" : ReadItemName(booking),
                Serial = "—",
                BorrowerName = borrower == null ? "Student" : (borrower.FirstName + " " + borrower.LastName).Trim(),
                BorrowerId = borrower == null ? payerId.ToString() : (borrower.StudentIdNumber ?? payerId.ToString()),
                BorrowerCourse = borrower == null ? "" : ((borrower.Program ?? "") + (string.IsNullOrWhiteSpace(borrower.YearLevel) ? "" : " " + borrower.YearLevel)).Trim(),
                OwnerName = owner == null ? "Student" : (owner.FirstName + " " + owner.LastName).Trim(),
                OwnerId = owner == null ? ownerId.ToString() : (owner.StudentIdNumber ?? ownerId.ToString()),
                OwnerCourse = owner == null ? "" : ((owner.Program ?? "") + (string.IsNullOrWhiteSpace(owner.YearLevel) ? "" : " " + owner.YearLevel)).Trim(),
                OwnerRating = "",
                BaseFee = "PHP " + (chargeType == "RENTAL_FEE" ? amount : 0m).ToString("N2", CultureInfo.InvariantCulture),
                LateFee = "PHP " + (chargeType == "LATE_FEE" ? amount : 0m).ToString("N2", CultureInfo.InvariantCulture),
                TotalAmount = "PHP " + amount.ToString("N2", CultureInfo.InvariantCulture),
                Amount = amount,
                GCashRef = method == "GCash" ? payment.GetValue("transactionReference", payment.GetValue("reference", payment.GetValue("transactionId", ""))).ToString() : "",
                Method = string.IsNullOrWhiteSpace(method) ? "—" : method,
                Status = status,
                SubmittedAt = submittedAt.HasValue ? submittedAt.Value.ToLocalTime().ToString("MMM dd, yyyy - hh:mm tt", CultureInfo.InvariantCulture) : "",
                OwnerConfirmedAt = ReadDate(payment, "ownerConfirmedAt")?.ToLocalTime().ToString("MMM dd, yyyy - hh:mm tt", CultureInfo.InvariantCulture) ?? "",
                RefundedAt = ReadDate(payment, "refundedAt")?.ToLocalTime().ToString("MMM dd, yyyy - hh:mm tt", CultureInfo.InvariantCulture) ?? "",
                DisputeReason = payment.GetValue("disputeReason", "").ToString()
            };
        }

        private static string GetDateRange(DateTime date)
        {
            if (date == DateTime.MinValue) return "";
            var today = DateTime.Today;
            var localDate = date.ToLocalTime().Date;
            if (localDate == today) return "Today";
            if (localDate >= today.AddDays(-7)) return "Last 7 Days";
            if (localDate >= today.AddDays(-30)) return "Last 30 Days";
            return "All Time";
        }

        public static PaymentRecordViewModel SubmitPayment(ObjectId borrowerId, PaymentSubmissionViewModel input)
        {
            EnsureIndexes();
            if (input == null) throw new PaymentWorkflowException("Payment details are required.");
            var booking = GetParticipantBooking(input.BookingId, borrowerId);
            if (booking == null || ReadId(booking, "borrowerId", "renterId") != borrowerId)
                throw new PaymentWorkflowException("Only the borrower can submit a payment for this booking.");
            if (ClosedBookingStatuses.Contains(booking.GetValue("status", "").ToString().ToUpperInvariant()))
                throw new PaymentWorkflowException("This booking is closed and cannot receive a payment.");

            var chargeType = NormalizeChargeType(input.ChargeType);
            var bookingStatus = booking.GetValue("status", "").ToString().ToUpperInvariant();
            if (chargeType == "RENTAL_FEE" && !new[] { "ACCEPTED", "CONFIRMED", "ACTIVE", "ONGOING", "OVERDUE", "RETURN_REQUESTED", "RETURN PENDING", "COMPLETED", "COMPLETE", "RETURNED" }.Contains(bookingStatus))
                throw new PaymentWorkflowException("Rental payment can be submitted after the owner accepts the request.");
            var paymentMethod = NormalizePaymentMethod(input.PaymentMethod);
            var amount = GetChargeAmount(booking, chargeType);
            if (amount < 0.01m) throw new PaymentWorkflowException("There is no amount due for this charge.");

            var bookingId = booking.GetValue("_id", BsonNull.Value);
            var existing = FindPayment(bookingId, chargeType);
            if (existing != null && NormalizePaymentStatus(existing.GetValue("status", "UNPAID").ToString()) != "UNPAID")
                throw new PaymentWorkflowException("This payment can no longer be changed.");

            var now = DateTime.UtcNow;
            var paymentId = existing == null ? ObjectId.GenerateNewId() : ReadObjectId(existing, "_id");
            var payment = new BsonDocument
            {
                { "_id", paymentId }, { "bookingId", bookingId },
                { "payerId", ReadId(booking, "borrowerId", "renterId") }, { "ownerId", ReadId(booking, "ownerId") },
                { "chargeType", chargeType }, { "amount", (double)amount }, { "currency", "PHP" },
                { "paymentMethod", paymentMethod }, { "transactionReference", paymentMethod == "GCASH" ? (input.TransactionReference ?? "").Trim() : "" },
                { "status", "UNPAID" }, { "paymentSubmittedAt", now }, { "ownerConfirmedBy", BsonNull.Value },
                { "ownerConfirmedAt", BsonNull.Value }, { "paidAt", BsonNull.Value }, { "updatedAt", now }
            };
            if (existing == null)
            {
                payment["createdAt"] = now;
                MongoDatabase.Payments.InsertOne(payment);
            }
            else
            {
                payment["createdAt"] = existing.GetValue("createdAt", BsonDateTime.Create(now));
                var existingStatus = existing.GetValue("status", "UNPAID").ToString();
                var replaceFilter = Builders<BsonDocument>.Filter.Eq("_id", paymentId) &
                    Builders<BsonDocument>.Filter.In("status", new[] { "UNPAID", "PENDING", existingStatus });
                if (MongoDatabase.Payments.ReplaceOne(replaceFilter, payment).ModifiedCount != 1)
                    throw new PaymentWorkflowException("Payment changed before the submission could be saved.");
            }
            return MapPayment(payment, booking, borrowerId);
        }

        public static PaymentRecordViewModel ConfirmReceipt(ObjectId ownerId, string paymentId)
        {
            EnsureIndexes();
            var payment = FindPaymentById(paymentId);
            if (payment == null) throw new PaymentWorkflowException("Payment record not found.");
            var booking = FindBookingById(payment.GetValue("bookingId", BsonNull.Value));
            if (booking == null || ReadId(booking, "ownerId") != ownerId) throw new PaymentWorkflowException("Only this booking's owner can confirm receipt.");
            if (NormalizePaymentStatus(payment.GetValue("status", "UNPAID").ToString()) != "UNPAID" || !HasSubmittedPayment(payment))
                throw new PaymentWorkflowException("This payment has not been submitted or is no longer awaiting confirmation.");

            var now = DateTime.UtcNow;
            var filter = Builders<BsonDocument>.Filter.Eq("_id", payment["_id"]) & Builders<BsonDocument>.Filter.In("status", new[] { "UNPAID", "PENDING" });
            var update = Builders<BsonDocument>.Update.Set("status", "PAID").Set("ownerConfirmedBy", ownerId)
                .Set("ownerConfirmedAt", now).Set("paidAt", now).Set("updatedAt", now);
            if (MongoDatabase.Payments.UpdateOne(filter, update).ModifiedCount != 1) throw new PaymentWorkflowException("Payment changed before it could be confirmed.");
            payment["status"] = "PAID"; payment["ownerConfirmedBy"] = ownerId; payment["ownerConfirmedAt"] = now; payment["paidAt"] = now;
            return MapPayment(payment, booking, ownerId);
        }

        public static PaymentRecordViewModel DisputePayment(ObjectId payerId, PaymentActionViewModel input)
        {
            EnsureIndexes();
            var payment = FindPaymentById(input == null ? null : input.PaymentId);
            if (payment == null) throw new PaymentWorkflowException("Payment record not found.");
            var booking = FindBookingById(payment.GetValue("bookingId", BsonNull.Value));
            if (booking == null || ReadId(booking, "borrowerId", "renterId") != payerId) throw new PaymentWorkflowException("Only the borrower can dispute this payment.");
            var status = NormalizePaymentStatus(payment.GetValue("status", "UNPAID").ToString());
            if (status != "UNPAID" && status != "PAID") throw new PaymentWorkflowException("This payment cannot be disputed in its current status.");
            if (status == "UNPAID" && !HasSubmittedPayment(payment)) throw new PaymentWorkflowException("Submit the payment details before disputing this payment.");
            if (input == null || string.IsNullOrWhiteSpace(input.Reason)) throw new PaymentWorkflowException("Add a reason for the dispute.");
            var now = DateTime.UtcNow;
            var filter = Builders<BsonDocument>.Filter.Eq("_id", payment["_id"]) & Builders<BsonDocument>.Filter.In("status", new[] { "UNPAID", "PENDING", "PAID" });
            var update = Builders<BsonDocument>.Update.Set("status", "DISPUTED").Set("disputedBy", payerId)
                .Set("disputeReason", input.Reason.Trim()).Set("disputedAt", now).Set("updatedAt", now);
            if (MongoDatabase.Payments.UpdateOne(filter, update).ModifiedCount != 1) throw new PaymentWorkflowException("Payment changed before the dispute could be saved.");
            payment["status"] = "DISPUTED"; payment["disputedAt"] = now;
            return MapPayment(payment, booking, payerId);
        }

        public static PaymentRecordViewModel RefundPayment(ObjectId adminId, PaymentActionViewModel input)
        {
            EnsureIndexes();
            var payment = FindPaymentById(input == null ? null : input.PaymentId);
            if (payment == null) throw new PaymentWorkflowException("Payment record not found.");
            var status = NormalizePaymentStatus(payment.GetValue("status", "UNPAID").ToString());
            if (status != "PAID" && status != "DISPUTED") throw new PaymentWorkflowException("Only paid or disputed payments can be refunded.");
            if (input == null || string.IsNullOrWhiteSpace(input.Reason)) throw new PaymentWorkflowException("Add a reason for the refund.");
            var booking = FindBookingById(payment.GetValue("bookingId", BsonNull.Value));
            var now = DateTime.UtcNow;
            var update = Builders<BsonDocument>.Update.Set("status", "REFUNDED").Set("refundedBy", adminId)
                .Set("refundReason", input.Reason.Trim()).Set("refundedAt", now).Set("updatedAt", now);
            if (MongoDatabase.Payments.UpdateOne(Builders<BsonDocument>.Filter.Eq("_id", payment["_id"]) & Builders<BsonDocument>.Filter.In("status", new[] { "PAID", "DISPUTED" }), update).ModifiedCount != 1)
                throw new PaymentWorkflowException("Payment changed before the refund could be saved.");
            payment["status"] = "REFUNDED"; payment["refundedAt"] = now;
            return MapPayment(payment, booking, adminId);
        }

        public static LateFeeDetailsViewModel RefreshLateFee(string rentalId, ObjectId userId)
        {
            var details = GetLateFeeDetails(rentalId, userId);
            if (details == null) throw new PaymentWorkflowException("Rental not found or you do not have access to its fee.");
            return details;
        }

        public static LateFeeDetailsViewModel FinalizeLateFee(ObjectId ownerId, string rentalId, decimal? amountOverride)
        {
            EnsureIndexes();
            ObjectId id;
            if (!ObjectId.TryParse(rentalId, out id)) throw new PaymentWorkflowException("Rental ID is invalid.");
            var rental = MongoDatabase.Rentals.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstOrDefault();
            if (rental == null) rental = MongoDatabase.Rentals.Find(Builders<BsonDocument>.Filter.Eq("bookingId", id)).FirstOrDefault();
            if (rental == null) throw new PaymentWorkflowException("Rental not found.");
            var bookingIdValue = rental.GetValue("bookingId", rental.GetValue("_id", BsonNull.Value));
            var booking = FindBookingById(bookingIdValue);
            if (booking == null || ReadId(booking, "ownerId") != ownerId) throw new PaymentWorkflowException("Only the item owner can finalize this late fee.");

            var returnedAt = ReadDate(rental, "actualReturnDate") ?? ReadDate(rental, "returnedAt") ?? ReadDate(booking, "actualReturnDate") ?? ReadDate(booking, "returnedAt") ?? ReadReturnDate(rental);
            if (!returnedAt.HasValue) throw new PaymentWorkflowException("Confirm the item return before finalizing its late fee.");
            if (amountOverride.HasValue && amountOverride.Value < 0) throw new PaymentWorkflowException("The finalized late fee cannot be negative.");

            var calculation = CalculateLateFee(booking, rental, returnedAt.Value);
            var finalAmount = amountOverride.HasValue ? decimal.Round(amountOverride.Value, 2, MidpointRounding.AwayFromZero) : calculation.Amount;
            var existingPayment = FindPayment(bookingIdValue, "LATE_FEE");
            if (existingPayment != null)
            {
                var existingPaymentStatus = NormalizePaymentStatus(existingPayment.GetValue("status", "UNPAID").ToString());
                var hasSubmitted = HasSubmittedPayment(existingPayment);
                var existingAmount = ReadMoney(existingPayment, "amount");
                if ((existingPaymentStatus != "UNPAID" || hasSubmitted) && existingAmount != finalAmount)
                    throw new PaymentWorkflowException("A late fee cannot be changed after its payment has been submitted or confirmed.");
            }
            var fee = UpsertLateFee(booking, rental, calculation, returnedAt.Value, finalAmount, ownerId);
            if (existingPayment != null && !HasSubmittedPayment(existingPayment) && NormalizePaymentStatus(existingPayment.GetValue("status", "UNPAID").ToString()) == "UNPAID")
                MongoDatabase.Payments.UpdateOne(Builders<BsonDocument>.Filter.Eq("_id", existingPayment["_id"]), Builders<BsonDocument>.Update.Set("amount", (double)finalAmount).Set("updatedAt", DateTime.UtcNow));
            return MapLateFee(fee, rental, booking);
        }

        public static void FinalizeForConfirmedReturn(ObjectId bookingId, ObjectId ownerId, DateTime returnedAt)
        {
            EnsureIndexes();
            var booking = FindBookingById(bookingId);
            if (booking == null || ReadId(booking, "ownerId") != ownerId) return;
            var rental = GetRentalForBooking(bookingId);
            if (rental == null) return;
            var calculation = CalculateLateFee(booking, rental, returnedAt);
            UpsertLateFee(booking, rental, calculation, returnedAt, calculation.Amount, ownerId);
        }

        private static void EnsureIndexes()
        {
            if (_indexesReady) return;
            lock (IndexLock)
            {
                if (_indexesReady) return;
                CreateIndexes();
                _indexesReady = true;
            }
        }

        private static void CreateIndexes()
        {
            MongoDatabase.Payments.Indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("bookingId").Ascending("chargeType"),
                new CreateIndexOptions { Unique = true }));
            MongoDatabase.Payments.Indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("payerId").Descending("createdAt")));
            MongoDatabase.Payments.Indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("ownerId").Descending("createdAt")));
            MongoDatabase.LateFees.Indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("rentalId"), new CreateIndexOptions { Unique = true }));
            MongoDatabase.LateFees.Indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("bookingId")));
        }

        private static BsonDocument RefreshLateFeeEstimate(BsonDocument booking, BsonDocument rental)
        {
            var rentalId = rental.GetValue("_id", BsonNull.Value);
            var existing = MongoDatabase.LateFees.Find(Builders<BsonDocument>.Filter.Eq("rentalId", rentalId)).FirstOrDefault();
            if (existing != null && string.Equals(existing.GetValue("status", "").ToString(), "FINALIZED", StringComparison.OrdinalIgnoreCase)) return existing;
            var now = DateTime.UtcNow;
            var returnedAt = ReadDate(rental, "actualReturnDate") ?? ReadDate(rental, "returnedAt") ?? ReadDate(booking, "actualReturnDate") ?? ReadDate(booking, "returnedAt") ?? ReadReturnDate(rental);
            var calculation = CalculateLateFee(booking, rental, returnedAt ?? now);
            return UpsertLateFee(booking, rental, calculation, returnedAt, null, null);
        }

        private static BsonDocument UpsertLateFee(BsonDocument booking, BsonDocument rental, LateFeeCalculation calculation, DateTime? returnedAt, decimal? finalAmount, ObjectId? finalizedBy)
        {
            var now = DateTime.UtcNow;
            var rentalId = rental.GetValue("_id", BsonNull.Value);
            var bookingId = booking.GetValue("_id", BsonNull.Value);
            var existing = MongoDatabase.LateFees.Find(Builders<BsonDocument>.Filter.Eq("rentalId", rentalId)).FirstOrDefault();
            var fee = new BsonDocument
            {
                { "_id", existing == null ? (BsonValue)ObjectId.GenerateNewId() : existing.GetValue("_id", ObjectId.GenerateNewId()) },
                { "rentalId", rentalId }, { "bookingId", bookingId },
                { "borrowerId", ReadId(booking, "borrowerId", "renterId") }, { "ownerId", ReadId(booking, "ownerId") },
                { "dueAt", calculation.DueAt }, { "returnedAt", returnedAt.HasValue ? (BsonValue)BsonDateTime.Create(returnedAt.Value) : BsonNull.Value },
                { "gracePeriodMinutes", calculation.GracePeriodMinutes }, { "hourlyRate", (double)calculation.HourlyRate },
                { "billableHours", calculation.BillableHours }, { "estimatedAmount", (double)calculation.Amount },
                { "estimatedThrough", calculation.CalculatedThrough }, { "estimatedAt", now },
                { "finalizedAmount", finalAmount.HasValue ? (BsonValue)new BsonDouble((double)finalAmount.Value) : BsonNull.Value },
                { "finalizedAt", finalAmount.HasValue ? (BsonValue)BsonDateTime.Create(now) : BsonNull.Value },
                { "finalizedBy", finalizedBy.HasValue ? (BsonValue)finalizedBy.Value : BsonNull.Value },
                { "status", finalAmount.HasValue ? "FINALIZED" : "ESTIMATED" },
                { "createdAt", existing == null ? (BsonValue)BsonDateTime.Create(now) : existing.GetValue("createdAt", BsonDateTime.Create(now)) },
                { "updatedAt", now }
            };
            if (existing == null)
            {
                try { MongoDatabase.LateFees.InsertOne(fee); }
                catch (MongoWriteException ex) when (ex.WriteError != null && ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
                {
                    existing = MongoDatabase.LateFees.Find(Builders<BsonDocument>.Filter.Eq("rentalId", rentalId)).FirstOrDefault();
                    if (existing == null) throw;
                    if (!finalAmount.HasValue && string.Equals(existing.GetValue("status", "").ToString(), "FINALIZED", StringComparison.OrdinalIgnoreCase)) return existing;
                    fee["_id"] = existing["_id"];
                    fee["createdAt"] = existing.GetValue("createdAt", BsonDateTime.Create(now));
                    MongoDatabase.LateFees.ReplaceOne(Builders<BsonDocument>.Filter.Eq("_id", existing["_id"]), fee);
                }
            }
            else if (finalAmount.HasValue)
            {
                MongoDatabase.LateFees.ReplaceOne(Builders<BsonDocument>.Filter.Eq("_id", existing["_id"]), fee);
            }
            else
            {
                var filter = Builders<BsonDocument>.Filter.Eq("_id", existing["_id"]) & Builders<BsonDocument>.Filter.Ne("status", "FINALIZED");
                var result = MongoDatabase.LateFees.ReplaceOne(filter, fee);
                if (result.ModifiedCount != 1)
                {
                    var latest = MongoDatabase.LateFees.Find(Builders<BsonDocument>.Filter.Eq("rentalId", rentalId)).FirstOrDefault();
                    if (latest != null) return latest;
                }
            }
            return fee;
        }

        private static LateFeeCalculation CalculateLateFee(BsonDocument booking, BsonDocument rental, DateTime through)
        {
            var dueAt = ReadDate(rental, "expectedReturnAt") ?? ReadDate(rental, "expectedReturnDate") ?? ReadDate(rental, "dueDate") ?? ReadDate(booking, "expectedReturnAt") ?? ReadDate(booking, "expectedReturnDate") ?? ReadDate(booking, "endDate");
            var hours = ReadNumber(rental, "durationHours", ReadNumber(booking, "durationHours", 0));
            var start = ReadDate(rental, "handoverAt") ?? ReadDate(booking, "handoverAt") ?? ReadDate(booking, "handoverConfirmedAt") ?? ReadDate(rental, "startDate") ?? ReadDate(rental, "startedAt") ?? ReadDate(booking, "handoverDate") ?? ReadDate(booking, "startDate");
            if (!dueAt.HasValue && start.HasValue)
            {
                if (hours <= 0) hours = 1;
                dueAt = start.Value.AddHours(hours);
            }
            if (hours <= 0 && start.HasValue && dueAt.HasValue) hours = Math.Max(1d, (dueAt.Value - start.Value).TotalHours);
            if (!dueAt.HasValue) dueAt = through;
            var grace = Math.Max(0, ReadInt(rental, "lateFeeGracePeriodMinutes", ReadInt(booking, "lateFeeGracePeriodMinutes", DefaultGracePeriodMinutes)));
            var rate = ReadMoney(rental, "lateFeeHourlyRate", ReadMoney(booking, "lateFeeHourlyRate", ReadMoney(rental, "hourlyRate", ReadMoney(booking, "hourlyRate", 0m))));
            if (rate <= 0)
            {
                var duration = hours > 0 ? (decimal)hours : 0m;
                var rentalAmount = GetRentalFee(booking);
                if (duration > 0) rate = rentalAmount / duration;
            }
            var billableMilliseconds = Math.Max(0d, (through.ToUniversalTime() - dueAt.Value.ToUniversalTime()).TotalMilliseconds - grace * 60000d);
            var billableHours = (int)Math.Ceiling(billableMilliseconds / 3600000d);
            var amount = decimal.Round(rate * billableHours, 2, MidpointRounding.AwayFromZero);
            return new LateFeeCalculation { DueAt = dueAt.Value, CalculatedThrough = through.ToUniversalTime(), GracePeriodMinutes = grace, HourlyRate = rate, BillableHours = billableHours, Amount = amount };
        }

        private static PaymentRecordViewModel MapPayment(BsonDocument payment, BsonDocument booking, ObjectId viewerId)
        {
            var status = NormalizePaymentStatus(payment.GetValue("status", "UNPAID").ToString());
            var payerId = ReadId(payment, "payerId");
            var ownerId = ReadId(payment, "ownerId");
            if (booking != null)
            {
                if (payerId == ObjectId.Empty) payerId = ReadId(booking, "borrowerId", "renterId");
                if (ownerId == ObjectId.Empty) ownerId = ReadId(booking, "ownerId");
            }
            var otherId = viewerId == payerId ? ownerId : payerId;
            return new PaymentRecordViewModel
            {
                Id = payment.GetValue("_id", BsonNull.Value).ToString(),
                BookingId = payment.GetValue("bookingId", BsonNull.Value).ToString(),
                ChargeType = NormalizeChargeType(payment.GetValue("chargeType", "RENTAL_FEE").ToString()),
                Amount = ReadMoney(payment, "amount"), Currency = payment.GetValue("currency", "PHP").ToString(),
                PaymentMethod = NormalizePaymentMethodForDisplay(payment.GetValue("paymentMethod", payment.GetValue("method", "" )).ToString()),
                TransactionReference = payment.GetValue("transactionReference", payment.GetValue("reference", payment.GetValue("transactionId", ""))).ToString(), Status = status,
                CreatedAt = ReadDate(payment, "createdAt") ?? DateTime.MinValue,
                SubmittedAt = ReadDate(payment, "paymentSubmittedAt") ?? ReadDate(payment, "reportedPaidAt"),
                OwnerConfirmedAt = ReadDate(payment, "ownerConfirmedAt"), PaidAt = ReadDate(payment, "paidAt"),
                DisputedAt = ReadDate(payment, "disputedAt"), RefundedAt = ReadDate(payment, "refundedAt"),
                ItemName = booking == null ? "Item" : ReadItemName(booking), OtherPartyName = ReadUserName(otherId)
            };
        }

        private static LateFeeDetailsViewModel MapLateFee(BsonDocument fee, BsonDocument rental, BsonDocument booking)
        {
            return new LateFeeDetailsViewModel
            {
                Id = fee.GetValue("_id", BsonNull.Value).ToString(),
                RentalId = fee.GetValue("rentalId", rental.GetValue("_id", BsonNull.Value)).ToString(),
                BookingId = fee.GetValue("bookingId", booking.GetValue("_id", BsonNull.Value)).ToString(),
                Status = fee.GetValue("status", "ESTIMATED").ToString(),
                DueAt = ReadDate(fee, "dueAt") ?? DateTime.MinValue, ReturnedAt = ReadDate(fee, "returnedAt"),
                GracePeriodMinutes = ReadInt(fee, "gracePeriodMinutes", DefaultGracePeriodMinutes),
                HourlyRate = ReadMoney(fee, "hourlyRate"), BillableHours = ReadInt(fee, "billableHours", 0),
                EstimatedAmount = ReadMoney(fee, "estimatedAmount"), EstimatedThrough = ReadDate(fee, "estimatedThrough") ?? DateTime.UtcNow,
                FinalizedAmount = ReadNullableMoney(fee, "finalizedAmount"), FinalizedAt = ReadDate(fee, "finalizedAt")
            };
        }

        private static BsonDocument GetParticipantBooking(string bookingId, ObjectId userId)
        {
            ObjectId id;
            if (!ObjectId.TryParse(bookingId, out id)) return null;
            var filter = Builders<BsonDocument>.Filter.Eq("_id", id) &
                         (Builders<BsonDocument>.Filter.Eq("borrowerId", userId) | Builders<BsonDocument>.Filter.Eq("ownerId", userId) |
                          Builders<BsonDocument>.Filter.Eq("renterId", userId.ToString()) | Builders<BsonDocument>.Filter.Eq("borrowerId", userId.ToString()) |
                          Builders<BsonDocument>.Filter.Eq("ownerId", userId.ToString()));
            return MongoDatabase.Bookings.Find(filter).FirstOrDefault();
        }

        private static bool IsParticipant(BsonDocument booking, ObjectId userId)
        {
            return ReadId(booking, "ownerId") == userId || ReadId(booking, "borrowerId", "renterId") == userId;
        }

        private static BsonDocument FindBookingById(BsonValue value)
        {
            if (value == null || value == BsonNull.Value) return null;
            ObjectId id;
            var filter = value.IsObjectId ? Builders<BsonDocument>.Filter.Eq("_id", value.AsObjectId) :
                ObjectId.TryParse(value.ToString(), out id) ? Builders<BsonDocument>.Filter.Eq("_id", id) : null;
            return filter == null ? null : MongoDatabase.Bookings.Find(filter).FirstOrDefault();
        }

        private static BsonDocument GetRentalForBooking(BsonValue bookingId)
        {
            if (bookingId == null || bookingId == BsonNull.Value) return null;
            return MongoDatabase.Rentals.Find(Builders<BsonDocument>.Filter.Eq("bookingId", bookingId)).FirstOrDefault();
        }

        private static DateTime? ReadReturnDate(BsonDocument rental)
        {
            var rentalId = rental.GetValue("_id", BsonNull.Value);
            if (rentalId == BsonNull.Value) return null;
            var returned = MongoDatabase.Returns.Find(Builders<BsonDocument>.Filter.Eq("rentalId", rentalId))
                .SortByDescending(x => x["createdAt"]).FirstOrDefault();
            return returned == null ? null : ReadDate(returned, "actualReturnDate") ?? ReadDate(returned, "returnDate") ?? ReadDate(returned, "returnedAt");
        }

        private static BsonDocument FindPayment(BsonValue bookingId, string chargeType)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("bookingId", bookingId) & Builders<BsonDocument>.Filter.Eq("chargeType", chargeType);
            var found = MongoDatabase.Payments.Find(filter).ToList().OrderByDescending(x => ReadDate(x, "createdAt") ?? DateTime.MinValue).FirstOrDefault();
            if (found == null && chargeType == "RENTAL_FEE")
                found = MongoDatabase.Payments.Find(Builders<BsonDocument>.Filter.Eq("bookingId", bookingId) & Builders<BsonDocument>.Filter.Exists("chargeType", false)).FirstOrDefault();
            return found;
        }

        private static BsonDocument FindPaymentById(string id)
        {
            ObjectId objectId;
            return ObjectId.TryParse(id, out objectId) ? MongoDatabase.Payments.Find(Builders<BsonDocument>.Filter.Eq("_id", objectId)).FirstOrDefault() : null;
        }

        private static decimal GetChargeAmount(BsonDocument booking, string chargeType)
        {
            if (chargeType == "RENTAL_FEE") return GetRentalFee(booking);
            var rental = GetRentalForBooking(booking.GetValue("_id", BsonNull.Value));
            if (rental == null) throw new PaymentWorkflowException("No rental record exists for the late fee.");
            var fee = MongoDatabase.LateFees.Find(Builders<BsonDocument>.Filter.Eq("rentalId", rental.GetValue("_id", BsonNull.Value))).FirstOrDefault();
            if (fee == null || !string.Equals(fee.GetValue("status", "").ToString(), "FINALIZED", StringComparison.OrdinalIgnoreCase))
                throw new PaymentWorkflowException("The owner must finalize the late fee before it can be paid.");
            return ReadNullableMoney(fee, "finalizedAmount") ?? 0m;
        }

        private static decimal GetRentalFee(BsonDocument booking)
        {
            var amount = ReadMoney(booking, "rentalFeeAmount", ReadMoney(booking, "estimatedFee", ReadMoney(booking, "totalAmount", ReadMoney(booking, "rentalFee", 0m))));
            if (amount > 0) return amount;
            var savedPayment = FindPayment(booking.GetValue("_id", BsonNull.Value), "RENTAL_FEE");
            if (savedPayment != null && ReadMoney(savedPayment, "amount") > 0) return ReadMoney(savedPayment, "amount");
            var hours = ReadNumber(booking, "durationHours", 0);
            var rate = ReadMoney(booking, "hourlyRate", 0m);
            if (hours <= 0)
            {
                var start = ReadDate(booking, "startDate") ?? ReadDate(booking, "handoverDate");
                var end = ReadDate(booking, "endDate");
                hours = start.HasValue && end.HasValue ? Math.Max(1d, (end.Value - start.Value).TotalHours) : 1d;
            }
            if (rate <= 0)
            {
                var itemId = booking.GetValue("itemId", BsonNull.Value);
                if (itemId.IsObjectId)
                {
                    var item = MongoDatabase.Items.Find(x => x.Id == itemId.AsObjectId).FirstOrDefault();
                    if (item != null && item.RentalDetails != null) rate = (decimal)(item.RentalDetails.HourlyRate > 0 ? item.RentalDetails.HourlyRate : item.RentalDetails.PricePerDay / 24d);
                }
            }
            return decimal.Round(rate * (decimal)hours, 2, MidpointRounding.AwayFromZero);
        }

        private static string ReadItemName(BsonDocument booking)
        {
            var itemId = booking.GetValue("itemId", BsonNull.Value);
            if (itemId.IsObjectId)
            {
                var item = MongoDatabase.Items.Find(x => x.Id == itemId.AsObjectId).FirstOrDefault();
                if (item != null) return item.Name;
            }
            return booking.GetValue("itemName", "Item").ToString();
        }

        private static string ReadUserName(ObjectId id)
        {
            if (id == ObjectId.Empty) return "Student";
            var user = MongoDatabase.Users.Find(x => x.Id == id).FirstOrDefault();
            return user == null ? "Student" : (user.FirstName + " " + user.LastName).Trim();
        }

        private static bool HasSubmittedPayment(BsonDocument payment)
        {
            return ReadDate(payment, "paymentSubmittedAt").HasValue || ReadDate(payment, "reportedPaidAt").HasValue;
        }

        private static string NormalizeChargeType(string value)
        {
            var normalized = (value ?? "").Trim().Replace('-', '_').Replace(' ', '_').ToUpperInvariant();
            if (normalized == "RENTAL" || normalized == "RENT" || normalized == "RENTAL_FEE") return "RENTAL_FEE";
            if (normalized == "LATE" || normalized == "LATE_FEE") return "LATE_FEE";
            throw new PaymentWorkflowException("Choose a rental fee or late fee payment.");
        }

        private static string NormalizePaymentMethod(string value)
        {
            var normalized = (value ?? "").Trim().Replace(" ", "").ToUpperInvariant();
            if (normalized == "CASH" || normalized == "GCASH") return normalized;
            throw new PaymentWorkflowException("Choose Cash or GCash.");
        }

        private static string NormalizePaymentMethodForDisplay(string value)
        {
            var normalized = (value ?? "").Trim().ToUpperInvariant();
            return normalized == "GCASH" ? "GCash" : normalized == "CASH" ? "Cash" : value;
        }

        private static string NormalizePaymentStatus(string value)
        {
            var status = (value ?? "UNPAID").Trim().ToUpperInvariant();
            return status == "PENDING" || status == "PENDING_CONFIRMATION" ? "UNPAID" : status;
        }

        private static ObjectId ReadId(BsonDocument doc, params string[] keys)
        {
            foreach (var key in keys)
            {
                var value = doc.GetValue(key, BsonNull.Value);
                if (value.IsObjectId) return value.AsObjectId;
                ObjectId id;
                if (value != BsonNull.Value && ObjectId.TryParse(value.ToString(), out id)) return id;
            }
            return ObjectId.Empty;
        }

        private static ObjectId ReadObjectId(BsonDocument doc, string key)
        {
            var value = doc.GetValue(key, BsonNull.Value);
            if (value.IsObjectId) return value.AsObjectId;
            ObjectId parsed; return ObjectId.TryParse(value.ToString(), out parsed) ? parsed : ObjectId.GenerateNewId();
        }

        private static DateTime? ReadDate(BsonDocument doc, string key)
        {
            var value = doc.GetValue(key, BsonNull.Value);
            if (value.IsValidDateTime) return value.ToUniversalTime();
            DateTime parsed;
            return value != BsonNull.Value && DateTime.TryParse(value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out parsed) ? parsed.ToUniversalTime() : (DateTime?)null;
        }

        private static decimal ReadMoney(BsonDocument doc, string key, decimal fallback = 0m)
        {
            var value = doc.GetValue(key, BsonNull.Value);
            return value.IsNumeric ? value.ToDecimal() : fallback;
        }

        private static decimal? ReadNullableMoney(BsonDocument doc, string key)
        {
            var value = doc.GetValue(key, BsonNull.Value);
            return value.IsNumeric ? (decimal?)value.ToDecimal() : null;
        }

        private static double ReadNumber(BsonDocument doc, string key, double fallback)
        {
            var value = doc.GetValue(key, BsonNull.Value);
            return value.IsNumeric ? value.ToDouble() : fallback;
        }

        private static int ReadInt(BsonDocument doc, string key, int fallback)
        {
            var value = doc.GetValue(key, BsonNull.Value);
            return value.IsNumeric ? value.ToInt32() : fallback;
        }

        private sealed class LateFeeCalculation
        {
            public DateTime DueAt { get; set; }
            public DateTime CalculatedThrough { get; set; }
            public int GracePeriodMinutes { get; set; }
            public decimal HourlyRate { get; set; }
            public int BillableHours { get; set; }
            public decimal Amount { get; set; }
        }
    }

    public class PaymentWorkflowException : Exception
    {
        public PaymentWorkflowException(string message) : base(message) { }
    }
}
