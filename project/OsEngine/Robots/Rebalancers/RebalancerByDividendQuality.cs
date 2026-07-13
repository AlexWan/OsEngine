/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_transaction.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers.Optimizer;
using OsEngine.OsTrader;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Wiki;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

/* Description
Робот еженедельно переключает капитал между акциями с близкими дивидендами и LQDT.
Если в скринере есть акции, по которым дата Т-1 ближайшего дивиденда находится 
в пределах 7 дней от текущей даты, весь капитал входит в эти акции равномерно.
Если таких акций нет - весь капитал переходит в LQDT.
Только лонг, без плеча.
*/

namespace OsEngine.Robots.Rebalancers
{
    [Bot("RebalancerByDividendQuality")]
    public class RebalancerByDividendQuality : BotPanel
    {
        #region Fields

        private BotTabScreener _tabScreenerStocks;
        private BotTabSimple _tabLqdt;

        private StrategyParameterString _regime;
        private StrategyParameterString _rebalanceDayOfWeek;
        private StrategyParameterTimeOfDay _rebalanceTime;
        private StrategyParameterInt _lookaheadDays;
        private StrategyParameterDecimal _minDividendPercent;
        private StrategyParameterDecimal _maxLqdtDepositPercent;
        private StrategyParameterDecimal _maxStocksDepositPercent;

        private StrategyParameterString _autoUpdateDividends;
        private StrategyParameterTimeOfDay _dividendsUpdateCheckTime;
        private StrategyParameterInt _dividendsMaxAgeDays;
        private StrategyParameterButton _startUpdateDividendsButton;
        private StrategyParameterButton _rebalanceNowButton;

        private DateTime _lastRebalanceDate = DateTime.MinValue;
        private DateTime _lastDividendsUpdateCheckDate = DateTime.MinValue;

        private bool _dividendsUpdating = false;

        #endregion

        #region Constructor

        public RebalancerByDividendQuality(string name, StartProgram startProgram) : base(name, startProgram)
        {
            _regime = CreateParameter("Regime", "Off", new[] { "On", "Off" }, "Base");
            _rebalanceDayOfWeek = CreateParameter("Rebalance day of week", "Monday", new[] { "Monday", "Tuesday", "Wednesday" }, "Base");
            _rebalanceTime = CreateParameterTimeOfDay("Rebalance time", 11, 0, 0, 0, "Base");
            _lookaheadDays = CreateParameter("Lookahead days", 7, 1, 30, 1, "Base");
            _minDividendPercent = CreateParameter("Min dividend %", 0.8m, 0.0m, 50.0m, 0.5m, "Base");
            _maxLqdtDepositPercent = CreateParameter("Max LQDT deposit percent", 49.0m, 10.0m, 100.0m, 1.0m, "Base");
            _maxStocksDepositPercent = CreateParameter("Max stocks deposit percent", 50.0m, 10.0m, 100.0m, 1.0m, "Base");

            _autoUpdateDividends = CreateParameter("Auto update dividends", "On", new[] { "On", "Off" }, "Update");
            _dividendsUpdateCheckTime = CreateParameterTimeOfDay("Dividends update check time", 8, 0, 0, 0, "Update");
            _dividendsMaxAgeDays = CreateParameter("Dividends max age days", 5, 1, 30, 1, "Update");
            _startUpdateDividendsButton = CreateParameterButton("Start update dividends", "Update");
            _startUpdateDividendsButton.UserClickOnButtonEvent += StartUpdateDividendsButton_UserClickOnButtonEvent;

            _rebalanceNowButton = CreateParameterButton("Rebalance NOW", "Base");
            _rebalanceNowButton.UserClickOnButtonEvent += RebalanceNowButton_UserClickOnButtonEvent;

            _tabScreenerStocks = TabCreate<BotTabScreener>();
            _tabLqdt = TabCreate<BotTabSimple>();

            _tabScreenerStocks.CandlesSyncFinishedEvent += _tabScreenerStocks_CandlesSyncFinishedEvent;
            _tabScreenerStocks.CandleFinishedEvent += _tabScreenerStocks_CandleFinishedEvent;

            Description = OsLocalization.ConvertToLocString(
                "Eng:Weekly rebalancer between dividend stocks and LQDT_" +
                "Ru:Еженедельный ребалансировщик между дивидендными акциями и LQDT_");
        }

        #endregion

        #region Events

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
                    ExecuteRebalance(serverTime);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
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

                DayOfWeek targetDay = ParseDayOfWeek(_rebalanceDayOfWeek.ValueString);

                if (serverTime.DayOfWeek != targetDay)
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

                return ageDays > _dividendsMaxAgeDays.ValueInt;
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

        #region Rebalance logic

        private void ExecuteRebalance(DateTime serverTime)
        {
            try
            {
                _lastRebalanceDate = serverTime;

                SendNewLogMessage("Rebalance started", LogMessageType.System);

                List<CandidateInfo> candidates = GetCandidates();

                if (candidates.Count == 0)
                {
                    CloseAllStockPositions();
                    TryResetLqdtByYear();

                    if (_tabLqdt.PositionsOpenAll.Count == 0)
                    {
                        decimal capital = GetCurrentCapital();
                        decimal targetCapital = capital * _maxLqdtDepositPercent.ValueDecimal / 100m;

                        decimal freeMoney = GetFreeMoney();

                        decimal availableCapital = targetCapital;
                        
                        if(StartProgram == StartProgram.IsOsTrader)
                        {
                            availableCapital = Math.Min(targetCapital, freeMoney);
                        }

                        EntryInPosition(_tabLqdt, availableCapital);
                    }

                    return;
                }

                CloseLqdtPosition();
                CloseAllStockPositions();

                decimal stockCapital = GetCurrentCapital();
                decimal availableStockCapital = stockCapital * _maxStocksDepositPercent.ValueDecimal / 100m;
                decimal moneyOnOneStock = availableStockCapital / candidates.Count;

                for (int i = 0; i < candidates.Count; i++)
                {
                    EntryInPosition(candidates[i].Tab, moneyOnOneStock);
                }

                SendNewLogMessage("Rebalance finished", LogMessageType.System);
            }
            catch (Exception error)
            {
                SendNewLogMessage($"Rebalance error: {error}", LogMessageType.Error);
            }
        }

