/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;

namespace OsEngine.Connectors.TestStand
{
    /// <summary>
    /// Where a test parameter value comes from.
    /// </summary>
    public enum ParamValueSource
    {
        Security,
        SecurityClass,
        SecuritiesList,
        Separator,
        Minutes,
        StartDate,
        Portfolio,
        Volume,
        Asset,
        CountOrders,
        SecuritiesCount,
        TimeFrame
    }

    /// <summary>
    /// One WServerTester parameter: exact parameter name on the robot
    /// plus the source of its value (from the stand config / runtime).
    /// </summary>
    public class TestParam
    {
        public string Name;
        public ParamValueSource Source;

        public TestParam(string name, ParamValueSource source)
        {
            Name = name;
            Source = source;
        }
    }

    /// <summary>
    /// One WServerTester test: id, start button name, report marker in the
    /// robot log (REPORT + tester class name), owning stand module, whether
    /// it places real orders and the parameters to set before the click.
    ///
    /// The stand checks the WServerTester robot itself ("module is not broken")
    /// on a reference connector: the API works, the button clicks, the test
    /// thread starts and produces a well-formed report (REPORT ... STATUS: OK|FAIL).
    /// The report CONTENT is the responsibility of the test itself: catching
    /// errors is its job, so STATUS: FAIL with caught errors = a successful run.
    /// PASS = test started and a well-formed report arrived. FAIL = the button
    /// was not clicked, the test did not start, or no/garbage report in time.
    /// </summary>
    public class ServerTestInfo
    {
        public string Id;
        public string ButtonName;
        public string ReportMarker;
        public string Module;
        public bool RequiresLiveTrade;
        public TestParam[] Params;

        public ServerTestInfo(string id, string buttonName, string reportMarker,
            string module, bool requiresLiveTrade, params TestParam[] testParams)
        {
            Id = id;
            ButtonName = buttonName;
            ReportMarker = reportMarker;
            Module = module;
            RequiresLiveTrade = requiresLiveTrade;
            Params = testParams;
        }
    }

    /// <summary>
    /// Registry of all 34 WServerTester tests
    /// (project\OsEngine\Robots\AutoTestBots\ServerTests\AServerTester.cs).
    /// Parameter names must match the robot constructor exactly.
    /// </summary>
    public static class KnownTests
    {
        public const string ModuleSecurities = "Securities";
        public const string ModuleMarketDepth = "MarketDepth";
        public const string ModuleTrades = "Trades";
        public const string ModuleData = "Data";
        public const string ModuleConnection = "Connection";
        public const string ModulePortfolio = "Portfolio";
        public const string ModuleOrders = "Orders";
        public const string ModuleBotTabOrders = "BotTabOrders";

        private static TestParam[] OrderParams(string group, bool withVolume = true)
        {
            List<TestParam> result = new List<TestParam>
            {
                new TestParam("Portfolio. " + group, ParamValueSource.Portfolio),
                new TestParam("Sec name. " + group, ParamValueSource.Security),
                new TestParam("Sec class. " + group, ParamValueSource.SecurityClass)
            };

            if (withVolume)
            {
                result.Add(new TestParam("Volume. " + group, ParamValueSource.Volume));
            }

            return result.ToArray();
        }

        private static TestParam[] WithCountOrders(string group)
        {
            List<TestParam> result = new List<TestParam>(OrderParams(group));
            result.Add(new TestParam("Count orders " + group, ParamValueSource.CountOrders));
            return result.ToArray();
        }

        private static TestParam[] BotTabParams(int number)
        {
            return OrderParams("BotTabSimple " + number);
        }

