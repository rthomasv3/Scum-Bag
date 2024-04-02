using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Scum_Bag.Services;

internal sealed class LoggingService : IDisposable
{
    #region Fields

    private readonly string _logFileLocation;
    private readonly Queue<string> _logQueue;
    private readonly CancellationTokenSource _logQueueTokenSource;

    #endregion

    #region Constructor

    public LoggingService(Config config)
    {
        _logFileLocation = Path.Combine(config.DataDirectory, "logs.txt");
        _logQueue = new();
        _logQueueTokenSource = new();

        Task.Factory.StartNew(ProcessLogQueue);
    }

    #endregion

    #region Public Methods

    public void LogError(string text)
    {
        _logQueue.Enqueue($"[Error]: {text}\n");
    }

    public void LogInfo(string text)
    {
        _logQueue.Enqueue($"[Info]: {text}\n");
    }

    public void Dispose()
    {
        _logQueueTokenSource.Cancel();
    }

    #endregion

    #region Private Methods

    private void ProcessLogQueue()
    {
        while (!_logQueueTokenSource.Token.IsCancellationRequested)
        {
            if (_logQueue.Count > 0)
            {
                string text = _logQueue.Dequeue();
                File.AppendAllText(_logFileLocation, text);
            }

            Thread.Sleep(100);
        }
    }

    #endregion
}