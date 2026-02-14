using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace JimmiLauncher.ViewModels
{
    public class ReplayItem
    {
        public string Name { get; set; }
        public DateTime CreationDate { get; set; }
        public Bitmap? Thumbnail { get; set; }
        public string GameType { get; set; }

        public ReplayItem(string name, DateTime creationDate, Bitmap? thumbnail, string gametype)
        {
            Name = name;
            CreationDate = creationDate;
            Thumbnail = thumbnail;
            GameType = gametype;
        }
    }

    /// <summary>
    /// An abstract class for enabling page navigation.
    /// </summary>
    public partial class ReplayMenuViewModel : MenuViewModelBase
    {
        private const int ItemsPerPage = 10;

        [ObservableProperty]
        private ObservableCollection<ReplayItem> replays = new();

        [ObservableProperty]
        private int currentPage = 1;

        [ObservableProperty]
        private int totalPages = 1;

        [ObservableProperty]
        private ObservableCollection<ReplayItem> currentPageReplays = new();

        public RelayCommand NextPageCommand { get; }
        public RelayCommand PreviousPageCommand { get; }
        public RelayCommand NavigateToMainCommand { get; }
        public RelayCommand<ReplayItem> WatchReplayCommand { get; }

        private Action<string>? _onNavigateRequested;

        public override bool CanNavigateReplays {
            get => false;
            protected set => throw new Exception("Cannot set CanNavigateReplays in ReplayMenuViewModel");
        }

        public override bool CanNavigateMain { 
            get => true;
            protected set => throw new Exception("Cannot set CanNavigateMain in ReplayMenuViewModel");
        }
        public override bool CanNavigateOnline {
            get => false;
            protected set => throw new Exception("Cannot set CanNavigateOnline in ReplayMenuViewModel");
        }

        public override bool CanNavigateOffline {
            get => false;
            protected set => throw new Exception("Cannot set CanNavigateOffline in ReplayMenuViewModel");
        }

        public ReplayMenuViewModel(Action<string>? onNavigateRequested = null)
        {
            try
            {
                _onNavigateRequested = onNavigateRequested;
                NextPageCommand = new RelayCommand(NextPage, () => CurrentPage < TotalPages);
                PreviousPageCommand = new RelayCommand(PreviousPage, () => CurrentPage > 1);
                NavigateToMainCommand = new RelayCommand(() => _onNavigateRequested?.Invoke("Main"));
                WatchReplayCommand = new RelayCommand<ReplayItem>(WatchReplay);
                LoadReplays();
                UpdatePageReplays();
                Console.WriteLine("ReplayMenuViewModel initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ReplayMenuViewModel constructor: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        private void LoadReplays()
        {
            try
            {
                var replaysFolderPath = Globals.ReplaysFolderPath;
                if (!Directory.Exists(replaysFolderPath))
                {
                    Console.WriteLine($"Replays folder not found: {replaysFolderPath}");
                    return;
                }

                var files = Directory.EnumerateFiles(replaysFolderPath)
                    .Where(f => f.EndsWith(".jrpl"))
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();

                var replayItems = new System.Collections.Generic.List<ReplayItem>();

                foreach (var file in files)
                {
                    using (ZipArchive zip = ZipFile.Open($"{file.FullName}", ZipArchiveMode.Read))
                    {
                        var gametype = zip.GetEntry("remix") != null ? "Remix" : "Vanilla";
                        var name = file.Name;
                        var date = file.CreationTime;
                        Bitmap? thumbnail = null;
                        var thumbnailPath = Path.Combine("temp_thumbnails", name.Replace(".jrpl", ".jpg"));
                        if (File.Exists(thumbnailPath))
                        {
                            try
                            {
                                thumbnail = new Bitmap(thumbnailPath);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error loading thumbnail for {name}: {ex.Message}");
                            }
                        }
                        replayItems.Add(new ReplayItem(name, date, thumbnail, gametype));
                    }
                }

                Console.WriteLine($"Found {replayItems.Count} replay files");
                Replays = new ObservableCollection<ReplayItem>(replayItems);
                TotalPages = Math.Max(1, (Replays.Count + ItemsPerPage - 1) / ItemsPerPage);
                CurrentPage = 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading replays: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        private void UpdatePageReplays()
        {
            var startIndex = (CurrentPage - 1) * ItemsPerPage;
            var pageItems = Replays.Skip(startIndex).Take(ItemsPerPage).ToList();
            CurrentPageReplays = new ObservableCollection<ReplayItem>(pageItems);
        }

        private void NextPage()
        {
            if (CurrentPage < TotalPages)
            {
                CurrentPage++;
                UpdatePageReplays();
            }
        }

        private void PreviousPage()
        {
            if (CurrentPage > 1)
            {
                CurrentPage--;
                UpdatePageReplays();
            }
        }

        private void WatchReplay(ReplayItem? item)
        {
            if (item == null) return;

            try
            {
                var replayName = item.Name;
                if (!replayName.EndsWith(".jrpl"))
                {
                    replayName += ".jrpl";
                }

                var extractedPath = ReplayMethods.ExtractReplayData(replayName);
                
                string gamePath;
                if (File.Exists(Path.Combine(extractedPath, "remix")))
                {
                    gamePath = Globals.RemixRomPath;
                }
                else
                {
                    gamePath = Globals.VanillaRomPath;
                }

                if (!File.Exists(gamePath))
                {
                    Debug.WriteLine($"ROM not found at {gamePath}");
                    return;
                }

                WatchReplaysProcess(gamePath, extractedPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error watching replay: {ex.Message}");
            }
        }

        private void WatchReplaysProcess(string gamePath, string replayPath)
        {
            try
            {
                // var folder = "../mupen64plus-ui-console/projects/msvc/x64/Release";
                var folder = "./mupen";
                var arguments = $"--playback {replayPath} --configdir {folder} --datadir {folder} --plugindir {folder} {gamePath}";
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = Globals.MupenExecutablePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                };

                Debug.WriteLine($"Starting process: {processStartInfo.FileName} {processStartInfo.Arguments}");

                Process? game = Process.Start(processStartInfo);
                if (game != null)
                {
                    game.EnableRaisingEvents = true;
                    game.Exited += ReplayMethods.DeleteExtractedReplayData;
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Could not start process for {gamePath} with replay {replayPath}: {e.Message}");
            }
        }
    }
}