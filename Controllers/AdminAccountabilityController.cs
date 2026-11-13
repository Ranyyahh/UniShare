using System;
using System.Web.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using UniShare.Services;

namespace UniShare.Controllers
{
    [Authorize(Roles = "ADMIN")]
    public class UserManagementController : Controller
    {
        [HttpGet]
        public ActionResult Index(string search)
        {
            try { return View(AdminAccountabilityService.GetUsers(search)); }
            catch (MongoException) { ViewBag.DatabaseUnavailable = true; return View(new UniShare.Models.UserManagementViewModel { Search = search }); }
        }
    }

    [Authorize(Roles = "ADMIN")]
    public class AccountRestrictionController : Controller
    {
        [HttpGet]
        public ActionResult Index()
        {
            try { return View(AdminAccountabilityService.GetRestrictionsPage()); }
            catch (MongoException) { ViewBag.DatabaseUnavailable = true; return View(new UniShare.Models.AccountRestrictionPageViewModel()); }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Create(string userId, string type, string reason, int? days)
        {
            ObjectId adminId;
            if (!ObjectId.TryParse(User.Identity.Name, out adminId)) return new HttpStatusCodeResult(401);
            try
            {
                var saved = AdminAccountabilityService.CreateRestriction(userId, adminId, type, reason, days);
                TempData[saved ? "Success" : "Error"] = saved ? "Account action recorded." : "Check the selected student, reason, and suspension length.";
            }
            catch (MongoException) { TempData["Error"] = "The account action could not be saved."; }
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Revoke(string id, string notes)
        {
            ObjectId adminId;
            if (!ObjectId.TryParse(User.Identity.Name, out adminId)) return new HttpStatusCodeResult(401);
            try
            {
                var saved = AdminAccountabilityService.RevokeRestriction(id, adminId, notes);
                TempData[saved ? "Success" : "Error"] = saved ? "Restriction revoked." : "This restriction is no longer active.";
            }
            catch (MongoException) { TempData["Error"] = "The restriction could not be updated."; }
            return RedirectToAction("Index");
        }
    }

    [Authorize(Roles = "ADMIN")]
    public class RatingsReviewsController : Controller
    {
        [HttpGet]
        public ActionResult Index(string filter)
        {
            try { return View(new UniShare.Models.RatingsReviewsViewModel { Filter = filter ?? "all", Reviews = AdminAccountabilityService.GetReviews(filter) }); }
            catch (MongoException) { ViewBag.DatabaseUnavailable = true; return View(new UniShare.Models.RatingsReviewsViewModel { Filter = filter ?? "all" }); }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Moderate(string id, string decision, string notes)
        {
            ObjectId adminId;
            if (!ObjectId.TryParse(User.Identity.Name, out adminId)) return new HttpStatusCodeResult(401);
            try
            {
                var saved = AdminAccountabilityService.ModerateReview(id, adminId, decision, notes);
                TempData[saved ? "Success" : "Error"] = saved ? "Review moderation saved." : "The review could not be updated.";
            }
            catch (MongoException) { TempData["Error"] = "The review could not be updated."; }
            return RedirectToAction("Index", new { filter = "reported" });
        }
    }

    [Authorize]
    public class ReviewReportsController : Controller
    {
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Report(string id, string reason, string returnUrl)
        {
            ObjectId reporterId;
            if (!ObjectId.TryParse(User.Identity.Name, out reporterId)) return new HttpStatusCodeResult(401);
            try
            {
                var reported = AdminAccountabilityService.ReportReview(reporterId, id, reason);
                TempData[reported ? "Success" : "Error"] = reported ? "Review sent to the moderation queue." : "This review cannot be reported with those details.";
            }
            catch (MongoException) { TempData["Error"] = "The review report could not be saved."; }
            return !String.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) ? (ActionResult)Redirect(returnUrl) : RedirectToAction("Index", "BrowseItems");
        }
    }

    public class NoShowReportsController : Controller
    {
        [Authorize(Roles = "ADMIN"), HttpGet]
        public ActionResult Index()
        {
            try { return View(AdminAccountabilityService.GetNoShowIncidents()); }
            catch (MongoException) { ViewBag.DatabaseUnavailable = true; return View(new System.Collections.Generic.List<UniShare.Models.NoShowIncidentViewModel>()); }
        }

        [Authorize, HttpPost, ValidateAntiForgeryToken]
        public ActionResult Report(string bookingId, string reason, string returnUrl)
        {
            ObjectId reporterId;
            if (!ObjectId.TryParse(User.Identity.Name, out reporterId)) return new HttpStatusCodeResult(401);
            try
            {
                var saved = AdminAccountabilityService.ReportNoShow(reporterId, bookingId, reason);
                TempData[saved ? "Success" : "Error"] = saved ? "No-show report sent to the admin review queue." : "This booking is not eligible for a no-show report, or one is already open.";
                if (Request.IsAjaxRequest()) return Json(new { ok = saved, message = saved ? (string)TempData["Success"] : (string)TempData["Error"] });
            }
            catch (MongoException)
            {
                TempData["Error"] = "The report could not be saved.";
                if (Request.IsAjaxRequest()) { Response.StatusCode = 500; return Json(new { ok = false, message = (string)TempData["Error"] }); }
            }
            return !String.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) ? (ActionResult)Redirect(returnUrl) : RedirectToAction("Index", "MyRentals");
        }

        [Authorize(Roles = "ADMIN"), HttpPost, ValidateAntiForgeryToken]
        public ActionResult Review(string id, bool confirm, string notes)
        {
            ObjectId adminId;
            if (!ObjectId.TryParse(User.Identity.Name, out adminId)) return new HttpStatusCodeResult(401);
            try
            {
                var saved = AdminAccountabilityService.ReviewNoShow(id, adminId, confirm, notes);
                TempData[saved ? "Success" : "Error"] = saved ? (confirm ? "No-show confirmed. The booking was cancelled." : "No-show report declined.") : "This report could not be reviewed. The booking may have changed since it was reported.";
            }
            catch (MongoException) { TempData["Error"] = "The no-show decision could not be saved."; }
            return RedirectToAction("Index");
        }
    }

    [Authorize(Roles = "ADMIN")]
    public class AuditLogsController : Controller
    {
        [HttpGet]
        public ActionResult Index(string search)
        {
            try { ViewBag.Search = search ?? ""; return View(AdminAccountabilityService.GetAuditLogs(search)); }
            catch (MongoException) { ViewBag.DatabaseUnavailable = true; return View(new System.Collections.Generic.List<UniShare.Models.AuditLogRowViewModel>()); }
        }
    }
}
