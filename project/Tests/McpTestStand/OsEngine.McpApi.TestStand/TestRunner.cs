/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.McpApi.TestStand.Tests;

namespace OsEngine.McpApi.TestStand
{
    /// <summary>
    /// Orchestrates MCP API module tests against a live OsEngine instance.
    /// </summary>
    public class TestRunner
    {
        private readonly McpApiClient _client;

        public TestRunner(McpApiClient client)
        {
            try
            {
                _client = client ?? throw new ArgumentNullException(nameof(client));
            }
            catch (Exception error)
            {
                throw new InvalidOperationException($"Failed to create TestRunner: {error}");
            }
        }

        public List<TestResult> RunAll()
        {
            try
            {
                var context = new TestContext(_client);
                context.PrintHeader();

                RunModule("Protocol", () => new ProtocolTests(context).RunAll());
                RunModule("Logs", () => new LogsTests(context).RunAll());
                RunModule("Settings", () => new SettingsTests(context).RunAll());
                RunModule("Config", () => new ConfigTests(context).RunAll());
                RunModule("ServerManagement", () => new ServerManagementTests(context).RunAll());
                RunModule("ServerInstance", () => new ServerInstanceTests(context).RunAll());
                RunModule("SSE", () => new SseTests(context).RunAll());
                RunModule("Errors", () => new ErrorTests(context).RunAll());

                // Terminal tests are always last because launch/stop/kill
                // terminate or restart the OsEngine process.
                RunModule("Terminal", () => new TerminalTests(context).RunAll());

                context.PrintSummary();
                return context.Results;
            }
            catch (Exception error)
            {
                return new List<TestResult>
                {
                    TestResult.Failed("RunAll", error.Message)
                };
            }
        }

        private static void RunModule(string name, Action run)
        {
            try
            {
                run();
            }
            catch (Exception error)
            {
                Console.WriteLine($"[{name}] Module failed: {error.Message}");
            }
        }
    }
}
