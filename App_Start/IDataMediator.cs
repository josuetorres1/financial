using Core.Repositories;

namespace AngularJSProofofConcept
{
    public interface IDataMediator
    {
        IBalanceRepository BalanceRepository { get; }
    }
}