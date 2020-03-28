using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers.Optimizer;
using OsEngine.Market.Servers.Tester;
using OsEngine.OsTrader.Panels;
using OsEngine.Robots;

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
        }

        /// <summary>
        /// object providing data for settings
        /// объект предоставляющий данные для настроек
        /// </summary>
        private OptimizerMaster _master;

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

            _primeThreadWorker = new Task(PrimeThreadWorkerPlace);
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
        private List<IIStrategyParameter> _parameters;

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

        // optimization algorithm/алгоритм оптимизации 

        /// <summary>
        /// main stream responsible for optimization
        /// основной поток отвечающий за оптимизацию
        /// </summary>
        private Task _primeThreadWorker;

        /// <summary>
        /// place of work flow responsible for optimization
        /// место работы потока отвечающего за оптимизацию
        /// </summary>
        private async void PrimeThreadWorkerPlace()
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


            // 1 consider how many passes we need to do in the first phase/1 считаем сколько проходов нам нужно сделать в первой фазе

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


            _countAllServersMax = countBots;

            SendLogMessage(OsLocalization.Optimizer.Message4 + countBots, LogMessageType.System);

            // 2 pass the first phase when you need to bypass all the options
            // 2 проходим первую фазу, когда нужно обойти все варианты

            List<IIStrategyParameter> optimizedParam = new List<IIStrategyParameter>();

            for (int i = 0; i < allParam.Count; i++)
            {
                if (allOptimezedParam[i])
                {
                    optimizedParam.Add(allParam[i]);
                }
            }

            List<OptimizerFaze> fazes = _master.Fazes;

            List<IIStrategyParameter> currentParam = CopyParameters(optimizedParam);

            for (int i2 = 0; i2 < optimizedParam.Count; i2++)
            {
                ReloadParam(currentParam[i2]);
            }

            List<BotPanel> botsInFaze = new List<BotPanel>();

            isStart = true;


            bool neadToReloadParam = false;

            while (true)
            {
                bool isAndOfFaze = false; // all parameters passed/все параметры пройдены

                for (int i2 = 0; i2 < currentParam.Count + 1; i2++)
                {
                    if (i2 == currentParam.Count)
                    {
                        isAndOfFaze = true;
                        break;
                    }

                    if (isStart)
                    {
                        isStart = false;
                        break;
                    }

                    if (currentParam[i2].Type == StrategyParameterType.Int)
                    {
                        StrategyParameterInt parameter = (StrategyParameterInt)currentParam[i2];

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
                                    ReloadParam(currentParam[i3]);
                                }
                            }

                            break;
                        }
                    }
                    else if (currentParam[i2].Type == StrategyParameterType.Decimal
                        )
                    {
                        StrategyParameterDecimal parameter = (StrategyParameterDecimal)currentParam[i2];

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
                                    ReloadParam(currentParam[i3]);
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
                    await Task.Delay(2000);
                }

                if (_neadToStop)
                {
                    while (true)
                    {
                        await Task.Delay(1000);
                        if (_servers.Count == 0)
                        {
                            break;
                        }
                    }

                    for (int i = 0; i < botsInFaze.Count; i++)
                    {
                        botsInFaze[i].Delete();
                    }
                    if (TestReadyEvent != null)
                    {
                        TestReadyEvent(botsInFaze, null);
                    }
                    _primeThreadWorker = null;
                    return;
                }

                StartNewBot(allParam, currentParam, fazes[0], botsInFaze, _serverNum.ToString() + " InSample");

            } // while enumerating parameters/while по перебору параметров

            while (true)
            {
                await Task.Delay(1000);
                if (_servers.Count == 0)
                {
                    break;
                }
            }

            for (int i = 0; i < botsInFaze.Count; i++)
            {
                botsInFaze[i].Delete();
            }

            SendLogMessage(OsLocalization.Optimizer.Message5, LogMessageType.System);

            // 3 filter/3 фильтруем 

            List<BotPanel> botsToOutOfSample = new List<BotPanel>();

            EndOfFazeFiltration(botsInFaze, fazes[0], botsToOutOfSample);

            // 4 do forwards/4 делаем форварды

            SendLogMessage(OsLocalization.Optimizer.Message6, LogMessageType.System);

            List<BotPanel> botsOutOfSample = new List<BotPanel>();

            for (int i = 0; i < botsToOutOfSample.Count; i++)
            {
                while (_servers.Count >= _master.ThreadsCount)
                {
                    await Task.Delay(2000);
                }

                if (_neadToStop)
                {
                    while (true)
                    {
                        await Task.Delay(1000);
                        if (_servers.Count == 0)
                        {
                            break;
                        }
                    }
                    for (int i2 = 0; i2 < botsOutOfSample.Count; i2++)
                    {
                        botsOutOfSample[i2].Delete();
                    }
                    if (TestReadyEvent != null)
                    {
                        TestReadyEvent(botsToOutOfSample, botsOutOfSample);
                    }
                    _primeThreadWorker = null;
                    return;
                }

                StartNewBot(botsToOutOfSample[i].Parameters, new List<IIStrategyParameter>(), fazes[1], botsOutOfSample,
                    botsToOutOfSample[i].NameStrategyUniq.Replace(" InSample", "") + " OutOfSample");
            }

            while (true)
            {
                await Task.Delay(1000);
                if (_servers.Count == 0)
                {
                    break;
                }
            }

            for (int i = 0; i < botsOutOfSample.Count; i++)
            {
                botsOutOfSample[i].Delete();
            }

            SendLogMessage(OsLocalization.Optimizer.Message7, LogMessageType.System);

            if (TestReadyEvent != null)
            {
                TestReadyEvent(botsInFaze, botsOutOfSample);
            }
            _primeThreadWorker = null;
        }

        /// <summary>
        /// reset parameter to initial values
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
        private void EndOfFazeFiltration(List<BotPanel> bots, OptimizerFaze faze, List<BotPanel> botsToOutOfSample)
        {
            int startCount = bots.Count;

            for (int i = 0; i < bots.Count; i++)
            {
                if (_master.FilterMiddleProfitIsOn &&
                    bots[i].MiddleProfitInPersent < _master.FilterMiddleProfitValue)
                {

                }
                else if (_master.FilterProfitIsOn &&
                         bots[i].TotalProfitInPersent < _master.FilterProfitValue)
                {

                }
                else if (_master.FilterMaxDrowDownIsOn &&
                         bots[i].MaxDrowDown < _master.FilterMaxDrowDownValue)
                {

                }
                else if (_master.FilterProfitFactorIsOn &&
                         bots[i].ProfitFactor < _master.FilterProfitFactorValue)
                {

                }
                else if (_master.FilterWinPositionIsOn &&
                         bots[i].WinPositionPersent < _master.FilterWinPositionValue / 100)
                {

                }
                else if (_master.FilterDealsCountIsOn &&
                         bots[i].PositionsCount < _master.FilterDealsCountValue)
                {

                }
                else
                {
                    botsToOutOfSample.Add(bots[i]);
                }
            }


            if (botsToOutOfSample.Count == 0)
            {
                SendLogMessage(OsLocalization.Optimizer.Message8, LogMessageType.System);
                MessageBox.Show(OsLocalization.Optimizer.Message8);
                NeadToMoveUiToEvent(NeadToMoveUiTo.TabsAndTimeFrames);
            }
            else if (startCount != botsToOutOfSample.Count)
            {
                SendLogMessage(OsLocalization.Optimizer.Message9 + (startCount - botsToOutOfSample.Count), LogMessageType.System);
            }

        }

        /// <summary>
        /// launch another robot as part of optimization
        /// запустить ещё одного робота, в рамках оптимизации
        /// </summary>
        /// <param name="parametrs">list of all parameters/список всех параметров</param>
        /// <param name="paramOptimized">brute force options/параметры по которым осуществляется перебор</param>
        /// <param name="faze">current optimization phase/текущая фаза оптимизации</param>
        /// <param name="botsInFaze">list of bots already running in the current phase/список ботов уже запущенный в текущей фазе</param>
        /// <param name="botName">the name of the created robot/имя создаваемого робота</param>
        private async void StartNewBot(List<IIStrategyParameter> parametrs, List<IIStrategyParameter> paramOptimized,
            OptimizerFaze faze, List<BotPanel> botsInFaze, string botName)
        {
            if (!MainWindow.GetDispatcher.CheckAccess())
            {
                MainWindow.GetDispatcher.Invoke(
                    new Action
                        <List<IIStrategyParameter>, List<IIStrategyParameter>,
                            OptimizerFaze, List<BotPanel>, string>(StartNewBot),
                    parametrs, paramOptimized, faze, botsInFaze, botName);
                await Task.Delay(1000);
                return;
            }

            // 1. Create a new server for optimization. And one thread respectively
            // 1. создаём новый сервер для оптимизации. И один поток соответственно
            OptimizerServer server = ServerMaster.CreateNextOptimizerServer(_master.Storage, _serverNum,
            _master.StartDepozit);

            _serverNum++;
            _servers.Add(server);

            server.TestingEndEvent += server_TestingEndEvent;
            server.TypeTesterData = _master.Storage.TypeTesterData;
            server.TestintProgressChangeEvent += server_TestintProgressChangeEvent;

            for (int i = 0; i < _master.TabsSimpleNamesAndTimeFrames.Count; i++)
            {
                Security secToStart =
                    _master.Storage.Securities.Find(s => s.Name == _master.TabsSimpleNamesAndTimeFrames[i].NameSecurity);

                server.GetDataToSecurity(secToStart, _master.TabsSimpleNamesAndTimeFrames[i].TimeFrame, faze.TimeStart,
                    faze.TimeEnd);
            }

            // 2. create a new robot and upload it with the appropriate settings and parameters
            // 2. создаём нового робота и прогружаем его соответствующими настройками и параметрами

            BotPanel bot = BotFactory.GetStrategyForName(_master.StrategyName, botName, StartProgram.IsOsOptimizer, _master.IsScript);

            for (int i = 0; i < parametrs.Count; i++)
            {
                IIStrategyParameter par = paramOptimized.Find(p => p.Name == parametrs[i].Name);

                if (par == null)
                {
                    par = parametrs[i];
                }

                if (bot.Parameters[i].Type == StrategyParameterType.Bool)
                {
                    ((StrategyParameterBool)bot.Parameters[i]).ValueBool = ((StrategyParameterBool)par).ValueBool;
                }
                else if (bot.Parameters[i].Type == StrategyParameterType.String)
                {
                    ((StrategyParameterString)bot.Parameters[i]).ValueString = ((StrategyParameterString)par).ValueString;
                }
                else if (bot.Parameters[i].Type == StrategyParameterType.Int)
                {
                    ((StrategyParameterInt)bot.Parameters[i]).ValueInt = ((StrategyParameterInt)par).ValueInt;
                }
                else if (bot.Parameters[i].Type == StrategyParameterType.Decimal)
                {
                    ((StrategyParameterDecimal)bot.Parameters[i]).ValueDecimal = ((StrategyParameterDecimal)par).ValueDecimal;
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

            for (int i = 0; i < _master.TabsIndexNamesAndTimeFrames.Count; i++)
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

            // wait for the robot to connect to its data server
            // ждём пока робот подключиться к своему серверу данных

            DateTime timeStartWaiting = DateTime.Now;

            while (bot.IsConnected == false)
            {
                await Task.Delay(1000);

                if (timeStartWaiting.AddSeconds(20) < DateTime.Now)
                {

                    SendLogMessage(
                        OsLocalization.Optimizer.Message10,
                        LogMessageType.Error);
                    return;
                }
            }

            server.TestingStart();

            botsInFaze.Add(bot);
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
            if (TestingProgressChangeEvent != null)
            {
                TestingProgressChangeEvent(100, 100, serverNum);
            }

            if (PrimeProgressChangeEvent != null)
            {
                PrimeProgressChangeEvent(serverNum, _countAllServersMax);
            }

            lock (_serverRemoveLocker)
            {
                for (int i = 0; i < _servers.Count; i++)
                {
                    if (_servers[i].NumberServer == serverNum)
                    {
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
        public event Action<List<BotPanel>, List<BotPanel>> TestReadyEvent;

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