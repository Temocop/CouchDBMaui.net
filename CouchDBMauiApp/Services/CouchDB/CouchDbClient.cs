using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using CouchDBMauiApp.Models.CouchDB;

namespace CouchDBMauiApp.Services.CouchDB;

/// <summary>
/// Configuration for CouchDB connection
/// </summary>
public class CouchDbConfiguration
{
    public string ServerUrl { get; set; } = "http://localhost:5984";
    public string DatabaseName { get; set; } = "myapp_db";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    public bool UseSSL { get; set; } = false;
}

/// <summary>
/// Core CouchDB client for database operations
/// </summary>
public interface ICouchDbClient
{
    Task<CouchDbResponse> CreateDatabaseAsync();
    Task<bool> DatabaseExistsAsync();
    Task<T?> GetDocumentAsync<T>(string id) where T : CouchDbDocument;
    Task<CouchDbResponse> SaveDocumentAsync<T>(T document) where T : CouchDbDocument;
    Task<CouchDbResponse> DeleteDocumentAsync(string id, string revision);
    Task<CouchDbViewResponse<T>> QueryViewAsync<T>(string viewName, Dictionary<string, object>? parameters = null) where T : CouchDbDocument;
    Task<CouchDbChangesResponse> GetChangesAsync(string? since = null, bool includeDocs = true);
    Task<List<CouchDbResponse>> BulkOperationAsync(List<CouchDbDocument> documents);
    Task<List<string>> GetConflictsAsync(string documentId);
}

public class CouchDbClient : ICouchDbClient
{
    private readonly HttpClient _httpClient;
    private readonly CouchDbConfiguration _configuration;
    private readonly JsonSerializerOptions _jsonOptions;

