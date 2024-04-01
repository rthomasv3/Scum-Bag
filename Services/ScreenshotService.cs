using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Newtonsoft.Json;
using Scum_Bag.DataAccess.Data;
using Scum_Bag.DataAccess.Data.Steam;
using Scum_Bag.Models;

namespace Scum_Bag.Services;

internal sealed class ScreenshotService
{
    #region Native

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width 
        {
            get { return Right - Left; }
        }

        public int Height 
        {
            get { return Bottom - Top; }
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowRect(IntPtr hwnd, ref RECT rectangle);

    #endregion

    #region Fields

    private static readonly int _headerHeight = 32;
    private static readonly int _shadowSize = 7;

    private readonly Config _config;
    private readonly GameService _gameService;
    private readonly LoggingService _loggingService;
    private readonly Dictionary<string, WatchLocation> _watchers;

    #endregion

    #region Constructor

    public ScreenshotService(Config config, GameService gameService, LoggingService loggingService)
    {
        _config = config;
        _gameService = gameService;
        _loggingService = loggingService;
        _watchers = new();

        Initialize();
    }

    #endregion

    #region Public Methods

    public void StartWatching(Guid saveGameId, string location, string game)
    {
        try
        {
            if (!String.IsNullOrWhiteSpace(location) && !String.IsNullOrWhiteSpace(game))
            {
                RemoveExistingWatcher(saveGameId);

                IEnumerable<AppState> games = _gameService.GetInstalledApps();
                AppState app = games.FirstOrDefault(x => x.Name == game);

                if (app != null)
                {
                    string directory = Path.GetDirectoryName(location);

                    FileSystemWatcher watcher = new(directory)
                    {
                        NotifyFilter = NotifyFilters.LastWrite,
                        IncludeSubdirectories = true
                    };
                    watcher.Changed += OnChanged;
                    watcher.EnableRaisingEvents = true;

                    lock (_watchers)
                    {
                        _watchers.Add(directory, new WatchLocation()
                        {
                            Watcher = watcher,
                            SaveGameId = saveGameId,
                            Location = location,
                            GameDirectory = app.FullInstallDir
                        });
                    }
                }
            }
        }
        catch (Exception e)
        {
            _loggingService.LogError($"{nameof(ScreenshotService)}>{nameof(StartWatching)} - {e}");
        }
    }

    public void StopWatching(Guid saveGameId)
    {
        RemoveExistingWatcher(saveGameId);
    }

    #endregion

    #region Private Methods

    private void Initialize()
    {
        try
        {
            if (File.Exists(_config.SavesPath))
            {
                IEnumerable<SaveGame> saveGames = JsonConvert.DeserializeObject<IEnumerable<SaveGame>>(File.ReadAllText(_config.SavesPath));

                foreach(SaveGame saveGame in saveGames)
                {
                    if (saveGame.Enabled && !String.IsNullOrWhiteSpace(saveGame.Game))
                    {
                        StartWatching(saveGame.Id, saveGame.SaveLocation, saveGame.Game);
                    }
                }
            }
        }
        catch (Exception e)
        {
            _loggingService.LogError($"{nameof(ScreenshotService)}>{nameof(Initialize)} - {e}");
        }
    }

    private void RemoveExistingWatcher(Guid saveGameId)
    {
        try
        {
            lock (_watchers)
            {
                KeyValuePair<string, WatchLocation> watcher = _watchers.FirstOrDefault(x => x.Value.SaveGameId == saveGameId);

                if (watcher.Value != null)
                {
                    watcher.Value.Watcher.EnableRaisingEvents = false;
                    watcher.Value.Watcher.Changed -= OnChanged;
                    watcher.Value.Watcher.Dispose();
                    _watchers.Remove(watcher.Key);
                }
            }
        }
        catch (Exception e)
        {
            _loggingService.LogError($"{nameof(ScreenshotService)}>{nameof(RemoveExistingWatcher)} - {e}");
        }
    }

    private void OnChanged(object sender, FileSystemEventArgs args)
    {
        try
        {
            if (!String.IsNullOrEmpty(args.FullPath) && !String.IsNullOrWhiteSpace(args.Name))
            {
                string directory = args.FullPath.Replace(args.Name, "").TrimEnd('/', '\\');
                
                if (_watchers.TryGetValue(directory, out WatchLocation watchLocation))
                {
                    string saveDirectory = Path.Combine(_config.DataDirectory, watchLocation.SaveGameId.ToString());
                    TakeScreenshot(saveDirectory, watchLocation.GameDirectory);
                }
            }
        }
        catch (Exception e)
        {
            _loggingService.LogError($"{nameof(ScreenshotService)}>{nameof(OnChanged)} - {e}");
        }
    }

    private void TakeScreenshot(string saveDirectory, string gameDirectory)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (Directory.Exists(gameDirectory))
                {
                    Process gameProcess = GetGameProcess(gameDirectory);

                    if (gameProcess != null)
                    {
                        RECT rectangle = GetWindowRect(gameProcess.MainWindowHandle);

                        if (rectangle.Width > 0 && rectangle.Height > 0)
                        {
                            bool removeWindowBorder = true; // may remove this later...

                            int width = rectangle.Width - (removeWindowBorder ? _shadowSize * 2 : 0);
                            int height = removeWindowBorder ? rectangle.Height - _headerHeight - _shadowSize : rectangle.Height;
                            int left = rectangle.Left + (removeWindowBorder ? _shadowSize : 0);
                            int top = rectangle.Top + (removeWindowBorder ? _headerHeight : 0);

                            Bitmap bitmap = new Bitmap(width, height);

                            using (Graphics g = Graphics.FromImage(bitmap))
                            {

                                g.CopyFromScreen(left, top, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
                            }

                            string savePath = Path.Combine(saveDirectory, _config.LatestScreenshotName);

                            if (File.Exists(savePath))
                            {
                                File.Delete(savePath);
                            }

                            bitmap.Save(savePath, ImageFormat.Jpeg);
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            _loggingService.LogError($"{nameof(ScreenshotService)}>{nameof(TakeScreenshot)} - {e}");
        }
    }

    private static RECT GetWindowRect(IntPtr hwnd)
    {
        RECT rectangle = default;
        int attempts = 0;

        while (attempts++ < 5)
        {
            rectangle = new RECT();
            GetWindowRect(hwnd, ref rectangle);

            if (rectangle.Width > 0 && rectangle.Height > 0)
            {
                break;
            }

            Thread.Sleep(50);
        }

        return rectangle;
    }

    private HashSet<string> GetExecutables(string directory)
    {
        HashSet<string> fileSet = new();

        try
        {
            if (Directory.Exists(directory))
            {
                IEnumerable<string> files = Directory
                    .GetFiles(directory, "*.*", SearchOption.AllDirectories)
                    .Select(x => Path.GetFileName(x))
                    .Where(x =>
                    {
                        string ext = Path.GetExtension(x);
                        return String.IsNullOrWhiteSpace(ext) ||
                            String.Equals(ext, ".exe", StringComparison.OrdinalIgnoreCase);
                    })
                    .Distinct();

                fileSet = new(files);
            }
        }
        catch (Exception e)
        {
            _loggingService.LogError($"{nameof(ScreenshotService)}>{nameof(GetExecutables)} - {e}");
        }

        return fileSet;
    }

    private Process GetGameProcess(string directory)
    {
        Process gameProcess = null;

        HashSet<string> executables = GetExecutables(directory);

        foreach (Process process in Process.GetProcesses())
        {
            try
            {
                string fullPath =  Path.GetFileName(process.MainModule?.FileName);

                if (!String.IsNullOrWhiteSpace(fullPath) && executables.Contains(fullPath))
                {
                    gameProcess = process;
                    break;
                }
            }
            catch { /* Empty because tons of processes always throw errors when checking module */ }
        }

        return gameProcess;
    }

    #endregion
}