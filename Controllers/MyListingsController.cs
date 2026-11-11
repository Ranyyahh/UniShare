using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using UniShare.Models;

namespace UniShare.Controllers
{
    public class MyListingsController : Controller
    {
        private static readonly object _lock = new object();
        private static List<ListingItemViewModel> _listings;

        internal static List<ListingItemViewModel> GetOrCreateListings()
        {
            if (_listings == null)
            {
                lock (_lock)
                {
                    if (_listings == null)
                    {
                        const string img = "~/Content/img/placeholder.png";
                        _listings = new List<ListingItemViewModel>
                        {
                            new ListingItemViewModel { Id = 1, Title = "Casio fx-991EX ClassWiz Scientific Calculator", CategoryLabel = "Electronic Gadgets", CategoryBadge = "Gadgets", CategoryId = 1, ImageUrl = img, Condition = "Like New", ConditionScore = 9.5m, Price = 25, PriceNote = "\u20B150 whole day", Location = "CEA Building Lobby (San Bartolome Campus)", Status = ListingStatus.Available },
                            new ListingItemViewModel { Id = 2, Title = "Engineering Mechanics: Statics & Dynamics", CategoryLabel = "Books & References", CategoryBadge = "Books", ImageUrl = img, Condition = "Excellent", ConditionScore = 9m, Price = 15, PriceNote = "\u20B130 whole day", Location = "University Library Front Desk", Status = ListingStatus.Available },
                            new ListingItemViewModel { Id = 3, Title = "Rotring Isograph Junior Pen Set (0.2, 0.3, 0.5)", CategoryLabel = "School Supplies (Drafting)", CategoryBadge = "Drafting", ImageUrl = img, Condition = "Like New", ConditionScore = 10m, Price = 30, PriceNote = "\u20B160 whole day", Location = "CEA Drafting Studio Room 304", Status = ListingStatus.Available },
                            new ListingItemViewModel { Id = 4, Title = "Casio Calculator Hard Slide-on Case", CategoryLabel = "Electronic Accessories", CategoryBadge = "Accessories", CategoryId = 2, ImageUrl = img, Condition = "Good", ConditionScore = 8m, Price = 10, PriceNote = "Fixed flat rate", Location = "Student Canteen Guard Station", Status = ListingStatus.Rented },
                            new ListingItemViewModel { Id = 5, Title = "Molten GG7X Official Indoor/Outdoor Basketball", CategoryLabel = "Sports Equipment", CategoryBadge = "Sports", ImageUrl = img, Condition = "Good", ConditionScore = 8.5m, Price = 20, PriceNote = "Standard match ball", Location = "QCU Gymnasium Entrance", Status = ListingStatus.Available }
                        };
                    }
                }
            }
            return _listings;
        }

        // ===================== LIST =====================

        // GET: MyListings / Listings
        public ActionResult Index(bool empty = false)
        {
            return RedirectToAction("ItemListings", new { empty });
        }

        // GET: MyListings/ItemListings
        // TEMP: /MyListings/ItemListings?empty=true previews the empty state. Remove the parameter once real data is wired up.
        public ActionResult ItemListings(bool empty = false)
        {
            var vm = new ListingsPageViewModel();

            if (!empty)
            {
                lock (_lock)
                {
                    vm.Listings = GetOrCreateListings().ToList();
                }
            }

            return View("~/Views/MyListings/ItemListings.cshtml", vm);
        }

        // POST: MyListings/ToggleAvailability/5  (pause / resume)
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ToggleAvailability(int id)
        {
            lock (_lock)
            {
                var item = GetOrCreateListings().FirstOrDefault(l => l.Id == id);
                if (item != null)
                {
                    item.Status = (item.Status == ListingStatus.Available) ? ListingStatus.Paused : ListingStatus.Available;
                }
            }
            return RedirectToAction("ItemListings");
        }

        // Keep the earlier action name as an alias while the UI uses ToggleAvailability.
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ToggleVisibility(int id)
        {
            return ToggleAvailability(id);
        }

