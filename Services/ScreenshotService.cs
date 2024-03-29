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
    private readonly Dictionary<string, WatchLocation> _watchers;

    #endregion

    #region Constructor

    public ScreenshotService(Config config, GameService gameService)
    {
        _config = config;
        _gameService = gameService;
        _watchers = new();

        Initialize();
    }

    #endregion

    #region Public Methods

    public void StartWatching(Guid saveGameId, string location, string game)
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

    public void StopWatching(Guid saveGameId)
    {
        RemoveExistingWatcher(saveGameId);
    }

    #endregion

    #region Private Methods

    private void Initialize()
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

    private void RemoveExistingWatcher(Guid saveGameId)
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

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (!String.IsNullOrEmpty(e.FullPath) && !String.IsNullOrWhiteSpace(e.Name))
        {
            string directory = e.FullPath.Replace(e.Name, "").TrimEnd('/', '\\');
            
            if (_watchers.ContainsKey(directory))
            {
                WatchLocation watchLocation = _watchers[directory];
                string saveDirectory = Path.Combine(_config.DataDirectory, watchLocation.SaveGameId.ToString());
                TakeScreenshot(saveDirectory, watchLocation.GameDirectory);
            }
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
                    Process gameProcess = null;

                    IEnumerable<string> files = Directory
                        .GetFiles(gameDirectory, "*.*", SearchOption.AllDirectories)
                        .Select(x => Path.GetFileName(x))
                        .Distinct();
                    HashSet<string> fileSet = new(files);

                    Process[] processes = Process.GetProcesses();

                    foreach (Process process in processes)
                    {
                        string fullPath = null;

                        try
                        {
                            fullPath =  Path.GetFileName(process.MainModule?.FileName);
                        }
                        catch{ }

                        if (!String.IsNullOrWhiteSpace(fullPath) && fileSet.Contains(fullPath))
                        {
                            gameProcess = process;
                            break;
                        }
                    }

                    if (gameProcess != null)
                    {
                        Debug.WriteLine("Taking screenshot...");

                        RECT rectangle = GetWindowRect(gameProcess.MainWindowHandle);

                        if (rectangle.Width > 0 && rectangle.Height > 0)
                        {
                            bool removeWindowBorder = true; // may remove this later...

                            int width = rectangle.Width - (removeWindowBorder ? _shadowSize * 2 : 0);
                            int height = removeWindowBorder ? rectangle.Height - _headerHeight - _shadowSize : rectangle.Height;
                            int left = rectangle.Left + (removeWindowBorder ? _shadowSize : 0);
                            int top = rectangle.Top + (removeWindowBorder ? _headerHeight : 0);

                            Bitmap bitmap = new Bitmap(width, height);

                            Debug.WriteLine($"Left: {rectangle.Left} ({left}), Top: {rectangle.Top} ({top}), Right: {rectangle.Right}, Width: {width}, Height: {height}");

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
            Debug.WriteLine(e.ToString());
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

            Thread.Sleep(100);
        }

        return rectangle;
    }

    #endregion
}