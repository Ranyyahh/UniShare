using UniShare.Models;
using System.Web.Mvc;

namespace UniShare.Controllers
{
    public class DashboardController : Controller
    {
        public ActionResult Index()
        {
            // TODO: replace sample data with database/service calls.
            const string img = "~/Content/img/placeholder.png";

            var vm = new DashboardViewModel
            {
                UserName = "Alex",
                Metrics =
                {
                    new MetricViewModel { Title = "My Listings", Value = 4, IconName = "store", TintClasses = "bg-blue-50 text-blue-600", LinkText = "View Listings ?" },
                    new MetricViewModel { Title = "Active Rentals", Value = 1, IconName = "calendar-check-2", TintClasses = "bg-sky-50 text-sky-600", LinkText = "View Details ?" },
                    new MetricViewModel { Title = "Pending Requests", Value = 2, IconName = "archive", TintClasses = "bg-amber-50 text-amber-600", LinkText = "Review ?" },
                    new MetricViewModel { Title = "Completed Rentals", Value = 18, IconName = "check-circle-2", TintClasses = "bg-blue-50 text-blue-600", Footnote = "All time across 2 semesters" }
                },
                Categories =
                {
                    new CategoryViewModel { Name = "Electronics", Description = "Drawing tablets, USB hubs,...", IconName = "laptop", TintClasses = "bg-blue-50 text-blue-600", AvailableCount = 42 },
                    new CategoryViewModel { Name = "Books", Description = "Calculus, Data Structures,...", IconName = "book-open", TintClasses = "bg-indigo-50 text-indigo-600", AvailableCount = 35 },
                    new CategoryViewModel { Name = "School Supplies", Description = "Scientific calcs, T-squares, drafting", IconName = "pen-tool", TintClasses = "bg-blue-50 text-blue-600", AvailableCount = 28 },
                    new CategoryViewModel { Name = "Sports Equipment", Description = "Badminton rackets, chess sets,...", IconName = "activity", TintClasses = "bg-sky-50 text-sky-600", AvailableCount = 16 },
                    new CategoryViewModel { Name = "Other Items", Description = "Tripods, clickers, lab coats", IconName = "box", TintClasses = "bg-indigo-50 text-indigo-600", AvailableCount = 19 }
                },
                FeaturedItems =
                {
                    new RentalItemViewModel { Id = 1, Name = "Casio fx-991EX ClassWiz", Condition = "Like New", PricePerHour = 25, OwnerName = "Maria S.", OwnerCourse = "BSCE", ImageUrl = img },
                    new RentalItemViewModel { Id = 2, Name = "Huion Inspiroy H640P Graphic Drawing Tablet", Condition = "Excellent", PricePerHour = 120, OwnerName = "Kenneth D.", OwnerCourse = "BSIT", ImageUrl = img },
                    new RentalItemViewModel { Id = 3, Name = "Staedtler 36-inch T-Square", Condition = "Good", PricePerHour = 40, OwnerName = "Angelo C.", OwnerCourse = "BSA", ImageUrl = img },
                    new RentalItemViewModel { Id = 4, Name = "Sony WH-CH520 Wireless Headset", Condition = "Like New", PricePerHour = 80, OwnerName = "Aira G.", OwnerCourse = "BSIT", ImageUrl = img }
                },
                RecentItems =
                {
                    new RentalItemViewModel { Id = 5, Name = "Engineering Mechanics", Subtitle = "Statics 14th Ed. (Hibbeler)", Condition = "Good", PricePerHour = 35, ImageUrl = img },
                    new RentalItemViewModel { Id = 6, Name = "Anker 7-in-1 USB-C Hub", Subtitle = "HDMI 4K, 100W PD, SD", Condition = "Mint", ConditionBadgeClasses = "bg-emerald-50 text-emerald-700 border border-emerald-100", PricePerHour = 45, ImageUrl = img },
                    new RentalItemViewModel { Id = 7, Name = "Yonex Carbonex Set", Subtitle = "2 Rackets + 3 Shuttlecocks", Condition = "Very Good", PricePerHour = 60, ImageUrl = img },
                    new RentalItemViewModel { Id = 8, Name = "Lab Coat + Goggles", Subtitle = "Size M, unisex, sanitised", Condition = "Clean & Pressed", ConditionBadgeClasses = "bg-sky-50 text-sky-700 border border-sky-100", PricePerHour = 30, ImageUrl = img }
                }
            };

            return View(vm);
        }
    }
}
