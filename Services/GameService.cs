using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using Scum_Bag.DataAccess.Data.Steam;
using VdfParser;

namespace Scum_Bag.Services;

internal sealed class GameService
{
    #region Fields

    private static readonly HashSet<string> _blackList = ["Steamworks Common Redistributables"];

    private readonly string _steamExePath;
    private readonly string _libraryPath;
    private readonly VdfDeserializer _deserializer;

    #endregion

    #region Constructor

    public GameService()
    {
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
    }

    #endregion

    #region Public Methods

    public IEnumerable<string> GetInstalledGames()
    {
        List<string> games = new();

        FileStream libraryStream = File.OpenRead(_libraryPath);
        Library library = _deserializer.Deserialize<Library>(libraryStream);

        foreach (LibraryFolder libraryFolder in library.LibraryFolders.Values)
        {
            string appsPath = Path.Combine(libraryFolder.Path, "steamapps");

            foreach (string file in Directory.EnumerateFiles(appsPath, "*.acf"))
            {
                try
                {
                    FileStream fileStream = File.OpenRead(file);
                    App app = _deserializer.Deserialize<App>(fileStream);

                    if (!_blackList.Contains(app.AppState.Name))
                    {
                        games.Add(ConvertToAscii(app.AppState.Name));
                    }
                }
                catch { }
            }
        }
        
        return games.AsReadOnly();
    }

    public IEnumerable<AppState> GetInstalledApps()
    {
        List<AppState> games = new();

        FileStream libraryStream = File.OpenRead(_libraryPath);
        Library library = _deserializer.Deserialize<Library>(libraryStream);

        foreach (LibraryFolder libraryFolder in library.LibraryFolders.Values)
        {
            string appsPath = Path.Combine(libraryFolder.Path, "steamapps");

            foreach (string file in Directory.EnumerateFiles(appsPath, "*.acf"))
            {
                try
                {
                    FileStream fileStream = File.OpenRead(file);
                    App app = _deserializer.Deserialize<App>(fileStream);
                    app.AppState.LibraryAppDir = Path.Combine(appsPath, "common");
                    app.AppState.Name = ConvertToAscii(app.AppState.Name);

                    if (!_blackList.Contains(app.AppState.Name))
                    {
                        games.Add(app.AppState);
                    }
                }
                catch { }
            }
        }
        
        return games.AsReadOnly();
    }

    #endregion

    #region Private Methods

    public string ConvertToAscii(string text)
    {
        string cleanedText = text.Replace('’','\'').Replace('–', '-').Replace('“', '"').Replace('”', '"').Replace("…", "...").Replace("—", "--").Replace("™", "");
        byte[] textData = Encoding.Convert(Encoding.Default, Encoding.ASCII, Encoding.Default.GetBytes(cleanedText));
        return Encoding.ASCII.GetString(textData);
    }

    #endregion
}