/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using System.IO;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;
using System;
using OsEngine.Market.Servers;
using OsEngine.Market;

public class StrategyLevermor : BotPanel
{
    private BotTabSimple _tab;
    
    // Basic settings
    public StrategyParameterInt Slippage;
    public StrategyParameterString Regime;
    public StrategyParameterInt MaximumPosition;
    public StrategyParameterDecimal PersentDopBuy;
    public StrategyParameterDecimal PersentDopSell;
    public StrategyParameterInt ChannelLength;
    public StrategyParameterInt SmaLength; 

    // GetVolume Settings
    private StrategyParameterString _volumeType;
    private StrategyParameterDecimal _volume;
    private StrategyParameterString _tradeAssetInPortfolio;
    
    // Indicator
    private Aindicator _sma;
    private Aindicator _channel;

    // Exit settings
    public StrategyParameterDecimal TralingStopLength;
    public StrategyParameterString ExitType;

    public StrategyLevermor(string name, StartProgram startProgram) : base(name, startProgram)
    {
        TabCreate(BotTabType.Simple);
        _tab = TabsSimple[0];
        
        // Basic settings
        Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
        ChannelLength = CreateParameter("ChannelLength", 10, 10, 400, 10);
        SmaLength = CreateParameter("SmaLength", 10, 5, 150, 2);
        MaximumPosition = CreateParameter("MaxPosition", 5, 1, 20, 3);
        Slippage = CreateParameter("Slipage", 0, 0, 20, 1);
        PersentDopBuy = CreateParameter("PersentDopBuy", 0.5m, 0.1m, 2, 0.1m);
        PersentDopSell = CreateParameter("PersentDopSell", 0.5m, 0.1m, 2, 0.1m);
        
        // GetVolume Settings
        _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
        _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
        _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

        // Exit settings
        TralingStopLength = CreateParameter("TralingStopLength", 3, 3, 8, 0.5m);
        ExitType = CreateParameter("ExitType", "Traling", new[] { "Traling", "Sma" });

        // Create indicator Sma
        _sma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "MovingLong", false);
        _sma = (Aindicator)_tab.CreateCandleIndicator(_sma, "Prime");
        _sma.ParametersDigit[0].Value = SmaLength.ValueInt;
        _sma.Save();

        // Create indicator PriceChannel
        _channel = IndicatorsFactory.CreateIndicatorByName("PriceChannel", name + "Chanel", false);
        _channel = (Aindicator)_tab.CreateCandleIndicator(_channel, "Prime");
        _channel.ParametersDigit[0].Value = ChannelLength.ValueInt;
        _channel.ParametersDigit[1].Value = ChannelLength.ValueInt;
        _channel.Save();

        _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

        _tab.PositionOpeningSuccesEvent += PositionOpeningSuccesEvent;

        DeleteEvent += Strategy_DeleteEvent;

        ParametrsChangeByUser += StrategyLevermor_ParametrsChangeByUser;

