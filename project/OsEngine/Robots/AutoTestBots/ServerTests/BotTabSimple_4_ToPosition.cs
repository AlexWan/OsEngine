/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace OsEngine.Robots.AutoTestBots.ServerTests
{
    /// <summary>
    /// Test for BotTabSimple.BuyAtStopOnServerToPosition / BuyAtStopMarketOnServerToPosition
    /// (adding volume to a position by server stop orders)
    /// </summary>
    public class BotTabSimple_4_ToPosition : AServerTester
    {
        #region Test description (shown from WServerTester)

        /// <summary>
        /// Detailed description of the test. Shown from the WServerTester panel
        /// </summary>
        public static string TestDescription
        {
            get
            {
                string messageEng =
                    "Test B4. Adding volume to an open long position by server stop orders via BotTabSimple methods BuyAtStopOnServerToPosition and BuyAtStopMarketOnServerToPosition. Also checks the side mismatch rejection and protective branches. The test creates its own BotTabSimple tab and deletes it at the end.\n\n" +
                    "Parameters\n" +
                    "Portfolio - account used for trading.\n" +
                    "Sec name - security name.\n" +
                    "Sec class - security class.\n" +
                    "Volume - volume of one doline.\n" +
                    "Attempts - how many times the stop is re-placed if it was rejected or not triggered.\n" +
                    "Cleanup timeout sec - max time for teardown (cancel orders, close position), seconds.\n\n" +
                    "Checks in order\n" +
                    "1. Protective branches. Null position must not create orders.\n" +
                    "2. Open Long at market, the volume is correct.\n" +
                    "3. Side mismatch. SellAtStopOnServerToPosition on a Long position is rejected, no new orders.\n" +
                    "4. Doline by StopLimit. A new stop order in the position - type StopLimit, side Buy, condition Open, IsStopOrProfit flag, StopPrice, Price, Volume.\n" +
                    "5. The stop becomes Active, then wait for the trigger. The stop goes to Cancel, a child Limit order appears with parent/child links. OpenVolume grows exactly by Volume, MyTrades arrive on the doline volume.\n" +
                    "6. Doline by StopMarket. The same checks with a child Market order.\n" +
                    "7. If a doline stop is not triggered in time, it is cancelled and re-placed with fresh prices.\n" +
                    "If the server does not support server stop orders, the test is skipped. At the end all test orders are cancelled and the position is closed at market. If the account is not back to its initial state, the test fails with Cleanup FAIL. Detailed log is in the folder Engine\\WServerTester\\<TestName>\\.";

                string messageRu =
                    "Тест B4. Доливка объёма в открытую длинную позицию серверными стоп-ордерами через методы BotTabSimple BuyAtStopOnServerToPosition и BuyAtStopMarketOnServerToPosition. Также проверяются отклонение при несовпадении стороны и защитные ветки. Тест создаёт собственную вкладку BotTabSimple и удаляет её в конце.\n\n" +
                    "Параметры\n" +
                    "Portfolio - счёт для торговли.\n" +
                    "Sec name - имя бумаги.\n" +
                    "Sec class - класс бумаги.\n" +
                    "Volume - объём одной доливки.\n" +
                    "Attempts - сколько раз стоп перевыставляется, если он отклонён или не сработал.\n" +
                    "Cleanup timeout sec - максимальное время на зачистку (отмена ордеров, закрытие позиции), секунды.\n\n" +
                    "Проверки по порядку\n" +
                    "1. Защитные ветки. Null позиция не должна создавать ордера.\n" +
                    "2. Открыть Long по рынку, объём корректен.\n" +
                    "3. Несовпадение стороны. SellAtStopOnServerToPosition на Long позиции отклоняется, новых ордеров нет.\n" +
                    "4. Доливка StopLimit. Новый стоп-ордер в позиции - тип StopLimit, сторона Buy, условие Open, флаг IsStopOrProfit, StopPrice, Price, объём.\n" +
                    "5. Стоп переходит в Active, затем ожидание срабатывания. Стоп переходит в Cancel, появляется дочерний Limit ордер со связями parent/child. OpenVolume вырастает ровно на Volume, приходят MyTrades на объём доливки.\n" +
                    "6. Доливка StopMarket. Те же проверки с дочерним Market ордером.\n" +
                    "7. Если стоп доливки не сработал вовремя, он отменяется и перевыставляется по свежим ценам.\n" +
                    "Если сервер не поддерживает серверные стоп-ордера, тест пропускается. В конце все ордера теста отменяются, позиция закрывается по рынку. Если счёт не вернулся в исходное состояние, тест падает с Cleanup FAIL. Подробный лог в папке Engine\\WServerTester\\<ИмяТеста>\\.";

                return OsLocalization.ConvertToLocString(
                    "Eng:" + messageEng + "_Ru:" + messageRu + "_");
            }
        }

        #endregion

        #region Settings (filled by WServerTester)

        public BotTabSimple Tab;

        public StartProgram StartProgram;

        public string SecurityNameToTrade = "ETHUSDT";

        public string SecurityClassToTrade = "Futures";

        public string PortfolioName = "BinanceFutures";

        public decimal VolumeToTrade = 0.01m;

        public int WaitActiveSeconds = 60;

        public int WaitTriggerSeconds = 60;

        public int WaitMyTradeSeconds = 120;

        public int CleanupTimeoutSeconds = 120;

        // Offset from the best bid/ask for orders that must execute, in percent
        private const decimal OffsetPercent = 0.05m;

        public int Attempts = 4;

        #endregion

        #region Tab creation and disposal

        private bool CreateTab()
        {
            try
            {
                string tabName = GetType().Name + "_"
                    + DateTime.Now.ToString("yyyyMMdd_HHmmss");

                Tab = new BotTabSimple(tabName, StartProgram);
                Tab.LogMessageEvent += Tab_LogMessageEvent;

                Tab.Connector.ServerType = Server.ServerType;
                Tab.Connector.ServerFullName = Server.ServerNameAndPrefix;
                Tab.Connector.PortfolioName = PortfolioName;
                Tab.Connector.SecurityClass = SecurityClassToTrade;
                Tab.Connector.SecurityName = SecurityNameToTrade;

                Tab.Connector.ReconnectHard();

                Log("Waiting tab connector. Sec: " + SecurityNameToTrade
                    + " Class: " + SecurityClassToTrade + " Portfolio: " + PortfolioName);

                if (WaitFor(() =>
                        Tab.Connector.IsConnected
                        && Tab.Connector.IsReadyToTrade
                        && Tab.Connector.Security != null,
                        180, "wait tab connector ready") == false)
                {
                    SetNewError("Error 7. Tab connector is not ready for test. Timeout 3 minutes");
                    return false;
                }

                Log("Tab connector is ready. Tab: " + tabName);

                return true;
            }
            catch (Exception e)
            {
                SetNewError("Error 7. Tab creation failed. " + e.ToString());
                return false;
            }
        }

        private void DeleteTab()
        {
            try
            {
                if (Tab != null)
                {
                    Tab.LogMessageEvent -= Tab_LogMessageEvent;
                    Tab.Delete();
                    Tab = null;
                }
            }
            catch (Exception e)
            {
                Log("DeleteTab error: " + e.Message);
            }
        }

        private void Tab_LogMessageEvent(string message, LogMessageType messageType)
        {
            Log("TAB. " + messageType + ". " + message);

            if (messageType == LogMessageType.Error)
            {
                SendLogMessage(GetType().Name + " | TAB. " + message, LogMessageType.Error);
            }
        }

        #endregion

        #region Test logic

        public override void Process()
        {
            InitFileLog();
            Log("TEST START. Methods: Buy/SellAtStopOnServerToPosition, Buy/SellAtStopMarketOnServerToPosition");

            try
            {
                if (CreateTab() == false)
                {
                    return;
                }

                if (CheckCommonConditions() == false)
                {
                    return;
                }

                SubscribeEvents();
                SnapshotInitialState();

                Security sec = Tab.Connector.Security;

                // Step 1. Protective branches. No real trading

                Log("[Step 1/5] Protective branches: null position");

                Tab.BuyAtStopOnServerToPosition(null, VolumeToTrade, 1, 1);
                Tab.SellAtStopOnServerToPosition(null, VolumeToTrade, 1, 1);
                Tab.BuyAtStopMarketOnServerToPosition(null, VolumeToTrade, 1);
                Tab.SellAtStopMarketOnServerToPosition(null, VolumeToTrade, 1);

                Thread.Sleep(2000);

                if (Tab.PositionsAll.Count != 0)
                {
                    SetNewError("Error 10. Protective branches created a position. Count: "
                        + Tab.PositionsAll.Count);
                    return;
                }

                Log("Protective branches OK. No positions, no orders created");

                // Step 2. Open Long at market

                Log("[Step 2/5] Open Long " + VolumeToTrade + " at market");

                Position pos = Tab.BuyAtMarket(VolumeToTrade);

                if (pos == null)
                {
                    SetNewError("Error 20. BuyAtMarket returned null");
                    return;
                }

                TrackPosition(pos);

                if (WaitPositionOpen(pos, "open at market") == false)
                {
                    SetNewError("Error 21. Position was not opened at market");
                    return;
                }

                if (pos.OpenVolume != VolumeToTrade)
                {
                    SetNewError("Error 22. Position OpenVolume is wrong. Expected: " + VolumeToTrade
                        + " Real: " + pos.OpenVolume);
                    return;
                }

                DumpPosition(pos, "opened at market");

                // Step 3. Side mismatch branch: Sell stop to a Long position

                Log("[Step 3/5] Side mismatch: SellAtStopOnServerToPosition to a Long position");

                int openOrdersCount = pos.OpenOrders.Count;

                decimal mismatchPrice = Tab.RoundPrice(Tab.PriceBestBid * (1m - OffsetPercent / 100m), sec, Side.Sell);
                decimal mismatchActivation = Tab.RoundPrice(mismatchPrice * (1m - OffsetPercent / 100m), sec, Side.Sell);

                Tab.SellAtStopOnServerToPosition(pos, VolumeToTrade, mismatchPrice, mismatchActivation);

                Thread.Sleep(2000);

                if (pos.OpenOrders.Count != openOrdersCount)
                {
                    SetNewError("Error 30. SellAtStopOnServerToPosition to a Long position created an order. "
                        + "Side mismatch branch is broken");
                    return;
                }

                Log("Side mismatch OK. Order was rejected, no new orders in position");

                SetNewServiceInfo("ToPosition side mismatch: checks passed");

                // Step 4. Doline by StopLimit

                Log("[Step 4/5] BuyAtStopOnServerToPosition (StopLimit doline)");

                if (DolineWithRetry(pos, sec, false, "StopLimit doline") == false)
                {
                    return;
                }

                SetNewServiceInfo("BuyAtStopOnServerToPosition: doline executed, checks passed");

                // Step 5. Doline by StopMarket

                Log("[Step 5/5] BuyAtStopMarketOnServerToPosition (StopMarket doline)");

                if (DolineWithRetry(pos, sec, true, "StopMarket doline") == false)
                {
                    return;
                }

                SetNewServiceInfo("BuyAtStopMarketOnServerToPosition: doline executed, checks passed");
            }
            finally
            {
                Teardown();
                UnsubscribeEvents();
                DeleteTab();
                Log("TEST END. " + (_errors.Count == 0 ? "STATUS: OK" : "STATUS: FAIL"));
                CloseFileLog();
                TestEnded();
            }
        }

        private bool DolineWithRetry(Position pos, Security sec, bool stopMarket, string what)
        {
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                if (WaitBestBidAsk(what + " attempt " + (attempt + 1)) == false)
                {
                    continue;
                }

                decimal activation = Tab.RoundPrice(Tab.PriceBestAsk * (1m + OffsetPercent / 100m), sec, Side.Buy);
                decimal limit = Tab.RoundPrice(activation * (1m + OffsetPercent / 100m), sec, Side.Buy);

                int openOrdersBefore = pos.OpenOrders.Count;
                decimal volumeBefore = pos.OpenVolume;
                decimal entryPriceBefore = pos.EntryPrice;

                Log(what + ". Attempt " + (attempt + 1)
                    + ". " + (stopMarket ? "StopMarket" : "StopLimit")
                    + " Activation: " + activation
                    + (stopMarket ? "" : " Limit: " + limit)
                    + " Volume: " + VolumeToTrade
                    + " OpenVolume before: " + volumeBefore);

                if (stopMarket)
                {
                    Tab.BuyAtStopMarketOnServerToPosition(pos, VolumeToTrade, activation);
                }
                else
                {
                    Tab.BuyAtStopOnServerToPosition(pos, VolumeToTrade, limit, activation);
                }

                Thread.Sleep(2000);

                if (pos.OpenOrders.Count == openOrdersBefore)
                {
                    SetNewError("Error 40. No doline order created. " + what);
                    return false;
                }

                // find the doline stop we just placed: if it triggered during the pause,
                // a child order may already be the last one in OpenOrders

                OrderPriceType waitType = stopMarket ? OrderPriceType.StopMarket : OrderPriceType.StopLimit;

                Order order = null;

                for (int i = openOrdersBefore; i < pos.OpenOrders.Count; i++)
                {
                    if (pos.OpenOrders[i].TypeOrder == waitType
                        && pos.OpenOrders[i].IsStopOrProfit == true)
                    {
                        order = pos.OpenOrders[i];
                        break;
                    }
                }

                if (order == null)
                {
                    SetNewError("Error 52. No doline stop order found after placement. " + what);
                    return false;
                }

                TrackOrder(order);
                DumpPosition(pos, what + " after doline stop placed");

                // doline order validation

                if (order.TypeOrder != waitType)
                {
                    SetNewError("Error 41. Doline order type is not " + waitType
                        + ". Real: " + order.TypeOrder + ". " + what);
                    return false;
                }

                if (order.Side != Side.Buy)
                {
                    SetNewError("Error 42. Doline order side is not Buy (direction of the position). Real: "
                        + order.Side + ". " + what);
                    return false;
                }

                if (order.PositionConditionType != OrderPositionConditionType.Open)
                {
                    SetNewError("Error 43. Doline order PositionConditionType is not Open. Real: "
                        + order.PositionConditionType + ". " + what);
                    return false;
                }

                if (order.IsStopOrProfit == false)
                {
                    SetNewError("Error 44. Doline order IsStopOrProfit == false. " + what);
                    return false;
                }

                decimal expectedStop = Tab.RoundPrice(activation, sec, Side.Buy);

                if (order.StopPrice != expectedStop)
                {
                    SetNewError("Error 45. Doline order StopPrice is wrong. Expected: " + expectedStop
                        + " Real: " + order.StopPrice + ". " + what);
                    return false;
                }

                decimal expectedPrice = stopMarket
                    ? expectedStop
                    : Tab.RoundPrice(limit, sec, Side.Buy);

                if (order.Price != expectedPrice)
                {
                    SetNewError("Error 46. Doline order Price is wrong. Expected: " + expectedPrice
                        + " Real: " + order.Price + ". " + what);
                    return false;
                }

                if (order.Volume != VolumeToTrade)
                {
                    SetNewError("Error 47. Doline order Volume is wrong. Expected: " + VolumeToTrade
                        + " Real: " + order.Volume + ". " + what);
                    return false;
                }

                // wait Active (skip if the stop already triggered during the pause after placement)

                if (order.State == OrderStateType.Cancel)
                {
                    Log(what + " doline stop triggered during the pause after placement. Already Cancel");
                }
                else if (WaitOrderActive(order, what) == false)
                {
                    SetNewError("Error 48. No Active state from doline order. " + what);
                    return false;
                }

                if (order.State == OrderStateType.Fail)
                {
                    Log(what + " doline stop was rejected. Re-place with fresh prices");
                    continue;
                }

                if (OrderIsNormal(order, Side.Buy, waitType) == false)
                {
                    return false;
                }

                // wait trigger

                bool triggered = WaitFor(() => pos.OpenVolume > volumeBefore,
                    WaitTriggerSeconds, what + " wait trigger");

                if (triggered == false)
                {
                    Log(what + " doline stop was not triggered in time. Cancel and re-place");

                    Tab.CloseAllOrderToPosition(pos);
                    WaitFor(() => AnyTrackedOrderActive() == false, WaitActiveSeconds,
                        what + " wait cancel before re-place");
                    continue;
                }

                // stop triggered: it is Cancel now and a child order was spawned

                if (CheckStopTriggerWithChild(order, Side.Buy,
                    stopMarket ? OrderPriceType.Market : OrderPriceType.Limit, what) == false)
                {
                    return false;
                }

                // the child order may fill partially: wait for the full doline volume

                if (WaitFor(() => pos.OpenVolume >= volumeBefore + VolumeToTrade,
                    WaitMyTradeSeconds, what + " wait full OpenVolume") == false)
                {
                    SetNewError("Error 49. OpenVolume after doline is wrong. Expected: "
                        + (volumeBefore + VolumeToTrade) + " Real: " + pos.OpenVolume + ". " + what);
                    return false;
                }

                if (pos.EntryPrice != entryPriceBefore)
                {
                    Log(what + " EntryPrice recalculated: " + entryPriceBefore
                        + " -> " + pos.EntryPrice);
                }
                else
                {
                    Log(what + " EntryPrice was not changed: " + pos.EntryPrice
                        + " (possible if doline price equals average)");
                }

                // wait MyTrades (linked to the stop or to its child orders)

                if (WaitFor(() => MyTradesVolumeForStopWithChildren(order) >= VolumeToTrade,
                    WaitMyTradeSeconds, what + " wait MyTrades") == false)
                {
                    SetNewError("Error 50. No MyTrades on doline volume. Stop order: "
                        + order.NumberMarket + ". " + what);
                    return false;
                }

                for (int i = 0; i < _myTrades.Count; i++)
                {
                    if (MyTradeBelongsToStop(_myTrades[i], order)
                        && MyTradeIsNormal(_myTrades[i], Side.Buy) == false)
                    {
                        return false;
                    }
                }

                DumpPosition(pos, what + " triggered");

                return true;
            }

            SetNewError("Error 51. " + what + ". No triggered doline stop after " + Attempts + " attempts");
            return false;
        }

        #endregion

        #region File log

        private StreamWriter _fileLog;

        private readonly object _fileLogLocker = new object();

        private string _fileLogPath;

        private void InitFileLog()
        {
            try
            {
                string testName = GetType().Name;

                string dir = @"Engine\WServerTester\" + testName;

                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                _fileLogPath = dir + @"\" + testName + "_"
                    + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff") + ".txt";

                _fileLog = new StreamWriter(_fileLogPath, false);
                _fileLog.AutoFlush = true;

                SetNewServiceInfo("Log file: " + _fileLogPath);
            }
            catch (Exception e)
            {
                SetNewError("Can not create log file. " + e.ToString());
            }
        }

        private void CloseFileLog()
        {
            lock (_fileLogLocker)
            {
                if (_fileLog != null)
                {
                    try { _fileLog.Close(); } catch { }
                    _fileLog = null;
                }
            }
        }

        private void Log(string message)
        {
            string line = DateTime.Now.ToString("HH:mm:ss.fff") + "  " + message;

            lock (_fileLogLocker)
            {
                if (_fileLog != null)
                {
                    try { _fileLog.WriteLine(line); } catch { }
                }
            }

            SendLogMessage(GetType().Name + " | " + line, LogMessageType.System);
        }

        #endregion

        #region Tracking. Positions and orders created by the test

        private readonly List<Position> _trackedPositions = new List<Position>();

        private readonly List<Order> _trackedOrders = new List<Order>();

        private readonly HashSet<string> _initialActiveOrders = new HashSet<string>();

        private void SnapshotInitialState()
        {
            _initialActiveOrders.Clear();

            List<Order> active = null;

            try { active = Server.GetActiveOrders(); }
            catch (Exception e) { Log("Snapshot. GetActiveOrders error: " + e.Message); }

            if (active != null)
            {
                for (int i = 0; i < active.Count; i++)
                {
                    if (string.IsNullOrEmpty(active[i].NumberMarket) == false)
                    {
                        _initialActiveOrders.Add(active[i].NumberMarket);
                    }
                }
            }

            Log("Snapshot initial state. Active orders on server: " + _initialActiveOrders.Count);
        }

        private void TrackPosition(Position pos)
        {
            if (pos == null)
            {
                return;
            }

            for (int i = 0; i < _trackedPositions.Count; i++)
            {
                if (_trackedPositions[i].Number == pos.Number)
                {
                    return;
                }
            }

            _trackedPositions.Add(pos);

            TrackOrdersOfPosition(pos);
        }

        private void TrackOrdersOfPosition(Position pos)
        {
            if (pos == null)
            {
                return;
            }

            if (pos.OpenOrders != null)
            {
                for (int i = 0; i < pos.OpenOrders.Count; i++)
                {
                    TrackOrder(pos.OpenOrders[i]);
                }
            }

            if (pos.CloseOrders != null)
            {
                for (int i = 0; i < pos.CloseOrders.Count; i++)
                {
                    TrackOrder(pos.CloseOrders[i]);
                }
            }
        }

        private void TrackOrder(Order order)
        {
            if (order == null)
            {
                return;
            }

            for (int i = 0; i < _trackedOrders.Count; i++)
            {
                if (_trackedOrders[i].NumberUser == order.NumberUser)
                {
                    return;
                }
            }

            _trackedOrders.Add(order);
        }

        private bool AnyTrackedOrderActive()
        {
            for (int i = 0; i < _trackedPositions.Count; i++)
            {
                TrackOrdersOfPosition(_trackedPositions[i]);
            }

            for (int i = 0; i < _trackedOrders.Count; i++)
            {
                Order order = _trackedOrders[i];

                if (order.State == OrderStateType.Active
                    || order.State == OrderStateType.Pending
                    || order.State == OrderStateType.Partial)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Teardown. Return the account to its initial state

        private void Teardown()
        {
            DateTime teardownEnd = DateTime.Now.AddSeconds(CleanupTimeoutSeconds);

            Log("=== TEARDOWN START ===");

            // Step 1. Cancel all active orders of tracked positions

            try
            {
                for (int i = 0; i < _trackedPositions.Count; i++)
                {
                    Position pos = _trackedPositions[i];

                    TrackOrdersOfPosition(pos);

                    try
                    {
                        if (PositionHasActiveOrders(pos))
                        {
                            Log("Teardown. Cancel all orders to position #" + pos.Number);
                            Tab.CloseAllOrderToPosition(pos);
                        }
                    }
                    catch (Exception e)
                    {
                        Log("Teardown. Cancel orders error. Pos #" + pos.Number + " " + e.Message);
                    }
                }

                WaitFor(() => AnyTrackedOrderActive() == false,
                    SecondsLeft(teardownEnd), "Teardown. Wait orders cancelled");
            }
            catch (Exception e)
            {
                Log("Teardown. Step 1 critical error: " + e.Message);
            }

            // Step 2. Close open positions at market

            for (int i = 0; i < _trackedPositions.Count; i++)
            {
                Position pos = _trackedPositions[i];

                int attempt = 0;

                while (pos.OpenVolume > 0
                    && pos.State != PositionStateType.Done
                    && attempt < 3
                    && DateTime.Now < teardownEnd)
                {
                    attempt++;

                    try
                    {
                        Log("Teardown. Close at market. Pos #" + pos.Number
                            + " volume: " + pos.OpenVolume + " attempt " + attempt);

                        Tab.CloseAtMarket(pos, pos.OpenVolume);
                    }
                    catch (Exception e)
                    {
                        Log("Teardown. CloseAtMarket error. Pos #" + pos.Number + " " + e.Message);
                    }

                    WaitFor(() => pos.OpenVolume == 0 || pos.State == PositionStateType.Done,
                        SecondsLeft(teardownEnd) / 3 + 1, "Teardown. Wait position closed #" + pos.Number);
                }
            }

            // Step 3. Verify the account is in its initial state

            bool cleanupOk = true;

            for (int i = 0; i < _trackedPositions.Count; i++)
            {
                Position pos = _trackedPositions[i];

                if (pos.OpenVolume != 0)
                {
                    cleanupOk = false;
                    Log("Teardown verify FAIL. Pos #" + pos.Number
                        + " still has OpenVolume: " + pos.OpenVolume + " State: " + pos.State);
                }
            }

            if (AnyTrackedOrderActive())
            {
                cleanupOk = false;
                Log("Teardown verify FAIL. There are still active tracked orders");
            }

            try
            {
                List<Order> activeNow = Server.GetActiveOrders();

                if (activeNow != null)
                {
                    for (int i = 0; i < activeNow.Count; i++)
                    {
                        Order order = activeNow[i];

                        if (order.SecurityNameCode != SecurityNameToTrade)
                        {
                            continue;
                        }

                        if (string.IsNullOrEmpty(order.NumberMarket) == false
                            && _initialActiveOrders.Contains(order.NumberMarket) == false)
                        {
                            cleanupOk = false;
                            Log("Teardown verify FAIL. Leftover order on server: "
                                + order.NumberMarket + " " + order.Side + " " + order.State);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log("Teardown verify. GetActiveOrders error: " + e.Message);
            }

            if (cleanupOk)
            {
                Log("Cleanup: OK");
                SetNewServiceInfo("Cleanup: OK");
            }
            else
            {
                Log("Cleanup: FAIL");
                SetNewServiceInfo("Cleanup: FAIL");
                SetNewError("Cleanup failed. The account is NOT in its initial state. See log file");
            }

            Log("=== TEARDOWN END ===");
        }

        private bool PositionHasActiveOrders(Position pos)
        {
            if (pos.OpenOrders != null)
            {
                for (int i = 0; i < pos.OpenOrders.Count; i++)
                {
                    Order order = pos.OpenOrders[i];

                    if (order.State == OrderStateType.Active
                        || order.State == OrderStateType.Pending
                        || order.State == OrderStateType.Partial)
                    {
                        return true;
                    }
                }
            }

            if (pos.CloseOrders != null)
            {
                for (int i = 0; i < pos.CloseOrders.Count; i++)
                {
                    Order order = pos.CloseOrders[i];

                    if (order.State == OrderStateType.Active
                        || order.State == OrderStateType.Pending
                        || order.State == OrderStateType.Partial)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private int SecondsLeft(DateTime end)
        {
            int seconds = (int)(end - DateTime.Now).TotalSeconds;

            if (seconds < 1)
            {
                seconds = 1;
            }

            return seconds;
        }

        #endregion

        #region Wait helpers

        private bool WaitFor(Func<bool> condition, int seconds, string what)
        {
            DateTime start = DateTime.Now;
            DateTime end = start.AddSeconds(seconds);

            while (DateTime.Now < end)
            {
                bool done = false;

                try { done = condition(); }
                catch { }

                if (done)
                {
                    Log(what + " - DONE in " + (DateTime.Now - start).TotalSeconds.ToString("F1") + " sec");
                    return true;
                }

                Thread.Sleep(500);
            }

            Log(what + " - TIMEOUT after " + seconds + " sec");
            return false;
        }

        private bool WaitOrderActive(Order order, string what)
        {
            return WaitFor(() =>
                order.State == OrderStateType.Active
                || order.State == OrderStateType.Done
                || order.State == OrderStateType.Partial
                || order.State == OrderStateType.Fail
                || order.State == OrderStateType.Cancel,
                WaitActiveSeconds, what + " wait Active");
        }

        private bool WaitPositionOpen(Position pos, string what)
        {
            return WaitFor(() =>
                pos.State == PositionStateType.Open,
                WaitTriggerSeconds, what + " wait position Open");
        }

        private bool WaitPositionDone(Position pos, string what)
        {
            return WaitFor(() =>
                pos.State == PositionStateType.Done,
                WaitTriggerSeconds, what + " wait position Done");
        }

        private bool WaitBestBidAsk(string what)
        {
            return WaitFor(() =>
                Tab.PriceBestAsk > 0 && Tab.PriceBestBid > 0,
                WaitActiveSeconds, what + " wait best bid/ask");
        }

        #endregion

        #region Tab and Server events

        private void SubscribeEvents()
        {
            Tab.PositionOpeningSuccesEvent += Tab_PositionOpeningSuccesEvent;
            Tab.PositionClosingSuccesEvent += Tab_PositionClosingSuccesEvent;
            Tab.PositionOpeningFailEvent += Tab_PositionOpeningFailEvent;
            Tab.PositionClosingFailEvent += Tab_PositionClosingFailEvent;

            Server.NewOrderIncomeEvent += Server_NewOrderIncomeEvent;
            Server.NewMyTradeEvent += Server_NewMyTradeEvent;
        }

        private void UnsubscribeEvents()
        {
            Tab.PositionOpeningSuccesEvent -= Tab_PositionOpeningSuccesEvent;
            Tab.PositionClosingSuccesEvent -= Tab_PositionClosingSuccesEvent;
            Tab.PositionOpeningFailEvent -= Tab_PositionOpeningFailEvent;
            Tab.PositionClosingFailEvent -= Tab_PositionClosingFailEvent;

            Server.NewOrderIncomeEvent -= Server_NewOrderIncomeEvent;
            Server.NewMyTradeEvent -= Server_NewMyTradeEvent;
        }

        private void Tab_PositionOpeningSuccesEvent(Position pos)
        {
            Log("EVENT PositionOpeningSucces. Pos #" + pos.Number
                + " " + pos.Direction + " OpenVolume: " + pos.OpenVolume);
            TrackPosition(pos);
        }

        private void Tab_PositionClosingSuccesEvent(Position pos)
        {
            Log("EVENT PositionClosingSucces. Pos #" + pos.Number
                + " " + pos.Direction + " State: " + pos.State);
            TrackPosition(pos);
        }

        private void Tab_PositionOpeningFailEvent(Position pos)
        {
            Log("EVENT PositionOpeningFail. Pos #" + pos.Number
                + " " + pos.Direction + " State: " + pos.State);
            TrackPosition(pos);
        }

        private void Tab_PositionClosingFailEvent(Position pos)
        {
            Log("EVENT PositionClosingFail. Pos #" + pos.Number
                + " " + pos.Direction + " State: " + pos.State);
            TrackPosition(pos);
        }

        private void Server_NewOrderIncomeEvent(Order order)
        {
            if (order.SecurityNameCode != SecurityNameToTrade)
            {
                return;
            }

            _incomeOrders.Add(order);

            Log("ORDER income. NumUser: " + order.NumberUser
                + " NumMarket: " + order.NumberMarket
                + " " + order.Side + " " + order.TypeOrder
                + " State: " + order.State
                + " Price: " + order.Price
                + " StopPrice: " + order.StopPrice
                + " Volume: " + order.Volume
                + " Parent: " + order.ParentOrderNumberMarket
                + " Child: " + order.ChildOrderNumberMarket);
        }

        private readonly List<Order> _incomeOrders = new List<Order>();

        private readonly List<MyTrade> _myTrades = new List<MyTrade>();

        private void Server_NewMyTradeEvent(MyTrade myTrade)
        {
            if (myTrade.SecurityNameCode != SecurityNameToTrade)
            {
                return;
            }

            Log("MYTRADE income. NumTrade: " + myTrade.NumberTrade
                + " ParentOrder: " + myTrade.NumberOrderParent
                + " " + myTrade.Side
                + " Price: " + myTrade.Price
                + " Volume: " + myTrade.Volume);

            _myTrades.Add(myTrade);
        }

        #endregion

        #region Stop trigger with child orders (T-Invest semantics)

        // When a server stop is triggered on T-Invest:
        // 1. The stop order itself goes to Cancel state (StopOrderStatusExecuted -> Cancel by design).
        // 2. The exchange spawns a child order (Limit for StopLimit, Market for StopMarket)
        //    with ParentOrderNumberMarket = stop NumberMarket.
        // 3. The stop order gets ChildOrderNumberMarket = child NumberMarket (best effort, via REST).
        // 4. MyTrades are linked to the CHILD order, not to the stop.

        private List<Order> FindChildOrdersOf(Order stopOrder)
        {
            List<Order> children = new List<Order>();

            if (stopOrder == null
                || string.IsNullOrEmpty(stopOrder.NumberMarket))
            {
                return children;
            }

            for (int i = 0; i < _incomeOrders.Count; i++)
            {
                if (_incomeOrders[i].ParentOrderNumberMarket == stopOrder.NumberMarket)
                {
                    children.Add(_incomeOrders[i]);
                }
            }

            return children;
        }

        /// <summary>
        /// Full verification of a triggered stop order: stop is Cancel,
        /// child order exists and is linked to the stop (ParentOrderNumberMarket),
        /// the stop is linked back to the child (ChildOrderNumberMarket - strict when present)
        /// </summary>
        private bool CheckStopTriggerWithChild(Order stopOrder, Side side,
            OrderPriceType expectedChildType, string what)
        {
            // 1. stop order must be Cancel (triggered semantics)

            if (stopOrder.State != OrderStateType.Cancel)
            {
                SetNewError("Error. " + what
                    + ". Stop order state after trigger is not Cancel. Real: " + stopOrder.State);
                return false;
            }

            Log(what + " stop order is Cancel after trigger (triggered semantics) OK");

            // 2. child order must appear

            if (WaitFor(() => FindChildOrdersOf(stopOrder).Count > 0,
                WaitActiveSeconds, what + " wait child order") == false)
            {
                SetNewError("Error. " + what
                    + ". No child order after stop trigger. Stop: " + stopOrder.NumberMarket);
                return false;
            }

            Order child = FindChildOrdersOf(stopOrder)[0];

            Log(what + " child order: " + OrderToString(child));

            if (child.ParentOrderNumberMarket != stopOrder.NumberMarket)
            {
                SetNewError("Error. " + what
                    + ". Child order ParentOrderNumberMarket is wrong. Expected: "
                    + stopOrder.NumberMarket + " Real: " + child.ParentOrderNumberMarket);
                return false;
            }

            if (child.NumberUser == 0)
            {
                SetNewError("Error. " + what + ". Child order NumberUser is zero");
                return false;
            }

            if (string.IsNullOrEmpty(child.NumberMarket))
            {
                SetNewError("Error. " + what + ". Child order NumberMarket is null or empty");
                return false;
            }

            if (child.SecurityNameCode != stopOrder.SecurityNameCode)
            {
                SetNewError("Error. " + what
                    + ". Child order SecurityNameCode is wrong. Expected: "
                    + stopOrder.SecurityNameCode + " Real: " + child.SecurityNameCode);
                return false;
            }

            if (string.IsNullOrEmpty(child.PortfolioNumber))
            {
                SetNewError("Error. " + what + ". Child order PortfolioNumber is null or empty");
                return false;
            }

            if (child.Side != side)
            {
                SetNewError("Error. " + what
                    + ". Child order side is wrong. Expected: " + side
                    + " Real: " + child.Side);
                return false;
            }

            if (child.Volume != stopOrder.Volume)
            {
                SetNewError("Error. " + what
                    + ". Child order Volume is wrong. Expected: " + stopOrder.Volume
                    + " Real: " + child.Volume);
                return false;
            }

            if (child.TypeOrder != expectedChildType)
            {
                SetNewError("Error. " + what
                    + ". Child order type is wrong. Expected: " + expectedChildType
                    + " Real: " + child.TypeOrder);
                return false;
            }

            // 3. the stop order may reference the child. On T-Invest this is best effort:
            // the Cancel update carries ChildOrderNumberMarket only when the REST query
            // returned the exchange order id in time. Strict when present, note otherwise

            if (WaitFor(() => string.IsNullOrEmpty(stopOrder.ChildOrderNumberMarket) == false,
                10, what + " wait stop ChildOrderNumberMarket"))
            {
                if (stopOrder.ChildOrderNumberMarket != child.NumberMarket)
                {
                    SetNewError("Error. " + what
                        + ". Stop order ChildOrderNumberMarket is wrong. Expected: "
                        + child.NumberMarket + " Real: " + stopOrder.ChildOrderNumberMarket);
                    return false;
                }

                Log(what + " stop ChildOrderNumberMarket -> child OK");
            }
            else
            {
                Log(what + " NOTE. Stop order ChildOrderNumberMarket was not set by the connector. "
                    + "The child->parent link is verified instead");
                SetNewServiceInfo(what
                    + ": stop ChildOrderNumberMarket was not set by the connector "
                    + "(child->parent link verified)");
            }

            Log(what + " parent/child links OK. Stop: " + stopOrder.NumberMarket
                + " Child: " + child.NumberMarket);

            SetNewServiceInfo(what
                + ": stop triggered (Cancel), child order and parent/child links verified");

            return true;
        }

        private bool MyTradeBelongsToStop(MyTrade trade, Order stopOrder)
        {
            if (trade.NumberOrderParent == stopOrder.NumberMarket)
            {
                return true;
            }

            List<Order> children = FindChildOrdersOf(stopOrder);

            for (int i = 0; i < children.Count; i++)
            {
                if (trade.NumberOrderParent == children[i].NumberMarket)
                {
                    return true;
                }
            }

            return false;
        }

        private decimal MyTradesVolumeForStopWithChildren(Order stopOrder)
        {
            decimal volume = 0;

            for (int i = 0; i < _myTrades.Count; i++)
            {
                if (MyTradeBelongsToStop(_myTrades[i], stopOrder))
                {
                    volume += _myTrades[i].Volume;
                }
            }

            return volume;
        }

        #endregion

        #region Dump and validation helpers

        private void DumpPosition(Position pos, string caption)
        {
            if (pos == null)
            {
                Log("DUMP " + caption + ". Position is NULL");
                return;
            }

            TrackOrdersOfPosition(pos);

            string dump = "DUMP " + caption + ". Pos #" + pos.Number
                + " " + pos.Direction
                + " State: " + pos.State
                + " OpenVolume: " + pos.OpenVolume
                + " EntryPrice: " + pos.EntryPrice
                + " SignalOpen: '" + pos.SignalTypeOpen + "'"
                + " SignalStop: '" + pos.SignalTypeStop + "'";

            if (pos.State == PositionStateType.Done)
            {
                dump += " ProfitAbs: " + pos.ProfitOperationAbs
                    + " Profit%: " + pos.ProfitOperationPercent;
            }

            Log(dump);

            if (pos.OpenOrders != null)
            {
                for (int i = 0; i < pos.OpenOrders.Count; i++)
                {
                    Log("  OpenOrder[" + i + "] " + OrderToString(pos.OpenOrders[i]));
                }
            }

            if (pos.CloseOrders != null)
            {
                for (int i = 0; i < pos.CloseOrders.Count; i++)
                {
                    Log("  CloseOrder[" + i + "] " + OrderToString(pos.CloseOrders[i]));
                }
            }
        }

        private string OrderToString(Order order)
        {
            return "NumUser: " + order.NumberUser
                + " NumMarket: " + order.NumberMarket
                + " " + order.Side + " " + order.TypeOrder
                + " State: " + order.State
                + " Price: " + order.Price
                + " StopPrice: " + order.StopPrice
                + " Volume: " + order.Volume
                + " IsStopOrProfit: " + order.IsStopOrProfit
                + " Cond: " + order.PositionConditionType
                + " Parent: " + order.ParentOrderNumberMarket
                + " Child: " + order.ChildOrderNumberMarket;
        }

        private bool OrderIsNormal(Order order, Side waitSide, OrderPriceType waitType)
        {
            if (order.Side != waitSide)
            {
                SetNewError("Error. Order wait side not equal. Wait: " + waitSide
                    + " Side in order: " + order.Side);
                return false;
            }

            if (order.TypeOrder != waitType)
            {
                SetNewError("Error. Order type is not " + waitType + ". Real type: " + order.TypeOrder);
                return false;
            }

            if (order.NumberUser == 0)
            {
                SetNewError("Error. Order NumberUser is zero");
                return false;
            }

            if (order.State != OrderStateType.Fail
                && string.IsNullOrEmpty(order.NumberMarket))
            {
                SetNewError("Error. Order NumberMarket is null or empty");
                return false;
            }

            if (string.IsNullOrEmpty(order.SecurityNameCode))
            {
                SetNewError("Error. Order SecurityNameCode is null or empty");
                return false;
            }

            if (string.IsNullOrEmpty(order.PortfolioNumber))
            {
                SetNewError("Error. Order PortfolioNumber is null or empty");
                return false;
            }

            if (order.Side == Side.None)
            {
                SetNewError("Error. Order Side is None");
                return false;
            }

            if (order.State != OrderStateType.Fail
                && order.StopPrice <= 0)
            {
                SetNewError("Error. Order StopPrice is zero");
                return false;
            }

            if (order.State != OrderStateType.Fail
                && order.State != OrderStateType.Cancel
                && order.Volume <= 0)
            {
                SetNewError("Error. Order Volume is zero");
                return false;
            }

            return true;
        }

        private bool MyTradeIsNormal(MyTrade myTrade, Side waitSide)
        {
            if (myTrade.Side != waitSide)
            {
                SetNewError("Error. MyTrade wait side not equal. Wait: " + waitSide
                    + " Side in trade: " + myTrade.Side);
                return false;
            }

            if (myTrade.Volume <= 0)
            {
                SetNewError("Error. MyTrade Volume is zero");
                return false;
            }

            if (myTrade.Price <= 0)
            {
                SetNewError("Error. MyTrade Price is zero");
                return false;
            }

            if (string.IsNullOrEmpty(myTrade.SecurityNameCode))
            {
                SetNewError("Error. MyTrade SecurityNameCode is null or empty");
                return false;
            }

            if (string.IsNullOrEmpty(myTrade.NumberOrderParent))
            {
                SetNewError("Error. MyTrade NumberOrderParent is null or empty");
                return false;
            }

            if (string.IsNullOrEmpty(myTrade.NumberTrade))
            {
                SetNewError("Error. MyTrade NumberTrade is null or empty");
                return false;
            }

            if (myTrade.Time == DateTime.MinValue)
            {
                SetNewError("Error. MyTrade Time is min value");
                return false;
            }

            return true;
        }

        #endregion

        #region Common checks before start

        private bool CheckCommonConditions()
        {
            IServerPermission permission = ServerMaster.GetServerPermission(Server.ServerType);

            if (permission == null)
            {
                SetNewError("Error 0. No server permission");
                return false;
            }

            if (permission.StopOrdersIsSupport == false)
            {
                SetNewServiceInfo("Server " + Server.ServerType
                    + " does not support server stop orders (StopOrdersIsSupport == false). Test SKIPPED");
                return false;
            }

            if (Server.ServerStatus != ServerConnectStatus.Connect)
            {
                SetNewError("Error 1. Server Status Disconnect");
                return false;
            }

            if (Tab == null)
            {
                SetNewError("Error 2. BotTabSimple tab is null");
                return false;
            }

            if (Tab.Connector.IsConnected == false
                || Tab.Connector.IsReadyToTrade == false)
            {
                SetNewError("Error 3. Tab connector is not ready to trade");
                return false;
            }

            Security sec = Tab.Connector.Security;

            if (sec == null)
            {
                SetNewError("Error 4. No security in tab connector: " + SecurityNameToTrade);
                return false;
            }

            if (VolumeToTrade <= 0)
            {
                SetNewError("Error 5. Volume is zero");
                return false;
            }

            int waitAfterConnect = permission.WaitTimeSecondsAfterFirstStartToSendOrders;

            if (waitAfterConnect > 0)
            {
                Log("Waiting " + (waitAfterConnect + 5) + " sec after server connect before sending orders");
                Thread.Sleep((waitAfterConnect + 5) * 1000);
            }

            return true;
        }

        #endregion
    }
}
