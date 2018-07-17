using System;

namespace AngularJSProofofConcept
{
    public static class DomainEvents
    {
        public static void Queue(IDomainEvent @event)
        {
            if (@event == null)
            {
                throw new ArgumentNullException("event");
            }

            var scope = DomainEventScope.Instance;

            if (scope != null)
            {
                scope.Queue(@event);
            }
        }
    }
}