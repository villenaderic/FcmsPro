# FCMS Pro

A desktop app for freelancers to track commissions, invoices, quotes, payments, and expenses.
Runs entirely on your own machine. No account, no server, no subscription.

Built with Avalonia UI on .NET 8. One codebase, native builds for Windows, macOS, and Linux.

## Why local-first

Most commission and invoice tools for freelancers are subscription SaaS products with your
financial data sitting on someone else's server. FCMS Pro takes the opposite approach: everything
runs on your own machine, in a single SQLite file you control. No account to create, no monthly
fee, no risk of a company shutting down and taking your records with it.

## Screenshots

<!-- Add screenshots here, for example:
![Dashboard](docs/screenshots/dashboard.png)
![Kanban board](docs/screenshots/kanban.png)
-->

## Download

Get the latest release for your operating system from the
[Releases page](https://github.com/villenaderic/FcmsPro/releases).

| Platform | File |
|---|---|
| Windows | `.exe` installer |
| macOS | `.dmg` |
| Linux | `.deb` or `.AppImage` |

### About the security warning on first launch

This app is not code signed, since that requires a paid certificate. Windows will likely show a
SmartScreen warning ("Windows protected your PC"), and macOS will show a Gatekeeper warning
("cannot be opened because the developer cannot be verified"). This is normal for small
independently distributed apps and does not mean anything is wrong with the download, as long as
you got it from this repository's own Releases page. On Windows, click "More info" then "Run
anyway." On macOS, right click the app and choose "Open," then confirm.

## Features

- **Dashboard** with income, balances, overdue items, and due-soon warnings at a glance
- **Clients** with profile pages, activity history, and file attachments for things like signed
  contracts
- **Commissions** with both a table view and a kanban board, plus optional recurring commissions
- **Quotes and Invoices** with PDF export and file attachments
- **Payments and Receipts** with automatic balance tracking and refund handling
- **Expenses** with category tracking and support for recurring expenses like subscriptions
- **Analytics** with income breakdowns and a tax and quarterly summary export
- **Global search** across clients, commissions, invoices, quotes, and expenses, triggered by
  pressing `/` anywhere in the app
- **Automatic local backups** with a configurable retention count, plus manual export and import
- **Soft delete and Trash** so a client or commission you remove by accident isn't gone for good
- **Templates and Goals** for repeatable commission types and income targets

## Your data stays local

FCMS Pro stores everything in a SQLite file on your own computer. There is no server, no account,
and no telemetry. This also means backups are your responsibility. Use the built in backup and
export feature regularly, since there is no cloud copy to restore from if the file is lost or
corrupted.

## Built with

- [Avalonia UI](https://avaloniaui.net/) for the cross-platform interface
- .NET 8
- Entity Framework Core with SQLite
- PdfSharpCore for PDF generation
- xUnit and Moq for testing

## Building from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
git clone https://github.com/villenaderic/FcmsPro.git
cd FcmsPro
dotnet restore
dotnet build
dotnet run --project src/FcmsPro.Avalonia
```

Database migrations are already checked into `src/FcmsPro.Data/Migrations` and are applied
automatically on first launch. There is no manual migration step for a normal run.

Run the test suite with:

```bash
dotnet test src/FcmsPro.Tests
```

## Building installers yourself

Each platform has its own packaging script under `installers/`. Windows packaging needs Inno
Setup and must run on Windows. The macOS `.dmg` needs `create-dmg` and must run on macOS. The
Linux scripts need `dpkg-deb` and `appimagetool`. See `RELEASE_CHECKLIST.md` for the full process,
including how the automated build in `.github/workflows/` works if you want to set up your own
fork's releases.

## Reporting bugs

Please open an [issue](../../issues). The bug report template asks for your version and operating
system, which helps a lot. Screenshots help even more.

## Contributing

Feature requests and pull requests are welcome. For anything nontrivial, opening an issue first to
discuss the approach is a good idea before writing code.

## Documentation

`docs/USER_GUIDE.md` covers onboarding and every module in detail. `CHANGELOG.md` has the full
release history.

## License

MIT. See `LICENSE`.
