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
            try
            {
                await Task.Run(() => _tag.ReadAsync());
                var tagValue = _tag.GetBit(0);
                _logger.Debug($"Read PLC tag value: {tagValue}");
                TagValueChanged?.Invoke(this, tagValue);
                return tagValue;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error reading PLC tag");
                throw;
            }
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
