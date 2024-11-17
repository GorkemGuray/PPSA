using System;
using System.Threading.Tasks;
using System.Timers;
using NLog;
using PPSA.Models;
using PPSA.Services;

namespace PPSA
{
    class Program
    {
        private static readonly ILogger _logger = LogManager.GetCurrentClassLogger();
        private static Configuration _config;
        private static Timer _timer;
        private static PlcService _plcService;
        private static ProgramCloser _programCloser;
        private static FolderCleaner _folderCleaner;
        private static ShutdownService _shutdownService;
        private static bool _shutdownSequenceInitiated;

        static async Task Main(string[] args)
        {
            try
            {
                _logger.Info("Starting PPSA application");

                // Initialize services first
                if (!InitializeServices())
                {
                    _logger.Error("Failed to initialize services. Starting shutdown sequence.");
                    await InitiateShutdownSequence(isError: true);
                    return;
                }

                // Only start PLC monitoring if initialization was successful
                StartPlcMonitoring();

                // Keep the application running
                await Task.Delay(-1);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Fatal error in main application loop");
                await InitiateShutdownSequence(isError: true);
            }
        }

        private static bool InitializeServices()
        {
            try
            {
                _config = new Configuration();

                // Initialize timer first
                _timer = new Timer(_config.Plc.ReadInterval);

                try
                {
                    _plcService = new PlcService(_config.Plc);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to initialize PLC service");
                    return false;
                }

                _programCloser = new ProgramCloser(_config.Program);
                _folderCleaner = new FolderCleaner(_config.Folder);
                _shutdownService = new ShutdownService(_config.Program);

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to initialize services");
                return false;
            }
        }

        private static void StartPlcMonitoring()
        {
            if (_timer == null)
            {
                _logger.Error("Timer not initialized. Cannot start PLC monitoring.");
                return;
            }

            try
            {
                _timer.Elapsed += async (sender, e) => await CheckPlcTagAsync();
                _timer.AutoReset = true;
                _timer.Enabled = true;
                _logger.Info("PLC monitoring started");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to start PLC monitoring");
                Task.Run(() => InitiateShutdownSequence(isError: true)).Wait();
            }
        }

        private static async Task CheckPlcTagAsync()
        {
            if (_shutdownSequenceInitiated || _plcService == null)
            {
                return;
            }

            try
            {
                bool tagValue = await _plcService.ReadTagValue();
                if (tagValue)
                {
                    await InitiateShutdownSequence();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during PLC tag check");
                await InitiateShutdownSequence(isError: true);
            }
        }

        private static async Task StopPlcMonitoring()
        {
            try
            {
                // Add null checks for timer and PLC service
                if (_timer != null)
                {
                    _timer.Stop();
                    _timer.Dispose();
                    _timer = null;  // Set to null after disposal
                }

                if (_plcService != null)
                {
                    _plcService.Dispose();
                    _plcService = null;  // Set to null after disposal
                }

                await Task.Delay(100); // Small delay to ensure cleanup
                _logger.Info("PLC monitoring stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error stopping PLC monitoring");
                // Don't throw here, just log the error
            }
        }

        private static async Task InitiateShutdownSequence(bool isError = false)
        {
            if (_shutdownSequenceInitiated)
            {
                return;
            }

            _shutdownSequenceInitiated = true;
            _logger.Info($"Initiating shutdown sequence. Error triggered: {isError}");

            try
            {
                // Stop PLC monitoring first
                await StopPlcMonitoring();

                // Close Soft-NA program if programCloser was initialized
                if (_programCloser != null)
                {
                    bool programClosed = await _programCloser.CloseProgramAsync();
                    if (!programClosed)
                    {
                        _logger.Warn("Failed to close program properly, continuing with shutdown sequence");
                    }
                }

                // Clean folders if folderCleaner was initialized
                if (_folderCleaner != null)
                {
                    bool foldersClean = await _folderCleaner.CleanFoldersAsync();
                    if (!foldersClean)
                    {
                        _logger.Warn("Folder cleanup incomplete, continuing with shutdown sequence");
                    }
                }

                // Initiate system shutdown if shutdownService was initialized
                if (_shutdownService != null)
                {
                    await _shutdownService.InitiateShutdown();
                }
                else
                {
                    _logger.Error("ShutdownService not initialized, cannot perform system shutdown");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during shutdown sequence");
                if (!isError) // Prevent infinite recursion
                {
                    await InitiateShutdownSequence(isError: true);
                }
            }
        }
    }
}