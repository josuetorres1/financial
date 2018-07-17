using System;
using System.Collections.Generic;
using Ninject;

namespace AngularJSProofofConcept
{
    public class NinjectArtezDependencyResolver : IDependencyResolver
    {
        private readonly IKernel _kernel;

        public NinjectArtezDependencyResolver(IKernel kernel)
        {
            _kernel = kernel;
        }

        public IEnumerable<object> GetServices(Type serviceType)
        {
            return _kernel.GetAll(serviceType);
        }
    }
}
