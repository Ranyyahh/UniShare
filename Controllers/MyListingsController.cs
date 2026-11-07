using System.Linq;
using System.Web.Mvc;
using UniShare.Models;

namespace UniShare.Controllers
{
    public class MyListingsController : Controller
    {
        // GET: Listings
        // TEMP: /Listings?empty=true previews the empty state. Remove the parameter once real data is wired up.
        public ActionResult ItemListings(bool empty = false)
        {
            var vm = new ListingsPageViewModel();

            if (!empty)
            {
                // TODO: replace with the signed-in student's listings from the database.
                const string img = "~/Content/img/placeholder.png";
                vm.Listings.AddRange(new[]
                {
                    new ListingItemViewModel { Id = 1, Title = "Casio fx-991EX ClassWiz Scientific Calculator", CategoryLabel = "Electronics & Gadgets", CategoryBadge = "Electronics", ImageUrl = img, Condition = "Like New", ConditionScore = 9.5m, Price = 25, PriceNote = "\u20B150 whole day", Location = "CEA Building Lobby (San Bartolome Campus)", Status = ListingStatus.Available },
                    new ListingItemViewModel { Id = 2, Title = "Engineering Mechanics: Statics & Dynamics", CategoryLabel = "Books & References", CategoryBadge = "Books", ImageUrl = img, Condition = "Excellent", ConditionScore = 9m, Price = 15, PriceNote = "\u20B130 whole day", Location = "University Library Front Desk", Status = ListingStatus.Available },
                    new ListingItemViewModel { Id = 3, Title = "Rotring Isograph Junior Pen Set (0.2, 0.3, 0.5)", CategoryLabel = "School Supplies (Drafting)", CategoryBadge = "Drafting", ImageUrl = img, Condition = "Like New", ConditionScore = 10m, Price = 30, PriceNote = "\u20B160 whole day", Location = "CEA Drafting Studio Room 304", Status = ListingStatus.Available },
                    new ListingItemViewModel { Id = 4, Title = "Casio Calculator Hard Slide-on Case", CategoryLabel = "Electronics & Accessories", CategoryBadge = "Accessories", ImageUrl = img, Condition = "Good", ConditionScore = 8m, Price = 10, PriceNote = "Fixed flat rate", Location = "Student Canteen Guard Station", Status = ListingStatus.Rented },
                    new ListingItemViewModel { Id = 5, Title = "Molten GG7X Official Indoor/Outdoor Basketball", CategoryLabel = "Sports Equipment", CategoryBadge = "Sports", ImageUrl = img, Condition = "Good", ConditionScore = 8.5m, Price = 20, PriceNote = "Standard match ball", Location = "QCU Gymnasium Entrance", Status = ListingStatus.Available }
                });
            }

            return View(vm);
        }

        // POST: MyListings/ToggleAvailability/5  (pause / resume)
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ToggleAvailability(int id)
        {
            // TODO: flip Available <-> Paused for this listing (check ownership first).
            return RedirectToAction("ItemListings");
        }

        // POST: MyListings/Delete/5
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            // TODO: delete the listing (check ownership first).
            return RedirectToAction("ItemListings");
        }

        // Still needed for the buttons: Create (GET/POST) and Edit(int id).
    }
}