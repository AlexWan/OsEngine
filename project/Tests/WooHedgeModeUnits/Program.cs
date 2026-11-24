// BUG-0651 / bug-0655 - юниты S1-S3: маппинг галки "Hedge Mode" -> WooServerRealization.HedgeMode.
// ТЗ тестовых стендов: plan-20261124-010 (Одобрятор тестовых стендов).
//
// Голова бага: WooServer подписан ServerParameters[2].ValueChange (WooServer.cs :36), а обработчик читал
// ServerParameters[3] ("Extended Data"). На живой бирже (tr380) режим уходил в PUT не от галки, а при
// "Extended Data"=true - с инверсией. Фикс: обработчик читает [2].
//
// Дискриминатор S1: посев [3]=true + смена [2] true->false. На unfixed-базе хеле возьмет [3] -> поле
// станет true -> FAIL. На фиксе поле следует [2] -> остаётся false -> PASS. Второй такт (посев [3]=false +
// [2] false->true) ловит ту же подмену в обратную сторону.
// ЛОВУШКА, из-за которой наивный тест падает НА ФИКСЕ: default [2]=true, а сеттер ServerParameterBool
// не поднимает ValueChange при равном значении (ServerParameter.cs:226-229) -> шаг "[2]=true, жду true"
// есть no-op. Отсюда обязателен реальный toggle + счётчик-подписка на [2] как premise-probe.
//
// Сети, ключей и живого коннектора нет: new WooServer() без Connect, фоновые треди конструктора спят до
// Connect (:1195, :1521, :1606), а SetPositionMode() коротится guard'ом ServerStatus == Disconnect (:236)
// ДО CreatePrivateQuery -> PUT физически не уходит.
//
// ПОБОЧКА, обязательная к учёту: каждый ValueChange будит и AServer.userChangeParameter_ValueChange ->
// SaveParam() (AServer.cs :705) - файл Engine\<ServerNameUnique>Params.txt ОТНОСИТЕЛЬНО текущего каталога,
// и конструктор читает тот же файл (LoadParam). Поэтому процесс сам ставит CWD в пустой temp и удаляет его
// в конце. Запускать из каталога с живыми Engine\Params.txt нельзя - затрёт настройки.
//
// Выход: 0 - все кейсы зелёные; 1 - расхождение; 2 - preflight (пин имён/индексов, dll не собрана, fatal).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using OsEngine.Logging;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.Woo;

namespace OsEngine.Tests.WooHedgeModeUnits
{
    internal static class Program
    {
        private const string HedgeModeName = "Hedge Mode";
        private const string ExtendedDataName = "Extended Data";

        private static readonly Stopwatch Watch = Stopwatch.StartNew();
        private static readonly object Sync = new object();
        private static readonly string EngineBin = FindEngineBin();

        private static int _checks;
        private static int _failures;
        private static int _preflightFailures;
        private static bool _fatal;
        private static string _tempCwd;

        private static int Main(string[] args)
        {
            string mode = "all";
            string expectSha = "";

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--expect-sha" && i + 1 < args.Length)
                {
                    expectSha = args[i + 1].ToLowerInvariant();
                    i++;
                }
                else
                {
                    mode = args[i].ToLowerInvariant();
                }
            }

            try
            {
                Console.OutputEncoding = Encoding.UTF8;
            }
            catch (Exception)
            {
                // консоль может не принимать кодировку - отчёта не лишает
            }

            AppDomain.CurrentDomain.UnhandledException += delegate (object sender, UnhandledExceptionEventArgs e)
            {
                lock (Sync)
                {
                    _fatal = true;
                }
                Console.WriteLine("FATAL unhandled: " + e.ExceptionObject);
            };
            TaskScheduler.UnobservedTaskException += delegate (object sender, UnobservedTaskExceptionEventArgs e)
            {
                lock (Sync)
                {
                    _fatal = true;
                }
                Console.WriteLine("FATAL task: " + e.Exception);
            };

