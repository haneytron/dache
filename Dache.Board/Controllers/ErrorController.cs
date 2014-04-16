using System.Web.Mvc;

namespace Dache.Board.Controllers
{
    /// <summary>
    /// Controls error actions.
    /// </summary>
    public class ErrorController : Controller
    {
        /// <summary>
        /// Creates the 404 not found view.
        /// </summary>
        /// <returns>The error view.</returns>
        public ActionResult NotFound()
        {
            return View();
        }
    }
}
