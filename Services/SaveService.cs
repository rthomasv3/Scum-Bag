using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Scum_Bag.DataAccess.Data;
using Scum_Bag.Models;

namespace Scum_Bag.Services;

internal sealed class SaveService
{
    #region Fields

    private readonly Config _config;
    private readonly BackupService _backupService;
    private readonly ScreenshotService _screenshotService;
    private readonly LoggingService _loggingService;

    #endregion

    #region Constructor

    public SaveService(Config config, BackupService backupService, ScreenshotService screenshotService,
        LoggingService loggingService)
    {
        _config = config;
        _backupService = backupService;
        _screenshotService = screenshotService;
        _loggingService = loggingService;
    }

    #endregion

    #region Public Methods

    public IEnumerable<TreeNode> GetSaves()
    {
        List<TreeNode> saves = new();

        try
        {
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
        }
        catch (Exception e)
        {
            _loggingService.LogError($"{nameof(SaveService)}>{nameof(GetSaves)} - {e}");
        }

        return saves.AsReadOnly();
    }

    public SaveGame GetSave(Guid id)
    {
        SaveGame saveGame = null;

        try
        {
            IEnumerable<SaveGame> saveGames = JsonConvert.DeserializeObject<IEnumerable<SaveGame>>(File.ReadAllText(_config.SavesPath));

            foreach (SaveGame save in saveGames)
            {
                if (save.Id == id)
                {
                    saveGame = save;

                    if (String.IsNullOrWhiteSpace(save.BackupLocation))
                    {
                        save.BackupLocation = Path.Combine(_config.DataDirectory, save.Id.ToString());
                    }

                    break;
                }
            }
        }
        catch (Exception e)
        {
            _loggingService.LogError($"{nameof(SaveService)}>{nameof(GetSave)} - {e}");
        }

        return saveGame;
    }

    public IEnumerable<Backup> GetBackups(Guid id)
    {
        List<Backup> backups = new();

        try
        {
            SaveGame saveGame = GetSave(id);

            if (saveGame != null)
            {
                string path = Path.Combine(_config.DataDirectory, id.ToString());

                DirectoryInfo parent = new(path);

                if (parent.Exists)
                {
                    DirectoryInfo[] dirs = parent.GetDirectories();

                    foreach (DirectoryInfo dir in dirs)
                    {
                        string tag = null;
                        bool isFavorite = false;

                        if (saveGame.BackupMetadata.TryGetValue(dir.FullName, out BackupMetadata metadata))
                        {
                            tag = metadata.Tag;
                            isFavorite = metadata.IsFavorite;
                        }

                        backups.Add(new Backup()
                        {
                            SaveId = saveGame.Id,
                            Time = ((DateTimeOffset)dir.CreationTime).ToUnixTimeMilliseconds(),
                            Directory = dir.FullName,
                            Tag = tag,
                            IsFavorite = isFavorite
                        });
                    }
                }
            }
        }
        catch (Exception e)
        {
            _loggingService.LogError($"{nameof(SaveService)}>{nameof(GetBackups)} - {e}");
        }

        return backups.AsReadOnly();
    }

    public string GetScreenshot(string directory)
    {
        string screenshot = String.Empty;

        try
        {
            if (!String.IsNullOrEmpty(directory))
            {
                string screenshotPath = Path.Combine(directory, _config.BackupScreenshotName);

                if (File.Exists(screenshotPath))
                {
                    screenshot = Convert.ToBase64String(File.ReadAllBytes(screenshotPath));
                }
            }
        }
        catch (Exception e)
        {
            _loggingService.LogError($"{nameof(SaveService)}>{nameof(GetScreenshot)} - {e}");
        }

        return screenshot;
    }

    public Guid CreateSave(SaveGame saveGame)
    {
        try
        {
            if (!File.Exists(_config.SavesPath))
            {
                File.WriteAllText(_config.SavesPath, "[]");
            }
            
            saveGame.Id = Guid.NewGuid();
            saveGame.BackupLocation = Path.Combine(_config.DataDirectory, saveGame.Id.ToString());

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
        }
        catch (Exception e)
        {
            _loggingService.LogError($"{nameof(SaveService)}>{nameof(CreateSave)} - {e}");
        }

        return saveGame.Id;
    }

    public bool UpdateSave(SaveGame saveGame)
    {
        bool updated = false;

        try
        {
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
        }
        catch (Exception e)
        {
            _loggingService.LogError($"{nameof(SaveService)}>{nameof(UpdateSave)} - {e}");
        }

        return updated;
    }

    public bool DeleteSave(Guid id)
    {
        bool deleted = false;

        try
        {
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
        }
        catch (Exception e)
        {
            _loggingService.LogError($"{nameof(SaveService)}>{nameof(DeleteSave)} - {e}");
        }

        return deleted;
    }

