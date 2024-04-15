using System;
using System.IO;
using Newtonsoft.Json;
using Scum_Bag.DataAccess.Data;

namespace Scum_Bag;

internal sealed class Config
{
    #region Fields

    private readonly string _dataDir;
    private readonly string _savesPath;
    private readonly string _latestScreenshotName;
    private readonly string _backupScreenshotName;
    private readonly string _settingsPath;
    private string _backupsDirectory;

    #endregion

    #region Constructor

    public Config()
    {
        _dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Scum Bag");
        _backupsDirectory = _dataDir;
        _savesPath = Path.Combine(_dataDir, "saves.json");
        _backupScreenshotName = "Scum_Bag_Screenshot.jpg";
        _latestScreenshotName = "latest_screenshot.jpg";
        _settingsPath = Path.Combine(_dataDir, "settings.json");

        if (!Directory.Exists(_dataDir))
        {
            Directory.CreateDirectory(_dataDir);
        }

        Initialize();
    }

    #endregion

    #region Properties
    
    public string DataDirectory
    {
        get { return _dataDir; }
    }

    public string SavesPath
    {
        get { return _savesPath; }
    }

    public string LatestScreenshotName
    {
        get { return _latestScreenshotName; }
    }

    public string BackupScreenshotName
    {
        get { return _backupScreenshotName; }
    }

    public string BackupsDirectory
    {
        get { return _backupsDirectory; }
    }

    public string SettingsPath
    {
        get { return _settingsPath; }
    }

    #endregion

    #region Public Methods

    public Settings GetSettings()
    {
        Settings settings = new()
        {
            Theme = "Indigo",
            IsDark = true,
            BackupsDirectory = _backupsDirectory
        };

        if (File.Exists(_settingsPath))
        {
            settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(_settingsPath));
        }

        return settings;
    }

    public void SaveSettings(Settings settings)
    {
        _backupsDirectory = settings.BackupsDirectory;
        string settingsFileContent = JsonConvert.SerializeObject(settings, Formatting.Indented);
        File.WriteAllText(_settingsPath, settingsFileContent);
    }

    #endregion

    #region Private Methods

    private void Initialize()
    {
        if (File.Exists(_settingsPath))
        {
            Settings settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(_settingsPath));
            _backupsDirectory = settings.BackupsDirectory;
        }
    }

    #endregion
}