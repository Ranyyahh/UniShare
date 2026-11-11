using System.Web.Optimization;

namespace UniShare
{
    public class BundleConfig
    {
        public static void RegisterBundles(BundleCollection bundles)
        {
            bundles.Add(new ScriptBundle("~/bundles/jquery").Include(
                        "~/Scripts/jquery-{version}.js"));

            bundles.Add(new ScriptBundle("~/bundles/jqueryval").Include(
                        "~/Scripts/jquery.validate.js",
                        "~/Scripts/jquery.validate.unobtrusive.js"));

            bundles.Add(new ScriptBundle("~/bundles/modernizr").Include(
                        "~/Scripts/modernizr-*"));

            // Plain Bundle (no minifier): WebGrease cannot parse Bootstrap 6's modern CSS.
            // Bootstrap 6 alpha JS is an ES module, so it is intentionally NOT bundled.
            bundles.Add(new Bundle("~/Content/css").Include(
                        "~/Content/bootstrap.css",
                        "~/Content/Site.css",
                        "~/Content/unishare-popups.css",
                        "~/Content/admin-sidebar.css"));
        }
    }
}
