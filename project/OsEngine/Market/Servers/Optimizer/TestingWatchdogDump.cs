/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 */

using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using OsEngine.Market.Servers.Tester;

namespace OsEngine.Market.Servers.Optimizer
{
    /// <summary>
    /// Логика watchdog зависшего прогона оптимизатора (FEATURE-0089).
    /// Предикат чистый, часы инжектируемые — юнит-тесты не ждут реальные секунды.
    /// </summary>
    public static class TestingWatchdog
    {
        /// <summary>
        /// Часы watchdog. Тесты подменяют их детерминированными; прод-код не трогает.
        /// </summary>
        public static Func<DateTime> NowProvider { get; set; } = delegate { return DateTime.UtcNow; };

        public static DateTime UtcNowProvider()
        {
            // null-fallback: heartbeat и диагностику не должен ронять сброшенный провайдер часов
            Func<DateTime> provider = NowProvider;

            try
            {
                return provider != null ? provider() : DateTime.UtcNow;
            }
            catch (Exception)
            {
                return DateTime.UtcNow;
            }
        }

        private static int _firedCount;

        /// <summary>
        /// Срабатываний watchdog за сессию процесса; попадает в диагностический лог (P8).
        /// </summary>
        public static int FiredCount
        {
            get { return Volatile.Read(ref _firedCount); }
        }

        public static int RegisterFire()
        {
            return Interlocked.Increment(ref _firedCount);
        }

        /// <summary>
        /// Пора ли снимать прогон: Play, прогон не завершён, сервер не снят,
        /// watchdog не выключен и бездействие Play-потока не меньше N.
        /// Детектор по бездействию, а не по TimeNow &gt;= TimeEnd: зависший колбэк
        /// морозит TimeNow ниже конца данных (см. ТЗ FEATURE-0089).
        /// </summary>
        public static bool ShouldFire(TesterRegime regime, DateTime lastProgressUtc,
            DateTime nowUtc, int timeoutSeconds, bool eventRaised, bool cleared)
        {
            if (timeoutSeconds <= 0)
            {
                return false;
            }

            if (regime != TesterRegime.Play)
            {
                return false;
            }

            if (eventRaised || cleared)
            {
                return false;
            }

            return (nowUtc - lastProgressUtc).TotalSeconds >= timeoutSeconds;
        }
    }

    /// <summary>
    /// Диагностика срабатывания watchdog: точная Error-строка и минидамп стеков в Engine\Log.
    /// </summary>
    public static class TestingWatchdogDump
    {
        /// <summary>
        /// Флаги MINIDUMP_TYPE по документации MiniDumpWriteDump (dbghelp.h).
        /// </summary>
        [Flags]
        public enum MINIDUMP_TYPE
        {
            MiniDumpNormal = 0x00000000,
            MiniDumpWithDataSegs = 0x00000001,
            MiniDumpWithFullMemory = 0x00000002,
            MiniDumpWithHandleData = 0x00000004,
            MiniDumpFilterMemory = 0x00000008,
            MiniDumpScanMemory = 0x00000010,
            MiniDumpWithUnloadedModules = 0x00000020,
            MiniDumpWithIndirectlyReferencedMemory = 0x00000040,
            MiniDumpFilterModulePaths = 0x00000080,
            MiniDumpWithProcessThreadData = 0x00000100,
            MiniDumpWithPrivateReadWriteMemory = 0x00000200,
            MiniDumpWithoutOptionalData = 0x00000400,
            MiniDumpWithFullMemoryInfo = 0x00000800,
            MiniDumpWithThreadInfo = 0x00001000,
            MiniDumpWithCodeSegs = 0x00002000
        }

        /// <summary>
        /// Потоки с контекстами и стеками плюс косвенная память; полной памяти процесса нет
        /// (R1: вместо «WithoutMemory», которого в dbghelp нет).
        /// </summary>
        public const MINIDUMP_TYPE DumpFlags =
            MINIDUMP_TYPE.MiniDumpWithThreadInfo
            | MINIDUMP_TYPE.MiniDumpWithIndirectlyReferencedMemory;

