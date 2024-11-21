using System;
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
        private int _currentRetryCount = 0;

        public event EventHandler<bool> TagValueChanged;

        public PlcService(PlcConfig config)
        {
            _config = config;
            InitializeTag();
        }

        private void InitializeTag()
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
        }

        public async Task<bool> ReadTagValue()
        {
            _currentRetryCount = 0;
            while (_currentRetryCount < _config.MaxRetries)
            {
                try
                {
                    _logger.Debug($"Attempting to read PLC tag (Attempt {_currentRetryCount + 1}/{_config.MaxRetries})");
                    await Task.Run(() => _tag.ReadAsync());
                    var tagValue = _tag.GetBit(0);
                    _logger.Debug($"Successfully read PLC tag value: {tagValue}");
                    _currentRetryCount = 0; // Reset retry count on successful read
                    TagValueChanged?.Invoke(this, tagValue);
                    return tagValue;
                }
                catch (Exception ex)
                {
                    _currentRetryCount++;
                    _logger.Error(ex, $"Error reading PLC tag (Attempt {_currentRetryCount}/{_config.MaxRetries})");
                    
                    if (_currentRetryCount >= _config.MaxRetries)
                    {
                        _logger.Error($"Maximum retry attempts ({_config.MaxRetries}) reached. Giving up.");
                        throw new Exception($"Failed to read PLC tag after {_config.MaxRetries} attempts", ex);
                    }
                    
                    // Calculate delay with exponential backoff, starting from InitialRetryDelayMs
                    int delayMs = Math.Min(
                        _config.InitialRetryDelayMs * (int)Math.Pow(2, _currentRetryCount - 1), 
                        _config.MaxRetryDelayMs
                    );
                    _logger.Debug($"Waiting {delayMs}ms before retry attempt {_currentRetryCount + 1}");
                    await Task.Delay(delayMs);
                }
            }
            
            // This should never be reached due to throw in catch block
            return false;
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
                }
                _disposed = true;
            }
        }
    }
}
