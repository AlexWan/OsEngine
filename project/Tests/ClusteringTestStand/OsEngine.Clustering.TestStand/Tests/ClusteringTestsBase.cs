/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.IO;
using System.Text.Json;
using OsEngine.Clustering.TestStand;

namespace OsEngine.Clustering.TestStand.Tests
{
    /// <summary>
    /// Общая база тестов кластеризации («Аллокация капитала»): MCP-вызовы и разбор ответов.
    /// </summary>
    public abstract class ClusteringTestsBase
    {
        protected readonly TestContext _context;

        protected readonly string Module;

        protected ClusteringTestsBase(TestContext context, string module)
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

        #region Allocation helpers

        /// <summary>
        /// Открыть режим «Аллокация капитала» (terminal_open_mode → allocation_get_status is_open).
        /// </summary>
        protected bool OpenAllocationMode()
        {
            string response = ToolsCall("terminal_open_mode", new { mode = "allocation" });

            if (IsError(response))
            {
                RecordFail("terminal_open_mode", "не удалось открыть режим allocation: " + GetErrorText(response));
                return false;
            }

            if (!WaitForAllocationReady(30))
            {
                RecordFail("terminal_open_mode", "режим allocation не поднялся (allocation_get_status is_open=false)");
                return false;
            }

            RecordPass("terminal_open_mode", "режим allocation открыт");
            return true;
        }

        /// <summary>
        /// Дождаться, пока режим аллокации поднимется (is_open=true).
        /// </summary>
        protected bool WaitForAllocationReady(int timeoutSeconds)
        {
            DateTime deadline = DateTime.Now.AddSeconds(timeoutSeconds);

            while (DateTime.Now < deadline)
            {
                string response = QuietCall("allocation_get_status", new { });

                if (TryParseJson(response, out JsonElement status)
                    && status.TryGetProperty("is_open", out JsonElement isOpen)
                    && isOpen.ValueKind == JsonValueKind.True)
                {
                    return true;
                }

                System.Threading.Thread.Sleep(500);
            }

            return false;
        }

        /// <summary>
        /// Все *.txt-файлы ботов из папки данных стенда.
        /// </summary>
        protected string[] GetAllBotFiles()
        {
            string dir = _context.DataDir;

            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                return Array.Empty<string>();
            }

            return Directory.GetFiles(dir, "*.txt");
        }

        /// <summary>
        /// Текущее число загруженных ботов (allocation_bots_get).
        /// </summary>
        protected int GetBotCount()
        {
            string response = QuietCall("allocation_bots_get", new { });

            if (TryParseJson(response, out JsonElement root)
                && root.TryGetProperty("bots_count", out JsonElement count)
                && count.ValueKind == JsonValueKind.Number)
            {
                return count.GetInt32();
            }

            return 0;
        }

        /// <summary>
        /// Загрузить все боты из папки данных. Возвращает число загруженных.
        /// </summary>
        protected int SetAllBots()
        {
            string[] files = GetAllBotFiles();

            if (files.Length == 0)
            {
                return 0;
            }

            string response = QuietCall("allocation_bots_set", new { files = files });

            if (TryParseJson(response, out JsonElement root)
                && root.TryGetProperty("bots_count", out JsonElement count)
                && count.ValueKind == JsonValueKind.Number)
            {
                return count.GetInt32();
            }

            return 0;
        }

        /// <summary>
        /// Подготовка перед тестами: режим открыт + боты загружены (без PASS/FAIL — это не проверки).
        /// </summary>
        protected bool EnsureSetup()
        {
            if (!WaitForAllocationReady(2))
            {
                string response = QuietCall("terminal_open_mode", new { mode = "allocation" });

                if (!WaitForAllocationReady(30))
                {
                    return false;
                }
            }

            if (GetBotCount() == 0)
            {
                if (SetAllBots() == 0)
                {
                    return false;
                }
            }

            return true;
        }

        #endregion
    }
}
