using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using OsEngine.Market.Servers.MFD;

// bug-0633 (публ. bug-0640) gap C: durable юниты G1-G4 парсера MfdServerRealization (дедуп PR 2275).
// G1 — структурный guard: ровно один объявленный метод bool(string, out decimal). RED при 0 или >=2.
// G2/G4 — golden-таблица 17 границ парсера, пин фактического прогона на 2d5c36add; дёргаем рефлексией
//        единственный метод, найденный G1 (NonPublic|Static), имя не литералится, инстанс не создаётся.
// G3 — culture-independence: тот же набор при InvariantCulture / ru-RU / en-US совпадает побитово.
// Отчёт — stdout, первой строкой sha256 загруженного OsEngine.dll. Exit: 0 GREEN, 1 кейс упал, 2 fatal/preflight.
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
        ("Infinity", false, 0m)
    };

    private static int Main(string[] args)
    {
        try
        {
            return Run(args.FirstOrDefault() ?? "");
        }
        catch (Exception error)
        {
            Console.WriteLine("FATAL: " + error);
            return 2;
        }
    }

    private static int Run(string filter)
    {
        Assembly engineAssembly = typeof(MfdServerRealization).Assembly;
        Console.WriteLine("sha256 OsEngine.dll=" + Sha256File(engineAssembly.Location));
        Console.WriteLine("loaded: " + engineAssembly.Location);

        Type targetType = typeof(MfdServerRealization);
        MethodInfo[] hits = FindDecimalHelpers(targetType);
        Console.WriteLine("G1 candidates: " + (hits.Length == 0
            ? "(нет)"
            : string.Join(", ", hits.Select(x => x.Name + " [" + Visibility(x) + (x.IsStatic ? " static" : " instance") + "]"))));
        bool runGolden = filter == "" || filter == "g2" || filter == "g4";
        if (hits.Length != 1)
        {
            Console.WriteLine("G1 FAIL: ожидается ровно 1 метод bool(string, out decimal) в MfdServerRealization, найдено "
                + hits.Length + " (дедуп PR 2275 врастает обратно / пропал)");
            Console.WriteLine("RESULT RED exit=1");
            return 1;
        }
        MethodInfo target = hits[0];
        Console.WriteLine("G1 PASS: ровно 1 метод");
        if (!target.IsStatic && runGolden)
        {
            Console.WriteLine("FATAL: единственный хелпер не static — контракт G4 (инстанс не создаём) нарушен, golden не исполняется");
            return 2;
        }

        bool green = true;
        if (runGolden)
        {
            green &= RunGoldenCases(target, "G2");
        }
        if (filter == "" || filter == "g3")
        {
            green &= RunCultureIndependence(target, "G3");
        }
        if (filter == "" || filter == "g4")
        {
            Console.WriteLine("G4 PASS: golden-кейсы G2/G3 исполнялись рефлексией по NonPublic|Static-мишени "
                + target.Name + " с target=null (инстанс MfdServerRealization не создавался)");
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
            (bool Ok, string Value) actual = InvokeParse(target, expected.Input);
            bool rowPass = actual.Ok == expected.Ok
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

    // G3: golden под InvariantCulture/ru-RU/en-US — (bool, значение) совпадают побитово с инвариантным прогоном.
    private static bool RunCultureIndependence(MethodInfo target, string label)
    {
        CultureInfo oldCulture = CultureInfo.CurrentCulture;
        CultureInfo oldUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            List<(bool, string)> baseline = InvokeSet(target);

            bool pass = true;
            foreach (string cultureName in new[] { "ru-RU", "en-US" })
            {
                CultureInfo culture = new CultureInfo(cultureName);
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
                List<(bool, string)> actual = InvokeSet(target);
                var diffs = new List<string>();
                for (int i = 0; i < GoldenTable.Length; i++)
                {
                    if (actual[i] != baseline[i])
                    {
                        diffs.Add(Quote(GoldenTable[i].Input) + ": inv=" + baseline[i] + " vs " + cultureName + "=" + actual[i]);
                    }
                }
                Console.WriteLine(label + " " + cultureName + ": " + (diffs.Count == 0
                    ? GoldenTable.Length + "/" + GoldenTable.Length + " совпадает с InvariantCulture"
                    : "ОТЛИЧИЯ: " + string.Join(" | ", diffs)));
                pass &= diffs.Count == 0;
            }
            Console.WriteLine(label + (pass ? " PASS" : " FAIL"));
            return pass;
        }
        finally
        {
            CultureInfo.CurrentCulture = oldCulture;
            CultureInfo.CurrentUICulture = oldUiCulture;
        }
    }

    private static List<(bool, string)> InvokeSet(MethodInfo target)
    {
        var rows = new List<(bool, string)>();
        foreach ((string Input, bool Ok, decimal Value) entry in GoldenTable)
        {
            rows.Add(InvokeParse(target, entry.Input));
        }
        return rows;
    }

    private static (bool, string) InvokeParse(MethodInfo target, string input)
    {
        try
        {
            object[] arguments = { input, null };
            bool ok = (bool)target.Invoke(null, arguments);
            return (ok, ((decimal)arguments[1]).ToString("0.####################", CultureInfo.InvariantCulture));
        }
        catch (Exception error)
        {
            return (false, "EX:" + error.GetBaseException().GetType().Name);
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
        return value == null ? "null" : "\"" + value + "\"";
    }
}
