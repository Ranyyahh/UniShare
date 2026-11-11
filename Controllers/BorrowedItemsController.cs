using System.Web.Mvc;
using UniShare.Services;

namespace UniShare.Controllers
{
    [Authorize]
    [VerifiedStudent]
    public class BorrowedItemsController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }
    }
}
