using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using UniShare.Models;
using UniShare.Services;

namespace UniShare.Controllers
{
    [Authorize(Roles = "ADMIN")]
    public class AdminReportsAnalyticsController : Controller
    {
        private static readonly string[] ExcludedStatuses = { "CANCELLED", "CANCELED", "DECLINED", "REJECTED", "EXPIRED", "FAILED", "REFUNDED" };

        [HttpGet]
        public ActionResult Index()
        {
            ViewBag.Title = "Reports & Analytics";
            ViewBag.ShowAdminSidebar = true;
            return View();
        }

        [HttpGet]
        public ActionResult Data(string period = "monthly")
        {
            if (!new[] { "daily", "weekly", "monthly", "yearly" }.Contains((period ?? "").ToLowerInvariant()))
                return new HttpStatusCodeResult(400, "Choose a valid reporting period.");
            try
            {
                var usersFilter = Builders<UserDocument>.Filter.Eq(x => x.VerificationStatus, "APPROVED") &
                                  Builders<UserDocument>.Filter.Eq(x => x.Status, "ACTIVE") &
                                  Builders<UserDocument>.Filter.Ne(x => x.Role, "ADMIN");
                var verifiedUsers = (int)MongoDatabase.Users.CountDocuments(usersFilter);
                var items = MongoDatabase.Items.Find(Builders<ItemDocument>.Filter.Empty)
                    .Project(x => new ItemDocument { Id = x.Id, Name = x.Name, ApprovalStatus = x.ApprovalStatus }).ToList();
                var bookingProjection = Builders<BsonDocument>.Projection.Include("_id").Include("requestId")
                    .Include("itemId").Include("itemName").Include("status").Include("createdAt").Include("requestedAt")
                    .Include("startDate").Include("borrowerId").Include("renterId").Include("ownerId");
                var bookings = MongoDatabase.Bookings.Find(Builders<BsonDocument>.Filter.Empty).Project(bookingProjection).ToList()
                    .GroupBy(BookingKey, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
                var listingStatuses = new
                {
                    pending = items.Count(x => StatusIs(x.ApprovalStatus, "PENDING")),
                    approved = items.Count(x => StatusIs(x.ApprovalStatus, "APPROVED")),
                    rejected = items.Count(x => StatusIs(x.ApprovalStatus, "REJECTED")),
                    total = items.Count
                };
                var rentalsByDate = bookings.Select(x => new { booking = x, date = ReadDate(x, "createdAt") ?? ReadDate(x, "requestedAt") ?? ReadDate(x, "startDate") })
                    .Where(x => x.date.HasValue).GroupBy(x => Bucket(x.date.Value, period.ToLowerInvariant()))
                    .OrderBy(x => x.Key).Select(x => new { label = x.Key.ToString("yyyy-MM-dd"), count = x.Count() }).ToList();
                var itemNames = items.ToDictionary(x => x.Id.ToString(), x => x.Name ?? "Untitled item", StringComparer.OrdinalIgnoreCase);
                var topItems = bookings.Where(x => !ExcludedStatuses.Contains(Status(x)))
                    .GroupBy(x => x.GetValue("itemId", BsonNull.Value).ToString())
                    .Select(g => new { name = itemNames.ContainsKey(g.Key) ? itemNames[g.Key] : g.First().GetValue("itemName", "Unknown item").ToString(), count = g.Count() })
                    .OrderByDescending(x => x.count).ThenBy(x => x.name).Take(10).ToList();

                var userCounts = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);
                foreach (var booking in bookings)
                {
                    var renter = IdValue(booking, "borrowerId", "renterId");
                    var owner = IdValue(booking, "ownerId");
                    AddCount(userCounts, renter, 0);
                    AddCount(userCounts, owner, 1);
                }
                var userIds = userCounts.Keys.Select(TryObjectId).Where(x => x.HasValue).Select(x => x.Value).ToList();
                var userNames = MongoDatabase.Users.Find(Builders<UserDocument>.Filter.In(x => x.Id, userIds)).ToList()
                    .ToDictionary(x => x.Id.ToString(), x => ((x.FirstName ?? "") + " " + (x.LastName ?? "")).Trim(), StringComparer.OrdinalIgnoreCase);
                var activity = userCounts.Select(x => new { user = userNames.ContainsKey(x.Key) && userNames[x.Key].Length > 0 ? userNames[x.Key] : "User " + x.Key.Substring(0, Math.Min(6, x.Key.Length)), renting = x.Value[0], borrowing = x.Value[1] })
                    .OrderByDescending(x => x.renting + x.borrowing).Take(12).ToList();

                return Json(new
                {
                    cards = new { platformRevenue = (decimal?)null, totalRentals = bookings.Count, totalUsers = verifiedUsers, totalItems = items.Count },
                    revenueAvailable = false,
                    revenueNote = "Platform fees or commission are not recorded in the current payment data.",
                    rentalsByDate, listingStatuses, topItems, activity,
                    activityNote = "Renting counts requests initiated by the renter. Borrowing counts transactions for which the user is the item owner.",
                    period = period.ToLowerInvariant()
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) when (ex is MongoException || ex is InvalidOperationException || ex is FormatException)
            {
                Response.StatusCode = 503;
                return Json(new { error = "Analytics could not be loaded. Check the database connection and try again." }, JsonRequestBehavior.AllowGet);
            }
        }

        private static string BookingKey(BsonDocument booking) { return booking.GetValue("_id", booking.GetValue("requestId", BsonNull.Value)).ToString(); }
        private static string Status(BsonDocument booking) { return booking.GetValue("status", "").ToString().Trim().ToUpperInvariant(); }
        private static bool StatusIs(string status, string expected) { return string.Equals(status, expected, StringComparison.OrdinalIgnoreCase); }
        private static string IdValue(BsonDocument doc, params string[] fields)
        {
            foreach (var field in fields) { var v = doc.GetValue(field, BsonNull.Value); if (!v.IsBsonNull && !string.IsNullOrWhiteSpace(v.ToString())) return v.ToString(); }
            return null;
        }
        private static void AddCount(Dictionary<string, int[]> counts, string id, int index)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            if (!counts.ContainsKey(id)) counts[id] = new int[2];
            counts[id][index]++;
        }
        private static ObjectId? TryObjectId(string value)
        {
            ObjectId id;
            return ObjectId.TryParse(value, out id) ? (ObjectId?)id : null;
        }
        private static DateTime? ReadDate(BsonDocument doc, string field)
        {
            var value = doc.GetValue(field, BsonNull.Value);
            return value.IsValidDateTime ? (DateTime?)value.ToUniversalTime().ToLocalTime() : null;
        }
        private static DateTime Bucket(DateTime date, string period)
        {
            if (period == "daily") return date.Date;
            if (period == "weekly") return date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
            if (period == "yearly") return new DateTime(date.Year, 1, 1);
            return new DateTime(date.Year, date.Month, 1);
        }
    }
}
