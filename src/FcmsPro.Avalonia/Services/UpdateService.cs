using System;
using System.Threading.Tasks;
using NetSparkleUpdater;
using NetSparkleUpdater.Enums;
using NetSparkleUpdater.SignatureVerifiers;
using NetSparkleUpdater.UI.Avalonia;
using Serilog;

namespace FcmsPro.Avalonia.Services;

public class UpdateService
{
    private SparkleUpdater? _sparkle;

    public void Initialize()
    {
        try
        {
            // For GitHub, we can point to a raw appcast.xml file that we host in the repo,
            // OR use the NetSparkleUpdater.Github extension. For simplicity and robustness,
            // we will point it to an appcast.xml that we will generate on the website/repo.
            string appcastUrl = "https://raw.githubusercontent.com/villenaderic/FcmsPro/main/docs/appcast.xml";
            
            _sparkle = new SparkleUpdater(appcastUrl, new Ed25519Checker(SecurityMode.Unsafe))
            {
                UIFactory = new UIFactory(null!), // Avalonia UI factory
                RelaunchAfterUpdate = true,
            };

            // Check for updates in the background
            _sparkle.StartLoop(true, true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize auto-updater.");
        }
    }
}
