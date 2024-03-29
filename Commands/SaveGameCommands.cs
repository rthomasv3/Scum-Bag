using System;
using System.Collections.Generic;
using Galdr;
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

    [Command]
    public IEnumerable<TreeNode> GetSaves()
    {
        return _saveService.GetSaves();
    }

    [Command]
    public SaveGame GetSave(SaveGame saveGame)
    {
        return _saveService.GetSave(saveGame.Id);
    }

    [Command]
    public IEnumerable<Backup> GetBackups(SaveGame saveGame)
    {
        return _saveService.GetBackups(saveGame.Id);
    }

    [Command]
    public string GetScreenshot(string directory)
    {
        return _saveService.GetScreenshot(directory);
    }

    [Command]
    public Guid CreateSave(SaveGame saveGame)
    {
        return _saveService.CreateSave(saveGame);
    }

    [Command]
    public bool UpdateSave(SaveGame saveGame)
    {
        return _saveService.UpdateSave(saveGame);
    }

    [Command]
    public bool DeleteSave(SaveGame save)
    {
        return _saveService.DeleteSave(save.Id);
    }

    [Command]
    public bool CreateManualBackup(SaveGame save)
    {
        return _saveService.CreateManualBackup(save.Id);
    }

    [Command]
    public bool Restore(Restore restore)
    {
        return _saveService.RestoreSave(restore.Id, restore.Time);
    }

    [Command]
    public IEnumerable<string> GetGames()
    {
        return _gameService.GetInstalledGames();
    }

    [Command]
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

    [Command]
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

    #endregion
}