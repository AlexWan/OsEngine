/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Р’Р°С€Рё РїСЂР°РІР° РЅР° РёСЃРїРѕР»СЊР·РѕРІР°РЅРёРµ РєРѕРґР° СЂРµРіСѓР»РёСЂСѓСЋС‚СЃСЏ РґР°РЅРЅРѕР№ Р»РёС†РµРЅР·РёРµР№ http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Collections.Generic;
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
    /// РўРµСЃС‚ СЂРѕР±РѕС‚РЅРѕРіРѕ СЃР»РѕСЏ СЃРѕР·РґР°РЅРёСЏ СЃС‚Р°РєР°РЅРЅС‹С… РёРЅРґРёРєР°С‚РѕСЂРѕРІ РЅР° BotTabSimple:
    /// СЃРѕР·РґР°РЅРёРµ С‡РµСЂРµР· CreateMarketDepthIndicator, РїСЂРёРІСЏР·РєР° РїР°СЂР°РјРµС‚СЂРѕРІ, CanDelete Рё С‚.Рґ.
    /// </summary>
    [Bot("MarketDepthRobotLayerTestBot")]
    public class MarketDepthRobotLayerTestBot : BotPanel
    {
        private const string Marker = "MDI_TEST_DONE";

        private BotTabSimple _tab;

        private StrategyParameterString _mode;

        private StrategyParameterInt _period;

        private StrategyParameterString _testState;

        private StrategyParameterString _reportJson;

        private StrategyParameterButton _deployButton;

        private AMarketDepth _main;

        private bool _isDeployed;

        private int _depthEvents;

        public MarketDepthRobotLayerTestBot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _mode = (StrategyParameterString)CreateParameter("Mode", "Absolute", new[] { "Absolute", "Percent" });
            _period = (StrategyParameterInt)CreateParameter("Period", 10, 1, 60, 1);
            _testState = (StrategyParameterString)CreateParameter("TestState", "Waiting");
            _reportJson = (StrategyParameterString)CreateParameter("ReportJson", "");
            _deployButton = CreateParameterButton("DeployIndicator");
            _deployButton.UserClickOnButtonEvent += DeployIndicator;

            _tab.MarketDepthUpdateEvent += Tab_MarketDepthUpdateEvent;
            _tab.Connector.TestOverEvent += Connector_TestOverEvent;
        }

        public override string GetNameStrategyType()
        {
            return "MarketDepthRobotLayerTestBot";
        }

        public override void ShowIndividualSettingsDialog()
        {
        }

        private void Tab_MarketDepthUpdateEvent(MarketDepth depth)
        {
            if (_isDeployed && depth != null)
            {
                _depthEvents++;
            }
        }

        private void DeployIndicator()
        {
            if (_isDeployed)
            {
                return;
            }

            _main = _tab.CreateMarketDepthIndicator(this, "Spread", false);

            if (_main == null)
            {
                _testState.ValueString = "Failed";
                SendNewLogMessage("Deploy failed", LogMessageType.Error);
                return;
            }

            ((ParameterMarketDepthString)_main.Parameters.Find(p => p.Name == "Mode")).ValueString = _mode.ValueString;
            ((ParameterMarketDepthInt)_main.Parameters.Find(p => p.Name == "Period")).ValueInt = _period.ValueInt;

            _isDeployed = true;
            _testState.ValueString = "Deployed";
        }

        private void Connector_TestOverEvent()
        {
            List<Dictionary<string, object>> tests = new List<Dictionary<string, object>>();

            TestDeploy(tests);
            TestCanDelete(tests);
            TestDataSource(tests);
            TestInMaster(tests);
            TestParamBindMode(tests);
            TestParamBindPeriod(tests);
            TestSeriesPresence(tests);
            TestValuesProduced(tests);
            TestInputCoverage(tests);
            TestParamChangeReapply(tests);
            TestUniqueName(tests);
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

        private void TestDeploy(List<Dictionary<string, object>> tests)
        {
            bool ok = _main != null && _isDeployed;
            tests.Add(Result("Deploy", ok, ok ? "indicator created" : "indicator not created"));
        }

        private void TestCanDelete(List<Dictionary<string, object>> tests)
        {
            bool ok = _main != null && _main.CanDelete == false;
            tests.Add(Result("CanDelete", ok, ok ? "CanDelete=false" : "CanDelete!=false"));
        }

        private void TestDataSource(List<Dictionary<string, object>> tests)
        {
            bool ok = _main != null && _main.DataSource == MarketDepthIndicatorDataSource.MarketDepth;
            tests.Add(Result("DataSource", ok, ok ? "MarketDepth" : _main != null ? _main.DataSource.ToString() : "no indicator"));
        }

        private void TestInMaster(List<Dictionary<string, object>> tests)
        {
            bool ok = _tab.MarketDepthIndicators != null && _tab.MarketDepthIndicators.Indicators.Contains(_main);
            tests.Add(Result("InMaster", ok, ok ? "indicator in master" : "not in master"));
        }

        private void TestParamBindMode(List<Dictionary<string, object>> tests)
        {
            if (_main == null)
            {
                tests.Add(Result("ParamBindMode", false, "no indicator"));
                return;
            }

            ParameterMarketDepthString mode = (ParameterMarketDepthString)_main.Parameters.Find(p => p.Name == "Mode");
            bool ok = mode != null && mode.ValueString == _mode.ValueString;

            tests.Add(Result("ParamBindMode", ok, ok ? "Mode=" + mode.ValueString : "Mode mismatch"));
        }

        private void TestParamBindPeriod(List<Dictionary<string, object>> tests)
        {
            if (_main == null)
            {
                tests.Add(Result("ParamBindPeriod", false, "no indicator"));
                return;
            }

            ParameterMarketDepthInt period = (ParameterMarketDepthInt)_main.Parameters.Find(p => p.Name == "Period");
            bool ok = period != null && period.ValueInt == _period.ValueInt;

            tests.Add(Result("ParamBindPeriod", ok, ok ? "Period=" + period.ValueInt : "Period mismatch"));
        }

        private void TestSeriesPresence(List<Dictionary<string, object>> tests)
        {
            bool ok = _main != null && _main.DataSeries.Count == 3;
            tests.Add(Result("SeriesPresence", ok, ok ? "3 series" : _main != null ? _main.DataSeries.Count + " series" : "no indicator"));
        }

        private void TestValuesProduced(List<Dictionary<string, object>> tests)
        {
            if (_main == null)
            {
                tests.Add(Result("ValuesProduced", false, "no indicator"));
                return;
            }

            bool ok = true;

            for (int i = 0; i < _main.DataSeries.Count; i++)
            {
                if (_main.DataSeries[i].Values.Count == 0)
                {
                    ok = false;
                }
            }

            tests.Add(Result("ValuesProduced", ok, ok ? "all series have values" : "some series empty"));
        }

        private void TestInputCoverage(List<Dictionary<string, object>> tests)
        {
            bool ok = _depthEvents > 0;
            tests.Add(Result("InputCoverage", ok, ok ? "depth events " + _depthEvents : "no depth events"));
        }

        private void TestParamChangeReapply(List<Dictionary<string, object>> tests)
        {
            if (_main == null)
            {
                tests.Add(Result("ParamChangeReapply", false, "no indicator"));
                return;
            }

            int oldValue = _period.ValueInt;
            int newValue = oldValue == 1 ? 2 : 1;

            _period.ValueInt = newValue;

            ParameterMarketDepthInt period = (ParameterMarketDepthInt)_main.Parameters.Find(p => p.Name == "Period");
            period.ValueInt = _period.ValueInt;

            bool ok = period.ValueInt == newValue;

            try
            {
                _main.Recalculate();
            }
            catch
            {
                ok = false;
            }

            _period.ValueInt = oldValue;
            period.ValueInt = _period.ValueInt;

            tests.Add(Result("ParamChangeReapply", ok, ok ? "param re-applied and recalculated" : "reapply failed"));
        }

        private void TestUniqueName(List<Dictionary<string, object>> tests)
        {
            if (_main == null)
            {
                tests.Add(Result("UniqueName", false, "no indicator"));
                return;
            }

            AMarketDepth second = _tab.CreateMarketDepthIndicator(this, "Spread", false);

            bool ok = second != null && second.Name != _main.Name;

            tests.Add(Result("UniqueName", ok, ok ? "second name unique" : "name not unique"));
        }

        private void TestNoExceptions(List<Dictionary<string, object>> tests)
        {
            bool ok = _main == null || _main.ErrorCount == 0;
            tests.Add(Result("NoExceptions", ok, ok ? "no exceptions" : "errors " + _main.ErrorCount + ": " + _main.LastError));
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
