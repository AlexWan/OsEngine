using System;
using System.Collections.Generic;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers.Optimizer;
using OsEngine.Market.Servers.Tester;
using OsEngine.OsTrader.Panels;
using OsEngine.Robots;
using OsEngine.OsOptimizer.OptimizerEntity;

namespace OsEngine.OsOptimizer
{

    /// <summary>
    /// optimization class
    /// класс проводящий оптимизацию
    /// </summary>
    public class OptimizerExecutor
    {
        public OptimizerExecutor(OptimizerMaster master)
        {
            _master = master;

            _asyncBotFactory = new AsyncBotFactory();
        }

        /// <summary>
        /// object providing data for settings
        /// объект предоставляющий данные для настроек
        /// </summary>
        private OptimizerMaster _master;

        private AsyncBotFactory _asyncBotFactory;

        /// <summary>
        /// start the optimization process
        /// запустить процесс оптимизации
        /// </summary>
        /// <param name="parametersOn">the list of included parameters/список включенных в перебор параметров</param>
        /// <param name="parameters">all strategy parameters/все параметры стратегии</param>
        /// <returns>true if optimization start is successful/true если старт оптимизации закончился успешно</returns>
        public bool Start(List<bool> parametersOn, List<IIStrategyParameter> parameters)
        {
            if (_primeThreadWorker != null)
            {
                SendLogMessage(OsLocalization.Optimizer.Message1, LogMessageType.System);
                return false;
            }
            _parametersOn = parametersOn;
            _parameters = parameters;

            SendLogMessage(OsLocalization.Optimizer.Message2, LogMessageType.System);

            _neadToStop = false;
            _servers = new List<OptimizerServer>();
            _countAllServersMax = 0;
            _serverNum = 0;

            _primeThreadWorker = new Thread(PrimeThreadWorkerPlace);
            _primeThreadWorker.Name = "OptimizerExecutorThread";
            _primeThreadWorker.Start();

            return true;
        }

        /// <summary>
        /// the list of included parameters. If true, then the parameter with this number must be iterated.
        /// список включенных в перебор параметров. Если true, то параметр с таким номером нужно перебирать
        /// </summary>
        private List<bool> _parametersOn;

        /// <summary>
        /// strategy parameters
        /// параметры стратегии
        /// </summary>
        public List<IIStrategyParameter> _parameters;

        /// <summary>
        /// stop the optimization process
        /// остановить процесс оптимизации
        /// </summary>
        public void Stop()
        {
            _neadToStop = true;
            SendLogMessage(OsLocalization.Optimizer.Message3, LogMessageType.System);
        }

        /// <summary>
        /// variable telling the thread to the optimization manager
        /// on whether to continue launching new bots or
        /// user requested to stop the process
        /// переменная сообщающая потоку управляющему процессом оптимизации 
        /// о том, нужно ли продолжать запускать новых ботов или 
        /// пользователь запросил остановку процесса
        /// </summary>
        private bool _neadToStop;

        // алгоритм оптимизации 

