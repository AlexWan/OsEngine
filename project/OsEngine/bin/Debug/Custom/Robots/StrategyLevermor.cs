/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Logging;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.Language;


/* Description
trading robot for osengine

Trend strategy with pyramiding and optional exit by trailing stop or SMA.

Buy:
1. If the price is above the moving average (lastPrice >= SMA):
   - If dont have position:
     BuyAtStop at (upper channel border + slippage).
   - If a long position exists and price > lastEntry + (lastEntry * PercentDopBuy/100):
     BuyAtLimit (pyramiding).

Sell:
1. If the price is below the moving average (lastPrice <= SMA):
   - If dont have position:
     SellAtStop at (lower channel border - slippage).
   - If a short position exists and price < lastEntry - (lastEntry * PercentDopSell/100):
     SellAtLimit (pyramiding).

Exit from a long position:
1. Trailing stop at (close - close * TralingStopLength / 100), or
2. Market exit if Close < SMA (if ExitType = "Sma").

Exit from the short position:
1. Trailing stop at (close + close * TralingStopLength / 100), or
2. Market exit if Close > SMA (if ExitType = "Sma").
*/

[Bot("StrategyLevermor")]
public class StrategyLevermor : BotPanel
{
    // Reference to the main trading tab
    private BotTabSimple _tab;

    // Basic settings
    private StrategyParameterInt _slippage;
    private StrategyParameterString _regime;
    private StrategyParameterInt _maximumPosition;
    private StrategyParameterDecimal _percentDopBuy;
    private StrategyParameterDecimal _percentDopSell;
    private StrategyParameterTimeOfDay _startTradeTime;
    private StrategyParameterTimeOfDay _endTradeTime;

    // GetVolume Settings
    private StrategyParameterString _volumeType;
    private StrategyParameterDecimal _volume;
    private StrategyParameterString _tradeAssetInPortfolio;

    // Indicators
    private Aindicator _sma;
    private Aindicator _channel;

    //indicators settings
    private StrategyParameterInt _channelLength;
    private StrategyParameterInt _smaLength;

    // Exit settings
    private StrategyParameterDecimal TralingStopLength;
    private StrategyParameterString ExitType;

    public StrategyLevermor(string name, StartProgram startProgram) : base(name, startProgram)
    {
        // Create and assign the main trading tab
        TabCreate(BotTabType.Simple);
        _tab = TabsSimple[0];

        // Basic settings
        _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
        _maximumPosition = CreateParameter("MaxPosition", 5, 1, 20, 3, "Base");
        _slippage = CreateParameter("Slippage", 0, 0, 20, 1, "Base");
        _percentDopBuy = CreateParameter("PersentDopBuy", 0.5m, 0.1m, 2, 0.1m, "Base");
        _percentDopSell = CreateParameter("PersentDopSell", 0.5m, 0.1m, 2, 0.1m, "Base");
        _startTradeTime = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
        _endTradeTime = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

        // GetVolume Settings
        _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
        _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
        _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

        // Indicators settings
        _channelLength = CreateParameter("ChannelLength", 10, 10, 400, 10, "Indicators");
        _smaLength = CreateParameter("SmaLength", 10, 5, 150, 2, "Indicators");

        // Exit settings
        TralingStopLength = CreateParameter("TralingStopLength", 3, 3, 8, 0.5m, "Exit");
        ExitType = CreateParameter("ExitType", "Traling", new[] { "Traling", "Sma" }, "Exit");

        // Create indicator Sma
        _sma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "MovingLong", false);
        _sma = (Aindicator)_tab.CreateCandleIndicator(_sma, "Prime");
        _sma.ParametersDigit[0].Value = _smaLength.ValueInt;
        _sma.Save();

        // Create indicator PriceChannel
        _channel = IndicatorsFactory.CreateIndicatorByName("PriceChannel", name + "Chanel", false);
        _channel = (Aindicator)_tab.CreateCandleIndicator(_channel, "Prime");
        _channel.ParametersDigit[0].Value = _channelLength.ValueInt;
        _channel.ParametersDigit[1].Value = _channelLength.ValueInt;
        _channel.Save();

        _tab.CandleFinishedEvent += _tabCandleFinishedEvent;

        _tab.PositionOpeningSuccesEvent += _tabPositionOpeningSuccesEvent;

        ParametrsChangeByUser += StrategyLevermor_ParametrsChangeByUser;

