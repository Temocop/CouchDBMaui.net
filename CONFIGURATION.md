# Configuration Guide

## CouchDB Server Setup

### Local Development

1. **Install CouchDB**:
   ```bash
   # Ubuntu/Debian
   sudo apt-get install couchdb
   
   # macOS with Homebrew
   brew install couchdb
   
   # Windows
   Download from https://couchdb.apache.org/
   ```

2. **Configure CouchDB**:
   - Access Fauxton (CouchDB admin interface) at `http://localhost:5984/_utils/`
   - Create an admin user
   - Enable CORS for cross-origin requests

3. **Update Application Configuration**:
   ```csharp
   // In Program.cs
   builder.Services.AddScoped<CouchDbConfiguration>(provider => new CouchDbConfiguration
   {
       ServerUrl = "http://localhost:5984",
       DatabaseName = "couchdb_maui_app",
       Username = "admin",
       Password = "your_password",
       TimeoutSeconds = 30
   });
   ```

### Production Setup

1. **Security**:
   - Use HTTPS (configure SSL certificates)
   - Set up proper authentication
   - Configure firewall rules
   - Enable logging and monitoring

2. **Performance**:
   - Configure appropriate memory settings
   - Set up clustering if needed
   - Optimize compaction settings
   - Monitor disk usage

3. **Backup**:
   - Set up regular database backups
   - Test restore procedures
   - Configure replication for redundancy

## Application Configuration

### Environment Variables

Create an `appsettings.json` file for different environments:

```json
{
  "CouchDB": {
    "ServerUrl": "http://localhost:5984",
    "DatabaseName": "couchdb_maui_app",
    "Username": "",
    "Password": "",
    "TimeoutSeconds": 30,
    "SyncIntervalMinutes": 5,
    "RetryAttempts": 3
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "CouchDBMauiApp.Services.CouchDB": "Debug"
    }
  }
}
```

### Platform-Specific Settings

For MAUI applications, configure platform-specific settings:

#### Android (`Platforms/Android/appsettings.android.json`)
```json
{
  "CouchDB": {
    "ServerUrl": "http://10.0.2.2:5984"
  }
}
```

#### iOS (`Platforms/iOS/appsettings.ios.json`)
```json
{
  "CouchDB": {
    "ServerUrl": "http://localhost:5984"
  }
}
```

### Network Configuration

1. **CORS Settings** (for CouchDB):
   ```ini
   [cors]
   enable_cors = true
   headers = accept, authorization, content-type, origin, referer, x-csrf-token
   methods = GET, PUT, POST, HEAD, DELETE
   origins = *
   ```

2. **HTTP Client Settings**:
   - Configure timeout values
   - Set up retry policies
   - Handle SSL certificate validation

### Security Best Practices

1. **Credentials Management**:
   - Use environment variables for sensitive data
   - Implement secure storage for credentials
   - Rotate passwords regularly

2. **Data Protection**:
   - Encrypt sensitive data at rest
   - Use HTTPS for all communications
   - Implement proper access controls

3. **Validation**:
   - Validate all input data
   - Sanitize user inputs
   - Implement proper error handling

## Database Schema

### Design Documents

The application creates the following design documents:

```javascript
// _design/app
{
  "_id": "_design/app",
  "views": {
    "tasks_by_type": {
      "map": "function(doc) { if (doc.type === 'task') { emit(doc.type, doc); } }"
    },
    "tasks_by_completed": {
      "map": "function(doc) { if (doc.type === 'task') { emit(doc.completed, doc); } }"
    },
    "tasks_by_priority": {
      "map": "function(doc) { if (doc.type === 'task') { emit(doc.priority, doc); } }"
    },
    "sync_status": {
      "map": "function(doc) { if (doc.type === 'sync_status') { emit(doc.device_id, doc); } }"
    }
  }
}
```

### Indexes

For better query performance, create indexes:

```json
{
  "index": {
    "fields": ["type", "completed"]
  },
  "name": "tasks-by-type-and-completion",
  "type": "json"
}
```

## Troubleshooting

### Common Issues

1. **Connection Refused**:
   - Check if CouchDB service is running
   - Verify firewall settings
   - Confirm network connectivity

2. **Authentication Failed**:
   - Verify username and password
   - Check CouchDB user permissions
   - Ensure CORS is properly configured

3. **Sync Conflicts**:
   - Monitor conflict resolution logs
   - Implement proper conflict handling
   - Consider last-write-wins strategies

### Debugging

Enable detailed logging to troubleshoot issues:

```csharp
// In Program.cs
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Debug);
});
```

### Performance Monitoring

Monitor key metrics:
- Sync duration and frequency
- Network requests and responses
- Local storage usage
- Conflict resolution rates