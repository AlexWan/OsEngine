/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;

/* Description
Trading robot for osEngine

The trend robot-screener on LinearRegression channel and Volatility group.

Buy:
1. The candle closed above the upper line of the Linear Regression Channel
2. Filter by volatility groups. All screener papers are divided into 3 groups. One of them is traded.

Exit for long: When the Linear Regression Channel bottom line is broken

*/

namespace OsEngine.Robots.AlgoStart
{
    [Bot("AlgoStart1LinearRegression")]
    public class AlgoStart1LinearRegression : BotPanel
    {
        private BotTabScreener _screenerTab;

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

        public AlgoStart1LinearRegression(string name, StartProgram startProgram) : base(name, startProgram)
        {

            // non trade periods
            _tradePeriodsSettings = new NonTradePeriods(name);

            _tradePeriodsSettings.NonTradePeriod1Start = new TimeOfDay() { Hour = 5, Minute = 0 };
            _tradePeriodsSettings.NonTradePeriod1End = new TimeOfDay() { Hour = 10, Minute = 05 };
            _tradePeriodsSettings.NonTradePeriod1OnOff = true;

            _tradePeriodsSettings.NonTradePeriod2Start = new TimeOfDay() { Hour = 13, Minute = 54 };
            _tradePeriodsSettings.NonTradePeriod2End = new TimeOfDay() { Hour = 14, Minute = 6 };
            _tradePeriodsSettings.NonTradePeriod2OnOff = false;

            _tradePeriodsSettings.NonTradePeriod3Start = new TimeOfDay() { Hour = 18, Minute = 1 };
            _tradePeriodsSettings.NonTradePeriod3End = new TimeOfDay() { Hour = 23, Minute = 58 };
            _tradePeriodsSettings.NonTradePeriod3OnOff = true;

            _tradePeriodsSettings.TradeInSunday = false;
            _tradePeriodsSettings.TradeInSaturday = false;

            _tradePeriodsSettings.Load();

            // Source creation

            TabCreate(BotTabType.Screener);
            _screenerTab = TabsScreener[0];

            // Subscribe to the candle finished event
            _screenerTab.CandleFinishedEvent += _screenerTab_CandleFinishedEvent;

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On"});
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


            // Create indicator LinearRegressionChannelFast_Indicator
            _screenerTab.CreateCandleIndicator(1, "LinearRegressionChannelFast_Indicator", new List<string>() { _lrLength.ValueInt.ToString(), "Close", _lrDeviation.ValueDecimal.ToString(), _lrDeviation.ValueDecimal.ToString() }, "Prime");

            // Create indicator Sma
            _screenerTab.CreateCandleIndicator(2, "Sma", new List<string>() { _smaFilterLen.ValueInt.ToString(), "Close" }, "Prime");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += SmaScreener_ParametrsChangeByUser;

            Description = OsLocalization.Description.DescriptionLabel324;
            DeleteEvent += AlgoStart1ScreenerLinearRegression_DeleteEvent;
        }

        private void AlgoStart1ScreenerLinearRegression_DeleteEvent()
        {
            try
            {
                _tradePeriodsSettings.Delete();
            }
            catch (Exception)
            {
                // ignore
            }
        }

        private void _tradePeriodsShowDialogButton_UserClickOnButtonEvent()
        {
            _tradePeriodsSettings.ShowDialog();
        }

        private void SmaScreener_ParametrsChangeByUser()
        {
            _screenerTab._indicators[0].Parameters
              = new List<string>()
             {
                 _lrLength.ValueInt.ToString(),
                 "Close",
                 _lrDeviation.ValueDecimal.ToString(),
                 _lrDeviation.ValueDecimal.ToString()
             };

            _screenerTab._indicators[1].Parameters
                = new List<string>() { _smaFilterLen.ValueInt.ToString(), "Close" };

            _screenerTab.UpdateIndicatorsParameters();
        }

        // Logic
        private void _screenerTab_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            if (candles.Count < 50)
            {
                return;
            }

            if (_tradePeriodsSettings.CanTradeThisTime(candles[^1].TimeStart) == false)
            {
                return;
            }

            List<Position> openPositions = tab.PositionsOpenAll;

            if (openPositions.Count == 0
                && _clusterToTrade.ValueInt != 0)
            {
                if (_lastTimeSetClusters == DateTime.MinValue
                 || _lastTimeSetClusters != candles[^1].TimeStart)
                {
                    _volatilityStageClusters.Calculate(_screenerTab.Tabs, _clustersLookBack.ValueInt);
                    _lastTimeSetClusters = candles[^1].TimeStart;
                }

                if (_clusterToTrade.ValueInt == 1)
                {
                    if (_volatilityStageClusters.ClusterOne.Find(source => source.Connector.SecurityName == tab.Connector.SecurityName) == null)
                    {
                        return;
                    }
                }
                else if (_clusterToTrade.ValueInt == 2)
                {
                    if (_volatilityStageClusters.ClusterTwo.Find(source => source.Connector.SecurityName == tab.Connector.SecurityName) == null)
                    {
                        return;
                    }
                }
                else if (_clusterToTrade.ValueInt == 3)
                {
                    if (_volatilityStageClusters.ClusterThree.Find(source => source.Connector.SecurityName == tab.Connector.SecurityName) == null)
                    {
                        return;
                    }
                }
                else
                {
                    return;
                }
            }

