using Avalonia;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace FcmsPro.Avalonia;

internal static class Program
{
    // Sets the explicit AppUserModelID on Windows so the running application
    // binds to its own taskbar identity and uses the branded icon rather than
    // a generic or fallback host process icon.
    [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string appID);

    // Named mutex for single-instance enforcement across all three OSes.
    // Named mutexes are supported cross-platform in .NET Core/5+ on Windows,
    // macOS, and Linux (backed by different OS primitives under the hood, but
    // the .NET API surface is consistent) - zero-cost, no extra package needed.
    private const string SingleInstanceMutexName = "FcmsPro.SingleInstance.9f1c9e3a";

    private static readonly string CrashLogPath = Path.Combine(Path.GetTempPath(), "fcmspro-crash.log");

    [STAThread]
    public static void Main(string[] args)
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                SetCurrentProcessExplicitAppUserModelID("RodericVillena.FCMSPro");
            }
            catch
            {
                // Non-critical if Shell32 call fails on certain Windows environments
            }
        }
        // Belt-and-suspenders crash visibility: async void exceptions (like
        // ones thrown inside App.OnFrameworkInitializationCompleted) do NOT
        // propagate back through a normal try/catch around
        // StartWithClassicDesktopLifetime - they get routed through the
        // dispatcher/synchronization context instead, which can result in a
        // silent exit with no console output at all if something upstream
        // swallows or redirects the log. These two handlers plus the
        // AppDomain hook below guarantee SOMETHING gets written to
        // %TEMP%\fcmspro-crash.log no matter which path the exception takes.
        AppDomain.CurrentDomain.UnhandledException += (_, e) => LogCrash(e.ExceptionObject as Exception, "AppDomain.UnhandledException");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogCrash(e.Exception, "TaskScheduler.UnobservedTaskException");
            e.SetObserved();
        };

        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);

        if (!createdNew)
        {
            // Another instance is already running. Signal it to come to the
            // foreground (mechanism implemented in SingleInstanceService via a
            // named pipe) and exit immediately rather than opening a second window.
            Console.WriteLine("Another FcmsPro instance appears to be running already - signaling it and exiting. " +
                               "If no FcmsPro window is actually open, check Task Manager for a leftover FcmsPro.exe process.");
            SingleInstanceService.NotifyRunningInstance();
            return;
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            LogCrash(ex, "Main try/catch around StartWithClassicDesktopLifetime");
            throw;
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static void LogCrash(Exception? ex, string source)
    {
        if (ex is null) return;
        try
        {
            File.AppendAllText(CrashLogPath, $"[{DateTime.Now:O}] via {source}:\n{ex}\n\n");
            Console.Error.WriteLine($"FATAL ({source}): {ex}");
        }
        catch
        {
            // If even crash logging fails, there's nothing further we can do.
        }
    }
}

