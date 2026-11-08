using System.Linq;
using System.Web.Mvc;
using UniShare.Models;

namespace UniShare.Controllers
{
    public class MessagesController : Controller
    {
        // /Messages or /Messages?id=2
        public ActionResult Index(int? id)
        {
            var convo = SampleData.Conversations.FirstOrDefault(c => c.Id == (id ?? SampleData.Conversations[0].Id));
            if (convo != null) convo.Unread = false;
            ViewBag.Active = convo;
            return View(SampleData.Conversations);
        }

        // POST /Messages/Send
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Send(int id, string text)
        {
            var convo = SampleData.Conversations.FirstOrDefault(c => c.Id == id);
            if (convo == null) return HttpNotFound();
            if (!string.IsNullOrWhiteSpace(text))
            {
                convo.Messages.Add(new ChatMessage { Mine = true, Text = text.Trim(), Time = System.DateTime.Now.ToString("h:mm tt") });
                convo.LastMessage = text.Trim();
                convo.Time = "Just now";
            }
            return RedirectToAction("Index", new { id = id });
        }
    }
}
