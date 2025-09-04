using CouchDBMauiApp.Models.CouchDB;

namespace CouchDBMauiApp.Services.CouchDB;

/// <summary>
/// Repository interface for task operations
/// </summary>
public interface ITaskRepository
{
    Task<List<TaskDocument>> GetAllTasksAsync();
    Task<List<TaskDocument>> GetTasksByCompletedStatusAsync(bool completed);
    Task<List<TaskDocument>> GetTasksByPriorityAsync(TaskPriority priority);
    Task<TaskDocument?> GetTaskByIdAsync(string id);
    Task<bool> SaveTaskAsync(TaskDocument task);
    Task<bool> DeleteTaskAsync(string id);
    Task<List<TaskDocument>> SearchTasksAsync(string searchTerm);
    Task<bool> MarkTaskAsCompletedAsync(string id);
    Task<int> GetTaskCountAsync();
}

/// <summary>
/// CouchDB-based task repository with local caching and synchronization
/// </summary>
public class CouchDbTaskRepository : ITaskRepository
{
    private readonly ICouchDbClient _couchDbClient;
    private readonly ILocalStorageService _localStorage;
    private readonly ILogger<CouchDbTaskRepository> _logger;
    private readonly string _deviceId;

    public CouchDbTaskRepository(
        ICouchDbClient couchDbClient,
        ILocalStorageService localStorage,
        ILogger<CouchDbTaskRepository> logger)
    {
        _couchDbClient = couchDbClient;
        _localStorage = localStorage;
        _logger = logger;
        _deviceId = GetDeviceId();
    }

