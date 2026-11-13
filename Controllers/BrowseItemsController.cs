using System;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using UniShare.Models;
using UniShare.Services;

namespace UniShare.Controllers
{
    public class BrowseItemsController : Controller
    {
        public ActionResult Index()
        {
            var documents = MarketplaceService.GetApprovedItems();
            var ownerIds = documents.Select(x => x.OwnerId).Distinct().ToList();
            var owners = ownerIds.Count == 0
                ? new System.Collections.Generic.List<UserDocument>()
                : MongoDatabase.Users.Find(Builders<UserDocument>.Filter.In(x => x.Id, ownerIds)).ToList();
            var ownerMap = owners.ToDictionary(x => x.Id);
            var ratingDocs = MongoDatabase.Ratings.Find(Builders<BsonDocument>.Filter.Ne("isHidden", true)).ToList();
            var ratingMap = ratingDocs.Where(x => x.GetValue("reviewedUserId", BsonNull.Value).IsObjectId && x.GetValue("rating", BsonNull.Value).IsNumeric)
                .GroupBy(x => x["reviewedUserId"].AsObjectId).ToDictionary(g => g.Key, g => new { Average = g.Average(x => x["rating"].ToDouble()), Count = g.Count() });
            var bookingDocs = MongoDatabase.Bookings.Find(Builders<BsonDocument>.Filter.Empty).ToList();

            var model = new BrowseItemsViewModel();
            model.Items = documents.Select(item =>
            {
                var details = item.RentalDetails ?? new ItemRentalDetailsDocument();
                var location = item.Location ?? new ItemLocationDocument();
                var available = string.Equals(item.AvailabilityStatus, "AVAILABLE", StringComparison.OrdinalIgnoreCase);
                var hourly = details.HourlyRate > 0 ? (decimal)details.HourlyRate : (decimal)(details.PricePerDay / 24d);
                UserDocument owner;
                var rating = ratingMap.ContainsKey(item.OwnerId) ? ratingMap[item.OwnerId] : null;
                var photo = item.Photos != null && item.Photos.Count > 0 ? MarketplaceService.ToImageUrl(item.Photos[0]) : null;
                return new ItemViewModel
                {
                    ItemId = item.Id.ToString(),
                    ItemName = item.Name,
                    Description = item.Description,
                    Category = ItemCategoryCatalog.Normalize(item.Category),
                    Condition = ToConditionLabel(item.Condition),
                    Campus = location.Campus,
                    ImageUrl = photo,
                    Availability = available ? "Available" : item.AvailabilityStatus,
                    IsAvailable = available,
                    HourlyRate = hourly,
                    DailyRate = (decimal)(details.DailyRate > 0 ? details.DailyRate : details.PricePerDay),
                    OwnerName = ownerMap.TryGetValue(item.OwnerId, out owner) ? (owner.FirstName + " " + owner.LastName).Trim() : null,
                    Rating = rating == null ? (double?)null : rating.Average,
                    ReviewCount = rating == null ? 0 : rating.Count,
                    BookingCount = bookingDocs.Count(b => b.GetValue("itemId", BsonNull.Value).IsObjectId && b["itemId"].AsObjectId == item.Id && !new[] { "CANCELLED", "CANCELED", "DECLINED", "EXPIRED", "REJECTED" }.Contains(b.GetValue("status", "").ToString().ToUpperInvariant())),
                    CreatedAtTicks = item.CreatedAt.Ticks
                };
            }).ToList();
            model.Categories = new System.Collections.Generic.List<string>(ItemCategoryCatalog.Names);
            model.Conditions = new System.Collections.Generic.List<string> { "Like New", "Good", "Fair", "Heavy Use" };
            model.Campuses = new System.Collections.Generic.List<string> { "San Bartolome", "San Francisco", "Batasan" };
            return View(model);
        }

        public ActionResult ItemDetails(string id)
        {
            var authenticated = User != null && User.Identity != null && User.Identity.IsAuthenticated;
            var verified = false;
            if (authenticated)
            {
                ObjectId userId;
                if (ObjectId.TryParse(User.Identity.Name, out userId))
                {
                    var user = MongoDatabase.Users.Find(x => x.Id == userId).FirstOrDefault();
                    verified = user != null && user.VerificationStatus == "APPROVED";
                }
            }
            ViewBag.IsAuthenticated = authenticated;
            ViewBag.IsStudentVerified = verified;
            var item = MarketplaceService.GetItem(id);
            if (item == null) return HttpNotFound();
            var owner = MongoDatabase.Users.Find(x => x.Id == item.OwnerId).FirstOrDefault();
            return View(MarketplaceService.ToDetails(item, owner));
        }

