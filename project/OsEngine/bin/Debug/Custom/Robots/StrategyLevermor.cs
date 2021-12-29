using System.Collections.Generic;
using System.IO;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;
using System;


public class StrategyLevermor : BotPanel
{
    public StrategyLevermor(string name, StartProgram startProgram)
        : base(name, startProgram)
    {
        TabCreate(BotTabType.Simple);
        _tab = TabsSimple[0];

        Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
        ChannelLength = CreateParameter("ChannelLength", 10, 10, 400, 10);
        SmaLength = CreateParameter("SmaLength", 10, 5, 150, 2);
        MaximumPosition = CreateParameter("MaxPosition", 5, 1, 20, 3);

        Volume = CreateParameter("Volume", 3, 1.0m, 50, 4);
        VolumeType = CreateParameter("Volume type", "Absolute", new[] { "Absolute", "Portfolio %", });
        VolumeDecimals = CreateParameter("Volume decimals", 0, 0, 30, 1);
        Slippage = CreateParameter("Slipage", 0, 0, 20, 1);
        PersentDopBuy = CreateParameter("PersentDopBuy", 0.5m, 0.1m, 2, 0.1m);
        PersentDopSell = CreateParameter("PersentDopSell", 0.5m, 0.1m, 2, 0.1m);

        TralingStopLength = CreateParameter("TralingStopLength", 3, 3, 8, 0.5m);
        ExitType = CreateParameter("ExitType", "Traling", new[] { "Traling", "Sma" });

        _sma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "MovingLong", false);
        _sma = (Aindicator)_tab.CreateCandleIndicator(_sma, "Prime");
        _sma.ParametersDigit[0].Value = SmaLength.ValueInt;

        _sma.Save();

        _channel = IndicatorsFactory.CreateIndicatorByName("PriceChannel", name + "Chanel", false);
        _channel = (Aindicator)_tab.CreateCandleIndicator(_channel, "Prime");
        _channel.ParametersDigit[0].Value = ChannelLength.ValueInt;
        _channel.ParametersDigit[1].Value = ChannelLength.ValueInt;
        _channel.Save();

        _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;
        _tab.PositionOpeningSuccesEvent += PositionOpeningSuccesEvent;
        DeleteEvent += Strategy_DeleteEvent;

