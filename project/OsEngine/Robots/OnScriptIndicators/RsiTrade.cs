using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using OsEngine.Charts.CandleChart.Elements;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;

/// <summary>
/// RSI's concurrent overbought and oversold strategy
/// конттрендовая стратегия RSI на перекупленность и перепроданность
/// </summary>
public class RsiTrade : BotPanel
{
    public RsiTrade(string name, StartProgram startProgram)
        : base(name, startProgram)
    {
        TabCreate(BotTabType.Simple);
        _tab = TabsSimple[0];

        _rsi = IndicatorsFactory.CreateIndicatorByName("RSI",name + "RSI", false);
        _rsi = (Aindicator)_tab.CreateCandleIndicator(_rsi, "RsiArea");

        Upline = new LineHorisontal("upline", "RsiArea", false)
        {
            Color = Color.Green,
            Value = 0,
        };
        _tab.SetChartElement(Upline);

        Downline = new LineHorisontal("downline", "RsiArea", false)
        {
            Color = Color.Yellow,
            Value = 0
        };
        _tab.SetChartElement(Downline);

        Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
        Volume = CreateParameter("Volume", 1, 1.0m, 50, 4);
        Slippage = CreateParameter("Slippage", 0, 0, 20, 1);
        RsiLength = CreateParameter("Rsi Length", 20, 10, 40, 2);
        UpLineValue = CreateParameter("Up Line Value", 65, 60.0m, 90, 0.5m);
        DownLineValue = CreateParameter("Down Line Value", 35, 10.0m, 40, 0.5m);

        _rsi.ParametersDigit[0].Value = RsiLength.ValueInt;

        _rsi.Save();

        Upline.Value = UpLineValue.ValueDecimal;
        Downline.Value = DownLineValue.ValueDecimal;

        Upline.TimeEnd = DateTime.Now;
        Downline.TimeEnd = DateTime.Now;

        _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;
        DeleteEvent += Strategy_DeleteEvent;
        ParametrsChangeByUser += Event_ParametrsChangeByUser;
    }

    void Event_ParametrsChangeByUser()
    {
        if (_rsi.ParametersDigit[0].Value != RsiLength.ValueInt)
        {
            _rsi.ParametersDigit[0].Value = RsiLength.ValueInt;
            _rsi.Reload();
        }

        Upline.Value = UpLineValue.ValueDecimal;
        Upline.Refresh();
        Downline.Value = DownLineValue.ValueDecimal;
        Downline.Refresh();
    }

    /// <summary>
    /// uniq name
    /// взять уникальное имя
    /// </summary>
    public override string GetNameStrategyType()
    {
        return "RsiTrade";
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

    private Aindicator _rsi;

    //settings настройки публичные

    public StrategyParameterInt Slippage;

    public StrategyParameterDecimal Volume;

    public StrategyParameterString Regime;

    public StrategyParameterDecimal UpLineValue;

    public StrategyParameterDecimal DownLineValue;

    public StrategyParameterInt RsiLength;

    /// <summary>
    /// верхняя линия для отрисовки
    /// </summary>
    public LineHorisontal Upline;

    /// <summary>
    /// нижняя линия для отрисовки
    /// </summary>
    public LineHorisontal Downline;

    /// <summary>
    /// delete save file
    /// удаление файла с сохранением
    /// </summary>
    void Strategy_DeleteEvent()
    {
        if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
        {
            File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
        }
    }

    private decimal _lastPrice;
    private decimal _firstRsi;
    private decimal _secondRsi;

    //logic логика

    /// <summary>
    /// candles finished event
    /// событие завершения свечи
    /// </summary>
    private void Strateg_CandleFinishedEvent(List<Candle> candles)
    {
        if (Regime.ValueString == "Off")
        {
            return;
        }

        if (_rsi.DataSeries[0].Values == null)
        {
            return;
        }

        if (_rsi.DataSeries[0].Values.Count < _rsi.ParametersDigit[0].Value + 5)
        {
            return;

        }

        _lastPrice = candles[candles.Count - 1].Close;
        _firstRsi = _rsi.DataSeries[0].Values[_rsi.DataSeries[0].Values.Count - 1];
        _secondRsi = _rsi.DataSeries[0].Values[_rsi.DataSeries[0].Values.Count - 2];




        List<Position> openPositions = _tab.PositionsOpenAll;

        if (openPositions != null && openPositions.Count != 0)
        {
            for (int i = 0; i < openPositions.Count; i++)
            {
                LogicClosePosition(candles, openPositions[i]);

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
    /// logic open first position
    /// логика открытия первой позиции
    /// </summary>
    private void LogicOpenPosition(List<Candle> candles, List<Position> position)
    {
        if (_secondRsi < Downline.Value && _firstRsi > Downline.Value
                                            && Regime.ValueString != "OnlyShort")
        {
            _tab.BuyAtLimit(Volume.ValueDecimal,
                _lastPrice + Slippage.ValueInt * _tab.Securiti.PriceStep);
        }

        if (_secondRsi > Upline.Value && _firstRsi < Upline.Value
                                          && Regime.ValueString != "OnlyLong")
        {
            _tab.SellAtLimit(Volume.ValueDecimal,
                _lastPrice - Slippage.ValueInt * _tab.Securiti.PriceStep);
        }
    }

    /// <summary>
    /// logic close position
    /// логика зыкрытия позиции и открытие по реверсивной системе
    /// </summary>
    private void LogicClosePosition(List<Candle> candles, Position position)
    {

        if (position.State == PositionStateType.Closing)
        {
            return;
        }
        if (position.Direction == Side.Buy)
        {
            if (_secondRsi >= Upline.Value && _firstRsi <= Upline.Value)
            {
                _tab.CloseAtLimit(position,
                    _lastPrice - Slippage.ValueInt * _tab.Securiti.PriceStep, position.OpenVolume);

                if (Regime.ValueString != "OnlyLong" && Regime.ValueString != "OnlyClosePosition")
                {
                    _tab.SellAtLimit(Volume.ValueDecimal,
                        _lastPrice - Slippage.ValueInt * _tab.Securiti.PriceStep);
                }
            }
        }
        if (position.Direction == Side.Sell)
        {
            if (_secondRsi <= Downline.Value && _firstRsi >= Downline.Value)
            {
                _tab.CloseAtLimit(position,
                    _lastPrice + Slippage.ValueInt * _tab.Securiti.PriceStep, position.OpenVolume);

                if (Regime.ValueString != "OnlyShort" && Regime.ValueString != "OnlyClosePosition")
                {
                    _tab.BuyAtLimit(Volume.ValueDecimal,
                        _lastPrice + Slippage.ValueInt * _tab.Securiti.PriceStep);
                }
            }
        }
    }
}