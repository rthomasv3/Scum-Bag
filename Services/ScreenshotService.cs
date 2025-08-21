using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
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
    private bool _flameshotSetup;
    private readonly bool _isInFlatpak;
    private readonly string _flameshotCommand;
    private readonly string _flameshotArgs;

    #endregion

    #region Constructor

    public ScreenshotService(Config config, GameService gameService, LoggingService loggingService)
    {
        _config = config;
        _gameService = gameService;
        _loggingService = loggingService;
        _watchers = new();

        _isInFlatpak = File.Exists("/.flatpak-info");

        if (_isInFlatpak)
        {
            _flameshotCommand = "flatpak-spawn";
            _flameshotArgs = "--host flameshot";
        }
        else
        {
            _flameshotCommand = "flameshot";
            _flameshotArgs = "";
        }

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
                IEnumerable<SaveGame> saveGames = JsonSerializer
                    .Deserialize<IEnumerable<SaveGame>>(File.ReadAllText(_config.SavesPath), SaveDataJsonSerializerContext.Default.Options);

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
                    if (!watchLocation.IsTakingScreenshot)
                    {
                        watchLocation.IsTakingScreenshot = true;

                        try
                        {
                            string saveDirectory = Path.Combine(_config.BackupsDirectory, watchLocation.SaveGameId.ToString());
                            TakeScreenshot(saveDirectory, watchLocation.GameDirectory);
                        }
                        finally
                        {
                            watchLocation.IsTakingScreenshot = false;
                        }
                    }
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
                TakeWindowsScreenshot(saveDirectory, gameDirectory);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                TakeLinuxScreenshot(saveDirectory);
            }
            else
            {
                _loggingService.LogInfo($"{nameof(ScreenshotService)}>{nameof(TakeScreenshot)} - Skipping screenshot for platform {RuntimeInformation.OSDescription}");
            }
        }
        catch (Exception e)
        {
            _loggingService.LogError($"{nameof(ScreenshotService)}>{nameof(TakeScreenshot)} - {e}");
        }
    }

    private void TakeWindowsScreenshot(string saveDirectory, string gameDirectory)
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

                    // need to update these trim sizes to a percentage instead of fixed values
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
            else
            {
                _loggingService.LogInfo($"{nameof(ScreenshotService)}>{nameof(TakeScreenshot)} - Failed to find game process for directory {gameDirectory}");
            }
        }
    }

    private void TakeLinuxScreenshot(string saveDirectory)
    {
        if (IsFlameshotSetup())
        {
            string savePath = Path.Combine(saveDirectory, _config.LatestScreenshotName.Normalize());

            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }

            string arguments = _isInFlatpak
                ? $"{_flameshotArgs} screen -r -p \"{savePath}\""
                : $"screen -r -p \"{savePath}\"";

            ProcessStartInfo flameshotStartInfo = new()
            {
                FileName = _flameshotCommand,
                Arguments = arguments,
            };

            Process process = Process.Start(flameshotStartInfo);
            process.WaitForExit(5000);
            process.Close();

            if (!File.Exists(savePath))
            {
                _loggingService.LogInfo($"{nameof(ScreenshotService)}>{nameof(TakeLinuxScreenshot)} - Screenshot may have failed");
                _loggingService.LogInfo($"{nameof(ScreenshotService)}>{nameof(TakeLinuxScreenshot)} - {_flameshotCommand} {arguments}");
            }
        }
        else
        {
            _loggingService.LogInfo($"{nameof(ScreenshotService)}>{nameof(TakeLinuxScreenshot)} - flameshot not available");
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
                    .Where (x => x.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || String.IsNullOrWhiteSpace(Path.GetExtension(x)))
                    .Select(Path.GetFileNameWithoutExtension)
                    .Distinct();

                fileSet = new(files, StringComparer.OrdinalIgnoreCase);
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
            if (executables.Contains(process.ProcessName))
            {
                gameProcess = process;
                break;
            }
        }

        return gameProcess;
    }

    private bool IsFlameshotSetup()
    {
        if (!_flameshotSetup)
        {
            try
            {
                ProcessStartInfo flameshotStartInfo;

                if (_isInFlatpak)
                {
                    flameshotStartInfo = new()
                    {
                        FileName = "flatpak-spawn",
                        Arguments = "--host which flameshot",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false
                    };
                }
                else
                {
                    flameshotStartInfo = new()
                    {
                        FileName = "which",
                        Arguments = "flameshot",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false
                    };
                }

                Process process = Process.Start(flameshotStartInfo);
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    SetupFlameshotConfig();

                    _flameshotSetup = true;
                    _loggingService.LogInfo($"{nameof(ScreenshotService)} - Flameshot detected and configured (Flatpak: {_isInFlatpak})");
                }
                else
                {
                    _loggingService.LogInfo($"{nameof(ScreenshotService)} - Flameshot not found (Flatpak: {_isInFlatpak})");
                }
            }
            catch (Exception e)
            {
                _loggingService.LogError($"{nameof(ScreenshotService)}>{nameof(IsFlameshotSetup)} - {e}");
            }
        }

        return _flameshotSetup;
    }

    private void SetupFlameshotConfig()
    {
        try
        {
            string flameshotConfigPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "flameshot/flameshot.ini"
            );

            bool isConfigDefault = false;
            if (File.Exists(flameshotConfigPath))
            {
                string currentConfig = File.ReadAllText(flameshotConfigPath).Trim();
                isConfigDefault = String.IsNullOrWhiteSpace(currentConfig) ||
                                  currentConfig == "[General]" ||
                                  currentConfig == "[General]\ncontrastOpacity=188";
            }
            else
            {
                isConfigDefault = true;
                string configDir = Path.GetDirectoryName(flameshotConfigPath);
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }
            }

            if (isConfigDefault)
            {
                string flameshotConfig = "[General]\ncontrastOpacity=188\nshowDesktopNotification=false\n";
                File.WriteAllText(flameshotConfigPath, flameshotConfig);
            }
        }
        catch (Exception e)
        {
            _loggingService.LogError($"{nameof(ScreenshotService)}>{nameof(SetupFlameshotConfig)} - {e}");
        }
    }

    #endregion
}