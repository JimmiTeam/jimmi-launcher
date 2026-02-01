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

public partial class OnlineMenuViewModel : MenuViewModelBase
{
    private static readonly HttpClient _httpClient = new HttpClient();
    private const string BaseUrl = "http://45.76.57.98:8080/v1";
    private readonly Action<string>? _onNavigateRequested;

    public override bool CanNavigateReplays { get; protected set; } = true;
    public override bool CanNavigateMain { get; protected set; } = true;
    public override bool CanNavigateOnline { get; protected set; } = false;
    public override bool CanNavigateOffline { get; protected set; } = false;

    [ObservableProperty]
    private string _joinCode = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _createdRoomCode = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRoomReady))]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isReplaysEnabled = Globals.ReplaysEnabled;

    partial void OnIsReplaysEnabledChanged(bool value)
    {
        Globals.ReplaysEnabled = value;
    }

    public bool IsRoomReady => !string.IsNullOrEmpty(Globals.OnlineHostToken) || !string.IsNullOrEmpty(Globals.OnlineClientToken);

    [RelayCommand]
    private void StartGame()
    {
         if (!File.Exists(Globals.RemixRomPath))
        {
            StatusMessage = "Remix ROM not found.";
            return;
        }
        PlayGame(Globals.RemixRomPath);
    }

    private void PlayGame(string gamePath)
    {
        try
        {      
            var folder = "../mupen64plus-ui-console/projects/msvc/x64/Release";
            var arguments = $"--netplay --netplayrelayhost 45.76.57.98 --configdir . --datadir {folder} --plugindir {folder} {gamePath}";
            
            // Online Tokens
            if (!string.IsNullOrEmpty(Globals.OnlineHostToken))
            {
                arguments = $"--netplaytoken {Globals.OnlineHostToken} --netplayhosting " + arguments;
            }
            else if (!string.IsNullOrEmpty(Globals.OnlineClientToken))
            {
                 arguments = $"--netplaytoken {Globals.OnlineClientToken} " + arguments;
            }

            // Replays - must be prepended or appended carefully. 
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
            StatusMessage = $"Error starting game: {e.Message}";
        }
    }

    public OnlineMenuViewModel(Action<string>? onNavigateRequested = null)
    {
        _onNavigateRequested = onNavigateRequested;
    }

    [RelayCommand]
    private void NavigateToMain()
    {
        _onNavigateRequested?.Invoke("Main");
    }

    [RelayCommand]
    private async Task CreateRoom()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Creating room...";

            var content = new StringContent("{\"hostVersion\": {\n      \"netplayProtocol\": 1,\n      \"md5\": \"dafdf\",\n      \"emulatorBuild\": \"mupen-netplay-0.1.0\"\n    }\n  }", Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{BaseUrl}/rooms", content);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<RoomResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result != null)
            {
                Globals.OnlineHostToken = result.Token?.HostToken;
                Globals.OnlineRoomCode = result.RoomCode;
                Globals.OnlineClientToken = null;

                StatusMessage = $"Room Created! Code: {result.RoomCode}";
                CreatedRoomCode = result.RoomCode ?? string.Empty;
                OnPropertyChanged(nameof(IsRoomReady));
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error creating room: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task JoinRoom()
    {
        if (string.IsNullOrWhiteSpace(JoinCode))
        {
            StatusMessage = "Please enter a join code.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Joining room...";

            var content = new StringContent("{\"clientVersion\": {\n      \"netplayProtocol\": 1,\n      \"md5\": \"dafdf\",\n      \"emulatorBuild\": \"mupen-netplay-0.1.0\"\n    }\n  }", Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{BaseUrl}/rooms/{JoinCode}/join", content);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<RoomResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result != null)
            {
                Globals.OnlineClientToken = result.Token?.ClientToken;
                Globals.OnlineRoomCode = result.RoomCode;
                Globals.OnlineHostToken = null;

                StatusMessage = $"Joined Room: {result.RoomCode}";
                OnPropertyChanged(nameof(IsRoomReady));
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error joining room: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}


// Data structures for JSON
public class RoomResponse
{
    public string? RoomCode { get; set; }
    public string? RoomId { get; set; }
    public RelayInfo? Relay { get; set; }
    public TokenInfo? Token { get; set; }
}

public class RelayInfo
{
    public string? RelayId { get; set; }
    public string? PublicHost { get; set; }
    public int ControlPort { get; set; }
    public int DataPort { get; set; }
    public string? RegionName { get; set; }
}

public class TokenInfo
{
    public string? HostToken { get; set; }
    public string? ClientToken { get; set; }
    public int ExpiresInSeconds { get; set; }
}
