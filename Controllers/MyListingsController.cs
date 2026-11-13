using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using UniShare.Models;
using UniShare.Services;

namespace UniShare.Controllers
{
    public class RentalRecordsPageViewModel
    {
        public string Title { get; set; }
        public string ActiveTab { get; set; }
        public List<RentalRecordDto> Rentals { get; set; }
    }

    [Authorize]
    public class MyListingsController : Controller
    {
        private ObjectId CurrentOwnerId { get { return ObjectId.Parse(User.Identity.Name); } }

        // ===================== LIST =====================

        // GET: MyListings / Listings
        public ActionResult Index()
        {
            return RedirectToAction("ItemListings");
        }

        // GET: MyListings/ItemListings
        public ActionResult ItemListings()
        {
            var vm = new ListingsPageViewModel();
            vm.Listings = MarketplaceService.GetOwnerListings(CurrentOwnerId);
            var ownerBookings = BookingService.ForOwner(CurrentOwnerId);
            vm.PendingRequestCount = ownerBookings.Count(x => new[] { "PENDING", "REQUESTED" }
                .Contains(x.GetValue("status", "").ToString().Trim().ToUpperInvariant()));
            vm.ActiveRentalCount = ownerBookings.Count(x => new[] { "ACCEPTED", "APPROVED", "ACTIVE", "ONGOING", "OVERDUE", "RETURN_REQUESTED", "RETURN PENDING", "DISPUTED" }
                .Contains(x.GetValue("status", "").ToString().Trim().ToUpperInvariant()));

            return View("~/Views/MyListings/ItemListings.cshtml", vm);
        }

        [HttpGet]
        public ActionResult Details(string id)
        {
            var document = MarketplaceService.GetOwnerItem(CurrentOwnerId, id);
            if (document == null) return HttpNotFound();
            return View("~/Views/MyListings/ListingDetails.cshtml", MarketplaceService.ToListing(document));
        }

        [HttpGet]
        public ActionResult Ongoing()
        {
            BorrowedItemsService.RefreshOverdue(CurrentOwnerId);
            var rentals = BookingService.ForOwner(CurrentOwnerId)
                .Where(x => new[] { "ACCEPTED", "APPROVED", "ACTIVE", "ONGOING", "OVERDUE", "RETURN_REQUESTED", "RETURN PENDING", "DISPUTED" }
                    .Contains(x.GetValue("status", "").ToString().Trim().ToUpperInvariant()))
                .Select(MyRentalsController.MapBooking).ToList();
            return View("~/Views/MyListings/RentalRecords.cshtml", new RentalRecordsPageViewModel { Title = "Ongoing Rentals", ActiveTab = "ongoing", Rentals = rentals });
        }

        [HttpGet]
        public ActionResult PastTransactions()
        {
            var rentals = BookingService.ForOwner(CurrentOwnerId)
                .Where(x => new[] { "COMPLETED", "COMPLETE", "RETURNED", "CANCELLED", "CANCELED", "DECLINED", "REJECTED", "EXPIRED" }
                    .Contains(x.GetValue("status", "").ToString().Trim().ToUpperInvariant()))
                .Select(MyRentalsController.MapBooking).ToList();
            return View("~/Views/MyListings/RentalRecords.cshtml", new RentalRecordsPageViewModel { Title = "Past Transactions", ActiveTab = "past", Rentals = rentals });
        }

        // POST: MyListings/ToggleAvailability/5  (pause / resume)
        [HttpPost, ValidateAntiForgeryToken]
        [VerifiedStudent]
        public ActionResult ToggleAvailability(string id)
        {
            var item = MarketplaceService.GetOwnerItem(CurrentOwnerId, id);
            if (item != null && !string.Equals(item.AvailabilityStatus, "RENTED", StringComparison.OrdinalIgnoreCase))
            {
                var next = string.Equals(item.AvailabilityStatus, "AVAILABLE", StringComparison.OrdinalIgnoreCase) ? "PAUSED" : "AVAILABLE";
                MongoDatabase.Items.UpdateOne(x => x.Id == item.Id && x.OwnerId == CurrentOwnerId,
                    Builders<ItemDocument>.Update.Set(x => x.AvailabilityStatus, next).Set(x => x.UpdatedAt, DateTime.UtcNow));
            }
            return RedirectToAction("ItemListings");
        }

