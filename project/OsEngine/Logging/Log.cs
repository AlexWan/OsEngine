﻿/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Media;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using OsEngine.Market.Servers;
using OsEngine.OsConverter;
using OsEngine.OsData;
using OsEngine.OsTrader;
using OsEngine.OsTrader.Panels;

namespace OsEngine.Logging
{
    /// <summary>
    /// класс для логирования сообщений программы
    /// </summary>
    public class Log
    {
        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="uniqName">имя объекта которому принадлежит лог</param>
        public Log(string uniqName)
        {
            _uniqName = uniqName;

            Thread saver = new Thread(SaverArea);
            saver.IsBackground = true;
            saver.CurrentCulture = new CultureInfo("RU-ru");
            saver.Start();

            _grid = new DataGridView();

            _grid.AllowUserToOrderColumns = true;
            _grid.AllowUserToResizeRows = true;
            _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToAddRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;

            DataGridViewCellStyle style = new DataGridViewCellStyle();
            style.Alignment = DataGridViewContentAlignment.TopLeft;
            style.WrapMode = DataGridViewTriState.True;
            _grid.DefaultCellStyle = style;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = style;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = @"Время";
            column0.ReadOnly = true;
            column0.Width = 170;

            _grid.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = @"Тип";
            column1.ReadOnly = true;
            column1.Width = 100;

            _grid.Columns.Add(column1);

            DataGridViewColumn column = new DataGridViewColumn();
            column.CellTemplate = cell0;
            column.HeaderText = @"Сообщение";
            column.ReadOnly = true;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column);

            _grid.Rows.Add(null, null);
            _grid.DoubleClick += _grid_DoubleClick;

            _messageSender = new MessageSender(uniqName);

            CreateErrorLogGreed();
        }

        /// <summary>
        /// имя
        /// </summary>
        private string _uniqName;

        /// <summary>
        /// начать прослушку сервера
        /// </summary>
        /// <param name="server">сервер</param>
        public void Listen(IServer server)
        {
            server.LogMessageEvent += LogMessageEvent;
        }

        /// <summary>
        /// начать прослушку OsData
        /// </summary>
        /// <param name="master"></param>
        public void Listen(OsDataMaster master)
        {
            master.NewLogMessageEvent += LogMessageEvent;
        }

        /// <summary>
        /// начать прослушку панели робота
        /// </summary>
        public void Listen(BotPanel panel)
        {
            panel.LogMessageEvent += LogMessageEvent;
        }

        /// <summary>
        /// начать прослушку хранилища роботов
        /// </summary>
        public void Listen(OsTraderMaster master)
        {
            master.LogMessageEvent += LogMessageEvent;
        }

        /// <summary>
        /// начать прослушку хранилища роботов
        /// </summary>
        public void Listen(OsConverterMaster master)
        {
            master.LogMessageEvent += LogMessageEvent;
        }

        /// <summary>
        /// таблица с записями
        /// </summary>
        private DataGridView _grid;

        /// <summary>
        /// хост для отрисовки таблицы лога
        /// </summary>
        private WindowsFormsHost _host;

        /// <summary>
        /// входящее сообщение
        /// </summary>
        /// <param name="message">сообщение</param>
        /// <param name="type">тип сообщения</param>
        void LogMessageEvent(string message, LogMessageType type)
        {
            try
            {
                if (_grid.InvokeRequired)
                {
                    _grid.Invoke(new Action<string, LogMessageType>(LogMessageEvent), message, type);
                    return;
                }

                if (_messageses == null)
                {
                    _messageses = new List<LogMessage>();
                }
                LogMessage messageLog = new LogMessage {Message = message, Time = DateTime.Now, Type = type};
                _messageses.Add(messageLog);

                DataGridViewRow row = new DataGridViewRow();
                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[0].Value = messageLog.Time;

                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[1].Value = messageLog.Type;

                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[2].Value = messageLog.Message;
                _grid.Rows.Insert(0,row);

                _messageSender.AddNewMessage(messageLog);

                if (type == LogMessageType.Error)
                {
                    SetNewErrorMessage(message);
                }

            }
            catch (Exception)
            {
                 // ignore
            }
        }

 // сохранение сообщений      

        /// <summary>
        /// все сообщения лога
        /// </summary>
        private List<LogMessage> _messageses;

        private int _lastAreaCount = 0;

        /// <summary>
        /// метод в котором работает поток который сохранит
        /// лог когда приложение начнёт закрыаться
        /// </summary>
        public void SaverArea()
        {
            if (!Directory.Exists(@"Engine\Log\"))
            {
                Directory.CreateDirectory(@"Engine\Log\");
            }

            while (true)
            {
                Thread.Sleep(1000);
                if (MainWindow.ProccesIsWorked == true)
                {
                    try
                    {
                        if (_messageses == null ||
                            _lastAreaCount == _messageses.Count)
                        {
                            continue;
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
                else
                {
                    return;
                }
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
                _host.Dispatcher.Invoke(new Action<WindowsFormsHost>(StartPaint),host);
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
            _gridErrorLog = new DataGridView();

            _gridErrorLog = new DataGridView();

            _gridErrorLog.AllowUserToOrderColumns = true;
            _gridErrorLog.AllowUserToResizeRows = true;
            _gridErrorLog.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            _gridErrorLog.AllowUserToDeleteRows = false;
            _gridErrorLog.AllowUserToAddRows = false;
            _gridErrorLog.RowHeadersVisible = false;
            _gridErrorLog.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _gridErrorLog.MultiSelect = false;

            DataGridViewCellStyle style = new DataGridViewCellStyle();
            style.Alignment = DataGridViewContentAlignment.TopLeft;
            style.WrapMode = DataGridViewTriState.True;
            _gridErrorLog.DefaultCellStyle = style;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = style;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = @"Время";
            column0.ReadOnly = true;
            column0.Width = 170;

            _gridErrorLog.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = @"Тип";
            column1.ReadOnly = true;
            column1.Width = 100;

            _gridErrorLog.Columns.Add(column1);

            DataGridViewColumn column = new DataGridViewColumn();
            column.CellTemplate = cell0;
            column.HeaderText = @"Сообщение";
            column.ReadOnly = true;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridErrorLog.Columns.Add(column);

        }

        /// <summary>
        /// выслать новое сообщение об ошибке
        /// </summary>
        private static void SetNewErrorMessage(string message)
        {
            if (_gridErrorLog.InvokeRequired)
            {
                _gridErrorLog.Invoke(new Action<string>(SetNewErrorMessage), message);
                return;
            }

            DataGridViewRow row = new DataGridViewRow();
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[0].Value = DateTime.Now;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[1].Value = LogMessageType.Error;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[2].Value = message;
            _gridErrorLog.Rows.Insert(0, row);

            if (_logErrorUi == null)
            {
                _logErrorUi = new LogErrorUi(_gridErrorLog);
                _logErrorUi.Closing += _logErrorUi_Closing;
                _logErrorUi.Show();
            }

            SystemSounds.Beep.Play();
        }

        /// <summary>
        /// окно лога закрылось
        /// </summary>
        static void _logErrorUi_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _logErrorUi = null;
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
