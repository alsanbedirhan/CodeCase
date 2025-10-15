using DBConnection;
using DBConnection.Models;
using MongoDB.Driver;

namespace CodeCase
{
    public class ConfigurationReader : IDisposable
    {
        MongoDbContext _context;
        string _applicationName;
        private PeriodicTimer? _timer;
        private readonly CancellationTokenSource _cts = new();
        private readonly List<cs_ConfigData> _cache = new List<cs_ConfigData>();
        public ConfigurationReader(string applicationName, int refreshTimerIntervalInMs)
        {
            _applicationName = applicationName;
            _context = new MongoDbContext(Program.mongoConnectionString);
            _timer = new PeriodicTimer(TimeSpan.FromMilliseconds(refreshTimerIntervalInMs));
            _ = RunPeriodic(_cts.Token);
        }
        private async Task RunPeriodic(CancellationToken token)
        {
            _cache.Clear();
            _cache.AddRange(await _context.GetConfigDataCollection()
                .Find(x => x.ApplicationName == _applicationName && x.IsActive == 1)
                .ToListAsync());

            if (_timer == null)
            {
                return;
            }

            while (await _timer.WaitForNextTickAsync(token))
            {
                await SyncConfigs();
            }
        }
        private async Task SyncConfigs()
        {
            var centralCol = _context.GetConfigDataCollection();

            var now = DateTime.UtcNow;

            var lastSyncDate = _cache.Any(x => x.ApplicationName == _applicationName) ? _cache.Where(x => x.ApplicationName == _applicationName)
                .Max(x => x.LastSynced) : DateTime.MinValue;

            foreach (var item in _cache.Where(x => x.Id <= 0))
            {
                item.Id = await _context.GetCollectionId(MongoDbContext.collectionName);
                item.LastSynced = now;
                await centralCol.InsertOneAsync(item);
            }

            foreach (var item in _cache.Where(x => x.LastModified >= lastSyncDate))
            {
                var filter = Builders<cs_ConfigData>.Filter.And(
                  Builders<cs_ConfigData>.Filter.Eq(x => x.ApplicationName, _applicationName),
                  Builders<cs_ConfigData>.Filter.Eq(x => x.Name, item.Name),
                  Builders<cs_ConfigData>.Filter.Eq(x => x.Id, item.Id)
              );

                item.LastSynced = now;
                await centralCol.ReplaceOneAsync(filter, item, new ReplaceOptions { IsUpsert = true });
            }

            var updated = await _context.GetConfigDataCollection()
                .Find(x => x.ApplicationName == _applicationName && x.LastModified >= lastSyncDate)
                .ToListAsync();

            foreach (var item in updated)
            {
                var existing = _cache.FirstOrDefault(x => x.Id == item.Id);
                if (existing == null)
                {
                    _cache.Add(item);
                }
                else
                {
                    existing.IsActive = item.IsActive;
                    existing.LastModified = item.LastModified;
                    existing.Value = item.Value;
                    existing.LastSynced = item.LastSynced;
                }
            }
        }
        public T getValue<T>(string key)
        {
            var cfg = _cache.FirstOrDefault(x => x.Name == key && x.ApplicationName == _applicationName && x.IsActive == 1 && x.Id > 0);
            return cfg != null ? ((T)Convert.ChangeType(cfg.Value, typeof(T))) : default!;
        }
        public bool addConfig<T>(string key, T value)
        {
            var cfg = _cache.FirstOrDefault(x => x.Name == key && x.ApplicationName == _applicationName);
            if (cfg != null)
            {
                cfg.Value = value?.ToString() ?? "";
                cfg.LastModified = DateTime.UtcNow;
                cfg.IsActive = 1;
            }
            else
            {
                _cache.Add(new cs_ConfigData
                {
                    Name = key,
                    Type = typeof(T).Name,
                    Value = value?.ToString() ?? "",
                    IsActive = 1,
                    ApplicationName = _applicationName
                });
            }
            return true;
        }
        public bool deleteConfig(string key)
        {
            var cfg = _cache.FirstOrDefault(x => x.Name == key && x.ApplicationName == _applicationName);
            if (cfg != null)
            {
                cfg.IsActive = 0;
                cfg.LastModified = DateTime.UtcNow;
                return true;
            }
            return false;
        }
        public void Dispose()
        {
            _cts.Cancel();
            _timer?.Dispose();
            _cts.Dispose();
        }
    }
}
