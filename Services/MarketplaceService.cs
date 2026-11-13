using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using MongoDB.Bson;
using MongoDB.Driver;
using UniShare.Models;

namespace UniShare.Services
{
    public static class MarketplaceService
    {
        public static List<ItemDocument> GetApprovedItems()
        {
            return MongoDatabase.Items.Find(x => x.ApprovalStatus == "APPROVED" && x.AvailabilityStatus == "AVAILABLE")
                .SortByDescending(x => x.CreatedAt).ToList();
        }

        public static ItemDocument GetItem(string id)
        {
            ObjectId objectId;
            if (!ObjectId.TryParse(id, out objectId)) return null;
            return MongoDatabase.Items.Find(x => x.Id == objectId && x.ApprovalStatus == "APPROVED" && x.AvailabilityStatus == "AVAILABLE").FirstOrDefault();
        }

        public static List<ListingItemViewModel> GetOwnerListings(ObjectId ownerId)
        {
            return MongoDatabase.Items.Find(x => x.OwnerId == ownerId)
                .SortByDescending(x => x.CreatedAt).ToList().Select(ToListing).ToList();
        }

        public static ItemDocument GetOwnerItem(ObjectId ownerId, string id)
        {
            ObjectId objectId;
            if (!ObjectId.TryParse(id, out objectId)) return null;
            return MongoDatabase.Items.Find(x => x.Id == objectId && x.OwnerId == ownerId).FirstOrDefault();
        }

        public static Tuple<double?, int> GetOwnerRatingSummary(ObjectId ownerId)
        {
            var documents = MongoDatabase.Ratings.Find(Builders<BsonDocument>.Filter.Eq("reviewedUserId", ownerId)).ToList();
            var values = documents.Select(x => x.GetValue("rating", BsonNull.Value))
                .Where(x => x.IsNumeric).Select(x => x.ToDouble()).ToList();
            return Tuple.Create(values.Count == 0 ? (double?)null : values.Average(), values.Count);
        }

        public static int GetCompletedRentalCount(ObjectId ownerId)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("ownerId", ownerId) &
                Builders<BsonDocument>.Filter.In("status", new[] { "COMPLETED", "COMPLETE" });
            return (int)MongoDatabase.Rentals.CountDocuments(filter);
        }

        public static ListingItemViewModel ToListing(ItemDocument item)
        {
            var details = item.RentalDetails ?? new ItemRentalDetailsDocument();
            var location = item.Location ?? new ItemLocationDocument();
            var unitHours = details.PricingUnitHours;
            var price = unitHours > 0 && details.HourlyRate > 0
                ? (decimal)details.HourlyRate * unitHours
                : (decimal)(details.PricePerDay > 0 ? details.PricePerDay : details.DailyRate);
            if (unitHours <= 0) unitHours = 24;
            var category = ItemCategoryCatalog.Normalize(item.Category);
            var categoryId = CategoryId(category);
            var status = string.Equals(item.AvailabilityStatus, "RENTED", StringComparison.OrdinalIgnoreCase)
                ? ListingStatus.Rented
                : string.Equals(item.AvailabilityStatus, "PAUSED", StringComparison.OrdinalIgnoreCase)
                    ? ListingStatus.Paused : ListingStatus.Available;

            return new ListingItemViewModel
            {
                Id = item.Id.ToString(),
                Title = item.Name,
                CategoryLabel = category,
                CategoryBadge = CategoryBadge(category),
                CategoryId = categoryId,
                ImageUrl = item.Photos != null && item.Photos.Count > 0 ? ToImageUrl(item.Photos[0]) : null,
                Condition = NormalizeCondition(item.Condition),
                Price = price,
                PerHours = unitHours,
                PriceNote = details.EnableFullDayBundle ? "₱" + (decimal)details.DailyRate + " whole day" : "Daily rate ₱" + (decimal)details.PricePerDay,
                Location = FormatLocation(location),
                Status = status,
                EarliestHandoverTime = item.EarliestHandoverTime,
                LatestReturnTime = item.LatestReturnTime,
                Description = item.Description,
                EnableFullDayBundle = details.EnableFullDayBundle,
                AvailableDays = item.AvailableDays ?? new List<string>(),
                ReplacementValue = (decimal)details.ReplacementValue,
                SecurityDeposit = (decimal)details.SecurityDeposit,
                Accessories = item.Accessories,
                ApprovalStatus = string.IsNullOrWhiteSpace(item.ApprovalStatus) ? "PENDING" : item.ApprovalStatus,
                RejectionReason = item.ApprovalRejectionReason,
                CreatedAt = item.CreatedAt
            };
        }

