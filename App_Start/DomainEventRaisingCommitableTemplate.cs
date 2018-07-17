namespace AngularJSProofofConcept
{
    public abstract class DomainEventRaisingCommitableTemplate : ICommittable
    {
        private readonly IDomainEventRaiser _domainEventRaiser;
        private readonly DomainEventScope _domainEventScope;

        protected DomainEventRaisingCommitableTemplate(IDomainEventRaiser domainEventRaiser)
        {
            _domainEventRaiser = domainEventRaiser;
            _domainEventScope = DomainEventScope.Start();
        }

        public void Commit()
        {
            DoCommit();

            _domainEventScope.RaiseAll(_domainEventRaiser);
        }

        public void Dispose()
        {
            _domainEventScope.Dispose();

            DoDispose();
        }

        protected abstract void DoCommit();
        protected abstract void DoDispose();
    }
}