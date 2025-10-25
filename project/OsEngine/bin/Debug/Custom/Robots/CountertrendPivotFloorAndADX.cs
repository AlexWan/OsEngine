/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Language;

/* Description
trading robot for osengine

The Countertrend robot on PivotFloor And ADX.

Buy:
1. The price touched the S2 level or closed below, then returned back and closed above the level.
2. The Adx indicator is falling and below a certain level (AdxLevel).
Sell:
1. The price touched the R2 level or closed higher, then returned back and closed below the level.
2. The Adx indicator is falling and below a certain level (AdxLevel).

Exit from buy: stop – S3, profit - R1.
Exit from sell: stop – R3, profit –S1.
 */

namespace OsEngine.Robots
{
    [Bot("CountertrendPivotFloorAndADX")] // We create an attribute so that we don't write anything to the BotFactory
    public class CountertrendPivotFloorAndADX : BotPanel
    {
        private BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _slippage;
        private StrategyParameterTimeOfDay _startTradeTime;
        private StrategyParameterTimeOfDay _endTradeTime;

        // GetVolume Settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator setting s
        private StrategyParameterString _pivotFloorPeriod;
        private StrategyParameterInt _periodADX;
        private StrategyParameterDecimal _adxLevel;

        // Indicator
        private Aindicator _pivotFloor;
        private Aindicator _ADX;

        // The last value of the indicator
        private decimal _lastR1;
        private decimal _lastR2;
        private decimal _lastR3;
        private decimal _lastS1;
        private decimal _lastS2;
        private decimal _lastS3;
        private decimal _lastADX;

        // The prev value of the indicator
        private decimal _prevADX;
        private decimal _prevS2;
        private decimal _prevR2;

        public CountertrendPivotFloorAndADX(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            _slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            _startTradeTime = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            _endTradeTime = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // GetVolume Settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator settings
            _pivotFloorPeriod = CreateParameter("Period", "Daily", new[] { "Daily", "Weekly" }, "Indicator");
            _periodADX = CreateParameter("ADX Length", 21, 7, 48, 7, "Indicator");
            _adxLevel = CreateParameter("ADX Level", 21.0m, 7, 48, 7, "Indicator");

            // Create indicator ChaikinOsc
            _pivotFloor = IndicatorsFactory.CreateIndicatorByName("PivotFloor", name + "PivotFloor", false);
            _pivotFloor = (Aindicator)_tab.CreateCandleIndicator(_pivotFloor, "Prime");
            ((IndicatorParameterString)_pivotFloor.Parameters[0]).ValueString = _pivotFloorPeriod.ValueString;
            _pivotFloor.Save();

            // Create indicator ADX
            _ADX = IndicatorsFactory.CreateIndicatorByName("ADX", name + "ADX", false);
            _ADX = (Aindicator)_tab.CreateCandleIndicator(_ADX, "NewArea");
            ((IndicatorParameterInt)_ADX.Parameters[0]).ValueInt = _periodADX.ValueInt;
            _ADX.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += CountertrendPivotFloorAndADX_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel192;
        }

        private void CountertrendPivotFloorAndADX_ParametrsChangeByUser()
        {
            ((IndicatorParameterString)_pivotFloor.Parameters[0]).ValueString = _pivotFloorPeriod.ValueString;
            _pivotFloor.Save();
            _pivotFloor.Reload();
            ((IndicatorParameterInt)_ADX.Parameters[0]).ValueInt = _periodADX.ValueInt;
            _ADX.Save();
            _ADX.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "CountertrendPivotFloorAndADX";
        }
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Candle Finished Event
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            // If the robot is turned off, exit the event handler
            if (_regime.ValueString == "Off")
            {
                return;
            }

            _lastR1 = _pivotFloor.DataSeries[1].Last;

            // If there are not enough candles to build an indicator, we exit
            if (_lastR1 == 0 ||
                candles.Count < _periodADX.ValueInt)
            {
                return;
            }

            // If the time does not match, we leave
            if (_startTradeTime.Value > _tab.TimeServerCurrent ||
                _endTradeTime.Value < _tab.TimeServerCurrent)
            {
                return;
            }

            List<Position> openPositions = _tab.PositionsOpenAll;

            // If there are positions, then go to the position closing method
            if (openPositions != null && openPositions.Count != 0)
            {
                LogicClosePosition(candles);
            }

            // If the position closing mode, then exit the method
            if (_regime.ValueString == "OnlyClosePosition")
            {
                return;
            }

            // If there are no positions, then go to the position opening method
            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles);
            }
        }

        // Opening logic
        private void LogicOpenPosition(List<Candle> candles)
        {
            // The last value of the indicator
            _lastR1 = _pivotFloor.DataSeries[1].Last;
            _lastR2 = _pivotFloor.DataSeries[2].Last;
            _lastR3 = _pivotFloor.DataSeries[3].Last;
            _lastS1 = _pivotFloor.DataSeries[4].Last;
            _lastS2 = _pivotFloor.DataSeries[5].Last;
            _lastS3 = _pivotFloor.DataSeries[6].Last;
            _lastADX = _ADX.DataSeries[0].Last;

            // The orev value of the indicator
            _prevR2 = _pivotFloor.DataSeries[2].Values[_pivotFloor.DataSeries[2].Values.Count - 2];
            _prevS2 = _pivotFloor.DataSeries[5].Values[_pivotFloor.DataSeries[5].Values.Count - 2];
            _prevADX = _ADX.DataSeries[0].Values[_ADX.DataSeries[0].Values.Count - 2];

            List <Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;
                decimal prevPrice = candles[candles.Count - 2].Close;

                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_prevS2 > prevPrice && _lastS2 < lastPrice && _prevADX > _lastADX && _lastADX < _adxLevel.ValueDecimal)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_prevR2 < prevPrice && _lastR2 > lastPrice && _prevADX > _lastADX && _lastADX < _adxLevel.ValueDecimal)
                    {
                        _tab.SellAtLimit(GetVolume(_tab), _tab.PriceBestBid - _slippage);
                    }
                }
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;
            
            decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    _tab.CloseAtProfit(pos, _lastR1, _lastR1 + _slippage);
                    _tab.CloseAtStop(pos, _lastS3, _lastS3 - _slippage);
                }
                else // If the direction of the position is sale
                {
                    _tab.CloseAtProfit(pos, _lastS1, _lastS1 - _slippage);
                    _tab.CloseAtStop(pos, _lastR3, _lastR3 + _slippage);
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