        // POST: MyListings/Delete   (id comes from the delete dialog's hidden field)
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            lock (_lock)
            {
                var item = GetOrCreateListings().FirstOrDefault(l => l.Id == id);
                if (item != null)
                {
                    if (item.Status == ListingStatus.Rented)
                    {
                        TempData["Error"] = "Cannot delete a listing with an active rental.";
                        return RedirectToAction("ItemListings");
                    }
                    GetOrCreateListings().Remove(item);
                    TempData["Message"] = "Listing deleted.";
                }
            }
            return RedirectToAction("ItemListings");
        }

        // ===================== CREATE =====================

        // GET: MyListings/Create
        [HttpGet]
        public ActionResult Create()
        {
            var model = new CreateListingViewModel
            {
                AvailableDays = new List<string> { "Mon", "Tue", "Wed", "Thu", "Fri" }
            };
            PopulateOptions(model);
            return View("~/Views/MyListings/CreateListing.cshtml", model);
        }

        // GET: MyListings/CreateListing
        [HttpGet]
        public ActionResult CreateListing()
        {
            return RedirectToAction("Create");
        }

        // POST: MyListings/Create
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Create(CreateListingViewModel model)
        {
            ValidatePhotos(GetUploadedFiles(model), 0, 5);
            ValidateSchedule(model);

            if (!ModelState.IsValid)
            {
                PopulateOptions(model);
                return View("~/Views/MyListings/CreateListing.cshtml", model);
            }

            // Save first photo as cover photo
            string imageUrl = "~/Content/img/placeholder.png";
            var photos = GetUploadedFiles(model);
            if (photos.Count > 0 && photos[0] != null)
            {
                try
                {
                    var uploadDir = Server.MapPath("~/Uploads");
                    if (!Directory.Exists(uploadDir))
                    {
                        Directory.CreateDirectory(uploadDir);
                    }
                    var ext = Path.GetExtension(photos[0].FileName);
                    if (string.IsNullOrEmpty(ext)) ext = ".jpg";
                    var fileName = Guid.NewGuid().ToString("N") + ext;
                    var filePath = Path.Combine(uploadDir, fileName);
                    photos[0].SaveAs(filePath);
                    imageUrl = "~/Uploads/" + fileName;
                }
                catch
                {
                    imageUrl = "~/Content/img/placeholder.png";
                }
            }

            var catOptions = GetCategoryOptions();
            var selectedCat = catOptions.FirstOrDefault(c => c.Id == model.CategoryId);
            var catLabel = selectedCat != null ? selectedCat.Name : "Academic Supplies";
            var catBadge = selectedCat != null ? selectedCat.Group : "Supplies";

            var cpOptions = GetCheckpointOptions();
            var selectedCp = cpOptions.FirstOrDefault(c => c.Value == model.Checkpoint);
            var location = selectedCp != null ? $"{selectedCp.Name} ({selectedCp.Detail})" : "CEA Building Lobby (San Bartolome Campus)";

            string conditionStr = "Good";
            decimal conditionScore = 8.5m;
            if (model.Condition.HasValue)
            {
                switch (model.Condition.Value)
                {
                    case ConditionGrade.LikeNew:
                        conditionStr = "Like New";
                        conditionScore = 9.8m;
                        break;
                    case ConditionGrade.Good:
                        conditionStr = "Good";
                        conditionScore = 8.5m;
                        break;
                    case ConditionGrade.Fair:
                        conditionStr = "Fair";
                        conditionScore = 7.0m;
                        break;
                    case ConditionGrade.HeavyUse:
                        conditionStr = "Heavy Use";
                        conditionScore = 5.5m;
                        break;
                }
            }

            var rate = model.Price ?? 0;
            var priceNote = model.EnableFullDayBundle ? $"\u20B1{(rate * 2):0.##} whole day" : "Fixed rate";

            int newId;
            lock (_lock)
            {
                var list = GetOrCreateListings();
                newId = list.Count > 0 ? list.Max(x => x.Id) + 1 : 1;
                list.Insert(0, new ListingItemViewModel
                {
                    Id = newId,
                    Title = model.ItemName,
                    CategoryLabel = catLabel,
                    CategoryBadge = catBadge,
                    ImageUrl = imageUrl,
                    Condition = conditionStr,
                    ConditionScore = conditionScore,
                    Price = rate,
                    PerHours = model.PricingUnitHours > 0 ? model.PricingUnitHours : 4,
                    PriceNote = priceNote,
                    Location = location,
                    Status = ListingStatus.Available,
                    // store the handover times
                    EarliestHandoverTime = model.EarliestHandoverTime,
                    LatestReturnTime = model.LatestReturnTime,
                    CategoryId = model.CategoryId ?? 1,
                    Description = model.Description,
                    EnableFullDayBundle = model.EnableFullDayBundle,
                    AvailableDays = new List<string>(model.AvailableDays ?? new List<string>()),
                    Checkpoint = selectedCp != null ? selectedCp.Value : "cea-lobby"
                });
            }

            return RedirectToAction("Created", new { id = newId });
        }

