using System.Text.Json.Serialization;

namespace CouchDBMauiApp.Models.CouchDB;

/// <summary>
/// Sample task document for demonstrating CouchDB synchronization
/// </summary>
public class TaskDocument : CouchDbDocument
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "task";

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("completed")]
    public bool Completed { get; set; }

    [JsonPropertyName("priority")]
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    [JsonPropertyName("due_date")]
    public DateTime? DueDate { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("assigned_to")]
    public string? AssignedTo { get; set; }

    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }

    /// <summary>
    /// Updates task and touches timestamp
    /// </summary>
    public void UpdateTask(string title, string description, bool completed, TaskPriority priority)
    {
        Title = title;
        Description = description;
        Completed = completed;
        Priority = priority;
        Touch();
    }
}

/// <summary>
/// Task priority levels
/// </summary>
public enum TaskPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

/// <summary>
/// Synchronization status document
/// </summary>
public class SyncStatusDocument : CouchDbDocument
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "sync_status";

    [JsonPropertyName("device_id")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonPropertyName("last_sync")]
    public DateTime LastSync { get; set; }

    [JsonPropertyName("sync_sequence")]
    public string? SyncSequence { get; set; }

    [JsonPropertyName("pending_changes")]
    public int PendingChanges { get; set; }

    [JsonPropertyName("conflicts_detected")]
    public int ConflictsDetected { get; set; }

    [JsonPropertyName("conflicts_resolved")]
    public int ConflictsResolved { get; set; }
}