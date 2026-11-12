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
    public class RentalSummaryDto
    {
        public int TotalRentals { get; set; }      // every record, whatever its status
        public int PendingRequests { get; set; }   // Pending only
        public int ActiveRentals { get; set; }     // Ongoing only
        public int NeedAttention { get; set; }    // Overdue + Disputed
        public int OverdueCount { get; set; }
        public int DisputedCount { get; set; }
    }

    public class RentalRecordDto
    {
        public string BookingObjectId { get; set; }
        public string RequestId { get; set; }
        public string ItemName { get; set; }
        public string BorrowerName { get; set; }
        public string BorrowerInitials { get; set; }
        public string BorrowerProgram { get; set; }
        public DateTime RequestDate { get; set; }
        public string RequestDateFormatted { get; set; }
        public DateTime HandoverDate { get; set; }
        public string HandoverScheduleFormatted { get; set; }
        public string Duration { get; set; }
        public decimal RentalFee { get; set; }
        public string RentalFeeFormatted { get; set; }
        public string Status { get; set; } // Pending, Accepted, Declined, Ongoing, Overdue, Returned, Disputed, Completed
        public bool CanManageRequest { get; set; }
        public string Category { get; set; }
        public string BackAction { get; set; }
        public string BackController { get; set; }
        // Added for the status-specific modals (mock data, filled by PrepareMockRentals)
        public string OwnerName { get; set; }
        public string OwnerProgram { get; set; }
        public string ItemLocation { get; set; }
        public string HandoverLocation { get; set; }
        public string ItemImageUrl { get; set; } // null = no image available, the view shows a placeholder
        public string RequestNote { get; set; }
        public double DurationHours { get; set; }
        public decimal HourlyRate { get; set; }
        public string HourlyRateFormatted { get; set; }
        public string HandoverDateFormatted { get; set; }
        public string HandoverTimeFormatted { get; set; }
        public DateTime ExpectedReturnDate { get; set; }
        public string ExpectedReturnFormatted { get; set; }

        // Late-fee rule used by the Overdue / Return modals (mock data, set by PrepareMockRentals)
        public int GracePeriodMinutes { get; set; }

        // Accepted modal: two-party handover verification (mock data, set by PrepareMockRentals)
        public bool OwnerHandoverConfirmed { get; set; }
        public string OwnerHandoverConfirmedAt { get; set; }
        public bool BorrowerHandoverConfirmed { get; set; }
        public string BorrowerHandoverConfirmedAt { get; set; }

        // Declined modal
        public string DeclineReason { get; set; }
        public string DeclineNote { get; set; }
        public string DeclinedAtFormatted { get; set; }
        public string AcceptedAtFormatted { get; set; }

        // Disputed modal
        public string DisputeCategory { get; set; }
        public string DisputeDescription { get; set; }
        public string DisputeSubmittedAtFormatted { get; set; }
        public List<string> DisputeEvidenceUrls { get; set; } // empty = no evidence images submitted

        // Returned / Completed modals
        public string ReturnedAtFormatted { get; set; }
        public string CompletedAtFormatted { get; set; }
    }

    public class MyRentalsViewModel
    {
        public RentalSummaryDto Summary { get; set; }
        public List<RentalRecordDto> Rentals { get; set; }
        public List<RentalRecordDto> IncomingRequests { get; set; }

        public MyRentalsViewModel()
        {
            Summary = new RentalSummaryDto();
            Rentals = new List<RentalRecordDto>();
            IncomingRequests = new List<RentalRecordDto>();
        }
    }

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
                Rentals = BookingService.ForRenter(userId).Select(x => MapBooking(x, userId)).ToList(),
                IncomingRequests = BookingService.ForOwner(userId)
                    .Where(x => new[] { "PENDING", "REQUESTED" }.Contains(x.GetValue("status", "").ToString().Trim().ToUpperInvariant()))
                    .Where(x => x.GetValue("borrowerId", BsonNull.Value).ToString() != userId.ToString())
                    .Select(MapBooking).ToList()
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
            var startValue = booking.GetValue("handoverDate", booking.GetValue("startDate", BsonDateTime.Create(DateTime.UtcNow)));
            var start = startValue.IsValidDateTime ? startValue.ToUniversalTime().ToLocalTime() : DateTime.Now;
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
                HandoverTimeFormatted = booking.GetValue("handoverTime", start.ToString("hh:mm tt")).ToString(),
                HandoverScheduleFormatted = start.ToString("MMM d, yyyy") + " · " + booking.GetValue("handoverTime", start.ToString("hh:mm tt")),
                Duration = hours + " hour" + (hours == 1 ? "" : "s"), DurationHours = hours,
                RentalFee = fee, RentalFeeFormatted = "₱" + fee.ToString("N2"), Status = ToDisplayStatus(status),
                OwnerName = item == null ? "" : (MongoDatabase.Users.Find(x => x.Id == item.OwnerId).FirstOrDefault() is UserDocument owner ? (owner.FirstName + " " + owner.LastName).Trim() : ""),
                RequestNote = booking.GetValue("notes", "").ToString(), Category = item == null ? "" : item.Category,
                BorrowerHandoverConfirmed = booking.GetValue("borrowerHandoverConfirmed", booking.GetValue("handoverConfirmed", false)).ToBoolean() ||
                    new[] { "ONGOING", "ACTIVE", "OVERDUE", "RETURN_REQUESTED", "RETURN PENDING", "COMPLETED", "COMPLETE", "RETURNED", "DISPUTED" }.Contains(status.Trim().ToUpperInvariant()),
                OwnerHandoverConfirmed = booking.GetValue("ownerHandoverConfirmed", false).ToBoolean() ||
                    new[] { "ONGOING", "ACTIVE", "OVERDUE", "RETURN_REQUESTED", "RETURN PENDING", "COMPLETED", "COMPLETE", "RETURNED", "DISPUTED" }.Contains(status.Trim().ToUpperInvariant()),
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
            var normalized = status.ToLowerInvariant();
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

