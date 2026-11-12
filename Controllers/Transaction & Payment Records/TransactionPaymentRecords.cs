using System.Linq;
using System.Web.Mvc;
using UniShare.Services;

namespace UniShare.Controllers
{
    [Authorize(Roles = "ADMIN")]
    public class TransactionPaymentRecordsController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.Title = "Transaction and Payment Records";
            var transactions = PaymentManagementService.GetAdminHistory();
            ViewBag.TotalTransactions = transactions.Count;
            ViewBag.ConfirmedPayments = transactions.Count(x => x.Status == "PAID");
            ViewBag.PendingConfirmation = transactions.Count(x => x.Status == "UNPAID" && !string.IsNullOrWhiteSpace(x.SubmittedAt));
            ViewBag.TotalRecordedFees = "PHP " + transactions.Sum(x => x.Amount).ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
            return View("TransactionPaymentRecords", transactions);
        }

        public ActionResult TransactionPaymentRecords()
        {
            return Index();
        }

        [HttpGet]
        public ActionResult GetTransactionDetails(string id)
        {
            var details = PaymentManagementService.GetAdminPaymentDetails(id);
            return details == null ? (ActionResult)HttpNotFound() : PartialView("TransactionDetails", details);
        }
    }
}
