using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Security;
using System.Security.Principal;
using System.Threading;
using UniShare.Services;

namespace UniShare
{
    public class MvcApplication : System.Web.HttpApplication
    {
        private static Timer _bookingExpiryTimer;
        protected void Application_AuthenticateRequest()
        {
            var cookie = Request.Cookies[FormsAuthentication.FormsCookieName];
            if (cookie == null || String.IsNullOrEmpty(cookie.Value)) return;
            try
            {
                var ticket = FormsAuthentication.Decrypt(cookie.Value);
                if (ticket == null || ticket.Expired) return;
                var identity = new FormsIdentity(ticket);
                var rawRole = (ticket.UserData ?? "STUDENT").Trim().ToUpperInvariant();
                var rolesList = new List<string> { rawRole };
                if (rawRole == "STUDENT" || rawRole == "OWNER" || rawRole == "BORROWER")
                {
                    if (!rolesList.Contains("STUDENT")) rolesList.Add("STUDENT");
                    if (!rolesList.Contains("OWNER")) rolesList.Add("OWNER");
                    if (!rolesList.Contains("BORROWER")) rolesList.Add("BORROWER");
                }
                else if (rawRole == "ADMIN")
                {
                    if (!rolesList.Contains("ADMIN")) rolesList.Add("ADMIN");
                    if (!rolesList.Contains("STUDENT")) rolesList.Add("STUDENT");
                    if (!rolesList.Contains("OWNER")) rolesList.Add("OWNER");
                    if (!rolesList.Contains("BORROWER")) rolesList.Add("BORROWER");
                }
                Context.User = new GenericPrincipal(identity, rolesList.ToArray());
            }
            catch { FormsAuthentication.SignOut(); }
        }

        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            // Expire stale requests even when no rental page is being viewed. IIS must be configured
            // for AlwaysRunning with preload enabled for this in-process job to remain active.
            _bookingExpiryTimer = new Timer(_ =>
            {
                try { BookingService.ExpirePending(); }
                catch { /* Database outages are retried on the next interval. */ }
            }, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));
        }

        protected void Application_End() { if (_bookingExpiryTimer != null) _bookingExpiryTimer.Dispose(); }
    }
}
