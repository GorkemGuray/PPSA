using System;
using System.Threading;
using System.Threading.Tasks;
using libplctag;
using NLog;
using PPSA.Models;

namespace PPSA.Services
{
    public class PlcService : IDisposable
    {
        private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
        private readonly PlcConfig _config;
        private Tag _tag;
        private bool _disposed;
        private bool _isConnected;
        private int _currentRetryCount = 0;
        private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);
        private DateTime _lastSuccessfulRead;
        private DateTime _connectionLostTime;
        private const int CONNECTION_GRACE_PERIOD_MS = 5000; // 5 second grace period

        public event EventHandler<bool> TagValueChanged;
        public event EventHandler<bool> ConnectionStatusChanged;

        public PlcService(PlcConfig config)
        {
            _config = config;
            _lastSuccessfulRead = DateTime.MinValue;
            InitializeTagAsync().Wait();
        }

        private async Task InitializeTagAsync()
        {
            await _connectionLock.WaitAsync();
            try
            {
                _tag?.Dispose();
                
                int initRetryCount = 0;
                var initStartTime = DateTime.Now;

                while (initRetryCount < _config.InitializeMaxRetries)
                {
                    try
                    {
                        _tag = new Tag
                        {
                            Name = _config.TagName,
                            Gateway = _config.Gateway,
                            Path = _config.Path,
                            PlcType = PlcType.Omron,
                            Protocol = Protocol.ab_eip,
                            Timeout = TimeSpan.FromMilliseconds(_config.Timeout)
                        };

                        // Attempt initial connection
                        await _tag.ReadAsync();
                        var status = _tag.GetStatus();
                        if (status != Status.Ok)
                        {
                            throw new InvalidOperationException($"Failed to read tag: {status}");
                        }
                        UpdateConnectionStatus(true);
                        return;
                    }
                    catch (Exception ex)
                    {
                        initRetryCount++;
                        
                        // Check if we've exceeded total timeout
                        if ((DateTime.Now - initStartTime).TotalMilliseconds > _config.InitializeTotalTimeoutMs)
                        {
                            _logger.Error(ex, $"PLC initialization exceeded total timeout of {_config.InitializeTotalTimeoutMs}ms");
                            throw;
                        }

                        // Check if we've exceeded max retries
                        if (initRetryCount >= _config.InitializeMaxRetries)
                        {
                            _logger.Error(ex, $"Failed to initialize PLC after {_config.InitializeMaxRetries} attempts");
                            throw;
                        }

                        // Calculate delay for next retry
                        var delay = Math.Min(
                            _config.InitializeInitialDelayMs * Math.Pow(2, initRetryCount - 1),
                            _config.InitializeMaxDelayMs
                        );

                        _logger.Warn($"PLC initialization attempt {initRetryCount} failed. Retrying in {delay}ms");
                        await Task.Delay((int)delay);
                    }
                }

                throw new InvalidOperationException($"Failed to initialize PLC after {_config.InitializeMaxRetries} attempts");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to initialize PLC tag");
                UpdateConnectionStatus(false);
                throw;
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        private void UpdateConnectionStatus(bool isConnected)
        {
            if (_isConnected != isConnected)
            {
                _isConnected = isConnected;
                if (!isConnected)
                {
                    _connectionLostTime = DateTime.UtcNow;
                    _logger.Warn($"PLC connection lost at {_connectionLostTime:yyyy-MM-dd HH:mm:ss.fff}");
                }
                else
                {
                    _logger.Info($"PLC connection restored at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}");
                }
                ConnectionStatusChanged?.Invoke(this, isConnected);
            }
        }

        public bool IsHealthy()
        {
            // Consider the service unhealthy if we haven't had a successful read in twice the read interval
            var healthyTimeWindow = TimeSpan.FromMilliseconds(_config.ReadInterval * 2);
            var timeSinceLastRead = DateTime.UtcNow - _lastSuccessfulRead;

            // If connection is lost but we're within grace period, still consider it healthy
            if (!_isConnected)
            {
                var timeSinceConnectionLost = DateTime.UtcNow - _connectionLostTime;
                if (timeSinceConnectionLost.TotalMilliseconds <= CONNECTION_GRACE_PERIOD_MS)
                {
                    _logger.Debug($"PLC connection lost but within grace period ({timeSinceConnectionLost.TotalMilliseconds:F0}ms < {CONNECTION_GRACE_PERIOD_MS}ms)");
                    return true;
                }
                _logger.Warn($"PLC connection lost and grace period exceeded ({timeSinceConnectionLost.TotalMilliseconds:F0}ms > {CONNECTION_GRACE_PERIOD_MS}ms)");
            }

            var isHealthy = _isConnected && timeSinceLastRead <= healthyTimeWindow;
            if (!isHealthy)
            {
                _logger.Warn($"PLC service unhealthy: Connected={_isConnected}, TimeSinceLastRead={timeSinceLastRead.TotalMilliseconds:F0}ms");
            }
            return isHealthy;
        }

        private async Task ReconnectAsync()
        {
            await _connectionLock.WaitAsync();
            try
            {
                if (_isConnected)
                    return;

                _logger.Info("Attempting to reconnect to PLC...");
                await InitializeTagAsync();
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        public async Task<bool> ReadTagValue()
        {
            _currentRetryCount = 0;
            while (_currentRetryCount < _config.MaxRetries)
            {
                try
                {
                    if (!_isConnected)
                    {
                        await ReconnectAsync();
                        if (!_isConnected)
                        {
                            throw new InvalidOperationException("Failed to reconnect to PLC");
                        }
                    }

                    await _tag.ReadAsync();
                    var status = _tag.GetStatus();
                    if (status != Status.Ok)
                    {
                        throw new InvalidOperationException($"Failed to read tag: {status}");
                    }

                    var value = _tag.GetInt8(0) != 0;
                    _lastSuccessfulRead = DateTime.UtcNow;
                    TagValueChanged?.Invoke(this, value);
                    return value;
                }
                catch (Exception ex)
                {
                    _currentRetryCount++;
                    UpdateConnectionStatus(false);

                    if (_currentRetryCount >= _config.MaxRetries)
                    {
                        _logger.Error(ex, "Failed to read PLC tag after all retries");
                        throw;
                    }

                    // Use operational retry delays, not initialization delays
                    var delay = Math.Min(
                        _config.OperationalInitialRetryDelayMs * Math.Pow(2, _currentRetryCount - 1),
                        _config.OperationalMaxRetryDelayMs
                    );
                    _logger.Warn($"PLC read attempt {_currentRetryCount} failed. Retrying in {delay}ms");
                    await Task.Delay((int)delay);
                }
            }

            throw new InvalidOperationException("Failed to read PLC tag after exhausting all retries");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _tag?.Dispose();
                    _connectionLock.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
