using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;

/// <summary>
/// Trend strategy at the intersection of the MACD indicator
/// ��������� ��������� �� ����������� ���������� MACD
/// </summary>
[Bot("MacdRevers")]
public class MacdRevers : BotPanel
{
    public MacdRevers(string name, StartProgram startProgram)
        : base(name, startProgram)
    {
        TabCreate(BotTabType.Simple);
        _tab = TabsSimple[0];

        _macd = IndicatorsFactory.CreateIndicatorByName("MACD",name + "MacdArea", false);
        _macd = (Aindicator)_tab.CreateCandleIndicator(_macd, "MacdArea");
        _macd.Save();

        Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
        Volume = CreateParameter("Volume", 1, 1.0m, 50, 4);
        Slippage = CreateParameter("Slippage", 0, 0, 20, 1);

        _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

        Description = "Trend strategy at the intersection of the MACD indicator " +
            "Logic of the first Enter: " +
            "lastMacdDown < 0 and lastMacdUp > lastMacdDown - Buy " +
            "lastMacdDown > 0 and lastMacdUp < lastMacdDown - Sell " +
            "Next: lastMacdDown > 0 and lastMacdUp < lastMacdDown close position and open Short. " +
            "lastMacdDown < 0 and lastMacdUp > lastMacdDown close position and open Long.";
    }

    /// <summary>
    /// uniq strategy name
    /// ����� ���������� ���
    /// </summary>
    public override string GetNameStrategyType()
    {
        return "MacdRevers";
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
    /// MACD 
    /// </summary>
    private Aindicator _macd;

    //settings ��������� ���������

    public StrategyParameterInt Slippage;

    public StrategyParameterDecimal Volume;

    public StrategyParameterString Regime;

    private decimal _lastPrice;
    private decimal _lastMacdUp;
    private decimal _lastMacdDown;

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

        _lastPrice = candles[candles.Count - 1].Close;
        _lastMacdUp = _macd.DataSeries[0].Last;
        _lastMacdDown = _macd.DataSeries[1].Last;

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
        if (_lastMacdDown < 0 &&
            _lastMacdUp > _lastMacdDown
            && Regime.ValueString != "OnlyShort")
        {
            _tab.BuyAtLimit(Volume.ValueDecimal, _lastPrice + Slippage.ValueInt * _tab.Securiti.PriceStep);
        }

        if (_lastMacdDown > 0 &&
            _lastMacdUp < _lastMacdDown
            && Regime.ValueString != "OnlyLong")
        {
            _tab.SellAtLimit(Volume.ValueDecimal, _lastPrice - Slippage.ValueInt * _tab.Securiti.PriceStep);
        }
    }

    /// <summary>
    /// logic close position
    /// ������ �������� ������� � �������� �� ����������� �������
    /// </summary>
    private void LogicClosePosition(List<Candle> candles, Position position)
    {
        if (position.Direction == Side.Buy)
        {
            if (_lastMacdDown > 0 && _lastMacdUp < _lastMacdDown)
            {
                _tab.CloseAtLimit(position, _lastPrice - Slippage.ValueInt * _tab.Securiti.PriceStep, position.OpenVolume);

                if (Regime.ValueString != "OnlyLong"
                    && Regime.ValueString != "OnlyClosePosition")
                {
                    _tab.SellAtLimit(Volume.ValueDecimal, _lastPrice - Slippage.ValueInt * _tab.Securiti.PriceStep);
                }
            }
        }

        if (position.Direction == Side.Sell)
        {
            if (_lastMacdDown < 0 && _lastMacdUp > _lastMacdDown)
            {
                _tab.CloseAtLimit(position, _lastPrice + Slippage.ValueInt * _tab.Securiti.PriceStep, position.OpenVolume);

                if (Regime.ValueString != "OnlyShort"
                    && Regime.ValueString != "OnlyClosePosition")
                {
                    _tab.BuyAtLimit(Volume.ValueDecimal, _lastPrice + Slippage.ValueInt * _tab.Securiti.PriceStep);
                }
            }
        }
    }

}