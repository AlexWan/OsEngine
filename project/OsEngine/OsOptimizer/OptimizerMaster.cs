using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Forms.Integration;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers.Optimizer;
using OsEngine.Market.Servers.Tester;
using OsEngine.OsTrader.Panels;

namespace OsEngine.OsOptimizer
{
    /// <summary>
    /// класс хранящий и предоставляющий в себе настройки для оптимизации
    /// </summary>
    public class OptimizerMaster
    {
        public OptimizerMaster()
        {
            _log = new Log("OptimizerLog", StartProgram.IsOsOptimizer);
            _log.Listen(this);

            _threadsCount = 1;
            _startDepozit = 100000;

            Storage = new OptimizerDataStorage();
            Storage.SecuritiesChangeEvent += _storage_SecuritiesChangeEvent;
            Storage.TimeChangeEvent += _storage_TimeChangeEvent;

            _filterProfitValue = 10;
            _filterProfitIsOn = false;
            _filterMaxDrowDownValue = -10;
            _filterMaxDrowDownIsOn = false;
            _filterMiddleProfitValue = 0.001m;
            _filterMiddleProfitIsOn = false;
            _filterWinPositionValue = 40;
            _filterWinPositionIsOn = false;
            _filterProfitFactorValue = 1;
            _filterProfitFactorIsOn = false;

            _percentOnFilration = 30;

            Load();

            _fazeCount = 1;

            SendLogMessage("Начинаем проверку всех стратегий в системе на наличие параметров",LogMessageType.System);

            for (int i = 0; i < 3; i++)
            {
                Thread worker = new Thread(GetNamesStrategyToOptimization);
                worker.Name = i.ToString();
                worker.IsBackground = true;
                worker.Start();
            }

            _optimizerExecutor= new OptimizerExecutor(this);
            _optimizerExecutor.LogMessageEvent += SendLogMessage;
            _optimizerExecutor.TestingProgressChangeEvent += _optimizerExecutor_TestingProgressChangeEvent;
            _optimizerExecutor.PrimeProgressChangeEvent += _optimizerExecutor_PrimeProgressChangeEvent;
            _optimizerExecutor.TestReadyEvent += _optimizerExecutor_TestReadyEvent;
            _optimizerExecutor.NeadToMoveUiToEvent += _optimizerExecutor_NeadToMoveUiToEvent;
            ProgressBarStatuses = new List<ProgressBarStatus>();
            PrimeProgressBarStatus = new ProgressBarStatus();
        }

