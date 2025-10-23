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

Bollinger Bands trading bargaining robot with pull-up Trailing-Stop through Bollinger Bands.

Buy: The price is more than BollingerUpLine.

Sell: Price below BollingerDownLine.

Exit: Trailing-Stop through Bollinger Bands.
 */

[Bot("BollingerTrailing")] // We create an attribute so that we don't write anything to the BotFactory
public class BollingerTrailing : BotPanel
{
    private BotTabSimple _tab;

    // Indicator
    private Aindicator _bollinger;

    // Basic settings
    private StrategyParameterString _regime;
    private StrategyParameterInt _slippage;

    // GetVolume settings
    private StrategyParameterString _volumeType;
    private StrategyParameterDecimal _volume;
    private StrategyParameterString _tradeAssetInPortfolio;

    // Indicator settings
    private StrategyParameterInt _indLength;
    private StrategyParameterDecimal _bollingerDeviation;

    // The last value of the indicator
    private decimal _lastPrice;
    private decimal _lastBbUp;
    private decimal _lastBbDown;

    public BollingerTrailing(string name, StartProgram startProgram) : base(name, startProgram)
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
        _indLength = CreateParameter("IndLength", 10, 10, 80, 3);
        _bollingerDeviation = CreateParameter("Bollinger Deviation", 2, 0.5m, 4, 0.1m);

        // Create indicator Bollinger
        _bollinger = IndicatorsFactory.CreateIndicatorByName("Bollinger",name + "Bollinger", false);
        _bollinger = (Aindicator)_tab.CreateCandleIndicator(_bollinger, "Prime");
        _bollinger.ParametersDigit[0].Value = _indLength.ValueInt;
        _bollinger.ParametersDigit[1].Value = _bollingerDeviation.ValueDecimal;
        _bollinger.Save();

        // Subscribe to the candle finished event
        _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

        // Logic close position
        _tab.PositionOpeningSuccesEvent += ReloadTrailingPosition;

        // Subscribe to the indicator update event
        ParametrsChangeByUser += Event_ParametrsChangeByUser;

        Description = OsLocalization.Description.DescriptionLabel56;
    }

    void Event_ParametrsChangeByUser()
    {
        if (_indLength.ValueInt != _bollinger.ParametersDigit[0].Value ||
            _bollingerDeviation.ValueDecimal != _bollinger.ParametersDigit[1].Value)
        {
            _bollinger.ParametersDigit[0].Value = _indLength.ValueInt;
            _bollinger.ParametersDigit[1].Value = _bollingerDeviation.ValueDecimal;
            _bollinger.Reload();
        }
    }

    // The name of the robot in OsEngine
    public override string GetNameStrategyType()
    {
        return "BollingerTrailing";
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

        if (_bollinger.DataSeries[0].Values == null 
            || _bollinger.DataSeries[1].Values == null
            || _bollinger.DataSeries[0].Values.Count < 10
            || _bollinger.DataSeries[1].Values.Count < 10)
        {
            return;
        }

        _lastPrice = candles[candles.Count - 1].Close;
        _lastBbUp = _bollinger.DataSeries[0].Values[_bollinger.DataSeries[0].Values.Count-2];
        _lastBbDown = _bollinger.DataSeries[1].Values[_bollinger.DataSeries[1].Values.Count - 2];

        if (_bollinger.DataSeries[0].Values.Count < ((IndicatorParameterInt)_bollinger.Parameters[0]).ValueInt + 2)
        {
            return;
        }

        List<Position> openPositions = _tab.PositionsOpenAll;

        if (openPositions != null && openPositions.Count != 0)
        {
            LogicClosePosition(candles, openPositions);
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

    // Logic close pos
    private void LogicClosePosition(List<Candle> candles, List<Position> position)
    {
        List<Position> openPositions = _tab.PositionsOpenAll;

        for (int i = 0; openPositions != null && i < openPositions.Count; i++)
        {
            ReloadTrailingPosition(openPositions[i]);
        }
    }

    // Close one pos
    private void ReloadTrailingPosition(Position position)
    {
        List<Position> openPositions = _tab.PositionsOpenAll;

        for (int i = 0; openPositions != null && i < openPositions.Count; i++)
        {
            if (openPositions[i].Direction == Side.Buy)
            {
                decimal valueDown = _bollinger.DataSeries[1].Values[_bollinger.DataSeries[1].Values.Count - 1];

                _tab.CloseAtTrailingStop(
                    openPositions[i], valueDown,
                    valueDown - _slippage.ValueInt * _tab.Security.PriceStep);
            }
            else
            {
                decimal valueUp = _bollinger.DataSeries[0].Values[_bollinger.DataSeries[0].Values.Count - 1];
                _tab.CloseAtTrailingStop(
                    openPositions[i], valueUp,
                    valueUp + _slippage.ValueInt * _tab.Security.PriceStep);
            }
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
                if (_lastPrice > _lastBbUp)
                {
                    _tab.BuyAtLimit(GetVolume(_tab), _lastPrice + _slippage.ValueInt * _tab.Security.PriceStep);
                }
            }

            // Short
            if (_regime.ValueString != "OnlyLong")
            {
                if (_lastPrice < _lastBbDown)
                {
                    _tab.SellAtLimit(GetVolume(_tab), _lastPrice - _slippage.ValueInt * _tab.Security.PriceStep);
                }
            }

            return;
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