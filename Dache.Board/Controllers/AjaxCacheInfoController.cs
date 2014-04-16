using System.Web.Mvc;
using Dache.Board.Handlers;

namespace Dache.Board.Controllers
{
    /// <summary>
    /// Controls AJAX cache info actions.
    /// </summary>
    public class AjaxCacheInfoController : Controller
    {
        /// <summary>
        /// Creates a cache info JSON object.
        /// </summary>
        /// <returns>The response in form of a JSON.</returns>
        public JsonResult CacheInfo()
        {
            // Get the cache info
            var result = CacheInfoHandler.GetCacheInfo();
            return Json(result, JsonRequestBehavior.AllowGet);
        }
    }
}
