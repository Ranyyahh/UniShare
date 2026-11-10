using System;
using System.Collections.Generic;

namespace UniShare.Models
{
    public class ReportsViewModel
    {
        public ReportsOverviewViewModel Overview { get; set; } = new ReportsOverviewViewModel();
        public ReportsChartDataViewModel ChartData { get; set; } = new ReportsChartDataViewModel();
    }

    public class ReportsOverviewViewModel
    {
        public string TotalPlatformRevenue { get; set; }
        public string TotalRentals { get; set; }
        public string TotalUsers { get; set; }
        public string TotalListedItems { get; set; }
    }

    public class ReportsChartDataViewModel
    {
        public LineChartData RentalTransactionsOverTime { get; set; } = new LineChartData();
        public DonutChartData ItemListingStatus { get; set; } = new DonutChartData();
        public BarChartData MostRentedItems { get; set; } = new BarChartData();
        public GroupedBarChartData UserActivityOverview { get; set; } = new GroupedBarChartData();
    }

    public class LineChartData
    {
        public List<string> Labels { get; set; } = new List<string>();
        public List<int> Transactions { get; set; } = new List<int>();
    }

    public class DonutChartData
    {
        public List<string> Labels { get; set; } = new List<string>();
        public List<int> Counts { get; set; } = new List<int>();
    }

    public class BarChartData
    {
        public List<string> Labels { get; set; } = new List<string>();
        public List<int> Counts { get; set; } = new List<int>();
    }

    public class GroupedBarChartData
    {
        public List<string> Labels { get; set; } = new List<string>();
        public List<int> ActiveUsers { get; set; } = new List<int>();
        public List<int> RentingTransactions { get; set; } = new List<int>();
        public List<int> BorrowingTransactions { get; set; } = new List<int>();
    }
}
