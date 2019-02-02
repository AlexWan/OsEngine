/*
*Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Media;
using System.Threading;
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
    /// класс для логирования сообщений программы
    /// </summary>
    public class Log
    {

        // статическая часть с работой потока сохраняющего логи

        /// <summary>
        /// поток 
        /// </summary>
        public static Thread Watcher;

        /// <summary>
        /// логи которые нужно обслуживать
        /// </summary>
        public static List<Log> LogsToCheck = new List<Log>();

        private static object _activatorLocker = new object();

        /// <summary>
        /// активировать поток для сохранения
        /// </summary>
        public static void Activate()
        {
            lock (_activatorLocker)
            {
                if (Watcher != null)
                {
                    return;
                }

                Watcher = new Thread(WatcherHome);
                Watcher.Name = "LogSaveThread";
                Watcher.IsBackground = true;
                Watcher.Start();
            }
        }

        /// <summary>
        /// место работы потока который сохраняет логи
        /// </summary>
        public static void WatcherHome()
        {
            while (true)
            {
                Thread.Sleep(2000);

                for (int i = 0; i < LogsToCheck.Count; i++)
                {
                    LogsToCheck[i].TrySaveLog();
                    LogsToCheck[i].TryPaintLog();
                }

                if (!MainWindow.ProccesIsWorked)
                {
                    return;
                }
            }
        }

        // объект лога

        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="uniqName">имя объекта которому принадлежит лог</param>
        /// <param name="startProgram">программа создавшая класс</param>
        public Log(string uniqName, StartProgram startProgram)
        {
            _uniqName = uniqName;
            _startProgram = startProgram;

            if (Watcher == null)
            {
                Activate();
            }

            LogsToCheck.Add(this);

            _grid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders);

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

            _messageSender = new MessageSender(uniqName,_startProgram);

            CreateErrorLogGreed();
        }

        /// <summary>
        /// удалить объект и очистить все файлы связанные с ним
        /// </summary>
        public void Delete()
        {
            for (int i = 0; i < LogsToCheck.Count; i++)
            {
                if (LogsToCheck[i]._uniqName == this._uniqName)
                {
                    LogsToCheck.RemoveAt(i);
                    break;
                }
            }
            _isDelete = true;

            string date = DateTime.Now.Year + "_" + DateTime.Now.Month + "_" + DateTime.Now.Day;

            if (File.Exists(@"Engine\Log\" + _uniqName + @"Log_" + date + ".txt"))
            {
                File.Delete(@"Engine\Log\" + _uniqName + @"Log_" + date + ".txt");
            }

            _grid = null;

            _messageses = null;
        }

        /// <summary>
        /// уничтожен ли объект
        /// </summary>
        private bool _isDelete;

        /// <summary>
        /// имя
        /// </summary>
        private string _uniqName;

        private StartProgram _startProgram;

        /// <summary>
        /// начать прослушку сервера
        /// </summary>
        /// <param name="server">сервер</param>
        public void Listen(IServer server)
        {
            server.LogMessageEvent += ProcessMessage;
        }

        /// <summary>
        /// начать прослушку сервера майнера
        /// </summary>
        public void Listen(OsMinerServer server)
        {
            server.LogMessageEvent += ProcessMessage;
        }

        /// <summary>
        /// начать прослушку оптимизатора
        /// </summary>
        /// <param name="optimizer">оптимизатор</param>
        public void Listen(OptimizerMaster optimizer)
        {
            optimizer.LogMessageEvent += ProcessMessage;
        }

        /// <summary>
        /// начать прослушку OsData
        /// </summary>
        /// <param name="master"></param>
        public void Listen(OsDataMaster master)
        {
            master.NewLogMessageEvent += ProcessMessage;
        }

        /// <summary>
        /// начать прослушку OsData
        /// </summary>
        /// <param name="master"></param>
        public void Listen(OsMinerMaster master)
        {
            master.LogMessageEvent += ProcessMessage;
        }


        /// <summary>
        /// начать прослушку хранилища оптимизатора
        /// </summary>
        public void Listen(OptimizerDataStorage panel)
        {
            panel.LogMessageEvent += ProcessMessage;
        }

        /// <summary>
        /// начать прослушку панели робота
        /// </summary>
        public void Listen(BotPanel panel)
        {
            panel.LogMessageEvent += ProcessMessage;
        }

        /// <summary>
        /// начать прослушку хранилища роботов
        /// </summary>
        public void Listen(OsTraderMaster master)
        {
            master.LogMessageEvent += ProcessMessage;
        }

        /// <summary>
        /// начать прослушку хранилища роботов
        /// </summary>
        public void Listen(OsConverterMaster master)
        {
            master.LogMessageEvent += ProcessMessage;
        }

        /// <summary>
        /// начать прослушку роутера
        /// </summary>
        public void ListenServerMaster()
        {
            ServerMaster.LogMessageEvent += ProcessMessage;
        }

        /// <summary>
        /// таблица с записями
        /// </summary>
        private DataGridView _grid;

        /// <summary>
        /// хост для отрисовки таблицы лога
        /// </summary>
        private WindowsFormsHost _host;

        private ConcurrentQueue<LogMessage> _incomingMessages = new ConcurrentQueue<LogMessage>();

        /// <summary>
        /// входящее сообщение
        /// </summary>
        /// <param name="message">сообщение</param>
        /// <param name="type">тип сообщения</param>
        private void ProcessMessage(string message, LogMessageType type)
        {
            if (_isDelete)
            {
                return;
            }

            LogMessage messageLog = new LogMessage { Message = message, Time = DateTime.Now, Type = type };
            _incomingMessages.Enqueue(messageLog);
            _messageSender.AddNewMessage(messageLog);

            if (messageLog.Type == LogMessageType.Error)
            {
                SetNewErrorMessage(messageLog);
            }
        }

        private void PaintMessage(LogMessage messageLog)
        {
            try
            {
                if (_grid.InvokeRequired)
                {
                    _grid.Invoke(new Action<LogMessage>(PaintMessage), messageLog);
                    return;
                }

                if (_messageses == null)
                {
                    _messageses = new List<LogMessage>();
                }

                _messageses.Add(messageLog);

                

                DataGridViewRow row = new DataGridViewRow();
                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[0].Value = messageLog.Time;

                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[1].Value = messageLog.Type;

                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[2].Value = messageLog.Message;
                _grid.Rows.Insert(0, row);

            }
            catch (Exception)
            {
                // ignore
            }
        }

        private void TryPaintLog()
        {
            if (_host != null && !_incomingMessages.IsEmpty)
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

        // сохранение сообщений      

        /// <summary>
        /// все сообщения лога
        /// </summary>
        private List<LogMessage> _messageses;

        private int _lastAreaCount;

        /// <summary>
        /// метод в котором работает поток который сохранит
        /// лог когда приложение начнёт закрыаться
        /// </summary>
        public void TrySaveLog()
        {
            if (!Directory.Exists(@"Engine\Log\"))
            {
                Directory.CreateDirectory(@"Engine\Log\");
            }

            try
            {
                if (_messageses == null ||
                    _lastAreaCount == _messageses.Count)
                {
                    return;
                }

                string date = DateTime.Now.Year + "_" + DateTime.Now.Month + "_" + DateTime.Now.Day;

                using (
                    StreamWriter writer = new StreamWriter(
                        @"Engine\Log\" + _uniqName + @"Log_" + date + ".txt", true))
                {
                    string str = "";
                    for (int i = _lastAreaCount; _messageses != null && i < _messageses.Count; i++)
                    {
                        str += _messageses[i].GetString() + "\r\n";
                    }
                    writer.Write(str);
                }
                _lastAreaCount = _messageses.Count;
            }
            catch (Exception)
            {
                // ignore
            }

        }

        // рассылка

        /// <summary>
        /// объект рассылающий сообщения
        /// </summary>
        private MessageSender _messageSender;

        /// <summary>
        /// пользователь дважды нажал на окно лога
        /// </summary>
        void _grid_DoubleClick(object sender, EventArgs e)
        {
            _messageSender.ShowDialog();
        }

        // прорисовка

        /// <summary>
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


        // общий лог для ошибок

        /// <summary>
        /// таблица для прорисовки сообщений с ошибками
        /// </summary>
        private static DataGridView _gridErrorLog;

        /// <summary>
        /// окно лога с ошибками
        /// </summary>
        private static LogErrorUi _logErrorUi;

        /// <summary>
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
        /// выслать новое сообщение об ошибке
        /// </summary>
        private static void SetNewErrorMessage(LogMessage message)
        {
            if (!MainWindow.GetDispatcher.CheckAccess())
            {
                MainWindow.GetDispatcher.Invoke(new Action<LogMessage>(SetNewErrorMessage), message);
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
    /// сообщение для лога
    /// </summary>
    public class LogMessage
    {
        /// <summary>
        /// время сообщения
        /// </summary>
        public DateTime Time;

        /// <summary>
        /// тип сообщения
        /// </summary>
        public LogMessageType Type;

        /// <summary>
        /// сообщение
        /// </summary>
        public string Message;

        /// <summary>
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
    /// тип сообщения для лога
    /// </summary>
    public enum LogMessageType
    {
        /// <summary>
        /// Системное сообщение
        /// </summary>
        System,

        /// <summary>
        /// Робот получил сигнал из одной из стратегий
        /// </summary>
        Signal,

        /// <summary>
        /// Случилась ошибка
        /// </summary>
        Error,

        /// <summary>
        /// Сообщение о установке или обрыве соединения
        /// </summary>
        Connect,

        /// <summary>
        /// Сообщение об исполнении транзакции
        /// </summary>
        Trade,

        /// <summary>
        /// Сообщение без спецификации
        /// </summary>
        NoName,

        /// <summary>
        /// Зафиксировано действие пользователя
        /// </summary>
        User
    }
}