        public ActionResult Details(string id)
        {
            return RedirectToAction("ItemDetails", new { id = id });
        }

        public ActionResult OwnerProfile(string id)
        {
            ObjectId ownerId;
            if (!ObjectId.TryParse(id, out ownerId)) return HttpNotFound();
            var owner = MongoDatabase.Users.Find(x => x.Id == ownerId).FirstOrDefault();
            if (owner == null) return HttpNotFound();
            var fullName = (owner.FirstName + " " + owner.LastName).Trim();
            var ownerItems = MongoDatabase.Items.Find(x => x.OwnerId == ownerId && x.ApprovalStatus == "APPROVED" && x.AvailabilityStatus == "AVAILABLE")
                .SortByDescending(x => x.CreatedAt).ToList().Select(MarketplaceService.ToListing).ToList();
            var reviewFilter = (Builders<BsonDocument>.Filter.Eq("reviewedUserId", ownerId) | Builders<BsonDocument>.Filter.Eq("reviewedUserId", ownerId.ToString())) &
                Builders<BsonDocument>.Filter.Ne("isHidden", true);
            var reviewDocs = MongoDatabase.Ratings.Find(reviewFilter).SortByDescending(x => x["createdAt"]).Limit(50).ToList();
            var reviewers = reviewDocs.Select(x => x.GetValue("reviewerId", BsonNull.Value)).Where(x => x.IsObjectId).Select(x => x.AsObjectId).Distinct().ToList();
            var reviewerMap = reviewers.Count == 0 ? new System.Collections.Generic.List<UserDocument>() : MongoDatabase.Users.Find(Builders<UserDocument>.Filter.In(x => x.Id, reviewers)).ToList();
            var reviewerNames = reviewerMap.ToDictionary(x => x.Id, x => ((x.FirstName ?? "") + " " + (x.LastName ?? "")).Trim());
            return View("~/Views/BrowseItems/OwnerProfile.cshtml", new OwnerProfileViewModel
            {
                OwnerId = ownerId.ToString(),
                Name = fullName,
                Initials = string.Concat(fullName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Take(2).Select(x => char.ToUpperInvariant(x[0]))),
                Campus = owner.Campus,
                Program = owner.Program,
                YearLevel = owner.YearLevel,
                Bio = owner.Bio,
                ProfilePhoto = MarketplaceService.ToImageUrl(owner.ProfilePhoto),
                IsVerified = string.Equals(owner.VerificationStatus, "APPROVED", StringComparison.OrdinalIgnoreCase),
                Rating = MarketplaceService.GetOwnerRatingSummary(ownerId).Item1,
                ReviewCount = MarketplaceService.GetOwnerRatingSummary(ownerId).Item2,
                CompletedRentals = MarketplaceService.GetCompletedRentalCount(ownerId),
                Listings = ownerItems,
                Reviews = reviewDocs.Select(x =>
                {
                    var reviewerIdValue = x.GetValue("reviewerId", BsonNull.Value);
                    var reviewerId = reviewerIdValue.IsObjectId ? (ObjectId?)reviewerIdValue.AsObjectId : null;
                    var itemValue = x.GetValue("itemRating", BsonNull.Value);
                    var ownerValue = x.GetValue("ownerRating", x.GetValue("rating", BsonNull.Value));
                    var createdValue = x.GetValue("createdAt", BsonDateTime.Create(DateTime.UtcNow));
                    return new PublicReviewViewModel
                    {
                        Id = x.GetValue("_id", ObjectId.Empty).ToString(), ReviewerId = reviewerId.HasValue ? reviewerId.Value.ToString() : "",
                        ReviewerName = reviewerId.HasValue && reviewerNames.ContainsKey(reviewerId.Value) ? reviewerNames[reviewerId.Value] : "Student",
                        ItemRating = itemValue.IsNumeric ? (int?)itemValue.ToInt32() : null, OwnerRating = ownerValue.IsNumeric ? (int?)ownerValue.ToInt32() : null,
                        Review = x.GetValue("review", "").ToString(), CreatedAt = createdValue.IsValidDateTime ? createdValue.ToUniversalTime().ToLocalTime() : DateTime.MinValue
                    };
                }).ToList()
            });
        }

        [HttpGet, VerifiedStudent]
        public ActionResult RequestToRent(string id)
        {
            var item = MarketplaceService.GetItem(id);
            if (item == null) return HttpNotFound();
            var details = MarketplaceService.ToDetails(item, MongoDatabase.Users.Find(x => x.Id == item.OwnerId).FirstOrDefault());
            if (!details.IsAvailable) return RedirectToAction("ItemDetails", new { id = id });
            return View(details);
        }

