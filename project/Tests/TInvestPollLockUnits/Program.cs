// BUG-0097 — V-2 юниты U-9'/U-11' по договору план-20261109-016 (зона _pollSubscribedSecurities / _pollLocker).
// U-9'  — гонка Find×3: реальные SendOrder/ChangeOrderPrice/SendStopOrder против конкурентных Add/Clear poll-списка.
//         known-seed: засиданный тикер обязан найденным каждым хостом каждый раунд («молчаливый промах» = fail).
// U-11' — Connect/Subscribe/Dispose ×4 + Subscribe-после-Dispose: bookkeeping пуст, unhandled=0,
//         единственный допустимый Error — «токен не задан» (Connect на пустом токене в сеть не идёт).
// Харнесс: UO-инстанс TInvestServerRealization + инжект приватных полей (тот же приём, что у V-1 стенда line 0097).
// gRPC — наследники codegen-клиентов поверх FakeInvoker: PostOrder/ReplaceOrder/PostStopOrder — успешные заглушки, сети нет.
// Пин: sha256 OsEngine.dll — первой строкой отчёта, до ассертов. Прогон: <exe> u9 | u11. Код возврата: 0 GREEN,
// 1 — кейс упал, 2 — fatal/preflight, 3 — unhandled. RED на dll без _pollLocker невозможен: preflight abort (гонка
// unfixed-базы доказана V-1 стендом, здесь кейсы формулируются на shipping-фиксе).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.TInvest;
using Tinkoff.InvestApi.V1;
using Type = System.Type;
using Order = OsEngine.Entity.Order;

