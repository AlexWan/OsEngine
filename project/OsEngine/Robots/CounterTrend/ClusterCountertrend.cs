/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.GateIo.GateIoSpot.Entities;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Windows;

/* Description
The Countertrend robot
Buy:
we buy if we are under the largest volume for sale in the last 10 candles.
Sell:
sell if we are above the largest purchase volume for the last 10 candles.
Exit logic:
By return signal.
*/

namespace OsEngine.Robots.CounterTrend
{
    [Bot("ClusterCountertrend")] // We create an attribute so that we don't write anything to the BotFactory
    public class ClusterCountertrend : BotPanel
    {
        private BotTabSimple _tabToTrade;
        private BotTabCluster _tabCluster;

        // Basic settings
        public StrategyParameterInt _backLook;
        public StrategyParameterString _regime;

        // GetVolume Settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        public ClusterCountertrend(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tabToTrade = TabsSimple[0];

            TabCreate(BotTabType.Cluster);
            _tabCluster = TabsCluster[0];

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On"});
            _backLook = CreateParameter("Back Look", 1, 1, 10, 1);

            // GetVolume Settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Subscribe to the candle finished event
            _tabToTrade.CandleFinishedEvent += _tabToTrade_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel23;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "ClusterCountertrend";
        }

        // show settings
        public override void ShowIndividualSettingsDialog()
        {
            MessageBox.Show(OsLocalization.Trader.Label111);
        }

        private void _tabToTrade_CandleFinishedEvent(List<Candle> candles)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            if (_tabCluster.VolumeClusters.Count <= _backLook.ValueInt)
            {
                return;
            }

            HorizontalVolumeCluster maxBuyCluster =
                _tabCluster.FindMaxVolumeCluster(candles.Count - _backLook.ValueInt, candles.Count, ClusterType.BuyVolume);

            if(maxBuyCluster == null)
            {
                return;
            }

            if (candles[candles.Count - 1].Close > maxBuyCluster.MaxBuyVolumeLine.Price)
            { // sell and exit long positions

                List<Position> myPosShort = _tabToTrade.PositionOpenShort;

                if (myPosShort.Count == 0)
                {
                    _tabToTrade.SellAtLimit(GetVolume(_tabToTrade), candles[candles.Count - 1].Close);
                }

                List<Position> myPosLong = _tabToTrade.PositionOpenLong;

                if (myPosLong.Count != 0 && myPosLong[0].State == PositionStateType.Open)
                {
                    _tabToTrade.CloseAtMarket(myPosLong[0],myPosLong[0].OpenVolume);
                }
            }

            HorizontalVolumeCluster maxSellCluster =
                _tabCluster.FindMaxVolumeCluster(candles.Count - _backLook.ValueInt, candles.Count, ClusterType.SellVolume);

            if(maxSellCluster == null)
            {
                return;
            }

            if (candles[candles.Count - 1].Close < maxSellCluster.MaxSellVolumeLine.Price)
            { // buy and exit short

                List<Position> myPosLong = _tabToTrade.PositionOpenLong;

                if (myPosLong.Count == 0)
                {
                    _tabToTrade.BuyAtLimit(GetVolume(_tabToTrade), candles[candles.Count - 1].Close);
                }

                List<Position> myPosShort = _tabToTrade.PositionOpenShort;

                if (myPosShort.Count != 0 && myPosShort[0].State == PositionStateType.Open)
                {
                    _tabToTrade.CloseAtMarket(myPosShort[0], myPosShort[0].OpenVolume);
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