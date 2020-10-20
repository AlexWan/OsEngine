using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CSharp;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;

namespace OsEngine.OsTrader
{
    public sealed class HotUpdateManager
    {
        public static HotUpdateManager Instance => LazyInstance.Value;

        private static readonly Lazy<HotUpdateManager> LazyInstance =
            new Lazy<HotUpdateManager>(() => new HotUpdateManager());

        private static readonly HotUpdateResult<BotPanel> ErrorResult =
            new HotUpdateResult<BotPanel>(null, HotUpdateResultStatus.Error);

        private readonly Dictionary<string, string> _classPathCache =
            new Dictionary<string, string>();

        private HotUpdateManager()
        {
        }

        /// Two different bots must not have the same
        /// class name and strategy name <see cref="BotPanel.GetNameStrategyType"/> at the same time
        /// У двух разных роботов не должны одновременно совпадать
        /// имя класса и название стратегии <see cref="BotPanel.GetNameStrategyType"/> >
        public HotUpdateResult<BotPanel> Update(BotPanel bot)
        {
            try
            {
                if (bot.GetType().IsNested)
                {
                    return ErrorResult;
                }

                List<string> sourceFiles = SeekForSourceFiles(bot);

                HotUpdateResult<BotPanel>? result = null;
                string botClassPath = default;
                foreach (string sourceFile in sourceFiles)
                {
                    HotUpdateResult<BotPanel> updatedBotResult = Execute(() =>
                        Instantiate<BotPanel>(sourceFile, bot.NameStrategyUniq, bot.StartProgram));
                    BotPanel updatedBot = updatedBotResult.UpdatedObject;
                    if (updatedBot != null && updatedBot.GetNameStrategyType().Equals(bot.GetNameStrategyType()))
                    {
                        // Two bot classes with the same class name and strategy name
                        if (result?.UpdatedObject != null)
                        {
                            string errorMessage =
                                $"Duplicated classes with a name = \"{bot.GetType().Name}\" and strategy = \"{bot.GetNameStrategyType()}\" ";
                            return new HotUpdateResult<BotPanel>(null, HotUpdateResultStatus.Error, errorMessage);
                        }

                        botClassPath = sourceFile;
                    }
                    result = updatedBotResult;
                }

                if (result != null)
                {
                    _classPathCache.Remove(GetBotUniqName(bot));
                    _classPathCache[GetBotUniqName(bot)] = botClassPath;
                }

                return result ?? ErrorResult;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return ErrorResult;
            }
        }

        private List<string> SeekForSourceFiles(BotPanel bot)
        {
            string botUniqName = GetBotUniqName(bot);
            if (_classPathCache.ContainsKey(botUniqName))
            {
                return new List<string> {_classPathCache[botUniqName]};
            }

            string className = bot.GetType().Name;
            string fileName = className + ".cs";
            string projectPath = Directory.GetParent(Environment.CurrentDirectory).Parent.FullName;
            return IndicatorsFactory.GetFullNamesFromFolder(projectPath)
                .Where(f => fileName.Equals(Path.GetFileName(f)))
                .ToList();
        }

        private string GetBotUniqName(BotPanel bot)
        {
            return $"{bot.GetType().Name}#{bot.GetHashCode()}";
        }

        private static T Instantiate<T>(string path, params object[] initParams)
        {
            try
            {
                string fileData = File.ReadAllText(path);
                CSharpCodeProvider provider = new CSharpCodeProvider();
                CompilerParameters compilerParameters = new CompilerParameters(
                    Array.ConvertAll(AppDomain.CurrentDomain.GetAssemblies(),
                        x => x.Location));
                compilerParameters.GenerateInMemory = true;
                compilerParameters.TempFiles.KeepFiles = false;

                CompilerResults results = provider.CompileAssemblyFromSource(compilerParameters, fileData);
                if (results.Errors != null && results.Errors.Count != 0)
                {
                    string errorString = "Error! RunTime compilation problem! \n";
                    errorString += "Path to class: " + path + " \n";
                    int errorNum = 1;
                    foreach (var error in results.Errors)
                    {
                        errorString += "Error Number: " + errorNum + " \n";
                        errorString += error + "\n";
                        errorNum++;
                    }

                    throw new Exception(errorString);
                }

                string typeName = results.CompiledAssembly.DefinedTypes.ElementAt(0).FullName;
                T result = (T) results.CompiledAssembly.CreateInstance(typeName, false,
                    BindingFlags.Instance | BindingFlags.Public, null,
                    initParams, null, null);
                compilerParameters.TempFiles.Delete();

                return result;
            }
            catch (Exception e)
            {
                throw new Exception(e.ToString());
            }
        }

        private static HotUpdateResult<T> Execute<T>(Func<T> func)
        {
            try
            {
                T result = func();
                return new HotUpdateResult<T>(result, HotUpdateResultStatus.Success);
            }
            catch (Exception e)
            {
                return new HotUpdateResult<T>(default, HotUpdateResultStatus.Error, e.Message);
            }
        }
    }

    public readonly struct HotUpdateResult<T>
    {
        public HotUpdateResult(T updatedObject, HotUpdateResultStatus status, string errorMessage = default)
        {
            UpdatedObject = updatedObject;
            Status = status;
            ErrorMessage = errorMessage;
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