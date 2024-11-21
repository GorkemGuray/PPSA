using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using NLog;
using PPSA.Models;

namespace PPSA.Services
{
    public class FolderCleaner
    {
        private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
        private readonly FolderConfig _config;
        private readonly SemaphoreSlim _cleanupLock = new SemaphoreSlim(1, 1);
        private bool _isCleanupInProgress;

        public event EventHandler<string> CleanupCompleted;
        public event EventHandler<Exception> CleanupError;

        public FolderCleaner(FolderConfig config)
        {
            _config = config;
        }

        public bool IsCleanupInProgress => _isCleanupInProgress;

        public async Task<bool> CleanFoldersAsync()
        {
            if (!await _cleanupLock.WaitAsync(TimeSpan.FromSeconds(30)))
            {
                _logger.Warn("Could not acquire cleanup lock - another cleanup operation might be in progress");
                return false;
            }

            try
            {
                _isCleanupInProgress = true;
                var tasks = _config.FolderPaths.Select(CleanFolderAsync);
                var results = await Task.WhenAll(tasks);

                bool allSucceeded = results.All(x => x);
                if (allSucceeded)
                {
                    OnCleanupCompleted("Folder cleanup completed successfully");
                }
                return allSucceeded;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Folder cleanup failed");
                OnCleanupError(ex);
                return false;
            }
            finally
            {
                _isCleanupInProgress = false;
                _cleanupLock.Release();
            }
        }

        private async Task<bool> CleanFolderAsync(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                _logger.Warn($"Folder not found: {folderPath}");
                return true; // Consider it a success if folder doesn't exist
            }

            try
            {
                var folders = await Task.Run(() =>
                    Directory.GetDirectories(folderPath)
                        .Select(dir => new DirectoryInfo(dir))
                        .Where(dir => IsValidDateFolder(dir.Name))
                        .OrderBy(dir => dir.Name)
                        .ToList());

                if (folders.Count <= _config.MaxFolderCount)
                {
                    _logger.Info($"No folders to delete in: {folderPath}");
                    return true;
                }

                var thresholdDate = DateTime.Now.AddDays(-_config.DaysThreshold);
                var foldersToDelete = folders
                    .Where(dir => DateTime.ParseExact(dir.Name, "yyyyMMdd", null) < thresholdDate)
                    .Take(folders.Count - _config.MaxFolderCount)
                    .ToList();

                foreach (var folder in foldersToDelete)
                {
                    await DeleteFolderWithRetryAsync(folder);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to clean folder: {folderPath}");
                return false;
            }
        }

        private async Task DeleteFolderWithRetryAsync(DirectoryInfo folder, int maxRetries = 3)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    await Task.Run(() => Directory.Delete(folder.FullName, true));
                    _logger.Info($"Deleted folder: {folder.FullName}");
                    return;
                }
                catch (IOException) when (i < maxRetries - 1)
                {
                    _logger.Warn($"Failed to delete folder: {folder.FullName}, attempt {i + 1} of {maxRetries}");
                    await Task.Delay(1000 * (i + 1)); // Exponential backoff
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Failed to delete folder: {folder.FullName}");
                    throw;
                }
            }
        }

        private bool IsValidDateFolder(string folderName)
        {
            return DateTime.TryParseExact(folderName, "yyyyMMdd", null,
                System.Globalization.DateTimeStyles.None, out _);
        }

        protected virtual void OnCleanupCompleted(string message)
        {
            CleanupCompleted?.Invoke(this, message);
        }

        protected virtual void OnCleanupError(Exception ex)
        {
            CleanupError?.Invoke(this, ex);
        }
    }
}
