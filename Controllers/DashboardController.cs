using System;
using System.Linq;
using System.Web.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using UniShare.Models;
using UniShare.Services;

namespace UniShare.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        public ActionResult Index()
        {
            ObjectId userId;
            if (!ObjectId.TryParse(User.Identity.Name, out userId)) return new HttpUnauthorizedResult();

            var user = MongoDatabase.Users.Find(x => x.Id == userId).FirstOrDefault();
            var documents = MarketplaceService.GetApprovedItems();
            var ownerIds = documents.Select(x => x.OwnerId).Distinct().ToList();
            var owners = ownerIds.Count == 0
                ? new System.Collections.Generic.List<UserDocument>()
                : MongoDatabase.Users.Find(Builders<UserDocument>.Filter.In(x => x.Id, ownerIds)).ToList();
            var ownerMap = owners.ToDictionary(x => x.Id);

            var categoryGroups = ItemCategoryCatalog.Names.Select(name => new { Key = name, Items = documents.Where(item => ItemCategoryCatalog.Normalize(item.Category) == name).ToList() }).ToList();
            var categoryIcons = new[] { "laptop", "book-open", "pen-tool", "activity", "box" };
            var categoryTints = new[] { "bg-blue-50 text-blue-600", "bg-indigo-50 text-indigo-600", "bg-blue-50 text-blue-600", "bg-sky-50 text-sky-600", "bg-indigo-50 text-indigo-600" };
            var categories = categoryGroups.Select((group, index) => new CategoryViewModel
            {
                Name = group.Key,
                Description = "Available items in " + group.Key,
                IconName = categoryIcons[Math.Min(index, categoryIcons.Length - 1)],
                TintClasses = categoryTints[Math.Min(index, categoryTints.Length - 1)],
                AvailableCount = group.Items.Count,
                Url = Url.Action("Index", "BrowseItems", new { category = group.Key })
            }).ToList();

            var featured = ItemCategoryCatalog.Names.Select(name => documents.FirstOrDefault(item => ItemCategoryCatalog.Normalize(item.Category) == name)).Where(item => item != null).Take(4).Select(item => ToDashboardItem(item, ownerMap)).ToList();
            var recent = documents.Take(4).Select(item => ToDashboardItem(item, ownerMap)).ToList();
            var myListingCount = MongoDatabase.Items.CountDocuments(x => x.OwnerId == userId);
            var myRentals = BookingService.ForRenter(userId);
            var activeRentals = myRentals.Count(x => new[] { "ONGOING", "ACTIVE" }.Contains(x.GetValue("status", "").ToString().Trim().ToUpperInvariant()));
            var pendingRequests = myRentals.Count(x => new[] { "PENDING", "REQUESTED" }.Contains(x.GetValue("status", "").ToString().Trim().ToUpperInvariant()));
            var completedRentals = myRentals.Count(x => new[] { "COMPLETED", "COMPLETE" }.Contains(x.GetValue("status", "").ToString().Trim().ToUpperInvariant()));

            var vm = new DashboardViewModel
            {
                UserName = user == null || string.IsNullOrWhiteSpace(user.FirstName) ? "Student" : user.FirstName,
                Metrics =
                {
                    new MetricViewModel { Title = "My Listings", Value = (int)myListingCount, IconName = "store", TintClasses = "bg-blue-50 text-blue-600", LinkText = "View Listings", LinkUrl = Url.Action("ItemListings", "MyListings") },
                    new MetricViewModel { Title = "Active Rentals", Value = (int)activeRentals, IconName = "calendar-check-2", TintClasses = "bg-sky-50 text-sky-600", LinkText = "View Details", LinkUrl = Url.Action("Index", "MyRentals") },
                    new MetricViewModel { Title = "Pending Requests", Value = (int)pendingRequests, IconName = "archive", TintClasses = "bg-amber-50 text-amber-600", LinkText = "Review Requests", LinkUrl = Url.Action("Index", "MyRentals") },
                    new MetricViewModel { Title = "Completed Rentals", Value = (int)completedRentals, IconName = "check-circle-2", TintClasses = "bg-blue-50 text-blue-600" }
                },
                Categories = categories,
                FeaturedItems = featured,
                RecentItems = recent
            };
            return View(vm);
        }

        private static RentalItemViewModel ToDashboardItem(ItemDocument item, System.Collections.Generic.Dictionary<ObjectId, UserDocument> owners)
        {
            var details = item.RentalDetails ?? new ItemRentalDetailsDocument();
            var pricePerHour = details.HourlyRate > 0
                ? (decimal)details.HourlyRate
                : (decimal)(details.PricePerDay / 24d);
            UserDocument owner;
            var ownerName = owners.TryGetValue(item.OwnerId, out owner)
                ? (owner.FirstName + " " + owner.LastName).Trim()
                : null;
            var ownerCourse = owner != null ? owner.Program : null;
            return new RentalItemViewModel
            {
                Id = item.Id.ToString(),
                Name = item.Name,
                Subtitle = item.Description,
                Campus = item.Location == null ? "Campus not specified" : item.Location.Campus,
                Condition = item.Condition == null ? "Good" : item.Condition.Replace('_', ' '),
                PricePerHour = pricePerHour,
                ImageUrl = item.Photos != null && item.Photos.Count > 0 ? MarketplaceService.ToImageUrl(item.Photos[0]) : null,
                OwnerName = ownerName,
                OwnerCourse = ownerCourse
            };
        }
    }
}
