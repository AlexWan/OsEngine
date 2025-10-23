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

Pair trading based on the RSI indicator.

Logic: if RsiOne > RsiTwo + RsiSpread - then we are Sell on the first instrument and Buy on the second one. 

Logic: if RsiTwo > RsiOne + RsiSpread - then we are Buy on the first instrument and Sell on the second one. 

Exit: on the return signal.
 */

[Bot("PairRsiTrade")] // We create an attribute so that we don't write anything to the BotFactory
public class PairRsiTrade : BotPanel
{
    private BotTabSimple _tab1;
    private BotTabSimple _tab2;

    // Basic settings
    private StrategyParameterString _regime;
    private StrategyParameterDecimal _rsiSpread;

    // GetVolume settings
    private StrategyParameterDecimal _volume1;
    private StrategyParameterDecimal _volume2;
    private StrategyParameterString _volumeType1;
    private StrategyParameterString _tradeAssetInPortfolio1;
    private StrategyParameterString _volumeType2;
    private StrategyParameterString _tradeAssetInPortfolio2;

    // Indicator settings
    private StrategyParameterInt _rsiOnePeriod;
    private StrategyParameterInt _rsiTwoPeriod;

    // List candles
    private List<Candle> _candles1;
    private List<Candle> _candles2;

    // Indicator
    private Aindicator _rsi1;
    private Aindicator _rsi2;

