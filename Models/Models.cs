using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace UniShare.Models
{
    public class Item
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Owner { get; set; }
        public decimal PricePerDay { get; set; }
        public string Condition { get; set; }
        public string Status { get; set; }          // Available / Rented / Hidden
        public string Description { get; set; }
    }

    public class Rental
    {
        public int Id { get; set; }
        public string ItemName { get; set; }
        public string Owner { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal Total { get; set; }
        public string Status { get; set; }          // Pending / Approved / Ongoing / Completed
    }

    public class BorrowedItem
    {
        public int Id { get; set; }
        public string RecordId { get; set; }
        public string OwnerId { get; set; }
        public string ItemId { get; set; }
        public string ImageUrl { get; set; }
        public double? OwnerRating { get; set; }
        public double? ItemRating { get; set; }
        public bool ReturnRequested { get; set; }
        public string ReturnRequestedOn { get; set; }
        public string ReturnedOn { get; set; }
        public double? LateMilliseconds { get; set; }
        public double? PausedMilliseconds { get; set; }
        public DateTime? DisputedAt { get; set; }
        public string ItemName { get; set; }
        public string Category { get; set; }
        public string Lender { get; set; }
        public string LenderInitials { get; set; }
        public string LenderProgram { get; set; }
        public decimal PricePerHour { get; set; }
        public decimal? PaidAmount { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string PickupPoint { get; set; }
        public string PaymentMethod { get; set; }
        public string TransactionId { get; set; }
        public string About { get; set; }
        public string Status { get; set; }          // Pending (approved, awaiting handover) / Ongoing / Completed / Disputed
        public bool HandoverConfirmed { get; set; }
        public bool BorrowerHandoverConfirmed { get; set; }
        public bool OwnerHandoverConfirmed { get; set; }
        public bool CanConfirmHandover { get; set; }
        public bool CanReportNoShow { get; set; }
        public int? Rating { get; set; }
        public string Review { get; set; }
        public string DisputeReason { get; set; }
        public string Theme { get; set; }           // indigo / peach / mint (tile background)
        public DateTime? RequestedOn { get; set; }
        public DateTime? ApprovedOn { get; set; }
        public DateTime? HandoverOn { get; set; }
        public DateTime? CompletedOn { get; set; }
        public DateTime? HandoverAt { get; set; }    // exact moment the handover was confirmed (starts the rental timer)

        public int Hours { get { return (int)Math.Round((End - Start).TotalHours); } }
        public decimal TotalPaid { get { return PaidAmount ?? PricePerHour * Hours; } }
        public string PeriodLabel
        {
            get
            {
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                return Start.ToString("MMM d, h:mm tt", inv) + " \u2013 " + End.ToString("h:mm tt", inv);
            }
        }
    }

    public class SaleRecord
    {
        public int Id { get; set; }
        public string ItemName { get; set; }
        public string Renter { get; set; }
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; }
        public string Status { get; set; }          // Paid / Pending
    }

    public class Conversation
    {
        public int Id { get; set; }
        public string With { get; set; }
        public string Initials { get; set; }
        public string ItemName { get; set; }
        public string LastMessage { get; set; }
        public string Time { get; set; }
        public bool Unread { get; set; }
        public List<ChatMessage> Messages { get; set; }
    }

    public class ChatMessage
    {
        public bool Mine { get; set; }
        public string Text { get; set; }
        public string Time { get; set; }
    }

    public class NotificationItem
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public string Time { get; set; }
        public bool Unread { get; set; }
        public string EventKey { get; set; }
        public string LinkUrl { get; set; }
    }

    public class ListingForm
    {
        [Required, StringLength(80)]
        public string Name { get; set; }
        [Required]
        public string Category { get; set; }
        [Range(1, 100000)]
        public decimal PricePerDay { get; set; }
        [Required]
        public string Condition { get; set; }
        [StringLength(400)]
        public string Description { get; set; }
    }

    public class LoginForm
    {
        [Required, EmailAddress]
        public string Email { get; set; }
        [Required, DataType(DataType.Password)]
        public string Password { get; set; }
    }

    public class SalesReportViewModel
    {
        public List<SaleRecord> Sales { get; set; }
        public decimal TotalEarned { get; set; }
        public decimal Pending { get; set; }
        public int Transactions { get; set; }
    }

    public static class SampleData
    {
        public static List<Rental> MyRentals = new List<Rental>
        {
            new Rental { Id = 1, ItemName = "Engineering Drawing Set", Owner = "Paolo Reyes", StartDate = DateTime.Today.AddDays(-2), EndDate = DateTime.Today.AddDays(3), Total = 150, Status = "Ongoing" },
            new Rental { Id = 2, ItemName = "Lab Gown (Medium)", Owner = "Leah Mendoza", StartDate = DateTime.Today.AddDays(2), EndDate = DateTime.Today.AddDays(4), Total = 40, Status = "Pending" },
            new Rental { Id = 3, ItemName = "Badminton Racket", Owner = "Jose Ramos", StartDate = DateTime.Today.AddDays(-20), EndDate = DateTime.Today.AddDays(-17), Total = 105, Status = "Completed" }
        };

        public const decimal LateFeeMultiplier = 1m;

        public const int LateGraceMinutes = 30;

        public static readonly string[] BorrowedCategories = { "Electronics & Gadgets", "Books & References", "School Supplies", "Sports Equipment", "Accessories" };

        public static List<BorrowedItem> Borrowed = new List<BorrowedItem>
        {
            // 1. PENDING (Testing dual-confirmation handover)
            new BorrowedItem { Id = 1, ItemName = "Casio fx-991EX ClassWiz Calculator", Category = "Electronics & Gadgets",
                Lender = "Maria Santos", LenderInitials = "MS", LenderProgram = "BSIT 3rd Year", PricePerHour = 25,
                Start = new DateTime(2026, 11, 8, 13, 0, 0), End = new DateTime(2026, 11, 8, 15, 0, 0),
                PickupPoint = "CS Building Lobby", PaymentMethod = "Cash", TransactionId = "#UNI-20451",
                About = "Allowed in most exams. Comes with its hard cover and fresh batteries.",
                Status = "Pending", Theme = "indigo",
                RequestedOn = new DateTime(2026, 11, 7), ApprovedOn = new DateTime(2026, 11, 7) },

            // 2. ONGOING (Testing live timer)
            new BorrowedItem { Id = 2, ItemName = "Engineering Mechanics (Statics)", Category = "Books & References",
                Lender = "Carlo Reyes", LenderInitials = "CR", LenderProgram = "BSCE 4th Year", PricePerHour = 75,
                Start = new DateTime(2026, 11, 8, 14, 0, 0), End = new DateTime(2026, 11, 8, 16, 0, 0),
                PickupPoint = "University Library Entrance", PaymentMethod = "Cash", TransactionId = "#UNI-20466",
                About = "14th edition. No missing pages, light highlighting in chapters 3 to 5.",
                Status = "Ongoing", HandoverConfirmed = true, HandoverAt = DateTime.Now.AddMinutes(-165), Theme = "indigo",
                RequestedOn = new DateTime(2026, 11, 7), ApprovedOn = new DateTime(2026, 11, 8), HandoverOn = DateTime.Today },

            // 3. ONGOING (Testing live timer)
            new BorrowedItem { Id = 3, ItemName = "Rotring Isograph Junior Pen Set", Category = "School Supplies",
                Lender = "Jessa Villanueva", LenderInitials = "JV", LenderProgram = "BSArch 2nd Year", PricePerHour = 30,
                Start = new DateTime(2026, 11, 8, 9, 0, 0), End = new DateTime(2026, 11, 8, 11, 0, 0),
                PickupPoint = "Architecture Building Steps", PaymentMethod = "Cash", TransactionId = "#UNI-20472",
                About = "Set of 3 technical pens (0.2, 0.4, 0.6). Cleaned and filled with ink.",
                Status = "Ongoing", HandoverConfirmed = true, HandoverAt = DateTime.Now.AddMinutes(-30), Theme = "indigo",
                RequestedOn = new DateTime(2026, 11, 7), ApprovedOn = new DateTime(2026, 11, 7), HandoverOn = DateTime.Today },

            // 4. COMPLETED (No rating yet - Testing "To Rate" button and modal)
            new BorrowedItem { Id = 4, ItemName = "Molten GG7X Official Basketball", Category = "Sports Equipment",
                Lender = "Miguel Santos", LenderInitials = "MS", LenderProgram = "BSCpE 3rd Year", PricePerHour = 20,
                Start = new DateTime(2026, 10, 30, 15, 0, 0), End = new DateTime(2026, 10, 30, 17, 0, 0),
                PickupPoint = "QCU Gymnasium Entrance", PaymentMethod = "Cash", TransactionId = "#UNI-19877",
                About = "Official size 7 game ball. Please return it clean and properly inflated.",
                Status = "Completed", HandoverConfirmed = true, Theme = "peach", // Rating is null here
                RequestedOn = new DateTime(2026, 10, 29), ApprovedOn = new DateTime(2026, 10, 29),
                HandoverOn = new DateTime(2026, 10, 30), CompletedOn = new DateTime(2026, 10, 30) },

            // 5. COMPLETED (Already rated - Testing rating display)
            new BorrowedItem { Id = 5, ItemName = "Casio Calculator Hard Slide-on Case", Category = "Accessories",
                Lender = "Leah Mendoza", LenderInitials = "LM", LenderProgram = "BSEd 2nd Year", PricePerHour = 10,
                Start = new DateTime(2026, 10, 28, 13, 0, 0), End = new DateTime(2026, 10, 28, 15, 0, 0),
                PickupPoint = "Student Center Lobby", PaymentMethod = "Cash", TransactionId = "#UNI-19734",
                About = "Hard slide-on case that fits the fx-991 series.",
                Status = "Completed", HandoverConfirmed = true, Rating = 4, Review = "Fits perfectly, thanks!", Theme = "peach",
                RequestedOn = new DateTime(2026, 10, 27), ApprovedOn = new DateTime(2026, 10, 27),
                HandoverOn = new DateTime(2026, 10, 28), CompletedOn = new DateTime(2026, 10, 28) },

            // 6. DISPUTED (Testing dispute layout without buttons)
            new BorrowedItem { Id = 6, ItemName = "Arduino Uno R3 Starter Kit", Category = "Electronics & Gadgets",
                Lender = "Kevin Lim", LenderInitials = "KL", LenderProgram = "BSCpE 4th Year", PricePerHour = 40,
                Start = new DateTime(2026, 11, 4, 10, 0, 0), End = new DateTime(2026, 11, 4, 13, 0, 0),
                PickupPoint = "Engineering Lab 2", PaymentMethod = "Cash", TransactionId = "#UNI-20310",
                About = "Uno R3 board with breadboard, jumper wires, LEDs and resistors.",
                Status = "Disputed", Theme = "mint",
                DisputeReason = "Item not working as described.",
                RequestedOn = new DateTime(2026, 11, 3), ApprovedOn = new DateTime(2026, 11, 3), HandoverOn = new DateTime(2026, 11, 4) }
        };

        public static List<SaleRecord> Sales = new List<SaleRecord>
        {
            new SaleRecord { Id = 1, ItemName = "Data Structures Book", Renter = "Ana Cruz", Date = DateTime.Today.AddDays(-1), Amount = 60, PaymentMethod = "Cash", Status = "Paid" },
            new SaleRecord { Id = 2, ItemName = "Arduino Starter Kit", Renter = "Kevin Lim", Date = DateTime.Today.AddDays(-6), Amount = 220, PaymentMethod = "Cash", Status = "Paid" },
            new SaleRecord { Id = 3, ItemName = "USB-C Hub", Renter = "Leah Mendoza", Date = DateTime.Today.AddDays(-9), Amount = 40, PaymentMethod = "Cash", Status = "Pending" }
        };

        public static List<Conversation> Conversations = new List<Conversation>
        {
            new Conversation { Id = 1, With = "Maria Santos", Initials = "MS", ItemName = "Scientific Calculator", LastMessage = "Sure, see you at the library.", Time = "10:42 AM", Unread = true,
                Messages = new List<ChatMessage> {
                    new ChatMessage { Mine = true, Text = "Hi! Is the calculator still available?", Time = "10:30 AM" },
                    new ChatMessage { Mine = false, Text = "Yes it is. Pickup tomorrow?", Time = "10:38 AM" },
                    new ChatMessage { Mine = false, Text = "Sure, see you at the library.", Time = "10:42 AM" } } },
            new Conversation { Id = 2, With = "Paolo Reyes", Initials = "PR", ItemName = "Engineering Drawing Set", LastMessage = "Please return it by Friday.", Time = "Yesterday", Unread = false,
                Messages = new List<ChatMessage> {
                    new ChatMessage { Mine = false, Text = "Please return it by Friday.", Time = "Yesterday" },
                    new ChatMessage { Mine = true, Text = "Noted, thank you!", Time = "Yesterday" } } }
        };

        public static List<NotificationItem> Notifications = new List<NotificationItem>
        {
            new NotificationItem { Id = 1, Title = "Rental request approved", Body = "Paolo Reyes approved your request for the Engineering Drawing Set.", Time = "2 hours ago", Unread = true },
            new NotificationItem { Id = 2, Title = "New message", Body = "Maria Santos sent you a message.", Time = "3 hours ago", Unread = true },
            new NotificationItem { Id = 3, Title = "Payment received", Body = "You received P60 for Data Structures Book.", Time = "Yesterday", Unread = false }
        };
    }
}
