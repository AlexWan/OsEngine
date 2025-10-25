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
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Language;

/*Discription
Trading robot for osengine

Trend robot on the Strategy Sma, Ema, Parabolic And MacdLine.

Buy:
1. Ema above Sma;
2. The price is higher than the Parabolic value;
3. Macd line (green) above the signal line (red);
Sell:
1. The Ema is lower than the Sma;
2. The price is below the Parabolic value;
3. Macd line (green) below the signal line (red);

Exit from buy: The Ema is below the Sma.
Exit from sell: Ema is higher than Sma.
*/

namespace OsEngine.Robots
{
    [Bot("StrategySmaEmaParabolicAndMacdLine")] //We create an attribute so that we don't write anything in the Boot factory
    public class StrategySmaEmaParabolicAndMacdLine : BotPanel
    {
        BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _slippage;
        private StrategyParameterTimeOfDay _timeStart;
        private StrategyParameterTimeOfDay _timeEnd;

        // GetVolume Settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator Settings
        private StrategyParameterInt _fastPeriod;
        private StrategyParameterInt _slowPeriod;
        private StrategyParameterInt _signalPeriod;
        private StrategyParameterInt _periodSma;
        private StrategyParameterInt _periodEma;
        private StrategyParameterDecimal _step;
        private StrategyParameterDecimal _maxStep;

        // Indicator
        private Aindicator _sma;
        private Aindicator _ema;
        private Aindicator _macdLine;
        private Aindicator _parabolic;

        //The last value of the indicators
        private decimal _lastSma;
        private decimal _lastEma;
        private decimal _lastMacdSignal;
        private decimal _lastMacdGreen;
        private decimal _lastParabolic;

        public StrategySmaEmaParabolicAndMacdLine(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic Settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            _slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            _timeStart = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            _timeEnd = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // GetVolume Settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator Settings
            _fastPeriod = CreateParameter("Fast period", 12, 50, 300, 1, "Indicator");
            _slowPeriod = CreateParameter("Slow period", 26, 50, 300, 1, "Indicator");
            _signalPeriod = CreateParameter("Signal Period", 9, 50, 300, 1, "Indicator");
            _periodSma = CreateParameter("Period Sma", 100, 10, 300, 10, "Indicator");
            _periodEma = CreateParameter("Period Ema", 200, 10, 300, 10, "Indicator");
            _step = CreateParameter("Step", 10, 10.0m, 300, 10, "Indicator");
            _maxStep = CreateParameter("Max Step", 20, 10.0m, 300, 10, "Indicator");

            // Create indicator Sma
            _sma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "Sma", false);
            _sma = (Aindicator)_tab.CreateCandleIndicator(_sma, "Prime");
            ((IndicatorParameterInt)_sma.Parameters[0]).ValueInt = _periodSma.ValueInt;
            _sma.Save();

            // Create indicator Ema
            _ema = IndicatorsFactory.CreateIndicatorByName("Ema", name + "Ema", false);
            _ema = (Aindicator)_tab.CreateCandleIndicator(_ema, "Prime");
            ((IndicatorParameterInt)_ema.Parameters[0]).ValueInt = _periodEma.ValueInt;
            _ema.Save();

            // Create indicator macd
            _macdLine = IndicatorsFactory.CreateIndicatorByName("MacdLine", name + "MacdLine", false);
            _macdLine = (Aindicator)_tab.CreateCandleIndicator(_macdLine, "NewArea");
            ((IndicatorParameterInt)_macdLine.Parameters[0]).ValueInt = _fastPeriod.ValueInt;
            ((IndicatorParameterInt)_macdLine.Parameters[1]).ValueInt = _slowPeriod.ValueInt;
            ((IndicatorParameterInt)_macdLine.Parameters[2]).ValueInt = _signalPeriod.ValueInt;
            _macdLine.Save();

            // Create indicator Parabolic
            _parabolic = IndicatorsFactory.CreateIndicatorByName("ParabolicSAR", name + "Parabolic", false);
            _parabolic = (Aindicator)_tab.CreateCandleIndicator(_parabolic, "Prime");
            ((IndicatorParameterDecimal)_parabolic.Parameters[0]).ValueDecimal = _step.ValueDecimal;
            ((IndicatorParameterDecimal)_parabolic.Parameters[1]).ValueDecimal = _maxStep.ValueDecimal;
            _parabolic.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategySmaEmaParabolicAndMacdLine_ParametrsChangeByUser;

            // Subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel275;
        }

        // Indicator Update event
        private void StrategySmaEmaParabolicAndMacdLine_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_macdLine.Parameters[0]).ValueInt = _fastPeriod.ValueInt;
            ((IndicatorParameterInt)_macdLine.Parameters[1]).ValueInt = _slowPeriod.ValueInt;
            ((IndicatorParameterInt)_macdLine.Parameters[2]).ValueInt = _signalPeriod.ValueInt;
            _macdLine.Save();
            _macdLine.Reload();
            ((IndicatorParameterInt)_sma.Parameters[0]).ValueInt = _periodSma.ValueInt;
            _sma.Save();
            _sma.Reload();
            ((IndicatorParameterInt)_ema.Parameters[0]).ValueInt = _periodEma.ValueInt;
            _ema.Save();
            _ema.Reload();
            ((IndicatorParameterDecimal)_parabolic.Parameters[0]).ValueDecimal = _step.ValueDecimal;
            ((IndicatorParameterDecimal)_parabolic.Parameters[1]).ValueDecimal = _maxStep.ValueDecimal;
            _parabolic.Save();
            _parabolic.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategySmaEmaParabolicAndMacdLine";
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
            if (candles.Count < _fastPeriod.ValueInt)
            {
                return;
            }

            // If the time does not match, we exit
            if (_timeStart.Value > _tab.TimeServerCurrent ||
                _timeEnd.Value < _tab.TimeServerCurrent)
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
                _lastMacdSignal = _macdLine.DataSeries[1].Last;
                _lastMacdGreen = _macdLine.DataSeries[0].Last;
                _lastSma = _sma.DataSeries[0].Last;
                _lastEma = _ema.DataSeries[0].Last;
                _lastParabolic = _parabolic.DataSeries[0].Last;

                decimal lastPrice = candles[candles.Count - 1].Close;

                // Long
                if (_regime.ValueString != "OnlyShort") // if the mode is not only short, then we enter long
                {
                    if (_lastMacdGreen > _lastMacdSignal &&
                        _lastSma < _lastEma &&
                        lastPrice > _lastParabolic)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // if the mode is not only long, we enter the short
                {
                    if (_lastMacdGreen < _lastMacdSignal &&
                        _lastSma > _lastEma &&
                        lastPrice < _lastParabolic)
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
            decimal lastPrice = candles[candles.Count - 1].Close;

            // The last value of the indicators
            _lastSma = _sma.DataSeries[0].Last;
            _lastEma = _ema.DataSeries[0].Last;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is long
                {
                    if (_lastEma < _lastSma)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice - _slippage, openPositions[i].OpenVolume);
                    }
                }
                else // If the direction of the position is short
                {
                    if (_lastEma > _lastSma)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice + _slippage, openPositions[i].OpenVolume);
                    }
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