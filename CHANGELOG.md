# Changelog

All notable changes to FCMS Pro are documented here. Format loosely follows
[Keep a Changelog](https://keepachangelog.com/).

## [1.1.2]

### Fixed
- Light theme now actually changes the app's appearance. Previously the Settings > Appearance >
  Theme dropdown had no visible effect.
- Search boxes on Clients, Payments, Receipts, Quotes, Invoices, and Commissions no longer
  overflow past the window edge when the window is narrow.

### Changed
- Sharpened the app icon at small sizes (16px and 32px) for better clarity in title bars and
  taskbars.

## [1.1.1]

### Fixed
- Removed a stray developer-name reference from the onboarding terms screen.

## [1.1.0]

### Added
- Recurring expenses — set a monthly/biweekly/weekly schedule on an expense
  (software subscriptions, retainers) and it re-logs itself automatically.
- Global search — press `/` anywhere in the app to search clients,
  commissions, invoices, quotes, and expenses at once.
- Due-soon warnings on the Dashboard — a second banner alongside the
  existing overdue one, for commissions/invoices coming due in the next few
  days rather than only after the deadline has passed.
- File attachments on Clients, Invoices, and Quotes (previously Commissions
  only) — supports images and PDFs, useful for signed contracts and
  reference documents.
- Tax/quarterly summary export — year and quarter breakdown of income,
  expenses, and net, exportable as a PDF from the Analytics page.
- Expanded automated test coverage across services and view models.

## [1.0.2]

### Added
- Soft delete and a Trash page — deleted clients, commissions, payments,
  expenses, quotes, and invoices are recoverable for 30 days instead of
  being removed immediately.
- Kanban board view for Commissions, toggleable alongside the table view.
- Scheduled automatic local backups on app close, with a configurable
  retention count.
- Overdue notifications — a dashboard banner and KPI tiles for overdue
  commissions and invoices.
- Reference image attachments on Commissions.

### Changed
- Activity log moved from the main sidebar into Settings → Advanced.

### Removed
- Unused PNG receipt export path (never implemented, unreachable from the UI).

## [1.0.1]

### Fixed
- Crash when updating a commission or invoice already tracked in the same
  session (Entity Framework change-tracker key collision).
- Client/commission dropdowns silently appearing empty on a load failure.
- Main window and dialog windows opening partially off-screen on smaller
  displays.
- Thermal and A5 receipts missing the client's name and contact info.
- Several numeric input fields crashing when cleared to blank.

### Added
- Thermal (80mm) receipt format alongside the existing A5 layout.
- Optional automatic invoice generation when a commission is marked
  Delivered with an outstanding balance.

### Changed
- Sidebar auto-collapses on narrow windows; minimum window width lowered to
  support split-screen use.

## [1.0.0]

### Fixed
- Currency symbol setting not being applied to any price display in the app.
- Theme/accent color not persisting across restarts.
- Text fields and dropdowns rendering unreadably white-on-white when focused.
- Several numeric fields crashing when cleared to blank.

### Added
- Initial release: Dashboard, Clients, Commissions, Quotes, Payments,
  Receipts, Invoices, Expenses, Goals, Templates, Analytics, Backup, Logs,
  and Settings.
