using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Web.Mvc;

namespace UniShare.Controllers
{
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

            var transactions = new List<dynamic>();

            // Record 1
            dynamic t1 = new ExpandoObject();
            t1.TransactionId = "TXN-2026-0482";
            t1.BookingId = "BK-2026-084";
            t1.ItemName = "Casio fx-991EX ClassWiz";
            t1.BorrowerName = "Maria Santos";
            t1.OwnerName = "Juan Dela Cruz";
            t1.BaseFee = "PHP 120.00";
            t1.LateFee = "PHP 30.00";
            t1.TotalAmount = "PHP 150.00";
            t1.Status = "Awaiting Owner Confirmation";
            t1.Method = "GCash";
            t1.DateRange = "Today";
            transactions.Add(t1);

            // Record 2
            dynamic t2 = new ExpandoObject();
            t2.TransactionId = "TXN-2026-0479";
            t2.BookingId = "BK-2026-075";
            t2.ItemName = "Arduino Uno R3 Ultimate Kit";
            t2.BorrowerName = "Kenneth Ramirez";
            t2.OwnerName = "Angelo Reyes";
            t2.BaseFee = "PHP 280.00";
            t2.LateFee = "PHP 50.00";
            t2.TotalAmount = "PHP 330.00";
            t2.Status = "Awaiting Owner Confirmation";
            t2.Method = "GCash";
            t2.DateRange = "Today";
            transactions.Add(t2);

            // Record 3
            dynamic t3 = new ExpandoObject();
            t3.TransactionId = "TXN-2026-0465";
            t3.BookingId = "BK-2026-064";
            t3.ItemName = "Staedtler Mars 700 Technical Pen";
            t3.BorrowerName = "Chloe Anne Cruz";
            t3.OwnerName = "Patricia Gomez";
            t3.BaseFee = "PHP 120.00";
            t3.LateFee = "PHP 0.00";
            t3.TotalAmount = "PHP 120.00";
            t3.Status = "Confirmed";
            t3.Method = "Maya";
            t3.DateRange = "Last 7 Days";
            transactions.Add(t3);

            // Record 4
            dynamic t4 = new ExpandoObject();
            t4.TransactionId = "TXN-2026-0451";
            t4.BookingId = "BK-2026-052";
            t4.ItemName = "TI-84 Plus CE Graphing Calculator";
            t4.BorrowerName = "Jeric Alcantara";
            t4.OwnerName = "Jerome Bautista";
            t4.BaseFee = "PHP 350.00";
            t4.LateFee = "PHP 75.00";
            t4.TotalAmount = "PHP 425.00";
            t4.Status = "Awaiting Owner Confirmation";
            t4.Method = "GCash";
            t4.DateRange = "Last 7 Days";
            transactions.Add(t4);

            // Record 5
            dynamic t5 = new ExpandoObject();
            t5.TransactionId = "TXN-2026-0440";
            t5.BookingId = "BK-2026-041";
            t5.ItemName = "Canon EOS 3000D DSLR Camera Kit";
            t5.BorrowerName = "Bea Patricia Reyes";
            t5.OwnerName = "Mark Joshua Tan";
            t5.BaseFee = "PHP 750.00";
            t5.LateFee = "PHP 0.00";
            t5.TotalAmount = "PHP 750.00";
            t5.Status = "Confirmed";
            t5.Method = "GCash";
            t5.DateRange = "Last 7 Days";
            transactions.Add(t5);

