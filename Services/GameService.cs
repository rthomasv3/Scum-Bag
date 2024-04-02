using System;
using System.Collections.Generic;
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
            _steamExePath = "~/.steam/steam";
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
                    if (logInfo)
                    {
                        _loggingService.LogInfo($"{nameof(GameService)}>{nameof(UpdateSteamLibrary)} - Steam Library Found: {libraryFolder.Path}");
                    }

                    string appsPath = Path.Combine(libraryFolder.Path, "steamapps");

                    foreach (string file in Directory.EnumerateFiles(appsPath, "*.acf"))
                    {
                        if (logInfo)
                        {
                            _loggingService.LogInfo($"{nameof(GameService)}>{nameof(UpdateSteamLibrary)} - Steam App Entry Found: {file}");
                        }

                        try
                        {
                            FileStream fileStream = File.OpenRead(file);
                            App app = _deserializer.Deserialize<App>(fileStream);
                            app.AppState.LibraryAppDir = Path.Combine(appsPath, "common");

                            if (!_blackList.Contains(app.AppState.Name))
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
                        catch (Exception e)
                        {
                            _loggingService.LogError($"{nameof(GameService)}>{nameof(UpdateSteamLibrary)} - {e}");
                        }
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
        string cleanedText = text.Replace('’','\'').Replace('–', '-').Replace('“', '"').Replace('”', '"').Replace("…", "...").Replace("—", "--").Replace("™", "");
        byte[] textData = Encoding.Convert(Encoding.Default, Encoding.ASCII, Encoding.Default.GetBytes(cleanedText));
        return Encoding.ASCII.GetString(textData);
    }

    #endregion
}