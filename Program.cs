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
        private static HealthMonitor _healthMonitor;
        private static bool _shutdownSequenceInitiated;
        private static readonly object _shutdownLock = new object();

        static async Task Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                _logger.Fatal(e.ExceptionObject as Exception, "Unhandled exception occurred");
                InitiateShutdownSequence(isError: true).Wait();
            };

            try
            {
                LogManager.ThrowConfigExceptions = true;
                LogManager.ThrowExceptions = true;

                _logger.Info("*******************************Starting PPSA application*******************************");

                // Initialize services first
                if (!await InitializeServices())
                {
                    _logger.Error("Failed to initialize services. Starting shutdown sequence.");
                    await InitiateShutdownSequence(isError: true);
                    return;
                }

                // Initialize health monitoring
                InitializeHealthMonitoring();

                // Start PLC monitoring
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

        private static void InitializeHealthMonitoring()
        {
            _healthMonitor = new HealthMonitor();

            // Register PLC service health check
            _healthMonitor.RegisterService("PlcService", () => _plcService?.IsHealthy() ?? false);

            // Register folder cleaner health check
            _healthMonitor.RegisterService("FolderCleaner", () => _folderCleaner != null);

            // Register shutdown service health check
            _healthMonitor.RegisterService("ShutdownService", () => _shutdownService != null);

            // Monitor overall application health
            _healthMonitor.HealthStatusChanged += (sender, e) =>
            {
                if (!e.Status.IsHealthy && e.Status.ServiceName == "PlcService")
                {
                    _logger.Error($"Critical service {e.Status.ServiceName} is unhealthy. Initiating shutdown sequence.");
                    InitiateShutdownSequence(isError: true).Wait();
                }
            };
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

                    // Subscribe to connection status changes
                    _plcService.ConnectionStatusChanged += (sender, isConnected) =>
                    {
                        if (!isConnected)
                        {
                            _logger.Warn("PLC connection lost");
                        }
                    };

                    _logger.Info("Successfully initialized PLC service");
                    return true;
                }
                catch (Exception ex)
                {
                    retryCount++;
                    _logger.Error(ex, $"Failed to initialize PLC service (Attempt {retryCount}/{_config.Plc.InitializeMaxRetries})");

                    if (DateTime.Now - startTime > TimeSpan.FromMilliseconds(_config.Plc.InitializeTotalTimeoutMs))
                    {
                        _logger.Error($"PLC service initialization exceeded total timeout of {_config.Plc.InitializeTotalTimeoutMs}ms");
                        return false;
                    }

                    if (retryCount >= _config.Plc.InitializeMaxRetries)
                    {
                        _logger.Error($"Maximum retry attempts ({_config.Plc.InitializeMaxRetries}) reached for PLC service initialization");
                        return false;
                    }

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
                _logger.Error(ex, "Error during PLC tag check");
                await InitiateShutdownSequence(isError: true);
            }
        }

        private static async Task StopPlcMonitoring()
        {
            try
            {
                if (_timer != null)
                {
                    _timer.Stop();
                    _timer.Dispose();
                    _timer = null;
                }

                if (_plcService != null)
                {
                    _plcService.Dispose();
                    _plcService = null;
                }

                if (_healthMonitor != null)
                {
                    _healthMonitor.Dispose();
                    _healthMonitor = null;
                }

                await Task.Delay(100);
                _logger.Info("PLC monitoring stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error stopping PLC monitoring");
            }
        }

        private static async Task InitiateShutdownSequence(bool isError = false)
        {
            lock (_shutdownLock)
            {
                if (_shutdownSequenceInitiated)
                {
                    return;
                }
                _shutdownSequenceInitiated = true;
            }

            _logger.Info($"Initiating shutdown sequence. Error triggered: {isError}. Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss.ffff}");

            try
            {
                await StopPlcMonitoring();

                _logger.Info("Starting process termination wait");
                bool processTerminated = await WaitForProcessTermination();
                if (!processTerminated)
                {
                    _logger.Info("Soft-NA process is still running, proceeding with system shutdown anyway");
                }

                if (_folderCleaner != null)
                {
                    _logger.Info("Starting folder cleanup");
                    bool foldersClean = await _folderCleaner.CleanFoldersAsync();
                    if (!foldersClean)
                    {
                        _logger.Warn("Folder cleanup incomplete");
                    }
                }

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
                if (!isError)
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
                _logger.Info($"Waiting for {processName} process to terminate (timeout: {timeout.TotalSeconds} seconds)");
                
                var startTime = DateTime.Now;
                var checkInterval = TimeSpan.FromSeconds(1);

                while (DateTime.Now - startTime < timeout)
                {
                    var processes = Process.GetProcessesByName(processName);
                    if (processes.Length == 0)
                    {
                        _logger.Info($"{processName} process has terminated");
                        return true;
                    }
                    await Task.Delay(checkInterval);
                }

                _logger.Info($"Timeout waiting for {processName} process to terminate");
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