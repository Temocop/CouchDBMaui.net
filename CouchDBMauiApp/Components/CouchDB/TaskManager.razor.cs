using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using CouchDBMauiApp.Models.CouchDB;
using CouchDBMauiApp.Services.CouchDB;

namespace CouchDBMauiApp.Components.CouchDB;

public partial class TaskManager : ComponentBase, IDisposable
{
    private List<TaskDocument> _tasks = new();
    private List<TaskDocument> _filteredTasks = new();
    private List<CouchDbConflict> _conflicts = new();
    private TaskDocument? _editingTask;
    private string _searchTerm = "";
    private string _filterStatus = "";
    private string _filterPriority = "";
    
    private string FilterStatus
    {
        get => _filterStatus;
        set
        {
            _filterStatus = value;
            ApplyFilters();
        }
    }
    
    private string FilterPriority
    {
        get => _filterPriority;
        set
        {
            _filterPriority = value;
            ApplyFilters();
        }
    }
    private string _tagsInput = "";
    private bool _loading = true;
    
    private SyncStatus _syncStatus = SyncStatus.Idle;
    private string _syncMessage = "Ready to sync";
    private DateTime _lastSyncTime = DateTime.MinValue;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _loading = true;
            StateHasChanged();

            // Subscribe to sync events
            if (SyncService != null)
            {
                SyncService.SyncStatusChanged += OnSyncStatusChanged;
            
                // Initialize sync service
                await SyncService.InitializeDatabaseAsync();
            }
            
            // Load tasks
            await LoadTasksAsync();
            
