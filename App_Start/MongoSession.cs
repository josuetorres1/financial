using System;
using MongoDB.Driver;

namespace AngularJSProofofConcept
{
    public class MongoSession : IArtezSession
    {
        private readonly IMongoDatabase _mongoClient;

        public MongoSession(IMongoDatabase db)
        {
            _mongoClient = db;
        }

        void IDisposable.Dispose()
        {
            ((IDisposable) this).Dispose();
        }

        public IUnitOfWorkFactory CreateUnitOfWorkFactory()
        {
            return new MongoUnitOfWorkFactory(_mongoClient);
        }
    }
}