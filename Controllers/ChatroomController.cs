using System.Web.Mvc;

namespace UniShare.Controllers
{
    public class ChatroomController : Controller
    {
        public ActionResult Index()
        {
            return View("Chatroom");
        }

        public ActionResult Chatroom()
        {
            return View();
        }

    }
}
