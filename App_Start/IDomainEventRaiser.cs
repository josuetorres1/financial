namespace AngularJSProofofConcept
{
    public interface IDomainEventRaiser
    {
        void Raise(IDomainEvent args);
    }
}