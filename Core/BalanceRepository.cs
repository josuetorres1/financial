using System.Collections.Generic;
using System.Linq;
using Core.Models;
using Core.Repositories;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Core
{
    public class BalanceRepository : IBalanceRepository
    {
        private MongoClient _mongoClient;
        private readonly string _connectionstring;

        public BalanceRepository()
        {
            _connectionstring = "mongodb://josue:josuetorres1@ds055525.mlab.com:55525/ionic-josue";
        }

        public void Update(BalanceObj balance)
        {
            if (new string(balance.balance.ToString().Where(x => "0123456789".Contains(x)).ToArray()).Length !=
                balance.balance.ToString().Length) return;

            var collection = GetData();
            var filter = Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(balance.Id));
            var found = collection.Find(filter).FirstOrDefault();
            var bson = BsonSerializer.Deserialize<BalanceObj>(found);
            var updateBalance = Builders<BsonDocument>.Update.Set("balance", balance.balance + bson.balance);
            var updateAvailablefunds = Builders<BsonDocument>.Update.Set("availablefunds", bson.availablefunds - balance.balance);
            collection.UpdateOne(filter, updateBalance);
            collection.UpdateOne(filter, updateAvailablefunds);
        }

        public IMongoCollection<BsonDocument> GetData()
        {
            _mongoClient = new MongoClient(_connectionstring);
            IMongoDatabase db = _mongoClient.GetDatabase("ionic-josue");

            var collection = db.GetCollection<BsonDocument>("balances");

            if (collection.Find(new BsonDocument()).ToList().Count == 0)
            {
                var document = new BsonDocument
                {
                    {"balance", new BsonDouble(0)},
                    {"availablefunds", new BsonDouble(100000)},
                    {"creditlimit", new BsonDouble(100000)}
                };

                collection.InsertOne(document);
            }
            else
            {
                return collection;
            }

            return collection;
        }

        public IList<BalanceObj> RenderJson(IMongoCollection<BsonDocument> docs)
        {
            var sb = Enumerable.Empty<BalanceObj>().ToList();
            using (var cursor = docs.FindSync(new BsonDocument()))
            {
                while (cursor.MoveNext())
                {
                    var batch = cursor.Current;
                    sb.AddRange(batch.Select(document => new BalanceObj
                    {
                        balance = document.FirstOrDefault(d => d.Name.Contains("balance")).Value.ToDouble(),
                        creditlimit = document.FirstOrDefault(d => d.Name.Contains("creditlimit")).Value.ToDouble(),
                        availablefunds =
                            document.FirstOrDefault(d => d.Name.Contains("availablefunds")).Value.ToDouble(),
                        Id = document.FirstOrDefault(d => d.Name.Contains("_id")).Value.ToString()
                    }));
                    return sb;
                }
            }
            return null;
        }

        public BalanceObj Find(string id)
        {
            var f = GetData();
            var filter = new BsonDocument("_id", id);
            using (var cursor = f.FindSync(new BsonDocument()))
            {
                while (cursor.MoveNext())
                {
                    var batch = cursor.Current;
                    return batch.Select(document => new BalanceObj
                    {
                        balance = document.FirstOrDefault(d => d.Name.Contains("balance")).Value.ToDouble(),
                        creditlimit = document.FirstOrDefault(d => d.Name.Contains("creditlimit")).Value.ToDouble(),
                        availablefunds =
                            document.FirstOrDefault(d => d.Name.Contains("availablefunds")).Value.ToDouble(),
                        Id = document.FirstOrDefault(d => d.Name.Contains("_id")).Value.ToString()
                    }).FirstOrDefault();
                }
            }

            return BsonSerializer.Deserialize<BalanceObj>((BsonDocument)null);
        }
    }
}
    
