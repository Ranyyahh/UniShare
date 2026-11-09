using System.Collections.Generic;

namespace UniShare.Models
{
    public class DashboardViewModel
    {
        public string UserName { get; set; }
        public List<MetricViewModel> Metrics { get; set; } = new List<MetricViewModel>();
        public List<CategoryViewModel> Categories { get; set; } = new List<CategoryViewModel>();
        public List<RentalItemViewModel> FeaturedItems { get; set; } = new List<RentalItemViewModel>();
        public List<RentalItemViewModel> RecentItems { get; set; } = new List<RentalItemViewModel>();
    }

    public class MetricViewModel
    {
        public string Title { get; set; }
        public int Value { get; set; }
        public string IconName { get; set; }        // Lucide icon name
        public string TintClasses { get; set; }     // e.g. "bg-blue-50 text-blue-600"
        public string LinkText { get; set; }
        public string LinkUrl { get; set; } = "#";
        public string Footnote { get; set; }        // optional; replaces link when set
    }

    public class CategoryViewModel
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string IconName { get; set; }
        public string TintClasses { get; set; }
        public int AvailableCount { get; set; }
        public string Url { get; set; } = "#";
    }

    public class RentalItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Subtitle { get; set; }
        public string Condition { get; set; }
        public string ConditionBadgeClasses { get; set; } = "bg-slate-100 text-slate-700";
        public decimal PricePerHour { get; set; }
        public string ImageUrl { get; set; }
        public string OwnerName { get; set; }
        public string OwnerCourse { get; set; }
    }
}
