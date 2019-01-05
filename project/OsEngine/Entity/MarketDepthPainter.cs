﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using OsEngine.Logging;

namespace OsEngine.Entity
{
    /// <summary>
    /// класс отвечающий за отрисовку стакана и линий бид-аск
    /// </summary>
    public class MarketDepthPainter
    {

// статическая часть с работой потока прорисовывающего стакан

        /// <summary>
        /// поток 
        /// </summary>
        public static Thread Watcher;

        /// <summary>
        /// логи которые нужно обслуживать
        /// </summary>
        public static List<MarketDepthPainter> MarketDepthsToCheck = new List<MarketDepthPainter>();

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
                Watcher.Name = "MarketDepthPainterThread";
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
                Thread.Sleep(700);

                for (int i = 0; i < MarketDepthsToCheck.Count; i++)
                {
                    MarketDepthsToCheck[i].TryPaintMarketDepth();
                }

                if (!MainWindow.ProccesIsWorked)
                {
                    return;
                }
            }
        }

// основной класс

        /// <summary>
        /// конструктор объекта прорисовывающего стакан
        /// </summary>
        public MarketDepthPainter(string botName)
        {
            CreateGlass();
            if (Watcher == null)
            {
                Activate();
            }
            MarketDepthsToCheck.Add(this);
            _name = botName;
        }

        /// <summary>
        /// удалить данный объект из прорисовки
        /// </summary>
        public void Delete()
        {
            for (int i = 0; i < MarketDepthsToCheck.Count; i++)
            {
                if (MarketDepthsToCheck[i]._name == _name)
                {
                    MarketDepthsToCheck.RemoveAt(i);
                    return;
                }
            }
        }

        /// <summary>
        /// имя робота которому принадлежит стакан
        /// </summary>
        private string _name;

        /// <summary>
        /// область для размещения стакана
        /// </summary>
        private WindowsFormsHost _hostGlass;

        /// <summary>
        /// таблица стакана
        /// </summary>
        DataGridView _glassBox;

        /// <summary>
        /// элемент для отрисовки выбранной пользователем цены
        /// </summary>
        private System.Windows.Controls.TextBox _textBoxLimitPrice;

        /// <summary>
        /// последняя выбранная пользователем цена
        /// </summary>
        private decimal _lastSelectPrice;

        /// <summary>
        /// загрузить контролы в коннектор
        /// </summary>
        public void CreateGlass()
        {
            try
            {
                _glassBox = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
                    DataGridViewAutoSizeRowsMode.None);
                _glassBox.AllowUserToResizeRows = false;

                _glassBox.SelectionChanged += _glassBox_SelectionChanged;

                DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
                cell0.Style = _glassBox.DefaultCellStyle;

                DataGridViewColumn column0 = new DataGridViewColumn();
                column0.CellTemplate = cell0;
                column0.HeaderText = @"Сумма";
                column0.ReadOnly = true;
                column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                _glassBox.Columns.Add(column0);

                DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();
                DataGridViewColumn column = new DataGridViewColumn();
                column.CellTemplate = cell;
                column.HeaderText = @"Объём";
                column.ReadOnly = true;
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                _glassBox.Columns.Add(column);

                DataGridViewTextBoxCell cell2 = new DataGridViewTextBoxCell();
                DataGridViewColumn column1 = new DataGridViewColumn();
                column1.CellTemplate = cell2;
                column1.HeaderText = @"Цена";
                column1.ReadOnly = true;
                column1.Width = 90;

                _glassBox.Columns.Add(column1);


                DataGridViewTextBoxCell cell3 = new DataGridViewTextBoxCell();
                DataGridViewColumn column3 = new DataGridViewColumn();
                column3.CellTemplate = cell3;
                column3.HeaderText = @"Объём";
                column3.ReadOnly = true;
                column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                _glassBox.Columns.Add(column3);

                DataGridViewCellStyle styleRed = new DataGridViewCellStyle();
                styleRed.Alignment = DataGridViewContentAlignment.MiddleRight;
                styleRed.ForeColor = Color.FromArgb(254, 84, 0);
                styleRed.Font = new Font("Areal", 3);


                for (int i = 0; i < 25; i++)
                {
                    _glassBox.Rows.Add(null, null, null);
                    _glassBox.Rows[_glassBox.Rows.Count - 1].DefaultCellStyle.BackColor = Color.FromArgb(28, 33, 37);
                    _glassBox.Rows[_glassBox.Rows.Count - 1].DefaultCellStyle.ForeColor = Color.FromArgb(254, 84, 0);
                    _glassBox.Rows[_glassBox.Rows.Count - 1].DefaultCellStyle.Font = new Font("New Times Roman", 10);
                    _glassBox.Rows[_glassBox.Rows.Count - 1].Cells[0].Style = styleRed;
                    _glassBox.Rows[_glassBox.Rows.Count - 1].Cells[1].Style = styleRed;
                }

                DataGridViewCellStyle styleBlue = new DataGridViewCellStyle();
                styleBlue.Alignment = DataGridViewContentAlignment.MiddleRight;
                styleBlue.ForeColor = Color.FromArgb(57, 157,54);
                styleBlue.Font = new Font("Areal", 3);

                for (int i = 0; i < 25; i++)
                {
                    _glassBox.Rows.Add(null, null, null);
                    //_glassBox.Rows[_glassBox.Rows.Count - 1].DefaultCellStyle.BackColor = Color.Black;
                    _glassBox.Rows[_glassBox.Rows.Count - 1].DefaultCellStyle.ForeColor = Color.FromArgb(57, 157, 54);
                    _glassBox.Rows[_glassBox.Rows.Count - 1].DefaultCellStyle.Font = new Font("New Times Roman", 10);
                    _glassBox.Rows[_glassBox.Rows.Count - 1].Cells[0].Style = styleBlue;
                    _glassBox.Rows[_glassBox.Rows.Count - 1].Cells[1].Style = styleBlue;
                }

                _glassBox.Rows[22].Cells[0].Selected = true;
                _glassBox.Rows[22].Cells[0].Selected = false;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// изменился текст в поле лимитной цены рядом со стаканом
        /// </summary>
        void _textBoxLimitPrice_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                try
                {
                    if (Convert.ToDecimal(_textBoxLimitPrice.Text) < 0)
                    {
                        throw new Exception();
                    }
                }
                catch (Exception)
                {
                    _textBoxLimitPrice.Text = _lastSelectPrice.ToString(new CultureInfo("RU-ru"));
                }

                _lastSelectPrice = Convert.ToDecimal(_textBoxLimitPrice.Text);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// пользователь щёлкнул по стакану
        /// </summary>
        void _glassBox_SelectionChanged(object sender, EventArgs e)
        {
            try
            {
                if (_glassBox.InvokeRequired)
                {
                    _glassBox.Invoke(new Action<object, EventArgs>(_glassBox_SelectionChanged), sender, e);
                    return;
                }

                decimal price;
                try
                {
                    if (_glassBox.CurrentCell == null)
                    {
                        return;
                    }
                    price = Convert.ToDecimal(_glassBox.Rows[_glassBox.CurrentCell.RowIndex].Cells[2].Value);
                }
                catch (Exception)
                {
                    return;
                }

                if (price == 0)
                {
                    return;
                }
                if (_hostGlass != null)
                {
                    _lastSelectPrice = price;
                    _textBoxLimitPrice.Text = Convert.ToDouble(_lastSelectPrice).ToString(new CultureInfo("RU-ru"));
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// начать прорисовывать элементы коннектора
        /// </summary>
        public void StartPaint(WindowsFormsHost glass, System.Windows.Controls.TextBox textBoxLimitPrice)
        {
            try
            {
                if (_glassBox == null)
                {
                    return;
                }
                if (_glassBox.InvokeRequired)
                {
                    _glassBox.Invoke(new Action<WindowsFormsHost, System.Windows.Controls.TextBox>(StartPaint), glass, textBoxLimitPrice);
                    return;
                }

                _textBoxLimitPrice = textBoxLimitPrice;
                _textBoxLimitPrice.TextChanged += _textBoxLimitPrice_TextChanged;
                _hostGlass = glass;

                ProcessBidAsk(_bid, _ask);
                _hostGlass.Child = _glassBox;
                _hostGlass.Child.Refresh();

                _textBoxLimitPrice.Text = Convert.ToDouble(_lastSelectPrice).ToString(new CultureInfo("RU-ru"));

                ProcessMarketDepth(_lastMarketDepth);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// остановить прорисовывание элементов коннектора
        /// </summary>
        public void StopPaint()
        {
            try
            {
                if (_glassBox != null && _glassBox.InvokeRequired)
                {
                    _glassBox.Invoke(new Action(StopPaint));
                    return;
                }

                if (_textBoxLimitPrice != null)
                {
                    _textBoxLimitPrice.TextChanged -= _textBoxLimitPrice_TextChanged;
                    _textBoxLimitPrice = null;
                }

                if (_hostGlass != null)
                {
                    _hostGlass.Child = null;
                    _hostGlass = null;
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

// работа потока прорисовывающего стаканы

        /// <summary>
        /// прорисовать стакан
        /// </summary>
        private void TryPaintMarketDepth()
        {
            if (_hostGlass == null)
            {
                return;
            }

            MarketDepth depth = _currentMaretDepth;
            _currentMaretDepth = null;

            if (depth != null)
            {
                PaintMarketDepth(depth);
            }

            if (_bid != 0 && _ask != 0)
            {
                PaintBidAsk(_bid, _ask);
                _bid = 0;
                _ask = 0;
            }
        }

        /// <summary>
        /// предпоследний стакан
        /// </summary>
        private MarketDepth _lastMarketDepth;

        /// <summary>
        /// текущий стакан
        /// </summary>
        private MarketDepth _currentMaretDepth;

        /// <summary>
        /// отправить стакан на прорисовку
        /// </summary>
        public void ProcessMarketDepth(MarketDepth depth)
        {
            _currentMaretDepth = depth;
        }

        /// <summary>
        /// прорисовать стакан
        /// </summary>
        private void PaintMarketDepth(MarketDepth depth)
        {
            try
            {
                _lastMarketDepth = depth;

                if (_hostGlass == null || depth == null)
                {
                    return;
                }

                if (_glassBox.InvokeRequired)
                {
                    _glassBox.Invoke(new Action<MarketDepth>(PaintMarketDepth), depth);
                    return;
                }

                if (depth.Bids[0].Bid == 0 ||
                    depth.Asks[0].Ask == 0)
                {
                    return;
                }

                decimal maxVol = 0;

                decimal allBid = 0;

                decimal allAsk = 0;

                for (int i = 0; depth.Bids != null && i < 25; i++)
                {
                    if (i < depth.Bids.Count)
                    {
                        _glassBox.Rows[25 + i].Cells[2].Value = depth.Bids[i].Price;
                        _glassBox.Rows[25 + i].Cells[3].Value = depth.Bids[i].Bid;
                        if (depth.Bids[i].Bid > maxVol)
                        {
                            maxVol = depth.Bids[i].Bid;
                        }
                        allAsk += depth.Bids[i].Bid;
                    }
                    else if (_glassBox.Rows[25 + i].Cells[2].Value != null)
                    {
                        _glassBox.Rows[25 + i].Cells[0].Value = null;
                        _glassBox.Rows[25 + i].Cells[1].Value = null;
                        _glassBox.Rows[25 + i].Cells[2].Value = null;
                        _glassBox.Rows[25 + i].Cells[3].Value = null;
                    }
                }


                for (int i = 0; depth.Asks != null && i < 25; i++)
                {
                    if (i < depth.Asks.Count)
                    {
                        _glassBox.Rows[24 - i].Cells[2].Value = depth.Asks[i].Price;
                        _glassBox.Rows[24 - i].Cells[3].Value = depth.Asks[i].Ask;

                        if (depth.Asks[i].Ask > maxVol)
                        {
                            maxVol = depth.Asks[i].Ask;
                        }

                        allBid += depth.Asks[i].Ask;
                    }
                    else if (_glassBox.Rows[24 - i].Cells[2].Value != null)
                    {
                        _glassBox.Rows[24 - i].Cells[2].Value = null;
                        _glassBox.Rows[24 - i].Cells[3].Value = null;
                        _glassBox.Rows[24 - i].Cells[0].Value = null;
                        _glassBox.Rows[24 - i].Cells[1].Value = null;
                    }

                }

                // объём в палках для аска
                for (int i = 0; depth.Bids != null && i < 25 && i < depth.Bids.Count; i++)
                {
                    int percentFromMax = Convert.ToInt32(depth.Bids[i].Bid / maxVol * 50);

                    if (percentFromMax == 0)
                    {
                        percentFromMax = 1;
                    }

                    StringBuilder builder = new StringBuilder(percentFromMax);
                    for (int i2 = 0; i2 < percentFromMax; i2++)
                    {
                        builder.Append('|');
                    }

                    _glassBox.Rows[25 + i].Cells[1].Value = builder;

                }

                // объём в палках для бида
                for (int i = 0; depth.Asks != null && i < 25 && i < depth.Asks.Count; i++)
                {
                    int percentFromMax = Convert.ToInt32(depth.Asks[i].Ask / maxVol * 50);

                    if (percentFromMax == 0)
                    {
                        percentFromMax = 1;
                    }

                    StringBuilder builder = new StringBuilder(percentFromMax);

                    for (int i2 = 0; i2 < percentFromMax ; i2++)
                    {
                        builder.Append('|');
                    }

                    _glassBox.Rows[24 - i].Cells[1].Value = builder;
                }

                decimal maxSeries;

                if (allAsk > allBid)
                {
                    maxSeries = allAsk;
                }
                else
                {
                    maxSeries = allBid;
                }

                // объём комулятивный для аска
                decimal summ = 0;
                for (int i = 0; depth.Bids != null && i < 25 && i < depth.Bids.Count; i++)
                {
                    summ += depth.Bids[i].Bid;

                    int percentFromMax = Convert.ToInt32(summ / maxSeries * 50);

                    if (percentFromMax == 0)
                    {
                        percentFromMax = 1;
                    }

                    StringBuilder builder = new StringBuilder(percentFromMax);
                    for (int i2 = 0; i2 < percentFromMax; i2++)
                    {
                        builder.Append('|');
                    }

                    _glassBox.Rows[25 + i].Cells[0].Value = builder;

                }

                // объём комулятивный для бида
                summ = 0;
                for (int i = 0; depth.Asks != null && i < 25 && i < depth.Asks.Count; i++)
                {
                    summ += depth.Asks[i].Ask;

                    int percentFromMax = Convert.ToInt32(summ / maxSeries * 50);

                    if (percentFromMax == 0)
                    {
                        percentFromMax = 1;
                    }

                    StringBuilder builder = new StringBuilder(percentFromMax);
                    for (int i2 = 0; i2 < percentFromMax; i2++)
                    {
                        builder.Append('|');
                    }

                    _glassBox.Rows[24 - i].Cells[0].Value = builder;

                }
                // _glassBox.Refresh();
            }
            catch (Exception error)
            {
                //SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private decimal _bid;

        private decimal _ask;

        /// <summary>
        /// отправить Бид с Аском на прорисовку
        /// </summary>
        public void ProcessBidAsk(decimal bid, decimal ask)
        {
            _bid = bid;
            _ask = ask;
        }

        /// <summary>
        /// прорисовать бид с аском в стакане
        /// </summary>
        private void PaintBidAsk(decimal bid, decimal ask)
        {
            try
            {
                if (_hostGlass == null)
                {
                    return;
                }
                if (_glassBox.InvokeRequired)
                {
                    _glassBox.Invoke(new Action<decimal, decimal>(PaintBidAsk), bid, ask);
                    return;
                }

                if (ask != 0 && bid != 0)
                {
                    _glassBox.Rows[25].Cells[2].Value = bid;
                    _glassBox.Rows[24].Cells[2].Value = ask;
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

// сообщения в лог 

        /// <summary>
        /// выслать новое сообщение на верх
        /// </summary>
        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            { // если на нас никто не подписан и в логе ошибка
                System.Windows.MessageBox.Show(message);
            }
        }

        /// <summary>
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

    }
}
