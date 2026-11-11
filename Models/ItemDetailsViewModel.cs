using System;
using System.Collections.Generic;

namespace UniShare.Models
{
    public class ItemDetailsViewModel
    {
        public int ItemId { get; set; }
        public string ItemCode { get; set; }            // e.g. QCU-EL-8842
        public string Name { get; set; }                // short name (breadcrumb)
        public string Title { get; set; }               // full page title
        public string Category { get; set; }
        public string CategoryLabel { get; set; }
        public string Subcategory { get; set; }         // e.g. Electronics & Calculators
        public string Condition { get; set; }           // Like New, Excellent, Good, Fair
        public decimal ConditionScore { get; set; }     // out of 10
        public string Campus { get; set; }
        public string Description { get; set; }
        public decimal HourlyRate { get; set; }
        public decimal DailyRate { get; set; }
        public double Rating { get; set; }
        public int ReviewCount { get; set; }
        public bool IsAvailable { get; set; }
        public string ListedText { get; set; }          // e.g. "2 hours ago"
        public string AvailableHours { get; set; }
        public string HandoverZones { get; set; }
        public string ConditionNotes { get; set; }
        public List<string> ConditionTags { get; set; }
        public List<GalleryImageViewModel> Gallery { get; set; }
        public LenderViewModel Lender { get; set; }
        public List<DateTime> BookedDates { get; set; }

        public ItemDetailsViewModel()
        {
            ConditionTags = new List<string>();
            Gallery = new List<GalleryImageViewModel>();
            BookedDates = new List<DateTime>();
        }
    }

    public class GalleryImageViewModel
    {
        public string ImageUrl { get; set; }
        public string Label { get; set; }
        public string Transform { get; set; }          // CSS crop (sample only)
        public string TransformOrigin { get; set; }

        public GalleryImageViewModel()
        {
            Transform = "scale(1)";
            TransformOrigin = "50% 50%";
        }
    }

    public class LenderViewModel
    {
        public string Name { get; set; }
        public string Initials { get; set; }
        public string College { get; set; }
        public string Program { get; set; }             // e.g. "BSCE 3rd Year"
        public string ProgramCode { get; set; }         // e.g. "BSCE"
        public double Rating { get; set; }
        public int ReviewCount { get; set; }
        public string OnTimeRate { get; set; }
        public int CompletedRentals { get; set; }
    }
}
