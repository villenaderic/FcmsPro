using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace FcmsPro.Avalonia;

/// <summary>
/// Pairs with Program.cs's named-mutex check: the first instance opens a named
/// pipe server and listens for a "activate" signal; a second launch attempt
/// (which loses the mutex) connects to that pipe and sends the signal, then
/// exits. The running instance's listener bounces the request onto the UI
/// thread to restore/focus the main window. Zero-cost - System.IO.Pipes is
/// part of the BCL on all three target OSes.
/// </summary>
public static class SingleInstanceService
{
    private const string PipeName = "FcmsPro.Activate.9f1c9e3a";
    public static event Action? ActivationRequested;

    public static void NotifyRunningInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(timeout: 500);
            using var writer = new StreamWriter(client);
            writer.WriteLine("activate");
            writer.Flush();
        }
        catch
        {
            // Best-effort: if the other instance isn't listening yet or the
            // pipe fails, there's nothing meaningful to do besides exit anyway.
        }
    }

    public static void StartListening(CancellationToken ct)
    {
        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PipeName, PipeDirection.In);
                    await server.WaitForConnectionAsync(ct);
                    using var reader = new StreamReader(server);
                    var message = await reader.ReadLineAsync();
                    if (message == "activate")
                        ActivationRequested?.Invoke();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Pipe errors shouldn't crash the app - just retry the listen loop.
                }
            }
        }, ct);
    }
}
