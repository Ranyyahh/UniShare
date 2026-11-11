using System.Web.Mvc;
using System.Web.Security;

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

        [HttpGet]
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
        public ActionResult Profile()
        {
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
                return new HttpUnauthorizedResult();

            // The current project has no student profile store. Return only the
            // authenticated principal name; never substitute sample profile data.
            return Json(new
            {
                displayName = User.Identity.Name,
                profileDataAvailable = false
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult Settings()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            Session.Clear();
            Session.Abandon();
            Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            return RedirectToAction("Login", "Account");
        }
    }
}
