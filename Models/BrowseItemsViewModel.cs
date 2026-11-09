using System.Collections.Generic;

namespace UniShare.Models
{
    public class BrowseItemsViewModel
    {
        public string SearchTerm { get; set; }

        public string SelectedCategory { get; set; }

        public string SelectedCondition { get; set; }

        public string SelectedCampus { get; set; }

        public string SortBy { get; set; }

        public List<string> Categories { get; set; }

        public List<string> Conditions { get; set; }

        public List<string> Campuses { get; set; }

        public List<ItemViewModel> Items { get; set; }

        public int CurrentPage { get; set; }

        public int TotalPages { get; set; }

        public BrowseItemsViewModel()
        {
            Categories = new List<string>();
            Conditions = new List<string>();
            Campuses = new List<string>();
            Items = new List<ItemViewModel>();

            CurrentPage = 1;
            TotalPages = 1;
        }
    }

    public class ItemViewModel
    {
        public int ItemId { get; set; }

        public string ItemName { get; set; }

        public string Description { get; set; }

        public decimal Price { get; set; }

        public string Category { get; set; }

        public string Condition { get; set; }

        public string Campus { get; set; }

        public string ImageUrl { get; set; }

        public string Availability { get; set; }

        public string OwnerName { get; set; }
    }
}