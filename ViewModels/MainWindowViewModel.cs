
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.Threading.Tasks;


namespace JimmiLauncher.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public string Greeting { get; } = "Jimmi Launcher";
    public string ReplaysLabel { get; } = "Enable Replays";

    [ObservableProperty]
     bool isReplaysEnabled = false;

    public string RemixPlayButtonLabel { get; } = "Play Smash Remix";
    public string VanillaPlayButtonLabel { get; } = "Play Vanilla Smash 64";
    public string WatchReplaysButtonLabel { get; } = "View Saved Replays";

    private Bitmap? _logoImageSource;
    public Bitmap? LogoImageSource
    {
        get => _logoImageSource;
        private set
        {
            if (_logoImageSource != value)
            {
                _logoImageSource = value;
                OnPropertyChanged(nameof(LogoImageSource));
            }
        }
    }
    private Bitmap? _backgroundImageSource;
    public Bitmap? BackgroundImageSource
    {
        get => _backgroundImageSource;
        private set
        {
            if (_backgroundImageSource != value)
            {
                _backgroundImageSource = value;
                OnPropertyChanged(nameof(BackgroundImageSource));
            }
        }
    }

    public RelayCommand PlayRemixCommand { get; }
    public RelayCommand PlayVanillaCommand { get; }
    public RelayCommand ToggleReplaysCommand { get; }

    public MainWindowViewModel()
    {
        PlayRemixCommand = new RelayCommand(PlayRemix);
        PlayVanillaCommand = new RelayCommand(PlayVanilla);
        ToggleReplaysCommand = new RelayCommand(ToggleReplays);

        // Load bitmap asynchronously to avoid blocking UI
        _ = LoadLogoAsync();
        _ = LoadBackgroundAsync();
    }
    private async Task LoadLogoAsync()
    {
        try
        {
            await Task.Run(() =>
            {
                LogoImageSource = new Bitmap("Assets/jimmi-logo.png");
            });
        }
        catch (System.Exception ex)
        {
            Debug.WriteLine($"Failed to load logo: {ex.Message}");
        }
    }
    private async Task LoadBackgroundAsync()
    {
        try
        {
            await Task.Run(() =>
            {
                BackgroundImageSource = new Bitmap("Assets/background.png");
            });
        }
        catch (System.Exception ex)
        {
            Debug.WriteLine($"Failed to load background: {ex.Message}");
        }
    }

    private void PlayRemix()
    {
        var folder = "../mupen64plus-ui-console/projects/msvc/x64/Release";
        var arguments = $"--configdir . --datadir {folder} --plugindir {folder} {Globals.RemixRomPath}";
        if (Globals.ReplaysEnabled)
        {
            arguments = "--replays " + arguments;
        }
        var processStartInfo = new ProcessStartInfo
        {
            FileName = Globals.MupenExecutablePath,
            Arguments = arguments,
            UseShellExecute = true
        };

        Debug.WriteLine($"Starting process: {processStartInfo.FileName} {processStartInfo.Arguments}");

        Process.Start(processStartInfo);
    }

    private void PlayVanilla()
    {
        var folder = "../mupen64plus-ui-console/projects/msvc/x64/Release";
        var arguments = $"--configdir . --datadir {folder} --plugindir {folder} {Globals.VanillaRomPath}";
        if (Globals.ReplaysEnabled)
        {
            arguments = "--replays " + arguments;
        }
        var processStartInfo = new ProcessStartInfo
        {
            FileName = Globals.MupenExecutablePath,
            Arguments = arguments,
            UseShellExecute = true
        };

        Process.Start(processStartInfo);
    }

    private void ToggleReplays()
    {
        Globals.ReplaysEnabled = !Globals.ReplaysEnabled;
    }
}