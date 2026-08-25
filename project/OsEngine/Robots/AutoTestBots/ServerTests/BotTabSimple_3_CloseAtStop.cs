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
    /// Test for BotTabSimple.CloseAtStopOnServer / CloseAtStopMarketOnServer (server stop position closing)
    /// </summary>
    public class BotTabSimple_3_CloseAtStop : AServerTester
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
                    "Test B3. Closing long positions by server stop orders via BotTabSimple methods CloseAtStopOnServer (StopLimit) and CloseAtStopMarketOnServer (StopMarket). Full close, partial close with a signal, volume clamp and protective no-op branches. The test creates its own BotTabSimple tab and deletes it at the end.\n\n" +
                    "Parameters\n" +
                    "Portfolio - account used for trading.\n" +
                    "Sec name - security name.\n" +
                    "Sec class - security class.\n" +
                    "Volume - base trade volume.\n" +
                    "Attempts - how many times the stop is re-placed if it was rejected or not triggered.\n" +
                    "Cleanup timeout sec - max time for teardown (cancel orders, close positions), seconds.\n\n" +
                    "Checks in order\n" +
                    "1. Protective branches. Null position must not create orders.\n" +
                    "2. Open Long at market and fully close it by a server StopLimit. Close order checks - type StopLimit, side Sell, condition Close, IsStopOrProfit flag, StopPrice, Price, Volume. Then wait for Active, wait for the trigger, check the child Limit order and parent/child links.\n" +
                    "3. The position is fully closed, profit is calculated. A repeated close call on the Done position is a no-op, no new orders.\n" +
                    "4. Open Long with double volume at market. Close with zero volume is a no-op, no new orders.\n" +
                    "5. Partial close of one volume with signalType. SignalTypeStop equals the signal, the remaining volume is correct.\n" +
                    "6. Close with a volume much bigger than the open one. The volume is clamped, the position is fully closed.\n" +
                    "7. Open Long at market and fully close it by a server StopMarket. The same checks with a child Market order.\n" +
                    "8. If a closing stop is not triggered in time, it is cancelled and re-placed with fresh prices.\n" +
                    "If the server does not support server stop orders, the test is skipped. At the end all test orders are cancelled and positions are closed at market. If the account is not back to its initial state, the test fails with Cleanup FAIL. Detailed log is in the folder Engine\\WServerTester\\<TestName>\\.";

                string messageRu =
                    "Тест B3. Закрытие длинных позиций серверными стоп-ордерами через методы BotTabSimple CloseAtStopOnServer (StopLimit) и CloseAtStopMarketOnServer (StopMarket). Полное закрытие, частичное закрытие с сигналом, обрезка объёма и защитные no-op ветки. Тест создаёт собственную вкладку BotTabSimple и удаляет её в конце.\n\n" +
                    "Параметры\n" +
                    "Portfolio - счёт для торговли.\n" +
                    "Sec name - имя бумаги.\n" +
                    "Sec class - класс бумаги.\n" +
                    "Volume - базовый торговый объём.\n" +
                    "Attempts - сколько раз стоп перевыставляется, если он отклонён или не сработал.\n" +
                    "Cleanup timeout sec - максимальное время на зачистку (отмена ордеров, закрытие позиций), секунды.\n\n" +
                    "Проверки по порядку\n" +
                    "1. Защитные ветки. Null позиция не должна создавать ордера.\n" +
                    "2. Открыть Long по рынку и полностью закрыть его серверным StopLimit. Проверки закрывающего ордера - тип StopLimit, сторона Sell, условие Close, флаг IsStopOrProfit, StopPrice, Price, объём. Затем ожидание Active, ожидание срабатывания, проверка дочернего Limit ордера и связей parent/child.\n" +
                    "3. Позиция полностью закрыта, профит посчитан. Повторный вызов закрытия на Done позиции - no-op, новых ордеров нет.\n" +
                    "4. Открыть Long двойным объёмом по рынку. Закрытие нулевым объёмом - no-op, новых ордеров нет.\n" +
                    "5. Частичное закрытие одного объёма с signalType. SignalTypeStop равен сигналу, остаток объёма корректен.\n" +
                    "6. Закрытие объёмом намного больше открытого. Объём обрезается, позиция закрыта полностью.\n" +
                    "7. Открыть Long по рынку и полностью закрыть его серверным StopMarket. Те же проверки с дочерним Market ордером.\n" +
                    "8. Если закрывающий стоп не сработал вовремя, он отменяется и перевыставляется по свежим ценам.\n" +
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
            Log("TEST START. Methods: CloseAtStopOnServer / CloseAtStopMarketOnServer (stop close)");

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

                Tab.CloseAtStopOnServer(null, 1, 1);
                Tab.CloseAtStopOnServer(null, 1, 1, VolumeToTrade);
                Tab.CloseAtStopMarketOnServer(null, 1);

                Thread.Sleep(2000);

                if (Tab.PositionsAll.Count != 0)
                {
                    SetNewError("Error 10. Protective branches created a position. Count: "
                        + Tab.PositionsAll.Count);
                    return;
                }

                Log("Protective branches OK. No positions, no orders created");

                // Step 2. Full close by StopLimit

                Log("[Step 2/5] Open Long " + VolumeToTrade + " and close it by CloseAtStopOnServer (StopLimit)");

                Position pos1 = OpenLongAtMarket(VolumeToTrade, "pos1");

                if (pos1 == null)
                {
                    return;
                }

                if (CloseLongWithRetry(pos1, sec, false, null, null, "pos1 StopLimit full close") == false)
                {
                    return;
                }

                if (pos1.OpenVolume != 0)
                {
                    SetNewError("Error 20. pos1 OpenVolume is not zero after close. Real: " + pos1.OpenVolume);
                    return;
                }

                DumpPosition(pos1, "pos1 closed by StopLimit");

                Log("pos1 ProfitAbs: " + pos1.ProfitOperationAbs
                    + " Profit%: " + pos1.ProfitOperationPercent);

                // close on Done position - no-op branch

                int closeOrdersCount = pos1.CloseOrders.Count;

                Tab.CloseAtStopOnServer(pos1, 1, 1);

                Thread.Sleep(2000);

                if (pos1.CloseOrders.Count != closeOrdersCount)
                {
                    SetNewError("Error 21. CloseAtStopOnServer on Done position created a new close order");
                    return;
                }

                Log("CloseAtStopOnServer on Done position: no-op OK");

                SetNewServiceInfo("CloseAtStopOnServer full close: checks passed");

                // Step 3. Partial close with signal, then volume clamp

                Log("[Step 3/5] Open Long " + (VolumeToTrade * 2) + ". Partial close with signal, then clamp");

                Position pos2 = OpenLongAtMarket(VolumeToTrade * 2, "pos2");

                if (pos2 == null)
                {
                    return;
                }

                // zero volume - no-op branch

                int closeOrdersCount2 = pos2.CloseOrders.Count;

                Tab.CloseAtStopOnServer(pos2, 1, 1, 0m);

                Thread.Sleep(2000);

                if (pos2.CloseOrders.Count != closeOrdersCount2)
                {
                    SetNewError("Error 22. CloseAtStopOnServer with zero volume created a close order");
                    return;
                }

                Log("CloseAtStopOnServer with zero volume: no-op OK");

                // partial close of VolumeToTrade with signal

                if (CloseLongWithRetry(pos2, sec, false, VolumeToTrade, "TestSignalClose",
                    "pos2 StopLimit partial close with signal") == false)
                {
                    return;
                }

                if (pos2.SignalTypeStop != "TestSignalClose")
                {
                    SetNewError("Error 23. pos2 SignalTypeStop is wrong. Expected: 'TestSignalClose"
                        + "' Real: '" + pos2.SignalTypeStop + "'");
                    return;
                }

                if (pos2.OpenVolume != VolumeToTrade)
                {
                    SetNewError("Error 24. pos2 OpenVolume after partial close is wrong. Expected: "
                        + VolumeToTrade + " Real: " + pos2.OpenVolume);
                    return;
                }

                Log("pos2 partial close OK. Remaining OpenVolume: " + pos2.OpenVolume
                    + " SignalTypeStop: '" + pos2.SignalTypeStop + "'");

                SetNewServiceInfo("CloseAtStopOnServer partial close with signal: checks passed");

                // clamp: requested volume is much bigger than OpenVolume

                if (CloseLongWithRetry(pos2, sec, false, VolumeToTrade * 100, null,
                    "pos2 StopLimit close with volume clamp") == false)
                {
                    return;
                }

                if (pos2.OpenVolume != 0)
                {
                    SetNewError("Error 25. pos2 OpenVolume is not zero after clamp close. Real: " + pos2.OpenVolume);
                    return;
                }

                SetNewServiceInfo("CloseAtStopOnServer volume clamp: checks passed");

                // Step 4. Full close by StopMarket

                Log("[Step 4/5] Open Long " + VolumeToTrade + " and close it by CloseAtStopMarketOnServer");

                Position pos3 = OpenLongAtMarket(VolumeToTrade, "pos3");

                if (pos3 == null)
                {
                    return;
                }

                if (CloseLongWithRetry(pos3, sec, true, null, null, "pos3 StopMarket full close") == false)
                {
                    return;
                }

                if (pos3.OpenVolume != 0)
                {
                    SetNewError("Error 26. pos3 OpenVolume is not zero after StopMarket close. Real: " + pos3.OpenVolume);
                    return;
                }

                DumpPosition(pos3, "pos3 closed by StopMarket");

                SetNewServiceInfo("CloseAtStopMarketOnServer full close: checks passed");

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

        private Position OpenLongAtMarket(decimal volume, string what)
        {
            Position pos = Tab.BuyAtMarket(volume);

            if (pos == null)
            {
                SetNewError("Error 40. BuyAtMarket returned null. " + what);
                return null;
            }

            TrackPosition(pos);

            if (WaitPositionOpen(pos, what + " open at market") == false)
            {
                SetNewError("Error 41. Position was not opened at market. " + what);
                return null;
            }

            if (pos.OpenVolume != volume)
            {
                SetNewError("Error 42. Position OpenVolume is wrong. Expected: " + volume
                    + " Real: " + pos.OpenVolume + ". " + what);
                return null;
            }

            DumpPosition(pos, what + " opened at market");

            return pos;
        }

        /// <summary>
        /// Place a closing server stop on a long position and wait for the trigger.
        /// If the stop is not triggered in time - cancel and re-place with fresh prices
        /// </summary>
        private bool CloseLongWithRetry(Position pos, Security sec, bool stopMarket,
            decimal? volume, string signal, string what)
        {
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                if (WaitBestBidAsk(what + " attempt " + (attempt + 1)) == false)
                {
                    continue;
                }

                decimal activation = Tab.RoundPrice(Tab.PriceBestBid * (1m - OffsetPercent / 100m), sec, Side.Sell);
                decimal priceOrder = Tab.RoundPrice(activation * (1m - OffsetPercent / 100m), sec, Side.Sell);

                int closeOrdersBefore = pos.CloseOrders.Count;
                decimal openVolumeAtPlace = pos.OpenVolume;

                decimal expectedVolume = volume == null
                    ? openVolumeAtPlace
                    : Math.Min(volume.Value, openVolumeAtPlace);

                Log(what + ". Attempt " + (attempt + 1)
                    + ". " + (stopMarket ? "StopMarket" : "StopLimit")
                    + " Activation: " + activation
                    + (stopMarket ? "" : " PriceOrder: " + priceOrder)
                    + " Volume request: " + (volume == null ? "full" : volume.Value.ToString())
                    + " Expected order volume: " + expectedVolume
                    + (signal == null ? "" : " Signal: '" + signal + "'"));

                if (stopMarket)
                {
                    Tab.CloseAtStopMarketOnServer(pos, activation);
                }
                else if (volume == null)
                {
                    Tab.CloseAtStopOnServer(pos, activation, priceOrder);
                }
                else if (signal == null)
                {
                    Tab.CloseAtStopOnServer(pos, activation, priceOrder, volume.Value);
                }
                else
                {
                    Tab.CloseAtStopOnServer(pos, activation, priceOrder, volume.Value, signal);
                }

                Thread.Sleep(2000);

                if (pos.CloseOrders.Count == closeOrdersBefore)
                {
                    SetNewError("Error 50. No close order created. " + what);
                    return false;
                }

                // find the close stop we just placed: if it triggered during the pause,
                // a child order may already be the last one in CloseOrders

                OrderPriceType waitType = stopMarket ? OrderPriceType.StopMarket : OrderPriceType.StopLimit;

                Order order = null;

                for (int i = closeOrdersBefore; i < pos.CloseOrders.Count; i++)
                {
                    if (pos.CloseOrders[i].TypeOrder == waitType
                        && pos.CloseOrders[i].IsStopOrProfit == true)
                    {
                        order = pos.CloseOrders[i];
                        break;
                    }
                }

                if (order == null)
                {
                    SetNewError("Error 63. No close stop order found after placement. " + what);
                    return false;
                }

                TrackOrder(order);
                DumpPosition(pos, what + " after close stop placed");

                // close order validation

                if (order.TypeOrder != waitType)
                {
                    SetNewError("Error 51. Close order type is not " + waitType
                        + ". Real: " + order.TypeOrder + ". " + what);
                    return false;
                }

                if (order.Side != Side.Sell)
                {
                    SetNewError("Error 52. Close order side is not Sell (close of Long). Real: "
                        + order.Side + ". " + what);
                    return false;
                }

                if (order.PositionConditionType != OrderPositionConditionType.Close)
                {
                    SetNewError("Error 53. Close order PositionConditionType is not Close. Real: "
                        + order.PositionConditionType + ". " + what);
                    return false;
                }

                if (order.IsStopOrProfit == false)
                {
                    SetNewError("Error 54. Close order IsStopOrProfit == false. " + what);
                    return false;
                }

                decimal expectedStop = Tab.RoundPrice(activation, sec, Side.Sell);

                if (order.StopPrice != expectedStop)
                {
                    SetNewError("Error 55. Close order StopPrice is wrong. Expected: " + expectedStop
                        + " Real: " + order.StopPrice + ". " + what);
                    return false;
                }

                decimal expectedPrice = stopMarket
                    ? expectedStop
                    : Tab.RoundPrice(priceOrder, sec, Side.Sell);

                if (order.Price != expectedPrice)
                {
                    SetNewError("Error 56. Close order Price is wrong. Expected: " + expectedPrice
                        + " Real: " + order.Price + ". " + what);
                    return false;
                }

                if (order.Volume != expectedVolume)
                {
                    SetNewError("Error 57. Close order Volume is wrong. Expected: " + expectedVolume
                        + " Real: " + order.Volume + ". " + what);
                    return false;
                }

                if (signal != null
                    && pos.SignalTypeStop != signal)
                {
                    SetNewError("Error 58. Position SignalTypeStop is wrong. Expected: '" + signal
                        + "' Real: '" + pos.SignalTypeStop + "'. " + what);
                    return false;
                }

                // wait Active (skip if the stop already triggered during the pause after placement)

                if (order.State == OrderStateType.Cancel)
                {
                    Log(what + " close stop triggered during the pause after placement. Already Cancel");
                }
                else if (WaitOrderActive(order, what) == false)
                {
                    SetNewError("Error 59. No Active state from close stop order. " + what);
                    return false;
                }

                if (order.State == OrderStateType.Fail)
                {
                    Log(what + " close stop was rejected. Re-place with fresh prices");
                    continue;
                }

                if (OrderIsNormal(order, Side.Sell, waitType) == false)
                {
                    return false;
                }

                // wait trigger

                decimal volumeBeforeTrigger = pos.OpenVolume;

                bool triggered = WaitFor(() =>
                    pos.OpenVolume < volumeBeforeTrigger
                    || pos.State == PositionStateType.Done,
                    WaitTriggerSeconds, what + " wait trigger");

                if (triggered == false)
                {
                    Log(what + " stop was not triggered in time. Cancel and re-place");

                    Tab.CloseAllOrderToPosition(pos);
                    WaitFor(() => AnyTrackedOrderActive() == false, WaitActiveSeconds,
                        what + " wait cancel before re-place");
                    continue;
                }

                // stop triggered: it is Cancel now and a child order was spawned

                if (CheckStopTriggerWithChild(order, Side.Sell,
                    stopMarket ? OrderPriceType.Market : OrderPriceType.Limit, what) == false)
                {
                    return false;
                }

                // check executed volume. The child order may fill partially: wait for the full close volume

                if (WaitFor(() => volumeBeforeTrigger - pos.OpenVolume >= expectedVolume,
                    WaitMyTradeSeconds, what + " wait full close volume") == false)
                {
                    SetNewError("Error 60. Executed close volume is wrong. Expected: " + expectedVolume
                        + " Real: " + (volumeBeforeTrigger - pos.OpenVolume) + ". " + what);
                    return false;
                }

                // wait MyTrades (linked to the stop or to its child orders)

                if (WaitFor(() => MyTradesVolumeForStopWithChildren(order) >= expectedVolume,
                    WaitMyTradeSeconds, what + " wait MyTrades") == false)
                {
                    SetNewError("Error 61. No MyTrades on close volume. Stop order: "
                        + order.NumberMarket + ". " + what);
                    return false;
                }

                for (int i = 0; i < _myTrades.Count; i++)
                {
                    if (MyTradeBelongsToStop(_myTrades[i], order)
                        && MyTradeIsNormal(_myTrades[i], Side.Sell) == false)
                    {
                        return false;
                    }
                }

                DumpPosition(pos, what + " triggered");

                return true;
            }

            SetNewError("Error 62. " + what + ". No triggered close stop after " + Attempts + " attempts");
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
