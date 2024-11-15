using libplctag;
using libplctag.DataTypes.Simple;
using libplctag.NativeImport;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PPSA
{
    internal class Program
    {

        private static Tag tag;
        private static bool tagValue;
        private static FolderCleaner folderCleaner;
        private static TaskCompletionSource<bool> folderCleanupCompletionSource;

        static async Task Main(string[] args)
        {
            // PLC connection parameters
            tag = new Tag
            {
                Name = "PC_KAPAT_V7",
                Gateway = "192.168.250.1",
                Path = "1,0",
                PlcType = PlcType.Omron,
                Protocol = Protocol.ab_eip,
            };

            // FolderCleaner settings
            int maxFolderCount = 15;
            int daysThreshold = 15;
            string[] folderPaths =
            {
                @"C:\OMRON\Soft-NA\Storage\SDCard\OperationLog",
                @"C:\OMRON\Soft-NA\Storage\SDCard\Data Logging\Log Files\DataSet0",
                @"C:\OMRON\Soft-NA\Storage\SDCard\Data Logging\Log Files\DataSet1",
                @"C:\OMRON\Soft-NA\Storage\SDCard\Data Logging\Log Files\DataSet2"
            };
            folderCleaner = new FolderCleaner(maxFolderCount, daysThreshold);
            folderCleaner.CleanupCompleted += FolderCleaner_CleanupCompleted;

            // Start timer to control PLC variable
            // The reading time from the PLC can be changed here.
            Timer timer = new Timer(CheckPLCVariable, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
            AppDomain.CurrentDomain.ProcessExit += (s, e) => timer.Dispose();

            // Main program loop
            while (true)
            {
                if (tagValue)
                {
                    await CloseProgram("Soft-NA");
                    folderCleanupCompletionSource = new TaskCompletionSource<bool>();
                    await folderCleaner.CleanFoldersAsync(folderPaths);
                    await folderCleanupCompletionSource.Task;  // Wait until folder cleanup is complete
                    ShutdownComputer();
                }

                await Task.Delay(Timeout.Infinite); // Endless waiting
            }
        }

        static void CheckPLCVariable(object state)
        {
            // Read PLC variable asynchronously
            ReadPLCVariableAsync(tag);
        }

        static async void ReadPLCVariableAsync(Tag tagID)
        {
            try
            {
                // Read PLC variable asynchronously
                bool value = true;
                await tagID.ReadAsync();
                bool result = tagID.GetBit(0);
                Console.WriteLine($"Tag Value: {result}");

                // If there has been a change, update the value and start the process
                if (!value && !tagValue)
                {
                    tagValue = true;
                }
                else if (value && tagValue)
                {
                    tagValue = false;
                }
            } catch (Exception ex)
            {
                Console.WriteLine($"Error reading PLC: {ex.Message}");
                // TODO: ERROR IN PLC COMMUNICATION CLOSE PROGRAM DELETE FOLDERS AND SHUT DOWN COMPUTER.
            }

        }

        static async Task CloseProgram(string programName)
        {
            Process[] processes = Process.GetProcessesByName(programName);
            if (processes.Length >= 1)
            {
                Console.WriteLine($"{programName} is being close.");
                // Close all windows of the program
                foreach (Process process in processes)
                {
                    process.CloseMainWindow();
                    process.WaitForExit();
                }
            } else
            {
                Console.WriteLine("The program is not open, the shutdown is in progress.");
                // TODO: notify main task as completed.
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

    }

    
}
