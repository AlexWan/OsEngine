using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;

/// <summary>
/// Pair trading based on the RSI indicator
/// Парная торговля на основе индикатора RSI
/// </summary>
public class PairRsiTrade : BotPanel
{
    public PairRsiTrade(string name, StartProgram startProgram)
        : base(name, startProgram)
    {
        TabCreate(BotTabType.Simple);
        _tab1 = TabsSimple[0];
        _tab1.CandleFinishedEvent += _tab1_CandleFinishedEvent;

        TabCreate(BotTabType.Simple);
        _tab2 = TabsSimple[1];
        _tab2.CandleFinishedEvent += _tab2_CandleFinishedEvent;

        Regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });
        Volume1 = CreateParameter("Volume 1", 1, 1.0m, 50, 1);
        Volume2 = CreateParameter("Volume 2", 1, 1.0m, 50, 1);

        RsiSpread = CreateParameter("Spread to Rsi", 10, 5.0m, 50, 2);
        RsiOnePeriod = CreateParameter("Rsi One period", 14, 5, 50, 2);
        RsiTwoPeriod = CreateParameter("Rsi Two period", 14, 5, 50, 2);

        _rsi1 = IndicatorsFactory.CreateIndicatorByName("RSI",name + "RSI1", false);
        _rsi1.ParametersDigit[0].Value = RsiOnePeriod.ValueInt;
        _rsi1 = (Aindicator)_tab1.CreateCandleIndicator(_rsi1, "Rsi1_Area");
        _rsi1.Save();

        _rsi2 = IndicatorsFactory.CreateIndicatorByName("RSI",name + "RSI2", false);
        _rsi2.ParametersDigit[0].Value = RsiTwoPeriod.ValueInt;
        _rsi2 = (Aindicator)_tab2.CreateCandleIndicator(_rsi2, "Rsi2_Area");
        _rsi2.Save();

        ParametrsChangeByUser += Event_ParametrsChangeByUser;
    }

    void Event_ParametrsChangeByUser()
    {
        if (_rsi1.ParametersDigit[0].Value != RsiOnePeriod.ValueInt)
        {
            _rsi1.ParametersDigit[0].Value = RsiOnePeriod.ValueInt;
            _rsi1.Reload();
        }
        if (_rsi2.ParametersDigit[0].Value != RsiTwoPeriod.ValueInt)
        {
            _rsi2.ParametersDigit[0].Value = RsiTwoPeriod.ValueInt;
            _rsi2.Reload();
        }
    }

    /// <summary>
    /// uniq strategy name
    /// взять уникальное имя стратегии
    /// </summary>
    public override string GetNameStrategyType()
    {
        return "PairRsiTrade";
    }

    /// <summary>
    /// settings GUI
    /// показать окно настроек
    /// </summary>
    public override void ShowIndividualSettingsDialog()
    {

    }

    // security публичные настройки

    public StrategyParameterDecimal RsiSpread;

    public StrategyParameterDecimal Volume1;

    public StrategyParameterDecimal Volume2;

    public StrategyParameterString Regime;

    public StrategyParameterInt RsiOnePeriod;

    public StrategyParameterInt RsiTwoPeriod;


    /// <summary>
    /// tab to trade tab1
    /// вкладка с первым инструметом
    /// </summary>
    private BotTabSimple _tab1;

    /// <summary>
    /// tab to trade tab2
    /// вкладка со вторым инструментом
    /// </summary>
    private BotTabSimple _tab2;

    /// <summary>
    /// ready candles tab1
    /// готовые свечи первого инструмента
    /// </summary>
    private List<Candle> _candles1;

    /// <summary>
    /// ready candles tab2
    /// готовые свечи второго инструмента
    /// </summary>
    private List<Candle> _candles2;

    private Aindicator _rsi1;

    private Aindicator _rsi2;

    void _tab1_CandleFinishedEvent(List<Candle> candles)
    {
        _candles1 = candles;

        if (_candles2 == null ||
            _candles2[_candles2.Count - 1].TimeStart != _candles1[_candles1.Count - 1].TimeStart)
        {
            return;
        }

        CheckExit();
        Trade();
    }

    void _tab2_CandleFinishedEvent(List<Candle> candles)
    {
        _candles2 = candles;

        if (_candles1 == null ||
            _candles2[_candles2.Count - 1].TimeStart != _candles1[_candles1.Count - 1].TimeStart)
        {
            return;
        }

        CheckExit();
        Trade();
    }

    /// <summary>
    /// trade logic
    /// </summary>
    private void Trade()
    {
        if (_candles1.Count < 20 && _candles2.Count < 20)
        {
            return; ;
        }

        if (Regime.ValueString == "Off")
        {
            return;
        }

        List<Position> pos1 = _tab1.PositionsOpenAll;
        List<Position> pos2 = _tab2.PositionsOpenAll;

        if (pos1 != null && pos1.Count != 0 || pos2 != null && pos2.Count != 0)
        {
            return;
        }

        if (_rsi1.DataSeries[0].Values == null || _rsi2.DataSeries[0].Values == null)
        {
            return;
        }

        if (_rsi1.DataSeries[0].Values.Count < _rsi1.ParametersDigit[0].Value + 3 
            || _rsi2.DataSeries[0].Values.Count < _rsi2.ParametersDigit[0].Value + 3)
        {
            return;
        }

        decimal lastRsi1 = _rsi1.DataSeries[0].Values[_rsi1.DataSeries[0].Values.Count - 1];
        decimal lastRsi2 = _rsi2.DataSeries[0].Values[_rsi2.DataSeries[0].Values.Count - 1];

        if (lastRsi1 > lastRsi2 + RsiSpread.ValueDecimal)
        {
            _tab1.SellAtMarket(Volume1.ValueDecimal);
            _tab2.BuyAtMarket(Volume2.ValueDecimal);
        }

        if (lastRsi2 > lastRsi1 + RsiSpread.ValueDecimal)
        {
            _tab1.BuyAtMarket(Volume1.ValueDecimal);
            _tab2.SellAtMarket(Volume2.ValueDecimal);
        }
    }

    private void CheckExit()
    {
        List<Position> positions1 = _tab1.PositionsOpenAll;
        List<Position> positions2 = _tab2.PositionsOpenAll;

        decimal lastRsi1 = _rsi1.DataSeries[0].Values[_rsi1.DataSeries[0].Values.Count - 1];
        decimal lastRsi2 = _rsi2.DataSeries[0].Values[_rsi2.DataSeries[0].Values.Count - 1];

        if (positions1.Count == 0)
        {
            return;
        }

        if (positions1[0].Direction == Side.Buy &&
            lastRsi1 <= lastRsi2)
        {
            CloseAllPositions();
        }
        if (positions1[0].Direction == Side.Sell &&
            lastRsi2 <= lastRsi1)
        {
            CloseAllPositions();
        }
    }

    private void CloseAllPositions()
    {
        List<Position> positions1 = _tab1.PositionsOpenAll;
        List<Position> positions2 = _tab2.PositionsOpenAll;

        if (positions1.Count != 0 && positions1[0].State == PositionStateType.Open)
        {
            _tab1.CloseAtMarket(positions1[0], positions1[0].OpenVolume);
        }

        if (positions2.Count != 0 && positions2[0].State == PositionStateType.Open)
        {
            _tab2.CloseAtMarket(positions2[0], positions2[0].OpenVolume);
        }
    }
}