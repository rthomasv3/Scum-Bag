using System;
using System.IO;

namespace Scum_Bag;

internal sealed class Config
{
    #region Fields

    private readonly string _dataDir;
    private readonly string _savesPath;

    #endregion

    #region Constructor

    public Config()
    {
        _dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Scum Bag");
        _savesPath = Path.Combine(_dataDir, "saves.json");

        if (!Directory.Exists(_dataDir))
        {
            Directory.CreateDirectory(_dataDir);
        }
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

    #endregion
}