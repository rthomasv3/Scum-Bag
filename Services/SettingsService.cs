using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Galdr;
using Scum_Bag.DataAccess.Data;

namespace Scum_Bag.Services;

internal sealed class SettingsService
{
    #region Fields

    private readonly Config _config;
    private readonly LoggingService _loggingService;
    private readonly SaveService _saveService;
    private readonly EventService _eventService;

    #endregion

    #region Constructor

    public SettingsService(Config config, LoggingService loggingService, SaveService saveService, EventService eventService)
    {
        _config = config;
        _loggingService = loggingService;
        _saveService = saveService;
        _eventService = eventService;
    }

    #endregion

    #region Public Methods

    public Settings GetSettings()
    {
        Settings settings = null;

        try
        {
            settings = _config.GetSettings();
        }
        catch (Exception e)
        {
            _loggingService.LogError($"{nameof(SettingsService)}>{nameof(GetSettings)} - {e}");
        }

        return settings;
    }

    public bool SaveSettings(Settings settings)
    {
        bool saved = false;

        if (!String.IsNullOrWhiteSpace(settings.BackupsDirectory) && Directory.Exists(settings.BackupsDirectory))
        {
            saved = true;
            bool notifyLocationChanged = false;

            try
            {
                if (_config.BackupsDirectory != settings.BackupsDirectory)
                {
                    saved = UpdateBackupsDirectory(_config.BackupsDirectory, settings.BackupsDirectory);
                    notifyLocationChanged = saved;
                }

                if (saved)
                {
                    _config.SaveSettings(settings);
                    _saveService.UpdateSavesBackupLocation();

                    if (notifyLocationChanged)
                    {
                        _eventService.PublishEvent("backupLocationChanged", settings.BackupsDirectory);
                    }
                }
            }
            catch (Exception e)
            {
                _loggingService.LogError($"{nameof(SettingsService)}>{nameof(SaveSettings)} - {e}");
            }
        }

        return saved;
    }

    #endregion

    #region Private Methods
    
    private bool UpdateBackupsDirectory(string oldDirectory, string newDirectory)
    {
        bool backupsMoved = false;

        if (!String.IsNullOrEmpty(newDirectory))
        {
            if (!Directory.Exists(newDirectory))
            {
                Directory.CreateDirectory(newDirectory);
            }

            backupsMoved = true;
            DirectoryInfo oldDirectoryInfo = new DirectoryInfo(oldDirectory);
            DirectoryInfo[] oldDirectories = oldDirectoryInfo.GetDirectories();

            foreach (DirectoryInfo directory in oldDirectories)
            {
                string newDirectoryPath = Path.Combine(newDirectory, directory.Name);
                CopyDirectory(directory.FullName, Path.Combine(newDirectory, directory.Name));

                if (VerifyContent(directory.FullName, newDirectoryPath))
                {
                    Directory.Delete(directory.FullName, true);
                    backupsMoved &= true;
                }
                else
                {
                    backupsMoved = false;
                }
            }
        }

        return backupsMoved;
    }

    private void CopyDirectory(string sourceDir, string destinationDir, bool recursive = true)
    {
        try
        {
            DirectoryInfo dir = new(sourceDir);

            if (dir.Exists)
            {
                DirectoryInfo[] dirs = dir.GetDirectories();

                Directory.CreateDirectory(destinationDir);

                foreach (FileInfo file in dir.GetFiles())
                {
                    string targetFilePath = Path.Combine(destinationDir, file.Name);
                    file.CopyTo(targetFilePath, true);
                }

                if (recursive)
                {
                    foreach (DirectoryInfo subDir in dirs)
                    {
                        string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                        CopyDirectory(subDir.FullName, newDestinationDir, true);
                    }
                }
            }
        }
        catch (Exception e)
        {
            _loggingService.LogError($"{nameof(SettingsService)}>{nameof(CopyDirectory)} - {e}");
        }
    }

    private bool VerifyContent(string oldDirectory, string newDirectory)
    {
        return GetHash(oldDirectory) == GetHash(newDirectory);
    }

    private string GetHash(string path)
    {
        string hash = String.Empty;
        byte[] hashData = null;

        using SHA256 mySHA256 = SHA256.Create();

        if (Directory.Exists(path))
        {
            List<byte> allHashes = new();

            DirectoryInfo dir = new(path);
            FileInfo[] files = dir.GetFiles("*.*", SearchOption.AllDirectories);

            foreach (FileInfo fileInfo in files)
            {
                try
                {
                    using FileStream fileStream = fileInfo.Open(FileMode.Open);
                    fileStream.Position = 0;
                    byte[] hashValue = mySHA256.ComputeHash(fileStream);
                    allHashes.AddRange(hashValue);
                }
                catch (Exception e)
                {
                    _loggingService.LogError($"{nameof(SettingsService)}>{nameof(GetHash)} - {e}");
                }
            }

            hashData = mySHA256.ComputeHash(allHashes.ToArray());
        }
        else if (File.Exists(path))
        {
            FileInfo fileInfo = new(path);
            try
            {
                using FileStream fileStream = fileInfo.Open(FileMode.Open);
                fileStream.Position = 0;
                hashData = mySHA256.ComputeHash(fileStream);
            }
            catch (Exception e)
            {
                _loggingService.LogError($"{nameof(SettingsService)}>{nameof(GetHash)} - {e}");
            }
        }

        if (hashData != null)
        {
            StringBuilder builder = new();
            for (int i = 0; i < hashData.Length; ++i)
            {
                builder.Append(hashData[i].ToString("x2"));
            }
            hash = builder.ToString();
        }

        return hash;
    }

    #endregion
}
