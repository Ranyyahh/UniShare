using System.Web.Mvc;

namespace UniShare.Controllers
{
    public class AccountController : Controller
    {
        [HttpGet]
        public ActionResult Login() { return View(); }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(FormCollection form)
        {
            ViewBag.Message = "Login submission is not implemented yet.";
            return View();
        }

        [HttpGet]
        public ActionResult Register() { return View(); }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(FormCollection form)
        {
            ViewBag.Message = "Registration submission is not implemented yet.";
            return View();
        }
    }
}
