using System;
using System.Linq;
using System.Web.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using UniShare.Services;

namespace UniShare.Controllers
{
    [Authorize(Roles = "ADMIN")]
    public class DisputeManagementController : Controller
    {
        [HttpGet]
        public ActionResult Index()
        {
            try
            {
                var disputes = AdminAccountabilityService.GetDisputes();
                ViewBag.TotalDisputes = disputes.Count;
                ViewBag.OpenDisputes = disputes.Count(x => x.Status == "Open");
                ViewBag.UnderReview = disputes.Count(x => x.Status == "Under Review" || x.Status == "Info Requested");
                ViewBag.ResolvedDisputes = disputes.Count(x => x.Status == "Resolved");
                return View("DisputeManagement", disputes);
            }
            catch (Exception ex) when (ex is MongoException || ex is InvalidOperationException)
            {
                ViewBag.DatabaseUnavailable = true;
                return View("DisputeManagement", new System.Collections.Generic.List<UniShare.Models.DisputeViewModel>());
            }
        }

        [HttpGet]
        public ActionResult DisputeManagement() { return Index(); }

        [HttpGet]
        public ActionResult GetDisputeDetails(string id)
        {
            try
            {
                var details = AdminAccountabilityService.GetDispute(id);
                return details == null ? (ActionResult)HttpNotFound() : PartialView("DisputeDetails", details);
            }
            catch (Exception ex) when (ex is MongoException || ex is InvalidOperationException)
            { return new HttpStatusCodeResult(503, "Dispute details are unavailable."); }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult UpdateDispute(string id, string decision, string notes)
        {
            ObjectId adminId;
            if (!ObjectId.TryParse(User.Identity.Name, out adminId)) return new HttpStatusCodeResult(401);
            try
            {
                var saved = AdminAccountabilityService.UpdateDispute(id, adminId, decision, notes);
                TempData[saved ? "Success" : "Error"] = saved ? "Dispute decision saved." : "This dispute is no longer open or could not be updated.";
            }
            catch (MongoException) { TempData["Error"] = "The dispute could not be saved because the database is unavailable."; }
            return RedirectToAction("Index");
        }
    }
}
