using System;
using System.IO;
using System.Linq;


namespace PPSA
{
    internal class FolderCleaner
    {
        // Event definition
        public event EventHandler<string> CleanupCompleted;

        private readonly int _maxFolderCount;
        private readonly int _daysThreshold;

        public FolderCleaner(int maxFolderCount, int daysThreshold)
        {
            _maxFolderCount = maxFolderCount;
            _daysThreshold = daysThreshold;
        }

        public void CleanFolders(string[] folderPaths)
        {
            foreach (var folderPath in folderPaths)
            {
                if (!Directory.Exists(folderPath))
                {
                    Console.WriteLine($"Folder not found: {folderPath}");
                    continue;
                }

                var folders = Directory.GetDirectories(folderPath)
                    .Select(dir => new DirectoryInfo(dir))
                    .Where(dir => DateTime.TryParseExact(dir.Name, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out _))
                    .OrderBy(dir => dir.Name)
                    .ToList();

                if (folders.Count <= _maxFolderCount)
                {
                    Console.WriteLine($"There are no folders to delete: {folderPath}");
                    continue;
                }

                // Delete (Oldest folders by date)
                var thresholdDate = DateTime.Now.AddDays(-_daysThreshold);
                var foldersToDelete = folders
                    .Where(dir => DateTime.ParseExact(dir.Name, "yyyyMMdd", null) < thresholdDate)
                    .Take(folders.Count - _maxFolderCount)
                    .ToList();

                foreach (var folder in foldersToDelete)
                {
                    try
                    {
                        Directory.Delete(folder.FullName, true);
                        Console.WriteLine($"Deleted: {folder.FullName}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Could not be deleted: {folder.FullName}, Hata: {ex.Message}");
                    }
                }
            }

            // Triggered after deletions in all folders are complete
            OnCleanupCompleted("The deletion of all folders is complete.");
        }

        protected virtual void OnCleanupCompleted(string message)
        {
            CleanupCompleted?.Invoke(this, message);
        }
    }
}
