using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UniShare.Models;
using UniShare.Services;

namespace UniShare.Controllers
{
    // ------------------------------------------------------------------------------------
    // Data access seam.
    // The project currently has NO transaction store (no DbContext, no connection string,
    // no rental/borrowing/payment tables; MyRentals and Dashboard run on hard-coded sample
    // data). To keep this report free of invented numbers, all reads go through this
    // interface. Implement it against the real store and pass it to the second controller
    // constructor (or return it from the parameterless one). Implementations MUST filter
    // by the user key in the query itself and return only the rows described below.
    // ------------------------------------------------------------------------------------
    public interface IReportDataSource
    {
        bool IsAvailable { get; }
        bool IsSampleData { get; }                       // true only for the preview data source
        string UnavailableReason { get; }

        // Rentals of items owned by ownerKey whose RequestedAt OR CompletedAt is in [fromInclusive, toExclusive).
        IList<RentalTransactionRecord> GetOwnerRentals(string ownerKey, DateTime fromInclusive, DateTime toExclusive);

        // Borrowings by borrowerKey whose RequestedAt OR PaidAt is in [fromInclusive, toExclusive).
        IList<BorrowingTransactionRecord> GetBorrowings(string borrowerKey, DateTime fromInclusive, DateTime toExclusive);
    }

    // Default source: reports honestly that nothing is connected and returns no rows.
    public class UnavailableReportDataSource : IReportDataSource
    {
        public bool IsAvailable { get { return false; } }
        public bool IsSampleData { get { return false; } }

        public string UnavailableReason
        {
            get
            {
                return "No rental or borrowing transaction store is connected to this project yet, so there is nothing to report. " +
                       "Connect an IReportDataSource implementation to show real figures.";
            }
        }

        public IList<RentalTransactionRecord> GetOwnerRentals(string ownerKey, DateTime fromInclusive, DateTime toExclusive)
        {
            return new List<RentalTransactionRecord>();
        }

        public IList<BorrowingTransactionRecord> GetBorrowings(string borrowerKey, DateTime fromInclusive, DateTime toExclusive)
        {
            return new List<BorrowingTransactionRecord>();
        }
    }

    // ------------------------------------------------------------------------------------
    // PREVIEW ONLY. Sample transactions so the page can be previewed without signing in
    // (same idea as the sample rentals on the MyRentals page). The data is generated in
    // memory, is not stored anywhere and is NOT the user's real activity. Dates are
    // relative to today so the default range and the This Week / Last Week / This Month
    // filters always have something to show. The CSV export labels it as sample data. Remove this class (and the parameterless constructor's use of it)
    // once a real IReportDataSource exists.
    // ------------------------------------------------------------------------------------
    public class MockReportDataSource : IReportDataSource
    {
        public const string PreviewUserKey = "sample-preview-user";

        private static readonly string[] OwnItems =
        {
            "Scientific Calculator", "Drafting Technical Drawing Set", "Engineering Mechanics Textbook",
            "Lab Coat + Goggles", "Wireless Headset", "USB-C Hub", "Badminton Racket Set", "Drawing Tablet"
        };
        private static readonly decimal[] OwnFees = { 40m, 85m, 60m, 45m, 90m, 55m, 120m, 150m };

        private static readonly string[] BorrowedItems =
        {
            "Arduino Starter Kit", "Digital Multimeter", "Portable Tripod", "Basketball", "Calculus Textbook"
        };
        private static readonly decimal[] BorrowedFees = { 110m, 70m, 50m, 35m, 45m };

        public bool IsAvailable { get { return true; } }
        public bool IsSampleData { get { return true; } }
        public string UnavailableReason { get { return null; } }

        public IList<RentalTransactionRecord> GetOwnerRentals(string ownerKey, DateTime fromInclusive, DateTime toExclusive)
        {
            return BuildRentals(ownerKey)
                .Where(r => (r.RequestedAt >= fromInclusive && r.RequestedAt < toExclusive) ||
                            (r.CompletedAt.HasValue && r.CompletedAt.Value >= fromInclusive && r.CompletedAt.Value < toExclusive))
                .ToList();
        }

