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
            // Basic PLC settings
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

            // Initialization settings
            if (config.InitializeMaxRetries <= 0)
                errors.Add("PLC InitializeMaxRetries must be greater than 0");
            if (config.InitializeInitialDelayMs <= 0)
                errors.Add("PLC InitializeInitialDelayMs must be greater than 0");
            if (config.InitializeMaxDelayMs <= config.InitializeInitialDelayMs)
                errors.Add("PLC InitializeMaxDelayMs must be greater than InitializeInitialDelayMs");
            if (config.InitializeTotalTimeoutMs <= 0)
                errors.Add("PLC InitializeTotalTimeoutMs must be greater than 0");

            // Operational settings
            if (config.MaxRetries <= 0)
                errors.Add("PLC MaxRetries must be greater than 0");
            if (config.OperationalInitialRetryDelayMs <= 0)
                errors.Add("PLC OperationalInitialRetryDelayMs must be greater than 0");
            if (config.OperationalMaxRetryDelayMs <= config.OperationalInitialRetryDelayMs)
                errors.Add("PLC OperationalMaxRetryDelayMs must be greater than OperationalInitialRetryDelayMs");
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
