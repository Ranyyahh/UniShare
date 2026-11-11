using System.Collections.Generic;

namespace UniShare.Models
{
    // Same fields as the Create form, plus the data the Edit page needs.
    public class EditListingViewModel : CreateListingViewModel
    {
        public string Id { get; set; }

        // Photos that already exist on the listing (the first one is the cover).
        public List<ListingPhotoViewModel> ExistingPhotos { get; set; } = new List<ListingPhotoViewModel>();

        // Posted back: ids of existing photos the student kept. New uploads come in through "Photos".
        public List<int> KeptPhotoIds { get; set; } = new List<int>();

        // ----- Read-only display data (not posted) -----
        public ListingStatus Status { get; set; }
        public bool HasActiveRental => Status == ListingStatus.Rented;
        public string CoverImageUrl { get; set; }
        public string PriceSummary { get; set; }              // e.g. "₱25 / 4 hrs (₱50 whole day)", used by the delete dialog
    }

    public class ListingPhotoViewModel
    {
        public int Id { get; set; }
        public string Url { get; set; }
        public string Label { get; set; }    // small tag on non-cover photos, e.g. "LCD Screen"
        public string Caption { get; set; }  // text along the bottom, e.g. "Natural Textbook Display"
    }
}
