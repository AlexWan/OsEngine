/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.CSharp;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.Robots.Engines;
using System.Windows;

namespace OsEngine.Robots
{
    public class BotFactory
    {
        private static readonly Dictionary<string, Type> BotsWithAttribute = GetTypesWithBotAttribute();

        /// <summary>
        /// Get list robots name
        /// </summary>
        public static List<string> GetNamesStrategy()
        {
            List<string> result = new List<string>();
            result.Add("Engine");
            result.Add("ScreenerEngine");
            result.Add("ClusterEngine");
            result.AddRange(BotsWithAttribute.Keys);

            List<string> resultTrue = new List<string>();

            for (int i = 0; i < result.Count; i++)
            {
                bool isInArray = false;

                for (int i2 = 0; i2 < resultTrue.Count; i2++)
                {
                    if (resultTrue[i2][0] > result[i][0])
                    {
                        resultTrue.Insert(i2, result[i]);
                        isInArray = true;
                        break;
                    }
                }

                if (isInArray == false)
                {
                    resultTrue.Add(result[i]);
                }
            }


            return resultTrue;
        }

        /// <summary>
        /// Create robot
        /// </summary>
        public static BotPanel GetStrategyForName(string nameClass, string name, StartProgram startProgram, bool isScript)
        {
            if(string.IsNullOrEmpty(nameClass))
            {
                return null;
            }

            BotPanel bot = null;

            if (isScript && bot == null)
            {
                bot = CreateScriptStrategyByName(nameClass, name, startProgram);
                return bot;
            }

            if (nameClass == "ScreenerEngine")
            {
                bot = new ScreenerEngine(name, startProgram);
            }
            if (nameClass == "Engine")
            {
                bot = new CandleEngine(name, startProgram);
            }
            if (nameClass == "ClusterEngine")
            {
                bot = new ClusterEngine(name, startProgram);
            }
            if (BotsWithAttribute.ContainsKey(nameClass))
            {
                Type botType = BotsWithAttribute[nameClass];
                bot = (BotPanel)Activator.CreateInstance(botType, name, startProgram);
            }

            if (bot == null)
            {
                bot = CreateScriptStrategyByName(nameClass, name, startProgram);
                return bot;
            }
            return bot;
        }

        /// <summary>
        /// Get all robots decorated with BotAttribute
        /// </summary>
        static Dictionary<string, Type> GetTypesWithBotAttribute()
        {
            Assembly assembly = Assembly.GetAssembly(typeof(BotPanel));
            Dictionary<string, Type> bots = new Dictionary<string, Type>();
            foreach (Type type in assembly.GetTypes())
            {
                object[] attributes = type.GetCustomAttributes(typeof(BotAttribute), false);
                if (attributes.Length > 0)
                {
                    bots[((BotAttribute)attributes[0]).Name] = type;
                }
            }

            return bots;
        }

        /// <summary>
        /// Get a list of all available robots
        /// </summary>
        public static List<string> GetScriptsNamesStrategy()
        {
            if (Directory.Exists(@"Custom") == false)
            {
                Directory.CreateDirectory(@"Custom");
            }

            if (Directory.Exists(@"Custom\Robots") == false)
            {
                Directory.CreateDirectory(@"Custom\Robots");
            }

            List<string> resultOne = GetFullNamesFromFolder(@"Custom\Robots");

            for (int i = 0; i < resultOne.Count; i++)
            {
                resultOne[i] = resultOne[i].Split('\\')[resultOne[i].Split('\\').Length - 1];
                resultOne[i] = resultOne[i].Split('.')[0];
            }

            List<string> resultTrue = new List<string>();

            for (int i = 0; i < resultOne.Count; i++)
            {
                bool isInArray = false;

                for (int i2 = 0; i2 < resultTrue.Count; i2++)
                {
                    if (resultTrue[i2][0] > resultOne[i][0])
                    {
                        resultTrue.Insert(i2, resultOne[i]);
                        isInArray = true;
                        break;
                    }
                }

                if (isInArray == false)
                {
                    resultTrue.Add(resultOne[i]);
                }
            }

            return resultTrue;
        }

        /// <summary>
        /// Get full path to files with user scripts
        /// </summary>
        /// <param name="directory">path to directory</param>
        private static List<string> GetFullNamesFromFolder(string directory)
        {
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
                if (results[i].EndsWith("cs") == false)
                {
                    results.RemoveAt(i);
                    i--;
                }
            }

