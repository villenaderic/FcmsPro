# FCMS Pro — User Guide

## Getting Started

The first time you launch FCMS Pro, you'll go through a short setup:

1. **Welcome** — a quick intro.
2. **Terms & Data Handling** — explains that all your data stays on this computer (no cloud sync), and that you're responsible for your own backups.
3. **Where Your Data Lives** — shows the folder your database is stored in.
4. **Create Your Admin Account** — set a username and password. This protects the app on this computer; it cannot be recovered if forgotten, only reset by deleting your local data.
5. **You're All Set** — click Finish to start using the app.

After that first run, you'll see a login screen instead — enter your password to get in.

## Navigating the App

The sidebar on the left lists every section: Dashboard, Analytics, Clients, Commissions, Quotes, Payments, Receipts, Invoices, Expenses, Goals, Templates, Backup, Logs, and Settings.

### Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| `/` | Focus the search box |
| `?` | Show/hide the shortcuts overlay |
| `[` | Collapse/expand the sidebar |
| `Shift` + `T` | Toggle light/dark theme |
| `r` | Refresh the current page |
| `g` then a letter | Jump to a page (e.g. `g` `c` → Clients, `g` `w` → Commissions) |
| `n` then a letter | Open a "new" form (e.g. `n` `c` → New Client, `n` `w` → New Commission) |

Shortcuts are disabled while you're typing in a text field.

## Clients

Add and manage your clients — name, type (individual, business, government, etc.), contact info, and notes. Clicking a client's **View** button opens their profile, showing everything they owe and have paid, plus their commission history.

> Deleting a client does **not** delete their commissions — those stay in place but no longer show a client name.

## Commissions

Track each job from start to finish: **Pending → In Progress → Revision → Completed → Delivered**, or **Cancelled** at any point. You can change status right from the list without opening the full form.

**Recurring commissions**: if you set a repeat frequency (weekly, biweekly, monthly), a new commission is automatically created — with the balance reset to the full price — as soon as the current one reaches **Delivered**.

## Quotes

Send a scope of work and price before committing to a commission. Once a quote is **Accepted**, use **Convert to Commission** to open a pre-filled commission form — you'll still need to hit Save to actually create it.

## Payments & Receipts

Recording a payment against a commission automatically:
- Reduces that commission's remaining balance.
- Generates a receipt you can download as a PDF.

If you need to undo a payment, use **Refund** — this deletes the payment and its receipt, and restores the balance owed. There's no separate "edit payment" option; refund and re-record instead.

## Invoices

Standalone billing documents — they can be linked to a commission or created independently. **Mark Paid** just updates the invoice's status; it doesn't check whether an actual payment was recorded against it, so keep your Payments and Invoices in sync manually if you use both.

## Expenses & Goals

Log business expenses by category to track your net profit (Income − Expenses). Set monthly/yearly income and expense targets under Goals to see how you're tracking.

## Templates

Reusable starting points for common jobs (the app ships with 8 defaults, like "Icon Commission" or "Logo Package"). Click **Use** on a template to open a pre-filled new-commission form. **Reset Defaults** restores the original 8 without touching any custom templates you've made.

## Backup

- **Export** saves everything — clients, commissions, payments, invoices, and more — to a single `.json` file. Save this somewhere safe; it's your only way to recover your data if something happens to this computer.
- **Import** restores from a backup file, including one exported from the older web version of this app. You can choose to **merge** (add/update records) or **replace** (wipe and restore everything).
- Your password is **never** included in a backup, in either direction — importing a backup does not change or restore your login.

## Settings

Business info (name, address, contact details, currency symbol), document defaults (invoice/quote terms, receipt footer), appearance (theme, accent color), and changing your password all live here.

## Logs

A read-only history of everything that's happened in the app — records created, updated, deleted, logins, and backups/restores. Useful for retracing your steps.

---

**Questions or something not working as expected?** All of your data lives in a single SQLite file on this computer — back it up regularly via the Backup page.
