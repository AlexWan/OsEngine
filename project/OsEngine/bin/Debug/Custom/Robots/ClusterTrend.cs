using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System.Collections.Generic;
using System.Linq;

/* Description
trading robot for osengine

The trend robot on ClusterTrend

Buy: When a pushing bullish cluster occurs.

Exit: When a pushing bearish cluster occurs.

 */

namespace OsEngine.Robots.Trend
{
    [Bot("ClusterTrend")] // We create an attribute so that we don't write anything to the BotFactory
    public class ClusterTrend : BotPanel
    {
        // Settings
        private StrategyParameterDecimal _minSignalVolume;
        private StrategyParameterDecimal _volume;
        private StrategyParameterInt _rollBackClusters;

        // Tabs
        private BotTabCluster _tabCluster;
        private BotTabSimple _tabToTrade;

        public ClusterTrend(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Settings
            _minSignalVolume = CreateParameter("Min signal volume", 50m, 10, 1000, 10);
            _volume = CreateParameter("Trade volume", 1m, 1, 10, 10);
            _rollBackClusters = CreateParameter("Roll back clusters", 10, 10, 30, 5);

            // Creating Tabs
            TabCreate(BotTabType.Cluster);
            _tabCluster = TabsCluster.Last();

            TabCreate(BotTabType.Simple);
            _tabToTrade = TabsSimple.Last();

            // Subscribe to event changed cluster with maximum total purchase amount
            _tabCluster.MaxBuyClusterChangeEvent += TabClusterMaxBuyClusterChangeEvent;

            // Subscribe to the candle finished event
            _tabToTrade.CandleFinishedEvent += TabToTradeCandleFinishedEvent;

            Description = "The trend robot on ClusterTrend. " +
                "Buy: When a pushing bullish cluster occurs. " +
                "Exit: When a pushing bearish cluster occurs.";
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
            Candle lastCandle = candles.Last();

            // From the list of all clusters we get the one that is the profile of this candle.
            HorizontalVolumeCluster lastCluster = _tabCluster.VolumeClusters
                .FindLast(cluster => cluster.Time == lastCandle.TimeStart);

            // If there are no open positions.
            if (_tabToTrade.PositionOpenLong.Count == 0)
            {
                // If there are no open positions, the bar is pushing, and the price of the instrument
                // is higher than the price of the section with the maximum total volume for the last n candles, open a long position.
                if (IsPushingClaster(lastCluster, lastCandle)
                    && PriceUpperMaxSumVolumeCluster(candles.Count - 1, lastCandle))
                {
                    _tabToTrade.BuyAtMarket(_volume.ValueDecimal);
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
            HorizontalVolumeCluster clusterWithMaxSumVolume = _tabCluster
               .FindMaxVolumeCluster(count - _rollBackClusters.ValueInt, count, ClusterType.SummVolume);

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
    }
}
