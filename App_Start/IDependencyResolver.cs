using System;
using System.Collections.Generic;

namespace AngularJSProofofConcept
{
    public interface IDependencyResolver
    {
        IEnumerable<object> GetServices(Type serviceType);
    }
}
