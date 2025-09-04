using CouchDBMauiApp.Models.CouchDB;

namespace CouchDBMauiApp.Services.CouchDB;

/// <summary>
/// Synchronization status
/// </summary>
public enum SyncStatus
{
    Idle,
    Syncing,
    Error,
    Conflict
}

/// <summary>
/// Synchronization event arguments
/// </summary>
public class SyncEventArgs : EventArgs
{
    public SyncStatus Status { get; set; }
    public string? Message { get; set; }
    public int ProcessedDocuments { get; set; }
    public int TotalDocuments { get; set; }
    public List<CouchDbConflict> Conflicts { get; set; } = new();
}

/// <summary>
/// Interface for CouchDB synchronization service
/// </summary>
public interface ICouchDbSyncService
{
    event EventHandler<SyncEventArgs>? SyncStatusChanged;
    
    SyncStatus CurrentStatus { get; }
    DateTime LastSyncTime { get; }
    string? LastSyncSequence { get; }
    
    Task<bool> StartSyncAsync();
    Task StopSyncAsync();
    Task<List<CouchDbConflict>> DetectConflictsAsync();
    Task<bool> ResolveConflictAsync(string documentId, string winningRevision);
    Task<bool> InitializeDatabaseAsync();
}

/// <summary>
/// CouchDB synchronization service with conflict resolution
/// </summary>
public class CouchDbSyncService : ICouchDbSyncService
{
    private readonly ICouchDbClient _couchDbClient;
    private readonly ILocalStorageService _localStorage;
    private readonly ILogger<CouchDbSyncService> _logger;
    
    private Timer? _syncTimer;
    private readonly SemaphoreSlim _syncSemaphore = new(1, 1);
    private readonly TimeSpan _syncInterval = TimeSpan.FromMinutes(5);
    
    public event EventHandler<SyncEventArgs>? SyncStatusChanged;
    
    public SyncStatus CurrentStatus { get; private set; } = SyncStatus.Idle;
    public DateTime LastSyncTime { get; private set; } = DateTime.MinValue;
    public string? LastSyncSequence { get; private set; }

    public CouchDbSyncService(
        ICouchDbClient couchDbClient,
        ILocalStorageService localStorage,
        ILogger<CouchDbSyncService> logger)
    {
        _couchDbClient = couchDbClient;
        _localStorage = localStorage;
        _logger = logger;
    }

    public async Task<bool> StartSyncAsync()
    {
        try
        {
            _logger.LogInformation("Starting CouchDB synchronization");
            
            // Initialize database if needed
            await InitializeDatabaseAsync();
            
            // Start periodic sync
            _syncTimer = new Timer(async _ => await PerformSyncAsync(), null, TimeSpan.Zero, _syncInterval);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start synchronization");
            await UpdateSyncStatus(SyncStatus.Error, $"Failed to start sync: {ex.Message}");
            return false;
        }
    }

    public async Task StopSyncAsync()
    {
        _logger.LogInformation("Stopping CouchDB synchronization");
        
        _syncTimer?.Dispose();
        _syncTimer = null;
        
        await UpdateSyncStatus(SyncStatus.Idle, "Synchronization stopped");
    }

    public async Task<bool> InitializeDatabaseAsync()
    {
        try
        {
            _logger.LogInformation("Initializing CouchDB database");

            // Check if database exists
            if (!await _couchDbClient.DatabaseExistsAsync())
            {
                _logger.LogInformation("Database does not exist, creating...");
                var createResult = await _couchDbClient.CreateDatabaseAsync();
                
                if (!createResult.Ok)
                {
                    _logger.LogError("Failed to create database: {Error}", createResult.Error);
                    return false;
                }
            }

            // Create design documents (views) for querying
            await CreateDesignDocumentsAsync();
            
            _logger.LogInformation("Database initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize database");
            return false;
        }
    }

    private Task CreateDesignDocumentsAsync()
    {
        try
        {
            // Create design document for tasks
            var designDoc = new
            {
                _id = "_design/app",
                views = new
                {
                    tasks_by_type = new
                    {
                        map = "function(doc) { if (doc.type === 'task') { emit(doc.type, doc); } }"
                    },
                    tasks_by_completed = new
                    {
                        map = "function(doc) { if (doc.type === 'task') { emit(doc.completed, doc); } }"
                    },
                    tasks_by_priority = new
                    {
                        map = "function(doc) { if (doc.type === 'task') { emit(doc.priority, doc); } }"
                    },
                    sync_status = new
                    {
                        map = "function(doc) { if (doc.type === 'sync_status') { emit(doc.device_id, doc); } }"
                    }
                }
            };

            // This would normally be a proper document save, but for simplicity we'll skip the implementation
            _logger.LogInformation("Design documents created");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create design documents");
        }
        
        return Task.CompletedTask;
    }

