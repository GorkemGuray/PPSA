using System;
using System.Collections.Generic;
using NLog;

namespace PPSA.Models
{
    public class ConfigurationValidator
    {
        private readonly ILogger _logger = LogManager.GetCurrentClassLogger();

        public (bool IsValid, List<string>) ValidateConfiguration(Configuration config)
        {
            var errors = new List<string>();
            
            ValidatePlcConfig(config.Plc, errors);
            ValidateProgramConfig(config.Program, errors);
            ValidateFolderConfig(config.Folder, errors);

            return (errors.Count == 0, errors);
        }

        private void ValidatePlcConfig(PlcConfig config, List<string> errors)
        {
            if (string.IsNullOrEmpty(config.TagName))
                errors.Add("PLC TagName is required");
            if (string.IsNullOrEmpty(config.Gateway))
                errors.Add("PLC Gateway is required");
            if (string.IsNullOrEmpty(config.Path))
                errors.Add("PLC Path is required");
            if (config.ReadInterval <= 0)
                errors.Add("PLC ReadInterval must be greater than 0");
            if (config.Timeout <= 0)
                errors.Add("PLC Timeout must be greater than 0");
            if (config.MaxRetries <= 0)
                errors.Add("PLC MaxRetries must be greater than 0");
            if (config.InitialRetryDelayMs <= 0)
                errors.Add("PLC InitialRetryDelayMs must be greater than 0");
            if (config.MaxRetryDelayMs <= config.InitialRetryDelayMs)
                errors.Add("PLC MaxRetryDelayMs must be greater than InitialRetryDelayMs");
        }

        private void ValidateProgramConfig(ProgramConfig config, List<string> errors)
        {
            if (string.IsNullOrEmpty(config.ProgramName))
                errors.Add("Program Name is required");
            if (string.IsNullOrEmpty(config.ProcessToMonitor))
                errors.Add("ProcessToMonitor is required");
            if (config.ProcessTerminationTimeout <= 0)
                errors.Add("ProcessTerminationTimeout must be greater than 0");
            if (config.ShutdownGracePeriod <= 0)
                errors.Add("ShutdownGracePeriod must be greater than 0");
            if (config.ProcessCloseTimeout <= 0)
                errors.Add("ProcessCloseTimeout must be greater than 0");
        }

        private void ValidateFolderConfig(FolderConfig config, List<string> errors)
        {
            if (config.MaxFolderCount <= 0)
                errors.Add("MaxFolderCount must be greater than 0");
            if (config.DaysThreshold <= 0)
                errors.Add("DaysThreshold must be greater than 0");
            if (config.FolderPaths == null || config.FolderPaths.Length == 0)
                errors.Add("At least one folder path must be specified");
            else
            {
                foreach (var path in config.FolderPaths)
                {
                    if (string.IsNullOrEmpty(path))
                        errors.Add("Folder path cannot be empty");
                }
            }
        }
    }
}
