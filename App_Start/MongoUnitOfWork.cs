using System;
using Core;
using Core.Repositories;
using MongoDB.Driver;

namespace AngularJSProofofConcept
{
    public class MongoUnitOfWork : IUnitOfWork
    {
        private IMongoDatabase _mongoClient;

        public MongoUnitOfWork(IMongoDatabase mongoClient)
        {
            _mongoClient = mongoClient;
        }

        void IDisposable.Dispose()
        {
            
        }

        public void Commit()
        {
            throw new NotImplementedException();
        }

        public IBalanceRepository BalanceRepository { get { return new BalanceRepository(); } }
    }
}