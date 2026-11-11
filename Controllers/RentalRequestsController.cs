using System.Linq;
using System.Web.Mvc;
using MongoDB.Bson;
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
            return View(BookingService.ForOwner(ownerId).Select(MyRentalsController.MapBooking).ToList());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Decide(string id, bool accept, string reason, string notes)
        {
            ObjectId bookingId, ownerId;
            if (!ObjectId.TryParse(id, out bookingId) || !ObjectId.TryParse(User.Identity.Name, out ownerId)) return new HttpStatusCodeResult(400);
            if (!accept && string.IsNullOrWhiteSpace(reason)) TempData["Error"] = "A decline reason is required.";
            else if (!BookingService.Decide(bookingId, ownerId, accept, reason, notes)) TempData["Error"] = "The request was processed, expired, or its schedule is no longer available.";
            else TempData["Success"] = accept ? "Rental request accepted." : "Rental request declined.";
            return RedirectToAction("Index");
        }
    }
}
