using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Web.Mvc;

namespace UniShare.Controllers
{
    public class ReportsAnalyticsController : Controller
    {
        // GET: ReportsAnalytics
        public ActionResult Index()
        {
            ViewBag.Title = "Reports & Analytics";
            ViewBag.TotalVolume = "1,248";
            ViewBag.TotalRevenue = "PHP 38,450.00";
            ViewBag.ReturnRate = "94.6%";
            ViewBag.ActiveDisputes = "7";

            return View("ReportsAnalytics");
        }

        // GET: ReportsAnalytics/GetChartData?timeframe=months|weeks|days
        [HttpGet]
        public JsonResult GetChartData(string timeframe)
        {
            timeframe = (timeframe ?? "months").Trim().ToLowerInvariant();

            string[] timelineLabels;
            int[] checkouts;
            int[] returns;
            decimal[] gcashRevenue;
            decimal[] cashRevenue;

            if (timeframe == "days")
            {
                timelineLabels = new[] { "Mon", "Tue", "Wed", "Thu", "Fri" };
                checkouts = new[] { 18, 26, 22, 34, 29 };
                returns = new[] { 16, 25, 20, 31, 28 };
                gcashRevenue = new[] { 2700m, 3900m, 3300m, 5100m, 4350m };
                cashRevenue = new[] { 900m, 1300m, 1100m, 1700m, 1450m };
            }
            else if (timeframe == "weeks")
            {
                timelineLabels = new[] { "Week 1", "Week 2", "Week 3", "Week 4" };
                checkouts = new[] { 74, 92, 108, 86 };
                returns = new[] { 70, 88, 101, 82 };
                gcashRevenue = new[] { 11200m, 13800m, 16400m, 12900m };
                cashRevenue = new[] { 3600m, 4600m, 5200m, 4300m };
            }
            else // default: "months"
            {
                timeframe = "months";
                timelineLabels = new[] { "Jul", "Aug", "Sep", "Oct", "Nov" };
                checkouts = new[] { 145, 198, 265, 312, 328 };
                returns = new[] { 138, 186, 251, 298, 311 };
                gcashRevenue = new[] { 21500m, 29200m, 39500m, 46800m, 49200m };
                cashRevenue = new[] { 7500m, 10400m, 13500m, 15600m, 16400m };
            }

            // Equipment Categories Performance
            string[] categoryLabels = new[] { "Calculators", "Engineering Kits", "Drafting & Art", "Electronics & Tech", "Lab Apparatus" };
            int[] categoryCounts = new[] { 412, 285, 218, 175, 158 };

            var result = new
            {
                success = true,
                timeframe = timeframe,
                activity = new
                {
                    labels = timelineLabels,
                    checkouts = checkouts,
                    returns = returns
                },
                revenue = new
                {
                    labels = timelineLabels,
                    gcash = gcashRevenue,
                    cash = cashRevenue
                },
                categories = new
                {
                    labels = categoryLabels,
                    data = categoryCounts
                }
            };

            return Json(result, JsonRequestBehavior.AllowGet);
        }
    }
}