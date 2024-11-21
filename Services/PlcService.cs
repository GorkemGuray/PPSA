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
                ConnectionStatusChanged?.Invoke(this, isConnected);
            }
        }

        public bool IsHealthy()
        {
            // Consider the service unhealthy if we haven't had a successful read in twice the read interval
            var healthyTimeWindow = TimeSpan.FromMilliseconds(_config.ReadInterval * 2);
            return _isConnected && (DateTime.UtcNow - _lastSuccessfulRead) <= healthyTimeWindow;
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

                    var delay = Math.Min(
                        _config.InitialRetryDelayMs * Math.Pow(2, _currentRetryCount - 1),
                        _config.MaxRetryDelayMs
                    );
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
