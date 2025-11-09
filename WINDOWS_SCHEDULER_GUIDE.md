# Windows Scheduler Integration Guide

This guide explains how to schedule SecondBrain to run automatically using Windows Task Scheduler with proper logging and error handling.

## Prerequisites

1. Build and publish SecondBrain:
```powershell
dotnet publish -c Release -o C:\Tools\SecondBrain
```

2. Configure `appsettings.json` with your settings
3. Ensure the vault path exists and is accessible

## Creating Scheduled Tasks

### Daily Task (Run Every Day at 9 PM)

1. Open **Task Scheduler** (`taskschd.msc`)
2. Click **Create Task** (not "Create Basic Task")
3. **General Tab:**
   - Name: `SecondBrain - Daily Processing`
   - Description: `Processes daily tasks and notes with AI summaries`
   - Run whether user is logged on or not: ☑
   - Run with highest privileges: ☑
   - Configure for: Windows 10/11

4. **Triggers Tab:**
   - New → Daily
   - Start: 9:00 PM
   - Recur every: 1 days
   - Enabled: ☑

5. **Actions Tab:**
   - Action: Start a program
   - Program/script: `C:\Tools\SecondBrain\SecondBrain.exe`
   - Add arguments: `daily`
   - Start in: `C:\Tools\SecondBrain`

6. **Conditions Tab:**
   - Start only if the computer is on AC power: ☐ (uncheck)
   - Wake the computer to run this task: ☑

7. **Settings Tab:**
   - Allow task to be run on demand: ☑
   - Run task as soon as possible after a scheduled start is missed: ☑
   - If the task fails, restart every: 1 minute, 3 times
   - Stop the task if it runs longer than: 1 hour

### Weekly Task (Run Every Monday at 10 PM)

Repeat the above steps with these changes:
- Name: `SecondBrain - Weekly Review`
- Trigger: Weekly, every Monday at 10:00 PM
- Arguments: `weekly`

### Backlog Review (Run Every Monday at 10:30 PM)

- Name: `SecondBrain - Backlog Review`
- Trigger: Weekly, every Monday at 10:30 PM
- Arguments: `backlog`

## Environment Variables

Set environment variables for configuration:

```powershell
# System-wide (requires admin)
[System.Environment]::SetEnvironmentVariable("SECONDBRAIN_OpenAIKey", "your-key", "Machine")

# Or in Task Scheduler, edit the task XML:
```xml
<Exec>
  <Command>C:\Tools\SecondBrain\SecondBrain.exe</Command>
  <Arguments>daily</Arguments>
  <WorkingDirectory>C:\Tools\SecondBrain</WorkingDirectory>
  <EnvironmentVariables>
    <Variable>
      <Name>SECONDBRAIN_OpenAIKey</Name>
      <Value>your-api-key</Value>
    </Variable>
    <Variable>
      <Name>ASPNETCORE_ENVIRONMENT</Name>
      <Value>Production</Value>
 </Variable>
  </EnvironmentVariables>
</Exec>
```

## Logging Configuration

### Console Logging (Captured by Task Scheduler)

Task Scheduler captures stdout/stderr in the Task History. View logs:
1. Open Task Scheduler
2. Find your task
3. Click **History** tab
4. Look for Event ID 102 (task started) and 201 (action completed)

### File Logging (Recommended)

Add file logging to `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
   "Default": "Information",
      "Microsoft": "Warning",
      "SecondBrain": "Information"
 },
    "Console": {
      "FormatterName": "simple",
      "FormatterOptions": {
  "SingleLine": true,
"IncludeScopes": false,
    "TimestampFormat": "yyyy-MM-dd HH:mm:ss "
      }
    }
  }
}
```

### Redirect Output to File

Modify the action in Task Scheduler:

**PowerShell wrapper script** (`C:\Tools\SecondBrain\run-daily.ps1`):
```powershell
$timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
$logFile = "C:\Tools\SecondBrain\Logs\daily_$timestamp.log"

# Ensure log directory exists
$logDir = Split-Path $logFile
if (!(Test-Path $logDir)) {
    New-Item -ItemType Directory -Path $logDir | Out-Null
}

# Run and capture output
& "C:\Tools\SecondBrain\SecondBrain.exe" daily 2>&1 | Tee-Object -FilePath $logFile

