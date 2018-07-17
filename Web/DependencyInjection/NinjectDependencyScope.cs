using System;
using System.Collections.Generic;
using System.Web.Http.Dependencies;
using Ninject;
using Ninject.Syntax;

namespace Artez.Web.DependencyInjection
{
    /// <summary>
    /// Provides a Ninject implementation of IDependencyScope which resolves 
    /// services using the NinjectContainer.
    /// </summary>
    /// <remarks>
    /// Credit: http://www.peterprovost.org/blog/2012/06/19/adding-ninject-to-web-api
    /// </remarks>
    public class NinjectDependencyScope : IDependencyScope
    {
        private readonly IResolutionRoot _resolver;
        private readonly bool _shouldDispose;

        internal NinjectDependencyScope(IResolutionRoot resolver, bool shouldDispose)
        {
            if (resolver == null)
            {
                throw new ArgumentNullException("resolver");
            }

            _resolver = resolver;
            _shouldDispose = shouldDispose;
        }

        public void Dispose()
        {
            if (_shouldDispose)
            {
                var disposable = _resolver as IDisposable;

                if (disposable != null)
                {
                    disposable.Dispose();
                }                
            }
        }

        public object GetService(Type serviceType)
        {
            return _resolver.TryGet(serviceType);
        }

        public IEnumerable<object> GetServices(Type serviceType)
        {
            return _resolver.GetAll(serviceType);
        }
    }
}