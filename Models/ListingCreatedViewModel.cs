namespace UniShare.Models
{
    public class ListingCreatedViewModel
    {
        public int Id { get; set; }
        public string ListingCode { get; set; }       // public ID, e.g. "QCU-EL-8842"
        public string ItemName { get; set; }
        public string CategoryBadge { get; set; }     // e.g. "Electronics & Gadgets"
        public string Campus { get; set; } = "San Bartolome"; // e.g. "San Bartolome"
        public string Condition { get; set; }         // e.g. "Like New"
        public string ImageUrl { get; set; }
        public decimal Price { get; set; }
        public int PerHours { get; set; } = 4;
        public decimal? WholeDayPrice { get; set; }   // null when the full-day bundle is off
        public string AvailabilityText { get; set; }  // e.g. "Available Weekdays 8:00 AM - 5:30 PM"
        public string Location { get; set; }          // e.g. "CEA Building Lobby (San Bartolome)"
    }
}
