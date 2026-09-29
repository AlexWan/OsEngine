/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace OsEngine.OsData.TestStand.Tests
{
    /// <summary>
    /// Общая часть модулей данных (Security/Candles/Ticks/...).
    /// Содержит общий поток: открыть OsData, погасить сеты, получить бумаги,
    /// выбрать представителей по классам, скачать и подождать загрузку.
    /// </summary>
    public abstract class OsDataTestsBase
    {
        /// <summary>
        /// Ликвидные тикеры-представители. Для каждого класса сначала ищем совпадение
        /// с этим списком (валюта → CNYRUB, акции → SBER и т.д.), иначе берём первую бумагу.
        /// </summary>
        protected static readonly string[] PreferredTickers = new[]
        {
            "CNYRUB_TOM", "CNYRUB_TOD", "CNYRUB", "USDRUB_TOD", "USDRUB_TOM", "USDRUB", "EURRUB_TOD", "EURRUB_TOM", "EURRUB",
            "SBER", "GAZP", "LKOH", "VTBR", "IMOEX",
            "TMOS", "SBMX", "LQDT",
            "BTCUSDT", "ETHUSDT", "AAPL", "SPY"
        };

        /// <summary>Базовые активы фьючерсов — для них берём ближайший не истёкший контракт.</summary>
        protected static readonly string[] FuturesUnderlyings = new[] { "Si", "GZ", "BR", "RTS", "MX", "CR" };

        protected readonly TestContext _context;
        protected readonly string Module;

        protected OsDataTestsBase(TestContext context, string module)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            Module = module ?? throw new ArgumentNullException(nameof(module));
        }

        /// <summary>
        /// Точечный режим: возвращает true, если задан --security; в target — найденная бумага
        /// (null, если не нашлась). В обычном режиме возвращает false.
        /// </summary>
        protected bool IsTargeted(List<SecurityItem> securities, out SecurityItem target)
        {
            target = null;

            if (string.IsNullOrEmpty(_context.TargetSecurity))
            {
                return false;
            }

            target = securities.Find(s =>
                s.Name.Equals(_context.TargetSecurity, StringComparison.OrdinalIgnoreCase));

            return true;
        }

        protected DateTime TargetDateFrom(DateTime defaultFrom)
        {
            return _context.TargetDateFrom ?? defaultFrom;
        }

        protected DateTime TargetDateTo(DateTime defaultTo)
        {
            return _context.TargetDateTo ?? defaultTo;
        }

        protected bool OpenOsDataMode()
        {
            try
            {
                _context.Client.ToolsCall("terminal_open_mode", new { mode = "data" });

                DateTime deadline = DateTime.Now.AddSeconds(30);

                while (DateTime.Now < deadline)
                {
                    string sets = _context.Client.ToolsCall("data_get_sets", new { });

                    if (TryGetInnerText(sets, out string text))
                    {
                        using (JsonDocument doc = JsonDocument.Parse(text))
                        {
                            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                            {
                                return true;
                            }
                        }
                    }

                    Thread.Sleep(500);
                }

                _context.RecordFail(Module, "open_osdata", "режим OsData не открылся за 30 сек");
                return false;
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, "open_osdata", error.Message);
                return false;
            }
        }

        /// <summary>
        /// Гасит все существующие сеты OsData на старте — чтобы чужие/остаточные сеты не мешали.
        /// </summary>
        protected void DisableAllSets()
        {
            try
            {
                string response = _context.Client.ToolsCall("data_get_sets", new { });

                if (TryGetInnerText(response, out string text) == false)
                {
                    return;
                }

                using (JsonDocument doc = JsonDocument.Parse(text))
                {
                    if (doc.RootElement.ValueKind != JsonValueKind.Array)
                    {
                        return;
                    }

                    foreach (JsonElement item in doc.RootElement.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.Object
                            || item.TryGetProperty("name", out JsonElement nameElement) == false
                            || nameElement.ValueKind != JsonValueKind.String)
                        {
                            continue;
                        }

                        string name = nameElement.GetString() ?? string.Empty;
                        string plain = name.StartsWith("Set_", StringComparison.OrdinalIgnoreCase)
                            ? name.Substring(4)
                            : name;

                        try
                        {
                            _context.Client.ToolsCall("data_set_off", new { name = plain });
                        }
                        catch
                        {
                            // не удалось выключить — не критично
                        }
                    }
                }
            }
            catch
            {
                // игнорируем ошибки очистки
            }
        }

        protected List<SecurityItem> LoadSecurities(string type)
        {
            try
            {
                _context.Client.ToolsCall("server_management_activate", new { type });
                _context.Client.ToolsCall("server_instance_connect", new { type });

                DateTime deadline = DateTime.Now.AddSeconds(120);

                while (DateTime.Now < deadline)
                {
                    string response = _context.Client.ToolsCall("server_instance_get_securities", new { type });

                    if (TryGetInnerText(response, out string text))
                    {
                        using (JsonDocument doc = JsonDocument.Parse(text))
                        {
                            if (doc.RootElement.TryGetProperty("securities", out JsonElement securitiesElement)
                                && securitiesElement.ValueKind == JsonValueKind.Array
                                && securitiesElement.GetArrayLength() > 0)
                            {
                                return ParseSecurities(securitiesElement);
                            }
                        }
                    }

                    Thread.Sleep(2000);
                }

                _context.RecordFail(Module, "get_securities", $"коннектор '{type}' не дал список бумаг за 120 сек");
                return null;
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, "get_securities", error.Message);
                return null;
            }
        }

        protected List<string> GetTimeframes(string type)
        {
            try
            {
                string response = _context.Client.ToolsCall("server_management_get_data_timeframes", new { type });

                if (TryGetInnerText(response, out string text) == false)
                {
                    return null;
                }

                using (JsonDocument doc = JsonDocument.Parse(text))
                {
                    if (doc.RootElement.TryGetProperty("timeframes", out JsonElement timeframesElement)
                        && timeframesElement.ValueKind == JsonValueKind.Array)
                    {
                        List<string> timeframes = new List<string>();

                        foreach (JsonElement item in timeframesElement.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String)
                            {
                                timeframes.Add(item.GetString() ?? string.Empty);
                            }
                        }

                        timeframes.RemoveAll(t => string.IsNullOrWhiteSpace(t) || t == "MarketDepthHistory" || t == "MarketDepth");
                        return timeframes;
                    }
                }

                return null;
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, "get_timeframes", error.Message);
                return null;
            }
        }

        protected List<SecurityItem> PickRepresentatives(List<SecurityItem> securities)
        {
            Dictionary<string, List<SecurityItem>> byClass = new Dictionary<string, List<SecurityItem>>(StringComparer.OrdinalIgnoreCase);

            foreach (SecurityItem security in securities)
            {
                if (string.IsNullOrWhiteSpace(security.Name))
                {
                    continue;
                }

                string cls = string.IsNullOrWhiteSpace(security.NameClass) ? "(без класса)" : security.NameClass;

                if (byClass.ContainsKey(cls) == false)
                {
                    byClass[cls] = new List<SecurityItem>();
                }

                byClass[cls].Add(security);
            }

            List<SecurityItem> result = new List<SecurityItem>();

            foreach (KeyValuePair<string, List<SecurityItem>> pair in byClass)
            {
                SecurityItem chosen = null;

                // Сначала фьючерсы — берём ближайший не истёкший контракт,
                // чтобы тикер акции ("LKOH") не подхватил старый фьючерс ("LKOH-12.15_FT").
                foreach (string underlying in FuturesUnderlyings)
                {
                    chosen = PickFutures(pair.Value, underlying);
                    if (chosen != null) break;
                }

                if (chosen == null)
                {
                    foreach (string preferred in PreferredTickers)
                    {
                        chosen = pair.Value.Find(s =>
                            s.Name.Equals(preferred, StringComparison.OrdinalIgnoreCase)
                            || s.Name.StartsWith(preferred, StringComparison.OrdinalIgnoreCase));

                        if (chosen != null)
                        {
                            break;
                        }
                    }
                }

                if (chosen != null)
                {
                    result.Add(chosen);
                }
            }

            result.Sort((a, b) => string.Compare(a.NameClass, b.NameClass, StringComparison.OrdinalIgnoreCase));

            return result;
        }

        /// <summary>
        /// Для фьючерсов (тикер вида GZU6) берём ближайший не истёкший контракт;
        /// если таких нет — самый дальний (хоть какой-то с данными).
        /// </summary>
        protected static SecurityItem PickFutures(List<SecurityItem> securities, string underlying)
        {
            List<SecurityItem> matches = securities.FindAll(s => s.Name.StartsWith(underlying, StringComparison.OrdinalIgnoreCase));

            if (matches.Count == 0)
            {
                return null;
            }

            DateTime today = DateTime.Now.Date;
            DateTime horizon = today.AddMonths(6);

            SecurityItem best = null;
            DateTime? bestExpiry = null;

            // ближайший не истёкший контракт, но не дальше горизонта (полгода) —
            // дальние контракты (SiH9 и т.п.) ещё без данных
            foreach (SecurityItem security in matches)
            {
                DateTime? expiry = ParseFuturesExpiry(security.Name);

                if (expiry == null)
                {
                    continue;
                }

                if (expiry.Value >= today && expiry.Value <= horizon)
                {
                    if (bestExpiry == null || expiry.Value < bestExpiry.Value)
                    {
                        bestExpiry = expiry;
                        best = security;
                    }
                }
            }

            return best;
        }

        protected static DateTime? ParseFuturesExpiry(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length < 3)
            {
                return null;
            }

            char yearChar = name[name.Length - 1];
            char monthChar = char.ToUpper(name[name.Length - 2]);

            if (yearChar < '0' || yearChar > '9')
            {
                return null;
            }

            int month = 0;

            switch (monthChar)
            {
                case 'F': month = 1; break;
                case 'G': month = 2; break;
                case 'H': month = 3; break;
                case 'J': month = 4; break;
                case 'K': month = 5; break;
                case 'M': month = 6; break;
                case 'N': month = 7; break;
                case 'Q': month = 8; break;
                case 'U': month = 9; break;
                case 'V': month = 10; break;
                case 'X': month = 11; break;
                case 'Z': month = 12; break;
                default: return null;
            }

            int year = 2020 + (yearChar - '0');

            return new DateTime(year, month, 1);
        }

        protected bool WaitForLoad(string setName, int timeoutSeconds = 120)
        {
            DateTime deadline = DateTime.Now.AddSeconds(timeoutSeconds);
            int zeroSeconds = 0;

            while (DateTime.Now < deadline)
            {
                string statusResponse = _context.Client.ToolsCall("data_get_set_status", new { name = setName });

                if (TryGetInnerText(statusResponse, out string text))
                {
                    using (JsonDocument doc = JsonDocument.Parse(text))
                    {
                        if (doc.RootElement.TryGetProperty("percent_load", out JsonElement percentElement)
                            && percentElement.ValueKind == JsonValueKind.Number)
                        {
                            decimal percent = percentElement.GetDecimal();

                            if (percent >= 100m)
                            {
                                return true;
                            }

                            // данные не идут: процент держится на нуле — обрываем, не ждём до бесконечности
                            if (percent > 0m)
                            {
                                zeroSeconds = 0;
                            }
                            else
                            {
                                zeroSeconds += 3;
                            }
                        }
                    }
                }

                if (zeroSeconds >= 45)
                {
                    return false;
                }

                Thread.Sleep(3000);
            }

            return false;
        }

        protected void CleanupSet(string setName)
        {
            try
            {
                _context.Client.ToolsCall("data_set_off", new { name = setName });
            }
            catch
            {
                // сета может уже не быть
            }

            try
            {
                _context.Client.ToolsCall("data_delete_set", new { name = setName });
            }
            catch
            {
                // сета может уже не быть
            }
        }

        protected static List<SecurityItem> ParseSecurities(JsonElement securitiesElement)
        {
            List<SecurityItem> result = new List<SecurityItem>();

            foreach (JsonElement item in securitiesElement.EnumerateArray())
            {
                SecurityItem security = new SecurityItem();
                security.Name = GetString(item, "name");
                security.NameId = GetString(item, "nameId");
                security.NameClass = GetString(item, "nameClass");
                result.Add(security);
            }

            return result;
        }

        protected static string RemoveExcess(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return name;
            }

            return name
                .Replace("/", "")
                .Replace("\\", "")
                .Replace("*", "")
                .Replace(":", "")
                .Replace("@", "")
                .Replace(";", "")
                .Replace("\"", "");
        }

        protected static string GetString(JsonElement item, string name)
        {
            if (item.TryGetProperty(name, out JsonElement element) && element.ValueKind == JsonValueKind.String)
            {
                return element.GetString() ?? string.Empty;
            }

            return string.Empty;
        }

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

                    if (root.TryGetProperty("IsError", out JsonElement isError) && isError.ValueKind == JsonValueKind.True)
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
                // не разобрать ответ
            }

            return false;
        }

        protected static bool IsError(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                return true;
            }

            try
            {
                using (JsonDocument doc = JsonDocument.Parse(response))
                {
                    return doc.RootElement.TryGetProperty("IsError", out JsonElement isError)
                        && isError.ValueKind == JsonValueKind.True;
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
                        return content[0].GetProperty("Text").GetString() ?? "unknown";
                    }
                }
            }
            catch
            {
                // не разобрать
            }

            return response;
        }

        protected class SecurityItem
        {
            public string Name = string.Empty;

            public string NameId = string.Empty;

            public string NameClass = string.Empty;
        }
    }
}
