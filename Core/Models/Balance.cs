using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Models
{
    public class BalanceObj
    {
        public double balance { get; set; }
        public double creditlimit { get; set; }
        public double availablefunds { get; set; }
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
    }
}
