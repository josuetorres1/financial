using System;
using System.Collections.Generic;
using System.Runtime.Remoting.Messaging;
using System.Web;

namespace AngularJSProofofConcept
{
    public class DomainEventScope : IDisposable
    {
        private const string ScopeKey = "__DomainEventScope";

        private readonly IList<IDomainEvent> _domainEvents = new List<IDomainEvent>();

        private bool _isDisposed;

        private DomainEventScope()
        {
            
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            CallOrHttpContextStorage.RemoveValue(ScopeKey);
        }

        public static DomainEventScope Start()
        {
            var scope = Instance;

            if (scope != null)
            {
                throw new InvalidOperationException("A scope may not be started if one already exists");
            }

            scope = new DomainEventScope();

            CallOrHttpContextStorage.SetValue(ScopeKey, scope);

            return scope;
        }

        public static DomainEventScope Instance
        {
            get { return (DomainEventScope)CallOrHttpContextStorage.GetValue(ScopeKey); }
        }

        public void RaiseAll(IDomainEventRaiser domainEventRaiser)
        {
            if (_isDisposed)
            {
                return;
            }

            Dispose();

            foreach (var domainEvent in _domainEvents)
            {
                domainEventRaiser.Raise(domainEvent);
            }
        }

        public void Queue(IDomainEvent domainEvent)
        {
            _domainEvents.Add(domainEvent);
        }
    }

    public static class CallOrHttpContextStorage
    {
        public static void SetValue(string key, object value)
        {
            if (HttpContext.Current != null)
            {
                HttpContext.Current.Items[key] = value;
            }
            else
            {
                CallContext.SetData(key, value);
            }
        }

        public static object GetValue(string key)
        {
            if (HttpContext.Current != null)
            {
                return HttpContext.Current.Items[key];
            }

            return CallContext.GetData(key);
        }

        public static void RemoveValue(string key)
        {
            if (HttpContext.Current != null)
            {
                HttpContext.Current.Items.Remove(key);
            }
            else
            {
                CallContext.FreeNamedDataSlot(key);
            }
        }
    }
}