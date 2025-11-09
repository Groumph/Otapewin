# Build Scripts

This directory contains helper scripts for building and testing Otapewin releases locally.

## Scripts

### `build-releases.ps1` (Windows/PowerShell)

Build release binaries locally for testing on Windows.

**Usage**:
```powershell
# Build all default platforms (win-x64, linux-x64, osx-arm64)
.\scripts\build-releases.ps1

# Build specific platforms
.\scripts\build-releases.ps1 -Platforms @("win-x64", "win-arm64")

# Set version
.\scripts\build-releases.ps1 -Version "1.0.0"

# Clean output before building
.\scripts\build-releases.ps1 -Clean

# All options combined
.\scripts\build-releases.ps1 -Version "1.2.0" -Platforms @("win-x64", "linux-x64") -Clean
```

### `build-releases.sh` (Linux/macOS/Bash)

Build release binaries locally for testing on Unix systems.

**Usage**:
```bash
# Make executable (first time only)
chmod +x scripts/build-releases.sh

# Build all default platforms (linux-x64, linux-arm64, osx-x64, osx-arm64)
./scripts/build-releases.sh

# Set version
./scripts/build-releases.sh 1.0.0

# Clean output before building
./scripts/build-releases.sh --clean

# Version and clean
./scripts/build-releases.sh --version 1.2.0 --clean
```

## Output

Both scripts create a `publish/` directory with:

```
publish/
├── win-x64/                    # Extracted files
│   └── otapewin.exe
├── linux-x64/                  # Extracted files
│   └── otapewin
├── osx-arm64/                  # Extracted files
│   └── otapewin
├── otapewin-win-x64.zip        # Archive
├── otapewin-linux-x64.tar.gz   # Archive
└── otapewin-osx-arm64.tar.gz   # Archive
```

## Testing Local Builds

After building, test the executables:

**Windows**:
```powershell
.\publish\win-x64\otapewin.exe --help
```

**Linux/macOS**:
```bash
./publish/linux-x64/otapewin --help
./publish/osx-arm64/otapewin --help
```

## Comparison with GitHub Actions

These scripts mimic the GitHub Actions release workflow but run locally:

| Feature | GitHub Actions | Local Scripts |
|---------|---------------|---------------|
| Self-contained | ✅ | ✅ |
| Single-file | ✅ | ✅ |
| Trimmed | ✅ | ✅ |
| Compressed | ✅ | ✅ |
| Archives | ✅ | ✅ |
| Upload to GitHub | ✅ | ❌ |
| Matrix builds | ✅ | Sequential |

## Troubleshooting

### PowerShell Execution Policy

If you get an execution policy error:
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

### Bash Script Not Executable

Make it executable:
```bash
chmod +x scripts/build-releases.sh
```

### Build Failures

- Check .NET 9 SDK is installed: `dotnet --version`
- Ensure you're in the repository root
- Try cleaning first: `--clean` or `-Clean`

### Cross-Platform Builds

Note: Building for certain platforms on others may have limitations:
- Windows can build for all platforms
- Linux can build for Linux and potentially other Unix platforms
- macOS can build for all platforms

## CI/CD Integration

These scripts are for **local testing only**. The actual release process uses:
- `.github/workflows/release.yml` - For production releases
- `.github/workflows/ci.yml` - For continuous integration

See [docs/CI-CD.md](../docs/CI-CD.md) for more information.
