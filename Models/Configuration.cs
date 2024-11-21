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
            Plc = new PlcConfig();
            Program = new ProgramConfig();
            Folder = new FolderConfig();
        }
    }

    public class PlcConfig
    {
        public string TagName { get; private set; }
        public string Gateway { get; private set; }
        public string Path { get; private set; }
        public int ReadInterval { get; private set; }
        public int Timeout { get; private set; }
        public int MaxRetries { get; private set; }
        public int InitialRetryDelayMs { get; private set; }
        public int MaxRetryDelayMs { get; private set; }
        // Initialize retry settings
        public int InitializeMaxRetries { get; private set; }
        public int InitializeInitialDelayMs { get; private set; }
        public int InitializeMaxDelayMs { get; private set; }
        public int InitializeTotalTimeoutMs { get; private set; }

        public PlcConfig()
        {
            TagName = ConfigurationManager.AppSettings["PlcTagName"];
            Gateway = ConfigurationManager.AppSettings["PlcGateway"];
            Path = ConfigurationManager.AppSettings["PlcPath"];
            ReadInterval = int.Parse(ConfigurationManager.AppSettings["PlcReadInterval"]);
            Timeout = int.Parse(ConfigurationManager.AppSettings["PlcTimeout"]);
            MaxRetries = int.Parse(ConfigurationManager.AppSettings["PlcMaxRetries"]);
            InitialRetryDelayMs = int.Parse(ConfigurationManager.AppSettings["PlcInitialRetryDelayMs"]);
            MaxRetryDelayMs = int.Parse(ConfigurationManager.AppSettings["PlcMaxRetryDelayMs"]);
            // Initialize retry settings
            InitializeMaxRetries = int.Parse(ConfigurationManager.AppSettings["PlcInitializeMaxRetries"]);
            InitializeInitialDelayMs = int.Parse(ConfigurationManager.AppSettings["PlcInitializeInitialDelayMs"]);
            InitializeMaxDelayMs = int.Parse(ConfigurationManager.AppSettings["PlcInitializeMaxDelayMs"]);
            InitializeTotalTimeoutMs = int.Parse(ConfigurationManager.AppSettings["PlcInitializeTotalTimeoutMs"]);
        }
    }

    public class ProgramConfig
    {
        public string ProgramName { get; }
        public string ProcessToMonitor { get; }
        public int ProcessTerminationTimeout { get; }
        public int ShutdownGracePeriod { get; }
        public int ProcessCloseTimeout { get; }

        public ProgramConfig()
        {
            ProgramName = ConfigurationManager.AppSettings["ProgramToClose"];
            ProcessToMonitor = ConfigurationManager.AppSettings["ProcessToMonitor"];
            ProcessTerminationTimeout = int.Parse(ConfigurationManager.AppSettings["ProcessTerminationTimeout"]);
            ShutdownGracePeriod = int.Parse(ConfigurationManager.AppSettings["ShutdownGracePeriod"]);
            ProcessCloseTimeout = int.Parse(ConfigurationManager.AppSettings["ProcessCloseTimeout"]);
        }
    }

    public class FolderConfig
    {
        public int MaxFolderCount { get; }
        public int DaysThreshold { get; }
        public string[] FolderPaths { get; }

        public FolderConfig()
        {
            MaxFolderCount = int.Parse(ConfigurationManager.AppSettings["MaxFolderCount"]);
            DaysThreshold = int.Parse(ConfigurationManager.AppSettings["DaysThreshold"]);
            FolderPaths = ConfigurationManager.AppSettings["FolderPaths"].Split(';');
        }
    }
}
