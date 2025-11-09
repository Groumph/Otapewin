# SecondBrain

AI-powered CLI to organize your daily notes, weekly reviews, and backlog with modern .NET8 practices.

[![CI](https://github.com/Groumph/SecondBrain/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Groumph/SecondBrain/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)

## Features

- 📝 Daily: Summarize and organize your inbox
- 📅 Weekly: Generate weekly summaries, coaching, and intentions
- 📋 Backlog: Review unresolved tasks from prior weeks
- 🤖 OpenAI integration: Summaries, pattern detection, and insights
- 🧪 Tests: Comprehensive unit + integration tests
- 📊 Observability: OpenTelemetry metrics/traces
- ⚙️ Modern CLI: System.CommandLine with DI, logging, options validation

## Getting Started

### Prerequisites
- .NET8 SDK
- OpenAI API key
- Markdown vault (e.g., Obsidian)

### Install
1) Clone

```
git clone https://github.com/Groumph/SecondBrain.git
cd SecondBrain
```

2) Build

```
dotnet build -c Release
```

3) Configure (choose ONE)
- Recommended: environment variables
- Or copy the example file

Environment variables (Windows PowerShell):

```
$env:SECONDBRAIN_OpenAIKey = "sk-your-api-key"
$env:SECONDBRAIN_VaultPath = "C:\path\to\vault"
$env:ASPNETCORE_ENVIRONMENT = "Production"
```

Environment variables (bash):

```
export SECONDBRAIN_OpenAIKey="sk-your-api-key"
export SECONDBRAIN_VaultPath="/path/to/vault"
export ASPNETCORE_ENVIRONMENT=Production
```

Optional file-based config:
- Copy `src/SecondBrain/appsettings.example.json` to `src/SecondBrain/appsettings.json`
- Update values as needed
- Note: `appsettings.json` is git-ignored. Do not commit secrets.

Configuration precedence (highest wins):
- Command line
- Environment variables (prefix `SECONDBRAIN_`)
- appsettings.{Environment}.json
- appsettings.json (optional)

### Run
```
# Daily processing
SecondBrain.exe daily

# Weekly review
SecondBrain.exe weekly

# Backlog review
SecondBrain.exe backlog

# Help
SecondBrain.exe --help
SecondBrain.exe daily --help
```

## Configuration
See `src/SecondBrain/appsettings.example.json` for a complete example. Key settings:
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
src/SecondBrain/
├── Clients/
│ ├── IOpenAIClient.cs
│ └── OpenAIClient.cs
├── Workers/
│ ├── IWorker.cs
│ ├── DailyWorker.cs
│ ├── WeeklyWorker.cs
│ └── BacklogReviewWorker.cs
├── Helpers/
│ ├── TagMatcher.cs
│ └── ConsoleUi.cs
├── Extensions/
│ └── StringExtensions.cs
├── BrainConfig.cs
└── Program.cs
```

## Testing

Run tests:
```
dotnet test
```

With coverage (artifact written by test host):
```
dotnet test --collect:"XPlat Code Coverage"
```

Current status:
- Tests:74/74 passing
- Coverage: Cobertura XML emitted in `tests/**/TestResults/*/coverage.cobertura.xml`

## CI/CD

- GitHub Actions CI: build, test, code coverage upload (`.github/workflows/ci.yml`)
- GitHub Release workflow: publish artifacts on tags like `v1.0.0` (`.github/workflows/release.yml`)

## Security

- Do not commit secrets. `appsettings.json` is git-ignored.
- Prefer environment variables (`SECONDBRAIN_*`).
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
MIT © SecondBrain Contributors
