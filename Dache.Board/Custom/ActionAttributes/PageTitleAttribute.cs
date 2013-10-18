using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Dache.Board.Custom.ActionAttributes
{
    /// <summary>
    /// Sets the page title based on route data.
    /// </summary>
    public class PageTitleAttribute : ActionFilterAttribute
    {
        /// <summary>
        /// Occurs when an action is executing.
        /// </summary>
        /// <param name="filterContext">The filter context.</param>
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            // Set the ViewBag PageTitle to the Route parameter pageTitle... If that doesn't exist, Route Action
            object pageTitle = null;
            if (!filterContext.RouteData.Values.TryGetValue("pageTitle", out pageTitle))
            {
                // If the pageTitle route parameter was missing, use Route Action
                pageTitle = filterContext.RouteData.Values["action"];
            }

            filterContext.Controller.ViewBag.PageTitle = pageTitle;
        }
    }
}