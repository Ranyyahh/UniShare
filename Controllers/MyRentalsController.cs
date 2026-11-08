using System.Linq;
using System.Web.Mvc;
using UniShare.Models;

namespace UniShare.Controllers
{
    public class MyRentalsController : Controller
    {
        // /MyRentals?tab=Active|History
        public ActionResult Index(string tab)
        {
            tab = tab == "History" ? "History" : "Active";
            ViewBag.Tab = tab;
            var list = tab == "Active"
                ? SampleData.MyRentals.Where(r => r.Status != "Completed").ToList()
                : SampleData.MyRentals.Where(r => r.Status == "Completed").ToList();
            return View(list);
        }

        // POST /MyRentals/Cancel/2
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Cancel(int id)
        {
            var r = SampleData.MyRentals.FirstOrDefault(x => x.Id == id);
            if (r == null) return HttpNotFound();
            if (r.Status != "Pending") { TempData["Error"] = "Only pending requests can be cancelled."; return RedirectToAction("Index"); }
            SampleData.MyRentals.Remove(r);
            TempData["Success"] = "Request cancelled.";
            return RedirectToAction("Index");
        }
    }
}
