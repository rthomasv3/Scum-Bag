using System;
using System.Collections.Generic;
using Galdr.Native;
using Scum_Bag.DataAccess.Data;
using Scum_Bag.Models;
using Scum_Bag.Services;

namespace Scum_Bag.Commands;

internal sealed class SaveGameCommands
{
    #region Fields

    private readonly SaveService _saveService;
    private readonly DialogService _dialogService;
    private readonly GameService _gameService;
    private readonly LoggingService _logger;

    #endregion

    #region Constructor

    public SaveGameCommands(SaveService saveService, DialogService dialogService, GameService gameService,
        LoggingService logger)
    {
        _saveService = saveService;
        _dialogService = dialogService;
        _gameService = gameService;
        _logger = logger;
    }

    #endregion

    #region Public Methods

    public SaveGameNodes GetSaves()
    {
        return _saveService.GetSaves();
    }

    public SaveGame GetSave(SaveGame saveGame)
    {
        return _saveService.GetSave(saveGame.Id);
    }

    public BackupsList GetBackups(SaveGame saveGame)
    {
        return _saveService.GetBackups(saveGame.Id);
    }

    public string GetScreenshot(string directory)
    {
        return _saveService.GetScreenshot(directory);
    }

    public Guid CreateSave(SaveGame saveGame)
    {
        return _saveService.CreateSave(saveGame);
    }

    public bool UpdateSave(SaveGame saveGame)
    {
        return _saveService.UpdateSave(saveGame);
    }

    public bool DeleteSave(SaveGame save)
    {
        return _saveService.DeleteSave(save.Id);
    }

    public bool CreateManualBackup(SaveGame save)
    {
        return _saveService.CreateManualBackup(save.Id);
    }

    public bool Restore(Restore restore)
    {
        return _saveService.RestoreSave(restore.Id, restore.Time);
    }

    public bool UpdateMetadata(Backup backup)
    {
        return _saveService.UpdateMetadata(backup.SaveId, backup.Directory, backup.Tag, backup.IsFavorite);
    }

    public bool DeleteBackup(Backup backup)
    {
        return _saveService.DeleteBackup(backup.SaveId, backup.Directory);
    }

    public GamesList GetGames()
    {
        return _gameService.GetInstalledGames();
    }

    public string OpenFileDialog()
    {
        string file = null;

        try
        {
            file = _dialogService.OpenFileDialog();
        }
        catch (Exception e)
        {
            _logger.LogError(e.ToString());
        }

        return file;
    }

    public string OpenDirectoryDialog()
    {
        string directory = null;

        try
        {
            directory = _dialogService.OpenDirectoryDialog();
        }
        catch (Exception e)
        {
            _logger.LogError(e.ToString());
        }

        return directory;
    }

    public bool LaunchGame(string gameName)
    {
        return _gameService.LaunchGame(gameName);
    }

    #endregion
}