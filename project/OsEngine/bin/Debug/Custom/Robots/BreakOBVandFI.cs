/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Drawing;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Language;

/* Description
trading robot for osengine

The trend robot on strategy Break OBV and FI.

Buy:
1. The value of the OBV indicator broke through the maximum for a certain number of candles and closed higher.
2. The values of the force index indicator cross the 0 level from bottom to top.

Sell:
1. The value of the OBV indicator broke through the minimum for a certain number of candles and closed lower.
2. The values of the force index indicator cross the level 0 from top to bottom.

Exit from buy:
trailing stop in % of the low of the last candle.

Exit from sell:
trailing stop in % of the high of the last candle.
 */

namespace OsEngine.Robots
{
    [Bot("BreakOBVandFI")] // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    public class BreakOBVandFI : BotPanel
    {
        // Reference to the main trading tab
        private BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _slippage;
        private StrategyParameterTimeOfDay _startTradeTime;
        private StrategyParameterTimeOfDay _endTradeTime;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator setting 
        private StrategyParameterInt _lengthFI;

        // Indicators
        private Aindicator _fI;
        private Aindicator _oBV;

        // Enter settings
        private StrategyParameterInt _entryCandlesLong;
        private StrategyParameterInt _entryCandlesShort;

        // Exit setting
        private StrategyParameterDecimal _trailingValue;

        // The last value of the indicator
        private decimal _lastFI;

        // The prev value of the indicator
        private decimal _prevFI;

        public BreakOBVandFI(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create and assign the main trading tab
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            _slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            _startTradeTime = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            _endTradeTime = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" }, "Base");
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4, "Base");
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime", "Base");

            // Indicator setting
            _lengthFI = CreateParameter("FI Length", 21, 7, 48, 7, "Indicator");

            // Create indicator FI
            _fI = IndicatorsFactory.CreateIndicatorByName("ForceIndex", name + "ForceIndex", false);
            _fI = (Aindicator)_tab.CreateCandleIndicator(_fI, "NewArea");
            ((IndicatorParameterInt)_fI.Parameters[0]).ValueInt = _lengthFI.ValueInt;
            _fI.Save();

            // Create indicator OBV
            _oBV = IndicatorsFactory.CreateIndicatorByName("OBV", name + "OBV", false);
            _oBV = (Aindicator)_tab.CreateCandleIndicator(_oBV, "NewArea0");
            _oBV.Save();

            // Enter settings
            _entryCandlesLong = CreateParameter("Entry Candles Long", 10, 5, 200, 5, "Enter");
            _entryCandlesShort = CreateParameter("Entry Candles Short", 10, 5, 200, 5, "Enter");

            // Exit setting
            _trailingValue = CreateParameter("Stop Value", 1.0m, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += BreakOBVandFI_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel158;
        }

        // Indicator update event
        private void BreakOBVandFI_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_fI.Parameters[0]).ValueInt = _lengthFI.ValueInt;
            _fI.Save();
            _fI.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "BreakOBVandFI";
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

            // If there are not enough candles to build an indicator, we exit
            if (candles.Count < _entryCandlesShort.ValueInt + 10 || candles.Count < _entryCandlesLong.ValueInt + 10)
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
            List<Position> openPositions = _tab.PositionsOpenAll;

            // The last value of the indicator
            _lastFI = _fI.DataSeries[0].Last;

            // The prev value of the indicator
            _prevFI = _fI.DataSeries[0].Values[_fI.DataSeries[0].Values.Count - 2];

            if (openPositions == null || openPositions.Count == 0)
            {
                List<decimal> values = _oBV.DataSeries[0].Values;

                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (EnterLongAndShort(values, _entryCandlesLong.ValueInt) == "true" &&
                        _lastFI > 0 && _prevFI < 0)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (EnterLongAndShort(values, _entryCandlesShort.ValueInt) == "false" &&
                        _lastFI < 0 && _prevFI > 0)
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

            decimal stopPrice;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    decimal lov = candles[candles.Count - 1].Low;
                    stopPrice = lov - lov * _trailingValue.ValueDecimal / 100;
                }
                else // If the direction of the position is sale
                {
                    decimal high = candles[candles.Count - 1].High;
                    stopPrice = high + high * _trailingValue.ValueDecimal / 100;
                }

                _tab.CloseAtTrailingStop(pos, stopPrice, stopPrice);
            }
        }

        private string EnterLongAndShort(List<decimal> values, int period)
        {
            decimal Max = -9999999;
            decimal Min = 9999999;

            for (int i = 1; i <= period; i++)
            {
                if (values[values.Count - 1 - i] > Max)
                {
                    Max = values[values.Count - 1 - i];
                }

                if (values[values.Count - 1 - i] < Min)
                {
                    Min = values[values.Count - 1 - i];
                }               
            }

            if (Max < values[values.Count - 1])
            {
                return "true";
            }
            else if (Min > values[values.Count - 1])
            {
                return "false";
            }

            return "nope";
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