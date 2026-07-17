/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_transaction.pdf
*/

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers.Optimizer;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Wiki;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

/* Description
Робот еженедельно ребалансирует портфель из дивидендных акций и золота.
Работает в двух режимах:
1. Классический режим: поддержание соотношения акции / золото по заданным весам.
2. Дивидендный режим: если у акций в скринере есть дивиденды в ближайшие N дней,
   весь выделенный депозит входит в эти акции равномерно.
Только лонг, без плеча.
*/

namespace OsEngine.Robots.Rebalancers
{
    [Bot("RebalancerClassicDividend")]
    public class RebalancerClassicDividend : BotPanel
    {
        #region Fields

        private BotTabScreener _tabScreenerStocks;
        private BotTabSimple _tabGold;

        private StrategyParameterString _regime;
        private StrategyParameterDecimal _tradeDepositPercent;
        private StrategyParameterString _rebalancePeriod;
        private StrategyParameterString _rebalanceDayOfWeek;
        private StrategyParameterTimeOfDay _rebalanceTime;

        private StrategyParameterString _detailedLoggingEnabled;

        private StrategyParameterDecimal _stocksWeightPercent;
        private StrategyParameterString _classicModeEnabled;
        private StrategyParameterString _smaFilterMode;
        private StrategyParameterInt _smaPeriod;

        private StrategyParameterString _dividendModeEnabled;
        private StrategyParameterDecimal _minDividendYieldPercent;

        private StrategyParameterString _autoUpdateDividends;
        private StrategyParameterTimeOfDay _dividendsUpdateCheckTime;
        private StrategyParameterButton _startUpdateDividendsButton;
        private StrategyParameterButton _rebalanceNowButton;

        private DateTime _lastRebalanceDate = DateTime.MinValue;
        private DateTime _lastDividendsUpdateCheckDate = DateTime.MinValue;

        private bool _dividendsUpdating = false;

        #endregion

        #region Constructor

        public RebalancerClassicDividend(string name, StartProgram startProgram) : base(name, startProgram)
        {
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "OnTestRebalance", "OnRealRebalance" }, "Base");
            _tradeDepositPercent = CreateParameter("Trade deposit percent", 50.0m, 10.0m, 100.0m, 1.0m, "Base");
            _rebalancePeriod = CreateParameter("Rebalance period", "Weekly", new[] { "Weekly", "Monthly" }, "Base");
            _rebalanceDayOfWeek = CreateParameter("Rebalance day of week", "Tuesday", new[] { "Monday", "Tuesday", "Wednesday" }, "Base");
            _rebalanceTime = CreateParameterTimeOfDay("Rebalance time", 11, 0, 0, 0, "Base");

            _detailedLoggingEnabled = CreateParameter("Detailed logging enabled", "Off", new[] { "On", "Off" }, "Base");

            _classicModeEnabled = CreateParameter("Classic mode enabled", "On", new[] { "On", "Off" }, "Classic");
            _stocksWeightPercent = CreateParameter("Stocks weight percent", 60.0m, 10.0m, 100.0m, 1.0m, "Classic");
            _smaFilterMode = CreateParameter("SMA filter mode", "OnBuyUpperSma", new[] { "Off", "OnBuyUpperSma", "OnBuyBelowSma" }, "Classic");
            _smaPeriod = CreateParameter("SMA period", 180, 50, 1000, 1, "Classic");
            
            _dividendModeEnabled = CreateParameter("Dividend mode enabled", "On", new[] { "On", "Off" }, "Dividend");
            _minDividendYieldPercent = CreateParameter("Min dividend yield percent", 1.0m, 0.0m, 50.0m, 0.1m, "Dividend");

            _autoUpdateDividends = CreateParameter("Auto update dividends", "On", new[] { "On", "Off" }, "Update");
            _dividendsUpdateCheckTime = CreateParameterTimeOfDay("Dividends update check time", 8, 0, 0, 0, "Update");
            _startUpdateDividendsButton = CreateParameterButton("Start update dividends", "Update");
            _startUpdateDividendsButton.UserClickOnButtonEvent += StartUpdateDividendsButton_UserClickOnButtonEvent;

            _rebalanceNowButton = CreateParameterButton("Rebalance NOW", "Base");
            _rebalanceNowButton.UserClickOnButtonEvent += RebalanceNowButton_UserClickOnButtonEvent;

