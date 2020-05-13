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
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.Robots.CounterTrend;
using OsEngine.Robots.Engines;
using OsEngine.Robots.High_Frequency;
using OsEngine.Robots.MarketMaker;
using OsEngine.Robots.Patterns;
using OsEngine.Robots.Trend;
using OsEngine.Robots.OnScriptIndicators;
using System.Runtime;

namespace OsEngine.Robots
{
    public class BotFactory
    {
        /// <summary>
        /// list robots name / 
        /// список доступных роботов
        /// </summary>
        public static List<string> GetNamesStrategy()
        {
            List<string> result = new List<string>();

            result.Add("Engine");
            result.Add("ClusterEngine");
            result.Add("FundBalanceDivergenceBot");
            result.Add("PairTraderSimple");
            result.Add("MomentumMACD");
            result.Add("MarketMakerBot");
            result.Add("PatternTrader");
            result.Add("HighFrequencyTrader");
            result.Add("Bollinger");
            result.Add("EnvelopTrend");
            result.Add("Williams Band");
            result.Add("TwoLegArbitrage");
            result.Add("ThreeSoldier");
            result.Add("PriceChannelTrade");
            result.Add("SmaStochastic");
            result.Add("ClusterCountertrend");
            result.Add("PairTraderSpreadSma");
            result.Add("WilliamsRangeTrade");
            result.Add("ParabolicSarTrade");
            result.Add("PivotPointsRobot");
            result.Add("RsiContrtrend");
            result.Add("PinBarTrade");
            result.Add("BbPowerTrade");
            result.Add("BollingerRevers");
            result.Add("BollingerTrailing");
            result.Add("CciTrade");
            result.Add("MacdRevers");
            result.Add("MacdTrail");
            result.Add("OneLegArbitrage");
            result.Add("PairRsiTrade");
            result.Add("PriceChannelBreak");
            result.Add("PriceChannelVolatility");
            result.Add("RsiTrade");
            result.Add("RviTrade");

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
        /// create robot
        /// создать робота
        /// </summary>
        public static BotPanel GetStrategyForName(string nameClass, string name, StartProgram startProgram, bool isScript)
        {
            BotPanel bot = null;
            // примеры и бесплатные боты

            if (isScript)
            {
                bot = CreateScriptStrategyByName(nameClass, name, startProgram);
                return bot;
            }

            if (nameClass == "FundBalanceDivergenceBot")
            {
                bot = new FundBalanceDivergenceBot(name, startProgram);
            }
            if (nameClass == "BbPowerTrade")
            {
                bot = new BbPowerTrade(name, startProgram);
            }
            if (nameClass == "BollingerRevers")
            {
                bot = new BollingerRevers(name, startProgram);
            }
            if (nameClass == "BollingerTrailing")
            {
                bot = new BollingerTrailing(name, startProgram);
            }
            if (nameClass == "CciTrade")
            {
                bot = new CciTrade(name, startProgram);
            }
            if (nameClass == "MacdRevers")
            {
                bot = new MacdRevers(name, startProgram);
            }
            if (nameClass == "MacdTrail")
            {
                bot = new MacdTrail(name, startProgram);
            }
            if (nameClass == "OneLegArbitrage")
            {
                bot = new OneLegArbitrage(name, startProgram);
            }
            if (nameClass == "PairRsiTrade")
            {
                bot = new PairRsiTrade(name, startProgram);
            }
            if (nameClass == "PriceChannelBreak")
            {
                bot = new PriceChannelBreak(name, startProgram);
            }
            if (nameClass == "PriceChannelVolatility")
            {
                bot = new PriceChannelVolatility(name, startProgram);
            }
            if (nameClass == "RsiTrade")
            {
                bot = new RsiTrade(name, startProgram);
            }
            if (nameClass == "RviTrade")
            {
                bot = new RviTrade(name, startProgram);
            }

            if (nameClass == "MomentumMACD")
            {
                bot = new MomentumMacd(name, startProgram);
            }

            if (nameClass == "Engine")
            {
                bot = new CandleEngine(name, startProgram);
            }
            if (nameClass == "ClusterEngine")
            {
                bot = new ClusterEngine(name, startProgram);
            }

            if (nameClass == "PairTraderSimple")
            {
                bot = new PairTraderSimple(name, startProgram);
            }
            if (nameClass == "EnvelopTrend")
            {
                bot = new EnvelopTrend(name, startProgram);
            }
            if (nameClass == "ClusterCountertrend")
            {
                bot = new ClusterCountertrend(name, startProgram);
            }
            if (nameClass == "PatternTrader")
            {
                bot = new PatternTrader(name, startProgram);
            }
            if (nameClass == "HighFrequencyTrader")
            {
                bot = new HighFrequencyTrader(name, startProgram);
            }
            if (nameClass == "PivotPointsRobot")
            {
                bot = new PivotPointsRobot(name, startProgram);
            }
            if (nameClass == "Williams Band")
            {
                bot = new StrategyBillWilliams(name, startProgram);
            }
            if (nameClass == "MarketMakerBot")
            {
                bot = new MarketMakerBot(name, startProgram);
            }
            if (nameClass == "Bollinger")
            {
                bot = new StrategyBollinger(name, startProgram);
            }
            if (nameClass == "ParabolicSarTrade")
            {
                bot = new ParabolicSarTrade(name, startProgram);
            }
            if (nameClass == "PriceChannelTrade")
            {
                bot = new PriceChannelTrade(name, startProgram);
            }
            if (nameClass == "WilliamsRangeTrade")
            {
                bot = new WilliamsRangeTrade(name, startProgram);
            }
            if (nameClass == "SmaStochastic")
            {
                bot = new SmaStochastic(name, startProgram);
            }
            if (nameClass == "PinBarTrade")
            {
                bot = new PinBarTrade(name, startProgram);
            }
            if (nameClass == "TwoLegArbitrage")
            {
                bot = new TwoLegArbitrage(name, startProgram);
            }
            if (nameClass == "ThreeSoldier")
            {
                bot = new ThreeSoldier(name, startProgram);
            }
            if (nameClass == "RsiContrtrend")
            {
                bot = new RsiContrtrend(name, startProgram);
            }
            if (nameClass == "PairTraderSpreadSma")
            {
                bot = new PairTraderSpreadSma(name, startProgram);
            }


            return bot;
        }

        // Scripts

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

            // resultOne.Add("Ssma");

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

            return results;
        }

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
                    if (fullPaths[i].EndsWith(longNameClass) ||
                        fullPaths[i].EndsWith(longNameClass2))
                    {
                        myPath = fullPaths[i];
                        break;
                    }
                }
                
