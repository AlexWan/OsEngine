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
using OsEngine.OsTrader.Panels; // Assuming BotPanel is here

// Roslyn specific usings
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace OsEngine.OsTrader
{
    public sealed class HotUpdateManager
    {
        public static HotUpdateManager Instance => LazyInstance.Value;

        private static readonly Lazy<HotUpdateManager> LazyInstance =
            new Lazy<HotUpdateManager>(() => new HotUpdateManager());

        private static readonly HotUpdateResult<BotPanel> ErrorResultDefault =
            new HotUpdateResult<BotPanel>(null, HotUpdateResultStatus.Error, "Default error result.");

        // Cache key: Tuple<ClassName, StrategyTypeName>, Value: Full path to the source file
        private readonly Dictionary<Tuple<string, string>, string> _classPathCache =
            new Dictionary<Tuple<string, string>, string>();
        private readonly object _cacheLock = new object();


        private HotUpdateManager()
        {
        }

        /// Two different bots must not have the same
        /// class name and strategy name <see cref="BotPanel.GetNameStrategyType"/> at the same time
        public HotUpdateResult<BotPanel> Update(BotPanel botToUpdate)
        {
            if (botToUpdate == null)
            {
                return new HotUpdateResult<BotPanel>(null, HotUpdateResultStatus.Error, "Bot to update cannot be null.");
            }

            // It's generally not recommended to hot-reload nested types via simple file compilation.
            if (botToUpdate.GetType().IsNested)
            {
                return new HotUpdateResult<BotPanel>(null, HotUpdateResultStatus.Error, "Hot update for nested types is not supported.");
            }

            try
            {
                List<string> potentialSourceFiles = SeekForSourceFiles(botToUpdate);

                if (potentialSourceFiles == null || !potentialSourceFiles.Any())
                {
                    return new HotUpdateResult<BotPanel>(null, HotUpdateResultStatus.Error, $"No source files found for bot class '{botToUpdate.GetType().Name}'.");
                }

                HotUpdateResult<BotPanel>? finalResult = null;
                string successfullyUpdatedBotClassPath = null;

                foreach (string sourceFile in potentialSourceFiles)
                {
                    if (!File.Exists(sourceFile))
                    {
                        // Log or handle missing file if it was expected to exist (e.g., from cache)
                        Console.WriteLine($"HotUpdateManager: Source file '{sourceFile}' not found during update attempt.");
                        continue;
                    }

                    HotUpdateResult<BotPanel> currentUpdateAttemptResult = Execute(() =>
                        CompileAndInstantiateBotPanelFromPath(sourceFile, botToUpdate.NameStrategyUniq, botToUpdate.StartProgram));

                    BotPanel updatedBotCandidate = currentUpdateAttemptResult.UpdatedObject;

                    if (currentUpdateAttemptResult.Status == HotUpdateResultStatus.Success && updatedBotCandidate != null)
                    {
                        // Check if the newly compiled bot matches the "strategy type" of the bot we are trying to update.
                        // This is the disambiguation logic from the original code.
                        if (updatedBotCandidate.GetNameStrategyType().Equals(botToUpdate.GetNameStrategyType(), StringComparison.Ordinal))
                        {
                            // If we already found a matching bot from another source file, it's an ambiguity.
                            if (finalResult?.Status == HotUpdateResultStatus.Success && finalResult.Value.UpdatedObject != null)
                            {
                                string errorMessage =
                                    $"Ambiguity: Multiple source files produce a bot with class name '{botToUpdate.GetType().Name}' and strategy type '{botToUpdate.GetNameStrategyType()}'. " +
                                    $"Files: '{successfullyUpdatedBotClassPath}' and '{sourceFile}'.";
                                return new HotUpdateResult<BotPanel>(null, HotUpdateResultStatus.Error, errorMessage);
                            }

                            finalResult = currentUpdateAttemptResult;
                            successfullyUpdatedBotClassPath = sourceFile;
                            // Don't break here if you want to detect ambiguities.
                            // If only the first match is desired, you could break.
                        }
                        else
                        {
                            // Compiled successfully, but it's not the bot we're looking for (different strategy type).
                            // Dispose if necessary or just ignore.
                            updatedBotCandidate.Delete(); // Assuming BotPanel has a dispose/delete method
                        }
                    }
                    else if (currentUpdateAttemptResult.Status == HotUpdateResultStatus.Error)
                    {
                        // Optionally, collect all errors or return on first significant compilation error.
                        // For now, we'll let it try other files if any, but the finalResult will capture the first success or last error.
                        if (finalResult == null || finalResult.Value.Status != HotUpdateResultStatus.Success)
                        {
                            finalResult = currentUpdateAttemptResult; // Keep the error if no success yet
                        }
                        Console.WriteLine($"HotUpdateManager: Compilation/Instantiation failed for '{sourceFile}': {currentUpdateAttemptResult.ErrorMessage}");
                    }
                }

                if (finalResult?.Status == HotUpdateResultStatus.Success && successfullyUpdatedBotClassPath != null)
                {
                    var cacheKey = GetBotCacheKey(botToUpdate.GetType(), botToUpdate.GetNameStrategyType());
                    lock (_cacheLock)
                    {
                        _classPathCache[cacheKey] = successfullyUpdatedBotClassPath;
                    }
                }

                return finalResult ?? new HotUpdateResult<BotPanel>(null, HotUpdateResultStatus.Error, "No suitable bot found or compiled after checking all source files.");
            }
            catch (Exception e)
            {
                Console.WriteLine($"HotUpdateManager: Unhandled exception during update: {e}");
                return new HotUpdateResult<BotPanel>(null, HotUpdateResultStatus.Error, $"Unhandled exception: {e.Message}");
            }
        }

        private Tuple<string, string> GetBotCacheKey(Type botType, string strategyTypeName)
        {
            return Tuple.Create(botType.Name, strategyTypeName);
        }

        private List<string> SeekForSourceFiles(BotPanel bot)
        {
            var cacheKey = GetBotCacheKey(bot.GetType(), bot.GetNameStrategyType());
            lock (_cacheLock)
            {
                if (_classPathCache.TryGetValue(cacheKey, out string cachedPath))
                {
                    if (File.Exists(cachedPath)) // Verify cached path still exists
                    {
                        return new List<string> { cachedPath };
                    }
                    else
                    {
                        _classPathCache.Remove(cacheKey); // Remove invalid cache entry
                    }
                }
            }

            // If not in cache, search in the standard custom robots directory
            string className = bot.GetType().Name;
            string fileNameToSearch = className + ".cs";
            string customRobotsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Custom", "Robots");

            if (!Directory.Exists(customRobotsPath))
            {
                return new List<string>(); // No custom robots directory
            }

            try
            {
                // Search recursively for the .cs file matching the class name
                return Directory.GetFiles(customRobotsPath, fileNameToSearch, SearchOption.AllDirectories).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HotUpdateManager: Error seeking source files in '{customRobotsPath}': {ex}");
                return new List<string>();
            }
        }


        // --- Roslyn Compilation Logic ---
        private static List<MetadataReference> _baseReferences;
        private static readonly object _referencesLock = new object();

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
                                    Console.WriteLine($"HotUpdateManager: Warning - Could not create metadata reference for {assembly.FullName} from {assembly.Location}. {ex.Message}");
                                }
                            }
                        }
                        _baseReferences = references.ToList();
                    }
                }
            }
        }

        private static BotPanel CompileAndInstantiateBotPanelFromPath(string scriptPath, params object[] constructorParams)
        {
            InitializeBaseReferences(); // Ensure references are loaded

            string sourceCode = File.ReadAllText(scriptPath);
            if (string.IsNullOrWhiteSpace(sourceCode))
            {
                throw new InvalidOperationException($"Source code file is empty or could not be read: {scriptPath}");
            }

            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(
                sourceCode,
                CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest));

            string assemblyName = $"HotUpdate_Bot_{Path.GetFileNameWithoutExtension(scriptPath)}_{Guid.NewGuid():N}";

            // Add references specific to this script's "Dlls" folder, if any
            List<MetadataReference> currentCompilationReferences = new List<MetadataReference>(_baseReferences);
            string scriptDllsFolder = Path.Combine(Path.GetDirectoryName(scriptPath), "Dlls");
            if (Directory.Exists(scriptDllsFolder))
            {
                foreach (var dllFile in Directory.GetFiles(scriptDllsFolder, "*.dll"))
                {
                    if (!currentCompilationReferences.Any(r => r.Display.Equals(dllFile, StringComparison.OrdinalIgnoreCase)))
                    {
                        try
                        {
                            currentCompilationReferences.Add(MetadataReference.CreateFromFile(dllFile));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"HotUpdateManager: Warning - Could not create metadata reference for custom DLL {dllFile}. {ex.Message}");
                        }
                    }
                }
            }


            CSharpCompilationOptions compilationOptions = new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Debug, // Or Release; Debug is better for hot-reloading
                platform: Platform.AnyCpu);

            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: new[] { syntaxTree },
                references: currentCompilationReferences, // Use combined list
                options: compilationOptions);

            using (var assemblyStream = new MemoryStream())
            using (var pdbStream = new MemoryStream()) // For debug symbols, helps if debugging reloaded code
            {
                EmitOptions emitOptions = new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb);
                EmitResult result = compilation.Emit(assemblyStream, pdbStream, options: emitOptions);

                if (!result.Success)
                {
                    var failures = result.Diagnostics.Where(d => d.IsWarningAsError || d.Severity == DiagnosticSeverity.Error);
                    string errorString = $"HotUpdateManager: Runtime compilation problem for '{scriptPath}':\n";
                    foreach (var diagnostic in failures)
                    {
                        errorString += $"  {diagnostic.Id}: {diagnostic.GetMessage()} at {diagnostic.Location.GetLineSpan()}\n";
                    }
                    throw new Exception(errorString);
                }

                assemblyStream.Seek(0, SeekOrigin.Begin);
                pdbStream.Seek(0, SeekOrigin.Begin);
                Assembly compiledAssembly = Assembly.Load(assemblyStream.ToArray(), pdbStream.ToArray());

                string expectedClassName = Path.GetFileNameWithoutExtension(scriptPath);
                Type botPanelBaseType = typeof(BotPanel);

                Type typeToInstantiate = compiledAssembly.GetTypes().FirstOrDefault(t =>
                                            t.Name.Equals(expectedClassName, StringComparison.OrdinalIgnoreCase) &&
                                            t.IsPublic && !t.IsAbstract && botPanelBaseType.IsAssignableFrom(t))
                                         ?? compiledAssembly.GetTypes().FirstOrDefault(t => // Fallback
                                            t.IsPublic && !t.IsAbstract && botPanelBaseType.IsAssignableFrom(t));

                if (typeToInstantiate == null)
                {
                    throw new TypeLoadException($"Could not find a public BotPanel derivative (ideally named '{expectedClassName}') in compiled script: {scriptPath}");
                }

                // This uses Activator which can find the constructor based on params.
                // For BotPanel, constructorParams are (string name, StartProgram startProgram)
                object instance = Activator.CreateInstance(typeToInstantiate,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.CreateInstance,
                    null, constructorParams, CultureInfo.CurrentCulture);

                return (BotPanel)instance;
            }
        }
        // --- End Roslyn Compilation Logic ---

        private static HotUpdateResult<T> Execute<T>(Func<T> func)
        {
            try
            {
                T result = func();
                return new HotUpdateResult<T>(result, HotUpdateResultStatus.Success);
            }
            catch (Exception e)
            {
                // Catch specific exceptions from compilation/instantiation if needed for more granular error messages
                return new HotUpdateResult<T>(default, HotUpdateResultStatus.Error, e.Message + (e.InnerException != null ? " Inner: " + e.InnerException.Message : ""));
            }
        }
    }

    public readonly struct HotUpdateResult<T>
    {
        public HotUpdateResult(T updatedObject, HotUpdateResultStatus status, string errorMessage = default)
        {
            UpdatedObject = updatedObject;
            Status = status;
            ErrorMessage = errorMessage ?? string.Empty; // Ensure ErrorMessage is not null
        }

        public T UpdatedObject { get; }
        public HotUpdateResultStatus Status { get; }
        public string ErrorMessage { get; }
    }

    public enum HotUpdateResultStatus
    {
        Success,
        Error
    }
}