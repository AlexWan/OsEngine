/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Collections.Generic;
using System.Text.Json;
using OsEngine.Optimizer.TestStand;

namespace OsEngine.Optimizer.TestStand.Tests
{
    /// <summary>
    /// Общая база тестов оптимизатора: MCP-вызовы, разбор ответов и помощники
    /// для работы с оптимизатором (данные, робот, вкладки, параметры, прогон, отчёт).
    /// </summary>
    public abstract class OsOptimizerTestsBase
    {
        protected readonly TestContext _context;

        protected readonly string Module;

        /// <summary>Диапазон оптимизации в тестах (короткий, чтобы прогон был быстрым).</summary>
        protected const string TestDateFrom = "2024-01-01T00:00:00";

        protected const string TestDateTo = "2024-06-30T00:00:00";

        protected OsOptimizerTestsBase(TestContext context, string module)
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

        #region Optimizer helpers

        /// <summary>
        /// Дождаться, пока оптимизатор-сервер ответит (optimizer_data_get_status перестаёт отвечать ошибкой).
        /// </summary>
        protected bool WaitForOptimizerReady(int timeoutSeconds)
        {
            DateTime deadline = DateTime.Now.AddSeconds(timeoutSeconds);

            while (DateTime.Now < deadline)
            {
                string response = QuietCall("optimizer_data_get_status", new { });

                if (!IsError(response))
                {
                    return true;
                }

                System.Threading.Thread.Sleep(500);
            }

            return false;
        }

        /// <summary>
        /// Выбрать сет данных оптимизатора, диапазон оптимизации и построить фазы.
        /// </summary>
        protected bool SelectDataSet()
        {
            return SelectDataSetByName(_context.DataSetName);
        }

        /// <summary>
        /// Выбрать конкретный сет данных оптимизатора и построить фазы.
        /// </summary>
        protected bool SelectDataSetByName(string setName)
        {
            WaitForOptimizerReady(60);

            string response = ToolsCall("optimizer_data_set_config", new
            {
                source_type = "Set",
                set_name = setName,
                date_from = TestDateFrom,
                date_to = TestDateTo
            });

            if (IsError(response))
            {
                return false;
            }

            BuildPhases();
            return true;
        }

        /// <summary>
        /// Построить фазы walk-forward (обязательно перед optimizer_start).
        /// </summary>
        protected void BuildPhases()
        {
            ToolsCall("optimizer_phases_set", new
            {
                time_start = TestDateFrom,
                time_end = TestDateTo,
                iteration_count = 1,
                percent_on_filtration = 30,
                last_in_sample = false
            });
        }

        /// <summary>
        /// Дождаться загрузки хранилища данных оптимизатора.
        /// </summary>
        protected bool WaitForStorageLoad(int timeoutSeconds)
        {
            DateTime deadline = DateTime.Now.AddSeconds(timeoutSeconds);

            while (DateTime.Now < deadline)
            {
                string response = QuietCall("optimizer_data_get_status", new { });

                if (TryParseJson(response, out JsonElement root)
                    && root.TryGetProperty("is_loaded", out JsonElement loadedElement)
                    && loadedElement.ValueKind == JsonValueKind.True)
                {
                    return true;
                }

                System.Threading.Thread.Sleep(1000);
            }

            return false;
        }

        /// <summary>
        /// Выбрать робота для оптимизации. Возвращает true, если робот загружен.
        /// </summary>
        protected bool CreateBot(string strategyName)
        {
            string response = ToolsCall("optimizer_bot_set", new { strategy_name = strategyName });

            if (!TryParseJson(response, out JsonElement root))
            {
                return false;
            }

            return root.TryGetProperty("is_loaded", out JsonElement loadedElement)
                && loadedElement.ValueKind == JsonValueKind.True;
        }

