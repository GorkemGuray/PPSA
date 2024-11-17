using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using PPSA.Models;

namespace PPSA.Services
{
    public class FolderCleaner
    {
        private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
        private readonly FolderConfig _config;

        public event EventHandler<string> CleanupCompleted;

        public FolderCleaner(FolderConfig config)
        {
            _config = config;
        }

        public async Task<bool> CleanFoldersAsync()
        {
            try
            {
                foreach (var folderPath in _config.FolderPaths)
                {
                    await CleanFolderAsync(folderPath);
                }

                OnCleanupCompleted("Folder cleanup completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Folder cleanup failed");
                return false;
            }
        }

        private async Task CleanFolderAsync(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                _logger.Warn($"Folder not found: {folderPath}");
                return;
            }

            await Task.Run(() =>
            {
                var folders = Directory.GetDirectories(folderPath)
                    .Select(dir => new DirectoryInfo(dir))
                    .Where(dir => DateTime.TryParseExact(dir.Name, "yyyyMMdd", null,
                        System.Globalization.DateTimeStyles.None, out _))
                    .OrderBy(dir => dir.Name)
                    .ToList();

                if (folders.Count <= _config.MaxFolderCount)
                {
                    _logger.Info($"No folders to delete in: {folderPath}");
                    return;
                }

                var thresholdDate = DateTime.Now.AddDays(-_config.DaysThreshold);
                var foldersToDelete = folders
                    .Where(dir => DateTime.ParseExact(dir.Name, "yyyyMMdd", null) < thresholdDate)
                    .Take(folders.Count - _config.MaxFolderCount)
                    .ToList();

                foreach (var folder in foldersToDelete)
                {
                    try
                    {
                        Directory.Delete(folder.FullName, true);
                        _logger.Info($"Deleted folder: {folder.FullName}");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Failed to delete folder: {folder.FullName}");
                        throw;
                    }
                }
            });
        }

        protected virtual void OnCleanupCompleted(string message)
        {
            CleanupCompleted?.Invoke(this, message);
        }
    }
}
