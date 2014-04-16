using System.Web.Mvc;
using System.Web.Routing;
using Dache.Board.Configuration;
using Dache.Board.Custom.ActionAttributes;

namespace Dache.Board
{
    // Note: For instructions on enabling IIS6 or IIS7 classic mode, 
    // visit http://go.microsoft.com/?LinkId=9394801

    /// <summary>
    /// An MVC application.
    /// </summary>
    public class MvcApplication : System.Web.HttpApplication
    {
        /// <summary>
        /// Registers global filters.
        /// </summary>
        /// <param name="filters">The global filters.</param>
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            // Create the handle error attribute for MVC error handling
            var handleErrorAttribute = new HandleErrorAttribute
            {
                // No Type specification == handle all exceptions
                View = "Exception",
            };

            filters.Add(handleErrorAttribute);

            // Create a new action attribute that sets the ViewBag PageTitle based on the route parameters
            filters.Add(new PageTitleAttribute());
        }

        /// <summary>
        /// Registers routes.
        /// </summary>
        /// <param name="routes">The routes.</param>
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
                name: "Home",
                url: string.Empty,
                defaults: new { controller = "Board", action = "Index", pageTitle = "What's Happening" });

            routes.MapRoute(
                "AJAX Cache Info", // Route name
                "ajax/cacheinfo", // URL with parameters
                new { controller = "AjaxCacheInfo", action = "CacheInfo" }); // Parameter defaults

            routes.MapRoute(
                name: "404",
                url: "{*anything}",
                defaults: new { controller = "Error", action = "NotFound", pageTitle = "Not Found" });
        }

        /// <summary>
        /// Called on application start.
        /// </summary>
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();

            RegisterGlobalFilters(GlobalFilters.Filters);
            RegisterRoutes(RouteTable.Routes);

            // Create the board to manager client
            var address = DacheboardConfigurationSection.Settings.Address;
            var port = DacheboardConfigurationSection.Settings.Port;
            var managerReconnectIntervalMilliseconds = DacheboardConfigurationSection.Settings.ManagerReconnectIntervalMilliseconds;

            // Build the endpoint address
            var endpointAddressFormattedString = "net.tcp://{0}:{1}/Dache/Dacheboard";
            var managerAddress = string.Format(endpointAddressFormattedString, address, port);

            // TODO: initialize dache board cache host client here?
            // BoardToManagerClientContainer.Instance = new BoardToManagerClient(managerAddress, managerReconnectIntervalMilliseconds);
        }
    }
}