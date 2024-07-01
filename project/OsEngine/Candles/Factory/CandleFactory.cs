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
using Microsoft.CSharp;
using System.CodeDom.Compiler;

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

            List<string> resultFolderSort = new List<string>();
            resultFolderSort.Add(resultOne[0]);

            for (int i = 1; i < resultOne.Count; i++)
            {
                bool isInArray = false;

                for (int i2 = 1; i2 < resultFolderSort.Count; i2++)
                {

                    if (resultFolderSort[i2][0] > resultOne[i][0])
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

                    string longNameClass = nameClass + ".txt";
                    string longNameClass2 = nameClass + ".cs";

                    string myPath = "";

                    for (int i = 0; i < fullPaths.Count; i++)
                    {
                        string nameInFile = fullPaths[i].Split('\\')[fullPaths[i].Split('\\').Length - 1];

                        if (nameInFile == longNameClass ||
                            nameInFile == longNameClass2)
                        {
                            myPath = fullPaths[i];
                            break;
                        }
                    }

                    if (myPath == "")
                    {
                        MessageBox.Show("Error! Candle series with name " + nameClass + " not found");
                        return series;
                    }

                    series = Serialize(myPath, nameClass);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
            }

            return series;
        }

        private static bool _isFirstTime = true;

        private static string[] linksToDll;

        private static List<ACandlesSeriesRealization> _serializedInd = new List<ACandlesSeriesRealization>();

        private static ACandlesSeriesRealization Serialize(string path, string nameClass)
        {
            // 1 пробуем клонировать из ранее сериализованных объектов. Это быстрее чем подымать из файла

            for (int i = 0; i < _serializedInd.Count; i++)
            {
                if (_serializedInd[i].GetType().Name == nameClass)
                {
                    object[] param = new object[] { };
                    ACandlesSeriesRealization newPanel = (ACandlesSeriesRealization)Activator.CreateInstance(_serializedInd[i].GetType());
                    return newPanel;
                }
            }

            // сериализуем из файла

            try
            {
                if (linksToDll == null)
                {
                    var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                    var res = Array.ConvertAll<Assembly, string>(assemblies, (x) =>
                    {
                        if (!x.IsDynamic)
                        {
                            return x.Location;
                        }

                        return null;
                    });

                    for (int i = 0; i < res.Length; i++)
                    {
                        if (string.IsNullOrEmpty(res[i]))
                        {
                            List<string> list = res.ToList();
                            list.RemoveAt(i);
                            res = list.ToArray();
                            i--;
                        }
                        else if (res[i].Contains("System.Runtime.Serialization")
                                 || i > 24)
                        {
                            List<string> list = res.ToList();
                            list.RemoveAt(i);
                            res = list.ToArray();
                            i--;
                        }
                    }

                    string dllPath = AppDomain.CurrentDomain.BaseDirectory + "System.Runtime.Serialization.dll";

                    List<string> listRes = res.ToList();
                    listRes.Add(dllPath);
                    res = listRes.ToArray();

                    linksToDll = res;
                }

                List<string> dllsToCompiler = linksToDll.ToList();

                List<string> dllsFromPath = GetDllsPathFromFolder(path);

                if (dllsFromPath != null && dllsFromPath.Count != 0)
                {
                    for (int i = 0; i < dllsFromPath.Count; i++)
                    {
                        string dll = dllsFromPath[i].Split('\\')[dllsFromPath[i].Split('\\').Length - 1];

                        if (dllsToCompiler.Find(d => d.Contains(dll)) == null)
                        {
                            dllsToCompiler.Add(dllsFromPath[i]);
                        }
                    }
                }


                CompilerParameters cp = new CompilerParameters(dllsToCompiler.ToArray());
                cp.IncludeDebugInformation = true;
                cp.GenerateInMemory = true;

                string folderCur = AppDomain.CurrentDomain.BaseDirectory + "Engine\\Temp";

                if (Directory.Exists(folderCur) == false)
                {
                    Directory.CreateDirectory(folderCur);
                }

                folderCur += "\\Indicators";

                if (Directory.Exists(folderCur) == false)
                {
                    Directory.CreateDirectory(folderCur);
                }

                if (_isFirstTime)
                {
                    _isFirstTime = false;

                    string[] files = Directory.GetFiles(folderCur);

                    for (int i = 0; i < files.Length; i++)
                    {
                        try
                        {
                            File.Delete(files[i]);
                        }
                        catch
                        {
                            // ignore
                        }
                    }
                }

                cp.TempFiles = new TempFileCollection(folderCur, false);

                ACandlesSeriesRealization result = null;

                string fileStr = ReadFile(path);

                CSharpCodeProvider prov = new CSharpCodeProvider();

                CompilerResults results = prov.CompileAssemblyFromSource(cp, fileStr);

                if (results.Errors != null && results.Errors.Count != 0)
                {
                    string errorString = "Error! Indicator script runTime compilation problem! \n";
                    errorString += "Path to indicator: " + path + " \n";

                    int errorNum = 1;

                    foreach (var error in results.Errors)
                    {
                        errorString += "Error Number: " + errorNum + " \n";
                        errorString += error.ToString() + "\n";
                        errorNum++;
                    }

                    throw new Exception(errorString);
                }

                result = (ACandlesSeriesRealization)results.CompiledAssembly.CreateInstance(results.CompiledAssembly.DefinedTypes.ElementAt(0).FullName);

                cp.TempFiles.Delete();

                bool isInArray = false;

                for (int i = 0; i < _serializedInd.Count; i++)
                {
                    if (_serializedInd[i].GetType().Name == nameClass)
                    {
                        isInArray = true;
                        break;
                    }
                }

                if (isInArray == false)
                {
                    _serializedInd.Add(result);
                }

                return result;
            }
            catch (Exception e)
            {
                throw new Exception(e.ToString());
            }
        }

        private static List<string> GetDllsPathFromFolder(string path)
        {
            string folderPath = path.Remove(path.LastIndexOf('\\'), path.Length - path.LastIndexOf('\\'));

            if (Directory.Exists(folderPath + "\\Dlls") == false)
            {
                return null;
            }

            string[] filesInFolder = Directory.GetFiles(folderPath + "\\Dlls");

            List<string> dlls = new List<string>();

            for (int i = 0; i < filesInFolder.Length; i++)
            {
                if (filesInFolder[i].EndsWith(".dll") == false)
                {
                    continue;
                }

                string dllPath = AppDomain.CurrentDomain.BaseDirectory + filesInFolder[i];

                dlls.Add(dllPath);
            }

            return dlls;
        }

        private static string ReadFile(string path)
        {
            String result = "";

            using (StreamReader reader = new StreamReader(path))
            {
                result = reader.ReadToEnd();
                reader.Close();
            }

            return result;
        }

        private static List<NamesFilesFromFolder> _filesInDir = new List<NamesFilesFromFolder>();

        private static List<string> GetFullNamesFromFolder(string directory)
        {
            for (int i = 0; i < _filesInDir.Count; i++)
            {
                if (_filesInDir[i] == null)
                {
                    continue;
                }

                if (_filesInDir[i].Folder == directory)
                {
                    return _filesInDir[i].GetFilesCopy();
                }
            }

            List<string> results = new List<string>();

            string[] subDirectories = Directory.GetDirectories(directory);

            for (int i = 0; i < subDirectories.Length; i++)
            {
                results.AddRange(GetFullNamesFromFolder(subDirectories[i]));
            }

            string[] files = Directory.GetFiles(directory);

            results.AddRange(files.ToList());

            for (int i = 0; i < results.Count; i++)
            {
                if (results.Contains("Dlls"))
                {
                    results.RemoveAt(i);
                    i--;
                    continue;
                }
            }

            NamesFilesFromFolder dir = new NamesFilesFromFolder();
            dir.Folder = directory;
            dir.Files = results;
            _filesInDir.Add(dir);

            return dir.GetFilesCopy();
        }

        private static readonly Dictionary<string, Type> _candlesTypes = GetCandlesTypesWithAttribute();

        private static Dictionary<string, Type> GetCandlesTypesWithAttribute()
        {
            Assembly assembly = Assembly.GetAssembly(typeof(BotPanel));
            Dictionary<string, Type> candles = new Dictionary<string, Type>();
            foreach (Type type in assembly.GetTypes())
            {
                object[] attributes = type.GetCustomAttributes(typeof(CandleAttribute), false);
                if (attributes.Length > 0)
                {
                    candles[((CandleAttribute)attributes[0]).Name] = type;
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

    public class NamesFilesFromFolder
    {
        public string Folder;

        public List<string> Files;

        public List<string> GetFilesCopy()
        {
            List<string> results = new List<string>();

            for (int i = 0; i < Files.Count; i++)
            {
                results.Add(Files[i]);
            }

            return results;
        }
    }
}