            List<Position> positions = tab.PositionsOpenAll;

            if (positions.Count == 0)
            { // Opening logic

                if (_screenerTab.PositionsOpenAll.Count >= _maxPositionsCount.ValueInt)
                {
                    return;
                }

                decimal candleClose = candles[candles.Count - 1].Close;

                Aindicator lrIndicator = (Aindicator)tab.Indicators[0];

                decimal lrUp = lrIndicator.DataSeries[0].Values[^1];
                decimal lrDown = lrIndicator.DataSeries[2].Values[^1];

                if (lrUp == 0
                    || lrDown == 0)
                {
                    return;
                }

                if (candleClose > lrUp)
                {
                    if (_smaFilterIsOn.ValueBool == true)
                    {// Sma filter
                        Aindicator sma = (Aindicator)tab.Indicators[1];

                        decimal lastSma = sma.DataSeries[0].Last;

                        if (candleClose < lastSma)
                        {
                            return;
                        }
                    }

                    tab.BuyAtIcebergMarket(GetVolume(tab), _icebergCount.ValueInt, 1000);
                }
            }
            else // Logic close position
            {
                Position pos = positions[0];

                if (pos.State != PositionStateType.Open)
                {
                    return;
                }

                Aindicator lrIndicator = (Aindicator)tab.Indicators[0];

                decimal lrDown = lrIndicator.DataSeries[2].Last;

                if (lrDown == 0)
                {
                    return;
                }

                decimal lastClose = candles[^1].Close;

                if (lastClose <= lrDown)
                {
                    tab.CloseAtIcebergMarket(pos, pos.OpenVolume, _icebergCount.ValueInt, 1000);
                }
            }
        }

        // Method for calculating the volume of entry into a position
        private decimal GetVolume(BotTabSimple tab)
        {
            decimal volume = 0;

            if (_volumeType.ValueString == "Contracts")
            {
                volume = _volume.ValueDecimal;
            }
            else if (_volumeType.ValueString == "Contract currency")
            {
                decimal contractPrice = tab.PriceBestAsk;
                volume = _volume.ValueDecimal / contractPrice;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);

                    if (serverPermission != null &&
                        serverPermission.IsUseLotToCalculateProfit &&
                    tab.Security.Lot != 0 &&
                        tab.Security.Lot > 1)
                    {
                        volume = _volume.ValueDecimal / (contractPrice * tab.Security.Lot);
                    }

                    volume = Math.Round(volume, tab.Security.DecimalsVolume);
                }
                else // Tester or Optimizer
                {
                    volume = Math.Round(volume, 6);
                }
            }
            else if (_volumeType.ValueString == "Deposit percent")
            {
                Portfolio myPortfolio = tab.Portfolio;

                if (myPortfolio == null)
                {
                    return 0;
                }

                decimal portfolioPrimeAsset = 0;

                if (_tradeAssetInPortfolio.ValueString == "Prime")
                {
                    portfolioPrimeAsset = myPortfolio.ValueCurrent;
                }
                else
                {
                    List<PositionOnBoard> positionOnBoard = myPortfolio.GetPositionOnBoard();

                    if (positionOnBoard == null)
                    {
                        return 0;
                    }

                    for (int i = 0; i < positionOnBoard.Count; i++)
                    {
                        if (positionOnBoard[i].SecurityNameCode == _tradeAssetInPortfolio.ValueString)
                        {
                            portfolioPrimeAsset = positionOnBoard[i].ValueCurrent;
                            break;
                        }
                    }
                }

                if (portfolioPrimeAsset == 0)
                {
                    SendNewLogMessage("Can`t found portfolio " + _tradeAssetInPortfolio.ValueString, Logging.LogMessageType.Error);
                    return 0;
                }

                decimal moneyOnPosition = portfolioPrimeAsset * (_volume.ValueDecimal / 100);

                decimal qty = moneyOnPosition / tab.PriceBestAsk / tab.Security.Lot;

                if (tab.StartProgram == StartProgram.IsOsTrader)
                {
                    if (tab.Security.UsePriceStepCostToCalculateVolume == true
                        && tab.Security.PriceStep != tab.Security.PriceStepCost
                        && tab.PriceBestAsk != 0
                        && tab.Security.PriceStep != 0
                        && tab.Security.PriceStepCost != 0)
                    {// расчёт количества контрактов для фьючерсов и опционов на Мосбирже
                        qty = moneyOnPosition / (tab.PriceBestAsk / tab.Security.PriceStep * tab.Security.PriceStepCost);
                    }

                    qty = Math.Round(qty, tab.Security.DecimalsVolume);
                }
                else
                {
                    qty = Math.Round(qty, 7);
                }

                return qty;
            }

            return volume;
        }
    }
}