        public IList<BorrowingTransactionRecord> GetBorrowings(string borrowerKey, DateTime fromInclusive, DateTime toExclusive)
        {
            return BuildBorrowings(borrowerKey)
                .Where(b => (b.RequestedAt >= fromInclusive && b.RequestedAt < toExclusive) ||
                            (b.PaidAt.HasValue && b.PaidAt.Value >= fromInclusive && b.PaidAt.Value < toExclusive))
                .ToList();
        }

        // Same status words the MyRentals page uses. Recent requests are still open,
        // older ones are mostly finished, so the picture looks like a real history.
        private static string PickStatus(Random rnd, int daysAgo)
        {
            int roll = rnd.Next(100);
            if (daysAgo == 0)
                return roll < 40 ? "Pending" : roll < 70 ? "Accepted" : "Ongoing";
            if (daysAgo <= 3)
                return roll < 35 ? "Ongoing" : roll < 45 ? "Overdue" : roll < 60 ? "Returned"
                     : roll < 85 ? "Completed" : roll < 92 ? "Disputed" : "Declined";
            return roll < 70 ? "Completed" : roll < 78 ? "Disputed" : roll < 86 ? "Declined"
                 : roll < 90 ? "Returned" : "Completed";
        }

        private static List<RentalTransactionRecord> BuildRentals(string ownerKey)
        {
            var rnd = new Random(2026);                       // fixed seed: the preview is stable between refreshes
            DateTime now = DateTime.Now;
            DateTime today = now.Date;
            var list = new List<RentalTransactionRecord>();
            int seq = 1;

            for (int back = 0; back <= 420; back++)
            {
                DateTime day = today.AddDays(-back);
                int count = rnd.Next(0, 3);
                if (back <= 1) count = Math.Max(count, 1);       // today and yesterday always have a rental

                for (int i = 0; i < count; i++)
                {
                    int itemIndex = rnd.Next(OwnItems.Length);
                    int quantity = rnd.Next(4) == 0 ? 2 : 1;
                    string status = PickStatus(rnd, back);
                    DateTime requested = day.AddHours(rnd.Next(8, 19)).AddMinutes(rnd.Next(0, 60));
                    if (requested > now) requested = now;

                    DateTime? completed = null;
                    // Guarantee revenue dated today so the current week always has data.
                    if (back == 1 && i == 0) status = "Completed";
                    if (status == "Completed")
                    {
                        completed = (back == 1 && i == 0)
                            ? today.AddMinutes(Math.Min(now.TimeOfDay.TotalMinutes, 540))
                            : requested.AddHours(rnd.Next(2, 49));
                        if (completed.Value > now) completed = now;
                    }

                    list.Add(new RentalTransactionRecord
                    {
                        TransactionId = "R-" + (seq++),
                        ItemId = "own-" + itemIndex,
                        ItemName = OwnItems[itemIndex],
                        OwnerKey = ownerKey,
                        Quantity = quantity,
                        Amount = OwnFees[itemIndex] * quantity,
                        Status = status,
                        RequestedAt = requested,
                        CompletedAt = completed
                    });
                }
            }
            return list;
        }