            for (int i = 0; i < results.Count; i++)
            {
                if (results.Contains("Dlls"))
                {
                    results.RemoveAt(i);
                    i--;
                }
            }

            return results;
        }

        /// <summary>
        /// Find the right script and create a robot
        /// </summary>
        /// <param name="nameClass">the name of the type to be instantiated</param>
        /// <param name="name">name for the robot</param>
        /// <param name="startProgram">the type of program that is requesting the action</param>
        public static BotPanel CreateScriptStrategyByName(string nameClass, string name, StartProgram startProgram)
        {
            BotPanel bot = null;

            if (bot == null)
            {
                List<string> fullPaths = GetFullNamesFromFolder(@"Custom\Robots");

                string longNameClass = nameClass + ".txt";
                string longNameClass2 = nameClass + ".cs";

                string myPath = "";

                for (int i = 0; i < fullPaths.Count; i++)
                {
                    string nameInFile =
                        fullPaths[i].Split('\\')[fullPaths[i].Split('\\').Length - 1];

                    if (nameInFile == longNameClass ||
                        nameInFile == longNameClass2)
                    {
                        myPath = fullPaths[i];
                        break;
                    }
                }

                if (myPath == "")
                {
                    MessageBox.Show("Error! Bot with name " + nameClass + " not found");
                    return null;
                }
                try
                {
                    bot = Serialize(myPath, nameClass, name, startProgram);
                }
                catch (Exception e)
                {
                    MessageBox.Show("BotFactory. Serialization error: " + e.ToString());
                    return null;
                }
            }

            bot.IsScript = true;
            bot.FileName = nameClass;

            return bot;
        }

        private static bool _isFirstTime = true;

        private static string[] linksToDll;

        private static List<BotPanel> _serializedPanels = new List<BotPanel>();

