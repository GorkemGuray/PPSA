using System;
using System.Diagnostics;
using System.Threading.Tasks;
using NLog;
using PPSA.Models;

namespace PPSA.Services
{
    public class ProgramCloser
    {
        private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
        private readonly ProgramConfig _config;

        public event EventHandler<string> ClosingCompleted;

        public ProgramCloser(ProgramConfig config)
        {
            _config = config;
        }

        public async Task<bool> CloseProgramAsync()
        {
            try
            {
                _logger.Info($"Attempting to close {_config.ProgramName}");
                Process[] processes = Process.GetProcessesByName(_config.ProgramName);

                if (processes.Length == 0)
                {
                    _logger.Info($"No instances of {_config.ProgramName} found running");
                    OnClosingCompleted($"No instances of {_config.ProgramName} to close");
                    return true;
                }

                foreach (var process in processes)
                {
                    await CloseProcessAsync(process);
                }

                OnClosingCompleted($"Successfully closed all instances of {_config.ProgramName}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to close {_config.ProgramName}");
                return false;
            }
        }

        private async Task CloseProcessAsync(Process process)
        {
            try
            {
                if (!process.CloseMainWindow())
                {
                    _logger.Warn($"Failed to close window for process {process.Id}, forcing termination");
                    process.Kill();
                }

                var closeTask = Task.Run(() => process.WaitForExit());
                if (await Task.WhenAny(closeTask, Task.Delay(_config.ProcessCloseTimeout)) != closeTask)
                {
                    _logger.Warn($"Process {process.Id} did not exit within timeout, forcing termination");
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error closing process {process.Id}");
                throw;
            }
            finally
            {
                process.Dispose();
            }
        }

        protected virtual void OnClosingCompleted(string message)
        {
            ClosingCompleted?.Invoke(this, message);
        }
    }
}
