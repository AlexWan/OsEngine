/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using System.Windows;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Robots.CounterTrend
{
    public class ClusterCountertrend : BotPanel
    {

        public ClusterCountertrend(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            //create a tab for trading
            //создаём вкладку для торговли
            TabCreate(BotTabType.Simple);
            _tabToTrade = TabsSimple[0];
            _tabToTrade.CandleFinishedEvent += _tabToTrade_CandleFinishedEvent;

            // create a tab for the cluster
            // создаём вкдалку для кластера
            TabCreate(BotTabType.Cluster);
            _tabCluster = TabsCluster[0];

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On"});
            Volume = CreateParameter("Volume", 1, 1.0m, 50, 1);
            BackLook = CreateParameter("Back Look", 1, 1, 10, 1);

        }

        /// <summary>
        /// volume
        /// объём 
        /// </summary>
        public StrategyParameterDecimal Volume;

        /// <summary>
        /// how many clusters are back we look at the maximum amount
        /// сколько кластеров назад мы смотрим на максимальный объём
        /// </summary>
        public StrategyParameterInt BackLook;

        /// <summary>
        /// regime
        /// режим работы
        /// </summary>
        public StrategyParameterString Regime;

        private BotTabSimple _tabToTrade;

        private BotTabCluster _tabCluster;

        /// <summary>
        /// strategy name
        /// униальное имя стратегии
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "ClusterCountertrend";
        }

        /// <summary>
        /// show settings
        /// показать настройки
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            MessageBox.Show(OsLocalization.Trader.Label111);

        }

        private void _tabToTrade_CandleFinishedEvent(List<Candle> candles)
        {
            // we buy if we are under the largest volume for sale in the last 10 candles
            // sell if we are above the largest purchase volume for the last 10 candles
            // покупаем если находимся под самым большим объёма на продажу за последние 10 свечек
            // продаём если находимся над самым большим объёма на покупку за последние 10 свечек

            if (Regime.ValueString == "Off")
            {
                return;
            }

            if (_tabCluster.VolumeClusters.Count <= BackLook.ValueInt)
            {
                return;
            }

            HorizontalVolumeCluster maxBuyCluster =
                _tabCluster.FindMaxVolumeCluster(candles.Count - BackLook.ValueInt, candles.Count, ClusterType.BuyVolume);
            if (candles[candles.Count - 1].Close > maxBuyCluster.MaxBuyVolumeLine.Price)
            { // sell and exit long positions
              // продаём и выходим из позиции лонг

                List<Position> myPosShort = _tabToTrade.PositionOpenShort;

                if (myPosShort.Count == 0)
                {
                    _tabToTrade.SellAtLimit(Volume.ValueDecimal, candles[candles.Count - 1].Close);
                }

                List<Position> myPosLong = _tabToTrade.PositionOpenLong;

                if (myPosLong.Count != 0 && myPosLong[0].State == PositionStateType.Open)
                {
                    _tabToTrade.CloseAtMarket(myPosLong[0],myPosLong[0].OpenVolume);
                }
            }


            HorizontalVolumeCluster maxSellCluster =
                _tabCluster.FindMaxVolumeCluster(candles.Count - BackLook.ValueInt, candles.Count, ClusterType.SellVolume);

            if (candles[candles.Count - 1].Close < maxSellCluster.MaxSellVolumeLine.Price)
            { // buy and exit short
              // покупаем и выходим из позиции шорт

                List<Position> myPosLong = _tabToTrade.PositionOpenLong;
                if (myPosLong.Count == 0)
                {
                    _tabToTrade.BuyAtLimit(Volume.ValueDecimal, candles[candles.Count - 1].Close);
                }

                List<Position> myPosShort = _tabToTrade.PositionOpenShort;

                if (myPosShort.Count != 0 && myPosShort[0].State == PositionStateType.Open)
                {
                    _tabToTrade.CloseAtMarket(myPosShort[0], myPosShort[0].OpenVolume);
                }
            }
        }
    }
}
