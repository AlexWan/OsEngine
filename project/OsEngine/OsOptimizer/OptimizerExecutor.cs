/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

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
using OsEngine.OsOptimizer.OptimizerEntity;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Market.Connectors;

namespace OsEngine.OsOptimizer
{
    public class OptimizerExecutor
    {
        #region Service

        public OptimizerExecutor(OptimizerMaster master)
        {
            _master = master;

            _asyncBotFactory = new AsyncBotFactory();
            _asyncBotFactory.LogMessageEvent += SendLogMessage;
        }

        private OptimizerMaster _master;

        private AsyncBotFactory _asyncBotFactory;

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

            _needToStop = false;
            _servers = new List<OptimizerServer>();
            _countAllServersMax = 0;
            _countAllServersEndTest = 0;
            _serverNum = 1;
            _testBotsTime.Clear();

            _primeThreadWorker = new Thread(PrimeThreadWorkerPlace);
            _primeThreadWorker.Name = "OptimizerExecutorThread";
            _primeThreadWorker.IsBackground = true;
            _primeThreadWorker.Start();

            return true;
        }

        public void Stop()
        {
            _needToStop = true;
            SendLogMessage(OsLocalization.Optimizer.Message3, LogMessageType.System);
        }

        private bool _needToStop;

        #endregion

        #region Optimization algorithm

        private void PrimeThreadWorkerPlace()
        {
            ReportsToFazes = new List<OptimizerFazeReport>();

            int countBots = BotCountOneFaze(_parameters, _parametersOn);

            _countAllServersMax = countBots * (_master.IterationCount * 2);

            if (_master.LastInSample)
            {
                _countAllServersMax = _countAllServersMax - countBots;
            }

            SendLogMessage(OsLocalization.Optimizer.Message4 + _countAllServersMax, LogMessageType.System);

            DateTime timeStart = DateTime.Now;

            for (int i = 0; i < _master.Fazes.Count; i++)
            {
                if (_needToStop)
                {
                    _primeThreadWorker = null;
                    TestReadyEvent?.Invoke(ReportsToFazes);
                    return;
                }

                if (_master.Fazes[i].TypeFaze == OptimizerFazeType.InSample)
                {
                    OptimizerFazeReport report = new OptimizerFazeReport();
                    report.Faze = _master.Fazes[i];

                    ReportsToFazes.Add(report);

                    StartAsuncBotFactoryInSample(countBots, _master.StrategyName, _master.IsScript, "InSample");

                    StartOptimizeFazeInSample(_master.Fazes[i], report, _parameters, _parametersOn);

                    EndOfFazeFiltration(ReportsToFazes[ReportsToFazes.Count - 1]);
                }
                else
                {

                    SendLogMessage("ReportsCount " + ReportsToFazes[ReportsToFazes.Count - 1].Reports.Count.ToString(), LogMessageType.System);

                    OptimizerFazeReport report = new OptimizerFazeReport();
                    report.Faze = _master.Fazes[i];

                    ReportsToFazes.Add(report);

                    StartAsuncBotFactoryOutOfSample(ReportsToFazes[ReportsToFazes.Count - 2], _master.StrategyName, _master.IsScript, "OutOfSample");

                    StartOptimizeFazeOutOfSample(report, ReportsToFazes[ReportsToFazes.Count - 2]);
                }
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

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
                string botName = (startServerIndex + i) + " OpT " + faze;
                botNames.Add(botName);
            }

            _asyncBotFactory.CreateNewBots(botNames, botType, isScript, StartProgram.IsOsOptimizer);
        }

        private void StartAsuncBotFactoryOutOfSample(OptimizerFazeReport reportFiltered, string botType, bool isScript, string faze)
        {
            List<string> botNames = new List<string>();

            for (int i = 0; i < reportFiltered.Reports.Count; i++)
            {
                if (reportFiltered.Reports[i] == null)
                {
                    reportFiltered.Reports.RemoveAt(i);
                    i--;
                    continue;
                }

                string botName = reportFiltered.Reports[i].BotName.Replace(" InSample", "") + " OutOfSample";
                botNames.Add(botName);
            }

            _asyncBotFactory.CreateNewBots(botNames, botType, isScript, StartProgram.IsOsOptimizer);
        }

        private Thread _primeThreadWorker;

