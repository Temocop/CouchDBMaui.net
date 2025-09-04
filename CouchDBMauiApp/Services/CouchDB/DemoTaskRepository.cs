using CouchDBMauiApp.Models.CouchDB;

namespace CouchDBMauiApp.Services.CouchDB;

/// <summary>
/// Demo task repository that works without CouchDB for demonstration purposes
/// </summary>
public class DemoTaskRepository : ITaskRepository
{
    private readonly List<TaskDocument> _demoTasks;
    private readonly ILogger<DemoTaskRepository> _logger;

    public DemoTaskRepository(ILogger<DemoTaskRepository> logger)
    {
        _logger = logger;
        _demoTasks = CreateDemoTasks();
    }

    private List<TaskDocument> CreateDemoTasks()
    {
        return new List<TaskDocument>
        {
            new TaskDocument
            {
                Id = "demo_task_1",
                Title = "Welcome to CouchDB MAUI App",
                Description = "This is a demo task showing offline functionality. Connect to CouchDB to enable synchronization.",
                Priority = TaskPriority.High,
                Completed = false,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                UpdatedAt = DateTime.UtcNow.AddDays(-2),
                Tags = new List<string> { "demo", "welcome", "offline" },
                DueDate = DateTime.UtcNow.AddDays(7),
                DeviceId = "demo_device"
            },
            new TaskDocument
            {
                Id = "demo_task_2",
                Title = "Configure CouchDB Connection",
                Description = "Update the CouchDB configuration in Program.cs to connect to your CouchDB server and enable real synchronization.",
                Priority = TaskPriority.Medium,
                Completed = false,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow.AddDays(-1),
                Tags = new List<string> { "setup", "configuration", "couchdb" },
                DueDate = DateTime.UtcNow.AddDays(1),
                DeviceId = "demo_device"
            },
            new TaskDocument
            {
                Id = "demo_task_3",
                Title = "Explore Synchronization Features",
                Description = "Test the bidirectional synchronization and conflict resolution capabilities once CouchDB is connected.",
                Priority = TaskPriority.Low,
                Completed = true,
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                UpdatedAt = DateTime.UtcNow.AddHours(-2),
                Tags = new List<string> { "testing", "sync", "features" },
                DeviceId = "demo_device"
            },
            new TaskDocument
            {
                Id = "demo_task_4",
                Title = "Create Custom Tasks",
                Description = "Try creating your own tasks to see how the offline-first architecture works.",
                Priority = TaskPriority.Medium,
                Completed = false,
                CreatedAt = DateTime.UtcNow.AddHours(-6),
                UpdatedAt = DateTime.UtcNow.AddHours(-6),
                Tags = new List<string> { "custom", "practice" },
                DueDate = DateTime.UtcNow.AddDays(3),
                DeviceId = "demo_device"
            }
        };
    }

    public Task<List<TaskDocument>> GetAllTasksAsync()
    {
        _logger.LogInformation("Getting all tasks from demo repository");
        return Task.FromResult(_demoTasks.ToList());
    }

    public Task<List<TaskDocument>> GetTasksByCompletedStatusAsync(bool completed)
    {
        var filtered = _demoTasks.Where(t => t.Completed == completed).ToList();
        return Task.FromResult(filtered);
    }

    public Task<List<TaskDocument>> GetTasksByPriorityAsync(TaskPriority priority)
    {
        var filtered = _demoTasks.Where(t => t.Priority == priority).ToList();
        return Task.FromResult(filtered);
    }

    public Task<TaskDocument?> GetTaskByIdAsync(string id)
    {
        var task = _demoTasks.FirstOrDefault(t => t.Id == id);
        return Task.FromResult(task);
    }

    public Task<bool> SaveTaskAsync(TaskDocument task)
    {
        try
        {
            if (string.IsNullOrEmpty(task.Id))
            {
                task.Id = $"demo_task_{Guid.NewGuid():N}";
                task.DeviceId = "demo_device";
                _demoTasks.Add(task);
                _logger.LogInformation("Added new demo task: {TaskId}", task.Id);
            }
            else
            {
                var existingIndex = _demoTasks.FindIndex(t => t.Id == task.Id);
                if (existingIndex >= 0)
                {
                    task.Touch();
                    _demoTasks[existingIndex] = task;
                    _logger.LogInformation("Updated demo task: {TaskId}", task.Id);
                }
                else
                {
                    _demoTasks.Add(task);
                    _logger.LogInformation("Added demo task: {TaskId}", task.Id);
                }
            }
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save demo task: {TaskId}", task.Id);
            return Task.FromResult(false);
        }
    }

    public Task<bool> DeleteTaskAsync(string id)
    {
        try
        {
            var taskIndex = _demoTasks.FindIndex(t => t.Id == id);
            if (taskIndex >= 0)
            {
                _demoTasks.RemoveAt(taskIndex);
                _logger.LogInformation("Deleted demo task: {TaskId}", id);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete demo task: {TaskId}", id);
            return Task.FromResult(false);
        }
    }

    public Task<List<TaskDocument>> SearchTasksAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return GetAllTasksAsync();
        }

        var searchLower = searchTerm.ToLowerInvariant();
        var filtered = _demoTasks.Where(t =>
            t.Title.ToLowerInvariant().Contains(searchLower) ||
            t.Description.ToLowerInvariant().Contains(searchLower) ||
            t.Tags.Any(tag => tag.ToLowerInvariant().Contains(searchLower)) ||
            (t.AssignedTo?.ToLowerInvariant().Contains(searchLower) ?? false)
        ).ToList();

        return Task.FromResult(filtered);
    }

    public Task<bool> MarkTaskAsCompletedAsync(string id)
    {
        var task = _demoTasks.FirstOrDefault(t => t.Id == id);
        if (task != null)
        {
            task.Completed = true;
            task.Touch();
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<int> GetTaskCountAsync()
    {
        return Task.FromResult(_demoTasks.Count);
    }
}