using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;

/// <summary>
/// Trend Strategy Based on Breaking Bollinger Lines
/// ��������� ��������� �� ������ �������� ����� ����������
/// </summary>
[Bot("BollingerRevers")]
public class BollingerRevers : BotPanel
{
    public BollingerRevers(string name, StartProgram startProgram)
        : base(name, startProgram)
    {
        TabCreate(BotTabType.Simple);
        _tab = TabsSimple[0];

        Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
        Volume = CreateParameter("Volume", 3, 1.0m, 50, 4);
        Slippage = CreateParameter("Slippage", 0, 0, 20, 1);

        BollingerLength = CreateParameter("Bollinger Length", 12, 4, 100, 2);
        BollingerDeviation = CreateParameter("Bollinger Deviation", 2, 0.5m, 4, 0.1m);

        _bol = IndicatorsFactory.CreateIndicatorByName("Bollinger", name + "Bollinger", false);
        _bol = (Aindicator)_tab.CreateCandleIndicator(_bol, "Prime");

        _bol.ParametersDigit[0].Value= BollingerLength.ValueInt;
        _bol.ParametersDigit[1].Value = BollingerDeviation.ValueDecimal;

        _bol.Save();

        _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;
        ParametrsChangeByUser += Event_ParametrsChangeByUser;

        Description = "Trend Strategy Based on Breaking Bollinger Lines " +
                "Buy: " +
                "1. The price is more than BollingerUpLine. " +
                "Sell: " +
                "1. Price below BollingerDownLine." +
                "Exit: " +
                "1. At the intersection of Sma with the price";
    }

    void Event_ParametrsChangeByUser()
    {
        _bol.ParametersDigit[0].Value = BollingerLength.ValueInt;
        _bol.ParametersDigit[1].Value = BollingerDeviation.ValueDecimal;
        _bol.Reload();
    }

    /// <summary>
    /// bot name
    /// ����� ���������� ���
    /// </summary>
    public override string GetNameStrategyType()
    {
        return "BollingerRevers";
    }

    /// <summary>
    /// strategy name
    /// �������� ���� ��������
    /// </summary>
    public override void ShowIndividualSettingsDialog()
    {

    }

    /// <summary>
    /// trade tab
    /// ������� ��� ��������
    /// </summary>
    private BotTabSimple _tab;

    //indicators ����������

    private Aindicator _bol;

    //settings ��������� ���������

    public StrategyParameterInt Slippage;

    public StrategyParameterDecimal Volume;

    public StrategyParameterString Regime;

    public StrategyParameterDecimal BollingerDeviation;

    public StrategyParameterInt BollingerLength;

    private decimal _lastPrice;
    private decimal _bolLastUp;
    private decimal _bolLastDown;

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

        if (_bol.DataSeries[0].Values == null || candles.Count < _bol.ParametersDigit[0].Value + 2)
        {
            return;
        }

        _lastPrice = candles[candles.Count - 1].Close;
        _bolLastUp = _bol.DataSeries[0].Last;
        _bolLastDown = _bol.DataSeries[1].Last;

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
        if (_lastPrice > _bolLastUp
            && Regime.ValueString != "OnlyShort")
        {
            _tab.BuyAtLimit(Volume.ValueDecimal, _lastPrice + Slippage.ValueInt * _tab.Securiti.PriceStep);
        }

        if (_lastPrice < _bolLastDown
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
        if (position.State == PositionStateType.Closing ||
            position.CloseActiv == true ||
            (position.CloseOrders != null &&  position.CloseOrders.Count > 0)) 
        {
            return;
        }

        if (position.Direction == Side.Buy)
        {
            if (_lastPrice < _bolLastDown)
            {
                _tab.CloseAtLimit(
                    position,
                    _lastPrice - Slippage.ValueInt * _tab.Securiti.PriceStep,
                    position.OpenVolume);

                if (Regime.ValueString != "OnlyLong"
                    && Regime.ValueString != "OnlyClosePosition")
                {
                    _tab.SellAtLimit(Volume.ValueDecimal, _lastPrice - Slippage.ValueInt * _tab.Securiti.PriceStep);
                }
            }
        }
        if (position.Direction == Side.Sell)
        {
            if (_lastPrice > _bolLastUp)
            {
                _tab.CloseAtLimit(
                    position,
                    _lastPrice + Slippage.ValueInt * _tab.Securiti.PriceStep,
                    position.OpenVolume);

                if (Regime.ValueString != "OnlyShort"
                    && Regime.ValueString != "OnlyClosePosition")
                {
                    _tab.BuyAtLimit(Volume.ValueDecimal, _lastPrice + Slippage.ValueInt * _tab.Securiti.PriceStep);
                }

            }
        }
    }
}