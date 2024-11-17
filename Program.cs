using libplctag;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace PPSA
{
    internal class Program
    {
        // PLC data read interval
        private const int INTERVAL = 2000;

        // PLC data read timer
        private static System.Timers.Timer timer = new System.Timers.Timer(INTERVAL);

        // PLC connection parameters
        private static Tag tag = new Tag
        {
            Name = "PC_KAPAT_V7",
            Gateway = "192.168.250.1",
            Path = "1,0",
            PlcType = PlcType.Omron,
            Protocol = Protocol.ab_eip,
            Timeout = TimeSpan.FromMilliseconds(INTERVAL-100)
        };

        private static bool tagValue;

        // FolderCleaner settings
        private static int maxFolderCount = 15;
        private static int daysThreshold = 15;
        private static string[] folderPaths =
        {
                @"C:\OMRON\Soft-NA\Storage\SDCard\OperationLog",
                @"C:\OMRON\Soft-NA\Storage\SDCard\Data Logging\Log Files\DataSet0",
                @"C:\OMRON\Soft-NA\Storage\SDCard\Data Logging\Log Files\DataSet1",
                @"C:\OMRON\Soft-NA\Storage\SDCard\Data Logging\Log Files\DataSet2"
        };

        private static readonly FolderCleaner folderCleaner = new FolderCleaner(maxFolderCount, daysThreshold);
        private static TaskCompletionSource<bool> folderCleanupCompletionSource;

        // ProgramCloser Settings
        private static string programName = "Soft-NA";
        private static readonly ProgramCloser programCloser = new ProgramCloser();
        private static TaskCompletionSource<bool> programCloserCompletionSource;

        static async Task Main(string[] args)
        {
            
            folderCleaner.CleanupCompleted += FolderCleaner_CleanupCompleted;
            programCloser.ClosingCompleted += ProgramCloser_ClosingCompleted;

            timer.Elapsed += ReadPlcTag;
            timer.AutoReset = true;
            timer.Enabled = true;


            // Main program loop
            while (true)
            {
                Console.WriteLine($"Tag değeri: {tagValue}");

                if (tagValue)
                {
                    timer.Stop();
                    timer.Dispose();

                    programCloserCompletionSource = new TaskCompletionSource<bool> ();
                    programCloser.CloseProgram(programName);
                    await programCloserCompletionSource.Task; // Wait until program closing is complete

                    folderCleanupCompletionSource = new TaskCompletionSource<bool>();
                    folderCleaner.CleanFolders(folderPaths);
                    await folderCleanupCompletionSource.Task;  // Wait until folder cleanup is complete

                    ShutdownComputer();
                }

                await Task.Delay(Timeout.Infinite); // Endless waiting
            }
        }

        private static void ReadPlcTag(Object source, ElapsedEventArgs e)
        {
            try
            {
                tag.Read();
                tagValue = tag.GetBit(0);
                Console.WriteLine($"Tag Value: {tagValue}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading PLC: {ex.Message}");
                timer.Stop();
                timer.Dispose();
            }
        }

        static void ShutdownComputer()
        {
            Console.WriteLine("PC shutdown process has begun.");
            /*
            // Shut down the computer after folder cleanup is complete
            Process.Start(new ProcessStartInfo("shutdown", "/s /t 0")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            });
            */
        }

        static void FolderCleaner_CleanupCompleted(object sender, string message)
        {
            Console.WriteLine(message);
            folderCleanupCompletionSource?.TrySetResult(true);
        }

        static void ProgramCloser_ClosingCompleted(object sender, string message)
        {
            Console.WriteLine(message);
            programCloserCompletionSource?.TrySetResult(true);
        }

    }

    
}
