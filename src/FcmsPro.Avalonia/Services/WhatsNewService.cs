using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Serilog;

namespace FcmsPro.Avalonia.Services;

public class WhatsNewService
{
    private class VersionState
    {
        public string? LastSeenVersion { get; set; }
    }

    private static string StateFilePath =>
        Path.Combine(Data.FcmsPaths.GetAppDataDirectory(), "version-state.json");

    public static string GetCurrentVersion()
    {
        var v = Assembly.GetEntryAssembly()?.GetName().Version;
        return v is not null ? $"{v.Major}.{v.Minor}.{v.Build}" : "1.6.9";
    }

    public static bool ShouldShowWhatsNew(out string currentVersion, out string highlights)
    {
        currentVersion = GetCurrentVersion();
        highlights = GetReleaseNotesForVersion(currentVersion);

        try
        {
            var path = StateFilePath;
            if (!File.Exists(path))
            {
                // First run on this machine: record version and do not pop up during initial onboarding.
                SaveLastSeenVersion(currentVersion);
                return false;
            }

            var json = File.ReadAllText(path);
            var state = JsonSerializer.Deserialize<VersionState>(json);
            if (state?.LastSeenVersion != currentVersion)
            {
                SaveLastSeenVersion(currentVersion);
                return !string.IsNullOrWhiteSpace(highlights);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "WhatsNewService: could not check version state");
        }

        return false;
    }

    public static void SaveLastSeenVersion(string version)
    {
        try
        {
            var state = new VersionState { LastSeenVersion = version };
            File.WriteAllText(StateFilePath, JsonSerializer.Serialize(state));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "WhatsNewService: could not save version state");
        }
    }

    public static string GetReleaseNotesForVersion(string version)
    {
        try
        {
            var assembly = typeof(WhatsNewService).Assembly;
            using var stream = assembly.GetManifestResourceStream("FcmsPro.Avalonia.Assets.CHANGELOG.md")
                               ?? assembly.GetManifestResourceStream("FcmsPro.Assets.CHANGELOG.md");
            if (stream is null) return string.Empty;
            using var reader = new StreamReader(stream);
            var markdown = reader.ReadToEnd();

            var pattern = $@"##\s*\[{Regex.Escape(version)}\][^\r\n]*\r?\n([\s\S]*?)(?=\r?\n##\s*\[|\Z)";
            var match = Regex.Match(markdown, pattern);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "WhatsNewService: could not read embedded changelog");
        }

        return string.Empty;
    }
}
