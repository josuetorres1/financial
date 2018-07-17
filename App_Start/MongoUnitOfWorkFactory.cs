using System;
using MongoDB.Driver;

namespace AngularJSProofofConcept
{
    public class MongoUnitOfWorkFactory : IUnitOfWorkFactory
    {
        private readonly string _conn;
        private readonly IMongoDatabase _mongoClient;

        public MongoUnitOfWorkFactory(IMongoDatabase mongoClient)
        {
            _mongoClient = mongoClient;
        }

        public IUnitOfWork Create()
        {
            return new MongoUnitOfWork(_mongoClient);
        }
    }
}