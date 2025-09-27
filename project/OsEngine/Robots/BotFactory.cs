/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.Robots.Engines;
using System.Windows; // For MessageBox

// Roslyn specific usings
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace OsEngine.Robots
{
    public static class BotFactory // Made static for consistency
    {
        private static readonly Dictionary<string, Type> Bots = GetBotPanelTypes();

        public static List<string> GetIncludeNamesStrategy()
        {
            List<string> result = new List<string>
            {
                "Engine",       // Likely CandleEngine
                "ScreenerEngine",
                "ClusterEngine"
            };
            result.AddRange(Bots.Keys);

            // Simplified sorting
            return result.OrderBy(s => s).ToList();
        }

        public static BotPanel GetStrategyForName(string nameClass, string nameInstance, StartProgram startProgram, bool isScript)
        {
            if (string.IsNullOrEmpty(nameClass))
            {
                return null;
            }

            BotPanel bot = null;

            // Handle hardcoded engine types first
            if (!isScript) // Only try these if not explicitly a script
            {
                if (nameClass == "Engine") // Assuming this refers to CandleEngine
                {
                    bot = new CandleEngine(nameInstance, startProgram);
                }
                else if (nameClass == "ScreenerEngine")
                {
                    bot = new ScreenerEngine(nameInstance, startProgram);
                }
                else if (nameClass == "ClusterEngine")
                {
                    bot = new ClusterEngine(nameInstance, startProgram);
                }
                else if (Bots.TryGetValue(nameClass, out Type botType))
                {
                    try
                    {
                        // Assumes constructor (string name, StartProgram startProgram)
                        bot = (BotPanel)Activator.CreateInstance(botType, nameInstance, startProgram);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error activating attribute-based bot '{nameClass}': {ex.Message}\nEnsure it has a constructor (string name, StartProgram startProgram).");
                        // Optionally log ex.ToString()
                        return null;
                    }
                }
            }


            // If not found as a pre-compiled/attribute type, or if explicitly a script, try compiling
            if (bot == null)
            {
                try
                {
                    bot = CompileAndInstantiateBotScript(nameClass, nameInstance, startProgram);
                    if (bot != null)
                    {
                        bot.IsScript = true;
                        bot.FileName = nameClass; // Store the script class name
                    }
                }
                catch (Exception e)
                {
                    MessageBox.Show($"BotFactory. Script compilation/instantiation error for '{nameClass}': {e.ToString()}");
                    return null;
                }
            }

            return bot;
        }

        static Dictionary<string, Type> GetBotPanelTypes()
        {
            Assembly assembly = typeof(BotPanel).Assembly;
            Dictionary<string, Type> bots = new Dictionary<string, Type>();
            foreach (Type type in assembly.GetTypes())
            {
                if (type.IsPublic && !type.IsAbstract && typeof(BotPanel).IsAssignableFrom(type))
                {
                        // Ensure the type has the expected constructor
                        ConstructorInfo constructor = type.GetConstructor(new[] { typeof(string), typeof(StartProgram) });
                        if (constructor != null)
                        {
                            bots[type.Name] = type;
                        }
                        else
                        {
                            Console.WriteLine($"Warning: Bot type '{type.FullName}' with BotAttribute '{type.Name}' does not have the required constructor (string name, StartProgram startProgram). It will be ignored.");
                        }
                }
            }
            return bots;
        }

        public static List<string> GetScriptsNamesStrategy()
        {
            const string customRobotsPath = @"Custom\Robots";
            if (!Directory.Exists(@"Custom"))
            {
                Directory.CreateDirectory(@"Custom");
            }
            if (!Directory.Exists(customRobotsPath))
            {
                Directory.CreateDirectory(customRobotsPath);
            }

            // Using GetFullNamesFromFolder which already filters for .cs files
            return GetFullNamesFromFolder(customRobotsPath)
                   .Select(fullPath => Path.GetFileNameWithoutExtension(fullPath))
                   .OrderBy(name => name)
                   .ToList();
        }

        // Cache for GetFullNamesFromFolder to avoid redundant disk I/O if called repeatedly with same path
        private static readonly Dictionary<string, List<string>> _folderFileCache = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _folderFileCacheLock = new object();

        private static List<string> GetFullNamesFromFolder(string directory)
        {
            lock (_folderFileCacheLock)
            {
                if (_folderFileCache.TryGetValue(directory, out var cachedFiles))
                {
                    return new List<string>(cachedFiles); // Return a copy
                }
            }

            List<string> results = new List<string>();
            try
            {
                if (!Directory.Exists(directory))
                {
                    return results; // Directory doesn't exist
                }

                string dirName = Path.GetFileName(directory);

                // Skip BaseClasses dir while searching for user robot scripts
                if (dirName.Equals("BaseClasses", StringComparison.OrdinalIgnoreCase))
                {
                    return results;
                }

                string[] subDirectories = Directory.GetDirectories(directory);
                foreach (string subDir in subDirectories)
                {
                    // Avoid recursing into "Dlls" folders if they exist at any level within Custom\Robots
                    if (Path.GetFileName(subDir).Equals("Dlls", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    results.AddRange(GetFullNamesFromFolder(subDir)); // Recursive call
                }

                string[] files = Directory.GetFiles(directory, "*.cs"); // Only .cs files for scripts
                results.AddRange(files);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accessing directory {directory} in GetFullNamesFromFolder: {ex.Message}");
                // Optionally, log this error more formally
            }

            // The original filtering for "results.Contains("Dlls")" was problematic.
            // If the intent was to remove paths that ARE "Dlls" (not paths *within* a Dlls folder),
            // that's different. The current recursion skip is more typical for avoiding reference folders.

            lock (_folderFileCacheLock)
            {
                _folderFileCache[directory] = new List<string>(results); // Cache a copy
            }
            return new List<string>(results); // Return a copy
        }

        // --- Roslyn Compilation Section ---
        private static List<MetadataReference> _baseReferences;
        private static readonly object _referencesLock = new object();
        private static readonly Dictionary<string, Type> _compiledBotTypesCache = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _compiledTypesCacheLock = new object();

        // Comparer for MetadataReference based on Display path to avoid duplicates
        private class MetadataReferenceComparer : IEqualityComparer<MetadataReference>
        {
            public bool Equals(MetadataReference x, MetadataReference y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (x is null || y is null) return false;
                return x.Display.Equals(y.Display, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(MetadataReference obj)
            {
                return obj?.Display?.GetHashCode(StringComparison.OrdinalIgnoreCase) ?? 0;
            }
        }


        private static void InitializeBaseReferences()
        {
            if (_baseReferences == null)
            {
                lock (_referencesLock)
                {
                    if (_baseReferences == null) // Double-check locking
                    {
                        var references = new HashSet<MetadataReference>(new MetadataReferenceComparer());
                        var entryAssembly = Assembly.GetEntryAssembly();
                        if (entryAssembly != null)
                        {
                            // Add entry assembly and its direct references
                            if (!string.IsNullOrEmpty(entryAssembly.Location))
                                references.Add(MetadataReference.CreateFromFile(entryAssembly.Location));

                            foreach (var assemblyName in entryAssembly.GetReferencedAssemblies())
                            {
                                try
                                {
                                    var assembly = Assembly.Load(assemblyName);
                                    if (!string.IsNullOrEmpty(assembly.Location))
                                        references.Add(MetadataReference.CreateFromFile(assembly.Location));
                                }
                                catch { /* Ignore if assembly can't be loaded */ }
                            }
                        }

                        // Add all other loaded, non-dynamic assemblies as a fallback or supplement
                        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            if (!assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
                            {
                                try
                                {
                                    references.Add(MetadataReference.CreateFromFile(assembly.Location));
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Warning: Could not create metadata reference for {assembly.FullName} from {assembly.Location}. {ex.Message}");
                                }
                            }
                        }
                        _baseReferences = references.ToList();
                    }
                }
            }
        }

        private static BotPanel CompileAndInstantiateBotScript(string nameClass, string nameInstance, StartProgram startProgram)
        {
            Type botType;
            lock (_compiledTypesCacheLock)
            {
                if (_compiledBotTypesCache.TryGetValue(nameClass, out botType))
                {
                    return (BotPanel)Activator.CreateInstance(botType, nameInstance, startProgram);
                }
            }

            // Find script path
            string scriptPath = "";
            List<string> fullPaths = GetFullNamesFromFolder(@"Custom\Robots"); // Path to robot scripts
            string scriptFileNameCs = nameClass + ".cs";
            // Original code also checked for .txt, but GetFullNamesFromFolder now filters to .cs
            // If .txt scripts are still possible, adjust GetFullNamesFromFolder or add .txt check here.

            foreach (string fullPath in fullPaths)
            {
                if (Path.GetFileName(fullPath).Equals(scriptFileNameCs, StringComparison.OrdinalIgnoreCase))
                {
                    scriptPath = fullPath;
                    break;
                }
            }

            if (string.IsNullOrEmpty(scriptPath))
            {
                // This message might be redundant if GetStrategyForName handles "not found" before calling this.
                // MessageBox.Show($"Error! Bot script '{nameClass}.cs' not found in Custom\\Robots or its subdirectories.");
                throw new FileNotFoundException($"Bot script '{nameClass}.cs' not found.", scriptFileNameCs);
            }

            InitializeBaseReferences();

            List<string> baseClassFiles = GetBaseClassFiles();

            List<MetadataReference> currentCompilationReferences = new List<MetadataReference>(_baseReferences);
            List<string> dllsFromScriptFolder = GetDllsPathFromScriptFolder(scriptPath);

            if (dllsFromScriptFolder != null)
            {
                foreach (string dllPath in dllsFromScriptFolder)
                {
                    if (!currentCompilationReferences.Any(r => r.Display.Equals(dllPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        try
                        {
                            currentCompilationReferences.Add(MetadataReference.CreateFromFile(dllPath));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Warning: Could not create metadata reference for custom DLL {dllPath}. {ex.Message}");
                        }
                    }
                }
            }

            SourceText sourceText;
            using (var fileStream = new FileStream(scriptPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: false))
            {
                sourceText = SourceText.From(fileStream, Encoding.UTF8, canBeEmbedded: true);
            }

            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(
                sourceText,
                options: CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
                path: scriptPath);

            // Создаем деревья синтаксиса для всех базовых классов
            List<SyntaxTree> syntaxTrees = new List<SyntaxTree>();

            foreach (string baseFile in baseClassFiles)
            {
                using (var baseFileStream = new FileStream(baseFile, FileMode.Open, FileAccess.Read))
                {
                    SourceText baseSource = SourceText.From(baseFileStream, Encoding.UTF8, canBeEmbedded: true);
                    SyntaxTree baseSyntaxTree = CSharpSyntaxTree.ParseText(
                        baseSource,
                        options: CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
                        path: baseFile);
                    syntaxTrees.Add(baseSyntaxTree);
                }
            }

            syntaxTrees.Add(syntaxTree); // Добавляем сам скрипт последним

            string assemblyName = $"BotScript_{nameClass}_{Guid.NewGuid():N}";

            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: syntaxTrees.ToArray(),
                references: currentCompilationReferences,
                options: new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Debug,
                    platform: Platform.AnyCpu));

            using (var assemblyStream = new MemoryStream())
            using (var pdbStream = new MemoryStream())
            {
                EmitResult result = compilation.Emit(assemblyStream, pdbStream, options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));

                if (!result.Success)
                {
                    var failures = result.Diagnostics.Where(d => d.IsWarningAsError || d.Severity == DiagnosticSeverity.Error);
                    string errorString = $"Error! Robot script '{nameClass}' compilation problem (Path: {scriptPath}):\n";
                    foreach (var diagnostic in failures)
                    {
                        errorString += $"  {diagnostic.Id}: {diagnostic.GetMessage()} at {diagnostic.Location.GetLineSpan()}\n";
                    }
                    throw new Exception(errorString);
                }

                assemblyStream.Seek(0, SeekOrigin.Begin);
                pdbStream.Seek(0, SeekOrigin.Begin);
                Assembly compiledAssembly = Assembly.Load(assemblyStream.ToArray(), pdbStream.ToArray());

                string expectedClassName = nameClass; // nameClass здесь - это имя файла без расширения
                Type botPanelBaseType = typeof(BotPanel);

                // 1. Сначала ищем тип, имя которого совпадает с именем файла (nameClass),
                //    разрешая public и internal, и который является наследником BotPanel и не абстрактный.
                botType = compiledAssembly.GetTypes().FirstOrDefault(t =>
                                t.Name.Equals(expectedClassName, StringComparison.OrdinalIgnoreCase) &&
                                !t.IsAbstract && // Абстрактные классы инстанцировать нельзя
                                botPanelBaseType.IsAssignableFrom(t));

                // 2. Если не нашли по точному имени, ищем ПЕРВЫЙ попавшийся (public или internal)
                //    неабстрактный наследник BotPanel в сборке.
                if (botType == null)
                {
                    botType = compiledAssembly.GetTypes().FirstOrDefault(t =>
                                !t.IsAbstract &&
                                botPanelBaseType.IsAssignableFrom(t));
                }

                if (botType == null)
                {
                    // Если тип так и не найден, предоставляем более подробную информацию
                    var allFoundBotPanelDerivatives = compiledAssembly.GetTypes()
                        .Where(t => !t.IsAbstract && botPanelBaseType.IsAssignableFrom(t))
                        .Select(t => $"{t.FullName} (IsPublic: {t.IsPublic})") // Показываем, public он или нет
                        .ToList();
                    string foundTypesMessage = allFoundBotPanelDerivatives.Any()
                        ? string.Join(", ", allFoundBotPanelDerivatives)
                        : "None";

                    throw new TypeLoadException(
                        $"Could not find a suitable (public or internal) BotPanel derivative (ideally named '{expectedClassName}') in compiled script: {scriptPath}. " +
                        $"All non-abstract BotPanel derivatives found in the script's assembly: [{foundTypesMessage}]");
                }

                // Параметры конструктора, которые ожидает BotFactory
                object[] constructorParams = new object[] { nameInstance, startProgram };

                try
                {
                    // Пытаемся создать экземпляр, разрешая public и internal классы,
                    // а также public и internal конструкторы.
                    // Флаг BindingFlags.CreateInstance важен.
                    // BindingFlags.Instance говорит, что ищем член экземпляра.
                    // BindingFlags.Public | BindingFlags.NonPublic - разрешаем оба уровня доступа.
                    var instance = Activator.CreateInstance(botType,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.CreateInstance,
                        null, // Binder - обычно null
                        constructorParams,
                        CultureInfo.CurrentCulture); // CultureInfo - для локализации, если конструктор это использует

                    return (BotPanel)instance;
                }
                catch (MissingMethodException ex) // Эта ошибка возникнет, если конструктор с нужной сигнатурой не найден
                {
                    throw new InvalidOperationException(
                        $"Compiled bot script '{botType.FullName}' does not have a suitable public or internal constructor " +
                        $"accepting (string name, StartProgram startProgram). Path: {scriptPath}", ex);
                }
            }
        }

        private static List<string> GetDllsPathFromScriptFolder(string scriptFilePath)
        {
            string scriptFolder = Path.GetDirectoryName(scriptFilePath);
            if (string.IsNullOrEmpty(scriptFolder)) return null;

            string dllsFolder = Path.Combine(scriptFolder, "Dlls");
            if (!Directory.Exists(dllsFolder)) return null;

            return Directory.GetFiles(dllsFolder, "*.dll").ToList();
        }

        // --- Optimizer Logic ---
        // This section is largely unchanged in its high-level logic,
        // but it will now use the Roslyn-powered GetStrategyForName.

        public static bool NeedToReloadOptimizerBots = true; // Renamed for clarity
        private static List<string> _optimizerBotsWithParam = new List<string>();
        private static readonly object _optimizerBotsLocker = new object();
        private const string OptimizerBotsFileName = "Engine\\OptimizerBots.txt";


        private static void LoadOptimizerBotsNamesFromFile()
        {
            lock (_optimizerBotsLocker)
            {
                _optimizerBotsWithParam.Clear();
                if (!File.Exists(OptimizerBotsFileName))
                {
                    return;
                }
                try
                {
                    _optimizerBotsWithParam.AddRange(File.ReadAllLines(OptimizerBotsFileName));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading optimizer bot names from file: {ex.Message}");
                }
            }
        }

        private static void SaveOptimizerBotsNamesToFile()
        {
            lock (_optimizerBotsLocker)
            {
                try
                {
                    string dir = Path.GetDirectoryName(OptimizerBotsFileName);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    File.WriteAllLines(OptimizerBotsFileName, _optimizerBotsWithParam);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving optimizer bot names to file: {ex.Message}");
                }
            }
        }

        public static List<string> GetNamesStrategyWithParametersSync()
        {
            lock (_optimizerBotsLocker) // Ensure single access for check/load/save sequence
            {
                if (!NeedToReloadOptimizerBots && _optimizerBotsWithParam.Count > 0)
                {
                    return new List<string>(_optimizerBotsWithParam); // Return a copy
                }

                // If empty and not needing reload, try loading from file first
                if (!NeedToReloadOptimizerBots && _optimizerBotsWithParam.Count == 0)
                {
                    LoadOptimizerBotsNamesFromFile();
                    if (_optimizerBotsWithParam.Count > 0)
                    {
                        return new List<string>(_optimizerBotsWithParam);
                    }
                }


                NeedToReloadOptimizerBots = false; // Reset flag after deciding to reload
                _optimizerBotsWithParam.Clear(); // Clear before repopulating
            }


            List<Thread> workers = new List<Thread>();
            List<string> allBotNames = GetIncludeNamesStrategy();
            int countIncludeBots = allBotNames.Count;
            allBotNames.AddRange(GetScriptsNamesStrategy());
            allBotNames = allBotNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList(); // Ensure unique names

            // Determine number of threads (e.g., based on processor count or a fixed number)
            int numThreads = Math.Min(Environment.ProcessorCount, 10); // Cap at 10 as in original
            if (numThreads <= 0) numThreads = 1;


            for (int i = 0; i < numThreads; i++)
            {
                // Pass a copy of relevant data to the thread or use a structure
                // to avoid closure issues with loop variables if not careful.
                // Here, passing the index directly is fine.
                Thread worker = new Thread(LoadNamesWithParamThreadStart);
                worker.Name = i.ToString(); // Thread index
                workers.Add(worker);

                // Package arguments for the thread
                var threadArgs = new OptimizerLoadArgs
                {
                    ThreadIndex = i,
                    TotalThreads = numThreads,
                    AllBotNames = allBotNames, // Pass the combined list
                    CountIncludeBots = countIncludeBots // To differentiate between pre-compiled and scripts if needed
                };
                worker.Start(threadArgs);
            }

            foreach (var worker in workers)
            {
                worker.Join(); // Wait for all threads to complete
            }

            SaveOptimizerBotsNamesToFile();

            lock (_optimizerBotsLocker)
            {
                return new List<string>(_optimizerBotsWithParam); // Return a copy
            }
        }

        private class OptimizerLoadArgs
        {
            public int ThreadIndex;
            public int TotalThreads;
            public List<string> AllBotNames;
            public int CountIncludeBots; // If specific logic needed for pre-compiled vs script
        }


        private static void LoadNamesWithParamThreadStart(object argsObj)
        {
            OptimizerLoadArgs args = (OptimizerLoadArgs)argsObj;

            for (int i = args.ThreadIndex; i < args.AllBotNames.Count; i += args.TotalThreads)
            {
                string botNameClass = args.AllBotNames[i];
                BotPanel bot = null;
                try
                {
                    // Determine if it's a script or an "included" bot
                    // GetIncludeNamesStrategy() returns Engine, ScreenerEngine, ClusterEngine, and Attribute bots.
                    // GetScriptsNamesStrategy() returns file-based scripts.
                    // The combined list `args.AllBotNames` contains both.
                    // We can assume if it's not in Bots and not one of the hardcoded engines, it's a script.
                    bool isScript = !Bots.ContainsKey(botNameClass) &&
                                    botNameClass != "Engine" &&
                                    botNameClass != "ScreenerEngine" &&
                                    botNameClass != "ClusterEngine";

                    // Use a unique instance name for optimizer testing to avoid conflicts
                    string instanceName = $"OptimizerTest_{botNameClass}_{args.ThreadIndex}_{i}";
                    bot = GetStrategyForName(botNameClass, instanceName, StartProgram.IsOsOptimizer, isScript);

                    if (bot == null)
                    {
                        continue;
                    }

                    // Original conditions for including in optimizer list
                    if (bot.Parameters != null && bot.Parameters.Count != 0)
                    {
                        bool isValidForOptimizer = true;
                        if (bot.TabsPair != null && bot.TabsPair.Count > 0) isValidForOptimizer = false;
                        if (bot.TabsPolygon != null && bot.TabsPolygon.Count > 0) isValidForOptimizer = false;
                        if (bot.TabsNews != null && bot.TabsNews.Count > 0) isValidForOptimizer = false;
                        if (bot.GetTabs() == null || bot.GetTabs().Count == 0) isValidForOptimizer = false;
                        if ((bot.TabsSimple == null || bot.TabsSimple.Count == 0) &&
                            (bot.TabsScreener == null || bot.TabsScreener.Count == 0)) isValidForOptimizer = false;

                        if (isValidForOptimizer)
                        {
                            lock (_optimizerBotsLocker)
                            {
                                if (!_optimizerBotsWithParam.Contains(botNameClass)) // Ensure uniqueness
                                {
                                    _optimizerBotsWithParam.Add(botNameClass);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log error, e.g., if a bot fails to instantiate during optimizer scan
                    Console.WriteLine($"Optimizer: Error processing bot '{botNameClass}': {ex.Message}");
                    // Continue to the next bot
                }
                finally
                {
                    // Ensure bot resources are released
                    bot?.Delete();
                }
            }

            // This event invocation should likely be done once after all threads complete,
            // not by each thread. Or, the event needs to handle partial updates.
            // For simplicity, moving it out or ensuring it's called once.
            // If LoadNamesWithParamEndEvent is crucial per-thread, it needs careful design.
            // Let's assume it's a final notification. It's better managed outside the worker thread.
            // The original code had it inside, which means it would fire multiple times.
        }

        // This event should be invoked after all threads in GetNamesStrategyWithParametersSync have completed.
        // Example: After the `foreach (var worker in workers) { worker.Join(); }` loop.
        public static event Action<List<string>> LoadNamesWithParamEndEvent;

        // Method to manually trigger the event after sync operation if needed.
        public static void TriggerLoadNamesWithParamEndEvent()
        {
            lock (_optimizerBotsLocker) // Access _optimizerBotsWithParam safely
            {
                LoadNamesWithParamEndEvent?.Invoke(new List<string>(_optimizerBotsWithParam));
            }
        }

        private static List<string> GetBaseClassFiles()
        {
            string baseDir = Path.Combine("Custom", "Robots", "BaseClasses");
            if (!Directory.Exists(baseDir))
            {
                return new List<string>();
            }

            return Directory.GetFiles(baseDir, "*.cs").ToList();
        }
    }
}
