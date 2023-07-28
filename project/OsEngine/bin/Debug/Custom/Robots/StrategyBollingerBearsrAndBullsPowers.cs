using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Drawing;

/*Discription
Trading robot for osengine.

Trend strategy on Bears Power Divergence.

Sale:
1. Bulls Power columns must be higher than 0;
2. The highs on the chart are rising, and on the indicator they are decreasing

Exit:
The Bulls Power indicator has become lower.
*/

namespace OsEngine.Robots.My_bots
{
    [Bot("StrategyBollingerBearsrAndBullsPowers")]
    public class StrategyBollingerBearsrAndBullsPowers : BotPanel
    {
        private BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString Regime;
        private StrategyParameterString VolumeRegime;
        private StrategyParameterDecimal VolumeOnPosition;
        private StrategyParameterDecimal Slippage;
        private StrategyParameterTimeOfDay StartTradeTime;
        private StrategyParameterTimeOfDay EndTradeTime;

        // Indicator Settings
        private StrategyParameterInt BollingerLength;
        private StrategyParameterDecimal BollingerDeviation;
        private StrategyParameterInt BearsPeriod;
        private StrategyParameterInt BullsPeriod;      

        // Indicator
        private Aindicator Bollinger;
        private Aindicator _bullsPower;
        private Aindicator _bearsPower;

        //Exit
        private StrategyParameterInt TrailBars;

        // The last value of the indicators      
        private decimal _lastUpBollinger;
        private decimal _lastDownBollinger;
        private decimal _lastBears;
        private decimal _lastBulls;


        public StrategyBollingerBearsrAndBullsPowers(string name, StartProgram startProgram) : base(name, startProgram)
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

            // Indicator Settings
            BollingerLength = CreateParameter("BollingerLength", 250, 50, 500, 20, "Indicator");
            BollingerDeviation = CreateParameter("BollingerDeviation", 0.2m, 0.01m, 2, 0.02m, "Indicator");
            BearsPeriod = CreateParameter("Bears Period", 20, 10, 300, 10, "Indicator");
            BullsPeriod = CreateParameter("Bulls Period", 20, 10, 300, 10, "Indicator");
           

             // Create indicator Ema
            Bollinger = IndicatorsFactory.CreateIndicatorByName("Bollinger", name + "Bollinger", false);
            Bollinger = (Aindicator)_tab.CreateCandleIndicator(Bollinger, "Prime");
            ((IndicatorParameterInt)Bollinger.Parameters[0]).ValueInt = BollingerLength.ValueInt;
            ((IndicatorParameterDecimal)Bollinger.Parameters[1]).ValueDecimal = BollingerDeviation.ValueDecimal;
            Bollinger.DataSeries[0].Color = Color.Red;
            Bollinger.DataSeries[1].Color = Color.Red;
            Bollinger.Save();

            // Create indicator BullsPower
            _bullsPower = IndicatorsFactory.CreateIndicatorByName("BullsPower", name + "BullsPower", false);
            _bullsPower = (Aindicator)_tab.CreateCandleIndicator(_bullsPower, "NewArea0");
            ((IndicatorParameterInt)_bullsPower.Parameters[0]).ValueInt = BullsPeriod.ValueInt;

            // Create indicator BearsPower
            _bearsPower = IndicatorsFactory.CreateIndicatorByName("BearsPower", name + "BearsPower", false);
            _bearsPower = (Aindicator)_tab.CreateCandleIndicator(_bearsPower, "NewArea1");
            ((IndicatorParameterInt)_bearsPower.Parameters[0]).ValueInt = BearsPeriod.ValueInt;

            // Exit
            TrailBars = CreateParameter("TrailBars", 1, 1, 10, 1, "Exit settings");


            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyBollingerBearsrAndBullsPowers_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "Trend strategy on Bears Power Divergence." +
                "Sale:"+
              
                "1.Bulls Power columns must be higher than" +
                "2.The highs on the chart are rising, and on the indicator they are decreasing" +
               
                "Exit" +
                "The Bulls Power indicator has become lower.";
        }

        // Indicator Update event
        private void StrategyBollingerBearsrAndBullsPowers_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)Bollinger.Parameters[0]).ValueInt = BollingerLength.ValueInt;
            ((IndicatorParameterDecimal)Bollinger.Parameters[1]).ValueDecimal = BollingerDeviation.ValueDecimal;
            Bollinger.Save();
            Bollinger.Reload();

            ((IndicatorParameterInt)_bearsPower.Parameters[0]).ValueInt = BearsPeriod.ValueInt;
            _bearsPower.Save();
            _bearsPower.Reload();

            ((IndicatorParameterInt)_bullsPower.Parameters[0]).ValueInt = BullsPeriod.ValueInt;
            _bullsPower.Save();
            _bullsPower.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyBollingerBearsrAndBullsPowers";
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
            if (candles.Count < BollingerLength.ValueInt || candles.Count < BearsPeriod.ValueInt || candles.Count < BullsPeriod.ValueInt)
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
            List<Position> openPositions = _tab.PositionsOpenAll;

            // The last value of the indicators
            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;
            decimal lastPrice = candles[candles.Count - 1].Close;

            // He last value of the indicator           
            _lastUpBollinger = Bollinger.DataSeries[0].Last;
            _lastDownBollinger = Bollinger.DataSeries[1].Last; ;
            _lastBulls = _bullsPower.DataSeries[0].Last;
            _lastBears = _bearsPower.DataSeries[0].Last;
            if (openPositions == null || openPositions.Count == 0)
            {
                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastUpBollinger < lastPrice && _lastBears > 0 && _lastBulls > 0)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {

                    if (_lastDownBollinger > lastPrice && _lastBulls < 0 && _lastBears < 0)
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

            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            decimal lastPrice = candles[candles.Count - 1].Close;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position positions = openPositions[i];

                if (positions.State != PositionStateType.Open)
                {
                    continue;
                }

                if (positions.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    decimal price = GetPriceStop(Side.Buy, candles, candles.Count - 1);
                    if (price == 0)
                    {
                        return;
                    }
                    _tab.CloseAtTrailingStop(openPositions[0], price, price - _slippage);
                }
                else // If the direction of the position is sale
                {
                    decimal price = GetPriceStop(Side.Sell, candles, candles.Count - 1);
                    if (price == 0)
                    {
                        return;
                    }
                    _tab.CloseAtTrailingStop(openPositions[0], price, price + _slippage);
                }

            }
        }
        private decimal GetPriceStop(Side side, List<Candle> candles, int index)
        {
            if (candles == null || index < TrailBars.ValueInt || index < TrailBars.ValueInt)
            {
                return 0;
            }

            if (side == Side.Buy)
            {
                decimal price = decimal.MaxValue;

                for (int i = index; i > index - TrailBars.ValueInt; i--)
                {
                    if (candles[i].Low < price)
                    {
                        price = candles[i].Low;
                    }
                }
                return price;
            }

            if (side == Side.Sell)
            {
                decimal price = 0;

                for (int i = index; i > index - TrailBars.ValueInt; i--)
                {
                    if (candles[i].High > price)
                    {
                        price = candles[i].High;
                    }
                }

                return price;
            }
            return 0;
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
