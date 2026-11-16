using System;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;
using MongoDB.Bson;
using MongoDB.Driver;
using UniShare.Controllers;

namespace UniShare.Services
{
    public class AccountRestrictionFilter : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var principal = filterContext.HttpContext.User;
            if (principal == null || !principal.Identity.IsAuthenticated || principal.IsInRole("ADMIN")) return;
            ObjectId userId;
            if (!ObjectId.TryParse(principal.Identity.Name, out userId)) return;
            try
            {
                if (!AdminAccountabilityService.IsUserRestricted(userId))
                {
                    AccountController.RefreshUserSession(filterContext.HttpContext.Session, principal.Identity.Name);
                    return;
                }
                FormsAuthentication.SignOut();
                filterContext.Result = new RedirectToRouteResult(new RouteValueDictionary(new { controller = "Account", action = "Restricted" }));
            }
            catch (MongoException)
            {
                // Other secured pages still report their own database availability errors.
            }
        }
    }
}
