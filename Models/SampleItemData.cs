using System;
using System.Collections.Generic;
using System.Linq;

namespace UniShare.Models
{
    /// <summary>
    /// Sample data for the Item Details page. Replace Get() with a database query when ready.
    /// </summary>
    public static class SampleItemData
    {
        private const string Img1 = "https://lh3.googleusercontent.com/aida-public/AB6AXuD43NDeaCI8F4hBx3FLTNk0uwj5y5SboznXM6ER4GsM08TZZDMYIrzZaRAGaSg9FeuvvyaDtKs_TRTmbeoITdxLzKYsJrrGA2RWPzHjD2XQAo965xacYbseOWJFHVDdOrviIMi_331wbAh0tQZCnImV3TbrDl7HiZ4iygl9A8kRdoFJ3jYFMSBY35a-hI4eEmymArheje_VRBYvB-GH0P1HRypI9Q79sU7vurcfl8U";
        private const string Img2 = "https://lh3.googleusercontent.com/aida-public/AB6AXuAeqNCyhCudAPDf6GioorIUM7OJRABvY4OPE7o-AtOSGLSKVTz4eCnYFwLQx447-bdZWiSeoOjyM2qrHU2AAjiH9FGz3J55IlDjOH_D4LNkG35uf6pA6qHlNHtDj1XdJsO2__ZsCtMeafbDSmBklGqyCDvbZDjbyKdLDtwoWM7Hf6eaXsGMSQLpmxhvndzMj0tgm5bXu-4BFpK95tlnl_d8gwGP4o3Q9pNuEtF8LVo";
        private const string Img3 = "https://lh3.googleusercontent.com/aida-public/AB6AXuA14k44VRTZh1TLEpfoOTL9DDpZqp3lScIsxbMbpQHBR7btU6kSZsMmwzq0n6gHS-pSCHydbiDC8BlRgErvoxGtWvpQWiHlU2ehmU8e2ec6Qp5hAq6mXl3hvmKSw5PWUb8ft1XMj_rXgGBMC9onN-zzBYeAZVIZDQ0N95KU4f_XPIqS5xFkQCQfTNz3pqHoeWjhr4xsfMh2wTnFNxkezqckLoEjGANJx1rP47hW__w";
        private const string Img4 = "https://lh3.googleusercontent.com/aida-public/AB6AXuB8kRvGBOF0Ly9h7ENtF1PtW1f9TD-pk0kmcCnP5TU2dvphy8FjuvQSf8JftokhIKIByLa6GCmjmz0Lw37EUyzYvjCp6jo_X-XkpVeG0OFHS97sRELoSkSlJLtT93Gd8XfO-Jtykx_fGDbslIpnJ_bQFRM_e_MuNaf8XbnMK7QGXtOcrcFXWpCEG2AFHtB5QQcoFGseq9Uv0M5kbgSUS03SGc_rpggMWQWo4Z9adXA";
        private const string Img5 = "https://lh3.googleusercontent.com/aida-public/AB6AXuBJu54DB8y0iKBOHnb08fQjPnxuvssuqm-yU_h686n07NJgbhp7Z1QyK1Th2Y_-mLUX95z2lgw3JG4p4aJVSQksakOYM-tGaKqOhK4kwwEsrh7_mKtvv0lfFKSk7gpsQvyZRIhCPcxzV36qnr1B0iJxCpa34UWRyHcE-l5L2q6KtSQaJO0pVNxEhHtnqTupgAc4lPrl_-jmKemoAG_FZ5WBtW53ZCRb-TBZsqbv4Q0";
        private const string Img6 = "https://lh3.googleusercontent.com/aida-public/AB6AXuBASRK8_HeIEBdBkPhK9e9usdsEYGcL3Ff6ZAJ_i-Ks9Yd7l3zd0fim7TV7PEze0EbivXH8EwdnIEBuJmdcJnjY6qNeA8KsuUZbSaK4TkIwTqsX_bII7VHPPkw_qYoPqCrfT1UQ568tFL_ultk_feehMD9tw-xFsYFYtOUs6fsXPs6Li3mEdyBPoS5UD0DTb9mpauycIyDIMbfVWe_qfSurFS4fUpgY9RIG-lv4AVY";
        private const string Img7 = "https://lh3.googleusercontent.com/aida-public/AB6AXuA9WdwOWagw7kH2v-OmWqDqxnUfADFR5p8GfOpPI1FFZ2LTiu3mfxHMb9TlTvcq7xppAN9ixvnWbJ_r9dJLdw65I2XS0UA6z2oyHZxg5Ns5rmGBywtSyXabaUE2x6LeiqGYE49FiybG6D289iNcEPXvAp7O7E8zDvYJs7BowDg5sFRYr17pQ6wYBkYQxtD6hXdqQLLmzyLTVFl4ONN9IDOObCLGtNko-y1aja16pcI";
        private const string Img8 = "https://lh3.googleusercontent.com/aida-public/AB6AXuDJ6LsA9K4An3KGvF56ga1WHFDdSQQlnx94Q8Snfl1yd_PZWnS1HuZibAq01seL1mu_DlDvAy9t2CschJF7iNagN4zLBpmi3-zDnsXp96NRuxdX5J91XiF3SzUb3CadOS4eH_6mIIjLjTBr-WHOvSpHSgekmSeqYJmVmrJCqzGbKs34VDLy-zabxENKjEoIeMrn1clujsUJ0VfPPUxI7aatkvBc7ObEcpMDr7w2g8Y";