        Description = OsLocalization.Description.DescriptionLabel255;
    }

    // Indicator Update event
    private void StrategyLevermor_ParametrsChangeByUser()
    {
        _channel.ParametersDigit[0].Value = _channelLength.ValueInt;
        _channel.ParametersDigit[1].Value = _channelLength.ValueInt;
        _channel.Save();
        _channel.Reload();

        _sma.ParametersDigit[0].Value = _smaLength.ValueInt;
        _sma.Save();
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

    private void _tabPositionOpeningSuccesEvent(Position position)
    {
        if (_regime.ValueString == "Off")
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
    private void _tabCandleFinishedEvent(List<Candle> candles)
    {
        // If the robot is turned off, exit the event handler
        if (_regime.ValueString == "Off")
        {
            return;
        }

        // If there are not enough candles to build an indicator, we exit
        if (_sma.ParametersDigit[0].Value >= candles.Count ||
            _channel.ParametersDigit[0].Value + 1 >= candles.Count)
        {
            return;
        }

        if (_startTradeTime.Value > _tab.TimeServerCurrent ||
            _endTradeTime.Value < _tab.TimeServerCurrent)
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
        if (_regime.ValueString == "OnlyClosePosition")
        {
            return;
        }

        // Position opening method
        LogicOpenPosition(candles);
    }

    // Opening logic
    private void LogicOpenPosition(List<Candle> candles)
    {
        decimal lastMa = _sma.DataSeries[0].Last;

        decimal lastPrice = candles[candles.Count - 1].Close;

        if (lastMa == 0)
        {
            return;
        }

        //last values of price channel
        decimal maxToCandleSeries = _channel.DataSeries[0].Last;
        decimal minToCandleSeries = _channel.DataSeries[1].Last;

        List<Position> positions = _tab.PositionsOpenAll;

        if (lastPrice >= lastMa && _regime.ValueString != "OnlyShort") //Enter Long
        {
            if (positions != null && positions.Count != 0 &&
                positions[0].Direction == Side.Buy)
            {
                if (positions.Count >= _maximumPosition.ValueInt)
                {
                    return;
                }

                decimal lastIntro = positions[positions.Count - 1].EntryPrice;

                if (lastIntro + lastIntro * (_percentDopBuy.ValueDecimal / 100) < lastPrice)
                {
                    _tab.BuyAtLimit(GetVolume(_tab), lastPrice + (_slippage.ValueInt * _tab.Securiti.PriceStep));
                }
            }
            else if (positions == null || positions.Count == 0)
            {
                _tab.SellAtStopCancel();
                _tab.BuyAtStopCancel();
                _tab.BuyAtStop(GetVolume(_tab), maxToCandleSeries + (_slippage.ValueInt * _tab.Securiti.PriceStep), maxToCandleSeries, StopActivateType.HigherOrEqual);
            }
        }

        if (lastPrice <= lastMa && _regime.ValueString != "OnlyLong") //Enter short
        {
            if (positions != null && positions.Count != 0 &&
                     positions[0].Direction == Side.Sell)
            {
                if (positions.Count >= _maximumPosition.ValueInt)
                {
                    return;
                }

                decimal lastIntro = positions[positions.Count - 1].EntryPrice;

                if (lastIntro - lastIntro * (_percentDopSell.ValueDecimal / 100) > lastPrice)
                {
                    _tab.SellAtLimit(GetVolume(_tab), lastPrice - (_slippage.ValueInt * _tab.Securiti.PriceStep));
                }
            }
            else if (positions == null || positions.Count == 0)
            {
                if (positions != null && positions.Count >= _maximumPosition.ValueInt)
                {
                    return;
                }

                _tab.SellAtStopCancel();
                _tab.BuyAtStopCancel();
                _tab.SellAtStop(GetVolume(_tab), minToCandleSeries - (_slippage.ValueInt * _tab.Securiti.PriceStep), minToCandleSeries, StopActivateType.LowerOrEqual);
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
                    decimal trailingStopPrice = candles[candles.Count - 1].Close - candles[candles.Count - 1].Close * TralingStopLength.ValueDecimal / 100;

                    _tab.CloseAtTrailingStop(positions[i], trailingStopPrice, trailingStopPrice);
                }
                else
                {
                    decimal trailingStopPrice = candles[candles.Count - 1].Close + candles[candles.Count - 1].Close * TralingStopLength.ValueDecimal / 100;

                    _tab.CloseAtTrailingStop(positions[i], trailingStopPrice, trailingStopPrice);
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
                //SendNewLogMessage("Can`t found portfolio " + _tradeAssetInPortfolio.ValueString, Logging.LogMessageType.Error);
                return 0;
            }

            decimal moneyOnPosition = portfolioPrimeAsset * (_volume.ValueDecimal / 100);

            decimal qty = moneyOnPosition / tab.PriceBestAsk / tab.Security.Lot;

            if (tab.StartProgram == StartProgram.IsOsTrader)
            {
                if (tab.Security.UsePriceStepCostToCalculateVolume == true
                   && tab.Security.PriceStep != tab.Security.PriceStepCost
                   && tab.PriceBestAsk != 0
                   && tab.Security.PriceStep != 0
                   && tab.Security.PriceStepCost != 0)
                {// расчёт количества контрактов для фьючерсов и опционов на Мосбирже
                    qty = moneyOnPosition / (tab.PriceBestAsk / tab.Security.PriceStep * tab.Security.PriceStepCost);
                }
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