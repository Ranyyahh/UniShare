using System.Linq;
using System.Web.Mvc;
using UniShare.Models;
using UniShare.Services;
using MongoDB.Bson;

namespace UniShare.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        // /Notifications
        public ActionResult Index()
        {
            ObjectId userId;
            if (!ObjectId.TryParse(User.Identity.Name, out userId)) return new HttpStatusCodeResult(401);
            return View(NotificationService.GetForUser(userId));
        }

        // POST /Notifications/MarkAllRead
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult MarkAllRead()
        {
            ObjectId userId;
            if (!ObjectId.TryParse(User.Identity.Name, out userId)) return new HttpStatusCodeResult(401);
            NotificationService.MarkAllRead(userId);
            return RedirectToAction("Index");
        }
    }
}
