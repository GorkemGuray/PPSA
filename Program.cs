using System;
using System.Diagnostics;
using libplctag;
using System.Threading.Tasks;
using NLog;

namespace PPSA
{
    class Program
    {
        private static readonly ILogger _logger = LogManager.GetCurrentClassLogger();
        private static Tag _tag;
        private static bool _shouldRun = true;
        private static DateTime _startTime;
        private static bool _hasCheckedInitialDelay = false;
        private const int MAX_RETRIES = 3;
        private const int RETRY_DELAY_MS = 5000;

        static async Task Main(string[] args)
        {
            try
            {
                _startTime = DateTime.Now;
                if (!await InitializePlcWithRetry())
                {
                    _logger.Error("Failed to initialize PLC after all retries. Shutting down.");
                    await InitiateShutdown();
                    return;
                }

                _logger.Info("PPSA application started");

                while (_shouldRun)
                {
                    await CheckShutdownConditions();
                    await Task.Delay(1000); // Check every second
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error in main loop");
                await InitiateShutdown();
            }
        }

        private static async Task<bool> InitializePlcWithRetry()
        {
            for (int attempt = 1; attempt <= MAX_RETRIES; attempt++)
            {
                try
                {
                    _logger.Info($"Attempting to initialize PLC (Attempt {attempt} of {MAX_RETRIES})");
                    _tag = new Tag
                    {
                        Name = "PC_KAPAT_V7",
                        Gateway = "192.168.250.1",
                        Path = "1,0",
                        PlcType = PlcType.Omron,
                        Protocol = Protocol.ab_eip,
                        Timeout = TimeSpan.FromMilliseconds(1000)
                    };

                    _tag.Initialize();
                    
                    // Test the connection by trying to read the tag
                    await _tag.ReadAsync();
                    var status = _tag.GetStatus();
                    if (status != Status.Ok)
                    {
                        throw new Exception($"Tag initialization check failed with status: {status}");
                    }

                    _logger.Info("PLC initialized successfully");
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"PLC initialization attempt {attempt} failed");
                    
                    if (attempt < MAX_RETRIES)
                    {
                        _logger.Info($"Waiting {RETRY_DELAY_MS/1000} seconds before next retry...");
                        await Task.Delay(RETRY_DELAY_MS);
                    }
                }
            }

            return false;
        }

        private static async Task CheckShutdownConditions()
        {
            try
            {
                bool isSoftNaRunning = Process.GetProcessesByName("soft-na").Length > 0;
                bool shutdownTag = await ReadPlcTag(_tag);
                TimeSpan timeSinceStart = DateTime.Now - _startTime;

                if (!_hasCheckedInitialDelay)
                {
                    // Initial check when tag is false
                    if (!shutdownTag)
                    {
                        if (timeSinceStart.TotalSeconds >= 20)
                        {
                            _hasCheckedInitialDelay = true;
                            if (!isSoftNaRunning)
                            {
                                _logger.Info("Tag is false and soft-na process not found after 20 seconds - initiating shutdown");
                                await InitiateShutdown();
                                return;
                            }
                            else
                            {
                                _logger.Info("soft-na process started within 20 seconds - continuing normal operation");
                            }
                        }
                    }
                    else
                    {
                        // If tag is true during startup, immediately check soft-na
                        _hasCheckedInitialDelay = true;
                        if (!isSoftNaRunning)
                        {
                            _logger.Info("Tag is true and soft-na process not running - initiating shutdown");
                            await InitiateShutdown();
                            return;
                        }
                        else
                        {
                            _logger.Info("Tag is true but soft-na is running - continuing normal operation");
                        }
                    }
                }
                else
                {
                    // Regular operation after initial delay
                    if (shutdownTag && !isSoftNaRunning)
                    {
                        _logger.Info("Tag is true and soft-na process not running - initiating shutdown");
                        await InitiateShutdown();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error checking shutdown conditions");
                throw;
            }
        }

        private static async Task<bool> ReadPlcTag(Tag tag)
        {
            try
            {
                await tag.ReadAsync();
                var status = tag.GetStatus();
                if (status != Status.Ok)
                {
                    throw new Exception($"Error reading tag: {status}");
                }
                return tag.GetBit(0);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error reading PLC tag: {tag.Name}");
                throw;
            }
        }

        private static async Task InitiateShutdown()
        {
            try
            {
                _logger.Info("Initiating system shutdown");
                _shouldRun = false;
                Process.Start("shutdown", "/s /t 0");
                await Task.Delay(2000); // Give some time for logging
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during shutdown");
                // Force shutdown even if there's an error
                Process.Start("shutdown", "/s /f /t 0");
            }
        }
    }
}