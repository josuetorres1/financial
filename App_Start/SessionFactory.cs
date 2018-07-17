using System;
using MongoDB.Driver;

namespace AngularJSProofofConcept
{
    public class SessionFactory : IArtezSessionFactory
    {
        private readonly MongoClient _mongoClient;

        public SessionFactory(MongoClient mongoClient)
        {
            _mongoClient = mongoClient;
        }

        void IDisposable.Dispose()
        {
            
        }

        public IArtezSession Create()
        {
            return new MongoSession(_mongoClient.GetDatabase("ionic-josue"));
        }
    }
}