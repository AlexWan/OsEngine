/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Optimizer;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Robots.Rebalancers;
using System;
using System.Collections.Generic;
using System.Linq;


namespace OsEngine.Robots.Sectors
{
    [Bot("SectorsKeltner")]
    public class SectorsKeltner : BotPanel
    {
        #region Fields

        private BotTabSimple _tabLqdt;
        private BotTabScreener _tabScreenerGasOil;
        private BotTabScreener _tabScreenerFinance;
        private BotTabScreener _tabScreenerBlackMetall;
        private BotTabScreener _tabScreenerPreciousMetall;
        private BotTabScreener _tabScreenerBuilders;
        private BotTabScreener _tabScreenerConsumer;

        private StrategyParameterString _regime;
        private StrategyParameterString _mainRebalancePeriodType;
        private StrategyParameterString _mainRebalanceDayOfWeek;
        private StrategyParameterTimeOfDay _mainRebalanceTime;

        private StrategyParameterInt _maxPositionsCount;
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        private StrategyParameterInt _keltnerEmaPeriod;
        private StrategyParameterInt _keltnerAtrPeriod;
        private StrategyParameterDecimal _keltnerMultiplier;

        private StrategyParameterBool _lqdtRebalanceOn;
        private StrategyParameterTimeOfDay _lqdtRebalanceTime;

        #endregion

        #region Constructor

        public SectorsKeltner(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // 1 main settings

            _regime = CreateParameter("Regime", "On", new[] { "On", "Off" });
            _mainRebalancePeriodType = CreateParameter("Rebalance period", "Weekly", new[] { "Daily", "Weekly", "Monthly" });
            _mainRebalanceDayOfWeek = CreateParameter("Rebalance day of week", "Monday", new[] { "Monday", "Tuesday", "Wednesday" });
            _mainRebalanceTime = CreateParameterTimeOfDay("Main rebalance time", 10, 0, 0, 0);

            // 2 stock rebalance settings

            _tabScreenerGasOil = TabCreate<BotTabScreener>();
            _tabScreenerFinance = TabCreate<BotTabScreener>();
            _tabScreenerBlackMetall = TabCreate<BotTabScreener>();
            _tabScreenerPreciousMetall = TabCreate<BotTabScreener>();
            _tabScreenerBuilders = TabCreate<BotTabScreener>();
            _tabScreenerConsumer = TabCreate<BotTabScreener>();

            _keltnerEmaPeriod = CreateParameter("Keltner EMA period", 300, 5, 100, 1);
            _keltnerAtrPeriod = CreateParameter("Keltner ATR period", 20, 5, 100, 1);
            _keltnerMultiplier = CreateParameter("Keltner multiplier", 3.0m, 0.5m, 5.0m, 0.1m);
            _maxPositionsCount = CreateParameter("Max positions ", 3, 1, 50, 4);
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 33, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            CreateIndicatorsForScreener(_tabScreenerGasOil);
            CreateIndicatorsForScreener(_tabScreenerFinance);
            CreateIndicatorsForScreener(_tabScreenerBlackMetall);
            CreateIndicatorsForScreener(_tabScreenerPreciousMetall);
            CreateIndicatorsForScreener(_tabScreenerBuilders);
            CreateIndicatorsForScreener(_tabScreenerConsumer);

            _keltnerEmaPeriod.ValueChange += OnKeltnerStocksParameterChanged;
            _keltnerAtrPeriod.ValueChange += OnKeltnerStocksParameterChanged;
            _keltnerMultiplier.ValueChange += OnKeltnerStocksParameterChanged;

            // 3 LQDT rebalance settings

            _tabLqdt = TabCreate<BotTabSimple>();
            _lqdtRebalanceOn = CreateParameter("LQDT rebalance on", true, "LQDT ");
            _lqdtRebalanceTime = CreateParameterTimeOfDay("LQDT rebalance time", 11, 0, 0, 0, "LQDT ");

            // 4 other

            _tabScreenerGasOil.CandlesSyncFinishedEvent += _tabScreener_CandlesSyncFinishedEvent;
            _tabScreenerGasOil.CandleFinishedEvent += _tabScreenerStocks_CandleFinishedEvent;

        }

