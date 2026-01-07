/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Tester;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;


namespace OsEngine.Robots.FuturesStart
{
    [Bot("FuturesStartContangoScreener")]
    public class FuturesStartContangoScreener : BotPanel
    {
        BotTabSimple _base1;
        BotTabScreener _futs1;

        BotTabSimple _base2;
        BotTabScreener _futs2;

        BotTabSimple _base3;
        BotTabScreener _futs3;

        BotTabSimple _base4;
        BotTabScreener _futs4;

        BotTabSimple _base5;
        BotTabScreener _futs5;

        BotTabSimple _base6;
        BotTabScreener _futs6;

        BotTabSimple _base7;
        BotTabScreener _futs7;

        BotTabSimple _base8;
        BotTabScreener _futs8;

        BotTabSimple _base9;
        BotTabScreener _futs9;

        BotTabSimple _base10;
        BotTabScreener _futs10;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterInt _icebergCount;
        private StrategyParameterInt _maxPositionsCount;
        private StrategyParameterInt _clusterToTrade;
        private StrategyParameterInt _clustersLookBack;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator settings
        private StrategyParameterInt _lrLength;
        private StrategyParameterDecimal _lrDeviation;
        private StrategyParameterBool _smaFilterIsOn;
        private StrategyParameterInt _smaFilterLen;

        // Volatility clusters
        private VolatilityStageClusters _volatilityStageClusters = new VolatilityStageClusters();
        private DateTime _lastTimeSetClusters;

        // Trade periods
        private NonTradePeriods _tradePeriodsSettings;
        private StrategyParameterButton _tradePeriodsShowDialogButton;
        private void _tradePeriodsShowDialogButton_UserClickOnButtonEvent()
        {
            _tradePeriodsSettings.ShowDialog();
        }

