using System.Web.Http;
using AngularJSProofofConcept;
using Ninject.Web.Common;

[assembly: WebActivator.PreApplicationStartMethod(typeof(NinjectConfigurator), "Start")]
[assembly: WebActivator.ApplicationShutdownMethodAttribute(typeof(NinjectConfigurator), "Stop")]

namespace AngularJSProofofConcept
{
    internal static class NinjectConfigurator
    {
        private static readonly Bootstrapper Bootstrapper = new Bootstrapper();

        public static void Start()
        {
            Bootstrapper.Initialize(new NinjectKernelFactory().Create);

            GlobalConfiguration.Configuration.DependencyResolver = new NinjectDependencyResolver(Bootstrapper.Kernel);
        }

        public static void Stop()
        {
            Bootstrapper.ShutDown();
        }
    }
}