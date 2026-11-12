using System;
using System.Linq;
using System.Web.Mvc;
using MongoDB.Bson;
using UniShare.Models;
using UniShare.Services;

namespace UniShare.Controllers
{
    [Authorize]
    [VerifiedStudent]
    public class BorrowedItemsController : Controller
    {
        [HttpGet]
        public ActionResult Index(string status)
        {
            ObjectId borrowerId;
            if (!ObjectId.TryParse(User.Identity.Name, out borrowerId)) return new HttpStatusCodeResult(401);
            ViewBag.Status = NormalizeTab(status);
            return View(BorrowedItemsService.GetForBorrower(borrowerId));
        }

        [HttpGet]
        public ActionResult Details(string id)
        {
            ObjectId borrowerId;
            if (!ObjectId.TryParse(User.Identity.Name, out borrowerId)) return new HttpStatusCodeResult(401);
            var record = BorrowedItemsService.GetDetails(borrowerId, id);
            return record == null ? (ActionResult)HttpNotFound() : Json(record, JsonRequestBehavior.AllowGet);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ConfirmHandover(string id, bool received)
        {
            if (!received) return Json(new { ok = false, message = "Confirm that you received the item before continuing." });
            ObjectId borrowerId;
            if (!ObjectId.TryParse(User.Identity.Name, out borrowerId)) return Json(new { ok = false, message = "Please sign in again." });
            return Run(() => BorrowedItemsService.ConfirmHandover(borrowerId, id), "Your handover confirmation has been recorded.", "This handover is no longer awaiting your confirmation.");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ConfirmHandoverByOwner(string id)
        {
            ObjectId ownerId;
            if (!ObjectId.TryParse(User.Identity.Name, out ownerId)) return new HttpStatusCodeResult(401);
            var confirmed = BorrowedItemsService.ConfirmHandoverByOwner(ownerId, id);
            TempData[confirmed ? "Success" : "Error"] = confirmed
                ? "Your handover confirmation has been recorded. The rental is active once both parties confirm."
                : "This rental is no longer awaiting your handover confirmation.";
            return RedirectToAction("Ongoing", "MyListings");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ReturnItem(string id, bool returned)
        {
            if (!returned) return Json(new { ok = false, message = "Confirm that you returned the item before continuing." });
            ObjectId borrowerId;
            if (!ObjectId.TryParse(User.Identity.Name, out borrowerId)) return Json(new { ok = false, message = "Please sign in again." });
            return Run(() => BorrowedItemsService.RequestReturn(borrowerId, id), "Return request sent to the item owner.", "This rental cannot be returned from its current status.");
        }

        // Owner confirmation is kept separate from the borrower's return request.
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ConfirmReturn(string id)
        {
            ObjectId ownerId;
            if (!ObjectId.TryParse(User.Identity.Name, out ownerId)) return Json(new { ok = false, message = "Please sign in again." });
            return Run(() => BorrowedItemsService.ConfirmReturnByOwner(ownerId, id), "Return confirmed.", "This record is not awaiting your confirmation.");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Rate(string id, int itemRating, int ownerRating, string review)
        {
            ObjectId borrowerId;
            if (!ObjectId.TryParse(User.Identity.Name, out borrowerId)) return Json(new { ok = false, message = "Please sign in again." });
            if (itemRating < 1 || itemRating > 5 || ownerRating < 1 || ownerRating > 5 || (review ?? "").Length > 300)
                return Json(new { ok = false, message = "Choose a rating from one to five stars." });
            return Run(() => BorrowedItemsService.SubmitRating(borrowerId, id, itemRating, ownerRating, review), "Thanks for reviewing this rental.", "Only completed, unrated rentals can be reviewed.");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Dispute(string id, string reason, string details)
        {
            ObjectId borrowerId;
            if (!ObjectId.TryParse(User.Identity.Name, out borrowerId)) return Json(new { ok = false, message = "Please sign in again." });
            if (string.IsNullOrWhiteSpace(reason) || reason.Length > 100 || (details ?? "").Length > 400)
                return Json(new { ok = false, message = "Select a reason and check the details." });
            return Run(() => BorrowedItemsService.SubmitDispute(borrowerId, id, reason, details), "Dispute sent for review.", "A dispute cannot be submitted for this rental.");
        }

        private JsonResult Run(Func<bool> action, string success, string failure)
        {
            try
            {
                var changed = action();
                return Json(new { ok = changed, message = changed ? success : failure });
            }
            catch
            {
                Response.StatusCode = 409;
                return Json(new { ok = false, message = "This rental changed while you were viewing it. Refresh the page and try again." });
            }
        }

        private static string NormalizeTab(string status)
        {
            var allowed = new[] { "Pending", "Ongoing", "Completed", "Disputed" };
            return allowed.FirstOrDefault(x => string.Equals(x, status, StringComparison.OrdinalIgnoreCase)) ?? "Pending";
        }
    }
}
