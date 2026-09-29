/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;

namespace OsEngine.OsData.TestStand.Tests
{
    /// <summary>
    /// Модуль Security (бумаги) — глава 4.1 контекста стенда.
    /// Проверяет список бумаг коннектора: нет дублей по имени, уникальны идентификаторы.
    /// </summary>
    public class SecurityTests
    {
        private const string Module = "SECURITY";

        private readonly TestContext _context;

        public SecurityTests(TestContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void RunAll()
        {
            _context.PrintModuleHeader(Module);

            if (OpenOsDataMode() == false)
            {
                return;
            }

            string type = _context.ConnectorType;

            List<SecurityItem> securities = LoadSecurities(type);

            if (securities == null)
            {
                return;
            }

            TestNoDuplicateSecurities(securities);
            TestUniqueIds(securities);
        }

        /// <summary>
        /// Перевести терминал в режим OsData и дождаться, пока data_get_sets начнёт отдавать массив.
        /// </summary>
        private bool OpenOsDataMode()
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
        /// Активировать коннектор, подключить, дождаться загрузки списка бумаг.
        /// </summary>
        private List<SecurityItem> LoadSecurities(string type)
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

        private static List<SecurityItem> ParseSecurities(JsonElement securitiesElement)
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

        // 4.1.1. Отсутствие дублей бумаг — ключ «имя + класс».
        private void TestNoDuplicateSecurities(List<SecurityItem> securities)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> duplicates = new List<string>();

            foreach (SecurityItem security in securities)
            {
                if (string.IsNullOrWhiteSpace(security.Name))
                {
                    continue;
                }

                string key = security.Name + " | " + security.NameClass;

                if (seen.Add(key) == false && duplicates.Contains(key) == false)
                {
                    duplicates.Add(key);
                }
            }

            if (duplicates.Count == 0)
            {
                _context.RecordPass(Module, "no_duplicate_securities", $"бумаг: {securities.Count}, дублей (имя+класс) нет");
            }
            else
            {
                _context.RecordFail(Module, "no_duplicate_securities", "дубли (имя+класс): " + string.Join("; ", duplicates));
            }
        }

        // 4.1.2. Уникальность идентификаторов (nameId).
        private void TestUniqueIds(List<SecurityItem> securities)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> duplicates = new List<string>();
            int emptyIds = 0;

            foreach (SecurityItem security in securities)
            {
                if (string.IsNullOrWhiteSpace(security.NameId))
                {
                    emptyIds++;
                    continue;
                }

                if (seen.Add(security.NameId) == false && duplicates.Contains(security.NameId) == false)
                {
                    duplicates.Add(security.NameId);
                }
            }

            if (duplicates.Count == 0 && emptyIds == 0)
            {
                _context.RecordPass(Module, "unique_ids", $"уникальных name_id: {seen.Count}, дублей/пустых нет");
            }
            else if (duplicates.Count > 0)
            {
                _context.RecordFail(Module, "unique_ids", "дубли name_id: " + string.Join(", ", duplicates));
            }
            else
            {
                _context.RecordFail(Module, "unique_ids", $"пустых name_id: {emptyIds}");
            }
        }

        private static string GetString(JsonElement item, string name)
        {
            if (item.TryGetProperty(name, out JsonElement element) && element.ValueKind == JsonValueKind.String)
            {
                return element.GetString() ?? string.Empty;
            }

            return string.Empty;
        }

        /// <summary>
        /// Достаёт внутренний JSON (Content[0].Text) из ответа tools/call.
        /// </summary>
        private static bool TryGetInnerText(string response, out string text)
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
                // не разобрать ответ
            }

            return false;
        }

        private class SecurityItem
        {
            public string Name = string.Empty;

            public string NameId = string.Empty;

            public string NameClass = string.Empty;
        }
    }
}
