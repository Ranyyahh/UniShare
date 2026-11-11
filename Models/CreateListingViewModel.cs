using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web;

namespace UniShare.Models
{
    public enum ConditionGrade
    {
        LikeNew,
        Good,
        Fair,
        HeavyUse
    }

    public class CreateListingViewModel
    {
        // ----- Section 1: Item information -----
        [Required(ErrorMessage = "Enter the item name.")]
        [StringLength(120)]
        [Display(Name = "Item Name")]
        public string ItemName { get; set; }

        [Required(ErrorMessage = "Choose a category.")]
        public int? CategoryId { get; set; }

        [Required(ErrorMessage = "Select the item's condition.")]
        public ConditionGrade? Condition { get; set; }

        [Required(ErrorMessage = "Add a description.")]
        [StringLength(500, ErrorMessage = "Description can be at most 500 characters.")]
        public string Description { get; set; }

        // Up to 4 images. The first one is the cover photo.
        public IEnumerable<HttpPostedFileBase> Photos { get; set; }

        // ----- Section 2: Rental details & handover -----
        [Required(ErrorMessage = "Enter a rental rate.")]
        [Range(typeof(decimal), "1", "10000", ErrorMessage = "Rate must be between 1 and 10,000.")]
        public decimal? Price { get; set; }

        public int PricingUnitHours { get; set; } = 4;
        public bool EnableFullDayBundle { get; set; } = true;

        public List<string> AvailableDays { get; set; } = new List<string>();

        [Required, RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "Use HH:mm (24-hour).")]
        public string EarliestHandoverTime { get; set; } = "08:00";

        [Required, RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "Use HH:mm (24-hour).")]
        public string LatestReturnTime { get; set; } = "17:30";

        // Checkpoint is optional for Create Listing
        public string Checkpoint { get; set; }

        // ----- Dropdown / option data (filled by the controller, not posted) -----
        public List<CategoryOptionViewModel> Categories { get; set; } = new List<CategoryOptionViewModel>();
        public List<CheckpointOptionViewModel> Checkpoints { get; set; } = new List<CheckpointOptionViewModel>();
    }

    public class CategoryOptionViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Group { get; set; }   // shown as "Categorized under {Group}"
    }

    public class CheckpointOptionViewModel
    {
        public string Value { get; set; }
        public string Name { get; set; }
        public string Detail { get; set; }
        public string IconName { get; set; } // Lucide icon name
        public string Feature { get; set; }
    }
}
