using System;
using System.Collections.Generic;
using System.Web.Mvc;
using UniShare.Models;

namespace UniShare.Controllers
{
    [Authorize(Roles = "ADMIN")]
    public class TransactionPaymentRecordsController : Controller
    {
        // GET: TransactionPaymentRecords
        public ActionResult Index()
        {
            ViewBag.Title = "Transaction and Payment Records";
            ViewBag.TotalTransactions = 128;
            ViewBag.ConfirmedPayments = 104;
            ViewBag.PendingConfirmation = 24;
            ViewBag.TotalRecordedFees = "PHP 38,450.00";

            var transactions = new List<TransactionViewModel>
            {
                new TransactionViewModel
                {
                    TransactionId = "TXN-2026-0482",
                    BookingId = "BK-2026-084",
                    ItemName = "Casio fx-991EX ClassWiz",
                    BorrowerName = "Maria Santos",
                    OwnerName = "Juan Dela Cruz",
                    BaseFee = "PHP 120.00",
                    LateFee = "PHP 30.00",
                    TotalAmount = "PHP 150.00",
                    Status = "Awaiting Owner Confirmation",
                    Method = "GCash",
                    DateRange = "Today"
                },
                new TransactionViewModel
                {
                    TransactionId = "TXN-2026-0479",
                    BookingId = "BK-2026-075",
                    ItemName = "Arduino Uno R3 Ultimate Kit",
                    BorrowerName = "Kenneth Ramirez",
                    OwnerName = "Angelo Reyes",
                    BaseFee = "PHP 280.00",
                    LateFee = "PHP 50.00",
                    TotalAmount = "PHP 330.00",
                    Status = "Awaiting Owner Confirmation",
                    Method = "GCash",
                    DateRange = "Today"
                },
                new TransactionViewModel
                {
                    TransactionId = "TXN-2026-0465",
                    BookingId = "BK-2026-064",
                    ItemName = "Staedtler Mars 700 Technical Pen",
                    BorrowerName = "Chloe Anne Cruz",
                    OwnerName = "Patricia Gomez",
                    BaseFee = "PHP 120.00",
                    LateFee = "PHP 0.00",
                    TotalAmount = "PHP 120.00",
                    Status = "Confirmed",
                    Method = "Maya",
                    DateRange = "Last 7 Days"
                },
                new TransactionViewModel
                {
                    TransactionId = "TXN-2026-0451",
                    BookingId = "BK-2026-052",
                    ItemName = "TI-84 Plus CE Graphing Calculator",
                    BorrowerName = "Jeric Alcantara",
                    OwnerName = "Jerome Bautista",
                    BaseFee = "PHP 350.00",
                    LateFee = "PHP 75.00",
                    TotalAmount = "PHP 425.00",
                    Status = "Awaiting Owner Confirmation",
                    Method = "GCash",
                    DateRange = "Last 7 Days"
                },
                new TransactionViewModel
                {
                    TransactionId = "TXN-2026-0440",
                    BookingId = "BK-2026-041",
                    ItemName = "Canon EOS 3000D DSLR Camera Kit",
                    BorrowerName = "Bea Patricia Reyes",
                    OwnerName = "Mark Joshua Tan",
                    BaseFee = "PHP 750.00",
                    LateFee = "PHP 0.00",
                    TotalAmount = "PHP 750.00",
                    Status = "Confirmed",
                    Method = "GCash",
                    DateRange = "Last 7 Days"
                }
            };

            return View("TransactionPaymentRecords", transactions);
        }

        public ActionResult TransactionPaymentRecords()
        {
            return Index();
        }

