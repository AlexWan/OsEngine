using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Optimizer;
using OsEngine.Market.Servers.Tester;
using OsEngine.OsTrader.Panels;

namespace OsEngine.OsOptimizer
{

    /// <summary>
    /// класс проводящий оптимизацию
    /// </summary>
    public class OptimizerExecutor
    {
        
        public OptimizerExecutor(OptimizerMaster master)
        {
            _master = master;
        }

        /// <summary>
        /// объект предоставляющий данные для настроек
        /// </summary>
        private OptimizerMaster _master;

        /// <summary>
        /// запустить процесс оптимизации
        /// </summary>
        /// <param name="parametersOn">список включенных в перебор параметров</param>
        /// <param name="parameters">все параметры стратегии</param>
        /// <returns>true если старт оптимизации закончился успешно</returns>
        public bool Start(List<bool> parametersOn, List<IIStrategyParameter> parameters)
        {
            if (_primeThreadWorker != null)
            {
                SendLogMessage("Процесс оптимизации уже запущен. ", LogMessageType.System);
                return false;
            }
            _parametersOn = parametersOn;
            _parameters = parameters;

            SendLogMessage("Запущен процесс оптимизации. ", LogMessageType.System);

            _neadToStop = false;
            _servers = new List<OptimizerServer>();
            _countAllServersMax = 0;
            _serverNum = 0;
            
            _primeThreadWorker = new Thread(PrimeThreadWorkerPlace);
            _primeThreadWorker.IsBackground = true;
            _primeThreadWorker.Start();

            return true;
        }

        /// <summary>
        /// список включенных в перебор параметров. Если true, то параметр с таким номером нужно перебирать
        /// </summary>
        private List<bool> _parametersOn;

        /// <summary>
        /// параметры стратегии
        /// </summary>
        private List<IIStrategyParameter> _parameters;

        /// <summary>
        /// остановить процесс оптимизации
        /// </summary>
        public void Stop()
        {
            _neadToStop = true;
            SendLogMessage("Запрошено экстренное завершение завершение оптимизации. Ждите остановки процессов.", LogMessageType.System);
        }

        /// <summary>
        /// переменная сообщающая потоку управляющему процессом оптимизации 
        /// о том, нужно ли продолжать запускать новых ботов или 
        /// пользователь запросил остановку процесса
        /// </summary>
        private bool _neadToStop;

// алгоритм оптимизации 
        
        /// <summary>
        /// основной поток отвечающий за оптимизацию
        /// </summary>
        private Thread _primeThreadWorker;

