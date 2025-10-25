/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System.Collections.Generic;
using System.Linq;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Language;

/* Description
trading robot for osengine

The trend robot on ClusterTrend

Buy: When a pushing bullish cluster occurs.

Exit: When a pushing bearish cluster occurs.
 */

namespace OsEngine.Robots
{
    [Bot("ClusterTrend")] // We create an attribute so that we don't write anything to the BotFactory
    public class ClusterTrend : BotPanel
    {
        // Settings
        private StrategyParameterDecimal _minSignalVolume;
        private StrategyParameterInt _rollBackClusters;

        // GetVolume Settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Tabs
        private BotTabCluster _tabCluster;
        private BotTabSimple _tabToTrade;

        public ClusterTrend(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Settings
            _minSignalVolume = CreateParameter("Min signal volume", 50m, 10, 1000, 10);
            _rollBackClusters = CreateParameter("Roll back clusters", 10, 10, 30, 5);

            // GetVolume Settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Creating Tabs
            TabCreate(BotTabType.Cluster);
            _tabCluster = TabsCluster.Last();

            TabCreate(BotTabType.Simple);
            _tabToTrade = TabsSimple.Last();

            // Subscribe to event changed cluster with maximum total purchase amount
            _tabCluster.MaxBuyClusterChangeEvent += TabClusterMaxBuyClusterChangeEvent;

            // Subscribe to the candle finished event
            _tabToTrade.CandleFinishedEvent += TabToTradeCandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel175;
        }

        private void TabClusterMaxBuyClusterChangeEvent(HorizontalVolumeCluster volumeCluster)
        {
            if (_tabToTrade.PositionsOpenAll.Count != 0)
            {
                _tabToTrade.CloseAllAtMarket("Max buy cluster changed");
            }
        }

        private void TabToTradeCandleFinishedEvent(List<Candle> candles)
        {
            if (candles.Count < 2)
            {
                return;
            }

            Candle lastCandle = candles.Last();

            // From the list of all clusters we get the one that is the profile of this candle.
            HorizontalVolumeCluster lastCluster = _tabCluster.VolumeClusters.FindLast(cluster => cluster.Time == lastCandle.TimeStart);

            // If there are no open positions.
            if (_tabToTrade.PositionOpenLong.Count == 0)
            {
                // If there are no open positions, the bar is pushing, and the price of the instrument
                // is higher than the price of the section with the maximum total volume for the last n candles, open a long position.
                if (IsPushingClaster(lastCluster, lastCandle)
                    && PriceUpperMaxSumVolumeCluster(candles.Count - 1, lastCandle))
                {
                    _tabToTrade.BuyAtMarket(GetVolume(_tabToTrade));
                }
            }
            else
            {
                // If there is a position and the current bar is braking, close the position.
                if (IsBrakingClaster(lastCluster, lastCandle))
                {
                    _tabToTrade.CloseAllAtMarket();
                }
            }
        }

        // The method checks whether the price of the instrument has exceeded the price
        // of the cluster with the maximum total volume over the last N bars.
        private bool PriceUpperMaxSumVolumeCluster(int count, Candle lastCandle)
        {
            // We find the section with the maximum total volume.
            HorizontalVolumeCluster clusterWithMaxSumVolume = _tabCluster.FindMaxVolumeCluster(count - _rollBackClusters.ValueInt, count, ClusterType.SummVolume);

            //If the price of an instrument exceeds the price with the maximum total volume. Returning true.
            if (lastCandle.Close > clusterWithMaxSumVolume.MaxSummVolumeLine.Price)
            {
                return true;
            }

            // Otherwise we return false.
            return false;
        }

        // The method checks whether the bar is a push bar.
        private bool IsPushingClaster(HorizontalVolumeCluster cluster, Candle lastCandle)
        {
            // if the candle is falling, false is returned.
            if (lastCandle.IsDown)
            {
                return false;
            }

            // From the cluster we obtain the section with the maximum volume of market sales.
            HorizontalVolumeLine line = cluster.MaxSellVolumeLine;

            // Check that the volume is in the lower shadow of the candle.
            if (line.Price > lastCandle.Open)
            {
                return false;
            }

            // If the line volume is less than that specified by the parameter, we also exit.
            if (line.VolumeSell < _minSignalVolume.ValueDecimal)
            {
                return false;
            }

            // If all conditions match, return true.
            return true;
        }

        // The method determines whether the bar is braking.
        private bool IsBrakingClaster(HorizontalVolumeCluster cluster, Candle lastCandle)
        {
            // The candle must be falling, if not, we exit.
            if (lastCandle.IsUp)
            {
                return false;
            }

            // From the cluster we obtain a section with the maximum volume of market purchases.
            HorizontalVolumeLine line = cluster.MaxBuyVolumeLine;

            // If the price of this line is less than the opening price of the candle, false is returned
            if (line.Price < lastCandle.Open)
            {
                return false;
            }

            // If the line volume is less than that specified by the parameter, we also exit.
            if (line.VolumeBuy < _minSignalVolume.ValueDecimal)
            {
                return false;
            }

            // If all conditions match, return true.
            return true;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "ClusterTrend";
        }

        public override void ShowIndividualSettingsDialog()
        {
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