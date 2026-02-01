using Avalonia.Controls;
using JimmiLauncher.ViewModels;
using Avalonia.Interactivity;
using Avalonia;

namespace JimmiLauncher.Views;

public partial class OnlineMenuView : UserControl
{
    public OnlineMenuView()
    {
        InitializeComponent();
    }

    private void CopyRoomCode_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is OnlineMenuViewModel viewModel && !string.IsNullOrEmpty(viewModel.CreatedRoomCode))
        {
            var topLevel = TopLevel.GetTopLevel(this);
            topLevel?.Clipboard?.SetTextAsync(viewModel.CreatedRoomCode);
        }
    }
}