/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using OsEngine.Optimizer.TestStand;

namespace OsEngine.Optimizer.TestStand.Tests
{
    /// <summary>
    /// Модуль 11 — SSE: во время прогона приходят события optimizer.test.progress / optimizer.test.finished.
    /// </summary>
    public class SseTests : OsOptimizerTestsBase
    {
        public SseTests(TestContext context) : base(context, "Sse")
        {
        }

        public void RunAll()
        {
            if (!Setup())
            {
                return;
            }

            SseEvents();
        }

        private bool Setup()
        {
            if (!SelectDataSet() || !WaitForStorageLoad(60))
            {
                RecordFail("Setup", "не удалось выбрать сет / загрузить хранилище");
                return false;
            }

            if (!CreateBot("SmaTrendSample"))
            {
                RecordFail("CreateBot", "робот не загрузился");
                return false;
            }

            string tabName = GetTabName("Simple");

            if (string.IsNullOrEmpty(tabName) || !ConfigureSimpleTab(tabName, "SBER.txt", "Min30"))
            {
                RecordFail("ConfigureTab", "вкладка Simple не настроилась");
                return false;
            }

            string paramName = FindIntParamName();

            if (string.IsNullOrEmpty(paramName) || !SetEnumeration(paramName, 20, 22, 1))
            {
                RecordFail("Enumeration", "не удалось задать перебор");
                return false;
            }

            return true;
        }

        private void SseEvents()
        {
            // инициализируем сессию и завершаем handshake, чтобы сервер слал SSE-события
            QuietCall("optimizer_data_get_status", new { });
            _context.Client.SendInitialized();

            bool finishedSeen = false;
            bool progressSeen = false;
            bool terminalSeen = false;
            object locker = new object();

            HttpResponseMessage sseResponse;

            try
            {
                sseResponse = _context.Client.GetSseResponse();
            }
            catch (Exception error)
            {
                RecordFail("SseEvents", "не удалось открыть SSE-стрим: " + error.Message);
                return;
            }

            Stream stream = sseResponse.Content.ReadAsStreamAsync().GetAwaiter().GetResult();

            Thread readerThread = new Thread(() =>
            {
                try
                {
                    using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        string line;

                        while ((line = reader.ReadLine()) != null)
                        {
                            if (line.StartsWith("data: ") == false)
                            {
                                continue;
                            }

                            string data = line.Substring("data: ".Length).Trim();

                            if (data.Contains("optimizer.test.finished"))
                            {
                                lock (locker)
                                {
                                    finishedSeen = true;
                                }
                            }

                            if (data.Contains("optimizer.test.progress"))
                            {
                                lock (locker)
                                {
                                    progressSeen = true;
                                }
                            }

                            if (data.Contains("terminal.launched"))
                            {
                                lock (locker)
                                {
                                    terminalSeen = true;
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // стрим закрылся — это нормально
                }
            });

            readerThread.IsBackground = true;
            readerThread.Start();

            Thread.Sleep(2000);

            if (!StartAndWait(600))
            {
                RecordFail("SseEvents", "прогон не завершился за отведённое время");
                return;
            }

            // finished-событие может прийти чуть позже, чем is_running=false
            for (int i = 0; i < 10; i++)
            {
                lock (locker)
                {
                    if (finishedSeen)
                    {
                        break;
                    }
                }

                Thread.Sleep(1000);
            }

            lock (locker)
            {
                if (finishedSeen)
                {
                    RecordPass("SseEvents", $"события SSE получены (finished={finishedSeen}, progress={progressSeen}, terminal={terminalSeen})");
                }
                else
                {
                    RecordFail("SseEvents", $"finished={finishedSeen}, progress={progressSeen}, terminal={terminalSeen}");
                }
            }

            try
            {
                stream.Dispose();
                sseResponse.Dispose();
            }
            catch
            {
                // ресурсы могли уже освободиться
            }
        }
    }
}
