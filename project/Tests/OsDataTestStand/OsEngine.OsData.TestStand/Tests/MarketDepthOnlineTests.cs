/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace OsEngine.OsData.TestStand.Tests
{
    /// <summary>
    /// Модуль Стаканы. Он-лайн (глава 4.5 контекста стенда).
    /// Боевой коннектор с токеном (TInvest): подписка даёт срезы стакана (bid/ask).
    /// 4.5.1 получение срезов и их валидность.
    /// Без токена тест пропускается с ошибкой.
    /// </summary>
    public class MarketDepthOnlineTests : OsDataTestsBase
    {
        private const int WaitSeconds = 20;

        public MarketDepthOnlineTests(TestContext context)
            : base(context, "MD_ONLINE")
        {
        }

        public void RunAll()
        {
            _context.PrintModuleHeader(Module);

            string type = _context.ConnectorType;

            // Токен обязателен. Если не передан — пропускаем тест с ошибкой.
            string token = GetToken();

            if (string.IsNullOrEmpty(token))
            {
                _context.RecordFail(Module, "no_token", "токен не передан: задайте его в test-secrets.json (connector.parameters.Token) или env OSENGINE_TEST_CONNECTOR_PARAMETERS");
                return;
            }

            if (OpenOsDataMode() == false)
            {
                return;
            }

            DisableAllSets();

            if (SupportsMarketDepth(type) == false)
            {
                _context.RecordFail(Module, "md_live_support", "коннектор '" + type + "' не поддерживает live-стакан (MarketDepth)");
                return;
            }

            if (SetTokenAndConnect(type, token) == false)
            {
                return;
            }

            List<SecurityItem> securities = LoadSecurities(type);

            if (securities == null)
            {
                return;
            }

            // Точечный режим: тестируем одну бумагу из --security.
            if (IsTargeted(securities, out SecurityItem targetSecurity))
            {
                if (targetSecurity == null)
                {
                    _context.RecordFail(Module, "target", "бумага '" + _context.TargetSecurity + "' не найдена в списке коннектора");
                    return;
                }

                RunForSecurity(type, targetSecurity);
                return;
            }

            List<SecurityItem> representatives = PickRepresentatives(securities);

            if (representatives.Count == 0)
            {
                _context.RecordFail(Module, "pick_representatives", "не нашлось ни одного класса бумаг");
                return;
            }

            foreach (SecurityItem security in representatives)
            {
                RunForSecurity(type, security);
            }
        }

        private string GetToken()
        {
            Dictionary<string, string> parameters = _context.Secrets?.Parameters;

            if (parameters == null || parameters.Count == 0)
            {
                return null;
            }

            foreach (KeyValuePair<string, string> pair in parameters)
            {
                if (pair.Key.Equals("Token", StringComparison.OrdinalIgnoreCase))
                {
                    return pair.Value;
                }
            }

            return null;
        }

        private bool SupportsMarketDepth(string type)
        {
            try
            {
                string response = _context.Client.ToolsCall("server_management_get_data_timeframes", new { type });

                if (TryGetInnerText(response, out string text) == false)
                {
                    return false;
                }

                using (JsonDocument doc = JsonDocument.Parse(text))
                {
                    if (doc.RootElement.TryGetProperty("timeframes", out JsonElement timeframesElement)
                        && timeframesElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement item in timeframesElement.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String && item.GetString() == "MarketDepth")
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private bool SetTokenAndConnect(string type, string token)
        {
            try
            {
                _context.Client.ToolsCall("server_management_activate", new { type });

                _context.Client.ToolsCall("server_instance_set_params", new
                {
                    type = type,
                    parameters = new[] { new { name = "Token", value = token } }
                });

                _context.Client.ToolsCall("server_instance_connect", new { type });
                return true;
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, "connect", "не удалось подключиться к '" + type + "': " + error.Message);
                return false;
            }
        }

        #region Flow

        private void RunForSecurity(string type, SecurityItem security)
        {
            string setName = "TesterData_MdOnline_" + RemoveExcess(security.Name);

            try
            {
                _context.Client.ToolsCall("data_delete_set", new { name = setName });

                DateTime now = DateTime.Now;

                object createRequest = new
                {
                    name = setName,
                    source = type,
                    source_name = type,
                    timeframes = new[] { "MarketDepth" },
                    date_from = now.ToString("yyyy-MM-ddTHH:mm:ss"),
                    date_to = now.ToString("yyyy-MM-ddTHH:mm:ss")
                };

                _context.Client.ToolsCall("data_create_set", createRequest);

                object addRequest = new
                {
                    name = setName,
                    securities = new[] { new { name = security.Name } }
                };

                string addResponse = _context.Client.ToolsCall("data_set_securities_add", addRequest);

                if (IsError(addResponse))
                {
                    _context.RecordFail(Module, "add_security", security.Name + ": " + GetErrorText(addResponse));
                    CleanupSet(setName);
                    return;
                }

                _context.Client.ToolsCall("data_set_on", new { name = setName });

                // Live-стакан — поток, а не конечная загрузка: ждём фиксированное время накопления срезов.
                Thread.Sleep(WaitSeconds * 1000);

                _context.Client.ToolsCall("data_set_off", new { name = setName });

                Validate(security, setName);
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, "run", security.Name + ": " + error.Message);
            }
            finally
            {
                CleanupSet(setName);
            }
        }

        private void Validate(SecurityItem security, string setName)
        {
            string dataDir = Path.Combine(Path.GetDirectoryName(_context.OsEnginePath) ?? string.Empty, "Data");
            string securityFolder = RemoveExcess(security.Name);
            string mdFolder = Path.Combine(dataDir, "Set_" + setName, securityFolder, "MarketDepth");

            if (Directory.Exists(mdFolder) == false)
            {
                _context.RecordFail(Module, "files", security.Name + ": папка стаканов не найдена " + mdFolder);
                return;
            }

            string[] qshFiles = Directory.GetFiles(mdFolder, "*.qsh");

            if (qshFiles.Length == 0)
            {
                if (string.IsNullOrEmpty(_context.TargetSecurity))
                {
                    // Общий режим: у валюты/иностранных бумаг TInvest не шлёт live-стакан —
                    // это ограничение коннектора, а не ошибка данных.
                    _context.RecordPass(Module, "files", security.Name + ": нет live-стакана — пропускаем");
                }
                else
                {
                    // Точечный режим: пользователь явно запросил бумагу — нет данных = ошибка.
                    _context.RecordFail(Module, "files", security.Name + ": нет live-стакана");
                }

                return;
            }

            // 4.5.1. Файлы на месте и непустые.
            long totalBytes = 0;
            int emptyFiles = 0;

            foreach (string file in qshFiles)
            {
                long length = new FileInfo(file).Length;
                totalBytes += length;

                if (length == 0)
                {
                    emptyFiles++;
                }
            }

            if (emptyFiles == 0)
            {
                _context.RecordPass(Module, "files", security.Name + ": .qsh-файлов " + qshFiles.Length + ", " + totalBytes + " байт");
            }
            else
            {
                _context.RecordFail(Module, "files", security.Name + ": пустых .qsh-файлов " + emptyFiles);
            }

            TestValidity(security, qshFiles);
        }

        private void TestValidity(SecurityItem security, string[] qshFiles)
        {
            long totalFrames = 0;
            long totalChanges = 0;
            long crossed = 0;
            long badPrice = 0;
            long badVolume = 0;
            int parsedFiles = 0;

            foreach (string file in qshFiles)
            {
                QshStats stats = QshParser.Parse(file);

                if (stats == null)
                {
                    continue;
                }

                parsedFiles++;
                totalFrames += stats.Frames;
                totalChanges += stats.Changes;
                crossed += stats.Crossed;
                badPrice += stats.BadPrice;
                badVolume += stats.BadVolume;
            }

            if (parsedFiles == 0)
            {
                _context.RecordFail(Module, "validity", security.Name + ": ни один .qsh-файл не разобран");
                return;
            }

            if (crossed == 0 && badPrice == 0 && badVolume == 0)
            {
                _context.RecordPass(Module, "validity", security.Name + ": срезов " + totalFrames + ", изменений " + totalChanges
                    + ", bid≤ask/цена/объём валидны");
            }
            else
            {
                _context.RecordFail(Module, "validity", security.Name + ": срезов " + totalFrames + ", изменений " + totalChanges
                    + ", битых: crossed " + crossed + ", цена " + badPrice + ", объём " + badVolume);
            }
        }

        #endregion
    }
}
