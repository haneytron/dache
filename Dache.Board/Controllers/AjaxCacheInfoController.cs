using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Dache.Board.Handlers;

namespace Dache.Board.Controllers
{
    public class AjaxCacheInfoController : Controller
    {
        public JsonResult CacheInfo()
        {
            // Get the cache info
            var result = CacheInfoHandler.GetCacheInfo();
            return Json(result, JsonRequestBehavior.AllowGet);
        }
    }
}
