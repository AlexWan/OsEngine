/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Indicators;
using OsEngine.Language;

/* Description
Trading robot for osEngine

The trend robot-screener on Adaptive Price Channel and Volatility group.

Buy:
1. The candle closed above the upper line of the Price Channel
2. Filter by volatility groups. All screener papers are divided into 3 groups. One of them is traded.

Exit for long: When the Price Channel bottom line is broken

*/

namespace OsEngine.Robots.AlgoStart
{
    [Bot("AlgoStart3PriceChannel")]
    public class AlgoStart3PriceChannel : BotPanel
    {
        private BotTabScreener _screenerTab;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterInt _icebergCount;
        private StrategyParameterInt _maxPositions;
        private StrategyParameterInt _clusterToTrade;
        private StrategyParameterInt _clustersLookBack;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator settings
        private StrategyParameterInt _pcAdxLength;
        private StrategyParameterInt _pcRatio;
        private StrategyParameterBool _smaFilterIsOn;
        private StrategyParameterInt _smaFilterLen;

        // Trade periods
        private NonTradePeriods _tradePeriodsSettings;
        private StrategyParameterButton _tradePeriodsShowDialogButton;

        // Volatility clusters
        private VolatilityStageClusters _volatilityStageClusters = new VolatilityStageClusters();
        private DateTime _lastTimeSetClusters;

        public AlgoStart3PriceChannel(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // non trade periods
            _tradePeriodsSettings = new NonTradePeriods(name);

            _tradePeriodsSettings.NonTradePeriod1Start = new TimeOfDay() { Hour = 5, Minute = 0 };
            _tradePeriodsSettings.NonTradePeriod1End = new TimeOfDay() { Hour = 9, Minute = 55 };
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
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });
            _icebergCount = CreateParameter("Iceberg orders count", 1, 1, 3, 1);
            _clusterToTrade = CreateParameter("Volatility cluster to trade", 2, 1, 3, 1);
            _clustersLookBack = CreateParameter("Volatility cluster lookBack", 100, 10, 300, 1);
            _maxPositions = CreateParameter("Max poses", 20, 1, 20, 1);
            _tradePeriodsShowDialogButton = CreateParameterButton("Non trade periods");
            _tradePeriodsShowDialogButton.UserClickOnButtonEvent += _tradePeriodsShowDialogButton_UserClickOnButtonEvent;

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 8, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator settings
            _pcAdxLength = CreateParameter("Pc adx length", 50, 5, 300, 1);
            _pcRatio = CreateParameter("Pc ratio", 840, 5, 2000, 1);
            _smaFilterIsOn = CreateParameter("Sma filter is on", true);
            _smaFilterLen = CreateParameter("Sma filter Len", 70, 100, 300, 10);

            // Create indicator PriceChannelAdaptive
            _screenerTab.CreateCandleIndicator(2,
                "PriceChannelAdaptive",
                new List<string>() { _pcAdxLength.ValueInt.ToString(), _pcRatio.ValueInt.ToString() },
                "Prime");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += SmaScreener_ParametrsChangeByUser;

            Description = OsLocalization.Description.DescriptionLabel326;
            DeleteEvent += AlgoStart3ScreenerPriceChannel_DeleteEvent;
        }

        private void SmaScreener_ParametrsChangeByUser()
        {
            _screenerTab._indicators[0].Parameters = new List<string>() { _pcAdxLength.ValueInt.ToString(), _pcRatio.ValueInt.ToString() };
            _screenerTab.UpdateIndicatorsParameters();
        }

        private void AlgoStart3ScreenerPriceChannel_DeleteEvent()
        {
            _tradePeriodsSettings.Delete();
        }

        private void _tradePeriodsShowDialogButton_UserClickOnButtonEvent()
        {
            _tradePeriodsSettings.ShowDialog();
        }

        // Logic
        private void _screenerTab_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
        {
            // 1 If there is a position, then we close the trailing stop

            // 2 There is no pose. Open long if the last N candles we were above the moving average

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

            if (positions.Count == 0) // Open position logic
            {
                int allPosesInAllTabs = this.PositionsCount;

                if (allPosesInAllTabs >= _maxPositions.ValueInt)
                {
                    return;
                }

                Aindicator priceChannel = (Aindicator)tab.Indicators[0];

                decimal pcUp = priceChannel.DataSeries[0].Values[priceChannel.DataSeries[0].Values.Count - 2];

                if (pcUp == 0)
                {
                    return;
                }

                decimal candleClose = candles[candles.Count - 1].Close;

                if (candleClose > pcUp)
                {

                    if (_smaFilterIsOn.ValueBool == true)
                    {
                        decimal smaValue = Sma(candles, _smaFilterLen.ValueInt, candles.Count - 1);
                        decimal smaPrev = Sma(candles, _smaFilterLen.ValueInt, candles.Count - 2);

                        if (smaValue < smaPrev)
                        {
                            return;
                        }
                    }

                    tab.BuyAtIcebergMarket(GetVolume(tab), _icebergCount.ValueInt, 1000);
                }
            }
            else // Close logic
            {
                Position pos = positions[0];

                if (pos.State != PositionStateType.Open)
                {
                    return;
                }

                Aindicator priceChannel = (Aindicator)tab.Indicators[0];

                decimal pcDown = priceChannel.DataSeries[1].Values[^2];

                if (pcDown == 0)
                {
                    return;
                }

                decimal lastClose = candles[^1].Close;

                if(lastClose <= pcDown)
                {
                    tab.CloseAtIcebergMarket(pos,pos.OpenVolume,_icebergCount.ValueInt,1000);
                }
            }
        }

        // Method for calculating Sma
        private decimal Sma(List<Candle> candles, int len, int index)
        {
            if (candles.Count == 0
                || index >= candles.Count
                || index <= 0)
            {
                return 0;
            }

            decimal summ = 0;

            int countPoints = 0;

            for (int i = index; i >= 0 && i > index - len; i--)
            {
                countPoints++;
                summ += candles[i].Close;
            }

            if (countPoints == 0)
            {
                return 0;
            }

            return summ / countPoints;
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