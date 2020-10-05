using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;

/// <summary>
/// Trend strategy based on two indicators BullsPower and BearsPower
/// Трендовая стратегия на основе двух индикаторов BullsPower и BearsPower
/// </summary>
public class BbPowerTrade : BotPanel
{
    public BbPowerTrade(string name, StartProgram startProgram)
        : base(name, startProgram)
    {
        TabCreate(BotTabType.Simple);
        _tab = TabsSimple[0];

        Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
        Volume = CreateParameter("Volume", 1, 1.0m, 50, 4);
        Slippage = CreateParameter("Slippage", 0, 0, 20, 1);
        Step = CreateParameter("Step", 100, 50m, 500, 20);
        BullsPowerPeriod = CreateParameter("Bulls Period", 13, 10, 50, 2);
        BearsPowerPeriod = CreateParameter("Bears Period", 13, 10, 50, 2);


        _bearsP = IndicatorsFactory.CreateIndicatorByName("BearsPower", name + "BearsPower", false);
        _bearsP = (Aindicator)_tab.CreateCandleIndicator(_bearsP, "BearsArea");
        _bearsP.ParametersDigit[0].Value = BearsPowerPeriod.ValueInt;
        _bearsP.Save();

        _bullsP = IndicatorsFactory.CreateIndicatorByName("BullsPower",name + "BullsPower", false);
        _bullsP = (Aindicator)_tab.CreateCandleIndicator(_bullsP, "BullsArea");
        _bullsP.ParametersDigit[0].Value = BullsPowerPeriod.ValueInt;
        _bullsP.Save();

        _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;
        ParametrsChangeByUser += Event_ParametrsChangeByUser;
    }

    void Event_ParametrsChangeByUser()
    {
        if (_bearsP.ParametersDigit[0].Value != BearsPowerPeriod.ValueInt)
        {
            _bearsP.ParametersDigit[0].Value = BearsPowerPeriod.ValueInt;
            _bearsP.Reload();
        }
        if (_bullsP.ParametersDigit[0].Value != BullsPowerPeriod.ValueInt)
        {
            _bullsP.ParametersDigit[0].Value = BullsPowerPeriod.ValueInt;
            _bullsP.Reload();
        }
    }

    /// <summary>
    /// uniq strategy name
    /// взять уникальное имя
    /// </summary>
    public override string GetNameStrategyType()
    {
        return "BbPowerTrade";
    }
    /// <summary>
    /// settings GUI
    /// показать окно настроек
    /// </summary>
    public override void ShowIndividualSettingsDialog()
    {
    }
    /// <summary>
    /// tab to trade
    /// вкладка для торговли
    /// </summary>
    private BotTabSimple _tab;

    // indicators индикаторы

    private Aindicator _bullsP;

    private Aindicator _bearsP;

    //settings настройки публичные

    public StrategyParameterInt Slippage;

    public StrategyParameterDecimal Volume;

    public StrategyParameterString Regime;

    /// <summary>
    /// value to trade formula
    /// шаг от 0-го уровня
    /// </summary>
    public StrategyParameterDecimal Step;

    public StrategyParameterInt BullsPowerPeriod;

    public StrategyParameterInt BearsPowerPeriod;

    private decimal _lastPrice;
    private decimal _lastBearsPrice;
    private decimal _lastBullsPrice;

    // logic логика

    /// <summary>
    /// candle finished event
    /// событие завершения свечи
    /// </summary>
    private void Strateg_CandleFinishedEvent(List<Candle> candles)
    {
        if (Regime.ValueString == "Off")
        {
            return;
        }

        if (_bearsP.DataSeries[0].Values == null 
            || _bullsP.DataSeries[0].Values == null 
            || _bullsP.DataSeries[0].Values.Count < _bullsP.ParametersDigit[0].Value + 2)
        {
            return;
        }

        _lastPrice = candles[candles.Count - 1].Close;

        _lastBearsPrice = _bearsP.DataSeries[0].Values[_bearsP.DataSeries[0].Values.Count - 1];
        _lastBullsPrice = _bullsP.DataSeries[0].Values[_bullsP.DataSeries[0].Values.Count - 1];

        List<Position> openPositions = _tab.PositionsOpenAll;

        if (openPositions != null && openPositions.Count != 0)
        {
            for (int i = 0; i < openPositions.Count; i++)
            {
                LogicClosePosition(candles, openPositions[i]);
            }
        }

        if (Regime.ValueString == "OnlyClosePosition")
        {
            return;
        }
        if (openPositions == null || openPositions.Count == 0)
        {
            LogicOpenPosition(candles, openPositions);
        }
    }

    /// <summary>
    /// logic open position
    /// логика открытия первой позиции
    /// </summary>
    private void LogicOpenPosition(List<Candle> candles, List<Position> position)
    {
        if (_lastBullsPrice + _lastBearsPrice > Step.ValueDecimal
            && Regime.ValueString != "OnlyShort")
        {
            _tab.BuyAtLimit(Volume.ValueDecimal,
                _lastPrice + Slippage.ValueInt * _tab.Securiti.PriceStep);
        }

        if (_lastBullsPrice + _lastBearsPrice < -Step.ValueDecimal
            && Regime.ValueString != "OnlyLong")
        {
            _tab.SellAtLimit(Volume.ValueDecimal,
                _lastPrice - Slippage.ValueInt * _tab.Securiti.PriceStep);
        }

    }

    /// <summary>
    /// logic close position
    /// логика зыкрытия позиции и открытие по реверсивной системе
    /// </summary>
    private void LogicClosePosition(List<Candle> candles, Position position)
    {
        if (position.State == PositionStateType.Closing ||
            position.State == PositionStateType.Opening ||
            position.CloseActiv == true ||
            (position.CloseOrders != null && position.CloseOrders.Count > 0))
        {
            return;
        }

        if (position.Direction == Side.Buy)
        {
            if (_lastBullsPrice + _lastBearsPrice < -Step.ValueDecimal)
            {
                _tab.CloseAtLimit(position, _lastPrice - Slippage.ValueInt * _tab.Securiti.PriceStep, position.OpenVolume);

                if (Regime.ValueString != "OnlyLong" && Regime.ValueString != "OnlyClosePosition")
                {
                    _tab.SellAtLimit(Volume.ValueDecimal, _lastPrice - Slippage.ValueInt * _tab.Securiti.PriceStep);
                }
            }
        }

        if (position.Direction == Side.Sell)
        {
            if (_lastBullsPrice + _lastBearsPrice > Step.ValueDecimal)
            {
                _tab.CloseAtLimit(position, _lastPrice + Slippage.ValueInt * _tab.Securiti.PriceStep, position.OpenVolume);

                if (Regime.ValueString != "OnlyShort" && Regime.ValueString != "OnlyClosePosition")
                {
                    _tab.BuyAtLimit(Volume.ValueDecimal, _lastPrice + Slippage.ValueInt * _tab.Securiti.PriceStep);
                }

            }
        }
    }
}