        // GET: MyListings/Created/5  (success screen shown after Create)
        [HttpGet]
        public ActionResult Created(int id)
        {
            ListingItemViewModel item;
            lock (_lock)
            {
                item = GetOrCreateListings().FirstOrDefault(l => l.Id == id);
            }

            if (item == null)
            {
                item = new ListingItemViewModel
                {
                    Id = id,
                    Title = "Academic Course Gear",
                    CategoryLabel = "Electronic Gadgets",
                    CategoryBadge = "Gadgets",
                    ImageUrl = "~/Content/img/placeholder.png",
                    Condition = "Like New",
                    Price = 25,
                    PerHours = 4,
                    PriceNote = "\u20B150 whole day",
                    Location = "CEA Building Lobby (San Bartolome Campus)"
                };
            }

            decimal? wholeDayPrice = null;
            if (item.PriceNote != null && item.PriceNote.Contains("whole day"))
            {
                wholeDayPrice = item.Price * 2;
            }

            var vm = new ListingCreatedViewModel
            {
                Id = item.Id,
                ListingCode = $"QCU-EL-{item.Id + 8840}",
                ItemName = item.Title,
                CategoryBadge = item.CategoryLabel ?? item.CategoryBadge,
                Campus = "San Bartolome",
                Condition = item.Condition,
                ImageUrl = item.ImageUrl,
                Price = item.Price,
                PerHours = item.PerHours,
                WholeDayPrice = wholeDayPrice,
                AvailabilityText = $"Available Weekdays {FormatTimeForDisplay(item.EarliestHandoverTime)} \u2013 {FormatTimeForDisplay(item.LatestReturnTime)}",
                Location = item.Location
            };

            return View("~/Views/MyListings/Created.cshtml", vm);
        }

        // ===================== EDIT =====================

        // GET: MyListings/Edit/5
        [HttpGet]
        public ActionResult Edit(int id)
        {
            var model = LoadEditModel(id);
            if (model == null)
                return HttpNotFound();

            PopulateOptions(model);
            return View("~/Views/MyListings/EditListing.cshtml", model);
        }