        // Keep the earlier action name as an alias while the UI uses ToggleAvailability.
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ToggleVisibility(string id)
        {
            return ToggleAvailability(id);
        }

        // POST: MyListings/Delete   (id comes from the delete dialog's hidden field)
        [HttpPost, ValidateAntiForgeryToken]
        [VerifiedStudent]
        public ActionResult Delete(string id)
        {
            var item = MarketplaceService.GetOwnerItem(CurrentOwnerId, id);
            if (item != null)
            {
                if (string.Equals(item.AvailabilityStatus, "RENTED", StringComparison.OrdinalIgnoreCase) || HasBlockingBooking(item.Id))
                {
                    TempData["Error"] = "Cannot delete a listing with an active rental or reservation.";
                    return RedirectToAction("ItemListings");
                }
                var deleted = MongoDatabase.Items.DeleteOne(x => x.Id == item.Id && x.OwnerId == CurrentOwnerId && x.AvailabilityStatus != "RENTED");
                if (deleted.DeletedCount > 0)
                {
                    DeletePhotos(item.Photos);
                    TempData["Message"] = "Listing deleted.";
                }
                else TempData["Error"] = "The listing could not be deleted. It may now be rented.";
            }
            return RedirectToAction("ItemListings");
        }

        // ===================== CREATE =====================

        // GET: MyListings/Create
        [HttpGet]
        [VerifiedStudent]
        public ActionResult Create()
        {
            var model = new CreateListingViewModel();
            PopulateOptions(model);
            return View("~/Views/MyListings/CreateListing.cshtml", model);
        }

        // GET: MyListings/CreateListing
        [HttpGet]
        [VerifiedStudent]
        public ActionResult CreateListing()
        {
            return RedirectToAction("Create");
        }

        // POST: MyListings/Create
        [HttpPost, ValidateAntiForgeryToken]
        [VerifiedStudent]
        public ActionResult Create(CreateListingViewModel model)
        {
            ValidatePhotos(GetUploadedFiles(model), 0, 5);
            ValidateSchedule(model);
            var catOptions = GetCategoryOptions();
            var selectedCat = model.CategoryId.HasValue ? catOptions.FirstOrDefault(c => c.Id == model.CategoryId.Value) : null;
            if (selectedCat == null) ModelState.AddModelError("CategoryId", "Choose a valid category.");

            if (!ModelState.IsValid)
            {
                PopulateOptions(model);
                return View("~/Views/MyListings/CreateListing.cshtml", model);
            }

            var photos = GetUploadedFiles(model);
            List<string> savedPhotos;
            try
            {
                savedPhotos = SavePhotos(photos);
            }
            catch (IOException)
            {
                ModelState.AddModelError("Photos", "The uploaded photos could not be saved. Please try again.");
                PopulateOptions(model);
                return View("~/Views/MyListings/CreateListing.cshtml", model);
            }

            var now = DateTime.UtcNow;
            var rate = model.Price.GetValueOrDefault();
            var unitHours = model.PricingUnitHours > 0 ? model.PricingUnitHours : 4;
            var dailyRate = model.EnableFullDayBundle ? rate * 2 : rate * 24 / unitHours;
            var item = new ItemDocument
            {
                Id = ObjectId.GenerateNewId(),
                OwnerId = CurrentOwnerId,
                Name = model.ItemName.Trim(),
                Description = model.Description.Trim(),
                Category = selectedCat != null ? selectedCat.Name : "Others",
                Photos = savedPhotos,
                Condition = ToStoredCondition(model.Condition),
                AvailabilityStatus = "AVAILABLE",
                ApprovalStatus = "PENDING",
                AvailableDays = NormalizeAvailableDays(model.AvailableDays),
                EarliestHandoverTime = model.EarliestHandoverTime,
                LatestReturnTime = model.LatestReturnTime,
                Accessories = model.Accessories,
                RentalDetails = new ItemRentalDetailsDocument
                {
                    PricePerDay = (double)dailyRate,
                    DailyRate = (double)dailyRate,
                    HourlyRate = (double)(rate / unitHours),
                    PricingUnitHours = unitHours,
                    SecurityDeposit = (double)model.SecurityDeposit.GetValueOrDefault(),
                    ReplacementValue = (double)model.ReplacementValue.GetValueOrDefault(),
                    MinimumRentalDays = 1,
                    MaximumRentalDays = 7,
                    EnableFullDayBundle = model.EnableFullDayBundle
                },
                Location = new ItemLocationDocument
                {
                    Campus = model.Campus.Trim(),
                    Building = "",
                    Room = null,
                    HandoverLocation = SafeTrim(model.HandoverLocation).Length > 0 ? SafeTrim(model.HandoverLocation) : SafeTrim(model.Campus)
                },
                CreatedAt = now,
                UpdatedAt = now
            };

            try
            {
                MongoDatabase.Items.InsertOne(item);
            }
            catch (MongoException)
            {
                DeletePhotos(savedPhotos);
                ModelState.AddModelError("", "The listing could not be saved. Check the database connection and try again.");
                PopulateOptions(model);
                return View("~/Views/MyListings/CreateListing.cshtml", model);
            }

            return RedirectToAction("Created", new { id = item.Id.ToString() });
        }

