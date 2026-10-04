namespace FcmsPro.Core.Entities;

/// <summary>
/// Singleton row (Id fixed = 1). Replaces the PWA's flat `fcms_settings`
/// localStorage blob - see Phase 1 audit §1. Business/document config only;
/// UI-only prefs live in UiPreferences so a "restore business data" action
/// doesn't silently change appearance settings too.
/// </summary>
public class AppSettings
{
    public int Id { get; set; } = 1;

    public string? BusinessName { get; set; }
    public string? FreelancerName { get; set; }
    public string? Address { get; set; }
    public string? ContactNumber { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? Tin { get; set; }

    public string CurrencySymbol { get; set; } = "\u20b1"; // PHP peso, matches PWA default

    /// <summary>JSON-serialized string array of accepted payment methods (labels), user-editable.</summary>
    public string PaymentMethodsJson { get; set; } = "[]";

    /// <summary>JSON-serialized string array. Free-text service type list, not a hard enum (matches PWA).</summary>
    public string ServiceTypesJson { get; set; } = "[]";
}

/// <summary>
/// Singleton row (Id fixed = 1). UI-only preferences, kept separate from
/// AppSettings so backup/restore of business data doesn't touch appearance.
/// </summary>
public class UiPreferences
{
    public int Id { get; set; } = 1;

    public string Theme { get; set; } = "System"; // "Light" | "Dark" | "System"
    public string Density { get; set; } = "Comfortable";
    public string AccentColor { get; set; } = "#6366F1";
    // Unused since the Kanban board was removed - left in place (rather than
    // a migration to drop the column) since an unread column is harmless and
    // dropping it isn't worth a schema change of its own.
    public string CommissionsView { get; set; } = "Table";
    public DateTimeOffset? LastBackupAt { get; set; }
    public bool AutoBackupReminderEnabled { get; set; } = true;

    /// <summary>
    /// Silently writes a full backup JSON to the app data folder's backups/
    /// subfolder on every clean app close, independent of - and a stronger
    /// safety net than - AutoBackupReminderEnabled above (which only nags
    /// the user to manually export a copy somewhere, e.g. external
    /// storage/cloud drive, which is still the right move for surviving a
    /// dead machine - these on-disk auto-backups live on the SAME disk).
    /// Defaults on since it's a pure safety net with no real downside.
    /// </summary>
    public bool AutoBackupOnCloseEnabled { get; set; } = true;

    /// <summary>How many rotating auto-backup files to keep before deleting the oldest.</summary>
    public int AutoBackupKeepCount { get; set; } = 7;
}
