using System.Web.Mvc;

namespace UniShare.Controllers
{
    public class AdminController : Controller
    {
        public ActionResult Index() { return View(); }
        public ActionResult StudentVerification() { return View(); }
        public ActionResult ItemListings() { return View(); }
        public ActionResult DisputeReports() { return View(); }
        public ActionResult ReportsAndAnalytics() { return View(); }
    }
}
