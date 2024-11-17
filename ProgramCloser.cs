using System;
using System.Diagnostics;


namespace PPSA
{
    internal class ProgramCloser
    {
        public event EventHandler<String> ClosingCompleted;

        public void CloseProgram(string programName)
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
            }
            else
            {
                Console.WriteLine("The program is not open, the shutdown is in progress.");
                // TODO: notify main task as completed.
            }

            OnClosingCompleted("The closing is over");
        }

        protected virtual void OnClosingCompleted(string message)
        {
            ClosingCompleted?.Invoke(this, message);
        }
    }
}
