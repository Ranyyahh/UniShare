using System.Web;
using System.Web.Mvc;
using UniShare.Services;

namespace UniShare
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
            filters.Add(new AccountRestrictionFilter());
        }
    }
}