        /// <summary>
        /// место работы потока отвечающего за оптимизацию
        /// </summary>
        private void PrimeThreadWorkerPlace()
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
                bool isAndOfFaze = false; // все параметры пройдены

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
                            // по текущему индексу можно приращивать значение
                            parameter.ValueInt = parameter.ValueInt + parameter.ValueIntStep;
                            if (i2 > 0)
                            {
                                for (int i3 = 0; i3 < i2; i3++)
                                {
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
                            // по текущему индексу можно приращивать значение
                            parameter.ValueDecimal = parameter.ValueDecimal + parameter.ValueDecimalStep;
                            if (i2 > 0)
                            {
                                for (int i3 = 0; i3 < i2; i3++)
                                {
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

            SendLogMessage("Количество ботов для обхода: " + countBots, LogMessageType.System);

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
                bool isAndOfFaze = false; // все параметры пройдены
               
                for (int i2 = 0; i2 < currentParam.Count+1; i2++)
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
                            // по текущему индексу можно приращивать значение
                            parameter.ValueInt = parameter.ValueInt + parameter.ValueIntStep;
                            if (i2 > 0)
                            {
                                for (int i3 = 0; i3 < i2; i3++)
                                {
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
                            // по текущему индексу можно приращивать значение
                            parameter.ValueDecimal = parameter.ValueDecimal + parameter.ValueDecimalStep;
                            if (i2 > 0)
                            {
                                for (int i3 = 0; i3 < i2; i3++)
                                {
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
                    Thread.Sleep(2000);
                }

                if (_neadToStop)
                {
                    while (true)
                    {
                        Thread.Sleep(1000);
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

            } // while по перебору параметров

            while (true)
            {
                Thread.Sleep(1000);
                if (_servers.Count == 0)
                {
                    break;
                }
            }

            for (int i = 0; i < botsInFaze.Count; i++)
            {
                botsInFaze[i].Delete();
            }

            SendLogMessage("InSample этап закончен. Фильтруем данные...", LogMessageType.System);

// 3 фильтруем 

            List<BotPanel> botsToOutOfSample = new List<BotPanel>();

            EndOfFazeFiltration(botsInFaze, fazes[0], botsToOutOfSample);

// 4 делаем форварды

            SendLogMessage("Фильтрация окончена. Делаем форвардные тесты...", LogMessageType.System);

            List<BotPanel> botsOutOfSample = new List<BotPanel>();

            for (int i = 0; i < botsToOutOfSample.Count; i++)
            {
                while (_servers.Count >= _master.ThreadsCount)
                {
                    Thread.Sleep(2000);
                }

                if (_neadToStop)
                {
                    while (true)
                    {
                        Thread.Sleep(1000);
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
                Thread.Sleep(1000);
                if (_servers.Count == 0)
                {
                    break;
                }
            }

            for (int i = 0; i < botsOutOfSample.Count; i++)
            {
                botsOutOfSample[i].Delete();
            }

            SendLogMessage("Оптимизация закончена.", LogMessageType.System);

            if (TestReadyEvent != null)
            {
                TestReadyEvent(botsInFaze, botsOutOfSample);
            }
            _primeThreadWorker = null;
        }

        /// <summary>
        /// сбросить параметр на начальные значения
        /// </summary>
        /// <param name="param">параметр который нужно привести в исходное состояние</param>
        private void ReloadParam(IIStrategyParameter param)
        {
            if (param.Type == StrategyParameterType.Int)
            {
                ((StrategyParameterInt) param).ValueInt = ((StrategyParameterInt) param).ValueIntStart;
            }

            if (param.Type == StrategyParameterType.Decimal)
            {
                ((StrategyParameterDecimal) param).ValueDecimal = ((StrategyParameterDecimal) param).ValueDecimalStart;
            }
        }

        /// <summary>
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
                        ((StrategyParameterInt) paramsToCopy[i]).ValueIntDefolt,
                        ((StrategyParameterInt) paramsToCopy[i]).ValueIntStart,
                        ((StrategyParameterInt) paramsToCopy[i]).ValueIntStop,
                        ((StrategyParameterInt) paramsToCopy[i]).ValueIntStep);
                    ((StrategyParameterInt) newParam).ValueInt = ((StrategyParameterInt) paramsToCopy[i]).ValueIntStart;
                }
                else if (paramsToCopy[i].Type == StrategyParameterType.Decimal)
                {
                    newParam = new StrategyParameterDecimal(paramsToCopy[i].Name,
                        ((StrategyParameterDecimal) paramsToCopy[i]).ValueDecimalDefolt,
                        ((StrategyParameterDecimal) paramsToCopy[i]).ValueDecimalStart,
                        ((StrategyParameterDecimal) paramsToCopy[i]).ValueDecimalStop,
                        ((StrategyParameterDecimal) paramsToCopy[i]).ValueDecimalStep);
                    ((StrategyParameterDecimal)newParam).ValueDecimal = ((StrategyParameterDecimal)paramsToCopy[i]).ValueDecimalStart;
                }
                newParameters.Add(newParam);

            }
            return newParameters;
        }

        /// <summary>
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
                         bots[i].WinPositionPersent < _master.FilterWinPositionValue/100)
                {

                }
                else
                {
                    botsToOutOfSample.Add(bots[i]);
                }
            }


            if (botsToOutOfSample.Count == 0)
            {
                SendLogMessage("К сожалению все боты были отфильтрованы. Поставьте более щадящие настройки для выбраковки результатов", LogMessageType.System);
                MessageBox.Show("К сожалению все боты были отфильтрованы. Поставьте более щадящие настройки для выбраковки результатов");
                NeadToMoveUiToEvent(NeadToMoveUiTo.TabsAndTimeFrames);
            }
            else if (startCount != botsToOutOfSample.Count)
            {
                SendLogMessage("Отфильтрованно ботов: " + (startCount - botsToOutOfSample.Count), LogMessageType.System);
            }

        }

        /// <summary>
        /// запустить ещё одного робота, в рамках оптимизации
        /// </summary>
        /// <param name="parametrs">список всех параметров</param>
        /// <param name="paramOptimized">параметры по которым осуществляется перебор</param>
        /// <param name="faze">текущая фаза оптимизации</param>
        /// <param name="botsInFaze">список ботов уже запущенный в текущей фазе</param>
        /// <param name="botName">имя создаваемого робота</param>
        private void StartNewBot(List<IIStrategyParameter> parametrs, List<IIStrategyParameter> paramOptimized,
            OptimizerFaze faze, List<BotPanel> botsInFaze, string botName)
        {

            if (!MainWindow.GetDispatcher.CheckAccess())
            {
                MainWindow.GetDispatcher.Invoke(
                    new Action
                        <List<IIStrategyParameter>, List<IIStrategyParameter>,
                            OptimizerFaze, List<BotPanel>, string>(StartNewBot),
                    parametrs, paramOptimized, faze, botsInFaze, botName);
                Thread.Sleep(1000);
                return;
            }

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
// 2. создаём нового робота и прогружаем его соответствующими настройками и параметрами

            BotPanel bot = PanelCreator.GetStrategyForName(_master.StrategyName, botName);

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
// настраиваем вкладки
            for (int i = 0; i < _master.TabsSimpleNamesAndTimeFrames.Count; i++)
            {
                bot.TabsSimple[i].Connector.ServerType = ServerType.Optimizer;
                bot.TabsSimple[i].Connector.PortfolioName = server.Portfolios[0].Number;
                bot.TabsSimple[i].Connector.NamePaper = _master.TabsSimpleNamesAndTimeFrames[i].NameSecurity;
                bot.TabsSimple[i].Connector.TimeFrameBuilder.TimeFrame =
                    _master.TabsSimpleNamesAndTimeFrames[i].TimeFrame;

                if (server.TypeTesterData == TesterDataType.Candle)
                {
                    bot.TabsSimple[i].Connector.TimeFrameBuilder.CandleCreateType = CandleSeriesCreateDataType.Tick;
                }
                else if (server.TypeTesterData == TesterDataType.MarketDepthAllCandleState ||
                         server.TypeTesterData == TesterDataType.MarketDepthOnlyReadyCandle)
                {
                    bot.TabsSimple[i].Connector.TimeFrameBuilder.CandleCreateType =
                        CandleSeriesCreateDataType.MarketDepth;
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
                    bot.TabsIndex[i].Tabs[i2].TimeFrameBuilder.TimeFrame =
                        _master.TabsIndexNamesAndTimeFrames[i].TimeFrame;

                    if (server.TypeTesterData == TesterDataType.Candle)
                    {
                        bot.TabsIndex[i].Tabs[i2].TimeFrameBuilder.CandleCreateType = CandleSeriesCreateDataType.Tick;
                    }
                    else if (server.TypeTesterData == TesterDataType.MarketDepthAllCandleState ||
                             server.TypeTesterData == TesterDataType.MarketDepthOnlyReadyCandle)
                    {
                        bot.TabsIndex[i].Tabs[i2].TimeFrameBuilder.CandleCreateType =
                            CandleSeriesCreateDataType.MarketDepth;
                    }
                }
                bot.TabsIndex[i].UserFormula = _master.TabsIndexNamesAndTimeFrames[i].Formula;
            }

// ждём пока робот подключиться к своему серверу данных

            DateTime timeStartWaiting = DateTime.Now;

            while (bot.IsConnected == false)
            {
                Thread.Sleep(1000);

                if (timeStartWaiting.AddSeconds(20) < DateTime.Now)
                {

                    SendLogMessage(
                        "Слишком долгое ожидание подклчючения робота к серверу данных. Что-то пошло не так!",
                        LogMessageType.Error);
                    return;
                }
            }

            server.TestingStart();

            botsInFaze.Add(bot);
        }

        /// <summary>
        /// изменилось состояние прогресса оптимизации
        /// первый параметр: текущее значение progressBar
        /// второй параемтр: максимальное значение progressBar
        /// </summary>
        public event Action<int,int> PrimeProgressChangeEvent;

        /// <summary>
        /// событие о том что нужно переместить интерфейс в определённое место
        /// </summary>
        public event Action<NeadToMoveUiTo> NeadToMoveUiToEvent;

// сервера проводящие оптимизацию

        /// <summary>
        /// сервера оптимизации
        /// </summary>
        private List<OptimizerServer> _servers = new List<OptimizerServer>();

        /// <summary>
        /// порядковый номер текущего сервера
        /// </summary>
        private int _serverNum;

        /// <summary>
        /// максимальное кол-во обходов возможных за процесс оптимизации
        /// </summary>
        private int _countAllServersMax;

        /// <summary>
        /// объект препятствующий многопоточному доступу в server_TestingEndEvent
        /// </summary>
        private object _serverRemoveLocker = new object();

        /// <summary>
        /// сервер закончил тестирование
        /// </summary>
        /// <param name="serverNum">номер сервера</param>
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
        /// событие: оптимизация окончена
        /// </summary>
        public event Action<List<BotPanel>, List<BotPanel>> TestReadyEvent;

        /// <summary>
        /// входящее событие: изменилось состояние прогресса тестирования
        /// </summary>
        /// <param name="curVal">текущее значение для Прогрессбара</param>
        /// <param name="maxVal">максимальное значение для прогрессБара</param>
        /// <param name="numServer">номер сервера</param>
        private void server_TestintProgressChangeEvent(int curVal, int maxVal, int numServer)
        {
            if (TestingProgressChangeEvent != null)
            {
                TestingProgressChangeEvent(curVal, maxVal, numServer);
            }
        }


        /// <summary>
        /// событие: изменилось состояние прогресса 
        /// </summary>
        public event Action<int, int, int> TestingProgressChangeEvent;

// логирование

        /// <summary>
        /// выслать наверх новое сообщение
        /// </summary>
        /// <param name="message">текст сообщения</param>
        /// <param name="type">тип сообщения</param>
        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        /// <summary>
        /// событие: новое сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }
}



