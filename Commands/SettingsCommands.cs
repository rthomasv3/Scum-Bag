using Scum_Bag.DataAccess.Data;
using Scum_Bag.Services;

namespace Scum_Bag.Commands;

internal sealed class SettingsCommands
{
    #region Fields

    private readonly SettingsService _settingsService;

    #endregion

    #region Constructor

    public SettingsCommands(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    #endregion

    #region Public Methods

    public Settings GetSettings()
    {
        return _settingsService.GetSettings();
    }

    public bool SaveSettings(Settings settings)
    {
        return _settingsService.SaveSettings(settings);
    }

    #endregion
}