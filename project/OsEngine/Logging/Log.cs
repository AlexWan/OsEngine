/*
*Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
*Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Media;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Miner;
using OsEngine.Market.Servers.Optimizer;
using OsEngine.OsConverter;
using OsEngine.OsData;
using OsEngine.OsMiner;
using OsEngine.OsOptimizer;
using OsEngine.OsTrader;
using OsEngine.OsTrader.Panels;
using OsEngine.PrimeSettings;

namespace OsEngine.Logging
{
    /// <summary>
    /// class for logging messages of program
    /// класс для логирования сообщений программы
    /// </summary>
    public class Log
    {
        // static part with thread work saving logs
        // статическая часть с работой потока сохраняющего логи

        /// <summary>
        /// thread
        /// поток 
        /// </summary>
        private static Task _watcher;

        /// <summary>
        /// logs that need to be serviced
        /// логи которые нужно обслуживать
        /// </summary>
        public static readonly List<Log> LogsToCheck = new List<Log>();

        private static object _activatorLocker = new object();

        /// <summary>
        /// activate thread for saving
        /// активировать поток для сохранения
        /// </summary>
        public static void Activate()
        {
            lock (_activatorLocker)
            {
                if (_watcher != null)
                {
                    return;
                }

                _watcher = new Task(WatcherHome);
                _watcher.Start();
            }
        }

        /// <summary>
        /// work place of thread that save logs
        /// место работы потока который сохраняет логи
        /// </summary>
        public static async void WatcherHome()
        {
            while (true)
            {
                try
                {
                    await Task.Delay(2000);

                    for (int i = 0; i < Math.Min(LogsToCheck.Count, LogsToCheck.Capacity); i++)// не потокобезопасная работа с LogsToCheck приводит к Capacity<Count
                    {
                        if (LogsToCheck[i] == null)
                        {
                            continue;
                        }

                        LogsToCheck[i].TrySaveLog();
                        LogsToCheck[i].TryPaintLog();
                    }

                    if (!MainWindow.ProccesIsWorked)
                    {
                        return;
                    }
                }
                catch(Exception error)
                {
                    MessageBox.Show(error.ToString());
                }
            }
        }

        // object of log
        // объект лога

        private string _starterLocker = "logStarterLocker";

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="uniqName">log object name / имя объекта которому принадлежит лог</param>
        /// <param name="startProgram">program createing class / программа создавшая класс</param>
        public Log(string uniqName, StartProgram startProgram)
        {
            _uniqName = uniqName;
            _startProgram = startProgram;

            lock(_starterLocker)
            {
                if (_watcher == null)
                {
                    CreateErrorLogGreed();
                    Activate();
                }
            }

            if(_startProgram != StartProgram.IsOsOptimizer)
            {
                CreateGrid();
                AddToLogsToCheck(this);
            }

            if (_startProgram == StartProgram.IsOsTrader)
            {
                _messageSender = new MessageSender(uniqName, _startProgram);
            }
        }

        private void CreateGrid()
        {
            _grid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders);
            _grid.ScrollBars = ScrollBars.Vertical;
            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _grid.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = OsLocalization.Logging.Column1;
            column0.ReadOnly = true;
            column0.Width = 170;

            _grid.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = OsLocalization.Logging.Column2;
            column1.ReadOnly = true;
            column1.Width = 100;

            _grid.Columns.Add(column1);

            DataGridViewColumn column = new DataGridViewColumn();
            column.CellTemplate = cell0;
            column.HeaderText = OsLocalization.Logging.Column3;
            column.ReadOnly = true;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column);

            _grid.Rows.Add(null, null);
            _grid.DoubleClick += _grid_DoubleClick;
        }

        /// <summary>
        /// delete the object and clear all files associated with it
        /// удалить объект и очистить все файлы связанные с ним
        /// </summary>
        public void Delete()
        {          
            _isDelete = true;

            if(_startProgram != StartProgram.IsOsOptimizer)
            {
                DeleteFromLogsToCheck(this);

                string date = DateTime.Now.Year + "_" + DateTime.Now.Month + "_" + DateTime.Now.Day;

                if (File.Exists(@"Engine\Log\" + _uniqName + @"Log_" + date + ".txt"))
                {
                    File.Delete(@"Engine\Log\" + _uniqName + @"Log_" + date + ".txt");
                }
            }

            if(_grid != null)
            {
                _grid.DoubleClick -= _grid_DoubleClick;
                _grid.Rows.Clear();
                _grid.Columns.Clear();
                DataGridFactory.ClearLinks(_grid);
                _grid = null;
            }

            while(_messagesesToSaveInFile.IsEmpty == false)
            {
                LogMessage s;
                _messagesesToSaveInFile.TryDequeue(out s);
            }

            while (_incomingMessages.IsEmpty == false)
            {
                LogMessage s;
                _incomingMessages.TryDequeue(out s);
            }

            ServerMaster.LogMessageEvent -= ProcessMessage;

            for(int i = 0;i < _candleConverters.Count;i++)
            {
                _candleConverters[i].LogMessageEvent -= ProcessMessage;
            }

            for (int i = 0; i < _osConverterMasters.Count; i++)
            {
                _osConverterMasters[i].LogMessageEvent -= ProcessMessage;
            }

            for (int i = 0; i < _osTraderMasters.Count; i++)
            {
                _osTraderMasters[i].LogMessageEvent -= ProcessMessage;
            }

            for (int i = 0; i < _botPanels.Count; i++)
            {
                _botPanels[i].LogMessageEvent -= ProcessMessage;
            }

            for (int i = 0; i < _optimizerDataStoreges.Count; i++)
            {
                _optimizerDataStoreges[i].LogMessageEvent -= ProcessMessage;
            }

            for (int i = 0; i < _osMinerMasters.Count; i++)
            {
                _osMinerMasters[i].LogMessageEvent -= ProcessMessage;
            }

            for (int i = 0; i < _osDataMasters.Count; i++)
            {
                _osDataMasters[i].NewLogMessageEvent -= ProcessMessage;
            }

            for (int i = 0; i < _optimizers.Count; i++)
            {
                _optimizers[i].LogMessageEvent -= ProcessMessage;
            }

            for (int i = 0; i < _miners.Count; i++)
            {
                _miners[i].LogMessageEvent -= ProcessMessage;
            }

            for (int i = 0; i < _serversToListen.Count; i++)
            {
                _serversToListen[i].LogMessageEvent -= ProcessMessage;
            }

            _candleConverters.Clear();
            _osConverterMasters.Clear();
            _osTraderMasters.Clear();
            _botPanels.Clear();
            _optimizerDataStoreges.Clear();
            _osMinerMasters.Clear();
            _osDataMasters.Clear();
            _optimizers.Clear();
            _miners.Clear();
            _serversToListen.Clear();

            _candleConverters = null;
            _osConverterMasters = null;
            _osTraderMasters = null;
            _botPanels = null;
            _optimizerDataStoreges = null;
            _osMinerMasters = null;
            _osDataMasters = null;
            _optimizers = null;
            _miners = null;
            _serversToListen = null;
        }

        private static readonly object LogLocker = new object();
        
        private static void AddToLogsToCheck(Log log)
        {
            lock (LogLocker)
            {
                LogsToCheck.Add(log);
            }
        }       
        
        private static void DeleteFromLogsToCheck(Log log)
        {
            lock (LogLocker)
            {
                for (int i = 0; i < LogsToCheck.Count; i++)
                {
                    if (log._uniqName.Equals(LogsToCheck[i]?._uniqName))
                    {
                        LogsToCheck.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// clear the object
        /// очистить объект от данных и сообщений
        /// </summary>
        public void Clear()
        {
            if (_grid != null &&
                _grid.InvokeRequired)
            {
                _grid.Invoke(new Action(Clear));
                return;
            }

            try
            {
                if (_grid != null)
                {
                    _grid.Rows.Clear();
                }

                while (_messagesesToSaveInFile.IsEmpty == false)
                {
                    LogMessage s;
                    _messagesesToSaveInFile.TryDequeue(out s);
                }

                while (_incomingMessages.IsEmpty == false)
                {
                    LogMessage s;
                    _incomingMessages.TryDequeue(out s);
                }
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }
        }

        /// <summary>
        /// shows whether the object is destroyed
        /// уничтожен ли объект
        /// </summary>
        private bool _isDelete;

        /// <summary>
        /// name
        /// имя
        /// </summary>
        private string _uniqName;

        private StartProgram _startProgram;

        List<CandleConverter> _candleConverters = new List<CandleConverter>();
        List<OsConverterMaster> _osConverterMasters = new List<OsConverterMaster>();
        List<OsTraderMaster> _osTraderMasters = new List<OsTraderMaster>();
        List<BotPanel> _botPanels = new List<BotPanel>();
        List<OptimizerDataStorage> _optimizerDataStoreges = new List<OptimizerDataStorage>();
        List<OsMinerMaster> _osMinerMasters = new List<OsMinerMaster>();
        List<OsDataMaster> _osDataMasters = new List<OsDataMaster>();
        List<OptimizerMaster> _optimizers = new List<OptimizerMaster>();
        List<OsMinerServer> _miners = new List<OsMinerServer>();
        List<IServer> _serversToListen = new List<IServer>();

        /// <summary>
        /// start listening to the server
        /// начать прослушку сервера
        /// </summary>
        /// <param name="server">сервер</param>
        public void Listen(IServer server)
        {
            server.LogMessageEvent += ProcessMessage;
            _serversToListen.Add(server);
        }

        /// <summary>
        /// start listening to the Server Miner
        /// начать прослушку сервера майнера
        /// </summary>
        public void Listen(OsMinerServer miner)
        {
            miner.LogMessageEvent += ProcessMessage;
            _miners.Add(miner);
        }

        /// <summary>
        /// start listening to the Optimizer
        /// начать прослушку оптимизатора
        /// </summary>
        /// <param name="optimizer">оптимизатор</param>
        public void Listen(OptimizerMaster optimizer)
        {
            optimizer.LogMessageEvent += ProcessMessage;
            _optimizers.Add(optimizer);
        }

        /// <summary>
        /// start listening to the OsData
        /// начать прослушку OsData
        /// </summary>
        /// <param name="master"></param>
        public void Listen(OsDataMaster master)
        {
            master.NewLogMessageEvent += ProcessMessage;
            _osDataMasters.Add(master);
        }

        /// <summary>
        /// start listening to the OsData
        /// начать прослушку OsData
        /// </summary>
        /// <param name="master"></param>
        public void Listen(OsMinerMaster master)
        {
            master.LogMessageEvent += ProcessMessage;
            _osMinerMasters.Add(master);
        }

        /// <summary>
        /// start listening to the Optimizer storage
        /// начать прослушку хранилища оптимизатора
        /// </summary>
        public void Listen(OptimizerDataStorage storage)
        {
            storage.LogMessageEvent += ProcessMessage;
            _optimizerDataStoreges.Add(storage);
        }

        /// <summary>
        /// start listening to the bot panel
        /// начать прослушку панели робота
        /// </summary>
        public void Listen(BotPanel panel)
        {
            panel.LogMessageEvent += ProcessMessage;
            _botPanels.Add(panel);
        }

        /// <summary>
        /// start listening to the bot storage
        /// начать прослушку хранилища роботов
        /// </summary>
        public void Listen(OsTraderMaster master)
        {
            master.LogMessageEvent += ProcessMessage;
            _osTraderMasters.Add(master);
        }

        /// <summary>
        /// start listening to the bot storage
        /// начать прослушку хранилища роботов
        /// </summary>
        public void Listen(OsConverterMaster master)
        {
            master.LogMessageEvent += ProcessMessage;
            _osConverterMasters.Add(master);
        }

        public void Listen(CandleConverter master)
        {
            master.LogMessageEvent += ProcessMessage;
            _candleConverters.Add(master);
        }

        bool _listenServerMasterAlreadyOn;

        /// <summary>
        /// start listening to the router
        /// начать прослушку роутера
        /// </summary>
        public void ListenServerMaster()
        {
            if(_listenServerMasterAlreadyOn == true)
            {
                return;
            }
            ServerMaster.LogMessageEvent += ProcessMessage;
            _listenServerMasterAlreadyOn = true;
        }

        /// <summary>
        /// table with records
        /// таблица с записями
        /// </summary>
        private DataGridView _grid;

        /// <summary>
        /// host to get log table
        /// хост для отрисовки таблицы лога
        /// </summary>
        private WindowsFormsHost _host;

        private ConcurrentQueue<LogMessage> _incomingMessages = new ConcurrentQueue<LogMessage>();

        /// <summary>
        /// incoming message
        /// входящее сообщение
        /// </summary>
        /// <param name="message">message / сообщение</param>
        /// <param name="type">message type / тип сообщения</param>
        private void ProcessMessage(string message, LogMessageType type)
        {
            if (_isDelete)
            {
                return;
            }

            if(_startProgram != StartProgram.IsOsOptimizer)
            {
                LogMessage messageLog = new LogMessage { Message = message, Time = DateTime.Now, Type = type };
                _incomingMessages.Enqueue(messageLog);

                if(_incomingMessages.Count > 500)
                {
                    LogMessage mes;
                    _incomingMessages.TryDequeue(out mes);
                }

                if (_messageSender != null)
                {
                    _messageSender.AddNewMessage(messageLog);
                }
            }
            if(type == LogMessageType.Error)
            {
                LogMessage messageLog = new LogMessage { Message = message, Time = DateTime.Now, Type = type };
                SetNewErrorMessage(messageLog);
            }
        }

        private void TryPaintLog()
        {
            if (!_incomingMessages.IsEmpty)
            {
                List<LogMessage> elements = new List<LogMessage>();

                while (!_incomingMessages.IsEmpty)
                {
                    LogMessage newElement;
                    _incomingMessages.TryDequeue(out newElement);

                    if (newElement != null)
                    {
                        elements.Add(newElement);
                    }
                }

                for (int i = 0; i < elements.Count; i++)
                {
                    PaintMessage(elements[i]);
                }
            }
        }

        private void PaintMessage(LogMessage messageLog)
        {
            try
            {
                if(_isDelete == true)
                {
                    return;
                }

                if (_grid != null 
                    && _grid.InvokeRequired)
                {
                    _grid.Invoke(new Action<LogMessage>(PaintMessage), messageLog);
                    return;
                }

                _messagesesToSaveInFile.Enqueue(messageLog);


                if(_grid != null)
                {
                    DataGridViewRow row = new DataGridViewRow();
                    row.Cells.Add(new DataGridViewTextBoxCell());
                    row.Cells[0].Value = messageLog.Time;

                    row.Cells.Add(new DataGridViewTextBoxCell());
                    row.Cells[1].Value = messageLog.Type;

                    row.Cells.Add(new DataGridViewTextBoxCell());
                    row.Cells[2].Value = messageLog.Message;

                    if(_grid.Columns.Count != 0)
                    {
                        _grid.Rows.Insert(0, row);
                    }
                }
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }
        }

        // saving messages 

        /// <summary>
        /// all log messages
        /// все сообщения лога
        /// </summary>
        private ConcurrentQueue<LogMessage> _messagesesToSaveInFile = new ConcurrentQueue<LogMessage>();

        /// <summary>
        /// method for working saving log thread when the application starts closing
        /// метод в котором работает поток который сохранит лог когда приложение начнёт закрываться
        /// </summary>
        public void TrySaveLog()
        {
            if(_startProgram == StartProgram.IsOsOptimizer)
            {
                return;
            }
            if (!Directory.Exists(@"Engine\Log\"))
            {
                Directory.CreateDirectory(@"Engine\Log\");
            }

            try
            {
                if (_messagesesToSaveInFile == null ||
                    _messagesesToSaveInFile.IsEmpty)
                {
                    return;
                }

                string date = DateTime.Now.Year + "_" + DateTime.Now.Month + "_" + DateTime.Now.Day;
                string path = @"Engine\Log\" + _uniqName + @"Log_" + date + ".txt";

                using (StreamWriter writer = new StreamWriter(
                        path,true))
                {
                    while (_messagesesToSaveInFile.IsEmpty == false)
                    {
                        LogMessage message;

                        if(_messagesesToSaveInFile.TryDequeue(out message))
                        {
                            writer.Write(message.Message);
                        }
                    }
                }

            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }

        }

        // distribution

        /// <summary>
        /// object for distribution massages
        /// объект рассылающий сообщения
        /// </summary>
        private MessageSender _messageSender;

        /// <summary>
        /// user clicked on the log window twice 
        /// пользователь дважды нажал на окно лога
        /// </summary>
        void _grid_DoubleClick(object sender, EventArgs e)
        {
            if(_messageSender != null)
            {
                _messageSender.ShowDialog();
            }
        }

        // drawing

        /// <summary>
        /// start drawing the object
        /// начать прорисовку объекта
        /// </summary>
        public void StartPaint(WindowsFormsHost host)
        {
            _host = host;
            if (!_host.CheckAccess())
            {
                _host.Dispatcher.Invoke(new Action<WindowsFormsHost>(StartPaint), host);
                return;
            }

            _host.Child = _grid;
        }

        /// <summary>
        /// finish drawing the object
        /// остановить прорисовку объекта
        /// </summary>
        public void StopPaint()
        {
            if (_host == null)
            {
                return;
            }
            if (!_host.CheckAccess())
            {
                _host.Dispatcher.Invoke(StopPaint);
                return;
            }
            if (_host != null)
            {
                _host.Child = null;
                _host = null;
            }
        }

        // general error log 

        /// <summary>
        /// table for drawing error messages
        /// таблица для прорисовки сообщений с ошибками
        /// </summary>
        private static DataGridView _gridErrorLog;

        /// <summary>
        /// window with error log
        /// окно лога с ошибками
        /// </summary>
        private static LogErrorUi _logErrorUi;

        /// <summary>
        /// create the table for errors
        /// создать таблицу для ошибок
        /// </summary>
        private static void CreateErrorLogGreed()
        {
            if (_gridErrorLog != null)
            {
                return;
            }

            _gridErrorLog = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
                DataGridViewAutoSizeRowsMode.AllCells);

            DataGridViewCellStyle style = new DataGridViewCellStyle();
            style.Alignment = DataGridViewContentAlignment.TopLeft;
            style.WrapMode = DataGridViewTriState.True;
            _gridErrorLog.DefaultCellStyle = style;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = style;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = OsLocalization.Logging.Column1;
            column0.ReadOnly = true;
            column0.Width = 170;

            _gridErrorLog.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = OsLocalization.Logging.Column2;
            column1.ReadOnly = true;
            column1.Width = 100;

            _gridErrorLog.Columns.Add(column1);

            DataGridViewColumn column = new DataGridViewColumn();
            column.CellTemplate = cell0;
            column.HeaderText = OsLocalization.Logging.Column3;
            column.ReadOnly = true;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridErrorLog.Columns.Add(column);
        }

        /// <summary>
        /// send new error message
        /// выслать новое сообщение об ошибке
        /// </summary>
        private static void SetNewErrorMessage(LogMessage message)
        {
            if (!MainWindow.GetDispatcher.CheckAccess())
            {
                MainWindow.GetDispatcher.Invoke(new Action<LogMessage>(SetNewErrorMessage), message);
                return;
            }

            if(_gridErrorLog.Rows.Count == 500)
            {
                DataGridViewRow row1 = new DataGridViewRow();
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[0].Value = DateTime.Now;

                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[1].Value = LogMessageType.Error;

                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[2].Value = "To much ERRORS. Error log shut down.";
                _gridErrorLog.Rows.Insert(0, row1);
                return;
            }
            else if(_gridErrorLog.Rows.Count > 500)
            {
                return;
            }


            DataGridViewRow row = new DataGridViewRow();
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[0].Value = DateTime.Now;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[1].Value = LogMessageType.Error;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[2].Value = message.Message;
            _gridErrorLog.Rows.Insert(0, row);

            if (PrimeSettingsMaster.ErrorLogMessageBoxIsActiv)
            {
                if (_logErrorUi == null)
                {
                    _logErrorUi = new LogErrorUi(_gridErrorLog);
                    _logErrorUi.Closing += delegate (object sender, CancelEventArgs args)
                    {
                        _logErrorUi = null;
                    };
                    _logErrorUi.Show();
                }
            }

            if (PrimeSettingsMaster.ErrorLogBeepIsActiv)
            {
                SystemSounds.Beep.Play();
            }
        }
    }

    /// <summary>
    /// log message
    /// сообщение для лога
    /// </summary>
    public class LogMessage
    {
        /// <summary>
        /// message time
        /// время сообщения
        /// </summary>
        public DateTime Time;

        /// <summary>
        /// message type
        /// тип сообщения
        /// </summary>
        public LogMessageType Type;

        /// <summary>
        /// message
        /// сообщение
        /// </summary>
        public string Message;

        /// <summary>
        /// take a string representation of object
        /// взять строковое представление объекта
        /// </summary>
        public string GetString()
        {
            string result = Time + "_";
            result += Type + "_";
            result += Message;

            return result;
        }

    }

    /// <summary>
    /// log message type
    /// тип сообщения для лога
    /// </summary>
    public enum LogMessageType
    {
        /// <summary>
        /// systemic message
        /// Системное сообщение
        /// </summary>
        System,

        /// <summary>
        /// Bot got a signal from one of strategies 
        /// Робот получил сигнал из одной из стратегий
        /// </summary>
        Signal,

        /// <summary>
        /// error happened
        /// Случилась ошибка
        /// </summary>
        Error,

        /// <summary>
        /// connect or disconnect message
        /// Сообщение о установке или обрыве соединения
        /// </summary>
        Connect,

        /// <summary>
        /// transaction message
        /// Сообщение об исполнении транзакции
        /// </summary>
        Trade,

        /// <summary>
        /// message without specification
        /// Сообщение без спецификации
        /// </summary>
        NoName,

        /// <summary>
        /// user action recorded
        /// Зафиксировано действие пользователя
        /// </summary>
        User
    }
}