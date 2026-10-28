using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FcmsPro.Avalonia.Views.Shared;

public partial class WhatsNewWindow : Window
{
    public WhatsNewWindow()
    {
        InitializeComponent();
    }

    public WhatsNewWindow(string version, string notes) : this()
    {
        VersionBadge.Text = $"v{version}";
        NotesBlock.Text = string.IsNullOrWhiteSpace(notes)
            ? "Bug fixes and performance improvements."
            : notes;
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