        // GET: MyListings/Created/5  (success screen shown after Create)
        [HttpGet]
        public ActionResult Created(string id)
        {
            var document = MarketplaceService.GetOwnerItem(CurrentOwnerId, id);
            if (document == null) return HttpNotFound();
            var item = MarketplaceService.ToListing(document);
            if (item == null)
                return HttpNotFound();

            decimal? wholeDayPrice = null;
            if (item.PriceNote != null && item.PriceNote.Contains("whole day"))
            {
                wholeDayPrice = item.Price * 2;
            }

            var vm = new ListingCreatedViewModel
            {
                Id = item.Id,
                ListingCode = "QCU-" + item.Id.Substring(item.Id.Length - 6).ToUpperInvariant(),
                ItemName = item.Title,
                CategoryBadge = item.CategoryLabel ?? item.CategoryBadge,
                Campus = document.Location != null ? document.Location.Campus : null,
                Condition = item.Condition,
                ImageUrl = item.ImageUrl,
                Price = item.Price,
                PerHours = item.PerHours,
                WholeDayPrice = wholeDayPrice,
                AvailabilityText = (item.AvailableDays == null || item.AvailableDays.Count == 0 ? "Schedule not provided" : string.Join(", ", item.AvailableDays)) +
                    (string.IsNullOrWhiteSpace(item.EarliestHandoverTime) || string.IsNullOrWhiteSpace(item.LatestReturnTime) ? "" : " · " + FormatTimeForDisplay(item.EarliestHandoverTime) + " – " + FormatTimeForDisplay(item.LatestReturnTime)),
                Location = item.Location
            };

            return View("~/Views/MyListings/Created.cshtml", vm);
        }

        // ===================== EDIT =====================

        // GET: MyListings/Edit/5
        [HttpGet]
        [VerifiedStudent]
        public ActionResult Edit(string id)
        {
            var model = LoadEditModel(id);
            if (model == null)
                return HttpNotFound();

            PopulateOptions(model);
            return View("~/Views/MyListings/EditListing.cshtml", model);
        }

