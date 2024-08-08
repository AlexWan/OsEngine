using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;

/// <summary>
///Breakthrough of the channel built by PriceChannel + -ATR * coefficient,
/// additional input when the price leaves below the channel line by ATR * coefficient.
/// Trailing stop on the bottom line of the PriceChannel channel
/// 
/// ������ ������ ����������� �� PriceChannel ,
/// �������������� ���� ��� ����� ���� ���� ����� ������ �� ATR*�����������.
/// �������� ���� �� ������ ����� ������ PriceChannel
/// </summary>
[Bot("PriceChannelVolatility")]
public class PriceChannelVolatility : BotPanel
{
    public PriceChannelVolatility(string name, StartProgram startProgram)
        : base(name, startProgram)
    {
        TabCreate(BotTabType.Simple);
        _tab = TabsSimple[0];

        Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
        Slippage = CreateParameter("Slippage", 0, 0, 20, 1);
        VolumeFix1 = CreateParameter("Volume 1", 3, 1.0m, 50, 4);
        VolumeFix2 = CreateParameter("Volume 2", 3, 1.0m, 50, 4);
        LengthAtr = CreateParameter("Length Atr", 14, 5, 80, 3);
        KofAtr = CreateParameter("Atr mult", 0.5m, 0.1m, 5, 0.1m);
        LengthChannelUp = CreateParameter("Length Channel Up", 12, 5, 80, 2);
        LengthChannelDown = CreateParameter("Length Channel Down", 12, 5, 80, 2);

        _pc = IndicatorsFactory.CreateIndicatorByName("PriceChannel", name + "PriceChannel", false);

        _pc.ParametersDigit[0].Value = LengthChannelUp.ValueInt;
        _pc.ParametersDigit[1].Value = LengthChannelDown.ValueInt;

        _pc = (Aindicator)_tab.CreateCandleIndicator(_pc, "Prime");
        _pc.Save();

        _atr = IndicatorsFactory.CreateIndicatorByName("ATR",name + "ATR", false);

        _atr.ParametersDigit[0].Value = LengthAtr.ValueInt;

        _atr = (Aindicator)_tab.CreateCandleIndicator(_atr, "Second");
        _atr.Save();

        _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

        ParametrsChangeByUser += Event_ParametrsChangeByUser;

        Description = "Breakthrough of the channel built by PriceChannel + -ATR * coefficient," +
            " additional input when the price leaves below the channel line by ATR * coefficient." +
            " Trailing stop on the bottom line of the PriceChannel channel";
    }

    void Event_ParametrsChangeByUser()
    {
        if (_atr.ParametersDigit[0].Value != LengthAtr.ValueInt)
        {
            _atr.ParametersDigit[0].Value = LengthAtr.ValueInt;
            _atr.Reload();
        }
        if (_pc.ParametersDigit[0].Value != LengthChannelUp.ValueInt)
        {
            _pc.ParametersDigit[0].Value = LengthChannelUp.ValueInt;
            _pc.Reload();
        }
        if (_pc.ParametersDigit[1].Value != LengthChannelDown.ValueInt)
        {
            _pc.ParametersDigit[1].Value = LengthChannelDown.ValueInt;
            _pc.Reload();
        }
    }

    /// <summary>
    /// uniq name
    /// ����� ���������� ���
    /// </summary>
    public override string GetNameStrategyType()
    {
        return "PriceChannelVolatility";
    }

    /// <summary>
    /// settings GUI
    /// �������� ���� ��������
    /// </summary>
    public override void ShowIndividualSettingsDialog()
    {
    }

    /// <summary>
    /// trading tab
    /// ������� ��� ��������
    /// </summary>
    private BotTabSimple _tab;

    /// <summary>
    /// ATR
    /// </summary>
    private Aindicator _atr;

    /// <summary>
    /// PriceChannel
    /// </summary>
    private Aindicator _pc;

    /// <summary>
    /// Atr period
    /// ������ ATR
    /// </summary>
    public StrategyParameterInt LengthAtr;

    /// <summary>
    /// PriceChannel up line length
    /// ������ PriceChannel Up
    /// </summary>
    public StrategyParameterInt LengthChannelUp;

    /// <summary>
    /// PriceChannel down line length
    /// ������ PriceChannel Down
    /// </summary>
    public StrategyParameterInt LengthChannelDown;

    /// <summary>
    /// volume first
    /// ������������� ����� ��� ����� � ������ �������
    /// </summary>
    public StrategyParameterDecimal VolumeFix1;

