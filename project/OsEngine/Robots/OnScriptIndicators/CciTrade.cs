using System.Collections.Generic;
using System.Drawing;
using OsEngine.Charts.CandleChart.Elements;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;

/// <summary>
/// Counter Trend Strategy Based on CCI Indicator. Max - 3 poses
/// Контртрендовая стратегия на основе индикатора CCI. Входит в три позиции
/// </summary>
public class CciTrade : BotPanel
{
    public CciTrade(string name, StartProgram startProgram)
        : base(name, startProgram)
    {
        TabCreate(BotTabType.Simple);
        _tab = TabsSimple[0];

        Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
        Volume = CreateParameter("Volume", 3, 1.0m, 50, 4);
        Slippage = CreateParameter("Slippage", 0, 0, 20, 1);
        CciLength = CreateParameter("Cci Length", 20, 10, 40, 2);
        UpLineValue = CreateParameter("Up Line Value", 150, 50.0m, 300, 20m);
        DownLineValue = CreateParameter("Down Line Value", -150, -300.0m, -50, 20);

        _cci = IndicatorsFactory.CreateIndicatorByName("CCI",name + "Cci", false);
        _cci.ParametersDigit[0].Value = CciLength.ValueInt;
        _cci = (Aindicator)_tab.CreateCandleIndicator(_cci, "CciArea");
        _cci.Save();

        Upline = new LineHorisontal("upline", "CciArea", false)
        {
            Color = Color.Green,
            Value = 0,

        };
        _tab.SetChartElement(Upline);

        Downline = new LineHorisontal("downline", "CciArea", false)
        {
            Color = Color.Yellow,
            Value = 0

        };
        _tab.SetChartElement(Downline);

        Upline.Value = UpLineValue.ValueDecimal;
        Downline.Value = DownLineValue.ValueDecimal;

        _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;
        ParametrsChangeByUser += Event_ParametrsChangeByUser;
    }

    void Event_ParametrsChangeByUser()
    {
        if (_cci.ParametersDigit[0].Value != CciLength.ValueInt)
        {
            _cci.ParametersDigit[0].Value = CciLength.ValueInt;
            _cci.Reload();
        }

        Upline.Value = UpLineValue.ValueDecimal;
        Upline.Refresh();
        Downline.Value = DownLineValue.ValueDecimal;
        Downline.Refresh();
    }

    /// <summary>
    /// strategy name
    /// взять уникальное имя
    /// </summary>
    public override string GetNameStrategyType()
    {
        return "CciTrade";
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

    private Aindicator _cci;

    //settings настройки публичные


    public StrategyParameterInt Slippage;

    public StrategyParameterDecimal Volume;

    public StrategyParameterString Regime;

    public StrategyParameterDecimal UpLineValue;

    public StrategyParameterDecimal DownLineValue;

    public StrategyParameterInt CciLength;

    /// <summary>
    /// up line to trade
    /// верхняя линия для отрисовки
    /// </summary>
    public LineHorisontal Upline;

    /// <summary>
    /// down line to trade
    /// нижняя линия для отрисовки
    /// </summary>
    public LineHorisontal Downline;

    private decimal _lastPrice;
    private decimal _lastCci;

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

        if (_cci.DataSeries[0].Values == null)
        {
            return;
        }

        _lastPrice = candles[candles.Count - 1].Close;
        _lastCci = _cci.DataSeries[0].Values[_cci.DataSeries[0].Values.Count - 1];

        List<Position> openPositions = _tab.PositionsOpenAll;

        if (openPositions != null && openPositions.Count != 0)
        {
            for (int i = 0; i < openPositions.Count; i++)
            {
                LogicClosePosition(candles, openPositions[i], openPositions);

                Upline.Refresh();
                Downline.Refresh();
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
        if (_lastCci < Downline.Value
            && Regime.ValueString != "OnlyShort")
        {
            _tab.BuyAtLimit(Volume.ValueDecimal, _lastPrice + Slippage.ValueInt * _tab.Securiti.PriceStep);
        }

        if (_lastCci > Upline.Value && Regime.ValueString != "OnlyLong")
        {
            _tab.SellAtLimit(Volume.ValueDecimal, _lastPrice - Slippage.ValueInt * _tab.Securiti.PriceStep);
        }
    }

    /// <summary>
    /// logic close position
    /// логика зыкрытия позиции и открытие по реверсивной системе
    /// </summary>
    private void LogicClosePosition(List<Candle> candles, Position position, List<Position> allPos)
    {
        if (position.State != PositionStateType.Open)
        {
            return;
        }

        if (position.Direction == Side.Buy)
        {
            if (_lastCci > Upline.Value)
            {
                _tab.CloseAtLimit(position, _lastPrice - Slippage.ValueInt * _tab.Securiti.PriceStep, position.OpenVolume);

                if (Regime.ValueString != "OnlyLong" &&
                    Regime.ValueString != "OnlyClosePosition" &&
                    allPos.Count < 3)
                {
                    _tab.SellAtLimit(Volume.ValueDecimal, _lastPrice - Slippage.ValueInt * _tab.Securiti.PriceStep);
                }
            }
        }

        if (position.Direction == Side.Sell)
        {
            if (_lastCci < Downline.Value)
            {
                _tab.CloseAtLimit(position, _lastPrice + Slippage.ValueInt * _tab.Securiti.PriceStep, position.OpenVolume);

                if (Regime.ValueString != "OnlyShort" &&
                    Regime.ValueString != "OnlyClosePosition" &&
                    allPos.Count < 3)
                {
                    _tab.BuyAtLimit(Volume.ValueDecimal, _lastPrice + Slippage.ValueInt * _tab.Securiti.PriceStep);
                }
            }
        }
    }

}