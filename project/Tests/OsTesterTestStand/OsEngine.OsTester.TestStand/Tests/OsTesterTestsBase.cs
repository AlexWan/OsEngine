/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Text.Json;
using OsEngine.OsTester.TestStand;

namespace OsEngine.OsTester.TestStand.Tests
{
    /// <summary>
    /// Общая база тестов тестера: MCP-вызовы и разбор ответов.
    /// </summary>
    public abstract class OsTesterTestsBase
    {
        protected readonly TestContext _context;

        protected readonly string Module;

        protected OsTesterTestsBase(TestContext context, string module)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            Module = module;
        }

        #region MCP calls

        protected string ToolsCall(string method, object arguments)
        {
            _context.PrintRequest(Module, method, arguments);
            string response = _context.Client.ToolsCall(method, arguments);
            _context.PrintResponse(response);
            return response;
        }

        /// <summary>
        /// MCP-вызов без печати в лог (для циклов опроса состояния).
        /// </summary>
        protected string QuietCall(string method, object arguments)
        {
            return _context.Client.ToolsCall(method, arguments);
        }

        #endregion

        #region Reporting

        protected void RecordPass(string method, string message)
        {
            _context.RecordPass(Module, method, message);
        }

        protected void RecordFail(string method, string message)
        {
            _context.RecordFail(Module, method, message);
        }

        #endregion

        #region Response parsing

        protected static bool TryGetInnerText(string response, out string text)
        {
            text = string.Empty;

            if (string.IsNullOrWhiteSpace(response))
            {
                return false;
            }

            try
            {
                using (JsonDocument doc = JsonDocument.Parse(response))
                {
                    JsonElement root = doc.RootElement;

                    if (root.TryGetProperty("IsError", out JsonElement isError)
                        && isError.ValueKind == JsonValueKind.True)
                    {
                        return false;
                    }

                    if (root.TryGetProperty("Content", out JsonElement content) && content.GetArrayLength() > 0)
                    {
                        text = content[0].GetProperty("Text").GetString() ?? string.Empty;
                        return string.IsNullOrEmpty(text) == false;
                    }
                }
            }
            catch
            {
                // не разобрать ответ — считаем пустым
            }

            return false;
        }

        /// <summary>
        /// Разобрать тело ответа MCP в JSON-элемент.
        /// </summary>
        protected static bool TryParseJson(string response, out JsonElement root)
        {
            root = default;

            if (!TryGetInnerText(response, out string text))
            {
                return false;
            }

            try
            {
                using (JsonDocument doc = JsonDocument.Parse(text))
                {
                    root = doc.RootElement.Clone();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        protected static bool IsError(string response)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(response))
                {
                    return doc.RootElement.TryGetProperty("IsError", out JsonElement e)
                        && e.ValueKind == JsonValueKind.True;
                }
            }
            catch
            {
                return true;
            }
        }

