using System.Web.Mvc;
using Core;

namespace AngularJSProofofConcept.Controllers
{
    public class BalanceController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult RouteBalances()
        {
            return View();
        }

        public ActionResult RouteBalance(string balance)
        {
            return View(balance);
        }

        public ActionResult Update(string id)
        {
            var br = new BalanceRepository();
            var found = br.Find(id);
            br.Update(found);

            return RedirectToAction("RouteBalances");
        }
    }
}