        private static List<BorrowingTransactionRecord> BuildBorrowings(string borrowerKey)
        {
            var rnd = new Random(2027);
            DateTime now = DateTime.Now;
            DateTime today = now.Date;
            var list = new List<BorrowingTransactionRecord>();
            int seq = 1;

            for (int back = 0; back <= 420; back++)
            {
                DateTime day = today.AddDays(-back);
                int count = (back == 0 || rnd.Next(5) == 0) ? 1 : 0;

                for (int i = 0; i < count; i++)
                {
                    int itemIndex = rnd.Next(BorrowedItems.Length);
                    int quantity = rnd.Next(5) == 0 ? 2 : 1;
                    string status = PickStatus(rnd, back);
                    DateTime requested = day.AddHours(rnd.Next(8, 19)).AddMinutes(rnd.Next(0, 60));
                    if (requested > now) requested = now;

                    // Only transactions that actually started have a payment, and a few
                    // of those are deliberately left unpaid to exercise the "no payment" note.
                    bool started = status == "Ongoing" || status == "Overdue" || status == "Completed" ||
                                   status == "Disputed" || status == "Returned";
                    bool paid = started && rnd.Next(100) < 85;

                    list.Add(new BorrowingTransactionRecord
                    {
                        TransactionId = "B-" + (seq++),
                        ItemId = "borrowed-" + itemIndex,
                        ItemName = BorrowedItems[itemIndex],
                        BorrowerKey = borrowerKey,
                        Quantity = quantity,
                        Status = status,
                        RequestedAt = requested,
                        AmountPaid = paid ? (decimal?)(BorrowedFees[itemIndex] * quantity) : null,
                        PaidAt = paid ? (DateTime?)requested : null
                    });
                }
            }
            return list;
        }
    }

    [Authorize]
    public class ReportsAnalyticsController : Controller
    {
        private const string DateFormat = "yyyy-MM-dd";
        private const int DefaultRangeDays = 30;
        private const int MaxRangeDays = 366;
        private const int TopItemsLimit = 10;

        private readonly IReportDataSource _source;

        public ReportsAnalyticsController() : this(new MongoReportDataSource()) { }

        public ReportsAnalyticsController(IReportDataSource source)
        {
            _source = source ?? new UnavailableReportDataSource();
        }

        // GET: /ReportsAnalytics
        [HttpGet]
        public ActionResult Index()
        {
            ViewBag.Title = "Reports & Analytics";

            DateTime to = DateTime.Today;
            DateTime from = to.AddDays(-(DefaultRangeDays - 1));

            var model = new ReportsAnalyticsViewModel
            {
                DefaultFrom = from.ToString(DateFormat, CultureInfo.InvariantCulture),
                DefaultTo = to.ToString(DateFormat, CultureInfo.InvariantCulture),
                DataUrl = Url.Action("Data", "ReportsAnalytics"),
                ExportUrl = Url.Action("Export", "ReportsAnalytics"),
                DataSourceAvailable = _source.IsAvailable && !_source.IsSampleData,
                DataSourceNote = !_source.IsAvailable ? _source.UnavailableReason : null
            };
            return View(model);
        }

        // GET: /ReportsAnalytics/Data?from=yyyy-MM-dd&to=yyyy-MM-dd
        [HttpGet]
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
        public ActionResult Data(string from, string to)
        {
            string userKey = ResolveUserKey();
            if (userKey == null) return new HttpUnauthorizedResult();

            DateTime fromDate, toDate;
            string error;
            if (!TryReadRange(from, to, out fromDate, out toDate, out error))
                return JsonError(error);

            try { return JsonPayload(BuildReport(userKey, fromDate, toDate)); }
            catch
            {
                Response.StatusCode = 503;
                Response.TrySkipIisCustomErrors = true;
                return JsonPayload(new { error = "Reports are temporarily unavailable. Refresh and try again." });
            }
        }

        // GET: /ReportsAnalytics/Export?from=yyyy-MM-dd&to=yyyy-MM-dd  (CSV download)
        [HttpGet]
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
        public ActionResult Export(string from, string to)
        {
            string userKey = ResolveUserKey();
            if (userKey == null) return new HttpUnauthorizedResult();

            DateTime fromDate, toDate;
            string error;
            if (!TryReadRange(from, to, out fromDate, out toDate, out error))
                return JsonError(error);

            ReportPayload report = BuildReport(userKey, fromDate, toDate);
            string csv = BuildCsv(report);

            // UTF-8 with BOM so Excel shows the peso sign correctly.
            byte[] preamble = new UTF8Encoding(true).GetPreamble();
            byte[] body = Encoding.UTF8.GetBytes(csv);
            byte[] bytes = new byte[preamble.Length + body.Length];
            Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
            Buffer.BlockCopy(body, 0, bytes, preamble.Length, body.Length);

            string fileName = string.Format(CultureInfo.InvariantCulture,
                "UniShare-Report_{0}_to_{1}.csv", report.From, report.To);
            return File(bytes, "text/csv", fileName);
        }

