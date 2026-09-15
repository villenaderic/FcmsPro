using CommunityToolkit.Mvvm.ComponentModel;
using FcmsPro.Avalonia.Services;

namespace FcmsPro.Avalonia.ViewModels.Shell;

/// <summary>Shown for any AppPage that doesn't have a real module implementation yet.</summary>
public partial class PlaceholderPageViewModel : ObservableObject
{
    [ObservableProperty] private string _pageName;

    public PlaceholderPageViewModel(AppPage page) => _pageName = page.ToString();
}
