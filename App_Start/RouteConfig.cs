using System.Web.Mvc;
using System.Web.Routing;
using Core;

namespace AngularJSProofofConcept
{
    public class RouteConfig
    {
        private const string DefaultController = "Balance";
        private const string DefaultAction = "Index";

        private static IBalanceSessionFactory _artezSessionFactory;

        public RouteConfig(IBalanceSessionFactory artezSessionFactory)
        {
            _artezSessionFactory = artezSessionFactory;
        }

        public static void Configure(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
            
            ConfigureRegistrationAreaRoutes(routes);
        }

        private static void ConfigureRegistrationAreaRoutes(RouteCollection routes)
        {
            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new {controller = "Balance", action = "Index", id = UrlParameter.Optional}
                );

            routes.MapRoute(
                null,
                "RouteBalances",
                new { controller = DefaultController, action = "RouteBalances" }
            );

            routes.MapRoute(
                "RouteBalance",
                "RouteBalance",
                new { controller = DefaultController, action = "RouteBalance" }
            );

            routes.MapRoute(
                null,
                "{controller}/Update/{id}",
                new { controller = DefaultController, action = "Update", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                "Balances",
                "Api/{controller}/{action}/{id}",
                new { Controller = DefaultController, Action = DefaultAction, id = UrlParameter.Optional });
        }
    }
}