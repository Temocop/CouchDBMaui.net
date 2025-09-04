using CouchDBMauiApp.Models.CouchDB;

namespace CouchDBMauiApp.Services.CouchDB;

/// <summary>
/// Demo sync service that simulates sync operations for offline demo mode
/// </summary>
public class DemoSyncService : ICouchDbSyncService
{
    public event EventHandler<SyncEventArgs>? SyncStatusChanged;
    
    public SyncStatus CurrentStatus { get; private set; } = SyncStatus.Idle;
    public DateTime LastSyncTime { get; private set; } = DateTime.MinValue;
    public string? LastSyncSequence { get; private set; }

    public Task<bool> StartSyncAsync()
    {
        CurrentStatus = SyncStatus.Error;
        SyncStatusChanged?.Invoke(this, new SyncEventArgs
        {
            Status = SyncStatus.Error,
            Message = "Demo mode - CouchDB not available"
        });
        return Task.FromResult(false);
    }

    public Task StopSyncAsync()
    {
        CurrentStatus = SyncStatus.Idle;
        SyncStatusChanged?.Invoke(this, new SyncEventArgs
        {
            Status = SyncStatus.Idle,
            Message = "Demo mode - sync not available"
        });
        return Task.CompletedTask;
    }

    public Task<List<CouchDbConflict>> DetectConflictsAsync()
    {
        return Task.FromResult(new List<CouchDbConflict>());
    }

    public Task<bool> ResolveConflictAsync(string documentId, string winningRevision)
    {
        return Task.FromResult(false);
    }

    public Task<bool> InitializeDatabaseAsync()
    {
        CurrentStatus = SyncStatus.Error;
        SyncStatusChanged?.Invoke(this, new SyncEventArgs
        {
            Status = SyncStatus.Error,
            Message = "Demo mode - working offline"
        });
        return Task.FromResult(false);
    }
}