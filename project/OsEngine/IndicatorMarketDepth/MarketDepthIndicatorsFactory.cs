/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace OsEngine.IndicatorMarketDepth
{
    /// <summary>
    /// Фабрика стаканных индикаторов. Ищет прекомпилированные (внутри сборки)
    /// и кастомные скрипты из Custom/IndicatorsMarketDepth/Scripts (компилируются на лету).
    /// </summary>
    public static class MarketDepthIndicatorsFactory
    {
        public static readonly Dictionary<string, Type> Indicators = GetIndicatorTypes();

        private const string CustomScriptsFolder = @"Custom\IndicatorsMarketDepth\Scripts";

        private static Dictionary<string, Type> GetIndicatorTypes()
        {
            Assembly assembly = typeof(AMarketDepth).Assembly;
            Dictionary<string, Type> indicators = new Dictionary<string, Type>();

            foreach (Type type in assembly.GetTypes())
            {
                if (type.IsPublic && !type.IsAbstract && typeof(AMarketDepth).IsAssignableFrom(type))
                {
                    indicators[type.Name] = type;
                }
            }

            return indicators;
        }

        public static List<string> GetIndicatorsNames()
        {
            List<string> names = new List<string>(Indicators.Keys);

            List<string> scriptNames = GetScriptFileNames();

            for (int i = 0; i < scriptNames.Count; i++)
            {
                if (names.Contains(scriptNames[i], StringComparer.OrdinalIgnoreCase) == false)
                {
                    names.Add(scriptNames[i]);
                }
            }

            names.Sort();
            return names;
        }

        public static bool IsCustomScript(string nameClass)
        {
            return Indicators.ContainsKey(nameClass) == false;
        }

        public static AMarketDepth CreateByName(string nameClass, string name, bool canDelete, StartProgram startProgram = StartProgram.IsOsTrader)
        {
            AMarketDepth indicator = null;

            if (Indicators.TryGetValue(nameClass, out Type type))
            {
                indicator = (AMarketDepth)Activator.CreateInstance(type);
            }

            if (indicator == null)
            {
                indicator = CompileAndInstantiateScript(nameClass);
            }

            if (indicator != null)
            {
                indicator.Init(name, startProgram);
                indicator.CanDelete = canDelete;
            }

            return indicator;
        }

        public static string GetIndicatorDescription(string name)
        {
            try
            {
                AMarketDepth indicator = CreateByName(name, "", false, StartProgram.IsOsOptimizer);

                if (indicator != null)
                {
                    string description = indicator.Description;
                    indicator.Delete();
                    return description;
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }

            return string.Empty;
        }

        public static MarketDepthIndicatorDataSource GetIndicatorDataSource(string name)
        {
            try
            {
                AMarketDepth indicator = CreateByName(name, "", false, StartProgram.IsOsOptimizer);

                if (indicator != null)
                {
                    MarketDepthIndicatorDataSource source = indicator.DataSource;
                    indicator.Delete();
                    return source;
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }

            return MarketDepthIndicatorDataSource.MarketDepth;
        }

        #region Custom scripts

        private static List<string> GetScriptFileNames()
        {
            List<string> result = new List<string>();

            if (Directory.Exists(CustomScriptsFolder) == false)
            {
                Directory.CreateDirectory(CustomScriptsFolder);
                return result;
            }

            string[] files = Directory.GetFiles(CustomScriptsFolder, "*.*", SearchOption.AllDirectories);

            for (int i = 0; i < files.Length; i++)
            {
                string extension = Path.GetExtension(files[i]).ToLowerInvariant();

                if (extension == ".cs" || extension == ".txt")
                {
                    string fileName = Path.GetFileNameWithoutExtension(files[i]);

                    if (result.Contains(fileName, StringComparer.OrdinalIgnoreCase) == false)
                    {
                        result.Add(fileName);
                    }
                }
            }

            return result;
        }

        private static string FindScriptPath(string nameClass)
        {
            if (Directory.Exists(CustomScriptsFolder) == false)
            {
                Directory.CreateDirectory(CustomScriptsFolder);
                return null;
            }

            string[] files = Directory.GetFiles(CustomScriptsFolder, "*.*", SearchOption.AllDirectories);

            for (int i = 0; i < files.Length; i++)
            {
                string fileName = Path.GetFileNameWithoutExtension(files[i]);

                if (fileName.Equals(nameClass, StringComparison.OrdinalIgnoreCase))
                {
                    string extension = Path.GetExtension(files[i]).ToLowerInvariant();

                    if (extension == ".cs" || extension == ".txt")
                    {
                        return files[i];
                    }
                }
            }

            return null;
        }

        private static List<MetadataReference> _baseReferences;

        private static readonly object _referencesLock = new object();

        private static readonly Dictionary<string, Type> _compiledTypesCache = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        private static readonly object _compiledTypesCacheLock = new object();

        private static void InitializeBaseReferences()
        {
            if (_baseReferences != null)
            {
                return;
            }

            lock (_referencesLock)
            {
                if (_baseReferences != null)
                {
                    return;
                }

                List<MetadataReference> references = new List<MetadataReference>();

                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.IsDynamic || string.IsNullOrEmpty(assembly.Location))
                    {
                        continue;
                    }

                    try
                    {
                        references.Add(MetadataReference.CreateFromFile(assembly.Location));
                    }
                    catch
                    {
                        // ignore assemblies that cannot be referenced
                    }
                }

                _baseReferences = references;
            }
        }

        private static AMarketDepth CompileAndInstantiateScript(string nameClass)
        {
            Type indicatorType;

            lock (_compiledTypesCacheLock)
            {
                if (_compiledTypesCache.TryGetValue(nameClass, out indicatorType))
                {
                    return (AMarketDepth)Activator.CreateInstance(indicatorType);
                }
            }

            string scriptPath = FindScriptPath(nameClass);

            if (string.IsNullOrEmpty(scriptPath))
            {
                return null;
            }

            InitializeBaseReferences();

            List<MetadataReference> references = new List<MetadataReference>(_baseReferences);

            List<string> dlls = GetDllsFromScriptFolder(scriptPath);

            if (dlls != null)
            {
                for (int i = 0; i < dlls.Count; i++)
                {
                    try
                    {
                        references.Add(MetadataReference.CreateFromFile(dlls[i]));
                    }
                    catch
                    {
                        // ignore broken dll
                    }
                }
            }

            string sourceCode = File.ReadAllText(scriptPath);

            if (string.IsNullOrWhiteSpace(sourceCode))
            {
                return null;
            }

            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sourceCode,
                CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest));

            string assemblyName = "IndicatorMarketDepth_" + Path.GetFileNameWithoutExtension(scriptPath) + "_" + Guid.NewGuid().ToString("N");

            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Debug,
                    platform: Platform.AnyCpu));

            using (MemoryStream assemblyStream = new MemoryStream())
            using (MemoryStream pdbStream = new MemoryStream())
            {
                EmitResult result = compilation.Emit(assemblyStream, pdbStream,
                    options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));

                if (result.Success == false)
                {
                    IEnumerable<Diagnostic> failures = result.Diagnostics.Where(d =>
                        d.IsWarningAsError || d.Severity == DiagnosticSeverity.Error);

                    string errorText = "Indicator script compilation error (" + scriptPath + "):";

                    foreach (Diagnostic diagnostic in failures)
                    {
                        errorText += Environment.NewLine + "  " + diagnostic.Id + ": " + diagnostic.GetMessage();
                    }

                    ServerMaster.SendNewLogMessage(errorText, LogMessageType.Error);
                    return null;
                }

                assemblyStream.Seek(0, SeekOrigin.Begin);
                pdbStream.Seek(0, SeekOrigin.Begin);

                Assembly compiledAssembly = Assembly.Load(assemblyStream.ToArray(), pdbStream.ToArray());

                indicatorType = compiledAssembly.GetTypes().FirstOrDefault(t =>
                        t.Name.Equals(nameClass, StringComparison.OrdinalIgnoreCase)
                        && typeof(AMarketDepth).IsAssignableFrom(t)
                        && t.IsPublic && !t.IsAbstract)
                    ?? compiledAssembly.GetTypes().FirstOrDefault(t =>
                        typeof(AMarketDepth).IsAssignableFrom(t) && t.IsPublic && !t.IsAbstract);

                if (indicatorType == null)
                {
                    ServerMaster.SendNewLogMessage("Indicator script has no public AMarketDepth type: " + scriptPath, LogMessageType.Error);
                    return null;
                }

                lock (_compiledTypesCacheLock)
                {
                    if (_compiledTypesCache.ContainsKey(nameClass) == false)
                    {
                        _compiledTypesCache[nameClass] = indicatorType;
                    }
                    else
                    {
                        indicatorType = _compiledTypesCache[nameClass];
                    }
                }

                return (AMarketDepth)Activator.CreateInstance(indicatorType);
            }
        }

        private static List<string> GetDllsFromScriptFolder(string scriptFilePath)
        {
            string scriptFolder = Path.GetDirectoryName(scriptFilePath);

            if (string.IsNullOrEmpty(scriptFolder))
            {
                return null;
            }

            string dllsFolder = Path.Combine(scriptFolder, "Dlls");

            if (Directory.Exists(dllsFolder) == false)
            {
                return null;
            }

            return Directory.GetFiles(dllsFolder, "*.dll").ToList();
        }

        #endregion
    }
}
