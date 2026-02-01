using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using JimmiLauncher.ViewModels;
using System.Linq;

namespace JimmiLauncher.Views;

public partial class MainMenuView : UserControl
{
    public MainMenuView()
    {
        InitializeComponent();
    }

    private async void AddGame_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select ROM File",
            AllowMultiple = false
        });

        if (files.Count >= 1)
        {
            if (DataContext is MainMenuViewModel vm)
            {
               await vm.AddGameAsync(files[0].Path.LocalPath);
            }
        }
    }
}