using System.Web.Mvc;
using UniShare.Models;

namespace UniShare.Controllers
{
    public class AccountController : Controller
    {
        // /Account/Login  (the header account button links here)
        public ActionResult Login()
        {
            return View(new LoginForm());
        }

        // Demo login - accepts any valid @qcu.edu.ph email. Replace with real authentication later.
        [HttpPost, ValidateAntiForgeryToken]
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

    }
}