                bot = Serialize(myPath, nameClass, name, startProgram);
            }

            bot.IsScript = true;
            bot.FileName = nameClass;

            return bot;
        }

        private static BotPanel Serialize(string path, string nameClass, string name, StartProgram startProgram)
        {
            try
            {
                BotPanel result = null;

                string fileStr = ReadFile(path);

                //Объявляем провайдер кода С#
                CSharpCodeProvider prov = new CSharpCodeProvider();

                var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                var res = Array.ConvertAll<Assembly, string>(assemblies, (x) =>
                {
                    if (!x.IsDynamic)
                    {
                        return x.Location;
                    }

                    return null;
                });

                CompilerParameters cp = new CompilerParameters(res);

                // Помечаем сборку, как временную
                cp.GenerateInMemory = true;
                cp.IncludeDebugInformation = true;
               
                // Обрабатываем CSC компилятором
                CompilerResults results = prov.CompileAssemblyFromSource(cp, fileStr);

                if (results.Errors != null && results.Errors.Count != 0)
                {
                    string errorString = "Error! Robot script runTime compilation problem! \n";
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
                //string name, StartProgram startProgram)

                List<object> param = new List<object>();
                param.Add(name);
                param.Add(startProgram);

                result = (BotPanel)results.CompiledAssembly.CreateInstance(
                    results.CompiledAssembly.DefinedTypes.ElementAt(0).FullName, false, BindingFlags.Default, null,
                    param.ToArray(), CultureInfo.CurrentCulture, null);

                if (result == null)
                {
                    string errorString = "Error! Robot script runTime compilation problem! \n";
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

                return result;
            }
            catch (Exception e)
            {
                string errorString = e.ToString();
                throw new Exception(errorString);
            }
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


        // Names Include Bots With Params

        public static List<string> GetNamesStrategyWithParameters()
        {
            if (_searchIsStarted == false)
            {
                _searchIsStarted = true;

                for (int i = 0; i < 3; i++)
                {
                    Thread worker = new Thread(LoadNamesWithParam);
                    worker.Name = i.ToString();
                    worker.IsBackground = true;
                    worker.Start();
                }
            }

            return _namesWithParam;
        }

        private static List<string> _namesWithParam = new List<string>();

        private static bool _searchIsStarted;

        private static void LoadNamesWithParam()
        {
            List<string> names = GetNamesStrategy();

            int numThread = Convert.ToInt32(Thread.CurrentThread.Name);

            for (int i = numThread; i < names.Count; i += 3)
            {
                try
                {
                    BotPanel bot = GetStrategyForName(names[i], numThread.ToString(), StartProgram.IsOsOptimizer, false);

                    if (bot.Parameters == null ||
                        bot.Parameters.Count == 0)
                    {
                        //SendLogMessage("We are not optimizing. Without parameters/Не оптимизируем. Без параметров: " + bot.GetNameStrategyType(), LogMessageType.System);
                    }
                    else
                    {
                        // SendLogMessage("With parameters/С параметрами: " + bot.GetNameStrategyType(), LogMessageType.System);
                        _namesWithParam.Add(names[i]);
                    }
                    if (numThread == 2)
                    {

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

        public static event Action<List<string>> LoadNamesWithParamEndEvent;

    }
}