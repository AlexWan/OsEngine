// BUG-0352 юниты BinanceSpot — ПЕРЕНОС НА НОВЫЙ ШОВ (bug-0432/BUG-0431, rebase на master после #2137).
// История: PR #2137 верифицировал выражение `result.serverTime + 500` (BinanceServerSpot.cs :3321 post-merge)
// репликой на типе BinanceTime. Фикс bug-0432 удаляет это выражение: GetNonce считает timestamp из
// КЭШИРОВАННОГО offset (local + _serverTimeOffset + 500), парсит ответ анонимным типом, BinanceTime не читает.
// Смысл проверок сохранён — «+500 есть арифметика long, а не конкатенация строк» — но прогоняется через
// РЕАЛЬНЫЙ GetNonce с инжектированным кэшем (свежий TTL: сети нет, SyncServerTime не дёргается).
// R3 type-gate (анти-регрессия long->string из OdTC plan-20261115-013) — без изменений.
// Режим red (характеризация unfixed-базы через конкатенацию) после re-seam невоспроизводим: базового
// выражения в дереве нет, базовый GetNonce требует сети. red => exit=2 «unsupported».
// Выход: 0 — все кейсы совпали; 1 — расхождение; 2 — фатальный preflight/unsupported режим.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

internal static class Program
{
    private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly object LogLock = new object();
    private static readonly List<string> LogMsgs = new List<string>();

    private static int _failures;
    private static Assembly _osEngine;
    private static Type _logMsgType;
    private static MethodInfo _getNonce;
    private static MethodInfo _apply;
    private static FieldInfo _fOffset;
    private static FieldInfo _fSync;

    private static int Main(string[] args)
    {
        string mode = (args.Length > 0 ? args[0] : "green").ToLowerInvariant();
        Console.WriteLine("mode=" + mode);
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch (Exception) { }
        if (mode == "red")
        {
            Console.WriteLine("FATAL preflight: red-mode unsupported post-re-seam (BUG-0431): базовое выражение" +
                " `serverTime + 500` удалено из GetNonce, characterization-база на этом шве не существует;" +
                " см. README (перенос по OdTC plan-20261115-016)");
            return 2;
        }
        try
        {
            Type timeType = Type.GetType(
                "OsEngine.Market.Servers.Binance.Spot.BinanceSpotEntity.BinanceTime, OsEngine", true);
            _osEngine = timeType.Assembly;
            Console.WriteLine("osengine.location=" + _osEngine.Location);
            using (FileStream fs = File.OpenRead(_osEngine.Location))
                Console.WriteLine("osengine.sha256=" + Convert.ToHexString(SHA256.HashData(fs)));

            PropertyInfo serverTime = timeType.GetProperty("serverTime");

            // R3: type-gate против свеченной dll (анти-регрессия повторного long->string).
            Check("R3 type-gate serverTime == Int64",
                serverTime.PropertyType == typeof(long), "PropertyType=" + serverTime.PropertyType.Name);

            SetupRealizationAccessors();

            // T1 (новый шов): fixture-оракул = инжектированный skew +30 с при свежем TTL.
            // nonce обязан = local+30 с+500 (арифметика). Конкатенация дала бы либо нечисло,
            // либо 16+ цифр, либо отсутствие +30 с — все три варианта ловятся.
            {
                object r = NewRealization();
                SetCache(r, TimeSpan.FromSeconds(30), DateTime.UtcNow);
                ResetLog();
                DateTime before = DateTime.UtcNow;
                long nonce = CallNonce(r);
                DateTime after = DateTime.UtcNow;
                long lo = (long)(before - Epoch).TotalMilliseconds + 30000 + 500 - 1000;
                long hi = (long)(after - Epoch).TotalMilliseconds + 30000 + 500 + 1000;
                Check("T1 nonce = local+30с+500 (арифметика, ±1с)", nonce >= lo && nonce <= hi,
                    "nonce=" + nonce + " окно=[" + lo + ";" + hi + "]");
                Check("T1 premise-probe: ноль логов за вызов (кэш держит, epoch-fallback/проба не использованы)",
                    LogCount() == 0, "записей=" + LogCount());
            }

            // T2 (новый шов): матрица на арифметике nonce.
            {
                object r = NewRealization();
                SetCache(r, TimeSpan.Zero, DateTime.UtcNow);
                ResetLog();
                long nonce = CallNonce(r);
                long delta = nonce - (long)(DateTime.UtcNow - Epoch).TotalMilliseconds;
                Check("T2a offset=0 → pad ровно +500 (не «…500»-хвост)", delta >= 400 && delta <= 1500,
                    "delta=" + delta + " digits=" + nonce.ToString(CultureInfo.InvariantCulture).Length);

                SetCache(r, TimeSpan.FromSeconds(-30), DateTime.UtcNow);
                nonce = CallNonce(r);
                delta = nonce - (long)(DateTime.UtcNow - Epoch).TotalMilliseconds;
                Check("T2b offset=−30с → signed арифметика (−29.5с..−30с+pad)", delta <= -29000 && delta >= -30000 - 500,
                    "delta=" + delta);

                SetCache(r, TimeSpan.FromDays(30), DateTime.UtcNow);
                nonce = CallNonce(r);
                Check("T2c offset=+30 суток → отличим от fallback (>30 суток от now)",
                    Math.Abs(nonce - (long)(DateTime.UtcNow - Epoch).TotalMilliseconds) > 30L * 24 * 3600 * 1000 - 5000,
                    "nonce=" + nonce);
            }

            // T2d (carryover quoted-кейса матрицы): JSON-число в кавычках на новом шве ПРИНЯТО
            // (Newtonsoft приводит к long в анонимном типе ApplyTimeResponse — пин CDN-формы).
            {
                object r = NewRealization();
                ResetLog();
                bool ok = (bool)_apply.Invoke(r, new object[] { "{\"serverTime\":\"1763000000000\"}" });
                double offDays = GetOffset(r).TotalDays;
                Check("T2d quoted-число принято, offset=delta",
                    ok && Math.Abs(offDays - (Epoch.AddMilliseconds(1763000000000L) - DateTime.UtcNow).TotalDays) < 0.01,
                    "ok=" + ok + " offsetDays=" + offDays.ToString("F2"));
            }

            Console.WriteLine(_failures == 0
                ? "ALL PASS (mode=" + mode + ")"
                : "FAILURES=" + _failures + " (mode=" + mode + ")");
            Console.Out.Flush();
            Environment.Exit(_failures == 0 ? 0 : 1);   // facade поднимает фоновые обвязки — штатный return может не дожить
            return _failures == 0 ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine("FATAL preflight: " + ex);
            return 2;
        }
    }

