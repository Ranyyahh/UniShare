using System.Linq;
using System.Web.Mvc;
using UniShare.Models;

namespace UniShare.Controllers
{
    public class DashboardController : Controller
    {
        // /Dashboard
        public ActionResult Index()
        {
            var vm = new DashboardViewModel
            {
                ActiveListings = SampleData.MyListings.Count(i => i.Status != "Hidden"),
                ActiveRentals = SampleData.MyRentals.Count(r => r.Status == "Ongoing" || r.Status == "Approved"),
                BorrowedCount = SampleData.Borrowed.Count(b => b.Status == "Ongoing"),
                Earnings = SampleData.Sales.Where(s => s.Status == "Paid").Sum(s => s.Amount),
                Recent = SampleData.Notifications.Take(3).ToList()
            };
            return View(vm);
        }
    }
}