        // POST: MyListings/Edit/5
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Edit(int id, EditListingViewModel model)
        {
            model.Id = id;
            var original = LoadEditModel(id);
            if (original == null)
                return HttpNotFound();

            var kept = model.KeptPhotoIds ?? new List<int>();

            ValidatePhotos(GetUploadedFiles(model), kept.Count, 10);
            ValidateSchedule(model);

            if (!ModelState.IsValid)
            {
                model.Status = original.Status;
                model.CoverImageUrl = original.CoverImageUrl;
                model.PriceSummary = original.PriceSummary;
                model.CategoryApprovalNote = original.CategoryApprovalNote;
                model.ExamSuitabilityLabel = original.ExamSuitabilityLabel;
                model.ExamSuitabilityBadge = original.ExamSuitabilityBadge;
                model.ExamSuitabilityNote = original.ExamSuitabilityNote;
                model.ExistingPhotos = original.ExistingPhotos.Where(p => kept.Contains(p.Id)).ToList();
                PopulateOptions(model);
                return View("~/Views/MyListings/EditListing.cshtml", model);
            }

            // Update the in-memory item
            lock (_lock)
            {
                var item = GetOrCreateListings().FirstOrDefault(l => l.Id == id);
                if (item != null)
                {
                    item.Title = model.ItemName;
                    item.Description = model.Description;
                    item.CategoryId = model.CategoryId ?? item.CategoryId;
                    item.EnableFullDayBundle = model.EnableFullDayBundle;
                    item.AvailableDays = new List<string>(model.AvailableDays ?? new List<string>());
                    item.EarliestHandoverTime = model.EarliestHandoverTime;
                    item.LatestReturnTime = model.LatestReturnTime;
                    item.Checkpoint = model.Checkpoint;
                    var cat = GetCategoryOptions().FirstOrDefault(c => c.Id == model.CategoryId);
                    if (cat != null)
                    {
                        item.CategoryLabel = cat.Name;
                        item.CategoryBadge = cat.Group;
                    }

                    if (model.Condition.HasValue)
                    {
                        switch (model.Condition.Value)
                        {
                            case ConditionGrade.LikeNew:
                                item.Condition = "Like New";
                                item.ConditionScore = 9.8m;
                                break;
                            case ConditionGrade.Good:
                                item.Condition = "Good";
                                item.ConditionScore = 8.5m;
                                break;
                            case ConditionGrade.Fair:
                                item.Condition = "Fair";
                                item.ConditionScore = 7.0m;
                                break;
                            case ConditionGrade.HeavyUse:
                                item.Condition = "Heavy Use";
                                item.ConditionScore = 5.5m;
                                break;
                        }
                    }

                    item.Price = model.Price ?? 0;
                    item.PerHours = model.PricingUnitHours > 0 ? model.PricingUnitHours : 4;
                    item.PriceNote = model.EnableFullDayBundle
                        ? $"\u20B1{((model.Price ?? 0) * 2):0.##} whole day"
                        : "Fixed flat rate";

                    var cp = GetCheckpointOptions().FirstOrDefault(c => c.Value == model.Checkpoint);
                    if (cp != null)
                    {
                        item.Location = $"{cp.Name} ({cp.Detail})";
                    }

                    // If new photo was uploaded, save it
                    var newFiles = GetUploadedFiles(model);
                    if (newFiles.Count > 0 && newFiles[0] != null)
                    {
                        try
                        {
                            var uploadDir = Server.MapPath("~/Uploads");
                            if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);
                            var ext = Path.GetExtension(newFiles[0].FileName);
                            if (string.IsNullOrEmpty(ext)) ext = ".jpg";
                            var fileName = Guid.NewGuid().ToString("N") + ext;
                            newFiles[0].SaveAs(Path.Combine(uploadDir, fileName));
                            item.ImageUrl = "~/Uploads/" + fileName;
                        }
                        catch { }
                    }
                }
            }

            TempData["Message"] = "Listing updated.";
            return RedirectToAction("ItemListings");
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

        private void ValidatePhotos(List<HttpPostedFileBase> newFiles, int keptCount, int maxMb)
        {
            int total = keptCount + newFiles.Count;
            if (total == 0)
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
            if (TimeSpan.TryParse(model.EarliestHandoverTime, out from) &&
                TimeSpan.TryParse(model.LatestReturnTime, out to) && to <= from)
                ModelState.AddModelError("LatestReturnTime", "Latest return must be after the earliest handover.");
        }

