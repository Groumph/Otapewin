# 🚀 Getting Started with CI/CD

Your Otapewin project now has a complete CI/CD pipeline! Here's how to use it.

## ✅ What's Been Set Up

- ✅ **Continuous Integration** - Automatic builds and tests on every push/PR
- ✅ **Automated Releases** - Multi-platform binaries on version tags
- ✅ **Documentation** - Complete guides for contributors and maintainers
- ✅ **Scripts** - Local testing scripts for release builds
- ✅ **Templates** - Issue templates for bug reports and feature requests

## 🎯 Quick Start

### For Your First Release

1. **Push these changes to GitHub**:
   ```bash
   git add .
   git commit -m "feat: add CI/CD pipeline"
   git push origin master
   ```

2. **Watch the CI run**:
   - Go to: https://github.com/Groumph/Otapewin/actions
   - You'll see the CI workflow running
   - It will build and test your code

3. **Create your first release** (when ready):
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```

4. **Monitor the release**:
   - Watch the Actions tab
   - After ~10-15 minutes, check Releases tab
   - Download and test one of the binaries

## 📁 New Files Overview

### Workflows (`.github/workflows/`)
- `ci.yml` - Runs on every push/PR (build + test)
- `release.yml` - Runs on version tags (creates releases)

### Documentation (`docs/`)
- `CI-CD.md` - Comprehensive CI/CD documentation
- `CI-CD-SETUP.md` - Setup summary (this covers what was added)
- `RELEASE-GUIDE.md` - Quick reference for creating releases

### Scripts (`scripts/`)
- `build-releases.ps1` - Build releases locally (Windows)
- `build-releases.sh` - Build releases locally (Unix)
- `README.md` - Scripts documentation

### Templates (`.github/ISSUE_TEMPLATE/`)
- `bug_report.md` - Bug report template
- `feature_request.md` - Feature request template

### Updated Files
- `README.md` - Updated with CI/CD info, badges, download links
- `CHANGELOG.md` - Ready to track versions (if it didn't exist)
- `CONTRIBUTING.md` - Guidelines for contributors (if it didn't exist)

## 🎯 Next Steps

### Immediate Actions

1. **Review the workflows**:
   ```bash
   # View CI workflow
   cat .github/workflows/ci.yml
   
   # View release workflow
   cat .github/workflows/release.yml
   ```

2. **Update repository settings** (optional but recommended):
   - Go to Settings → Branches
   - Add branch protection for `master`:
     - ✅ Require status checks (CI must pass)
     - ✅ Require branches to be up to date
     - ✅ Require pull request reviews

3. **Test locally** (optional):
   ```powershell
   # Windows
   .\scripts\build-releases.ps1 -Platforms @("win-x64")
   
   # Or Linux/macOS
   ./scripts/build-releases.sh
   ```

### Before First Release

- [ ] Update version in `src/Otapewin/Otapewin.csproj` if needed
- [ ] Update `CHANGELOG.md` with release notes
- [ ] Ensure all tests pass: `dotnet test`
- [ ] Ensure code builds: `dotnet build -c Release`
- [ ] Review `README.md` for accuracy

### Creating Releases

**Simple version**:
```bash
git tag v1.0.0
git push origin v1.0.0
```

**Detailed version** (see `docs/RELEASE-GUIDE.md`):
```bash
# 1. Update version
# Edit src/Otapewin/Otapewin.csproj

# 2. Commit
git add .
git commit -m "chore: bump version to 1.0.0"
git push origin master

# 3. Tag and push
git tag v1.0.0
git push origin v1.0.0

# 4. Wait for Actions to complete
# 5. Verify release on GitHub
```

## 📊 Monitoring

### CI Status

Check CI status on every commit:
- Actions tab: https://github.com/Groumph/Otapewin/actions
- README badges show current status
- PR checks show before merging

### Release Status

Watch releases being built:
- Actions → Release workflow
- Each platform builds in parallel
- ~10-15 minutes total
- Releases appear in Releases tab

## 🎓 Learning Resources

### Documentation
- **Full CI/CD docs**: [docs/CI-CD.md](docs/CI-CD.md)
- **Release guide**: [docs/RELEASE-GUIDE.md](docs/RELEASE-GUIDE.md)
- **Setup summary**: [docs/CI-CD-SETUP.md](docs/CI-CD-SETUP.md)
- **Contributing**: [CONTRIBUTING.md](CONTRIBUTING.md)

### Key Concepts

**Continuous Integration (CI)**:
- Automatic build on every push
- Run tests automatically
- Check code quality
- Fast feedback on PRs

**Automated Releases**:
- Tag-based releases
- Multi-platform builds
- Self-contained executables
- Automatic publishing

## 🔍 Troubleshooting

### CI Fails

1. Check the Actions tab for error details
2. Run locally: `dotnet build && dotnet test`
3. Fix errors and push again
4. CI will run automatically

### Release Fails

1. Check Actions → Release workflow
2. Look for error messages
3. Common issues:
   - Tag already exists (delete and recreate)
   - Build errors (test locally first)
   - GitHub token issues (should be automatic)

### Need Help?

- Read the docs in `docs/` directory
- Check workflow logs in Actions tab
- Review README sections
- Open an issue if stuck

## 🎉 Success Indicators

You'll know everything is working when:

✅ Pushing code triggers CI automatically  
✅ PR shows CI status before merge  
✅ Creating a tag triggers release build  
✅ Release appears with 6 platform binaries  
✅ Downloaded binaries run correctly  
✅ Badges show passing status  

## 📝 Summary

**What You Can Do Now**:
- ✅ Push code → automatic build + test
- ✅ Create PR → automatic checks
- ✅ Push tag → automatic release
- ✅ Download binaries → no build needed for users

**What Users Get**:
- Pre-built executables for 6 platforms
- No .NET SDK required
- Single-file, self-contained
- Easy downloads from Releases page

**Your Workflow**:
```bash
# Normal development
git add .
git commit -m "feat: new feature"
git push origin master
# → CI runs automatically

# Creating a release
git tag v1.0.0
git push origin v1.0.0
# → Release builds automatically
```

## 🎯 Final Checklist

Before committing everything:

- [ ] Review the workflow files
- [ ] Read the documentation
- [ ] Update README if needed
- [ ] Test CI by pushing to a branch
- [ ] Plan your first release version

## Let's Go! 🚀

You're all set! Push your changes and watch the magic happen:

```bash
git add .
git commit -m "feat: add comprehensive CI/CD pipeline with multi-platform releases"
git push origin master
```

Then visit your Actions tab to see it in action!

---

**Questions?** Check `docs/CI-CD.md` for detailed information.

**Ready to release?** See `docs/RELEASE-GUIDE.md` for step-by-step instructions.

**Happy automating!** 🎊
