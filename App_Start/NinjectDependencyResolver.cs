using System.Web.Http.Dependencies;
using Ninject;

namespace AngularJSProofofConcept
{
    public class NinjectDependencyResolver : NinjectDependencyScope, IDependencyResolver, System.Web.Http.Dependencies.IDependencyResolver
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

        System.Collections.Generic.IEnumerable<object> IDependencyResolver.GetServices(System.Type serviceType)
        {
            return _kernel.GetAll(serviceType);
        }
    }
}