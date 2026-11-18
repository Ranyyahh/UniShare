using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace UniShare.Models
{

    public class ReturnInspectionViewModel
    {
        public string BookingId { get; set; }
        public string ReturnId { get; set; }
        public string ItemId { get; set; }

        public string ItemName { get; set; }
        public string ItemCategory { get; set; }

        // Original condition recorded during handover by the owner.
        public string ItemConditionBefore { get; set; }
        public string HandoverConditionNotes { get; set; }
        public List<string> ItemPhotos { get; set; } = new List<string>();
        public List<string> HandoverConditionPhotos { get; set; } = new List<string>();

        // Condition submitted by the borrower after returning the item.
        public string BorrowerConditionAfter { get; set; }
        public string BorrowerConditionNotes { get; set; }
        public List<string> BorrowerConditionPhotos { get; set; } = new List<string>();

        public string ReturnStatus { get; set; }

        public DateTime? ReturnRequestedAt { get; set; }
        public DateTime? InspectionDate { get; set; }

        public bool InspectionRequired { get; set; }
    }

}