using System.Web.Mvc;

namespace UniShare.Controllers
{
    [Authorize]
    public class ChatroomController : Controller
    {
        public ActionResult Index()
        {
            return RedirectToAction("Index", "Messages");
        }

        public ActionResult Chatroom()
        {
            return RedirectToAction("Index", "Messages");
        }

    }
}