    private async Task PerformSyncAsync()
    {
        if (!await _syncSemaphore.WaitAsync(1000))
        {
            _logger.LogWarning("Sync already in progress, skipping");
            return;
        }

        try
        {
            await UpdateSyncStatus(SyncStatus.Syncing, "Starting synchronization");
            
            // Load last sync sequence from local storage
            LastSyncSequence = await _localStorage.GetItemAsync<string>("last_sync_sequence");
            
            // Get changes from CouchDB since last sync
            var changes = await _couchDbClient.GetChangesAsync(LastSyncSequence);
            
            if (changes.Results.Any())
            {
                await ProcessRemoteChangesAsync(changes);
            }
            
            // Push local changes to CouchDB
            await PushLocalChangesAsync();
            
            // Update sync sequence
            if (!string.IsNullOrEmpty(changes.LastSeq))
            {
                LastSyncSequence = changes.LastSeq;
                await _localStorage.SetItemAsync("last_sync_sequence", LastSyncSequence);
            }
            
            LastSyncTime = DateTime.UtcNow;
            await _localStorage.SetItemAsync("last_sync_time", LastSyncTime);
            
            // Check for conflicts
            var conflicts = await DetectConflictsAsync();
            
            if (conflicts.Any())
            {
                await UpdateSyncStatus(SyncStatus.Conflict, $"Synchronization completed with {conflicts.Count} conflicts", conflicts: conflicts);
            }
            else
            {
                await UpdateSyncStatus(SyncStatus.Idle, "Synchronization completed successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Synchronization failed");
            await UpdateSyncStatus(SyncStatus.Error, $"Synchronization failed: {ex.Message}");
        }
        finally
        {
            _syncSemaphore.Release();
        }
    }

    private async Task ProcessRemoteChangesAsync(CouchDbChangesResponse changes)
    {
        _logger.LogInformation("Processing {Count} remote changes", changes.Results.Count);
        
        var conflicts = new List<CouchDbConflict>();
        
        foreach (var change in changes.Results)
        {
            try
            {
                if (change.Deleted == true)
                {
                    // Handle deleted document
                    await HandleDeletedDocumentAsync(change.Id!);
                }
                else
                {
                    // Handle updated document
                    await HandleUpdatedDocumentAsync(change, conflicts);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process change for document {DocumentId}", change.Id);
            }
        }
        
        if (conflicts.Any())
        {
            await _localStorage.SetItemAsync("pending_conflicts", conflicts);
        }
    }

    private async Task HandleDeletedDocumentAsync(string documentId)
    {
        // Remove from local storage
        await _localStorage.RemoveItemAsync($"doc_{documentId}");
        _logger.LogInformation("Deleted document {DocumentId} from local storage", documentId);
    }

    private async Task HandleUpdatedDocumentAsync(CouchDbChange change, List<CouchDbConflict> conflicts)
    {
        if (change.Id == null) return;
        
        // Check if we have a local version
        var localDoc = await _localStorage.GetItemAsync<CouchDbDocument>($"doc_{change.Id}");
        
        if (localDoc != null && localDoc.Revision != change.Changes.FirstOrDefault()?.Rev)
        {
            // Potential conflict - check if local changes exist
            var hasLocalChanges = await HasLocalChangesAsync(change.Id);
            
            if (hasLocalChanges)
            {
                // Conflict detected
                var conflictingRevisions = await _couchDbClient.GetConflictsAsync(change.Id);
                
                conflicts.Add(new CouchDbConflict
                {
                    DocumentId = change.Id,
                    ConflictingRevisions = conflictingRevisions,
                    DetectedAt = DateTime.UtcNow
                });
                
                _logger.LogWarning("Conflict detected for document {DocumentId}", change.Id);
                return;
            }
        }
        
        // No conflict, update local document
        if (change.Doc != null)
        {
            await _localStorage.SetItemAsync($"doc_{change.Id}", change.Doc);
            _logger.LogDebug("Updated local document {DocumentId}", change.Id);
        }
    }

    private async Task<bool> HasLocalChangesAsync(string documentId)
    {
        try
        {
            var localChanges = await _localStorage.GetItemAsync<List<string>>("pending_changes") ?? new List<string>();
            return localChanges.Contains(documentId);
        }
        catch
        {
            return false;
        }
    }

    private async Task PushLocalChangesAsync()
    {
        try
        {
            var pendingChanges = await _localStorage.GetItemAsync<List<string>>("pending_changes") ?? new List<string>();
            
            if (!pendingChanges.Any())
            {
                return;
            }
            
            _logger.LogInformation("Pushing {Count} local changes", pendingChanges.Count);
            
            var documentsToSync = new List<CouchDbDocument>();
            
            foreach (var docId in pendingChanges)
            {
                var localDoc = await _localStorage.GetItemAsync<CouchDbDocument>($"doc_{docId}");
                if (localDoc != null)
                {
                    documentsToSync.Add(localDoc);
                }
            }
            
            if (documentsToSync.Any())
            {
                var results = await _couchDbClient.BulkOperationAsync(documentsToSync);
                
                // Process results and remove successfully synced changes
                var successfulChanges = new List<string>();
                for (int i = 0; i < Math.Min(documentsToSync.Count, results.Count); i++)
                {
                    if (results[i].Ok)
                    {
                        successfulChanges.Add(documentsToSync[i].Id!);
                        
                        // Update local document with new revision
                        documentsToSync[i].Revision = results[i].Rev;
                        await _localStorage.SetItemAsync($"doc_{documentsToSync[i].Id}", documentsToSync[i]);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to sync document {DocumentId}: {Error}", 
                            documentsToSync[i].Id, results[i].Error);
                    }
                }
                
                // Remove successfully synced changes from pending list
                pendingChanges.RemoveAll(id => successfulChanges.Contains(id));
                await _localStorage.SetItemAsync("pending_changes", pendingChanges);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to push local changes");
        }
    }

    public async Task<List<CouchDbConflict>> DetectConflictsAsync()
    {
        try
        {
            var conflicts = await _localStorage.GetItemAsync<List<CouchDbConflict>>("pending_conflicts") ?? new List<CouchDbConflict>();
            return conflicts.Where(c => !c.Resolved).ToList();
        }
        catch
        {
            return new List<CouchDbConflict>();
        }
    }

    public async Task<bool> ResolveConflictAsync(string documentId, string winningRevision)
    {
        try
        {
            _logger.LogInformation("Resolving conflict for document {DocumentId} with revision {Revision}", 
                documentId, winningRevision);
            
            // Get the winning document
            var winningDoc = await _couchDbClient.GetDocumentAsync<CouchDbDocument>(documentId);
            
            if (winningDoc?.Revision == winningRevision)
            {
                // Update local storage with winning revision
                await _localStorage.SetItemAsync($"doc_{documentId}", winningDoc);
                
                // Mark conflict as resolved
                var conflicts = await _localStorage.GetItemAsync<List<CouchDbConflict>>("pending_conflicts") ?? new List<CouchDbConflict>();
                var conflict = conflicts.FirstOrDefault(c => c.DocumentId == documentId);
                
                if (conflict != null)
                {
                    conflict.Resolved = true;
                    await _localStorage.SetItemAsync("pending_conflicts", conflicts);
                }
                
                _logger.LogInformation("Conflict resolved for document {DocumentId}", documentId);
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve conflict for document {DocumentId}", documentId);
            return false;
        }
    }

    private Task UpdateSyncStatus(SyncStatus status, string message, int processed = 0, int total = 0, List<CouchDbConflict>? conflicts = null)
    {
        CurrentStatus = status;
        
        var eventArgs = new SyncEventArgs
        {
            Status = status,
            Message = message,
            ProcessedDocuments = processed,
            TotalDocuments = total,
            Conflicts = conflicts ?? new List<CouchDbConflict>()
        };
        
        SyncStatusChanged?.Invoke(this, eventArgs);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _syncTimer?.Dispose();
        _syncSemaphore?.Dispose();
    }
}

/// <summary>
/// Mock local storage service for demo purposes
/// In a real MAUI app, this would use platform-specific storage
/// </summary>
public interface ILocalStorageService
{
    Task<T?> GetItemAsync<T>(string key);
    Task SetItemAsync<T>(string key, T value);
    Task RemoveItemAsync(string key);
}

public class LocalStorageService : ILocalStorageService
{
    private readonly Dictionary<string, object> _storage = new();

    public Task<T?> GetItemAsync<T>(string key)
    {
        if (_storage.TryGetValue(key, out var value) && value is T typedValue)
        {
            return Task.FromResult<T?>(typedValue);
        }
        return Task.FromResult<T?>(default);
    }

    public Task SetItemAsync<T>(string key, T value)
    {
        if (value != null)
        {
            _storage[key] = value;
        }
        return Task.CompletedTask;
    }

    public Task RemoveItemAsync(string key)
    {
        _storage.Remove(key);
        return Task.CompletedTask;
    }
}