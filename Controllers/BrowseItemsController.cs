using System.Linq;
using System.Web.Mvc;
using UniShare.Models;

namespace UniShare.Controllers
{
    public class BrowseItemsController : Controller
    {
        // /BrowseItems?q=calc&category=Books
        public ActionResult Index(string q, string category)
        {
            var items = SampleData.Items.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(q))
                items = items.Where(i => i.Name.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) >= 0);
            if (!string.IsNullOrWhiteSpace(category))
                items = items.Where(i => i.Category == category);

            ViewBag.Query = q;
            ViewBag.Category = category;
            return View(items.ToList());
        }

        // /BrowseItems/Details/3
        public ActionResult Details(int id)
        {
            var item = SampleData.Items.FirstOrDefault(i => i.Id == id);
            if (item == null) return HttpNotFound();
            return View(item);
        }

        // POST /BrowseItems/RequestRental/3
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult RequestRental(int id)
        {
            var item = SampleData.Items.FirstOrDefault(i => i.Id == id);
            if (item == null) return HttpNotFound();
            TempData["Success"] = "Rental request sent to " + item.Owner + " for " + item.Name + ".";
            return RedirectToAction("Details", new { id = id });
        }
    }
}
