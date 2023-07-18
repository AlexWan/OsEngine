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

Trend robot at the intersection of three smoothed averages.

Buy: Fast Ssma is higher than slow Ssma.

Sale: Fast Ssma is lower than slow Ssma.

Exit: on the opposite signal.
*/

[Bot("IntersectionOfThreeSsma")] // We create an attribute so that we don't write anything in the Boot factory
 public class IntersectionOfThreeSsma : BotPanel
{
    BotTabSimple _tab;

    // Basic Settings
    private StrategyParameterString Regime;
    private StrategyParameterDecimal VolumeOnPosition;
    private StrategyParameterString VolumeRegime;
    private StrategyParameterDecimal Slippage;
    private StrategyParameterTimeOfDay TimeStart;
    private StrategyParameterTimeOfDay TimeEnd;

    // Indicator Settings  
    private StrategyParameterInt _periodSsmaFast;
    private StrategyParameterInt _periodSsmaMiddle;
    private StrategyParameterInt _periodSsmaSlow;

    // Indicator
    private Aindicator _ssma1;
    private Aindicator _ssma2;
    private Aindicator _ssma3;

    // The last value of the indicators
    private decimal _lastSsmaFast;
    private decimal _lastSsmaMiddle;
    private decimal _lastSsmaSlow;

    public IntersectionOfThreeSsma(string name, StartProgram startProgram) : base(name, startProgram)
    {
        // Basic Settings
        TabCreate(BotTabType.Simple);
        _tab = TabsSimple[0];
        Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
        VolumeRegime = CreateParameter("Volume type", "Number of contracts", new[] { "Number of contracts", "Contract currency", }, "Base");
        VolumeOnPosition = CreateParameter("Volume", 10, 1.0m, 50, 4, "Base");
        Slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
        TimeStart = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
        TimeEnd = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

        // Indicator Settings
        _periodSsmaFast = CreateParameter("fast Ssma1 period", 100, 10, 300, 1, "Indicator");
        _periodSsmaMiddle = CreateParameter("middle Ssma2 period", 200, 10, 300, 1, "Indicator");
        _periodSsmaSlow = CreateParameter("slow Ssma3 period", 300, 10, 300, 1, "Indicator");

        // Creating an indicator EmaFast
        _ssma1 = IndicatorsFactory.CreateIndicatorByName(nameClass: "Ssma", name: name + "Ssma1", canDelete: false);
        _ssma1 = (Aindicator)_tab.CreateCandleIndicator(_ssma1, nameArea: "Prime");
        ((IndicatorParameterInt)_ssma1.Parameters[0]).ValueInt = _periodSsmaFast.ValueInt;
        _ssma1.ParametersDigit[0].Value = _periodSsmaFast.ValueInt;
        _ssma1.DataSeries[0].Color = Color.Red;
        _ssma1.Save();

        // Creating an indicator  SsmaMiddle
        _ssma2 = IndicatorsFactory.CreateIndicatorByName(nameClass: "Ssma", name: name + "Ssma2", canDelete: false);
        _ssma2 = (Aindicator)_tab.CreateCandleIndicator(_ssma2, nameArea: "Prime");
        ((IndicatorParameterInt)_ssma2.Parameters[0]).ValueInt = _periodSsmaMiddle.ValueInt;
        _ssma2.ParametersDigit[0].Value = _periodSsmaMiddle.ValueInt;
        _ssma2.DataSeries[0].Color = Color.Blue;
        _ssma2.Save();

        // Creating an indicator SsmaSlow
        _ssma3 = IndicatorsFactory.CreateIndicatorByName(nameClass: "Ssma", name: name + "Ssma3", canDelete: false);
        _ssma3 = (Aindicator)_tab.CreateCandleIndicator(_ssma3, nameArea: "Prime");
        ((IndicatorParameterInt)_ssma3.Parameters[0]).ValueInt = _periodSsmaSlow.ValueInt;
        _ssma3.ParametersDigit[0].Value = _periodSsmaSlow.ValueInt;
        _ssma3.DataSeries[0].Color = Color.Green;
        _ssma3.Save();

        // Subscribe to the indicator update event
        ParametrsChangeByUser += IntersectionOfThreeSsma_ParametrsChangeByUser;

        // Subscribe to the candle completion event
        _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

        Description = "Trend robot at the intersection of three smoothed averages. " +
            "Buy: Fast Ssma is higher than slow Ssma. " +
            "Sale: Fast Ssma is lower than slow Ssma. " +
            "Exit: on the opposite signal.";
    }

    // Indicator Update event
    private void IntersectionOfThreeSsma_ParametrsChangeByUser()
    {
        ((IndicatorParameterInt)_ssma1.Parameters[0]).ValueInt = _periodSsmaFast.ValueInt;
        _ssma1.Save();
        _ssma1.Reload();

        ((IndicatorParameterInt)_ssma2.Parameters[0]).ValueInt = _periodSsmaMiddle.ValueInt;
        _ssma2.Save();
        _ssma2.Reload();

        ((IndicatorParameterInt)_ssma3.Parameters[0]).ValueInt = _periodSsmaSlow.ValueInt;
        _ssma3.Save();
        _ssma3.Reload();

    }

    // Candle Completion Event
    private void _tab_CandleFinishedEvent(List<Candle> candles)
    {
        if (Regime.ValueString == "Off")
        {
            return;
        }

        // If there are not enough candles to build an indicator, we exit
        if (candles.Count < _periodSsmaFast.ValueInt || candles.Count < _periodSsmaMiddle.ValueInt || candles.Count < _periodSsmaSlow.ValueInt)
        {
            return;
        }

        // If the time does not match, we leave
        if (TimeStart.Value > _tab.TimeServerCurrent ||
            TimeEnd.Value < _tab.TimeServerCurrent)
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


        // He last value of the indicators
        _lastSsmaFast = _ssma1.DataSeries[0].Last;
        _lastSsmaMiddle = _ssma2.DataSeries[0].Last;
        _lastSsmaSlow = _ssma3.DataSeries[0].Last;

        if (openPositions == null || openPositions.Count == 0)
        {
            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            // Long
            if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
            {
                if (_lastSsmaFast > _lastSsmaMiddle && _lastSsmaMiddle > _lastSsmaSlow)
                {
                    // We put a stop on the buy                       
                    _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                }
            }

            // Short
            if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
            {
                if (_lastSsmaFast < _lastSsmaMiddle && _lastSsmaMiddle < _lastSsmaSlow)
                {
                    // Putting a stop on sale
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

        // He last value of the indicators
        _lastSsmaFast = _ssma1.DataSeries[0].Last;
        _lastSsmaMiddle = _ssma2.DataSeries[0].Last;
        _lastSsmaSlow = _ssma3.DataSeries[0].Last;

        for (int i = 0; openPositions != null && i < openPositions.Count; i++)
        {
            if (openPositions[i].State != PositionStateType.Open)
            {
                continue;
            }
            if (openPositions[i].Direction == Side.Buy) // If the direction of the position is buy
            {
                if (_lastSsmaFast < _lastSsmaMiddle && _lastSsmaMiddle < _lastSsmaSlow)
                {
                    _tab.CloseAtLimit(openPositions[0], lastPrice - _slippage, openPositions[0].OpenVolume);
                }
            }
            else // If the direction of the position is sale
            {
                if (_lastSsmaFast > _lastSsmaMiddle && _lastSsmaMiddle > _lastSsmaSlow)
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

    // The name of the robot in OsEngin
    public override string GetNameStrategyType()
    {
        return "IntersectionOfThreeSsma";
    }

    public override void ShowIndividualSettingsDialog()
    {

    }
}


