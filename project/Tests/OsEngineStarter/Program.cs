using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace OsEngineStarter
{
    internal class Program
    {
        static int Main(string[] args)
        {
            string baseDirectory = NormalizeDirectoryPath(AppDomain.CurrentDomain.BaseDirectory);
            string osEnginePath = Path.Combine(baseDirectory, "OsEngine.exe");

            if (!File.Exists(osEnginePath))
            {
                Console.WriteLine($"Error: OsEngine.exe not found in {baseDirectory}");
                return 1;
            }

            string processName = Path.GetFileNameWithoutExtension(osEnginePath);

            bool alreadyRunning = Process.GetProcessesByName(processName)
                .Any(p =>
                {
                    try
                    {
                        string? mainModule = p.MainModule?.FileName;
                        if (string.IsNullOrWhiteSpace(mainModule))
                            return false;

                        string processDirectory = NormalizeDirectoryPath(Path.GetDirectoryName(mainModule)!);
                        return string.Equals(processDirectory, baseDirectory, StringComparison.OrdinalIgnoreCase);
                    }
                    catch
                    {
                        return false;
                    }
                });

            if (alreadyRunning)
            {
                Console.WriteLine($"OsEngine is already running from {baseDirectory}");
                return 0;
            }

            string arguments = string.Join(" ", args);

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = osEnginePath,
                WorkingDirectory = baseDirectory,
                Arguments = arguments,
                UseShellExecute = true,
                CreateNoWindow = false
            };

            Process.Start(startInfo);

            Console.WriteLine($"OsEngine started from {osEnginePath}");
            return 0;
        }

        private static string NormalizeDirectoryPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}
