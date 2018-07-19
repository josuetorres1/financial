using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Core.Repositories
{
    public interface IBalanceRepository
    {
        void Update(BalanceObj balance);
        IMongoCollection<BsonDocument> GetData();
        IList<BalanceObj> RenderJson(IMongoCollection<BsonDocument> docs);
        BalanceObj Find(string id);
    }
}
