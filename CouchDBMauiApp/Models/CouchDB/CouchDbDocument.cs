using System.Text.Json.Serialization;

namespace CouchDBMauiApp.Models.CouchDB;

/// <summary>
/// Base class for all CouchDB documents
/// </summary>
public abstract class CouchDbDocument
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [JsonPropertyName("_rev")]
    public string? Revision { get; set; }

    [JsonPropertyName("_deleted")]
    public bool? Deleted { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Updates the UpdatedAt timestamp
    /// </summary>
    public virtual void Touch()
    {
        UpdatedAt = DateTime.UtcNow;
    }
}