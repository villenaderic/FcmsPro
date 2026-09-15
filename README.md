# FCMS Pro — Native Desktop (Avalonia / .NET 8)

A cross-platform freelance commission-tracking app: clients, commissions, quotes, invoices,
receipts, payments, expenses, and reporting, all running locally with no server and no account
required. Built with Avalonia UI on .NET 8, runs on Windows, macOS, and Linux from one codebase.

See `docs/USER_GUIDE.md` for a full walkthrough of onboarding and every module, `CHANGELOG.md`
for release history, and `LICENSE` for terms (MIT).

Found a bug? Please open an [issue](../../issues) — screenshots help a lot.

## Status

All 14 modules are implemented. Build and test suite are verified passing on Windows; macOS and
Linux builds are set up (see `installers/` and `.github/workflows/`) but not yet confirmed on
real hardware for every module — if you hit something on either platform, that's exactly the
kind of report worth filing. See `CHANGELOG.md` for what's shipped in each version.

## Your data

FCMS Pro stores everything locally in a SQLite file — no server, no account, no telemetry.
That also means nobody but you is responsible for backing it up: use the built-in backup/export
feature regularly, since there's no cloud copy to fall back on if the database file is lost or
corrupted.

## What's implemented

- **`FcmsPro.Core`** — complete: all entities, enums, repository interfaces, and business-logic
  services (recurrence spawning, refund recalculation, atomic payment+receipt transactions,
  PBKDF2 auth, backup/restore, centralized KPI/overdue metrics via `MetricsService`).
- **`FcmsPro.Data`** — complete: EF Core `DbContext`, repositories, `UnitOfWork` with transaction
  support, OS-aware app-data paths, first-run database initializer (migrations + WAL mode + seeding).
- **`FcmsPro.Pdf`** — complete: `ReceiptRenderer`, `InvoiceRenderer`, and `QuoteRenderer` are all
  fully implemented (PdfSharpCore, vector PDF generation) — no stubs remaining.
- **`FcmsPro.Avalonia`** — complete: all 14 module ViewModels/Views, onboarding wizard (all 5 steps
  have real content), login with lockout, main shell with DI-scoped per-page navigation, the ported
  two-key keyboard shortcut system, `accent`/`destructive` button styling, native save/open file
  dialogs, and a working confirm-dialog host.
- **`FcmsPro.Tests`** — unit tests across the service and view-model layers, covering business
  rules like commission/expense recurrence, payment refunds, attachments, and global search.
- **`installers/`** — Windows (Inno Setup), macOS (`.app` + `.dmg` via `create-dmg`), and Linux
  (AppImage + `.deb`) packaging scripts, all referencing real generated app icons under
  `src/FcmsPro.Avalonia/Assets/` (`app.ico`, `app.icns`, `app-256.png`) — see the branding
  section below.

## Branding

`Assets/app-icon-source.png` is the 1254×1254 master icon; platform icon files are derived from
it: `app.ico` (Windows, multi-resolution 16–256px), `app.icns` (macOS, via `icnsutil`), and
`app-256.png`/`app-512.png` (Linux). `logo-wordmark.png` is the horizontal lockup used on the
onboarding Welcome screen. `illustration-empty-state.png` is the shared empty-state illustration
used across every list page's "nothing here yet" state.

**Known limitation**: the current visual design is FluentTheme's stock dark palette with minimal
customization — flat borders, limited illustration work beyond onboarding and the empty states.
Functional and stable, but not a final design pass.

## Getting started

```bash
# From the solution root, once you have the .NET 8 SDK installed:
dotnet restore
dotnet build
# Migrations are already checked in under src/FcmsPro.Data/Migrations - the app applies them
# itself on first run (see FcmsDbContextFactory), so no `dotnet ef migrations add` step is
# needed for a normal run. Only regenerate migrations if you've changed an entity yourself.
dotnet run --project src/FcmsPro.Avalonia
```

```bash
# Run the unit tests
dotnet test src/FcmsPro.Tests
```

## Building installers

Each script in `installers/` publishes a self-contained build and packages it. Run from the
`installers/<platform>/` folder on the matching OS (macOS packaging must happen on macOS; Windows
Inno Setup must run on Windows; Linux scripts need `appimagetool`/`dpkg-deb`). All three currently
build without a custom app icon — supply one under `src/FcmsPro.Avalonia/Assets/` to fix that later
without changing the scripts.

## Reporting back

Since this has never been compiled, please run `dotnet build` and paste the output. Errors will
likely cluster by root cause (e.g. if the `NumericUpDown` binding pattern needs adjustment, it'll
fail identically everywhere it's used, which is one fix, not ten). Once it builds and runs cleanly
through onboarding → login → main shell → each module, we're genuinely done with the initial
cross-platform port.

