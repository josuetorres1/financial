namespace AngularJSProofofConcept
{
    public interface IDomainEventHandler<in T>
    {
        void Handle(T e);
    }
}
