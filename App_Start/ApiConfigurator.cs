using System.Web.Http;
using System.Web.Routing;
using RouteParameter = System.Web.Http.RouteParameter;

namespace AngularJSProofofConcept
{
    public class ApiConfigurator
    {
        public static void Configure(HttpConfiguration configuration)
        {
            MapRoutes(configuration.Routes);
        }

        private static void MapRoutes(HttpRouteCollection httpRouteCollection)
        {
            httpRouteCollection.MapHttpRoute(
                null,
                "Api/{controller}/Post/{id}",
                new { action = "Post", id = RouteParameter.Optional },
                new { httpMethod = new HttpMethodConstraint("POST") });

            httpRouteCollection.MapHttpRoute(
                null,
                "Api/{controller}/UpdateBalanceData",
                new { action = "UpdateBalanceData", id = RouteParameter.Optional },
                new { httpMethod = new HttpMethodConstraint("POST") });

            httpRouteCollection.MapHttpRoute(
                "BalanceApi",
                "Api/{controller}/{id}",
                new { id = RouteParameter.Optional },
                new { httpMethod = new HttpMethodConstraint("GET") });
        }
    }
}
