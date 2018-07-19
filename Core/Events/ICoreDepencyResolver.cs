using System;
using System.Collections.Generic;

namespace Core.Events
{
    public interface ICoreDependencyResolver
    {
        IEnumerable<object> GetServices(Type serviceType);
    }
}
