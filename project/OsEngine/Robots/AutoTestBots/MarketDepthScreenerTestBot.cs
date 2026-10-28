/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using OsEngine.Entity;
using OsEngine.IndicatorMarketDepth;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Robots.AutoTestBots
{
    /// <summary>
    /// Тест роботного слоя создания стаканных индикаторов на BotTabScreener:
    /// создание через CreateMarketDepthIndicator, load/save, раздача по табам.
    /// </summary>
    [Bot("MarketDepthScreenerTestBot")]
    public class MarketDepthScreenerTestBot : BotPanel
    {
        private const string Marker = "MDI_TEST_DONE";

        private BotTabScreener _screenerTab;

        private StrategyParameterString _mode;

        private StrategyParameterInt _period;

        private StrategyParameterString _testState;

        private StrategyParameterString _reportJson;

        private StrategyParameterButton _deployButton;

        private bool _isDeployed;

        public MarketDepthScreenerTestBot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);
            _screenerTab = TabsScreener[0];

            _mode = (StrategyParameterString)CreateParameter("Mode", "Absolute", new[] { "Absolute", "Percent" });
            _period = (StrategyParameterInt)CreateParameter("Period", 10, 1, 60, 1);
            _testState = (StrategyParameterString)CreateParameter("TestState", "Waiting");
            _reportJson = (StrategyParameterString)CreateParameter("ReportJson", "");
            _deployButton = CreateParameterButton("DeployIndicator");
            _deployButton.UserClickOnButtonEvent += DeployIndicator;
        }

        public override string GetNameStrategyType()
        {
            return "MarketDepthScreenerTestBot";
        }

        public override void ShowIndividualSettingsDialog()
        {
        }

        private void DeployIndicator()
        {
            if (_isDeployed)
            {
                return;
            }

            _screenerTab.CreateMarketDepthIndicator(1, "Spread",
                new List<string> { _mode.ValueString, _period.ValueInt.ToString() }, false);

            if (_screenerTab.Tabs.Count > 0)
            {
                _screenerTab.Tabs[0].Connector.TestOverEvent += Connector_TestOverEvent;
            }

            _isDeployed = true;
            _testState.ValueString = "Deployed";
        }

        private void Connector_TestOverEvent()
        {
            List<Dictionary<string, object>> tests = new List<Dictionary<string, object>>();

            TestDeploy(tests);
            TestDescriptorNumType(tests);
            TestDescriptorCanDelete(tests);
            TestDescriptorParams(tests);
            TestOnTabs(tests);
            TestTabCanDelete(tests);
            TestValuesProduced(tests);
            TestParamMapping(tests);
            TestLoadSave(tests);
            TestNoExceptions(tests);

            _testState.ValueString = HasFail(tests) ? "Failed" : "Completed";
            _reportJson.ValueString = JsonSerializer.Serialize(new Dictionary<string, object> { { "tests", tests } });

            SendNewLogMessage(Marker + " " + _testState.ValueString + " " + _reportJson.ValueString, LogMessageType.System);
        }

        private static bool HasFail(List<Dictionary<string, object>> tests)
        {
            for (int i = 0; i < tests.Count; i++)
            {
                if (tests[i].ContainsKey("status") && tests[i]["status"].ToString() == "FAIL")
                {
                    return true;
                }
            }

            return false;
        }

        private IndicatorOnTabs Descriptor => _screenerTab._marketDepthIndicators.Count > 0 ? _screenerTab._marketDepthIndicators[0] : null;

        private void TestDeploy(List<Dictionary<string, object>> tests)
        {
            bool ok = _isDeployed && _screenerTab._marketDepthIndicators.Count == 1;
            tests.Add(Result("Deploy", ok, ok ? "descriptor stored" : "descriptor missing"));
        }

        private void TestDescriptorNumType(List<Dictionary<string, object>> tests)
        {
            IndicatorOnTabs ind = Descriptor;
            bool ok = ind != null && ind.Num == 1 && ind.Type == "Spread";
            tests.Add(Result("DescriptorNumType", ok, ok ? "Num=1 Type=Spread" : ind != null ? "Num=" + ind.Num + " Type=" + ind.Type : "no descriptor"));
        }

        private void TestDescriptorCanDelete(List<Dictionary<string, object>> tests)
        {
            IndicatorOnTabs ind = Descriptor;
            bool ok = ind != null && ind.CanDelete == false;
            tests.Add(Result("DescriptorCanDelete", ok, ok ? "CanDelete=false" : "CanDelete!=false"));
        }

        private void TestDescriptorParams(List<Dictionary<string, object>> tests)
        {
            IndicatorOnTabs ind = Descriptor;
            bool ok = ind != null && ind.Parameters != null && ind.Parameters.Count == 2
                && ind.Parameters[0] == _mode.ValueString
                && ind.Parameters[1] == _period.ValueInt.ToString();
            tests.Add(Result("DescriptorParams", ok, ok ? "Mode/Period stored" : "params mismatch"));
        }

        private void TestOnTabs(List<Dictionary<string, object>> tests)
        {
            int tabs = _screenerTab.Tabs.Count;
            int withIndicator = 0;

            for (int i = 0; i < _screenerTab.Tabs.Count; i++)
            {
                if (_screenerTab.Tabs[i].MarketDepthIndicators != null
                    && _screenerTab.Tabs[i].MarketDepthIndicators.Indicators.Count > 0)
                {
                    withIndicator++;
                }
            }

            bool ok = tabs > 0 && withIndicator == tabs;
            tests.Add(Result("OnTabs", ok, ok ? "indicator on " + withIndicator + "/" + tabs + " tabs" : "tabs " + tabs + ", with indicator " + withIndicator));
        }

        private void TestTabCanDelete(List<Dictionary<string, object>> tests)
        {
            bool ok = _screenerTab.Tabs.Count > 0;

            for (int i = 0; i < _screenerTab.Tabs.Count; i++)
            {
                List<AMarketDepth> indicators = _screenerTab.Tabs[i].MarketDepthIndicators.Indicators;

                if (indicators.Count == 0 || indicators[0].CanDelete != false)
                {
                    ok = false;
                }
            }

            tests.Add(Result("TabCanDelete", ok, ok ? "tabs CanDelete=false" : "tab CanDelete!=false"));
        }

        private void TestValuesProduced(List<Dictionary<string, object>> tests)
        {
            bool ok = _screenerTab.Tabs.Count > 0;

            for (int i = 0; i < _screenerTab.Tabs.Count; i++)
            {
                List<AMarketDepth> indicators = _screenerTab.Tabs[i].MarketDepthIndicators.Indicators;

                if (indicators.Count == 0)
                {
                    ok = false;
                    continue;
                }

                for (int s = 0; s < indicators[0].DataSeries.Count; s++)
                {
                    if (indicators[0].DataSeries[s].Values.Count == 0)
                    {
                        ok = false;
                    }
                }
            }

            tests.Add(Result("ValuesProduced", ok, ok ? "tabs have values" : "some tab series empty"));
        }

        private void TestParamMapping(List<Dictionary<string, object>> tests)
        {
            bool ok = _screenerTab.Tabs.Count > 0;

            for (int i = 0; i < _screenerTab.Tabs.Count; i++)
            {
                List<AMarketDepth> indicators = _screenerTab.Tabs[i].MarketDepthIndicators.Indicators;

                if (indicators.Count == 0)
                {
                    ok = false;
                    continue;
                }

                AMarketDepth ind = indicators[0];

                ParameterMarketDepthString mode = (ParameterMarketDepthString)ind.Parameters.Find(p => p.Name == "Mode");
                ParameterMarketDepthInt period = (ParameterMarketDepthInt)ind.Parameters.Find(p => p.Name == "Period");

                if (mode == null || mode.ValueString != _mode.ValueString)
                {
                    ok = false;
                }

                if (period == null || period.ValueInt != _period.ValueInt)
                {
                    ok = false;
                }

                IParameterMarketDepth historyDepth = ind.Parameters.Find(p => p.Name == "HistoryDepth");

                if (historyDepth == null || historyDepth.Type != MarketDepthParameterType.Int)
                {
                    ok = false;
                }
            }

            tests.Add(Result("ParamMapping", ok, ok ? "HistoryDepth skipped, user params mapped" : "param mapping failed"));
        }

        private void TestLoadSave(List<Dictionary<string, object>> tests)
        {
            string path = @"Engine\" + _screenerTab.TabName + @"ScreenerMarketDepthIndicators.txt";

            bool exists = File.Exists(path);

            bool contentOk = false;

            if (exists)
            {
                try
                {
                    string[] lines = File.ReadAllLines(path);
                    contentOk = lines.Length >= 1 && lines[0].Contains("Spread");
                }
                catch
                {
                    contentOk = false;
                }
            }

            bool ok = exists && contentOk;
            tests.Add(Result("LoadSave", ok, ok ? "file saved and contains Spread" : "file missing or empty"));
        }

        private void TestNoExceptions(List<Dictionary<string, object>> tests)
        {
            bool ok = true;
            int errors = 0;

            for (int i = 0; i < _screenerTab.Tabs.Count; i++)
            {
                List<AMarketDepth> indicators = _screenerTab.Tabs[i].MarketDepthIndicators.Indicators;

                for (int s = 0; s < indicators.Count; s++)
                {
                    errors += indicators[s].ErrorCount;
                }
            }

            ok = errors == 0;
            tests.Add(Result("NoExceptions", ok, ok ? "no exceptions" : "errors " + errors));
        }

        private static Dictionary<string, object> Result(string name, bool ok, string details)
        {
            return new Dictionary<string, object>
            {
                { "name", name },
                { "status", ok ? "PASS" : "FAIL" },
                { "details", details }
            };
        }
    }
}