            // Start synchronization
            if (SyncService != null)
            {
                await SyncService.StartSyncAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing TaskManager: {ex.Message}");
            _syncMessage = "Error initializing - working in demo mode";
            _syncStatus = SyncStatus.Error;
        }
        finally
        {
            _loading = false;
            StateHasChanged();
        }
    }

    private async Task LoadTasksAsync()
    {
        try
        {
            _loading = true;
            StateHasChanged();
            
            if (TaskRepository != null)
            {
                _tasks = await TaskRepository.GetAllTasksAsync();
            }
            else
            {
                _tasks = new List<TaskDocument>();
            }
            
            ApplyFilters();
            
            if (SyncService != null)
            {
                _lastSyncTime = SyncService.LastSyncTime;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading tasks: {ex.Message}");
            _tasks = new List<TaskDocument>();
        }
        finally
        {
            _loading = false;
            StateHasChanged();
        }
    }

    private async Task CreateDemoTasksAsync()
    {
        try
        {
            _tasks = new List<TaskDocument>
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
                    Tags = new List<string> { "demo", "welcome" },
                    DueDate = DateTime.UtcNow.AddDays(7)
                },
                new TaskDocument
                {
                    Id = "demo_task_2",
                    Title = "Configure CouchDB Connection",
                    Description = "Update the CouchDB configuration in Program.cs to connect to your CouchDB server.",
                    Priority = TaskPriority.Medium,
                    Completed = false,
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    UpdatedAt = DateTime.UtcNow.AddDays(-1),
                    Tags = new List<string> { "setup", "configuration" },
                    DueDate = DateTime.UtcNow.AddDays(1)
                },
                new TaskDocument
                {
                    Id = "demo_task_3",
                    Title = "Explore Synchronization Features",
                    Description = "Test the bidirectional synchronization and conflict resolution capabilities.",
                    Priority = TaskPriority.Low,
                    Completed = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-3),
                    UpdatedAt = DateTime.UtcNow.AddHours(-2),
                    Tags = new List<string> { "testing", "sync" }
                }
            };

            Console.WriteLine("Created demo tasks for offline mode");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating demo tasks: {ex.Message}");
            _tasks = new List<TaskDocument>();
        }
    }

    private void OnSyncStatusChanged(object? sender, SyncEventArgs e)
    {
        _syncStatus = e.Status;
        _syncMessage = e.Message ?? "";
        _conflicts = e.Conflicts;
        _lastSyncTime = SyncService.LastSyncTime;
        
        InvokeAsync(() =>
        {
            StateHasChanged();
            if (e.Status == SyncStatus.Idle)
            {
                // Reload tasks after successful sync
                _ = LoadTasksAsync();
            }
        });
    }

    private async Task ToggleSync()
    {
        if (_syncStatus == SyncStatus.Idle)
        {
            await SyncService.StartSyncAsync();
        }
        else
        {
            await SyncService.StopSyncAsync();
        }
    }

    private void OpenAddTaskModal()
    {
        _editingTask = new TaskDocument
        {
            Priority = TaskPriority.Medium,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _tagsInput = "";
        
        InvokeAsync(() => JSRuntime.InvokeVoidAsync("window.showModal", "taskModal"));
    }

    private void EditTask(TaskDocument task)
    {
        _editingTask = new TaskDocument
        {
            Id = task.Id,
            Revision = task.Revision,
            Title = task.Title,
            Description = task.Description,
            Priority = task.Priority,
            DueDate = task.DueDate,
            AssignedTo = task.AssignedTo,
            Completed = task.Completed,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
            Tags = new List<string>(task.Tags)
        };
        _tagsInput = string.Join(", ", task.Tags);
        
        InvokeAsync(() => JSRuntime.InvokeVoidAsync("window.showModal", "taskModal"));
    }

    private async Task SaveTask()
    {
        if (_editingTask == null || string.IsNullOrWhiteSpace(_editingTask.Title))
        {
            return;
        }

        try
        {
            // Parse tags from input
            _editingTask.Tags = _tagsInput
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(tag => tag.Trim())
                .Where(tag => !string.IsNullOrEmpty(tag))
                .ToList();

            var success = await TaskRepository.SaveTaskAsync(_editingTask);
            
            if (success)
            {
                await JSRuntime.InvokeVoidAsync("window.hideModal", "taskModal");
                await LoadTasksAsync();
            }
            else
            {
                await JSRuntime.InvokeVoidAsync("alert", "Failed to save task. Please try again.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving task: {ex.Message}");
            await JSRuntime.InvokeVoidAsync("alert", $"Error saving task: {ex.Message}");
        }
    }

    private async Task DeleteTask(TaskDocument task)
    {
        var confirmed = await JSRuntime.InvokeAsync<bool>("confirm", 
            $"Are you sure you want to delete the task '{task.Title}'?");
        
        if (confirmed)
        {
            try
            {
                var success = await TaskRepository.DeleteTaskAsync(task.Id!);
                if (success)
                {
                    await LoadTasksAsync();
                }
                else
                {
                    await JSRuntime.InvokeVoidAsync("alert", "Failed to delete task. Please try again.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting task: {ex.Message}");
                await JSRuntime.InvokeVoidAsync("alert", $"Error deleting task: {ex.Message}");
            }
        }
    }

    private async Task ToggleTaskCompletion(TaskDocument task, bool completed)
    {
        try
        {
            task.Completed = completed;
            task.Touch();
            
            var success = await TaskRepository.SaveTaskAsync(task);
            if (success)
            {
                StateHasChanged();
            }
            else
            {
                // Revert the change if save failed
                task.Completed = !completed;
                StateHasChanged();
                await JSRuntime.InvokeVoidAsync("alert", "Failed to update task. Please try again.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error toggling task completion: {ex.Message}");
            task.Completed = !completed; // Revert
            StateHasChanged();
        }
    }

    private void OnSearchChanged(ChangeEventArgs e)
    {
        _searchTerm = e.Value?.ToString() ?? "";
        ApplyFilters();
    }

    private async Task SearchTasks()
    {
        ApplyFilters();
        await Task.CompletedTask;
    }

    private async Task FilterTasks()
    {
        ApplyFilters();
        await Task.CompletedTask;
    }

    private void ApplyFilters()
    {
        var filtered = _tasks.AsEnumerable();

        // Search filter
        if (!string.IsNullOrWhiteSpace(_searchTerm))
        {
            var searchLower = _searchTerm.ToLowerInvariant();
            filtered = filtered.Where(t =>
                t.Title.ToLowerInvariant().Contains(searchLower) ||
                t.Description.ToLowerInvariant().Contains(searchLower) ||
                t.Tags.Any(tag => tag.ToLowerInvariant().Contains(searchLower)) ||
                (t.AssignedTo?.ToLowerInvariant().Contains(searchLower) ?? false));
        }

        // Status filter
        if (!string.IsNullOrEmpty(_filterStatus) && bool.TryParse(_filterStatus, out var completedStatus))
        {
            filtered = filtered.Where(t => t.Completed == completedStatus);
        }

        // Priority filter
        if (!string.IsNullOrEmpty(_filterPriority) && int.TryParse(_filterPriority, out var priorityValue))
        {
            filtered = filtered.Where(t => (int)t.Priority == priorityValue);
        }

        _filteredTasks = filtered.OrderByDescending(t => t.UpdatedAt).ToList();
        StateHasChanged();
    }

    private void OnDueDateChanged(ChangeEventArgs e)
    {
        if (_editingTask != null && DateTime.TryParse(e.Value?.ToString(), out var dueDate))
        {
            _editingTask.DueDate = dueDate;
        }
        else if (_editingTask != null)
        {
            _editingTask.DueDate = null;
        }
    }

    private async Task ShowConflicts()
    {
        _conflicts = await SyncService.DetectConflictsAsync();
        await JSRuntime.InvokeVoidAsync("window.showModal", "conflictsModal");
    }

    private async Task ResolveConflict(string documentId, string winningRevision)
    {
        try
        {
            var success = await SyncService.ResolveConflictAsync(documentId, winningRevision);
            if (success)
            {
                _conflicts = await SyncService.DetectConflictsAsync();
                await LoadTasksAsync();
                StateHasChanged();
            }
            else
            {
                await JSRuntime.InvokeVoidAsync("alert", "Failed to resolve conflict. Please try again.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error resolving conflict: {ex.Message}");
            await JSRuntime.InvokeVoidAsync("alert", $"Error resolving conflict: {ex.Message}");
        }
    }

    // Helper methods for CSS classes
    private string GetSyncButtonClass() => _syncStatus switch
    {
        SyncStatus.Idle => "btn-outline-primary",
        SyncStatus.Syncing => "btn-warning",
        SyncStatus.Error => "btn-danger",
        SyncStatus.Conflict => "btn-warning",
        _ => "btn-outline-primary"
    };

    private string GetSyncIcon() => _syncStatus switch
    {
        SyncStatus.Idle => "fa-sync",
        SyncStatus.Syncing => "fa-spinner fa-spin",
        SyncStatus.Error => "fa-exclamation-triangle",
        SyncStatus.Conflict => "fa-exclamation-triangle",
        _ => "fa-sync"
    };

    private string GetSyncText() => _syncStatus switch
    {
        SyncStatus.Idle => "Start Sync",
        SyncStatus.Syncing => "Syncing...",
        SyncStatus.Error => "Sync Error",
        SyncStatus.Conflict => "Conflicts",
        _ => "Sync"
    };

    private string GetSyncAlertClass() => _syncStatus switch
    {
        SyncStatus.Idle => "alert-info",
        SyncStatus.Syncing => "alert-warning",
        SyncStatus.Error => "alert-danger",
        SyncStatus.Conflict => "alert-warning",
        _ => "alert-info"
    };

    private string GetPriorityBorderClass(TaskPriority priority) => priority switch
    {
        TaskPriority.Critical => "border-danger",
        TaskPriority.High => "border-warning",
        TaskPriority.Medium => "border-info",
        TaskPriority.Low => "border-secondary",
        _ => "border-secondary"
    };

    private string GetPriorityBadgeClass(TaskPriority priority) => priority switch
    {
        TaskPriority.Critical => "bg-danger",
        TaskPriority.High => "bg-warning",
        TaskPriority.Medium => "bg-info",
        TaskPriority.Low => "bg-secondary",
        _ => "bg-secondary"
    };

    public void Dispose()
    {
        try
        {
            if (SyncService != null)
            {
                SyncService.SyncStatusChanged -= OnSyncStatusChanged;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during disposal: {ex.Message}");
        }
    }
}