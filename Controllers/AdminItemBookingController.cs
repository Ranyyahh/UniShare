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
    [Authorize(Roles = "ADMIN")]
    public class AdminItemBookingController : Controller
    {
        [HttpGet]
        public ActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public JsonResult GetListings()
        {
            var records = MongoDatabase.Items.Find(Builders<ItemDocument>.Filter.Empty)
                .SortByDescending(x => x.CreatedAt).ToList()
                .Select(item =>
                {
                    var owner = MongoDatabase.Users.Find(x => x.Id == item.OwnerId).FirstOrDefault();
                    var details = item.RentalDetails ?? new ItemRentalDetailsDocument();
                    var location = item.Location ?? new ItemLocationDocument();
                    var submitted = item.CreatedAt == default(DateTime) ? "" :
                        item.CreatedAt.ToLocalTime().ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
                    var unitHours = details.PricingUnitHours > 0 ? details.PricingUnitHours : 24;
                    var rate = details.HourlyRate > 0 ? (decimal)details.HourlyRate : (decimal)(details.PricePerDay / 24d);
                    return new
                    {
                        id = item.Id.ToString(),
                        name = item.Name ?? "Untitled item",
                        category = item.Category ?? "Other Items",
                        owner = owner == null ? "Student" : (owner.FirstName + " " + owner.LastName).Trim(),
                        status = (item.ApprovalStatus ?? "PENDING").ToLowerInvariant(),
                        active = string.Equals(item.ApprovalStatus, "APPROVED", StringComparison.OrdinalIgnoreCase) &&
                                 string.Equals(item.AvailabilityStatus, "AVAILABLE", StringComparison.OrdinalIgnoreCase) ? "active" : "inactive",
                        price = "\u20B1" + rate.ToString("0.##", CultureInfo.InvariantCulture) + " / " + unitHours + " hrs",
                        deposit = "\u20B1" + ((decimal)details.SecurityDeposit).ToString("N2", CultureInfo.InvariantCulture),
                        replacementValue = "\u20B1" + ((decimal)details.ReplacementValue).ToString("N2", CultureInfo.InvariantCulture),
                        accessories = item.Accessories ?? "None specified",
                        meta = "Submitted " + submitted,
                        desc = item.Description ?? "",
                        course = owner == null ? "Not specified" : string.Join(" ", new[] { owner.Program, owner.YearLevel }.Where(x => !string.IsNullOrWhiteSpace(x))),
                        condition = item.Condition ?? "Not specified",
                        handover = item.EarliestHandoverTime ?? "Not specified",
                        returnTime = item.LatestReturnTime ?? "Not specified",
                        days = string.Join(",", item.AvailableDays ?? new System.Collections.Generic.List<string>()),
                        imageUrl = item.Photos != null && item.Photos.Count > 0 ? MarketplaceService.ToImageUrl(item.Photos[0]) : null,
                        rejectionReason = item.ApprovalRejectionReason ?? "",
                        location = location.HandoverLocation ?? location.Building ?? ""
                    };
                }).ToList();

            return Json(new { success = true, data = records }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Approve(string id)
        {
            ObjectId itemId, adminId;
            if (!ObjectId.TryParse(id, out itemId) || !ObjectId.TryParse(User.Identity.Name, out adminId))
                return Json(new { success = false, message = "The listing or admin account is invalid." });

            var current = MongoDatabase.Items.Find(x => x.Id == itemId).FirstOrDefault();
            if (current == null) return Json(new { success = false, message = "Listing not found." });
            if (current.OwnerId == adminId) return new HttpStatusCodeResult(403, "Administrators cannot approve their own listings.");

            var update = Builders<ItemDocument>.Update.Set(x => x.ApprovalStatus, "APPROVED")
                .Set(x => x.ApprovalReviewedAt, DateTime.UtcNow)
                .Set(x => x.ApprovalReviewedBy, adminId)
                .Unset(x => x.ApprovalRejectionReason);
            var result = MongoDatabase.Items.UpdateOne(PendingFilter(itemId), update);
            return result.ModifiedCount == 1
                ? (ActionResult)Json(new { success = true, message = "Listing approved." })
                : Json(new { success = false, message = "This listing is no longer pending review." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Reject(string id, string reason)
        {
            ObjectId itemId, adminId;
            if (!ObjectId.TryParse(id, out itemId) || !ObjectId.TryParse(User.Identity.Name, out adminId))
                return Json(new { success = false, message = "The listing or admin account is invalid." });
            reason = (reason ?? "").Trim();
            if (reason.Length == 0 || reason.Length > 500)
                return Json(new { success = false, message = "Enter a rejection reason (up to 500 characters)." });

            var current = MongoDatabase.Items.Find(x => x.Id == itemId).FirstOrDefault();
            if (current == null) return Json(new { success = false, message = "Listing not found." });
            if (current.OwnerId == adminId) return new HttpStatusCodeResult(403, "Administrators cannot reject their own listings.");

            var now = DateTime.UtcNow;
            var update = Builders<ItemDocument>.Update.Set(x => x.ApprovalStatus, "REJECTED")
                .Set(x => x.ApprovalRejectionReason, reason)
                .Set(x => x.ApprovalReviewedAt, now)
                .Set(x => x.ApprovalReviewedBy, adminId)
                .Set(x => x.UpdatedAt, now);
            var result = MongoDatabase.Items.UpdateOne(PendingFilter(itemId), update);
            return result.ModifiedCount == 1
                ? (ActionResult)Json(new { success = true, message = "Listing rejected." })
                : Json(new { success = false, message = "This listing is no longer pending review." });
        }

        private static FilterDefinition<ItemDocument> PendingFilter(ObjectId itemId)
        {
            var filters = Builders<ItemDocument>.Filter;
            var pendingStatus = filters.In(x => x.ApprovalStatus, new[] { "PENDING", "Pending", "pending" }) |
                                filters.Exists("approvalStatus", false);
            return filters.Eq(x => x.Id, itemId) & pendingStatus;
        }
    }
}
