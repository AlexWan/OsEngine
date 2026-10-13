/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace OsEngine.Logging
{
    /// <summary>
    /// Страховка от необработанных исключений фазы старта до рождения MainWindow (bug-20261012-005).
    /// Attach() — первой строкой App.OnStartup; Detach() — в конструкторе MainWindow (handover
    /// к зрелым CurrentDomain/TaskScheduler/Dispatcher-обработчикам).
    /// Самостоятельная: ServerMaster-лог не активирован, OsLocalization не определена, оконного
    /// состояния и Reboot нет. Обработчики не бросают никогда.
    /// </summary>
    public static class StartupCrashGuard
    {
        /// <summary> Таймаут connect/send при умирании процесса, мс (C1: отчёт синхронный, без отдельного потока) </summary>
        private const int ReportTimeoutMs = 1000;

        /// <summary> Токен фазы внутри тела отчёта. Префикс протокола Crash% не меняем (R6) </summary>
        private const string PhaseToken = " PHASE:PRE-MW ";

        private static object _reportedException;

        // 1 = подписаны, 0 = сняты. Ленточная гарантия: обработчик молчит сразу после Detach
        // (стенд detachprobe показал, что полагаться только на remove-аксессоры нельзя)
        private static volatile int _active;

        /// <summary> Подписать три обработчика. Идемпотентно: повторный Attach переподписывает </summary>
        public static void Attach()
        {
            try
            {
                Detach();

                Interlocked.Exchange(ref _active, 1);

                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
                TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

                if (Application.Current != null)
                {
                    Application.Current.DispatcherUnhandledException += OnDispatcherUnhandledException;
                }
            }
            catch
            {
                // страховка не бросает никогда
            }
        }

        /// <summary> Снять три обработчика. Снятие неподписанного — no-op </summary>
        public static void Detach()
        {
            try
            {
                Interlocked.Exchange(ref _active, 0);

                AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
                TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;

                if (Application.Current != null)
                {
                    Application.Current.DispatcherUnhandledException -= OnDispatcherUnhandledException;
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Отчёт+лог для catch-мест вне обработчиков (например, тема в OnStartup).
        /// Дедуп по экземпляру: то же исключение повторно не уходит.
        /// </summary>
        public static void SendStartupReport(string phrase, string label, Exception error)
        {
            string text;

            try
            {
                text = BuildBody(phrase, label, error);
                LogStartup(text);
            }
            catch
            {
                return;
            }

            try
            {
                SendReport(error, text);
            }
            catch
            {
            }
        }

        /// <summary> Локальный лог Engine\CrashStart.log, best-effort; фолбэк Trace (C3) </summary>
        public static void LogStartup(string text)
        {
            string line = null;

            try
            {
                line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + " " + text + Environment.NewLine;
            }
            catch
            {
            }

            if (line == null)
            {
                return;
            }

            try
            {
                File.AppendAllText(@"Engine\CrashStart.log", line);
                return;
            }
            catch
            {
                // Engine\ мог ещё не существовать на OnStartup
            }

            try
            {
                Trace.Write(line);
            }
            catch
            {
            }
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                if (_active == 0)
                {
                    return; // Detach (handover): страховка молчит, не дожидаясь фактического снятия подписки
                }

                Exception error = e.ExceptionObject as Exception
                    ?? new Exception("Unhandled exception object: " + e.ExceptionObject);

                if (error is OutOfMemoryException)
                { // политика 1c1c48c/BUG-0030: под OOM без отчёта, процесс умирает
                    LogStartup("IsMainWindow  " + PhaseToken + " STARTUP OOM (not reported)." + " THREAD ");
                    return;
                }

                string text = BuildBody("Startup unhandled exception.", " THREAD ", error);
                LogStartup(text);
                SendReport(error, text); // синхронно: CanContinue=false, поток отправки не дожил бы до смерти (C1)
            }
            catch
            {
            }
        }

        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            try
            {
                if (_active == 0)
                {
                    return; // Detach (handover)
                }

                Exception error = e.Exception;

                if (error is OutOfMemoryException
                    || (error != null && error.InnerException is OutOfMemoryException))
                {
                    e.SetObserved();
                    LogStartup("IsMainWindow  " + PhaseToken + " STARTUP OOM (not reported, task)." + " TASK ");
                    return;
                }

                if (error != null
                    && error.ToString().Contains("(995):"))
                { // прерывания потока за делом по кансел-токену (фильтр зрелого обработчика)
                    return;
                }

                string text = BuildBody("Startup unobserved task exception.", " TASK ", error);
                LogStartup(text);
                SendReport(error, text);
            }
            catch
            {
            }
        }

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                if (_active == 0)
                {
                    e.Handled = false; // Detach (handover): решают зрелые обработчики
                    return;
                }

                Exception error = e.Exception;

                if (error is OutOfMemoryException)
                { // без отчёта и без Handled (OOM-политика PR 1904): процесс умирает
                    LogStartup("IsMainWindow  " + PhaseToken + " STARTUP OOM dispatcher (not reported)." + " UI ");
                    return;
                }

                if (error != null
                    && error.ToString().Contains("(995):"))
                { // кансел-прерывания (фильтр зрелого dispatcher-хендлера)
                    e.Handled = true;
                    return;
                }

                string text = BuildBody("Startup unhandled UI exception.", " UI ", error);
                LogStartup(text);
                SendReport(error, text);

                // Фаза старта уже сорвана, окна нет; с Handled=true при OnExplicitShutdown
                // остался бы зомби-процесс. C4: осознанно false, Reboot не звать.
                e.Handled = false;
            }
            catch
            {
            }
        }

        private static string BuildBody(string phrase, string label, Exception error)
        {
            // без OsLocalization: язык старта не определён — ASCII-литерал
            return "IsMainWindow  " + PhaseToken + phrase + label + (error == null ? " <unknown>" + label : " " + error);
        }

        private static void SendReport(Exception error, string text)
        {
            if (error != null
                && Interlocked.CompareExchange(ref _reportedException, (object)error, null) != null)
            { // это же исключение уже отчитано (цепочка UI->THREAD); инвариант «ровно один отчёт на крах»
                return;
            }

            try
            {
                using (TcpClient client = new TcpClient())
                {
                    IAsyncResult ar = client.BeginConnect(MainWindow.CrashServerHost, MainWindow.CrashServerPort, null, null);

                    if (!ar.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(ReportTimeoutMs)))
                    {
                        client.Close();
                        LogStartup("IsMainWindow  " + PhaseToken + " STARTUP REPORT LOST (connect timeout)." + " THREAD ");
                        return;
                    }

                    client.EndConnect(ar);
                    client.SendTimeout = ReportTimeoutMs;
                    // не дать ядру выбросить незакрытый буфер при смерти процесса (C1): linger блокирует close до отправки
                    client.Client.LingerState = new LingerOption(true, 3);

                    byte[] sendBytes = Encoding.UTF8.GetBytes("Crash% " + text);
                    NetworkStream tcpStream = client.GetStream();
                    tcpStream.Write(sendBytes, 0, sendBytes.Length);
                    tcpStream.Flush();
                    client.Close();
                }
            }
            catch
            {
                try
                {
                    LogStartup("IsMainWindow  " + PhaseToken + " STARTUP REPORT LOST (send failed)." + " THREAD ");
                }
                catch
                {
                }
            }
        }
    }
}
