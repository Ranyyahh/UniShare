using System.Web.Mvc;

namespace UniShare.Controllers
{
    public class BorrowedItemsController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }
    }
}
