using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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

    #endregion

    #region Constructor

    public SaveGameCommands(SaveService saveService, DialogService dialogService, GameService gameService)
    {
        _saveService = saveService;
        _dialogService = dialogService;
        _gameService = gameService;
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
        return _dialogService.OpenFileDialog();
    }

    [Command]
    public string OpenDirectoryDialog()
    {
        return _dialogService.OpenDirectoryDialog();
    }

    #endregion
}