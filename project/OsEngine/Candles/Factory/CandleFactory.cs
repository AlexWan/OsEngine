/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.OsTrader.Panels;
using System;
using System.Collections.Generic;
using System.Reflection;
using OsEngine.Candles.Series;
using System.IO;
using System.Linq;
using System.Windows;

// Roslyn specific usings
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace OsEngine.Candles
{
    public static class CandleFactory
    {
        public static List<string> GetCandlesNames()
        {
            if (Directory.Exists(@"Custom") == false)
            {
                Directory.CreateDirectory(@"Custom");
            }
            if (Directory.Exists(@"Custom\CandleSeries") == false)
            {
                Directory.CreateDirectory(@"Custom\CandleSeries");
            }

            List<string> resultOne = GetFullNamesFromFolder(@"Custom\CandleSeries");

            for (int i = 0; i < resultOne.Count; i++)
            {
                resultOne[i] = resultOne[i].Split('\\')[resultOne[i].Split('\\').Length - 1];
                resultOne[i] = resultOne[i].Split('.')[0];
            }

            resultOne.AddRange(_candlesTypes.Keys);

            for (int i = 0; i < resultOne.Count; i++)
            {
                if (resultOne[i] == "Simple")
                {
                    resultOne.RemoveAt(i);
                    resultOne.Insert(0, "Simple");
                    break;
                }
            }

            // This sorting logic seems a bit complex and might not always produce
            // a standard alphabetical sort if that's the intent.
            // Consider using resultOne.Sort() if simple alphabetical is desired after the "Simple" adjustment.
            List<string> resultFolderSort = new List<string>();
            if (resultOne.Count > 0)
            {
                resultFolderSort.Add(resultOne[0]);

                for (int i = 1; i < resultOne.Count; i++)
                {
                    bool isInArray = false;
                    // Start i2 from 0 if comparing with all elements already in resultFolderSort for insertion
                    for (int i2 = 0; i2 < resultFolderSort.Count; i2++) // Adjusted to iterate correctly
                    {
                        if (string.Compare(resultOne[i], resultFolderSort[i2], StringComparison.Ordinal) < 0)
                        {
                            resultFolderSort.Insert(i2, resultOne[i]);
                            isInArray = true;
                            break;
                        }
                    }

                    if (isInArray == false)
                    {
                        resultFolderSort.Add(resultOne[i]);
                    }
                }
            }

            return resultFolderSort;
        }

        public static ACandlesSeriesRealization CreateCandleSeriesRealization(string nameClass)
        {
            ACandlesSeriesRealization series = null;

            try
            {
                if (_candlesTypes.ContainsKey(nameClass))
                {
                    series = (ACandlesSeriesRealization)Activator.CreateInstance(_candlesTypes[nameClass]);
                }

                if (series == null)
                {
                    if (!Directory.Exists(@"Custom\CandleSeries"))
                        Directory.CreateDirectory(@"Custom\CandleSeries");

                    List<string> fullPaths = GetFullNamesFromFolder(@"Custom\CandleSeries");

                    // Scripts can be .cs or .txt
                    string longNameClassTxt = nameClass + ".txt";
                    string longNameClassCs = nameClass + ".cs";

                    string myPath = "";

                    for (int i = 0; i < fullPaths.Count; i++)
                    {
                        string nameInFile = Path.GetFileName(fullPaths[i]);

                        if (nameInFile.Equals(longNameClassTxt, StringComparison.OrdinalIgnoreCase) ||
                            nameInFile.Equals(longNameClassCs, StringComparison.OrdinalIgnoreCase))
                        {
                            myPath = fullPaths[i];
                            break;
                        }
                    }

                    if (string.IsNullOrEmpty(myPath))
                    {
                        // It's better to throw an exception or return null consistently
                        // MessageBox is UI dependent and might not be suitable for all contexts
                        // For now, keeping MessageBox as in original code.
                        MessageBox.Show("Error! Candle series script with name " + nameClass + " not found");
                        return null; // Or throw new FileNotFoundException(...)
                    }

                    series = CompileAndInstantiateScript(myPath, nameClass);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("Error creating candle series realization: " + nameClass + "\n" + e.ToString());
                // Consider logging the exception or re-throwing specific exceptions
            }

            return series;
        }

        private static readonly List<ACandlesSeriesRealization> _compiledScriptInstancesCache = new List<ACandlesSeriesRealization>();
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
                    if (_baseReferences == null)
                    {
                        var references = new HashSet<MetadataReference>(new MetadataReferenceComparer());

                        // Add all assemblies currently loaded that are not dynamic and have a location.
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
                                    // Log this warning, e.g., to Console or a proper logger
                                    Console.WriteLine($"Warning: Could not create metadata reference for {assembly.FullName} from {assembly.Location}. {ex.Message}");
                                }
                            }
                        }
                        _baseReferences = references.ToList();
                    }
                }
            }
        }

        private static ACandlesSeriesRealization CompileAndInstantiateScript(string scriptPath, string nameClass)
        {
            // 1. Try to clone from previously compiled and cached instances
            lock (_compiledScriptInstancesCache) // Ensure thread safety for cache access
            {
                foreach (var cachedInstance in _compiledScriptInstancesCache)
                {
                    if (cachedInstance.GetType().Name == nameClass)
                    {
                        // Create a new instance of the already compiled type
                        return (ACandlesSeriesRealization)Activator.CreateInstance(cachedInstance.GetType());
                    }
                }
            }

            // 2. Compile from file
            InitializeBaseReferences();

            List<MetadataReference> currentCompilationReferences = new List<MetadataReference>(_baseReferences);

            List<string> dllsFromScriptFolder = GetDllsPathFromScriptFolder(scriptPath);
            if (dllsFromScriptFolder != null)
            {
                foreach (string dllPath in dllsFromScriptFolder)
                {
                    // Avoid adding duplicates if _baseReferences already contains it
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

            string sourceCode = ReadFile(scriptPath);
            if (string.IsNullOrWhiteSpace(sourceCode))
            {
                throw new InvalidOperationException($"Source code file is empty or could not be read: {scriptPath}");
            }

            // Parse the source code
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(
                sourceCode,
                CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest)); // Or specific version

            // Define compilation options
            // Using a unique assembly name for each compilation to avoid conflicts if loaded into same context
            string assemblyName = Path.GetRandomFileName();
            CSharpCompilationOptions compilationOptions = new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Debug, // Or Release for production
                platform: Platform.AnyCpu, // Or specific
                warningLevel: 4 // Standard warning level
                                //concurrentBuild: true // Can enable for performance on multi-core
                );

            // Create the compilation
            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: new[] { syntaxTree },
                references: currentCompilationReferences,
                options: compilationOptions);

            using (var assemblyStream = new MemoryStream())
            using (var pdbStream = new MemoryStream()) // For debug symbols
            {
                EmitOptions emitOptions = new EmitOptions(
                    debugInformationFormat: DebugInformationFormat.PortablePdb);
                // pdbFilePath: if you want to save PDBs to disk, specify a path

                EmitResult result = compilation.Emit(assemblyStream, pdbStream, options: emitOptions);

                if (!result.Success)
                {
                    IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic =>
                        diagnostic.IsWarningAsError ||
                        diagnostic.Severity == DiagnosticSeverity.Error);

                    string errorString = $"Error! Script compilation problem for: {scriptPath}\n";
                    int errorNum = 1;
                    foreach (Diagnostic diagnostic in failures)
                    {
                        errorString += $"Error Number: {errorNum}\n";
                        errorString += $"ID: {diagnostic.Id}\n";
                        errorString += $"Message: {diagnostic.GetMessage()}\n";
                        errorString += $"Location: {diagnostic.Location.GetLineSpan().ToString()}\n\n";
                        errorNum++;
                    }
                    throw new Exception(errorString);
                }

                assemblyStream.Seek(0, SeekOrigin.Begin);
                pdbStream.Seek(0, SeekOrigin.Begin);

                // Load the assembly and its symbols from memory
                Assembly compiledAssembly = Assembly.Load(assemblyStream.ToArray(), pdbStream.ToArray());

                // Instantiate the target type
                // Assuming the class name in the script matches `nameClass` (without namespace)
                // or it's the first public type defined.
                Type typeToInstantiate = compiledAssembly.GetTypes().FirstOrDefault(t => t.Name == nameClass && t.IsPublic && !t.IsAbstract);

                if (typeToInstantiate == null) // Fallback: try first public non-abstract class if name match failed
                {
                    typeToInstantiate = compiledAssembly.GetTypes().FirstOrDefault(t => t.IsPublic && !t.IsAbstract && typeof(ACandlesSeriesRealization).IsAssignableFrom(t));
                }

                if (typeToInstantiate == null)
                {
                    throw new TypeLoadException($"Could not find a public type named '{nameClass}' or a suitable ACandlesSeriesRealization derivative in compiled script: {scriptPath}");
                }

                ACandlesSeriesRealization newInstance = (ACandlesSeriesRealization)Activator.CreateInstance(typeToInstantiate);

                // Cache the successfully compiled instance for future cloning
                lock (_compiledScriptInstancesCache)
                {
                    // Check again to prevent adding duplicates if another thread just compiled it
                    if (!_compiledScriptInstancesCache.Any(ci => ci.GetType().FullName == newInstance.GetType().FullName))
                    {
                        _compiledScriptInstancesCache.Add(newInstance);
                    }
                }
                return newInstance;
            }
        }

        private static List<string> GetDllsPathFromScriptFolder(string scriptFilePath)
        {
            string scriptFolder = Path.GetDirectoryName(scriptFilePath);
            if (string.IsNullOrEmpty(scriptFolder)) return null;

            string dllsFolder = Path.Combine(scriptFolder, "Dlls");

            if (!Directory.Exists(dllsFolder))
            {
                return null;
            }

            // Directory.GetFiles returns full paths
            return Directory.GetFiles(dllsFolder, "*.dll").ToList();
        }

        private static string ReadFile(string path)
        {
            // Using File.ReadAllText for simplicity, ensure encoding is appropriate (UTF-8 usually)
            return File.ReadAllText(path);
        }

        private static List<NamesFilesFromFolder> _filesInDir = new List<NamesFilesFromFolder>();

        private static List<string> GetFullNamesFromFolder(string directory)
        {
            // This caching and retrieval logic for file names can be kept as is,
            // though it could be simplified or made more robust.
            // The original 'results.Contains("Dlls")' check was likely a bug.
            // It's removed here as it wouldn't correctly filter a "Dlls" folder path.
            // If filtering out "Dlls" subdirectories is intended, it needs different logic.

            lock (_filesInDir) // Basic thread safety for the cache
            {
                var existingEntry = _filesInDir.FirstOrDefault(f => f != null && f.Folder == directory);
                if (existingEntry != null)
                {
                    return existingEntry.GetFilesCopy();
                }
            }


            List<string> results = new List<string>();
            try
            {
                string[] subDirectories = Directory.GetDirectories(directory);
                foreach (string subDir in subDirectories)
                {
                    // If you want to exclude specific folder names like "Dlls" from recursive search:
                    // if (Path.GetFileName(subDir).Equals("Dlls", StringComparison.OrdinalIgnoreCase)) continue;
                    results.AddRange(GetFullNamesFromFolder(subDir));
                }

                string[] files = Directory.GetFiles(directory);
                results.AddRange(files); // No .ToList() needed, AddRange takes IEnumerable

            }
            catch (Exception ex)
            {
                // Log or handle directory access errors
                Console.WriteLine($"Error accessing directory {directory}: {ex.Message}");
                return results; // Return whatever was collected so far or an empty list
            }

            lock (_filesInDir)
            {
                // Ensure no duplicate entry is added if multiple threads call concurrently for the same new folder
                if (!_filesInDir.Any(f => f != null && f.Folder == directory))
                {
                    NamesFilesFromFolder dirEntry = new NamesFilesFromFolder
                    {
                        Folder = directory,
                        Files = results // Store the actual list, GetFilesCopy will create a copy
                    };
                    _filesInDir.Add(dirEntry);
                }
            }

            // Return a copy as per original GetFilesCopy logic
            return new List<string>(results);
        }

        private static readonly Dictionary<string, Type> _candlesTypes = GetCandlesTypesWithAttribute();

        private static Dictionary<string, Type> GetCandlesTypesWithAttribute()
        {
            // This method seems fine for discovering pre-compiled types with attributes.
            // Assembly.GetAssembly(typeof(BotPanel)) might be better as:
            // typeof(BotPanel).Assembly to be more direct.
            Assembly assembly = typeof(BotPanel).Assembly;
            Dictionary<string, Type> candles = new Dictionary<string, Type>();
            foreach (Type type in assembly.GetTypes())
            {
                // Ensure type is public and not abstract if it's meant to be instantiated
                if (type.IsPublic && !type.IsAbstract && typeof(ACandlesSeriesRealization).IsAssignableFrom(type))
                {
                    object[] attributes = type.GetCustomAttributes(typeof(CandleAttribute), false);
                    if (attributes.Length > 0)
                    {
                        candles[((CandleAttribute)attributes[0]).Name] = type;
                    }
                }
            }
            return candles;
        }
    }

    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class CandleAttribute : System.Attribute
    {
        public string Name { get; }

        public CandleAttribute(string name)
        {
            Name = name;
        }
    }

    // This class seems to be a simple container for caching file lists.
    public class NamesFilesFromFolder
    {
        public string Folder;
        public List<string> Files; // Should ideally be private with a getter

        public List<string> GetFilesCopy()
        {
            // Create a defensive copy
            return Files != null ? new List<string>(Files) : new List<string>();
        }
    }
}