        /// <summary>
        /// сохранить настройки
        /// </summary>
        private void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\OptimizerSettings.txt", false)
                    )
                {
                    writer.WriteLine(ThreadsCount);
                    writer.WriteLine(StrategyName);
                    writer.WriteLine(StartDepozit);

                    writer.WriteLine(_filterProfitValue);
                    writer.WriteLine(_filterProfitIsOn);
                    writer.WriteLine(_filterMaxDrowDownValue);
                    writer.WriteLine(_filterMaxDrowDownIsOn);
                    writer.WriteLine(_filterMiddleProfitValue);
                    writer.WriteLine(_filterMiddleProfitIsOn);
                    writer.WriteLine(_filterWinPositionValue);
                    writer.WriteLine(_filterWinPositionIsOn);
                    writer.WriteLine(_filterProfitFactorValue);
                    writer.WriteLine(_filterProfitFactorIsOn);

                    writer.WriteLine(_typeOptimization);
                    writer.WriteLine(_typeOptimizationFunction);

                    writer.WriteLine(_timeStart);
                    writer.WriteLine(_timeEnd);
                    writer.WriteLine(_fazeCount);
                    writer.WriteLine(_percentOnFilration);

                    writer.Close();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// загрузить настройки
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\OptimizerSettings.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\OptimizerSettings.txt"))
                {
                    _threadsCount = Convert.ToInt32(reader.ReadLine());
                    _strategyName = reader.ReadLine();
                    _startDepozit = Convert.ToDecimal(reader.ReadLine());
                    _filterProfitValue = Convert.ToDecimal(reader.ReadLine());
                    _filterProfitIsOn = Convert.ToBoolean(reader.ReadLine());
                    _filterMaxDrowDownValue = Convert.ToDecimal(reader.ReadLine());
                    _filterMaxDrowDownIsOn = Convert.ToBoolean(reader.ReadLine());
                    _filterMiddleProfitValue = Convert.ToDecimal(reader.ReadLine());
                    _filterMiddleProfitIsOn = Convert.ToBoolean(reader.ReadLine());
                    _filterWinPositionValue = Convert.ToDecimal(reader.ReadLine());
                    _filterWinPositionIsOn = Convert.ToBoolean(reader.ReadLine());
                    _filterProfitFactorValue = Convert.ToDecimal(reader.ReadLine());
                    _filterProfitFactorIsOn = Convert.ToBoolean(reader.ReadLine());

                    Enum.TryParse(reader.ReadLine(), out _typeOptimization);
                    Enum.TryParse(reader.ReadLine(), out  _typeOptimizationFunction);

                    _timeStart = Convert.ToDateTime(reader.ReadLine());
                    _timeEnd = Convert.ToDateTime(reader.ReadLine());
                    _fazeCount = Convert.ToInt32(reader.ReadLine());
                    _percentOnFilration = Convert.ToDecimal(reader.ReadLine());

                    reader.Close();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

// проверка стратегий на наличие параметров

        /// <summary>
        /// все стратегии с параметрами которые есть в платформе
        /// </summary>
        private List<string> _namesWhithParams = new List<string>();

        /// <summary>
        /// взять все стратегии с параметрами которые есть в платформе
        /// </summary>
        public void GetNamesStrategyToOptimization()
        {
            List<string> names = PanelCreator.GetNamesStrategy();

            int numThread = Convert.ToInt32(Thread.CurrentThread.Name);

            for (int i = numThread; i < names.Count; i += 3)
            {
                BotPanel bot = PanelCreator.GetStrategyForName(names[i], numThread.ToString(), StartProgram.IsOsOptimizer);
                if (bot.Parameters == null ||
                    bot.Parameters.Count == 0)
                {
                    //SendLogMessage("Не оптимизируем. Без параметров: " + bot.GetNameStrategyType(), LogMessageType.System);
                }
                else
                {
                    // SendLogMessage("С параметрами: " + bot.GetNameStrategyType(), LogMessageType.System);
                    _namesWhithParams.Add(names[i]);
                }
                if (numThread == 2)
                {

                }
                bot.Delete();
            }

            if (StrategyNamesReadyEvent != null)
            {

                StrategyNamesReadyEvent(_namesWhithParams);
            }
        }

        /// <summary>
        /// изменился список стратегий с параметрами которые есть в системе
        /// </summary>
        public event Action<List<string>> StrategyNamesReadyEvent;


// работа с прогрессом процесса оптимизации

        /// <summary>
        /// входящее событие: изменился основной прогресс оптимизации
        /// </summary>
        /// <param name="curVal">текущее значение прогрессБара</param>
        /// <param name="maxVal">максимальное значение прогрессБара</param>
        void _optimizerExecutor_PrimeProgressChangeEvent(int curVal, int maxVal)
        {
            PrimeProgressBarStatus.CurrentValue = curVal;
            PrimeProgressBarStatus.MaxValue = maxVal;
        }

        /// <summary>
        /// входящее событие: оптимизация завершилась
        /// </summary>
        /// <param name="bots">роботы InSample</param>
        /// <param name="botsOutOfSample">OutOfSample</param>
        void _optimizerExecutor_TestReadyEvent(List<BotPanel> bots, List<BotPanel> botsOutOfSample)
        {
            PrimeProgressBarStatus.CurrentValue = PrimeProgressBarStatus.MaxValue;
            if (TestReadyEvent != null)
            {
                TestReadyEvent(bots,botsOutOfSample);
            }
        }

        /// <summary>
        /// событие: тестирование завершилось
        /// </summary>
        public event Action<List<BotPanel>, List<BotPanel>> TestReadyEvent; 

        /// <summary>
        /// изменился прогресс по определённому роботу
        /// </summary>
        /// <param name="curVal">текущее значение для прогрессБара</param>
        /// <param name="maxVal">максимальное значение для прогрессБара</param>
        /// <param name="numServer">номер сервера</param>
        void _optimizerExecutor_TestingProgressChangeEvent(int curVal, int maxVal, int numServer)
        {
            ProgressBarStatus status = ProgressBarStatuses.Find(st => st.Num == numServer);

            if (status == null)
            {
                status = new ProgressBarStatus();
                status.Num = numServer;
                ProgressBarStatuses.Add(status);
            }

            status.CurrentValue = curVal;
            status.MaxValue = maxVal;
        }

        /// <summary>
        /// значения для прорисовки прогрессБаров отдельных ботов
        /// </summary>
        public List<ProgressBarStatus> ProgressBarStatuses;

        /// <summary>
        /// значение прогресса для главного прогрессБара
        /// </summary>
        public ProgressBarStatus PrimeProgressBarStatus;

// хранилище данных

        /// <summary>
        /// показать настройки хранилища данных
        /// </summary>
        public void ShowDataStorageDialog()
        {
            Storage.ShowDialog();
        }

        /// <summary>
        /// хранилище данных
        /// </summary>
        public OptimizerDataStorage Storage;

        /// <summary>
        /// в хранилище изменилось время старта и завершения.
        /// Означает что сет был перезагружен
        /// </summary>
        /// <param name="timeStart">время начала данных</param>
        /// <param name="timeEnd">время завершения данных</param>
        void _storage_TimeChangeEvent(DateTime timeStart, DateTime timeEnd)
        {
            TimeStart = timeStart;
            TimeEnd = timeEnd;
        }

        /// <summary>
        /// в хранилище изменился состав бумаг.
        /// Означает что сет был перезагружен
        /// </summary>
        /// <param name="securities">новый список бумаг</param>
        void _storage_SecuritiesChangeEvent(List<Security> securities)
        {
            if (NewSecurityEvent != null)
            {
                NewSecurityEvent(securities);
            }

            TimeStart = Storage.TimeStart;
            TimeEnd = Storage.TimeEnd;
        }

        /// <summary>
        /// событие: изменился список бумаг в хранилище
        /// </summary>
        public event Action<List<Security>> NewSecurityEvent;

// управление 1 вкладка

        /// <summary>
        /// кол-во потоков которые будут одновременно работать над оптимизацией
        /// </summary>
        public int ThreadsCount
        {
            get { return _threadsCount; }
            set
            {
                _threadsCount = value;
                Save();
            }
        }
        private int _threadsCount;

        /// <summary>
        /// имя стратегии которую мы будем оптимизировать
        /// </summary>
        public string StrategyName
        {
            get { return _strategyName; }
            set
            {
                _strategyName = value;
                TabsSimpleNamesAndTimeFrames = new List<TabSimpleEndTimeFrame>();
                TabsIndexNamesAndTimeFrames = new List<TabIndexEndTimeFrame>();
                Save();
            }
        }
        private string _strategyName;

        /// <summary>
        /// начальный депозит
        /// </summary>
        public decimal StartDepozit
        {
            get { return _startDepozit; }
            set
            {
                _startDepozit = value;
                Save();
            }
        }
        private decimal _startDepozit;

        /// <summary>
        /// настройки подключения для обычных вкладок робота
        /// </summary>
        public List<TabSimpleEndTimeFrame> TabsSimpleNamesAndTimeFrames;

        /// <summary>
        /// настройки подключения для вкладок индексов у робота
        /// </summary>
        public List<TabIndexEndTimeFrame> TabsIndexNamesAndTimeFrames; 

        /// <summary>
        /// список бумаг доступных в хранилище
        /// </summary>
        public List<SecurityTester> SecurityTester
        {
            get { return Storage.SecuritiesTester; }
        } 

// вкладка 3, фильтры

        /// <summary>
        /// значение фильтра по профиту
        /// </summary>
        public decimal FilterProfitValue
        {
            get { return _filterProfitValue; }
            set
            {
                _filterProfitValue = value;
                Save();
            }
        }
        private decimal _filterProfitValue;

        /// <summary>
        /// включен ли фильтр по профиту
        /// </summary>
        public bool FilterProfitIsOn
        {
            get { return _filterProfitIsOn; }
            set
            {
                _filterProfitIsOn = value;
                Save();
            }
        }
        private bool _filterProfitIsOn;

        /// <summary>
        /// значение фильтра максимальной просадки
        /// </summary>
        public decimal FilterMaxDrowDownValue
        {
            get { return _filterMaxDrowDownValue; }
            set
            {
                _filterMaxDrowDownValue = value;
                Save();
            }
        }
        private decimal _filterMaxDrowDownValue;

        /// <summary>
        /// включен ли фильтр максимальной просадки
        /// </summary>
        public bool FilterMaxDrowDownIsOn
        {
            get { return _filterMaxDrowDownIsOn; }
            set
            {
                _filterMaxDrowDownIsOn = value;
                Save();
            }
        }
        private bool _filterMaxDrowDownIsOn;

        /// <summary>
        /// значение фильтра среднего профита со сделки
        /// </summary>
        public decimal FilterMiddleProfitValue
        {
            get { return _filterMiddleProfitValue; }
            set
            {
                _filterMiddleProfitValue = value;
                Save();
            }
        }
        private decimal _filterMiddleProfitValue;

        /// <summary>
        /// включен ли фильтр среднего профита со сделки
        /// </summary>
        public bool FilterMiddleProfitIsOn
        {
            get { return _filterMiddleProfitIsOn; }
            set
            {
                _filterMiddleProfitIsOn = value;
                Save();
            }
        }
        private bool _filterMiddleProfitIsOn;

        /// <summary>
        /// значение фильтра процента выигранных сделок
        /// </summary>
        public decimal FilterWinPositionValue
        {
            get { return _filterWinPositionValue; }
            set
            {
                _filterWinPositionValue = value;
                Save();
            }
        }
        private decimal _filterWinPositionValue;

        /// <summary>
        /// включен ли фильтр процента выигранных сделок
        /// </summary>
        public bool FilterWinPositionIsOn
        {
            get { return _filterWinPositionIsOn; }
            set
            {
                _filterWinPositionIsOn = value;
                Save();
            }
        }
        private bool _filterWinPositionIsOn;

        /// <summary>
        /// значение фильтра по профит фактору
        /// </summary>
        public decimal FilterProfitFactorValue
        {
            get { return _filterProfitFactorValue; }
            set
            {
                _filterProfitFactorValue = value;
                Save();
            }
        }
        private decimal _filterProfitFactorValue;

        /// <summary>
        /// включен ли фильтр по профит фактору
        /// </summary>
        public bool FilterProfitFactorIsOn
        {
            get { return _filterProfitFactorIsOn; }
            set
            {
                _filterProfitFactorIsOn = value;
                Save();
            }
        }
        private bool _filterProfitFactorIsOn;

// вкладка 4, оптимизация

        /// <summary>
        /// способ оптимизации
        /// </summary>
        public OptimizationType TypeOptimization
        {
            get { return _typeOptimization; }
            set
            {
                _typeOptimization = value;
                Save();
            }
        }
        private OptimizationType _typeOptimization;

        /// <summary>
        /// выбранная функция на которую будет ориентироваться алгоритм
        /// оптимизации при отсеивании не нужных к обходу веток
        /// </summary>
        public OptimizationFunctionType TypeOprimizationFunction
        {
            get { return _typeOptimizationFunction; }
            set
            {
                _typeOptimizationFunction = value;
                Save();
            }
        }
        private OptimizationFunctionType _typeOptimizationFunction;

// вкладка 5, фазы оптимизации

        /// <summary>
        /// фазы оптимизации
        /// </summary>
        public List<OptimizerFaze> Fazes;

        /// <summary>
        /// время истории для старта оптимизации
        /// </summary>
        public DateTime TimeStart
        {
            get { return _timeStart; }
            set
            {
                _timeStart = value;
                Save();

                if (DateTimeStartEndChange != null)
                {
                    DateTimeStartEndChange();
                }
            }
        }
        private DateTime _timeStart;

        /// <summary>
        /// время истории для завершения оптимизации
        /// </summary>
        public DateTime TimeEnd
        {
            get { return _timeEnd; }
            set
            {
                _timeEnd = value; 
                Save();
                if (DateTimeStartEndChange != null)
                {
                    DateTimeStartEndChange();
                }
            }
        }
        private DateTime _timeEnd;

        /// <summary>
        /// количество фаз оптимизации
        /// </summary>
        public int FazeCount
        {
            get { return _fazeCount; }
            set
            {
                _fazeCount = value;
                Save();
            }
        }
        private int _fazeCount;

        /// <summary>
        /// процент времени на OutOfSample
        /// </summary>
        public decimal PercentOnFilration
        {
            get { return _percentOnFilration; }
            set
            {
                _percentOnFilration = value;
                Save();
            }
        }
        private decimal _percentOnFilration;

        /// <summary>
        /// разбить общее время на фазы
        /// </summary>
        public void ReloadFazes()
        {
            int fazeCount = _fazeCount;

            if (fazeCount < 1)
            {
                fazeCount = 1;
            }

            fazeCount *= 2;
            
            int dayAll = Convert.ToInt32((TimeEnd - TimeStart).TotalDays);

            if (dayAll < 2)
            {
                SendLogMessage("Число дней в истории слишком мало для оптимизации",LogMessageType.System);
                return;
            }

            while (dayAll / fazeCount < 1)
            {
                fazeCount -= 2;
            }

            int dayOutOfSample = Convert.ToInt32(dayAll * (_percentOnFilration/100)) / (fazeCount/2);
            if (dayOutOfSample < 1)
            {
                dayOutOfSample = 1;
            }

            int dayInSample = (dayAll - (dayOutOfSample * (fazeCount / 2))) / (fazeCount / 2);
            if (dayInSample < 0)
            {
                dayInSample = 1;
            }

            List<int> fazesLenght = new List<int>();

            for (int i = 0; i < fazeCount; i++)
            {
                if (i%2 == 0)
                {
                    fazesLenght.Add(dayInSample);
                }
                else
                {
                    fazesLenght.Add(dayOutOfSample);
                }
            }

            while (fazesLenght.Sum() > dayAll)
            {
                for (int i = 0; i < fazesLenght.Count; i++)
                {
                    if (fazesLenght[i] != 1)
                    {
                        fazesLenght[i] -= 1;
                        break;
                    }
                    if (i + 1 == fazesLenght.Count)
                    {
                        SendLogMessage("Слишком малое кол-во дней для такого количества фаз",LogMessageType.System);
                        return;
                    }
                }
            }

            while (fazesLenght.Sum() < dayAll)
            {
                  fazesLenght[0] += 1;
            }


            Fazes = new List<OptimizerFaze>();

            DateTime time = _timeStart;

            for (int i = 0; i < fazeCount; i++)
            {
                OptimizerFaze newFaze = new OptimizerFaze();
                newFaze.TimeStart = time;
                time = time.AddDays(fazesLenght[i]);
                newFaze.TimeEnd = time;
                newFaze.Days = fazesLenght[i];

                if (i%2 != 0)
                {
                    newFaze.TypeFaze = OptimizerFazeType.OutOfSample;
                }
                else
                {
                    newFaze.TypeFaze = OptimizerFazeType.InSample;
                }

                Fazes.Add(newFaze);
            }
        }

        /// <summary>
        /// время старта времени истории для оптимизации изменилось
        /// </summary>
        public event Action DateTimeStartEndChange;

// параметры оптимизации

        /// <summary>
        /// реально применяемые параметры для оптимизации.
        /// доступны для изменения в интерфейсе
        /// </summary>
        public List<IIStrategyParameter> Parameters
        {
            get
            {
                if (string.IsNullOrEmpty(_strategyName))
                {
                    return null;
                }

                BotPanel bot = PanelCreator.GetStrategyForName(_strategyName, "", StartProgram.IsOsOptimizer);

                if (bot == null)
                {
                    return null;
                }

                if (bot.Parameters == null ||
                    bot.Parameters.Count == 0)
                {
                    return null;
                }
                _parameters = bot.Parameters;
                bot.Delete();

                return bot.Parameters;
            }
        }
        private List<IIStrategyParameter> _parameters;

        /// <summary>
        /// список параметров которые включены в оптимизацию
        /// </summary>
        public List<bool> ParametersOn
        {
            get
            {

                    _paramOn = new List<bool>();
                    for (int i = 0; _parameters != null && i < _parameters.Count; i++)
                    {
                        _paramOn.Add(false);
                    }
                

                return _paramOn;
            }
        }
        private List<bool> _paramOn; 


// работа запуска алгоритма оптимизации

        /// <summary>
        /// объект который производит оптимизацию
        /// </summary>
        private OptimizerExecutor _optimizerExecutor;

        /// <summary>
        /// запустить оптимизацию
        /// </summary>
        /// <returns>true - если запуск прошёл успешно</returns>
        public bool Start()
        {
            if (CheckReadyData() == false)
            {
                return false;
            }

            if (_optimizerExecutor.Start(_paramOn, _parameters))
            {
                ProgressBarStatuses = new List<ProgressBarStatus>();
                PrimeProgressBarStatus = new ProgressBarStatus();
            }
            return true;
        }

        /// <summary>
        /// остановить процесс оптимизации
        /// </summary>
        public void Stop()
        {
            _optimizerExecutor.Stop();
        }

        /// <summary>
        /// проверить, всё ли готово для старта тестирования
        /// </summary>
        /// <returns>true - всё готово</returns>
        private bool CheckReadyData()
        {
            if (Fazes == null || Fazes.Count == 0)
            {
                MessageBox.Show("Не возможно запустить оптимизацию. Не сформирована последовательность этапов.");
                SendLogMessage("Не возможно запустить оптимизацию. Не сформирована последовательность этапов.", LogMessageType.System);
                if (NeadToMoveUiToEvent != null)
                {
                    NeadToMoveUiToEvent(NeadToMoveUiTo.Fazes);
                }
                return false;
            }


            if (TabsSimpleNamesAndTimeFrames == null ||
                TabsSimpleNamesAndTimeFrames.Count == 0)
            {
                MessageBox.Show("Не возможно запустить оптимизацию. Для текущего робота не выбраны бумаги и таймфремы во вкладки.");
                SendLogMessage("Не возможно запустить оптимизацию. Для текущего робота не выбраны бумаги и таймфремы во вкладки.", LogMessageType.System);
                if (NeadToMoveUiToEvent != null)
                {
                    NeadToMoveUiToEvent(NeadToMoveUiTo.TabsAndTimeFrames);
                }
                return false;
            }

            if (string.IsNullOrEmpty(Storage.ActiveSet) ||
                Storage.SecuritiesTester == null ||
                Storage.SecuritiesTester.Count == 0)
            {
                MessageBox.Show("Не возможно запустить оптимизацию. Не подключены данные для тестирования.");
                SendLogMessage("Не возможно запустить оптимизацию. Не подключены данные для тестирования.",LogMessageType.System);

                if (NeadToMoveUiToEvent != null)
                {
                    NeadToMoveUiToEvent(NeadToMoveUiTo.Storage);
                }
                return false;
            }

            if (string.IsNullOrEmpty(_strategyName))
            {
                MessageBox.Show("Не возможно запустить оптимизацию. Не выбрана стратегия.");
                SendLogMessage("Не возможно запустить оптимизацию. Не выбрана стратегия.", LogMessageType.System);
                if (NeadToMoveUiToEvent != null)
                {
                    NeadToMoveUiToEvent(NeadToMoveUiTo.NameStrategy);
                }
                return false;
            }

            bool onParamesReady = false;

            for (int i = 0; i < _paramOn.Count; i++)
            {
                if (_paramOn[i])
                {
                    onParamesReady = true;
                    break;
                }
            }

            if (onParamesReady == false)
            {
                MessageBox.Show("Не возможно запустить оптимизацию. Т.к. не выбран ни один параметр оптимизации.");
                SendLogMessage("Не возможно запустить оптимизацию. Т.к. не выбран ни один параметр оптимизации.", LogMessageType.System);
                if (NeadToMoveUiToEvent != null)
                {
                    NeadToMoveUiToEvent(NeadToMoveUiTo.Parametrs);
                }
                return false;
            }


            return true;
        }

        /// <summary>
        /// входящее событие: нужно переместить ГУИ в определённое место
        /// </summary>
        /// <param name="moveUiTo">место для перемещения</param>
        void _optimizerExecutor_NeadToMoveUiToEvent(NeadToMoveUiTo moveUiTo)
        {
            if (NeadToMoveUiToEvent != null)
            {
                NeadToMoveUiToEvent(moveUiTo);
            }
        }

        /// <summary>
        /// событие: нужно переместить ГУИ в определённое место
        /// </summary>
        public event Action<NeadToMoveUiTo> NeadToMoveUiToEvent;

// логирование

        /// <summary>
        /// лог
        /// </summary>
        private Log _log;

        /// <summary>
        /// начать прорисовку лога
        /// </summary>
        public void StartPaintLog(WindowsFormsHost logHost)
        {
            _log.StartPaint(logHost);
        }

        /// <summary>
        /// отправить новое сообщение в лог
        /// </summary>
        /// <param name="message">сообщение</param>
        /// <param name="type">тип сообщения</param>
        public void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message,type);
            }
        }

        /// <summary>
        /// событие: новое сообщение
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

    }

    /// <summary>
    /// объект хранящий в себе значения для прорисовки прогресса
    /// в ProgressBar
    /// </summary>
    public class ProgressBarStatus
    {
        /// <summary>
        /// текущее значение
        /// </summary>
        public int CurrentValue;

        /// <summary>
        /// максимальное значение
        /// </summary>
        public int MaxValue;

        /// <summary>
        /// номер сервера / робота
        /// </summary>
        public int Num;
    }

    /// <summary>
    /// по какому параметру проходит оптимизация
    /// </summary>
    public enum OptimizationFunctionType
    {
        /// <summary>
        /// Итоговый профит
        /// </summary>
        EndProfit,

        /// <summary>
        /// Средний профит со сделки
        /// </summary>
        MiddleProfitFromPosition,

        /// <summary>
        /// Максимальная просадка
        /// </summary>
        MaxDrowDown,

        /// <summary>
        /// Профит фактор
        /// </summary>
        ProfitFactor
    }

    /// <summary>
    /// способ оптимизации
    /// </summary>
    public enum OptimizationType
    {
        /// <summary>
        /// Имитация отжига
        /// </summary>
        SimulatedAnnealing,

        /// <summary>
        /// Генетический алгоритм
        /// </summary>
        GeneticАlgorithm
    }

    /// <summary>
    /// Фаза оптимизации
    /// </summary>
    public class OptimizerFaze
    {
        /// <summary>
        /// тип фазы. Что делаем
        /// </summary>
        public OptimizerFazeType TypeFaze;

        /// <summary>
        /// время начала
        /// </summary>
        public DateTime TimeStart;

        /// <summary>
        /// время завершения
        /// </summary>
        public DateTime TimeEnd;

        /// <summary>
        /// дней на фазу
        /// </summary>
        public int Days;

    }

    /// <summary>
    /// Тип фазы оптимизации
    /// </summary>
    public enum OptimizerFazeType
    {
        /// <summary>
        /// оптимизация
        /// </summary>
        InSample,

        /// <summary>
        /// фильтрация
        /// </summary>
        OutOfSample
    }

    /// <summary>
    /// спецификация инструмента для запуска обычной вкладки
    /// </summary>
    public class TabSimpleEndTimeFrame
    {
        /// <summary>
        /// номер вкладки
        /// </summary>
        public int NumberOfTab;

        /// <summary>
        /// название бумаги
        /// </summary>
        public string NameSecurity;

        /// <summary>
        /// таймфрейм
        /// </summary>
        public TimeFrame TimeFrame;
    }

    /// <summary>
    /// спецификация инструмента для запуска вкладки индекса
    /// </summary>
    public class TabIndexEndTimeFrame
    {
        /// <summary>
        /// номер вкладки
        /// </summary>
        public int NumberOfTab;

        /// <summary>
        /// список бумаг у вкладки
        /// </summary>
        public List<string> NamesSecurity;

        /// <summary>
        /// таймфрейм бумаг в вкладки
        /// </summary>
        public TimeFrame TimeFrame;

        /// <summary>
        /// формула для рассчёта индекса
        /// </summary>
        public string Formula;
    }



    /// <summary>
    /// сообщение о том куда нужно сместить интерфейс, чтобы пользователь увидел что он ещё не настроил для запуска оптимизатора
    /// </summary>
    public enum NeadToMoveUiTo
    {
        /// <summary>
        /// название стратегии
        /// </summary>
        NameStrategy,
        /// <summary>
        /// фазы оптимизации
        /// </summary>
        Fazes,
        /// <summary>
        /// хранилище
        /// </summary>
        Storage,
        /// <summary>
        /// таблица таймфреймов и бумаг для вкладок
        /// </summary>
        TabsAndTimeFrames,
        /// <summary>
        /// таблица параметров
        /// </summary>
        Parametrs,
        /// <summary>
        /// Фильтры
        /// </summary>
        Filters
    }
}
