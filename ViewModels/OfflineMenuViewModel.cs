using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

namespace JimmiLauncher.ViewModels;

public partial class OfflineMenuViewModel : MenuViewModelBase
{
    public override bool CanNavigateReplays { get; protected set; } = false;
    public override bool CanNavigateMain { get; protected set; } = true;
    public override bool CanNavigateOnline { get; protected set; } = false;
    public override bool CanNavigateOffline { get; protected set; } = false;

    [ObservableProperty]
    private bool _isReplaysEnabled = Globals.ReplaysEnabled;
    partial void OnIsReplaysEnabledChanged(bool value)
    {
        Globals.ReplaysEnabled = value;
    }


    private void PlayGame(string gamePath)
    {
        try
        {      
            var folder = "../mupen64plus-ui-console/projects/msvc/x64/Release";
            var arguments = $"--configdir . --datadir {folder} --plugindir {folder} {gamePath}";
            
            // The original code prepended it.
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
            if(Globals.ReplaysEnabled) {
                game.Exited += ReplayMethods.CompressArchives;
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Could not start process for {gamePath}: {e.Message}");
        }
    }

    private readonly Action<string>? _onNavigateRequested;

    [ObservableProperty]
    private System.Collections.ObjectModel.ObservableCollection<GameRom> _availableGames = new();

    [ObservableProperty]
    private GameRom? _selectedGame;

    public OfflineMenuViewModel(Action<string>? onNavigateRequested = null)
    {
        _onNavigateRequested = onNavigateRequested;
        try {
            AvailableGames = new System.Collections.ObjectModel.ObservableCollection<GameRom>(DatabaseHandler.GetGames());
            SelectedGame = System.Linq.Enumerable.FirstOrDefault(AvailableGames);
        } catch {} 
    }

    [RelayCommand]
    private void PlaySelectedGame()
    {
        if (SelectedGame != null)
            PlayGame(SelectedGame.GamePath);
    }

    [RelayCommand]
    private void NavigateToMain()
    {
        _onNavigateRequested?.Invoke("Main");
    }
}
