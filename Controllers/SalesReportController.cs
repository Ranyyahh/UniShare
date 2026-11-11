using System.Linq;
using System.Web.Mvc;
using UniShare.Models;

namespace UniShare.Controllers
{
    [Authorize]
    public class SalesReportController : Controller
    {
        // /SalesReport
        public ActionResult Index()
        {
            var vm = new SalesReportViewModel
            {
                Sales = SampleData.Sales.OrderByDescending(s => s.Date).ToList(),
                TotalEarned = SampleData.Sales.Where(s => s.Status == "Paid").Sum(s => s.Amount),
                Pending = SampleData.Sales.Where(s => s.Status == "Pending").Sum(s => s.Amount),
                Transactions = SampleData.Sales.Count
            };
            return View(vm);
        }
    }
}
