using System;
using System.Collections.Generic;

namespace AngularJSProofofConcept
{
    public class DomainEventRaiser : IDomainEventRaiser
    {
        private readonly IDependencyResolver _dependencyResolver;

        public DomainEventRaiser(IDependencyResolver dependencyResolver)
        {
            if (dependencyResolver == null)
            {
                throw new ArgumentNullException("dependencyResolver");
            }

            _dependencyResolver = dependencyResolver;
        }

        public void Raise(IDomainEvent args)
        {
            foreach (var handler in GetEventHandlers(args.GetType()))
            {
                InvokeHandler(handler, args);
            }
        }

        private IEnumerable<object> GetEventHandlers(Type argType)
        {
            var serviceType = typeof (IDomainEventHandler<>).MakeGenericType(argType);

            return _dependencyResolver.GetServices(serviceType);
        }

        private static void InvokeHandler(object handler, IDomainEvent args)
        {
            var method = handler
                .GetType()
                .GetMethod("Handle");

            method.Invoke(handler, new object[] {args});
        }
    }
}