# Return exit code
exit $LASTEXITCODE
```

Update Task Scheduler action:
- Program/script: `powershell.exe`
- Add arguments: `-ExecutionPolicy Bypass -File C:\Tools\SecondBrain\run-daily.ps1`

## OpenTelemetry Integration

SecondBrain includes OpenTelemetry for observability. To export telemetry:

### Export to Azure Application Insights

1. Install Azure exporter:
```powershell
cd C:\Tools\SecondBrain
dotnet add package Azure.Monitor.OpenTelemetry.Exporter
```

2. Update Program.cs to add Azure exporter:
```csharp
.WithTracing(builder =>
{
  builder
        .SetResourceBuilder(resourceBuilder)
 .AddSource("SecondBrain.*")
        .AddAzureMonitorTraceExporter(options =>
    {
      options.ConnectionString = configuration["ApplicationInsights:ConnectionString"];
        });
})
```

3. Add to appsettings.json:
```json
{
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=your-key;..."
  }
}
```

## Monitoring and Alerts

### Check Task Status via PowerShell

```powershell
# Get last run result
Get-ScheduledTask -TaskName "SecondBrain - Daily Processing" | Get-ScheduledTaskInfo

# Get task history
Get-WinEvent -FilterHashtable @{
    LogName='Microsoft-Windows-TaskScheduler/Operational'
    ID=102,201
} -MaxEvents 10 | Where-Object {$_.Message -like "*SecondBrain*"}
```

### Email Notifications on Failure

In Task Scheduler:
1. Actions Tab → Add action
2. Action: Send an e-mail (deprecated in newer Windows)

**Alternative**: Use PowerShell in the wrapper script:
```powershell
try {
    & "C:\Tools\SecondBrain\SecondBrain.exe" daily 2>&1 | Tee-Object -FilePath $logFile
   $exitCode = $LASTEXITCODE
   
 if ($exitCode -ne 0) {
     Send-MailMessage `
     -To "admin@example.com" `
   -From "scheduler@example.com" `
  -Subject "SecondBrain Daily Task Failed" `
         -Body "Exit code: $exitCode. Check log: $logFile" `
        -SmtpServer "smtp.example.com"
  }
    
    exit $exitCode
}
catch {
    # Handle errors
  Write-Error $_.Exception.Message
    exit 1
}
```

## Exit Codes

SecondBrain uses standard exit codes:
- `0`: Success
- `1`: Error/failure
- `130`: Cancelled (Ctrl+C)

Use these in Task Scheduler conditions or wrapper scripts for alerting.

## Troubleshooting

### Task Doesn't Run

1. Check **Task History** for errors
2. Verify working directory is set correctly
3. Ensure user account has permissions to vault path
4. Test manually: Run task → Right-click → Run

### Permission Issues

Run as SYSTEM account or create a dedicated service account:
```powershell
# Create service account
$password = ConvertTo-SecureString "StrongPassword123!" -AsPlainText -Force
New-LocalUser -Name "SecondBrainService" -Password $password -Description "Service account for SecondBrain"

# Grant permissions
icacls "C:\path\to\vault" /grant "SecondBrainService:(OI)(CI)F" /T
```

Update task to run as this user.

### Vault Path Not Found

Ensure paths in appsettings.json are absolute:
```json
{
  "VaultPath": "C:\\Users\\YourUser\\Documents\\SecondBrain\\vault"
}
```

## Best Practices

1. **Use absolute paths** in configuration
2. **Enable logging** to files for debugging
3. **Set timeout limits** to prevent hung tasks
4. **Test manually** before scheduling
5. **Monitor task history** regularly
6. **Use environment-specific configs** (Production vs Development)
7. **Backup vault** before automated processing
8. **Review AI summaries** periodically to ensure quality

## Advanced: Run as Windows Service

For always-on scenarios, convert to a Windows Service:

```powershell
# Install as service using NSSM
choco install nssm

nssm install SecondBrain "C:\Tools\SecondBrain\SecondBrain.exe"
nssm set SecondBrain AppDirectory "C:\Tools\SecondBrain"
nssm set SecondBrain AppParameters "daily"
nssm set SecondBrain DisplayName "SecondBrain - Daily Processing"
nssm set SecondBrain Description "AI-powered task and note processing"
nssm set SecondBrain Start SERVICE_AUTO_START

# Start service
nssm start SecondBrain
```

This allows SecondBrain to run continuously with proper Windows service management.