        ParametrsChangeByUser += StrategyLevermor_ParametrsChangeByUser;
    }

    void StrategyLevermor_ParametrsChangeByUser()
    {
        _channel.ParametersDigit[0].Value = ChannelLength.ValueInt;
        _channel.ParametersDigit[1].Value = ChannelLength.ValueInt;
        _channel.Reload();

        _sma.ParametersDigit[0].Value = SmaLength.ValueInt;
        _sma.Reload();
    }

    public override string GetNameStrategyType()
    {
        return "Levermor";
    }

    public override void ShowIndividualSettingsDialog()
    {

    }

    private BotTabSimple _tab;

    private Aindicator _sma;


    private Aindicator _channel;

    public StrategyParameterInt Slippage;


    public StrategyParameterString Regime;

    public StrategyParameterDecimal Volume;

    public StrategyParameterString VolumeType;

    public StrategyParameterInt VolumeDecimals;

    public StrategyParameterInt MaximumPosition;
    public StrategyParameterDecimal PersentDopBuy;
    public StrategyParameterDecimal PersentDopSell;

    public StrategyParameterInt ChannelLength;
    public StrategyParameterInt SmaLength;

    public StrategyParameterDecimal TralingStopLength;
    public StrategyParameterString ExitType;

    void Strategy_DeleteEvent()
    {
        if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
        {
            File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
        }
    }

    private void PositionOpeningSuccesEvent(Position position)
    {
        if (Regime.ValueString == "Off")
        {
            return;
        }

        List<Position> openPosition = _tab.PositionsOpenAll;

        if (openPosition != null && openPosition.Count != 0)
        {
            LogicClosePosition(openPosition, _tab.CandlesFinishedOnly);
        }
    }

    private void Strateg_CandleFinishedEvent(List<Candle> candles)
    {
        if (Regime.ValueString == "Off")
        {
            return;
        }

        if (_sma.ParametersDigit[0].Value > candles.Count ||
            _channel.ParametersDigit[0].Value > candles.Count ||
            _channel.ParametersDigit[1].Value > candles.Count)
        {
            return;
        }

        // распределяем логику в зависимости от текущей позиции
        // we distribute logic depending on the current position

        List<Position> openPosition = _tab.PositionsOpenAll;

        if (openPosition != null && openPosition.Count != 0)
        {
            LogicClosePosition(openPosition, candles);
        }

        if (Regime.ValueString == "OnlyClosePosition")
        {
            return;
        }

        LogicOpenPosition(candles);

    }


    private void LogicOpenPosition(List<Candle> candles)
    {

        decimal lastMa = _sma.DataSeries[0].Values[_sma.DataSeries[0].Values.Count - 1];

        decimal lastPrice = candles[candles.Count - 1].Close;

        if (lastMa == 0)
        {
            return;
        }

        decimal maxToCandleSeries = _channel.DataSeries[0].Values[_channel.DataSeries[0].Values.Count - 1];
        decimal minToCandleSeries = _channel.DataSeries[1].Values[_channel.DataSeries[1].Values.Count - 1];

        List<Position> positions = _tab.PositionsOpenAll;

        if (lastPrice >= lastMa && Regime.ValueString != "OnlyShort")
        {
            if (positions != null && positions.Count != 0 &&
                positions[0].Direction == Side.Buy)
            {
                if (positions.Count >= MaximumPosition.ValueInt)
                {
                    return;
                }
                decimal lastIntro = positions[positions.Count - 1].EntryPrice;
                if (lastIntro + lastIntro * (PersentDopSell.ValueDecimal / 100) < lastPrice)
                {
                    if (positions.Count >= MaximumPosition.ValueInt)
                    {
                        return;
                    }
                    _tab.BuyAtLimit(GetVolume(), lastPrice + (Slippage.ValueInt * _tab.Securiti.PriceStep));
                }
            }
            else if (positions == null || positions.Count == 0)
            {
                _tab.SellAtStopCancel();
                _tab.BuyAtStopCancel();
                _tab.BuyAtStop(GetVolume(), maxToCandleSeries + (Slippage.ValueInt * _tab.Securiti.PriceStep), maxToCandleSeries, StopActivateType.HigherOrEqual);
            }
        }

        if (lastPrice <= lastMa && Regime.ValueString != "OnlyLong")
        {
            if (positions != null && positions.Count != 0 &&
                     positions[0].Direction == Side.Sell)
            {
                if (positions.Count >= MaximumPosition.ValueInt)
                {
                    return;
                }
                decimal lastIntro = positions[positions.Count - 1].EntryPrice;

                if (lastIntro - lastIntro * (PersentDopSell.ValueDecimal / 100) > lastPrice)
                {
                    _tab.SellAtLimit(GetVolume(), lastPrice - (Slippage.ValueInt * _tab.Securiti.PriceStep));
                }
            }
            else if (positions == null || positions.Count == 0)
            {
                if (positions != null && positions.Count >= MaximumPosition.ValueInt)
                {
                    return;
                }

                _tab.SellAtStopCancel();
                _tab.BuyAtStopCancel();
                _tab.SellAtStop(GetVolume(), minToCandleSeries - (Slippage.ValueInt * _tab.Securiti.PriceStep), minToCandleSeries, StopActivateType.LowerOrEqyal);
            }
        }
    }

    private void LogicClosePosition(List<Position> positions, List<Candle> candles)
    {
        if (positions == null || positions.Count == 0)
        {
            return;
        }

        for (int i = 0; i < positions.Count; i++)
        {
            if (positions[i].State != PositionStateType.Open)
            {
                continue;
            }

            if (positions[i].State == PositionStateType.Closing)
            {
                continue;
            }

            if (ExitType.ValueString == "Sma")
            {
                if (positions[i].Direction == Side.Buy)
                {
                    if (candles[candles.Count - 1].Close < _sma.DataSeries[0].Last)
                    {
                        _tab.CloseAtMarket(positions[i], positions[i].OpenVolume);
                    }
                }
                else
                {
                    if (candles[candles.Count - 1].Close > _sma.DataSeries[0].Last)
                    {
                        _tab.CloseAtMarket(positions[i], positions[i].OpenVolume);
                    }
                }
            }
            else if (ExitType.ValueString == "Traling")
            {
                if (positions[i].Direction == Side.Buy)
                {
                    _tab.CloseAtTrailingStop(positions[i],
                        candles[candles.Count - 1].Close -
                        candles[candles.Count - 1].Close * TralingStopLength.ValueDecimal / 100,
                        candles[candles.Count - 1].Close -
                        candles[candles.Count - 1].Close * TralingStopLength.ValueDecimal / 100);
                }
                else
                {
                    _tab.CloseAtTrailingStop(positions[i],
                        candles[candles.Count - 1].Close +
                        candles[candles.Count - 1].Close * TralingStopLength.ValueDecimal / 100,
                        candles[candles.Count - 1].Close +
                        candles[candles.Count - 1].Close * TralingStopLength.ValueDecimal / 100);
                }
            }
        }
    }

    private decimal GetVolume()
    {
        if (VolumeType.ValueString == "Absolute")
        {
            return Volume.ValueDecimal;
        }

        decimal volume = 0;

        decimal portfolioNow = _tab.Portfolio.ValueCurrent * (Volume.ValueDecimal / 100);

        decimal priceNow = _tab.PriceBestAsk;

        volume = portfolioNow / priceNow;

        return Math.Round(volume, VolumeDecimals.ValueInt);
    }
}