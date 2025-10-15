using DBConnection.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace DBConnection
{
    public class MongoDbContext
    {
        public const string collectionName = "ConfigDatas";
        private readonly IMongoDatabase _database;
        private readonly MongoClient _client;

        public MongoDbContext(string connectionString)
        {
            _client = new MongoClient(connectionString);
            _database = _client.GetDatabase("ConfigDB");
        }

        public IMongoCollection<cs_ConfigData> GetConfigDataCollection()
        {
            return GetCollection<cs_ConfigData>(collectionName);
        }

        public IMongoCollection<T> GetCollection<T>(string collectionName)
        {
            return _database.GetCollection<T>(collectionName);
        }

        public async Task<int> GetCollectionId(string collectionName)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("_id", collectionName + "Id");
            var update = Builders<BsonDocument>.Update.Inc("seq", 1);
            var options = new FindOneAndUpdateOptions<BsonDocument>
            {
                ReturnDocument = ReturnDocument.After,
                IsUpsert = true
            };

            var counter = await GetCollection<BsonDocument>("Counters")
                                  .FindOneAndUpdateAsync(filter, update, options);

            return counter["seq"].AsInt32;
        }
    }
}
