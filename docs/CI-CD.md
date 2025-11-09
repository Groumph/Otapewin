# CI/CD Pipeline Documentation

This document explains the CI/CD setup for Otapewin using GitHub Actions.

## Overview

The project uses two main workflows:

1. **CI Workflow** (`.github/workflows/ci.yml`) - Continuous Integration
2. **Release Workflow** (`.github/workflows/release.yml`) - Automated Releases

## Continuous Integration (CI)

### Triggers

The CI workflow runs on:
- Push to `master` or `main` branches
- Pull requests targeting these branches

### Jobs

#### 1. Build and Test
- **Platform**: Ubuntu Latest
- **Steps**:
  1. Checkout code
  2. Setup .NET 9
  3. Restore NuGet packages
  4. Build in Release configuration
  5. Run all tests with TRX logger
  6. Publish test results to PR/commit

#### 2. Code Quality
- **Platform**: Ubuntu Latest
- **Steps**:
  1. Checkout code
  2. Setup .NET 9
  3. Restore NuGet packages
  4. Check code formatting with `dotnet format`
  5. Build with warnings as errors

### Configuration

The CI workflow uses these environment variables:
- `DOTNET_VERSION`: '9.0.x'
- `DOTNET_SKIP_FIRST_TIME_EXPERIENCE`: true
- `DOTNET_CLI_TELEMETRY_OPTOUT`: true

## Release Workflow

### Triggers

The release workflow runs when you push a tag that starts with `v`:
```bash
git tag v1.0.0
git push origin v1.0.0
```

### Jobs

#### 1. Create Release
- **Platform**: Ubuntu Latest
- **Purpose**: Creates the GitHub Release
- **Steps**:
  1. Extract version from tag (e.g., `v1.0.0` → `1.0.0`)
  2. Create GitHub Release with auto-generated notes
  3. Output release upload URL for other jobs

#### 2. Build Release Artifacts
- **Platform**: Multi-platform matrix
- **Purpose**: Build optimized executables for all platforms
- **Platforms**:
  - Windows x64
  - Windows ARM64
  - Linux x64
  - Linux ARM64
  - macOS x64
  - macOS ARM64 (Apple Silicon)

**Build Configuration**:
```bash
dotnet publish \
  --configuration Release \
  --runtime {platform-rid} \
  --self-contained true \
  /p:PublishSingleFile=true \
  /p:PublishTrimmed=true \
  /p:EnableCompressionInSingleFile=true \
  /p:DebugType=none \
  /p:DebugSymbols=false
```

**Optimizations Applied**:
- ✅ Single-file executable
- ✅ Self-contained (no .NET runtime needed)
- ✅ Trimmed unused code
- ✅ Compressed
- ✅ No debug symbols (smaller size)

**Archive Formats**:
- Windows: `.zip`
- Linux/macOS: `.tar.gz`

### Release Process

#### For Maintainers

1. **Update Version** (if needed):
   ```xml
   <!-- In src/Otapewin/Otapewin.csproj -->
   <Version>1.2.0</Version>
   ```

2. **Commit Changes**:
   ```bash
   git add .
   git commit -m "chore: prepare for v1.2.0 release"
   git push origin master
   ```

3. **Create and Push Tag**:
   ```bash
   git tag v1.2.0
   git push origin v1.2.0
   ```

4. **Wait for Workflow**:
   - Watch the Actions tab on GitHub
   - Workflow will take ~10-15 minutes
   - Check for any errors

5. **Verify Release**:
   - Go to Releases page
   - Verify all 6 platform binaries are attached
   - Download and test one platform
   - Edit release notes if needed

#### Tag Naming Convention

Follow semantic versioning with a `v` prefix:
- Major releases: `v1.0.0`, `v2.0.0`
- Minor releases: `v1.1.0`, `v1.2.0`
- Patch releases: `v1.0.1`, `v1.0.2`
- Pre-releases: `v1.0.0-alpha.1`, `v1.0.0-beta.2`, `v1.0.0-rc.1`

## GitHub Secrets

The workflows use these GitHub secrets (automatically available):
- `GITHUB_TOKEN` - Automatically provided by GitHub Actions

No additional secrets configuration is required!

## Local Testing

### Test CI Build Locally

```bash
# Restore
dotnet restore

# Build
dotnet build --configuration Release

# Test
dotnet test --configuration Release --no-build

# Format check
dotnet format --verify-no-changes
```

### Test Release Build Locally

```bash
# Windows x64
dotnet publish src/Otapewin/Otapewin.csproj \
  --configuration Release \
  --runtime win-x64 \
  --self-contained true \
  --output ./publish/win-x64 \
  /p:PublishSingleFile=true \
  /p:PublishTrimmed=true

# Linux x64
dotnet publish src/Otapewin/Otapewin.csproj \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained true \
  --output ./publish/linux-x64 \
  /p:PublishSingleFile=true \
  /p:PublishTrimmed=true

# macOS ARM64
dotnet publish src/Otapewin/Otapewin.csproj \
  --configuration Release \
  --runtime osx-arm64 \
  --self-contained true \
  --output ./publish/osx-arm64 \
  /p:PublishSingleFile=true \
  /p:PublishTrimmed=true
```

## Troubleshooting

### CI Failures

**Build Errors**:
- Check compiler errors in the workflow log
- Run `dotnet build` locally to reproduce
- Fix errors and push again

**Test Failures**:
- Review test output in the workflow log
- Run `dotnet test --verbosity normal` locally
- Check for environment-specific issues (paths, etc.)

**Format Issues**:
- Run `dotnet format` locally to fix
- Commit formatting changes
- Push again

### Release Failures

**Tag Already Exists**:
```bash
# Delete local tag
git tag -d v1.0.0

# Delete remote tag
git push --delete origin v1.0.0

# Create new tag
git tag v1.0.0
git push origin v1.0.0
```

**Release Already Exists**:
- Delete the release on GitHub
- Delete the tag (see above)
- Create tag again

**Build Fails for Specific Platform**:
- Check the workflow log for that platform
- Look for platform-specific code issues
- Test that platform's RID locally

## Best Practices

1. **Always test locally before creating a release tag**
2. **Use meaningful version numbers**
3. **Keep CHANGELOG.md updated**
4. **Test downloaded binaries on target platforms**
5. **Monitor workflow runs in the Actions tab**
6. **Add custom release notes for major versions**

## Workflow Badges

Add these to your README:

```markdown
[![CI](https://github.com/Groumph/Otapewin/actions/workflows/ci.yml/badge.svg)](https://github.com/Groumph/Otapewin/actions/workflows/ci.yml)
[![Release](https://github.com/Groumph/Otapewin/actions/workflows/release.yml/badge.svg)](https://github.com/Groumph/Otapewin/actions/workflows/release.yml)
```

## Future Enhancements

Potential improvements to consider:

- [ ] Code coverage reports (Codecov/Coveralls)
- [ ] Automated changelog generation
- [ ] Docker image publishing
- [ ] NuGet package publishing
- [ ] Automated dependency updates (Dependabot)
- [ ] Security scanning (CodeQL)
- [ ] Performance benchmarks
- [ ] Preview builds for PRs
- [ ] Automatic draft releases

## Resources

- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [.NET CLI Documentation](https://docs.microsoft.com/en-us/dotnet/core/tools/)
- [Semantic Versioning](https://semver.org/)
