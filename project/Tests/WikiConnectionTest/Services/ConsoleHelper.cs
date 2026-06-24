/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple.pdf
*/

using System.Runtime.InteropServices;

namespace WikiConnectionTest.Services
{
    /// <summary>
    /// Detects whether the application was launched directly by a user
    /// (e.g. double-click in Explorer) and waits for a key press before exit
    /// so the console window does not close immediately.
    /// </summary>
    public static class ConsoleHelper
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint GetConsoleProcessList(uint[] processList, uint processCount);

        /// <summary>
        /// Returns true when the user likely launched the executable directly
        /// (only one process is attached to the console).
        /// </summary>
        public static bool IsRunByUser()
        {
            try
            {
                if (Console.IsInputRedirected || Console.IsOutputRedirected)
                {
                    return false;
                }

                uint[] processes = new uint[1];
                uint count = GetConsoleProcessList(processes, 1);

                // If only our process is attached to the console, the user likely
                // launched the executable directly from Explorer.
                return count <= 1;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Waits for a key press when the application was launched directly by the user.
        /// </summary>
        public static void WaitIfRunByUser()
        {
            if (!IsRunByUser())
            {
                return;
            }

            try
            {
                Console.WriteLine();
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey(true);
            }
            catch
            {
                // ignore
            }
        }
    }
}
