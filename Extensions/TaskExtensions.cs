using System;
using System.Threading.Tasks;

namespace PPSA.Extensions
{
    public static class TaskExtensions
    {
        public static async Task<T> WithTimeout<T>(this Task<T> task, TimeSpan timeout, string operationName)
        {
            if (await Task.WhenAny(task, Task.Delay(timeout)) != task)
            {
                throw new TimeoutException($"Operation {operationName} timed out after {timeout.TotalSeconds} seconds");
            }

            return await task;
        }
    }
}