        Description = "Buy: " +
            "1) Close exits PriceChannel up and Close is above the moving average. " +
            "2) We buy more every N percent in the direction of travel. (Pyramid). " +
            "Sale: " +
            "1) Close exits PriceChannel down and Close is below the moving average. " +
            "2) We buy more every N percent in the direction of travel. " +
            "Closure: " +
            "1) Fixed Stop Loss. " +
            "2) Crossing the moving average";
    }

    // Indicator Update event
    void StrategyLevermor_ParametrsChangeByUser()
    {
        _channel.ParametersDigit[0].Value = ChannelLength.ValueInt;
        _channel.ParametersDigit[1].Value = ChannelLength.ValueInt;
        _channel.Reload();
        _sma.ParametersDigit[0].Value = SmaLength.ValueInt;
        _sma.Reload();
    }

    // The name of the robot in OsEngine
    public override string GetNameStrategyType()
    {
        return "StrategyLevermor";
    }
    public override void ShowIndividualSettingsDialog()
    {

    }

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

    // Candle Finished Event
    private void Strateg_CandleFinishedEvent(List<Candle> candles)
    {
        // If the robot is turned off, exit the event handler
        if (Regime.ValueString == "Off")
        {
            return;
        }

        // If there are not enough candles to build an indicator, we exit
        if (_sma.ParametersDigit[0].Value > candles.Count ||
            _channel.ParametersDigit[0].Value > candles.Count ||
            _channel.ParametersDigit[1].Value > candles.Count)
        {
            return;
        }

        // we distribute logic depending on the current position

        List<Position> openPosition = _tab.PositionsOpenAll;

        // If there are positions, then go to the position closing method
        if (openPosition != null && openPosition.Count != 0)
        {
            LogicClosePosition(openPosition, candles);
        }

        // If the position closing mode, then exit the method
        if (Regime.ValueString == "OnlyClosePosition")
        {
            return;
        }

        // Position opening method
        LogicOpenPosition(candles);
    }

    // Opening logic
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
                    _tab.BuyAtLimit(GetVolume(_tab), lastPrice + (Slippage.ValueInt * _tab.Securiti.PriceStep));
                }
            }
            else if (positions == null || positions.Count == 0)
            {
                _tab.SellAtStopCancel();
                _tab.BuyAtStopCancel();
                _tab.BuyAtStop(GetVolume(_tab), maxToCandleSeries + (Slippage.ValueInt * _tab.Securiti.PriceStep), maxToCandleSeries, StopActivateType.HigherOrEqual);
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
                    _tab.SellAtLimit(GetVolume(_tab), lastPrice - (Slippage.ValueInt * _tab.Securiti.PriceStep));
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
                _tab.SellAtStop(GetVolume(_tab), minToCandleSeries - (Slippage.ValueInt * _tab.Securiti.PriceStep), minToCandleSeries, StopActivateType.LowerOrEqyal);
            }
        }
    }

    // Logic close position
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

    // Method for calculating the volume of entry into a position
    private decimal GetVolume(BotTabSimple tab)
    {
        decimal volume = 0;

        if (_volumeType.ValueString == "Contracts")
        {
            volume = _volume.ValueDecimal;
        }
        else if (_volumeType.ValueString == "Contract currency")
        {
            decimal contractPrice = tab.PriceBestAsk;
            volume = _volume.ValueDecimal / contractPrice;

            if (StartProgram == StartProgram.IsOsTrader)
            {
                IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);

                if (serverPermission != null &&
                    serverPermission.IsUseLotToCalculateProfit &&
                tab.Security.Lot != 0 &&
                    tab.Security.Lot > 1)
                {
                    volume = _volume.ValueDecimal / (contractPrice * tab.Security.Lot);
                }

                volume = Math.Round(volume, tab.Security.DecimalsVolume);
            }
            else // Tester or Optimizer
            {
                volume = Math.Round(volume, 6);
            }
        }
        else if (_volumeType.ValueString == "Deposit percent")
        {
            Portfolio myPortfolio = tab.Portfolio;

            if (myPortfolio == null)
            {
                return 0;
            }

            decimal portfolioPrimeAsset = 0;

            if (_tradeAssetInPortfolio.ValueString == "Prime")
            {
                portfolioPrimeAsset = myPortfolio.ValueCurrent;
            }
            else
            {
                List<PositionOnBoard> positionOnBoard = myPortfolio.GetPositionOnBoard();

                if (positionOnBoard == null)
                {
                    return 0;
                }

                for (int i = 0; i < positionOnBoard.Count; i++)
                {
                    if (positionOnBoard[i].SecurityNameCode == _tradeAssetInPortfolio.ValueString)
                    {
                        portfolioPrimeAsset = positionOnBoard[i].ValueCurrent;
                        break;
                    }
                }
            }

            if (portfolioPrimeAsset == 0)
            {
                SendNewLogMessage("Can`t found portfolio " + _tradeAssetInPortfolio.ValueString, OsEngine.Logging.LogMessageType.Error);
                return 0;
            }

            decimal moneyOnPosition = portfolioPrimeAsset * (_volume.ValueDecimal / 100);

            decimal qty = moneyOnPosition / tab.PriceBestAsk / tab.Security.Lot;

            if (tab.StartProgram == StartProgram.IsOsTrader)
            {
                qty = Math.Round(qty, tab.Security.DecimalsVolume);
            }
            else
            {
                qty = Math.Round(qty, 7);
            }

            return qty;
        }

        return volume;
    }
}