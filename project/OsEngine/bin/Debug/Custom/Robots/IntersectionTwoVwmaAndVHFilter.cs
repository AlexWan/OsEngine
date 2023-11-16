using System;
using System.Collections.Generic;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
Trading robot for osengine.

The trend robot on Intersection Two Vwma And VHFilter.

Buy:
1. Price is higher than fast Vwma, fast is higher than slow Vwma.
2. VHFilter value is lower (higher) than minLevel and growing.
Sell:
1. The price is lower than the fast Vwma, the fast is lower than the slow Vwma.
2. VHFilter value is lower (higher) than minLevel and growing.

Exit: reverse intersection of Vwma.
 */

namespace OsEngine.Robots.My_bots
{
    [Bot("IntersectionTwoVwmaAndVHFilter")] // We create an attribute so that we don't write anything to the BotFactory
    public class IntersectionTwoVwmaAndVHFilter : BotPanel
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
        private StrategyParameterInt LengthVHF;
        private StrategyParameterDecimal MinLevel;
        private StrategyParameterInt PeriodVWMAFast;
        private StrategyParameterInt PeriodVWMASlow;

        // Indicator
        Aindicator _VHF;
        Aindicator _VWMAFast;
        Aindicator _VWMASlow;

        // The last value of the indicator
        private decimal _lastVHF;
        private decimal _lastVWMAFast;
        private decimal _lastVWMASlow;

        // The prev value of the indicator
        private decimal _prevVHF;


        public IntersectionTwoVwmaAndVHFilter(string name, StartProgram startProgram) : base(name, startProgram)
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
            LengthVHF = CreateParameter("VHF Length", 10, 7, 48, 7, "Indicator");
            MinLevel = CreateParameter("Min Level", 1.0m, 1, 5, 0.1m, "Indicator");
            PeriodVWMAFast = CreateParameter("Period SMA Fast", 100, 10, 300, 10, "Indicator");
            PeriodVWMASlow = CreateParameter("Period SMA Slow", 200, 10, 300, 10, "Indicator");

            // Create indicator SmaFast
            _VWMAFast = IndicatorsFactory.CreateIndicatorByName("VWMA", name + "VWMAFast", false);
            _VWMAFast = (Aindicator)_tab.CreateCandleIndicator(_VWMAFast, "Prime");
            ((IndicatorParameterInt)_VWMAFast.Parameters[0]).ValueInt = PeriodVWMAFast.ValueInt;
            _VWMAFast.Save();

            // Create indicator SmaSlow
            _VWMASlow = IndicatorsFactory.CreateIndicatorByName("VWMA", name + "VWMASlow", false);
            _VWMASlow = (Aindicator)_tab.CreateCandleIndicator(_VWMASlow, "Prime");
            _VWMASlow.DataSeries[0].Color = System.Drawing.Color.Yellow;
            ((IndicatorParameterInt)_VWMASlow.Parameters[0]).ValueInt = PeriodVWMASlow.ValueInt;
            _VWMASlow.Save();

            // Create indicator VHF
            _VHF = IndicatorsFactory.CreateIndicatorByName("VHFilter", name + "VHFilter", false);
            _VHF = (Aindicator)_tab.CreateCandleIndicator(_VHF, "NewArea");
            ((IndicatorParameterInt)_VHF.Parameters[0]).ValueInt = LengthVHF.ValueInt;
            _VHF.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += IntersectionTwoVwmaAndVHFilter_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Intersection Two Vwma And VHFilter. " +
                "Buy: " +
                "1. Price is higher than fast Vwma, fast is higher than slow Vwma. " +
                "2. VHFilter value is lower (higher) than minLevel and growing. " +
                "Sell: " +
                "1. The price is lower than the fast Vwma, the fast is lower than the slow Vwma. " +
                "2. VHFilter value is lower (higher) than minLevel and growing. " +
                "Exit: reverse intersection of Vwma.";
        }

        private void IntersectionTwoVwmaAndVHFilter_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_VHF.Parameters[0]).ValueInt = LengthVHF.ValueInt;
            _VHF.Save();
            _VHF.Reload();
            ((IndicatorParameterInt)_VWMAFast.Parameters[0]).ValueInt = PeriodVWMAFast.ValueInt;
            _VWMAFast.Save();
            _VWMAFast.Reload();
            ((IndicatorParameterInt)_VWMASlow.Parameters[0]).ValueInt = PeriodVWMASlow.ValueInt;
            _VWMASlow.Save();
            _VWMASlow.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "IntersectionTwoVwmaAndVHFilter";
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
            if (candles.Count < PeriodVWMAFast.ValueInt ||
                candles.Count < LengthVHF.ValueInt ||
                candles.Count < PeriodVWMASlow.ValueInt)
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
            _lastVHF = _VHF.DataSeries[0].Last;
            _lastVWMAFast = _VWMAFast.DataSeries[0].Last;
            _lastVWMASlow = _VWMASlow.DataSeries[0].Last;

            // The prev value of the indicator
            _prevVHF = _VHF.DataSeries[0].Values[_VHF.DataSeries[0].Values.Count - 2];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastVWMAFast < lastPrice && _lastVWMAFast > _lastVWMASlow && _lastVHF < MinLevel.ValueDecimal && _prevVHF < _lastVHF)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastVWMAFast > lastPrice && _lastVWMAFast < _lastVWMASlow && _lastVHF < MinLevel.ValueDecimal && _prevVHF < _lastVHF)
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

            // The last value of the indicators
            _lastVWMAFast = _VWMAFast.DataSeries[0].Last;
            _lastVWMASlow = _VWMASlow.DataSeries[0].Last;

            decimal lastPrice = candles[candles.Count - 1].Close;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }


                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {
                    if (_lastVWMAFast < _lastVWMASlow)
                    {
                        _tab.CloseAtLimit(openPositions[0], lastPrice - _slippage, openPositions[0].OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (_lastVWMAFast > _lastVWMASlow)
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