using SQLite;
using System.Text.Json;

namespace Clubes.Mobile.Core.Services;

public sealed class OfflineStore
{
    private readonly SQLiteAsyncConnection _database;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private bool _initialized;
    public OfflineStore(string path) => _database = new SQLiteAsyncConnection(path);
    public async Task InitializeAsync()
    {
        if (_initialized) return;
        await _initializationGate.WaitAsync();
        try
        {
            if (_initialized) return;
            await _database.CreateTableAsync<CachedEntry>();
            await _database.CreateTableAsync<PendingAction>();
            var columns = await _database.QueryAsync<TableColumn>("PRAGMA table_info(pending_actions)");
            if (!columns.Any(column => column.Name == nameof(PendingAction.OwnerPersonId)))
                await _database.ExecuteAsync($"ALTER TABLE pending_actions ADD COLUMN {nameof(PendingAction.OwnerPersonId)} INTEGER NOT NULL DEFAULT 0");
            _initialized = true;
        }
        finally { _initializationGate.Release(); }
    }
    public async Task SaveCacheAsync(string key, string json)
    {
        await _database.InsertOrReplaceAsync(new CachedEntry { Key = key, Json = json, UpdatedUtc = DateTime.UtcNow });
    }
    public async Task<(string? Json, DateTime? UpdatedUtc)> GetCacheAsync(string key)
    {
        var entry = await _database.FindAsync<CachedEntry>(key);
        return (entry?.Json, entry?.UpdatedUtc);
    }
    public async Task<DateTime?> GetCacheTimeAsync(string key) => (await _database.FindAsync<CachedEntry>(key))?.UpdatedUtc;
    public async Task<Guid> EnqueueAsync(int clubId, int ownerPersonId, string endpoint, string json)
    {
        var item = new PendingAction { Id = Guid.NewGuid(), ClubId = clubId, OwnerPersonId = ownerPersonId, Endpoint = endpoint, Json = json, QueuedUtc = DateTime.UtcNow, Status = "Aguardando envio" };
        await _database.InsertAsync(item); return item.Id;
    }
    public Task<List<PendingAction>> GetPendingAsync(int ownerPersonId) => _database.Table<PendingAction>().Where(item => item.OwnerPersonId == ownerPersonId && item.Status == "Aguardando envio").OrderBy(item => item.QueuedUtc).ToListAsync();
    public Task<List<PendingAction>> GetActionsByClubAsync(int clubId, int ownerPersonId) => _database.Table<PendingAction>().Where(item => item.ClubId == clubId && item.OwnerPersonId == ownerPersonId).OrderByDescending(item => item.QueuedUtc).ToListAsync();
    public Task UpdateActionAsync(PendingAction action) => _database.UpdateAsync(action);

    [Table("offline_cache")]
    public sealed class CachedEntry
    {
        [PrimaryKey] public string Key { get; set; } = "";
        public string Json { get; set; } = "";
        public DateTime UpdatedUtc { get; set; }
    }
    [Table("pending_actions")]
    public sealed class PendingAction
    {
        [PrimaryKey] public Guid Id { get; set; }
        public int ClubId { get; set; }
        public int OwnerPersonId { get; set; }
        public string Endpoint { get; set; } = "";
        public string Json { get; set; } = "";
        public DateTime QueuedUtc { get; set; }
        public string Status { get; set; } = "";
        public string? Error { get; set; }
    }

    private sealed class TableColumn
    {
        [Column("name")] public string Name { get; set; } = "";
    }
}
