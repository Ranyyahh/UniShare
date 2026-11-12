using System;
using System.Collections.Generic;

namespace UniShare.Models
{
    public static class ItemCategoryCatalog
    {
        public static readonly string[] Names = {
            "Electronics & Gadgets", "Books & Learning Materials", "School Supplies",
            "Clothing & Uniforms", "Sports & Recreation", "Others"
        };

        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Others";
            var name = value.Trim();
            if (name.IndexOf("elect", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("camera", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("accessor", StringComparison.OrdinalIgnoreCase) >= 0) return Names[0];
            if (name.IndexOf("book", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("reference", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("learning", StringComparison.OrdinalIgnoreCase) >= 0) return Names[1];
            if (name.IndexOf("school", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("draft", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("supply", StringComparison.OrdinalIgnoreCase) >= 0) return Names[2];
            if (name.IndexOf("cloth", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("uniform", StringComparison.OrdinalIgnoreCase) >= 0) return Names[3];
            if (name.IndexOf("sport", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("recreation", StringComparison.OrdinalIgnoreCase) >= 0) return Names[4];
            if (Array.IndexOf(Names, name) >= 0) return name;
            return Names[5];
        }
    }
}
