using System;

namespace AngularJSProofofConcept
{
    public interface ICommittable : IDisposable
    {
        void Commit();
    }
}