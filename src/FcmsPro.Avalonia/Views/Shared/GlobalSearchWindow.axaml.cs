using Avalonia.Controls;
using Avalonia.Threading;

namespace FcmsPro.Avalonia.Views.Shared;

public partial class GlobalSearchWindow : Window
{
    public GlobalSearchWindow()
    {
        InitializeComponent();
        Opened += (_, _) => Dispatcher.UIThread.Post(() => QueryBox.Focus());
    }
}
