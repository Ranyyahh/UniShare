using System;

namespace UniShare.Models
{
    public class TransactionViewModel
    {
        public string TransactionId { get; set; }
        public string BookingId { get; set; }
        public string DateLogged { get; set; }
        public string DateRange { get; set; }
        public string ItemName { get; set; }
        public string Serial { get; set; }
        public string BorrowerName { get; set; }
        public string BorrowerId { get; set; }
        public string BorrowerCourse { get; set; }
        public string OwnerName { get; set; }
        public string OwnerId { get; set; }
        public string OwnerCourse { get; set; }
        public string OwnerRating { get; set; }
        public string BaseFee { get; set; }
        public string LateFee { get; set; }
        public string TotalAmount { get; set; }
        public string GCashRef { get; set; }
        public string Method { get; set; }
        public string Status { get; set; }
    }
}
