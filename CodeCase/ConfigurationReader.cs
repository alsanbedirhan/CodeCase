using DBConnection;
using DBConnection.Models;
using MongoDB.Driver;

namespace CodeCase
{
    public class ConfigurationReader
    {
        MongoDbContext _clientContext;
        MongoDbContext _context;
        string _applicationName, _connectionString;
        private PeriodicTimer? _timer;
        private readonly CancellationTokenSource _cts = new();
        public ConfigurationReader(string applicationName, string connectionString, int refreshTimerIntervalInMs)
        {
            _applicationName = applicationName;
            _connectionString = connectionString;
            _clientContext = new MongoDbContext(_connectionString);
            _context = new MongoDbContext(Program.mongoConnectionString);
            _timer = new PeriodicTimer(TimeSpan.FromMilliseconds(refreshTimerIntervalInMs));
            _ = RunPeriodicSyncAsync(_cts.Token);
        }
        private async Task RunPeriodicSyncAsync(CancellationToken token)
        {
            if (_timer == null)
            {
                return;
            }

            while (await _timer.WaitForNextTickAsync(token))
            {
                await SyncConfigsAsync();
            }
        }
        private async Task SyncConfigsAsync()
        {
            var clientCol = _clientContext.GetConfigDataCollection();
            var centralCol = _context.GetConfigDataCollection();

            var now = DateTime.UtcNow;

            var lastSyncDate = await centralCol
                .Find(x => x.ApplicationName == _applicationName)
                .SortByDescending(x => x.LastSynced)
                .Project(x => x.LastSynced)
                .FirstOrDefaultAsync();

            var changedUserConfigs = await clientCol
                .Find(x => x.ApplicationName == _applicationName && x.LastModified >= lastSyncDate)
                .ToListAsync();

            var changedCentralConfigs = await centralCol
                .Find(x => x.ApplicationName == _applicationName && x.LastModified >= lastSyncDate)
                .ToListAsync();

            foreach (var userCfg in changedUserConfigs)
            {
                var filter = Builders<cs_ConfigData>.Filter.And(
                    Builders<cs_ConfigData>.Filter.Eq(x => x.ApplicationName, _applicationName),
                    Builders<cs_ConfigData>.Filter.Eq(x => x.Name, userCfg.Name)
                );

                userCfg.LastSynced = now;
                await centralCol.ReplaceOneAsync(filter, userCfg, new ReplaceOptions { IsUpsert = true });
            }

            foreach (var centralCfg in changedCentralConfigs)
            {
                var filter = Builders<cs_ConfigData>.Filter.And(
                    Builders<cs_ConfigData>.Filter.Eq(x => x.ApplicationName, _applicationName),
                    Builders<cs_ConfigData>.Filter.Eq(x => x.Name, centralCfg.Name)
                );

                centralCfg.LastSynced = now;
                await clientCol.ReplaceOneAsync(filter, centralCfg, new ReplaceOptions { IsUpsert = true });
            }
        }
        public async Task<T> getValue<T>(string key)
        {
            var doc = await _clientContext.GetConfigDataCollection().Find(x => x.ApplicationName == _applicationName && x.Name == key && x.IsActive == 1).FirstOrDefaultAsync();
            if (doc is null) throw new KeyNotFoundException(key);
            return (T)Convert.ChangeType(doc.Value, typeof(T));
        }
        public async Task<bool> addConfig(cs_ConfigDataView data)
        {
            try
            {
                await _context.GetConfigDataCollection().InsertOneAsync(new cs_ConfigData
                {
                    Id = await _context.GetCollectionId(MongoDbContext.collectionName),
                    Name = data.Name,
                    Type = data.Value.GetType().Name,
                    Value = data.Value,
                    IsActive = 1,
                    ApplicationName = _applicationName
                });
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        public async Task<bool> deleteConfig(int id)
        {
            try
            {
                var filter = Builders<cs_ConfigData>.Filter.Eq(x => x.Id, id);
                var update = Builders<cs_ConfigData>.Update.Set(x => x.IsActive, 0)
                    .Set(x => x.LastModified, DateTime.UtcNow);

                await _context.GetConfigDataCollection()
                              .UpdateOneAsync(filter, update);
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}
