using System;
using System.Threading.Tasks;
using System.Timers;
using NLog;
using PPSA.Models;
using PPSA.Services;
using System.Diagnostics;

namespace PPSA
{
    class Program
    {
        private static readonly ILogger _logger = LogManager.GetCurrentClassLogger();
        private static Configuration _config;
        private static Timer _timer;
        private static PlcService _plcService;
        private static FolderCleaner _folderCleaner;
        private static ShutdownService _shutdownService;
        private static bool _shutdownSequenceInitiated;

        static async Task Main(string[] args)
        {

            try
            {

                LogManager.ThrowConfigExceptions = true;
                LogManager.ThrowExceptions = true;

                _logger.Info("Starting PPSA application");

                // Initialize services first
                if (!await InitializeServices())
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

        private static async Task<bool> InitializeServices()
        {
            try
            {
                _config = new Configuration();

                // Initialize timer first
                _timer = new Timer(_config.Plc.ReadInterval);

                // Initialize PLC service with retry mechanism
                if (!await InitializePlcServiceWithRetry())
                {
                    return false;
                }

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

        private static async Task<bool> InitializePlcServiceWithRetry()
        {
            int retryCount = 0;
            var startTime = DateTime.Now;

            while (retryCount < _config.Plc.InitializeMaxRetries)
            {
                try
                {
                    _logger.Info($"Attempting to initialize PLC service (Attempt {retryCount + 1}/{_config.Plc.InitializeMaxRetries})");
                    _plcService = new PlcService(_config.Plc);
                    _logger.Info("Successfully initialized PLC service");
                    return true;
                }
                catch (Exception ex)
                {
                    retryCount++;
                    _logger.Error(ex, $"Failed to initialize PLC service (Attempt {retryCount}/{_config.Plc.InitializeMaxRetries})");

                    // Check if we've exceeded total timeout
                    var elapsed = DateTime.Now - startTime;
                    if (elapsed.TotalMilliseconds >= _config.Plc.InitializeTotalTimeoutMs)
                    {
                        _logger.Error($"PLC service initialization exceeded total timeout of {_config.Plc.InitializeTotalTimeoutMs}ms");
                        return false;
                    }

                    // Check if we should try again
                    if (retryCount >= _config.Plc.InitializeMaxRetries)
                    {
                        _logger.Error($"Maximum retry attempts ({_config.Plc.InitializeMaxRetries}) reached for PLC service initialization");
                        return false;
                    }

                    // Calculate delay with exponential backoff
                    int delayMs = Math.Min(
                        _config.Plc.InitializeInitialDelayMs * (int)Math.Pow(2, retryCount - 1),
                        _config.Plc.InitializeMaxDelayMs
                    );

                    _logger.Debug($"Waiting {delayMs}ms before retry attempt {retryCount + 1}");
                    await Task.Delay(delayMs);
                }
            }

            return false;
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
                _logger.Debug("Starting PLC tag check");
                bool tagValue = await _plcService.ReadTagValue();
                if (tagValue)
                {
                    _logger.Info("PLC tag value is True, initiating normal shutdown sequence");
                    await InitiateShutdownSequence();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during PLC tag check after all retry attempts");
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
            _logger.Info($"Initiating shutdown sequence. Error triggered: {isError}. Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss.ffff}");

            try
            {
                // Stop PLC monitoring first
                await StopPlcMonitoring();

                // Wait for Soft-NA process to terminate by itself
                _logger.Info("Starting process termination wait");
                bool processTerminated = await WaitForProcessTermination();
                if (!processTerminated)
                {
                    _logger.Error($"Soft-NA process did not terminate within the expected timeframe. Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss.ffff}");
                    return;
                }

                // Clean folders only after process has terminated
                if (_folderCleaner != null)
                {
                    _logger.Info("Starting folder cleanup");
                    bool foldersClean = await _folderCleaner.CleanFoldersAsync();
                    if (!foldersClean)
                    {
                        _logger.Warn("Folder cleanup incomplete, stopping shutdown sequence");
                        return;
                    }
                }

                // Initiate system shutdown only after folder cleanup is complete
                if (_shutdownService != null)
                {
                    _logger.Info("Initiating system shutdown");
                    await _shutdownService.InitiateShutdown();
                }
                else
                {
                    _logger.Error("ShutdownService not initialized, cannot perform system shutdown");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error during shutdown sequence. Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss.ffff}");
                if (!isError) // Prevent infinite recursion
                {
                    await InitiateShutdownSequence(isError: true);
                }
            }
        }

        private static async Task<bool> WaitForProcessTermination()
        {
            try
            {
                var processName = _config.Program.ProcessToMonitor;
                var timeout = TimeSpan.FromSeconds(_config.Program.ProcessTerminationTimeout);
                _logger.Info($"Waiting for {processName} process to terminate (timeout: {timeout.TotalMinutes} minutes)");
                
                var startTime = DateTime.Now;

                while (DateTime.Now - startTime < timeout)
                {
                    var processes = Process.GetProcessesByName(processName);
                    if (processes.Length == 0)
                    {
                        _logger.Info($"{processName} process has terminated");
                        return true;
                    }
                    await Task.Delay(1000); // Check every second
                }

                _logger.Error($"Timeout waiting for {processName} process to terminate");
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error while waiting for process termination");
                return false;
            }
        }
    }
}