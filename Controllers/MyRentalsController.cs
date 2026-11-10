using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;

namespace UniShare.Controllers
{
    public class RentalSummaryDto
    {
        public int TotalRentals { get; set; }      // every record, whatever its status
        public int PendingRequests { get; set; }   // Pending only
        public int ActiveRentals { get; set; }     // Ongoing only
        public int NeedAttention { get; set; }    // Overdue + Disputed
        public int OverdueCount { get; set; }
        public int DisputedCount { get; set; }
    }

    public class RentalRecordDto
    {
        public string RequestId { get; set; }
        public string ItemName { get; set; }
        public string BorrowerName { get; set; }
        public string BorrowerInitials { get; set; }
        public string BorrowerProgram { get; set; }
        public DateTime RequestDate { get; set; }
        public string RequestDateFormatted { get; set; }
        public DateTime HandoverDate { get; set; }
        public string HandoverScheduleFormatted { get; set; }
        public string Duration { get; set; }
        public decimal RentalFee { get; set; }
        public string RentalFeeFormatted { get; set; }
        public string Status { get; set; } // Pending, Accepted, Declined, Ongoing, Overdue, Returned, Disputed, Completed
        public string Category { get; set; }

        // Added for the status-specific modals (mock data, filled by PrepareMockRentals)
        public string OwnerName { get; set; }
        public string OwnerProgram { get; set; }
        public string ItemLocation { get; set; }
        public string HandoverLocation { get; set; }
        public string ItemImageUrl { get; set; } // null = no image available, the view shows a placeholder
        public string RequestNote { get; set; }
        public double DurationHours { get; set; }
        public decimal HourlyRate { get; set; }
        public string HourlyRateFormatted { get; set; }
        public string HandoverDateFormatted { get; set; }
        public string HandoverTimeFormatted { get; set; }
        public DateTime ExpectedReturnDate { get; set; }
        public string ExpectedReturnFormatted { get; set; }

        // Late-fee rule used by the Overdue / Return modals (mock data, set by PrepareMockRentals)
        public int GracePeriodMinutes { get; set; }

        // Accepted modal: two-party handover verification (mock data, set by PrepareMockRentals)
        public bool OwnerHandoverConfirmed { get; set; }
        public string OwnerHandoverConfirmedAt { get; set; }
        public bool BorrowerHandoverConfirmed { get; set; }
        public string BorrowerHandoverConfirmedAt { get; set; }

        // Declined modal
        public string DeclineReason { get; set; }
        public string DeclineNote { get; set; }
        public string DeclinedAtFormatted { get; set; }

        // Disputed modal
        public string DisputeCategory { get; set; }
        public string DisputeDescription { get; set; }
        public string DisputeSubmittedAtFormatted { get; set; }
        public List<string> DisputeEvidenceUrls { get; set; } // empty = no evidence images submitted

        // Returned / Completed modals
        public string ReturnedAtFormatted { get; set; }
        public string CompletedAtFormatted { get; set; }
    }

    public class MyRentalsViewModel
    {
        public RentalSummaryDto Summary { get; set; }
        public List<RentalRecordDto> Rentals { get; set; }

        public MyRentalsViewModel()
        {
            Summary = new RentalSummaryDto();
            Rentals = new List<RentalRecordDto>();
        }
    }

    public class MyRentalsController : Controller
    {
        // GET: /MyRentals or /MyRentals/Index
        [HttpGet]
        public ActionResult Index()
        {
            ViewBag.Title = "Rentals";

            var model = new MyRentalsViewModel
            {
                Rentals = GetMockRentals()
            };

            // Summary cards are counted from the records, so they always agree with the table
            // (the page script keeps them in sync when a rental changes status).
            int overdue = model.Rentals.Count(r => r.Status == "Overdue");
            int disputed = model.Rentals.Count(r => r.Status == "Disputed");
            model.Summary = new RentalSummaryDto
            {
                TotalRentals = model.Rentals.Count,
                PendingRequests = model.Rentals.Count(r => r.Status == "Pending"),
                ActiveRentals = model.Rentals.Count(r => r.Status == "Ongoing"),
                NeedAttention = overdue + disputed,
                OverdueCount = overdue,
                DisputedCount = disputed
            };

            return View(model);
        }

