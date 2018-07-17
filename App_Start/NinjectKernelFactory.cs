using Ninject;

namespace AngularJSProofofConcept
{
    internal class NinjectKernelFactory : NinjectKernelFactoryTemplate
    {
        protected override void RegisterServices(IKernel kernel)
        {
            UnitOfWorkDependencies.BindToKernel(kernel);

            kernel.Bind<NinjectArtezDependencyResolver>().ToMethod(ctx => new NinjectArtezDependencyResolver(kernel));
        }
    }
}