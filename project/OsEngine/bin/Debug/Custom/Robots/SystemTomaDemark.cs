using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Drawing;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System.Linq;
using OsEngine.Logging;
using System.Windows.Media.Animation;
using System.Security.Cryptography;

/* Description
trading robot for osengine

The trend robot on Strategy System Toma Demark.

Buy: Fast Ema above slow Ema and Momentum line above Momentum (e.g. above 100);

Sell: Fast Ema below slow Ema and Momentum line below Momentum Cell (e.g. below 100);

Exit from buy: trailing stop in % of the loy of the candle on which you entered.
Exit from sell: trailing stop in % of the high of the candle on which you entered.
 */


namespace OsEngine.Robots.AO
{
    [Bot("SystemTomaDemark")] // We create an attribute so that we don't write anything to the BotFactory
    public class SystemTomaDemark : BotPanel
    {
        private BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString Regime;
        private StrategyParameterString VolumeRegime;
        private StrategyParameterDecimal VolumeOnPosition;
        private StrategyParameterDecimal Slippage;
        private StrategyParameterTimeOfDay StartTradeTime;
        private StrategyParameterTimeOfDay EndTradeTime;

        // Indicator setting 
        private StrategyParameterInt _EmaPeriodFast;
        private StrategyParameterInt _EmaPeriodSlow;
        private StrategyParameterInt _MomentumPeriod;
        private StrategyParameterInt _MomentumBuy;
        private StrategyParameterInt _MomentumSell;

        // Indicator
        Aindicator _EmaFast;
        Aindicator _EmaSlow;
        Aindicator _Momentum;

        // The last value of the indicator
        private decimal _lastEmaFast;
        private decimal _lastEmaSlow;
        private decimal _lastMomentum;

        // Exit
        private StrategyParameterDecimal TrailingValue;

        public SystemTomaDemark(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic setting
            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            VolumeRegime = CreateParameter("Volume type", "Number of contracts", new[] { "Number of contracts", "Contract currency" }, "Base");
            VolumeOnPosition = CreateParameter("Volume", 1, 1.0m, 50, 4, "Base");
            Slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            StartTradeTime = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            EndTradeTime = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // Indicator setting
            _EmaPeriodFast = CreateParameter("Ema Length Fast", 50, 7, 480, 7, "Indicator");
            _EmaPeriodSlow = CreateParameter("Ema Length Slow", 100, 7, 480, 7, "Indicator");
            _MomentumPeriod = CreateParameter("Momentum Period", 16, 10, 300, 7, "Indicator");
            _MomentumBuy = CreateParameter("Momentum Buy", 32, 10, 300, 10, "Indicator");
            _MomentumSell = CreateParameter("Momentum Sell", 8, 10, 300, 10, "Indicator");

            // Create indicator Ema Fast
            _EmaFast = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EMAFast", false);
            _EmaFast = (Aindicator)_tab.CreateCandleIndicator(_EmaFast, "Prime");
            ((IndicatorParameterInt)_EmaFast.Parameters[0]).ValueInt = _EmaPeriodFast.ValueInt;
            _EmaFast.Save();

            // Create indicator Ema Slow
            _EmaSlow = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EMASlow", false);
            _EmaSlow = (Aindicator)_tab.CreateCandleIndicator(_EmaSlow, "Prime");
            ((IndicatorParameterInt)_EmaSlow.Parameters[0]).ValueInt = _EmaPeriodSlow.ValueInt;
            _EmaSlow.Save();

            // Create indicator MACD
            _Momentum = IndicatorsFactory.CreateIndicatorByName("Momentum", name + "Momentum", false);
            _Momentum = (Aindicator)_tab.CreateCandleIndicator(_Momentum, "NewArea");
            ((IndicatorParameterInt)_Momentum.Parameters[0]).ValueInt = _MomentumPeriod.ValueInt;
            _Momentum.Save();

            // Exit
            TrailingValue = CreateParameter("Stop Value", 1.0m, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += SystemTomaDemark_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Strategy System Toma Demark. " +
                "Buy: Fast Ema above slow Ema and Momentum line above Momentum (e.g. above 100); " +
                "Sell: Fast Ema below slow Ema and Momentum line below Momentum Cell (e.g. below 100); " +
                "Exit from buy: trailing stop in % of the loy of the candle on which you entered. " +
                "Exit from sell: trailing stop in % of the high of the candle on which you entered.";
        }

        private void SystemTomaDemark_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_EmaFast.Parameters[0]).ValueInt = _EmaPeriodFast.ValueInt;
            _EmaFast.Save();
            _EmaFast.Reload();
            ((IndicatorParameterInt)_EmaSlow.Parameters[0]).ValueInt = _EmaPeriodSlow.ValueInt;
            _EmaSlow.Save();
            _EmaSlow.Reload();
            ((IndicatorParameterInt)_Momentum.Parameters[0]).ValueInt = _MomentumPeriod.ValueInt;
            _Momentum.Save();
            _Momentum.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "SystemTomaDemark";
        }
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Candle Finished Event
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            // If the robot is turned off, exit the event handler
            if (Regime.ValueString == "Off")
            {
                return;
            }

            // If there are not enough candles to build an indicator, we exit
            if (candles.Count < _EmaPeriodFast.ValueInt ||
                candles.Count < _EmaPeriodSlow.ValueInt ||
                candles.Count < _MomentumPeriod.ValueInt ||
                candles.Count < _MomentumBuy.ValueInt ||
                candles.Count < _MomentumSell.ValueInt)
            {
                return;
            }

            // If the time does not match, we leave
            if (StartTradeTime.Value > _tab.TimeServerCurrent ||
                EndTradeTime.Value < _tab.TimeServerCurrent)
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
            if (Regime.ValueString == "OnlyClosePosition")
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
            _lastEmaFast = _EmaFast.DataSeries[0].Last;
            _lastEmaSlow = _EmaSlow.DataSeries[0].Last;
            _lastMomentum = _Momentum.DataSeries[0].Last;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastEmaFast > _lastEmaSlow && _lastMomentum > _MomentumBuy.ValueInt)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastEmaFast < _lastEmaSlow && _lastMomentum < _MomentumSell.ValueInt)
                    {
                        _tab.SellAtLimit(GetVolume(), _tab.PriceBestBid - _slippage);
                    }
                }
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;
            Position pos = openPositions[0];

            decimal stopPrice;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position positions = openPositions[i];

                if (positions.State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {
                    decimal lov = candles[candles.Count - 1].Low;
                    stopPrice = lov - lov * TrailingValue.ValueDecimal / 100;
                }
                else // If the direction of the position is sale
                {
                    decimal high = candles[candles.Count - 1].High;
                    stopPrice = high + high * TrailingValue.ValueDecimal / 100;
                }
                _tab.CloseAtTrailingStop(pos, stopPrice, stopPrice);
            }
        }

        // Method for calculating the volume of entry into a position
        private decimal GetVolume()
        {
            decimal volume = 0;

            if (VolumeRegime.ValueString == "Contract currency")
            {
                decimal contractPrice = _tab.PriceBestAsk;
                volume = VolumeOnPosition.ValueDecimal / contractPrice;
            }
            else if (VolumeRegime.ValueString == "Number of contracts")
            {
                volume = VolumeOnPosition.ValueDecimal;
            }

            // If the robot is running in the tester
            if (StartProgram == StartProgram.IsTester)
            {
                volume = Math.Round(volume, 6);
            }
            else
            {
                volume = Math.Round(volume, _tab.Securiti.DecimalsVolume);
            }
            return volume;
        }
    }
}