        private List<RentalRecordDto> GetMockRentals()
        {
            var rentals = new List<RentalRecordDto>
            {
                // Required sample starting records
                new RentalRecordDto
                {
                    RequestId = "#REQ-1024",
                    ItemName = "Scientific Calculator",
                    BorrowerName = "Alex Santos",
                    BorrowerInitials = "AS",
                    BorrowerProgram = "BSIT • 2nd Year",
                    RequestDate = new DateTime(2026, 10, 25, 16, 20, 0),
                    RequestDateFormatted = "Oct 25, 2026",
                    HandoverDate = new DateTime(2026, 10, 28, 10, 0, 0),
                    HandoverScheduleFormatted = "Oct 28, 2026 • 10:00 AM",
                    Duration = "2 hours",
                    RentalFee = 40m,
                    RentalFeeFormatted = "₱40",
                    Status = "Pending",
                    Category = "Calculators & Tools"
                },
                new RentalRecordDto
                {
                    RequestId = "#REQ-1025",
                    ItemName = "Drafting Technical Drawing Set",
                    BorrowerName = "Camille Reyes",
                    BorrowerInitials = "CR",
                    BorrowerProgram = "BSCE • 3rd Year",
                    RequestDate = new DateTime(2026, 10, 24, 9, 45, 0),
                    RequestDateFormatted = "Oct 24, 2026",
                    HandoverDate = new DateTime(2026, 10, 27, 13, 30, 0),
                    HandoverScheduleFormatted = "Oct 27, 2026 • 1:30 PM",
                    Duration = "4 hours",
                    RentalFee = 60m,
                    RentalFeeFormatted = "₱60",
                    Status = "Ongoing",
                    Category = "Drafting & Arts"
                },
                new RentalRecordDto
                {
                    RequestId = "#REQ-1027",
                    ItemName = "Digital Multimeter Pro",
                    BorrowerName = "Bea Mendoza",
                    BorrowerInitials = "BM",
                    BorrowerProgram = "BSEE • 4th Year",
                    RequestDate = new DateTime(2026, 10, 26, 14, 10, 0),
                    RequestDateFormatted = "Oct 26, 2026",
                    HandoverDate = new DateTime(2026, 10, 29, 8, 30, 0),
                    HandoverScheduleFormatted = "Oct 29, 2026 • 8:30 AM",
                    Duration = "3 hours",
                    RentalFee = 50m,
                    RentalFeeFormatted = "₱50",
                    Status = "Accepted",
                    Category = "Lab Equipment"
                },
                new RentalRecordDto
                {
                    RequestId = "#REQ-1028",
                    ItemName = "Chemistry Laboratory Coat (M)",
                    BorrowerName = "Patricia Cruz",
                    BorrowerInitials = "PC",
                    BorrowerProgram = "BSCpE • 1st Year",
                    RequestDate = new DateTime(2026, 10, 27, 11, 5, 0),
                    RequestDateFormatted = "Oct 27, 2026",
                    HandoverDate = new DateTime(2026, 10, 29, 13, 0, 0),
                    HandoverScheduleFormatted = "Oct 29, 2026 • 1:00 PM",
                    Duration = "5 hours",
                    RentalFee = 35m,
                    RentalFeeFormatted = "₱35",
                    Status = "Pending",
                    Category = "Lab Attire"
                },
                new RentalRecordDto
                {
                    RequestId = "#REQ-1032",
                    ItemName = "Engineering Mechanics Textbook",
                    BorrowerName = "Joshua Dizon",
                    BorrowerInitials = "JD",
                    BorrowerProgram = "BSME • 2nd Year",
                    RequestDate = new DateTime(2026, 10, 26, 17, 30, 0),
                    RequestDateFormatted = "Oct 26, 2026",
                    HandoverDate = new DateTime(2026, 10, 29, 10, 0, 0),
                    HandoverScheduleFormatted = "Oct 29, 2026 • 10:00 AM",
                    Duration = "4 hours",
                    RentalFee = 50m,
                    RentalFeeFormatted = "₱50",
                    Status = "Accepted",
                    Category = "Books & Notes"
                },
                new RentalRecordDto
                {
                    RequestId = "#REQ-1033",
                    ItemName = "Graphing Calculator TI-84",
                    BorrowerName = "Maria Reyes",
                    BorrowerInitials = "MR",
                    BorrowerProgram = "BSIT • 3rd Year",
                    RequestDate = new DateTime(2026, 10, 27, 8, 50, 0),
                    RequestDateFormatted = "Oct 27, 2026",
                    HandoverDate = new DateTime(2026, 10, 29, 14, 0, 0),
                    HandoverScheduleFormatted = "Oct 29, 2026 • 2:00 PM",
                    Duration = "3 hours",
                    RentalFee = 60m,
                    RentalFeeFormatted = "₱60",
                    Status = "Ongoing",
                    Category = "Calculators & Tools"
                },

                // Additional mock records demonstrating all 8 statuses and diverse date ranges
                new RentalRecordDto
                {
                    RequestId = "#REQ-1035",
                    ItemName = "Arduino Uno R3 Starter Kit",
                    BorrowerName = "Kenneth Ramirez",
                    BorrowerInitials = "KR",
                    BorrowerProgram = "BSCpE • 3rd Year",
                    RequestDate = new DateTime(2026, 11, 5, 10, 15, 0),
                    RequestDateFormatted = "Nov 05, 2026",
                    HandoverDate = new DateTime(2026, 11, 8, 9, 0, 0),
                    HandoverScheduleFormatted = "Nov 08, 2026 • 9:00 AM",
                    Duration = "4 hours",
                    RentalFee = 80m,
                    RentalFeeFormatted = "₱80",
                    Status = "Overdue",
                    Category = "Electronics"
                },
                new RentalRecordDto
                {
                    RequestId = "#REQ-1036",
                    ItemName = "Staedtler Mars 700 Technical Pen",
                    BorrowerName = "Chloe Anne Cruz",
                    BorrowerInitials = "CC",
                    BorrowerProgram = "BSArch • 2nd Year",
                    RequestDate = new DateTime(2026, 11, 4, 15, 40, 0),
                    RequestDateFormatted = "Nov 04, 2026",
                    HandoverDate = new DateTime(2026, 11, 7, 16, 0, 0),
                    HandoverScheduleFormatted = "Nov 07, 2026 • 4:00 PM",
                    Duration = "2 hours",
                    RentalFee = 30m,
                    RentalFeeFormatted = "₱30",
                    Status = "Returned",
                    Category = "Drafting & Arts"
                },
                new RentalRecordDto
                {
                    RequestId = "#REQ-1038",
                    ItemName = "Casio fx-991EX ClassWiz",
                    BorrowerName = "Juan Dela Cruz",
                    BorrowerInitials = "JC",
                    BorrowerProgram = "BSCS • 3rd Year",
                    RequestDate = new DateTime(2026, 11, 2, 13, 25, 0),
                    RequestDateFormatted = "Nov 02, 2026",
                    HandoverDate = new DateTime(2026, 11, 5, 11, 0, 0),
                    HandoverScheduleFormatted = "Nov 05, 2026 • 11:00 AM",
                    Duration = "3 hours",
                    RentalFee = 45m,
                    RentalFeeFormatted = "₱45",
                    Status = "Disputed",
                    Category = "Calculators & Tools"
                },
                new RentalRecordDto
                {
                    RequestId = "#REQ-1039",
                    ItemName = "Canon EOS 3000D DSLR Camera Kit",
                    BorrowerName = "Mark Joshua Tan",
                    BorrowerInitials = "MT",
                    BorrowerProgram = "BACOMM • 4th Year",
                    RequestDate = new DateTime(2026, 11, 1, 19, 0, 0),
                    RequestDateFormatted = "Nov 01, 2026",
                    HandoverDate = new DateTime(2026, 11, 4, 13, 0, 0),
                    HandoverScheduleFormatted = "Nov 04, 2026 • 1:00 PM",
                    Duration = "5 hours",
                    RentalFee = 250m,
                    RentalFeeFormatted = "₱250",
                    Status = "Declined",
                    Category = "Electronics"
                },
                new RentalRecordDto
                {
                    RequestId = "#REQ-1040",
                    ItemName = "University PE Uniform Male (M)",
                    BorrowerName = "Miguel Santos",
                    BorrowerInitials = "MS",
                    BorrowerProgram = "BSEd • 2nd Year",
                    RequestDate = new DateTime(2026, 11, 6, 16, 45, 0),
                    RequestDateFormatted = "Nov 06, 2026",
                    HandoverDate = new DateTime(2026, 11, 9, 8, 30, 0),
                    HandoverScheduleFormatted = "Nov 09, 2026 • 8:30 AM",
                    Duration = "4 hours",
                    RentalFee = 40m,
                    RentalFeeFormatted = "₱40",
                    Status = "Pending",
                    Category = "Uniforms & Attire"
                },
                new RentalRecordDto
                {
                    RequestId = "#REQ-1041",
                    ItemName = "Staedtler Compass & Drafting Kit",
                    BorrowerName = "Danica Flores",
                    BorrowerInitials = "DF",
                    BorrowerProgram = "BSCE • 1st Year",
                    RequestDate = new DateTime(2026, 11, 7, 9, 30, 0),
                    RequestDateFormatted = "Nov 07, 2026",
                    HandoverDate = new DateTime(2026, 11, 9, 11, 15, 0),
                    HandoverScheduleFormatted = "Nov 09, 2026 • 11:15 AM",
                    Duration = "2 hours",
                    RentalFee = 35m,
                    RentalFeeFormatted = "₱35",
                    Status = "Pending",
                    Category = "Drafting & Arts"
                },
                new RentalRecordDto
                {
                    RequestId = "#REQ-1042",
                    ItemName = "Molten GG7X Official Basketball",
                    BorrowerName = "Jeric Alcantara",
                    BorrowerInitials = "JA",
                    BorrowerProgram = "BSBA • 3rd Year",
                    RequestDate = new DateTime(2026, 10, 19, 12, 15, 0),
                    RequestDateFormatted = "Oct 19, 2026",
                    HandoverDate = new DateTime(2026, 10, 22, 15, 0, 0),
                    HandoverScheduleFormatted = "Oct 22, 2026 • 3:00 PM",
                    Duration = "2 hours",
                    RentalFee = 30m,
                    RentalFeeFormatted = "₱30",
                    Status = "Completed",
                    Category = "Sports Equipment"
                },
                new RentalRecordDto
                {
                    RequestId = "#REQ-1044",
                    ItemName = "TI-84 Plus CE Graphing Calculator",
                    BorrowerName = "Angelo Reyes",
                    BorrowerInitials = "AR",
                    BorrowerProgram = "BSIT • 2nd Year",
                    RequestDate = new DateTime(2026, 10, 17, 10, 5, 0),
                    RequestDateFormatted = "Oct 17, 2026",
                    HandoverDate = new DateTime(2026, 10, 20, 9, 30, 0),
                    HandoverScheduleFormatted = "Oct 20, 2026 • 9:30 AM",
                    Duration = "3 hours",
                    RentalFee = 60m,
                    RentalFeeFormatted = "₱60",
                    Status = "Completed",
                    Category = "Calculators & Tools"
                },
                new RentalRecordDto
                {
                    RequestId = "#REQ-1045",
                    ItemName = "Architectural T-Square 36-inch",
                    BorrowerName = "Leah Mendoza",
                    BorrowerInitials = "LM",
                    BorrowerProgram = "BSArch • 3rd Year",
                    RequestDate = new DateTime(2026, 10, 15, 14, 50, 0),
                    RequestDateFormatted = "Oct 15, 2026",
                    HandoverDate = new DateTime(2026, 10, 18, 11, 0, 0),
                    HandoverScheduleFormatted = "Oct 18, 2026 • 11:00 AM",
                    Duration = "4 hours",
                    RentalFee = 50m,
                    RentalFeeFormatted = "₱50",
                    Status = "Completed",
                    Category = "Drafting & Arts"
                },
                new RentalRecordDto
                {
                    RequestId = "#REQ-1046",
                    ItemName = "Nursing Scrub Suit Navy Blue (L)",
                    BorrowerName = "Hannah Morales",
                    BorrowerInitials = "HM",
                    BorrowerProgram = "BSN • 4th Year",
                    RequestDate = new DateTime(2026, 10, 13, 9, 20, 0),
                    RequestDateFormatted = "Oct 13, 2026",
                    HandoverDate = new DateTime(2026, 10, 16, 13, 0, 0),
                    HandoverScheduleFormatted = "Oct 16, 2026 • 1:00 PM",
                    Duration = "8 hours",
                    RentalFee = 90m,
                    RentalFeeFormatted = "₱90",
                    Status = "Completed",
                    Category = "Uniforms & Attire"
                },
                new RentalRecordDto
                {
                    RequestId = "#REQ-1047",
                    ItemName = "Laboratory Safety Goggles & Gloves",
                    BorrowerName = "Christian David",
                    BorrowerInitials = "CD",
                    BorrowerProgram = "BSChem • 2nd Year",
                    RequestDate = new DateTime(2026, 10, 11, 16, 10, 0),
                    RequestDateFormatted = "Oct 11, 2026",
                    HandoverDate = new DateTime(2026, 10, 14, 10, 0, 0),
                    HandoverScheduleFormatted = "Oct 14, 2026 • 10:00 AM",
                    Duration = "3 hours",
                    RentalFee = 25m,
                    RentalFeeFormatted = "₱25",
                    Status = "Completed",
                    Category = "Lab Attire"
                },
                new RentalRecordDto
                {
                    RequestId = "#REQ-1048",
                    ItemName = "Microcontroller Development Board",
                    BorrowerName = "Paolo Reyes",
                    BorrowerInitials = "PR",
                    BorrowerProgram = "BSCpE • 4th Year",
                    RequestDate = new DateTime(2026, 10, 9, 11, 35, 0),
                    RequestDateFormatted = "Oct 09, 2026",
                    HandoverDate = new DateTime(2026, 10, 12, 14, 0, 0),
                    HandoverScheduleFormatted = "Oct 12, 2026 • 2:00 PM",
                    Duration = "5 hours",
                    RentalFee = 70m,
                    RentalFeeFormatted = "₱70",
                    Status = "Completed",
                    Category = "Electronics"
                },
                new RentalRecordDto
                {
                    RequestId = "#REQ-1049",
                    ItemName = "Cisco Networking CCNA Study Guide",
                    BorrowerName = "Stephanie Tan",
                    BorrowerInitials = "ST",
                    BorrowerProgram = "BSIT • 4th Year",
                    RequestDate = new DateTime(2026, 10, 7, 13, 0, 0),
                    RequestDateFormatted = "Oct 07, 2026",
                    HandoverDate = new DateTime(2026, 10, 10, 16, 0, 0),
                    HandoverScheduleFormatted = "Oct 10, 2026 • 4:00 PM",
                    Duration = "7 hours",
                    RentalFee = 45m,
                    RentalFeeFormatted = "₱45",
                    Status = "Completed",
                    Category = "Books & Notes"
                }
            };

            return PrepareMockRentals(rentals);
        }

