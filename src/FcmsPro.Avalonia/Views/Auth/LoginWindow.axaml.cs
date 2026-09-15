using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using FcmsPro.Avalonia.ViewModels.Auth;
using FcmsPro.Avalonia.ViewModels.Shell;
using FcmsPro.Avalonia.Views.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace FcmsPro.Avalonia.Views.Auth;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is LoginViewModel vm)
                vm.LoginSucceeded += OnLoginSucceeded;
        };
    }

    private void OnPasswordKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is LoginViewModel vm && vm.SubmitCommand.CanExecute(null))
            vm.SubmitCommand.Execute(null);
    }

    private void OnLoginSucceeded()
    {
        var mainVm = App.Services.GetRequiredService<MainWindowViewModel>();
        var mainWindow = new MainWindow { DataContext = mainVm };

        if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = mainWindow;

        mainWindow.Show();
        Close();
    }
}
