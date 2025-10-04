/*
 *Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using OsEngine.Charts.CandleChart;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Connectors;
using MessageBox = System.Windows.MessageBox;
using MouseEventArgs = System.Windows.Forms.MouseEventArgs;

namespace OsEngine.Alerts
{
    public class AlertMaster
    {
        public AlertMaster(string name, ConnectorCandles connector, ChartCandleMaster chartMaster)
        {
            _name = name;
            _connector = connector;
            _chartMaster = chartMaster;
            chartMaster.ChartClickEvent += ChartMaster_ChartClickEvent;
            Load();
        }

        private readonly string _name;

        private ConnectorCandles _connector;

        private ChartCandleMaster _chartMaster;

        public AlertSignal CheckAlerts()
        {
            try
            {
                if (_alertArray == null
                    || _alertArray.Count == 0)
                {
                    return null;
                }

                if (_connector == null)
                {
                    return null;
                }

                List<Candle> candles = _connector.Candles(false);

                Security sec = _connector.Security;

                if (sec == null)
                {
                    return null;
                }

                for (int i = 0; i < _alertArray.Count; i++)
                {
                    if (_alertArray[i].IsOn == false)
                    {
                        continue;
                    }
                    AlertSignal signal = _alertArray[i].CheckSignal(candles, sec);

                    if (signal != null)
                    {
                        Paint();
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

        private void ChartMaster_ChartClickEvent(ChartClickType clickType)
        {
            // check to see if you need to download information from cursor to alert window
            // проверить, не надо ли сейчас загрузить информацию с курсора в окно Алерта
            CheckAlert();
        }

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
                catch (Exception error)
                {
                    SendNewMessage(error.ToString(), LogMessageType.Error);
                    return;
                }

                if (pricePoint == 0)
                {
                    return;
                }

                _alertChartUi.SetFormChart(_connector.Candles(false), numberCandle, pricePoint);
            }
            catch (Exception error)
            {
                SendNewMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #region Data Grid Alerts

        private DataGridView GridViewBox;

        public WindowsFormsHost HostAlert;

        public void StartPaint(WindowsFormsHost alertHost)
        {
            try
            {
                HostAlert = alertHost;

                if (!HostAlert.Dispatcher.CheckAccess())
                {
                    HostAlert.Dispatcher.Invoke(new Action<WindowsFormsHost>(StartPaint), alertHost);
                    return;
                }

                CreateGrid();

                HostAlert.Child = GridViewBox;
                _isPaint = true;
                Paint();
                HostAlert.Child.Show();

            }
            catch (Exception error)
            {
                SendNewMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void StopPaint()
        {
            try
            {
                if (HostAlert == null)
                {
                    return;
                }
                if (!HostAlert.Dispatcher.CheckAccess())
                {
                    HostAlert.Dispatcher.Invoke(StopPaint);
                    return;
                }

                DeleteGrid();
                _isPaint = false;

                if (HostAlert != null)
                {
                    HostAlert.Child = null;
                    HostAlert = null;
                }
            }
            catch (Exception error)
            {
                SendNewMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private bool _isPaint;

        private void Paint()
        {
            try
            {
                if (HostAlert == null)
                {
                    return;
                }

                if (!HostAlert.Dispatcher.CheckAccess())
                {
                    HostAlert.Dispatcher.Invoke((Paint));
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

        private void PaintAlertOnChart()
        {
            if (_chartMaster == null)
            {
                return;
            }
            _chartMaster.PaintAlerts(_alertArray, false);
        }

        private void PaintGridBox()
        {
            try
            {
                if (_isPaint == false)
                {
                    return;
                }

                if (GridViewBox == null)
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

        private void CreateGrid()
        {
            if (HostAlert == null)
            {
                return;
            }

            if (!HostAlert.Dispatcher.CheckAccess())
            {
                HostAlert.Dispatcher.Invoke(new Action(CreateGrid));
                return;
            }

            GridViewBox = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
                DataGridViewAutoSizeRowsMode.AllCells);

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
            GridViewBox.DataError += GridViewBox_DataError;
        }

        private void GridViewBox_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            ServerMaster.SendNewLogMessage(e.ToString(), Logging.LogMessageType.Error);
        }

        private void DeleteGrid()
        {
            if (HostAlert == null
                || GridViewBox == null)
            {
                return;
            }

            if (!HostAlert.Dispatcher.CheckAccess())
            {
                HostAlert.Dispatcher.Invoke(new Action(DeleteGrid));
                return;
            }

            if (GridViewBox != null)
            {
                DataGridFactory.ClearLinks(GridViewBox);
                GridViewBox.Click -= GridViewBox_Click;
                GridViewBox.DoubleClick -= GridViewBox_DoubleClick;
                GridViewBox.DataError -= GridViewBox_DataError;
                GridViewBox.Rows.Clear();
                GridViewBox.Columns.Clear();
                GridViewBox = null;
            }

        }

        private void ClearGrid()
        {
            if (GridViewBox == null)
            {
                return;
            }

            if (GridViewBox.Rows.Count == 0)
            {
                return;
            }

            if (GridViewBox.InvokeRequired)
            {
                GridViewBox.Invoke(new Action(ClearGrid));
                return;
            }

            GridViewBox.Rows.Clear();
        }

        private void GridViewBox_Click(object sender, EventArgs e)
        {
            MouseEventArgs mouse = (MouseEventArgs)e;
            if (mouse.Button != MouseButtons.Right)
            {
                return;
            }

            // shortcut menu creation
            // cоздание контекстного меню

            ToolStripMenuItem[] items = new ToolStripMenuItem[4];

            items[0] = new ToolStripMenuItem();
            items[0].Text = OsLocalization.Alerts.ContextMenu1;
            items[0].Click += AlertDelete_Click;

            items[1] = new ToolStripMenuItem() { Text = OsLocalization.Alerts.ContextMenu2 };
            items[1].Click += AlertRedact_Click;

            items[2] = new ToolStripMenuItem() { Text = OsLocalization.Alerts.ContextMenu3 };
            items[2].Click += AlertChartCreate_Click;

            items[3] = new ToolStripMenuItem() { Text = OsLocalization.Alerts.ContextMenu4 };
            items[3].Click += AlertPriceCreate_Click;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.AddRange(items);

            GridViewBox.ContextMenuStrip = menu;
            GridViewBox.ContextMenuStrip.Show(GridViewBox, new Point(mouse.X, mouse.Y));
        }

        private void GridViewBox_DoubleClick(object sender, EventArgs e)
        {
            if (GridViewBox.CurrentCell == null ||
               GridViewBox.CurrentCell.RowIndex == -1)
            {
                return;
            }

            ShowAlertRedactDialog(GridViewBox.CurrentCell.RowIndex);
        }

        private void AlertDelete_Click(object sender, EventArgs e)
        {
            if (GridViewBox == null)
            {
                return;
            }

            if (GridViewBox.CurrentCell == null ||
                GridViewBox.CurrentCell.RowIndex <= -1)
            {
                return;
            }
            DeleteFromNumber(GridViewBox.CurrentCell.RowIndex);
        }

        private void AlertRedact_Click(object sender, EventArgs e)
        {
            if (GridViewBox == null)
            {
                return;
            }
            if (GridViewBox.CurrentCell == null ||
                GridViewBox.CurrentCell.RowIndex == -1)
            {
                return;
            }

            ShowAlertRedactDialog(GridViewBox.CurrentCell.RowIndex);
        }

        private void AlertChartCreate_Click(object sender, EventArgs e)
        {
            ShowAlertNewDialog(AlertType.ChartAlert);
        }

        private void AlertPriceCreate_Click(object sender, EventArgs e)
        {
            ShowAlertNewDialog(AlertType.PriceAlert);
        }

        #endregion

        #region Alerts creation and deletion

        private List<IIAlert> _alertArray;

        private void Load()
        {
            if (!File.Exists(@"Engine\" + _name + "AlertKeeper.txt"))
            {
                // if there is no file we need. Just go out
                // если нет нужного нам файла. Просто выходим
                return;
            }

            if (_connector.StartProgram != StartProgram.IsOsTrader)
            {
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
                            _alertArray.Add(new AlertToChart(saveStr, HostAlert));
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
                SendNewMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void Save()
        {
            try
            {
                if (_connector.StartProgram != StartProgram.IsOsTrader)
                {
                    return;
                }

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

        public void Clear()
        {
            if (_alertArray != null)
            {
                for (int i = 0; i < _alertArray.Count; i++)
                {
                    _alertArray[i].Delete();
                }
                _alertArray.Clear();
            }

            ClearGrid();
        }

        public void Delete()
        {
            try
            {
                if (File.Exists(@"Engine\" + _name + "AlertKeeper.txt"))
                {
                    File.Delete(@"Engine\" + _name + "AlertKeeper.txt");
                }

                if (_alertArray != null)
                {
                    for (int i = 0; i < _alertArray.Count; i++)
                    {
                        _alertArray[i].Delete();
                    }
                    _alertArray.Clear();
                    _alertArray = null;
                }

                if (_connector != null)
                {
                    _connector = null;
                }

                if (_chartMaster != null)
                {
                    _chartMaster.ChartClickEvent -= ChartMaster_ChartClickEvent;
                    _chartMaster = null;
                }

                DeleteVisual();
            }
            catch (Exception error)
            {
                SendNewMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void DeleteVisual()
        {
            if (HostAlert == null)
            {
                return;
            }

            if (!HostAlert.Dispatcher.CheckAccess())
            {
                HostAlert.Dispatcher.Invoke(new Action(DeleteVisual));
                return;
            }

            if (GridViewBox != null)
            {
                GridViewBox.Rows.Clear();
                GridViewBox.Click -= GridViewBox_Click;
                GridViewBox.DoubleClick -= GridViewBox_DoubleClick;
                GridViewBox.DataError -= GridViewBox_DataError;
                DataGridFactory.ClearLinks(GridViewBox);
                GridViewBox = null;
            }

            if (HostAlert != null)
            {
                HostAlert.Child = null;
                HostAlert = null;
            }
        }

        public void DeleteFromNumber(int number)
        {
            try
            {
                if (_alertArray == null ||
                number >= _alertArray.Count
                || _alertArray.Count == 0)
                {
                    return;
                }

                IIAlert activAlert = _alertArray[number];
                Delete(activAlert);
            }
            catch (Exception error)
            {
                SendNewMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void Delete(IIAlert alert)
        {
            try
            {
                if (_alertArray == null
                     || alert == null
                     || _alertArray.Count == 0)
                {
                    return;
                }

                _chartMaster.DeleteAlert(alert);

                alert.Delete();

                for (int i = 0; i < _alertArray.Count; i++)
                {
                    if (_alertArray[i].Name == alert.Name)
                    {
                        _alertArray.RemoveAt(i);
                        break;
                    }
                }

                Save();

                Paint();
            }
            catch (Exception error)
            {
                SendNewMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void SetNewAlert(IIAlert newAlert)
        {
            try
            {
                if (newAlert == null)
                {
                    return;
                }

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

        #endregion

        #region Show Dialog UI 

        private AlertToChartCreateUi _alertChartUi;

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
                        _alertChartUi.Closing += _ChartAlertUi_Closing;
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
                    _alertChartUi.Closing += _ChartAlertUi_Closing;
                    _alertChartUi.Show();
                }
                if (_alertArray[number].TypeAlert == AlertType.PriceAlert)
                {
                    ((AlertToPrice)_alertArray[number]).ShowDialog();
                    Save();
                }
            }
            catch (Exception error)
            {
                SendNewMessage(error.ToString(), LogMessageType.Error);
            }

            Paint();
        }

        void _ChartAlertUi_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                if (_alertChartUi.NeedToSave == false)
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

        #endregion

        #region Log

        private void SendNewMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            {
                ServerMaster.SendNewLogMessage(message, type);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }

    public enum AlertMusic
    {
        Duck,

        Bird,

        Wolf
    }
}