# Release Checklist

The **git tag is the only version number you set.** CI stamps it into the csproj, installer
scripts and .deb control file (`.github/set-version.sh`), builds all platforms, publishes the
release, then regenerates `docs/appcast.xml` for the in-app updater.

1. `dotnet build` and `dotnet test src/FcmsPro.Tests`, then click through the core flows.
2. Add a section to `CHANGELOG.md`.
3. Tag and push (tag must be exactly `vX.Y.Z`):
   ```bash
   git tag v1.6.7
   git push origin v1.6.7
   ```
4. Watch the Actions run. All five jobs must be green, including **Update appcast.xml**.
5. Confirm the release has `.exe`, `.dmg`, `.deb` and `.AppImage`, and that
   `docs/appcast.xml` on `main` shows the new version.
6. Test the updater: install the previous version, launch it, and expect the update prompt.
   The app log shows `Updater:` lines with the installed version and appcast status.
