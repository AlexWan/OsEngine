/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Indicators;
using System;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.Language;

/* Description
Trading robot for osengine

The trend robot on LinearRegressionFast indicator.

Buy:
1. If the ADX filter is active, and the value is zero — no entry occurs.
2. If the SMA filter is active, and the current candle close price (candleClose) is below the SMA — no entry occurs.
3. If the last candle's close price (candleClose) is above this line (lrUp) — a buy is initiated.

Exit: In case of breakdown of the reverse side of the channel
 */

namespace OsEngine.Robots.Screeners
{
    [Bot("LinearRegressionFastScreener")] // We create an attribute so that we don't write anything to the BotFactory
    public class LinearRegressionFastScreener : BotPanel
    {
        private BotTabScreener _screenerTab;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterInt _maxPoses;
        private StrategyParameterInt _icebergOrdersCount;
        private StrategyParameterTimeOfDay _timeStart;
        private StrategyParameterTimeOfDay _timeEnd;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator settings
        private StrategyParameterBool _adxFilterIsOn;
        private StrategyParameterInt _adxFilterLength;
        private StrategyParameterDecimal _minAdxValue;
        private StrategyParameterDecimal _maxAdxValue;
        private StrategyParameterInt _lrLength;
        private StrategyParameterDecimal _lrDeviation;
        private StrategyParameterBool _smaFilterIsOn;
        private StrategyParameterInt _smaFilterLen;

        public LinearRegressionFastScreener(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);
            _screenerTab = TabsScreener[0];

            // Subscribe to the candle finished event
            _screenerTab.CandleFinishedEvent += _screenerTab_CandleFinishedEvent;

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });
            _maxPoses = CreateParameter("Max poses", 5, 1, 20, 1);
            _icebergOrdersCount = CreateParameter("Iceberg orders count", 1, 1, 20, 1);
            _timeStart = CreateParameterTimeOfDay("Start Trade Time", 10, 32, 0, 0);
            _timeEnd = CreateParameterTimeOfDay("End Trade Time", 18, 25, 0, 0);

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator settings
            _adxFilterIsOn = CreateParameter("ADX filter is on", true);
            _adxFilterLength = CreateParameter("ADX filter Len", 30, 10, 100, 3);
            _minAdxValue = CreateParameter("ADX min value", 10, 20, 90, 1m);
            _maxAdxValue = CreateParameter("ADX max value", 40, 20, 90, 1m);
            _smaFilterIsOn = CreateParameter("Sma filter is on", true);
            _smaFilterLen = CreateParameter("Sma filter Len", 100, 100, 300, 10);
            _lrLength = CreateParameter("Linear regression Length", 50, 20, 300, 10);
            _lrDeviation = CreateParameter("Linear regression deviation", 2, 1, 4, 0.1m);

            // Create indicator ADX
            _screenerTab.CreateCandleIndicator(1, "ADX", new List<string>() { _adxFilterLength.ValueInt.ToString()}, "Second");

            // Create indicator LinearRegressionChannelFast_Indicator
            _screenerTab.CreateCandleIndicator(2, "LinearRegressionChannelFast_Indicator",new List<string>() {_lrLength.ValueInt.ToString(), "Close", _lrDeviation.ValueDecimal.ToString(), _lrDeviation.ValueDecimal.ToString()}, "Prime");

            // Create indicator Sma
            _screenerTab.CreateCandleIndicator(3, "Sma", new List<string>() { _smaFilterLen.ValueInt.ToString(), "Close" }, "Prime");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += SmaScreener_ParametrsChangeByUser;

            Description = OsLocalization.Description.DescriptionLabel88;
        }

        private void SmaScreener_ParametrsChangeByUser()
        {
            _screenerTab._indicators[0].Parameters 
                = new List<string>() { _adxFilterLength.ValueInt.ToString()};

            _screenerTab._indicators[1].Parameters
              = new List<string>()
             {
                 _lrLength.ValueInt.ToString(),
                 "Close",
                 _lrDeviation.ValueDecimal.ToString(),
                 _lrDeviation.ValueDecimal.ToString()
             };

            _screenerTab._indicators[2].Parameters 
                = new List<string>() { _smaFilterLen.ValueInt.ToString(), "Close" };

            _screenerTab.UpdateIndicatorsParameters();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "LinearRegressionFastScreener";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Logic
        private void _screenerTab_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            if (candles.Count < 10)
            {
                return;
            }

            if (_timeStart.Value > tab.TimeServerCurrent ||
                _timeEnd.Value < tab.TimeServerCurrent)
            {
                return;
            }

            List<Position> positions = tab.PositionsOpenAll;

            if (positions.Count == 0)
            { // Opening logic

                int allPosesInAllTabs = _screenerTab.PositionsOpenAll.Count;

                if (allPosesInAllTabs >= _maxPoses.ValueInt)
                {
                    return;
                }

                if(_adxFilterIsOn.ValueBool == true)
                {// Adx filter
                    Aindicator adx = (Aindicator)tab.Indicators[0];

                    decimal adxLast = adx.DataSeries[0].Last;

                    if(adxLast == 0)
                    {
                        return;
                    }

                    if (adxLast < _minAdxValue.ValueDecimal
                        || adxLast > _maxAdxValue.ValueDecimal)
                    {
                        return;
                    }
                }

                decimal candleClose = candles[candles.Count - 1].Close;

                if (_smaFilterIsOn.ValueBool == true)
                {// Sma filter
                    Aindicator sma = (Aindicator)tab.Indicators[2];

                    decimal lastSma = sma.DataSeries[0].Last;

                    if(candleClose < lastSma)
                    {   
                        return;
                    }
                }

                Aindicator lrIndicator = (Aindicator)tab.Indicators[1];

                decimal lrUp = lrIndicator.DataSeries[0].Values[lrIndicator.DataSeries[0].Values.Count - 1];

                if (lrUp == 0)
                {
                    return;
                }

                if (candleClose > lrUp)
                {
                    tab.BuyAtIcebergMarket(GetVolume(tab), _icebergOrdersCount.ValueInt, 2000);
                }
            }
            else // Logic close position
            {
                Position pos = positions[0];

                if (pos.State != PositionStateType.Open)
                {
                    return;
                }

                Aindicator lrIndicator = (Aindicator)tab.Indicators[1];

                decimal lrDown = lrIndicator.DataSeries[2].Last;

                if (lrDown == 0)
                {
                    return;
                }

                decimal lastCandleClose = candles[candles.Count - 1].Close;

                if (lastCandleClose < lrDown)
                {
                    tab.CloseAtIcebergMarket(pos, pos.OpenVolume, _icebergOrdersCount.ValueInt, 2000);
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