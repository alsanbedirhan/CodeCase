using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace DBConnection.Models
{
    public class cs_ConfigData 
    {
        [BsonId]
        [BsonRepresentation(BsonType.Int32)]
        public int Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Value { get; set; }
        public int IsActive { get; set; }
        public string ApplicationName { get; set; }
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
        public DateTime LastSynced { get; set; } = DateTime.MinValue;
    }
}