        private void CreateIndicatorsForScreener(BotTabScreener screener)
        {
            screener.CreateCandleIndicator(1, "KeltnerChannel", new List<string>() {
                _keltnerEmaPeriod.ValueInt.ToString(),
                _keltnerAtrPeriod.ValueInt.ToString(),
                _keltnerAtrPeriod.ValueInt.ToString(),
                _keltnerMultiplier.ValueDecimal.ToString(),
                "Typical"
            }, "Prime");
        }

        private void OnKeltnerStocksParameterChanged()
        {   
            for (int i = 0; i < _tabScreenerGasOil._indicators.Count; i++)
            {
                if (_tabScreenerGasOil._indicators[i].Num == 1)
                {
                    _tabScreenerGasOil._indicators[i].Parameters[0] = _keltnerEmaPeriod.ValueInt.ToString();
                    _tabScreenerGasOil._indicators[i].Parameters[1] = _keltnerAtrPeriod.ValueInt.ToString();
                    _tabScreenerGasOil._indicators[i].Parameters[2] = _keltnerAtrPeriod.ValueInt.ToString();
                    _tabScreenerGasOil._indicators[i].Parameters[3] = _keltnerMultiplier.ValueDecimal.ToString();
                    break;
                }
            }

            _tabScreenerGasOil.UpdateIndicatorsParameters();

            for (int i = 0; i < _tabScreenerFinance._indicators.Count; i++)
            {
                if (_tabScreenerFinance._indicators[i].Num == 1)
                {
                    _tabScreenerFinance._indicators[i].Parameters[0] = _keltnerEmaPeriod.ValueInt.ToString();
                    _tabScreenerFinance._indicators[i].Parameters[1] = _keltnerAtrPeriod.ValueInt.ToString();
                    _tabScreenerFinance._indicators[i].Parameters[2] = _keltnerAtrPeriod.ValueInt.ToString();
                    _tabScreenerFinance._indicators[i].Parameters[3] = _keltnerMultiplier.ValueDecimal.ToString();
                    break;
                }
            }

            _tabScreenerFinance.UpdateIndicatorsParameters();

            for (int i = 0; i < _tabScreenerBlackMetall._indicators.Count; i++)
            {
                if (_tabScreenerBlackMetall._indicators[i].Num == 1)
                {
                    _tabScreenerBlackMetall._indicators[i].Parameters[0] = _keltnerEmaPeriod.ValueInt.ToString();
                    _tabScreenerBlackMetall._indicators[i].Parameters[1] = _keltnerAtrPeriod.ValueInt.ToString();
                    _tabScreenerBlackMetall._indicators[i].Parameters[2] = _keltnerAtrPeriod.ValueInt.ToString();
                    _tabScreenerBlackMetall._indicators[i].Parameters[3] = _keltnerMultiplier.ValueDecimal.ToString();
                    break;
                }
            }

            _tabScreenerPreciousMetall.UpdateIndicatorsParameters();

            for (int i = 0; i < _tabScreenerPreciousMetall._indicators.Count; i++)
            {
                if (_tabScreenerPreciousMetall._indicators[i].Num == 1)
                {
                    _tabScreenerPreciousMetall._indicators[i].Parameters[0] = _keltnerEmaPeriod.ValueInt.ToString();
                    _tabScreenerPreciousMetall._indicators[i].Parameters[1] = _keltnerAtrPeriod.ValueInt.ToString();
                    _tabScreenerPreciousMetall._indicators[i].Parameters[2] = _keltnerAtrPeriod.ValueInt.ToString();
                    _tabScreenerPreciousMetall._indicators[i].Parameters[3] = _keltnerMultiplier.ValueDecimal.ToString();
                    break;
                }
            }

            _tabScreenerPreciousMetall.UpdateIndicatorsParameters();

            for (int i = 0; i < _tabScreenerBuilders._indicators.Count; i++)
            {
                if (_tabScreenerBuilders._indicators[i].Num == 1)
                {
                    _tabScreenerBuilders._indicators[i].Parameters[0] = _keltnerEmaPeriod.ValueInt.ToString();
                    _tabScreenerBuilders._indicators[i].Parameters[1] = _keltnerAtrPeriod.ValueInt.ToString();
                    _tabScreenerBuilders._indicators[i].Parameters[2] = _keltnerAtrPeriod.ValueInt.ToString();
                    _tabScreenerBuilders._indicators[i].Parameters[3] = _keltnerMultiplier.ValueDecimal.ToString();
                    break;
                }
            }

            _tabScreenerBuilders.UpdateIndicatorsParameters();

            for (int i = 0; i < _tabScreenerConsumer._indicators.Count; i++)
            {
                if (_tabScreenerConsumer._indicators[i].Num == 1)
                {
                    _tabScreenerConsumer._indicators[i].Parameters[0] = _keltnerEmaPeriod.ValueInt.ToString();
                    _tabScreenerConsumer._indicators[i].Parameters[1] = _keltnerAtrPeriod.ValueInt.ToString();
                    _tabScreenerConsumer._indicators[i].Parameters[2] = _keltnerAtrPeriod.ValueInt.ToString();
                    _tabScreenerConsumer._indicators[i].Parameters[3] = _keltnerMultiplier.ValueDecimal.ToString();
                    break;
                }
            }

            _tabScreenerConsumer.UpdateIndicatorsParameters();
        }

