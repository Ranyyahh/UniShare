using System;
using System.Web.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using UniShare.Services;

namespace UniShare.Controllers
{
    [Authorize]
    public class MessagesController : Controller
    {
        [HttpGet]
        public ActionResult Index(string userId, string itemId, string conversationId)
        {
            ObjectId currentUserId;
            if (!ObjectId.TryParse(User.Identity.Name, out currentUserId)) return new HttpStatusCodeResult(401);
            ViewBag.InitialConversationId = conversationId ?? "";
            ViewBag.ChatNotice = "";
            if (!string.IsNullOrWhiteSpace(userId) || !string.IsNullOrWhiteSpace(itemId))
            {
                ObjectId ownerId, listingId;
                if (!ObjectId.TryParse(userId, out ownerId) || !ObjectId.TryParse(itemId, out listingId))
                    ViewBag.ChatNotice = "This item's conversation could not be opened.";
                else
                {
                    var started = ChatroomService.StartOrFind(currentUserId, ownerId, listingId);
                    if (started.HasValue) ViewBag.InitialConversationId = started.Value.ToString();
                    else ViewBag.ChatNotice = "You cannot start a conversation for this item.";
                }
            }
            return View("~/Views/Chatroom/Chatroom.cshtml");
        }

        [HttpGet]
        public ActionResult Conversations()
        {
            ObjectId userId;
            if (!ObjectId.TryParse(User.Identity.Name, out userId)) return Json(new { ok = false, message = "Please sign in again." }, JsonRequestBehavior.AllowGet);
            try { return Json(new { ok = true, conversations = ChatroomService.GetConversations(userId) }, JsonRequestBehavior.AllowGet); }
            catch (MongoException) { Response.StatusCode = 503; return Json(new { ok = false, message = "Messages are temporarily unavailable." }, JsonRequestBehavior.AllowGet); }
        }

        [HttpGet]
        public ActionResult Thread(string id)
        {
            ObjectId userId;
            if (!ObjectId.TryParse(User.Identity.Name, out userId)) return Json(new { ok = false, message = "Please sign in again." }, JsonRequestBehavior.AllowGet);
            try
            {
                var thread = ChatroomService.GetThread(userId, id);
                if (thread == null) { Response.StatusCode = 404; return Json(new { ok = false, message = "Conversation not found." }, JsonRequestBehavior.AllowGet); }
                return Json(new { ok = true, thread }, JsonRequestBehavior.AllowGet);
            }
            catch (MongoException) { Response.StatusCode = 503; return Json(new { ok = false, message = "This conversation could not be loaded." }, JsonRequestBehavior.AllowGet); }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Send(string id, string text)
        {
            ObjectId userId;
            if (!ObjectId.TryParse(User.Identity.Name, out userId)) return Json(new { ok = false, message = "Please sign in again." });
            text = (text ?? "").Trim();
            if (text.Length == 0 || text.Length > 500) return Json(new { ok = false, message = "Write a message up to 500 characters." });
            try
            {
                var message = ChatroomService.Send(userId, id, text);
                if (message == null) { Response.StatusCode = 404; return Json(new { ok = false, message = "Conversation not found." }); }
                return Json(new { ok = true, message });
            }
            catch (MongoException) { Response.StatusCode = 503; return Json(new { ok = false, message = "Your message could not be sent. Try again." }); }
        }
    }
}
