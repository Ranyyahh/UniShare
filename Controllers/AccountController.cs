using System.Web.Mvc;
using System.Web.Security;
using UniShare.Models;

namespace UniShare.Controllers
{
    public class AccountController : Controller
    {
        [HttpGet]
        public ActionResult Login() { return View(); }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginForm form)
        {
            if (!ModelState.IsValid) return View(form);
            if (!form.Email.EndsWith("@qcu.edu.ph", System.StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("Email", "Please use your QCU email (@qcu.edu.ph).");
                return View(form);
            }
            return RedirectToAction("Index", "Dashboard");
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
