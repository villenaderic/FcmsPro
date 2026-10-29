# FCMS Pro — User Guide

> **Prefer video?** Watch the [Official 3-Minute Video Walkthrough on YouTube](https://www.youtube.com/watch?v=wxfsJB4re-M) covering installation, key features, and client workflows.

## Getting Started

The first time you launch FCMS Pro, you'll go through a short first-run setup:

1. **Welcome** — a quick intro to FCMS Pro.
2. **Terms & Data Handling** — explains that all your data stays on this computer (no cloud sync), and that you're responsible for your own backups.
3. **Where Your Data Lives** — displays the folder your SQLite database is stored in, with an option to select a custom data folder.
4. **Quick Start Guide** — a fast overview of key concepts, shortcuts, and navigation.
5. **You're All Set** — click Finish to enter your workspace. No passwords or account setup required.

## Navigating the App

The sidebar on the left provides access to every module: **Dashboard**, **Analytics**, **Clients**, **Commissions**, **Income (Payments)**, **Expenses**, **Goals**, **Backup**, **Settings**, and **Logs**.

You can also collapse the sidebar to maximize screen space using the toggle button in the sidebar header or pressing `[`.

### Keyboard Shortcuts

Two-key shortcut sequences let you move quickly through the app (press the second key within one second):

| Shortcut | Action |
|---|---|
| `/` | Focus global search |
| `[` | Collapse or expand sidebar |
| `g` then `d` | Go to Dashboard |
| `g` then `a` | Go to Analytics |
| `g` then `c` | Go to Clients |
| `g` then `w` | Go to Commissions |
| `g` then `p` | Go to Income (Payments) |
| `g` then `e` | Go to Expenses |
| `g` then `g` | Go to Goals |
| `g` then `b` | Go to Backup |
| `g` then `l` | Go to Logs |
| `g` then `s` | Go to Settings |
| `n` then `w` | New Commission |
| `n` then `c` | New Client |
| `n` then `p` | New Payment |
| `n` then `e` | New Expense |

Shortcuts are automatically disabled while focus is inside any text box or input field.

## Clients

Add and manage your freelance clients:
- Store name, client type (individual, business, agency, etc.), contact details, and notes.
- Click **View** to open a client's profile, displaying total billed, payments received, outstanding balance, and full commission history.
- **Attachments**: attach signed contracts, design briefs, or reference documents (PDFs and images) directly to the client's profile.

> Deleting a client does **not** delete their commissions — those stay in place so historical financial records remain intact, with the client listed as deleted.

## Commissions

Track each commission from inquiry to delivery:
- **Status pipeline**: **Pending → In Progress → Revision → Completed → Delivered**, or **Cancelled**. You can update statuses directly from the list.
- **Sorting & Filtering**: filter by status or search by commission title and client name.
- **Recurring commissions**: set a recurrence frequency (weekly, biweekly, monthly) to automatically create the next commission as soon as the current one is marked **Delivered**.
- **Attachments**: upload reference images and project files directly to any commission.

## Payments & Income

Record payments against commissions to keep balances accurate:
- Recording a payment automatically decreases the commission's remaining balance.
- Filter and search past income by client, date, or amount.
- **Refunds**: if an error occurs or a payment is reversed, use **Refund** to remove the payment and automatically restore the commission's balance owed.

## Expenses & Net Profit

Log your freelance business costs:
- Track expenses by category (software subscriptions, hardware, office supplies, subcontractor fees).
- **Recurring expenses**: set weekly, biweekly, or monthly repeat schedules for ongoing expenses like cloud tools and software subscriptions.
- Net profit is calculated automatically: **Net Profit = Gross Income − Expenses**.

## Analytics & Tax Summary

Analyze your freelance business performance:
- View visual income breakdowns by category and month.
- Track gross revenue, total expenses, and net profit over time.
- **Tax & Quarterly Summary PDF**: export a clean, printable PDF report breaking down revenue, expenses, and net earnings by quarter and year for your accountant or tax filing.

## Goals

Set monthly and annual income targets and expense budgets to visualize progress towards your freelance financial benchmarks.

## Backup & Restore

- **Export**: save your entire database — clients, commissions, payments, expenses, and settings — to a portable `.json` file.
- **Import**: restore from a backup file, with options to **merge** records or **replace** all local data.
- **Automatic Backups**: FCMS Pro automatically creates local database backups upon app exit, retaining a configurable number of backup archives in your data directory.

## Trash (Soft Delete)

Accidentally deleted a client, commission, payment, or expense? Deleted items are moved to **Trash** where they can be restored or permanently removed.

## Settings & Auto-Updates

- Configure your business name, freelancer contact info, currency symbol, and tax options.
- Choose between **Dark** and **Light** themes with customizable accent colors.
- **Software Updates**: check for updates on demand with the "Check for Updates" button or review recent release notes in the "What's New" dialog.

## Logs

A read-only audit log tracking every operation: records created, updated, soft-deleted, restored, and database backups.

---

**Questions or data management?** All data is stored in a single SQLite database (`fcms.db`) on your computer. Export backups regularly via the Backup page.