        // Load existing data for Edit page
        private static EditListingViewModel LoadEditModel(int id)
        {
            ListingItemViewModel item;
            lock (_lock)
            {
                item = GetOrCreateListings().FirstOrDefault(l => l.Id == id);
            }

            if (item == null) return null;

            int catId = item.CategoryId > 0 ? item.CategoryId : 1;
            var cat = GetCategoryOptions().FirstOrDefault(c => c.Id == catId || c.Name.Equals(item.CategoryLabel, StringComparison.OrdinalIgnoreCase));
            if (cat != null) catId = cat.Id;

            ConditionGrade cond = ConditionGrade.Good;
            if (item.Condition != null)
            {
                if (item.Condition.IndexOf("Like New", StringComparison.OrdinalIgnoreCase) >= 0) cond = ConditionGrade.LikeNew;
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
                Description = item.Description ?? "Official QCU compliant equipment in great condition. Perfect for engineering, math, and coursework sessions. Maintained carefully with high performance.",
                Price = item.Price,
                PricingUnitHours = item.PerHours,
                EnableFullDayBundle = item.EnableFullDayBundle || fullDay,
                AvailableDays = item.AvailableDays != null && item.AvailableDays.Count > 0 ? new List<string>(item.AvailableDays) : new List<string> { "Mon", "Tue", "Wed", "Thu", "Fri" },
                EarliestHandoverTime = item.EarliestHandoverTime,
                LatestReturnTime = item.LatestReturnTime ?? "17:30",
                Checkpoint = item.Checkpoint ?? "cea-lobby",
                Status = item.Status,
                CoverImageUrl = item.ImageUrl,
                PriceSummary = $"\u20B1{item.Price:0.##} / {item.PerHours} hrs ({(fullDay ? "\u20B1" + (item.Price * 2).ToString("0.##") + " whole day" : item.PriceNote)})",
                CategoryApprovalNote = "College of Engineering & Architecture (CEA) approved.",
                ExamSuitabilityLabel = "PRC & QCU Exam Legal",
                ExamSuitabilityBadge = "Non-Programmable",
                ExamSuitabilityNote = "Verified against latest PRC non-programmable list for 2024\u20132025.",
                ExistingPhotos = new List<ListingPhotoViewModel>
                {
                    new ListingPhotoViewModel { Id = 101, Url = item.ImageUrl, Label = "Cover Photo", Caption = "Angle: Main Front Setup" },
                    new ListingPhotoViewModel { Id = 102, Url = item.ImageUrl, Label = "Detail View", Caption = "Screen and Buttons" }
                }
            };
        }

        internal static List<CategoryOptionViewModel> GetCategoryOptions()
        {
            return new List<CategoryOptionViewModel>
            {
                new CategoryOptionViewModel { Id = 1, Name = "Electronic Gadgets",          Group = "Gadgets" },
                new CategoryOptionViewModel { Id = 2, Name = "Electronic Accessories",      Group = "Accessories" },
                new CategoryOptionViewModel { Id = 3, Name = "Books & References",          Group = "Books" },
                new CategoryOptionViewModel { Id = 4, Name = "School Supplies (Drafting)",  Group = "School Supplies" },
                new CategoryOptionViewModel { Id = 5, Name = "Sports Equipment",            Group = "Sports" },
                new CategoryOptionViewModel { Id = 6, Name = "Other Items",                 Group = "Other" }
            };
        }

        internal static List<CheckpointOptionViewModel> GetCheckpointOptions()
        {
            return new List<CheckpointOptionViewModel>
            {
                new CheckpointOptionViewModel { Value = "cea-lobby",     Name = "CEA Building Lobby",            Detail = "San Bartolome Campus \u2022 Main Gate 1 Access",   IconName = "building-2", Feature = "Guard Desk CCTV Active" },
                new CheckpointOptionViewModel { Value = "library-front", Name = "University Library Front",      Detail = "San Bartolome Campus \u2022 2nd Floor Mezzanine",  IconName = "library",    Feature = "Quiet Zone Turnstiles" },
                new CheckpointOptionViewModel { Value = "canteen-guard", Name = "Student Canteen Guard Station", Detail = "San Bartolome Campus \u2022 Gazebo Area",        IconName = "store",      Feature = "High Foot Traffic Zone" }
            };
        }

        internal static void PopulateOptions(CreateListingViewModel model)
        {
            model.Categories = GetCategoryOptions();
            model.Checkpoints = GetCheckpointOptions();
        }
    }
}
