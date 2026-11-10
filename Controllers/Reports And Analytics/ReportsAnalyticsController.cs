using System;
using System.Collections.Generic;
using System.Web.Mvc;
using UniShare.Models;

namespace UniShare.Controllers
{
    public class ReportsAnalyticsController : Controller
    {
        // GET: ReportsAnalytics
        public ActionResult Index()
        {
            ViewBag.Title = "Reports & Analytics";
            return View("ReportsAnalytics");
        }

        public ActionResult ReportAnalytics()
        {
            return Index();
        }

        // GET: ReportsAnalytics/Data?from=yyyy-MM-dd&to=yyyy-MM-dd
        // Exact JSON endpoint required by Alexis's expected structure
        [HttpGet]
        public ActionResult Data(string from, string to)
        {
            // Return dummy data matching Alexis's expected structure
            var data = new
            {
                from = string.IsNullOrEmpty(from) ? DateTime.Today.AddDays(-30).ToString("yyyy-MM-dd") : from,
                to = string.IsNullOrEmpty(to) ? DateTime.Today.ToString("yyyy-MM-dd") : to,
                granularity = "day",
                cards = new { totalRevenue = 148250.00, totalRentals = 1248, itemsRented = 3890, totalBorrowings = 642 },
                rentalRevenue = new { labels = new[] { "2026-11-01", "2026-11-02", "2026-11-03" }, values = new[] { 100, 200, 150 } },
                mostRentedItems = new[] { new { name = "Casio fx-991EX", quantity = 150 } },
                rentalStatus = new { active = 10, completed = 50, disputed = 5, notCounted = 0 },
                borrowingActivity = new { labels = new[] { "2026-11-01", "2026-11-02" }, values = new[] { 1, 2 }, details = new object[] { } },
                borrowingExpenses = new { labels = new[] { "2026-11-01", "2026-11-02" }, values = new[] { 100, 50 }, details = new object[] { } },
                borrowingsWithoutPayment = 0,
                borrowingsNotCounted = 0
            };
            return Json(data, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult Export(string from, string to)
        {
            return Content("Date,Metric,Value\n2026-11-01,Revenue,100\n", "text/csv");
        }

        // Backward compatibility for old chart calls if needed
        [HttpGet]
        public JsonResult GetChartData(string timeframe)
        {
            return Json(new { success = true }, JsonRequestBehavior.AllowGet);
        }
    }
}
