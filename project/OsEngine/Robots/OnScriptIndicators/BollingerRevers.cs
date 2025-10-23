/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using System;
using OsEngine.Market.Servers;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Language;

/* Description
trading robot for osengine

Trend Strategy Based on Breaking Bollinger Lines

Buy: The price is more than BollingerUpLine.

Sell: Price below BollingerDownLine.

Exit: At the intersection of Sma with the price.
 */

[Bot("BollingerRevers")] // We create an attribute so that we don't write anything to the BotFactory
public class BollingerRevers : BotPanel
{
    private BotTabSimple _tab;

    // Indicators
    private Aindicator _bol;

    // Basic settings
    private StrategyParameterString _regime;
    private StrategyParameterInt _slippage;

    // Indicator settings
    private StrategyParameterDecimal _bollingerDeviation;
    private StrategyParameterInt _bollingerLength;

    // GetVolume settings
    private StrategyParameterString _volumeType;
    private StrategyParameterDecimal _volume;
    private StrategyParameterString _tradeAssetInPortfolio;

    // The last value of the indicator
    private decimal _lastPrice;
    private decimal _bolLastUp;
    private decimal _bolLastDown;

    public BollingerRevers(string name, StartProgram startProgram) : base(name, startProgram)
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

        // Indicator settings
        _bollingerLength = CreateParameter("Bollinger Length", 12, 4, 100, 2);
        _bollingerDeviation = CreateParameter("Bollinger Deviation", 2, 0.5m, 4, 0.1m);

        // Create indicator Bollinger
        _bol = IndicatorsFactory.CreateIndicatorByName("Bollinger", name + "Bollinger", false);
        _bol = (Aindicator)_tab.CreateCandleIndicator(_bol, "Prime");
        _bol.ParametersDigit[0].Value = _bollingerLength.ValueInt;
        _bol.ParametersDigit[1].Value = _bollingerDeviation.ValueDecimal;
        _bol.Save();

        // Subscribe to the candle finished event
        _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

        // Subscribe to the indicator update event
        ParametrsChangeByUser += Event_ParametrsChangeByUser;

        Description = OsLocalization.Description.DescriptionLabel55;
    }

    void Event_ParametrsChangeByUser()
    {
        _bol.ParametersDigit[0].Value = _bollingerLength.ValueInt;
        _bol.ParametersDigit[1].Value = _bollingerDeviation.ValueDecimal;
        _bol.Reload();
    }

    // The name of the robot in OsEngine
    public override string GetNameStrategyType()
    {
        return "BollingerRevers";
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
        if (_lastPrice > _bolLastUp
            && _regime.ValueString != "OnlyShort")
        {
            _tab.BuyAtLimit(GetVolume(_tab), _lastPrice + _slippage.ValueInt * _tab.Security.PriceStep);
        }

        if (_lastPrice < _bolLastDown
            && _regime.ValueString != "OnlyLong")
        {
            _tab.SellAtLimit(GetVolume(_tab), _lastPrice - _slippage.ValueInt * _tab.Security.PriceStep);
        }
    }

    // Logic close position
    private void LogicClosePosition(List<Candle> candles, Position position)
    {
        if (position.State == PositionStateType.Closing ||
            position.CloseActive == true ||
            (position.CloseOrders != null && position.CloseOrders.Count > 0))
        {
            return;
        }

        if (position.Direction == Side.Buy)
        {
            if (_lastPrice < _bolLastDown)
            {
                _tab.CloseAtLimit(position,_lastPrice - _slippage.ValueInt * _tab.Security.PriceStep,position.OpenVolume);

                if (_regime.ValueString != "OnlyLong"
                    && _regime.ValueString != "OnlyClosePosition")
                {
                    _tab.SellAtLimit(GetVolume(_tab), _lastPrice - _slippage.ValueInt * _tab.Security.PriceStep);
                }
            }
        }

        if (position.Direction == Side.Sell)
        {
            if (_lastPrice > _bolLastUp)
            {
                _tab.CloseAtLimit(position,_lastPrice + _slippage.ValueInt * _tab.Security.PriceStep,position.OpenVolume);

                if (_regime.ValueString != "OnlyShort"
                    && _regime.ValueString != "OnlyClosePosition")
                {
                    _tab.BuyAtLimit(GetVolume(_tab), _lastPrice + _slippage.ValueInt * _tab.Security.PriceStep);
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
                SendNewLogMessage("Can`t found portfolio " + _tradeAssetInPortfolio.ValueString, LogMessageType.Error);
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