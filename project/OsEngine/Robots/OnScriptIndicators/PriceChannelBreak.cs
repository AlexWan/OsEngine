/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;

/* Description
trading robot for osengine

When the candle is closed outside the PriceChannel channel.

We enter the position, the stop loss is at the extremum of the last candle from the entry candle. 

Take profit by the channel size from the close of the candle at which the entry occurred.
 */

[Bot("PriceChannelBreak")] // We create an attribute so that we don't write anything to the BotFactory
public class PriceChannelBreak : BotPanel
{
    private BotTabSimple _tab;

    // Basic settings
    private StrategyParameterString _regime;
    private StrategyParameterInt _slippage;

    // GetVolume settings
    private StrategyParameterString _volumeType;
    private StrategyParameterDecimal _volume;
    private StrategyParameterString _tradeAssetInPortfolio;

    // Indicator setting
    private StrategyParameterInt _indLength;
    
    // Indicator
    private Aindicator _pc;

    // The last value of the indicator
    private decimal _lastPrice;
    private decimal _lastPcUp;
    private decimal _lastPcDown;

    public PriceChannelBreak(string name, StartProgram startProgram) : base(name, startProgram)
    {
        TabCreate(BotTabType.Simple);
        _tab = TabsSimple[0];

        // Basic settings
        _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
        _slippage = CreateParameter("Slippage", 0, 0, 20, 1);

        // GetVolume settings
        _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
        _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
        _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

        // Indicator setting
        _indLength = CreateParameter("Ind Length", 10, 10, 80, 3);

        // Create indicator PriceChannel
        _pc = IndicatorsFactory.CreateIndicatorByName("PriceChannel", name + "PriceChannel", false);
        _pc = (Aindicator)_tab.CreateCandleIndicator(_pc, "Prime");
        _pc.ParametersDigit[0].Value = _indLength.ValueInt;
        _pc.ParametersDigit[1].Value = _indLength.ValueInt;
        _pc.Save();

        // Subscribe to the candle finished event
        _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

        // Subscribe to the position open event
        _tab.PositionOpeningSuccesEvent += Strateg_PositionOpen;

        // Subscribe to the indicator update event
        ParametrsChangeByUser += Event_ParametrsChangeByUser;

        Description = OsLocalization.Description.DescriptionLabel63;
    }

    void Event_ParametrsChangeByUser()
    {
        if (_indLength.ValueInt != _pc.ParametersDigit[0].Value ||
            _indLength.ValueInt != _pc.ParametersDigit[1].Value)
        {
            _pc.ParametersDigit[0].Value = _indLength.ValueInt;
            _pc.ParametersDigit[1].Value = _indLength.ValueInt;

            _pc.Reload();
        }
    }

    // The name of the robot in OsEngine
    public override string GetNameStrategyType()
    {
        return "PriceChannelBreak";
    }

    // Show settings GUI
    public override void ShowIndividualSettingsDialog()
    {

    }

    // Logic
    private void Strateg_CandleFinishedEvent(List<Candle> candles)
    {
        if (_regime.ValueString == "Off")
        {
            return;
        }

        if (_pc.DataSeries[0].Values == null || _pc.DataSeries[1].Values == null)
        {
            return;
        }

        if (_pc.DataSeries[0].Values.Count < _pc.ParametersDigit[0].Value + 2 || _pc.DataSeries[1].Values.Count < _pc.ParametersDigit[1].Value + 2)
        {
            return;
        }

        _lastPrice = candles[candles.Count - 1].Close;
        _lastPcUp = _pc.DataSeries[0].Values[_pc.DataSeries[0].Values.Count - 2];
        _lastPcDown = _pc.DataSeries[1].Values[_pc.DataSeries[1].Values.Count - 2];

        List<Position> openPositions = _tab.PositionsOpenAll;

        if (_regime.ValueString == "OnlyClosePosition")
        {
            return;
        }

        if (openPositions == null || openPositions.Count == 0)
        {
            LogicOpenPosition(candles, openPositions);
        }
    }

    // Open position logic
    private void LogicOpenPosition(List<Candle> candles, List<Position> position)
    {
        List<Position> openPositions = _tab.PositionsOpenAll;

        if (openPositions == null || openPositions.Count == 0)
        {
            // long
            if (_regime.ValueString != "OnlyShort")
            {
                if (_lastPrice > _lastPcUp)
                {
                    _tab.BuyAtLimit(GetVolume(_tab), _lastPrice + _slippage.ValueInt * _tab.Security.PriceStep);
                }
            }

            // Short
            if (_regime.ValueString != "OnlyLong")
            {
                if (_lastPrice < _lastPcDown)
                {
                    _tab.SellAtLimit(GetVolume(_tab), _lastPrice - _slippage.ValueInt * _tab.Security.PriceStep);
                }
            }
        }
    }

    // Set stop orders and profit orders
    private void Strateg_PositionOpen(Position position)
    {
        List<Position> openPositions = _tab.PositionsOpenAll;

        for (int i = 0; openPositions != null && i < openPositions.Count; i++)
        {
            if (openPositions[i].Direction == Side.Buy)
            {
                decimal lowCandle = _tab.CandlesAll[_tab.CandlesAll.Count - 2].Low;

                _tab.CloseAtStop(openPositions[i], lowCandle, lowCandle - _slippage.ValueInt * _tab.Security.PriceStep);

                _tab.CloseAtProfit(
                    openPositions[i], _lastPrice + (_lastPcUp - _lastPcDown),
                    (_lastPrice + (_lastPcUp - _lastPcDown)) - _slippage.ValueInt * _tab.Security.PriceStep);
            }
            else
            {
                decimal highCandle = _tab.CandlesAll[_tab.CandlesAll.Count - 2].High;

                _tab.CloseAtStop(openPositions[i], highCandle, highCandle + _slippage.ValueInt * _tab.Security.PriceStep);

                _tab.CloseAtProfit(
                    openPositions[i], _lastPrice - (_lastPcUp - _lastPcDown),
                    (_lastPrice - (_lastPcUp - _lastPcDown)) + _slippage.ValueInt * _tab.Security.PriceStep);
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