        private void RebalanceNowButton_UserClickOnButtonEvent()
        {
            try
            {
                SendNewLogMessage("Manual rebalance requested", LogMessageType.System);
                ExecuteRebalance(TimeServer);
            }
            catch (Exception error)
            {
                SendNewLogMessage($"Manual rebalance error: {error}", LogMessageType.Error);
            }
        }

        private void TryResetLqdtByYear()
        {
            if (_tabLqdt == null)
            {
                return;
            }

            List<Position> positions = _tabLqdt.PositionsOpenAll;

            if (positions.Count == 0)
            {
                return;
            }

            Position lqdtPosition = positions[0];

            if (TimeServer.Year <= lqdtPosition.TimeOpen.Year)
            {
                return;
            }

            decimal capital = GetCurrentCapital();
            decimal availableCapital = capital * _maxLqdtDepositPercent.ValueDecimal / 100m;

            _tabLqdt.CloseAtMarket(lqdtPosition, lqdtPosition.OpenVolume);
            EntryInPosition(_tabLqdt, availableCapital);
        }

        private List<CandidateInfo> GetCandidates()
        {
            List<CandidateInfo> result = new List<CandidateInfo>();

            List<BotTabSimple> tabs = _tabScreenerStocks.Tabs;
            DateTime currentTime = TimeServer;
            DateTime maxDate = currentTime.AddDays(_lookaheadDays.ValueInt);

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

                if (future.future.dividend_yield < _minDividendPercent.ValueDecimal)
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

        private void CloseLqdtPosition()
        {
            if (_tabLqdt == null)
            {
                return;
            }

            List<Position> positions = _tabLqdt.PositionsOpenAll;

            for (int i = 0; i < positions.Count; i++)
            {
                _tabLqdt.CloseAtMarket(positions[i], positions[i].OpenVolume);
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

            decimal volumeToBuy = CalculateVolumeForMoney(tab, targetMoney);

            if (volumeToBuy <= 0)
            {
                return;
            }

            tab.BuyAtMarket(volumeToBuy);
        }

        private decimal GetFreeMoney()
        {
            try
            {
                if (_tabLqdt.Portfolio == null)
                {
                    return 0m;
                }

                if (_tabLqdt.Connector.MyServer.ServerType == ServerType.TInvest)
                {
                    List<PositionOnBoard> positions = _tabLqdt.Portfolio.GetPositionOnBoard();

                    for (int i = 0; i < positions.Count; i++)
                    {
                        if (positions[i].SecurityNameCode == "rub")
                        {
                            return positions[i].ValueCurrent;
                        }
                    }
                }
                else if (_tabLqdt.Connector.MyServer.ServerType == ServerType.Tester)
                {
                    decimal portfolioValue = _tabLqdt.Portfolio.ValueCurrent;
                    decimal volumeToPositions = GetVolumeToPositions();
                    return portfolioValue - volumeToPositions;
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage("GetFreeMoney: " + ex.Message, LogMessageType.Error);
            }

            return 0m;
        }

        private decimal GetVolumeToPositions()
        {
            decimal volumeToPosition = 0m;

            for (int i = 0; i < OsTraderMaster.Master.PanelsArray.Count; i++)
            {
                List<Position> positionsBot = OsTraderMaster.Master.PanelsArray[i].OpenPositions;

                for (int j = 0; j < positionsBot.Count; j++)
                {
                    decimal margin = GetMarginSecurities(positionsBot[j].SecurityName, positionsBot[j].Direction);

                    if (margin > 1)
                    {
                        volumeToPosition += positionsBot[j].OpenVolume * margin * positionsBot[j].Lots;
                    }
                    else if (margin <= 1 && _tabLqdt.Connector.MyServer.ServerType == ServerType.Tester)
                    {
                        volumeToPosition += positionsBot[j].OpenVolume * positionsBot[j].EntryPrice * positionsBot[j].Lots;
                    }
                }
            }

            return volumeToPosition;
        }

        private decimal GetMarginSecurities(string nameSecurity, Side side)
        {
            List<Security> securities = _tabLqdt.Connector.MyServer.Securities;

            for (int i = 0; i < securities.Count; i++)
            {
                if (securities[i].Name == nameSecurity)
                {
                    return side == Side.Buy ? securities[i].MarginBuy : securities[i].MarginSell;
                }
            }

            return 0m;
        }

        #endregion

        #region Helpers

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

            DayOfWeek targetDay = ParseDayOfWeek(_rebalanceDayOfWeek.ValueString);

            if (serverTime.DayOfWeek != targetDay)
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
            if (_tabLqdt == null || _tabLqdt.Portfolio == null)
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

        #endregion

        #region Types

        private class CandidateInfo
        {
            public BotTabSimple Tab;
            public string Ticker;
            public DateTime RegistryDate;
        }

        #endregion

        #region Override

        public override string GetNameStrategyType()
        {
            return "RebalancerByDividendQuality";
        }

        public override void ShowIndividualSettingsDialog()
        {
        }

        #endregion
    }
}
