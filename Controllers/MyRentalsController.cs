using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Web;
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
        public string Category { get; set; }

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

        public MyRentalsViewModel()
        {
            Summary = new RentalSummaryDto();
            Rentals = new List<RentalRecordDto>();
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
            var model = new MyRentalsViewModel { Rentals = BookingService.ForOwner(userId).Select(MapBooking).ToList() };

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
            return View(MapBooking(booking));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ReportDispute(string id, string reason, string details, HttpPostedFileBase[] evidenceFiles)
        {
            ObjectId ownerId;
            if (!ObjectId.TryParse(User.Identity.Name, out ownerId)) return Json(new { ok = false, message = "Please sign in again." });
            if (String.IsNullOrWhiteSpace(reason) || reason.Length > 100 || (details ?? "").Length > 1000)
                return Json(new { ok = false, message = "Select a reason and check the details." });
            var files = (evidenceFiles ?? new HttpPostedFileBase[0]).Where(x => x != null && x.ContentLength > 0).ToList();
            if (files.Count > 2) return Json(new { ok = false, message = "Upload no more than two evidence images." });
            var savedUrls = new List<string>();
            try
            {
                foreach (var file in files) savedUrls.Add(SaveDisputeEvidence(file));
                var saved = BorrowedItemsService.SubmitDisputeByOwner(ownerId, id, reason, details, savedUrls);
                if (!saved) { DeleteDisputeEvidence(savedUrls); return Json(new { ok = false, message = "This rental cannot be disputed in its current status." }); }
                return Json(new { ok = true, message = "Dispute sent for admin review.", evidenceUrls = savedUrls });
            }
            catch (InvalidDataException ex)
            {
                DeleteDisputeEvidence(savedUrls);
                return Json(new { ok = false, message = ex.Message });
            }
            catch (Exception ex) when (ex is MongoException || ex is IOException || ex is InvalidOperationException)
            {
                DeleteDisputeEvidence(savedUrls);
                Response.StatusCode = 409;
                return Json(new { ok = false, message = "The dispute could not be saved. Refresh and try again." });
            }
        }

        [HttpGet]
        public ActionResult History()
        {
            ObjectId userId;
            if (!ObjectId.TryParse(User.Identity.Name, out userId)) return new HttpStatusCodeResult(401);
            var history = BookingService.ForOwner(userId).Where(x => new[] { "COMPLETED", "COMPLETE", "DECLINED", "CANCELLED", "CANCELED", "EXPIRED" }.Contains(x.GetValue("status", "").ToString().ToUpperInvariant())).ToList();
            return View(history.Select(MapBooking).ToList());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Decide(string id, bool accept, string reason, string notes)
        {
            ObjectId bookingId, ownerId;
            if (!ObjectId.TryParse(id, out bookingId) || !ObjectId.TryParse(User.Identity.Name, out ownerId)) return new HttpStatusCodeResult(400);
            if (!accept && string.IsNullOrWhiteSpace(reason)) { TempData["Error"] = "Select a reason before declining this request."; return RedirectToAction("Index"); }
            if (!BookingService.Decide(bookingId, ownerId, accept, reason, notes)) TempData["Error"] = "This request is no longer pending, has expired, or its schedule is no longer available.";
            else TempData["Success"] = accept ? "Rental request accepted." : "Rental request declined.";
            return RedirectToAction("Index");
        }

        internal static RentalRecordDto MapBooking(BsonDocument booking)
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
                RequestId = booking.GetValue("requestId", booking.GetValue("_id", "").ToString()).ToString(),
                ItemName = item == null ? booking.GetValue("itemName", "Item").ToString() : item.Name,
                BorrowerName = fullName, BorrowerInitials = string.Concat(fullName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Take(2).Select(x => char.ToUpperInvariant(x[0]))),
                BorrowerProgram = borrower == null ? "" : borrower.Program,
                RequestDate = booking.GetValue("createdAt", BsonDateTime.Create(DateTime.UtcNow)).ToUniversalTime().ToLocalTime(),
                RequestDateFormatted = booking.GetValue("createdAt", BsonDateTime.Create(DateTime.UtcNow)).ToUniversalTime().ToLocalTime().ToString("MMM d, yyyy"),
                HandoverDate = start, HandoverDateFormatted = start.ToString("MMM d, yyyy"),
                ExpectedReturnDate = start.AddHours(hours), ExpectedReturnFormatted = start.AddHours(hours).ToString("MMM d, yyyy h:mm tt"),
                HandoverTimeFormatted = booking.GetValue("handoverTime", start.ToString("hh:mm tt")).ToString(),
                HandoverScheduleFormatted = start.ToString("MMM d, yyyy") + " · " + booking.GetValue("handoverTime", start.ToString("hh:mm tt")),
                Duration = hours + " hour" + (hours == 1 ? "" : "s"), DurationHours = hours,
                RentalFee = fee, RentalFeeFormatted = "₱" + fee.ToString("N2"), Status = ToDisplayStatus(status),
                OwnerName = item == null ? "" : (MongoDatabase.Users.Find(x => x.Id == item.OwnerId).FirstOrDefault() is UserDocument owner ? (owner.FirstName + " " + owner.LastName).Trim() : ""),
                RequestNote = booking.GetValue("notes", "").ToString(), Category = item == null ? "" : item.Category,
                DeclineReason = booking.GetValue("declineReason", "").ToString(), DeclineNote = booking.GetValue("declineNotes", "").ToString(),
                DeclinedAtFormatted = FormatDate(booking, "declinedAt"), AcceptedAtFormatted = FormatDate(booking, "acceptedAt"),
                ReturnedAtFormatted = FormatDate(booking, "returnedAt"), CompletedAtFormatted = FormatDate(booking, "completedAt"),
                DisputeCategory = booking.GetValue("disputeCategory", "").ToString(), DisputeDescription = booking.GetValue("disputeDetails", "").ToString(),
                DisputeSubmittedAtFormatted = FormatDate(booking, "disputedAt"), DisputeEvidenceUrls = ReadStringArray(booking, "disputeEvidenceUrls")
            };
        }

        private string SaveDisputeEvidence(HttpPostedFileBase file)
        {
            if (file.ContentLength > 5 * 1024 * 1024) throw new InvalidDataException("Each image must be 5 MB or smaller.");
            var extension = Path.GetExtension(file.FileName ?? "").ToLowerInvariant();
            if (!new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }.Contains(extension) || !IsImageSignature(file, extension))
                throw new InvalidDataException("Evidence must be a valid JPG, PNG, GIF, or WEBP image.");
            var folder = Server.MapPath("~/Uploads/disputes");
            Directory.CreateDirectory(folder);
            var fileName = Guid.NewGuid().ToString("N") + extension;
            file.SaveAs(Path.Combine(folder, fileName));
            return "/Uploads/disputes/" + fileName;
        }

        private static bool IsImageSignature(HttpPostedFileBase file, string extension)
        {
            var bytes = new byte[12];
            var read = file.InputStream.Read(bytes, 0, bytes.Length);
            if (file.InputStream.CanSeek) file.InputStream.Position = 0;
            if ((extension == ".jpg" || extension == ".jpeg") && read >= 3) return bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff;
            if (extension == ".png" && read >= 8) return bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4e && bytes[3] == 0x47 && bytes[4] == 0x0d && bytes[5] == 0x0a && bytes[6] == 0x1a && bytes[7] == 0x0a;
            if (extension == ".gif" && read >= 6) { var header = System.Text.Encoding.ASCII.GetString(bytes, 0, 6); return header == "GIF87a" || header == "GIF89a"; }
            if (extension == ".webp" && read >= 12) return System.Text.Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP";
            return false;
        }

        private void DeleteDisputeEvidence(IEnumerable<string> urls)
        {
            foreach (var url in urls ?? Enumerable.Empty<string>())
            {
                try { var path = Server.MapPath(url); if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
                catch { }
            }
        }

        private static List<string> ReadStringArray(BsonDocument doc, string key)
        {
            var value = doc.GetValue(key, BsonNull.Value);
            return value.IsBsonArray ? value.AsBsonArray.Where(x => !x.IsBsonNull).Select(x => x.ToString()).ToList() : new List<string>();
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

