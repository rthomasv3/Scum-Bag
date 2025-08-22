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

    public bool HasChanges(string sourcePath, string targetPath)
    {
        bool hasChanges = false;

        if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
        {
            hasChanges = false; // Source doesn't exist, nothing to compare
        }
        else if (!File.Exists(targetPath) && !Directory.Exists(targetPath))
        {
            hasChanges = true; // Source exists but target doesn't, this is a change
        }
        else if (File.Exists(sourcePath) && File.Exists(targetPath))
        {
            // Both are files - compare sizes first
            FileInfo sourceFile = new(sourcePath);
            FileInfo targetFile = new(targetPath);

            if (sourceFile.Length != targetFile.Length)
            {
                hasChanges = true;
            }
            else
            {
                string sourceHash = GetHash(sourcePath);
                string targetHash = GetHash(targetPath);
                hasChanges = sourceHash != targetHash;
            }
        }
        else if (Directory.Exists(sourcePath) && Directory.Exists(targetPath))
        {
            // Both are directories - quick checks first
            DirectoryInfo sourceDir = new(sourcePath);
            DirectoryInfo targetDir = new(targetPath);

            FileInfo[] sourceFiles = sourceDir.GetFiles("*.*", SearchOption.AllDirectories)
                .Where(f => f.Name != _config.BackupScreenshotName)
                .ToArray();
            FileInfo[] targetFiles = targetDir.GetFiles("*.*", SearchOption.AllDirectories)
                .Where(f => f.Name != _config.BackupScreenshotName)
                .ToArray();

            if (sourceFiles.Length != targetFiles.Length)
            {
                hasChanges = true;
            }
            else
            {
                long sourceTotalSize = sourceFiles.Sum(f => f.Length);
                long targetTotalSize = targetFiles.Sum(f => f.Length);

                if (sourceTotalSize != targetTotalSize)
                {
                    hasChanges = true;
                }
                else
                {
                    string sourceHash = GetHash(sourcePath);
                    string targetHash = GetHash(targetPath);
                    hasChanges = sourceHash != targetHash;
                }
            }
        }
        else
        {
            // One is a file and one is a directory, 
            // probably need to handle this...
            hasChanges = true;
        }

        return hasChanges;
    }

    public string GetHash(string path)
    {
        string hash = String.Empty;
        byte[] hashData = null;

        if (Directory.Exists(path))
        {
            using MD5 myMD5 = MD5.Create();
            myMD5.Initialize();

            DirectoryInfo dir = new(path);
            FileInfo[] files = dir.GetFiles("*.*", SearchOption.AllDirectories);

            // Sort files to ensure deterministic ordering across different file systems
            Array.Sort(files, (a, b) => String.Compare(a.FullName, b.FullName, StringComparison.Ordinal));

            foreach (FileInfo fileInfo in files)
            {
                if (fileInfo.Name != _config.BackupScreenshotName)
                {
                    try
                    {
                        byte[] fileHash = GetFileHash(fileInfo.FullName);
                        if (fileHash != null)
                        {
                            // Use relative path from the base directory for consistency
                            string relativePath = Path.GetRelativePath(path, fileInfo.FullName);
                            byte[] pathBytes = System.Text.Encoding.UTF8.GetBytes(relativePath);
                            myMD5.TransformBlock(pathBytes, 0, pathBytes.Length, null, 0);

                            // Include file hash
                            myMD5.TransformBlock(fileHash, 0, fileHash.Length, null, 0);
                        }
                    }
                    catch (Exception e)
                    {
                        _loggingService.LogError($"{nameof(FileService)}>{nameof(GetHash)} - {e}");
                    }
                }
            }

            // Finalize the hash
            myMD5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            hashData = myMD5.Hash;
        }
        else if (File.Exists(path))
        {
            try
            {
                hashData = GetFileHash(path);
            }
            catch (Exception e)
            {
                _loggingService.LogError($"{nameof(FileService)}>{nameof(GetHash)} - {e}");
            }
        }

        return String.Join("", hashData?.Select(x => x.ToString("x2")) ?? []);
    }

    private byte[] GetFileHash(string filePath)
    {
        int attempts = 0;
        byte[] hashData = null;
        Exception lastError = null;

        if (File.Exists(filePath))
        {
            while (hashData == null && attempts < 3)
            {
                try
                {
                    using FileStream fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using MD5 md5 = MD5.Create();
                    hashData = md5.ComputeHash(fileStream);
                }
                catch (Exception e)
                {
                    lastError = e;
                    attempts++;
                    if (attempts < 3)
                    {
                        // Exponential backoff: 50ms, 100ms, 200ms
                        int delayMs = 50 * (1 << (attempts - 1));
                        Thread.Sleep(delayMs);
                    }
                }
            }
        }

        if (hashData == null && lastError != null)
        {
            _loggingService.LogError($"{nameof(FileService)}>{nameof(GetFileHash)} - {lastError}");
        }

        return hashData;
    }

    public byte[] GetFileData(string filePath)
    {
        int attempts = 0;
        byte[] fileData = null;
        Exception lastError = null;

        if (File.Exists(filePath))
        {
            while (fileData == null && attempts < 3)
            {
                try
                {
                    using FileStream fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using MemoryStream memoryStream = new();
                    fileStream.CopyTo(memoryStream);
                    fileData = memoryStream.ToArray();
                }
                catch (Exception e)
                {
                    lastError = e;
                    attempts++;
                    if (attempts < 3)
                    {
                        // Exponential backoff: 50ms, 100ms, 200ms
                        int delayMs = 50 * (1 << (attempts - 1));
                        Thread.Sleep(delayMs);
                    }
                }
            }
        }

        if (fileData == null && lastError != null)
        {
            _loggingService.LogError($"{nameof(FileService)}>{nameof(GetFileData)} - {lastError}");
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
