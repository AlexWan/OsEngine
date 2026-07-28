/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Text.Json;
using System.Threading;

namespace OsEngine.McpApi.TestStand.Tests
{
    /// <summary>
    /// Tests for system load tools (system_load_*).
    /// </summary>
    public class SystemLoadTests
    {
        private const string Module = "SYSTEMLOAD";
        private readonly TestContext _context;

        private bool _originalRamIsOn;
        private string _originalRamPeriod = "Minute";
        private bool _originalCpuIsOn;
        private string _originalCpuPeriod = "Minute";

        public SystemLoadTests(TestContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void RunAll()
        {
            _context.PrintModuleHeader(Module);

            try
            {
                if (!TestGetSettings())
                {
                    return;
                }

                if (!TestSetSettings())
                {
                    return;
                }

                TestGetCurrent();
                TestGetHistory();
            }
            finally
            {
                RestoreSettings();
            }
        }

        private bool TestGetSettings()
        {
            const string method = "system_load_get_settings";
            object request = new { };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return false;
                }

                string[] sections = new[] { "ram", "cpu", "ecq", "moq" };

                foreach (string section in sections)
                {
                    if (!config.TryGetProperty(section, out JsonElement sectionElement)
                        || !sectionElement.TryGetProperty("collect_data_is_on", out _)
                        || !sectionElement.TryGetProperty("period", out _)
                        || !sectionElement.TryGetProperty("points_max", out _))
                    {
                        _context.RecordFail(Module, method, $"section '{section}' is incomplete");
                        return false;
                    }
                }

                JsonElement ram = config.GetProperty("ram");
                _originalRamIsOn = ram.GetProperty("collect_data_is_on").GetBoolean();
                _originalRamPeriod = ram.GetProperty("period").GetString() ?? "Minute";

                JsonElement cpu = config.GetProperty("cpu");
                _originalCpuIsOn = cpu.GetProperty("collect_data_is_on").GetBoolean();
                _originalCpuPeriod = cpu.GetProperty("period").GetString() ?? "Minute";

                _context.RecordPass(Module, method, "settings received");
                return true;
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
                return false;
            }
        }

        private bool TestSetSettings()
        {
            const string method = "system_load_set_settings";
            object request = new
            {
                ram_collect_data_is_on = true,
                ram_period = "OneSecond",
                cpu_collect_data_is_on = true,
                cpu_period = "OneSecond"
            };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return false;
                }

                JsonElement ram = config.GetProperty("ram");
                JsonElement cpu = config.GetProperty("cpu");

                if (ram.GetProperty("collect_data_is_on").GetBoolean() != true
                    || ram.GetProperty("period").GetString() != "OneSecond"
                    || cpu.GetProperty("collect_data_is_on").GetBoolean() != true
                    || cpu.GetProperty("period").GetString() != "OneSecond")
                {
                    _context.RecordFail(Module, method, "settings were not applied");
                    return false;
                }

                _context.RecordPass(Module, method, "ram and cpu collection enabled");
                return true;
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
                return false;
            }
        }

        private void TestGetCurrent()
        {
            const string method = "system_load_get_current";
            object request = new { };

            // ждём, пока сбор с периодом в секунду запишет первые точки:
            // старт рабочего потока и инициализация счётчиков занимают время
            Thread.Sleep(10000);

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return;
                }

                if (config.GetProperty("ram_collect_data_is_on").GetBoolean() != true
                    || config.GetProperty("cpu_collect_data_is_on").GetBoolean() != true)
                {
                    _context.RecordFail(Module, method, "collection flags mismatch");
                    return;
                }

                if (config.GetProperty("ram_time").ValueKind == JsonValueKind.Null
                    || config.GetProperty("ram_program_percent").GetDecimal() <= 0)
                {
                    _context.RecordFail(Module, method, "ram point missing after 10s of collection");
                    return;
                }

                if (config.GetProperty("cpu_time").ValueKind == JsonValueKind.Null)
                {
                    _context.RecordFail(Module, method, "cpu point missing after 10s of collection");
                    return;
                }

                _context.RecordPass(Module, method, $"ram={config.GetProperty("ram_program_percent").GetDecimal()}%");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestGetHistory()
        {
            const string method = "system_load_get_history";
            object request = new { type = "Ram", limit = 10 };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return;
                }

                if (config.GetProperty("count").GetInt32() == 0)
                {
                    _context.RecordFail(Module, method, "ram history is empty");
                    return;
                }

                JsonElement firstPoint = config.GetProperty("points")[0];

                if (!firstPoint.TryGetProperty("time", out _)
                    || !firstPoint.TryGetProperty("program_percent", out _)
                    || !firstPoint.TryGetProperty("system_percent", out _))
                {
                    _context.RecordFail(Module, method, "ram history point is incomplete");
                    return;
                }

                // Moq без подключённого сервера: проверяем только структуру ответа
                object moqRequest = new { type = "Moq" };
                string moqResponse = _context.Client.ToolsCall(method, moqRequest);

                if (!TryParseConfig(moqResponse, method, out JsonElement moqConfig)
                    || !moqConfig.TryGetProperty("count", out _)
                    || !moqConfig.TryGetProperty("points", out _))
                {
                    _context.RecordFail(Module, method, "moq history response is incomplete");
                    return;
                }

                _context.RecordPass(Module, method, $"ram_points={config.GetProperty("count").GetInt32()}");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void RestoreSettings()
        {
            try
            {
                object request = new
                {
                    ram_collect_data_is_on = _originalRamIsOn,
                    ram_period = _originalRamPeriod,
                    cpu_collect_data_is_on = _originalCpuIsOn,
                    cpu_period = _originalCpuPeriod
                };

                _context.Client.ToolsCall("system_load_set_settings", request);
            }
            catch
            {
                // восстановление настроек не должно ронять модуль
            }
        }

        private bool TryParseConfig(string response, string method, out JsonElement config)
        {
            config = default;

            using (var document = JsonDocument.Parse(response))
            {
                JsonElement result = document.RootElement;

                if (!result.TryGetProperty("IsError", out JsonElement isError) || isError.GetBoolean())
                {
                    _context.RecordFail(Module, method, "IsError is true");
                    return false;
                }

                if (!result.TryGetProperty("Content", out JsonElement content) || content.GetArrayLength() == 0)
                {
                    _context.RecordFail(Module, method, "Content is empty");
                    return false;
                }

                string text = content[0].GetProperty("Text").GetString() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(text))
                {
                    _context.RecordFail(Module, method, "Content text is empty");
                    return false;
                }

                using (var innerDocument = JsonDocument.Parse(text))
                {
                    config = innerDocument.RootElement.Clone();
                    return true;
                }
            }
        }
    }
}
