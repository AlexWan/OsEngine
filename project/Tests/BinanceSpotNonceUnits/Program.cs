// BUG-0352 юниты BinanceSpot: выражение `serverTime + 500` (BinanceServerSpot.cs :3321
// post-merge) на реальном типе BinanceTime (Market/Servers/Binance/Spot/BinanceSpotEntity).
// Режимы: red = unfixed база (string, конкатенация), green = фикс (long, арифметика).
// Выход: 0 — все кейсы совпали с ожиданиями режима; 1 — расхождение; 2 — фатальный preflight.
// R3 — type-gate (анти-регрессия повторного long->string, ОдТС plan-20261115-013).
// T1 — точный fixture-оракул 1763000000000+500 + premise-probe "epoch-fallback не
//      использован" (13-значный оракул недискриминирующ: фолбэк тоже даёт 13 цифр, R2).
// T2 — characterization-матрица (R4). Строка {"serverTime":0} — A/B: red "0500",
//      green "500" (стенд bug-0348 уже пиннил "0500" как U3-отклонение).
// Маркировка (R2): T1/T2 — type-level реплика выражения :3321 на реальном типе, НЕ
// прод-путь GetNonce (private; шов = MITM Connect(WebProxy)/_myProxy, прод-путь C1-C4
// закрыт стендом bug-0348). Прод-покрытие этого бага = разбор + live-пробел (R5).
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json;

internal static class Program
{
    private static int _failures;