        /// <summary>
        /// Creating a robot instance
        /// </summary>
        /// <param name="path">path to the script file</param>
        /// <param name="nameClass">the name of the type to be instantiated</param>
        /// <param name="name">name for the robot</param>
        /// <param name="startProgram">the type of program that is requesting the action</param>
        private static BotPanel Serialize(string path, string nameClass, string name, StartProgram startProgram)
        {
            // 1 we try to clone from previously serialized objects. It's faster than raising from a file

            try
            {
                for (int i = 0; i < _serializedPanels.Count; i++)
                {
                    if (_serializedPanels[i].GetType().Name == nameClass)
                    {
                        object[] param = new object[] { name, startProgram };
                        BotPanel newPanel = (BotPanel)Activator.CreateInstance(_serializedPanels[i].GetType(), param);
                        return newPanel;
                    }
                }

            }
            catch (Exception e)
            {
                string errorString = e.ToString();
                throw new Exception(errorString);
            }

            // serialize from file
            try
            {
                if (linksToDll == null)
                {
                    var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                    string[] res = Array.ConvertAll<Assembly, string>(assemblies, (x) =>
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

                // mark assembly as temporary
                cp.GenerateInMemory = true;
                cp.IncludeDebugInformation = true;
                cp.TempFiles.KeepFiles = false;


                string folderCur = AppDomain.CurrentDomain.BaseDirectory + "Engine\\Temp";

                if (Directory.Exists(folderCur) == false)
                {
                    Directory.CreateDirectory(folderCur);
                }

                folderCur += "\\Bots";

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

                BotPanel result = null;

                string fileStr = ReadFile(path);

                CSharpCodeProvider prov = new CSharpCodeProvider();

                CompilerResults results = prov.CompileAssemblyFromSource(cp, fileStr);

                if (results.Errors != null && results.Errors.Count != 0)
                {
                    string errorString = "Error! Robot script runTime compilation problem! \n";
                    errorString += "Path to Robot: " + path + " \n";

                    int errorNum = 1;

                    foreach (var error in results.Errors)
                    {
                        errorString += "Error Number: " + errorNum + " \n";
                        errorString += error.ToString() + "\n";
                        errorNum++;
                    }

                    throw new Exception(errorString);
                }               

                List<object> param = new List<object>();
                param.Add(name);
                param.Add(startProgram);

                result = (BotPanel)results.CompiledAssembly.CreateInstance(
                    results.CompiledAssembly.DefinedTypes.ElementAt(0).FullName, false, BindingFlags.CreateInstance, null,
                    param.ToArray(), CultureInfo.CurrentCulture, null);

                if (result == null)
                {
                    string errorString = "Error! Robot script runTime compilation problem! \n";
                    errorString += "Path to robot: " + path + " \n";

                    int errorNum = 1;

                    foreach (var error in results.Errors)
                    {
                        errorString += "Error Number: " + errorNum + " \n";
                        errorString += error.ToString() + "\n";
                        errorNum++;
                    }

                    throw new Exception(errorString);
                }

                bool isInArray = false;

                for (int i = 0; i < _serializedPanels.Count; i++)
                {
                    if (_serializedPanels[i].GetType().Name == nameClass)
                    {
                        isInArray = true;
                        break;
                    }
                }

                if (isInArray == false)
                {
                    _serializedPanels.Add(result);
                }

                return result;
            }
            catch (Exception e)
            {
                string errorString = e.ToString();
                throw new Exception(errorString);
            }
        }

        /// <summary>
        /// Get paths to libraries in a directory
        /// </summary>
        /// <param name="path">directory path</param>
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

        /// <summary>
        /// Read data from file
        /// </summary>
        /// <param name="path">the path to the file</param>
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

        // Names Include Bots With Params

        /// <summary>
        /// Start loading strategy names with parameters
        /// </summary>
        public static List<string> GetNamesStrategyWithParametersSync()
        {
            if (NeadToReload == false &&
                (_namesWithParam == null ||
                 _namesWithParam.Count == 0))
            {
                LoadBotsNames();
            }

            if (NeadToReload == false &&
                _namesWithParam != null &&
                _namesWithParam.Count != 0)
            {
                return _namesWithParam;
            }

            NeadToReload = false;

            List<Thread> workers = new List<Thread>();

            _namesWithParam = new List<string>();

            for (int i = 0; i < 3; i++)
            {
                Thread worker = new Thread(LoadNamesWithParam);
                worker.Name = i.ToString();
                workers.Add(worker);
                worker.Start();
            }

            while (workers.Find(w => w.IsAlive) != null)
            {
                Thread.Sleep(100);
            }

            SaveBotsNames();

            return _namesWithParam;
        }

        /// <summary>
        /// Strategy names need to be reloaded
        /// </summary>
        public static bool NeadToReload;

        /// <summary>
        /// Load strategy names from file
        /// </summary>
        private static void LoadBotsNames()
        {
            _namesWithParam.Clear();

            if (File.Exists("Engine\\OptimizerBots.txt") == false)
            {
                return;
            }

            using (StreamReader reader = new StreamReader("Engine\\OptimizerBots.txt"))
            {
                while (reader.EndOfStream == false)
                {
                    _namesWithParam.Add(reader.ReadLine());
                }
                reader.Close();
            }
        }

        /// <summary>
        /// Save strategy names to file
        /// </summary>
        private static void SaveBotsNames()
        {
            using (StreamWriter writer = new StreamWriter("Engine\\OptimizerBots.txt"))
            {
                for (int i = 0; i < _namesWithParam.Count; i++)
                {
                    writer.WriteLine(_namesWithParam[i]);
                }

                writer.Close();
            }
        }

        private static List<string> _namesWithParam = new List<string>();

        /// <summary>
        /// Load strategy names with parameters
        /// </summary>
        private static void LoadNamesWithParam()
        {
            List<string> names = GetNamesStrategy();

            int numThread = Convert.ToInt32(Thread.CurrentThread.Name);

            for (int i = numThread; i < names.Count; i += 3)
            {
                try
                {
                    BotPanel bot = GetStrategyForName(names[i], numThread.ToString(), StartProgram.IsOsOptimizer, false);

                    if (bot.Parameters.Count != 0)
                    {
                        if (bot.TabsScreener != null && bot.TabsScreener.Count > 0)
                        {
                            bot.Delete();
                            continue;
                        }

                        if (bot.TabsPair != null && bot.TabsPair.Count > 0)
                        {
                            bot.Delete();
                            continue;
                        }

                        _namesWithParam.Add(names[i]);
                    }
                    
                    bot.Delete();
                }
                catch
                {
                    continue;
                }
            }

            if (LoadNamesWithParamEndEvent != null)
            {
                LoadNamesWithParamEndEvent(_namesWithParam);
            }
        }

        /// <summary>
        /// Loaded strategy names with parameters
        /// </summary>
        public static event Action<List<string>> LoadNamesWithParamEndEvent;
    }
}