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
        public string TagName { get; }
        public string Gateway { get; }
        public string Path { get; }
        public int ReadInterval { get; }
        public int Timeout { get; }

        public PlcConfig()
        {
            TagName = ConfigurationManager.AppSettings["PlcTagName"];
            Gateway = ConfigurationManager.AppSettings["PlcGateway"];
            Path = ConfigurationManager.AppSettings["PlcPath"];
            ReadInterval = int.Parse(ConfigurationManager.AppSettings["PlcReadInterval"]);
            Timeout = int.Parse(ConfigurationManager.AppSettings["PlcTimeout"]);
        }
    }

    public class ProgramConfig
    {
        public string ProgramName { get; }
        public int ShutdownGracePeriod { get; }
        public int ProcessCloseTimeout { get; }

        public ProgramConfig()
        {
            ProgramName = ConfigurationManager.AppSettings["ProgramToClose"];
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
