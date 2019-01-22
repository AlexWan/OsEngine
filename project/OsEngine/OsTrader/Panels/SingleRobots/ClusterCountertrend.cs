using System.Collections.Generic;
using System.Windows;
using OsEngine.Entity;
using OsEngine.Market;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.OsTrader.Panels.SingleRobots
{
    class ClusterCountertrend : BotPanel
    {
        /// <summary>
        /// конструктор
        /// </summary>
        public ClusterCountertrend(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            //создание вкладку для торговли
            TabCreate(BotTabType.Simple);
            _tabToTrade = TabsSimple[0];
            _tabToTrade.CandleFinishedEvent += _tabToTrade_CandleFinishedEvent;

            // создаём вкдалку для кластера
            TabCreate(BotTabType.Cluster);
            _tabCluster = TabsCluster[0];

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On"});
            Volume = CreateParameter("Volume", 1, 1.0m, 50, 1);
            BackLook = CreateParameter("Back Look", 1, 1, 10, 1);

        }

        /// <summary>
        /// объём 
        /// </summary>
        public StrategyParameterDecimal Volume;

        /// <summary>
        /// сколько кластеров назад мы смотрим на максимальный объём
        /// </summary>
        public StrategyParameterInt BackLook;

        /// <summary>
        /// режим работы
        /// </summary>
        public StrategyParameterString Regime;

        private BotTabSimple _tabToTrade;

        private BotTabCluster _tabCluster;

        /// <summary>
        /// униальное имя стратегии
        /// </summary>
        /// <returns></returns>
        public override string GetNameStrategyType()
        {
            return "ClusterCountertrend";
        }

        /// <summary>
        /// показать настройки
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            MessageBox.Show("У данной стратегии настройки в виде параметров. Покупаем если находимся под самым большим объёмом на продажу за последние N кластеров. " + 
                            "Продаём если находимся над самым большим объёмом на покупку за последние N кластеров");

        }

        private void _tabToTrade_CandleFinishedEvent(List<Candle> candles)
        {
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
            { // продаём и выходим из позиции лонг

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
            { // покупаем и выходим из позиции шорт

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
