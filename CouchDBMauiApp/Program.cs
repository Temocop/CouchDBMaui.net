using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using CouchDBMauiApp;
using CouchDBMauiApp.Services.CouchDB;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure CouchDB services with demo fallback
builder.Services.AddScoped<CouchDbConfiguration>(provider => new CouchDbConfiguration
{
    ServerUrl = "http://localhost:5984", // This should be configurable
    DatabaseName = "couchdb_maui_app",
    Username = "", // Configure as needed
    Password = "", // Configure as needed
    TimeoutSeconds = 5 // Shorter timeout for demo
});

// Configure HttpClient
builder.Services.AddHttpClient<ICouchDbClient, CouchDbClient>();

builder.Services.AddScoped<ILocalStorageService, LocalStorageService>();

// Use demo services by default - in production you would have logic to detect CouchDB availability
builder.Services.AddScoped<ICouchDbSyncService, DemoSyncService>();
builder.Services.AddScoped<ITaskRepository, DemoTaskRepository>();

await builder.Build().RunAsync();
