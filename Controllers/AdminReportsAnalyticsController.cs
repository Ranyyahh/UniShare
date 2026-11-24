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
        public ActionResult Data(string period = "monthly", string from = null, string to = null, string status = null)
        {
            if (!new[] { "daily", "weekly", "monthly", "yearly" }.Contains((period ?? "").ToLowerInvariant()))
                return new HttpStatusCodeResult(400, "Choose a valid reporting period.");
            DateTime fromDate, toDate;
            if (!TryDateRange(from, to, out fromDate, out toDate))
                return new HttpStatusCodeResult(400, "Use valid from/to dates in yyyy-MM-dd format.");
            var statusFilter = (status ?? "").Trim().ToUpperInvariant();
            var hasDateFilter = !String.IsNullOrWhiteSpace(from) || !String.IsNullOrWhiteSpace(to);
            try
            {
                var usersFilter = Builders<UserDocument>.Filter.Eq(x => x.VerificationStatus, "APPROVED") &
                                  Builders<UserDocument>.Filter.Eq(x => x.Status, "ACTIVE") &
                                  Builders<UserDocument>.Filter.Ne(x => x.Role, "ADMIN");
                var verifiedUsers = (int)MongoDatabase.Users.CountDocuments(usersFilter);
                var items = MongoDatabase.Items.Find(Builders<ItemDocument>.Filter.Empty).ToList();
                var bookingProjection = Builders<BsonDocument>.Projection.Include("_id").Include("requestId")
                    .Include("itemId").Include("itemName").Include("status").Include("createdAt").Include("requestedAt")
                    .Include("startDate").Include("endDate").Include("handoverDate").Include("handoverAt")
                    .Include("returnedAt").Include("actualReturnAt").Include("completedAt")
                    .Include("durationHours").Include("estimatedFee").Include("rentalFee")
                    .Include("paymentMethod").Include("borrowerId").Include("renterId").Include("ownerId");
                var allBookings = MongoDatabase.Bookings.Find(Builders<BsonDocument>.Filter.Empty).Project(bookingProjection).ToList()
                    .GroupBy(BookingKey, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
                var bookings = allBookings.Where(x => statusFilter.Length == 0 || Status(x) == statusFilter)
                    .Where(x => InRange(ReadDate(x, "createdAt") ?? ReadDate(x, "requestedAt") ?? ReadDate(x, "startDate"), fromDate, toDate, hasDateFilter)).ToList();
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
                var userNames = MongoDatabase.Users.Find(Builders<UserDocument>.Filter.Empty).ToList()
                    .ToDictionary(x => x.Id.ToString(), x => ((x.FirstName ?? "") + " " + (x.LastName ?? "")).Trim(), StringComparer.OrdinalIgnoreCase);
                var activity = userCounts.Select(x => new { user = userNames.ContainsKey(x.Key) && userNames[x.Key].Length > 0 ? userNames[x.Key] : "User " + x.Key.Substring(0, Math.Min(6, x.Key.Length)), renting = x.Value[0], borrowing = x.Value[1] })
                    .OrderByDescending(x => x.renting + x.borrowing).Take(12).ToList();

                var allPayments = MongoDatabase.Payments.Find(Builders<BsonDocument>.Filter.Empty).ToList();
                var allLateFees = MongoDatabase.LateFees.Find(Builders<BsonDocument>.Filter.Empty).ToList();
                var allDisputes = MongoDatabase.Disputes.Find(Builders<BsonDocument>.Filter.Empty).ToList();
                var payments = allPayments.Where(x => (statusFilter.Length == 0 || PaymentStatus(x) == statusFilter) &&
                    InRange(ReadDate(x, "paidAt") ?? ReadDate(x, "createdAt"), fromDate, toDate, hasDateFilter)).ToList();
                var lateFees = allLateFees.Where(x => Number(x, "chargeableMinutes") > 0 &&
                    (statusFilter.Length == 0 || Status(x) == statusFilter) &&
                    InRange(ReadDate(x, "returnedAt") ?? ReadDate(x, "createdAt"), fromDate, toDate, hasDateFilter)).ToList();
                var disputes = allDisputes.Where(x => (statusFilter.Length == 0 || Status(x) == statusFilter) &&
                    InRange(ReadDate(x, "createdAt"), fromDate, toDate, hasDateFilter)).ToList();
                var allBookingsById = allBookings.ToDictionary(BookingKey, StringComparer.OrdinalIgnoreCase);
                var paymentsByBooking = allPayments.GroupBy(x => IdValue(x, "bookingId"), StringComparer.OrdinalIgnoreCase)
                    .Where(x => !String.IsNullOrWhiteSpace(x.Key)).ToDictionary(x => x.Key, x => x.OrderByDescending(y => ReadDate(y, "paidAt") ?? ReadDate(y, "createdAt")).First(), StringComparer.OrdinalIgnoreCase);
                var lateByBooking = allLateFees.GroupBy(x => IdValue(x, "bookingId"), StringComparer.OrdinalIgnoreCase)
                    .Where(x => !String.IsNullOrWhiteSpace(x.Key)).ToDictionary(x => x.Key, x => x.OrderByDescending(y => ReadDate(y, "returnedAt") ?? ReadDate(y, "createdAt")).First(), StringComparer.OrdinalIgnoreCase);
                var itemById = items.ToDictionary(x => x.Id.ToString(), StringComparer.OrdinalIgnoreCase);
                var rentalReports = bookings.OrderByDescending(x => ReadDate(x, "createdAt") ?? ReadDate(x, "startDate")).Select(booking =>
                {
                    BsonDocument payment, late;
                    ItemDocument item;
                    var key = BookingKey(booking);
                    var itemId = IdValue(booking, "itemId");
                    var borrowerId = IdValue(booking, "borrowerId", "renterId");
                    var ownerId = IdValue(booking, "ownerId");
                    itemById.TryGetValue(itemId ?? "", out item);
                    paymentsByBooking.TryGetValue(key, out payment);
                    lateByBooking.TryGetValue(key, out late);
                    return new
                    {
                        bookingId = key, requestId = IdValue(booking, "requestId"), status = Status(booking),
                        itemId, item = item == null ? booking.GetValue("itemName", "Item").ToString() : item.Name,
                        borrowerId, borrower = LookupName(userNames, borrowerId), ownerId, owner = LookupName(userNames, ownerId),
                        requestedAt = ReadDate(booking, "createdAt") ?? ReadDate(booking, "requestedAt"),
                        rentalDate = ReadDate(booking, "startDate") ?? ReadDate(booking, "handoverDate"),
                        expectedReturn = ReadDate(booking, "endDate"), actualReturn = ReadDate(booking, "returnedAt") ?? ReadDate(booking, "actualReturnAt"),
                        completedAt = ReadDate(booking, "completedAt"),
                        rentalFee = Number(booking, "estimatedFee", Number(booking, "rentalFee")),
                        lateFee = late == null ? 0d : Number(late, Status(late) == "FINALIZED" ? "finalAmount" : "amount"),
                        paymentMethod = payment == null ? "CASH" : payment.GetValue("paymentMethod", "CASH").ToString(),
                        paymentStatus = payment == null ? "UNRECORDED" : PaymentStatus(payment)
                    };
                }).ToList();
                var bookingReports = bookings.OrderByDescending(x => ReadDate(x, "createdAt") ?? ReadDate(x, "requestedAt"))
                    .Select(x => new { bookingId = BookingKey(x), requestId = IdValue(x, "requestId"), status = Status(x), itemId = IdValue(x, "itemId"), item = x.GetValue("itemName", "Item").ToString(), borrowerId = IdValue(x, "borrowerId", "renterId"), borrower = LookupName(userNames, IdValue(x, "borrowerId", "renterId")), ownerId = IdValue(x, "ownerId"), owner = LookupName(userNames, IdValue(x, "ownerId")), requestedAt = ReadDate(x, "createdAt") ?? ReadDate(x, "requestedAt"), startDate = ReadDate(x, "startDate"), endDate = ReadDate(x, "endDate"), rentalFee = Number(x, "estimatedFee", Number(x, "rentalFee")) }).ToList();
                var paymentReports = payments.OrderByDescending(x => ReadDate(x, "paidAt") ?? ReadDate(x, "createdAt")).Select(payment =>
                {
                    BsonDocument booking;
                    var key = IdValue(payment, "bookingId");
                    allBookingsById.TryGetValue(key ?? "", out booking);
                    return new { paymentId = IdValue(payment, "_id"), bookingId = key, requestId = booking == null ? "" : IdValue(booking, "requestId"), amount = Number(payment, "amount"), method = payment.GetValue("paymentMethod", "Cash").ToString(), status = PaymentStatus(payment), paymentDate = ReadDate(payment, "paidAt") ?? ReadDate(payment, "createdAt") };
                }).ToList();
                var lateReturnReports = lateFees.OrderByDescending(x => ReadDate(x, "returnedAt") ?? ReadDate(x, "createdAt")).Select(late =>
                {
                    BsonDocument booking;
                    var key = IdValue(late, "bookingId");
                    allBookingsById.TryGetValue(key ?? "", out booking);
                    var dueAt = ReadDate(late, "dueAt");
                    var returnedAt = ReadDate(late, "returnedAt");
                    return new { bookingId = key, requestId = booking == null ? "" : IdValue(booking, "requestId"), expectedReturn = dueAt, actualReturn = returnedAt, lateDurationMinutes = dueAt.HasValue && returnedAt.HasValue ? Math.Max(0, (int)(returnedAt.Value - dueAt.Value).TotalMinutes) : 0, chargeableMinutes = Number(late, "chargeableMinutes"), fee = Number(late, Status(late) == "FINALIZED" ? "finalAmount" : "amount"), status = Status(late) };
                }).ToList();
                var disputeReports = disputes.OrderByDescending(x => ReadDate(x, "createdAt")).Select(dispute =>
                {
                    var key = IdValue(dispute, "bookingId");
                    BsonDocument booking;
                    allBookingsById.TryGetValue(key ?? "", out booking);
                    var reporterId = IdValue(dispute, "reporterId");
                    var respondentId = IdValue(dispute, "respondentId");
                    return new { disputeId = IdValue(dispute, "_id"), bookingId = key, requestId = IdValue(dispute, "requestId"), item = IdValue(dispute, "itemName") ?? (booking == null ? "Rental item" : booking.GetValue("itemName", "Rental item").ToString()), status = Status(dispute), reporterId, reporter = LookupName(userNames, reporterId), respondentId, respondent = LookupName(userNames, respondentId), reportedAt = ReadDate(dispute, "createdAt"), resolvedAt = ReadDate(dispute, "resolvedAt"), notes = dispute.GetValue("adminNotes", "").ToString() };
                }).ToList();
                var itemPerformance = bookings.Where(x => !ExcludedStatuses.Contains(Status(x)))
                    .GroupBy(x => IdValue(x, "itemId") ?? "")
                    .Select(group =>
                    {
                        ItemDocument item;
                        itemById.TryGetValue(group.Key, out item);
                        var actualRevenue = group.Where(x => { BsonDocument payment; return paymentsByBooking.TryGetValue(BookingKey(x), out payment) && PaymentStatus(payment) == "PAID"; })
                            .Sum(x => { BsonDocument payment; return paymentsByBooking.TryGetValue(BookingKey(x), out payment) ? Number(payment, "amount") : 0d; });
                        return new { itemId = group.Key, item = item == null ? group.First().GetValue("itemName", "Item").ToString() : item.Name, ownerId = item == null ? "" : item.OwnerId.ToString(), owner = item == null ? "Unknown" : LookupName(userNames, item.OwnerId.ToString()), rentalCount = group.Count(), confirmedRevenue = actualRevenue };
                    }).OrderByDescending(x => x.rentalCount).ToList();
                var paymentSummary = new
                {
                    total = payments.Count,
                    paid = payments.Count(x => PaymentStatus(x) == "PAID"),
                    unpaid = payments.Count(x => PaymentStatus(x) == "UNPAID" || PaymentStatus(x) == "PENDING"),
                    disputed = payments.Count(x => PaymentStatus(x) == "DISPUTED"),
                    refunded = payments.Count(x => PaymentStatus(x) == "REFUNDED"),
                    confirmedAmount = payments.Where(x => PaymentStatus(x) == "PAID").Sum(x => Number(x, "amount"))
                };
                var lateReturnSummary = new
                {
                    assessed = lateFees.Count(x => Number(x, "chargeableMinutes") > 0),
                    estimated = lateFees.Count(x => Status(x) == "ESTIMATED"),
                    finalized = lateFees.Count(x => Status(x) == "FINALIZED"),
                    estimatedAmount = lateFees.Where(x => Status(x) == "ESTIMATED").Sum(x => Number(x, "amount")),
                    finalizedAmount = lateFees.Where(x => Status(x) == "FINALIZED").Sum(x => Number(x, "finalAmount"))
                };
                var disputeSummary = new
                {
                    total = disputes.Count,
                    open = disputes.Count(x => new[] { "OPEN", "UNDER_REVIEW", "INFO_REQUESTED" }.Contains(Status(x))),
                    resolved = disputes.Count(x => Status(x) == "RESOLVED")
                };

                return Json(new
                {
                    cards = new { platformRevenue = (decimal?)null, totalRentals = bookings.Count, totalUsers = verifiedUsers, totalItems = items.Count },
                    revenueAvailable = false,
                    revenueNote = "Platform fees or commission are not recorded in the current payment data.",
                    rentalsByDate, listingStatuses, topItems, activity,
                    paymentSummary, lateReturnSummary, disputeSummary,
                    rentalReports, bookingReports, paymentReports, itemPerformance, lateReturnReports, disputeReports,
                    totals = new { rentals = rentalReports.Count, bookings = bookingReports.Count, payments = paymentReports.Count, items = itemPerformance.Count, lateReturns = lateReturnReports.Count, disputes = disputeReports.Count },
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
        private static string PaymentStatus(BsonDocument payment)
        {
            return payment.GetValue("voidedAt", BsonNull.Value).IsValidDateTime ? "CANCELLED" : Status(payment);
        }
        private static double Number(BsonDocument document, string field)
        {
            var value = document.GetValue(field, BsonNull.Value);
            return value.IsNumeric ? value.ToDouble() : 0d;
        }
        private static double Number(BsonDocument document, string field, double fallback)
        {
            var value = document.GetValue(field, BsonNull.Value);
            return value.IsNumeric ? value.ToDouble() : fallback;
        }
        private static string LookupName(IDictionary<string, string> names, string id)
        {
            string name;
            return !String.IsNullOrWhiteSpace(id) && names.TryGetValue(id, out name) && !String.IsNullOrWhiteSpace(name) ? name : "Unknown";
        }
        private static bool TryDateRange(string from, string to, out DateTime fromDate, out DateTime toDate)
        {
            fromDate = DateTime.MinValue;
            toDate = DateTime.MaxValue;
            DateTime parsed;
            if (!String.IsNullOrWhiteSpace(from))
            {
                if (!DateTime.TryParseExact(from, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out parsed)) return false;
                fromDate = parsed.Date;
            }
            if (!String.IsNullOrWhiteSpace(to))
            {
                if (!DateTime.TryParseExact(to, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out parsed)) return false;
                toDate = parsed.Date.AddDays(1).AddTicks(-1);
            }
            return fromDate <= toDate;
        }
        private static bool InRange(DateTime? value, DateTime from, DateTime to, bool hasDateFilter)
        {
            return !hasDateFilter || (value.HasValue && value.Value.ToLocalTime() >= from && value.Value.ToLocalTime() <= to);
        }
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