            try
            {
                IsolateCurrentDirectory();
                PrintEnginePins(expectSha);

                Fixture probe = new Fixture();
                Preflight(probe);

                if (_preflightFailures == 0)
                {
                    if (mode == "s1" || mode == "all")
                    {
                        CaseS1();
                    }
                    if (mode == "s2" || mode == "all")
                    {
                        CaseS2();
                    }
                    if (mode == "s3" || mode == "all")
                    {
                        CaseS3();
                    }
                }
                else
                {
                    Console.WriteLine("PREFLIGHT FAIL - пин индексов/дефолтов или dll не та, кейсы не гонялись");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("FATAL preflight: " + e);
                Environment.Exit(2);
                return 2;
            }

            Console.WriteLine("tempdir.cleanup=" + CleanupCurrentDirectory());
            Console.WriteLine("checks=" + _checks + " failures=" + _failures + " preflight_failures=" + _preflightFailures);
            Console.WriteLine("elapsed_ms=" + Watch.ElapsedMilliseconds);

            int code;
            if (_fatal)
            {
                code = 2;
            }
            else if (_preflightFailures > 0)
            {
                code = 2;
            }
            else
            {
                code = _failures == 0 ? 0 : 1;
            }

            // Итог уже напечатан. Явный Exit: фон, поднятый сеттером AServer.ServerRealization
            // (storages, PrimeThreadArea, AnalysisServerAndNonTradePeriodsCheck), удерживает процесс.
            Environment.Exit(code);
            return code;
        }

        // S1 - дискриминатор маппинга галка -> поле. PASS на фиксе, FAIL на unfixed-базе.
        private static void CaseS1()
        {
            Console.WriteLine("-- S1 mapping [2] -> HedgeMode (дискриминатор)");
            Fixture f = new Fixture();

            Check("S1.1 поле false до Connect", !f.Realization.HedgeMode,
                "HedgeMode=" + f.Realization.HedgeMode);

            f.ExtParam.Value = true;
            Check("S1.2 посев [3]=true инертен (подписки нет)", !f.Realization.HedgeMode && f.Changes == 0,
                "HedgeMode=" + f.Realization.HedgeMode + " changes=" + f.Changes);

            f.HedgeParam.Value = false;
            Check("S1.3 событие [2] реально было (premise против no-op)", f.Changes == 1,
                "changes=" + f.Changes);
            Check("S1.4 галка off -> поле false (на базе RED: true из [3])", !f.Realization.HedgeMode,
                "HedgeMode=" + f.Realization.HedgeMode + " [2]=" + f.HedgeParam.Value + " [3]=" + f.ExtParam.Value);

            f.ExtParam.Value = false;
            f.HedgeParam.Value = true;
            Check("S1.5 галка on -> поле true (на базе RED: false из [3])", f.Realization.HedgeMode,
                "HedgeMode=" + f.Realization.HedgeMode + " [2]=" + f.HedgeParam.Value + " [3]=" + f.ExtParam.Value);

            Check("S1.6 событий ровно по числу реальных смен [2]", f.Changes == 2, "changes=" + f.Changes);
            Check("S1.7 Error-строк нет (PUT коротится Disconnect-guard'ом)", f.ErrorCount == 0,
                "errors=" + f.ErrorCount);
            Check("S1.8 SaveParam отработал (Engine\\WooParams.txt)", f.ParamsSaved(HedgeModeName + "^True"),
                "file=" + f.ParamsFileState);
        }

        // S2 - отвязка [3]: возврат подписки на "Extended Data" будет пойман и тут. Зелёный на обеих ветках.
        private static void CaseS2()
        {
            Console.WriteLine("-- S2 [3] не двигает режим");
            Fixture f = new Fixture();

            f.ExtParam.Value = true;
            f.ExtParam.Value = false;

            Check("S2.1 toggle только [3] -> поле и счётчик [2] не тронуты",
                !f.Realization.HedgeMode && f.Changes == 0,
                "HedgeMode=" + f.Realization.HedgeMode + " changes=" + f.Changes);
            Check("S2.2 Error-строк и PUT-попыток нет", f.ErrorCount == 0, "errors=" + f.ErrorCount);
        }

        // S3 - слой де-дупа N1 (равное значение параметра) и N2 (равнее значение поля).
        private static void CaseS3()
        {
            Console.WriteLine("-- S3 фиктивные значения");
            Fixture f = new Fixture();

            f.HedgeParam.Value = true;
            Check("S3.1 равное значение [2] -> ни события, ни изменения поля",
                f.Changes == 0 && !f.Realization.HedgeMode,
                "changes=" + f.Changes + " HedgeMode=" + f.Realization.HedgeMode);

            f.Realization.HedgeMode = false;
            Check("S3.2 прямой сеттер с равным полем -> SetPositionMode не позван (Error-строк нет)",
                f.ErrorCount == 0 && !f.Realization.HedgeMode,
                "errors=" + f.ErrorCount + " HedgeMode=" + f.Realization.HedgeMode);
        }