    private static int Main(string[] args)
    {
        string mode = (args.Length > 0 ? args[0] : "green").ToLowerInvariant();
        bool green = mode != "red";
        Console.WriteLine("mode=" + mode);
        try
        {
            Type timeType = Type.GetType(
                "OsEngine.Market.Servers.Binance.Spot.BinanceSpotEntity.BinanceTime, OsEngine", true);
            Assembly osEngine = timeType.Assembly;
            Console.WriteLine("osengine.location=" + osEngine.Location);
            using (FileStream fs = File.OpenRead(osEngine.Location))
                Console.WriteLine("osengine.sha256=" + Convert.ToHexString(SHA256.HashData(fs)));

            PropertyInfo serverTime = timeType.GetProperty("serverTime");

            // R3: type-gate против свеже собранной dll.
            Check("R3 type-gate serverTime",
                serverTime.PropertyType == (green ? typeof(long) : typeof(string)),
                "PropertyType=" + serverTime.PropertyType.Name +
                " want=" + (green ? "Int64" : "String"));

            // T1: точный fixture-оракул (R2). FIXTURE = зафиксированный ответ time-endpoint.
            EvalResult t1 = Eval(timeType, serverTime, "{\"serverTime\":1763000000000}");
            Check("T1 fixture 1763000000000 -> nonce",
                !t1.Threw && t1.Detail == (green ? "1763000000500" : "1763000000000500"),
                t1.Threw ? "threw " + t1.Detail : "nonce=" + t1.Detail);

            // T1 premise-probe: оракул должен отличаться от epoch-fallback (>30 суток),
            // иначе GREEN на сетевом отказе был бы ложным (fallback даёт ~UtcNow).
            long parsed = 0;
            bool isLong = t1.Detail != null &&
                long.TryParse(t1.Detail, NumberStyles.None, CultureInfo.InvariantCulture, out parsed);
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Check("T1 premise-probe: fallback NOT used (|nonce-now|>30d)",
                isLong && Math.Abs(parsed - now) > 30L * 24 * 3600 * 1000,
                "nonce=" + t1.Detail + " now~" + now + " (epoch-fallback would be ~now)");

            // T2 characterization-матрица (R4).
            EvalResult empty = Eval(timeType, serverTime, "{}");
            Check("T2 {} -> 500 (both)", !empty.Threw && empty.Detail == "500",
                empty.Threw ? "threw " + empty.Detail : "nonce=" + empty.Detail);

            EvalResult nullVal = Eval(timeType, serverTime, "{\"serverTime\":null}");
            Check("T2 null-value -> A/B (red: null concat 500; green: JsonSerializationException -> prod CreateQuery catch => Error+null => fallback)",
                green ? (nullVal.Threw && nullVal.Detail.StartsWith("JsonSerializationException"))
                      : (!nullVal.Threw && nullVal.Detail == "500"),
                nullVal.Threw ? "threw " + nullVal.Detail : "nonce=" + nullVal.Detail);

            EvalResult quoted = Eval(timeType, serverTime, "{\"serverTime\":\"1763000000000\"}");
            Check("T2 quoted number -> coercion/concat (A/B)",
                !quoted.Threw && quoted.Detail == (green ? "1763000000500" : "1763000000000500"),
                quoted.Threw ? "threw " + quoted.Detail : "nonce=" + quoted.Detail);

            EvalResult zero = Eval(timeType, serverTime, "{\"serverTime\":0}");
            Check("T2 zero -> A/B (0348 U3pin: red 0500)",
                !zero.Threw && zero.Detail == (green ? "500" : "0500"),
                zero.Threw ? "threw " + zero.Detail : "nonce=" + zero.Detail);

            // Переполнение: unchecked-перенос (в проекте /checked нет) -> отрицательный
            // nonce. Новый failure mode long-версии; гейт 0 < serverTime <= long.MaxValue-500
            // — follow-up, не скоуп 0352.
            long maxVal = long.MaxValue; // не константа: перенос считается в рантайме (unchecked)
            long maxPlus = maxVal + 500L;
            EvalResult overflow = Eval(timeType, serverTime, "{\"serverTime\":9223372036854775807}");
            Check("T2 overflow MaxValue -> unchecked wrap (green) / concat (red)",
                !overflow.Threw && overflow.Detail ==
                    (green ? maxPlus.ToString(CultureInfo.InvariantCulture) : "9223372036854775807500"),
                overflow.Threw ? "threw " + overflow.Detail : "nonce=" + overflow.Detail);
            if (green)
                Check("T2 overflow wrap is negative (new long failure mode)",
                    maxPlus < 0, "wrapped=" + maxPlus.ToString(CultureInfo.InvariantCulture));

            EvalResult abc = Eval(timeType, serverTime, "{\"serverTime\":\"abc\"}");
            Check("T2 abc -> A/B (green: throw -> prod CreateQuery catch => Error+null; red: abc500)",
                green ? abc.Threw
                      : !abc.Threw && abc.Detail == "abc500",
                abc.Threw ? "threw " + abc.Detail : "nonce=" + abc.Detail);

            EvalResult garbage = Eval(timeType, serverTime, "not-json{");
            Check("T2 raw garbage -> throws (both, characterization)",
                garbage.Threw, "result=" + (garbage.Threw ? "threw " + garbage.Detail : garbage.Detail));

            Console.WriteLine(_failures == 0
                ? "ALL PASS (mode=" + mode + ")"
                : "FAILURES=" + _failures + " (mode=" + mode + ")");
            return _failures == 0 ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine("FATAL preflight: " + ex);
            return 2;
        }
    }

    private struct EvalResult
    {
        public bool Threw;
        public string Detail; // имя исключения при Threw, иначе nonce
    }

    // Реплика прод-выражения :3321 (result.serverTime + 500).ToString() на реальном
    // типе свойства: string-свойство => конкатенация (как компилировал roslyn до фикса),
    // long => арифметика. Десериализация — тот же JsonConvert, что в CreateQuery.
    private static EvalResult Eval(Type timeType, PropertyInfo serverTime, string json)
    {
        try
        {
            object obj = JsonConvert.DeserializeObject(json, timeType);
            if (obj == null)
                return new EvalResult { Threw = true, Detail = "null-result" };
            object st = serverTime.GetValue(obj);
            if (st == null)
                // только string-свойство: прод `serverTime + 500` при null => string.Concat => "500"
                return new EvalResult { Threw = false, Detail = (string)null + "500" };
            string nonce = st is string
                ? (string)st + "500"
                : ((long)st + 500L).ToString(CultureInfo.InvariantCulture);
            return new EvalResult { Threw = false, Detail = nonce };
        }
        catch (Exception ex)
        {
            return new EvalResult { Threw = true, Detail = ex.GetType().Name + ": " + ex.Message };
        }
    }

    private static void Check(string name, bool ok, string detail)
    {
        if (!ok)
            _failures++;
        Console.WriteLine((ok ? "PASS " : "FAIL ") + name + " — " + detail);
    }
}