    private static void SetupRealizationAccessors()
    {
        _logMsgType = _osEngine.GetType("OsEngine.Logging.LogMessageType");
        object r = NewRealization();
        Type rt = r.GetType();
        _getNonce = Find(rt, "GetNonce");
        _apply = Find(rt, "ApplyTimeResponse");
        _fOffset = FindField(rt, "_serverTimeOffset");
        _fSync = FindField(rt, "_lastServerTimeSync");
        if (_getNonce == null || _apply == null || _fOffset == null || _fSync == null)
        {
            throw new Exception("_PREFLIGHT: шов bug-0432 (GetNonce-кэш/ApplyTimeResponse/поля) не найден в сборке —" +
                " тесты перенесены под fix/bug-0432, на ветке без фикса они неприменимы");
        }
    }

    private static object NewRealization()
    {
        Type facadeType = _osEngine.GetType("OsEngine.Market.Servers.Binance.Spot.BinanceServerSpot", true);
        object facade = Activator.CreateInstance(facadeType, 1);   // только через facade (OdTC: ServerParameters-null ловушка)
        object real = facadeType.GetProperty("ServerRealization").GetValue(facade, null);
        Type rt = real.GetType();
        Type iPars = _osEngine.GetType("OsEngine.Market.Servers.Entity.IServerParameter", true);
        Type boolPar = _osEngine.GetType("OsEngine.Market.Servers.Entity.ServerParameterBool", true);
        IList pars = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(iPars));
        for (int i = 0; i < 4; i++)
        {
            object p = Activator.CreateInstance(boolPar);
            boolPar.GetProperty("Name").SetValue(p, "param-" + i, null);
            boolPar.GetProperty("Value").SetValue(p, false, null);   // [3] Shared RateGate принудительно false
            pars.Add(p);
        }
        rt.GetProperty("ServerParameters").SetValue(real, pars, null);
        rt.GetField("ApiKey").SetValue(real, "synthetic-key");
        rt.GetField("SecretKey").SetValue(real, "synthetic-secret");
        if (_logMsgType != null)
        {
            Type actionType = typeof(Action<,>).MakeGenericType(typeof(string), _logMsgType);
            MethodInfo handler = typeof(Program).GetMethod("HandleLog", BindingFlags.NonPublic | BindingFlags.Static)
                .MakeGenericMethod(_logMsgType);
            rt.GetEvent("LogMessageEvent").AddEventHandler(real, Delegate.CreateDelegate(actionType, handler));
        }
        return real;
    }

    private static void HandleLog<TMessage>(string message, TMessage type)
    {
        lock (LogLock) { LogMsgs.Add(message); }
    }

    private static void ResetLog() { lock (LogLock) { LogMsgs.Clear(); } }
    private static int LogCount() { lock (LogLock) { return LogMsgs.Count; } }

    private static long CallNonce(object r)
    {
        return long.Parse((string)_getNonce.Invoke(r, null), CultureInfo.InvariantCulture);
    }

    private static void SetCache(object r, TimeSpan offset, DateTime sync)
    {
        _fOffset.SetValue(r, offset);
        _fSync.SetValue(r, sync);
    }

    private static TimeSpan GetOffset(object r) { return (TimeSpan)_fOffset.GetValue(r); }

    private static MethodInfo Find(Type t, string name)
    {
        for (Type c = t; c != null; c = c.BaseType)
        {
            MethodInfo m = c.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (m != null) return m;
        }
        return null;
    }

    private static FieldInfo FindField(Type t, string name)
    {
        for (Type c = t; c != null; c = c.BaseType)
        {
            FieldInfo f = c.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (f != null) return f;
        }
        return null;
    }

    private static void Check(string name, bool ok, string detail)
    {
        if (!ok) _failures++;
        Console.WriteLine((ok ? "PASS " : "FAIL ") + name + " — " + detail);
    }
}
