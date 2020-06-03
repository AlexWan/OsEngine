using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Charts.CandleChart.Elements;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;

/// <summary>
/// counter trend strategy stochastic
/// конттрендовая стратегия Stochastic
/// </summary>
public class StochasticTrade : BotPanel
{
    public StochasticTrade(string name, StartProgram startProgram)
        : base(name, startProgram)
    {
        TabCreate(BotTabType.Simple);
        _tab = TabsSimple[0];
        
        _stoch = IndicatorsFactory.CreateIndicatorByName("Stochastic", name + "Stochastic", false);
        _stoch = (Aindicator)_tab.CreateCandleIndicator(_stoch, "StochasticArea");

        Upline = new LineHorisontal("upline", "StochasticArea", false)
        {
            Color = Color.Green,
            Value = 0,
        };
        _tab.SetChartElement(Upline);

        Downline = new LineHorisontal("downline", "StochasticArea", false)
        {
            Color = Color.Yellow,
            Value = 0

        };
        _tab.SetChartElement(Downline);

        Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
        Volume = CreateParameter("Volume", 3, 1.0m, 50, 4);
        Slippage = CreateParameter("Slippage", 0, 0, 20, 1);

        UpLineValue = CreateParameter("Up Line Value", 80, 60.0m, 90, 0.5m);
        DownLineValue = CreateParameter("Down Line Value", 20, 10.0m, 40, 0.5m);

        StochPeriod1 = CreateParameter("Stoch Period 1", 5, 3, 40, 1);
        StochPeriod2 = CreateParameter("Stoch Period 2", 3, 2, 40, 1);
        StochPeriod3 = CreateParameter("Stoch Period 3", 3, 2, 40, 1);

        Upline.Value = UpLineValue.ValueDecimal;
        Downline.Value = DownLineValue.ValueDecimal;

        _stoch.ParametersDigit[0].Value = StochPeriod1.ValueInt;
        _stoch.ParametersDigit[1].Value = StochPeriod2.ValueInt;
        _stoch.ParametersDigit[2].Value = StochPeriod3.ValueInt;

        _stoch.Save();

        _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;



        Upline.TimeEnd = DateTime.Now;
        Downline.TimeEnd = DateTime.Now;

        ParametrsChangeByUser += RviTrade_ParametrsChangeByUser;
    }

    void RviTrade_ParametrsChangeByUser()
    {
        _stoch.ParametersDigit[0].Value = StochPeriod1.ValueInt;
        _stoch.ParametersDigit[1].Value = StochPeriod2.ValueInt;
        _stoch.ParametersDigit[2].Value = StochPeriod3.ValueInt;

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
        return "StochasticTrade";
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

    private Aindicator _stoch;

    public LineHorisontal Upline;

    public LineHorisontal Downline;

    public StrategyParameterDecimal UpLineValue;

    public StrategyParameterDecimal DownLineValue;

    public StrategyParameterInt Slippage;

    public StrategyParameterDecimal Volume;

    public StrategyParameterString Regime;

    public StrategyParameterInt StochPeriod1;

    public StrategyParameterInt StochPeriod2;

    public StrategyParameterInt StochPeriod3;

    private decimal _lastPrice;
    private decimal _stocLastUp;
    private decimal _stocLastDown;

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

        if (_stoch.DataSeries[0].Values == null ||
            _stoch.DataSeries[1].Values == null)
        {
            return;
        }

        _lastPrice = candles[candles.Count - 1].Close;
        _stocLastUp = _stoch.DataSeries[0].Values[_stoch.DataSeries[0].Values.Count - 1];
        _stocLastDown = _stoch.DataSeries[1].Values[_stoch.DataSeries[1].Values.Count - 1];

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
    /// logic open position
    /// логика открытия первой позиции
    /// </summary>
    private void LogicOpenPosition(List<Candle> candles, List<Position> position)
    {
        if (_stocLastDown < Downline.Value && _stocLastDown > _stocLastUp
                                           && Regime.ValueString != "OnlyShort")
        {
            _tab.BuyAtLimit(
                Volume.ValueDecimal,
                _lastPrice + Slippage.ValueInt * _tab.Securiti.PriceStep);
        }

        if (_stocLastDown > Upline.Value && _stocLastDown < _stocLastUp
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
        if (position.Direction == Side.Buy)
        {
            if (_stocLastDown > Upline.Value && _stocLastDown < _stocLastUp)
            {
                _tab.CloseAtLimit(
                    position,
                    _lastPrice - Slippage.ValueInt * _tab.Securiti.PriceStep,
                    position.OpenVolume);

                if (Regime.ValueString != "OnlyLong" && Regime.ValueString != "OnlyClosePosition")
                {
                    List<Position> positions = _tab.PositionsOpenAll;
                    if (positions.Count >= 2)
                    {
                        return;
                    }

                    _tab.SellAtLimit(Volume.ValueDecimal, _lastPrice - Slippage.ValueInt * _tab.Securiti.PriceStep);
                }
            }
        }

        if (position.Direction == Side.Sell)
        {
            if (_stocLastDown < Downline.Value && _stocLastDown > _stocLastUp)
            {
                _tab.CloseAtLimit(
                    position,
                    _lastPrice + Slippage.ValueInt * _tab.Securiti.PriceStep,
                    position.OpenVolume);

                if (Regime.ValueString != "OnlyShort" && Regime.ValueString != "OnlyClosePosition")
                {
                    List<Position> positions = _tab.PositionsOpenAll;
                    if (positions.Count >= 2)
                    {
                        return;
                    }
                    _tab.BuyAtLimit(Volume.ValueDecimal, _lastPrice + Slippage.ValueInt * _tab.Securiti.PriceStep);
                }

            }
        }
    }
}