        public static readonly ServerTestInfo[] All =
        {
            // V1. Securities validation
            new ServerTestInfo("V1", "Start test sec", "REPORT Var_1_Securities",
                ModuleSecurities, false),

            // V2. Market depth streaming
            new ServerTestInfo("V2", "Start test md", "REPORT Var_2_MarketDepth",
                ModuleMarketDepth, false,
                new TestParam("Md tester Sec names v2", ParamValueSource.SecuritiesList),
                new TestParam("Md tester Class Code v2", ParamValueSource.SecurityClass),
                new TestParam("Md tester work time minutes v2", ParamValueSource.Minutes),
                new TestParam("Securities Separator v2", ParamValueSource.Separator)),

            // V3. Trades streaming
            new ServerTestInfo("V3", "Start test trades", "REPORT Var_3_Trades",
                ModuleTrades, false,
                new TestParam("Sec names v3", ParamValueSource.SecuritiesList),
                new TestParam("Class Code v3", ParamValueSource.SecurityClass),
                new TestParam("Tester work time minutes v3", ParamValueSource.Minutes),
                new TestParam("Securities Separator v3", ParamValueSource.Separator)),

            // D1. History integrity from a base date
            new ServerTestInfo("D1", "Start test data 1", "REPORT Data_1_Integrity",
                ModuleData, false,
                new TestParam("Sec name data test 1", ParamValueSource.Security),
                new TestParam("Sec class data test 1", ParamValueSource.SecurityClass),
                new TestParam("Base date for data request test 1", ParamValueSource.StartDate)),

            // D2. Candles validation
            new ServerTestInfo("D2", "Start test data 2", "REPORT Data_2_Validation_Candles",
                ModuleData, false,
                new TestParam("Sec name data test 2", ParamValueSource.Security),
                new TestParam("Sec class data test 2", ParamValueSource.SecurityClass)),

            // D3. Ticks validation
            new ServerTestInfo("D3", "Start test data 3", "REPORT Data_3_Validation_Trades",
                ModuleData, false,
                new TestParam("Sec name data test 3", ParamValueSource.Security),
                new TestParam("Sec class data test 3", ParamValueSource.SecurityClass)),

            // D4. Stress candles on several securities
            new ServerTestInfo("D4", "Start test data 4", "REPORT Data_4_Stress_Candles",
                ModuleData, false,
                new TestParam("Sec name data test 4", ParamValueSource.SecuritiesList),
                new TestParam("Securities separator data test 4", ParamValueSource.Separator),
                new TestParam("Sec class data test 4", ParamValueSource.SecurityClass),
                new TestParam("Base date for data request test 4", ParamValueSource.StartDate)),

            // D5. Stress trades on several securities
            new ServerTestInfo("D5", "Start test data 5", "REPORT Data_5_Stress_Trades",
                ModuleData, false,
                new TestParam("Sec name data test 5", ParamValueSource.SecuritiesList),
                new TestParam("Securities separator test 5", ParamValueSource.Separator),
                new TestParam("Sec class data test 5", ParamValueSource.SecurityClass)),

            // C1. Connect/disconnect status cycles
            new ServerTestInfo("C1", "Start test connection 1", "REPORT Conn_1_Status",
                ModuleConnection, false),

            // C2. Subscribe to all securities of a class
            new ServerTestInfo("C2", "Start test connection 2", "REPORT Conn_2_SubscrAllSec",
                ModuleConnection, false,
                new TestParam("Sec class connection test 2", ParamValueSource.SecurityClass)),

            // C3. Memory stress
            new ServerTestInfo("C3", "Start test connection 3", "REPORT Conn_3_Stress_Memory",
                ModuleConnection, false,
                new TestParam("Sec class connection test 3", ParamValueSource.SecurityClass)),

            // C4. Real-time candles validation
            new ServerTestInfo("C4", "Start test connection 4", "REPORT Conn_4_Validation_Candles",
                ModuleConnection, false,
                new TestParam("Sec name connection test 4", ParamValueSource.SecuritiesList),
                new TestParam("Securities separator test 4", ParamValueSource.Separator),
                new TestParam("Sec class connection test 4", ParamValueSource.SecurityClass)),

            // C5. Screener on N securities
            new ServerTestInfo("C5", "Start test connection 5", "REPORT Conn_5_Screener",
                ModuleConnection, false,
                new TestParam("Sec class connection test 5", ParamValueSource.SecurityClass),
                new TestParam("Sec count connection test 5", ParamValueSource.SecuritiesCount),
                new TestParam("Screneer tester work time minutes C5", ParamValueSource.Minutes),
                new TestParam("Sec timeFrame connection test 5", ParamValueSource.TimeFrame)),

            // O1. Fake orders (too small / too big volume). Volumes stay at robot defaults
            new ServerTestInfo("O1", "Start test orders 1", "REPORT Orders_1_FakeOrders",
                ModuleOrders, true, OrderParams("orders test 1", withVolume: false)),

            // O2. Limit orders execution
            new ServerTestInfo("O2", "Start test orders 2", "REPORT Orders_2_LimitsExecute",
                ModuleOrders, true, OrderParams("orders test 2")),

            // O3. Market orders
            new ServerTestInfo("O3", "Start test orders 3", "REPORT Orders_3_MarketOrders",
                ModuleOrders, true, OrderParams("orders test 3")),

            // O4. Limit cancel spam
            new ServerTestInfo("O4", "Start test orders 4", "REPORT Orders_4_LimitCancel",
                ModuleOrders, true, WithCountOrders("test 4")),

            // O5. Change price
            new ServerTestInfo("O5", "Start test orders 5", "REPORT Orders_5_ChangePrice",
                ModuleOrders, true, WithCountOrders("test 5")),

            // O6. Change price to fake values (fake prices stay at robot defaults)
            new ServerTestInfo("O6", "Start test orders 6", "REPORT Orders_6_ChangePriceError",
                ModuleOrders, true, OrderParams("orders test 6")),

            // O7. Add / move / cancel spam
            new ServerTestInfo("O7", "Start test orders 7", "REPORT Orders_7_Add_Move_Cancel_Spam",
                ModuleOrders, true, WithCountOrders("test 7")),

            // O8. Orders request on reconnect
            new ServerTestInfo("O8", "Start test orders 8", "REPORT Orders_8_RequestOnReconnect",
                ModuleOrders, true, OrderParams("orders test 8")),

            // O9. Lost active order request
            new ServerTestInfo("O9", "Start test orders 9", "REPORT Orders_9_RequestLostActivOrder",
                ModuleOrders, true, OrderParams("orders test 9")),

            // O10. Lost done order request
            new ServerTestInfo("O10", "Start test orders 10", "REPORT Orders_10_RequestLostDoneOrder",
                ModuleOrders, true, OrderParams("orders test 10")),

            // O11. Lost my trades request
            new ServerTestInfo("O11", "Start test orders 11", "REPORT Orders_11_RequestLostMyTrades",
                ModuleOrders, true, OrderParams("orders test 11")),

            // O12. Orders list request
            new ServerTestInfo("O12", "Start test orders 12", "REPORT Orders_12_RequestOrdersList",
                ModuleOrders, true, WithCountOrders("test 12")),

            // O13. Server stop orders
            new ServerTestInfo("O13", "Start test orders 13", "REPORT Orders_13_StopOrders",
                ModuleOrders, true, OrderParams("orders test 13")),

            // O14. Stop-limit place / cancel
            new ServerTestInfo("O14", "Start test orders 14", "REPORT Orders_14_StopLimitPlaceCancel",
                ModuleOrders, true, WithCountOrders("test 14")),

            // O15. Stop-limit request on reconnect
            new ServerTestInfo("O15", "Start test orders 15", "REPORT Orders_15_StopLimitRequestOnReconnect",
                ModuleOrders, true, OrderParams("orders test 15")),

            // O16. Stop trigger on reconnect
            new ServerTestInfo("O16", "Start test orders 16", "REPORT Orders_16_StopTriggerOnReconnect",
                ModuleOrders, true, OrderParams("orders test 16")),

            // P1. Portfolio validation (note: robot parameter names have double spaces)
            new ServerTestInfo("P1", "Start test portfolio 1", "REPORT Portfolio_1_Validation",
                ModulePortfolio, false,
                new TestParam("Portfolio.  portfolio 1", ParamValueSource.Portfolio),
                new TestParam("Sec name.  portfolio 1", ParamValueSource.Security),
                new TestParam("Sec class.  portfolio 1", ParamValueSource.SecurityClass),
                new TestParam("Asset In portfolio 1", ParamValueSource.Asset),
                new TestParam("Volume.  portfolio 1", ParamValueSource.Volume)),

            // B1-B6. BotTabSimple server stop order methods
            new ServerTestInfo("B1", "Start test BotTabSimple 1", "REPORT BotTabSimple_1_OpenStopLimit",
                ModuleBotTabOrders, true, BotTabParams(1)),
            new ServerTestInfo("B2", "Start test BotTabSimple 2", "REPORT BotTabSimple_2_OpenStopMarket",
                ModuleBotTabOrders, true, BotTabParams(2)),
            new ServerTestInfo("B3", "Start test BotTabSimple 3", "REPORT BotTabSimple_3_CloseAtStop",
                ModuleBotTabOrders, true, BotTabParams(3)),
            new ServerTestInfo("B4", "Start test BotTabSimple 4", "REPORT BotTabSimple_4_ToPosition",
                ModuleBotTabOrders, true, BotTabParams(4)),
            new ServerTestInfo("B5", "Start test BotTabSimple 5", "REPORT BotTabSimple_5_Cancel",
                ModuleBotTabOrders, true, BotTabParams(5)),
            new ServerTestInfo("B6", "Start test BotTabSimple 6", "REPORT BotTabSimple_6_AutoRestCancel",
                ModuleBotTabOrders, true, BotTabParams(6)),
        };
    }
}