        // ------------------------------------------------------------------------------
        // Request helpers
        // ------------------------------------------------------------------------------

        // The report key always comes from the authenticated identity. No query-string user IDs are accepted.
        private string ResolveUserKey()
        {
            if (IsSignedIn()) return User.Identity.Name;
            return null;
        }

        private bool IsSignedIn()
        {
            return User != null && User.Identity != null && User.Identity.IsAuthenticated
                   && !string.IsNullOrEmpty(User.Identity.Name);
        }

        private static bool TryReadRange(string from, string to, out DateTime fromDate, out DateTime toDate, out string error)
        {
            error = null;
            toDate = DateTime.Today;
            fromDate = toDate.AddDays(-(DefaultRangeDays - 1));

            if (!string.IsNullOrWhiteSpace(from) &&
                !DateTime.TryParseExact(from, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out fromDate))
            {
                error = "The From date is not a valid date.";
                return false;
            }
            if (!string.IsNullOrWhiteSpace(to) &&
                !DateTime.TryParseExact(to, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out toDate))
            {
                error = "The To date is not a valid date.";
                return false;
            }
            if (fromDate > toDate)
            {
                error = "The From date must be on or before the To date.";
                return false;
            }
            if ((toDate - fromDate).TotalDays + 1 > MaxRangeDays)
            {
                error = "Please choose a range of 366 days or fewer.";
                return false;
            }
            return true;
        }

        private ActionResult JsonPayload(object value)
        {
            var settings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
            return Content(JsonConvert.SerializeObject(value, settings), "application/json");
        }

        private ActionResult JsonError(string message)
        {
            Response.StatusCode = 400;
            Response.TrySkipIisCustomErrors = true;
            return JsonPayload(new { error = message });
        }

        // ------------------------------------------------------------------------------
        // Report building
        // ------------------------------------------------------------------------------

