using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using UniShare.Services;
using UniShare.Models;

namespace UniShare.Controllers
{
    [Authorize]
    public class MyRentalsController : Controller
    {
        // GET: /MyRentals or /MyRentals/Index
        [HttpGet]
        public ActionResult Index()
        {
            ViewBag.Title = "Rentals";
            ObjectId userId;
            if (!ObjectId.TryParse(User.Identity.Name, out userId)) return new HttpStatusCodeResult(401);
            var model = new MyRentalsViewModel
            {
                Rentals = BookingService.ForRenter(userId).Select(x => MapBooking(x, userId))
                    .Concat(BookingService.ForOwner(userId)
                    .Where(x => x.GetValue("borrowerId", BsonNull.Value).ToString() != userId.ToString())
                    .Select(x => MapBooking(x, userId)))
                    .OrderByDescending(x => x.RequestDate).ToList(),
                IncomingRequests = BookingService.ForOwner(userId)
                    .Where(x => new[] { "PENDING", "REQUESTED" }.Contains(x.GetValue("status", "").ToString().Trim().ToUpperInvariant()))
                    .Where(x => x.GetValue("borrowerId", BsonNull.Value).ToString() != userId.ToString())
                    .Select(x => MapBooking(x, userId)).ToList()
            };

            // Summary cards are counted from the records, so they always agree with the table
            // (the page script keeps them in sync when a rental changes status).
            int overdue = model.Rentals.Count(r => r.Status == "Overdue");
            int disputed = model.Rentals.Count(r => r.Status == "Disputed");
            model.Summary = new RentalSummaryDto
            {
                TotalRentals = model.Rentals.Count,
                PendingRequests = model.Rentals.Count(r => r.Status == "Pending"),
                ActiveRentals = model.Rentals.Count(r => r.Status == "Ongoing"),
                NeedAttention = overdue + disputed,
                OverdueCount = overdue,
                DisputedCount = disputed
            };

            return View(model);
        }

        [HttpGet]
        public ActionResult Details(string id)
        {
            ObjectId userId;
            if (!ObjectId.TryParse(User.Identity.Name, out userId)) return new HttpStatusCodeResult(401);
            var booking = BookingService.GetForParticipant(id, userId);
            if (booking == null) return HttpNotFound();
            var model = MapBooking(booking);
            var ownerValue = booking.GetValue("ownerId", BsonNull.Value);
            var isOwner = ownerValue.IsObjectId && ownerValue.AsObjectId == userId;
            var status = booking.GetValue("status", "").ToString().Trim().ToUpperInvariant();
            var isPast = new[] { "COMPLETED", "COMPLETE", "RETURNED", "CANCELLED", "CANCELED", "DECLINED", "REJECTED", "EXPIRED" }.Contains(status);
            model.BackAction = isOwner ? (isPast ? "PastTransactions" : "Ongoing") : "Index";
            model.BackController = isOwner ? "MyListings" : "MyRentals";
            return View(model);
        }

        [HttpGet]
        public ActionResult History()
        {
            ObjectId userId;
            if (!ObjectId.TryParse(User.Identity.Name, out userId)) return new HttpStatusCodeResult(401);
            var history = BookingService.ForRenter(userId).Where(x => new[] { "COMPLETED", "COMPLETE", "DECLINED", "CANCELLED", "CANCELED", "EXPIRED" }.Contains(x.GetValue("status", "").ToString().ToUpperInvariant())).ToList();
            return View(history.Select(MapBooking).ToList());
        }

