using System;
using System.Web.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using UniShare.Models;

namespace UniShare.Services
{
    public sealed class VerifiedStudentAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var http = filterContext.HttpContext;
            if (http.User == null || http.User.Identity == null || !http.User.Identity.IsAuthenticated)
            {
                var url = new UrlHelper(filterContext.RequestContext).Action("Login", "Account", new { returnUrl = http.Request.RawUrl });
                filterContext.Result = new RedirectResult(url);
                return;
            }
            try
            {
                var userId = ObjectId.Parse(http.User.Identity.Name);
                var user = MongoDatabase.Users.Find(x => x.Id == userId).FirstOrDefault();
                if (user == null || user.VerificationStatus != "APPROVED")
                    filterContext.Result = new RedirectToRouteResult(new System.Web.Routing.RouteValueDictionary(new { controller = "Verification", action = "Status" }));
            }
            catch (Exception ex) when (ex is MongoException || ex is InvalidOperationException || ex is FormatException)
            { filterContext.Result = new HttpStatusCodeResult(503, "Student verification is temporarily unavailable."); }
        }
    }
}
