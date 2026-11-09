using System.Web.Mvc;
using UniShare.Models;

namespace UniShare.Controllers
{
    public class ItemsController : Controller
    {
        // /Items/Browse
        public ActionResult Browse()
        {
            return View("BrowseItems", new BrowseItemsViewModel());
        }

        // /Items/Details?id=1
        public ActionResult Details(int id)
        {
            var item = SampleItemData.Get(id);   // TODO: load from your database
            if (item == null) return HttpNotFound();
            return View("ItemDetails", item);
        }
    }
}
