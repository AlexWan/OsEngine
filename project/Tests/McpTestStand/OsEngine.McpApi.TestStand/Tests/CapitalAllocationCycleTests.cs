/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace OsEngine.McpApi.TestStand.Tests
{
    /// <summary>
    /// Полный цикл: аллокация капитала -> сохранение результата в файл -> переход в тестер ->
    /// создание робота -> загрузка аллокации -> проверка установленных объёмов.
    /// </summary>
    public class CapitalAllocationCycleTests
    {
        private const string Module = "CAPITAL_ALLOCATION_CYCLE";

        private const string StrategyName = "AlgoStart4Railway";

        private const string BotName = "3";

        private const string Shoulder = "2";

        private static readonly string TestDataDir =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData");

        private static readonly string ResultFile =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "allocation_cycle_test.txt");

        private readonly TestContext _context;

        public CapitalAllocationCycleTests(TestContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void RunAll()
        {
            _context.PrintModuleHeader(Module);

            TestOpenAllocation();
            TestSaveResult();

            RestartInTester();

            TestCreateRobot();
            TestLoadApplyAndVerify();
        }

        private void TestOpenAllocation()
        {
            const string method = "terminal_open_mode";
            object request = new { mode = "allocation" };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!WaitForAllocationOpen())
                {
                    return;
                }

                _context.RecordPass(Module, method, "allocation mode opened");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private bool WaitForAllocationOpen()
        {
            const string method = "allocation_get_status";
            DateTime deadline = DateTime.Now.AddSeconds(30);

            while (DateTime.Now < deadline)
            {
                try
                {
                    string response = _context.Client.ToolsCall(method, new { });

                    using (var document = JsonDocument.Parse(response))
                    {
                        if (document.RootElement.TryGetProperty("IsError", out JsonElement isError) && !isError.GetBoolean()
                            && document.RootElement.TryGetProperty("Content", out JsonElement content) && content.GetArrayLength() > 0)
                        {
                            string text = content[0].GetProperty("Text").GetString() ?? string.Empty;

                            using (var inner = JsonDocument.Parse(text))
                            {
                                if (inner.RootElement.TryGetProperty("is_open", out JsonElement isOpen)
                                    && isOpen.ValueKind == JsonValueKind.True)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // ignore and retry
                }

                Thread.Sleep(500);
            }

            _context.RecordFail(Module, "allocation_get_status", "allocation mode is not available after open");
            return false;
        }

        private void TestSaveResult()
        {
            string botFile = Path.Combine(TestDataDir, "3_Deals.txt");

            if (!SetBots(botFile))
            {
                return;
            }

            if (!Calculate())
            {
                return;
            }

            SaveResult();
        }

        private bool SetBots(string botFile)
        {
            const string method = "allocation_bots_set";
            object request = new { files = new[] { botFile } };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return false;
                }

                if (!config.TryGetProperty("bots_count", out JsonElement botsCount) || botsCount.GetInt32() != 1)
                {
                    _context.RecordFail(Module, method, $"bots_count is not 1");
                    return false;
                }

                _context.RecordPass(Module, method, "bots_count=1");
                return true;
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
                return false;
            }
        }

        private bool Calculate()
        {
            const string method = "allocation_clustering_calculate";
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

                if (!config.TryGetProperty("calculated", out JsonElement calculated) || !calculated.GetBoolean())
                {
                    _context.RecordFail(Module, method, "calculated is not true");
                    return false;
                }

                _context.RecordPass(Module, method, "calculated");
                return true;
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
                return false;
            }
        }

        private bool SaveResult()
        {
            const string method = "allocation_result_save";
            object request = new { file_path = ResultFile };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return false;
                }

                if (!config.TryGetProperty("saved", out JsonElement saved) || !saved.GetBoolean())
                {
                    _context.RecordFail(Module, method, "saved is not true");
                    return false;
                }

                _context.RecordPass(Module, method, "saved");
                return true;
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
                return false;
            }
        }

        private void RestartInTester()
        {
            try
            {
                _context.RestartOsEngine("-tester");
                Thread.Sleep(2000);
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, "restart", error.Message);
            }
        }

        private void TestCreateRobot()
        {
            const string method = "bot_create";
            object request = new { strategy_name = StrategyName, name = BotName };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (response.IndexOf("already exists", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _context.RecordPass(Module, method, "robot already exists");
                    return;
                }

                if (TryParseConfig(response, method, out JsonElement config))
                {
                    _context.RecordPass(Module, method, "robot created");
                    return;
                }

                _context.RecordFail(Module, method, "robot not created");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestLoadApplyAndVerify()
        {
            decimal expectedVolume = LoadApply();

            if (expectedVolume < 0)
            {
                return;
            }

            VerifyVolume(expectedVolume);
        }

        private decimal LoadApply()
        {
            const string method = "allocation_load_apply";
            object request = new { file_path = ResultFile, shoulder = Shoulder };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return -1;
                }

                if (!config.TryGetProperty("applied", out JsonElement applied)
                    || applied.ValueKind != JsonValueKind.Array
                    || applied.GetArrayLength() == 0)
                {
                    _context.RecordFail(Module, method, "applied is empty");
                    return -1;
                }

                decimal volume = -1;

                foreach (JsonElement item in applied.EnumerateArray())
                {
                    if (item.TryGetProperty("bot_name", out JsonElement botName)
                        && botName.GetString() == BotName
                        && item.TryGetProperty("volume", out JsonElement volumeElement))
                    {
                        volume = volumeElement.GetDecimal();
                        break;
                    }
                }

                if (volume < 0)
                {
                    _context.RecordFail(Module, method, $"bot '{BotName}' not found in applied list");
                    return -1;
                }

                _context.RecordPass(Module, method, $"applied volume={volume}");
                return volume;
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
                return -1;
            }
        }

        private void VerifyVolume(decimal expectedVolume)
        {
            const string method = "bot_get_params";
            object request = new { bot_id = BotName };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return;
                }

                if (!config.TryGetProperty("parameters", out JsonElement parameters)
                    || parameters.ValueKind != JsonValueKind.Array)
                {
                    _context.RecordFail(Module, method, "parameters is not an array");
                    return;
                }

                foreach (JsonElement param in parameters.EnumerateArray())
                {
                    if (param.TryGetProperty("name", out JsonElement name)
                        && name.GetString() == "Volume"
                        && param.TryGetProperty("value", out JsonElement value))
                    {
                        decimal actual = value.GetDecimal();

                        if (Math.Abs(actual - expectedVolume) < 0.001m)
                        {
                            _context.RecordPass(Module, method, $"volume={actual}");
                        }
                        else
                        {
                            _context.RecordFail(Module, method, $"volume mismatch: expected={expectedVolume}, actual={actual}");
                        }

                        return;
                    }
                }

                _context.RecordFail(Module, method, "Volume parameter not found");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private bool TryParseConfig(string response, string method, out JsonElement config)
        {
            config = default;

            try
            {
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
            catch (Exception error)
            {
                _context.RecordFail(Module, method, error.Message);
                return false;
            }
        }
    }
}
