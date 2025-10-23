/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

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

Trend strategy based on the Macd indicator and trail stop.

Logic Enter:
1. lastMacdDown < 0 and lastMacdUp > lastMacdDown - Buy.
2. lastMacdDown > 0 and lastMacdUp < lastMacdDown - Sell.

Exit: By TralingStop.
 */

[Bot("MacdTrail")] // We create an attribute so that we don't write anything to the BotFactory
public class MacdTrail : BotPanel
{
    private BotTabSimple _tab;

    // Indicator
    private Aindicator _macd;

    // Basic settings
    private StrategyParameterString _regime; 
    private StrategyParameterInt _slippage;

    // GetVolume settings
    private StrategyParameterString _volumeType;
    private StrategyParameterDecimal _volume;
    private StrategyParameterString _tradeAssetInPortfolio;

    // Exit settings
    private StrategyParameterDecimal _trailStop;

    // The last value of the indicator 
    private decimal _lastClose;
    private decimal _lastMacdDown;
    private decimal _lastMacdUp;

    public MacdTrail(string name, StartProgram startProgram) : base(name, startProgram)
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

        // Exit settings
        _trailStop = CreateParameter("Trail Stop Percent", 0.7m, 0.3m, 3, 0.1m);

        // Create indicator MACD
        _macd = IndicatorsFactory.CreateIndicatorByName("MacdLine",name + "MACD", false);
        _macd = (Aindicator)_tab.CreateCandleIndicator(_macd, "MacdArea");
        _macd.Save();

        // Subscribe to the candle finished event
        _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

        Description = OsLocalization.Description.DescriptionLabel60;
    }

    // The name of the robot in OsEngine
    public override string GetNameStrategyType()
    {
        return "MacdTrail";
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

        if (_macd.DataSeries[0].Values == null)
        {
            return;
        }

        _lastClose = candles[candles.Count - 1].Close;
        _lastMacdUp = _macd.DataSeries[0].Values[_macd.DataSeries[0].Values.Count - 1];
        _lastMacdDown = _macd.DataSeries[1].Values[_macd.DataSeries[1].Values.Count - 1];

        List<Position> openPositions = _tab.PositionsOpenAll;

        if (openPositions != null && openPositions.Count != 0)
        {
            for (int i = 0; i < openPositions.Count; i++)
            {
                LogicClosePosition(candles, openPositions[i]);

            }
        }

        if (_regime.ValueString == "OnlyClosePosition")
        {
            return;
        }

        if (openPositions == null || openPositions.Count == 0)
        {
            LogicOpenPosition(candles, openPositions);
        }
    }

    // Logic open position
    private void LogicOpenPosition(List<Candle> candles, List<Position> position)
    {
        if (_lastMacdDown < 0 && _lastMacdUp > _lastMacdDown
                              && _regime.ValueString != "OnlyShort")
        {
            _tab.BuyAtLimit(GetVolume(_tab), _lastClose + _slippage.ValueInt * _tab.Security.PriceStep);
        }

        if (_lastMacdDown > 0 && _lastMacdUp < _lastMacdDown
                              && _regime.ValueString != "OnlyLong")
        {
            _tab.SellAtLimit(GetVolume(_tab), _lastClose - _slippage.ValueInt * _tab.Security.PriceStep);
        }
    }

    // Logic close position
    private void LogicClosePosition(List<Candle> candles, Position position)
    {
        if (position.Direction == Side.Buy)
        {
            _tab.CloseAtTrailingStop(position,
                _lastClose - _lastClose * _trailStop.ValueDecimal / 100,
                _lastClose - _lastClose * _trailStop.ValueDecimal / 100);
        }

        if (position.Direction == Side.Sell)
        {
            _tab.CloseAtTrailingStop(position,
                _lastClose + _lastClose * _trailStop.ValueDecimal / 100,
                _lastClose + _lastClose * _trailStop.ValueDecimal / 100);
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