        [HttpGet]
        public ActionResult GetTransactionDetails(string id)
        {
            TransactionViewModel details;

            if (id == "TXN-2026-0479")
            {
                details = new TransactionViewModel
                {
                    TransactionId = "TXN-2026-0479",
                    BookingId = "BK-2026-075",
                    DateLogged = "Nov 08, 2026 - 11:15 AM",
                    ItemName = "Arduino Uno R3 Ultimate Kit",
                    Serial = "ARD-UNO-48192",
                    BorrowerName = "Kenneth Ramirez",
                    BorrowerId = "2023-0112-SB",
                    BorrowerCourse = "BS Electronics Engineering",
                    OwnerName = "Angelo Reyes",
                    OwnerId = "2024-0089-SB",
                    OwnerCourse = "BS Information Technology",
                    OwnerRating = "4.8 ★ (19 rentals)",
                    BaseFee = "PHP 280.00",
                    LateFee = "PHP 50.00",
                    TotalAmount = "PHP 330.00",
                    GCashRef = "GC-8832-1904-7721",
                    Status = "Awaiting Owner Confirmation",
                    Method = "GCash"
                };
            }
            else if (id == "TXN-2026-0465")
            {
                details = new TransactionViewModel
                {
                    TransactionId = "TXN-2026-0465",
                    BookingId = "BK-2026-064",
                    DateLogged = "Nov 07, 2026 - 04:45 PM",
                    ItemName = "Staedtler Mars 700 Technical Pen",
                    Serial = "STM-700-0918",
                    BorrowerName = "Chloe Anne Cruz",
                    BorrowerId = "2023-0891-SB",
                    BorrowerCourse = "BS Architecture",
                    OwnerName = "Patricia Gomez",
                    OwnerId = "2024-0312-SB",
                    OwnerCourse = "BS Architecture",
                    OwnerRating = "5.0 ★ (34 rentals)",
                    BaseFee = "PHP 120.00",
                    LateFee = "PHP 0.00",
                    TotalAmount = "PHP 120.00",
                    GCashRef = "MY-6641-0982-3341",
                    Status = "Confirmed",
                    Method = "Maya"
                };
            }
            else if (id == "TXN-2026-0451")
            {
                details = new TransactionViewModel
                {
                    TransactionId = "TXN-2026-0451",
                    BookingId = "BK-2026-052",
                    DateLogged = "Nov 06, 2026 - 10:20 AM",
                    ItemName = "TI-84 Plus CE Graphing Calculator",
                    Serial = "TI84-99120-CE",
                    BorrowerName = "Jeric Alcantara",
                    BorrowerId = "2022-0941-SB",
                    BorrowerCourse = "BS Civil Engineering",
                    OwnerName = "Jerome Bautista",
                    OwnerId = "2023-0518-SB",
                    OwnerCourse = "BS Mechanical Engineering",
                    OwnerRating = "4.7 ★ (15 rentals)",
                    BaseFee = "PHP 350.00",
                    LateFee = "PHP 75.00",
                    TotalAmount = "PHP 425.00",
                    GCashRef = "GC-7129-5021-9984",
                    Status = "Awaiting Owner Confirmation",
                    Method = "GCash"
                };
            }
            else if (id == "TXN-2026-0440")
            {
                details = new TransactionViewModel
                {
                    TransactionId = "TXN-2026-0440",
                    BookingId = "BK-2026-041",
                    DateLogged = "Nov 05, 2026 - 01:10 PM",
                    ItemName = "Canon EOS 3000D DSLR Camera Kit",
                    Serial = "CN-3000D-5512A",
                    BorrowerName = "Bea Patricia Reyes",
                    BorrowerId = "2023-0667-SB",
                    BorrowerCourse = "BA Communication",
                    OwnerName = "Mark Joshua Tan",
                    OwnerId = "2022-0331-SB",
                    OwnerCourse = "BS Information Technology",
                    OwnerRating = "4.9 ★ (42 rentals)",
                    BaseFee = "PHP 750.00",
                    LateFee = "PHP 0.00",
                    TotalAmount = "PHP 750.00",
                    GCashRef = "GC-5519-3382-4102",
                    Status = "Confirmed",
                    Method = "GCash"
                };
            }
            else
            {
                // Default: TXN-2026-0482 (Casio fx-991EX ClassWiz)
                details = new TransactionViewModel
                {
                    TransactionId = !string.IsNullOrEmpty(id) ? id : "TXN-2026-0482",
                    BookingId = "BK-2026-084",
                    DateLogged = "Nov 08, 2026 - 02:30 PM",
                    ItemName = "Casio fx-991EX ClassWiz",
                    Serial = "CW-98218-PH",
                    BorrowerName = "Maria Santos",
                    BorrowerId = "2023-0455-SB",
                    BorrowerCourse = "BS Information Technology",
                    OwnerName = "Juan Dela Cruz",
                    OwnerId = "2024-0022-SB",
                    OwnerCourse = "BS Computer Science",
                    OwnerRating = "4.9 ★ (28 rentals)",
                    BaseFee = "PHP 120.00",
                    LateFee = "PHP 30.00",
                    TotalAmount = "PHP 150.00",
                    GCashRef = "GC-9018-2948-1029",
                    Status = "Awaiting Owner Confirmation",
                    Method = "GCash"
                };
            }

            return PartialView("TransactionDetails", details);
        }
    }
}