    public PairRsiTrade(string name, StartProgram startProgram) : base(name, startProgram)
    {
        TabCreate(BotTabType.Simple);
        _tab1 = TabsSimple[0];
        _tab1.CandleFinishedEvent += _tab1_CandleFinishedEvent;

        TabCreate(BotTabType.Simple);
        _tab2 = TabsSimple[1];
        _tab2.CandleFinishedEvent += _tab2_CandleFinishedEvent;

        // Basic settings
        _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });
        _rsiSpread = CreateParameter("Spread to Rsi", 10, 5.0m, 50, 2,"Indicators");

        // GetVolume settings
        _volume1 = CreateParameter("Volume 1", 1, 1.0m, 50, 1);
        _volume2 = CreateParameter("Volume 2", 1, 1.0m, 50, 1);
        _volumeType1 = CreateParameter("Volume type 1", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
        _volumeType2 = CreateParameter("Volume type 2", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
        _tradeAssetInPortfolio1 = CreateParameter("Asset in portfolio 1", "Prime");
        _tradeAssetInPortfolio2 = CreateParameter("Asset in portfolio 2", "Prime");

        // Indicator settings
        _rsiOnePeriod = CreateParameter("Rsi One period", 14, 5, 50, 2, "Indicators");
        _rsiTwoPeriod = CreateParameter("Rsi Two period", 14, 5, 50, 2, "Indicators");

        // Create indicartor Rsi one
        _rsi1 = IndicatorsFactory.CreateIndicatorByName("RSI",name + "RSI1", false);
        _rsi1.ParametersDigit[0].Value = _rsiOnePeriod.ValueInt;
        _rsi1 = (Aindicator)_tab1.CreateCandleIndicator(_rsi1, "Rsi1_Area");
        _rsi1.Save();

        // Create indicator Rsi two
        _rsi2 = IndicatorsFactory.CreateIndicatorByName("RSI",name + "RSI2", false);
        _rsi2.ParametersDigit[0].Value = _rsiTwoPeriod.ValueInt;
        _rsi2 = (Aindicator)_tab2.CreateCandleIndicator(_rsi2, "Rsi2_Area");
        _rsi2.Save();

        // Subscribe to the indicator update event
        ParametrsChangeByUser += Event_ParametrsChangeByUser;

        ParamGuiSettings.Height = 300;
        ParamGuiSettings.Width = 500;
        ParamGuiSettings.Title = "Pair Rsi Bot Settings";

        Description = OsLocalization.Description.DescriptionLabel62;
    }

    void Event_ParametrsChangeByUser()
    {
        if (_rsi1.ParametersDigit[0].Value != _rsiOnePeriod.ValueInt)
        {
            _rsi1.ParametersDigit[0].Value = _rsiOnePeriod.ValueInt;
            _rsi1.Reload();
        }
        if (_rsi2.ParametersDigit[0].Value != _rsiTwoPeriod.ValueInt)
        {
            _rsi2.ParametersDigit[0].Value = _rsiTwoPeriod.ValueInt;
            _rsi2.Reload();
        }
    }

    // The name of the robot in OsEngine
    public override string GetNameStrategyType()
    {
        return "PairRsiTrade";
    }
    
    // Show settings GUI
    public override void ShowIndividualSettingsDialog()
    {

    }

    // Logic tab1
    void _tab1_CandleFinishedEvent(List<Candle> candles)
    {
        _candles1 = candles;

        if (_candles2 == null ||
            _candles2.Count == 0 ||
            _candles2[_candles2.Count - 1].TimeStart != _candles1[_candles1.Count - 1].TimeStart)
        {
            return;
        }

        CheckExit();
        Trade();
    }

    // Logic tab2
    void _tab2_CandleFinishedEvent(List<Candle> candles)
    {
        _candles2 = candles;

        if (_candles1 == null ||
            _candles1.Count == 0 ||
            _candles2[_candles2.Count - 1].TimeStart != _candles1[_candles1.Count - 1].TimeStart)
        {
            return;
        }

        CheckExit();
        Trade();
    }

    // Trade logic
    private void Trade()
    {
        if (_candles1.Count < 20 && _candles2.Count < 20)
        {
            return; ;
        }

        if (_regime.ValueString == "Off")
        {
            return;
        }

        List<Position> pos1 = _tab1.PositionsOpenAll;
        List<Position> pos2 = _tab2.PositionsOpenAll;

        if (pos1 != null && pos1.Count != 0 || pos2 != null && pos2.Count != 0)
        {
            return;
        }

        if (_rsi1.DataSeries[0].Values == null || _rsi2.DataSeries[0].Values == null)
        {
            return;
        }

        if (_rsi1.DataSeries[0].Values.Count < _rsi1.ParametersDigit[0].Value + 3 
            || _rsi2.DataSeries[0].Values.Count < _rsi2.ParametersDigit[0].Value + 3)
        {
            return;
        }

        decimal lastRsi1 = _rsi1.DataSeries[0].Values[_rsi1.DataSeries[0].Values.Count - 1];
        decimal lastRsi2 = _rsi2.DataSeries[0].Values[_rsi2.DataSeries[0].Values.Count - 1];

        if (lastRsi1 > lastRsi2 + _rsiSpread.ValueDecimal)
        {
            _tab1.SellAtMarket(GetVolume(_tab1, _volume1.ValueDecimal, _volumeType1.ValueString, _tradeAssetInPortfolio1.ValueString));
            _tab2.BuyAtMarket(GetVolume(_tab2, _volume2.ValueDecimal, _volumeType2.ValueString, _tradeAssetInPortfolio2.ValueString));
        }

        if (lastRsi2 > lastRsi1 + _rsiSpread.ValueDecimal)
        {
            _tab1.BuyAtMarket(GetVolume(_tab1, _volume1.ValueDecimal, _volumeType1.ValueString, _tradeAssetInPortfolio1.ValueString));
            _tab2.SellAtMarket(GetVolume(_tab2, _volume2.ValueDecimal, _volumeType2.ValueString, _tradeAssetInPortfolio2.ValueString));
        }
    }

    // Logic exit position
    private void CheckExit()
    {
        List<Position> positions1 = _tab1.PositionsOpenAll;
        List<Position> positions2 = _tab2.PositionsOpenAll;

        decimal lastRsi1 = _rsi1.DataSeries[0].Values[_rsi1.DataSeries[0].Values.Count - 1];
        decimal lastRsi2 = _rsi2.DataSeries[0].Values[_rsi2.DataSeries[0].Values.Count - 1];

        if (positions1.Count == 0)
        {
            return;
        }

        if (positions1[0].Direction == Side.Buy &&
            lastRsi1 <= lastRsi2)
        {
            CloseAllPositions();
        }

        if (positions1[0].Direction == Side.Sell &&
            lastRsi2 <= lastRsi1)
        {
            CloseAllPositions();
        }
    }

    // Logic close all position
    private void CloseAllPositions()
    {
        List<Position> positions1 = _tab1.PositionsOpenAll;
        List<Position> positions2 = _tab2.PositionsOpenAll;

        if (positions1.Count != 0 && positions1[0].State == PositionStateType.Open)
        {
            _tab1.CloseAtMarket(positions1[0], positions1[0].OpenVolume);
        }

        if (positions2.Count != 0 && positions2[0].State == PositionStateType.Open)
        {
            _tab2.CloseAtMarket(positions2[0], positions2[0].OpenVolume);
        }
    }

    // Method for calculating the volume of entry into a position
    private decimal GetVolume(BotTabSimple tab, decimal Volume, string VolumeType, string TradeAssetInPortfolio)
    {
        decimal volume = 0;

        if (VolumeType == "Contracts")
        {
            volume = Volume;
        }
        else if (VolumeType == "Contract currency")
        {
            decimal contractPrice = tab.PriceBestAsk;
            volume = Volume / contractPrice;

            if (StartProgram == StartProgram.IsOsTrader)
            {
                IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);

                if (serverPermission != null &&
                    serverPermission.IsUseLotToCalculateProfit &&
                    tab.Security.Lot != 0 &&
                    tab.Security.Lot > 1)
                {
                    volume = Volume / (contractPrice * tab.Security.Lot);
                }

                volume = Math.Round(volume, tab.Security.DecimalsVolume);
            }
            else // Tester or Optimizer
            {
                volume = Math.Round(volume, 6);
            }
        }
        else if (VolumeType == "Deposit percent")
        {
            Portfolio myPortfolio = tab.Portfolio;

            if (myPortfolio == null)
            {
                return 0;
            }

            decimal portfolioPrimeAsset = 0;

            if (TradeAssetInPortfolio == "Prime")
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
                    if (positionOnBoard[i].SecurityNameCode == TradeAssetInPortfolio)
                    {
                        portfolioPrimeAsset = positionOnBoard[i].ValueCurrent;
                        break;
                    }
                }
            }

            if (portfolioPrimeAsset == 0)
            {
                SendNewLogMessage("Can`t found portfolio " + TradeAssetInPortfolio, OsEngine.Logging.LogMessageType.Error);
                return 0;
            }

            decimal moneyOnPosition = portfolioPrimeAsset * (Volume / 100);

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