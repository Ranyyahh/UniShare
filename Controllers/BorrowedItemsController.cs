using System;
using System.Linq;
using System.Web.Mvc;
using UniShare.Services;

namespace UniShare.Controllers
{
    [Authorize]
    [VerifiedStudent]
    public class BorrowedItemsController : Controller
    {
        // /BorrowedItems  (optional ?status=Ongoing|Completed|Disputed pre-selects a tab)
        public ActionResult Index(string status)
        {
            ViewBag.Status = status;
            return View(SampleData.Borrowed.ToList());
        }

        // Confirm handover: the item STAYS in Ongoing and the rental timer starts.
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ConfirmHandover(int id)
        {
            var b = SampleData.Borrowed.FirstOrDefault(x => x.Id == id);
            if (b == null) return HttpNotFound();
            if ((b.Status != "Pending" && b.Status != "Ongoing") || b.HandoverConfirmed) return Json(new { ok = false });
            b.Status = "Ongoing";
            b.HandoverConfirmed = true;
            b.HandoverAt = DateTime.Now;
            b.HandoverOn = DateTime.Today;
            return Json(new { ok = true });
        }

        // Return item: only after the handover was confirmed. Moves the item to Completed.
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ReturnItem(int id)
        {
            var b = SampleData.Borrowed.FirstOrDefault(x => x.Id == id);
            if (b == null) return HttpNotFound();
            if (b.Status != "Ongoing" || !b.HandoverConfirmed) return Json(new { ok = false });
            b.Status = "Completed";
            b.CompletedOn = DateTime.Today;
            return Json(new { ok = true });
        }

        // Rating is only allowed once the item is Completed (returned).
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Rate(int id, int rating, string review)
        {
            var b = SampleData.Borrowed.FirstOrDefault(x => x.Id == id);
            if (b == null) return HttpNotFound();
            if (b.Status != "Completed" || b.Rating.HasValue || rating < 1 || rating > 5) return Json(new { ok = false });
            b.Rating = rating;
            b.Review = string.IsNullOrWhiteSpace(review) ? null : review.Trim();
            return Json(new { ok = true });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Dispute(int id, string reason, string details)
        {
            var b = SampleData.Borrowed.FirstOrDefault(x => x.Id == id);
            if (b == null) return HttpNotFound();
            if ((b.Status != "Pending" && b.Status != "Ongoing") || string.IsNullOrWhiteSpace(reason)) return Json(new { ok = false });
            b.Status = "Disputed";
            b.DisputeReason = string.IsNullOrWhiteSpace(details) ? reason : reason + ": " + details.Trim();
            return Json(new { ok = true });
        }
    }
}
