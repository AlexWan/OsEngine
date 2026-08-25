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
    /// Test for BotTabSimple.CloseAtStopOnServerCancel (cancel of server stop orders of one position / all positions)
    /// </summary>
    public class BotTabSimple_5_Cancel : AServerTester
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
                    "Test B5. Cancelling server stop orders via BotTabSimple methods CloseAtStopOnServerCancel(position) for one position and CloseAtStopOnServerCancel() for all positions on the tab. Limit orders and the positions themselves must not be affected. The test creates its own BotTabSimple tab and deletes it at the end.\n\n" +
                    "Parameters\n" +
                    "Portfolio - account used for trading.\n" +
                    "Sec name - security name.\n" +
                    "Sec class - security class.\n" +
                    "Volume - trade volume of each position.\n" +
                    "Far offset ticks - offset for far orders that must not trigger, in ticks.\n" +
                    "Cleanup timeout sec - max time for teardown (cancel orders, close positions), seconds.\n\n" +
                    "Checks in order\n" +
                    "1. Open two Long positions at market.\n" +
                    "2. On both positions place a far server StopLimit close order (must not trigger) and a far Limit close order. All four orders become Active.\n" +
                    "3. CloseAtStopOnServerCancel(posA). The stop of posA goes to Cancel, the Limit order of posA is still Active, the stop of posB is still Active, the position posA is not affected.\n" +
                    "4. CloseAtStopOnServerCancel(). The stops of both positions go to Cancel, both Limit orders are still Active, the positions are not affected.\n" +
                    "If the server does not support server stop orders, the test is skipped. At the end all test orders are cancelled and positions are closed at market. If the account is not back to its initial state, the test fails with Cleanup FAIL. Detailed log is in the folder Engine\\WServerTester\\<TestName>\\.";

                string messageRu =
                    "Тест B5. Отмена серверных стоп-ордеров через методы BotTabSimple CloseAtStopOnServerCancel(position) для одной позиции и CloseAtStopOnServerCancel() для всех позиций вкладки. Лимитные ордера и сами позиции затронуты быть не должны. Тест создаёт собственную вкладку BotTabSimple и удаляет её в конце.\n\n" +
                    "Параметры\n" +
                    "Portfolio - счёт для торговли.\n" +
                    "Sec name - имя бумаги.\n" +
                    "Sec class - класс бумаги.\n" +
                    "Volume - торговый объём каждой позиции.\n" +
                    "Far offset ticks - отступ для далёких ордеров, которые не должны сработать, в тиках.\n" +
                    "Cleanup timeout sec - максимальное время на зачистку (отмена ордеров, закрытие позиций), секунды.\n\n" +
                    "Проверки по порядку\n" +
                    "1. Открыть две Long позиции по рынку.\n" +
                    "2. На обеих позициях выставить далёкий серверный StopLimit на закрытие (не должен сработать) и далёкий Limit ордер на закрытие. Все четыре ордера переходят в Active.\n" +
                    "3. CloseAtStopOnServerCancel(posA). Стоп posA переходит в Cancel, Limit ордер posA всё ещё Active, стоп posB всё ещё Active, позиция posA не затронута.\n" +
                    "4. CloseAtStopOnServerCancel(). Стопы обеих позиций переходят в Cancel, оба Limit ордера всё ещё Active, позиции не затронуты.\n" +
                    "Если сервер не поддерживает серверные стоп-ордера, тест пропускается. В конце все ордера теста отменяются, позиции закрываются по рынку. Если счёт не вернулся в исходное состояние, тест падает с Cleanup FAIL. Подробный лог в папке Engine\\WServerTester\\<ИмяТеста>\\.";

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

        public int WaitTriggerSeconds = 75;

        public int WaitMyTradeSeconds = 120;

        public int CleanupTimeoutSeconds = 120;

        public int FarOffsetTicks = 100;

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
            Log("TEST START. Methods: CloseAtStopOnServerCancel(position) / CloseAtStopOnServerCancel()");

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

                // Step 1. Open two Long positions at market

                Log("[Step 1/5] Open two Long positions " + VolumeToTrade + " at market");

                Position posA = OpenLongAtMarket("posA");

                if (posA == null)
                {
                    return;
                }

                Position posB = OpenLongAtMarket("posB");

                if (posB == null)
                {
                    return;
                }

                // Step 2. Place far server stops (will not trigger) and far limit orders on both positions

                Log("[Step 2/5] Place far StopLimit close orders and far Limit close orders on both positions");

                Order stopA = PlaceFarStopAndLimit(posA, sec, "posA");

                if (stopA == null)
                {
                    return;
                }

                Order stopB = PlaceFarStopAndLimit(posB, sec, "posB");

                if (stopB == null)
                {
                    return;
                }

                Order limitA = posA.CloseOrders[posA.CloseOrders.Count - 1];
                Order limitB = posB.CloseOrders[posB.CloseOrders.Count - 1];

                if (WaitOrderActive(stopA, "posA stop") == false
                    || WaitOrderActive(stopB, "posB stop") == false
                    || WaitOrderActive(limitA, "posA limit") == false
                    || WaitOrderActive(limitB, "posB limit") == false)
                {
                    SetNewError("Error 20. Not all orders became Active");
                    return;
                }

                DumpPosition(posA, "posA with stop and limit");
                DumpPosition(posB, "posB with stop and limit");

                // Step 3. Cancel server stops of one position

                Log("[Step 3/5] CloseAtStopOnServerCancel(posA)");

                Tab.CloseAtStopOnServerCancel(posA);

                if (WaitFor(() => stopA.State == OrderStateType.Cancel, WaitActiveSeconds,
                    "posA stop wait Cancel") == false)
                {
                    SetNewError("Error 30. posA stop order was not cancelled by CloseAtStopOnServerCancel(position)");
                    return;
                }

                if (limitA.State != OrderStateType.Active)
                {
                    SetNewError("Error 31. posA LIMIT order was cancelled too. Only server stops must be cancelled. State: "
                        + limitA.State);
                    return;
                }

                if (stopB.State != OrderStateType.Active)
                {
                    SetNewError("Error 32. posB stop order was cancelled by CloseAtStopOnServerCancel(posA). "
                        + "Only posA stops must be cancelled. State: " + stopB.State);
                    return;
                }

                if (posA.State != PositionStateType.Open
                    || posA.OpenVolume != VolumeToTrade)
                {
                    SetNewError("Error 33. posA was affected by stops cancel. State: " + posA.State
                        + " OpenVolume: " + posA.OpenVolume);
                    return;
                }

                Log("CloseAtStopOnServerCancel(posA) OK. posA stop Cancel, posA limit Active, posB stop Active");

                SetNewServiceInfo("CloseAtStopOnServerCancel(position): checks passed");

                // Step 4. Cancel server stops of all positions on the tab

                Log("[Step 4/5] CloseAtStopOnServerCancel() - all positions");

                Tab.CloseAtStopOnServerCancel();

                if (WaitFor(() => stopB.State == OrderStateType.Cancel, WaitActiveSeconds,
                    "posB stop wait Cancel") == false)
                {
                    SetNewError("Error 40. posB stop order was not cancelled by CloseAtStopOnServerCancel()");
                    return;
                }

                if (limitA.State != OrderStateType.Active
                    || limitB.State != OrderStateType.Active)
                {
                    SetNewError("Error 41. LIMIT orders were cancelled by CloseAtStopOnServerCancel(). "
                        + "Only server stops must be cancelled. limitA: " + limitA.State
                        + " limitB: " + limitB.State);
                    return;
                }

                if (posA.OpenVolume != VolumeToTrade
                    || posB.OpenVolume != VolumeToTrade)
                {
                    SetNewError("Error 42. Positions were affected by stops cancel. posA vol: "
                        + posA.OpenVolume + " posB vol: " + posB.OpenVolume);
                    return;
                }

                DumpPosition(posA, "posA after global cancel");
                DumpPosition(posB, "posB after global cancel");

                Log("CloseAtStopOnServerCancel() OK. Both stops Cancel, both limits Active");

                SetNewServiceInfo("CloseAtStopOnServerCancel(): checks passed");

                // Step 5. Final

                Log("[Step 5/5] All scenarios passed");
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

        private Position OpenLongAtMarket(string what)
        {
            Position pos = Tab.BuyAtMarket(VolumeToTrade);

            if (pos == null)
            {
                SetNewError("Error 50. BuyAtMarket returned null. " + what);
                return null;
            }

            TrackPosition(pos);

            if (WaitPositionOpen(pos, what + " open at market") == false)
            {
                SetNewError("Error 51. Position was not opened at market. " + what);
                return null;
            }

            DumpPosition(pos, what + " opened at market");

            return pos;
        }

        /// <summary>
        /// Place a far server StopLimit close order (will not trigger) and a far Limit close order.
        /// Returns the stop order; the limit order is the last one in CloseOrders
        /// </summary>
        private Order PlaceFarStopAndLimit(Position pos, Security sec, string what)
        {
            if (WaitBestBidAsk(what + " place far orders") == false)
            {
                SetNewError("Error 60. No best bid/ask. " + what);
                return null;
            }

            decimal activation = Math.Round(Tab.PriceBestBid - sec.PriceStep * FarOffsetTicks, sec.Decimals);
            decimal priceOrder = Math.Round(activation - sec.PriceStep * 2, sec.Decimals);
            decimal limitPrice = Math.Round(Tab.PriceBestAsk + sec.PriceStep * FarOffsetTicks, sec.Decimals);

            int closeOrdersBefore = pos.CloseOrders.Count;

            Tab.CloseAtStopOnServer(pos, activation, priceOrder);

            Thread.Sleep(2000);

            if (pos.CloseOrders.Count != closeOrdersBefore + 1)
            {
                SetNewError("Error 61. Far stop order was not created. " + what);
                return null;
            }

            Order stopOrder = pos.CloseOrders[pos.CloseOrders.Count - 1];
            TrackOrder(stopOrder);

            if (stopOrder.TypeOrder != OrderPriceType.StopLimit
                || stopOrder.PositionConditionType != OrderPositionConditionType.Close
                || stopOrder.IsStopOrProfit == false)
            {
                SetNewError("Error 62. Far stop order fields are wrong. " + OrderToString(stopOrder));
                return null;
            }

            Tab.CloseAtLimit(pos, limitPrice, VolumeToTrade);

            Thread.Sleep(2000);

            if (pos.CloseOrders.Count != closeOrdersBefore + 2)
            {
                SetNewError("Error 63. Far limit order was not created. " + what);
                return null;
            }

            Order limitOrder = pos.CloseOrders[pos.CloseOrders.Count - 1];
            TrackOrder(limitOrder);

            if (limitOrder.TypeOrder != OrderPriceType.Limit)
            {
                SetNewError("Error 64. Far limit order type is not Limit. Real: " + limitOrder.TypeOrder);
                return null;
            }

            Log(what + " far orders placed. Stop: " + OrderToString(stopOrder)
                + " | Limit: " + OrderToString(limitOrder));

            return stopOrder;
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
