using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace PPSA.Services
{
    public class HealthMonitor : IDisposable
    {
        private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
        private readonly ConcurrentDictionary<string, HealthStatus> _serviceStatuses = new ConcurrentDictionary<string, HealthStatus>();
        private readonly Timer _healthCheckTimer;
        private readonly int _healthCheckInterval;
        private bool _disposed;

        public event EventHandler<HealthStatusEventArgs> HealthStatusChanged;

        public HealthMonitor(int healthCheckIntervalMs = 30000)
        {
            _healthCheckInterval = healthCheckIntervalMs;
            _healthCheckTimer = new Timer(PerformHealthCheck, null, _healthCheckInterval, _healthCheckInterval);
        }

        public void RegisterService(string serviceName, Func<bool> healthCheck)
        {
            var status = new HealthStatus
            {
                ServiceName = serviceName,
                HealthCheck = healthCheck,
                LastCheckTime = DateTime.UtcNow,
                IsHealthy = true
            };

            _serviceStatuses.AddOrUpdate(serviceName, status, (_, __) => status);
        }

        private void PerformHealthCheck(object state)
        {
            foreach (var status in _serviceStatuses.Values)
            {
                try
                {
                    var previousHealth = status.IsHealthy;
                    status.IsHealthy = status.HealthCheck();
                    status.LastCheckTime = DateTime.UtcNow;

                    if (previousHealth != status.IsHealthy)
                    {
                        OnHealthStatusChanged(new HealthStatusEventArgs(status));
                    }

                    // Log memory usage periodically
                    var process = Process.GetCurrentProcess();
                    _logger.Info($"Memory Usage - Working Set: {process.WorkingSet64 / 1024 / 1024}MB, " +
                               $"Private Memory: {process.PrivateMemorySize64 / 1024 / 1024}MB");
                }
                catch (Exception ex)
                {
                    status.IsHealthy = false;
                    _logger.Error(ex, $"Health check failed for service: {status.ServiceName}");
                    OnHealthStatusChanged(new HealthStatusEventArgs(status));
                }
            }
        }

        protected virtual void OnHealthStatusChanged(HealthStatusEventArgs e)
        {
            _logger.Info($"Health status changed for {e.Status.ServiceName}: IsHealthy={e.Status.IsHealthy}");
            HealthStatusChanged?.Invoke(this, e);
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
                    _healthCheckTimer?.Dispose();
                }
                _disposed = true;
            }
        }
    }

    public class HealthStatus
    {
        public string ServiceName { get; set; }
        public Func<bool> HealthCheck { get; set; }
        public DateTime LastCheckTime { get; set; }
        public bool IsHealthy { get; set; }
    }

    public class HealthStatusEventArgs : EventArgs
    {
        public HealthStatus Status { get; }

        public HealthStatusEventArgs(HealthStatus status)
        {
            Status = status;
        }
    }
}
