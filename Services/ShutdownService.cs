using System;
using System.Diagnostics;
using System.Threading.Tasks;
using NLog;
using PPSA.Models;

namespace PPSA.Services
{
    public class ShutdownService
    {
        private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
        private readonly ProgramConfig _config;

        public ShutdownService(ProgramConfig config)
        {
            _config = config;
        }

        public async Task InitiateShutdown()
        {
            try
            {
                _logger.Info($"Initiating system shutdown with {_config.ShutdownGracePeriod} seconds grace period");

                await Task.Run(() =>
                {
                    Process.Start(new ProcessStartInfo("shutdown",
                        $"/s /t {_config.ShutdownGracePeriod} /c \"System shutdown initiated by PPSA\"")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to initiate system shutdown");
                throw;
            }
        }
    }
}
