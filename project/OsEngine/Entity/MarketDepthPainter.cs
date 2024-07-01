/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using OsEngine.Language;
using OsEngine.Logging;

namespace OsEngine.Entity
{
    /// <summary>
    /// class responsible for drawing the market depths and lines bid-ask
    /// </summary>
    public class MarketDepthPainter
    {
        // static part with the work of drawing market depths flow

        public static List<MarketDepthPainter> MarketDepthsToCheck = new List<MarketDepthPainter>();

        private static object _activatorLocker = new object();

        public static void Activate()
        {
            lock (_activatorLocker)
            {
                if (_painter != null)
                {
                    return;
                }

                _painter = new Task(WatcherHome);
                _painter.Start();
            }
        }

        private static Task _painter;

        public static async void WatcherHome()
        {
            while (true)
            {
                await Task.Delay(700);

                for (int i = 0; i < MarketDepthsToCheck.Count; i++)
                {
                    if (MarketDepthsToCheck[i] == null)
                    {
                        continue;
                    }
                    MarketDepthsToCheck[i].TryPaintMarketDepth();
                }

                if (!MainWindow.ProccesIsWorked)
                {
                    return;
                }
            }
        }

        // main object

        public MarketDepthPainter(string botName)
        {
            Activate();

            MarketDepthsToCheck.Add(this);
            _name = botName;
        }

        /// <summary>
        /// remove all objects from the drawing
        /// </summary>
        public void Delete()
        {
            for (int i = 0; i < MarketDepthsToCheck.Count; i++)
            {
                if (MarketDepthsToCheck[i] == null ||
                    MarketDepthsToCheck[i]._name == _name)
                {

                    MarketDepthsToCheck.RemoveAt(i);
                    return;
                }
            }

            _marketDepthTable = null;
            _hostMd = null;
        }

        private string _name;

        private WindowsFormsHost _hostMd;

        DataGridView _marketDepthTable;

        private System.Windows.Controls.TextBox _textBoxLimitPrice;

        private System.Windows.Controls.TextBox _textBoxVolume;

        private decimal _lastSelectPrice;

