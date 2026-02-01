
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;


namespace JimmiLauncher.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {

        private MenuViewModelBase _currentMenu;
        public MenuViewModelBase CurrentMenu
        {
            get => _currentMenu;
            set => SetProperty(ref _currentMenu, value);
        }

        public RelayCommand<string> NavigateToMenuCommand { get; set;}

        public void NavigateToMenu(string menuName)
        {
            switch (menuName)
            {
                case "Main":
                    CurrentMenu = new MainMenuViewModel(NavigateToMenu);
                    break;
                case "Replays":
                    CurrentMenu = new ReplayMenuViewModel(NavigateToMenu);
                    break;
                case "Online":
                    CurrentMenu = new OnlineMenuViewModel(NavigateToMenu);
                    break;
                case "Offline":
                    CurrentMenu = new OfflineMenuViewModel(NavigateToMenu);
                    break;
                default:
                    throw new ArgumentException($"Unknown menu: {menuName}");
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

        public MainWindowViewModel()
        {
            NavigateToMenuCommand = new RelayCommand<string>(NavigateToMenu!);
            CurrentMenu = new MainMenuViewModel(NavigateToMenu);

            _ = LoadBackgroundAsync();
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
    }
}