    public async Task<List<TaskDocument>> GetAllTasksAsync()
    {
        try
        {
            _logger.LogInformation("Getting all tasks");

            // Try to get from local storage first (offline-first approach)
            var localTasks = await GetLocalTasksAsync();
            
            // If we have local tasks, return them immediately
            if (localTasks.Any())
            {
                _logger.LogInformation("Returning {Count} tasks from local storage", localTasks.Count);
                return localTasks;
            }

            // Fallback to remote query if no local data
            try
            {
                var viewResponse = await _couchDbClient.QueryViewAsync<TaskDocument>("tasks_by_type", 
                    new Dictionary<string, object> { { "key", "task" }, { "include_docs", true } });

                var remoteTasks = viewResponse.Rows.Where(r => r.Doc != null).Select(r => r.Doc!).ToList();
                
                // Cache remotely fetched tasks locally
                await CacheTasksLocallyAsync(remoteTasks);
                
                _logger.LogInformation("Returning {Count} tasks from remote CouchDB", remoteTasks.Count);
                return remoteTasks;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch tasks from remote CouchDB, returning empty list");
                return new List<TaskDocument>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all tasks");
            return new List<TaskDocument>();
        }
    }

    public async Task<List<TaskDocument>> GetTasksByCompletedStatusAsync(bool completed)
    {
        try
        {
            var allTasks = await GetAllTasksAsync();
            return allTasks.Where(t => t.Completed == completed).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get tasks by completed status: {Completed}", completed);
            return new List<TaskDocument>();
        }
    }

    public async Task<List<TaskDocument>> GetTasksByPriorityAsync(TaskPriority priority)
    {
        try
        {
            var allTasks = await GetAllTasksAsync();
            return allTasks.Where(t => t.Priority == priority).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get tasks by priority: {Priority}", priority);
            return new List<TaskDocument>();
        }
    }

    public async Task<TaskDocument?> GetTaskByIdAsync(string id)
    {
        try
        {
            _logger.LogInformation("Getting task by ID: {TaskId}", id);

            // Try local storage first
            var localTask = await _localStorage.GetItemAsync<TaskDocument>($"doc_{id}");
            if (localTask != null)
            {
                return localTask;
            }

            // Fallback to remote
            return await _couchDbClient.GetDocumentAsync<TaskDocument>(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get task by ID: {TaskId}", id);
            return null;
        }
    }

    public async Task<bool> SaveTaskAsync(TaskDocument task)
    {
        try
        {
            _logger.LogInformation("Saving task: {TaskId}", task.Id ?? "new task");

            // Set device ID for tracking
            task.DeviceId = _deviceId;

            // Generate ID if new task
            if (string.IsNullOrEmpty(task.Id))
            {
                task.Id = $"task_{Guid.NewGuid():N}";
            }

            // Save to local storage immediately (offline-first)
            await _localStorage.SetItemAsync($"doc_{task.Id}", task);

            // Mark as pending for synchronization
            await MarkDocumentForSyncAsync(task.Id);

            // Try to save to remote CouchDB if online
            try
            {
                var result = await _couchDbClient.SaveDocumentAsync(task);
                if (result.Ok)
                {
                    // Update local copy with new revision
                    await _localStorage.SetItemAsync($"doc_{task.Id}", task);
                    
                    // Remove from pending sync list
                    await RemoveDocumentFromSyncAsync(task.Id);
                    
                    _logger.LogInformation("Task saved successfully to remote CouchDB: {TaskId}", task.Id);
                }
                else
                {
                    _logger.LogWarning("Failed to save task to remote CouchDB: {Error}", result.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save task to remote CouchDB, will sync later");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save task: {TaskId}", task.Id);
            return false;
        }
    }

    public async Task<bool> DeleteTaskAsync(string id)
    {
        try
        {
            _logger.LogInformation("Deleting task: {TaskId}", id);

            // Get current task to get revision
            var task = await GetTaskByIdAsync(id);
            if (task == null)
            {
                _logger.LogWarning("Task not found for deletion: {TaskId}", id);
                return false;
            }

            // Mark as deleted in local storage
            task.Deleted = true;
            await _localStorage.SetItemAsync($"doc_{id}", task);
            await MarkDocumentForSyncAsync(id);

            // Try to delete from remote CouchDB if online
            try
            {
                if (!string.IsNullOrEmpty(task.Revision))
                {
                    var result = await _couchDbClient.DeleteDocumentAsync(id, task.Revision);
                    if (result.Ok)
                    {
                        // Remove from local storage
                        await _localStorage.RemoveItemAsync($"doc_{id}");
                        await RemoveDocumentFromSyncAsync(id);
                        
                        _logger.LogInformation("Task deleted successfully from remote CouchDB: {TaskId}", id);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to delete task from remote CouchDB: {Error}", result.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete task from remote CouchDB, will sync later");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete task: {TaskId}", id);
            return false;
        }
    }

    public async Task<List<TaskDocument>> SearchTasksAsync(string searchTerm)
    {
        try
        {
            var allTasks = await GetAllTasksAsync();
            
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return allTasks;
            }

            searchTerm = searchTerm.ToLowerInvariant();
            
            return allTasks.Where(t => 
                t.Title.ToLowerInvariant().Contains(searchTerm) ||
                t.Description.ToLowerInvariant().Contains(searchTerm) ||
                t.Tags.Any(tag => tag.ToLowerInvariant().Contains(searchTerm)) ||
                (t.AssignedTo?.ToLowerInvariant().Contains(searchTerm) ?? false)
            ).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search tasks with term: {SearchTerm}", searchTerm);
            return new List<TaskDocument>();
        }
    }

    public async Task<bool> MarkTaskAsCompletedAsync(string id)
    {
        try
        {
            var task = await GetTaskByIdAsync(id);
            if (task == null)
            {
                return false;
            }

            task.Completed = true;
            task.Touch();

            return await SaveTaskAsync(task);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark task as completed: {TaskId}", id);
            return false;
        }
    }

    public async Task<int> GetTaskCountAsync()
    {
        try
        {
            var tasks = await GetAllTasksAsync();
            return tasks.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get task count");
            return 0;
        }
    }

    private async Task<List<TaskDocument>> GetLocalTasksAsync()
    {
        try
        {
            var tasks = new List<TaskDocument>();
            var taskIds = await _localStorage.GetItemAsync<List<string>>("cached_task_ids") ?? new List<string>();

            foreach (var taskId in taskIds)
            {
                var task = await _localStorage.GetItemAsync<TaskDocument>($"doc_{taskId}");
                if (task != null && task.Deleted != true)
                {
                    tasks.Add(task);
                }
            }

            return tasks.OrderByDescending(t => t.UpdatedAt).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get local tasks");
            return new List<TaskDocument>();
        }
    }

    private async Task CacheTasksLocallyAsync(List<TaskDocument> tasks)
    {
        try
        {
            var taskIds = new List<string>();

            foreach (var task in tasks)
            {
                if (!string.IsNullOrEmpty(task.Id))
                {
                    await _localStorage.SetItemAsync($"doc_{task.Id}", task);
                    taskIds.Add(task.Id);
                }
            }

            await _localStorage.SetItemAsync("cached_task_ids", taskIds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache tasks locally");
        }
    }

    private async Task MarkDocumentForSyncAsync(string documentId)
    {
        try
        {
            var pendingChanges = await _localStorage.GetItemAsync<List<string>>("pending_changes") ?? new List<string>();
            if (!pendingChanges.Contains(documentId))
            {
                pendingChanges.Add(documentId);
                await _localStorage.SetItemAsync("pending_changes", pendingChanges);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark document for sync: {DocumentId}", documentId);
        }
    }

    private async Task RemoveDocumentFromSyncAsync(string documentId)
    {
        try
        {
            var pendingChanges = await _localStorage.GetItemAsync<List<string>>("pending_changes") ?? new List<string>();
            pendingChanges.Remove(documentId);
            await _localStorage.SetItemAsync("pending_changes", pendingChanges);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove document from sync: {DocumentId}", documentId);
        }
    }

    private string GetDeviceId()
    {
        // In a real MAUI app, this would get the actual device ID
        // For demo purposes, we'll generate a consistent ID
        return Environment.MachineName + "_" + Environment.UserName;
    }
}