        public void CreateMarketDepthControl()
        {
            try
            {
                _marketDepthTable = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
                    DataGridViewAutoSizeRowsMode.AllCells);
                _marketDepthTable.AllowUserToResizeRows = false;
                _marketDepthTable.ScrollBars = ScrollBars.Vertical;
                _marketDepthTable.SelectionChanged += _glassBox_SelectionChanged;

                DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
                cell0.Style = _marketDepthTable.DefaultCellStyle;

                DataGridViewColumn column0 = new DataGridViewColumn();
                column0.CellTemplate = cell0;
                column0.HeaderText = OsLocalization.Entity.ColumnMarketDepth1;
                column0.ReadOnly = true;
                column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                _marketDepthTable.Columns.Add(column0);

                DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();
                DataGridViewColumn column = new DataGridViewColumn();
                column.CellTemplate = cell;
                column.HeaderText = OsLocalization.Entity.ColumnMarketDepth3;
                column.ReadOnly = true;
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                _marketDepthTable.Columns.Add(column);

                DataGridViewTextBoxCell cell2 = new DataGridViewTextBoxCell();
                DataGridViewColumn column1 = new DataGridViewColumn();
                column1.CellTemplate = cell2;
                column1.HeaderText = OsLocalization.Entity.ColumnMarketDepth2;
                column1.ReadOnly = true;
                column1.Width = 90;

                _marketDepthTable.Columns.Add(column1);


                DataGridViewTextBoxCell cell3 = new DataGridViewTextBoxCell();
                DataGridViewColumn column3 = new DataGridViewColumn();
                column3.CellTemplate = cell3;
                column3.HeaderText = OsLocalization.Entity.ColumnMarketDepth3;
                column3.ReadOnly = true;
                column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                _marketDepthTable.Columns.Add(column3);

                DataGridViewCellStyle styleRed = new DataGridViewCellStyle();
                styleRed.Alignment = DataGridViewContentAlignment.MiddleRight;
                styleRed.ForeColor = Color.FromArgb(254, 84, 0);
                styleRed.Font = new Font("Areal", 3);

                for (int i = 0; i < 25; i++)
                {
                    _marketDepthTable.Rows.Add(null, null, null);
                    _marketDepthTable.Rows[_marketDepthTable.Rows.Count - 1].DefaultCellStyle.BackColor = Color.FromArgb(28, 33, 37);
                    _marketDepthTable.Rows[_marketDepthTable.Rows.Count - 1].DefaultCellStyle.ForeColor = Color.FromArgb(254, 84, 0);
                    _marketDepthTable.Rows[_marketDepthTable.Rows.Count - 1].DefaultCellStyle.Font = new Font("New Times Roman", 10);
                    _marketDepthTable.Rows[_marketDepthTable.Rows.Count - 1].Cells[0].Style = styleRed;
                    _marketDepthTable.Rows[_marketDepthTable.Rows.Count - 1].Cells[1].Style = styleRed;
                }

                DataGridViewCellStyle styleBlue = new DataGridViewCellStyle();
                styleBlue.Alignment = DataGridViewContentAlignment.MiddleRight;
                styleBlue.ForeColor = Color.FromArgb(57, 157,54);
                styleBlue.Font = new Font("Areal", 3);

                for (int i = 0; i < 25; i++)
                {
                    _marketDepthTable.Rows.Add(null, null, null);
                    //_glassBox.Rows[_glassBox.Rows.Count - 1].DefaultCellStyle.BackColor = Color.Black;
                    _marketDepthTable.Rows[_marketDepthTable.Rows.Count - 1].DefaultCellStyle.ForeColor = Color.FromArgb(57, 157, 54);
                    _marketDepthTable.Rows[_marketDepthTable.Rows.Count - 1].DefaultCellStyle.Font = new Font("New Times Roman", 10);
                    _marketDepthTable.Rows[_marketDepthTable.Rows.Count - 1].Cells[0].Style = styleBlue;
                    _marketDepthTable.Rows[_marketDepthTable.Rows.Count - 1].Cells[1].Style = styleBlue;
                }

                _marketDepthTable.Rows[22].Cells[0].Selected = true;
                _marketDepthTable.Rows[22].Cells[0].Selected = false;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

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

                _lastSelectPrice = _textBoxLimitPrice.Text.ToDecimal();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        void _glassBox_SelectionChanged(object sender, EventArgs e)
        {
            try
            {
                if (_marketDepthTable.InvokeRequired)
                {
                    _marketDepthTable.Invoke(new Action<object, EventArgs>(_glassBox_SelectionChanged), sender, e);
                    return;
                }

                decimal price;
                try
                {
                    if (_marketDepthTable.CurrentCell == null ||
                        _marketDepthTable.Rows.Count == 0 ||
                        _marketDepthTable.Rows[_marketDepthTable.CurrentCell.RowIndex].Cells.Count < 2 ||
                        _marketDepthTable.Rows[_marketDepthTable.CurrentCell.RowIndex].Cells[2].Value == null)
                    {
                        return;
                    }
  
                    price = _marketDepthTable.Rows[_marketDepthTable.CurrentCell.RowIndex].Cells[2].Value.ToString().ToDecimal();
                }
                catch (Exception)
                {
                    return;
                }

                if (price == 0)
                {
                    return;
                }
                if (_hostMd != null)
                {
                    _lastSelectPrice = price;
                    if(_textBoxLimitPrice != null)
                    {
                        _textBoxLimitPrice.Text = Convert.ToDouble(_lastSelectPrice).ToString(new CultureInfo("RU-ru"));
                    }
                    
                    if(UserClickOnMDAndSelectPriceEvent != null)
                    {
                        UserClickOnMDAndSelectPriceEvent(_lastSelectPrice);
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public event Action<decimal> UserClickOnMDAndSelectPriceEvent;

        public void StartPaint(WindowsFormsHost glass, System.Windows.Controls.TextBox textBoxLimitPrice, System.Windows.Controls.TextBox textBoxVolume)
        {
            try
            {
                if (glass.Dispatcher.CheckAccess() == false)
                {
                    glass.Dispatcher.Invoke(new Action<WindowsFormsHost, System.Windows.Controls.TextBox, System.Windows.Controls.TextBox>(StartPaint), glass, textBoxLimitPrice,textBoxVolume);
                    return;
                }

                if(_marketDepthTable == null)
                {
                    CreateMarketDepthControl();
                    TryPaintMarketDepth();
                }

                if(textBoxVolume != null)
                {
                    _textBoxVolume = textBoxVolume;

                    if(string.IsNullOrEmpty(_lastVolumeText) == false)
                    {
                        _textBoxVolume.Text = _lastVolumeText;
                    }
                }
                
                if(textBoxLimitPrice != null)
                {
                    _textBoxLimitPrice = textBoxLimitPrice;

                    if(string.IsNullOrEmpty(_lastPriceText) == false)
                    {
                        _textBoxLimitPrice.Text = _lastPriceText;
                    }

                    _textBoxLimitPrice.TextChanged += _textBoxLimitPrice_TextChanged;
                    _textBoxLimitPrice.Text = Convert.ToDouble(_lastSelectPrice).ToString(new CultureInfo("RU-ru"));
                }

                _hostMd = glass;

                ProcessBidAsk(_bid, _ask);
                _hostMd.Child = _marketDepthTable;
                _hostMd.Child.Refresh();

                if(_lastMarketDepth != null)
                {
                    ProcessMarketDepth(_lastMarketDepth);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private string _lastPriceText;

        private string _lastVolumeText; 

        public void StopPaint()
        {
            try
            {
                if (_marketDepthTable == null)
                {
                    return;
                }
                if (_marketDepthTable != null && _marketDepthTable.InvokeRequired)
                {
                    _marketDepthTable.Invoke(new Action(StopPaint));
                    return;
                }

                if (_textBoxLimitPrice != null)
                {
                    _textBoxLimitPrice.TextChanged -= _textBoxLimitPrice_TextChanged;
                    _lastPriceText = _textBoxLimitPrice.Text;
                    _textBoxLimitPrice = null;
                }

                if(_textBoxVolume != null)
                {
                    _lastVolumeText = _textBoxVolume.Text;
                    _textBoxVolume = null;
                }

                if (_hostMd != null)
                {
                    _hostMd.Child = null;
                    _hostMd = null;
                }

                if(_marketDepthTable != null)
                {

                    _marketDepthTable.SelectionChanged -= _glassBox_SelectionChanged;
                    _marketDepthTable.Rows.Clear();
                    DataGridFactory.ClearLinks(_marketDepthTable);
                    _marketDepthTable = null;
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        // operation of the flow of drawing market depths

        private void TryPaintMarketDepth()
        {
            if (_hostMd == null)
            {
                return;
            }

            if(_marketDepthTable == null)
            {
                return;
            }

            MarketDepth depth = _currentMaretDepth;
            _currentMaretDepth = null;

            if (depth != null)
            {
                PaintMarketDepth(depth);
            }
            else
            {
                if (_bid != 0 && _ask != 0)
                {
                    PaintBidAsk(_bid, _ask);
                    _bid = 0;
                    _ask = 0;
                }
            }
        }

        private MarketDepth _lastMarketDepth;

        private DateTime _lastMdTimeEntry;

        private MarketDepth _currentMaretDepth;

        public void ProcessMarketDepth(MarketDepth depth)
        {
            _currentMaretDepth = depth;
            _lastMdTimeEntry = DateTime.Now;
        }

        private void PaintMarketDepth(MarketDepth depth)
        {
            try
            {
                _lastMarketDepth = depth;

                if (_hostMd == null || depth == null)
                {
                    return;
                }

                if (_marketDepthTable.InvokeRequired)
                {
                    _marketDepthTable.Invoke(new Action<MarketDepth>(PaintMarketDepth), depth);
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
                        string price = depth.Bids[i].Price.ToStringWithNoEndZero();
                        string bid = depth.Bids[i].Bid.ToStringWithNoEndZero();

                        if(_marketDepthTable.Rows[25 + i].Cells[2].Value == null ||
                            _marketDepthTable.Rows[25 + i].Cells[2].Value.ToString() != price)
                        {
                            _marketDepthTable.Rows[25 + i].Cells[2].Value = price;
                        }
                        
                        if(_marketDepthTable.Rows[25 + i].Cells[3].Value == null ||
                            _marketDepthTable.Rows[25 + i].Cells[3].Value.ToString() != bid)
                        {
                            _marketDepthTable.Rows[25 + i].Cells[3].Value = bid;
                        }

                        if (depth.Bids[i].Bid > maxVol)
                        {
                            maxVol = depth.Bids[i].Bid;
                        }
                        allAsk += depth.Bids[i].Bid;
                    }
                    else if (_marketDepthTable.Rows[25 + i].Cells[2].Value != null)
                    {
                        _marketDepthTable.Rows[25 + i].Cells[0].Value = null;
                        _marketDepthTable.Rows[25 + i].Cells[1].Value = null;
                        _marketDepthTable.Rows[25 + i].Cells[2].Value = null;
                        _marketDepthTable.Rows[25 + i].Cells[3].Value = null;
                    }
                }


                for (int i = 0; depth.Asks != null && i < 25; i++)
                {
                    if (i < depth.Asks.Count)
                    {
                        string price = depth.Asks[i].Price.ToStringWithNoEndZero();
                        string ask = depth.Asks[i].Ask.ToStringWithNoEndZero();

                        if(_marketDepthTable.Rows[24 - i].Cells[2].Value == null ||
                            _marketDepthTable.Rows[24 - i].Cells[2].Value.ToString() != price)
                        {
                            _marketDepthTable.Rows[24 - i].Cells[2].Value = price;
                        }
                        
                        if(_marketDepthTable.Rows[24 - i].Cells[3].Value == null ||
                            _marketDepthTable.Rows[24 - i].Cells[3].Value.ToString() != ask)
                        {
                            _marketDepthTable.Rows[24 - i].Cells[3].Value = ask;
                        }

                        if (depth.Asks[i].Ask > maxVol)
                        {
                            maxVol = depth.Asks[i].Ask;
                        }

                        allBid += depth.Asks[i].Ask;
                    }
                    else if (_marketDepthTable.Rows[24 - i].Cells[2].Value != null)
                    {
                        _marketDepthTable.Rows[24 - i].Cells[2].Value = null;
                        _marketDepthTable.Rows[24 - i].Cells[3].Value = null;
                        _marketDepthTable.Rows[24 - i].Cells[0].Value = null;
                        _marketDepthTable.Rows[24 - i].Cells[1].Value = null;
                    }
                }
                // volume in sticks for ask
                // объём в палках для аска
                for (int i = 0; depth.Bids != null && i < 25 && i < depth.Bids.Count; i++)
                {
                    if (maxVol == 0)
                    {
                        break;
                    }

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

                    if(_marketDepthTable.Rows[25 + i].Cells[1].Value == null ||
                        _marketDepthTable.Rows[25 + i].Cells[1].Value.ToString() != builder.ToString())
                    {
                        _marketDepthTable.Rows[25 + i].Cells[1].Value = builder.ToString();
                    }
                }

                // volume in bid sticks
                // объём в палках для бида
                for (int i = 0; depth.Asks != null && i < 25 && i < depth.Asks.Count; i++)
                {
                    if(maxVol == 0)
                    {
                        break;
                    }
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

                    if(_marketDepthTable.Rows[24 - i].Cells[1].Value == null ||
                        _marketDepthTable.Rows[24 - i].Cells[1].Value.ToString() != builder.ToString())
                    {
                        _marketDepthTable.Rows[24 - i].Cells[1].Value = builder.ToString();
                    }
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
                // volume cumulative for ask
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

                    _marketDepthTable.Rows[25 + i].Cells[0].Value = builder;

                }
                // volume is cumulative for bids
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

                    _marketDepthTable.Rows[24 - i].Cells[0].Value = builder;

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

        public void ProcessBidAsk(decimal bid, decimal ask)
        {
            _bid = bid;
            _ask = ask;
        }

        private void PaintBidAsk(decimal bid, decimal ask)
        {
            try
            {
                if (_hostMd == null)
                {
                    return;
                }
                if (_marketDepthTable.InvokeRequired)
                {
                    _marketDepthTable.Invoke(new Action<decimal, decimal>(PaintBidAsk), bid, ask);
                    return;
                }

                if (ask != 0 && bid != 0)
                {
                    _marketDepthTable.Rows[25].Cells[2].Value = bid.ToStringWithNoEndZero();
                    _marketDepthTable.Rows[24].Cells[2].Value = ask.ToStringWithNoEndZero();
                }

                if (_marketDepthTable.Rows[26].Cells[2].Value != null ||
                         _marketDepthTable.Rows[23].Cells[2].Value != null)
                {
                    if(_lastMdTimeEntry.AddSeconds(5) < DateTime.Now)
                    {
                        for (int i = 0; i < 25; i++)
                        {
                            if(i == 0 &&
                                _marketDepthTable.Rows[25].Cells[1].Value != null)
                            {
                                _marketDepthTable.Rows[25].Cells[0].Value = null;
                                _marketDepthTable.Rows[25].Cells[1].Value = null;
                                _marketDepthTable.Rows[25].Cells[3].Value = null;
                            }
                            else if (_marketDepthTable.Rows[25 + i].Cells[2].Value != null)
                            {
                                _marketDepthTable.Rows[25 + i].Cells[0].Value = null;
                                _marketDepthTable.Rows[25 + i].Cells[1].Value = null;
                                _marketDepthTable.Rows[25 + i].Cells[2].Value = null;
                                _marketDepthTable.Rows[25 + i].Cells[3].Value = null;
                            }
                        }

                        for (int i = 0; i < 25; i++)
                        {
                            if (i == 0 &&
                                _marketDepthTable.Rows[24].Cells[1].Value != null)
                            {
                                _marketDepthTable.Rows[24].Cells[0].Value = null;
                                _marketDepthTable.Rows[24].Cells[1].Value = null;
                                _marketDepthTable.Rows[24].Cells[3].Value = null;
                            }
                            else if (_marketDepthTable.Rows[24 - i].Cells[2].Value != null)
                            {
                                _marketDepthTable.Rows[24 - i].Cells[2].Value = null;
                                _marketDepthTable.Rows[24 - i].Cells[3].Value = null;
                                _marketDepthTable.Rows[24 - i].Cells[0].Value = null;
                                _marketDepthTable.Rows[24 - i].Cells[1].Value = null;
                            }

                        }

                        if (_lastMarketDepth != null)
                        {
                            _lastMarketDepth = null;
                        }
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        // messages to log

        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            {
                // if nobody is signed to us and there is an error in the log
                // если на нас никто не подписан и в логе ошибка
                System.Windows.MessageBox.Show(message);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

    }
}
