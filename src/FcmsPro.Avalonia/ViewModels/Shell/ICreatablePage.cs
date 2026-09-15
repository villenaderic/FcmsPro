using CommunityToolkit.Mvvm.Input;

namespace FcmsPro.Avalonia.ViewModels.Shell;

/// <summary>
/// Implemented by any page ViewModel that has an "Add" action, so
/// MainWindowViewModel can invoke it generically when KeySequenceService's
/// "n"-prefix shortcut (nw, nc, np, ...) fires for the page currently on screen.
/// </summary>
public interface ICreatablePage
{
    IAsyncRelayCommand AddCommand { get; }
}
