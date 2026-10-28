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

    /// <summary>Ed25519 public key used to verify release package signatures.</summary>
    public const string PublicKey = "lw1Jki2HknZpQQwcC1h3Sk282FtS9G303XLgscGcVpQ=";

    private SparkleUpdater? _sparkle;

    public void Initialize()
    {
        try
        {
            // Logged so a "no update appeared" report can be diagnosed from the app log.
            var installed = Assembly.GetEntryAssembly()?.GetName().Version;
            Log.Information("Updater: installed version {Version}; appcast {Url}", installed, AppcastUrl);
            _ = LogAppcastReachabilityAsync();

            _sparkle = new SparkleUpdater(AppcastUrl, new Ed25519Checker(SecurityMode.Strict, PublicKey))
            {
                // A real icon instead of null, so the update window can be built safely.
                UIFactory = new UIFactory(LoadWindowIcon()!),
                RelaunchAfterUpdate = true,
                CustomInstallerArguments = OperatingSystem.IsWindows()
                    ? "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-"
                    : "",
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

    public string CurrentVersion
    {
        get
        {
            var v = Assembly.GetEntryAssembly()?.GetName().Version;
            return v is not null ? $"{v.Major}.{v.Minor}.{v.Build}" : "1.6.9";
        }
    }

    public DateTime? LastCheckedTime { get; private set; }

    /// <summary>Checks for updates on demand (invoked from Settings).</summary>
    public async Task CheckNowAsync()
    {
        LastCheckedTime = DateTime.Now;
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
                new Uri("avares://FcmsPro/Assets/app-256.png"));
            return new global::Avalonia.Controls.WindowIcon(stream);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Updater: could not load window icon");
            return null;
        }
    }
}
