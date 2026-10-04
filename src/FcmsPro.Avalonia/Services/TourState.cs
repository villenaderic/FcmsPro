using System;
using System.IO;

namespace FcmsPro.Avalonia.Services;

/// <summary>
/// One-shot "start the guided tour on next MainWindow open" flag, stored as an
/// empty marker file in the app data folder. Onboarding raises it when the
/// user finishes the first-run wizard; MainWindow consumes it once. Using a
/// file (instead of a new UiPreferences column) avoids a database migration
/// for what is purely transient UI state.
/// </summary>
public static class TourState
{
    private static string FlagPath => Path.Combine(Data.FcmsPaths.GetAppDataDirectory(), "tour-pending.flag");

    public static void RequestStart()
    {
        try { File.WriteAllBytes(FlagPath, Array.Empty<byte>()); }
        catch { /* non-critical - worst case the tour just doesn't auto-start */ }
    }

    /// <summary>Returns true (and clears the flag) if a tour start was requested.</summary>
    public static bool ConsumePending()
    {
        try
        {
            if (!File.Exists(FlagPath)) return false;
            File.Delete(FlagPath);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
