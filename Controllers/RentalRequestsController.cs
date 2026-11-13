using System.Linq;
using System.Web.Mvc;
using MongoDB.Bson;
using UniShare.Models;
using UniShare.Services;

namespace UniShare.Controllers
{
    [Authorize]
    public class RentalRequestsController : Controller
    {
        public ActionResult Index()
        {
            ObjectId ownerId;
            if (!ObjectId.TryParse(User.Identity.Name, out ownerId)) return new HttpStatusCodeResult(401);
            return View(BookingService.ForOwner(ownerId)
                .Where(x => new[] { "PENDING", "REQUESTED" }.Contains(x.GetValue("status", "").ToString().Trim().ToUpperInvariant()))
                .Where(x => x.GetValue("borrowerId", BsonNull.Value).ToString() != ownerId.ToString())
                .Select(MyRentalsController.MapBooking).ToList());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Decide(string id, bool accept, string reason, string notes)
        {
            ObjectId bookingId, ownerId;
            if (!ObjectId.TryParse(id, out bookingId) || !ObjectId.TryParse(User.Identity.Name, out ownerId)) return new HttpStatusCodeResult(400);
            var message = !accept && string.IsNullOrWhiteSpace(reason) ? "A decline reason is required." : null;
            var saved = message == null && BookingService.Decide(bookingId, ownerId, accept, reason, notes);
            if (!saved && message == null) message = "The request was processed, expired, or its schedule is no longer available.";
            if (Request.IsAjaxRequest())
            {
                Response.StatusCode = saved ? 200 : 409;
                return Json(new { ok = saved, message = saved ? (accept ? "Rental request accepted." : "Rental request declined.") : message });
            }
            TempData[saved ? "Success" : "Error"] = saved ? (accept ? "Rental request accepted." : "Rental request declined.") : message;
            return RedirectToAction("Index");
        }
    }
}
