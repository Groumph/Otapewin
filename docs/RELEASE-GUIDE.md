# Quick Release Guide

This is a quick reference for creating releases of Otapewin.

## Prerequisites

- Git access with push permissions
- Clean working directory
- All tests passing locally

## Steps

### 1. Prepare the Release

```bash
# Make sure you're on master and up to date
git checkout master
git pull origin master

# Run tests to ensure everything works
dotnet test

# Build to ensure no compilation errors
dotnet build -c Release
```

### 2. Update Version (Optional)

Edit `src/Otapewin/Otapewin.csproj`:

```xml
<Version>1.2.0</Version>
```

Commit if changed:

```bash
git add src/Otapewin/Otapewin.csproj
git commit -m "chore: bump version to 1.2.0"
git push origin master
```

### 3. Create and Push Tag

```bash
# Create the tag
git tag v1.2.0

# Push the tag (this triggers the release workflow)
git push origin v1.2.0
```

### 4. Monitor the Release

1. Go to: https://github.com/Groumph/Otapewin/actions
2. Watch the "Release" workflow
3. Wait ~10-15 minutes for all platforms to build

### 5. Verify the Release

1. Go to: https://github.com/Groumph/Otapewin/releases
2. Find your new release (e.g., "Otapewin v1.2.0")
3. Verify all 6 platform files are attached:
   - ✅ otapewin-win-x64.zip
   - ✅ otapewin-win-arm64.zip
   - ✅ otapewin-linux-x64.tar.gz
   - ✅ otapewin-linux-arm64.tar.gz
   - ✅ otapewin-osx-x64.tar.gz
   - ✅ otapewin-osx-arm64.tar.gz

### 6. Test a Build (Recommended)

Download one of the archives for your platform and test:

**Windows**:
```powershell
# Extract and test
Expand-Archive otapewin-win-x64.zip -DestinationPath test
cd test
.\otapewin.exe --help
```

**Linux/macOS**:
```bash
# Extract and test
tar -xzf otapewin-linux-x64.tar.gz
cd otapewin-linux-x64
./otapewin --help
```

### 7. Update Release Notes (Optional)

Click "Edit release" on GitHub and add:
- What's new
- Breaking changes
- Bug fixes
- Known issues

## Version Numbering

Follow [Semantic Versioning](https://semver.org/):

- **Major** (v2.0.0): Breaking changes
- **Minor** (v1.1.0): New features, backwards compatible
- **Patch** (v1.0.1): Bug fixes, backwards compatible

## Common Issues

### "Tag already exists"

```bash
# Delete and recreate
git tag -d v1.2.0
git push --delete origin v1.2.0
git tag v1.2.0
git push origin v1.2.0
```

### "Release workflow failed"

1. Check the Actions tab for error details
2. Fix the issue in code
3. Delete the tag and release
4. Try again

### "Wrong version number used"

1. Delete the release on GitHub
2. Delete the tag (see above)
3. Update version in .csproj
4. Commit and push
5. Create new tag

## Checklist

Before creating a release:

- [ ] All tests pass locally
- [ ] Code is committed and pushed
- [ ] Version number is correct (if specified in .csproj)
- [ ] CHANGELOG.md is updated (if exists)
- [ ] You're on the master branch
- [ ] You have push permissions

After creating a release:

- [ ] Workflow completed successfully
- [ ] All 6 platform binaries are attached
- [ ] At least one binary tested and works
- [ ] Release notes are clear and helpful

## Need Help?

- Check workflow logs: https://github.com/Groumph/Otapewin/actions
- Review full docs: [docs/CI-CD.md](CI-CD.md)
- Open an issue: https://github.com/Groumph/Otapewin/issues