        /// <summary>
        /// Имя файла дампа по часам фичи; формат фиксируют юнит-тесты.
        /// </summary>
        public static string DumpFileName(int numberServer, DateTime now)
        {
            return "Watchdog_"
                + now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)
                + "_srv" + numberServer + ".dmp";
        }

        /// <summary>
        /// Error-строка состояния зависшего прогона. Точный формат владеют тесты (AC8).
        /// threads = Process.Threads.Count / ThreadPool.ThreadCount: публичного
        /// счётчика managed-потоков в .NET нет, пишем оба доступных.
        /// </summary>
        public static string BuildDiagnostic(int numberServer, TesterRegime regime,
            DateTime timeNow, DateTime timeEnd, double idleSeconds, TimeSpan testDuration,
            int processThreadsCount, int threadPoolThreadCount, int firedCount)
        {
            return "TestingWatchdog srv=" + numberServer
                + " regime=" + regime.ToString()
                + " timeNow=" + timeNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                + " timeEnd=" + timeEnd.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                + " idle=" + (long)idleSeconds + "s"
                + " duration=" + (long)testDuration.TotalSeconds + "s"
                + " threads=" + processThreadsCount + "/" + threadPoolThreadCount
                + " fired=" + firedCount;
        }

        /// <summary>
        /// Код Win32 последнего сбоя дампа (0 — сбоев не было); пишется в Error-лог.
        /// </summary>
        public static int LastWin32Error { get; private set; }

        /// <summary>
        /// Минидамп текущего процесса в Engine\Log. Возвращает путь либо null при сбое;
        /// не бросает — сбой дампа не должен отменять принудительное завершение (AC8).
        /// </summary>
        public static string TryWriteDump(int numberServer)
        {
            try
            {
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    return null;
                }

                string directory = @"Engine\Log";

                if (Directory.Exists(directory) == false)
                {
                    Directory.CreateDirectory(directory);
                }

                DateTime now;

                try
                {
                    now = TestingWatchdog.UtcNowProvider();
                }
                catch (Exception)
                {
                    // часы подменены и бросают — имя файла не должно ронять дамп
                    now = DateTime.UtcNow;
                }

                string path = Path.Combine(directory, DumpFileName(numberServer, now));

                return WriteMiniDump(path) ? path : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Обнуление «последней ошибки Win32» перед попыткой дампа: GetLastWin32Error иначе
        // вернёт мусор чужого P/Invoke на том же потоке пула
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern void SetLastError(uint dwErr);

        private static bool WriteMiniDump(string path)
        {
            LastWin32Error = 0;
            SetLastError(0);

            IntPtr file = CreateFile(path, GenericWrite, ShareAll, IntPtr.Zero,
                CreateAlways, FileAttributeNormal, IntPtr.Zero);

            if (file == InvalidHandle)
            {
                LastWin32Error = Marshal.GetLastWin32Error();
                return false;
            }

            try
            {
                bool result = MiniDumpWriteDump(GetCurrentProcess(), GetCurrentProcessId(), file,
                    (uint)DumpFlags, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

                if (result == false)
                {
                    LastWin32Error = Marshal.GetLastWin32Error();
                }

                return result;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                CloseHandle(file);
            }
        }

        private const uint GenericWrite = 0x40000000;
        private const uint ShareAll = 0x00000001 | 0x00000002 | 0x00000004;
        private const uint CreateAlways = 2;
        private const uint FileAttributeNormal = 0x00000080;

        private static readonly IntPtr InvalidHandle = new IntPtr(-1);

        [DllImport("dbghelp.dll", SetLastError = true)]
        private static extern bool MiniDumpWriteDump(IntPtr hProcess, uint processId,
            IntPtr hFile, uint dumpType, IntPtr exceptionParam, IntPtr userStreamParam,
            IntPtr callbackParam);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess,
            uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition,
            uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentProcessId();
    }
}