        private static string BucketOf(string status)
        {
            string s = (status ?? string.Empty).Trim();
            if (s.Equals("Ongoing", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("Overdue", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("Active", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("Return Requested", StringComparison.OrdinalIgnoreCase)) return "Active";
            if (s.Equals("Completed", StringComparison.OrdinalIgnoreCase) || s.Equals("Returned", StringComparison.OrdinalIgnoreCase)) return "Completed";
            if (s.Equals("Disputed", StringComparison.OrdinalIgnoreCase)) return "Disputed";
            if (s.Equals("Pending", StringComparison.OrdinalIgnoreCase) || s.Equals("Requested", StringComparison.OrdinalIgnoreCase)) return "Pending";
            if (s.Equals("Accepted", StringComparison.OrdinalIgnoreCase) || s.Equals("Approved", StringComparison.OrdinalIgnoreCase)) return "Accepted";
            if (s.Equals("Cancelled", StringComparison.OrdinalIgnoreCase) || s.Equals("Canceled", StringComparison.OrdinalIgnoreCase) || s.Equals("Expired", StringComparison.OrdinalIgnoreCase)) return "Cancelled";
            if (s.Equals("Rejected", StringComparison.OrdinalIgnoreCase) || s.Equals("Declined", StringComparison.OrdinalIgnoreCase)) return "Rejected";
            return null;
        }

        private static bool InRange(DateTime value, DateTime from, DateTime toExclusive)
        {
            return value >= from && value < toExclusive;
        }

        private static string Granularity(DateTime from, DateTime to)
        {
            double days = (to - from).TotalDays + 1;
            if (days <= 31) return "day";
            if (days <= 120) return "week";
            return "month";
        }

        private static DateTime BucketStart(DateTime d, string granularity)
        {
            d = d.Date;
            if (granularity == "week")
            {
                int back = ((int)d.DayOfWeek + 6) % 7;      // Monday-based weeks
                return d.AddDays(-back);
            }
            if (granularity == "month") return new DateTime(d.Year, d.Month, 1);
            return d;
        }

        private static DateTime NextBucket(DateTime d, string granularity)
        {
            if (granularity == "week") return d.AddDays(7);
            if (granularity == "month") return d.AddMonths(1);
            return d.AddDays(1);
        }

        // Builds a zero-filled series: periods with no activity are real zeros, not gaps.
        // When details are given, each bucket also lists the transactions behind its value.
        private static TimeSeries BuildSeries(DateTime from, DateTime to, string granularity,
                                              IEnumerable<KeyValuePair<DateTime, decimal>> points,
                                              IEnumerable<KeyValuePair<DateTime, TransactionDetail>> details = null)
        {
            var totals = new Dictionary<DateTime, decimal>();
            foreach (var p in points)
            {
                DateTime key = BucketStart(p.Key, granularity);
                decimal current;
                totals.TryGetValue(key, out current);
                totals[key] = current + p.Value;
            }

            var detailLookup = new Dictionary<DateTime, List<TransactionDetail>>();
            if (details != null)
            {
                foreach (var d in details.OrderBy(x => x.Key).ThenBy(x => x.Value.TransactionId, StringComparer.Ordinal))
                {
                    DateTime key = BucketStart(d.Key, granularity);
                    List<TransactionDetail> list;
                    if (!detailLookup.TryGetValue(key, out list)) { list = new List<TransactionDetail>(); detailLookup[key] = list; }
                    list.Add(d.Value);
                }
            }

            var series = new TimeSeries();
            for (DateTime b = BucketStart(from, granularity); b <= to; b = NextBucket(b, granularity))
            {
                decimal v;
                totals.TryGetValue(b, out v);
                series.Labels.Add(b.ToString(DateFormat, CultureInfo.InvariantCulture));
                series.Values.Add(v);
                if (details != null)
                {
                    List<TransactionDetail> list;
                    series.Details.Add(detailLookup.TryGetValue(b, out list) ? list : new List<TransactionDetail>());
                }
            }
            return series;
        }

        private static TransactionDetail ToDetail(BorrowingTransactionRecord b, DateTime date)
        {
            return new TransactionDetail
            {
                TransactionId = b.TransactionId,
                ItemName = string.IsNullOrWhiteSpace(b.ItemName) ? "(Unnamed item)" : b.ItemName,
                Quantity = Math.Max(b.Quantity, 0),
                Status = b.Status,
                Amount = b.AmountPaid,
                Date = date.ToString(DateFormat, CultureInfo.InvariantCulture)
            };
        }

        private static TransactionDetail ToDetail(RentalTransactionRecord r, DateTime date)
        {
            return new TransactionDetail
            {
                TransactionId = r.TransactionId,
                ItemName = string.IsNullOrWhiteSpace(r.ItemName) ? "(Unnamed item)" : r.ItemName,
                Quantity = Math.Max(r.Quantity, 0),
                Status = r.Status,
                Amount = r.Amount,
                Date = date.ToString(DateFormat, CultureInfo.InvariantCulture)
            };
        }

        private ReportPayload BuildReport(string userKey, DateTime from, DateTime to)
        {
            DateTime toExclusive = to.AddDays(1);
            string granularity = Granularity(from, to);

            var report = new ReportPayload
            {
                From = from.ToString(DateFormat, CultureInfo.InvariantCulture),
                To = to.ToString(DateFormat, CultureInfo.InvariantCulture),
                Granularity = granularity,
                IsSampleData = _source.IsSampleData,
                GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
            };

            // ---- Renting: the user is the item OWNER. Re-checked here as well as in the source. ----
            var rentals = (_source.GetOwnerRentals(userKey, from, toExclusive) ?? new List<RentalTransactionRecord>())
                .Where(r => r != null && string.Equals(r.OwnerKey, userKey, StringComparison.OrdinalIgnoreCase))
                .GroupBy(r => r.TransactionId ?? string.Empty)       // one row per transaction, even if the source joined
                .Select(g => g.First())
                .ToList();

            // Activity metrics use RequestedAt (when the transaction exists)...
            var rentalsInRange = rentals.Where(r => InRange(r.RequestedAt, from, toExclusive)).ToList();
            var activeRentals = rentalsInRange.Where(r => BucketOf(r.Status) == "Active").ToList();
            var completedRentals = rentalsInRange.Where(r => BucketOf(r.Status) == "Completed").ToList();
            report.Cards.TotalRentals = rentalsInRange.Count;
            report.Cards.ItemsRented = activeRentals.Concat(completedRentals).Sum(r => Math.Max(r.Quantity, 0));
            report.Cards.PendingTransactions = rentalsInRange.Count(r => BucketOf(r.Status) == "Pending" || BucketOf(r.Status) == "Accepted");
            report.Cards.ActiveRentals = activeRentals.Count;
            report.Cards.CompletedRentals = completedRentals.Count;
            report.Cards.RejectedOrCancelledTransactions = rentalsInRange.Count(r => BucketOf(r.Status) == "Rejected" || BucketOf(r.Status) == "Cancelled");

            report.RentalStatus = new StatusDistribution
            {
                Pending = rentalsInRange.Count(r => BucketOf(r.Status) == "Pending"),
                Accepted = rentalsInRange.Count(r => BucketOf(r.Status) == "Accepted"),
                Active = activeRentals.Count,
                Completed = completedRentals.Count,
                Disputed = rentalsInRange.Count(r => BucketOf(r.Status) == "Disputed"),
                Cancelled = rentalsInRange.Count(r => BucketOf(r.Status) == "Cancelled"),
                Rejected = rentalsInRange.Count(r => BucketOf(r.Status) == "Rejected"),
                NotCounted = rentalsInRange.Count(r => BucketOf(r.Status) == null)
            };

            report.MostRentedItems = rentalsInRange.Where(r => BucketOf(r.Status) == "Active" || BucketOf(r.Status) == "Completed")
                .GroupBy(r => string.IsNullOrEmpty(r.ItemId) ? "name:" + (r.ItemName ?? string.Empty) : "id:" + r.ItemId)
                .Select(g => new TopItem
                {
                    Name = string.IsNullOrWhiteSpace(g.First().ItemName) ? "(Unnamed item)" : g.First().ItemName,
                    Quantity = g.Sum(r => Math.Max(r.Quantity, 0)),
                    Revenue = g.Where(r => BucketOf(r.Status) == "Completed").Sum(r => r.Amount)
                })
                .OrderByDescending(t => t.Quantity).ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .Take(TopItemsLimit)
                .ToList();

            // ...revenue uses CompletedAt (when the rental was completed), Completed status only.
            var revenuePoints = rentals
                .Where(r => BucketOf(r.Status) == "Completed" && r.CompletedAt.HasValue
                            && InRange(r.CompletedAt.Value, from, toExclusive))
                .Select(r => new KeyValuePair<DateTime, decimal>(r.CompletedAt.Value, r.Amount))
                .ToList();
            report.Cards.TotalRevenue = revenuePoints.Sum(p => p.Value);
            report.RentalRevenue = BuildSeries(from, to, granularity, revenuePoints);
            report.RentalRequestsReceived = BuildSeries(from, to, granularity,
                rentalsInRange.Select(r => new KeyValuePair<DateTime, decimal>(r.RequestedAt, 1m)),
                rentalsInRange.Select(r => new KeyValuePair<DateTime, TransactionDetail>(r.RequestedAt, ToDetail(r, r.RequestedAt))));
            report.Transactions.AddRange(rentalsInRange.Select(r => new ReportHistoryRow
            {
                TransactionId = r.TransactionId, ItemName = r.ItemName, TransactionType = "Rental received",
                Date = r.RequestedAt.ToString(DateFormat, CultureInfo.InvariantCulture), Amount = r.Amount, Status = r.Status
            }));

            // ---- Borrowing: the user is the BORROWER. Kept entirely separate from renting. ----
            var borrowings = (_source.GetBorrowings(userKey, from, toExclusive) ?? new List<BorrowingTransactionRecord>())
                .Where(b => b != null && string.Equals(b.BorrowerKey, userKey, StringComparison.OrdinalIgnoreCase))
                .GroupBy(b => b.TransactionId ?? string.Empty)
                .Select(g => g.First())
                .ToList();

            var borrowingsInRange = borrowings.Where(b => InRange(b.RequestedAt, from, toExclusive)).ToList();
            var activeBorrowings = borrowingsInRange.Where(b => BucketOf(b.Status) == "Active").ToList();
            var completedBorrowings = borrowingsInRange.Where(b => BucketOf(b.Status) == "Completed").ToList();
            report.Cards.TotalBorrowings = borrowingsInRange.Count;
            report.Cards.PendingBorrowingRequests = borrowingsInRange.Count(b => BucketOf(b.Status) == "Pending" || BucketOf(b.Status) == "Accepted");
            report.Cards.ActiveBorrowedRentals = activeBorrowings.Count;
            report.Cards.CompletedBorrowedRentals = completedBorrowings.Count;
            report.Cards.CancelledOrRejectedBorrowings = borrowingsInRange.Count(b => BucketOf(b.Status) == "Cancelled" || BucketOf(b.Status) == "Rejected");
            report.BorrowingsNotCounted = borrowingsInRange.Count(b => BucketOf(b.Status) == null);
            report.BorrowingsWithoutPayment = borrowingsInRange.Count(b => !b.AmountPaid.HasValue || !b.PaidAt.HasValue);
            report.BorrowingActivity = BuildSeries(from, to, granularity,
                borrowingsInRange.Select(b => new KeyValuePair<DateTime, decimal>(b.RequestedAt, 1m)),
                borrowingsInRange.Select(b => new KeyValuePair<DateTime, TransactionDetail>(b.RequestedAt, ToDetail(b, b.RequestedAt))));
            report.RentalRequestsSubmitted = report.BorrowingActivity;
            report.Transactions.AddRange(borrowingsInRange.Select(b => new ReportHistoryRow
            {
                TransactionId = b.TransactionId, ItemName = b.ItemName, TransactionType = "Rental requested",
                Date = b.RequestedAt.ToString(DateFormat, CultureInfo.InvariantCulture), Amount = b.EstimatedAmount, Status = b.Status
            }));
            report.Transactions = report.Transactions.OrderByDescending(t => t.Date, StringComparer.Ordinal).ThenBy(t => t.TransactionId, StringComparer.Ordinal).ToList();

            // Expenses: only rows that actually have a recorded payment, dated by PaidAt.
            var paidBorrowings = borrowings
                .Where(b => b.AmountPaid.HasValue && b.PaidAt.HasValue && InRange(b.PaidAt.Value, from, toExclusive))
                .ToList();
            report.BorrowingExpenses = BuildSeries(from, to, granularity,
                paidBorrowings.Select(b => new KeyValuePair<DateTime, decimal>(b.PaidAt.Value, b.AmountPaid.Value)),
                paidBorrowings.Select(b => new KeyValuePair<DateTime, TransactionDetail>(b.PaidAt.Value, ToDetail(b, b.PaidAt.Value))));

            return report;
        }

        // ------------------------------------------------------------------------------
        // CSV export (renting and borrowing are written as separate sections)
        // ------------------------------------------------------------------------------

        private static string Csv(object value)
        {
            string s = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            // Neutralise spreadsheet formulas coming from user-entered text such as item names.
            if (s.Length > 0 && "=+-@\t\r".IndexOf(s[0]) >= 0) s = "'" + s;
            if (s.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0) s = "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        private static string Money(decimal value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static string BuildCsv(ReportPayload r)
        {
            var sb = new StringBuilder();
            Action<object[]> row = cells => sb.Append(string.Join(",", cells.Select(Csv))).Append("\r\n");

            row(new object[] { "UniShare Reports & Analytics" });
            row(new object[] { "Period", r.From + " to " + r.To });
            row(new object[] { "Generated", r.GeneratedAt });
            if (r.IsSampleData) row(new object[] { "NOTE", "SAMPLE DATA - preview only, not real transactions" });
            row(new object[] { "Grouping", r.Granularity });
            row(new object[] { });

            row(new object[] { "RENTING (items you own)" });
            row(new object[] { "Total Revenue (PHP, completed rentals)", Money(r.Cards.TotalRevenue) });
            row(new object[] { "Total Rentals", r.Cards.TotalRentals });
            row(new object[] { "Items Rented (quantity)", r.Cards.ItemsRented });
            row(new object[] { "Pending Transactions", r.Cards.PendingTransactions });
            row(new object[] { "Active Rentals", r.Cards.ActiveRentals });
            row(new object[] { "Completed Rentals", r.Cards.CompletedRentals });
            row(new object[] { "Rejected or Cancelled Transactions", r.Cards.RejectedOrCancelledTransactions });
            row(new object[] { });
            row(new object[] { "Rental Revenue Over Time", "Period start", "Revenue (PHP)" });
            for (int i = 0; i < r.RentalRevenue.Labels.Count; i++)
                row(new object[] { "", r.RentalRevenue.Labels[i], Money(r.RentalRevenue.Values[i]) });
            row(new object[] { });
            row(new object[] { "Most Rented Items", "Item", "Quantity rented" });
            row(new object[] { "", "", "", "Completed rental fees (PHP)" });
            foreach (var item in r.MostRentedItems) row(new object[] { "", item.Name, item.Quantity, Money(item.Revenue) });
            row(new object[] { });
            row(new object[] { "Rental Status Distribution", "Status", "Transactions" });
            row(new object[] { "", "Active", r.RentalStatus.Active });
            row(new object[] { "", "Completed", r.RentalStatus.Completed });
            row(new object[] { "", "Disputed", r.RentalStatus.Disputed });
            row(new object[] { "", "Not counted (pending, accepted, declined, returned)", r.RentalStatus.NotCounted });
            row(new object[] { });

            row(new object[] { "BORROWING (items you rent from others)" });
            row(new object[] { "Total Borrowings", r.Cards.TotalBorrowings });
            row(new object[] { "Pending Rental Requests", r.Cards.PendingBorrowingRequests });
            row(new object[] { "Active Rentals", r.Cards.ActiveBorrowedRentals });
            row(new object[] { "Completed Rentals", r.Cards.CompletedBorrowedRentals });
            row(new object[] { "Cancelled or Rejected Requests", r.Cards.CancelledOrRejectedBorrowings });
            row(new object[] { "Borrowings with no payment recorded", r.BorrowingsWithoutPayment });
            row(new object[] { });
            row(new object[] { "Borrowing Activity", "Period start", "Borrowings" });
            for (int i = 0; i < r.BorrowingActivity.Labels.Count; i++)
                row(new object[] { "", r.BorrowingActivity.Labels[i], r.BorrowingActivity.Values[i].ToString("0", CultureInfo.InvariantCulture) });
            row(new object[] { });
            row(new object[] { "Borrowing Expenses (recorded payments only)", "Period start", "Amount paid (PHP)" });
            for (int i = 0; i < r.BorrowingExpenses.Labels.Count; i++)
                row(new object[] { "", r.BorrowingExpenses.Labels[i], Money(r.BorrowingExpenses.Values[i]) });
            row(new object[] { });
            row(new object[] { "TRANSACTION HISTORY", "Item", "Role", "Date", "Estimated Amount (PHP)", "Status", "Transaction" });
            foreach (var transaction in r.Transactions)
                row(new object[] { "", transaction.ItemName, transaction.TransactionType, transaction.Date, Money(transaction.Amount), transaction.Status, transaction.TransactionId });

            return sb.ToString();
        }
    }
}
