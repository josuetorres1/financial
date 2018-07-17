using System;

namespace AngularJSProofofConcept
{
    public interface IArtezSession : IDisposable
    {
        IUnitOfWorkFactory CreateUnitOfWorkFactory();
    }
}