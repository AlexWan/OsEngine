/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Language;

/*Discription
Trading robot for osengine

Trend robot on the intersection of two Linear Regression Line and RSI.

Buy: 
1. The fast EMA crosses the slow ONE from bottom to top.
2. The RSI is above 50 and growing.

Sale:
1. The fast EMA crosses the slow ONE from top to bottom.
2. The RSI is above 50 and growing.

Exit:
Stop and profit in % of the entry price.
*/

namespace OsEngine.Robots
{
    [Bot("IntersectionOfTwoLinearRegressionLineAndRSI")] //We create an attribute so that we don't write anything in the Boot factory
    public class IntersectionOfTwoLinearRegressionLineAndRSI : BotPanel
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

        // Indicator Settings
        private StrategyParameterInt _periodRsi;
        private StrategyParameterInt _periodLRMAFast;
        private StrategyParameterInt _periodLRMASlow;
        
        // Indicator
        private Aindicator _Rsi;
        private Aindicator _LRMA1;
        private Aindicator _LRMA2;

        //The last value of the indicators
        private decimal _lastRsi;
        private decimal _prevRsi;
        private decimal _lastLRMAFast;
        private decimal _lastLRMASlow;
        private decimal _prevLRMAFast;
        private decimal _prevLRMASlow;

        // Exit Settings
        private StrategyParameterDecimal _stopValue;
        private StrategyParameterDecimal _profitValue;

        public IntersectionOfTwoLinearRegressionLineAndRSI(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic Settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            _slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            _startTradeTime = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            _endTradeTime = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // GetVolume Settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator Settings
            _periodRsi = CreateParameter("Period RSI", 15, 50, 300, 10, "Indicator");
            _periodLRMAFast = CreateParameter("Period LRMA Fast", 250, 50, 500, 20, "Indicator");
            _periodLRMASlow = CreateParameter("Period LRMA Slow", 500, 100, 1500, 100, "Indicator");
           
            // Creating an indicator RSI
            _Rsi = IndicatorsFactory.CreateIndicatorByName("RSI", name + "Rsi", false);
            _Rsi = (Aindicator)_tab.CreateCandleIndicator(_Rsi, "NewArea");
            ((IndicatorParameterInt)_Rsi.Parameters[0]).ValueInt = _periodRsi.ValueInt;
            _Rsi.Save();

            // Creating an indicator LRMA1
            _LRMA1 = IndicatorsFactory.CreateIndicatorByName("LinearRegressionLine", name + "LRMA1", false);
            _LRMA1 = (Aindicator)_tab.CreateCandleIndicator(_LRMA1, "Prime");
            ((IndicatorParameterInt)_LRMA1.Parameters[0]).ValueInt = _periodLRMAFast.ValueInt;
            _LRMA1.Save();

            // Creating an indicator LRMA2
            _LRMA2 = IndicatorsFactory.CreateIndicatorByName("LinearRegressionLine", name + "LRMA2", false);
            _LRMA2 = (Aindicator)_tab.CreateCandleIndicator(_LRMA2, "Prime");
            ((IndicatorParameterInt)_LRMA2.Parameters[0]).ValueInt = _periodLRMASlow.ValueInt;
            _LRMA2.DataSeries[0].Color = Color.Aquamarine;
            _LRMA2.Save();

            // Exit Settings
            _stopValue = CreateParameter("Stop percent", 0.5m, 1, 10, 1, "Exit settings");
            _profitValue = CreateParameter("Profit percent", 0.5m, 1, 10, 1, "Exit settings");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += IntersectionOfTwoLinearRegressionLineAndRSI_ParametrsChangeByUser;

            // Subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel210;
        }

        // Indicator Update event
        private void IntersectionOfTwoLinearRegressionLineAndRSI_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_Rsi.Parameters[0]).ValueInt = _periodRsi.ValueInt;
            _Rsi.Save();
            _Rsi.Reload();

            ((IndicatorParameterInt)_LRMA1.Parameters[0]).ValueInt = _periodLRMAFast.ValueInt;
            _LRMA1.Save();
            _LRMA1.Reload();

            ((IndicatorParameterInt)_LRMA2.Parameters[0]).ValueInt = _periodLRMASlow.ValueInt;
            _LRMA2.Save();
            _LRMA2.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "IntersectionOfTwoLinearRegressionLineAndRSI";
        }
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Candle Completion Event
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            // If the robot is turned off, exit the event handler
            if (_regime.ValueString == "Off")
            {
                return;
            }

            // If there are not enough candles to build an indicator, we exit
            if (candles.Count < _periodRsi.ValueInt || candles.Count < _periodLRMAFast.ValueInt
               || candles.Count < _periodLRMASlow.ValueInt)
            {
                return;
            }

            // If the time does not match, we exit
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

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // The last value of the indicators               
                _lastRsi = _Rsi.DataSeries[0].Last;
                _prevRsi = _Rsi.DataSeries[0].Values[_Rsi.DataSeries[0].Values.Count - 2];
                _lastLRMAFast = _LRMA1.DataSeries[0].Last;
                _prevLRMAFast = _LRMA1.DataSeries[0].Values[_LRMA1.DataSeries[0].Values.Count - 2];
                _lastLRMASlow = _LRMA2.DataSeries[0].Last;
                _prevLRMASlow = _LRMA2.DataSeries[0].Values[_LRMA2.DataSeries[0].Values.Count - 2];

                // Long
                if (_regime.ValueString != "OnlyShort") // if the mode is not only short, then we enter long
                {
                    if (_lastLRMAFast > _prevLRMAFast && _prevLRMAFast > _prevLRMASlow
                        && _lastLRMAFast > _lastLRMASlow && _lastRsi > 50 && _prevRsi < _lastRsi)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // if the mode is not only long, we enter the short
                {
                    if( _lastLRMAFast < _prevLRMAFast && _prevLRMAFast < _prevLRMASlow
                        && _lastLRMAFast < _lastLRMASlow && _lastRsi > 50 && _prevRsi < _lastRsi)
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

                decimal lastPrice = candles[candles.Count - 1].Close;

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is long
                {
                    decimal profitActivation = pos.EntryPrice + pos.EntryPrice * _profitValue.ValueDecimal / 100;
                    decimal stopActivation = pos.EntryPrice - pos.EntryPrice * _stopValue.ValueDecimal / 100;

                    _tab.CloseAtProfit(pos, profitActivation, profitActivation + _slippage);
                    _tab.CloseAtStop(pos, stopActivation, stopActivation - _slippage);
                }
                else // If the direction of the position is short
                {
                    decimal profitActivation = pos.EntryPrice - pos.EntryPrice * _profitValue.ValueDecimal / 100;
                    decimal stopActivation = pos.EntryPrice + pos.EntryPrice * _stopValue.ValueDecimal / 100;

                    _tab.CloseAtProfit(pos, profitActivation, profitActivation - _slippage);
                    _tab.CloseAtStop(pos, stopActivation, stopActivation + _slippage);
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