    /// <summary>
    /// volume next
    /// ������������� ����� ��� ����� �� ������ �������
    /// </summary>
    public StrategyParameterDecimal VolumeFix2;

    /// <summary>
    /// atr coef
    /// ����������� ATR
    /// </summary>
    public StrategyParameterDecimal KofAtr;

    /// <summary>
    /// slippage
    /// ���������������
    /// </summary>
    public StrategyParameterInt Slippage;

    /// <summary>
    /// regime
    /// ����� ������
    /// </summary>
    public StrategyParameterString Regime;

    private decimal _lastPcUp;
    private decimal _lastPcDown;
    private decimal _lastAtr;

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

        _lastPcUp = _pc.DataSeries[0].Last;
        _lastPcDown = _pc.DataSeries[1].Last;
        _lastAtr = _atr.DataSeries[0].Last;

        if (_pc.DataSeries[0].Values == null || _pc.DataSeries[1].Values == null || 
            _pc.DataSeries[0].Values.Count < _pc.ParametersDigit[0].Value + 1 ||
            _pc.DataSeries[1].Values.Count < _pc.ParametersDigit[1].Value + 1 ||
            _atr.DataSeries[0].Values == null || _atr.DataSeries[0].Values.Count < _atr.ParametersDigit[0].Value + 1)
        {
            return;
        }

        List<Position> openPositions = _tab.PositionsOpenAll;

        if (openPositions != null && openPositions.Count != 0)
        {
            LogicClosePosition();
        }

        if (Regime.ValueString == "OnlyClosePosition")
        {
            return;
        }

        if (openPositions == null || openPositions.Count == 0)
        {
            LogicOpenPosition(candles);
        }
    }

    /// <summary>
    /// logic open position
    /// ������ �������� ������ ������� � ��������������� �����
    /// </summary>
    private void LogicOpenPosition(List<Candle> candles)
    {
        List<Position> openPositions = _tab.PositionsOpenAll;
        if (openPositions == null || openPositions.Count == 0)
        {
            _tab.BuyAtStopCancel();
            _tab.SellAtStopCancel();
            // long
            if (Regime.ValueString != "OnlyShort")
            {
                decimal priceEnter = _lastPcUp;
                _tab.BuyAtStop(VolumeFix1.ValueDecimal,
                    priceEnter + Slippage.ValueInt * _tab.Securiti.PriceStep,
                    priceEnter, StopActivateType.HigherOrEqual);
            }

            // Short
            if (Regime.ValueString != "OnlyLong")
            {
                decimal priceEnter = _lastPcDown;
                _tab.SellAtStop(VolumeFix1.ValueDecimal,
                    priceEnter - Slippage.ValueInt * _tab.Securiti.PriceStep,
                    priceEnter, StopActivateType.LowerOrEqyal);
            }
            return;
        }

        if (openPositions.Count == 1)
        {
            _tab.BuyAtStopCancel();
            _tab.SellAtStopCancel();
            if (openPositions[0].Direction == Side.Buy)
            {
                decimal priceEnter = _lastPcUp + (_lastAtr * KofAtr.ValueDecimal);
                _tab.BuyAtStop(VolumeFix2.ValueDecimal,
                    priceEnter + Slippage.ValueInt * _tab.Securiti.PriceStep,
                    priceEnter, StopActivateType.HigherOrEqual);
            }
            else
            {
                decimal priceEnter = _lastPcDown - (_lastAtr * KofAtr.ValueDecimal);
                _tab.SellAtStop(VolumeFix2.ValueDecimal,
                    priceEnter - Slippage.ValueInt * _tab.Securiti.PriceStep,
                    priceEnter, StopActivateType.LowerOrEqyal);
            }
        }
    }

    /// <summary>
    /// logic close position
    /// ������ �������� �������
    /// </summary>
    private void LogicClosePosition()
    {
        List<Position> openPositions = _tab.PositionsOpenAll;
        for (int i = 0; openPositions != null && i < openPositions.Count; i++)
        {
            if (openPositions[i].State != PositionStateType.Open)
            {
                continue;
            }

            if (openPositions[i].Direction == Side.Buy)
            {
                decimal priceClose = _lastPcDown;
                _tab.CloseAtTrailingStop(openPositions[i], priceClose,
                    priceClose - Slippage.ValueInt * _tab.Securiti.PriceStep);
            }
            else
            {
                decimal priceClose = _lastPcUp;
                _tab.CloseAtTrailingStop(openPositions[i], priceClose,
                    priceClose + Slippage.ValueInt * _tab.Securiti.PriceStep);
            }
        }

    }
}