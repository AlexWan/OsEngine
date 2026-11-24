using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using OsEngine.Market.Servers.MFD;

// bug-0633 (публ. bug-0640) gap C: durable юниты G1-G4 парсера MfdServerRealization (дедуп PR 2275).
// G1 — структурный guard: ровно один ОБЪЯВЛЕННЫЙ в типе метод bool(string, out decimal)
//      (DeclaredOnly: ловит in-class дедуп; дубль в другом классе — вне области G1, см. README).
// G2/G4 — golden-таблица границ парсера, пин фактического прогона; дёргаем рефлексией единственный
//      метод, найденный G1 (NonPublic|Static), имя не литералится, инстанс не создаётся.
// G3 — culture-independence: baseline инварианта сверяется с GoldenTable ВНУТРИ G3 (solo-safe),
//      затем тот же набор при ru-RU/en-US побитово совпадает с baseline; EX-строки = fatal;
//      premise-probe: культура обязана реально нести ожидаемый разделитель (invariant-mode → loud).
// Фильтр аргумента строгий: ""|g1|g2|g3|g4; всё прочее — FATAL exit 2 (вакуумный GREEN запрещён, FU-1).
// Отчёт — stdout (ASCII-маркеры решений), первой строкой sha256 загруженного OsEngine.dll.
// Exit: 0 GREEN, 1 кейс упал, 2 fatal/preflight (см. README).
internal static class Program
{
    private static readonly (string Input, bool Ok, decimal Value)[] GoldenTable =
    {
        ("1.5", true, 1.5m),
        ("-3.25", true, -3.25m),
        ("0", true, 0m),
        ("12345", true, 12345m),
        ("1,234", true, 1.234m),
        ("1,23", true, 1.23m),
        ("1,234.56", false, 0m),
        ("1E400", false, 0m),
        ("1E-400", false, 0m),
        ("1e5", false, 0m),
        ("", false, 0m),
        (" ", false, 0m),
        (null, false, 0m),
        ("1.2.3", false, 0m),
        (" 1.5 ", true, 1.5m),
        ("NaN", false, 0m),
        ("Infinity", false, 0m),
        ("5-", true, -5m),
        ("-79228162514264337593543950335", true, decimal.MinValue),
        ("79228162514264337593543950336", false, 0m),
        ("1,5", true, 1.5m),
        (",5", true, 0.5m),
        ("1,", true, 1m),
        (",", false, 0m),
        ("\t1.5\t", true, 1.5m),
        ("\t", false, 0m)
    };

