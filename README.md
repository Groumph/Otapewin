# Otapewin

AI-powered CLI to organize your daily notes, weekly reviews, and backlog with modern .NET 9 practices.

[![CI](https://github.com/Groumph/Otapewin/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/Groumph/Otapewin/actions/workflows/ci.yml)
[![Release](https://github.com/Groumph/Otapewin/actions/workflows/release.yml/badge.svg)](https://github.com/Groumph/Otapewin/actions/workflows/release.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/9.0)

## Features

- 📝 Daily: Summarize and organize your inbox
- 📅 Weekly: Generate weekly summaries, coaching, and intentions
- 📋 Backlog: Review unresolved tasks from prior weeks
- 🤖 OpenAI integration: Summaries, pattern detection, and insights
- 🧪 Tests: Comprehensive unit + integration tests
- 📊 Observability: OpenTelemetry metrics/traces
- ⚙️ Modern CLI: System.CommandLine with DI, logging, options validation

## Getting Started

### Download Pre-built Binaries

Download the latest release for your platform from the [Releases page](https://github.com/Groumph/Otapewin/releases):

- **Windows (x64)**: `otapewin-win-x64.zip`
- **Windows (ARM64)**: `otapewin-win-arm64.zip`
- **Linux (x64)**: `otapewin-linux-x64.tar.gz`
- **Linux (ARM64)**: `otapewin-linux-arm64.tar.gz`
- **macOS (x64)**: `otapewin-osx-x64.tar.gz`
- **macOS (ARM64/Apple Silicon)**: `otapewin-osx-arm64.tar.gz`

Extract the archive and run the executable.

### Build from Source

#### Prerequisites
- .NET 9 SDK
- OpenAI API key
- Markdown vault (e.g., Obsidian)

#### Install
1) Clone

```bash
git clone https://github.com/Groumph/Otapewin.git
cd Otapewin
```

2) Build

```bash
dotnet build -c Release
```

3) Configure (choose ONE)
- Recommended: environment variables
- Or copy the example file

Environment variables (Windows PowerShell):

```powershell
$env:OTAPEWIN_OpenAIKey = "sk-your-api-key"
$env:OTAPEWIN_VaultPath = "C:\path\to\vault"
$env:ASPNETCORE_ENVIRONMENT = "Production"
```

Environment variables (bash):

```bash
export OTAPEWIN_OpenAIKey="sk-your-api-key"
export OTAPEWIN_VaultPath="/path/to/vault"
export ASPNETCORE_ENVIRONMENT=Production
```

Optional file-based config:
- Copy `src/Otapewin/appsettings.example.json` to `src/Otapewin/appsettings.json`
- Update values as needed
- Note: `appsettings.json` is git-ignored. Do not commit secrets.

Configuration precedence (highest wins):
- Command line
- Environment variables (prefix `OTAPEWIN_`)
- appsettings.{Environment}.json
- appsettings.json (optional)

### Run
```bash
# Daily processing
otapewin daily

# Weekly review
otapewin weekly

# Backlog review
otapewin backlog

# Run all commands
otapewin

# Help
otapewin --help
otapewin daily --help
```

## Configuration
See `src/Otapewin/appsettings.example.json` for a complete example. Key settings:
- `OpenAIKey` – your OpenAI API key
- `VaultPath` – root path of your Markdown vault
- `InputFile` – inbox file name (e.g., `Memory Inbox.md`)
- `Tags` – tag configurations with optional prompts
- `Prompts` – prompt templates for summarization flows

## Architecture

- Dependency Injection + Hosting (Microsoft.Extensions)
- Options pattern with data annotations validation
- Structured logging with `ILogger<T>`
- Professional console output via `ConsoleUi` (used for status and UX)
- OpenTelemetry for metrics and traces
- Clean workers for daily/weekly/backlog flows

```
src/Otapewin/
├── Clients/
│   ├── IOpenAIClient.cs
│   └── OpenAIClient.cs
├── Workers/
│   ├── IWorker.cs
│   ├── DailyWorker.cs
│   ├── WeeklyWorker.cs
│   └── BacklogReviewWorker.cs
├── Helpers/
│   ├── TagMatcher.cs
│   └── ConsoleUi.cs
├── Extensions/
│   └── StringExtensions.cs
├── BrainConfig.cs
└── Program.cs
```

## Testing

Run tests:
```bash
dotnet test
```

With coverage (artifact written by test host):
```bash
dotnet test --collect:"XPlat Code Coverage"
```

Current status:
- Tests: 74/74 passing
- Coverage: Cobertura XML emitted in `tests/**/TestResults/*/coverage.cobertura.xml`

## CI/CD

The project uses GitHub Actions for continuous integration and automated releases.

### Continuous Integration

Every push and pull request triggers the CI pipeline (`.github/workflows/ci.yml`):

- ✅ **Build**: Compiles the project with .NET 9
- ✅ **Test**: Runs all unit and integration tests
- ✅ **Code Quality**: Checks formatting and builds with warnings as errors
- 📊 **Test Reports**: Publishes test results automatically

### Automated Releases

To create a new release:

1. **Update the version** in `src/Otapewin/Otapewin.csproj` if needed
2. **Create and push a version tag**:
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```

3. **GitHub Actions will automatically**:
   - Create a GitHub Release
   - Build self-contained executables for:
     - Windows (x64, ARM64)
     - Linux (x64, ARM64)
     - macOS (x64, ARM64)
   - Upload all binaries to the release
   - Generate release notes

The release pipeline (`.github/workflows/release.yml`) creates optimized, self-contained single-file executables that are ready to run without requiring .NET installation.

### Publishing Options

The executables are built with:
- ✅ Single-file deployment
- ✅ Trimmed for smaller size
- ✅ Self-contained (no .NET runtime required)
- ✅ Compressed

## Security

- Do not commit secrets. `appsettings.json` is git-ignored.
- Prefer environment variables (`OTAPEWIN_*`).
- See [SECURITY.md](SECURITY.md) for reporting and handling guidance.

## Documentation
- [CHANGELOG.md](CHANGELOG.md)
- [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md)
- [SECURITY.md](SECURITY.md)
- [BUG_FIXES_AND_PERFORMANCE.md](BUG_FIXES_AND_PERFORMANCE.md)
- [COMPLETE_TEST_COVERAGE.md](COMPLETE_TEST_COVERAGE.md)

## Roadmap
- Web dashboard for summaries
- Mobile integration
- Multi-vault support
- Plugins for custom processors
- Integrations (task tools)
- Advanced analytics
- Voice input
- Cloud sync

## License
MIT © Otapewin Contributors
