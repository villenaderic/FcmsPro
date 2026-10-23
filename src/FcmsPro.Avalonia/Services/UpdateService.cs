using System;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using NetSparkleUpdater;
using NetSparkleUpdater.Enums;
using NetSparkleUpdater.SignatureVerifiers;
using NetSparkleUpdater.UI.Avalonia;
using Serilog;

namespace FcmsPro.Avalonia.Services;

public class UpdateService
{
    private const string AppcastUrl =
        "https://raw.githubusercontent.com/villenaderic/FcmsPro/main/docs/appcast.xml";

    private SparkleUpdater? _sparkle;

    public void Initialize()
    {
        try
        {
            // Logged so a "no update appeared" report can be diagnosed from the app log.
            var installed = Assembly.GetEntryAssembly()?.GetName().Version;
            Log.Information("Updater: installed version {Version}; appcast {Url}", installed, AppcastUrl);
            _ = LogAppcastReachabilityAsync();

            _sparkle = new SparkleUpdater(AppcastUrl, new Ed25519Checker(SecurityMode.Unsafe))
            {
                // A real icon instead of null, so the update window can be built safely.
                UIFactory = new UIFactory(LoadWindowIcon()!),
                RelaunchAfterUpdate = true,
            };

            // Exit immediately so the installer can overwrite the running executable.
            _sparkle.CloseApplication += () => Environment.Exit(0);

            _sparkle.StartLoop(true, true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize auto-updater.");
        }
    }

    /// <summary>For a future "Check for updates" menu item.</summary>
    public async Task CheckNowAsync()
    {
        if (_sparkle is null) return;
        await _sparkle.CheckForUpdatesAtUserRequest();
    }

    private static async Task LogAppcastReachabilityAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("FcmsPro-UpdateCheck");
            using var resp = await http.GetAsync(AppcastUrl);
            var body = await resp.Content.ReadAsStringAsync();
            Log.Information("Updater: appcast HTTP {Status}, {Length} chars, contains items: {HasItems}",
                (int)resp.StatusCode, body.Length, body.Contains("<item>", StringComparison.Ordinal));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Updater: could not reach appcast (offline or blocked?)");
        }
    }

    private static global::Avalonia.Controls.WindowIcon? LoadWindowIcon()
    {
        try
        {
            using var stream = global::Avalonia.Platform.AssetLoader.Open(
                new Uri("avares://FcmsPro.Avalonia/Assets/app-256.png"));
            return new global::Avalonia.Controls.WindowIcon(stream);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Updater: could not load window icon");
            return null;
        }
    }
}
