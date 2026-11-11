using System.Web.Mvc;
using System.Linq;
using UniShare.Models;

namespace UniShare.Controllers
{
    [Authorize]
    public class MessagesController : Controller
    {
        public ActionResult Index()
        {
            return View("~/Views/Chatroom/Chatroom.cshtml");
        }

        // Preserve the earlier message-send endpoint for existing clients.
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Send(int id, string text)
        {
            var convo = SampleData.Conversations.FirstOrDefault(c => c.Id == id);
            if (convo == null) return HttpNotFound();
            if (!string.IsNullOrWhiteSpace(text))
            {
                convo.Messages.Add(new ChatMessage
                {
                    Mine = true,
                    Text = text.Trim(),
                    Time = System.DateTime.Now.ToString("h:mm tt")
                });
                convo.LastMessage = text.Trim();
                convo.Time = "Just now";
            }
            return RedirectToAction("Index", new { id = id });
        }
    }
}
