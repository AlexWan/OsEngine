/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Collections.Generic;
using System.IO;

namespace OsEngine.OsData.TestStand.Tests
{
    /// <summary>
    /// Модуль Стаканы. История (глава 4.4 контекста стенда).
    /// Качает исторические стаканы (.qsh) через QscalpMarketDepth и валидирует их:
    /// 4.4.1 файлы на месте и непустые; 4.4.2 валидность срезов.
    /// </summary>
    public class MarketDepthHistoryTests : OsDataTestsBase
    {
        public MarketDepthHistoryTests(TestContext context)
            : base(context, "MD_HISTORY")
        {
        }

        public void RunAll()
        {
            _context.PrintModuleHeader(Module);

            string type = _context.ConnectorType;

            if (OpenOsDataMode() == false)
            {
                return;
            }

            DisableAllSets();

            if (SupportsMarketDepthHistory(type) == false)
            {
                _context.RecordFail(Module, "md_history_support", "коннектор '" + type + "' не поддерживает исторические стаканы (MarketDepthHistory)");
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

                DateTime targetDateTo = DateTime.Now.Date.AddDays(-2);

                while (targetDateTo.DayOfWeek == DayOfWeek.Saturday || targetDateTo.DayOfWeek == DayOfWeek.Sunday)
                {
                    targetDateTo = targetDateTo.AddDays(-1);
                }

                RunForSecurity(type, targetSecurity,
                    TargetDateFrom(targetDateTo), TargetDateTo(targetDateTo));
                return;
            }

            List<SecurityItem> representatives = PickRepresentatives(securities);

            if (representatives.Count == 0)
            {
                _context.RecordFail(Module, "pick_representatives", "не нашлось ни одного класса бумаг");
                return;
            }

            // Один торговый день в недавнем прошлом — архив Qscalp публикуется с задержкой.
            DateTime dateTo = DateTime.Now.Date.AddDays(-2);

            while (dateTo.DayOfWeek == DayOfWeek.Saturday || dateTo.DayOfWeek == DayOfWeek.Sunday)
            {
                dateTo = dateTo.AddDays(-1);
            }

            DateTime dateFrom = dateTo;

            foreach (SecurityItem security in representatives)
            {
                RunForSecurity(type, security, dateFrom, dateTo);
            }
        }

        private bool SupportsMarketDepthHistory(string type)
        {
            try
            {
                string response = _context.Client.ToolsCall("server_management_get_data_timeframes", new { type });

                return response != null
                    && response.IndexOf("MarketDepthHistory", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }

        #region Flow

        private void RunForSecurity(string type, SecurityItem security, DateTime dateFrom, DateTime dateTo)
        {
            string setName = "TesterData_MdHistory_" + RemoveExcess(security.Name);

            try
            {
                _context.Client.ToolsCall("data_delete_set", new { name = setName });

                object createRequest = new
                {
                    name = setName,
                    source = type,
                    source_name = type,
                    timeframes = new[] { "MarketDepthHistory" },
                    date_from = dateFrom.ToString("yyyy-MM-ddTHH:mm:ss"),
                    date_to = dateTo.ToString("yyyy-MM-ddTHH:mm:ss")
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

                bool loaded = WaitForLoad(setName);

                _context.Client.ToolsCall("data_set_off", new { name = setName });

                if (loaded == false)
                {
                    _context.RecordFail(Module, "download", security.Name + ": загрузка не завершилась");
                    CleanupSet(setName);
                    return;
                }

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
                _context.RecordFail(Module, "files", security.Name + ": нет .qsh-файлов в " + mdFolder);
                return;
            }

            // 4.4.1. Файлы на месте и непустые.
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

            // 4.4.2. Валидность срезов.
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