        // Fills the derived/mock fields used by the Pending and Ongoing modals so the
        // schedule shown in the table and in the modals always comes from the same values.
        private List<RentalRecordDto> PrepareMockRentals(List<RentalRecordDto> rentals)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;

            // Ongoing rentals are mock "live" sessions: their start time is set relative to the
            // current time so the countdown in the Ongoing modal is always meaningful.
            // Value = minutes since the rental started.
            DateTime nowRaw = DateTime.Now;
            DateTime now = new DateTime(nowRaw.Year, nowRaw.Month, nowRaw.Day, nowRaw.Hour, nowRaw.Minute, 0, DateTimeKind.Local);
            var startedMinutesAgo = new Dictionary<string, int>
            {
                { "#REQ-1025", 35 },   // 4-hour rental
                { "#REQ-1033", 110 }   // 3-hour rental
            };

            // Overdue rentals are mock sessions that ended a while ago: value = minutes past the expected
            // return at page load. The start time is derived from it, so schedule, expected return,
            // overdue duration and late fee all agree. The Overdue modal then keeps counting live.
            var overdueMinutes = new Dictionary<string, int>
            {
                { "#REQ-1035", 95 }
            };

            foreach (var r in rentals)
            {
                int minutesAgo;
                if (r.Status == "Ongoing" && startedMinutesAgo.TryGetValue(r.RequestId, out minutesAgo))
                {
                    r.HandoverDate = now.AddMinutes(-minutesAgo);
                    r.HandoverScheduleFormatted = r.HandoverDate.ToString("MMM dd, yyyy '•' h:mm tt", inv);
                    r.RequestDate = r.HandoverDate.AddDays(-2);
                    r.RequestDateFormatted = r.RequestDate.ToString("MMM dd, yyyy", inv);
                }

                double hours;
                if (!double.TryParse((r.Duration ?? "").Split(' ')[0], NumberStyles.Float, inv, out hours) || hours <= 0)
                {
                    hours = 1;
                }

                int lateBy;
                if (r.Status == "Overdue" && overdueMinutes.TryGetValue(r.RequestId, out lateBy))
                {
                    r.HandoverDate = now.AddMinutes(-(hours * 60 + lateBy));
                    r.HandoverScheduleFormatted = r.HandoverDate.ToString("MMM dd, yyyy '•' h:mm tt", inv);
                    r.RequestDate = r.HandoverDate.AddDays(-2);
                    r.RequestDateFormatted = r.RequestDate.ToString("MMM dd, yyyy", inv);
                }

                r.GracePeriodMinutes = 15;
                r.DurationHours = hours;
                r.HourlyRate = Math.Round(r.RentalFee / (decimal)hours, 2);
                r.HourlyRateFormatted = "₱" + r.HourlyRate.ToString("N2", inv);
                r.HandoverDateFormatted = r.HandoverDate.ToString("MMM dd, yyyy", inv);
                r.HandoverTimeFormatted = r.HandoverDate.ToString("h:mm tt", inv);
                r.ExpectedReturnDate = r.HandoverDate.AddHours(hours);
                r.ExpectedReturnFormatted = r.ExpectedReturnDate.ToString("MMM dd, yyyy '•' h:mm tt", inv);

                r.OwnerName = "Julian Vance";
                r.OwnerProgram = "BSIT • 3rd Year";
                r.ItemLocation = "QCU San Bartolome Campus • Admin Building Lobby";
                r.HandoverLocation = "QCU San Bartolome Campus • Admin Building Lobby";
                r.RequestNote = "Hi! I'd like to borrow the " + r.ItemName + " for " + r.Duration + ". Can we meet at the Admin Building Lobby? Thanks!";

                // Status-specific mock details (shown by the Accepted / Declined / Disputed / Returned / Completed modals)
                r.DisputeEvidenceUrls = new List<string>();
                string dateTimeFormat = "MMM dd, yyyy '•' h:mm tt";

                if (r.Status == "Accepted")
                {
                    // Neither side has confirmed yet, except where the borrower already did
                    r.OwnerHandoverConfirmed = false;
                    r.BorrowerHandoverConfirmed = false;
                    if (r.RequestId == "#REQ-1027")
                    {
                        r.BorrowerHandoverConfirmed = true;
                        r.BorrowerHandoverConfirmedAt = r.HandoverDate.AddMinutes(-5).ToString(dateTimeFormat, inv);
                    }
                }

                if (r.Status == "Declined")
                {
                    r.DeclineReason = "Item is no longer available";
                    r.DeclineNote = "Sorry, the camera kit is already reserved for a campus shoot at that time.";
                    r.DeclinedAtFormatted = r.RequestDate.AddHours(13).ToString(dateTimeFormat, inv);
                }

                if (r.Status == "Disputed")
                {
                    r.DisputeCategory = "Item Damage";
                    r.DisputeDescription = "The calculator was returned with a cracked screen. It worked fine when it was handed over.";
                    r.DisputeSubmittedAtFormatted = r.ExpectedReturnDate.AddMinutes(20).ToString(dateTimeFormat, inv);
                }

                if (r.Status == "Returned" || r.Status == "Completed")
                {
                    r.ReturnedAtFormatted = r.ExpectedReturnDate.AddMinutes(-5).ToString(dateTimeFormat, inv);
                }

                if (r.Status == "Completed")
                {
                    r.CompletedAtFormatted = r.ExpectedReturnDate.AddMinutes(10).ToString(dateTimeFormat, inv);
                }
            }

            return rentals;
        }
    }
}