        private static void Preflight(Fixture f)
        {
            Check("P0 собранный dll движка == dll рядом с юнитами", f.EngineBinMatches, f.EngineBinDetail, true);
            Check("P0b --expect-sha", f.ExpectShaMatches, f.ExpectShaDetail, true);

            PreflightCheck("P1 параметров >= 4", f.Server.ServerParameters.Count >= 4,
                "count=" + f.Server.ServerParameters.Count);
            PreflightCheck("P2 [2] имеет Name \"" + HedgeModeName + "\"",
                IndexOfName(f.Server, HedgeModeName) == 2, "index=" + IndexOfName(f.Server, HedgeModeName));
            PreflightCheck("P3 [3] имеет Name \"" + ExtendedDataName + "\"",
                IndexOfName(f.Server, ExtendedDataName) == 3, "index=" + IndexOfName(f.Server, ExtendedDataName));
            PreflightCheck("P4 default [2]=true (иначе S1 - не дискриминатор)", f.HedgeParam.Value,
                "[2]=" + f.HedgeParam.Value);
            PreflightCheck("P5 default [3]=false", !f.ExtParam.Value, "[3]=" + f.ExtParam.Value);
            PreflightCheck("P6 ServerStatus=Disconnect (headless, сети нет)",
                f.Realization.ServerStatus == ServerConnectStatus.Disconnect,
                "status=" + f.Realization.ServerStatus);
            PreflightCheck("P7 ключи пустые (PUT не мог бы уйти)",
                ((ServerParameterString)f.Server.ServerParameters[0]).Value.Length == 0
                && ((ServerParameterPassword)f.Server.ServerParameters[1]).Value.Length == 0,
                "keys set on [0]/[1]");
        }

        private static int IndexOfName(AServer server, string name)
        {
            for (int i = 0; i < server.ServerParameters.Count; i++)
            {
                if (server.ServerParameters[i].Name == name)
                {
                    return i;
                }
            }
            return -1;
        }

        private static void IsolateCurrentDirectory()
        {
            _tempCwd = Path.Combine(Path.GetTempPath(),
                "osengine-woo-units-" + Guid.NewGuid().ToString("N").Substring(0, 12));
            Directory.CreateDirectory(_tempCwd);
            Directory.SetCurrentDirectory(_tempCwd);
            // Пустой Engine\ обязателен: AServer.SaveParam (:709) и Log пишут в Engine\ относительно CWD,
            // а без каталога StreamWriter бросает DirectoryNotFoundException, который catch (:742) молча съедает.
            // Файлов параметров внутри нет -> LoadParam даёт дефолты (premise P4/P5).
            Directory.CreateDirectory(Path.Combine(_tempCwd, "Engine"));
            Console.WriteLine("cwd=" + _tempCwd);
            Console.WriteLine("cwd.engine=" + (Directory.GetFiles(Path.Combine(_tempCwd, "Engine")).Length == 0
                ? "empty" : "NOT EMPTY (не чищ!!)"));
        }

        private static string CleanupCurrentDirectory()
        {
            try
            {
                Directory.SetCurrentDirectory(Path.GetTempPath());
                Directory.Delete(_tempCwd, true);
                return "ok";
            }
            catch (Exception e)
            {
                return "failed " + e.GetType().Name + ": " + e.Message;
            }
        }

        private static void PrintEnginePins(string expectSha)
        {
            Console.WriteLine("osengine.location=" + typeof(WooServer).Assembly.Location);
            Console.WriteLine("osengine.sha256=" + Sha256File(typeof(WooServer).Assembly.Location));
            Console.WriteLine("enginebin=" + (EngineBin ?? "not-found"));

            if (EngineBin != null)
            {
                AssemblyLoadContext.Default.Resolving += delegate (AssemblyLoadContext context, AssemblyName name)
                {
                    string path = Path.Combine(EngineBin, name.Name + ".dll");
                    return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
                };
            }

            if (expectSha.Length > 0)
            {
                Fixture.ExpectSha = expectSha;
                Console.WriteLine("expect-sha=" + expectSha);
            }
        }

