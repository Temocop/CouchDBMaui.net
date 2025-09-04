# CouchDB MAUI.NET Blazor Integration

This project demonstrates a complete implementation of CouchDB integration in a MAUI.NET Blazor cross-platform application with efficient synchronization and conflict resolution.

## Features

### 🔄 Bidirectional Synchronization
- **Offline-first architecture**: Data is stored locally and synchronized when connectivity is available
- **Incremental sync**: Only changes since the last sync are transferred
- **Real-time updates**: Changes are automatically detected and synchronized
- **Device tracking**: Each device maintains its own sync status and sequence

### ⚡ Conflict Resolution
- **Automatic conflict detection**: Identifies when the same document has been modified on multiple devices
- **Visual conflict resolution**: User-friendly interface to resolve conflicts
- **Multiple revision tracking**: Maintains history of conflicting revisions
- **Data integrity**: Ensures no data loss during conflict resolution

### 🌐 Cross-Platform Compatibility
- **MAUI.NET Blazor**: Single codebase for iOS, Android, Windows, and macOS
- **Responsive UI**: Adapts to different screen sizes and orientations
- **Platform-specific optimizations**: Leverages native features when available

## Architecture

### Core Components

1. **CouchDbClient**: HTTP-based client for CouchDB operations
2. **CouchDbSyncService**: Handles bidirectional synchronization and conflict resolution
3. **TaskRepository**: Repository pattern for task data access with local caching
4. **TaskManager**: Blazor component for task management UI

### Data Models

- **CouchDbDocument**: Base class for all CouchDB documents
- **TaskDocument**: Sample task entity with priority, tags, and assignment
- **SyncStatusDocument**: Tracks synchronization status per device
- **CouchDbConflict**: Represents detected conflicts

## Getting Started

### Prerequisites

- .NET 8.0 SDK
- CouchDB server (local or remote)
- Modern web browser for Blazor WebAssembly

### Configuration

1. **CouchDB Connection**: Update the configuration in `Program.cs`
   ```csharp
   builder.Services.AddScoped<CouchDbConfiguration>(provider => new CouchDbConfiguration
   {
       ServerUrl = "http://your-couchdb-server:5984",
       DatabaseName = "your_database_name",
       Username = "your_username",
       Password = "your_password"
   });
   ```

2. **Database Setup**: The application will automatically create the database and design documents if they don't exist.

### Running the Application

1. Build the project:
   ```bash
   cd CouchDBMauiApp
   dotnet build
   ```

2. Run the application:
   ```bash
   dotnet run
   ```

3. Navigate to the Task Manager to start creating and synchronizing tasks.

## Usage

### Task Management

1. **Create Tasks**: Use the "Add Task" button to create new tasks
2. **Edit Tasks**: Click the dropdown menu on any task card to edit
3. **Mark Complete**: Check the checkbox to mark tasks as completed
4. **Search & Filter**: Use the search bar and filters to find specific tasks

### Synchronization

1. **Start Sync**: Click the sync button to begin synchronization
2. **Monitor Status**: Watch the sync status panel for real-time updates
3. **Resolve Conflicts**: If conflicts are detected, use the conflict resolution interface

### Conflict Resolution

When conflicts occur:
1. The system detects conflicting document revisions
2. A warning appears in the sync status panel
3. Click "View Conflicts" to see the conflict resolution interface
4. Choose the winning revision for each conflict

## API Reference

### CouchDbClient

```csharp
// Get a document
var task = await couchDbClient.GetDocumentAsync<TaskDocument>("task_id");

// Save a document
var response = await couchDbClient.SaveDocumentAsync(task);

// Get changes
var changes = await couchDbClient.GetChangesAsync(lastSeq);
```

### TaskRepository

```csharp
// Get all tasks
var tasks = await taskRepository.GetAllTasksAsync();

// Save a task
var success = await taskRepository.SaveTaskAsync(task);

// Search tasks
var results = await taskRepository.SearchTasksAsync("search term");
```

### SyncService

```csharp
// Start synchronization
await syncService.StartSyncAsync();

// Detect conflicts
var conflicts = await syncService.DetectConflictsAsync();

// Resolve a conflict
await syncService.ResolveConflictAsync(documentId, winningRevision);
```

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests for new functionality
5. Submit a pull request

## License

This project is licensed under the MIT License.