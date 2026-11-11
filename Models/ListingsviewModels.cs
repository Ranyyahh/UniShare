using System.Collections.Generic;
using System.Linq;

namespace UniShare.Models
{
    public enum ListingStatus
    {
        Available,
        Rented,
        Paused
    }

    public class ListingsPageViewModel
    {
        public List<ListingItemViewModel>
    Listings
        { get; set; } = new List<ListingItemViewModel>
        ();

        public bool HasListings => Listings.Any();
        public int TotalCount => Listings.Count;
        public int AvailableCount => Listings.Count(l => l.Status == ListingStatus.Available);
        public int UnavailableCount => TotalCount - AvailableCount;
        public int PendingRequestCount { get; set; }
        public int ActiveRentalCount { get; set; }
    }

    public class ListingItemViewModel
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string CategoryLabel { get; set; }   // small text above the title, e.g. "Electronics & Gadgets"
        public string CategoryBadge { get; set; }   // badge on the image, e.g. "Electronics"
        public string ImageUrl { get; set; }
        public string Condition { get; set; }       // e.g. "Like New"
        public decimal Price { get; set; }
        public int PerHours { get; set; } = 4;
        public string PriceNote { get; set; }       // e.g. "₱50 whole day" or "Fixed flat rate"
        public string Location { get; set; }
        public ListingStatus Status { get; set; }
        // New fields to store handover schedule
        public string EarliestHandoverTime { get; set; } = "08:00";
        public string LatestReturnTime { get; set; } = "17:30";
        public int CategoryId { get; set; }
        public string Description { get; set; }
        public bool EnableFullDayBundle { get; set; }
        public List<string> AvailableDays { get; set; } = new List<string>();
        public decimal ReplacementValue { get; set; }
        public decimal SecurityDeposit { get; set; }
        public string Accessories { get; set; }

        public bool IsAvailable => Status == ListingStatus.Available;
    }

}
