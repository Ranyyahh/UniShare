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

        [Range(typeof(decimal), "0", "10000000", ErrorMessage = "Replacement value cannot be negative.")]
        [Display(Name = "Declared Replacement Value")]
        public decimal? ReplacementValue { get; set; }

        [Range(typeof(decimal), "0", "10000000", ErrorMessage = "Security deposit cannot be negative.")]
        [Display(Name = "Security Deposit")]
        public decimal? SecurityDeposit { get; set; }

        [StringLength(500)]
        [Display(Name = "Included Accessories")]
        public string Accessories { get; set; }

        // Up to 4 images. The first one is the cover photo.
        public IEnumerable<HttpPostedFileBase> Photos { get; set; }

        // ----- Section 2: Rental details & handover -----
        [Required(ErrorMessage = "Enter a rental rate.")]
        [Range(typeof(decimal), "1", "10000", ErrorMessage = "Rate must be between 1 and 10,000.")]
        public decimal? Price { get; set; }

        public int PricingUnitHours { get; set; } = 4;
        public bool EnableFullDayBundle { get; set; }

        public List<string> AvailableDays { get; set; } = new List<string>();

        [Required, RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "Use HH:mm (24-hour).")]
        public string EarliestHandoverTime { get; set; }

        [Required, RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "Use HH:mm (24-hour).")]
        public string LatestReturnTime { get; set; }

        [Required, RegularExpression("^(San Bartolome|San Francisco|Batasan)$", ErrorMessage = "Choose a valid campus.")] public string Campus { get; set; }
        [StringLength(120)] public string Building { get; set; }
        [StringLength(80)] public string Room { get; set; }
        [StringLength(160)] public string HandoverLocation { get; set; }

        // ----- Dropdown / option data (filled by the controller, not posted) -----
        public List<CategoryOptionViewModel> Categories { get; set; } = new List<CategoryOptionViewModel>();
    }

    public class CategoryOptionViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Group { get; set; }   // shown as "Categorized under {Group}"
    }

}
