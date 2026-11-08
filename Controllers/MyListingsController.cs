using System.Linq;
using System.Web.Mvc;
using UniShare.Models;

namespace UniShare.Controllers
{
    public class MyListingsController : Controller
    {
        // /MyListings
        public ActionResult Index()
        {
            return View(SampleData.MyListings);
        }

        // GET /MyListings/Create
        public ActionResult Create()
        {
            return View(new ListingForm());
        }

        // POST /MyListings/Create
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Create(ListingForm form)
        {
            if (!ModelState.IsValid) return View(form);

            int nextId = SampleData.MyListings.Max(i => i.Id) + 1;
            SampleData.MyListings.Add(new Item
            {
                Id = nextId,
                Name = form.Name,
                Category = form.Category,
                PricePerDay = form.PricePerDay,
                Condition = form.Condition,
                Description = form.Description,
                Owner = "Julian Vance",
                Status = "Available"
            });
            TempData["Success"] = "Listing posted.";
            return RedirectToAction("Index");
        }

        // POST /MyListings/ToggleVisibility/101
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ToggleVisibility(int id)
        {
            var item = SampleData.MyListings.FirstOrDefault(i => i.Id == id);
            if (item == null) return HttpNotFound();
            if (item.Status == "Rented") { TempData["Error"] = "A rented item cannot be hidden."; return RedirectToAction("Index"); }
            item.Status = item.Status == "Hidden" ? "Available" : "Hidden";
            return RedirectToAction("Index");
        }

        // POST /MyListings/Delete/101
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var item = SampleData.MyListings.FirstOrDefault(i => i.Id == id);
            if (item == null) return HttpNotFound();
            if (item.Status == "Rented") { TempData["Error"] = "A rented item cannot be deleted."; return RedirectToAction("Index"); }
            SampleData.MyListings.Remove(item);
            TempData["Success"] = "Listing deleted.";
            return RedirectToAction("Index");
        }
    }
}
