using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using Core;

namespace AngularJSProofofConcept
{
    public class MvcConfigurator
    {
        public void Configure()
        {
            ConfigureRoutes();

            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }

        private void ConfigureRoutes()
        {
            var artezSessionFactory = DependencyResolver.Current.GetService<IBalanceSessionFactory>();

            var routeConfigurator = new RouteConfig(artezSessionFactory);

            RouteConfig.Configure(RouteTable.Routes);
        }
    }
}
