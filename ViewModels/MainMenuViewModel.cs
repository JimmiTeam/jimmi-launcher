using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.Swift;
using System.Threading.Tasks;

namespace JimmiLauncher.ViewModels
{
    public partial class MainMenuViewModel : MenuViewModelBase
    {
        public override bool CanNavigateReplays {
            get => true;
            protected set => throw new Exception("Cannot set CanNavigateReplays in MainMenuViewModel");
        }

        public override bool CanNavigateMain { 
            get => false;
            protected set => throw new Exception("Cannot set CanNavigateMain in MainMenuViewModel");
        }

        public string Greeting { get; } = "Jimmi Launcher";
        public string ReplaysLabel { get; } = "Enable Replays";

        [ObservableProperty]
        bool isReplaysEnabled = Globals.ReplaysEnabled;

        public string RemixPlayButtonLabel { get; } = "Play Offline Smash Remix";
        public string VanillaPlayButtonLabel { get; } = "Play Offline Smash 64";
        public string NavigateToReplaysButtonLabel { get; } = "View Saved Replays";

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

        public RelayCommand PlayRemixCommand { get; set; }
        public RelayCommand PlayVanillaCommand { get; set; }
        public RelayCommand ToggleReplaysCommand { get; set; }
        public RelayCommand<object> WatchRemixCommand { get; set; }
        public RelayCommand NavigateToReplayMenuCommand { get; set; }

        private MenuViewModelBase? _currentMenu;
        public MenuViewModelBase? CurrentMenu
        {
            get => _currentMenu;
            set
            {
                if (_currentMenu != value)
                {
                    _currentMenu = value;
                    OnPropertyChanged(nameof(CurrentMenu));
                }
            }
        }

        private Action<string>? _onNavigateRequested;
        public Action<string>? OnNavigateRequested
        {
            get => _onNavigateRequested;
            set => _onNavigateRequested = value;
        }

        public MainMenuViewModel(Action<string>? onNavigateRequested = null)
        {
            _onNavigateRequested = onNavigateRequested;
            PlayRemixCommand = new RelayCommand(PlayRemix);
            PlayVanillaCommand = new RelayCommand(PlayVanilla);
            ToggleReplaysCommand = new RelayCommand(ToggleReplays);
            WatchRemixCommand = new RelayCommand<object>((param) => WatchRemix(param!));
            NavigateToReplayMenuCommand = new RelayCommand(NavigateToReplayMenu);

            // Load bitmap asynchronously to avoid blocking UI
            _ = LoadLogoAsync();
        }
        private async Task LoadLogoAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    LogoImageSource = new Bitmap("Assets/jimmi-logo2.png");
                });
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"Failed to load logo: {ex.Message}");
            }
        }
        

        private void PlayGame(string gamePath)
        {
            try
            {      
                var folder = "../mupen64plus-ui-console/projects/msvc/x64/Release";
                var arguments = $"--configdir . --datadir {folder} --plugindir {folder} {gamePath}";
                if (Globals.ReplaysEnabled)
                {
                    arguments = $"--replays {Globals.ReplaysFolderPath} " + arguments;
                }
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = Globals.MupenExecutablePath,
                    Arguments = arguments,
                    UseShellExecute = false
                };

                Debug.WriteLine($"Starting process: {processStartInfo.FileName} {processStartInfo.Arguments}");

                Process game = Process.Start(processStartInfo)!;
                game.EnableRaisingEvents = true;
                game.Exited += ReplayMethods.CompressArchives;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Could not start process for {gamePath}: {e.Message}");
            }
        }

        private void WatchReplays(string gamePath, string replayPath)
        {
            try
            {
                var folder = "../mupen64plus-ui-console/projects/msvc/x64/Release";
                var arguments = $"--configdir . --datadir {folder} --plugindir {folder} --playback {replayPath} {gamePath}";
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = Globals.MupenExecutablePath,
                    Arguments = arguments,
                    // UseShellExecute = true
                };

                Debug.WriteLine($"Starting process: {processStartInfo.FileName} {processStartInfo.Arguments}");

                Process game = Process.Start(processStartInfo)!;
                game.EnableRaisingEvents = true;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Could not start process for {gamePath} with replay {replayPath}: {e.Message}");
            }
        
        }

        private void WatchRemix(object param)
        {
            var replay = param.ToString()!;
            if (!File.Exists(Globals.RemixRomPath))
            {
                return;
            }
            var replayPath = ReplayMethods.ExtractReplayData(replay);
            WatchReplays(Globals.RemixRomPath, replayPath);
        }

        private void WatchVanilla(object param)
        {
            var replay = param.ToString()!;
            if (!File.Exists(Globals.VanillaRomPath))
            {
                return;
            }
            var replayPath = ReplayMethods.ExtractReplayData(replay);
            WatchReplays(Globals.VanillaRomPath, replayPath);
        }

        private void PlayRemix()
        {
            if (!File.Exists(Globals.RemixRomPath))
            {
                return;
            }
            PlayGame(Globals.RemixRomPath);
        }

        private void PlayVanilla()
        {
            if (!File.Exists(Globals.VanillaRomPath))
            {
                return;
            }
            PlayGame(Globals.VanillaRomPath);
        }

        private void ToggleReplays()
        {
            Globals.ReplaysEnabled = !Globals.ReplaysEnabled;
        }

        private void NavigateToReplayMenu()
        {
            _onNavigateRequested?.Invoke("Replays");
        }
    }
}