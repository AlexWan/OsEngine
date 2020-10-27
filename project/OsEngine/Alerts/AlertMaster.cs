/*
 *Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Forms.Integration;
using OsEngine.Charts.CandleChart;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Connectors;
using MessageBox = System.Windows.MessageBox;
using MouseEventArgs = System.Windows.Forms.MouseEventArgs;

namespace OsEngine.Alerts
{
    /// <summary>
    /// Aletrs
    /// Хранилище Алертов
    /// </summary>
    public class AlertMaster
    {

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="name">name of owner of alerts repository/имя владельца хранилища алертов</param>
        /// <param name="connector">connector/коннектор</param>
        /// <param name="chartMaster">chart/чарт</param>
        public AlertMaster(string name, ConnectorCandles connector, ChartCandleMaster chartMaster) 
        {
            _name = name;
            _connector = connector;
            _chartMaster = chartMaster;
            chartMaster.ChartClickEvent += ChartMaster_ChartClickEvent;
            Load();

            CreateGrid();
            PaintGridBox();

            OsLocalization.LocalizationTypeChangeEvent += delegate 
            {
                CreateGrid();
                PaintGridBox();
            };
        }

        private void CreateGrid()
        {
            GridViewBox = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.AllCells);

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = GridViewBox.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = OsLocalization.Alerts.GridHeader0;
            column0.ReadOnly = true;
            column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            //column0.Width = 150;

            GridViewBox.Columns.Add(column0);

            DataGridViewColumn column = new DataGridViewColumn();
            column.CellTemplate = cell0;
            column.HeaderText = OsLocalization.Alerts.GridHeader1;
            column.ReadOnly = true;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            // column.Width = 150;
            GridViewBox.Columns.Add(column);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            column1.ReadOnly = true;
            // column1.Width = 150;
            column1.HeaderText = OsLocalization.Alerts.GridHeader2;
            GridViewBox.Columns.Add(column1);

            GridViewBox.Rows.Add(null, null);
            GridViewBox.Click += GridViewBox_Click;
            GridViewBox.DoubleClick += GridViewBox_DoubleClick;
        }

        /// <summary>
        /// storage name
        /// имя хранилища
        /// </summary>
        private readonly string _name;

        /// <summary>
        /// alerts array
        /// массив Алертов
        /// </summary>
        private List<IIAlert> _alertArray; 

        /// <summary>
        /// connector
        /// коннектор
        /// </summary>
        private ConnectorCandles _connector;

        /// <summary>
        /// graphical master
        /// мастер прорисовки графика
        /// </summary>
        private ChartCandleMaster _chartMaster;

        /// <summary>
        /// spreadsheet host
        /// хост для прорисовки таблицы
        /// </summary>
        public WindowsFormsHost HostAllert;

        /// <summary>
        /// alert table
        /// таблица алертов
        /// </summary>
        public DataGridView GridViewBox;

        /// <summary>
        /// load settings from file
        /// загрузить настройки из файла
        /// </summary>
        private void Load() 
        {
            if (!File.Exists(@"Engine\" + _name + "AlertKeeper.txt"))
            {
                // if there is no file we need. Just go out
                // если нет нужного нам файла. Просто выходим
                return;
            }
            try
            {
                _alertArray = new List<IIAlert>();

                using (StreamReader reader = new StreamReader(@"Engine\" + _name + "AlertKeeper.txt"))
                {
                    // if there is file. Connect to it and download data
                    // если файл есть. Подключаемся к нему и качаем данные
                    // indicators
                    // индикаторы
                    while (!reader.EndOfStream)
                    {
                        string saveStr = reader.ReadLine();

                        if (string.IsNullOrWhiteSpace(saveStr))
                        {
                            break;
                        }

                        string[] alert = saveStr.Split('$');

                        if (alert.Length != 2)
                        {
                            break;
                        }

                        AlertType alertType;

                        Enum.TryParse(alert[0], out alertType);

                        if (alertType == AlertType.ChartAlert)
                        {
                            _alertArray.Add(new AlertToChart(saveStr, HostAllert));
                        }
                        else if (alertType == AlertType.PriceAlert)
                        {
                            _alertArray.Add(new AlertToPrice(saveStr));
                        }
                    }
                }
                // create array and robots
                // создаём массив и роботов

            }
            catch (Exception error)
            {
                SendNewMessage(error.ToString(),LogMessageType.Error);
            }
        }

        /// <summary>
        /// save settings to file
        /// сохранить настройки в файл
        /// </summary>
        private void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + _name + "AlertKeeper.txt", false))
                {
                    // create file and write settings data to it
                    // создаём файл и записываем в него данные настроек

                    for (int i = 0; _alertArray != null && i < _alertArray.Count; i++)
                    {
                        _alertArray[i].Name = _alertArray[i].TypeAlert + "$" + _name + i;
                        _alertArray[i].Save();
                        writer.WriteLine(_alertArray[i].Name);

                    }

                    writer.Close();
                }
            }
            catch (Exception error)
            {
                SendNewMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// send new log message to top
        /// выслать наверх новое сообщение для лога
        /// </summary>
        /// <param name="message">message/сообщение</param>
        /// <param name="type">message type/тип сообщения</param>
        private void SendNewMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            {
                // if no one's subscribed to us and there's mistake
                // если никто на нас не подписан и происходит ошибка
                MessageBox.Show(message);
            }
        }

        /// <summary>
        /// outgoing message for log
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        /// <summary>
        /// purge everything
        /// удалить всё
        /// </summary>
        public void DeleteAll()
        {
            try
            {
                if (File.Exists(@"Engine\" + _name + "AlertKeeper.txt"))
                {
                    File.Delete(@"Engine\" + _name + "AlertKeeper.txt");
                }

                if (_alertArray == null || _alertArray.Count == 0)
                {
                    return;
                }

                for (int i = 0; _alertArray != null &&
                                i < _alertArray.Count; i++)
                {
                    _alertArray[i].Delete();
                }
                _alertArray = new List<IIAlert>();

                _connector = null;
                _chartMaster = null;
            }
            catch (Exception error)
            {
                SendNewMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// delete alert by number
        /// удалить алерт по номеру
        /// </summary>
        /// <param name="number">alert number/номер алерта</param>
        public void DeleteFromNumber(int number) 
        {
            try
            {
                if (_alertArray == null ||
                number > _alertArray.Count
                || _alertArray.Count == 0)
                {
                    return;
                }

                IIAlert activAlert = _alertArray[number];

                activAlert.Delete();
                // 2 delete
                // 2 удаляем

                _alertArray.Remove(activAlert);

                Save();

                Paint();
            }
            catch (Exception error)
            {
                SendNewMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// delete alert
        ///  удалить Алерт
        /// </summary>
        /// <param name="alert">alert/алерт</param>
        public void Delete(IIAlert alert)
        {
            try
            {
                if (_alertArray == null ||
                alert == null || _alertArray.Count == 0)
                {
                    return;
                }

                alert.Delete();

                _alertArray.Remove(alert);

                Save();

                Paint();
            }
            catch (Exception error)
            {
                SendNewMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// save in storage new Alert
        /// сохранить в хранилище новый Алерт
        /// </summary>
        /// <param name="newAlert">new alert/новый алерт</param>
        public void SetNewAlert(IIAlert newAlert) 
        {
            try
            {
                if (newAlert == null)
                {
                    return;
                }

                //DeleteAll();

                if (_alertArray == null)
                {
                    _alertArray = new List<IIAlert>();
                }

                _alertArray.Add(newAlert);

                Save();

                Paint();
            }
            catch (Exception error)
            {
                SendNewMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// open window to create Alert
        /// открытое окно создания Алерта
        /// </summary>
        private AlertToChartCreateUi _alertChartUi;

        /// <summary>
        /// create new Alert
        /// вызвать создание нового Алерта
        /// </summary>
        public void ShowAlertNewDialog(AlertType type)
        {
            try
            {
                if (type == AlertType.ChartAlert)
                {
                    if (_alertChartUi != null)
                    {
                        MessageBox.Show(OsLocalization.Alerts.Message1);
                        return;
                    }

                    _alertChartUi = new AlertToChartCreateUi(null, this);

                    if (_alertChartUi != null)
                    {
                        _alertChartUi.Closing += _ChartAertUi_Closing;
                        _alertChartUi.Show();
                    }
                }

                if (type == AlertType.PriceAlert)
                {
                    int num = 0;

                    if (_alertArray != null)
                    {
                        num = _alertArray.Count;
                    }

                    AlertToPrice newPriceAlert = new AlertToPrice(_name + num);

                    newPriceAlert.ShowDialog();

                    SetNewAlert(newPriceAlert);
                }
            }
            catch (Exception error)
            {
                SendNewMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// call up old alert settings by number
        /// вызвать насройки старого алерта по номеру
        /// </summary>
        /// <param name="number">номер</param>
        public void ShowAlertRedactDialog(int number)
        {
            try
            {
                if (_alertChartUi != null)
                {
                    MessageBox.Show(OsLocalization.Alerts.Message1);
                    return;
                }


                if (number > _alertArray.Count || _alertArray.Count == 0)
                {
                    return;
                }
                if (_alertArray[number].TypeAlert == AlertType.ChartAlert)
                {
                    _alertChartUi = new AlertToChartCreateUi((AlertToChart)_alertArray[number], this);
                    _alertChartUi.Closing += _ChartAertUi_Closing;
                    _alertChartUi.Show();
                }
                if (_alertArray[number].TypeAlert == AlertType.PriceAlert)
                {
                   ((AlertToPrice)_alertArray[number]).ShowDialog();
                }
            }
            catch (Exception error)
            {
                SendNewMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// alert settings window close
        /// закрывается окно настроек Алерта
        /// </summary>
        void _ChartAertUi_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                if (_alertChartUi.NeadToSave == false)
                {
                    Delete(_alertChartUi.MyAlert);
                }
            }
            catch (Exception error)
            {
                SendNewMessage(error.ToString(), LogMessageType.Error);
            }
            finally
            {
                _alertChartUi = null;
            }
        }

        /// <summary>
        /// check alert for triggers
        /// проверить Алерт на срабатывание
        /// </summary>
        /// <returns>signal/сигнал</returns>
        public AlertSignal CheckAlerts()
        {
            try
            {
                if (_alertArray == null)
                {
                    return null;
                }

                if (_connector == null)
                {
                    return null;
                }

                List<Candle> candles = _connector.Candles(false);

                for (int i = 0; i < _alertArray.Count; i++)
                {
                    if(_alertArray[i].IsOn == false)
                    {
                        continue;
                    }
                    AlertSignal signal = _alertArray[i].CheckSignal(candles);

                    if (signal != null)
                    {
                        return signal;
                    }
                }
            }
            catch (Exception error)
            {
                SendNewMessage(error.ToString(), LogMessageType.Error);
            }

            return null;
        }

        /// <summary>
        /// user clicked on table
        /// пользователь кликнул по таблице
        /// </summary>
        void GridViewBox_Click(object sender, EventArgs e)
        {
            MouseEventArgs mouse = (MouseEventArgs)e;
            if (mouse.Button != MouseButtons.Right)
            {
                return;
            }

            // shortcut menu creation
            // cоздание контекстного меню

            MenuItem[] items = new MenuItem[4];

            items[0] = new MenuItem();
            items[0].Text = OsLocalization.Alerts.ContextMenu1;
            items[0].Click += AlertDelete_Click;

            items[1] = new MenuItem() { Text = OsLocalization.Alerts.ContextMenu2 };
            items[1].Click += AlertRedact_Click;

            items[2] = new MenuItem() { Text = OsLocalization.Alerts.ContextMenu3 };
            items[2].Click += AlertChartCreate_Click;

            items[3] = new MenuItem() { Text = OsLocalization.Alerts.ContextMenu4 };
            items[3].Click += AlertPriceCreate_Click;

            ContextMenu menu = new ContextMenu(items);

            GridViewBox.ContextMenu = menu;
            GridViewBox.ContextMenu.Show(GridViewBox, new Point(mouse.X, mouse.Y));
        }

        void GridViewBox_DoubleClick(object sender, EventArgs e)
        {
            if (GridViewBox.CurrentCell == null ||
               GridViewBox.CurrentCell.RowIndex == -1)
            {
                return;
            }

            ShowAlertRedactDialog(GridViewBox.CurrentCell.RowIndex);
        }

        /// <summary>
        ///  user has chosen to remove alert
        /// позователь выбрал удалить алерт
        /// </summary>
        void AlertDelete_Click(object sender, EventArgs e)
        {
            if (GridViewBox.CurrentCell == null ||
                GridViewBox.CurrentCell.RowIndex <= -1)
            {
                return;
            }
            DeleteFromNumber(GridViewBox.CurrentCell.RowIndex);
        }

        /// <summary>
        /// user pressed edit alert
        /// пользователь нажал редактировать алерт
        /// </summary>
        void AlertRedact_Click(object sender, EventArgs e)
        {
            if (GridViewBox.CurrentCell == null ||
                GridViewBox.CurrentCell.RowIndex == -1)
            {
                return;
            }


            ShowAlertRedactDialog(GridViewBox.CurrentCell.RowIndex);
        }

        /// <summary>
        /// user clicked to create an alert
        /// пользователь нажал создать алерт
        /// </summary>
        void AlertChartCreate_Click(object sender, EventArgs e)
        {
            ShowAlertNewDialog(AlertType.ChartAlert);
        }

        /// <summary>
        /// user clicked to create an alert
        /// пользователь нажал создать алерт
        /// </summary>
        void AlertPriceCreate_Click(object sender, EventArgs e)
        {
            ShowAlertNewDialog(AlertType.PriceAlert);
        }

        /// <summary>
        /// cursor position on chart has changed
        /// изменилась позиция курсора на чарте
        /// </summary>
        private void ChartMaster_ChartClickEvent(ChartClickType clickType)
        {
            // check to see if you need to download information from cursor to alert window
            // проверить, не надо ли сейчас загрузить информацию с курсора в окно Алерта
            CheckAlert();
        }

        /// <summary>
        /// check if alert is currently being edited
        /// проверить, не редактируется ли сейчас Алерт
        /// </summary>
        private void CheckAlert()
        {
            try
            {
                if (_alertChartUi == null)
                {
                    return;
                }

                int numberCandle = 0;
                decimal pricePoint;
                // crutch does not always help
                // костыль не всегда помогает
                try
                {
                    numberCandle = _chartMaster.GetSelectCandleNumber();
                    pricePoint = _chartMaster.GetCursorSelectPrice();
                    _chartMaster.RemoveCursor();
                }
                catch (Exception)
                {
                    return;
                    // ignore
                }

                _alertChartUi.SetFormChart(_connector.Candles(false), numberCandle, pricePoint);
            }
            catch (Exception error)
            {
                SendNewMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// start drawing alerts
        /// начать прорисовку Алертов
        /// </summary>
        public void StartPaint(WindowsFormsHost alertDataGrid) 
        {
            try
            {
                HostAllert = alertDataGrid;

                if (!HostAllert.Dispatcher.CheckAccess())
                {
                    HostAllert.Dispatcher.Invoke(new Action<WindowsFormsHost>(StartPaint),alertDataGrid);
                    return;
                }

                HostAllert = alertDataGrid;
                HostAllert.Child = GridViewBox;
                _isPaint = true;
                Paint();
                HostAllert.Child.Show();

            }
            catch (Exception error)
            {
                SendNewMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// stop alert's drawing
        /// остановить прорисовку Алертов
        /// </summary>
        public void StopPaint()
        {
            try
            {
                if (HostAllert == null)
                {
                    return;
                }
                if (!HostAllert.Dispatcher.CheckAccess())
                {
                    HostAllert.Dispatcher.Invoke(StopPaint);
                    return;
                }

                _isPaint = false;

                if (HostAllert != null)
                {
                    HostAllert.Child = null;
                    HostAllert = null;
                }
            }
            catch (Exception error)
            {
                SendNewMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// draw alerts
        /// прорисовать Алерты
        /// </summary>
        private void Paint()
        {
            try
            {
                if (HostAllert == null)
                {
                    return;
                }

                if (!HostAllert.Dispatcher.CheckAccess())
                {
                    HostAllert.Dispatcher.Invoke((Paint));
                    return;
                }

                PaintAlertOnChart();
                PaintGridBox();
            }
            catch (Exception error)
            {
                SendNewMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// drawing elements enabled
        /// включена ли прорисовка элементов
        /// </summary>
        private bool _isPaint;

        /// <summary>
        /// draw all available alerts
        /// прорисовать все имеющиеся алерты
        /// </summary>
        private void PaintGridBox()
        {
            try
            {
                if (_isPaint == false)
                {
                    return;
                }

                GridViewBox.Rows.Clear();

                for (int i = 0; _alertArray != null && _alertArray.Count != 0 && i < _alertArray.Count; i++)
                {

                    DataGridViewRow nRow = new DataGridViewRow();

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[0].Value = i;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[1].Value = _alertArray[i].TypeAlert;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    if (_alertArray[i].IsOn)
                    {
                        nRow.Cells[2].Value = "On"; 
                    }
                    else
                    {
                        nRow.Cells[2].Value = "Off"; 
                    }
                   

                    GridViewBox.Rows.Add(nRow);
                }
            }
            catch (Exception error)
            {
                SendNewMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        ///  draw all alerts on chart
        /// прорисовать все алерты на чарте
        /// </summary>
        private void PaintAlertOnChart()
        {
            if (_chartMaster == null)
            {
                return;
            }
            _chartMaster.PaintAlerts(_alertArray);
        }
    }

    /// <summary>
    /// alert type
    /// тип алерта
    /// </summary>
    public enum AlertType
    {
        /// <summary>
        /// alert for charts
        /// алерт для чарта
        /// </summary>
        ChartAlert,

        /// <summary>
        /// alert price
        /// алерт достижения цены
        /// </summary>
        PriceAlert
    }

    /// <summary>
    /// type of alert for Alert
    /// тип оповещения для Алерта
    /// </summary>
    public enum AlertMusic
    {
        /// <summary>
        /// duck
        /// утка
        /// </summary>
        Duck,
        /// <summary>
        /// bird
        /// птица
        /// </summary>
        Bird,
        /// <summary>
        /// wolf
        /// волк
        /// </summary>
        Wolf
    }
}
