/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace StopOrdersTestStand.Tests
{
    /// <summary>
    /// Module 1. Backward compatibility of position journal saves.
    /// A reference DealController file in the old format (order strings
    /// without the trailing PriceCondition field) is placed into the Engine
    /// folder before the robot is created. The robot tab journal
    /// (PositionController) loads the file on creation, then the loaded
    /// positions are verified through the MCP API and the journal file is
    /// checked to survive a full engine restart without losses.
    /// </summary>
    public class Module1_SaveCompatibilityTests
    {
        private const string Module = "SAVECOMPATIBILITY";

        // Robot and tab names: the journal file name is built from the tab name,
        // see PositionController ("Engine\" + name + "DealController.txt") and
        // BotPanel tab naming ("{NameStrategyUniq}tab{number}").
        private const string StrategyName = "TwoTimeFramesBot";
        private const string BotName = "StopSaveCompatBot";
        private const string TabName = BotName + "tab0";

        private readonly TestContext _context;
        private readonly string _referenceFilePath;

        private string _createdBotName = string.Empty;

        public Module1_SaveCompatibilityTests(TestContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

            string engineDir = Path.GetDirectoryName(_context.OsEnginePath) ?? string.Empty;
            _referenceFilePath = Path.Combine(engineDir, "Engine", TabName + "DealController.txt");
        }

        public void RunAll()
        {
            _context.PrintModuleHeader(Module);

            try
            {
                if (!WaitRobotMaster())
                {
                    return;
                }

                PreCleanup();

                if (!WriteReferenceFile())
                {
                    return;
                }

                _context.RestartOsEngine("-robotslight");

                if (!WaitRobotMaster())
                {
                    return;
                }

                if (!CreateBot())
                {
                    return;
                }

                if (!VerifyPositionsLoaded("positions_loaded"))
                {
                    return;
                }

                VerifyFilePreserved();

                if (!VerifyReloadAfterRestart())
                {
                    return;
                }
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, "RunAll", error.Message);
            }
            finally
            {
                Cleanup();
            }
        }

        #region Reference file

        // Open order of position 1: Buy, volume 3, fully executed.
        // Old format: 23 '@'-fields, ends with "IsSendToCancel&count&time",
        // no trailing PriceCondition field (see Order.GetStringForSave / SetOrderFromString).
        private const string OpenOrder1 =
            "1@Tester@123@Buy@100,5@100,5@3@3@Done@Limit@20.01.2025 10:00:00@SBER@TesterPortfolio@" +
            "20.01.2025 10:00:00@20.01.2025 10:00:00@20.01.2025 10:00:00@00:00:00@null@@20.01.2025 10:00:01@" +
            "GTC@Tester@False&0&01/20/2025 10:00:00";

        // Open order of position 2: Sell, volume 5, fully executed.
        private const string OpenOrder2 =
            "2@Tester@124@Sell@99,5@99,5@5@5@Done@Limit@20.01.2025 10:00:00@SBER@TesterPortfolio@" +
            "20.01.2025 10:00:00@20.01.2025 10:00:00@20.01.2025 10:00:00@00:00:00@null@@20.01.2025 10:00:01@" +
            "GTC@Tester@False&0&01/20/2025 10:00:00";

        // Close order of position 2: Buy, volume 2, fully executed. Open volume stays 5 - 2 = 3.
        private const string CloseOrder2 =
            "3@Tester@125@Buy@100@100@2@2@Done@Limit@20.01.2025 10:05:00@SBER@TesterPortfolio@" +
            "20.01.2025 10:05:00@20.01.2025 10:05:00@20.01.2025 10:05:00@00:00:00@null@@20.01.2025 10:05:01@" +
            "GTC@Tester@False&0&01/20/2025 10:05:00";

        // Position 1: Buy, open, stop 98,5 / red line 98, profit 105 / red line 106, no close orders.
        private const string Position1 =
            "Buy#Open#" + TabName + "#0#0#" + OpenOrder1 + "^#1#^^#" +
            "True^False^0^0#98,5#98#True^False^0^0#105#1^0^0#1#0,1#10000#106###0#None#True#False#SBER";

        // Position 2: Sell, open, stop 101,5 / red line 102, profit 95 / red line 94, one close order.
        private const string Position2 =
            "Sell#Open#" + TabName + "#0#0#" + OpenOrder2 + "^#2#^^#" +
            "True^False^0^0#101,5#102#True^False^0^0#95#1^0^0#1#0,1#10000#94###0#None#" +
            CloseOrder2 + "#True#False#SBER";

        private static string BuildReferenceContent()
        {
            StringBuilder result = new StringBuilder();

            // first two lines: CommissionType and CommissionValue (see PositionController.Load)
            result.Append("None\r\n");
            result.Append("0\r\n");
            result.Append(Position1 + "\r\n");
            result.Append(Position2 + "\r\n");

            return result.ToString();
        }

        #endregion

        #region Test steps

        // MCP API отвечает раньше, чем создаётся Robot master. После (пере)запуска
        // движка ждём, пока robot-инструменты начнут работать, иначе первый вызов
        // вернёт IsError "Robot master is not available"
        private bool WaitRobotMaster()
        {
            DateTime deadline = DateTime.Now.AddSeconds(90);

            while (DateTime.Now < deadline)
            {
                try
                {
                    string response = _context.Client.ToolsCall("bot_get_list", new { });

                    using (JsonDocument document = JsonDocument.Parse(response))
                    {
                        if (document.RootElement.TryGetProperty("IsError", out JsonElement isError)
                            && isError.GetBoolean() == false)
                        {
                            return true;
                        }
                    }
                }
                catch
                {
                    // engine is still starting. Wait
                }

                System.Threading.Thread.Sleep(1000);
            }

            _context.RecordFail(Module, "wait_robot_master", "robot master is not available 90 seconds after engine start");
            return false;
        }

        private void PreCleanup()
        {
            try
            {
                string listResponse = _context.Client.ToolsCall("bot_get_list", new { });

                if (TryParseConfig(listResponse, "bot_get_list", out JsonElement config)
                    && config.TryGetProperty("bots", out JsonElement bots))
                {
                    foreach (JsonElement bot in bots.EnumerateArray())
                    {
                        if (bot.TryGetProperty("name", out JsonElement name)
                            && name.GetString() == BotName)
                        {
                            _context.Client.ToolsCall("bot_delete", new { bot_id = BotName });
                            break;
                        }
                    }
                }

                if (File.Exists(_referenceFilePath))
                {
                    File.Delete(_referenceFilePath);
                }
            }
            catch (Exception error)
            {
                // pre-cleanup must not fail the module
                Console.WriteLine($"[{Module}] PreCleanup failed: {error.Message}");
            }
        }

        private bool WriteReferenceFile()
        {
            const string method = "reference_file_write";

            try
            {
                // stop the engine so it cannot hold or overwrite the journal file
                _context.StopOsEngine();

                string content = BuildReferenceContent();
                File.WriteAllText(_referenceFilePath, content, new UTF8Encoding(false));

                _context.RecordPass(Module, method, $"reference file written: {_referenceFilePath}");
                return true;
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, method, error.Message);
                return false;
            }
        }

        private bool CreateBot()
        {
            const string method = "bot_create";

            try
            {
                object request = new { strategy_name = StrategyName, name = BotName };

                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, method, out JsonElement config))
                {
                    return false;
                }

                string createdName = config.GetProperty("name").GetString() ?? string.Empty;

                if (createdName != BotName)
                {
                    _context.RecordFail(Module, method, $"created robot name mismatch: {createdName}");
                    return false;
                }

                _createdBotName = createdName;

                // verify the tab name matches the journal file naming convention
                string sourcesResponse = _context.Client.ToolsCall("bot_get_sources", new { bot_id = _createdBotName });

                if (!TryParseConfig(sourcesResponse, "bot_get_sources", out JsonElement sourcesConfig))
                {
                    return false;
                }

                foreach (JsonElement source in sourcesConfig.GetProperty("sources").EnumerateArray())
                {
                    if (source.TryGetProperty("type", out JsonElement type)
                        && type.GetString() == "Simple"
                        && source.TryGetProperty("name", out JsonElement name)
                        && name.GetString() == TabName)
                    {
                        _context.RecordPass(Module, method, $"robot created, tab '{TabName}' found");
                        return true;
                    }
                }

                _context.RecordFail(Module, method, $"tab '{TabName}' not found in bot_get_sources");
                return false;
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
                return false;
            }
        }

        private bool VerifyPositionsLoaded(string method)
        {
            const string getMethod = "bot_position_get_open";

            try
            {
                object request = new { bot_id = _createdBotName, tab_name = TabName };

                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(getMethod, request);
                _context.PrintResponse(response);

                if (!TryParseConfig(response, getMethod, out JsonElement config))
                {
                    return false;
                }

                int count = config.GetProperty("count").GetInt32();

                if (count != 2)
                {
                    _context.RecordFail(Module, method, $"expected 2 open positions, got {count}");
                    return false;
                }

                if (!CheckPosition(config, 1, "Buy", 3m, method)
                    || !CheckPosition(config, 2, "Sell", 3m, method))
                {
                    return false;
                }

                _context.RecordPass(Module, method, "2 positions loaded: Buy vol=3, Sell vol=3");
                return true;
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
                return false;
            }
        }

        private bool CheckPosition(JsonElement config, int number, string direction, decimal openVolume, string method)
        {
            foreach (JsonElement position in config.GetProperty("positions").EnumerateArray())
            {
                if (position.GetProperty("position_number").GetInt32() != number)
                {
                    continue;
                }

                string actualDirection = position.GetProperty("direction").GetString() ?? string.Empty;
                string actualState = position.GetProperty("state").GetString() ?? string.Empty;
                decimal actualVolume = position.GetProperty("open_volume").GetDecimal();

                if (actualDirection != direction)
                {
                    _context.RecordFail(Module, method, $"position {number}: direction {actualDirection}, expected {direction}");
                    return false;
                }

                if (actualVolume != openVolume)
                {
                    _context.RecordFail(Module, method, $"position {number}: open_volume {actualVolume}, expected {openVolume}");
                    return false;
                }

                if (actualState != "Open")
                {
                    _context.RecordFail(Module, method, $"position {number}: state {actualState}, expected Open");
                    return false;
                }

                return true;
            }

            _context.RecordFail(Module, method, $"position {number} not found in open positions");
            return false;
        }

        private void VerifyFilePreserved()
        {
            const string method = "file_after_load";

            try
            {
                if (!File.Exists(_referenceFilePath))
                {
                    _context.RecordFail(Module, method, "journal file disappeared after robot creation");
                    return;
                }

                string[] actualLines = File.ReadAllLines(_referenceFilePath);
                string[] expectedLines = BuildReferenceContent()
                    .Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

                if (actualLines.Length != expectedLines.Length)
                {
                    _context.RecordFail(Module, method,
                        $"journal file has {actualLines.Length} lines, expected {expectedLines.Length}");
                    return;
                }

                for (int i = 0; i < expectedLines.Length; i++)
                {
                    if (!LinesEqual(expectedLines[i], actualLines[i]))
                    {
                        _context.RecordFail(Module, method, $"journal file line {i + 1} mismatch");
                        return;
                    }
                }

                _context.RecordPass(Module, method, "journal file matches the reference after load");
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private bool VerifyReloadAfterRestart()
        {
            const string method = "reload_after_restart";

            try
            {
                // full restart: the engine must load the same journal file again without losses
                _context.RestartOsEngine("-robotslight");

                if (!WaitRobotMaster())
                {
                    return false;
                }

                return VerifyPositionsLoaded(method);
            }
            catch (Exception error)
            {
                _context.RecordFail(Module, method, error.Message);
                return false;
            }
        }

        private void Cleanup()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(_createdBotName))
                {
                    _context.Client.ToolsCall("bot_delete", new { bot_id = _createdBotName });
                    _createdBotName = string.Empty;
                }
            }
            catch (Exception error)
            {
                Console.WriteLine($"[{Module}] Failed to delete test robot: {error.Message}");
            }

            try
            {
                if (File.Exists(_referenceFilePath))
                {
                    File.Delete(_referenceFilePath);
                }
            }
            catch (Exception error)
            {
                Console.WriteLine($"[{Module}] Failed to delete reference file: {error.Message}");
            }
        }

        #endregion

        #region Comparison helpers

        private static bool LinesEqual(string expected, string actual)
        {
            if (expected == actual)
            {
                return true;
            }

            string[] expectedFields = expected.Split('#');
            string[] actualFields = actual.Split('#');

            if (expectedFields.Length != actualFields.Length)
            {
                return false;
            }

            for (int i = 0; i < expectedFields.Length; i++)
            {
                if (!FieldEqual(expectedFields[i], actualFields[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool FieldEqual(string expected, string actual)
        {
            if (expected == actual)
            {
                return true;
            }

            // order strings saved by the new engine version may gain
            // one trailing '@'-field (PriceCondition) — allow that
            if (!expected.Contains('@') || !actual.Contains('@'))
            {
                return false;
            }

            string[] expectedParts = expected.Split('@');
            string[] actualParts = actual.Split('@');

            if (actualParts.Length != expectedParts.Length + 1)
            {
                return false;
            }

            for (int i = 0; i < expectedParts.Length; i++)
            {
                if (expectedParts[i] != actualParts[i])
                {
                    return false;
                }
            }

            return true;
        }

        #endregion

        private bool TryParseConfig(string response, string method, out JsonElement config)
        {
            config = default;

            using (JsonDocument document = JsonDocument.Parse(response))
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

                using (JsonDocument innerDocument = JsonDocument.Parse(text))
                {
                    config = innerDocument.RootElement.Clone();
                    return true;
                }
            }
        }
    }
}
