using System.Web.Mvc;
using UniShare.Models;

namespace UniShare.Controllers
{
    public class BrowseItemsController : Controller
    {
        // /BrowseItems  ->  Views/BrowseItems/Index.cshtml
        // (If your controller already has an Index action, keep yours.)
        public ActionResult Index()
        {
            return View(new BrowseItemsViewModel());
        }

        // /BrowseItems/ItemDetails?id=1  ->  Views/BrowseItems/ItemDetails.cshtml
        public ActionResult ItemDetails(int id)
        {
            var item = SampleItemData.Get(id);   // TODO: load from your database
            if (item == null) return HttpNotFound();
            return View(item);
        }
    }
}