        public static ItemDetailsViewModel Get(int id)
        {
            var item = BuildAll().FirstOrDefault(i => i.ItemId == id);
            if (item != null) item.BookedDates = BuildBookedDates(item.ItemId);
            return item;
        }

        // Sample booked dates (weekends mostly booked). Replace with real reservations from your DB.
        private static List<DateTime> BuildBookedDates(int itemId)
        {
            var list = new List<DateTime>();
            for (int n = 0; n < 150; n++)
            {
                var d = DateTime.Today.AddDays(n);
                int h = (itemId * 7 + d.Day * 13 + (d.Month - 1) * 5) % 11;
                bool weekend = d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday;
                if (weekend ? h < 8 : h == 3) list.Add(d);
            }
            return list;
        }

        private static List<ItemDetailsViewModel> BuildAll()
        {
            return new List<ItemDetailsViewModel>
            {
            new ItemDetailsViewModel
            {
                ItemId = 1, ItemCode = "QCU-EL-8842", Name = "Casio fx-991EX ClassWiz Calculator",
                Title = "Casio fx-991EX Non-Programmable Scientific Calculator",
                Category = "Electronics", CategoryLabel = "Electronics", Subcategory = "Electronics & Calculators",
                Condition = "Like New", ConditionScore = 9.8m, Campus = "San Bartolome",
                Description = "Essential 552-function non-programmable scientific calculator with high-resolution Natural Textbook Display. Allowed for all QCU College of Engineering (CEA) and Computer Studies (CICS) board exam coursework, calculus, differential equations, and physics exams.",
                HourlyRate = 25m, DailyRate = 50m, Rating = 4.9, ReviewCount = 18,
                IsAvailable = true, ListedText = "2 hours ago", AvailableHours = "8:00 AM - 5:30 PM",
                HandoverZones = "Main Library Circulation Desk or CEA Student Lobby",
                ConditionNotes = "Pristine Natural Textbook Display with zero scratches, ghost lines, or dead pixels. All 552 function keys are crisp, fully readable, and responsive under rapid keystrokes.",
                ConditionTags = new List<string> { "Fresh LR44 Battery", "Solar Cell Functional" },
                Gallery = new List<GalleryImageViewModel>
                {
                    new GalleryImageViewModel { ImageUrl = Img1, Label = "Front", Transform = "scale(1)", TransformOrigin = "50% 50%" },
                    new GalleryImageViewModel { ImageUrl = Img1, Label = "LCD Screen", Transform = "scale(1.8)", TransformOrigin = "30% 35%" },
                    new GalleryImageViewModel { ImageUrl = Img1, Label = "Hard Case", Transform = "scale(1.5)", TransformOrigin = "85% 85%" }
                },
                Lender = new LenderViewModel
                {
                    Name = "Maria Santos", Initials = "MS", College = "College of Engineering (CEA)", Program = "BSCE 3rd Year", ProgramCode = "BSCE",
                    Rating = 4.9, ReviewCount = 38, OnTimeRate = "100%", CompletedRentals = 42
                }
            },
            new ItemDetailsViewModel
            {
                ItemId = 2, ItemCode = "QCU-EL-7310", Name = "Logitech M590 Silent Mouse",
                Title = "Logitech M590 Silent Mouse",
                Category = "Electronics", CategoryLabel = "Electronics", Subcategory = "Electronics & Peripherals",
                Condition = "Excellent", ConditionScore = 9.2m, Campus = "Quirino",
                Description = "Quiet-click wireless mouse with Bluetooth and USB receiver. Great for long laptop sessions in the library.",
                HourlyRate = 15m, DailyRate = 35m, Rating = 4.8, ReviewCount = 14,
                IsAvailable = true, ListedText = "5 hours ago", AvailableHours = "8:00 AM - 5:30 PM",
                HandoverZones = "Quirino Guard House or Student Lounge",
                ConditionNotes = "Smooth scroll wheel and quiet clicks. No sticky buttons, light cosmetic wear only.",
                ConditionTags = new List<string> { "Battery Included", "USB Receiver Included" },
                Gallery = new List<GalleryImageViewModel>
                {
                    new GalleryImageViewModel { ImageUrl = Img2, Label = "Front", Transform = "scale(1)", TransformOrigin = "50% 50%" },
                    new GalleryImageViewModel { ImageUrl = Img2, Label = "Top View", Transform = "scale(1.8)", TransformOrigin = "30% 35%" },
                    new GalleryImageViewModel { ImageUrl = Img2, Label = "Receiver", Transform = "scale(1.5)", TransformOrigin = "85% 85%" }
                },
                Lender = new LenderViewModel
                {
                    Name = "Juan Dela Cruz", Initials = "JD", College = "College of Computer Studies (CICS)", Program = "BSIT 2nd Year", ProgramCode = "BSIT",
                    Rating = 4.8, ReviewCount = 27, OnTimeRate = "98%", CompletedRentals = 31
                }
            },
            new ItemDetailsViewModel
            {
                ItemId = 3, ItemCode = "QCU-EL-6125", Name = "Anker 7-in-1 USB-C Hub",
                Title = "Anker 7-in-1 USB-C Hub",
                Category = "Electronics", CategoryLabel = "Electronics", Subcategory = "Electronics & Accessories",
                Condition = "Like New", ConditionScore = 9.7m, Campus = "San Bartolome",
                Description = "HDMI, USB-A, SD/microSD and USB-C power passthrough. Handy for presentations and project demos.",
                HourlyRate = 20m, DailyRate = 45m, Rating = 4.9, ReviewCount = 21,
                IsAvailable = true, ListedText = "1 day ago", AvailableHours = "8:00 AM - 5:30 PM",
                HandoverZones = "Main Library Circulation Desk or CEA Student Lobby",
                ConditionNotes = "All ports tested: HDMI outputs 4K, USB-A and SD readers work without dropouts.",
                ConditionTags = new List<string> { "HDMI 4K Tested", "Original Cable" },
                Gallery = new List<GalleryImageViewModel>
                {
                    new GalleryImageViewModel { ImageUrl = Img3, Label = "Front", Transform = "scale(1)", TransformOrigin = "50% 50%" },
                    new GalleryImageViewModel { ImageUrl = Img3, Label = "Ports", Transform = "scale(1.8)", TransformOrigin = "30% 35%" },
                    new GalleryImageViewModel { ImageUrl = Img3, Label = "Pouch", Transform = "scale(1.5)", TransformOrigin = "85% 85%" }
                },
                Lender = new LenderViewModel
                {
                    Name = "Angela Reyes", Initials = "AR", College = "College of Engineering (CEA)", Program = "BSCpE 4th Year", ProgramCode = "BSCpE",
                    Rating = 4.9, ReviewCount = 45, OnTimeRate = "100%", CompletedRentals = 58
                }
            },
            new ItemDetailsViewModel
            {
                ItemId = 4, ItemCode = "QCU-EL-5598", Name = "SanDisk Extreme 128GB SD Card",
                Title = "SanDisk Extreme 128GB SD Card",
                Category = "Electronics", CategoryLabel = "Electronics", Subcategory = "Electronics & Storage",
                Condition = "Excellent", ConditionScore = 9.1m, Campus = "Quirino",
                Description = "Fast UHS-I card for DSLR and mirrorless cameras. Formatted and ready to shoot.",
                HourlyRate = 15m, DailyRate = 30m, Rating = 5, ReviewCount = 11,
                IsAvailable = true, ListedText = "1 day ago", AvailableHours = "8:00 AM - 5:30 PM",
                HandoverZones = "Quirino Guard House or Student Lounge",
                ConditionNotes = "Freshly formatted with no bad sectors. Read/write speeds verified before every rental.",
                ConditionTags = new List<string> { "Formatted & Ready", "Case Included" },
                Gallery = new List<GalleryImageViewModel>
                {
                    new GalleryImageViewModel { ImageUrl = Img4, Label = "Front", Transform = "scale(1)", TransformOrigin = "50% 50%" },
                    new GalleryImageViewModel { ImageUrl = Img4, Label = "Label", Transform = "scale(1.8)", TransformOrigin = "30% 35%" },
                    new GalleryImageViewModel { ImageUrl = Img4, Label = "Case", Transform = "scale(1.5)", TransformOrigin = "85% 85%" }
                },
                Lender = new LenderViewModel
                {
                    Name = "Paolo Garcia", Initials = "PG", College = "College of Arts & Sciences", Program = "BA Comm 3rd Year", ProgramCode = "BA",
                    Rating = 5, ReviewCount = 19, OnTimeRate = "100%", CompletedRentals = 24
                }
            },
            new ItemDetailsViewModel
            {
                ItemId = 5, ItemCode = "QCU-EL-5021", Name = "Sony WH-CH520 Wireless Headset",
                Title = "Sony WH-CH520 Wireless Headset",
                Category = "Electronics", CategoryLabel = "Electronics", Subcategory = "Electronics & Audio",
                Condition = "Excellent", ConditionScore = 9m, Campus = "San Bartolome",
                Description = "Lightweight Bluetooth headset with built-in mic. Good for online classes and group calls.",
                HourlyRate = 30m, DailyRate = 60m, Rating = 4.8, ReviewCount = 16,
                IsAvailable = true, ListedText = "3 days ago", AvailableHours = "8:00 AM - 5:30 PM",
                HandoverZones = "Main Library Circulation Desk or CEA Student Lobby",
                ConditionNotes = "Clean ear cushions, full battery health, and clear microphone. Charging cable included.",
                ConditionTags = new List<string> { "Charging Cable", "Mic Tested" },
                Gallery = new List<GalleryImageViewModel>
                {
                    new GalleryImageViewModel { ImageUrl = Img5, Label = "Front", Transform = "scale(1)", TransformOrigin = "50% 50%" },
                    new GalleryImageViewModel { ImageUrl = Img5, Label = "Earcups", Transform = "scale(1.8)", TransformOrigin = "30% 35%" },
                    new GalleryImageViewModel { ImageUrl = Img5, Label = "Controls", Transform = "scale(1.5)", TransformOrigin = "85% 85%" }
                },
                Lender = new LenderViewModel
                {
                    Name = "Kristine Bautista", Initials = "KB", College = "College of Education", Program = "BSEd 2nd Year", ProgramCode = "BSEd",
                    Rating = 4.8, ReviewCount = 33, OnTimeRate = "97%", CompletedRentals = 36
                }
            },
            new ItemDetailsViewModel
            {
                ItemId = 6, ItemCode = "QCU-SC-4410", Name = "Rotring 300 Drafting Pencil",
                Title = "Rotring 300 Drafting Pencil",
                Category = "School Supplies", CategoryLabel = "School Supplies", Subcategory = "School Supplies & Drafting",
                Condition = "Like New", ConditionScore = 9.8m, Campus = "San Bartolome",
                Description = "0.5 mm mechanical drafting pencil with a knurled grip. Perfect for plates and technical drawing.",
                HourlyRate = 10m, DailyRate = 20m, Rating = 4.9, ReviewCount = 9,
                IsAvailable = true, ListedText = "4 days ago", AvailableHours = "8:00 AM - 5:30 PM",
                HandoverZones = "Main Library Circulation Desk or CEA Student Lobby",
                ConditionNotes = "Knurled grip intact, lead advance is smooth, and the sleeve is straight with no bends.",
                ConditionTags = new List<string> { "0.5 mm Lead", "Eraser Included" },
                Gallery = new List<GalleryImageViewModel>
                {
                    new GalleryImageViewModel { ImageUrl = Img6, Label = "Front", Transform = "scale(1)", TransformOrigin = "50% 50%" },
                    new GalleryImageViewModel { ImageUrl = Img6, Label = "Grip", Transform = "scale(1.8)", TransformOrigin = "30% 35%" },
                    new GalleryImageViewModel { ImageUrl = Img6, Label = "Tip", Transform = "scale(1.5)", TransformOrigin = "85% 85%" }
                },
                Lender = new LenderViewModel
                {
                    Name = "Miguel Ramos", Initials = "MR", College = "College of Architecture", Program = "BS Arch 3rd Year", ProgramCode = "BS",
                    Rating = 4.9, ReviewCount = 14, OnTimeRate = "100%", CompletedRentals = 17
                }
            },
            new ItemDetailsViewModel
            {
                ItemId = 7, ItemCode = "QCU-OT-3907", Name = "White Lab Coat (Size M)",
                Title = "White Lab Coat (Size M)",
                Category = "Other Items", CategoryLabel = "Other Items", Subcategory = "Uniforms & Lab Wear",
                Condition = "Good", ConditionScore = 8m, Campus = "Quirino",
                Description = "Clean, pressed white lab coat, size Medium. Required for most laboratory classes.",
                HourlyRate = 20m, DailyRate = 40m, Rating = 4.7, ReviewCount = 24,
                IsAvailable = true, ListedText = "1 week ago", AvailableHours = "8:00 AM - 5:30 PM",
                HandoverZones = "Quirino Guard House or Student Lounge",
                ConditionNotes = "Freshly laundered and pressed. Minor fading at the cuffs; all buttons and pockets intact.",
                ConditionTags = new List<string> { "Laundered", "Size Medium" },
                Gallery = new List<GalleryImageViewModel>
                {
                    new GalleryImageViewModel { ImageUrl = Img7, Label = "Front", Transform = "scale(1)", TransformOrigin = "50% 50%" },
                    new GalleryImageViewModel { ImageUrl = Img7, Label = "Pocket", Transform = "scale(1.8)", TransformOrigin = "30% 35%" },
                    new GalleryImageViewModel { ImageUrl = Img7, Label = "Collar", Transform = "scale(1.5)", TransformOrigin = "85% 85%" }
                },
                Lender = new LenderViewModel
                {
                    Name = "Sofia Navarro", Initials = "SN", College = "College of Health Sciences", Program = "BS MedTech 4th Year", ProgramCode = "BS",
                    Rating = 4.7, ReviewCount = 52, OnTimeRate = "96%", CompletedRentals = 63
                }
            },
            new ItemDetailsViewModel
            {
                ItemId = 8, ItemCode = "QCU-EL-3388", Name = "Casio fx-991ES PLUS 2nd Ed.",
                Title = "Casio fx-991ES PLUS 2nd Ed.",
                Category = "Electronics", CategoryLabel = "Electronics", Subcategory = "Electronics & Calculators",
                Condition = "Good", ConditionScore = 8.2m, Campus = "San Bartolome",
                Description = "Reliable scientific calculator with natural textbook display. Light wear on the case, works perfectly.",
                HourlyRate = 20m, DailyRate = 40m, Rating = 4.6, ReviewCount = 13,
                IsAvailable = true, ListedText = "1 week ago", AvailableHours = "8:00 AM - 5:30 PM",
                HandoverZones = "Main Library Circulation Desk or CEA Student Lobby",
                ConditionNotes = "Light wear on the case but the display and all keys work perfectly.",
                ConditionTags = new List<string> { "Fresh Batteries", "Slide Cover" },
                Gallery = new List<GalleryImageViewModel>
                {
                    new GalleryImageViewModel { ImageUrl = Img8, Label = "Front", Transform = "scale(1)", TransformOrigin = "50% 50%" },
                    new GalleryImageViewModel { ImageUrl = Img8, Label = "Display", Transform = "scale(1.8)", TransformOrigin = "30% 35%" },
                    new GalleryImageViewModel { ImageUrl = Img8, Label = "Cover", Transform = "scale(1.5)", TransformOrigin = "85% 85%" }
                },
                Lender = new LenderViewModel
                {
                    Name = "Carlo Mendoza", Initials = "CM", College = "College of Engineering (CEA)", Program = "BSEE 2nd Year", ProgramCode = "BSEE",
                    Rating = 4.6, ReviewCount = 21, OnTimeRate = "95%", CompletedRentals = 26
                }
            }
            };
        }
    }
}
