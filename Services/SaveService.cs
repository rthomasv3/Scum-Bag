using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Scum_Bag.DataAccess.Data;
using Scum_Bag.Models;

namespace Scum_Bag.Services;

internal sealed class SaveService
{
    #region Fields

    private static readonly HashSet<string> _blackList = ["Steamworks Common Redistributables"];

    private readonly Config _config;
    private readonly BackupService _backupService;
    private readonly ScreenshotService _screenshotService;

    #endregion

    #region Constructor

    public SaveService(Config config, BackupService backupService, ScreenshotService screenshotService)
    {
        _config = config;
        _backupService = backupService;
        _screenshotService = screenshotService;
    }

    #endregion

    #region Public Methods

    public IEnumerable<TreeNode> GetSaves()
    {
        List<TreeNode> saves = new();

        if (File.Exists(_config.SavesPath))
        {
            IEnumerable<SaveGame> saveGames = JsonConvert.DeserializeObject<IEnumerable<SaveGame>>(File.ReadAllText(_config.SavesPath));
            IEnumerable<IGrouping<string, SaveGame>> saveGroups = saveGames.GroupBy(x => String.IsNullOrEmpty(x.Game) ? "Game" : x.Game);

            foreach(IGrouping<string, SaveGame> group in saveGroups)
            {
                List<TreeNode> children = new();

                foreach(SaveGame saveGame in group)
                {
                    children.Add(new TreeNode()
                    {
                        key = saveGame.Id.ToString(),
                        label = saveGame.Name,
                    });
                }

                saves.Add(new TreeNode()
                {
                    key = group.Key,
                    label = group.Key,
                    type = "game",
                    children = children
                });
            }
        }

        return saves.AsReadOnly();
    }

    public SaveGame GetSave(Guid id)
    {
        SaveGame saveGame = null;

        IEnumerable<SaveGame> saveGames = JsonConvert.DeserializeObject<IEnumerable<SaveGame>>(File.ReadAllText(_config.SavesPath));

        foreach (SaveGame save in saveGames)
        {
            if (save.Id == id)
            {
                saveGame = save;
                break;
            }
        }

        return saveGame;
    }

    public IEnumerable<Backup> GetBackups(Guid id)
    {
        List<Backup> backups = new();

        string path = Path.Combine(_config.DataDirectory, id.ToString());

        DirectoryInfo parent = new(path);

        if (parent.Exists)
        {
            DirectoryInfo[] dirs = parent.GetDirectories();

            foreach (DirectoryInfo dir in dirs)
            {
                string screenshot = null;
                string screenshotPath = Path.Combine(dir.FullName, "Scum_Bag_Screenshot.jpg");

                if (File.Exists(screenshotPath))
                {
                    byte[] screenshotData = File.ReadAllBytes(screenshotPath);
                    screenshot = Convert.ToBase64String(screenshotData);
                }

                backups.Add(new Backup()
                {
                    Time = ((DateTimeOffset)dir.CreationTime).ToUnixTimeMilliseconds(),
                    Screenshot = screenshot
                });
            }
        }

        return backups.AsReadOnly();
    }

    public Guid CreateSave(SaveGame saveGame)
    {
        if (!File.Exists(_config.SavesPath))
        {
            File.WriteAllText(_config.SavesPath, "[]");
        }
        
        saveGame.Id = Guid.NewGuid();

        List<SaveGame> saveGames = JsonConvert.DeserializeObject<List<SaveGame>>(File.ReadAllText(_config.SavesPath));

        if (saveGame.Enabled)
        {
            DisableDuplicates(saveGame.Id, saveGame.SaveLocation, ref saveGames);
        }

        saveGames.Add(saveGame);
        string fileContent = JsonConvert.SerializeObject(saveGames);
        File.WriteAllText(_config.SavesPath, fileContent);
        
        _backupService.AddNewBackupTimer(saveGame);

        _screenshotService.StartWatching(saveGame.Id, saveGame.SaveLocation, saveGame.Game);

        return saveGame.Id;
    }

