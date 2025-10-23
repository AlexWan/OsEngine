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

/*Discription
Trading robot for osengine

Trend robot on the Two Time Frames Bot.

Buy:
1.The current price must be above the PriceChannel Up level.  
2. Additionally, the current price on the higher timeframe must be above the moving average.  

Exit: If the current price falls below the PriceChannel Down level.
*/

namespace OsEngine.Robots.Trend
{
    [Bot("TwoTimeFramesBot")] //We create an attribute so that we don't write anything in the Boot factory
    public class TwoTimeFramesBot : BotPanel
    {
        private BotTabSimple _tabToTrade;
        private BotTabSimple _tabBigTf;
        
        // Basic setting
        public StrategyParameterString Regime;

        // GetVolume Settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator
        private Aindicator _pc;
        private Aindicator _sma;

        // Indicator settings
        public StrategyParameterInt PcLength;
        public StrategyParameterInt SmaLength;

        public TwoTimeFramesBot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            TabCreate(BotTabType.Simple);
            _tabToTrade = TabsSimple[0];
            _tabBigTf = TabsSimple[1];

            // Basic settings
            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });

            // GetVolume Settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator settings
            PcLength = CreateParameter("PC length", 20, 5, 50, 1);
            SmaLength = CreateParameter("Sma length", 30, 0, 50, 1);

            // Create indicator PriceChannel
            _pc = IndicatorsFactory.CreateIndicatorByName("PriceChannel", name + "PriceChannel", false);
            _pc = (Aindicator)_tabToTrade.CreateCandleIndicator(_pc, "Prime");
            _pc.ParametersDigit[0].Value = PcLength.ValueInt;
            _pc.ParametersDigit[1].Value = PcLength.ValueInt;

            // Create indicator Sma
            _sma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "sma", false);
            _sma = (Aindicator)_tabBigTf.CreateCandleIndicator(_sma, "Prime");
            _sma.ParametersDigit[0].Value = SmaLength.ValueInt;

            // Subscribe to the candle completion event
            _tabToTrade.CandleFinishedEvent += _tabToTrade_CandleFinishedEvent;

            // Subscribe to the indicator update event
            ParametrsChangeByUser += TwoTimeFramesBot_ParametrsChangeByUser;

            Description = OsLocalization.Description.DescriptionLabel120;
        }

        private void TwoTimeFramesBot_ParametrsChangeByUser()
        {
            if(_pc.ParametersDigit[0].Value != PcLength.ValueInt)
            {
                _pc.ParametersDigit[0].Value = PcLength.ValueInt;
                _pc.ParametersDigit[1].Value = PcLength.ValueInt;
                _pc.Reload();
                _pc.Save();
            }

            if(_sma.ParametersDigit[0].Value != SmaLength.ValueInt)
            {
                _sma.ParametersDigit[0].Value = SmaLength.ValueInt;
                _sma.Reload();
                _sma.Save();
            }
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "TwoTimeFramesBot";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {
            
        }

        // logic
        private void _tabToTrade_CandleFinishedEvent(List<Candle> candles)
        {
            if (Regime.ValueString == "Off")
            {
                return;
            }

            if(_tabBigTf.CandlesAll == null
                || _tabBigTf.CandlesAll.Count < 5 
                || candles.Count < 5)
            {
                return;
            }

            decimal lastPriceOnTradeTab = candles[candles.Count - 1].Close;
            decimal lastPcUp = _pc.DataSeries[0].Values[_pc.DataSeries[0].Values.Count - 2];
            decimal lastPcDown = _pc.DataSeries[1].Values[_pc.DataSeries[1].Values.Count - 2];

            decimal lastPriceOnBigTfTab = _tabBigTf.CandlesAll[_tabBigTf.CandlesAll.Count - 1].Close;
            decimal lastSmaOnBigTfTab = _sma.DataSeries[0].Last;

            if(lastPriceOnTradeTab == 0 
                || lastPcUp == 0 
                || lastPriceOnBigTfTab == 0 
                || lastSmaOnBigTfTab == 0)
            { // data is note ready
                return;
            }

            List<Position> openPositions = _tabToTrade.PositionsOpenAll;

            if (openPositions == null 
                || openPositions.Count == 0)
            { // Open logic
                if(lastPriceOnTradeTab > lastPcUp 
                    && lastPriceOnBigTfTab > lastSmaOnBigTfTab)
                {
                    _tabToTrade.BuyAtMarket(GetVolume(_tabToTrade));
                }
            }
            else
            {
                Position openPos = openPositions[0];

                if(openPos.State != PositionStateType.Open)
                {
                    return;
                }

                if(lastPriceOnTradeTab < lastPcDown)
                {
                    _tabToTrade.CloseAtMarket(openPos, openPos.OpenVolume);
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
                    SendNewLogMessage("Can`t found portfolio " + _tradeAssetInPortfolio.ValueString, Logging.LogMessageType.Error);
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
}