            return View(transactions);
        }

        [HttpGet]
        public ActionResult GetTransactionDetails(string id)
        {
            dynamic details = new ExpandoObject();

            if (id == "TXN-2026-0479")
            {
                details.TransactionId = "TXN-2026-0479";
                details.BookingId = "BK-2026-075";
                details.DateLogged = "Nov 08, 2026 - 11:15 AM";
                details.ItemName = "Arduino Uno R3 Ultimate Kit";
                details.Serial = "ARD-UNO-48192";
                details.BorrowerName = "Kenneth Ramirez";
                details.BorrowerId = "2023-0112-SB";
                details.BorrowerCourse = "BS Electronics Engineering";
                details.OwnerName = "Angelo Reyes";
                details.OwnerId = "2024-0089-SB";
                details.OwnerCourse = "BS Information Technology";
                details.OwnerRating = "4.8 ★ (19 rentals)";
                details.BaseFee = "PHP 280.00";
                details.LateFee = "PHP 50.00";
                details.TotalAmount = "PHP 330.00";
                details.GCashRef = "GC-8832-1904-7721";
                details.Status = "Awaiting Owner Confirmation";
                details.Method = "GCash";
            }
            else if (id == "TXN-2026-0465")
            {
                details.TransactionId = "TXN-2026-0465";
                details.BookingId = "BK-2026-064";
                details.DateLogged = "Nov 07, 2026 - 04:45 PM";
                details.ItemName = "Staedtler Mars 700 Technical Pen";
                details.Serial = "STM-700-0918";
                details.BorrowerName = "Chloe Anne Cruz";
                details.BorrowerId = "2023-0891-SB";
                details.BorrowerCourse = "BS Architecture";
                details.OwnerName = "Patricia Gomez";
                details.OwnerId = "2024-0312-SB";
                details.OwnerCourse = "BS Architecture";
                details.OwnerRating = "5.0 ★ (34 rentals)";
                details.BaseFee = "PHP 120.00";
                details.LateFee = "PHP 0.00";
                details.TotalAmount = "PHP 120.00";
                details.GCashRef = "MY-6641-0982-3341";
                details.Status = "Confirmed";
                details.Method = "Maya";
            }
            else if (id == "TXN-2026-0451")
            {
                details.TransactionId = "TXN-2026-0451";
                details.BookingId = "BK-2026-052";
                details.DateLogged = "Nov 06, 2026 - 10:20 AM";
                details.ItemName = "TI-84 Plus CE Graphing Calculator";
                details.Serial = "TI84-99120-CE";
                details.BorrowerName = "Jeric Alcantara";
                details.BorrowerId = "2022-0941-SB";
                details.BorrowerCourse = "BS Civil Engineering";
                details.OwnerName = "Jerome Bautista";
                details.OwnerId = "2023-0518-SB";
                details.OwnerCourse = "BS Mechanical Engineering";
                details.OwnerRating = "4.7 ★ (15 rentals)";
                details.BaseFee = "PHP 350.00";
                details.LateFee = "PHP 75.00";
                details.TotalAmount = "PHP 425.00";
                details.GCashRef = "GC-7129-5021-9984";
                details.Status = "Awaiting Owner Confirmation";
                details.Method = "GCash";
            }
            else if (id == "TXN-2026-0440")
            {
                details.TransactionId = "TXN-2026-0440";
                details.BookingId = "BK-2026-041";
                details.DateLogged = "Nov 05, 2026 - 01:10 PM";
                details.ItemName = "Canon EOS 3000D DSLR Camera Kit";
                details.Serial = "CN-3000D-5512A";
                details.BorrowerName = "Bea Patricia Reyes";
                details.BorrowerId = "2023-0667-SB";
                details.BorrowerCourse = "BA Communication";
                details.OwnerName = "Mark Joshua Tan";
                details.OwnerId = "2022-0331-SB";
                details.OwnerCourse = "BS Information Technology";
                details.OwnerRating = "4.9 ★ (42 rentals)";
                details.BaseFee = "PHP 750.00";
                details.LateFee = "PHP 0.00";
                details.TotalAmount = "PHP 750.00";
                details.GCashRef = "GC-5519-3382-4102";
                details.Status = "Confirmed";
                details.Method = "GCash";
            }
            else
            {
                // Default: TXN-2026-0482 (Casio fx-991EX ClassWiz)
                details.TransactionId = !string.IsNullOrEmpty(id) ? id : "TXN-2026-0482";
                details.BookingId = "BK-2026-084";
                details.DateLogged = "Nov 08, 2026 - 02:30 PM";
                details.ItemName = "Casio fx-991EX ClassWiz";
                details.Serial = "CW-98218-PH";
                details.BorrowerName = "Maria Santos";
                details.BorrowerId = "2023-0455-SB";
                details.BorrowerCourse = "BS Information Technology";
                details.OwnerName = "Juan Dela Cruz";
                details.OwnerId = "2024-0022-SB";
                details.OwnerCourse = "BS Computer Science";
                details.OwnerRating = "4.9 ★ (28 rentals)";
                details.BaseFee = "PHP 120.00";
                details.LateFee = "PHP 30.00";
                details.TotalAmount = "PHP 150.00";
                details.GCashRef = "GC-9018-2948-1029";
                details.Status = "Awaiting Owner Confirmation";
                details.Method = "GCash";
            }

            return PartialView("_TransactionDetails", (object)details);
        }
    }
}

namespace UniShare.Controllers.Transaction___Payment_Records
{
    // Alias to support legacy / alternative namespace references
    public class TransactionPaymentRecords : UniShare.Controllers.TransactionPaymentRecordsController
    {
    }
}
