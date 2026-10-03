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
    /// Модуль 2 — Очистка: убрать всех роботов со старых прогонов.
    /// </summary>
    public class CleanupTests : OsTesterTestsBase
    {
        public CleanupTests(TestContext context) : base(context, "Cleanup")
        {
        }

        public void RunAll()
        {
            WaitForTesterReady(60);
            ResetTesterDefaults();
            RemoveAllRobots();
        }

        private void RemoveAllRobots()
        {
            int deleted = 0;

            for (int pass = 0; pass < 20; pass++)
            {
                string list = ToolsCall("bot_get_list", new { });

                if (!TryParseJson(list, out JsonElement root)
                    || !root.TryGetProperty("bots", out JsonElement bots)
                    || bots.ValueKind != JsonValueKind.Array)
                {
                    break;
                }

                int remaining = bots.GetArrayLength();

                if (remaining == 0)
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

                    ToolsCall("bot_delete", new { bot_id = name });
                    deleted++;
                }
            }

            string verify = ToolsCall("bot_get_list", new { });
            bool clean = TryParseJson(verify, out JsonElement finalRoot)
                && finalRoot.TryGetProperty("count", out JsonElement countElement)
                && countElement.ValueKind == JsonValueKind.Number
                && countElement.GetInt32() == 0;

            if (clean)
            {
                RecordPass("RemoveAllRobots", $"удалено роботов: {deleted}");
            }
            else
            {
                RecordFail("RemoveAllRobots", "остались роботы");
            }
        }
    }
}
