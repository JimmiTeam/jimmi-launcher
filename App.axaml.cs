using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using JimmiLauncher.ViewModels;
using JimmiLauncher.Views;
using System.Threading.Tasks;
using System.IO;

namespace JimmiLauncher;

public partial class App : Application
{
    private FileSystemWatcher? _fileWatcher;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Initialize database and globals
        DatabaseHandler.InitializeDatabase();
        Globals.InitializeGlobals();

        // Extract thumbnails
        try
        {
            ReplayMethods.ExtractThumbnails();
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to extract thumbnails: {ex.Message}");
        }

        // Initialize file watcher to run in background
        // InitializeFileWatcher();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };

            // Cleanup on application exit
            // desktop.Exit += (s, e) => _fileWatcher?.Dispose();
            desktop.Exit += (s, e) => ReplayMethods.DeleteThumbnails();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void InitializeFileWatcher()
    {
        try
        {
            _fileWatcher = new FileSystemWatcher(Globals.ReplaysFolderPath)
            {
                NotifyFilter = NotifyFilters.Attributes
                            | NotifyFilters.CreationTime
                            | NotifyFilters.DirectoryName
                            | NotifyFilters.FileName
                            | NotifyFilters.LastAccess
                            | NotifyFilters.LastWrite
                            | NotifyFilters.Security
                            | NotifyFilters.Size,
                Filter = "*.png",
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            _fileWatcher.Created += FileWatcher.OnCreated;
            // _fileWatcher.Changed += FileWatcher.OnChanged;
            // _fileWatcher.Deleted += FileWatcher.OnDeleted;
            // _fileWatcher.Renamed += FileWatcher.OnRenamed;
            // _fileWatcher.Error += FileWatcher.OnError;
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize file watcher: {ex.Message}");
        }
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}