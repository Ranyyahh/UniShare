using System.Web.Mvc;

namespace UniShare.Controllers
{
    public class ItemsController : Controller
    {
        // /Items/Browse
        public ActionResult Browse()
        {
            return RedirectToAction("Index", "BrowseItems");
        }

        // /Items/Details?id=1
        public ActionResult Details(int id)
        {
            return RedirectToAction("ItemDetails", "BrowseItems", new { id = id });
        }
    }
}