        public int BotCountOneFaze(List<IIStrategyParameter> parameters, List<bool> parametersOn)
        {
            List<IIStrategyParameter> allParam = parameters;

            for (int i = 0; i < allParam.Count; i++)
            {
                if (allParam[i].Type == StrategyParameterType.Int)
                {
                    ((StrategyParameterInt)allParam[i]).ValueInt = ((StrategyParameterInt)allParam[i]).ValueIntStart;
                }
                if (allParam[i].Type == StrategyParameterType.Decimal)
                {
                    ((StrategyParameterDecimal)allParam[i]).ValueDecimal = ((StrategyParameterDecimal)allParam[i]).ValueDecimalStart;
                }
                if (allParam[i].Type == StrategyParameterType.DecimalCheckBox)
                {
                    ((StrategyParameterDecimalCheckBox)allParam[i]).ValueDecimal = ((StrategyParameterDecimalCheckBox)allParam[i]).ValueDecimalStart;
                }
            }

            List<bool> allOptimezedParam = parametersOn;


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
                if (countBots > 5000000)
                {
                    SendLogMessage("Iteration count > 5000000. Warning!!!", LogMessageType.Error);
                    return countBots;
                }

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
                    else if (optimizedParamToCheckCount[i2].Type == StrategyParameterType.DecimalCheckBox
                        )
                    {
                        StrategyParameterDecimalCheckBox parameter = (StrategyParameterDecimalCheckBox)optimizedParamToCheckCount[i2];

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

            return countBots;
        }

        public List<OptimizerFazeReport> ReportsToFazes = new List<OptimizerFazeReport>();

        private void StartOptimizeFazeInSample(OptimizerFaze faze, OptimizerFazeReport report,
            List<IIStrategyParameter> allParameters, List<bool> parametersToOptimization)
        {
            ReloadAllParam(allParameters);

            // 2 проходим первую фазу, когда нужно обойти все варианты

            List<IIStrategyParameter> optimizedParametersStart = new List<IIStrategyParameter>();

            for (int i = 0; i < allParameters.Count; i++)
            {
                if (parametersToOptimization[i])
                {
                    optimizedParametersStart.Add(allParameters[i]);
                }
            }

            List<IIStrategyParameter> optimizeParamCurrent = CopyParameters(optimizedParametersStart);

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
                    else if (optimizeParamCurrent[i2].Type == StrategyParameterType.DecimalCheckBox
                        )
                    {
                        StrategyParameterDecimalCheckBox parameter = (StrategyParameterDecimalCheckBox)optimizeParamCurrent[i2];

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
                    Thread.Sleep(1);
                }

                if (_needToStop)
                {
                    while (true)
                    {
                        Thread.Sleep(1);

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
                    Thread.Sleep(1);
                }

                //SendLogMessage("BotInSample" ,LogMessageType.System);
                // (startServerIndex + i) + " OpT " + faze;
                StartNewBot(_parameters, optimizeParamCurrent, report, " OpT InSample");
            }

            while (true)
            {
                Thread.Sleep(50);
                if (_servers.Count == 0)
                {
                    break;
                }
            }

            SendLogMessage(OsLocalization.Optimizer.Message5, LogMessageType.System);
        }

        private void StartOptimizeFazeOutOfSample(OptimizerFazeReport report, OptimizerFazeReport reportInSample)
        {
            SendLogMessage(OsLocalization.Optimizer.Message6, LogMessageType.System);

            for (int i = 0; i < reportInSample.Reports.Count; i++)
            {
                while (_servers.Count >= _master.ThreadsCount)
                {
                    Thread.Sleep(1);
                }

                if (_needToStop)
                {
                    while (true)
                    {
                        Thread.Sleep(1);
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
                    Thread.Sleep(1);
                }
                // SendLogMessage("Bot Out of Sample", LogMessageType.System);
                StartNewBot(reportInSample.Reports[i].GetParameters(), null, report,
                    reportInSample.Reports[i].BotName.Replace(" InSample", "") + " OutOfSample");
            }

            while (true)
            {
                Thread.Sleep(1);
                if (_servers.Count == 0)// && _botsInTest.Count == 0)
                {
                    break;
                }
            }
        }

        private List<bool> _parametersOn;

        public List<IIStrategyParameter> _parameters;

        private void ReloadAllParam(List<IIStrategyParameter> parameters)
        {
            for (int i = 0; i < parameters.Count; i++)
            {
                ReloadParam(parameters[i]);
            }
        }

        private void ReloadParam(IIStrategyParameter parameters)
        {
            if (parameters.Type == StrategyParameterType.Int)
            {
                ((StrategyParameterInt)parameters).ValueInt = ((StrategyParameterInt)parameters).ValueIntStart;
            }

            if (parameters.Type == StrategyParameterType.Decimal)
            {
                ((StrategyParameterDecimal)parameters).ValueDecimal = ((StrategyParameterDecimal)parameters).ValueDecimalStart;
            }

            if (parameters.Type == StrategyParameterType.DecimalCheckBox)
            {
                ((StrategyParameterDecimalCheckBox)parameters).ValueDecimal = ((StrategyParameterDecimalCheckBox)parameters).ValueDecimalStart;
            }
        }

        private List<IIStrategyParameter> CopyParameters(List<IIStrategyParameter> parametersToCopy)
        {
            List<IIStrategyParameter> newParameters = new List<IIStrategyParameter>();

            for (int i = 0; i < parametersToCopy.Count; i++)
            {
                IIStrategyParameter newParam = null;

                if (parametersToCopy[i].Type == StrategyParameterType.Bool)
                {
                    newParam = new StrategyParameterBool(parametersToCopy[i].Name, ((StrategyParameterBool)parametersToCopy[i]).ValueBool);
                }
                else if (parametersToCopy[i].Type == StrategyParameterType.String)
                {
                    newParam = new StrategyParameterString(parametersToCopy[i].Name, ((StrategyParameterString)parametersToCopy[i]).ValueString,
                        ((StrategyParameterString)parametersToCopy[i]).ValuesString);
                }
                else if (parametersToCopy[i].Type == StrategyParameterType.Int)
                {
                    newParam = new StrategyParameterInt(parametersToCopy[i].Name,
                        ((StrategyParameterInt)parametersToCopy[i]).ValueIntDefolt,
                        ((StrategyParameterInt)parametersToCopy[i]).ValueIntStart,
                        ((StrategyParameterInt)parametersToCopy[i]).ValueIntStop,
                        ((StrategyParameterInt)parametersToCopy[i]).ValueIntStep);
                    ((StrategyParameterInt)newParam).ValueInt = ((StrategyParameterInt)parametersToCopy[i]).ValueIntStart;
                }
                else if (parametersToCopy[i].Type == StrategyParameterType.Decimal)
                {
                    newParam = new StrategyParameterDecimal(parametersToCopy[i].Name,
                        ((StrategyParameterDecimal)parametersToCopy[i]).ValueDecimalDefolt,
                        ((StrategyParameterDecimal)parametersToCopy[i]).ValueDecimalStart,
                        ((StrategyParameterDecimal)parametersToCopy[i]).ValueDecimalStop,
                        ((StrategyParameterDecimal)parametersToCopy[i]).ValueDecimalStep);
                    ((StrategyParameterDecimal)newParam).ValueDecimal = ((StrategyParameterDecimal)parametersToCopy[i]).ValueDecimalStart;
                }
                else if (parametersToCopy[i].Type == StrategyParameterType.DecimalCheckBox)
                {
                    newParam = new StrategyParameterDecimalCheckBox(parametersToCopy[i].Name,
                        ((StrategyParameterDecimalCheckBox)parametersToCopy[i]).ValueDecimalDefolt,
                        ((StrategyParameterDecimalCheckBox)parametersToCopy[i]).ValueDecimalStart,
                        ((StrategyParameterDecimalCheckBox)parametersToCopy[i]).ValueDecimalStop,
                        ((StrategyParameterDecimalCheckBox)parametersToCopy[i]).ValueDecimalStep,
                        Convert.ToBoolean(((StrategyParameterDecimalCheckBox)parametersToCopy[i]).CheckState));
                    ((StrategyParameterDecimalCheckBox)newParam).ValueDecimal = ((StrategyParameterDecimalCheckBox)parametersToCopy[i]).ValueDecimalStart;
                }

                newParameters.Add(newParam);

            }
            return newParameters;
        }

        private void EndOfFazeFiltration(OptimizerFazeReport bots)
        {
            try
            {
                if (bots.Reports == null ||
                    bots.Reports.Count == 0)
                {
                    return;
                }

                OptimizerFazeReport botsFiltered = new OptimizerFazeReport();

                int startCount = bots.Reports.Count;

                for (int i = 0; i < bots.Reports.Count; i++)
                {
                    if (_master.IsAcceptedByFilter(bots.Reports[i]))
                    {
                        botsFiltered.Reports.Add(bots.Reports[i]);
                    }
                }

                if (botsFiltered.Reports.Count == 0)
                {
                    /* SendLogMessage(OsLocalization.Optimizer.Message8, LogMessageType.System);
                     MessageBox.Show(OsLocalization.Optimizer.Message8);
                     NeedToMoveUiToEvent(NeedToMoveUiTo.TabsAndTimeFrames);*/
                }
                else if (startCount != botsFiltered.Reports.Count)
                {
                    SendLogMessage(OsLocalization.Optimizer.Message9 + (startCount - botsFiltered.Reports.Count), LogMessageType.System);
                }

                bots.Reports = botsFiltered.Reports;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void StartNewBot(List<IIStrategyParameter> parameters, List<IIStrategyParameter> parametersOptimized,
            OptimizerFazeReport report, string botName)
        {
            OptimizerServer server = CreateNewServer(report, true);

            try
            {
                decimal num = Convert.ToDecimal(botName.Substring(0, 1));
            }
            catch
            {
                botName = server.NumberServer + botName;
            }

            BotPanel bot = CreateNewBot(botName, parameters, parametersOptimized, server, StartProgram.IsOsOptimizer);

            if (bot == null)
            {
                SendLogMessage("Critical Optimizer Error. Robot cannot be created", LogMessageType.Error);
                return;
            }

            // wait for the robot to connect to its data server
            // ждём пока робот подключиться к своему серверу данных

            DateTime timeStartWaiting = DateTime.Now;

            while (bot.IsConnected == false)
            {
                Thread.Sleep(1);

                if (timeStartWaiting.AddSeconds(2000) < DateTime.Now)
                {

                    SendLogMessage(
                        OsLocalization.Optimizer.Message10,
                        LogMessageType.Error);
                    return;
                }
            }

            lock (_serverRemoveLocker)
            {
                _botsInTest.Add(bot);
            }

            server.TestingStart();
        }

        private List<BotPanel> _botsInTest = new List<BotPanel>();

        private OptimizerServer CreateNewServer(OptimizerFazeReport report, bool needToDelete)
        {
            // 1. Create a new server for optimization. And one thread respectively
            // 1. создаём новый сервер для оптимизации. И один поток соответственно
            OptimizerServer server = ServerMaster.CreateNextOptimizerServer(_master.Storage, _serverNum,
                _master.StartDeposit);

            server.OrderExecutionType = _master.OrderExecutionType;
            server.SlippageToSimpleOrder = _master.SlippageToSimpleOrder;
            server.SlippageToStopOrder = _master.SlippageToStopOrder;
            server.ClearingTimes = _master.ClearingTimes;
            server.NonTradePeriods = _master.NonTradePeriods;

            lock (_serverRemoveLocker)
            {
                _serverNum++;
                _servers.Add(server);
            }

            if (needToDelete)
            {
                server.TestingEndEvent += server_TestingEndEvent;
            }

            server.TypeTesterData = _master.Storage.TypeTesterData;
            server.TestingProgressChangeEvent += server_TestingProgressChangeEvent;

            List<IIBotTab> sources = _master.BotToTest.GetTabs();

            for (int i = 0; i < sources.Count; i++)
            {
                if (sources[i].TabType == BotTabType.Simple)
                {// BotTabSimple
                    BotTabSimple simple = (BotTabSimple)sources[i];

                    Security secToStart =
                    _master.Storage.Securities.Find(s => s.Name == simple.Connector.SecurityName);

                    server.GetDataToSecurity(secToStart, simple.Connector.TimeFrame, report.Faze.TimeStart,
                        report.Faze.TimeEnd);
                }
                else if (sources[i].TabType == BotTabType.Index)
                {// BotTabIndex
                    BotTabIndex index = (BotTabIndex)sources[i];

                    for (int i2 = 0; i2 < index.Tabs.Count; i2++)
                    {
                        Security secToStart =
                          _master.Storage.Securities.Find(s => s.Name == index.Tabs[i2].SecurityName);

                        server.GetDataToSecurity(secToStart, index.Tabs[i2].TimeFrame, report.Faze.TimeStart,
                            report.Faze.TimeEnd);
                    }
                }
                else if (sources[i].TabType == BotTabType.Screener)
                {// BotTabScreener
                    BotTabScreener screener = (BotTabScreener)sources[i];

                    for (int i2 = 0; i2 < screener.Tabs.Count; i2++)
                    {
                        Security secToStart =
                          _master.Storage.Securities.Find(s => s.Name == screener.Tabs[i2].Connector.SecurityName);

                        server.GetDataToSecurity(secToStart, screener.Tabs[i2].Connector.TimeFrame, report.Faze.TimeStart,
                            report.Faze.TimeEnd);
                    }
                }
            }

            return server;
        }

        private BotPanel CreateNewBot(string botName,
            List<IIStrategyParameter> parameters,
            List<IIStrategyParameter> parametersOptimized,
            OptimizerServer server, StartProgram regime)
        {
            BotPanel bot = null;

            try
            {
                bot = _asyncBotFactory.GetBot(_master.StrategyName, botName);

                if (bot.Parameters.Count != parameters.Count)
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
                return null;
            }

            try
            {
                for (int i = 0; i < parameters.Count; i++)
                {
                    IIStrategyParameter par = null;

                    if (parametersOptimized != null)
                    {
                        par = parametersOptimized.Find(p => p.Name == parameters[i].Name);
                    }
                    bool isInOptimizeParameters = true;

                    if (par == null)
                    {
                        isInOptimizeParameters = false;
                        par = parameters[i];
                    }

                    if (par == null)
                    {
                        continue;
                    }

                    if (par.Type == StrategyParameterType.Bool)
                    {
                        ((StrategyParameterBool)bot.Parameters[i]).ValueBool = ((StrategyParameterBool)par).ValueBool;
                    }
                    else if (par.Type == StrategyParameterType.String)
                    {
                        ((StrategyParameterString)bot.Parameters[i]).ValueString = ((StrategyParameterString)par).ValueString;
                    }
                    else if (par.Type == StrategyParameterType.TimeOfDay)
                    {
                        ((StrategyParameterTimeOfDay)bot.Parameters[i]).Value = ((StrategyParameterTimeOfDay)par).Value;
                    }
                    else if (par.Type == StrategyParameterType.CheckBox)
                    {
                        ((StrategyParameterCheckBox)bot.Parameters[i]).CheckState = ((StrategyParameterCheckBox)par).CheckState;
                    }

                    if (isInOptimizeParameters == true
                        || parametersOptimized == null)
                    {
                        if (par.Type == StrategyParameterType.Int)
                        {
                            ((StrategyParameterInt)bot.Parameters[i]).ValueInt = ((StrategyParameterInt)par).ValueInt;
                        }
                        else if (par.Type == StrategyParameterType.Decimal)
                        {
                            ((StrategyParameterDecimal)bot.Parameters[i]).ValueDecimal = ((StrategyParameterDecimal)par).ValueDecimal;
                        }
                        else if (par.Type == StrategyParameterType.DecimalCheckBox)
                        {
                            ((StrategyParameterDecimalCheckBox)bot.Parameters[i]).ValueDecimal = ((StrategyParameterDecimalCheckBox)par).ValueDecimal;
                            ((StrategyParameterDecimalCheckBox)bot.Parameters[i]).CheckState = ((StrategyParameterDecimalCheckBox)par).CheckState;
                        }
                    }
                    else //if (isInOptimizeParameters == false)
                    {
                        if (par.Type == StrategyParameterType.Int)
                        {
                            ((StrategyParameterInt)bot.Parameters[i]).ValueInt = ((StrategyParameterInt)par).ValueIntDefolt;
                        }
                        else if (par.Type == StrategyParameterType.Decimal)
                        {
                            ((StrategyParameterDecimal)bot.Parameters[i]).ValueDecimal = ((StrategyParameterDecimal)par).ValueDecimalDefolt;
                        }
                        else if (par.Type == StrategyParameterType.DecimalCheckBox)
                        {
                            ((StrategyParameterDecimalCheckBox)bot.Parameters[i]).ValueDecimal = ((StrategyParameterDecimalCheckBox)par).ValueDecimalDefolt;
                            ((StrategyParameterDecimalCheckBox)bot.Parameters[i]).CheckState = ((StrategyParameterDecimalCheckBox)par).CheckState;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
                return null;
            }

            try
            {
                // настраиваем источники

                List<IIBotTab> sourcesFrom = _master.BotToTest.GetTabs();
                List<IIBotTab> sourcesTo = bot.GetTabs();

                for (int i = 0; i < sourcesFrom.Count; i++)
                {
                    if (sourcesFrom[i].TabType == BotTabType.Simple)
                    {// BotTabSimple
                        BotTabSimple simpleFrom = (BotTabSimple)sourcesFrom[i];
                        BotTabSimple simpleTo = (BotTabSimple)sourcesTo[i];
                        CopySettingsInBotTabSimpleSource(simpleFrom, simpleTo, server);
                    }
                    else if (sourcesFrom[i].TabType == BotTabType.Index)
                    {// BotTabIndex
                        BotTabIndex indexFrom = (BotTabIndex)sourcesFrom[i];
                        BotTabIndex indexTo = (BotTabIndex)sourcesTo[i];

                        for (int i2 = 0; i2 < indexFrom.Tabs.Count; i2++)
                        {
                            indexTo.CreateNewSecurityConnector();

                            ConnectorCandles indexConnectorFrom = indexFrom.Tabs[i2];
                            ConnectorCandles indexConnectorTo = indexTo.Tabs[i2];

                            CopySettingsInConnectorCandlesSource(indexConnectorFrom, indexConnectorTo, server);
                        }

                        indexTo.AutoFormulaBuilder.DayOfWeekToRebuildIndex = indexFrom.AutoFormulaBuilder.DayOfWeekToRebuildIndex;
                        indexTo.AutoFormulaBuilder.DaysLookBackInBuilding = indexFrom.AutoFormulaBuilder.DaysLookBackInBuilding;
                        indexTo.AutoFormulaBuilder.HourInDayToRebuildIndex = indexFrom.AutoFormulaBuilder.HourInDayToRebuildIndex;
                        indexTo.AutoFormulaBuilder.IndexMultType = indexFrom.AutoFormulaBuilder.IndexMultType;
                        indexTo.AutoFormulaBuilder.IndexSecCount = indexFrom.AutoFormulaBuilder.IndexSecCount;
                        indexTo.AutoFormulaBuilder.IndexSortType = indexFrom.AutoFormulaBuilder.IndexSortType;
                        indexTo.AutoFormulaBuilder.Regime = indexFrom.AutoFormulaBuilder.Regime;
                        indexTo.AutoFormulaBuilder.WriteLogMessageOnRebuild = false;
                        indexTo.UserFormula = indexFrom.UserFormula;
                    }
                    else if (sourcesFrom[i].TabType == BotTabType.Screener)
                    {// BotTabScreener
                        BotTabScreener screenerFrom = (BotTabScreener)sourcesFrom[i];
                        BotTabScreener screenerTo = (BotTabScreener)sourcesTo[i];

                        CopySettingsInScreenerSource(screenerFrom, screenerTo, server);
                        screenerTo.TryLoadTabs();
                        screenerTo.NeedToReloadTabs = true;
                        screenerTo.TryReLoadTabs();
                        screenerTo.ReloadIndicatorsOnTabs();


                    }
                }

                return bot;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private void CopySettingsInScreenerSource(BotTabScreener from, BotTabScreener to, OptimizerServer server)
        {
            to.ServerType = ServerType.Optimizer;
            to.PortfolioName = server.Portfolios[0].Number;
            to.TimeFrame = from.TimeFrame;
            to.ServerUid = server.NumberServer;

            to.SecuritiesClass = from.SecuritiesClass;
            to.CandleCreateMethodType = from.CandleCreateMethodType;
            to.CandleMarketDataType = from.CandleMarketDataType;
            to.CommissionType = _master.CommissionType;
            to.CommissionValue = _master.CommissionValue;
            to.SaveTradesInCandles = from.SaveTradesInCandles;
            to.CandleSeriesRealization.SetSaveString(from.CandleSeriesRealization.GetSaveString());

            for (int i = 0; i < from.SecuritiesNames.Count; i++)
            {
                ActivatedSecurity sec = from.SecuritiesNames[i];
                to.SecuritiesNames.Add(sec);
            }
        }

        private void CopySettingsInConnectorCandlesSource(ConnectorCandles from, ConnectorCandles to, OptimizerServer server)
        {
            to.ServerType = ServerType.Optimizer;
            to.PortfolioName = server.Portfolios[0].Number;
            to.SecurityName = from.SecurityName;
            to.SecurityClass = from.SecurityClass;
            to.ServerUid = server.NumberServer;
            to.CandleCreateMethodType = from.CandleCreateMethodType;
            to.TimeFrame = from.TimeFrame;

            to.TimeFrameBuilder.CandleSeriesRealization.SetSaveString(
                 from.TimeFrameBuilder.CandleSeriesRealization.GetSaveString());

            if (server.TypeTesterData == TesterDataType.Candle)
            {
                to.CandleMarketDataType = CandleMarketDataType.Tick;
            }
            else if (server.TypeTesterData == TesterDataType.MarketDepthAllCandleState ||
                     server.TypeTesterData == TesterDataType.MarketDepthOnlyReadyCandle)
            {
                to.CandleMarketDataType = CandleMarketDataType.MarketDepth;
            }
        }

        private void CopySettingsInBotTabSimpleSource(BotTabSimple from, BotTabSimple to, OptimizerServer server)
        {
            to.Connector.ServerType = ServerType.Optimizer;
            to.Connector.PortfolioName = server.Portfolios[0].Number;
            to.Connector.SecurityName = from.Connector.SecurityName;
            to.Connector.TimeFrame = from.Connector.TimeFrame;

            to.Connector.CandleCreateMethodType = from.Connector.CandleCreateMethodType;

            to.Connector.TimeFrameBuilder.CandleSeriesRealization.SetSaveString(
                from.Connector.TimeFrameBuilder.CandleSeriesRealization.GetSaveString());

            to.Connector.ServerUid = server.NumberServer;
            to.CommissionType = _master.CommissionType;
            to.CommissionValue = _master.CommissionValue;

            if (server.TypeTesterData == TesterDataType.Candle
                || server.TypeTesterData == TesterDataType.TickAllCandleState
                || server.TypeTesterData == TesterDataType.TickOnlyReadyCandle)
            {
                to.Connector.CandleMarketDataType = CandleMarketDataType.Tick;
            }
            else if (server.TypeTesterData == TesterDataType.MarketDepthAllCandleState ||
                     server.TypeTesterData == TesterDataType.MarketDepthOnlyReadyCandle)
            {
                to.Connector.CandleMarketDataType =
                    CandleMarketDataType.MarketDepth;
            }

            to.ManualPositionSupport.DoubleExitIsOn = _master.ManualControl.DoubleExitIsOn;
            to.ManualPositionSupport.DoubleExitSlippage = _master.ManualControl.DoubleExitSlippage;

            to.ManualPositionSupport.ProfitDistance = _master.ManualControl.ProfitDistance;
            to.ManualPositionSupport.ProfitIsOn = _master.ManualControl.ProfitIsOn;
            to.ManualPositionSupport.ProfitSlippage = _master.ManualControl.ProfitSlippage;

            to.ManualPositionSupport.SecondToCloseIsOn = _master.ManualControl.SecondToCloseIsOn;
            to.ManualPositionSupport.SecondToClose = _master.ManualControl.SecondToClose;

            to.ManualPositionSupport.SecondToOpenIsOn = _master.ManualControl.SecondToOpenIsOn;
            to.ManualPositionSupport.SecondToOpen = _master.ManualControl.SecondToOpen;

            to.ManualPositionSupport.SetbackToCloseIsOn = _master.ManualControl.SetbackToCloseIsOn;
            to.ManualPositionSupport.SetbackToClosePosition = _master.ManualControl.SetbackToClosePosition;

            to.ManualPositionSupport.SetbackToOpenIsOn = _master.ManualControl.SetbackToOpenIsOn;
            to.ManualPositionSupport.SetbackToOpenPosition = _master.ManualControl.SetbackToOpenPosition;

            to.ManualPositionSupport.StopDistance = _master.ManualControl.StopDistance;
            to.ManualPositionSupport.StopIsOn = _master.ManualControl.StopIsOn;
            to.ManualPositionSupport.StopSlippage = _master.ManualControl.StopSlippage;

            to.ManualPositionSupport.ProfitDistance = _master.ManualControl.ProfitDistance;
            to.ManualPositionSupport.ProfitIsOn = _master.ManualControl.ProfitIsOn;
            to.ManualPositionSupport.ProfitSlippage = _master.ManualControl.ProfitSlippage;

            to.ManualPositionSupport.TypeDoubleExitOrder = _master.ManualControl.TypeDoubleExitOrder;
            to.ManualPositionSupport.ValuesType = _master.ManualControl.ValuesType;

            to.ManualPositionSupport.OrderTypeTime = _master.ManualControl.OrderTypeTime;
        }

        public event Action<int, int> PrimeProgressChangeEvent;

        public event Action<NeedToMoveUiTo> NeedToMoveUiToEvent { add { } remove { } }

        #endregion

        #region Single bot test

        public BotPanel TestBot(OptimizerFazeReport reportFaze,
            OptimizerReport reportToBot, StartProgram startProgram, AwaitObject awaitObj)
        {
            if (_primeThreadWorker != null)
            {
                return null;
            }

            DateTime startTime = DateTime.Now;

            string botName = NumberGen.GetNumberDeal(StartProgram.IsOsOptimizer).ToString();

            List<string> names = new List<string> { botName };
            _asyncBotFactory.CreateNewBots(names, _master.StrategyName, _master.IsScript, startProgram);

            OptimizerServer server = CreateNewServer(reportFaze, false);

            List<IIStrategyParameter> parametrs = reportToBot.GetParameters();

            BotPanel bot = CreateNewBot(botName,
                parametrs, parametrs, server, startProgram);

            if (bot == null)
            {
                SendLogMessage("Test over with error. A different robot is selected in the optimizer", LogMessageType.Error);
                awaitObj.Dispose();
                return null;
            }

            DateTime timeStartWaiting = DateTime.Now;

            while (bot.IsConnected == false)
            {
                Thread.Sleep(10);

                if (timeStartWaiting.AddSeconds(20) < DateTime.Now)
                {

                    SendLogMessage(
                        OsLocalization.Optimizer.Message10,
                        LogMessageType.Error);
                    return null;
                }
            }

            server.TestingStart();

            int countSameTime = 0;
            DateTime timeServerLast = DateTime.MinValue;

            timeStartWaiting = DateTime.Now;

            while (bot.TimeServer < reportFaze.Faze.TimeEnd)
            {

                Thread.Sleep(1000);
                if (timeStartWaiting.AddSeconds(300) < DateTime.Now)
                {
                    break;
                }

                if (timeServerLast == bot.TimeServer)
                {
                    countSameTime++;

                    if (countSameTime >= 5)
                    { // пять раз подряд время сервера не меняется. Тест окончен
                        break;
                    }
                }
                else
                {
                    timeServerLast = bot.TimeServer;
                    countSameTime = 0;
                }
            }

            if (startTime.AddSeconds(3) > DateTime.Now)
            {
                Thread.Sleep(3000);
            }

            awaitObj.Dispose();

            return bot;
        }

        #endregion

        #region Server performing optimization

        private List<OptimizerServer> _servers = new List<OptimizerServer>();

        private int _serverNum = 1;

        private int _countAllServersMax;

        private int _countAllServersEndTest;

        private object _serverRemoveLocker = new object();

        private List<TimeSpan> _testBotsTime = new List<TimeSpan>();

        private void server_TestingEndEvent(int serverNum, TimeSpan testTime)
        {
            TestingProgressChangeEvent?.Invoke(100, 100, serverNum);
            _countAllServersEndTest++;
            PrimeProgressChangeEvent?.Invoke(_countAllServersEndTest, _countAllServersMax);

            BotPanel bot = null;
            OptimizerServer server = null;

            lock (_serverRemoveLocker)
            {
                for (int i = 0; i < _botsInTest.Count; i++)
                {
                    BotPanel curBot = _botsInTest[i];

                    if (curBot != null
                        && curBot.TabsSimple != null
                        && curBot.TabsSimple.Count > 0
                        && curBot.TabsSimple[0].Connector != null
                        && curBot.TabsSimple[0].Connector.ServerUid == serverNum)
                    {
                        bot = curBot;
                        _botsInTest.RemoveAt(i);
                        break;
                    }
                    else if (curBot != null
                        && curBot.TabsScreener != null
                        && curBot.TabsScreener.Count > 0
                        && curBot.TabsScreener[0].ServerUid == serverNum)
                    {
                        bot = curBot;
                        _botsInTest.RemoveAt(i);
                        break;
                    }
                }

                if (bot != null)
                {
                    ReportsToFazes[ReportsToFazes.Count - 1].Load(bot);
                }

                for (int i = 0; i < _servers.Count; i++)
                {
                    if (_servers[i].NumberServer == serverNum)
                    {
                        _servers[i].TestingEndEvent -= server_TestingEndEvent;
                        _servers[i].TestingProgressChangeEvent -= server_TestingProgressChangeEvent;
                        server = _servers[i];
                        _servers.RemoveAt(i);
                        break;
                    }
                }

                _testBotsTime.Add(testTime);

                if (_testBotsTime.Count % 20 == 0)
                {
                    TimeSpan allTime = TimeSpan.Zero;

                    for (int i = 0; i < _testBotsTime.Count; i++)
                    {
                        allTime = TimeSpan.FromMilliseconds(allTime.TotalMilliseconds + _testBotsTime[i].TotalMilliseconds + 1000);
                    }

                    decimal secondsOnOneTest = Convert.ToDecimal(allTime.TotalSeconds / _testBotsTime.Count);

                    decimal secondsToEndAllTests = (_countAllServersMax - _testBotsTime.Count) * secondsOnOneTest;

                    decimal secondsToEndDivideThreads = secondsToEndAllTests / _master.ThreadsCount;

                    TimeSpan timeToEnd = TimeSpan.FromSeconds(Convert.ToInt32(secondsToEndDivideThreads));

                    if (TimeToEndChangeEvent != null
                        && timeToEnd.TotalSeconds != 0)
                    {
                        TimeToEndChangeEvent(timeToEnd);
                    }
                }
            }

            if (bot != null)
            {
                // уничтожаем робота
                bot.Clear();
                bot.Delete();
            }

            if (server != null)
            {
                ServerMaster.RemoveOptimizerServer(server);
            }
        }

        public event Action<TimeSpan> TimeToEndChangeEvent;

        public event Action<List<OptimizerFazeReport>> TestReadyEvent;

        private void server_TestingProgressChangeEvent(int curVal, int maxVal, int numServer)
        {
            if (TestingProgressChangeEvent != null)
            {
                TestingProgressChangeEvent(curVal, maxVal, numServer);
            }
        }

        public event Action<int, int, int> TestingProgressChangeEvent;

        #endregion

        #region Log

        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }
}