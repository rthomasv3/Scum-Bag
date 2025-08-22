using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace Scum_Bag.Services;

internal sealed class FileService
{
    #region Fields

    private readonly Config _config;
    private readonly LoggingService _loggingService;

    #endregion

    #region Constructor

    public FileService(Config config, LoggingService loggingService)
    {
        _config = config;
        _loggingService = loggingService;
    }

    #endregion

    #region Public Methods

    public string GetHash(string path)
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
                if (fileInfo.Name != _config.BackupScreenshotName)
                {
                    try
                    {
                        byte[] fileData = GetFileData(fileInfo.FullName);
                        byte[] hashValue = mySHA256.ComputeHash(fileData);
                        allHashes.AddRange(hashValue);
                    }
                    catch (Exception e)
                    {
                        _loggingService.LogError($"{nameof(FileService)}>{nameof(GetHash)} - {e}");
                    }
                }
            }

            hashData = mySHA256.ComputeHash(allHashes.ToArray());
        }
        else if (File.Exists(path))
        {
            try
            {
                byte[] fileData = GetFileData(path);
                hashData = mySHA256.ComputeHash(fileData);
            }
            catch (Exception e)
            {
                _loggingService.LogError($"{nameof(FileService)}>{nameof(GetHash)} - {e}");
            }
        }

        return String.Join("", hashData?.Select(x => x.ToString("x2")) ?? []);
    }

    public byte[] GetFileData(string filePath)
    {
        int attempts = 0;
        byte[] fileData = null;
        string error = null;

        if (File.Exists(filePath))
        {
            while (fileData == null && ++attempts < 3)
            {
                try
                {
                    using FileStream fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using MemoryStream memoryStream = new();
                    fileStream.CopyTo(memoryStream);
                    fileData = memoryStream.ToArray();
                }
                catch (Exception e)
                {
                    error = e.ToString();
                    Thread.Sleep(100);
                }
            }
        }

        if (error != null)
        {
            _loggingService.LogError($"{nameof(FileService)}>{nameof(GetFileData)} - {error}");
        }

        return fileData;
    }

    public void CopyDirectory(string sourceDir, string destinationDir, bool recursive = true, bool overwrite = false)
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
                    file.CopyTo(targetFilePath, overwrite);
                }

                if (recursive)
                {
                    foreach (DirectoryInfo subDir in dirs)
                    {
                        string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                        CopyDirectory(subDir.FullName, newDestinationDir, recursive, overwrite);
                    }
                }
            }
        }
        catch (Exception e)
        {
            _loggingService.LogError($"{nameof(FileService)}>{nameof(CopyDirectory)} - {e}");
        }
    }

    #endregion
}