        // каталог bin движка: вверх от каталога юнитов до project\, где OsEngine\bin\Debug
        private static string FindEngineBin()
        {
            DirectoryInfo dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int i = 0; i < 8 && dir != null; i++)
            {
                string candidate = Path.Combine(dir.FullName, "OsEngine", "bin", "Debug");
                if (File.Exists(Path.Combine(candidate, "OsEngine.dll")))
                {
                    return candidate;
                }
                dir = dir.Parent;
            }
            return null;
        }

        private static string Sha256File(string path)
        {
            using (FileStream fs = File.OpenRead(path))
            {
                return Convert.ToHexString(SHA256.HashData(fs)).ToLowerInvariant();
            }
        }

        private static void Check(string name, bool ok, string detail)
        {
            Check(name, ok, detail, false);
        }

        private static void Check(string name, bool ok, string detail, bool preflight)
        {
            _checks++;
            if (!ok)
            {
                if (preflight)
                {
                    _preflightFailures++;
                }
                else
                {
                    _failures++;
                }
            }
            Console.WriteLine((ok ? "PASS " : "FAIL ") + name + " | " + detail);
        }

        private static void PreflightCheck(string name, bool ok, string detail)
        {
            Check(name, ok, detail, true);
        }

        /// <summary>Свежий WooServer + spy на Error-логи и счётчик событий [2]. Конструктор в изолированном CWD.</summary>
        private sealed class Fixture
        {
            private readonly List<string> _errors = new List<string>();

            public readonly WooServer Server;
            public readonly WooServerRealization Realization;
            public readonly ServerParameterBool HedgeParam;
            public readonly ServerParameterBool ExtParam;
            public int Changes;

            public Fixture()
            {
                Server = new WooServer();
                Realization = (WooServerRealization)Server.ServerRealization;
                Realization.LogMessageEvent += OnLogMessage;

                HedgeParam = (ServerParameterBool)Server.ServerParameters[2];
                ExtParam = (ServerParameterBool)Server.ServerParameters[3];
                HedgeParam.ValueChange += delegate
                {
                    lock (Sync)
                    {
                        Changes++;
                    }
                };
            }

            public int ErrorCount
            {
                get
                {
                    lock (Sync)
                    {
                        return _errors.Count;
                    }
                }
            }

            public bool EngineBinMatches
            {
                get
                {
                    return EngineBin != null && Sha256File(Path.Combine(EngineBin, "OsEngine.dll"))
                        == Sha256File(typeof(WooServer).Assembly.Location);
                }
            }

            public string EngineBinDetail
            {
                get
                {
                    if (EngineBin == null)
                    {
                        return "OsEngine\\bin\\Debug не найден от " + AppContext.BaseDirectory;
                    }
                    return "osengine=" + Sha256File(typeof(WooServer).Assembly.Location).Substring(0, 12)
                        + " enginebin=" + Sha256File(Path.Combine(EngineBin, "OsEngine.dll")).Substring(0, 12);
                }
            }

            /// <summary>Ожидаемый sha256 OsEngine.dll (--expect-sha); пусто - не проверять.</summary>
            public static string ExpectSha = "";

            public bool ExpectShaMatches
            {
                get { return ExpectSha.Length == 0 || Sha256File(typeof(WooServer).Assembly.Location) == ExpectSha; }
            }

            public string ExpectShaDetail
            {
                get { return ExpectSha.Length == 0 ? "не задан" : "loaded=" + Sha256File(typeof(WooServer).Assembly.Location); }
            }

            public string ParamsFileState
            {
                get
                {
                    string path = Path.Combine(Directory.GetCurrentDirectory(), "Engine", "WooParams.txt");
                    return File.Exists(path) ? path : "нет файла";
                }
            }

            public bool ParamsSaved(string line)
            {
                try
                {
                    string path = Path.Combine(Directory.GetCurrentDirectory(), "Engine", "WooParams.txt");
                    if (!File.Exists(path))
                    {
                        return false;
                    }
                    return File.ReadAllText(path).Contains(line);
                }
                catch (Exception)
                {
                    return false;
                }
            }

            private void OnLogMessage(string text, LogMessageType type)
            {
                if (type == LogMessageType.Error)
                {
                    lock (Sync)
                    {
                        _errors.Add(text);
                    }
                }
            }
        }
    }
}
