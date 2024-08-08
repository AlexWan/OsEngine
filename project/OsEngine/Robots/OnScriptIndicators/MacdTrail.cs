using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;

/// <summary>
/// Trend strategy based on the Macd indicator and trail stop
/// ��������� ��������� �� ������ ���������� Macd � ����������
/// </summary>
[Bot("MacdTrail")]
public class MacdTrail : BotPanel
{
    public MacdTrail(string name, StartProgram startProgram)
        : base(name, startProgram)
    {
        TabCreate(BotTabType.Simple);
        _tab = TabsSimple[0];

        Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
        Volume = CreateParameter("Volume", 1, 1.0m, 50, 4);
        Slippage = CreateParameter("Slippage", 0, 0, 20, 1);
        TrailStop = CreateParameter("Trail Stop Percent", 0.7m, 0.3m, 3, 0.1m);

        _macd = IndicatorsFactory.CreateIndicatorByName("MacdLine",name + "MACD", false);
        _macd = (Aindicator)_tab.CreateCandleIndicator(_macd, "MacdArea");
        _macd.Save();

        _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

        Description = "Trend strategy based on the Macd indicator and trail stop " +
            "Logic Enter: " +
            "lastMacdDown < 0 and lastMacdUp > lastMacdDown - Buy " +
            "lastMacdDown > 0 and lastMacdUp < lastMacdDown - Sell " +
            "Exit: By TralingStop";
    }

    /// <summary>
    /// uniq strategy name
    /// ����� ���������� ���
    /// </summary>
    public override string GetNameStrategyType()
    {
        return "MacdTrail";
    }
    /// <summary>
    /// settings GUI
    /// �������� ���� ��������
    /// </summary>
    public override void ShowIndividualSettingsDialog()
    {

    }
    /// <summary>
    /// tab to trade
    /// ������� ��� ��������
    /// </summary>
    private BotTabSimple _tab;

    /// <summary>
    /// Macd 
    /// </summary>
    private Aindicator _macd;

    //settings ��������� ���������

    public StrategyParameterDecimal TrailStop;

    public StrategyParameterInt Slippage;

    public StrategyParameterDecimal Volume;

    public StrategyParameterString Regime;

    private decimal _lastClose;
    private decimal _lastMacdDown;
    private decimal _lastMacdUp;

    // logic ������

    /// <summary>
    /// candle finished event
    /// ������� ���������� �����
    /// </summary>
    private void Strateg_CandleFinishedEvent(List<Candle> candles)
    {
        if (Regime.ValueString == "Off")
        {
            return;
        }

        if (_macd.DataSeries[0].Values == null)
        {
            return;
        }

        _lastClose = candles[candles.Count - 1].Close;
        _lastMacdUp = _macd.DataSeries[0].Values[_macd.DataSeries[0].Values.Count - 1];
        _lastMacdDown = _macd.DataSeries[1].Values[_macd.DataSeries[1].Values.Count - 1];

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
    /// ������ �������� ������ �������
    /// </summary>
    private void LogicOpenPosition(List<Candle> candles, List<Position> position)
    {
        if (_lastMacdDown < 0 && _lastMacdUp > _lastMacdDown
                              && Regime.ValueString != "OnlyShort")
        {
            _tab.BuyAtLimit(Volume.ValueDecimal, _lastClose + Slippage.ValueInt * _tab.Securiti.PriceStep);
        }
        if (_lastMacdDown > 0 && _lastMacdUp < _lastMacdDown
                              && Regime.ValueString != "OnlyLong")
        {
            _tab.SellAtLimit(Volume.ValueDecimal, _lastClose - Slippage.ValueInt * _tab.Securiti.PriceStep);
        }
    }

    /// <summary>
    /// logic close position
    /// ������ �������� �������
    /// </summary>
    private void LogicClosePosition(List<Candle> candles, Position position)
    {
        if (position.Direction == Side.Buy)
        {
            _tab.CloseAtTrailingStop(position,
                _lastClose - _lastClose * TrailStop.ValueDecimal / 100,
                _lastClose - _lastClose * TrailStop.ValueDecimal / 100);
        }

        if (position.Direction == Side.Sell)
        {
            _tab.CloseAtTrailingStop(position,
                _lastClose + _lastClose * TrailStop.ValueDecimal / 100,
                _lastClose + _lastClose * TrailStop.ValueDecimal / 100);
        }
    }
}