        public static ItemDetailsViewModel ToDetails(ItemDocument item, UserDocument owner)
        {
            var details = item.RentalDetails ?? new ItemRentalDetailsDocument();
            var location = item.Location ?? new ItemLocationDocument();
            var rate = details.HourlyRate > 0 ? (decimal)details.HourlyRate : (decimal)(details.PricePerDay / 24d);
            var ownerName = owner == null ? null : (owner.FirstName + " " + owner.LastName).Trim();
            var images = (item.Photos ?? new List<string>()).Select(ToImageUrl).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            var rating = GetOwnerRatingSummary(item.OwnerId);
            var ownerLocation = owner != null && !string.IsNullOrWhiteSpace(owner.Campus) ? owner.Campus : location.Campus;

            return new ItemDetailsViewModel
            {
                ItemId = item.Id.ToString(),
                OwnerId = item.OwnerId.ToString(),
                ItemCode = "QCU-" + item.Id.ToString().Substring(18).ToUpperInvariant(),
                Name = item.Name,
                Title = item.Name,
                Category = ItemCategoryCatalog.Normalize(item.Category),
                CategoryLabel = ItemCategoryCatalog.Normalize(item.Category),
                Subcategory = item.Category,
                Condition = NormalizeCondition(item.Condition),
                Campus = location.Campus,
                Description = item.Description,
                HourlyRate = rate,
                DailyRate = (decimal)(details.DailyRate > 0 ? details.DailyRate : details.PricePerDay),
                IsAvailable = string.Equals(item.AvailabilityStatus, "AVAILABLE", StringComparison.OrdinalIgnoreCase),
                ListedText = item.CreatedAt == default(DateTime) ? null : item.CreatedAt.ToLocalTime().ToString("MMM d, yyyy", CultureInfo.InvariantCulture),
                AvailableHours = item.EarliestHandoverTime != null && item.LatestReturnTime != null ? item.EarliestHandoverTime + "–" + item.LatestReturnTime : null,
                HandoverZones = FormatLocation(location),
                ConditionNotes = null,
                ReplacementValue = (decimal)details.ReplacementValue,
                Accessories = item.Accessories,
                Gallery = images.Select((image, index) => new GalleryImageViewModel
                {
                    ImageUrl = image, Label = index == 0 ? "Cover" : "Photo " + (index + 1)
                }).ToList(),
                Rating = rating.Item1,
                ReviewCount = rating.Item2,
                AvailableDays = item.AvailableDays ?? new List<string>(),
                Lender = new LenderViewModel
                {
                    Name = ownerName,
                    Initials = Initials(ownerName),
                    College = ownerLocation,
                    Program = owner != null ? string.Join(" ", new[] { owner.Program, owner.YearLevel }.Where(x => !string.IsNullOrWhiteSpace(x))) : null,
                    ProgramCode = owner != null ? owner.Program : null,
                    Rating = rating.Item1,
                    ReviewCount = rating.Item2,
                    CompletedRentals = GetCompletedRentalCount(item.OwnerId),
                    IsVerified = owner != null && string.Equals(owner.VerificationStatus, "APPROVED", StringComparison.OrdinalIgnoreCase)
                }
            };
        }

        public static string ToImageUrl(string photo)
        {
            if (string.IsNullOrWhiteSpace(photo)) return null;
            if (photo.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return photo;
            if (photo.StartsWith("~/", StringComparison.Ordinal)) photo = photo.Substring(2);
            var fileName = System.IO.Path.GetFileName(photo);
            Guid uploadId;
            if (!Guid.TryParseExact(System.IO.Path.GetFileNameWithoutExtension(fileName), "N", out uploadId)) return null;
            var physical = HttpContext.Current == null ? null : HttpContext.Current.Server.MapPath("~/Uploads/" + fileName);
            if (physical != null && !System.IO.File.Exists(physical)) return null;
            return VirtualPathUtility.ToAbsolute("~/Uploads/" + fileName);
        }

        public static string FormatLocation(ItemLocationDocument location)
        {
            if (location == null) return null;
            if (!string.IsNullOrWhiteSpace(location.HandoverLocation)) return location.HandoverLocation;
            return string.Join(", ", new[] { location.Building, location.Room, location.Campus }.Where(x => !string.IsNullOrWhiteSpace(x)));
        }

        private static DateTime? ReadDate(BsonDocument document, string field)
        {
            BsonValue value;
            if (!document.TryGetValue(field, out value) || value == BsonNull.Value) return null;
            if (value.IsValidDateTime) return value.ToUniversalTime();
            DateTime parsed;
            return DateTime.TryParse(value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out parsed)
                ? parsed.ToUniversalTime() : (DateTime?)null;
        }

        private static string NormalizeCondition(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Not specified";
            var normalized = value.Replace('_', ' ').ToLowerInvariant();
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized);
        }

        private static string Initials(string name)
        {
            var parts = (name ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Concat(parts.Take(2).Select(p => char.ToUpperInvariant(p[0])));
        }

        private static int CategoryId(string category)
        {
            if (category.IndexOf("accessor", StringComparison.OrdinalIgnoreCase) >= 0) return 7;
            if (category.IndexOf("camera", StringComparison.OrdinalIgnoreCase) >= 0) return 2;
            if (category.IndexOf("book", StringComparison.OrdinalIgnoreCase) >= 0) return 3;
            if (category.IndexOf("draft", StringComparison.OrdinalIgnoreCase) >= 0 || category.IndexOf("school supplies", StringComparison.OrdinalIgnoreCase) >= 0) return 4;
            if (category.IndexOf("sport", StringComparison.OrdinalIgnoreCase) >= 0) return 5;
            if (category.IndexOf("electronic", StringComparison.OrdinalIgnoreCase) >= 0 || category.IndexOf("camera", StringComparison.OrdinalIgnoreCase) >= 0) return 1;
            return 6;
        }

        private static string CategoryBadge(string category)
        {
            switch (CategoryId(category))
            {
                case 1: return "Gadgets";
                case 2: return "Cameras";
                case 3: return "Books";
                case 4: return "School Supplies";
                case 5: return "Sports";
                case 7: return "Accessories";
                default: return "Other";
            }
        }
    }
}
