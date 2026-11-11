using System.Linq;
using System.Web.Mvc;
using UniShare.Models;

namespace UniShare.Controllers
{
    public class NotificationsController : Controller
    {
        // /Notifications
        public ActionResult Index()
        {
            return View(SampleData.Notifications);
        }

        // POST /Notifications/MarkAllRead
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult MarkAllRead()
        {
            foreach (var n in SampleData.Notifications) n.Unread = false;
            return RedirectToAction("Index");
        }
    }
}