    public CouchDbClient(HttpClient httpClient, CouchDbConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        try
        {
            // Only configure if not already configured
            if (_httpClient.BaseAddress == null)
            {
                _httpClient.BaseAddress = new Uri(_configuration.ServerUrl);
            }
            
            _httpClient.Timeout = TimeSpan.FromSeconds(_configuration.TimeoutSeconds);

            if (!string.IsNullOrEmpty(_configuration.Username) && !string.IsNullOrEmpty(_configuration.Password))
            {
                var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_configuration.Username}:{_configuration.Password}"));
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            }

            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }
        catch (Exception ex)
        {
            // Log but don't throw - allow the client to work in degraded mode
            Console.WriteLine($"Warning: Could not configure HTTP client: {ex.Message}");
        }
    }

    public async Task<CouchDbResponse> CreateDatabaseAsync()
    {
        try
        {
            var response = await _httpClient.PutAsync($"/{_configuration.DatabaseName}", null);
            var content = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                return JsonSerializer.Deserialize<CouchDbResponse>(content, _jsonOptions) ?? new CouchDbResponse { Ok = true };
            }
            
            var errorResponse = JsonSerializer.Deserialize<CouchDbResponse>(content, _jsonOptions);
            return errorResponse ?? new CouchDbResponse { Ok = false, Error = "Unknown error" };
        }
        catch (Exception ex)
        {
            return new CouchDbResponse { Ok = false, Error = "connection_error", Reason = ex.Message };
        }
    }

    public async Task<bool> DatabaseExistsAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, $"/{_configuration.DatabaseName}");
            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<T?> GetDocumentAsync<T>(string id) where T : CouchDbDocument
    {
        try
        {
            var response = await _httpClient.GetAsync($"/{_configuration.DatabaseName}/{Uri.EscapeDataString(id)}");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(content, _jsonOptions);
            }
            
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<CouchDbResponse> SaveDocumentAsync<T>(T document) where T : CouchDbDocument
    {
        try
        {
            document.Touch();
            var json = JsonSerializer.Serialize(document, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            if (string.IsNullOrEmpty(document.Id))
            {
                // Create new document
                response = await _httpClient.PostAsync($"/{_configuration.DatabaseName}", content);
            }
            else
            {
                // Update existing document
                response = await _httpClient.PutAsync($"/{_configuration.DatabaseName}/{Uri.EscapeDataString(document.Id)}", content);
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<CouchDbResponse>(responseContent, _jsonOptions) ?? new CouchDbResponse();

            if (response.IsSuccessStatusCode && result.Ok)
            {
                document.Id = result.Id;
                document.Revision = result.Rev;
            }

            return result;
        }
        catch (Exception ex)
        {
            return new CouchDbResponse { Ok = false, Error = "save_error", Reason = ex.Message };
        }
    }

    public async Task<CouchDbResponse> DeleteDocumentAsync(string id, string revision)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/{_configuration.DatabaseName}/{Uri.EscapeDataString(id)}?rev={Uri.EscapeDataString(revision)}");
            var content = await response.Content.ReadAsStringAsync();
            
            return JsonSerializer.Deserialize<CouchDbResponse>(content, _jsonOptions) ?? new CouchDbResponse { Ok = response.IsSuccessStatusCode };
        }
        catch (Exception ex)
        {
            return new CouchDbResponse { Ok = false, Error = "delete_error", Reason = ex.Message };
        }
    }

    public async Task<CouchDbViewResponse<T>> QueryViewAsync<T>(string viewName, Dictionary<string, object>? parameters = null) where T : CouchDbDocument
    {
        try
        {
            var queryString = "";
            if (parameters != null && parameters.Any())
            {
                var queryParams = parameters.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value.ToString() ?? "")}");
                queryString = "?" + string.Join("&", queryParams);
            }

            var response = await _httpClient.GetAsync($"/{_configuration.DatabaseName}/_design/app/_view/{viewName}{queryString}");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<CouchDbViewResponse<T>>(content, _jsonOptions) ?? new CouchDbViewResponse<T>();
            }
            
            return new CouchDbViewResponse<T>();
        }
        catch
        {
            return new CouchDbViewResponse<T>();
        }
    }

    public async Task<CouchDbChangesResponse> GetChangesAsync(string? since = null, bool includeDocs = true)
    {
        try
        {
            var queryParams = new List<string> { "feed=normal" };
            
            if (!string.IsNullOrEmpty(since))
                queryParams.Add($"since={Uri.EscapeDataString(since)}");
            
            if (includeDocs)
                queryParams.Add("include_docs=true");

            var queryString = string.Join("&", queryParams);
            var response = await _httpClient.GetAsync($"/{_configuration.DatabaseName}/_changes?{queryString}");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<CouchDbChangesResponse>(content, _jsonOptions) ?? new CouchDbChangesResponse();
            }
            
            return new CouchDbChangesResponse();
        }
        catch
        {
            return new CouchDbChangesResponse();
        }
    }

    public async Task<List<CouchDbResponse>> BulkOperationAsync(List<CouchDbDocument> documents)
    {
        try
        {
            documents.ForEach(d => d.Touch());
            
            var bulkRequest = new CouchDbBulkRequest
            {
                Docs = documents.Cast<object>().ToList()
            };

            var json = JsonSerializer.Serialize(bulkRequest, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"/{_configuration.DatabaseName}/_bulk_docs", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var results = JsonSerializer.Deserialize<List<CouchDbResponse>>(responseContent, _jsonOptions) ?? new List<CouchDbResponse>();
                
                // Update document IDs and revisions
                for (int i = 0; i < Math.Min(documents.Count, results.Count); i++)
                {
                    if (results[i].Ok)
                    {
                        documents[i].Id = results[i].Id;
                        documents[i].Revision = results[i].Rev;
                    }
                }
                
                return results;
            }
            
            return new List<CouchDbResponse> { new() { Ok = false, Error = "bulk_operation_failed" } };
        }
        catch (Exception ex)
        {
            return new List<CouchDbResponse> { new() { Ok = false, Error = "bulk_operation_error", Reason = ex.Message } };
        }
    }

    public async Task<List<string>> GetConflictsAsync(string documentId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/{_configuration.DatabaseName}/{Uri.EscapeDataString(documentId)}?conflicts=true");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var document = JsonSerializer.Deserialize<JsonElement>(content, _jsonOptions);
                
                if (document.TryGetProperty("_conflicts", out var conflicts))
                {
                    return conflicts.EnumerateArray().Select(c => c.GetString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList();
                }
            }
            
            return new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }
}