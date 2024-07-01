using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;

/// <summary>
/// Trend strategy at the intersection of the indicator RVI
/// ��������� ��������� �� ����������� ���������� RVI
/// </summary>
[Bot("RviTrade")]
public class RviTrade : BotPanel
{
    public RviTrade(string name, StartProgram startProgram)
        : base(name, startProgram)
    {
        TabCreate(BotTabType.Simple);
        _tab = TabsSimple[0];

        Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
        Volume = CreateParameter("Volume", 3, 1.0m, 50, 4);
        Slippage = CreateParameter("Slipage", 0, 0, 20, 1);

        RviLenght = CreateParameter("RviLength", 10, 10, 80, 3);

        _rvi = IndicatorsFactory.CreateIndicatorByName("RVI",name + "RviArea", false);
        _rvi = (Aindicator)_tab.CreateCandleIndicator(_rvi, "MacdArea");
        _rvi.ParametersDigit[0].Value = RviLenght.ValueInt;
        _rvi.Save();

        _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;
        ParametrsChangeByUser += RviTrade_ParametrsChangeByUser;

        Description = "Trend strategy at the intersection of the indicator RVI" +
            "Buy: lastRviDown < 0 and lastRviUp > lastRviDown. " +
            "Sell: lastRviDown > 0 and lastRviUp < lastRviDown. " +
            "Exit: By return signal";
    }

    void RviTrade_ParametrsChangeByUser()
    {
        if (RviLenght.ValueInt != _rvi.ParametersDigit[0].Value)
        {
            _rvi.ParametersDigit[0].Value = RviLenght.ValueInt;
            _rvi.Reload();
        }
    }

    /// <summary>
    /// strategy name
    /// ����� ���������� ���
    /// </summary>
    public override string GetNameStrategyType()
    {
        return "RviTrade";
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

    //indicators ����������

    private Aindicator _rvi;

    //settings ��������� ���������

    /// <summary>
    /// slippage
    /// ���������������
    /// </summary>
    public StrategyParameterInt Slippage;

    /// <summary>
    /// volume to inter
    /// ������������� ����� ��� �����
    /// </summary>
    public StrategyParameterDecimal Volume;

    /// <summary>
    /// regime
    /// ����� ������
    /// </summary>
    public StrategyParameterString Regime;

    /// <summary>
    /// indicator length
    /// ������ ����������
    /// </summary>
    public StrategyParameterInt RviLenght;

    private decimal _lastPrice;
    private decimal _lastRviUp;
    private decimal _lastRviDown;

    // logic / ������

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

        if (_rvi.DataSeries[0].Values == null)
        {
            return;
        }

        _lastPrice = candles[candles.Count - 1].Close;
        _lastRviUp = _rvi.DataSeries[0].Values[_rvi.DataSeries[0].Values.Count - 1];
        _lastRviDown = _rvi.DataSeries[1].Values[_rvi.DataSeries[1].Values.Count - 1];

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
    /// open position logic
    /// ������ �������� �������
    /// </summary>
    private void LogicOpenPosition(List<Candle> candles, List<Position> position)
    {
        if (_lastRviDown < 0 && _lastRviUp > _lastRviDown && Regime.ValueString != "OnlyShort")
        {
            _tab.BuyAtLimit(Volume.ValueDecimal, _lastPrice + Slippage.ValueInt * _tab.Securiti.PriceStep);
        }

        if (_lastRviDown > 0 && _lastRviUp < _lastRviDown && Regime.ValueString != "OnlyLong")
        {
            _tab.SellAtLimit(Volume.ValueDecimal, _lastPrice - Slippage.ValueInt * _tab.Securiti.PriceStep);
        }
    }

    /// <summary>
    /// logic close position
    /// ������ �������� �������
    /// </summary>
    private void LogicClosePosition(List<Candle> candles, Position position)
    {
        if (position.Direction == Side.Buy && position.State == PositionStateType.Open)
        {
            if (_lastRviDown > 0 && _lastRviUp < _lastRviDown)
            {
                _tab.CloseAtLimit(position, _lastPrice - Slippage.ValueInt, position.OpenVolume);

                if (Regime.ValueString != "OnlyLong" && Regime.ValueString != "OnlyClosePosition")
                {
                    _tab.SellAtLimit(Volume.ValueDecimal, _lastPrice - Slippage.ValueInt * _tab.Securiti.PriceStep);
                }
            }
        }

        if (position.Direction == Side.Sell && position.State == PositionStateType.Open)
        {
            if (_lastRviDown < 0 && _lastRviUp > _lastRviDown)
            {
                _tab.CloseAtLimit(position, _lastPrice + Slippage.ValueInt, position.OpenVolume);

                if (Regime.ValueString != "OnlyShort" && Regime.ValueString != "OnlyClosePosition")
                {
                    _tab.BuyAtLimit(Volume.ValueDecimal, _lastPrice + Slippage.ValueInt * _tab.Securiti.PriceStep);
                }
            }
        }
    }
}