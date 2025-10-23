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

Trading robot on the index.

The intersection of MA on the index from the bottom up long, with the reverse intersection of shorts
 */

[Bot("OneLegArbitrage")] // We create an attribute so that we don't write anything to the BotFactory
public class OneLegArbitrage : BotPanel
{
    private BotTabIndex _tab1;
    private BotTabSimple _tab2;

    // Basic settings
    private StrategyParameterString _regime;
    private StrategyParameterInt _slippage;

    // GetVolume settings
    private StrategyParameterString _volumeType;
    private StrategyParameterDecimal _volume;
    private StrategyParameterString _tradeAssetInPortfolio;

    // Indicator
    private Aindicator _sma;

    // The last value of the indicator and price
    private decimal _lastPrice;
    private decimal _lastIndex;
    private decimal _lastMa;

    public OneLegArbitrage(string name, StartProgram startProgram) : base(name, startProgram)
    {
        TabCreate(BotTabType.Index);
        _tab1 = TabsIndex[0];
        TabCreate(BotTabType.Simple);
        _tab2 = TabsSimple[0];

        // Basic settings
        _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
        _slippage = CreateParameter("Slippage", 0, 0, 20, 1);

        // GetVolume settings 
        _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
        _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
        _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

        // Create indicator Sma
        _sma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "MovingAverage", false);
        _sma = (Aindicator)_tab1.CreateCandleIndicator(_sma, "Prime");
        _sma.Save();

        // Subscribe to the candle finished event
        _tab2.CandleFinishedEvent += Strateg_CandleFinishedEvent;

        Description = OsLocalization.Description.DescriptionLabel61;
    }

    // The name of the robot in OsEngine
    public override string GetNameStrategyType()
    {
        return "OneLegArbitrage";
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

        if (_sma.DataSeries[0].Values == null ||
            _sma.DataSeries[0].Values.Count < _sma.ParametersDigit[0].Value + 2)
        {
            return;
        }

        if (_tab1.Candles == null ||
            _tab2.CandlesFinishedOnly == null)
        {
            return;
        }

        _lastIndex = _tab1.Candles[_tab1.Candles.Count - 1].Close;
        _lastMa = _sma.DataSeries[0].Values[_sma.DataSeries[0].Values.Count - 1];
        _lastPrice = candles[candles.Count - 1].Close;

        List<Position> openPositions = _tab2.PositionsOpenAll;

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

    // Open position logic
    private void LogicOpenPosition(List<Candle> candles, List<Position> position)
    {
        List<Position> openPositions = _tab2.PositionsOpenAll;

        if (openPositions == null || openPositions.Count == 0)
        {
            if (_regime.ValueString != "OnlyShort")
            {
                if (_lastIndex > _lastMa)
                {
                    _tab2.BuyAtLimit(GetVolume(_tab2), _lastPrice + _tab2.Security.PriceStep * _slippage.ValueInt);
                }
            }

            if (_regime.ValueString != "OnlyLong")
            {
                if (_lastIndex < _lastMa)
                {
                    _tab2.SellAtLimit(GetVolume(_tab2), _lastPrice - _tab2.Security.PriceStep * _slippage.ValueInt);
                }
            }

            return;
        }
    }

    // Close position logic
    private void LogicClosePosition(List<Candle> candles, List<Position> position)
    {
        List<Position> openPositions = _tab2.PositionsOpenAll;

        for (int i = 0; openPositions != null && i < openPositions.Count; i++)
        {
            if (openPositions[i].Direction == Side.Buy)
            {
                if (_lastIndex < _lastMa)
                {
                    if (_regime.ValueString == "OnlyClosePosition")
                    {
                        _tab2.CloseAtMarket(openPositions[i], openPositions[i].OpenVolume);
                    }
                    else
                    {
                        _tab2.CloseAtMarket(openPositions[i], openPositions[i].OpenVolume);

                        if (openPositions.Count < 2)
                        {
                            _tab2.SellAtLimit(GetVolume(_tab2), _lastPrice - _tab2.Security.PriceStep * _slippage.ValueInt);
                        }
                    }
                }
            }
            else
            {
                if (_lastIndex > _lastMa)
                {
                    if (_regime.ValueString == "OnlyClosePosition")
                    {
                        _tab2.CloseAtMarket(openPositions[i], openPositions[i].OpenVolume);
                    }
                    else
                    {
                        _tab2.CloseAtMarket(openPositions[i], openPositions[i].OpenVolume);

                        if(openPositions.Count < 2)
                        {
                            _tab2.BuyAtLimit(GetVolume(_tab2), _lastPrice + _tab2.Security.PriceStep * _slippage.ValueInt);
                        } 
                    }
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