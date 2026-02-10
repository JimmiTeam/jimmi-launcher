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
        public override bool CanNavigateReplays { get; protected set; } = true;

        public override bool CanNavigateMain { get; protected set; } = false;
        public override bool CanNavigateOnline { get; protected set; } = true;
        public override bool CanNavigateOffline { get; protected set; } = true;


        public string Greeting { get; } = "Jimmi Launcher";

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
        public RelayCommand NavigateToReplayMenuCommand { get; set; }
        public RelayCommand NavigateToOnlineMenuCommand { get; set; }
        public RelayCommand NavigateToOfflineMenuCommand { get; set; }
        public RelayCommand NavigateToSettingsMenuCommand { get; set; }
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
            NavigateToReplayMenuCommand = new RelayCommand(NavigateToReplayMenu);
            NavigateToOnlineMenuCommand = new RelayCommand(NavigateToOnlineMenu);
            NavigateToOfflineMenuCommand = new RelayCommand(NavigateToOfflineMenu);
            NavigateToSettingsMenuCommand = new RelayCommand(NavigateToSettingsMenu);
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

        private void NavigateToReplayMenu()
        {
            _onNavigateRequested?.Invoke("Replays");
        }

        private void NavigateToOnlineMenu()
        {
            _onNavigateRequested?.Invoke("Online");
        }

        private void NavigateToOfflineMenu()
        {
            _onNavigateRequested?.Invoke("Offline");
        }

        private void NavigateToSettingsMenu()
        {
            _onNavigateRequested?.Invoke("Settings");
        }

        public async Task AddGameAsync(string filePath)
        {
            try
            {
                var service = new RomManagementService();
                await service.AddRomAsync(filePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to add rom: {ex.Message}");
            }
        }
    }
}