using CommunityToolkit.Mvvm.ComponentModel;

namespace FcmsPro.Avalonia.ViewModels.Shared;

public partial class ConfirmDialogViewModel : ObservableObject
{
    [ObservableProperty] private string _title;
    [ObservableProperty] private string _message;
    [ObservableProperty] private string _confirmLabel;
    [ObservableProperty] private string _cancelLabel;

    /// <summary>True if this represents a destructive action (delete) - lets the view style the confirm button red.</summary>
    [ObservableProperty] private bool _isDestructive;

    public ConfirmDialogViewModel(string title, string message, bool isDestructive = false,
        string confirmLabel = "Confirm", string cancelLabel = "Cancel")
    {
        _title = title;
        _message = message;
        _isDestructive = isDestructive;
        _confirmLabel = confirmLabel;
        _cancelLabel = cancelLabel;
    }
}
