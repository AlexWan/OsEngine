using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;

/// <summary>
/// Trading robot on the index. The intersection of MA on the index from the bottom up long, with the reverse intersection of shorts
/// Торговый робот на индексе. Пересечение MA на индексе снизу вверх лонг, при обратном пересечении шорт 
/// </summary>
public class OneLegArbitrage : BotPanel
{
    public OneLegArbitrage(string name, StartProgram startProgram)
        : base(name, startProgram)
    {
        TabCreate(BotTabType.Index);
        _tab1 = TabsIndex[0];

        Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
        Volume = CreateParameter("Volume", 3, 1.0m, 50, 4);
        Slippage = CreateParameter("Slipage", 0, 0, 20, 1);

        _ma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "MovingAverage", false);
        _ma = (Aindicator)_tab1.CreateCandleIndicator(_ma, "Prime");
        _ma.Save();

        TabCreate(BotTabType.Simple);
        _tab2 = TabsSimple[0];

        _tab2.CandleFinishedEvent += Strateg_CandleFinishedEvent;
    }

    /// <summary>
    /// bot name
    /// взять уникальное имя
    /// </summary>
    public override string GetNameStrategyType()
    {
        return "OneLegArbitrage";
    }
    /// <summary>
    /// settings UI
    /// показать окно настроек
    /// </summary>
    public override void ShowIndividualSettingsDialog()
    {

    }

    /// <summary>
    /// index tab
    /// вкладка анализируемого индекса
    /// </summary>
    private BotTabIndex _tab1;

    /// <summary>
    /// trade tab
    /// вкладка для торговли
    /// </summary>
    private BotTabSimple _tab2;

    private Aindicator _ma;

    //settings / настройки публичные

    /// <summary>
    /// slippage
    /// проскальзывание
    /// </summary>
    public StrategyParameterInt Slippage;

    /// <summary>
    /// volume to inter
    /// фиксированный объем для входа
    /// </summary>
    public StrategyParameterDecimal Volume;

    /// <summary>
    /// regime
    /// режим работы
    /// </summary>
    public StrategyParameterString Regime;

    private decimal _lastPrice;
    private decimal _lastIndex;
    private decimal _lastMa;

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

        if (_ma.DataSeries[0].Values == null ||
            _ma.DataSeries[0].Values.Count < _ma.ParametersDigit[0].Value + 2)
        {
            return;
        }

        if (_tab1.Candles == null ||
            _tab2.CandlesFinishedOnly == null)
        {
            return;
        }

        _lastIndex = _tab1.Candles[_tab1.Candles.Count - 1].Close;
        _lastMa = _ma.DataSeries[0].Values[_ma.DataSeries[0].Values.Count - 1];
        _lastPrice = candles[candles.Count - 1].Close;

        List<Position> openPositions = _tab2.PositionsOpenAll;
        if (openPositions != null && openPositions.Count != 0)
        {
            LogicClosePosition(candles, openPositions);
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
    /// open position logic
    /// логика открытия позиции
    /// </summary>
    private void LogicOpenPosition(List<Candle> candles, List<Position> position)
    {
        List<Position> openPositions = _tab2.PositionsOpenAll;
        if (openPositions == null || openPositions.Count == 0)
        {
            if (Regime.ValueString != "OnlyShort")
            {
                if (_lastIndex > _lastMa)
                {
                    _tab2.BuyAtLimit(Volume.ValueDecimal, _lastPrice + _tab2.Securiti.PriceStep * Slippage.ValueInt);
                }
            }

            if (Regime.ValueString != "OnlyLong")
            {
                if (_lastIndex < _lastMa)
                {
                    _tab2.SellAtLimit(Volume.ValueDecimal, _lastPrice - _tab2.Securiti.PriceStep * Slippage.ValueInt);
                }
            }
            return;
        }
    }

    /// <summary>
    /// close position logic
    /// логика закрытия позиции
    /// </summary>
    private void LogicClosePosition(List<Candle> candles, List<Position> position)
    {
        List<Position> openPositions = _tab2.PositionsOpenAll;
        for (int i = 0; openPositions != null && i < openPositions.Count; i++)
        {
            if (openPositions[i].Direction == Side.Buy)
            {
                if (_lastIndex < _lastMa)
                {
                    if (Regime.ValueString == "OnlyClosePosition")
                    {
                        _tab2.CloseAtMarket(openPositions[i], openPositions[i].OpenVolume);
                    }
                    else
                    {
                        _tab2.CloseAtMarket(openPositions[i], openPositions[i].OpenVolume);
                        if (openPositions.Count < 2)
                        {
                            _tab2.SellAtLimit(Volume.ValueDecimal, _lastPrice - _tab2.Securiti.PriceStep * Slippage.ValueInt);
                        }
                    }
                }
            }
            else
            {
                if (_lastIndex > _lastMa)
                {
                    if (Regime.ValueString == "OnlyClosePosition")
                    {
                        _tab2.CloseAtMarket(openPositions[i], openPositions[i].OpenVolume);
                    }
                    else
                    {
                        _tab2.CloseAtMarket(openPositions[i], openPositions[i].OpenVolume);
                        if(openPositions.Count < 2)
                        {
                            _tab2.BuyAtLimit(Volume.ValueDecimal, _lastPrice + _tab2.Securiti.PriceStep * Slippage.ValueInt);
                        }
                        
                    }
                }
            }

        }
    }
}