    public bool UpdateSave(SaveGame saveGame)
    {
        bool updated = false;

        List<SaveGame> saveGames = JsonConvert.DeserializeObject<List<SaveGame>>(File.ReadAllText(_config.SavesPath));

        if (saveGame.Enabled)
        {
            DisableDuplicates(saveGame.Id, saveGame.SaveLocation, ref saveGames);
        }

        for (int i = 0; i < saveGames.Count; ++i)
        {
            if (saveGames[i].Id == saveGame.Id)
            {
                saveGames[i] = saveGame;
                updated = true;
                break;
            }
        }

        string fileContent = JsonConvert.SerializeObject(saveGames);
        File.WriteAllText(_config.SavesPath, fileContent);

        _backupService.UpdateSave(saveGame);

        _screenshotService.StartWatching(saveGame.Id, saveGame.SaveLocation, saveGame.Game);

        return updated;
    }

    public bool DeleteSave(Guid id)
    {
        bool deleted = false;

        List<SaveGame> saveGames = JsonConvert.DeserializeObject<List<SaveGame>>(File.ReadAllText(_config.SavesPath));

        for (int i = 0; i < saveGames.Count; ++i)
        {
            if (saveGames[i].Id == id)
            {
                saveGames.RemoveAt(i);
                deleted = true;
                break;
            }
        }

        string fileContent = JsonConvert.SerializeObject(saveGames);
        File.WriteAllText(_config.SavesPath, fileContent);

        _backupService.StopTimer(id);

        string path = Path.Combine(_config.DataDirectory, id.ToString());
        DirectoryInfo parent = new(path);

        if (parent.Exists)
        {
            parent.Delete(true);
        }

        _screenshotService.StopWatching(id);

        return deleted;
    }

    public bool RestoreSave(Guid id, long time)
    {
        bool restored = false;

        SaveGame saveGame = GetSave(id);

        string path = Path.Combine(_config.DataDirectory, id.ToString());

        DirectoryInfo parent = new(path);

        if (parent.Exists)
        {
            DirectoryInfo[] dirs = parent.GetDirectories();

            foreach (DirectoryInfo dir in dirs)
            {
                if (((DateTimeOffset)dir.CreationTime).ToUnixTimeMilliseconds() == time)
                {
                    restored = RestoreFile(dir.FullName, saveGame.SaveLocation);
                    break;
                }
            }
        }

        return restored;
    }

    #endregion

    #region Private Methods

    private bool RestoreFile(string source, string destination)
    {
        bool restored = false;

        if (Directory.Exists(source))
        {
            string[] files = Directory.GetFiles(source);

            if (files.Length == 1)
            {
                File.Copy(files[0], destination, true);
                restored = true;
            }
            else
            {
                OverwriteDirectory(source, destination);
                restored = true;
            }
        }

        return restored;
    }

    private void OverwriteDirectory(string sourceDir, string destinationDir, bool recursive = true)
    {
        // Get information about the source directory
        DirectoryInfo dir = new(sourceDir);

        // Check if the source directory exists
        if (dir.Exists)
        {
            // Cache directories before we start copying
            DirectoryInfo[] dirs = dir.GetDirectories();

            // Create the destination directory
            Directory.CreateDirectory(destinationDir);

            // Get the files in the source directory and copy to the destination directory
            foreach (FileInfo file in dir.GetFiles())
            {
                if (file.Name != "Scum_Bag_Screenshot.jpg")
                {
                    string targetFilePath = Path.Combine(destinationDir, file.Name);
                    file.CopyTo(targetFilePath, true);
                }
            }

            // If recursive and copying subdirectories, recursively call this method
            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    OverwriteDirectory(subDir.FullName, newDestinationDir, true);
                }
            }
        }
    }

    private void DisableDuplicates(Guid? currentId, string location, ref List<SaveGame> saveGames)
    {
        foreach (SaveGame saveGame in saveGames)
        {
            if (saveGame.Id != currentId.GetValueOrDefault() && saveGame.SaveLocation == location)
            {
                saveGame.Enabled = false;
            }
        }
    }
    
    #endregion
}