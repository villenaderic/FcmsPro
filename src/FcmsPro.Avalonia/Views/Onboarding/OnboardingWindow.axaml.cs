using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using FcmsPro.Avalonia.ViewModels.Onboarding;
using FcmsPro.Avalonia.ViewModels.Shell;
using FcmsPro.Avalonia.Views.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace FcmsPro.Avalonia.Views.Onboarding;

public partial class OnboardingWindow : Window
{
    public OnboardingWindow()
    {
        InitializeComponent();
        try
        {
            Icon = new WindowIcon(global::Avalonia.Platform.AssetLoader.Open(new System.Uri("avares://FcmsPro/Assets/app.ico")));
        }
        catch
        {
            // Fallback to XAML Icon attribute
        }
        DataContextChanged += (_, _) =>
        {
            if (DataContext is OnboardingFlowViewModel vm)
                vm.OnboardingCompleted += OnOnboardingCompleted;
        };
    }

    private void OnOnboardingCompleted()
    {
        var mainVm = App.Services.GetRequiredService<MainWindowViewModel>();
        var mainWindow = new MainWindow { DataContext = mainVm };

        if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = mainWindow;

        mainWindow.Show();
        Close();
    }
}
