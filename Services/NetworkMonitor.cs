using System;
using System.Net.NetworkInformation;
using System.Linq;
using NLog;
using System.Collections.Generic;

namespace PPSA.Services
{
    public class NetworkMonitor : IDisposable
    {
        private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
        private bool _disposed = false;
        private readonly Dictionary<string, NetworkStatus> _interfaceStatuses = new Dictionary<string, NetworkStatus>();
        private NetworkAddressChangedEventHandler _addressChangedHandler;
        private NetworkAvailabilityChangedEventHandler _availabilityChangedHandler;

        private class NetworkStatus
        {
            public bool IsConnected { get; set; }
            public DateTime LastStatusChange { get; set; }
            public string Type { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
        }

        public NetworkMonitor()
        {
            _addressChangedHandler = new NetworkAddressChangedEventHandler(OnNetworkAddressChanged);
            _availabilityChangedHandler = new NetworkAvailabilityChangedEventHandler(OnNetworkAvailabilityChanged);

            NetworkChange.NetworkAddressChanged += _addressChangedHandler;
            NetworkChange.NetworkAvailabilityChanged += _availabilityChangedHandler;

            // Initial check of all network interfaces
            CheckAllNetworkInterfaces();
        }

        private void OnNetworkAddressChanged(object sender, EventArgs e)
        {
            CheckAllNetworkInterfaces();
        }

        private void OnNetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            _logger.Info($"Overall network availability changed. Network is {(e.IsAvailable ? "available" : "unavailable")}");
            CheckAllNetworkInterfaces();
        }

        private void CheckAllNetworkInterfaces()
        {
            if (_disposed) return;

            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                               (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                                ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet));

                foreach (var ni in interfaces)
                {
                    var type = ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? "WiFi" : "Wired";
                    var key = ni.Id;

                    if (!_interfaceStatuses.ContainsKey(key))
                    {
                        // New interface detected
                        _interfaceStatuses[key] = new NetworkStatus
                        {
                            IsConnected = true,
                            LastStatusChange = DateTime.Now,
                            Type = type,
                            Name = ni.Name,
                            Description = ni.Description
                        };

                        _logger.Info($"New {type} connection detected - {ni.Name} ({ni.Description}) at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    }
                }

                // Check for disconnected interfaces
                var currentIds = interfaces.Select(ni => ni.Id).ToList();
                var disconnectedInterfaces = _interfaceStatuses.Keys
                    .Where(k => !currentIds.Contains(k))
                    .ToList();

                foreach (var key in disconnectedInterfaces)
                {
                    var status = _interfaceStatuses[key];
                    if (status.IsConnected)
                    {
                        status.IsConnected = false;
                        status.LastStatusChange = DateTime.Now;
                        _logger.Warn($"{status.Type} connection lost - {status.Name} ({status.Description}) at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    }
                }

                // Log current active connections
                var activeConnections = _interfaceStatuses
                    .Where(kvp => kvp.Value.IsConnected)
                    .Select(kvp => kvp.Value);

                if (!activeConnections.Any())
                {
                    _logger.Warn("No active network connections found!");
                }
                else
                {
                    foreach (var conn in activeConnections)
                    {
                        _logger.Debug($"Active {conn.Type} connection: {conn.Name} (Connected since {conn.LastStatusChange:yyyy-MM-dd HH:mm:ss})");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error checking network interfaces");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_addressChangedHandler != null)
                    NetworkChange.NetworkAddressChanged -= _addressChangedHandler;
                if (_availabilityChangedHandler != null)
                    NetworkChange.NetworkAvailabilityChanged -= _availabilityChangedHandler;

                _disposed = true;
                _logger.Debug("Network monitor disposed");
            }
        }
    }
}