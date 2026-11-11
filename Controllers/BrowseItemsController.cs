using System;
using System.Web.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using UniShare.Models;
using UniShare.Services;

namespace UniShare.Controllers
{
    public class BrowseItemsController : Controller
    {
        // /BrowseItems  ->  Views/BrowseItems/Index.cshtml
        // (If your controller already has an Index action, keep yours.)
        public ActionResult Index()
        {
            return View(new BrowseItemsViewModel());
        }

        // /BrowseItems/ItemDetails?id=1  ->  Views/BrowseItems/ItemDetails.cshtml
        public ActionResult ItemDetails(int id)
        {
            var authenticated = User != null && User.Identity != null && User.Identity.IsAuthenticated;
            var verified = false;
            if (authenticated)
            {
                try
                {
                    var user = MongoDatabase.Users.Find(x => x.Id == ObjectId.Parse(User.Identity.Name)).FirstOrDefault();
                    verified = user != null && user.VerificationStatus == "APPROVED";
                }
                catch { verified = false; }
            }
            ViewBag.IsAuthenticated = authenticated;
            ViewBag.IsStudentVerified = verified;
            var item = SampleItemData.Get(id);   // TODO: load from your database
            if (item == null) return HttpNotFound();
            return View(item);
        }

        // Compatibility alias for the earlier /BrowseItems/Details route.
        public ActionResult Details(int id)
        {
            return RedirectToAction("ItemDetails", new { id = id });
        }

        [HttpGet]
        public ActionResult RequestToRent(int id)
        {
            var item = SampleItemData.Get(id);
            if (item == null) return HttpNotFound();
            if (!item.IsAvailable) return RedirectToAction("ItemDetails", new { id = id });
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RequestToRent(int id, DateTime handoverDate, string handoverTime, int duration, string notes)
        {
            var item = SampleItemData.Get(id);
            if (item == null) return HttpNotFound();
            if (!item.IsAvailable) return RedirectToAction("ItemDetails", new { id = id });
            if (duration < 1 || duration > 24) duration = 2;
            if (string.IsNullOrWhiteSpace(handoverTime)) handoverTime = "09:00 AM";
            ViewBag.HandoverDate = handoverDate;
            ViewBag.HandoverTime = handoverTime;
            ViewBag.Duration = duration;
            ViewBag.Notes = notes;
            ViewBag.Total = item.HourlyRate * duration;
            return View("RequestSubmitted", item);
        }

        // Compatibility endpoint for clients using the earlier single-step request.
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult RequestRental(int id)
        {
            var item = SampleItemData.Get(id);
            if (item == null) return HttpNotFound();
            if (!item.IsAvailable) return RedirectToAction("ItemDetails", new { id = id });
            TempData["Success"] = "Rental request sent to " + item.Lender.Name + " for " + item.Name + ".";
            return RedirectToAction("ItemDetails", new { id = id });
        }
    }
}