        /// <summary>
        /// place of work flow responsible for optimization
        /// место работы потока отвечающего за оптимизацию
        /// </summary>
        private async void PrimeThreadWorkerPlace()
        {
            ReportsToFazes = new List<OptimazerFazeReport>();

            int countBots = BotCountOneFaze();

            SendLogMessage(OsLocalization.Optimizer.Message4 + countBots, LogMessageType.System);

            DateTime timeStart = DateTime.Now;

            _countAllServersMax = countBots;

            for (int i = 0; i < _master.Fazes.Count; i++)
            {
                if (_neadToStop)
                {
                    _primeThreadWorker = null;
                    TestReadyEvent?.Invoke(ReportsToFazes);
                    return;
                }

                if (_master.Fazes[i].TypeFaze == OptimizerFazeType.InSample)
                {
                    OptimazerFazeReport report = new OptimazerFazeReport();
                    report.Faze = _master.Fazes[i];

                    ReportsToFazes.Add(report);

                    StartAsuncBotFactoryInSample(countBots, _master.StrategyName, _master.IsScript, "InSample");
                    StartOptimazeFazeInSample(_master.Fazes[i], report, _parameters, _parametersOn);
                }
                else
                {

                    SendLogMessage("ReportsCount" + ReportsToFazes[ReportsToFazes.Count - 1].Reports.Count.ToString(), LogMessageType.System);

                    OptimazerFazeReport reportFiltred = new OptimazerFazeReport();
                    EndOfFazeFiltration(ReportsToFazes[ReportsToFazes.Count - 1], reportFiltred);

                    OptimazerFazeReport report = new OptimazerFazeReport();
                    report.Faze = _master.Fazes[i];

                    ReportsToFazes.Add(report);

                    StartAsuncBotFactoryOutOfSample(reportFiltred, _master.StrategyName, _master.IsScript, "OutOfSample");

                    StartOptimazeFazeOutOfSample(report, reportFiltred);
                }

                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            TimeSpan time = DateTime.Now - timeStart;

            SendLogMessage(OsLocalization.Optimizer.Message7, LogMessageType.System);
            SendLogMessage("Total test time = " + time.ToString(), LogMessageType.System);

            TestReadyEvent?.Invoke(ReportsToFazes);
            _primeThreadWorker = null;

            return;

        }

        private void StartAsuncBotFactoryInSample(int botCount, string botType, bool isScript, string faze)
        {
            List<string> botNames = new List<string>();
            int startServerIndex = _serverNum;

            for (int i = 0; i < botCount; i++)
            {
                string botName = (startServerIndex + i) + " " + faze;
                botNames.Add(botName);
            }

            _asyncBotFactory.CreateNewBots(botNames, botType, isScript,StartProgram.IsOsOptimizer);
        }

        private void StartAsuncBotFactoryOutOfSample(OptimazerFazeReport reportFiltred, string botType, bool isScript, string faze)
        {
            List<string> botNames = new List<string>();
            int startServerIndex = _serverNum;

            // reportFiltred.Reports[i].BotName.Replace(" InSample", "") + " OutOfSample"

            for (int i = 0; i < reportFiltred.Reports.Count; i++)
            {
                string botName = reportFiltred.Reports[i].BotName.Replace(" InSample", "") + " OutOfSample";
                botNames.Add(botName);
            }

            _asyncBotFactory.CreateNewBots(botNames, botType, isScript,StartProgram.IsOsOptimizer);
        }

        /// <summary>
        /// main stream responsible for optimization
        /// основной поток отвечающий за оптимизацию
        /// </summary>
        private Thread _primeThreadWorker;

        public int BotCountOneFaze()
        {
            List<IIStrategyParameter> allParam = _parameters;

            for (int i = 0; i < _parameters.Count; i++)
            {
                if (allParam[i].Type == StrategyParameterType.Int)
                {
                    ((StrategyParameterInt)allParam[i]).ValueInt = ((StrategyParameterInt)allParam[i]).ValueIntStart;
                }
                if (allParam[i].Type == StrategyParameterType.Decimal)
                {
                    ((StrategyParameterDecimal)allParam[i]).ValueDecimal = ((StrategyParameterDecimal)allParam[i]).ValueDecimalStart;
                }
            }

            List<bool> allOptimezedParam = _parametersOn;


            // 1 consider how many passes we need to do in the first phase/
            // 1 считаем сколько проходов нам нужно сделать в первой фазе

            List<IIStrategyParameter> optimizedParamToCheckCount = new List<IIStrategyParameter>();

            for (int i = 0; i < allParam.Count; i++)
            {
                if (allOptimezedParam[i])
                {
                    optimizedParamToCheckCount.Add(allParam[i]);
                    ReloadParam(allParam[i]);
                }
            }

            optimizedParamToCheckCount = CopyParameters(optimizedParamToCheckCount);

            int countBots = 0;

            bool isStart = true;

            while (true)
            {
                bool isAndOfFaze = false; // all parameters passed/все параметры пройдены

                for (int i2 = 0; i2 < optimizedParamToCheckCount.Count + 1; i2++)
                {
                    if (i2 == optimizedParamToCheckCount.Count)
                    {
                        isAndOfFaze = true;
                        break;
                    }

                    if (isStart)
                    {
                        countBots++;
                        isStart = false;
                        break;
                    }

                    if (optimizedParamToCheckCount[i2].Type == StrategyParameterType.Int)
                    {
                        StrategyParameterInt parameter = (StrategyParameterInt)optimizedParamToCheckCount[i2];

                        if (parameter.ValueInt < parameter.ValueIntStop)
                        {
                            // the current index can increment the value
                            // по текущему индексу можно приращивать значение
                            parameter.ValueInt = parameter.ValueInt + parameter.ValueIntStep;
                            if (i2 > 0)
                            {
                                for (int i3 = 0; i3 < i2; i3++)
                                {
                                    // reset all previous parameters to zero
                                    // сбрасываем все предыдущие параметры в ноль
                                    ReloadParam(optimizedParamToCheckCount[i3]);
                                }
                            }
                            countBots++;
                            break;
                        }
                    }
                    else if (optimizedParamToCheckCount[i2].Type == StrategyParameterType.Decimal
                        )
                    {
                        StrategyParameterDecimal parameter = (StrategyParameterDecimal)optimizedParamToCheckCount[i2];

                        if (parameter.ValueDecimal < parameter.ValueDecimalStop)
                        {
                            // at the current index you can increment the value
                            // по текущему индексу можно приращивать значение
                            parameter.ValueDecimal = parameter.ValueDecimal + parameter.ValueDecimalStep;
                            if (i2 > 0)
                            {
                                for (int i3 = 0; i3 < i2; i3++)
                                {
                                    // reset all previous parameters to zero
                                    // сбрасываем все предыдущие параметры в ноль
                                    ReloadParam(optimizedParamToCheckCount[i3]);
                                }
                            }
                            countBots++;
                            break;
                        }
                    }
                }

                if (isAndOfFaze)
                {
                    break;
                }
            }

            return countBots * (_master.IterationCount * 2);
        }

        public List<OptimazerFazeReport> ReportsToFazes = new List<OptimazerFazeReport>();

        private async void StartOptimazeFazeInSample(OptimizerFaze faze, OptimazerFazeReport report,
            List<IIStrategyParameter> allParameters, List<bool> parametersToOptimization)
        {
            ReloadAllParam(allParameters);

            // 2 проходим первую фазу, когда нужно обойти все варианты

            List<IIStrategyParameter> optimizedParamStart = new List<IIStrategyParameter>();

            for (int i = 0; i < allParameters.Count; i++)
            {
                if (parametersToOptimization[i])
                {
                    optimizedParamStart.Add(allParameters[i]);
                }
            }

            List<IIStrategyParameter> optimizeParamCurrent = CopyParameters(optimizedParamStart);

            ReloadAllParam(optimizeParamCurrent);

            bool isStart = true;

            while (true)
            {
                bool isAndOfFaze = false; // all parameters passed/все параметры пройдены

                for (int i2 = 0; i2 < optimizeParamCurrent.Count + 1; i2++)
                {
                    if (i2 == optimizeParamCurrent.Count)
                    {
                        isAndOfFaze = true;
                        break;
                    }

                    if (isStart)
                    {
                        isStart = false;
                        break;
                    }

                    if (optimizeParamCurrent[i2].Type == StrategyParameterType.Int)
                    {
                        StrategyParameterInt parameter = (StrategyParameterInt)optimizeParamCurrent[i2];

                        if (parameter.ValueInt < parameter.ValueIntStop)
                        {
                            // at the current index you can increment the value
                            // по текущему индексу можно приращивать значение
                            parameter.ValueInt = parameter.ValueInt + parameter.ValueIntStep;
                            if (i2 > 0)
                            {
                                for (int i3 = 0; i3 < i2; i3++)
                                {
                                    // reset all previous parameters to zero
                                    // сбрасываем все предыдущие параметры в ноль
                                    ReloadParam(optimizeParamCurrent[i3]);
                                }
                            }

                            break;
                        }
                    }
                    else if (optimizeParamCurrent[i2].Type == StrategyParameterType.Decimal
                        )
                    {
                        StrategyParameterDecimal parameter = (StrategyParameterDecimal)optimizeParamCurrent[i2];

                        if (parameter.ValueDecimal < parameter.ValueDecimalStop)
                        {
                            // at the current index you can increment the value
                            // по текущему индексу можно приращивать значение
                            parameter.ValueDecimal = parameter.ValueDecimal + parameter.ValueDecimalStep;
                            if (i2 > 0)
                            {
                                for (int i3 = 0; i3 < i2; i3++)
                                {
                                    // reset all previous parameters to zero
                                    // сбрасываем все предыдущие параметры в ноль
                                    ReloadParam(optimizeParamCurrent[i3]);
                                }
                            }
                            break;
                        }
                    }
                }

                if (isAndOfFaze)
                {
                    break;
                }

                while (_servers.Count >= _master.ThreadsCount)
                {
                    Thread.Sleep(50);
                }

                if (_neadToStop)
                {
                    while (true)
                    {
                        Thread.Sleep(50);
                        if (_servers.Count == 0)
                        {
                            break;
                        }
                    }

                    TestReadyEvent?.Invoke(ReportsToFazes);
                    _primeThreadWorker = null;
                    return;
                }

                while (_botsInTest.Count >= _master.ThreadsCount)
                {
                    Thread.Sleep(50);
                }

                //SendLogMessage("BotInSample" ,LogMessageType.System);

                StartNewBot(_parameters, optimizeParamCurrent, report, " InSample");
            }

            while (true)
            {
                Thread.Sleep(50);
                if (_servers.Count == 0)
                //   || _botsInTest.Count == 0)
                {
                    break;
                }
            }

            SendLogMessage(OsLocalization.Optimizer.Message5, LogMessageType.System);
        }

        private void StartOptimazeFazeOutOfSample(OptimazerFazeReport report, OptimazerFazeReport reportInSample)
        {
            SendLogMessage(OsLocalization.Optimizer.Message6, LogMessageType.System);

            for (int i = 0; i < reportInSample.Reports.Count; i++)
            {
                while (_servers.Count >= _master.ThreadsCount)
                {
                    Thread.Sleep(50);
                }

                if (_neadToStop)
                {
                    while (true)
                    {
                        Thread.Sleep(50);
                        if (_servers.Count == 0)
                        {
                            break;
                        }
                    }

                    if (TestReadyEvent != null)
                    {
                        TestReadyEvent(ReportsToFazes);
                    }
                    _primeThreadWorker = null;
                    return;
                }

                while (_botsInTest.Count >= _master.ThreadsCount)
                {
                    Thread.Sleep(50);
                }
                // SendLogMessage("Bot Out of Sample", LogMessageType.System);
                StartNewBot(reportInSample.Reports[i].GetParameters(), new List<IIStrategyParameter>(), report,
                    reportInSample.Reports[i].BotName.Replace(" InSample", "") + " OutOfSample");
            }

            while (true)
            {
                Thread.Sleep(50);
                if (_servers.Count == 0)// && _botsInTest.Count == 0)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// сбросить все параметры на дефолтные значения
        /// </summary>
        private void ReloadAllParam(List<IIStrategyParameter> parameters)
        {
            for (int i = 0; i < parameters.Count; i++)
            {
                ReloadParam(parameters[i]);
            }
        }

        /// <summary>
        /// сбросить параметр на начальные значения
        /// </summary>
        /// <param name="param">the parameter to be reset/параметр который нужно привести в исходное состояние</param>
        private void ReloadParam(IIStrategyParameter param)
        {
            if (param.Type == StrategyParameterType.Int)
            {
                ((StrategyParameterInt)param).ValueInt = ((StrategyParameterInt)param).ValueIntStart;
            }

            if (param.Type == StrategyParameterType.Decimal)
            {
                ((StrategyParameterDecimal)param).ValueDecimal = ((StrategyParameterDecimal)param).ValueDecimalStart;
            }
        }

        /// <summary>
        /// copy parameter list
        /// копировать список параметров
        /// </summary>
        private List<IIStrategyParameter> CopyParameters(List<IIStrategyParameter> paramsToCopy)
        {
            List<IIStrategyParameter> newParameters = new List<IIStrategyParameter>();

            for (int i = 0; i < paramsToCopy.Count; i++)
            {
                IIStrategyParameter newParam = null;

                if (paramsToCopy[i].Type == StrategyParameterType.Bool)
                {
                    newParam = new StrategyParameterBool(paramsToCopy[i].Name, ((StrategyParameterBool)paramsToCopy[i]).ValueBool);
                }
                else if (paramsToCopy[i].Type == StrategyParameterType.String)
                {
                    newParam = new StrategyParameterString(paramsToCopy[i].Name, ((StrategyParameterString)paramsToCopy[i]).ValueString,
                        ((StrategyParameterString)paramsToCopy[i]).ValuesString);
                }
                else if (paramsToCopy[i].Type == StrategyParameterType.Int)
                {
                    newParam = new StrategyParameterInt(paramsToCopy[i].Name,
                        ((StrategyParameterInt)paramsToCopy[i]).ValueIntDefolt,
                        ((StrategyParameterInt)paramsToCopy[i]).ValueIntStart,
                        ((StrategyParameterInt)paramsToCopy[i]).ValueIntStop,
                        ((StrategyParameterInt)paramsToCopy[i]).ValueIntStep);
                    ((StrategyParameterInt)newParam).ValueInt = ((StrategyParameterInt)paramsToCopy[i]).ValueIntStart;
                }
                else if (paramsToCopy[i].Type == StrategyParameterType.Decimal)
                {
                    newParam = new StrategyParameterDecimal(paramsToCopy[i].Name,
                        ((StrategyParameterDecimal)paramsToCopy[i]).ValueDecimalDefolt,
                        ((StrategyParameterDecimal)paramsToCopy[i]).ValueDecimalStart,
                        ((StrategyParameterDecimal)paramsToCopy[i]).ValueDecimalStop,
                        ((StrategyParameterDecimal)paramsToCopy[i]).ValueDecimalStep);
                    ((StrategyParameterDecimal)newParam).ValueDecimal = ((StrategyParameterDecimal)paramsToCopy[i]).ValueDecimalStart;
                }
                newParameters.Add(newParam);

            }
            return newParameters;
        }

        /// <summary>
        /// filtering results at the end of the current phase
        /// фильтрация результатов в конце текущей фазы
        /// </summary>
        private void EndOfFazeFiltration(OptimazerFazeReport bots, OptimazerFazeReport botsToOutOfSample)
        {
            int startCount = bots.Reports.Count;

            for (int i = 0; i < bots.Reports.Count; i++)
            {
                if (_master.IsAcceptedByFilter(bots.Reports[i]))
                {
                    botsToOutOfSample.Reports.Add(bots.Reports[i]);
                }
            }

            if (botsToOutOfSample.Reports.Count == 0)
            {
                /* SendLogMessage(OsLocalization.Optimizer.Message8, LogMessageType.System);
                 MessageBox.Show(OsLocalization.Optimizer.Message8);
                 NeadToMoveUiToEvent(NeadToMoveUiTo.TabsAndTimeFrames);*/
            }
            else if (startCount != botsToOutOfSample.Reports.Count)
            {
                SendLogMessage(OsLocalization.Optimizer.Message9 + (startCount - botsToOutOfSample.Reports.Count), LogMessageType.System);
            }

        }

        /// <summary>
        /// launch another robot as part of optimization
        /// запустить ещё одного робота, в рамках оптимизации
        /// </summary>
        /// <param name="parametrs">list of all parameters/список всех параметров</param>
        /// <param name="paramOptimized">brute force options/параметры по которым осуществляется перебор</param>
        /// <param name="report">current optimization phase/текущая фаза оптимизации</param>
        /// <param name="botsInFaze">list of bots already running in the current phase/список ботов уже запущенный в текущей фазе</param>
        /// <param name="botName">the name of the created robot/имя создаваемого робота</param>
        private void StartNewBot(List<IIStrategyParameter> parametrs, List<IIStrategyParameter> paramOptimized,
            OptimazerFazeReport report, string botName)
        {
            OptimizerServer server = CreateNewServer(report);

            try
            {
                decimal num = Convert.ToDecimal(botName.Substring(0, 1));
            }
            catch
            {
                botName = server.NumberServer + botName;
            }

            BotPanel bot = CreateNewBot(botName, parametrs, paramOptimized, server, StartProgram.IsOsOptimizer);

            // wait for the robot to connect to its data server
            // ждём пока робот подключиться к своему серверу данных

            DateTime timeStartWaiting = DateTime.Now;

            while (bot.IsConnected == false)
            {
                Thread.Sleep(20);

                if (timeStartWaiting.AddSeconds(2000) < DateTime.Now)
                {

                    SendLogMessage(
                        OsLocalization.Optimizer.Message10,
                        LogMessageType.Error);
                    return;
                }
            }

            _botsInTest.Add(bot);
            server.TestingStart();
        }

        private List<BotPanel> _botsInTest = new List<BotPanel>();

        private OptimizerServer CreateNewServer(OptimazerFazeReport report)
        {
            // 1. Create a new server for optimization. And one thread respectively
            // 1. создаём новый сервер для оптимизации. И один поток соответственно
            OptimizerServer server = ServerMaster.CreateNextOptimizerServer(_master.Storage, _serverNum,
                _master.StartDepozit);

            _serverNum++;
            _servers.Add(server);

            server.TestingEndEvent += server_TestingEndEvent;
            server.TypeTesterData = _master.Storage.TypeTesterData;
            server.TestintProgressChangeEvent += server_TestintProgressChangeEvent;

            for (int i = 0; _master.TabsSimpleNamesAndTimeFrames != null
                            && i < _master.TabsSimpleNamesAndTimeFrames.Count; i++)
            {
                Security secToStart =
                    _master.Storage.Securities.Find(s => s.Name == _master.TabsSimpleNamesAndTimeFrames[i].NameSecurity);

                server.GetDataToSecurity(secToStart, _master.TabsSimpleNamesAndTimeFrames[i].TimeFrame, report.Faze.TimeStart,
                    report.Faze.TimeEnd);
            }

            for (int i = 0; _master.TabsIndexNamesAndTimeFrames != null &&
                            i < _master.TabsIndexNamesAndTimeFrames.Count; i++)
            {
                List<string> secNames = _master.TabsIndexNamesAndTimeFrames[i].NamesSecurity;

                for (int i2 = 0; secNames != null && i2 < secNames.Count; i2++)
                {
                    string curSec = secNames[i2];

                    Security secToStart =
                        _master.Storage.Securities.Find(s => s.Name == curSec);

                    server.GetDataToSecurity(secToStart, _master.TabsIndexNamesAndTimeFrames[i].TimeFrame, report.Faze.TimeStart,
                        report.Faze.TimeEnd);
                }
            }

            return server;
        }

        private BotPanel CreateNewBot(string botName,
            List<IIStrategyParameter> parametrs,
            List<IIStrategyParameter> paramOptimized,
            OptimizerServer server, StartProgram regime)
        {
            BotPanel bot = _asyncBotFactory.GetBot(_master.StrategyName, botName);

            for (int i = 0; i < parametrs.Count; i++)
            {
                IIStrategyParameter par = paramOptimized.Find(p => p.Name == parametrs[i].Name);

                if (par == null)
                {
                    par = parametrs[i];
                }

                if (par.Type == StrategyParameterType.Bool)
                {
                    ((StrategyParameterBool)bot.Parameters[i]).ValueBool = ((StrategyParameterBool)par).ValueBool;
                }
                else if (par.Type == StrategyParameterType.String)
                {
                    ((StrategyParameterString)bot.Parameters[i]).ValueString = ((StrategyParameterString)par).ValueString;
                }
                else if (par.Type == StrategyParameterType.Int)
                {
                    ((StrategyParameterInt)bot.Parameters[i]).ValueInt = ((StrategyParameterInt)par).ValueInt;
                }
                else if (par.Type == StrategyParameterType.Decimal)
                {
                    ((StrategyParameterDecimal)bot.Parameters[i]).ValueDecimal = ((StrategyParameterDecimal)par).ValueDecimal;
                }
                else if (par.Type == StrategyParameterType.TimeOfDay)
                {
                    ((StrategyParameterTimeOfDay)bot.Parameters[i]).Value = ((StrategyParameterTimeOfDay)par).Value;
                }
            }

            // custom tabs
            // настраиваем вкладки
            for (int i = 0; i < _master.TabsSimpleNamesAndTimeFrames.Count; i++)
            {
                bot.TabsSimple[i].Connector.ServerType = ServerType.Optimizer;
                bot.TabsSimple[i].Connector.PortfolioName = server.Portfolios[0].Number;
                bot.TabsSimple[i].Connector.NamePaper = _master.TabsSimpleNamesAndTimeFrames[i].NameSecurity;
                bot.TabsSimple[i].Connector.TimeFrame =
                    _master.TabsSimpleNamesAndTimeFrames[i].TimeFrame;
                bot.TabsSimple[i].Connector.ServerUid = server.NumberServer;
                bot.TabsSimple[i].ComissionType = _master.CommissionType;
                bot.TabsSimple[i].ComissionValue = _master.CommissionValue;

                if (server.TypeTesterData == TesterDataType.Candle)
                {
                    bot.TabsSimple[i].Connector.CandleMarketDataType = CandleMarketDataType.Tick;
                }
                else if (server.TypeTesterData == TesterDataType.MarketDepthAllCandleState ||
                         server.TypeTesterData == TesterDataType.MarketDepthOnlyReadyCandle)
                {
                    bot.TabsSimple[i].Connector.CandleMarketDataType =
                        CandleMarketDataType.MarketDepth;
                }
            }

            for (int i = 0; _master.TabsIndexNamesAndTimeFrames != null &&
                            i < _master.TabsIndexNamesAndTimeFrames.Count; i++)
            {
                bot.TabsIndex[i].Tabs.Clear();
                for (int i2 = 0; i2 < _master.TabsIndexNamesAndTimeFrames[i].NamesSecurity.Count; i2++)
                {
                    if (i2 >= bot.TabsIndex[i].Tabs.Count)
                    {
                        bot.TabsIndex[i].CreateNewSecurityConnector();
                    }

                    bot.TabsIndex[i].Tabs[i2].ServerType = ServerType.Optimizer;
                    bot.TabsIndex[i].Tabs[i2].PortfolioName = server.Portfolios[0].Number;
                    bot.TabsIndex[i].Tabs[i2].NamePaper = _master.TabsIndexNamesAndTimeFrames[i].NamesSecurity[i2];
                    bot.TabsIndex[i].Tabs[i2].ServerUid = server.NumberServer;
                    bot.TabsIndex[i].Tabs[i2].TimeFrame =
                        _master.TabsIndexNamesAndTimeFrames[i].TimeFrame;

                    if (server.TypeTesterData == TesterDataType.Candle)
                    {
                        bot.TabsIndex[i].Tabs[i2].CandleMarketDataType = CandleMarketDataType.Tick;
                    }
                    else if (server.TypeTesterData == TesterDataType.MarketDepthAllCandleState ||
                             server.TypeTesterData == TesterDataType.MarketDepthOnlyReadyCandle)
                    {
                        bot.TabsIndex[i].Tabs[i2].CandleMarketDataType =
                            CandleMarketDataType.MarketDepth;
                    }
                }
                bot.TabsIndex[i].UserFormula = _master.TabsIndexNamesAndTimeFrames[i].Formula;
            }

            return bot;
        }

        /// <summary>
        /// changed the state of progress of optimization
        /// first parameter: current progressBar value
        /// second parameter: maximum progressBar value
        /// изменилось состояние прогресса оптимизации
        /// первый параметр: текущее значение progressBar
        /// второй параемтр: максимальное значение progressBar
        /// </summary>
        public event Action<int, int> PrimeProgressChangeEvent;

        /// <summary>
        /// the event that you need to move the interface to a certain place
        /// событие о том что нужно переместить интерфейс в определённое место
        /// </summary>
        public event Action<NeadToMoveUiTo> NeadToMoveUiToEvent;

        // единичный тест

        public BotPanel TestBot(OptimazerFazeReport reportFaze, OptimizerReport reportToBot)
        {
            if (_primeThreadWorker != null)
            {
                return null;
            }

            string botName = NumberGen.GetNumberDeal(StartProgram.IsOsOptimizer).ToString();

            List<string> names = new List<string> { botName };
            _asyncBotFactory.CreateNewBots(names, _master.StrategyName, _master.IsScript, StartProgram.IsTester);

            OptimizerServer server = CreateNewServer(reportFaze);

            List<IIStrategyParameter> parametrs = reportToBot.GetParameters();

            BotPanel bot = CreateNewBot(botName,
                parametrs, parametrs, server, StartProgram.IsTester);

            DateTime timeStartWaiting = DateTime.Now;

            while (bot.IsConnected == false)
            {
                Thread.Sleep(50);

                if (timeStartWaiting.AddSeconds(20) < DateTime.Now)
                {

                    SendLogMessage(
                        OsLocalization.Optimizer.Message10,
                        LogMessageType.Error);
                    return null;
                }
            }

            server.TestingStart();

            timeStartWaiting = DateTime.Now;
            while (bot.TabsSimple[0].CandlesAll == null
                   ||
                   bot.TabsSimple[0].TimeServerCurrent.AddDays(1) < reportFaze.Faze.TimeEnd)
            {
                Thread.Sleep(20);
                if (timeStartWaiting.AddSeconds(20) < DateTime.Now)
                {
                    break;
                }
            }

            return bot;
        }

        // server performing optimization сервера проводящие оптимизацию

        /// <summary>
        /// server optimization
        /// сервера оптимизации
        /// </summary>
        private List<OptimizerServer> _servers = new List<OptimizerServer>();

        /// <summary>
        /// current server sequence number
        /// порядковый номер текущего сервера
        /// </summary>
        private int _serverNum;

        /// <summary>
        /// maximum number of possible detours during the optimization process
        /// максимальное кол-во обходов возможных за процесс оптимизации
        /// </summary>
        private int _countAllServersMax;

        /// <summary>
        /// Object that prevents multi-threaded access in server_TestingEndEvent
        /// объект препятствующий многопоточному доступу в server_TestingEndEvent
        /// </summary>
        private object _serverRemoveLocker = new object();

        /// <summary>
        /// server completed testing
        /// сервер закончил тестирование
        /// </summary>
        /// <param name="serverNum">server number/номер сервера</param>
        private void server_TestingEndEvent(int serverNum)
        {
            TestingProgressChangeEvent?.Invoke(100, 100, serverNum);

            PrimeProgressChangeEvent?.Invoke(serverNum, _countAllServersMax);

            lock (_serverRemoveLocker)
            {
                BotPanel bot = _botsInTest.Find(b => b.TabsSimple[0].Connector.ServerUid == serverNum);

                if (bot != null)
                {
                    // записываем результаты тестов, когда они пройдут
                    ReportsToFazes[ReportsToFazes.Count - 1].Load(bot);
                    // уничтожаем робота
                    bot.Clear();
                    bot.Delete();
                   _botsInTest.Remove(bot);
                }

                for (int i = 0; i < _servers.Count; i++)
                {
                    if (_servers[i].NumberServer == serverNum)
                    {
                        _servers[i].TestingEndEvent -= server_TestingEndEvent;
                        _servers[i].TestintProgressChangeEvent -= server_TestintProgressChangeEvent;
                        _servers[i].Clear();
                        _servers.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// event: optimization is over
        /// событие: оптимизация окончена
        /// </summary>
        public event Action<List<OptimazerFazeReport>> TestReadyEvent;

        /// <summary>
        /// inbound event: testing progress state changed
        /// входящее событие: изменилось состояние прогресса тестирования
        /// </summary>
        /// <param name="curVal">current value for Progressbar/текущее значение для Прогрессбара</param>
        /// <param name="maxVal">maximum value for progress bar/максимальное значение для прогрессБара</param>
        /// <param name="numServer">server number/номер сервера</param>
        private void server_TestintProgressChangeEvent(int curVal, int maxVal, int numServer)
        {
            if (TestingProgressChangeEvent != null)
            {
                TestingProgressChangeEvent(curVal, maxVal, numServer);
            }
        }


        /// <summary>
        /// event: the state of progress has changed
        /// событие: изменилось состояние прогресса 
        /// </summary>
        public event Action<int, int, int> TestingProgressChangeEvent;

        // logging/логирование

        /// <summary>
        /// send up a new message
        /// выслать наверх новое сообщение
        /// </summary>
        /// <param name="message">Message text/текст сообщения</param>
        /// <param name="type">message type/тип сообщения</param>
        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        /// <summary>
        /// event: new message for log
        /// событие: новое сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }
}