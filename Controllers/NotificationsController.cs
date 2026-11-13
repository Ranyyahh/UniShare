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
            if (Request.IsAjaxRequest()) return Json(new { ok = true, message = "All notifications marked as read." });
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult SetRead(string eventKey, bool isRead)
        {
            ObjectId userId;
            if (!ObjectId.TryParse(User.Identity.Name, out userId)) return Json(new { ok = false, message = "Please sign in again." });
            var saved = NotificationService.SetRead(userId, eventKey, isRead);
            if (!saved) Response.StatusCode = 404;
            if (!Request.IsAjaxRequest() && saved) return RedirectToAction("Index");
            return Json(new { ok = saved, message = saved ? (isRead ? "Notification marked as read." : "Notification marked as unread.") : "Notification not found." });
        }
    }
}
