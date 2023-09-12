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
using System.Data.SqlClient;

/* Description
trading robot for osengine

The trend robot on Intersection Kalman With ChannelEma.

Buy:
1. The price is above the Kalman and above the upper line of the Ema channel.
2. Kalman is above the upper line of the Ema channel.

Sell:
1. The price is below the Kalman and below the lower line of the Ema channel.
2. The Kalman is below the lower line of the Ema channel.

Exit from buy: the kalman is below the upper line.

Exit from sell: Kalman is above the bottom line.

 */


namespace OsEngine.Robots.AO
{
    [Bot("IntersectionKalmanWithChannelEma")] // We create an attribute so that we don't write anything to the BotFactory
    public class IntersectionKalmanWithChannelEma : BotPanel
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
        private StrategyParameterDecimal Sharpness;
        private StrategyParameterDecimal CoefK;
        private StrategyParameterInt LengthVwmaChannel;

        // Indicator
        Aindicator _Kalman;
        Aindicator _VwmaHigh;
        Aindicator _VwmaLow;

        // The last value of the indicator
        private decimal _lastKalman;
        private decimal _lastVWMAHigh;
        private decimal _lastVWMALow;


        public IntersectionKalmanWithChannelEma(string name, StartProgram startProgram) : base(name, startProgram)
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
            Sharpness = CreateParameter("Sharpness", 1.0m, 1, 50, 1, "Indicator");
            CoefK = CreateParameter("CoefK", 1.0m, 1, 50, 1, "Indicator");
            LengthVwmaChannel = CreateParameter("Period VWMA", 100, 10, 300, 1, "Indicator");

            // Create indicator ChaikinOsc
            _Kalman = IndicatorsFactory.CreateIndicatorByName("KalmanFilter", name + "KalmanFilter", false);
            _Kalman = (Aindicator)_tab.CreateCandleIndicator(_Kalman, "Prime");
            ((IndicatorParameterDecimal)_Kalman.Parameters[0]).ValueDecimal = Sharpness.ValueDecimal;
            ((IndicatorParameterDecimal)_Kalman.Parameters[1]).ValueDecimal = CoefK.ValueDecimal;
            _Kalman.Save();

            // Create indicator VwmaHigh
            _VwmaHigh = IndicatorsFactory.CreateIndicatorByName("VWMA", name + "Vwma High", false);
            _VwmaHigh = (Aindicator)_tab.CreateCandleIndicator(_VwmaHigh, "Prime");
            ((IndicatorParameterInt)_VwmaHigh.Parameters[0]).ValueInt = LengthVwmaChannel.ValueInt;
            ((IndicatorParameterString)_VwmaHigh.Parameters[1]).ValueString = "High";
            _VwmaHigh.Save();

            // Create indicator VwmaLow
            _VwmaLow = IndicatorsFactory.CreateIndicatorByName("VWMA", name + "Vwma Low", false);
            _VwmaLow = (Aindicator)_tab.CreateCandleIndicator(_VwmaLow, "Prime");
            ((IndicatorParameterInt)_VwmaLow.Parameters[0]).ValueInt = LengthVwmaChannel.ValueInt;
            ((IndicatorParameterString)_VwmaLow.Parameters[1]).ValueString = "Low";
            _VwmaLow.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += IntersectionKalmanWithChannelEma_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Intersection Kalman With ChannelEma. " +
                "Buy: " +
                "1. The price is above the Kalman and above the upper line of the Ema channel. " +
                "2. Kalman is above the upper line of the Ema channel. " +
                "Sell: " +
                "1. The price is below the Kalman and below the lower line of the Ema channel. " +
                "2. The Kalman is below the lower line of the Ema channel. " +
                "Exit from buy: the kalman is below the upper line. " +
                "Exit from sell: Kalman is above the bottom line.";
        }

        private void IntersectionKalmanWithChannelEma_ParametrsChangeByUser()
        {
            ((IndicatorParameterDecimal)_Kalman.Parameters[0]).ValueDecimal = Sharpness.ValueDecimal;
            ((IndicatorParameterDecimal)_Kalman.Parameters[1]).ValueDecimal = CoefK.ValueDecimal;
            _Kalman.Save();
            _Kalman.Reload();
            ((IndicatorParameterInt)_VwmaHigh.Parameters[0]).ValueInt = LengthVwmaChannel.ValueInt;
            ((IndicatorParameterString)_VwmaHigh.Parameters[1]).ValueString = "High";
            _VwmaHigh.Save();
            _VwmaHigh.Reload();
            ((IndicatorParameterInt)_VwmaLow.Parameters[0]).ValueInt = LengthVwmaChannel.ValueInt;
            ((IndicatorParameterString)_VwmaLow.Parameters[1]).ValueString = "Low";
            _VwmaLow.Save();
            _VwmaLow.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "IntersectionKalmanWithChannelEma";
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
            if (candles.Count < CoefK.ValueDecimal ||
                candles.Count < Sharpness.ValueDecimal ||
                candles.Count < LengthVwmaChannel.ValueInt)
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
            _lastKalman = _Kalman.DataSeries[0].Last;
            _lastVWMAHigh = _VwmaHigh.DataSeries[0].Last;
            _lastVWMALow = _VwmaLow.DataSeries[0].Last;


            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {


                    if (_lastKalman < lastPrice && _lastVWMAHigh < lastPrice && _lastKalman > _lastVWMAHigh)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {

                    if (_lastKalman > lastPrice && _lastVWMALow > lastPrice && _lastKalman < _lastVWMALow)
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

            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            // The last value of the indicator
            _lastKalman = _Kalman.DataSeries[0].Last;
            _lastVWMAHigh = _VwmaHigh.DataSeries[0].Last;
            _lastVWMALow = _VwmaLow.DataSeries[0].Last;

            decimal lastPrice = candles[candles.Count - 1].Close;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {
                    if (_lastKalman < _lastVWMAHigh)
                    {
                        _tab.CloseAtLimit(openPositions[0], lastPrice - _slippage, openPositions[0].OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (_lastKalman > _lastVWMALow)
                    {
                        _tab.CloseAtLimit(openPositions[0], lastPrice + _slippage, openPositions[0].OpenVolume);
                    }
                }
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
