using System;

namespace AngularJSProofofConcept
{
    public interface IArtezSessionFactory : IDisposable
    {
        IArtezSession Create();
    }
}