        /// <summary>
        /// Имя вкладки заданного типа (Simple/Screener) у выбранного робота.
        /// </summary>
        protected string GetTabName(string tabType)
        {
            string response = QuietCall("optimizer_bot_tab_get_config", new { });

            if (!TryParseJson(response, out JsonElement root)
                || !root.TryGetProperty("tabs", out JsonElement tabs)
                || tabs.ValueKind != JsonValueKind.Array)
            {
                return string.Empty;
            }

            foreach (JsonElement tab in tabs.EnumerateArray())
            {
                string type = tab.TryGetProperty("tab_type", out JsonElement typeElement)
                    ? typeElement.GetString() ?? string.Empty
                    : string.Empty;

                if (type == tabType)
                {
                    return tab.TryGetProperty("tab_name", out JsonElement nameElement)
                        ? nameElement.GetString() ?? string.Empty
                        : string.Empty;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Настроить Simple-вкладку (бумага + таймфрейм).
        /// </summary>
        protected bool ConfigureSimpleTab(string tabName, string security, string timeFrame)
        {
            string response = ToolsCall("optimizer_bot_tab_set_config", new
            {
                tab_name = tabName,
                security_name = security,
                time_frame = timeFrame
            });

            return !IsError(response);
        }

        /// <summary>
        /// Настроить Screener-вкладку (массив бумаг + таймфрейм).
        /// </summary>
        protected bool ConfigureScreenerTab(string tabName, string[] securities, string timeFrame)
        {
            object[] list = new object[securities.Length];

            for (int i = 0; i < securities.Length; i++)
            {
                list[i] = new { name = securities[i] };
            }

            string response = ToolsCall("optimizer_bot_tab_set_config", new
            {
                tab_name = tabName,
                time_frame = timeFrame,
                securities = list
            });

            return !IsError(response);
        }

        /// <summary>
        /// Имя первого числового (Int) параметра робота — для перебора.
        /// </summary>
        protected string FindIntParamName()
        {
            string response = QuietCall("optimizer_params_get", new { });

            if (!TryParseJson(response, out JsonElement root)
                || !root.TryGetProperty("parameters", out JsonElement parameters)
                || parameters.ValueKind != JsonValueKind.Array)
            {
                return string.Empty;
            }

            foreach (JsonElement parameter in parameters.EnumerateArray())
            {
                string type = parameter.TryGetProperty("type", out JsonElement typeElement)
                    ? typeElement.GetString() ?? string.Empty
                    : string.Empty;

                if (type == "Int")
                {
                    return parameter.TryGetProperty("name", out JsonElement nameElement)
                        ? nameElement.GetString() ?? string.Empty
                        : string.Empty;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Включить перебор одного параметра (диапазон) и включить робота (Regime = On).
        /// Гасит перебор всех остальных параметров.
        /// </summary>
        protected bool SetEnumeration(string paramName, object start, object stop, object step)
        {
            List<EnumParam> list = new List<EnumParam>();
            list.Add(new EnumParam { Name = paramName, Start = start, Stop = stop, Step = step });
            return SetEnumerationMany(list);
        }

        /// <summary>
        /// Включить перебор нескольких параметров сразу. Гасит перебор всех остальных.
        /// </summary>
        protected bool SetEnumerationMany(List<EnumParam> enumerated)
        {
            List<object> items = new List<object>();

            string getResponse = QuietCall("optimizer_params_get", new { });

            if (TryParseJson(getResponse, out JsonElement getRoot)
                && getRoot.TryGetProperty("parameters", out JsonElement paramsElement)
                && paramsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement parameter in paramsElement.EnumerateArray())
                {
                    string name = parameter.TryGetProperty("name", out JsonElement nameElement)
                        ? nameElement.GetString() ?? string.Empty
                        : string.Empty;

                    bool isEnumerated = enumerated.Exists(e => e.Name == name);

                    if (isEnumerated || name == "Regime")
                    {
                        continue;
                    }

                    items.Add(new { name = name, on = false });
                }
            }

            foreach (EnumParam parameter in enumerated)
            {
                items.Add(new { name = parameter.Name, start = parameter.Start, stop = parameter.Stop, step = parameter.Step, on = true });
            }

            items.Add(new { name = "Regime", value = "On" });

            string response = ToolsCall("optimizer_params_set", new { parameters = items });

            return !IsError(response);
        }

        /// <summary>
        /// Список параметров робота (имя + тип).
        /// </summary>
        protected List<ParamInfo> GetParamList()
        {
            List<ParamInfo> result = new List<ParamInfo>();

            string response = QuietCall("optimizer_params_get", new { });

            if (TryParseJson(response, out JsonElement root)
                && root.TryGetProperty("parameters", out JsonElement parameters)
                && parameters.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement parameter in parameters.EnumerateArray())
                {
                    string name = parameter.TryGetProperty("name", out JsonElement nameElement)
                        ? nameElement.GetString() ?? string.Empty
                        : string.Empty;

                    string type = parameter.TryGetProperty("type", out JsonElement typeElement)
                        ? typeElement.GetString() ?? string.Empty
                        : string.Empty;

                    result.Add(new ParamInfo { Name = name, Type = type });
                }
            }

            return result;
        }

        /// <summary>
        /// Включён ли параметр в перебор (on-флаг).
        /// </summary>
        protected bool IsParamOn(string name)
        {
            string response = QuietCall("optimizer_params_get", new { });

            if (TryParseJson(response, out JsonElement root)
                && root.TryGetProperty("parameters", out JsonElement parameters)
                && parameters.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement parameter in parameters.EnumerateArray())
                {
                    string paramName = parameter.TryGetProperty("name", out JsonElement nameElement)
                        ? nameElement.GetString() ?? string.Empty
                        : string.Empty;

                    if (paramName == name)
                    {
                        return parameter.TryGetProperty("on", out JsonElement onElement)
                            && onElement.ValueKind == JsonValueKind.True;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Предполагаемое число проходов (или -1 при ошибке).
        /// </summary>
        protected int GetPassCount()
        {
            string response = QuietCall("optimizer_get_pass_count", new { });

            if (TryParseJson(response, out JsonElement root)
                && root.TryGetProperty("pass_count", out JsonElement countElement)
                && countElement.ValueKind == JsonValueKind.Number)
            {
                return countElement.GetInt32();
            }

            return -1;
        }

        /// <summary>
        /// Сбросить перебор всех параметров (все on:false), чтобы состояние не протекло в следующий модуль.
        /// </summary>
        protected void ClearAllEnumeration()
        {
            List<object> items = new List<object>();

            foreach (ParamInfo param in GetParamList())
            {
                items.Add(new { name = param.Name, on = false });
            }

            ToolsCall("optimizer_params_set", new { parameters = items });
        }

        /// <summary>
        /// Числовое поле параметра по имени (или defaultValue при отсутствии).
        /// </summary>
        protected int GetParamIntField(string paramName, string fieldName, int defaultValue)
        {
            string response = QuietCall("optimizer_params_get", new { });

            if (TryParseJson(response, out JsonElement root)
                && root.TryGetProperty("parameters", out JsonElement parameters)
                && parameters.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement parameter in parameters.EnumerateArray())
                {
                    string name = parameter.TryGetProperty("name", out JsonElement nameElement)
                        ? nameElement.GetString() ?? string.Empty
                        : string.Empty;

                    if (name != paramName)
                    {
                        continue;
                    }

                    if (parameter.TryGetProperty(fieldName, out JsonElement fieldElement)
                        && fieldElement.ValueKind == JsonValueKind.Number)
                    {
                        return fieldElement.GetInt32();
                    }
                }
            }

            return defaultValue;
        }

        protected int GetPhasesInt(string fieldName, int defaultValue)
        {
            string response = QuietCall("optimizer_phases_get", new { });

            if (TryParseJson(response, out JsonElement root)
                && root.TryGetProperty(fieldName, out JsonElement fieldElement)
                && fieldElement.ValueKind == JsonValueKind.Number)
            {
                return fieldElement.GetInt32();
            }

            return defaultValue;
        }

        protected bool GetPhasesBool(string fieldName)
        {
            string response = QuietCall("optimizer_phases_get", new { });

            if (TryParseJson(response, out JsonElement root)
                && root.TryGetProperty(fieldName, out JsonElement fieldElement)
                && fieldElement.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            return false;
        }

        protected string GetPhasesString(string fieldName)
        {
            string response = QuietCall("optimizer_phases_get", new { });

            if (TryParseJson(response, out JsonElement root)
                && root.TryGetProperty(fieldName, out JsonElement fieldElement)
                && fieldElement.ValueKind == JsonValueKind.String)
            {
                return fieldElement.GetString() ?? string.Empty;
            }

            return string.Empty;
        }

        protected int GetOutOfSampleDays()
        {
            string response = QuietCall("optimizer_phases_get", new { });

            if (TryParseJson(response, out JsonElement root)
                && root.TryGetProperty("fazes", out JsonElement fazes)
                && fazes.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement faze in fazes.EnumerateArray())
                {
                    string type = faze.TryGetProperty("type", out JsonElement typeElement)
                        ? typeElement.GetString() ?? string.Empty
                        : string.Empty;

                    if (type == "OutOfSample")
                    {
                        return faze.TryGetProperty("days", out JsonElement daysElement) && daysElement.ValueKind == JsonValueKind.Number
                            ? daysElement.GetInt32()
                            : -1;
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// Сбросить торговые настройки (комиссия/исполнение/проскальзывание/депозит) на дефолт.
        /// </summary>
        protected void ResetTradeSettings()
        {
            ToolsCall("optimizer_trade_settings_set", new
            {
                commission_type = "None",
                commission_value = 0m,
                order_execution_type = "Touch",
                slippage_to_simple_order = 0,
                slippage_to_stop_order = 0,
                start_deposit = 100000m
            });
        }

        /// <summary>
        /// Сбросить сопровождение позиции на дефолт.
        /// </summary>
        protected void ResetPositionSupport()
        {
            ToolsCall("optimizer_position_support_set", new { stop_is_on = false, profit_is_on = false });
        }

        /// <summary>
        /// Запустить оптимизацию и дождаться её окончания.
        /// </summary>
        protected bool StartAndWait(int timeoutSeconds)
        {
            string startResponse = ToolsCall("optimizer_start", new { });

            if (!TryParseJson(startResponse, out JsonElement startRoot)
                || !startRoot.TryGetProperty("started", out JsonElement startedElement)
                || startedElement.ValueKind != JsonValueKind.True)
            {
                string errors = startRoot.TryGetProperty("errors", out JsonElement errorsElement)
                    ? errorsElement.GetRawText()
                    : "(без списка ошибок)";

                Console.WriteLine($"  optimizer_start не запустился: {errors}");
                return false;
            }

            DateTime deadline = DateTime.Now.AddSeconds(timeoutSeconds);

            while (DateTime.Now < deadline)
            {
                string statusResponse = QuietCall("optimizer_get_status", new { });

                if (TryParseJson(statusResponse, out JsonElement statusRoot)
                    && statusRoot.TryGetProperty("is_running", out JsonElement runningElement)
                    && runningElement.ValueKind == JsonValueKind.False)
                {
                    Console.WriteLine("  Optimization finished.");
                    return true;
                }

                System.Threading.Thread.Sleep(1000);
            }

            return false;
        }

        /// <summary>
        /// Сводка отчёта оптимизации (число фаз/отчётов, прибыль первого отчёта, флаг прерывания).
        /// </summary>
        protected ReportSummary GetReportSummary()
        {
            ReportSummary summary = new ReportSummary();

            string response = QuietCall("optimizer_get_report", new { });

            if (!TryParseJson(response, out JsonElement root))
            {
                return summary;
            }

            summary.IsPartial = root.TryGetProperty("is_partial", out JsonElement partialElement)
                && partialElement.ValueKind == JsonValueKind.True;

            if (root.TryGetProperty("fazes_count", out JsonElement fazesCountElement)
                && fazesCountElement.ValueKind == JsonValueKind.Number)
            {
                summary.FazesCount = fazesCountElement.GetInt32();
            }

            if (root.TryGetProperty("reports_count", out JsonElement reportsCountElement)
                && reportsCountElement.ValueKind == JsonValueKind.Number)
            {
                summary.ReportsCount = reportsCountElement.GetInt32();
            }

            if (root.TryGetProperty("fazes", out JsonElement fazes)
                && fazes.ValueKind == JsonValueKind.Array
                && fazes.GetArrayLength() > 0)
            {
                JsonElement firstFaze = fazes[0];

                if (firstFaze.TryGetProperty("reports", out JsonElement reports)
                    && reports.ValueKind == JsonValueKind.Array
                    && reports.GetArrayLength() > 0)
                {
                    JsonElement firstReport = reports[0];

                    if (firstReport.TryGetProperty("total_profit", out JsonElement profitElement)
                        && profitElement.ValueKind == JsonValueKind.Number)
                    {
                        summary.FirstProfit = profitElement.GetDecimal();
                    }
                }
            }

            return summary;
        }

        #endregion

        #region Report summary

        public sealed class ReportSummary
        {
            public int FazesCount;
            public int ReportsCount;
            public decimal FirstProfit;
            public bool IsPartial;
        }

        #endregion

        #region Helper types

        public sealed class EnumParam
        {
            public string Name = string.Empty;
            public object? Start;
            public object? Stop;
            public object? Step;
        }

        public sealed class ParamInfo
        {
            public string Name = string.Empty;
            public string Type = string.Empty;
        }

        #endregion
    }
}
