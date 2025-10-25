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
using System.Linq;
using OsEngine.Market.Servers;
using OsEngine.Market;
using System.Drawing;
using OsEngine.Language;

/* Description
trading robot for osengine

The trend robot on Break DeMarker and the intersection of two exponential averages.

Buy: When the DeMarker indicator value is above the maximum for the period and the fast Ema is higher than the slow Ema.

Sell: When the DeMarker indicator value is below the minimum for the period and the fast Ema is below the slow Ema.

Exit from buy: When fast Ema is lower than slow Ema.

Exit from sell: When fast Ema is higher than slow Ema.
 */

namespace OsEngine.Robots
{
    [Bot("BreakDeMarkerAndIntersectionOfTwoEma")] // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    internal class BreakDeMarkerAndIntersectionOfTwoEma : BotPanel
    {
        // Reference to the main trading tab
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

        // Indicator settings
        private StrategyParameterInt _deMLength;
        private StrategyParameterInt _lengthEmaFast;
        private StrategyParameterInt _lengthEmaSlow;

        // Enter settings
        private StrategyParameterInt _entryCandlesLong;
        private StrategyParameterInt _entryCandlesShort;

        // Indicator
        private Aindicator _deM;
        private Aindicator _ema1;
        private Aindicator _ema2;

        public BreakDeMarkerAndIntersectionOfTwoEma(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create and assign the main trading tab
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
            _deMLength = CreateParameter("DeM Length", 14, 5, 200, 10, "Indicator");
            _lengthEmaFast = CreateParameter("fast EMA1 period", 30, 10, 300, 10, "Indicator");
            _lengthEmaSlow = CreateParameter("slow EMA2 period", 100, 50, 500, 10, "Indicator");

            // Enter settings
            _entryCandlesLong = CreateParameter("Entry Candles Long", 10, 5, 200, 5, "Enter");
            _entryCandlesShort = CreateParameter("Entry Candles Short", 10, 5, 200, 5, "Enter");

            // Create indicator DeMarker
            _deM = IndicatorsFactory.CreateIndicatorByName("DeMarker_DeM", name + "DeMarker", false);
            _deM = (Aindicator)_tab.CreateCandleIndicator(_deM, "DeMArea");
            ((IndicatorParameterInt)_deM.Parameters[0]).ValueInt = _deMLength.ValueInt;
            _deM.Save();

            // Creating indicator Ema1
            _ema1 = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EMA1", false);
            _ema1 = (Aindicator)_tab.CreateCandleIndicator(_ema1, "Prime");
            ((IndicatorParameterInt)_ema1.Parameters[0]).ValueInt = _lengthEmaFast.ValueInt;
            _ema1.DataSeries[0].Color = Color.Red;
            _ema1.Save();

            // Creating indicator Ema2
            _ema2 = IndicatorsFactory.CreateIndicatorByName("Ema", name + "Ema2", false);
            _ema2 = (Aindicator)_tab.CreateCandleIndicator(_ema2, "Prime");
            ((IndicatorParameterInt)_ema2.Parameters[0]).ValueInt = _lengthEmaSlow.ValueInt;
            _ema2.DataSeries[0].Color = Color.Green;
            _ema2.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += BreakDeMarker_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel145;
        }

        private void BreakDeMarker_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_deM.Parameters[0]).ValueInt = _deMLength.ValueInt;
            _deM.Save();
            _deM.Reload();

            ((IndicatorParameterInt)_ema1.Parameters[0]).ValueInt = _lengthEmaFast.ValueInt;
            _ema1.Save();
            _ema1.Reload();

            ((IndicatorParameterInt)_ema2.Parameters[0]).ValueInt = _lengthEmaSlow.ValueInt;
            _ema2.Save();
            _ema2.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "BreakDeMarkerAndIntersectionOfTwoEma";
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
            if (candles.Count < _deMLength.ValueInt + 10 ||
                candles.Count < _lengthEmaFast.ValueInt || candles.Count < _lengthEmaSlow.ValueInt)
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
            decimal lastDeM = _deM.DataSeries[0].Last;
            decimal lastEmaFast = _ema1.DataSeries[0].Last;
            decimal lastEmaSlow = _ema2.DataSeries[0].Last;

            // Indicator not ready
            if (lastEmaSlow == 0 || lastEmaFast == 0)
            {
                return;
            }

            if (openPositions == null || openPositions.Count == 0)
            {
                List<decimal> values = _deM.DataSeries[0].Values;

                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (EnterLong(values, _entryCandlesLong.ValueInt) < lastDeM && lastEmaFast > lastEmaSlow)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (EnterShort(values, _entryCandlesShort.ValueInt) > lastDeM && lastEmaFast < lastEmaSlow)
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

            // The last value of the indicator
            decimal lastEmaFast = _ema1.DataSeries[0].Last;
            decimal lastEmaSlow = _ema2.DataSeries[0].Last;

            // Slippage
            decimal _slippage = this._slippage.ValueDecimal / 100 * _tab.Securiti.PriceStep;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is long
                {
                    if (lastEmaFast < lastEmaSlow)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice - _slippage, openPositions[i].OpenVolume);
                    }
                }
                else // If the direction of the position is short
                {
                    if (lastEmaFast > lastEmaSlow)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice + _slippage, openPositions[i].OpenVolume);
                    }
                }
            }
        }

        // Method for finding the maximum for a period
        private decimal EnterLong(List<decimal> values, int period)
        {
            decimal Max = -9999999;

            for (int i = values.Count - 2; i > values.Count - 2 - period; i--)
            {
                if (i < 0)
                {
                    return Max;
                }

                if (values[i] > Max)
                {
                    Max = values[i];
                }
            }

            return Max;
        }

        // Method for finding the minimum for a period
        private decimal EnterShort(List<decimal> values, int period)
        {
            decimal Min = 9999999;

            for (int i = values.Count - 2; i > values.Count - 2 - period; i--)
            {
                if (i < 0)
                {
                    return Min;
                }

                if (values[i] < Min)
                {
                    Min = values[i];
                }
            }

            return Min;
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