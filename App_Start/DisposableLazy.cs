using System;
using System.Threading;

namespace Artez.Core
{
    public sealed class DisposableLazy<T> : Lazy<T>, IDisposable where T : IDisposable
    {
        public DisposableLazy()
        {
            
        }

        public DisposableLazy(bool isThreadSafe) 
            : base(isThreadSafe)
        {
            
        }

        public DisposableLazy(Func<T> valueFactory) 
            : base(valueFactory)
        {
            
        }

        public DisposableLazy(LazyThreadSafetyMode mode) 
            : base(mode)
        {
            
        }

        public DisposableLazy(Func<T> valueFactory, bool isThreadSafe) 
            : base(valueFactory, isThreadSafe)
        {
            
        }

        public DisposableLazy(Func<T> valueFactory, LazyThreadSafetyMode mode) 
            : base(valueFactory, mode)
        {
            
        }

        public void Dispose()
        {
            if (IsValueCreated)
            {
                Value.Dispose();
            }
        }
    }
}
