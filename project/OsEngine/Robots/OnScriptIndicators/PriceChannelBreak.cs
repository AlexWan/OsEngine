using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;


/// <summary>
///When the candle is closed outside the PriceChannel channel,
/// we enter the position, the stop loss is at the extremum of the last candle from the entry candle,
/// take profit by the channel size from the close of the candle at which the entry occurred
/// 
/// При закрытии свечи вне канала PriceChannel входим в позицию , стоп-лосс за экстремум прошлойсвечи от свечи входа,
/// тейкпрофит на величину канала от закрытия свечи на которой произошел вход
/// </summary>
public class PriceChannelBreak : BotPanel
{
    public PriceChannelBreak(string name, StartProgram startProgram)
        : base(name, startProgram)
    {
        TabCreate(BotTabType.Simple);
        _tab = TabsSimple[0];

        Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
        Volume = CreateParameter("Volume", 1, 1.0m, 50, 4);
        Slippage = CreateParameter("Slipage", 0, 0, 20, 1);
        IndLenght = CreateParameter("IndLength", 10, 10, 80, 3);

        _pc = IndicatorsFactory.CreateIndicatorByName("PriceChannel", name + "PriceChannel", false);
        _pc = (Aindicator)_tab.CreateCandleIndicator(_pc, "Prime");

        _pc.ParametersDigit[0].Value = IndLenght.ValueInt;
        _pc.ParametersDigit[1].Value = IndLenght.ValueInt;

        _pc.Save();

        _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;
        _tab.PositionOpeningSuccesEvent += Strateg_PositionOpen;

        ParametrsChangeByUser += Event_ParametrsChangeByUser;
    }

    void Event_ParametrsChangeByUser()
    {
        if (IndLenght.ValueInt != _pc.ParametersDigit[0].Value ||
            IndLenght.ValueInt != _pc.ParametersDigit[1].Value)
        {
            _pc.ParametersDigit[0].Value = IndLenght.ValueInt;
            _pc.ParametersDigit[1].Value = IndLenght.ValueInt;

            _pc.Reload();
        }
    }

    /// <summary>
    /// uniq name
    /// взять уникальное имя
    /// </summary>
    public override string GetNameStrategyType()
    {
        return "PriceChannelBreak";
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
    /// 
    private BotTabSimple _tab;

    /// <summary>
    /// PriceChannel
    /// </summary>
    private Aindicator _pc;

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

    private decimal _lastPrice;
    private decimal _lastPcUp;
    private decimal _lastPcDown;

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

        if (_pc.DataSeries[0].Values == null || _pc.DataSeries[1].Values == null)
        {
            return;
        }

        if (_pc.DataSeries[0].Values.Count < _pc.ParametersDigit[0].Value + 2 || _pc.DataSeries[1].Values.Count < _pc.ParametersDigit[1].Value + 2)
        {
            return;
        }

        _lastPrice = candles[candles.Count - 1].Close;
        _lastPcUp = _pc.DataSeries[0].Values[_pc.DataSeries[0].Values.Count - 2];
        _lastPcDown = _pc.DataSeries[1].Values[_pc.DataSeries[1].Values.Count - 2];




        List<Position> openPositions = _tab.PositionsOpenAll;

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
        List<Position> openPositions = _tab.PositionsOpenAll;
        if (openPositions == null || openPositions.Count == 0)
        {
            // long
            if (Regime.ValueString != "OnlyShort")
            {
                if (_lastPrice > _lastPcUp)
                {
                    _tab.BuyAtLimit(Volume.ValueDecimal, _lastPrice + Slippage.ValueInt * _tab.Securiti.PriceStep);
                }
            }

            // Short
            if (Regime.ValueString != "OnlyLong")
            {
                if (_lastPrice < _lastPcDown)
                {
                    _tab.SellAtLimit(Volume.ValueDecimal, _lastPrice - Slippage.ValueInt * _tab.Securiti.PriceStep);
                }
            }
        }
    }

    /// <summary>
    /// set stop orders and profit orders
    /// выставление стоп-лосс и таке-профит
    /// </summary>
    private void Strateg_PositionOpen(Position position)
    {
        List<Position> openPositions = _tab.PositionsOpenAll;
        for (int i = 0; openPositions != null && i < openPositions.Count; i++)
        {
            if (openPositions[i].Direction == Side.Buy)
            {
                decimal lowCandle = _tab.CandlesAll[_tab.CandlesAll.Count - 2].Low;
                _tab.CloseAtStop(openPositions[i], lowCandle, lowCandle - Slippage.ValueInt * _tab.Securiti.PriceStep);

                _tab.CloseAtProfit(
                    openPositions[i], _lastPrice + (_lastPcUp - _lastPcDown),
                    (_lastPrice + (_lastPcUp - _lastPcDown)) - Slippage.ValueInt * _tab.Securiti.PriceStep);
            }
            else
            {
                decimal highCandle = _tab.CandlesAll[_tab.CandlesAll.Count - 2].High;
                _tab.CloseAtStop(openPositions[i], highCandle, highCandle + Slippage.ValueInt * _tab.Securiti.PriceStep);

                _tab.CloseAtProfit(
                    openPositions[i], _lastPrice - (_lastPcUp - _lastPcDown),
                    (_lastPrice - (_lastPcUp - _lastPcDown)) + Slippage.ValueInt * _tab.Securiti.PriceStep);
            }
        }
    }
}