using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Description;
using AngularJSProofofConcept.Models;
using Core.Models;

namespace AngularJSProofofConcept.Controllers
{
    public class BalanceApiController : ApiController
    {
        private readonly IUnitOfWorkFactory _unitOfWorkFactory;

        public BalanceApiController(IUnitOfWorkFactory unitOfWorkFactory)
        {
            _unitOfWorkFactory = unitOfWorkFactory;
        }

        [ResponseType(typeof(IList<BalanceObj>))]
        public HttpResponseMessage Get()
        {
            using (var unitofWork = _unitOfWorkFactory.Create())
            {
                var d = unitofWork.BalanceRepository.GetData();

                return Request.CreateResponse(HttpStatusCode.OK,
                    unitofWork.BalanceRepository.RenderJson(d));
            }
        }

        [HttpPost]
        public IHttpActionResult UpdateBalanceData([FromBody] BalanceInput input) 
        {
            if (!ModelState.IsValid) return null;

            using (var unitofWork = _unitOfWorkFactory.Create())
            {
                unitofWork.BalanceRepository.Update((BalanceObj)input);
            }

            return
                Redirect(
                    new Uri(ControllerContext.Request.RequestUri.Host + ":" +
                            ControllerContext.Request.RequestUri.Port + "/Balance/RouteBalances"));
        }
    }
}
