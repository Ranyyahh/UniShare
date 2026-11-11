using System.Web.Mvc;
using System.Web.Routing;

namespace UniShare
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            // Friendly URLs for the navbar pages
            routes.MapRoute("Dashboard",     "dashboard",      new { controller = "Dashboard",     action = "Index" });
            routes.MapRoute("BrowseItems",   "browse",         new { controller = "BrowseItems",   action = "Index" });
            routes.MapRoute("MyListings",    "my-listings",    new { controller = "MyListings",    action = "Index" });
            routes.MapRoute("MyRentals",     "my-rentals",     new { controller = "MyRentals",     action = "Index" });
            routes.MapRoute("BorrowedItems", "borrowed-items", new { controller = "BorrowedItems", action = "Index" });
            routes.MapRoute("SalesReport",   "sales-report",   new { controller = "SalesReport",   action = "Index" });
            routes.MapRoute("Messages",      "messages",       new { controller = "Messages",      action = "Index" });
            routes.MapRoute("Notifications", "notifications",  new { controller = "Notifications", action = "Index" });
            routes.MapRoute("Login",         "login",          new { controller = "Account",       action = "Login" });

            // Fallback: /{controller}/{action}/{id}
            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