        [HttpGet]
        public ActionResult HistoryData()
        {
            ObjectId userId;
            if (!ObjectId.TryParse(User.Identity.Name, out userId)) return new HttpStatusCodeResult(401);
            var closed = new[] { "COMPLETED", "COMPLETE", "RETURNED", "DECLINED", "CANCELLED", "CANCELED", "EXPIRED", "REJECTED" };
            var ownerRows = BookingService.ForOwner(userId)
                .Where(x => closed.Contains(x.GetValue("status", "").ToString().Trim().ToUpperInvariant()))
                .Select(x => MapBooking(x, userId))
                .Select(x => new { x.RequestDate, row = new { x.RequestId, x.ItemName, Participant = x.BorrowerName, Role = "Owner", x.RequestDateFormatted, x.Status, x.RentalFeeFormatted } });
            var renterRows = BookingService.ForRenter(userId)
                .Where(x => closed.Contains(x.GetValue("status", "").ToString().Trim().ToUpperInvariant()))
                .Select(x => MapBooking(x, userId))
                .Select(x => new { x.RequestDate, row = new { x.RequestId, x.ItemName, Participant = x.OwnerName, Role = "Borrower", x.RequestDateFormatted, x.Status, x.RentalFeeFormatted } });
            var rows = ownerRows.Concat(renterRows).OrderByDescending(x => x.RequestDate).Select(x => x.row).ToList();
            return Json(rows, JsonRequestBehavior.AllowGet);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ConfirmHandover(string id)
        {
            ObjectId ownerId;
            if (!ObjectId.TryParse(User.Identity.Name, out ownerId)) return Json(new { ok = false, message = "Please sign in again." });
            var saved = BorrowedItemsService.ConfirmHandoverByOwner(ownerId, id);
            var booking = BookingService.GetForParticipant(id, ownerId);
            var handoverValue = booking == null ? BsonNull.Value : booking.GetValue("handoverAt", BsonNull.Value);
            return Json(new { ok = saved, message = saved ? "Your handover confirmation has been saved." : "This handover is no longer awaiting your confirmation.", ownerConfirmed = booking != null && booking.GetValue("ownerHandoverConfirmed", false).ToBoolean(), borrowerConfirmed = booking != null && booking.GetValue("borrowerHandoverConfirmed", booking.GetValue("handoverConfirmed", false)).ToBoolean(), status = booking == null ? "" : booking.GetValue("status", "").ToString(), handoverAt = handoverValue.IsValidDateTime ? handoverValue.ToUniversalTime().ToString("o") : "" });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ConfirmReturn(string id)
        {
            ObjectId ownerId;
            if (!ObjectId.TryParse(User.Identity.Name, out ownerId)) return Json(new { ok = false, message = "Please sign in again." });
            var saved = BorrowedItemsService.ConfirmReturnByOwner(ownerId, id);
            return Json(new { ok = saved, message = saved ? "The return confirmation has been saved." : "This rental is not awaiting your return confirmation." });
        }

        internal static RentalRecordDto MapBooking(BsonDocument booking)
        {
            return MapBooking(booking, ObjectId.Empty);
        }

        private static RentalRecordDto MapBooking(BsonDocument booking, ObjectId viewerId)
        {
            var itemValue = booking.GetValue("itemId", BsonNull.Value);
            var item = itemValue.IsObjectId ? MongoDatabase.Items.Find(x => x.Id == itemValue.AsObjectId).FirstOrDefault() : null;
            var borrowerValue = booking.GetValue("borrowerId", BsonNull.Value);
            var borrower = borrowerValue.IsObjectId ? MongoDatabase.Users.Find(x => x.Id == borrowerValue.AsObjectId).FirstOrDefault() : null;
            var requestedStartValue = booking.GetValue("handoverDate", booking.GetValue("startDate", BsonDateTime.Create(DateTime.UtcNow)));
            var requestedStart = requestedStartValue.IsValidDateTime ? requestedStartValue.ToUniversalTime().ToLocalTime() : DateTime.Now;
            var actualStartValue = booking.GetValue("handoverAt", BsonNull.Value);
            var start = actualStartValue.IsValidDateTime ? actualStartValue.ToUniversalTime().ToLocalTime() : requestedStart;
            var hoursValue = booking.GetValue("durationHours", 1);
            var hours = hoursValue.IsNumeric ? hoursValue.ToInt32() : 1;
            var status = booking.GetValue("status", "PENDING").ToString();
            var feeValue = booking.GetValue("estimatedFee", booking.GetValue("rentalFee", booking.GetValue("totalFee", BsonInt32.Create(0))));
            decimal fee = feeValue.IsNumeric ? feeValue.ToDecimal() : 0m;
            var fullName = borrower == null ? "Student" : (borrower.FirstName + " " + borrower.LastName).Trim();
            return new RentalRecordDto
            {
                BookingObjectId = booking.GetValue("_id", ObjectId.Empty).ToString(),
                CanManageRequest = !viewerId.Equals(ObjectId.Empty) &&
                    booking.GetValue("ownerId", BsonNull.Value).IsObjectId && booking["ownerId"].AsObjectId == viewerId &&
                    booking.GetValue("borrowerId", BsonNull.Value).IsObjectId && booking["borrowerId"].AsObjectId != viewerId,
                RequestId = booking.GetValue("requestId", booking.GetValue("_id", "").ToString()).ToString(),
                ItemName = item == null ? booking.GetValue("itemName", "Item").ToString() : item.Name,
                BorrowerName = fullName, BorrowerInitials = string.Concat(fullName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Take(2).Select(x => char.ToUpperInvariant(x[0]))),
                BorrowerProgram = borrower == null ? "" : borrower.Program,
                RequestDate = booking.GetValue("createdAt", BsonDateTime.Create(DateTime.UtcNow)).ToUniversalTime().ToLocalTime(),
                RequestDateFormatted = booking.GetValue("createdAt", BsonDateTime.Create(DateTime.UtcNow)).ToUniversalTime().ToLocalTime().ToString("MMM d, yyyy"),
                HandoverDate = start, HandoverDateFormatted = start.ToString("MMM d, yyyy"),
                ExpectedReturnDate = start.AddHours(hours), ExpectedReturnFormatted = start.AddHours(hours).ToString("MMM d, yyyy h:mm tt"),
                HandoverTimeFormatted = start.ToString("hh:mm tt"),
                HandoverScheduleFormatted = requestedStart.ToString("MMM d, yyyy") + " · " + booking.GetValue("handoverTime", requestedStart.ToString("hh:mm tt")),
                Duration = hours + " hour" + (hours == 1 ? "" : "s"), DurationHours = hours,
                RentalFee = fee, RentalFeeFormatted = "₱" + fee.ToString("N2"), Status = ToDisplayStatus(status),
                OwnerName = item == null ? "" : (MongoDatabase.Users.Find(x => x.Id == item.OwnerId).FirstOrDefault() is UserDocument owner ? (owner.FirstName + " " + owner.LastName).Trim() : ""),
                RequestNote = booking.GetValue("notes", "").ToString(), Category = item == null ? "" : item.Category,
                BorrowerHandoverConfirmed = booking.GetValue("borrowerHandoverConfirmed", booking.GetValue("handoverConfirmed", false)).ToBoolean() ||
                    new[] { "ONGOING", "ACTIVE", "OVERDUE", "RETURN_REQUESTED", "RETURN PENDING", "COMPLETED", "COMPLETE", "RETURNED", "DISPUTED" }.Contains(status.Trim().ToUpperInvariant()),
                OwnerHandoverConfirmed = booking.GetValue("ownerHandoverConfirmed", false).ToBoolean() ||
                    new[] { "ONGOING", "ACTIVE", "OVERDUE", "RETURN_REQUESTED", "RETURN PENDING", "COMPLETED", "COMPLETE", "RETURNED", "DISPUTED" }.Contains(status.Trim().ToUpperInvariant()),
                BorrowerHandoverConfirmedAt = FormatDate(booking, "borrowerHandoverConfirmedAt"),
                OwnerHandoverConfirmedAt = FormatDate(booking, "ownerHandoverConfirmedAt"),
                DeclineReason = booking.GetValue("declineReason", "").ToString(), DeclineNote = booking.GetValue("declineNotes", "").ToString(),
                DeclinedAtFormatted = FormatDate(booking, "declinedAt"), AcceptedAtFormatted = FormatDate(booking, "acceptedAt")
            };
        }

        private static string FormatDate(BsonDocument booking, string key)
        {
            var value = booking.GetValue(key, BsonNull.Value);
            return value.IsValidDateTime ? value.ToUniversalTime().ToLocalTime().ToString("MMM d, yyyy h:mm tt") : "";
        }

        private static string ToDisplayStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return "Pending";
            var raw = status.Trim().ToUpperInvariant();
            if (raw == "RETURN_REQUESTED" || raw == "RETURN PENDING") return "Returned";
            var normalized = raw.Replace('_', ' ').ToLowerInvariant();
            return char.ToUpperInvariant(normalized[0]) + normalized.Substring(1);
        }

        // Preserve the earlier pending-request cancellation endpoint.
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Cancel(string id)
        {
            ObjectId bookingId, renterId;
            if (!ObjectId.TryParse(id, out bookingId) || !ObjectId.TryParse(User.Identity.Name, out renterId)) return new HttpStatusCodeResult(400);
            var cancelled = BookingService.Cancel(bookingId, renterId);
            TempData[cancelled ? "Success" : "Error"] = cancelled ? "Request cancelled." : "Only your pending request can be cancelled.";
            return RedirectToAction("Index");
        }

    }
}

