using System.IO;

namespace Scum_Bag.Services;

internal sealed class LoggingService
{
    #region Fields

    private readonly string _logFileLocation;

    #endregion

    #region Constructor

    public LoggingService(Config config)
    {
        _logFileLocation = Path.Combine(config.DataDirectory, "logs.txt");
    }

    #endregion

    #region Public Methods

    public void LogError(string text)
    {
        File.AppendAllText(_logFileLocation, $"[Error]: {text}\n");
    }

    public void LogInfo(string text)
    {
        File.AppendAllText(_logFileLocation, $"[Info]: {text}\n");
    }

    #endregion
}