using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Timers;
using Microsoft.Win32;
using Scum_Bag.DataAccess.Data.Steam;
using VdfParser;

namespace Scum_Bag.Services;

internal sealed class GameService : IDisposable
{
    #region Native

    public enum WindowShowStyle : uint
    {
        Hide = 0,
        ShowNormal = 1,
        ShowMinimized = 2,
        ShowMaximized = 3,
        Maximize = 3,
        ShowNormalNoActivate = 4,
        Show = 5,
        Minimize = 6,
        ShowMinNoActivate = 7,
        ShowNoActivate = 8,
        Restore = 9,
        ShowDefault = 10,
        ForceMinimized = 11
    }

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, WindowShowStyle nCmdShow);

    #endregion

    #region Fields

    private static readonly HashSet<string> _blackList = ["Steamworks Common Redistributables"];

    private readonly LoggingService _loggingService;
    private readonly string _steamExePath;
    private readonly string _libraryPath;
    private readonly VdfDeserializer _deserializer;
    private readonly Timer _steamLibraryTimer;
    private List<AppState> _steamApps;

    #endregion

    #region Constructor

    public GameService(LoggingService loggingService)
    {
        _loggingService = loggingService;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _steamExePath = Registry.CurrentUser.OpenSubKey("Software\\Valve\\Steam").GetValue("SteamExe").ToString();
        }
        else
        {
            _steamExePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".steam/steam/steam.sh");
        }

        _libraryPath = Path.Combine(Path.Combine(Path.GetDirectoryName(_steamExePath), "steamapps"), "libraryfolders.vdf");

        _deserializer = new();
        _steamApps = new();

        _steamLibraryTimer = new Timer(TimeSpan.FromMinutes(1));
        _steamLibraryTimer.Elapsed += SteamLibraryTimer_Elapsed;
        _steamLibraryTimer.AutoReset = true;
        _steamLibraryTimer.Enabled = true;

        UpdateSteamLibrary(true);
    }

    #endregion

    #region Public Methods

    public IEnumerable<string> GetInstalledGames()
    {
        return _steamApps.Select(x => x.Name);
    }

    public IEnumerable<AppState> GetInstalledApps()
    {
        return _steamApps.AsReadOnly();
    }

    public void Dispose()
    {
        _steamLibraryTimer.Enabled = false;
        _steamLibraryTimer.Elapsed -= SteamLibraryTimer_Elapsed;
        _steamLibraryTimer.Dispose();
    }

    public bool LaunchGame(string gameName)
    {
        bool launched = false;

        if (!String.IsNullOrWhiteSpace(gameName))
        {
            AppState appState = _steamApps.FirstOrDefault(x => x.Name == gameName);

            if (appState != null)
            {
                try
                {
                    Process gameProcess = null;
                    Process steamProcess = Process.Start(_steamExePath, $"steam://launch/{appState.AppId}");

                    HashSet<string> possibleGameExecutables = new();

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        possibleGameExecutables = new(Directory
                            .GetFiles(appState.FullInstallDir, "*.*", SearchOption.AllDirectories)
                            .Where (x => x.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            .Select(Path.GetFileNameWithoutExtension));
                    }
                    else
                    {
                        // No reliable way to check if file is executable on Linux using C#...
                        possibleGameExecutables = new(Directory
                            .GetFiles(appState.FullInstallDir, "*.*", SearchOption.AllDirectories)
                            .Select(Path.GetFileNameWithoutExtension));
                    }

                    int retryCount = 0;
                    while (gameProcess == null && retryCount++ < 10)
                    {
                        System.Threading.Thread.Sleep(1000);

                        gameProcess = Process.GetProcesses()
                            .Where(x => possibleGameExecutables.Contains(x.ProcessName))
                            .OrderByDescending(x => x.NonpagedSystemMemorySize64)
                            .FirstOrDefault();
                    }
                    
                    if (gameProcess != null)
                    {
                        launched = true;
                        BringWindowToForeground(gameProcess.MainWindowHandle);
                    }
                    else
                    {
                        _loggingService.LogInfo($"{nameof(GameService)}>{nameof(LaunchGame)} - Game Not Found: {String.Join(", ", possibleGameExecutables)}");
                    }
                }
                catch (Exception e)
                {
                    _loggingService.LogError($"{nameof(GameService)}>{nameof(LaunchGame)} - {e}");
                }
            }
        }

        return launched;
    }

    #endregion

    #region Private Methods

    private void SteamLibraryTimer_Elapsed(object sender, ElapsedEventArgs e)
    {
        UpdateSteamLibrary();
    }

    private void UpdateSteamLibrary(bool logInfo = false)
    {
        try
        {
            lock (_steamApps)
            {
                _steamApps.Clear();

                if (logInfo)
                {
                    _loggingService.LogInfo($"{nameof(GameService)}>{nameof(UpdateSteamLibrary)} - Steam Path: {_steamExePath}");
                    _loggingService.LogInfo($"{nameof(GameService)}>{nameof(UpdateSteamLibrary)} - Steam Libraries Path: {_libraryPath}");
                }

                FileStream libraryStream = File.OpenRead(_libraryPath);
                Library library = _deserializer.Deserialize<Library>(libraryStream);

                foreach (LibraryFolder libraryFolder in library.LibraryFolders.Values)
                {
                    string appsPath = Path.Combine(libraryFolder.Path, "steamapps");

                    if (Directory.Exists(appsPath))
                    {
                        if (logInfo)
                        {
                            _loggingService.LogInfo($"{nameof(GameService)}>{nameof(UpdateSteamLibrary)} - Steam Library Found: {appsPath}");
                        }

                        foreach (string acfFile in Directory.EnumerateFiles(appsPath, "*.acf"))
                        {
                            try
                            {
                                if (File.Exists(acfFile))
                                {
                                    string acfText = File.ReadAllText(acfFile);

                                    if (acfText.StartsWith("\"AppState\""))
                                    {
                                        if (logInfo)
                                        {
                                            _loggingService.LogInfo($"{nameof(GameService)}>{nameof(UpdateSteamLibrary)} - Steam App Entry Found: {acfFile}");
                                        }

                                        App app = _deserializer.Deserialize<App>(acfText);
                                        app.AppState.LibraryAppDir = Path.Combine(appsPath, "common");

                                        if (!_blackList.Contains(app.AppState.Name) && 
                                            !app.AppState.Name.StartsWith("Proton ") && 
                                            !app.AppState.Name.StartsWith("Steam Linux Runtime "))
                                        {
                                            app.AppState.Name = ConvertToAscii(app.AppState.Name);
                                            _steamApps.Add(app.AppState);

                                            if (logInfo)
                                            {
                                                _loggingService.LogInfo($"{nameof(GameService)}>{nameof(UpdateSteamLibrary)} - Steam App Found: {app.AppState.Name}");
                                            }
                                        }
                                        else if (logInfo)
                                        {
                                            _loggingService.LogInfo($"{nameof(GameService)}>{nameof(UpdateSteamLibrary)} - Steam App Skipped: {app.AppState.Name}"); 
                                        }
                                    }
                                    else if (logInfo)
                                    {
                                        _loggingService.LogInfo($"{nameof(GameService)}>{nameof(UpdateSteamLibrary)} - Steam App Invalid: {acfFile}"); 
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                _loggingService.LogError($"{nameof(GameService)}>{nameof(UpdateSteamLibrary)} - {e}");
                            }
                        }
                    }
                    else if (logInfo)
                    {
                        _loggingService.LogInfo($"{nameof(GameService)}>{nameof(UpdateSteamLibrary)} - Steam Library Invalid: {appsPath}");
                    }
                }

                if (logInfo)
                {
                    _loggingService.LogInfo($"{nameof(GameService)}>{nameof(UpdateSteamLibrary)} - Done");
                }
            }
        }
        catch (Exception e)
        {
            _loggingService.LogError($"{nameof(GameService)}>{nameof(UpdateSteamLibrary)} - {e}");
        }
    }

    private static string ConvertToAscii(string text)
    {
        string cleanedText = text
            .Replace('’','\'')
            .Replace('–', '-')
            .Replace('“', '"')
            .Replace('”', '"')
            .Replace("…", "...")
            .Replace("—", "--")
            .Replace("™", "")
            .Replace("®", "");
        byte[] textData = Encoding.Convert(Encoding.Default, Encoding.ASCII, Encoding.Default.GetBytes(cleanedText));
        return Encoding.ASCII.GetString(textData);
    }

    private void BringWindowToForeground(nint windowHandle)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (windowHandle != GetForegroundWindow())
            {
                if (IsIconic(windowHandle))
                {
                    // Minimized so send restore
                    ShowWindow(windowHandle, WindowShowStyle.Restore);
                }
                else
                {
                    // Already Maximized or Restored so just bring to front
                    SetForegroundWindow(windowHandle);
                }
            }
        }
    }

    #endregion
}