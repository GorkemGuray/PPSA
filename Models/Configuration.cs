using System;
using System.Configuration;
using System.Linq;

namespace PPSA.Models
{
    public class Configuration
    {
        public PlcConfig Plc { get; }
        public ProgramConfig Program { get; }
        public FolderConfig Folder { get; }

        public Configuration()
        {
            try
            {
                Plc = new PlcConfig();
                Program = new ProgramConfig();
                Folder = new FolderConfig();

                var validator = new ConfigurationValidator();
                var (isValid, errors) = validator.ValidateConfiguration(this);
                
                if (!isValid)
                {
                    throw new ConfigurationException($"Configuration validation failed: {string.Join(Environment.NewLine, errors)}");
                }
            }
            catch (Exception ex) when (!(ex is ConfigurationException))
            {
                throw new ConfigurationException("Failed to load configuration", ex);
            }
        }

        public static T GetConfigValue<T>(string key, T defaultValue)
        {
            try
            {
                var value = ConfigurationManager.AppSettings[key];
                if (string.IsNullOrEmpty(value))
                    return defaultValue;

                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }
    }

    public class ConfigurationException : Exception
    {
        public ConfigurationException(string message) : base(message) { }
        public ConfigurationException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class PlcConfig
    {
        // Default values - Initialization
        private const int DEFAULT_INITIALIZE_MAX_RETRIES = 4;          // From App.config
        private const int DEFAULT_INITIALIZE_INITIAL_DELAY = 1500;     // From App.config
        private const int DEFAULT_INITIALIZE_MAX_DELAY = 3000;         // From App.config
        private const int DEFAULT_INITIALIZE_TOTAL_TIMEOUT = 20000;    // From App.config

        // Default values - Operation
        private const int DEFAULT_READ_INTERVAL = 1000;                // From App.config
        private const int DEFAULT_TIMEOUT = 2000;                      // From App.config
        private const int DEFAULT_MAX_RETRIES = 3;                     // From App.config
        private const int DEFAULT_OPERATIONAL_INITIAL_RETRY_DELAY = 1000; // From App.config
        private const int DEFAULT_OPERATIONAL_MAX_RETRY_DELAY = 2000;    // From App.config

        public string TagName { get; }
        public string Gateway { get; }
        public string Path { get; }
        public int ReadInterval { get; }
        public int Timeout { get; }

        // Initialization properties
        public int InitializeMaxRetries { get; }
        public int InitializeInitialDelayMs { get; }
        public int InitializeMaxDelayMs { get; }
        public int InitializeTotalTimeoutMs { get; }

        // Operational properties
        public int MaxRetries { get; }
        public int OperationalInitialRetryDelayMs { get; }
        public int OperationalMaxRetryDelayMs { get; }

        public PlcConfig()
        {
            // Basic settings
            TagName = Configuration.GetConfigValue("PlcTagName", string.Empty);
            Gateway = Configuration.GetConfigValue("PlcGateway", string.Empty);
            Path = Configuration.GetConfigValue("PlcPath", string.Empty);
            ReadInterval = Configuration.GetConfigValue("PlcReadInterval", DEFAULT_READ_INTERVAL);
            Timeout = Configuration.GetConfigValue("PlcTimeout", DEFAULT_TIMEOUT);

            // Initialization settings
            InitializeMaxRetries = Configuration.GetConfigValue("PlcInitializeMaxRetries", DEFAULT_INITIALIZE_MAX_RETRIES);
            InitializeInitialDelayMs = Configuration.GetConfigValue("PlcInitializeInitialDelayMs", DEFAULT_INITIALIZE_INITIAL_DELAY);
            InitializeMaxDelayMs = Configuration.GetConfigValue("PlcInitializeMaxDelayMs", DEFAULT_INITIALIZE_MAX_DELAY);
            InitializeTotalTimeoutMs = Configuration.GetConfigValue("PlcInitializeTotalTimeoutMs", DEFAULT_INITIALIZE_TOTAL_TIMEOUT);

            // Operational settings
            MaxRetries = Configuration.GetConfigValue("PlcMaxRetries", DEFAULT_MAX_RETRIES);
            OperationalInitialRetryDelayMs = Configuration.GetConfigValue("PlcInitialRetryDelayMs", DEFAULT_OPERATIONAL_INITIAL_RETRY_DELAY);
            OperationalMaxRetryDelayMs = Configuration.GetConfigValue("PlcMaxRetryDelayMs", DEFAULT_OPERATIONAL_MAX_RETRY_DELAY);
        }
    }

    public class ProgramConfig
    {
        // Default values
        private const int DEFAULT_PROCESS_TERMINATION_TIMEOUT = 15;   // Updated from 300 to 15
        private const double DEFAULT_SHUTDOWN_GRACE_PERIOD = 0.033;   // Updated from 60 to 0.033
        private const int DEFAULT_PROCESS_CLOSE_TIMEOUT = 3000;       // Updated from 30 to 3000

        public string ProgramName { get; }
        public string ProcessToMonitor { get; }
        public int ProcessTerminationTimeout { get; }
        public double ShutdownGracePeriod { get; }
        public int ProcessCloseTimeout { get; }

        public ProgramConfig()
        {
            ProgramName = Configuration.GetConfigValue("ProgramToClose", string.Empty);
            ProcessToMonitor = Configuration.GetConfigValue("ProcessToMonitor", string.Empty);
            ProcessTerminationTimeout = Configuration.GetConfigValue("ProcessTerminationTimeout", DEFAULT_PROCESS_TERMINATION_TIMEOUT);
            ShutdownGracePeriod = Configuration.GetConfigValue("ShutdownGracePeriod", DEFAULT_SHUTDOWN_GRACE_PERIOD);
            ProcessCloseTimeout = Configuration.GetConfigValue("ProcessCloseTimeout", DEFAULT_PROCESS_CLOSE_TIMEOUT);
        }
    }

    public class FolderConfig
    {
        // Default values
        private const int DEFAULT_MAX_FOLDER_COUNT = 15;  // Updated from 10 to 15
        private const int DEFAULT_DAYS_THRESHOLD = 15;    // Updated from 30 to 15

        public int MaxFolderCount { get; }
        public int DaysThreshold { get; }
        public string[] FolderPaths { get; }

        public FolderConfig()
        {
            MaxFolderCount = Configuration.GetConfigValue("MaxFolderCount", DEFAULT_MAX_FOLDER_COUNT);
            DaysThreshold = Configuration.GetConfigValue("DaysThreshold", DEFAULT_DAYS_THRESHOLD);
            FolderPaths = Configuration.GetConfigValue("FolderPaths", string.Empty).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