        #endregion

        #region Helpers

        private bool IsRebalanceDay(DateTime serverTime)
        {
            string period = _mainRebalancePeriodType.ValueString;

            if (period == "Daily")
            {
                return true;
            }

            DayOfWeek targetDay = ParseDayOfWeek(_mainRebalanceDayOfWeek.ValueString);

            if (serverTime.DayOfWeek != targetDay)
            {
                return false;
            }

            if (period == "Weekly")
            {
                return true;
            }

            if (period == "Monthly")
            {
                if (serverTime.Day > 7)
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        private DayOfWeek ParseDayOfWeek(string day)
        {
            if (Enum.TryParse(day, true, out DayOfWeek result))
            {
                return result;
            }

            return DayOfWeek.Monday;
        }

        private decimal GetCurrentCapital()
        {
            if (_tabLqdt.Portfolio == null)
            {
                return 0m;
            }

            decimal capital = _tabLqdt.Portfolio.ValueCurrent;

            if (capital == 0m)
            {
                capital = _tabLqdt.Portfolio.ValueBegin;
            }

            return capital;
        }

        #endregion

        #region Main entry logic

        private bool _isFirstTimeInOptimizer = false;

        private void _tabScreenerStocks_CandleFinishedEvent(List<Candle> candles, BotTabSimple source)
        {
            if (source.Connector.ServerType != Market.ServerType.Optimizer)
            {
                return;
            }

            if (_isFirstTimeInOptimizer == true)
            {
                return;
            }

            _isFirstTimeInOptimizer = true;

            OptimizerServer server = source.Connector.MyServer as OptimizerServer;

            server.EndNextMinuteWithCandlesEvent += Server_EndNextMinuteWithCandlesEvent;

        }

        private void Server_EndNextMinuteWithCandlesEvent()
        {
            _tabScreener_CandlesSyncFinishedEvent(_tabScreenerGasOil.Tabs);
        }

        private void _tabScreener_CandlesSyncFinishedEvent(List<BotTabSimple> tabs)
        {
            try
            {
                if (_regime.ValueString == "Off")
                {
                    return;
                }

                if (tabs.Count == 0
                    || tabs[0].CandlesAll.Count < 10)
                {
                    return;
                }

                Candle lastCandle = tabs[0].CandlesAll[^1];

                DateTime serverTime = TimeServer;

                if (serverTime == DateTime.MinValue)
                {
                    return;
                }


                if (IsMainRebalanceTime(serverTime, lastCandle))
                {
                    // 1 считаем позиции, которые нужно открыть 

                    RebalancerPositionPackage posesGasOil = GetFuturePositionsStocks(_tabScreenerGasOil);
                    RebalancerPositionPackage posesFinance = GetFuturePositionsStocks(_tabScreenerFinance);
                    RebalancerPositionPackage posesBlackMetall = GetFuturePositionsStocks(_tabScreenerBlackMetall);
                    RebalancerPositionPackage posesPreciousMetall = GetFuturePositionsStocks(_tabScreenerPreciousMetall);
                    RebalancerPositionPackage posesIt = GetFuturePositionsStocks(_tabScreenerBuilders);
                    RebalancerPositionPackage posesConsumer = GetFuturePositionsStocks(_tabScreenerConsumer);

                    // 2 открываем запланированные позиции

                    for (int i = 0; i < posesGasOil.TabsToEntry.Count; i++)
                    {
                        EntryInPositions(posesGasOil.TabsToEntry[i], posesGasOil.Direction);
                    }
                    for (int i = 0; i < posesFinance.TabsToEntry.Count; i++)
                    {
                        EntryInPositions(posesFinance.TabsToEntry[i], posesFinance.Direction);
                    }
                    for (int i = 0; i < posesBlackMetall.TabsToEntry.Count; i++)
                    {
                        EntryInPositions(posesBlackMetall.TabsToEntry[i], posesBlackMetall.Direction);
                    }
                    for (int i = 0; i < posesPreciousMetall.TabsToEntry.Count; i++)
                    {
                        EntryInPositions(posesPreciousMetall.TabsToEntry[i], posesPreciousMetall.Direction);
                    }
                    for (int i = 0; i < posesIt.TabsToEntry.Count; i++)
                    {
                        EntryInPositions(posesIt.TabsToEntry[i], posesIt.Direction);
                    }
                    for (int i = 0; i < posesConsumer.TabsToEntry.Count; i++)
                    {
                        EntryInPositions(posesConsumer.TabsToEntry[i], posesConsumer.Direction);
                    }
                }

                else if (_lqdtRebalanceOn.ValueBool
                    && IsLqdtRebalanceTime(serverTime, lastCandle))
                {
                    RebalanceLqdt();
                    return;
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void EntryInPositions(BotTabSimple tab, Side direction)
        {
            if (direction == Side.None)
            {
                throw new Exception("No position side value. Tab: " + tab.TabName);
            }

            int positionsCount = this.PositionsCount;

            if(_tabLqdt.PositionsOpenAll.Count > 0)
            {
                positionsCount--;
            }

            if(positionsCount >= _maxPositionsCount.ValueInt)
            {
                return;
            }

            if (tab == null 
                || tab.Security == null 
                || tab.CandlesAll == null 
                || tab.CandlesAll.Count == 0
                || tab.PositionsOpenAll.Count > 0)
            {
                return;
            }

            decimal price = tab.CandlesAll[tab.CandlesAll.Count - 1].Close;

            if (price == 0)
            {
                return;
            }

            decimal volumeToEntry = GetVolume(tab);

            tab.BuyAtMarket(volumeToEntry);
            
        }

        private bool IsMainRebalanceTime(DateTime serverTime, Candle candle)
        {
            if (candle == null)
            {
                return false;
            }

            if (candle.TimeStart.TimeOfDay.Hours != _mainRebalanceTime.Value.TimeSpan.Hours)
            {
                return false;
            }

            return IsRebalanceDay(serverTime);
        }

        #endregion

        #region Lqdt rebalance

        private bool IsLqdtRebalanceTime(DateTime serverTime, Candle candle)
        {
            if (candle == null)
            {
                return false;
            }

            if (candle.TimeStart.TimeOfDay.Hours != _lqdtRebalanceTime.Value.TimeSpan.Hours)
            {
                return false;
            }

            return IsRebalanceDay(serverTime);
        }

        private void RebalanceLqdt()
        {
            decimal capital = GetCurrentCapital();
            decimal stockValue = GetCurrentStockValue();
            decimal targetLqdtMoney = capital - stockValue;

            if (stockValue != 0
                && _tabLqdt.PositionsOpenAll.Count == 0)
            {
                return;
            }
            else if (stockValue != 0
                 && _tabLqdt.PositionsOpenAll.Count != 0)
            {
                _tabLqdt.CloseAtMarket(_tabLqdt.PositionsOpenAll[0], _tabLqdt.PositionsOpenAll[0].OpenVolume);
                return;
            }

            if (targetLqdtMoney < 0)
            {
                targetLqdtMoney = 0;
            }

            ReducePositionToTargetMoneyLQDT(_tabLqdt, targetLqdtMoney);
            IncreasePositionToTargetMoneyLQDT(_tabLqdt, targetLqdtMoney);

        }

        private decimal GetCurrentStockValue()
        {
            decimal value = 0m;
            List<BotTabSimple> stockTabs = _tabScreenerGasOil.Tabs;

            for (int i = 0; i < stockTabs.Count; i++)
            {
                BotTabSimple tab = stockTabs[i];
                List<Candle> candles = tab.CandlesAll;

                if (candles == null || candles.Count == 0)
                {
                    continue;
                }

                decimal price = candles[candles.Count - 1].Close;
                List<Position> positions = tab.PositionsOpenAll;

                for (int j = 0; j < positions.Count; j++)
                {
                    if (positions[j].State == PositionStateType.Open)
                    {
                        value += positions[j].OpenVolume * price;
                    }
                }
            }

            return value;
        }

        private void IncreasePositionToTargetMoneyLQDT(BotTabSimple tab, decimal targetMoney)
        {
            if (tab == null || tab.Security == null || tab.CandlesAll == null || tab.CandlesAll.Count == 0)
            {
                return;
            }

            decimal price = tab.CandlesAll[tab.CandlesAll.Count - 1].Close;

            if (price == 0)
            {
                return;
            }

            decimal currentVolume = GetOpenVolume(tab);
            decimal currentMoney = currentVolume * price;

            if (currentMoney >= targetMoney)
            {
                return;
            }

            decimal moneyToBuy = targetMoney - currentMoney;
            decimal volumeToBuy = CalculateVolumeForMoney(tab, moneyToBuy);

            if (volumeToBuy <= 0)
            {
                return;
            }

            List<Position> positions = tab.PositionsOpenAll;

            if (positions.Count > 0)
            {
                return;
            }

            tab.BuyAtMarket(volumeToBuy);
            
        }

        private void ReducePositionToTargetMoneyLQDT(BotTabSimple tab, decimal targetMoney)
        {
            if (tab == null || tab.Security == null || tab.CandlesAll == null || tab.CandlesAll.Count == 0)
            {
                return;
            }

            decimal price = tab.CandlesAll[tab.CandlesAll.Count - 1].Close;

            if (price == 0)
            {
                return;
            }

            decimal currentVolume = GetOpenVolume(tab);
            decimal currentMoney = currentVolume * price;

            if (currentMoney <= targetMoney)
            {
                return;
            }

            decimal moneyToSell = currentMoney - targetMoney;
            decimal volumeToSell = CalculateVolumeForMoney(tab, moneyToSell);

            if (volumeToSell <= 0)
            {
                return;
            }

            volumeToSell = Math.Min(volumeToSell, currentVolume);

            List<Position> positions = tab.PositionsOpenAll;

            for (int i = 0; i < positions.Count && volumeToSell > 0; i++)
            {
                Position position = positions[i];

                if (position.State != PositionStateType.Open)
                {
                    continue;
                }

                decimal closeVolume = Math.Min(volumeToSell, position.OpenVolume);
                tab.CloseAtMarket(position, closeVolume);
                volumeToSell -= closeVolume;
            }
        }

        #endregion

        #region Stocks rebalance

        private RebalancerPositionPackage GetFuturePositionsStocks(BotTabScreener screener)
        {
            RebalancerPositionPackage result = new RebalancerPositionPackage();
            result.Direction = Side.Buy;

            List<BotTabSimple> stockTabs = screener.Tabs;

            if (stockTabs.Count == 0)
            {
                return result;
            }

            bool isRising = UpperSmaAll(stockTabs);

            // 0. Ищем вкладку с позицией

            if(isRising)
            {
                for (int i = 0; i < stockTabs.Count; i++)
                {
                    List<Position> positions = stockTabs[i].PositionsOpenAll;
                    if (positions.Count > 0)
                    {
                        result.TabsToEntry.Add(stockTabs[i]);
                        break;
                    }
                }
            }

            // 1. Считаем текущие рабочие акции, которые можно купить

            if (result.TabsToEntry.Count == 0
                && isRising)
            {
                List<TempValuesRebalancerSectors> tempValues = new List<TempValuesRebalancerSectors>();

                for (int i = 0; i < screener.Tabs.Count; i++)
                {
                    Aindicator keltner = (Aindicator)screener.Tabs[i].Indicators[0];

                    decimal centerKeltnerChannel = keltner.DataSeries[1].Last;

                    if (centerKeltnerChannel == 0
                        || screener.Tabs[i].CandlesAll == null
                        || screener.Tabs[i].CandlesAll.Count == 0)
                    {
                        continue;
                    }

                    decimal lastPrice = screener.Tabs[i].CandlesAll[^1].Close;

                    decimal percentPriceByKeltnerCentre = (lastPrice - centerKeltnerChannel) / (centerKeltnerChannel / 100);

                    TempValuesRebalancerSectors newValue = new TempValuesRebalancerSectors();
                    newValue.Tab = screener.Tabs[i];
                    newValue.PercentPriceByKeltnerCentre = percentPriceByKeltnerCentre;

                    tempValues.Add(newValue);
                }

                if(tempValues.Count > 0)
                {
                    tempValues = tempValues.OrderBy(x => x.PercentPriceByKeltnerCentre).ToList();
                    result.TabsToEntry.Add(tempValues[0].Tab);
                }
            }

            // 2. Закрываем позиции в акциях, которые больше не входят в целевой список

            for (int i = 0; i < screener.Tabs.Count; i++)
            {
                BotTabSimple currentTab = screener.Tabs[i];

                List<Position> positions = currentTab.PositionsOpenAll;

                if (positions.Count == 0)
                {
                    continue;
                }

                if (result.TabsToEntry.Find(t => t.Connector.SecurityName == currentTab.Connector.SecurityName) != null)
                {
                    continue;
                }

                currentTab.CloseAtMarket(positions[0], positions[0].OpenVolume);
            }

            return result;

        }

        private bool UpperSmaAll(List<BotTabSimple> tabs)
        {
            int countRising = 0;
            int countTabs = 0;

            for (int i = 0; i < tabs.Count; i++)
            {
                Aindicator keltner = (Aindicator)tabs[i].Indicators[0];
                decimal centerKeltnerChannel = keltner.DataSeries[1].Last;

                if (tabs[i].CandlesAll.Count == 0)
                {
                    continue;
                }

                countTabs++;

                decimal lastPrice = tabs[i].CandlesAll[^1].Close;

                if (lastPrice >= centerKeltnerChannel)
                {
                    countRising++;
                }
            }

            if (countRising != countTabs)
            {
                return false;
            }

            return true;
        }

        #endregion

        #region Position management

        private decimal GetOpenVolume(BotTabSimple tab)
        {
            decimal volume = 0m;
            List<Position> positions = tab.PositionsOpenAll;

            for (int i = 0; i < positions.Count; i++)
            {
                if (positions[i].OpenVolume != 0)
                {
                    volume += positions[i].OpenVolume;
                }
            }

            return volume;
        }

        private decimal CalculateVolumeForMoney(BotTabSimple tab, decimal money)
        {
            if (tab == null || tab.Security == null || tab.CandlesAll == null || tab.CandlesAll.Count == 0)
            {
                return 0m;
            }

            decimal price = tab.CandlesAll[tab.CandlesAll.Count - 1].Close;

            if (price == 0)
            {
                return 0m;
            }

            decimal lot = tab.Security.Lot;

            if (lot == 0)
            {
                lot = 1m;
            }

            decimal volume = money / (price * lot);

            int decimals = tab.Security.DecimalsVolume;

            if (decimals < 0)
            {
                decimals = 0;
            }

            decimal multiplier = (decimal)Math.Pow(10, decimals);
            volume = Math.Floor(volume * multiplier) / multiplier;

            return volume;
        }

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

        #endregion

    }

    public class TempValuesRebalancerSectors
    {
        public BotTabSimple Tab;

        public decimal PercentPriceByKeltnerCentre;

    }
}
