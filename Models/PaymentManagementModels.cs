using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace UniShare.Models
{
    public class PaymentSubmissionViewModel
    {
        [Required] public string BookingId { get; set; }
        [Required] public string ChargeType { get; set; }
        [Required] public string PaymentMethod { get; set; }
        [StringLength(100)] public string TransactionReference { get; set; }
    }

    public class PaymentActionViewModel
    {
        [Required] public string PaymentId { get; set; }
        [StringLength(400)] public string Reason { get; set; }
    }

    public class LateFeeFinalizationViewModel
    {
        [Required] public string RentalId { get; set; }
        [Range(0, 1000000)] public decimal? FinalAmount { get; set; }
    }

    public class PaymentRecordViewModel
    {
        public string Id { get; set; }
        public string BookingId { get; set; }
        public string ChargeType { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        public string PaymentMethod { get; set; }
        public string TransactionReference { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? OwnerConfirmedAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime? DisputedAt { get; set; }
        public DateTime? RefundedAt { get; set; }
        public string ItemName { get; set; }
        public string OtherPartyName { get; set; }
    }

    public class PaymentDetailsViewModel
    {
        public string BookingId { get; set; }
        public string RentalId { get; set; }
        public string RequestId { get; set; }
        public string ItemName { get; set; }
        public string BookingStatus { get; set; }
        public bool IsOwner { get; set; }
        public bool CanSubmitRentalPayment { get; set; }
        public int DurationHours { get; set; }
        public decimal HourlyRate { get; set; }
        public decimal RentalFee { get; set; }
        public decimal EstimatedLateFee { get; set; }
        public decimal? FinalizedLateFee { get; set; }
        public decimal TotalDue { get; set; }
        public LateFeeDetailsViewModel LateFee { get; set; }
        public List<string> AvailablePaymentMethods { get; set; }
        public List<PaymentRecordViewModel> Payments { get; set; }
    }

    public class LateFeeDetailsViewModel
    {
        public string Id { get; set; }
        public string RentalId { get; set; }
        public string BookingId { get; set; }
        public string Status { get; set; }
        public DateTime DueAt { get; set; }
        public DateTime? ReturnedAt { get; set; }
        public int GracePeriodMinutes { get; set; }
        public decimal HourlyRate { get; set; }
        public int BillableHours { get; set; }
        public decimal EstimatedAmount { get; set; }
        public DateTime EstimatedThrough { get; set; }
        public decimal? FinalizedAmount { get; set; }
        public DateTime? FinalizedAt { get; set; }
    }
}
