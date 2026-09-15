using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FcmsPro.Avalonia.Views.Shared;

public partial class ConfirmDialogWindow : Window
{
    public ConfirmDialogWindow() => InitializeComponent();

    private void OnConfirmClick(object? sender, RoutedEventArgs e) => Close(true);
    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