            _tabScreenerStocks = TabCreate<BotTabScreener>();
            _tabGold = TabCreate<BotTabSimple>();

            _tabScreenerStocks.CreateCandleIndicator(1, "Sma", new List<string>() { _smaPeriod.ValueInt.ToString(), "Close" }, "Prime");

            ParametrsChangeByUser += RebalancerClassicDividend_ParametrsChangeByUser;

            _tabScreenerStocks.CandlesSyncFinishedEvent += _tabScreenerStocks_CandlesSyncFinishedEvent;
            _tabScreenerStocks.CandleFinishedEvent += _tabScreenerStocks_CandleFinishedEvent;

            Description = OsLocalization.ConvertToLocString(
                "Eng:Weekly rebalancer between dividend stocks and gold. Operates in two modes. " +
                "Classic mode maintains the configured stock/gold weight using SMA filter. " +
                "Dividend mode switches the whole deposit into stocks with upcoming dividends. " +
                "For real trading uses smart rebalancing. Closes only unwanted positions, opens missing ones, " +
                "and adjusts existing volumes via BuyAtMarketToPosition / CloseAtMarket._" 
                +
                "Ru:Еженедельный ребалансировщик между дивидендными акциями и золотом. " +
                "Работает в двух режимах. Классический режим поддерживает заданный вес акций/золота с фильтром по SMA. " +
                "Дивидендный режим переводит весь депозит в акции с ближайшими дивидендами. " +
                "Для реального трейдинга использует умный ребаланс. Закрывает только ненужные позиции, открывает недостающие " +
                "и корректирует объёмы через BuyAtMarketToPosition / CloseAtMarket._");
        }

        #endregion

        #region Events

        private void RebalancerClassicDividend_ParametrsChangeByUser()
        {
            try
            {
                if (_tabScreenerStocks == null
                    || _tabScreenerStocks._indicators == null
                    || _tabScreenerStocks._indicators.Count == 0)
                {
                    return;
                }

                _tabScreenerStocks._indicators[0].Parameters = new List<string>()
                {
                    _smaPeriod.ValueInt.ToString(),
                    "Close"
                };

                _tabScreenerStocks.UpdateIndicatorsParameters();
            }
            catch (Exception error)
            {
                SendNewLogMessage($"Parameter change error: {error}", LogMessageType.Error);
            }
        }

        private bool _optimizerEventSubscribed = false;

        private void _tabScreenerStocks_CandleFinishedEvent(List<Candle> candles, BotTabSimple source)
        {
            try
            {
                if (source.Connector.ServerType != ServerType.Optimizer)
                {
                    return;
                }

                if (_optimizerEventSubscribed == true)
                {
                    return;
                }

                _optimizerEventSubscribed = true;

                OptimizerServer server = source.Connector.MyServer as OptimizerServer;

                if (server == null)
                {
                    return;
                }

                server.EndNextMinuteWithCandlesEvent += Server_EndNextMinuteWithCandlesEvent;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void Server_EndNextMinuteWithCandlesEvent()
        {
            _tabScreenerStocks_CandlesSyncFinishedEvent(_tabScreenerStocks.Tabs);
        }

        private void RebalanceNowButton_UserClickOnButtonEvent()
        {
            try
            {
                SendNewLogMessage("Manual rebalance requested", LogMessageType.System);
                ExecuteRebalance();
            }
            catch (Exception error)
            {
                SendNewLogMessage($"Manual rebalance error: {error}", LogMessageType.Error);
            }
        }

        private void _tabScreenerStocks_CandlesSyncFinishedEvent(List<BotTabSimple> tabs)
        {
            try
            {
                if (_regime.ValueString == "Off")
                {
                    return;
                }

                if (tabs == null
                    || tabs.Count == 0
                    || tabs[0].CandlesAll == null
                    || tabs[0].CandlesAll.Count < 10)
                {
                    return;
                }

                Candle lastCandle = tabs[0].CandlesAll[tabs[0].CandlesAll.Count - 1];
                DateTime serverTime = TimeServer;

                if (serverTime == DateTime.MinValue)
                {
                    return;
                }

                if(StartProgram == StartProgram.IsOsTrader)
                {
                    CheckDividendsUpdate(serverTime);
                }

                if (_lastRebalanceDate.Date != serverTime.Date
                    && IsRebalanceTime(serverTime, lastCandle, tabs[0]))
                {
                    ExecuteRebalance();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ExecuteRebalance()
        {
            try
            {
                if (_regime.ValueString == "Off"
                    || _regime.ValueString == "OnTestRebalance")
                {
                    RebalanceTestCloseAllRegime();
                }
                else if (_regime.ValueString == "OnRealRebalance")
                {
                    RebalanceRealRegime();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage($"Rebalance error: {error}", LogMessageType.Error);
            }
        }

        #endregion

        #region Dividends update

        private void StartUpdateDividendsButton_UserClickOnButtonEvent()
        {
            try
            {
                if (StartProgram != StartProgram.IsOsTrader)
                {
                    SendNewLogMessage("Manual dividends update is available only in real trading mode", LogMessageType.Error);
                    return;
                }

                string path = GetDividendsBasePath();

                if (!Directory.Exists(path))
                {
                    SendNewLogMessage($"Dividends directory not found: {path}", LogMessageType.Error);
                    return;
                }


                if (_dividendsUpdating)
                {
                    SendNewLogMessage("Dividends update is already in progress", LogMessageType.System);
                    return;
                }

                SendNewLogMessage("Manual dividends update started", LogMessageType.System);
                StartDividendsUpdate();
            }
            catch (Exception error)
            {
                SendNewLogMessage($"Start update dividends button error: {error}", LogMessageType.Error);
            }
        }

        private void CheckDividendsUpdate(DateTime serverTime)
        {
            try
            {
                if (StartProgram != StartProgram.IsOsTrader)
                {
                    return;
                }

                if (_autoUpdateDividends.ValueString == "Off")
                {
                    return;
                }

                if (_lastDividendsUpdateCheckDate.Date == serverTime.Date)
                {
                    return;
                }

                if (!IsRebalanceDate(serverTime))
                {
                    return;
                }

                TimeSpan checkTime = _dividendsUpdateCheckTime.Value.TimeSpan;

                if (serverTime.TimeOfDay < checkTime)
                {
                    return;
                }

                _lastDividendsUpdateCheckDate = serverTime;

                if (!IsDividendsBaseStale(serverTime))
                {
                    SendNewLogMessage("Dividends base is up to date", LogMessageType.System);
                    return;
                }

                SendNewLogMessage("Dividends base is stale. Starting auto update", LogMessageType.System);
                StartDividendsUpdate();
            }
            catch (Exception error)
            {
                SendNewLogMessage($"CheckDividendsUpdate error: {error}", LogMessageType.Error);
            }
        }

        private void StartDividendsUpdate()
        {
            if (_dividendsUpdating)
            {
                return;
            }

            _dividendsUpdating = true;

            Task.Run(() =>
            {
                try
                {
                    WikiMaster.UpdateDividendsBase();
                }
                catch (Exception error)
                {
                    SendNewLogMessage($"Dividends update error: {error}", LogMessageType.Error);
                }
                finally
                {

                    _dividendsUpdating = false;
                    SendNewLogMessage("Dividends update finished", LogMessageType.System);
                }
            });
        }

        private bool IsDividendsBaseStale(DateTime currentTime)
        {
            try
            {
                string path = GetDividendsBasePath();

                if (!Directory.Exists(path))
                {
                    return true;
                }

                DateTime lastWrite = Directory.GetLastWriteTime(path);
                double ageDays = (currentTime - lastWrite).TotalDays;

                return ageDays > 5;
            }
            catch (Exception error)
            {
                SendNewLogMessage($"IsDividendsBaseStale error: {error}", LogMessageType.Error);
                return false;
            }
        }

        private string GetDividendsBasePath()
        {
            return AppDomain.CurrentDomain.BaseDirectory + "Wiki\\Dividends";
        }

        #endregion

        #region Real regime

        private void RebalanceRealRegime()
        {
            try
            {
                _lastRebalanceDate = TimeServer;

                decimal totalCapital = GetCurrentCapital();

                if (totalCapital <= 0)
                {
                    SendNewLogMessage("Rebalance skipped: portfolio capital is zero", LogMessageType.System);
                    return;
                }

                decimal tradeDeposit = totalCapital * _tradeDepositPercent.ValueDecimal / 100m;

                if (tradeDeposit <= 0)
                {
                    SendNewLogMessage("Rebalance skipped: trade deposit is zero", LogMessageType.System);
                    return;
                }

                Dictionary<BotTabSimple, decimal> targetVolumes = BuildTargetPortfolio(tradeDeposit);

                if (targetVolumes == null || targetVolumes.Count == 0)
                {
                    SendNewLogMessage("Rebalance skipped: target portfolio is empty", LogMessageType.System);
                    return;
                }

                ClosePositionsNotInTarget(targetVolumes);
                OpenMissingPositions(targetVolumes);
                AdjustExistingPositions(targetVolumes);

                SendNewLogMessage("Real rebalance finished", LogMessageType.System);
            }
            catch (Exception error)
            {
                SendNewLogMessage($"Rebalance error: {error}", LogMessageType.Error);
            }
        }

        private Dictionary<BotTabSimple, decimal> BuildTargetPortfolio(decimal tradeDeposit)
        {
            Dictionary<BotTabSimple, decimal> result = new Dictionary<BotTabSimple, decimal>();

            List<CandidateInfo> dividendCandidates = GetCandidatesToDividendsNextPeriod();

            if (_dividendModeEnabled.ValueString == "On"
                && dividendCandidates.Count > 0)
            {
                decimal moneyOnOneStock = tradeDeposit / dividendCandidates.Count;

                for (int i = 0; i < dividendCandidates.Count; i++)
                {
                    BotTabSimple tab = dividendCandidates[i].Tab;

                    if (tab == null || tab.Security == null || tab.CandlesAll == null || tab.CandlesAll.Count == 0)
                    {
                        continue;
                    }

                    decimal price = tab.CandlesAll[tab.CandlesAll.Count - 1].Close;

                    if (price == 0)
                    {
                        continue;
                    }

                    decimal targetVolume = CalculateVolumeForMoney(tab, moneyOnOneStock, price);

                    if (targetVolume > 0)
                    {
                        result[tab] = targetVolume;
                    }
                }

                return result;
            }

            if (_classicModeEnabled.ValueString == "On")
            {
                List<CandidateInfo> classicCandidates = GetCandidatesToClassicMode();

                if (classicCandidates.Count == 0)
                {
                    if (_tabGold != null && _tabGold.CandlesAll != null && _tabGold.CandlesAll.Count > 0)
                    {
                        decimal price = _tabGold.CandlesAll[_tabGold.CandlesAll.Count - 1].Close;

                        if (price != 0)
                        {
                            decimal targetVolume = CalculateVolumeForMoney(_tabGold, tradeDeposit, price);

                            if (targetVolume > 0)
                            {
                                result[_tabGold] = targetVolume;
                            }
                        }
                    }
                }
                else
                {
                    decimal stocksPercent = _stocksWeightPercent.ValueDecimal;

                    if (stocksPercent < 0 || stocksPercent > 100)
                    {
                        SendNewLogMessage("Rebalance skipped: stocks weight percent is out of range", LogMessageType.System);
                        return result;
                    }

                    decimal stockMoney = tradeDeposit * stocksPercent / 100m;
                    decimal goldMoney = tradeDeposit * (100m - stocksPercent) / 100m;
                    decimal moneyOnOneStock = stockMoney / classicCandidates.Count;

                    for (int i = 0; i < classicCandidates.Count; i++)
                    {
                        BotTabSimple tab = classicCandidates[i].Tab;

                        if (tab == null || tab.Security == null || tab.CandlesAll == null || tab.CandlesAll.Count == 0)
                        {
                            continue;
                        }

                        decimal price = tab.CandlesAll[tab.CandlesAll.Count - 1].Close;

                        if (price == 0)
                        {
                            continue;
                        }

                        decimal targetVolume = CalculateVolumeForMoney(tab, moneyOnOneStock, price);

                        if (targetVolume > 0)
                        {
                            result[tab] = targetVolume;
                        }
                    }

                    if (goldMoney > 0 && _tabGold != null && _tabGold.CandlesAll != null && _tabGold.CandlesAll.Count > 0)
                    {
                        decimal price = _tabGold.CandlesAll[_tabGold.CandlesAll.Count - 1].Close;

                        if (price != 0)
                        {
                            decimal targetVolume = CalculateVolumeForMoney(_tabGold, goldMoney, price);

                            if (targetVolume > 0)
                            {
                                result[_tabGold] = targetVolume;
                            }
                        }
                    }
                }
            }

            return result;
        }

        private void ClosePositionsNotInTarget(Dictionary<BotTabSimple, decimal> targetVolumes)
        {
            List<BotTabSimple> tabs = _tabScreenerStocks.Tabs;

            for (int i = 0; tabs != null && i < tabs.Count; i++)
            {
                BotTabSimple tab = tabs[i];

                if (tab.IsConnected == false
                    || tab.IsReadyToTrade == false
                    || tab.CandlesAll == null
                    || tab.PriceBestAsk == 0)
                {
                    continue;
                }

                if (tab == null || targetVolumes.ContainsKey(tab))
                {
                    continue;
                }

                List<Position> positions = tab.PositionsOpenAll;

                for (int j = 0; j < positions.Count; j++)
                {
                    tab.CloseAtMarket(positions[j], positions[j].OpenVolume);
                }
            }

            if (_tabGold != null
                && !targetVolumes.ContainsKey(_tabGold)
                && _tabGold.IsReadyToTrade == true
                && _tabGold.IsConnected == true
                && _tabGold.PriceBestAsk != 0)
            {
                List<Position> positions = _tabGold.PositionsOpenAll;

                for (int i = 0; i < positions.Count; i++)
                {
                    _tabGold.CloseAtMarket(positions[i], positions[i].OpenVolume);
                }
            }
        }

        private void OpenMissingPositions(Dictionary<BotTabSimple, decimal> targetVolumes)
        {
            foreach (KeyValuePair<BotTabSimple, decimal> kvp in targetVolumes)
            {
                BotTabSimple tab = kvp.Key;
                decimal targetVolume = kvp.Value;

                if (tab == null
                    || tab.Security == null
                    || tab.CandlesAll == null
                    || tab.CandlesAll.Count == 0
                    || tab.IsReadyToTrade == false)
                {
                    continue;
                }

                decimal currentVolume = GetOpenPositionVolume(tab);

                if (currentVolume > 0)
                {
                    continue;
                }

                decimal price = tab.CandlesAll[tab.CandlesAll.Count - 1].Close;

                if (price == 0)
                {
                    continue;
                }

                tab.BuyAtMarket(targetVolume);
            }
        }

        private decimal GetOpenPositionVolume(BotTabSimple tab)
        {
            if (tab == null)
            {
                return 0m;
            }

            decimal volume = 0m;
            List<Position> positions = tab.PositionsOpenAll;

            for (int i = 0; i < positions.Count; i++)
            {
                volume += positions[i].OpenVolume;
            }

            return volume;
        }

        private void AdjustExistingPositions(Dictionary<BotTabSimple, decimal> targetVolumes)
        {
            foreach (KeyValuePair<BotTabSimple, decimal> kvp in targetVolumes)
            {
                BotTabSimple tab = kvp.Key;
                decimal targetVolume = kvp.Value;

                if (tab == null
                    || tab.Security == null
                    || tab.IsReadyToTrade == false
                    || tab.PriceBestAsk == 0)
                {
                    continue;
                }

                decimal currentVolume = GetOpenPositionVolume(tab);

                if (currentVolume == 0)
                {
                    continue;
                }

                Position position = GetOpenPosition(tab);

                if (position == null)
                {
                    continue;
                }

                if (targetVolume > currentVolume)
                {
                    tab.BuyAtMarketToPosition(position, targetVolume - currentVolume);
                }
                else if (targetVolume < currentVolume)
                {
                    tab.CloseAtMarket(position, currentVolume - targetVolume);
                }
            }
        }

        private Position GetOpenPosition(BotTabSimple tab)
        {
            if (tab == null)
            {
                return null;
            }

            List<Position> positions = tab.PositionsOpenAll;

            if (positions.Count == 0)
            {
                return null;
            }

            return positions[0];
        }

        #endregion

        #region Test regime with close all positions on start week or month

        private void RebalanceTestCloseAllRegime()
        {
            try
            {
                _lastRebalanceDate = TimeServer;

                CloseAllStockPositions();
                CloseGoldPosition();

                SendNewLogMessage("Rebalance started", LogMessageType.System);

                decimal totalCapital = GetCurrentCapital();

                if (totalCapital <= 0)
                {
                    SendNewLogMessage("Rebalance skipped: portfolio capital is zero", LogMessageType.System);
                    return;
                }

                decimal tradeDeposit = totalCapital * _tradeDepositPercent.ValueDecimal / 100m;

                if (tradeDeposit <= 0)
                {
                    SendNewLogMessage("Rebalance skipped: trade deposit is zero", LogMessageType.System);
                    return;
                }

                List<CandidateInfo> candidatesWithDividends = GetCandidatesToDividendsNextPeriod();

                if (_dividendModeEnabled.ValueString == "On"
                    && candidatesWithDividends.Count > 0)
                {
                    OpenPositionsInDividendMode(candidatesWithDividends, tradeDeposit);
                }
                else if (_classicModeEnabled.ValueString == "On")
                {
                    OpenPositionsInClassicMode(tradeDeposit);
                }
                else
                {
                    SendNewLogMessage("Rebalance skipped: both modes are disabled or no dividend candidates", LogMessageType.System);
                }

                SendNewLogMessage("Rebalance finished", LogMessageType.System);
            }
            catch (Exception error)
            {
                SendNewLogMessage($"Rebalance error: {error}", LogMessageType.Error);
            }
        }

        private void OpenPositionsInClassicMode(decimal tradeDeposit)
        {
            List<CandidateInfo> candidates = GetCandidatesToClassicMode();

            decimal stockMoney = 0m;
            decimal goldMoney = 0m;

            if (candidates.Count == 0)
            {
                goldMoney = tradeDeposit;
                LogDetailed($"Classic mode: no candidates, all deposit to gold {Math.Round(goldMoney, 2)}");
            }
            else
            {
                decimal stocksPercent = _stocksWeightPercent.ValueDecimal;

                if (stocksPercent < 0 || stocksPercent > 100)
                {
                    SendNewLogMessage("Rebalance skipped: stocks weight percent is out of range", LogMessageType.System);
                    return;
                }

                stockMoney = tradeDeposit * stocksPercent / 100m;
                goldMoney = tradeDeposit * (100m - stocksPercent) / 100m;

                LogDetailed($"Classic mode: stock {Math.Round(stockMoney, 2)}, gold {Math.Round(goldMoney, 2)}, total deposit {Math.Round(tradeDeposit, 2)}");

                if (stockMoney > 0)
                {
                    decimal moneyOnOneStock = stockMoney / candidates.Count;

                    for (int i = 0; i < candidates.Count; i++)
                    {
                        if (candidates[i].Tab == null)
                        {
                            continue;
                        }

                        LogDetailed($"Classic candidate: {candidates[i].Ticker}, registry {candidates[i].RegistryDate:dd.MM.yyyy}");
                        EntryInPosition(candidates[i].Tab, moneyOnOneStock);
                    }
                }
            }

            if (goldMoney > 0)
            {
                EntryInPosition(_tabGold, goldMoney);
            }
        }

        private void OpenPositionsInDividendMode(List<CandidateInfo> candidatesWithDividends, decimal tradeDeposit)
        {
            decimal moneyOnOneStock = tradeDeposit / candidatesWithDividends.Count;

            LogDetailed($"Dividend mode: {candidatesWithDividends.Count} candidates, " +
                $"{Math.Round(moneyOnOneStock, 2)} per stock, total deposit {Math.Round(tradeDeposit, 2)}");

            for (int i = 0; i < candidatesWithDividends.Count; i++)
            {
                LogDetailed($"Dividend candidate: {candidatesWithDividends[i].Ticker}, registry {candidatesWithDividends[i].RegistryDate:dd.MM.yyyy}");
                EntryInPosition(candidatesWithDividends[i].Tab, moneyOnOneStock);
            }

        }

        #endregion

        #region Common methods

        private List<CandidateInfo> GetCandidatesToDividendsNextPeriod()
        {
            List<CandidateInfo> result = new List<CandidateInfo>();

            List<BotTabSimple> tabs = _tabScreenerStocks.Tabs;
            DateTime currentTime = TimeServer;
            DateTime maxDate = GetDividendMaxDate(currentTime);

            for (int i = 0; tabs != null && i < tabs.Count; i++)
            {
                BotTabSimple tab = tabs[i];

                if (tab == null
                    || tab.Security == null
                    || string.IsNullOrWhiteSpace(tab.Security.Name))
                {
                    continue;
                }

                string ticker = tab.Security.Name;

                WikiDividendFuture future = WikiMaster.GetDividendsFuture(ticker, currentTime);

                if (future == null
                    || future.future == null
                    || string.IsNullOrWhiteSpace(future.future.registry_close_date))
                {
                    continue;
                }

                if (future.future.dividend_yield < _minDividendYieldPercent.ValueDecimal)
                {
                    continue;
                }

                if (!DateTime.TryParseExact(future.future.registry_close_date, "dd.MM.yyyy",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out DateTime registryDate))
                {
                    continue;
                }

                if (registryDate.Date < currentTime.Date
                    || registryDate.Date > maxDate.Date)
                {
                    continue;
                }

                result.Add(new CandidateInfo
                {
                    Tab = tab,
                    Ticker = ticker,
                    RegistryDate = registryDate
                });
            }

            return result;
        }

        private DateTime GetDividendMaxDate(DateTime currentTime)
        {
            DayOfWeek targetDayOfWeek = ParseDayOfWeek(_rebalanceDayOfWeek.ValueString);

            if (_rebalancePeriod.ValueString == "Monthly")
            {
                DateTime currentMonthTarget = GetFirstWeekDayOfMonth(currentTime.Year, currentTime.Month, targetDayOfWeek);

                if (currentTime.Date < currentMonthTarget.Date)
                {
                    return currentMonthTarget;
                }

                int nextMonth = currentTime.Month == 12 ? 1 : currentTime.Month + 1;
                int nextYear = currentTime.Month == 12 ? currentTime.Year + 1 : currentTime.Year;

                return GetFirstWeekDayOfMonth(nextYear, nextMonth, targetDayOfWeek);
            }

            return currentTime.AddDays(7);
        }

        private DateTime GetFirstWeekDayOfMonth(int year, int month, DayOfWeek targetDayOfWeek)
        {
            DateTime firstDay = new DateTime(year, month, 1);
            int daysUntilTarget = ((int)targetDayOfWeek - (int)firstDay.DayOfWeek + 7) % 7;
            return firstDay.AddDays(daysUntilTarget);
        }

        private List<CandidateInfo> GetCandidatesToClassicMode()
        {
            List<CandidateInfo> result = new List<CandidateInfo>();

            List<BotTabSimple> tabs = _tabScreenerStocks.Tabs;
            DateTime currentTime = TimeServer;
            DateTime minDate = currentTime.AddDays(-380);

            for (int i = 0; tabs != null && i < tabs.Count; i++)
            {
                BotTabSimple tab = tabs[i];

                if (tab == null
                    || tab.Security == null
                    || string.IsNullOrWhiteSpace(tab.Security.Name))
                {
                    continue;
                }

                string ticker = tab.Security.Name;

                WikiDividendPast past = WikiMaster.GetDividendsPast(ticker, currentTime);

                if (past == null
                    || past.past == null
                    || string.IsNullOrWhiteSpace(past.past.registry_close_date))
                {
                    continue;
                }

                if (past.past.dividend_yield < _minDividendYieldPercent.ValueDecimal)
                {
                    continue;
                }

                if (!DateTime.TryParseExact(past.past.registry_close_date, "dd.MM.yyyy",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out DateTime registryDate))
                {
                    continue;
                }

                if (registryDate.Date < minDate.Date
                    || registryDate.Date > currentTime.Date)
                {
                    continue;
                }

                if (!IsPriceMatchSmaFilter(tab))
                {
                    continue;
                }

                result.Add(new CandidateInfo
                {
                    Tab = tab,
                    Ticker = ticker,
                    RegistryDate = registryDate
                });
            }

            if (result.Count == 0 && StartProgram == StartProgram.IsTester)
            {
                string[] fallbackTickers = new[] { "SBER", "SBERP", "GAZP", "LKOH", "VTBR" };

                for (int i = 0; tabs != null && i < tabs.Count; i++)
                {
                    BotTabSimple tab = tabs[i];

                    if (tab == null
                        || tab.Security == null
                        || string.IsNullOrWhiteSpace(tab.Security.Name))
                    {
                        continue;
                    }

                    string ticker = tab.Security.Name;

                    for (int j = 0; j < fallbackTickers.Length; j++)
                    {
                        if (ticker.Equals(fallbackTickers[j], StringComparison.InvariantCultureIgnoreCase))
                        {
                            result.Add(new CandidateInfo
                            {
                                Tab = tab,
                                Ticker = ticker,
                                RegistryDate = TimeServer
                            });

                            break;
                        }
                    }
                }

                if (result.Count > 0)
                {
                    SendNewLogMessage($"Tester fallback: {result.Count} fallback dividend candidates used", LogMessageType.System);
                }
            }

            return result;
        }

        private bool IsPriceMatchSmaFilter(BotTabSimple tab)
        {
            if (_smaFilterMode.ValueString == "Off")
            {
                return true;
            }

            if (tab == null || tab.CandlesAll == null || tab.CandlesAll.Count == 0)
            {
                return false;
            }

            Aindicator sma = tab.Indicators[0] as Aindicator;

            if (sma == null
                || sma.DataSeries == null
                || sma.DataSeries.Count == 0
                || sma.DataSeries[0].Values == null
                || sma.DataSeries[0].Values.Count == 0)
            {
                return false;
            }

            decimal lastSma = sma.DataSeries[0].Values[sma.DataSeries[0].Values.Count - 1];
            decimal lastClose = tab.CandlesAll[tab.CandlesAll.Count - 1].Close;

            if (lastSma == 0)
            {
                return false;
            }

            if (_smaFilterMode.ValueString == "OnBuyBelowSma")
            {
                return lastClose < lastSma;
            }

            return lastClose > lastSma;
        }

        private void CloseAllStockPositions()
        {
            List<BotTabSimple> tabs = _tabScreenerStocks.Tabs;

            for (int i = 0; tabs != null && i < tabs.Count; i++)
            {
                BotTabSimple tab = tabs[i];

                if (tab == null)
                {
                    continue;
                }

                List<Position> positions = tab.PositionsOpenAll;

                for (int i2 = 0; i2 < positions.Count; i2++)
                {
                    tab.CloseAtMarket(positions[i2], positions[i2].OpenVolume);
                }
            }
        }

        private void CloseGoldPosition()
        {
            if (_tabGold == null)
            {
                return;
            }

            List<Position> positions = _tabGold.PositionsOpenAll;

            for (int i = 0; i < positions.Count; i++)
            {
                _tabGold.CloseAtMarket(positions[i], positions[i].OpenVolume);
            }
        }

        private void EntryInPosition(BotTabSimple tab, decimal targetMoney)
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

            decimal volumeToBuy = CalculateVolumeForMoney(tab, targetMoney, price);

            if (volumeToBuy <= 0)
            {
                return;
            }

            tab.BuyAtMarket(volumeToBuy);
        }

        #endregion

        #region Helpers

        private void LogDetailed(string message)
        {
            if (_detailedLoggingEnabled.ValueString == "On")
            {
                SendNewLogMessage(message, LogMessageType.System);
            }
        }

        private bool IsRebalanceTime(DateTime serverTime, Candle candle, BotTabSimple tab)
        {
            if (candle == null)
            {
                return false;
            }

            DateTime realTime = candle.TimeStart + tab.TimeFrameBuilder.TimeFrameTimeSpan;

            if (realTime.TimeOfDay.Hours != _rebalanceTime.Value.TimeSpan.Hours
               || realTime.TimeOfDay.Minutes != _rebalanceTime.Value.TimeSpan.Minutes)
            {
                return false;
            }

            if (!IsRebalanceDate(serverTime))
            {
                return false;
            }

            if (StartProgram == StartProgram.IsOsTrader
                && _autoUpdateDividends.ValueString == "On")
            {
                if (_dividendsUpdating)
                {
                    SendNewLogMessage("Rebalance postponed: dividends update in progress", LogMessageType.System);
                    return false;
                }
            }

            return true;
        }

        private bool IsRebalanceDate(DateTime dateTime)
        {
            DayOfWeek targetDayOfWeek = ParseDayOfWeek(_rebalanceDayOfWeek.ValueString);

            if (_rebalancePeriod.ValueString == "Monthly")
            {
                DateTime targetDate = GetFirstWeekDayOfMonth(dateTime.Year, dateTime.Month, targetDayOfWeek);
                return dateTime.Date == targetDate.Date;
            }

            return dateTime.DayOfWeek == targetDayOfWeek;
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
            if (_tabGold == null || _tabGold.Portfolio == null)
            {
                return 0m;
            }

            decimal capital = _tabGold.Portfolio.ValueCurrent;

            if (capital == 0m)
            {
                capital = _tabGold.Portfolio.ValueBegin;
            }

            return capital;
        }

        private decimal CalculateVolumeForMoney(BotTabSimple tab, decimal money, decimal price)
        {
            if (tab == null || tab.Security == null)
            {
                return 0m;
            }

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

        #endregion

        #region Types

        private class CandidateInfo
        {
            public BotTabSimple Tab;
            public string Ticker;
            public DateTime RegistryDate;
        }

        #endregion
    }
}
