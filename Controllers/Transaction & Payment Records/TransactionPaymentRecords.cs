using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using UniShare.Models;
using UniShare.Services;

namespace UniShare.Controllers
{
    [Authorize(Roles = "ADMIN")]
    public class TransactionPaymentRecordsController : Controller
    {
        [HttpGet]
        public ActionResult Index()
        {
            var payments = MongoDatabase.Payments.Find(Builders<BsonDocument>.Filter.Empty)
                .Sort(Builders<BsonDocument>.Sort.Descending("createdAt")).Limit(1000).ToList();
            var rows = payments.Select(ToViewModel).Where(x => x != null).ToList();
            ViewBag.TotalTransactions = rows.Count;
            ViewBag.ConfirmedPayments = rows.Count(x => x.Status == "Confirmed");
            ViewBag.PendingConfirmation = rows.Count(x => x.Status == "Awaiting Owner Confirmation");
            var fees = payments.Sum(x => Number(x, "amount")) + payments.Sum(x => LateFeeFor(x));
            ViewBag.TotalRecordedFees = "PHP " + fees.ToString("N2", CultureInfo.InvariantCulture);
            return View("TransactionPaymentRecords", rows);
        }

        public ActionResult TransactionPaymentRecords() { return Index(); }

        [HttpGet]
        public ActionResult GetTransactionDetails(string id)
        {
            ObjectId paymentId;
            if (!ObjectId.TryParse((id ?? "").Replace("PAY-", ""), out paymentId)) return HttpNotFound();
            var payment = MongoDatabase.Payments.Find(Builders<BsonDocument>.Filter.Eq("_id", paymentId)).FirstOrDefault();
            var model = payment == null ? null : ToViewModel(payment);
            return model == null ? (ActionResult)HttpNotFound() : PartialView("TransactionDetails", model);
        }

        private static TransactionViewModel ToViewModel(BsonDocument payment)
        {
            var bookingId = payment.GetValue("bookingId", BsonNull.Value);
            var booking = bookingId.IsObjectId
                ? MongoDatabase.Bookings.Find(Builders<BsonDocument>.Filter.Eq("_id", bookingId)).FirstOrDefault() : null;
            var borrowerId = payment.GetValue("payerId", booking == null ? BsonNull.Value : booking.GetValue("borrowerId", BsonNull.Value));
            var ownerId = payment.GetValue("ownerId", booking == null ? BsonNull.Value : booking.GetValue("ownerId", BsonNull.Value));
            var borrower = FindUser(borrowerId);
            var owner = FindUser(ownerId);
            var itemValue = booking == null ? BsonNull.Value : booking.GetValue("itemId", BsonNull.Value);
            var item = itemValue.IsObjectId ? MongoDatabase.Items.Find(x => x.Id == itemValue.AsObjectId).FirstOrDefault() : null;
            var lateFee = bookingId.IsObjectId ? MongoDatabase.LateFees.Find(Builders<BsonDocument>.Filter.Eq("bookingId", bookingId)).FirstOrDefault() : null;
            var amount = Number(payment, "amount");
            var lateAmount = lateFee == null ? 0d : Number(lateFee, lateFee.Contains("finalAmount") ? "finalAmount" : "amount");
            var status = payment.GetValue("status", "UNPAID").ToString().ToUpperInvariant();
            var bookingStatus = booking == null ? "" : booking.GetValue("status", "").ToString().ToUpperInvariant();
            var bookingClosed = new[] { "CANCELLED", "CANCELED", "DECLINED", "REJECTED", "EXPIRED" }.Contains(bookingStatus);
            var date = ReadDate(payment, "createdAt") ?? DateTime.UtcNow;
            return new TransactionViewModel
            {
                TransactionId = "PAY-" + payment.GetValue("_id", "").ToString(),
                BookingId = booking == null ? bookingId.ToString() : booking.GetValue("requestId", bookingId.ToString()).ToString(),
                BookingObjectId = bookingId.IsObjectId ? bookingId.AsObjectId.ToString() : "",
                DateLogged = date.ToLocalTime().ToString("MMM dd, yyyy - hh:mm tt", CultureInfo.InvariantCulture),
                DateRange = date.Date == DateTime.Today ? "Today" : date >= DateTime.Today.AddDays(-7) ? "Last 7 Days" : date >= DateTime.Today.AddDays(-30) ? "Last 30 Days" : "",
                ItemName = item == null ? (booking == null ? "Rental item" : booking.GetValue("itemName", "Rental item").ToString()) : item.Name,
                Serial = bookingId.ToString(),
                BorrowerName = FullName(borrower), BorrowerId = borrower == null ? "" : borrower.StudentIdNumber,
                BorrowerCourse = borrower == null ? "" : String.Join(" ", new[] { borrower.Program, borrower.YearLevel }.Where(x => !String.IsNullOrWhiteSpace(x))),
                OwnerName = FullName(owner), OwnerId = owner == null ? "" : owner.StudentIdNumber,
                OwnerCourse = owner == null ? "" : String.Join(" ", new[] { owner.Program, owner.YearLevel }.Where(x => !String.IsNullOrWhiteSpace(x))),
                OwnerRating = "—",
                BaseFee = Money(amount), LateFee = Money(lateAmount), TotalAmount = Money(amount + lateAmount),
                Method = "Cash",
                Status = bookingClosed ? "Cancelled" : status == "PAID" ? "Confirmed" : status == "DISPUTED" ? "Disputed" : status == "REFUNDED" ? "Refunded" : "Awaiting Owner Confirmation",
                RefundAvailable = status == "PAID" || status == "DISPUTED"
            };
        }

        private static UniShare.Models.UserDocument FindUser(BsonValue id)
        {
            return id.IsObjectId ? MongoDatabase.Users.Find(x => x.Id == id.AsObjectId).FirstOrDefault() : null;
        }
        private static string FullName(UniShare.Models.UserDocument user) { return user == null ? "—" : (user.FirstName + " " + user.LastName).Trim(); }
        private static double Number(BsonDocument doc, string field) { var value = doc.GetValue(field, BsonNull.Value); return value.IsNumeric ? value.ToDouble() : 0d; }
        private static double LateFeeFor(BsonDocument payment)
        {
            var bookingId = payment.GetValue("bookingId", BsonNull.Value);
            if (!bookingId.IsObjectId) return 0d;
            var fee = MongoDatabase.LateFees.Find(Builders<BsonDocument>.Filter.Eq("bookingId", bookingId)).FirstOrDefault();
            return fee == null ? 0d : Number(fee, fee.Contains("finalAmount") ? "finalAmount" : "amount");
        }
        private static DateTime? ReadDate(BsonDocument doc, string field)
        {
            var value = doc.GetValue(field, BsonNull.Value);
            return value.IsValidDateTime ? (DateTime?)value.ToUniversalTime() : null;
        }
        private static string Money(double amount) { return "PHP " + amount.ToString("N2", CultureInfo.InvariantCulture); }
    }
}
