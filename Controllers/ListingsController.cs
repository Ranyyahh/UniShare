using System.Web.Mvc;

namespace UniShare.Controllers
{
    // Inherits from MyListingsController so both /MyListings and /Listings routes work interchangeably
    public class ListingsController : MyListingsController
    {
    }
}
