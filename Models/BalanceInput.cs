using Core.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AngularJSProofofConcept.Models
{
    public class BalanceInput
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        [BsonRepresentation(BsonType.Double)]
        public double Balance { get; set; }

        public static explicit operator BalanceObj(BalanceInput input)
        {
            return new BalanceObj
            {
                balance = input.Balance,
                Id = input.Id
            };
        }
    }
}