        // POST: MyListings/Edit/5
        [HttpPost, ValidateAntiForgeryToken]
        [VerifiedStudent]
        public ActionResult Edit(string id, EditListingViewModel model)
        {
            model.Id = id;
            var original = LoadEditModel(id);
            if (original == null)
                return HttpNotFound();

            var validPhotoIds = new HashSet<int>(original.ExistingPhotos.Select(p => p.Id));
            var kept = (model.KeptPhotoIds ?? new List<int>()).Where(validPhotoIds.Contains).Distinct().ToList();

            ValidatePhotos(GetUploadedFiles(model), kept.Count, 10, false);
            ValidateSchedule(model);
            var validCategory = model.CategoryId.HasValue && GetCategoryOptions().Any(c => c.Id == model.CategoryId.Value);
            if (!validCategory) ModelState.AddModelError("CategoryId", "Choose a valid category.");

            if (!ModelState.IsValid)
            {
                model.Status = original.Status;
                model.CoverImageUrl = original.CoverImageUrl;
                model.PriceSummary = original.PriceSummary;
                model.ExistingPhotos = original.ExistingPhotos.Where(p => kept.Contains(p.Id)).ToList();
                PopulateOptions(model);
                return View("~/Views/MyListings/EditListing.cshtml", model);
            }

            var existing = MarketplaceService.GetOwnerItem(CurrentOwnerId, id);
            if (existing == null) return HttpNotFound();
            if (string.Equals(existing.AvailabilityStatus, "RENTED", StringComparison.OrdinalIgnoreCase) || HasBlockingBooking(existing.Id))
            {
                TempData["Error"] = "A listing cannot be changed during an active rental or reservation.";
                return RedirectToAction("ItemListings");
            }

            var category = GetCategoryOptions().FirstOrDefault(c => c.Id == model.CategoryId);
            var retainedPhotos = (existing.Photos ?? new List<string>()).Where((photo, index) => kept.Contains(index + 1)).ToList();
            List<string> addedPhotos;
            try { addedPhotos = SavePhotos(GetUploadedFiles(model)); }
            catch (IOException)
            {
                ModelState.AddModelError("Photos", "The uploaded photos could not be saved. Please try again.");
                model.ExistingPhotos = original.ExistingPhotos.Where(p => kept.Contains(p.Id)).ToList();
                PopulateOptions(model);
                return View("~/Views/MyListings/EditListing.cshtml", model);
            }
            var newPhotos = retainedPhotos.Concat(addedPhotos).ToList();
            var price = model.Price.GetValueOrDefault();
            var unitHours = model.PricingUnitHours > 0 ? model.PricingUnitHours : 4;
            var dailyRate = model.EnableFullDayBundle ? price * 2 : price * 24 / unitHours;
            var requiresReview = string.Equals(existing.ApprovalStatus, "REJECTED", StringComparison.OrdinalIgnoreCase) ||
                (string.Equals(existing.ApprovalStatus, "APPROVED", StringComparison.OrdinalIgnoreCase) &&
                 HasImportantChanges(existing, model, category.Name, newPhotos, price, unitHours, dailyRate));
            var update = Builders<ItemDocument>.Update
                .Set(x => x.Name, model.ItemName.Trim())
                .Set(x => x.Description, model.Description.Trim())
                .Set(x => x.Category, category != null ? category.Name : "Others")
                .Set(x => x.Condition, ToStoredCondition(model.Condition))
                .Set(x => x.Photos, newPhotos)
                .Set(x => x.AvailableDays, NormalizeAvailableDays(model.AvailableDays))
                .Set(x => x.EarliestHandoverTime, model.EarliestHandoverTime)
                .Set(x => x.LatestReturnTime, model.LatestReturnTime)
                .Set(x => x.Accessories, model.Accessories)
                .Set(x => x.RentalDetails, new ItemRentalDetailsDocument
                {
                    PricePerDay = (double)dailyRate, DailyRate = (double)dailyRate,
                    HourlyRate = (double)(price / unitHours), PricingUnitHours = unitHours,
                    SecurityDeposit = (double)model.SecurityDeposit.GetValueOrDefault(),
                    ReplacementValue = (double)model.ReplacementValue.GetValueOrDefault(),
                    MinimumRentalDays = 1, MaximumRentalDays = 7,
                    EnableFullDayBundle = model.EnableFullDayBundle
                })
                .Set(x => x.Location, new ItemLocationDocument
                {
                    Campus = SafeTrim(model.Campus),
                    Building = SafeTrim(model.Building),
                    Room = string.IsNullOrWhiteSpace(model.Room) ? null : model.Room.Trim(),
                    HandoverLocation = SafeTrim(model.HandoverLocation).Length > 0 ? SafeTrim(model.HandoverLocation) : SafeTrim(model.Campus)
                })
                .Set(x => x.UpdatedAt, DateTime.UtcNow);
            if (requiresReview)
            {
                update = update.Set(x => x.ApprovalStatus, "PENDING")
                    .Unset(x => x.ApprovalRejectionReason)
                    .Unset(x => x.ApprovalReviewedAt)
                    .Unset(x => x.ApprovalReviewedBy);
            }
            var result = MongoDatabase.Items.UpdateOne(x => x.Id == existing.Id && x.OwnerId == CurrentOwnerId && x.AvailabilityStatus != "RENTED", update);
            if (result.MatchedCount == 0)
            {
                DeletePhotos(addedPhotos);
                TempData["Error"] = "The listing was not updated. It may have changed or be rented.";
                return RedirectToAction("ItemListings");
            }
            DeletePhotos((existing.Photos ?? new List<string>()).Except(newPhotos));

            TempData["Message"] = requiresReview
                ? "Listing changes submitted for admin review. The listing will remain hidden until approved."
                : "Listing updated.";
            return RedirectToAction("ItemListings");
        }

