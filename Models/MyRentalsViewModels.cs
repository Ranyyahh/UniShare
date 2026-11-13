using System;
using System.Collections.Generic;

namespace UniShare.Models
{
    /// <summary>Summary values shown above the rentals list.</summary>
    public class RentalSummaryDto
    {
        public int TotalRentals { get; set; }
        public int PendingRequests { get; set; }
        public int ActiveRentals { get; set; }
        public int NeedAttention { get; set; }
        public int OverdueCount { get; set; }
        public int DisputedCount { get; set; }
    }

    /// <summary>Rental fields shared by My Rentals, Rental Requests, and My Listings.</summary>
    public class RentalRecordDto
    {
        public string BookingObjectId { get; set; }
        public string RequestId { get; set; }
        public string ItemName { get; set; }
        public string BorrowerName { get; set; }
        public string BorrowerInitials { get; set; }
        public string BorrowerProgram { get; set; }
        public DateTime RequestDate { get; set; }
        public string RequestDateFormatted { get; set; }
        public DateTime HandoverDate { get; set; }
        public string HandoverScheduleFormatted { get; set; }
        public string Duration { get; set; }
        public decimal RentalFee { get; set; }
        public string RentalFeeFormatted { get; set; }
        public string Status { get; set; }
        public bool CanManageRequest { get; set; }
        public string Category { get; set; }
        public string BackAction { get; set; }
        public string BackController { get; set; }
        public string OwnerName { get; set; }
        public string OwnerProgram { get; set; }
        public string ItemLocation { get; set; }
        public string HandoverLocation { get; set; }
        public string ItemImageUrl { get; set; }
        public string RequestNote { get; set; }
        public double DurationHours { get; set; }
        public decimal HourlyRate { get; set; }
        public string HourlyRateFormatted { get; set; }
        public string HandoverDateFormatted { get; set; }
        public string HandoverTimeFormatted { get; set; }
        public DateTime ExpectedReturnDate { get; set; }
        public string ExpectedReturnFormatted { get; set; }
        public int GracePeriodMinutes { get; set; }
        public bool OwnerHandoverConfirmed { get; set; }
        public string OwnerHandoverConfirmedAt { get; set; }
        public bool BorrowerHandoverConfirmed { get; set; }
        public string BorrowerHandoverConfirmedAt { get; set; }
        public string DeclineReason { get; set; }
        public string DeclineNote { get; set; }
        public string DeclinedAtFormatted { get; set; }
        public string AcceptedAtFormatted { get; set; }
        public string DisputeCategory { get; set; }
        public string DisputeDescription { get; set; }
        public string DisputeSubmittedAtFormatted { get; set; }
        public List<string> DisputeEvidenceUrls { get; set; }
        public string ReturnedAtFormatted { get; set; }
        public string CompletedAtFormatted { get; set; }
        public string PaymentMethod { get; set; }
        public string PaymentStatus { get; set; }
        public string PaymentReference { get; set; }
        public decimal PaymentAmount { get; set; }
        public decimal EstimatedLateFee { get; set; }
        public decimal? FinalLateFee { get; set; }
        public string LateFeeStatus { get; set; }
        public bool CanChoosePayment { get; set; }
        public bool CanConfirmPayment { get; set; }
        public bool CanFinalizeLateFee { get; set; }
    }

    /// <summary>Strongly typed page model for Views/MyRentals/Index.cshtml.</summary>
    public class MyRentalsViewModel
    {
        public RentalSummaryDto Summary { get; set; }
        public List<RentalRecordDto> Rentals { get; set; }
        public List<RentalRecordDto> IncomingRequests { get; set; }

        public MyRentalsViewModel()
        {
            Summary = new RentalSummaryDto();
            Rentals = new List<RentalRecordDto>();
            IncomingRequests = new List<RentalRecordDto>();
        }
    }
}
