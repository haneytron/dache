using System.Web.Mvc;

namespace Dache.Board.Controllers
{
    /// <summary>
    /// Controls board related actions.
    /// </summary>
    public class BoardController : Controller
    {
        /// <summary>
        /// Creates the index view.
        /// </summary>
        /// <returns>BoardController index view.</returns>
        public ActionResult Index()
        {
            return View();
        }
    }
}