        private static bool HasImportantChanges(ItemDocument existing, EditListingViewModel model, string category,
            IList<string> photos, decimal price, int unitHours, decimal dailyRate)
        {
            var details = existing.RentalDetails ?? new ItemRentalDetailsDocument();
            var location = existing.Location ?? new ItemLocationDocument();
            var oldDays = (existing.AvailableDays ?? new List<string>()).OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
            var newDays = (model.AvailableDays ?? new List<string>()).OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
            return !string.Equals(existing.Name, model.ItemName.Trim(), StringComparison.Ordinal) ||
                !string.Equals(existing.Description, model.Description.Trim(), StringComparison.Ordinal) ||
                !string.Equals(existing.Category, category, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(existing.Condition, ToStoredCondition(model.Condition), StringComparison.OrdinalIgnoreCase) ||
                !(existing.Photos ?? new List<string>()).SequenceEqual(photos, StringComparer.OrdinalIgnoreCase) ||
                Math.Abs(details.HourlyRate - (double)(price / unitHours)) > 0.0001 ||
                Math.Abs(details.DailyRate - (double)dailyRate) > 0.0001 ||
                Math.Abs(details.SecurityDeposit - (double)model.SecurityDeposit.GetValueOrDefault()) > 0.0001 ||
                Math.Abs(details.ReplacementValue - (double)model.ReplacementValue.GetValueOrDefault()) > 0.0001 ||
                details.PricingUnitHours != unitHours || details.EnableFullDayBundle != model.EnableFullDayBundle ||
                !oldDays.SequenceEqual(newDays, StringComparer.OrdinalIgnoreCase) ||
                !string.Equals(existing.EarliestHandoverTime, model.EarliestHandoverTime, StringComparison.Ordinal) ||
                !string.Equals(existing.LatestReturnTime, model.LatestReturnTime, StringComparison.Ordinal) ||
                !string.Equals(existing.Accessories, model.Accessories, StringComparison.Ordinal) ||
                !string.Equals(location.Campus, SafeTrim(model.Campus), StringComparison.Ordinal) ||
                !string.Equals(location.Building, SafeTrim(model.Building), StringComparison.Ordinal) ||
                !string.Equals(location.Room, string.IsNullOrWhiteSpace(model.Room) ? null : model.Room.Trim(), StringComparison.Ordinal) ||
                !string.Equals(location.HandoverLocation, SafeTrim(model.HandoverLocation), StringComparison.Ordinal);
        }

        private static string SafeTrim(string value) { return (value ?? "").Trim(); }

        private static string NormalizeCampus(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "San Bartolome";
            var campus = value.Trim();
            if (campus.IndexOf("San Francisco", StringComparison.OrdinalIgnoreCase) >= 0) return "San Francisco";
            if (campus.IndexOf("Batasan", StringComparison.OrdinalIgnoreCase) >= 0) return "Batasan";
            return "San Bartolome";
        }

        private static List<string> NormalizeAvailableDays(IEnumerable<string> values)
        {
            var days = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
            var normalized = (values ?? Enumerable.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(value => days.FirstOrDefault(day => day.StartsWith(value.Trim().Substring(0, Math.Min(3, value.Trim().Length)), StringComparison.OrdinalIgnoreCase)))
                .Where(day => day != null).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            return normalized.Count > 0 ? normalized : new List<string> { "Mon", "Tue", "Wed", "Thu", "Fri" };
        }

        private static bool HasBlockingBooking(ObjectId itemId)
        {
            BookingService.ExpirePending();
            var activeStatuses = new[] { "PENDING", "REQUESTED", "ACCEPTED", "APPROVED", "ACTIVE", "ONGOING", "OVERDUE", "RETURN_REQUESTED", "RETURN PENDING", "DISPUTED" };
            var filter = Builders<BsonDocument>.Filter.Eq("itemId", itemId) &
                         Builders<BsonDocument>.Filter.In("status", activeStatuses);
            return MongoDatabase.Bookings.Find(filter).Limit(1).Any() || MongoDatabase.Rentals.Find(filter).Limit(1).Any();
        }

        private static string FormatTimeForDisplay(string value)
        {
            TimeSpan time;
            return TimeSpan.TryParse(value, out time)
                ? DateTime.Today.Add(time).ToString("h:mm tt", CultureInfo.InvariantCulture)
                : value;
        }

        // ===================== HELPERS =====================

        private static List<HttpPostedFileBase> GetUploadedFiles(CreateListingViewModel model)
        {
            return (model.Photos ?? Enumerable.Empty<HttpPostedFileBase>())
                .Where(f => f != null && f.ContentLength > 0).ToList();
        }

        private void ValidatePhotos(List<HttpPostedFileBase> newFiles, int keptCount, int maxMb, bool requireAtLeastOne = true)
        {
            int total = keptCount + newFiles.Count;
            if (requireAtLeastOne && total == 0)
                ModelState.AddModelError("Photos", "Keep or upload at least one photo.");
            else if (total > 4)
                ModelState.AddModelError("Photos", "A listing can have up to 4 photos.");
            else if (newFiles.Any(f => f.ContentType == null || !f.ContentType.StartsWith("image/") || f.ContentLength > maxMb * 1024 * 1024))
                ModelState.AddModelError("Photos", "Photos must be images of " + maxMb + " MB or less.");
        }

        private void ValidateSchedule(CreateListingViewModel model)
        {
            if (model.AvailableDays == null || model.AvailableDays.Count == 0)
                ModelState.AddModelError("AvailableDays", "Select at least one handover day.");

            TimeSpan from, to;
            if (TimeSpan.TryParse(model.EarliestHandoverTime, out from) && from < new TimeSpan(6, 0, 0))
                ModelState.AddModelError("EarliestHandoverTime", "Earliest handover cannot be before 6:00 AM.");
            if (TimeSpan.TryParse(model.LatestReturnTime, out to) && to > new TimeSpan(21, 0, 0))
                ModelState.AddModelError("LatestReturnTime", "Latest return cannot be after 9:00 PM.");
            if (TimeSpan.TryParse(model.EarliestHandoverTime, out from) &&
                TimeSpan.TryParse(model.LatestReturnTime, out to) && to <= from)
                ModelState.AddModelError("LatestReturnTime", "Latest return must be after the earliest handover.");
        }

        // Load existing data for Edit page
        private EditListingViewModel LoadEditModel(string id)
        {
            var document = MarketplaceService.GetOwnerItem(CurrentOwnerId, id);
            if (document == null) return null;
            var item = MarketplaceService.ToListing(document);

            int catId = item.CategoryId > 0 ? item.CategoryId : 1;
            var canonicalCategory = ItemCategoryCatalog.Normalize(item.CategoryLabel);
            var cat = GetCategoryOptions().FirstOrDefault(c => c.Name.Equals(canonicalCategory, StringComparison.OrdinalIgnoreCase));
            if (cat != null) catId = cat.Id;

            ConditionGrade cond = ConditionGrade.Good;
            if (item.Condition != null)
            {
                if (item.Condition.IndexOf("Like New", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    item.Condition.IndexOf("Excellent", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    item.Condition.IndexOf("New", StringComparison.OrdinalIgnoreCase) >= 0) cond = ConditionGrade.LikeNew;
                else if (item.Condition.IndexOf("Fair", StringComparison.OrdinalIgnoreCase) >= 0) cond = ConditionGrade.Fair;
                else if (item.Condition.IndexOf("Heavy", StringComparison.OrdinalIgnoreCase) >= 0) cond = ConditionGrade.HeavyUse;
            }

            bool fullDay = item.PriceNote != null && item.PriceNote.IndexOf("whole day", StringComparison.OrdinalIgnoreCase) >= 0;

            return new EditListingViewModel
            {
                Id = item.Id,
                ItemName = item.Title,
                CategoryId = catId,
                Condition = cond,
                Description = item.Description ?? "",
                Price = item.Price,
                PricingUnitHours = item.PerHours,
                ReplacementValue = item.ReplacementValue,
                SecurityDeposit = item.SecurityDeposit,
                Accessories = item.Accessories,
                EnableFullDayBundle = item.EnableFullDayBundle || fullDay,
                AvailableDays = NormalizeAvailableDays(item.AvailableDays),
                EarliestHandoverTime = string.IsNullOrWhiteSpace(item.EarliestHandoverTime) ? "08:00" : item.EarliestHandoverTime,
                LatestReturnTime = string.IsNullOrWhiteSpace(item.LatestReturnTime) ? "17:30" : item.LatestReturnTime,
                Campus = NormalizeCampus(document.Location != null ? document.Location.Campus : null),
                Building = document.Location != null ? document.Location.Building : null,
                Room = document.Location != null ? document.Location.Room : null,
                HandoverLocation = document.Location != null && !string.IsNullOrWhiteSpace(document.Location.HandoverLocation)
                    ? document.Location.HandoverLocation : NormalizeCampus(document.Location != null ? document.Location.Campus : null),
                Status = item.Status,
                CoverImageUrl = item.ImageUrl,
                PriceSummary = $"\u20B1{item.Price:0.##} / {item.PerHours} hrs ({(fullDay ? "\u20B1" + (item.Price * 2).ToString("0.##") + " whole day" : item.PriceNote)})",
                ExistingPhotos = (document.Photos ?? new List<string>()).Select((photo, index) => new ListingPhotoViewModel
                {
                    Id = index + 1, Url = MarketplaceService.ToImageUrl(photo),
                    Label = index == 0 ? "Cover Photo" : "Photo " + (index + 1), Caption = "Listing photo"
                }).ToList()
            };
        }

        private List<string> SavePhotos(IEnumerable<HttpPostedFileBase> files)
        {
            var saved = new List<string>();
            var uploadDir = Server.MapPath("~/Uploads");
            Directory.CreateDirectory(uploadDir);
            try
            {
                foreach (var file in files ?? Enumerable.Empty<HttpPostedFileBase>())
                {
                    if (file == null || file.ContentLength <= 0) continue;
                    var extension = Path.GetExtension(Path.GetFileName(file.FileName)).ToLowerInvariant();
                    if (!new[] { ".jpg", ".jpeg", ".png", ".webp" }.Contains(extension))
                        throw new IOException("Unsupported image format.");
                    var fileName = Guid.NewGuid().ToString("N") + extension;
                    file.SaveAs(Path.Combine(uploadDir, fileName));
                    saved.Add(fileName);
                }
                return saved;
            }
            catch { DeletePhotos(saved); throw; }
        }

        private void DeletePhotos(IEnumerable<string> photos)
        {
            var uploadDir = Server.MapPath("~/Uploads");
            foreach (var photo in photos ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(photo) || photo.Contains("/") || photo.Contains("\\") || !Directory.Exists(uploadDir)) continue;
                var fileName = Path.GetFileName(photo);
                Guid generatedName;
                if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(fileName), "N", out generatedName)) continue;
                var path = Path.Combine(uploadDir, fileName);
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
        }

        private static string ToStoredCondition(ConditionGrade? condition)
        {
            switch (condition.GetValueOrDefault(ConditionGrade.Good))
            {
                case ConditionGrade.LikeNew: return "LIKE_NEW";
                case ConditionGrade.Good: return "GOOD";
                case ConditionGrade.Fair: return "FAIR";
                default: return "HEAVY_USE";
            }
        }

        internal static List<CategoryOptionViewModel> GetCategoryOptions()
        {
            return new List<CategoryOptionViewModel>
            {
                new CategoryOptionViewModel { Id = 1, Name = "Electronics & Gadgets", Group = "Electronics & Gadgets" },
                new CategoryOptionViewModel { Id = 2, Name = "Books & Learning Materials", Group = "Books & Learning Materials" },
                new CategoryOptionViewModel { Id = 3, Name = "School Supplies", Group = "School Supplies" },
                new CategoryOptionViewModel { Id = 4, Name = "Clothing & Uniforms", Group = "Clothing & Uniforms" },
                new CategoryOptionViewModel { Id = 5, Name = "Sports & Recreation", Group = "Sports & Recreation" },
                new CategoryOptionViewModel { Id = 6, Name = "Others", Group = "Others" }
            };
        }

        internal static void PopulateOptions(CreateListingViewModel model)
        {
            model.Categories = GetCategoryOptions();
        }
    }
}
