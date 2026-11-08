using System.Web.Mvc;

namespace UniShare.Controllers
{
    public class HomeController : Controller
    {
        // "/" -> Dashboard
        public ActionResult Index()
        {
            return RedirectToAction("Index", "Dashboard");
        }

        public ActionResult About()
        {
            ViewBag.Message = "UniShare is the Quezon City University campus marketplace for renting and sharing items.";
            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Questions or concerns? Reach the UniShare team.";
            return View();
        }
    }
}
