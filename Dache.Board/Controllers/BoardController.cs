using System.Web.Mvc;

namespace Dache.Board.Controllers
{
    public class BoardController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }
    }
}