static class V2Units
{
    [DllImport("kernel32.dll")] static extern void ExitProcess(uint code);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr w, IntPtr l);

    const int WM_CLOSE = 0x0010;

    static string Mode = "u9";
    static string Dir;
    static string LogPath;
    static string RptPath;
    static StreamWriter Report;
    static readonly object LogLock = new object();

    static int Rounds = 300;

    // ---- оракулы: ЛЮБАЯ FirstChance классифицируется; на green всё должно быть нулём ----
    static long raceIOE, raceAOR, raceIOOR, otherEx;
    static long logsSys, logsErr;
    static long unhandledN;
    static readonly List<string> FirstOther = new List<string>();
    static readonly List<string> FirstErrors = new List<string>();

    // ---- состояние продукта (UO + инжект) ----
    static object inst;
    static Type T;
    static PropertyInfo statusProp;
    static Type statusEnum;
    static object locker;                  // _pollLocker — тот же объект, что берёт продукт
    static List<Security> pollList;        // _pollSubscribedSecurities — тот же список, что у продукта

    static volatile bool anyFail = false;

    static void Log(string s)
    {
        try { lock (LogLock) File.AppendAllText(LogPath, DateTime.Now.ToString("HH:mm:ss.fff ") + s + Environment.NewLine); } catch { }
        try { Console.WriteLine(s); } catch { }
    }

    static void Case(string id, string executed, string expected, string actual, bool pass)
    {
        if (!pass) anyFail = true;
        Report.WriteLine("CASE {0} executed={1} expected={2} actual={3} => {4}", id, executed, expected, actual, pass ? "PASS" : "FAIL");
        Log("CASE " + id + " executed=" + executed + " expected=" + expected + " actual=" + actual + " => " + (pass ? "PASS" : "FAIL"));
    }

    static void Classify(Exception ex)
    {
        string tn = ex.GetType().FullName;
        string m = ex.Message ?? "";
        if (tn == "System.InvalidOperationException" && (m.Contains("modified") || m.Contains("Перечисл") || m.Contains("измен")))
            Interlocked.Increment(ref raceIOE);
        else if (tn == "System.ArgumentOutOfRangeException") Interlocked.Increment(ref raceAOR);
        else if (tn == "System.IndexOutOfRangeException") Interlocked.Increment(ref raceIOOR);
        else
        {
            Interlocked.Increment(ref otherEx);
            lock (FirstOther) if (FirstOther.Count < 6) FirstOther.Add(ex.ToString().Substring(0, Math.Min(400, ex.ToString().Length)));
        }
    }

    static void OnLogMessage(string msg, LogMessageType type)
    {
        if (type == LogMessageType.Error)
        {
            Interlocked.Increment(ref logsErr);
            lock (FirstErrors) if (FirstErrors.Count < 8) FirstErrors.Add(msg.Substring(0, Math.Min(200, msg.Length)));
        }
        else Interlocked.Increment(ref logsSys);
    }

    // ---- grpc-подделки: наследники codegen-клиентов, успех без сети ----
    sealed class FakeInvoker : CallInvoker
    {
        public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host, CallOptions options, TRequest request) => throw new NotSupportedException();
        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host, CallOptions options, TRequest request) => throw new NotSupportedException();
        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host, CallOptions options, TRequest request) => throw new NotSupportedException();
        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host, CallOptions options) => throw new NotSupportedException();
        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host, CallOptions options) => throw new NotSupportedException();
    }

    sealed class FakeOrders : OrdersService.OrdersServiceClient
    {
        static int n;
        public FakeOrders() : base(new FakeInvoker()) { }
        public override PostOrderResponse PostOrder(PostOrderRequest request, Metadata headers = null, DateTime? deadline = null, CancellationToken cancellationToken = default)
            => new PostOrderResponse
            {
                ExecutionReportStatus = OrderExecutionReportStatus.ExecutionReportStatusNew,
                OrderId = request.OrderId,
                ResponseMetadata = new ResponseMetadata { ServerTime = Timestamp.FromDateTime(DateTime.UtcNow) }
            };
        public override PostOrderResponse ReplaceOrder(ReplaceOrderRequest request, Metadata headers = null, DateTime? deadline = null, CancellationToken cancellationToken = default)
            => new PostOrderResponse
            {
                ExecutionReportStatus = OrderExecutionReportStatus.ExecutionReportStatusNew,
                OrderId = request.OrderId + "-r" + Interlocked.Increment(ref n),
                ResponseMetadata = new ResponseMetadata { ServerTime = Timestamp.FromDateTime(DateTime.UtcNow) }
            };

        // #2056: на успехе ChangeOrderPrice работает добивка VerifyReplacedOrderIsClosed → GetOrderState.
        // Терминальный статус: old-заявка «закрыта», CancelOrder не зовём, успешный путь молчит (U9-4 System=0).
        public override OrderState GetOrderState(GetOrderStateRequest request, Metadata headers = null, DateTime? deadline = null, CancellationToken cancellationToken = default)
            => new OrderState
            {
                ExecutionReportStatus = OrderExecutionReportStatus.ExecutionReportStatusCancelled
            };
    }

    sealed class FakeStops : StopOrdersService.StopOrdersServiceClient
    {
        static int n;
        public FakeStops() : base(new FakeInvoker()) { }
        public override PostStopOrderResponse PostStopOrder(PostStopOrderRequest request, Metadata headers = null, DateTime? deadline = null, CancellationToken cancellationToken = default)
            => new PostStopOrderResponse { StopOrderId = "u9-stop-" + Interlocked.Increment(ref n) };
    }

    // ---- reflection-хелперы ----
    static FieldInfo F(string name)
    {
        for (Type t = T; t != null; t = t.BaseType)
        {
            var f = t.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (f != null) return f;
        }
        return null;
    }

    static void Set(string name, object val)
    {
        var f = F(name);
        if (f == null) throw new Exception("field not found: " + name);
        f.SetValue(inst, val);
    }

    static void SetNew(string name) // коллекция/словарь — new по типу поля (SDK-типы не именуем)
    {
        var f = F(name);
        if (f == null) throw new Exception("field not found: " + name);
        f.SetValue(inst, Activator.CreateInstance(f.FieldType));
    }

    static int Count(string name) => ((ICollection)F(name).GetValue(inst)).Count;

    static void SetStatus(string value) => statusProp.SetValue(inst, System.Enum.Parse(statusEnum, value));

    static void Enter() { Monitor.Enter(locker); }
    static void Exit() { Monitor.Exit(locker); }

    static Security Sec(string name) => new Security { Name = name, NameId = "uid" + name, Exchange = "MOEX_TST", SecurityType = SecurityType.Stock };

    public static int Main(string[] args)
    {
        CultureInfo inv = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = inv; CultureInfo.CurrentUICulture = inv;
        CultureInfo.DefaultThreadCurrentCulture = inv; CultureInfo.DefaultThreadCurrentUICulture = inv;

        if (args.Length > 0) Mode = args[0];
        foreach (var a in args.Skip(1))
        {
            var kv = a.Split('=');
            if (kv.Length == 2 && kv[0] == "--rounds") Rounds = int.Parse(kv[1]);
        }
        Dir = AppContext.BaseDirectory;
        LogPath = Path.Combine(Dir, "units-log-" + Mode + ".txt");
        File.AppendAllText(LogPath, Environment.NewLine + "==== run mode=" + Mode + " " + DateTime.Now + " rounds=" + Rounds + " ====" + Environment.NewLine);
        RptPath = Path.Combine(Dir, "units-report-" + Mode + ".txt");
        Report = new StreamWriter(RptPath, false) { AutoFlush = true };

        try { Run(); }
        catch (Exception ex)
        {
            Log("FATAL " + ex);
            Report.WriteLine("FATAL " + ex);
            ExitProcess(2);
        }
        Report.Flush();
        ExitProcess(anyFail ? 1u : 0u);
        return 0;
    }

    static void Run()
    {
        // watchdog модальных окон (#32770): стенд не должен ждать клика
        new Thread(() => {
            for (; ; )
            {
                try
                {
                    var h = FindWindow("#32770", null);
                    if (h != IntPtr.Zero) { PostMessage(h, WM_CLOSE, IntPtr.Zero, IntPtr.Zero); Log("WATCHDOG: closed #32770"); }
                }
                catch { }
                Thread.Sleep(1000);
            }
        }) { IsBackground = true }.Start();

        AppDomain.CurrentDomain.FirstChanceException += (s, e) => { try { Classify(e.Exception); } catch { } };
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            Interlocked.Increment(ref unhandledN);
            Log("UNHANDLED " + (e.ExceptionObject as Exception));
            try { Report.WriteLine("UNHANDLED " + e.ExceptionObject); Report.Flush(); } catch { }
            ExitProcess(3);
        };
        TaskScheduler.UnobservedTaskException += (s, e) => { Interlocked.Increment(ref unhandledN); Log("UNOBSERVED " + e.Exception); e.SetObserved(); };

        // ---------- PIN-FIRST: sha dll до ассертов ----------
        string shaOs = Sha(Path.Combine(Dir, "OsEngine.dll"));
        string shaTi = Sha(Path.Combine(Dir, "TInvestApi.dll"));
        Report.WriteLine("PIN OsEngine.dll sha256={0}", shaOs);
        Report.WriteLine("PIN TInvestApi.dll sha256={0}", shaTi);
        Report.WriteLine("PIN mode={0} rounds={1}", Mode, Rounds);
        Log("PIN OsEngine=" + shaOs);

        T = typeof(TInvestServerRealization);
        statusProp = T.GetProperty("ServerStatus");
        statusEnum = statusProp.PropertyType;

        if (Mode == "u9") RunU9();
        else if (Mode == "u11") RunU11();
        else throw new Exception("unknown mode: " + Mode);

        Report.WriteLine("METRICS mode={0} raceIOE={1} raceAOR={2} raceIOOR={3} otherEx={4} sys={5} err={6} unhandled={7}",
            Mode, raceIOE, raceAOR, raceIOOR, otherEx, logsSys, logsErr, unhandledN);
        foreach (var o in FirstOther) Report.WriteLine("OTHER-EX: " + o);
        foreach (var e2 in FirstErrors) Report.WriteLine("ERR-LOG: " + e2);
        Log("done, report=" + RptPath);
    }

    static string Sha(string path)
    {
        using var s = File.OpenRead(path);
        using var h = System.Security.Cryptography.SHA256.Create();
        return BitConverter.ToString(h.ComputeHash(s)).Replace("-", "");
    }

    static volatile bool WritersDone;
    static Security Seed;
    const string SeedName = "TSEED01";

    // =============== U-9': Find×3 против конкурентных Add/Clear poll-списка ===============
    static void RunU9()
    {
        inst = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(T);
        bool hasLocker = F("_pollLocker") != null;
        Case("U9-0-preflight", "1", "field _pollLocker present (shipping fix)", "present=" + hasLocker, hasLocker);
        if (!hasLocker) throw new Exception("_pollLocker absent — не фикс, прогон бессмыслен (см. шапку файла)");

        // инжект: продукт под UO, все поля, которых касается путь, — живые
        pollList = new List<Security>();
        Seed = Sec(SeedName);
        pollList.Add(Seed);
        Set("_pollSubscribedSecurities", pollList);
        locker = new object();
        Set("_pollLocker", locker);
        Set("_securities", new List<Security>());               // пусто → stream-путь минован, достигаем poll-Find
        SetNew("_securityStreamMap");
        SetNew("_orderNumbers");
        Set("_orderNumbersLocker", "_orderNumbersLocker");
        SetNew("_orderPrices");
        SetNew("_activeStopOrders");
        Set("_stopOrdersLocker", "_stopOrdersLocker");
        SetNew("_stopOrderNumbers");
        Set("_changeOrderPriceLocker", new object());
        Set("_rageGateOrdersLocker", "_rageGateOrdersLocker");
        Set("_rageGatePostOrdersLocker", "_rageGatePostOrdersLocker");
        // троттлинг сети — не предмет теста, ёмкость завышена, чтобы роботов не ограничивать
        Set("_rateGateOrders", new RateGate(1000000, TimeSpan.FromMinutes(1)));
        Set("_rateGatePostOrders", new RateGate(1000000, TimeSpan.FromMinutes(1)));
        Set("_rateGateStopOrders", new RateGate(1000000, TimeSpan.FromMinutes(1)));
        Set("_mskTimeZone", TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time"));
        Set("_gRpcMetadata", new Metadata());
        Set("_ordersClient", new FakeOrders());
        Set("_stopOrdersClient", new FakeStops());
        Set("MyOrderEvent", (Action<Order>)(o => { }));
        Set("LogMessageEvent", (Action<string, LogMessageType>)OnLogMessage);
        SetStatus("Connect");

        var universe = new Security[32];
        for (int i = 0; i < universe.Length; i++) universe[i] = Sec(string.Format("TST{0:00}", i));

        // писатели — паттерны продукта под тем же _pollLocker:
        // Clearer = Connect-Clear + атомарный ре-подпис seed (после секции seed всегда в списке);
        // Adder = SubscribeLoop-Add.
        long clearCycles = 0, addCycles = 0;
        WritersDone = false;
        var rndW = new Random(4242);

        var tClear = new Thread(() => {
            while (!WritersDone)
            {
                Enter(); try { pollList.Clear(); pollList.Add(Seed); } finally { Exit(); }
                Interlocked.Increment(ref clearCycles);
                if (Interlocked.Read(ref clearCycles) >= 2L * Rounds) WritersDone = true;   // писатели самовыключаемы: robot'ы добирают раунды и перекрытие
                Thread.Sleep(3);
            }
        }) { IsBackground = true };

        var tAdd = new Thread(() => {
            while (!WritersDone)
            {
                var s = universe[rndW.Next(universe.Length)];
                Enter(); try { if (pollList.Count < 200 && !pollList.Any(x => x.Name == s.Name)) pollList.Add(s); } finally { Exit(); }
                Interlocked.Increment(ref addCycles);
                Thread.Sleep(1);
            }
        }) { IsBackground = true };

        // роботы: три хоста Find, каждый Rounds раундов и минимум до конца писателей (перекрытие окон)
        long miss = 0, hitsA = 0, hitsB = 0, hitsC = 0;
        var rndR = new Random(777);

        var tA = new Thread(() => {                  // SendOrder → Find :5536
            int r = 0;
            do
            {
                var o = new Order { NumberUser = 5000 + r, SecurityNameCode = SeedName, TypeOrder = OrderPriceType.Limit, Price = 100m + r % 10, Volume = 1, Side = Side.Buy, PortfolioNumber = "TSTPORT", State = OrderStateType.None };
                try { ((TInvestServerRealization)inst).SendOrder(o); } catch (Exception ex) { Classify(ex); }
                if (o.State == OrderStateType.Active && o.NumberMarket != null) Interlocked.Increment(ref hitsA);
                else Interlocked.Increment(ref miss);
                r++;
            } while (r < Rounds || !WritersDone);
        }) { IsBackground = true };

        var oB = new Order { NumberUser = 9001, SecurityNameCode = SeedName, TypeOrder = OrderPriceType.Limit, Price = 100m, Volume = 5, VolumeExecute = 0, Side = Side.Buy, PortfolioNumber = "TSTPORT", State = OrderStateType.Active, NumberMarket = "m0" };
        var tB = new Thread(() => {                  // ChangeOrderPrice → Find :5895
            int r = 0;
            do
            {
                decimal np = 100m + (r % 10) + 0.5m;
                oB.Price = 100m + (r % 10);
                try { ((TInvestServerRealization)inst).ChangeOrderPrice(oB, np); } catch (Exception ex) { Classify(ex); }
                if (oB.Price == np) Interlocked.Increment(ref hitsB);   // успех: цену применил :6026
                else Interlocked.Increment(ref miss);
                r++;
            } while (r < Rounds || !WritersDone);
        }) { IsBackground = true };

        var tC = new Thread(() => {                  // SendStopOrder → Find :6906
            int r = 0;
            do
            {
                var o = new Order { NumberUser = 7000 + r, SecurityNameCode = SeedName, TypeOrder = OrderPriceType.StopLimit, Price = 100m + r % 10, StopPrice = 90m, Volume = 1, Side = Side.Sell, PortfolioNumber = "TSTPORT", State = OrderStateType.None };
                try { ((TInvestServerRealization)inst).SendStopOrder(o); } catch (Exception ex) { Classify(ex); }
                if (o.State == OrderStateType.Active && o.NumberMarket != null) Interlocked.Increment(ref hitsC);
                else Interlocked.Increment(ref miss);
                r++;
            } while (r < Rounds || !WritersDone);
        }) { IsBackground = true };

        tClear.Start(); tAdd.Start(); tA.Start(); tB.Start(); tC.Start();
        tA.Join(); tB.Join(); tC.Join();
        tClear.Join(); tAdd.Join();

        long race = Interlocked.Read(ref raceIOE) + Interlocked.Read(ref raceAOR) + Interlocked.Read(ref raceIOOR);
        Log(string.Format("U9 done hits={0}/{1}/{2} miss={3} clear={4} add={5} race={6} other={7} err={8}",
            hitsA, hitsB, hitsC, miss, clearCycles, addCycles, race, otherEx, logsErr));

        Case("U9-1-find-race-free", "1", "race(IOE/AOR/IOOR)=0 и ЛЮБАЯ FirstChance(otherEx)=0",
            string.Format("race={0} (IOE {1}/AOR {2}/IOOR {3}) otherEx={4}", race, raceIOE, raceAOR, raceIOOR, otherEx),
            race == 0 && Interlocked.Read(ref otherEx) == 0);
        Case("U9-2-known-seed", "1", "miss=0: seed найден каждым раундом (молчаливый промах=fail)",
            string.Format("miss={0}", miss), Interlocked.Read(ref miss) == 0);
        Case("U9-3-hosts-covered", "1", "SendOrder/ChangeOrderPrice/SendStopOrder>=" + Rounds + "; Clear/Add циклы>=" + Rounds,
            string.Format("hits={0}/{1}/{2} clear={3} add={4}", hitsA, hitsB, hitsC, clearCycles, addCycles),
            hitsA >= Rounds && hitsB >= Rounds && hitsC >= Rounds && clearCycles >= Rounds && addCycles >= Rounds);
        Case("U9-4-silent-green", "1", "Error=0; System=0 (успешный путь лог не пишет)",
            string.Format("err={0} sys={1}", logsErr, logsSys), Interlocked.Read(ref logsErr) == 0 && Interlocked.Read(ref logsSys) == 0);
    }

    // =============== U-11': Connect/Subscribe/Dispose ×4 + Subscribe-после-Dispose ===============
    static void RunU11()
    {
        inst = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(T);
        bool hasLocker = F("_pollLocker") != null;
        Case("U11-0-preflight", "1", "field _pollLocker present (shipping fix)", "present=" + hasLocker, hasLocker);
        if (!hasLocker) throw new Exception("_pollLocker absent — не фикс, прогон бессмыслен (см. шапку файла)");

        pollList = new List<Security>();
        Set("_pollSubscribedSecurities", pollList);
        locker = new object();
        Set("_pollLocker", locker);
        SetNew("_subscribeRetryCounts");
        SetNew("_activeStopOrders");
        Set("_stopOrdersLocker", "_stopOrdersLocker");
        Set("_maxMarketDataStreams", 0);            // подписка идёт в poll-ветку без сети (тот же приём, что фан-аут тест)
        Set("_marketDataStreamLocker", "_marketDataStreamLocker");   // UO: lok-поля-строки (=new) не инициализированы
        Set("_myPortfolios", Activator.CreateInstance(F("_myPortfolios").FieldType));
        SetNew("_stopOrderExecutions");
        Set("ConnectEvent", (Action)(() => { }));    // UO: события-делегаты = null → Dispose вызывает DisconnectEvent() на пустом
        Set("DisconnectEvent", (Action)(() => { }));
        Set("LogMessageEvent", (Action<string, LogMessageType>)OnLogMessage);
        Set("MyOrderEvent", (Action<Order>)(o => { }));

        // токен пустой: Connect выполняет bookkeeping и штатно возвращается на гейте токена (:187-192),
        // в сеть не идёт; каждый Connect даёт ровно один ожидаемый Error «токен не задан»
        var ps = new List<IServerParameter>
        {
            new ServerParameterPassword { Name = "token", Value = "" },
            new ServerParameterBool { Name = "useStock", Value = true },
            new ServerParameterBool { Name = "useFutures", Value = true },
            new ServerParameterBool { Name = "useOptions", Value = false },
            new ServerParameterBool { Name = "useOther", Value = true },
            new ServerParameterBool { Name = "filterDealer", Value = true },
            new ServerParameterBool { Name = "morningAuction", Value = true },
            new ServerParameterBool { Name = "fullLog", Value = false },
            new ServerParameterEnum { Name = "depth", Value = "10" },
        };
        T.GetProperty("ServerParameters").SetValue(inst, ps);

        long bookFail = 0;
        bool bookOk = true;
        for (int cycle = 1; cycle <= 4; cycle++)
        {
            ((TInvestServerRealization)inst).Connect(null);
            var sec = Sec(string.Format("U11S{0}", cycle));
            ((TInvestServerRealization)inst).Subscribe(sec);   // poll-ветка: Add под _pollLocker
            int afterSub = pollList.Count;
            ((TInvestServerRealization)inst).Dispose();        // Dispose чистит poll-список под _pollLocker (:496-499)
            int afterDisp = pollList.Count;
            int mapCnt = Count("_securityStreamMap");
            int streams = Count("_marketDataStreams");
            Log(string.Format("U11 cycle{0}: afterSub={1} afterDispose={2} map={3} streams={4}", cycle, afterSub, afterDisp, mapCnt, streams));
            if (afterSub != 1 || afterDisp != 0 || mapCnt != 0 || streams != 0) { bookOk = false; Interlocked.Increment(ref bookFail); }
        }
        Case("U11-1-bookkeeping-x4", "4", "Subscribe→1, Dispose→0, map=0, streams=0 каждый цикл",
            "cyclesFailed=" + bookFail, bookOk);

        // Subscribe-после-Dispose: cts отменён, обёртки не создаём (max=0 → poll-ветка), unhandled нет
        long errBefore = Interlocked.Read(ref logsErr);
        ((TInvestServerRealization)inst).Subscribe(Sec("U11S5"));
        int afterAdHoc = pollList.Count;
        int streamsAdHoc = Count("_marketDataStreams");
        long errAdHoc = Interlocked.Read(ref logsErr) - errBefore;
        ((TInvestServerRealization)inst).Dispose();
        int afterFinalDispose = pollList.Count;
        Case("U11-2-subscribe-after-dispose", "1", "Add=1, зомби-обёрток=0, новых Error=0, Dispose→0",
            string.Format("afterSub={0} streams={1} newErr={2} afterDispose={3}", afterAdHoc, streamsAdHoc, errAdHoc, afterFinalDispose),
            afterAdHoc == 1 && streamsAdHoc == 0 && errAdHoc == 0 && afterFinalDispose == 0);

        Case("U11-3-errors-expected-only", "5 Connect", "Error=4 (только гейт токена), остальные=0",
            string.Format("err={0} (sys={1})", logsErr, logsSys),
            Interlocked.Read(ref logsErr) == 4 && errAdHoc == 0);
        Case("U11-4-crash-free", "1", "unhandled=0; race=0; otherEx=0",
            string.Format("unhandled={0} race={1} otherEx={2}", unhandledN, raceIOE + raceAOR + raceIOOR, otherEx),
            unhandledN == 0 && (raceIOE + raceAOR + raceIOOR) == 0 && otherEx == 0);

        Report.WriteLine("INFO fixed≡unfixed: фикс меняет только способ доступа к _pollSubscribedSecurities (локи), семантику bookkeeping не меняет; кейс проверяет форму bookkeeping на фиксе, эквивалентность — по диффу фикса");
    }
}