    private static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception error)
        {
            Console.WriteLine("FATAL: " + Esc(error.GetType().Name + ": " + error.Message));
            return 2;
        }
    }

    private static int Run(string[] args)
    {
        // FU-1: строгий разбор фильтра; неизвестный/лишние аргументы — loud exit 2.
        if (args.Length > 1)
        {
            Console.WriteLine("FATAL: too many arguments (" + args.Length + "), allowed: none or one of g1 g2 g3 g4");
            return 2;
        }
        string filter = args.Length == 0 ? "" : args[0];
        if (filter != "" && filter != "g1" && filter != "g2" && filter != "g3" && filter != "g4")
        {
            Console.WriteLine("FATAL: unknown filter '" + Esc(filter) + "' (allowed: none or one of g1 g2 g3 g4)");
            return 2;
        }

        Assembly engineAssembly = typeof(MfdServerRealization).Assembly;
        Console.WriteLine("sha256 OsEngine.dll=" + Sha256File(engineAssembly.Location));
        Console.WriteLine("loaded: " + engineAssembly.Location);

        Type targetType = typeof(MfdServerRealization);
        MethodInfo[] hits = FindDecimalHelpers(targetType);
        Console.WriteLine("G1 candidates: " + (hits.Length == 0
            ? "(none)"
            : string.Join(", ", hits.Select(x => x.Name + " [" + Visibility(x) + (x.IsStatic ? " static" : " instance") + "]"))));
        if (hits.Length != 1)
        {
            Console.WriteLine("G1 FAIL: expected exactly 1 method bool(string, out decimal) declared in MfdServerRealization, found "
                + hits.Length + " (dedup PR 2275 regressed / parser gone)");
            Console.WriteLine("RESULT RED exit=1");
            return 1;
        }
        MethodInfo target = hits[0];
        Console.WriteLine("G1 PASS: exactly 1 method, scope=type-local DeclaredOnly (duplicate in another class is out of G1 scope)");

        // FU-2c: static-гейт до ЛЮБОГО golden-исполнения, включая solo-g3.
        if (filter != "g1" && !target.IsStatic)
        {
            Console.WriteLine("FATAL: sole helper is not static - G4 contract (instance is never created) broken, golden not executed");
            return 2;
        }

        bool green = true;
        if (filter == "" || filter == "g2")
        {
            green &= RunGoldenCases(target, "G2");
        }
        if (filter == "" || filter == "g3")
        {
            int g3 = RunCultureIndependence(target, "G3");
            if (g3 == 2)
            {
                Console.WriteLine("RESULT FATAL exit=2");
                return 2;
            }
            green &= g3 == 0;
        }
        if (filter == "" || filter == "g4")
        {
            Console.WriteLine("G4 PASS: golden cases above (G2" + (filter == "" ? "/G3" : "") + ") invoked the method found by G1 via "
                + "reflection NonPublic|Static, target=null (" + target.Name + "); no MfdServerRealization instance created");
        }
        if (filter == "g1")
        {
            Console.WriteLine("G4 NOT RUN: filter g1 executes only the structural guard");
        }
        Console.WriteLine(green ? "RESULT GREEN exit=0" : "RESULT RED exit=1");
        return green ? 0 : 1;
    }

    // G1: все объявленные в типе методы сигнатуры bool(string, out decimal), любая видимость/статичность.
    private static MethodInfo[] FindDecimalHelpers(Type targetType)
    {
        return targetType
            .GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Static | BindingFlags.Instance)
            .Where(x => x.ReturnType == typeof(bool))
            .Where(x =>
            {
                ParameterInfo[] p = x.GetParameters();
                return p.Length == 2
                    && p[0].ParameterType == typeof(string)
                    && p[1].IsOut
                    && p[1].ParameterType == typeof(decimal).MakeByRefType();
            })
            .ToArray();
    }

    private static bool RunGoldenCases(MethodInfo target, string label)
    {
        bool pass = true;
        int okCount = 0;
        foreach ((string Input, bool Ok, decimal Value) expected in GoldenTable)
        {
            (bool Ok, string Value, bool Threw) actual = InvokeParse(target, expected.Input);
            bool rowPass = !actual.Threw
                && actual.Ok == expected.Ok
                && decimal.TryParse(actual.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal actualValue)
                && actualValue == expected.Value;
            if (rowPass)
            {
                okCount++;
            }
            else
            {
                pass = false;
            }
            Console.WriteLine(label + " " + (rowPass ? "OK  " : "FAIL") + " " + Quote(expected.Input)
                + " -> " + actual.Ok + " " + actual.Value
                + " | expect " + expected.Ok + " " + expected.Value.ToString("0.####################", CultureInfo.InvariantCulture));
        }
        Console.WriteLine(label + (pass ? " PASS" : " FAIL") + " " + okCount + "/" + GoldenTable.Length);
        return pass;
    }

    // G3 (solo-safe): baseline инварианта строится здесь же и сверяется с GoldenTable; EX-строки fatal.
    private static int RunCultureIndependence(MethodInfo target, string label)
    {
        CultureInfo oldCulture = CultureInfo.CurrentCulture;
        CultureInfo oldUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            List<(bool Ok, string Value, bool Threw)> baseline = InvokeSet(target);
            if (baseline.Any(x => x.Threw))
            {
                Console.WriteLine(label + " FATAL: parser threw exceptions in invariant baseline, EX-rows forbidden in G3 (FU-2b)");
                return 2;
            }
            int matched = 0;
            for (int i = 0; i < GoldenTable.Length; i++)
            {
                (bool Ok, string Value, bool Threw) actual = baseline[i];
                bool rowPass = actual.Ok == GoldenTable[i].Ok
                    && decimal.TryParse(actual.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal actualValue)
                    && actualValue == GoldenTable[i].Value;
                if (rowPass)
                {
                    matched++;
                }
                else
                {
                    Console.WriteLine(label + " BASE-FAIL " + Quote(GoldenTable[i].Input)
                        + " -> " + actual.Ok + " " + actual.Value
                        + " | expect " + GoldenTable[i].Ok + " " + GoldenTable[i].Value.ToString("0.####################", CultureInfo.InvariantCulture));
                }
            }
            Console.WriteLine(label + " baseline-vs-GoldenTable: " + (matched == GoldenTable.Length ? "PASS" : "FAIL")
                + " " + matched + "/" + GoldenTable.Length);
            bool pass = matched == GoldenTable.Length;

            foreach (string cultureName in new[] { "ru-RU", "en-US" })
            {
                CultureInfo culture = new CultureInfo(cultureName);
                // Premise-probe: культура реально несёт ожидаемый разделитель, иначе сверка вакуумна
                // (например, DOTNET_SYSTEM_GLOBALIZATION_INVARIANT подменяет культуры — тут будет loud FAIL).
                string expectedSeparator = cultureName == "ru-RU" ? "," : ".";
                if (culture.NumberFormat.NumberDecimalSeparator != expectedSeparator)
                {
                    Console.WriteLine(label + " " + cultureName + " FAIL: premise broken, NumberDecimalSeparator='"
                        + culture.NumberFormat.NumberDecimalSeparator + "' expected '" + expectedSeparator + "'");
                    pass = false;
                    continue;
                }
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
                List<(bool Ok, string Value, bool Threw)> actualSet = InvokeSet(target);
                if (actualSet.Any(x => x.Threw))
                {
                    Console.WriteLine(label + " FATAL: parser threw exceptions under " + cultureName + ", EX-rows forbidden in G3 (FU-2b)");
                    return 2;
                }
                var diffs = new List<string>();
                for (int i = 0; i < GoldenTable.Length; i++)
                {
                    if (actualSet[i].Ok != baseline[i].Ok || actualSet[i].Value != baseline[i].Value)
                    {
                        diffs.Add(Quote(GoldenTable[i].Input) + ": inv=" + baseline[i].Ok + " " + baseline[i].Value
                            + " vs " + cultureName + "=" + actualSet[i].Ok + " " + actualSet[i].Value);
                    }
                }
                Console.WriteLine(label + " " + cultureName + ": " + (diffs.Count == 0
                    ? GoldenTable.Length + "/" + GoldenTable.Length + " IDENTICAL to invariant baseline"
                    : "DIFFS: " + string.Join(" | ", diffs)));
                pass &= diffs.Count == 0;
            }
            Console.WriteLine(label + (pass ? " PASS" : " FAIL"));
            return pass ? 0 : 1;
        }
        finally
        {
            CultureInfo.CurrentCulture = oldCulture;
            CultureInfo.CurrentUICulture = oldUiCulture;
        }
    }

    private static List<(bool, string, bool)> InvokeSet(MethodInfo target)
    {
        var rows = new List<(bool, string, bool)>();
        foreach ((string Input, bool Ok, decimal Value) entry in GoldenTable)
        {
            rows.Add(InvokeParse(target, entry.Input));
        }
        return rows;
    }

    private static (bool, string, bool) InvokeParse(MethodInfo target, string input)
    {
        try
        {
            object[] arguments = { input, null };
            bool ok = (bool)target.Invoke(null, arguments);
            return (ok, ((decimal)arguments[1]).ToString("0.####################", CultureInfo.InvariantCulture), false);
        }
        catch (Exception error)
        {
            return (false, "EX:" + error.GetBaseException().GetType().Name, true);
        }
    }

    private static string Visibility(MethodInfo method)
    {
        if (method.IsPrivate)
        {
            return "private";
        }
        if (method.IsPublic)
        {
            return "public";
        }
        if (method.IsFamily)
        {
            return "protected";
        }
        if (method.IsAssembly)
        {
            return "internal";
        }
        return "other";
    }

    private static string Sha256File(string path)
    {
        using (SHA256 sha = SHA256.Create())
        using (Stream file = File.OpenRead(path))
        {
            return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "");
        }
    }

    private static string Quote(string value)
    {
        return value == null ? "null" : "\"" + Esc(value) + "\"";
    }

    // ASCII-маркеры stdout — предусловие приёмки grep'ом и CR-пина: control-символы экранируются
    // (FU-7): каждая строка отчёта — ровно одна физическая строка, сырой CR/\n из golden не затирает маркер.
    private static string Esc(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            if (c == '\t')
            {
                sb.Append("\\t");
            }
            else if (c == '\r')
            {
                sb.Append("\\r");
            }
            else if (c == '\n')
            {
                sb.Append("\\n");
            }
            else if (c == '"')
            {
                sb.Append("'");
            }
            else if (char.IsControl(c))
            {
                sb.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
