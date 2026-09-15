# Release Checklist

Steps for cutting a new public release on GitHub.

## 1. Verify it actually builds and tests pass

```bash
dotnet restore
dotnet build
dotnet test src/FcmsPro.Tests
```

Then run the app itself and click through the core flows — a green build and passing tests don't
catch everything (UI layout, migrations against a real database, actual click-through behavior).
At minimum: onboarding → create a client → create a commission → mark it delivered → record a
payment → export a PDF → add an attachment → global search (`/`) → check the Dashboard banners.

## 2. Bump the version number

Currently `1.1.0` in:
- `installers/windows/fcmspro.iss` (`MyAppVersion`)
- `installers/linux/debian/DEBIAN/control` (`Version`)
- `installers/linux/build-deb.sh`, `installers/linux/build-appimage.sh` (`VERSION`)
- `installers/macos/build-dmg.sh` (`VERSION`)

Keep these in sync across platforms.

## 3. Update CHANGELOG.md

Add a new version section following the existing Added/Changed/Fixed/Removed format.

## 4. Build installers on each target OS

Each platform's packaging script needs to run **on that OS** — Windows packaging needs
Windows/Inno Setup, the macOS `.dmg` needs macOS, Linux AppImage/`.deb` needs Linux tooling. See
each script under `installers/<platform>/`. If you'd rather not do this manually on three
machines, a GitHub Actions workflow that cross-builds all three on every tag is a natural next
step — ask if you want one set up.

## 5. Tag and publish the release

```bash
git tag v1.1.0
git push origin v1.1.0
```

Create the GitHub Release from that tag, paste the relevant `CHANGELOG.md` section into the
release notes, and attach the built installers (`.exe`, `.dmg`, `.deb`/`.AppImage`) as binary
assets.

## 6. After release

Watch the repo's Issues tab for bug reports. When you patch something, bump the version, update
`CHANGELOG.md`, and repeat from step 1 — don't skip the build/test verification just because a
fix looks small.