        public FuturesStartContangoScreener(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // non trade periods
            _tradePeriodsSettings = new NonTradePeriods(name);

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1Start = new TimeOfDay() { Hour = 0, Minute = 0 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1End = new TimeOfDay() { Hour = 10, Minute = 05 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1OnOff = true;

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2Start = new TimeOfDay() { Hour = 13, Minute = 54 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2End = new TimeOfDay() { Hour = 14, Minute = 6 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2OnOff = false;

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3Start = new TimeOfDay() { Hour = 18, Minute = 1 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3End = new TimeOfDay() { Hour = 23, Minute = 58 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3OnOff = true;

            _tradePeriodsSettings.TradeInSunday = false;
            _tradePeriodsSettings.TradeInSaturday = false;

            _tradePeriodsSettings.Load();

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });
            _icebergCount = CreateParameter("Iceberg orders count", 1, 1, 3, 1);
            _clusterToTrade = CreateParameter("Volatility cluster to trade", 1, 1, 3, 1);
            _clustersLookBack = CreateParameter("Volatility cluster lookBack", 30, 10, 300, 1);
            _maxPositionsCount = CreateParameter("Max positions ", 10, 1, 50, 4);
            _tradePeriodsShowDialogButton = CreateParameterButton("Non trade periods");
            _tradePeriodsShowDialogButton.UserClickOnButtonEvent += _tradePeriodsShowDialogButton_UserClickOnButtonEvent;

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 10, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator settings
            _smaFilterIsOn = CreateParameter("Sma filter is on", true);
            _smaFilterLen = CreateParameter("Sma filter Len", 170, 100, 300, 10);
            _lrLength = CreateParameter("Linear regression Length", 180, 20, 300, 10);
            _lrDeviation = CreateParameter("Linear regression deviation", 2.4m, 1, 4, 0.1m);

            // Source creation

            _base1 = TabCreate<BotTabSimple>();
            _futs1 = TabCreate<BotTabScreener>();
            CreateIndicators(_base1, _futs1);

            _base2 = TabCreate<BotTabSimple>();
            _futs2 = TabCreate<BotTabScreener>();
            CreateIndicators(_base2, _futs2);

            _base3 = TabCreate<BotTabSimple>();
            _futs3 = TabCreate<BotTabScreener>();
            CreateIndicators(_base3, _futs3);

            _base4 = TabCreate<BotTabSimple>();
            _futs4 = TabCreate<BotTabScreener>();
            CreateIndicators(_base4, _futs4);

            _base5 = TabCreate<BotTabSimple>();
            _futs5 = TabCreate<BotTabScreener>();
            CreateIndicators(_base5, _futs5);

            _base6 = TabCreate<BotTabSimple>();
            _futs6 = TabCreate<BotTabScreener>();
            CreateIndicators(_base6, _futs6);

            _base7 = TabCreate<BotTabSimple>();
            _futs7 = TabCreate<BotTabScreener>();
            CreateIndicators(_base7, _futs7);

            _base8 = TabCreate<BotTabSimple>();
            _futs8 = TabCreate<BotTabScreener>();
            CreateIndicators(_base8, _futs8);

            _base9 = TabCreate<BotTabSimple>();
            _futs9 = TabCreate<BotTabScreener>();
            CreateIndicators(_base9, _futs9);

            _base10 = TabCreate<BotTabSimple>();
            _futs10 = TabCreate<BotTabScreener>();
            CreateIndicators(_base10, _futs10);

            ParametrsChangeByUser += FuturesStartContangoScreener_ParametrsChangeByUser;

            if(StartProgram == StartProgram.IsTester)
            {
                if (ServerMaster.GetServers() != null
                    && ServerMaster.GetServers().Count > 0)
                {
                    IServer tester = ServerMaster.GetServers()[0];

                    if (tester.ServerType == ServerType.Tester)
                    {
                        ((TesterServer)tester).EndNextMinuteWithCandlesEvent += Tester_EndNextMinuteWithCandlesEvent;
                    }
                }
            }
        }

        private void Tester_EndNextMinuteWithCandlesEvent()
        {
             
        }

        private void FuturesStartContangoScreener_ParametrsChangeByUser()
        {
            UpdateSettingsInIndicators(_base1, _futs1);
            UpdateSettingsInIndicators(_base2, _futs2);
            UpdateSettingsInIndicators(_base3, _futs3);
            UpdateSettingsInIndicators(_base4, _futs4);
            UpdateSettingsInIndicators(_base5, _futs5);
            UpdateSettingsInIndicators(_base6, _futs6);
            UpdateSettingsInIndicators(_base7, _futs7);
            UpdateSettingsInIndicators(_base8, _futs8);
            UpdateSettingsInIndicators(_base9, _futs9);
            UpdateSettingsInIndicators(_base10, _futs10);
        }

        private void CreateIndicators(BotTabSimple baseSource, BotTabScreener futuresSource)
        {
            Aindicator linearRegressionInd = IndicatorsFactory.CreateIndicatorByName("LinearRegressionChannelFast_Indicator", "Lr", false);
            linearRegressionInd = (Aindicator)baseSource.CreateCandleIndicator(linearRegressionInd, "Prime");
            ((IndicatorParameterInt)linearRegressionInd.Parameters[0]).ValueInt = _lrLength.ValueInt;
            ((IndicatorParameterDecimal)linearRegressionInd.Parameters[1]).ValueDecimal = _lrDeviation.ValueDecimal;
            ((IndicatorParameterDecimal)linearRegressionInd.Parameters[2]).ValueDecimal = _lrDeviation.ValueDecimal;
            linearRegressionInd.Save();

            futuresSource.CreateCandleIndicator(1, "LinearRegressionChannelFast_Indicator", new List<string>() { _lrLength.ValueInt.ToString(), "Close", _lrDeviation.ValueDecimal.ToString(), _lrDeviation.ValueDecimal.ToString() }, "Prime");
        }

        private void UpdateSettingsInIndicators(BotTabSimple baseSource, BotTabScreener futuresSource)
        {
            Aindicator linearRegressionInd = (Aindicator)baseSource.Indicators[0];
            bool isChanged = false;

            if(((IndicatorParameterInt)linearRegressionInd.Parameters[0]).ValueInt != _lrLength.ValueInt)
            {
                ((IndicatorParameterInt)linearRegressionInd.Parameters[0]).ValueInt = _lrLength.ValueInt;
                isChanged = true;
            }
            
            if(((IndicatorParameterDecimal)linearRegressionInd.Parameters[1]).ValueDecimal != _lrDeviation.ValueDecimal)
            {
                ((IndicatorParameterDecimal)linearRegressionInd.Parameters[1]).ValueDecimal = _lrDeviation.ValueDecimal;
                isChanged = true;
            }
            if(((IndicatorParameterDecimal)linearRegressionInd.Parameters[2]).ValueDecimal != _lrDeviation.ValueDecimal)
            {
                ((IndicatorParameterDecimal)linearRegressionInd.Parameters[2]).ValueDecimal = _lrDeviation.ValueDecimal;
                isChanged = true;
            }
            
            if(isChanged)
            {
                linearRegressionInd.Save();
                linearRegressionInd.Reload();
            }

            futuresSource._indicators[0].Parameters
             = new List<string>()
             {
                 _lrLength.ValueInt.ToString(),
                 "Close",
                 _lrDeviation.ValueDecimal.ToString(),
                 _lrDeviation.ValueDecimal.ToString()
             };

            futuresSource.UpdateIndicatorsParameters();
        }

        // Logic Entry



        // Logic



    }
}
