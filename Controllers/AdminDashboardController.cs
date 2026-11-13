using System;
using MongoDB.Driver;
using System.Web.Mvc;
using UniShare.Services;

namespace UniShare.Controllers
{
    [Authorize(Roles = "ADMIN")]
    public class AdminDashboardController : Controller
    {
        public ActionResult Index()
        {
            try { return View(AdminAccountabilityService.GetDashboard()); }
            catch (MongoException) { ViewBag.DatabaseUnavailable = true; return View(new UniShare.Models.AdminDashboardViewModel()); }
        }
    }
}
