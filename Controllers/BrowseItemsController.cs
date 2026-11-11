using System.Web.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using UniShare.Models;
using UniShare.Services;

namespace UniShare.Controllers
{
    public class BrowseItemsController : Controller
    {
        // /BrowseItems  ->  Views/BrowseItems/Index.cshtml
        // (If your controller already has an Index action, keep yours.)
        public ActionResult Index()
        {
            return View(new BrowseItemsViewModel());
        }

        // /BrowseItems/ItemDetails?id=1  ->  Views/BrowseItems/ItemDetails.cshtml
        public ActionResult ItemDetails(int id)
        {
            var authenticated = User != null && User.Identity != null && User.Identity.IsAuthenticated;
            var verified = false;
            if (authenticated)
            {
                try
                {
                    var user = MongoDatabase.Users.Find(x => x.Id == ObjectId.Parse(User.Identity.Name)).FirstOrDefault();
                    verified = user != null && user.VerificationStatus == "APPROVED";
                }
                catch { verified = false; }
            }
            ViewBag.IsAuthenticated = authenticated;
            ViewBag.IsStudentVerified = verified;
            var item = SampleItemData.Get(id);   // TODO: load from your database
            if (item == null) return HttpNotFound();
            return View(item);
        }
    }
}
