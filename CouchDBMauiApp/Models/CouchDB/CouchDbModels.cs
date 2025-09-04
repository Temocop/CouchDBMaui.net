using System.Text.Json.Serialization;

namespace CouchDBMauiApp.Models.CouchDB;

/// <summary>
/// Response from CouchDB operations
/// </summary>
public class CouchDbResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("rev")]
    public string? Rev { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

/// <summary>
/// Response from CouchDB view queries
/// </summary>
public class CouchDbViewResponse<T>
{
    [JsonPropertyName("total_rows")]
    public int TotalRows { get; set; }

    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("rows")]
    public List<CouchDbViewRow<T>> Rows { get; set; } = new();
}

/// <summary>
/// Row in CouchDB view response
/// </summary>
public class CouchDbViewRow<T>
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("key")]
    public object? Key { get; set; }

    [JsonPropertyName("value")]
    public object? Value { get; set; }

    [JsonPropertyName("doc")]
    public T? Doc { get; set; }
}

/// <summary>
/// CouchDB changes feed response
/// </summary>
public class CouchDbChangesResponse
{
    [JsonPropertyName("results")]
    public List<CouchDbChange> Results { get; set; } = new();

    [JsonPropertyName("last_seq")]
    public string? LastSeq { get; set; }

    [JsonPropertyName("pending")]
    public int Pending { get; set; }
}

/// <summary>
/// Individual change in CouchDB changes feed
/// </summary>
public class CouchDbChange
{
    [JsonPropertyName("seq")]
    public string? Seq { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("changes")]
    public List<CouchDbRevision> Changes { get; set; } = new();

    [JsonPropertyName("deleted")]
    public bool? Deleted { get; set; }

    [JsonPropertyName("doc")]
    public object? Doc { get; set; }
}

/// <summary>
/// CouchDB revision information
/// </summary>
public class CouchDbRevision
{
    [JsonPropertyName("rev")]
    public string? Rev { get; set; }
}

/// <summary>
/// CouchDB bulk operation request
/// </summary>
public class CouchDbBulkRequest
{
    [JsonPropertyName("docs")]
    public List<object> Docs { get; set; } = new();

    [JsonPropertyName("new_edits")]
    public bool NewEdits { get; set; } = true;
}

/// <summary>
/// CouchDB bulk operation response
/// </summary>
public class CouchDbBulkResponse
{
    public List<CouchDbResponse> Results { get; set; } = new();
}

/// <summary>
/// CouchDB conflict information
/// </summary>
public class CouchDbConflict
{
    public string DocumentId { get; set; } = string.Empty;
    public List<string> ConflictingRevisions { get; set; } = new();
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public bool Resolved { get; set; }
}