    public bool CreateManualBackup(Guid id)
    {
        return _backupService.CreateManualBackup(id);
    }

    public bool RestoreSave(Guid id, long time)
    {
        bool restored = false;

        try
        {
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
        }
        catch (Exception e)
        {
            _loggingService.LogError($"{nameof(SaveService)}>{nameof(RestoreSave)} - {e}");
        }

        return restored;
    }

    public bool UpdateMetadata(Guid saveGameId, string directory, string tag, bool isFavorite)
    {
        bool updated = false;

        try
        {
            if (Directory.Exists(directory))
            {
                List<SaveGame> saveGames = JsonConvert.DeserializeObject<List<SaveGame>>(File.ReadAllText(_config.SavesPath));
                
                foreach (SaveGame saveGame in saveGames)
                {
                    if (saveGameId == saveGame.Id)
                    {
                        saveGame.BackupMetadata[directory] = new BackupMetadata()
                        {
                            Tag = tag,
                            IsFavorite = isFavorite
                        };
                        updated = true;
                        break;
                    }
                }

                if (updated)
                {
                    string fileContent = JsonConvert.SerializeObject(saveGames);
                    File.WriteAllText(_config.SavesPath, fileContent);
                }
            }
        }
        catch (Exception e)
        {
            _loggingService.LogError($"{nameof(SaveService)}>{nameof(UpdateMetadata)} - {e}");
        }

        return updated;
    }

    public bool DeleteBackup(Guid saveGameId, string directory)
    {
        bool deleted = false;

        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
                DeleteMetadata(saveGameId, directory);
                deleted = true;
            }
        }
        catch (Exception e)
        {
            _loggingService.LogError($"{nameof(SaveService)}>{nameof(DeleteBackup)} - {e}");
        }

        return deleted;
    }

    public bool DeleteMetadata(Guid saveGameId, string directory)
    {
        bool deleted = false;

        try
        {
            List<SaveGame> saveGames = JsonConvert.DeserializeObject<List<SaveGame>>(File.ReadAllText(_config.SavesPath));
            
            foreach (SaveGame saveGame in saveGames)
            {
                if (saveGameId == saveGame.Id)
                {
                    saveGame.BackupMetadata.Remove(directory);
                    deleted = true;
                    break;
                }
            }

            if (deleted)
            {
                string fileContent = JsonConvert.SerializeObject(saveGames);
                File.WriteAllText(_config.SavesPath, fileContent);
            }
        }
        catch (Exception e)
        {
            _loggingService.LogError($"{nameof(SaveService)}>{nameof(DeleteMetadata)} - {e}");
        }

        return deleted;
    }

    #endregion

    #region Private Methods

    private bool RestoreFile(string source, string destination)
    {
        bool restored = false;

        if (Directory.Exists(source))
        {
            if (Directory.Exists(destination))
            {
                OverwriteDirectory(source, destination);
                restored = true;
            }
            else
            {
                string fileName = Path.GetFileName(destination);

                string[] files = Directory.GetFiles(source, "*.*", SearchOption.AllDirectories)
                    .Where(x => x.EndsWith(fileName))
                    .ToArray();

                if (files.Length == 1)
                {
                    File.Copy(files[0], destination, true);
                    restored = true;
                }
            }
        }

        return restored;
    }

    private void OverwriteDirectory(string sourceDir, string destinationDir, bool recursive = true)
    {
        try
        {
            DirectoryInfo dir = new(sourceDir);

            if (dir.Exists)
            {
                DirectoryInfo[] dirs = dir.GetDirectories();

                if (!Directory.Exists(destinationDir))
                {
                    Directory.CreateDirectory(destinationDir);
                }

                foreach (FileInfo file in dir.GetFiles())
                {
                    if (file.Name != _config.BackupScreenshotName)
                    {
                        string targetFilePath = Path.Combine(destinationDir, file.Name);
                        file.CopyTo(targetFilePath, true);
                    }
                }

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
        catch (Exception e)
        {
            _loggingService.LogError($"{nameof(SaveService)}>{nameof(OverwriteDirectory)} - {e}");
        }
    }

    private void DisableDuplicates(Guid? currentId, string location, ref List<SaveGame> saveGames)
    {
        try
        {
            foreach (SaveGame saveGame in saveGames)
            {
                if (saveGame.Id != currentId.GetValueOrDefault() && saveGame.SaveLocation == location)
                {
                    saveGame.Enabled = false;
                }
            }
        }
        catch (Exception e)
        {
            _loggingService.LogError($"{nameof(SaveService)}>{nameof(DisableDuplicates)} - {e}");
        }
    }
    
    #endregion
}