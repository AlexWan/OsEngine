using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;

/// <summary>
/// Bollinger Bands trading bargaining robot with pull-up Trailing-Stop through Bollinger Bands
/// Робот торгующий прорыв Bollinger Bands с подтягивающимся Trailing-Stop по линии Bollinger Bands
/// </summary>
public class BollingerTrailing : BotPanel
{

    public BollingerTrailing(string name, StartProgram startProgram)
        : base(name, startProgram)
    {
        TabCreate(BotTabType.Simple);
        _tab = TabsSimple[0];

        Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
        Volume = CreateParameter("Volume", 3, 1.0m, 50, 4);
        Slippage = CreateParameter("Slipage", 0, 0, 20, 1);
        IndLenght = CreateParameter("IndLength", 10, 10, 80, 3);
        BollingerDeviation = CreateParameter("Bollinger Deviation", 2, 0.5m, 4, 0.1m);

        _bollinger = IndicatorsFactory.CreateIndicatorByName("Bollinger",name + "Bollinger", false);

        _bollinger.ParametersDigit[0].Value = IndLenght.ValueInt;
        _bollinger.ParametersDigit[1].Value = BollingerDeviation.ValueDecimal;

        _bollinger = (Aindicator)_tab.CreateCandleIndicator(_bollinger, "Prime");
        _bollinger.Save();

        _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;
        _tab.PositionOpeningSuccesEvent += ReloadTrailingPosition;
        ParametrsChangeByUser += Event_ParametrsChangeByUser;
    }

    void Event_ParametrsChangeByUser()
    {
        if (IndLenght.ValueInt != _bollinger.ParametersDigit[0].Value ||
            BollingerDeviation.ValueDecimal != _bollinger.ParametersDigit[1].Value)
        {
            _bollinger.ParametersDigit[0].Value = IndLenght.ValueInt;
            _bollinger.ParametersDigit[1].Value = BollingerDeviation.ValueDecimal;
            _bollinger.Reload();
        }
    }

    /// <summary>
    /// uniq name
    /// взять уникальное имя
    /// </summary>
    public override string GetNameStrategyType()
    {
        return "BollingerTrailing";
    }

    /// <summary>
    /// settings GUI
    /// показать окно настроек
    /// </summary>
    public override void ShowIndividualSettingsDialog()
    {

    }

    /// <summary>
    /// trade tab
    /// вкладка для торговли
    /// </summary>
    private BotTabSimple _tab;

    private Aindicator _bollinger;

    //settings настройки публичные

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

    /// <summary>
    /// indicator length
    /// длинна индикатора
    /// </summary>
    public StrategyParameterInt IndLenght;

    public StrategyParameterDecimal BollingerDeviation;

    private decimal _lastPrice;
    private decimal _lastBbUp;
    private decimal _lastBbDown;

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

        if (_bollinger.DataSeries[0].Values == null || _bollinger.DataSeries[1].Values == null)
        {
            return;
        }

        _lastPrice = candles[candles.Count - 1].Close;
        _lastBbUp = _bollinger.DataSeries[0].Values[_bollinger.DataSeries[0].Values.Count-2];
        _lastBbDown = _bollinger.DataSeries[1].Values[_bollinger.DataSeries[1].Values.Count - 2];

        if (_bollinger.DataSeries[0].Values.Count < ((IndicatorParameterInt)_bollinger.Parameters[0]).ValueInt + 2)
        {
            return;
        }

        List<Position> openPositions = _tab.PositionsOpenAll;

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
    /// logic close pos
    /// логика закрытия позиции
    /// </summary>
    private void LogicClosePosition(List<Candle> candles, List<Position> position)
    {
        List<Position> openPositions = _tab.PositionsOpenAll;

        for (int i = 0; openPositions != null && i < openPositions.Count; i++)
        {
            ReloadTrailingPosition(openPositions[i]);
        }
    }

    /// <summary>
    /// close one pos
    /// логика закрытия позиции
    /// </summary>
    private void ReloadTrailingPosition(Position position)
    {
        List<Position> openPositions = _tab.PositionsOpenAll;

        for (int i = 0; openPositions != null && i < openPositions.Count; i++)
        {
            if (openPositions[i].Direction == Side.Buy)
            {
                decimal valueDown = _bollinger.DataSeries[1].Values[_bollinger.DataSeries[1].Values.Count - 1];

                _tab.CloseAtTrailingStop(
                    openPositions[i], valueDown,
                    valueDown - Slippage.ValueInt * _tab.Securiti.PriceStep);
            }
            else
            {
                decimal valueUp = _bollinger.DataSeries[0].Values[_bollinger.DataSeries[0].Values.Count - 1];
                _tab.CloseAtTrailingStop(
                    openPositions[i], valueUp,
                    valueUp + Slippage.ValueInt * _tab.Securiti.PriceStep);
            }
        }
    }


    /// <summary>
    /// open position logic
    /// логика открытия позиции
    /// </summary>
    private void LogicOpenPosition(List<Candle> candles, List<Position> position)
    {
        List<Position> openPositions = _tab.PositionsOpenAll;
        if (openPositions == null || openPositions.Count == 0)
        {
            // long
            if (Regime.ValueString != "OnlyShort")
            {
                if (_lastPrice > _lastBbUp)
                {
                    _tab.BuyAtLimit(Volume.ValueDecimal, _lastPrice + Slippage.ValueInt * _tab.Securiti.PriceStep);
                }
            }

            // Short
            if (Regime.ValueString != "OnlyLong")
            {
                if (_lastPrice < _lastBbDown)
                {
                    _tab.SellAtLimit(Volume.ValueDecimal, _lastPrice - Slippage.ValueInt * _tab.Securiti.PriceStep);
                }
            }
            return;
        }
    }

}