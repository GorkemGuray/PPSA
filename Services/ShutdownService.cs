using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Security.Principal;
using NLog;
using PPSA.Models;

namespace PPSA.Services
{
    public class ShutdownService
    {
        private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
        private readonly ProgramConfig _config;
        private readonly object _shutdownLock = new object();
        private bool _isShutdownInitiated;

        public event EventHandler ShutdownInitiated;
        public event EventHandler<Exception> ShutdownError;

        public ShutdownService(ProgramConfig config)
        {
            _config = config;
            ValidateAdministratorAccess();
        }

        private void ValidateAdministratorAccess()
        {
            try
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var principal = new WindowsPrincipal(identity);
                    if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
                    {
                        throw new UnauthorizedAccessException("Application requires administrator privileges to perform shutdown operations.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to validate administrator access");
                throw;
            }
        }

        public async Task InitiateShutdown()
        {
            lock (_shutdownLock)
            {
                if (_isShutdownInitiated)
                {
                    _logger.Warn("Shutdown already initiated");
                    return;
                }
                _isShutdownInitiated = true;
            }

            try
            {
                _logger.Info($"Initiating system shutdown with {_config.ShutdownGracePeriod} seconds grace period");
                OnShutdownInitiated();

                var shutdownMessage = $"System shutdown initiated by PPSA at {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                
                await Task.Run(() =>
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "shutdown",
                        Arguments = $"/s /t {_config.ShutdownGracePeriod} /c \"{shutdownMessage}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using (var process = Process.Start(startInfo))
                    {
                        if (process != null)
                        {
                            var output = process.StandardOutput.ReadToEnd();
                            var error = process.StandardError.ReadToEnd();

                            if (!string.IsNullOrEmpty(output))
                            {
                                _logger.Info($"Shutdown command output: {output}");
                            }

                            if (!string.IsNullOrEmpty(error))
                            {
                                _logger.Error($"Shutdown command error: {error}");
                            }

                            process.WaitForExit();
                            if (process.ExitCode != 0)
                            {
                                throw new Exception($"Shutdown command failed with exit code: {process.ExitCode}");
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to initiate system shutdown");
                OnShutdownError(ex);
                throw;
            }
        }

        public async Task AbortShutdown()
        {
            try
            {
                _logger.Info("Attempting to abort system shutdown");
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = "shutdown",
                    Arguments = "/a",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                await Task.Run(() =>
                {
                    using (var process = Process.Start(startInfo))
                    {
                        if (process != null)
                        {
                            var output = process.StandardOutput.ReadToEnd();
                            var error = process.StandardError.ReadToEnd();

                            if (!string.IsNullOrEmpty(output))
                            {
                                _logger.Info($"Abort shutdown command output: {output}");
                            }

                            if (!string.IsNullOrEmpty(error))
                            {
                                _logger.Error($"Abort shutdown command error: {error}");
                            }

                            process.WaitForExit();
                            if (process.ExitCode != 0)
                            {
                                throw new Exception($"Abort shutdown command failed with exit code: {process.ExitCode}");
                            }
                        }
                    }
                });

                lock (_shutdownLock)
                {
                    _isShutdownInitiated = false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to abort system shutdown");
                throw;
            }
        }

        protected virtual void OnShutdownInitiated()
        {
            ShutdownInitiated?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnShutdownError(Exception ex)
        {
            ShutdownError?.Invoke(this, ex);
        }
    }
}
