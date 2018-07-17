using System.Web.Http.Dependencies;
using Ninject;

namespace Artez.Web.DependencyInjection
{
    public class NinjectDependencyResolver : NinjectDependencyScope, IDependencyResolver
    {
        private readonly IKernel _kernel;

        public NinjectDependencyResolver(IKernel kernel) 
            : base(kernel, false)
        {
            _kernel = kernel;
        }

        public IDependencyScope BeginScope()
        {
            return new NinjectDependencyScope(_kernel.BeginBlock(), true);
        }
    }
}