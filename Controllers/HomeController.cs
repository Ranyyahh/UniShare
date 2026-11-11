using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using UniShare.Models;

namespace UniShare.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View(new LoginViewModel());
        }

        public ActionResult Register()
        {
            return RedirectToAction("Register", "Account");
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }
    }
}