        [HttpGet]
        public ActionResult UnavailableHandoverSlots(string id, string date, int duration = 2)
        {
            ObjectId itemId;
            DateTime selectedDate;
            if (!ObjectId.TryParse(id, out itemId) || !DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out selectedDate))
                return new HttpStatusCodeResult(400);
            if (MarketplaceService.GetItem(id) == null) return HttpNotFound();
            try
            {
                return Json(new { unavailableSlots = BookingService.GetUnavailableHandoverSlots(itemId, selectedDate, duration) }, JsonRequestBehavior.AllowGet);
            }
            catch (BookingException ex) { return new HttpStatusCodeResult(400, ex.Message); }
        }

        [HttpPost, ValidateAntiForgeryToken, VerifiedStudent]
        public ActionResult RequestToRent(string id, string handoverDate, string handoverTime, int duration, string notes)
        {
            ObjectId renterId;
            if (!ObjectId.TryParse(User.Identity.Name, out renterId)) return new HttpStatusCodeResult(401);
            DateTime selectedDate;
            if (!DateTime.TryParseExact(handoverDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out selectedDate))
            {
                ModelState.AddModelError("handoverDate", "Choose a valid handover date.");
                var invalidDateItem = MarketplaceService.GetItem(id);
                if (invalidDateItem == null) return HttpNotFound();
                return View(MarketplaceService.ToDetails(invalidDateItem, MongoDatabase.Users.Find(x => x.Id == invalidDateItem.OwnerId).FirstOrDefault()));
            }
            try
            {
                var booking = BookingService.Submit(renterId, id, selectedDate, handoverTime, duration, notes);
                return RedirectToAction("RequestSubmitted", new { id = booking["_id"].AsObjectId.ToString() });
            }
            catch (BookingException ex)
            {
                ModelState.AddModelError("", ex.Message);
                var item = MarketplaceService.GetItem(id);
                if (item == null) return HttpNotFound();
                return View(MarketplaceService.ToDetails(item, MongoDatabase.Users.Find(x => x.Id == item.OwnerId).FirstOrDefault()));
            }
        }

        [HttpGet, Authorize]
        public ActionResult RequestSubmitted(string id)
        {
            ObjectId renterId;
            if (!ObjectId.TryParse(User.Identity.Name, out renterId)) return new HttpStatusCodeResult(401);
            var booking = BookingService.GetForParticipant(id, renterId);
            if (booking == null || !booking.GetValue("borrowerId", BsonNull.Value).IsObjectId || booking["borrowerId"].AsObjectId != renterId) return HttpNotFound();
            ObjectId itemId;
            if (!ObjectId.TryParse(booking.GetValue("itemId", "").ToString(), out itemId)) return HttpNotFound();
            var item = MarketplaceService.GetItem(itemId.ToString());
            if (item == null) return HttpNotFound();
            ViewBag.BookingId = booking.GetValue("requestId", booking["_id"].ToString()).ToString();
            ViewBag.HandoverDate = booking.GetValue("handoverDate", booking.GetValue("startDate", BsonDateTime.Create(DateTime.UtcNow))).ToUniversalTime().ToLocalTime();
            ViewBag.HandoverTime = booking.GetValue("handoverTime", "").ToString();
            ViewBag.Duration = booking.GetValue("durationHours", 1).ToInt32();
            var feeValue = booking.GetValue("estimatedFee", booking.GetValue("rentalFee", booking.GetValue("totalFee", BsonInt32.Create(0))));
            ViewBag.Total = feeValue.IsNumeric ? feeValue.ToDecimal() : 0m;
            ViewBag.Status = booking.GetValue("status", "PENDING").ToString();
            return View(MarketplaceService.ToDetails(item, MongoDatabase.Users.Find(x => x.Id == item.OwnerId).FirstOrDefault()));
        }

        [HttpPost, ValidateAntiForgeryToken, VerifiedStudent]
        public ActionResult RequestRental(string id)
        {
            var item = MarketplaceService.GetItem(id);
            if (item == null) return HttpNotFound();
            if (!string.Equals(item.AvailabilityStatus, "AVAILABLE", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("ItemDetails", new { id = id });
            return RedirectToAction("RequestToRent", new { id = id });
        }

        private static string ToConditionLabel(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Good";
            var normalized = value.Replace("_", " ").Replace("-", " ").Trim().ToLowerInvariant();
            if (normalized == "like new" || normalized == "likenew") return "Like New";
            if (normalized == "heavy use" || normalized == "heavyuse") return "Heavy Use";
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized);
        }
    }
}
