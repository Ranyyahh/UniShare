using System;
using System.Collections.Generic;

namespace UniShare.Models
{
    // Model for Views/ReportsAnalytics/Index.cshtml. Only page configuration lives here;
    // every figure is loaded afterwards from ReportsAnalytics/Data.
    public class ReportsAnalyticsViewModel
    {
        public string DefaultFrom { get; set; }          // yyyy-MM-dd
        public string DefaultTo { get; set; }            // yyyy-MM-dd
        public string DataUrl { get; set; }
        public string ExportUrl { get; set; }
        public bool DataSourceAvailable { get; set; }
        public string DataSourceNote { get; set; }       // shown when no transaction store is connected
    }

    // ---- Rows a data source must supply (one row per transaction) ----------------------

    // A rental of an item the report user OWNS (the user is the lender).
    public class RentalTransactionRecord
    {
        public string TransactionId { get; set; }
        public string ItemId { get; set; }
        public string ItemName { get; set; }
        public string OwnerKey { get; set; }             // must equal the signed-in user
        public int Quantity { get; set; }                // units rented in this transaction
        public decimal Amount { get; set; }              // rental fee charged
        public string Status { get; set; }               // MyRentals vocabulary, see ReportsAnalyticsController
        public DateTime RequestedAt { get; set; }        // when the transaction was created
        public DateTime? CompletedAt { get; set; }       // when it reached Completed (null otherwise)
    }

    // A rental of someone else's item where the report user is the BORROWER.
    public class BorrowingTransactionRecord
    {
        public string TransactionId { get; set; }
        public string ItemId { get; set; }
        public string ItemName { get; set; }
        public string BorrowerKey { get; set; }          // must equal the signed-in user
        public int Quantity { get; set; }
        public string Status { get; set; }
        public DateTime RequestedAt { get; set; }
        public decimal? AmountPaid { get; set; }         // null = no payment recorded
        public DateTime? PaidAt { get; set; }            // null = not paid
    }

    // ---- JSON payload returned to the page and used by the CSV export ------------------

    public class ReportCards
    {
        public decimal TotalRevenue { get; set; }
        public int TotalRentals { get; set; }
        public int ItemsRented { get; set; }
        public int TotalBorrowings { get; set; }
    }

    // One borrowing transaction behind a chart point (shown in the chart tooltips).
    public class TransactionDetail
    {
        public string TransactionId { get; set; }
        public string ItemName { get; set; }
        public int Quantity { get; set; }
        public string Status { get; set; }
        public decimal? Amount { get; set; }             // amount paid, when a payment is recorded
        public string Date { get; set; }                 // yyyy-MM-dd: requested date (activity) or paid date (expenses)
    }

    public class TimeSeries
    {
        public List<string> Labels { get; set; }         // bucket start dates, yyyy-MM-dd
        public List<decimal> Values { get; set; }
        public List<List<TransactionDetail>> Details { get; set; }   // optional: transactions per bucket, same order as Labels
        public TimeSeries() { Labels = new List<string>(); Values = new List<decimal>(); Details = new List<List<TransactionDetail>>(); }
    }

    public class TopItem
    {
        public string Name { get; set; }
        public int Quantity { get; set; }
    }

    public class StatusDistribution
    {
        public int Active { get; set; }
        public int Completed { get; set; }
        public int Disputed { get; set; }
        public int NotCounted { get; set; }              // Pending / Accepted / Declined / Returned / unknown
    }

    public class ReportPayload
    {
        public string From { get; set; }
        public string To { get; set; }
        public string Granularity { get; set; }          // day | week | month
        public string GeneratedAt { get; set; }
        public bool IsSampleData { get; set; }           // true when the figures come from the preview data source
        public ReportCards Cards { get; set; }
        public TimeSeries RentalRevenue { get; set; }
        public List<TopItem> MostRentedItems { get; set; }
        public StatusDistribution RentalStatus { get; set; }
        public TimeSeries BorrowingActivity { get; set; }
        public TimeSeries BorrowingExpenses { get; set; }
        public int BorrowingsWithoutPayment { get; set; } // counted borrowings with no payment record
        public int BorrowingsNotCounted { get; set; }     // borrowings still Pending / Declined / etc.

        public ReportPayload()
        {
            Cards = new ReportCards();
            RentalRevenue = new TimeSeries();
            MostRentedItems = new List<TopItem>();
            RentalStatus = new StatusDistribution();
            BorrowingActivity = new TimeSeries();
            BorrowingExpenses = new TimeSeries();
        }
    }
}