        protected static string GetErrorText(string response)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(response))
                {
                    if (doc.RootElement.TryGetProperty("Content", out JsonElement content) && content.GetArrayLength() > 0)
                    {
                        return content[0].GetProperty("Text").GetString() ?? string.Empty;
                    }
                }
            }
            catch
            {
                // не разобрать текст ошибки — вернём пусто
            }

            return string.Empty;
        }

        #endregion

        #region Tester helpers

        /// <summary>
        /// Дождаться окончания прогона тестера (regime == NotActive или 100%).
        /// </summary>
        protected bool WaitForTestCompletion(int timeoutSeconds)
        {
            DateTime deadline = DateTime.Now.AddSeconds(timeoutSeconds);

            while (DateTime.Now < deadline)
            {
                string response = QuietCall("tester_get_status", new { });

                if (TryParseJson(response, out JsonElement status))
                {
                    string regime = status.TryGetProperty("regime", out JsonElement regimeElement)
                        ? regimeElement.GetString() ?? string.Empty
                        : string.Empty;

                    double progress = status.TryGetProperty("progress_percent", out JsonElement progressElement)
                        && progressElement.ValueKind == JsonValueKind.Number
                        ? progressElement.GetDouble()
                        : 0;

                    if (regime == "NotActive" || progress >= 100)
                    {
                        Console.WriteLine($"  Test finished (regime={regime}, progress={progress}).");
                        return true;
                    }
                }

                System.Threading.Thread.Sleep(1000);
            }

            return false;
        }

        /// <summary>
        /// Дождаться подъёма тестер-сервера (tester_data_get_config перестаёт отвечать ошибкой).
        /// </summary>
        protected bool WaitForTesterReady(int timeoutSeconds)
        {
            DateTime deadline = DateTime.Now.AddSeconds(timeoutSeconds);

            while (DateTime.Now < deadline)
            {
                string response = QuietCall("tester_data_get_config", new { });

                if (!IsError(response))
                {
                    return true;
                }

                System.Threading.Thread.Sleep(500);
            }

            return false;
        }

        /// <summary>
        /// Выбрать сет данных тестера (чтобы модуль работал и отдельно, без модуля Data).
        /// </summary>
        protected bool SelectDataSet()
        {
            WaitForTesterReady(60);
            ResetTesterDefaults();
            DeleteAllRobots();

            string response = ToolsCall("tester_data_set_config", new { set_name = _context.DataSetName });
            return !IsError(response);
        }

        /// <summary>
        /// Удалить всех роботов, чтобы каждый модуль был самодостаточным.
        /// </summary>
        protected void DeleteAllRobots()
        {
            for (int pass = 0; pass < 20; pass++)
            {
                string list = QuietCall("bot_get_list", new { });

                if (!TryParseJson(list, out JsonElement root)
                    || !root.TryGetProperty("bots", out JsonElement bots)
                    || bots.ValueKind != JsonValueKind.Array)
                {
                    break;
                }

                if (bots.GetArrayLength() == 0)
                {
                    break;
                }

                foreach (JsonElement bot in bots.EnumerateArray())
                {
                    string name = bot.TryGetProperty("name", out JsonElement nameElement)
                        ? nameElement.GetString() ?? string.Empty
                        : string.Empty;

                    if (string.IsNullOrEmpty(name))
                    {
                        continue;
                    }

                    QuietCall("bot_delete", new { bot_id = name });
                }
            }
        }

        /// <summary>
        /// Дождаться завершения подключения бумаг после bot_set_config_tab_*,
        /// чтобы tester_start не упирался в защитный интервал «идёт подключение бумаг».
        /// </summary>
        protected void WaitForReconnect()
        {
            System.Threading.Thread.Sleep(15000);
        }

        /// <summary>
        /// Сбросить тестер в стандартные настройки, чтобы предыдущие прогоны не влияли.
        /// </summary>
        protected void ResetTesterDefaults()
        {
            ToolsCall("tester_portfolio_set_config", new
            {
                start_portfolio = 1000000m,
                portfolio_calculation_enabled = false
            });

            ToolsCall("tester_execution_set_config", new
            {
                slippage_to_simple_order = 0,
                slippage_to_stop_order = 0,
                order_execution_type = "Touch",
                non_trade_periods = new object[0]
            });

            ToolsCall("tester_data_set_config", new { type_tester_data = "Candle" });
        }

        /// <summary>
        /// Дождаться загрузки бумаг сета (TesterServer грузит их асинхронно после выбора сета).
        /// </summary>
        protected bool WaitForSecurities(int timeoutSeconds)
        {
            DateTime deadline = DateTime.Now.AddSeconds(timeoutSeconds);

            while (DateTime.Now < deadline)
            {
                string response = QuietCall("tester_get_securities", new { });

                if (TryParseJson(response, out JsonElement root)
                    && root.TryGetProperty("count", out JsonElement countElement)
                    && countElement.ValueKind == JsonValueKind.Number
                    && countElement.GetInt32() > 0)
                {
                    return true;
                }

                System.Threading.Thread.Sleep(1000);
            }

            return false;
        }

        /// <summary>
        /// Дождаться, пока список бумаг перестанет расти (TesterServer грузит их по одной),
        /// чтобы скринер забрал полный список, а не частичный.
        /// </summary>
        protected bool WaitForSecuritiesStable(int timeoutSeconds)
        {
            DateTime deadline = DateTime.Now.AddSeconds(timeoutSeconds);
            int lastCount = -1;
            int stablePolls = 0;

            while (DateTime.Now < deadline)
            {
                string response = QuietCall("tester_get_securities", new { });

                int count = -1;

                if (TryParseJson(response, out JsonElement root)
                    && root.TryGetProperty("count", out JsonElement countElement)
                    && countElement.ValueKind == JsonValueKind.Number)
                {
                    count = countElement.GetInt32();
                }

                if (count > 0 && count == lastCount)
                {
                    stablePolls++;

                    if (stablePolls >= 2)
                    {
                        return true;
                    }
                }
                else
                {
                    stablePolls = 0;
                }

                lastCount = count;

                System.Threading.Thread.Sleep(1000);
            }

            return false;
        }

        /// <summary>
        /// Запустить тест (с повтором — у тестера есть защитный интервал между запусками)
        /// и дождаться завершения, включая fast-forward по ходу, если он выключен.
        /// </summary>
        protected bool StartTestAndWait(int timeoutSeconds)
        {
            DateTime startDeadline = DateTime.Now.AddSeconds(60);
            bool playing = false;

            while (DateTime.Now < startDeadline && playing == false)
            {
                string response = QuietCall("tester_start", new { fast_forward = true });

                if (TryParseJson(response, out JsonElement status))
                {
                    string regime = status.TryGetProperty("regime", out JsonElement regimeElement)
                        ? regimeElement.GetString() ?? string.Empty
                        : string.Empty;

                    if (regime == "Play")
                    {
                        playing = true;
                    }
                }

                if (playing == false)
                {
                    System.Threading.Thread.Sleep(2000);
                }
            }

            if (playing == false)
            {
                return false;
            }

            DateTime deadline = DateTime.Now.AddSeconds(timeoutSeconds);

            while (DateTime.Now < deadline)
            {
                string response = QuietCall("tester_get_status", new { });

                if (TryParseJson(response, out JsonElement status))
                {
                    string regime = status.TryGetProperty("regime", out JsonElement regimeElement)
                        ? regimeElement.GetString() ?? string.Empty
                        : string.Empty;

                    bool fastForward = status.TryGetProperty("fast_forward", out JsonElement fastForwardElement)
                        && fastForwardElement.ValueKind == JsonValueKind.True;

                    double progress = status.TryGetProperty("progress_percent", out JsonElement progressElement)
                        && progressElement.ValueKind == JsonValueKind.Number
                        ? progressElement.GetDouble()
                        : 0;

                    if (regime == "NotActive" || progress >= 100)
                    {
                        Console.WriteLine($"  Test finished (regime={regime}, progress={progress}).");
                        return true;
                    }

                    if (regime == "Play" && fastForward == false)
                    {
                        QuietCall("tester_fast_forward", new { });
                    }
                }

                System.Threading.Thread.Sleep(1000);
            }

            return false;
        }

        /// <summary>
        /// Имя первой бумаги, загруженной в тестер.
        /// </summary>
        protected string GetFirstSecurityName()
        {
            WaitForSecurities(30);

            string response = QuietCall("tester_get_securities", new { });

            if (!TryParseJson(response, out JsonElement root))
            {
                return string.Empty;
            }

            if (!root.TryGetProperty("securities", out JsonElement list)
                || list.ValueKind != JsonValueKind.Array
                || list.GetArrayLength() == 0)
            {
                return string.Empty;
            }

            JsonElement first = list[0];
            return first.TryGetProperty("name", out JsonElement nameElement)
                ? nameElement.GetString() ?? string.Empty
                : string.Empty;
        }

        /// <summary>
        /// Имя вкладки заданного типа у робота (Simple/Screener).
        /// </summary>
        protected string GetTabName(string botId, string tabType)
        {
            string response = QuietCall("bot_get_sources", new { bot_id = botId });

            if (!TryParseJson(response, out JsonElement root))
            {
                return string.Empty;
            }

            if (!root.TryGetProperty("sources", out JsonElement list) || list.ValueKind != JsonValueKind.Array)
            {
                return string.Empty;
            }

            foreach (JsonElement source in list.EnumerateArray())
            {
                string type = source.TryGetProperty("type", out JsonElement typeElement)
                    ? typeElement.GetString() ?? string.Empty
                    : string.Empty;

                if (type == tabType)
                {
                    return source.TryGetProperty("name", out JsonElement nameElement)
                        ? nameElement.GetString() ?? string.Empty
                        : string.Empty;
                }
            }

            return string.Empty;
        }

        #endregion

        #region Report metrics

        public sealed class TestMetrics
        {
            public int Trades;
            public decimal ProfitAbs;
            public decimal Commission;
            public decimal Volume;
        }

        /// <summary>
        /// Запустить тест, дождаться конца и вернуть метрики из отчёта (или null при ошибке).
        /// </summary>
        protected TestMetrics RunAndGetMetrics()
        {
            bool finished = StartTestAndWait(900);

            if (!finished)
            {
                return null;
            }

            string report = QuietCall("tester_get_report", new { });

            if (!TryParseJson(report, out JsonElement root))
            {
                return null;
            }

            return ExtractMetrics(root);
        }

        /// <summary>
        /// Извлечь из отчёта: кол-во позиций (trades), прибыль, комиссию и суммарный объём позиций.
        /// </summary>
        protected TestMetrics ExtractMetrics(JsonElement report)
        {
            TestMetrics metrics = new TestMetrics();

            if (report.TryGetProperty("statistics_full", out JsonElement stats) && stats.ValueKind == JsonValueKind.Object)
            {
                metrics.Trades = stats.TryGetProperty("trades", out JsonElement tradesElement) && tradesElement.ValueKind == JsonValueKind.Number
                    ? tradesElement.GetInt32()
                    : 0;

                metrics.ProfitAbs = stats.TryGetProperty("profit_abs", out JsonElement profitElement) && profitElement.ValueKind == JsonValueKind.Number
                    ? profitElement.GetDecimal()
                    : 0m;

                metrics.Commission = stats.TryGetProperty("commission", out JsonElement commissionElement) && commissionElement.ValueKind == JsonValueKind.Number
                    ? commissionElement.GetDecimal()
                    : 0m;
            }

            if (report.TryGetProperty("positions", out JsonElement positions) && positions.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement position in positions.EnumerateArray())
                {
                    if (position.TryGetProperty("volume", out JsonElement volumeElement) && volumeElement.ValueKind == JsonValueKind.Number)
                    {
                        metrics.Volume += volumeElement.GetDecimal();
                    }
                }
            }

            return metrics;
        }

        #endregion
    }
}
