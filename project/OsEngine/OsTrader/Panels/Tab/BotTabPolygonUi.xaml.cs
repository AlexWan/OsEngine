/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Layout;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using OsEngine.Market.Servers;
using OsEngine.Market;
using System.Windows.Forms;
using System.Threading;
using OsEngine.Logging;

namespace OsEngine.OsTrader.Panels.Tab
{
    /// <summary>
    /// Interaction logic for BotTabPolygonUi.xaml
    /// </summary>
    public partial class BotTabPolygonUi : Window
    {
        PolygonToTrade _polygon;

        public string NameElement;

        public BotTabPolygonUi(PolygonToTrade polygon)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            GlobalGUILayout.Listen(this, "botTabPairUi_" + polygon.Name);
            _polygon = polygon;

            NameElement = polygon.Name;

            TextBoxBaseCurrency.Text = _polygon.BaseCurrency;
            TextBoxBaseCurrency.TextChanged += TextBoxBaseCurrency_TextChanged;

            TextBoxSeparatorToSecurities.Text = _polygon.SeparatorToSecurities;
            TextBoxSeparatorToSecurities.TextChanged += TextBoxSeparatorToSecurities_TextChanged;

            ComboBoxComissionType.Items.Add(ComissionPolygonType.None.ToString());
            ComboBoxComissionType.Items.Add(ComissionPolygonType.Percent.ToString());
            ComboBoxComissionType.SelectedItem = _polygon.ComissionType.ToString();
            ComboBoxComissionType.SelectionChanged += ComboBoxComissionType_SelectionChanged;

            TextBoxComissionValue.Text = _polygon.ComissionValue.ToString();
            TextBoxComissionValue.TextChanged += TextBoxComissionValue_TextChanged;

            CheckBoxCommisionIsSubstract.IsChecked = _polygon.CommisionIsSubstract;
            CheckBoxCommisionIsSubstract.Click += CheckBoxCommisionIsSubstract_Click;

            ComboBoxDelayType.Items.Add(DelayPolygonType.ByExecution.ToString());
            ComboBoxDelayType.Items.Add(DelayPolygonType.InMLS.ToString());
            ComboBoxDelayType.Items.Add(DelayPolygonType.Instantly.ToString());
            ComboBoxDelayType.SelectedItem = _polygon.DelayType.ToString();
            ComboBoxDelayType.SelectionChanged += ComboBoxDelayType_SelectionChanged;

            TextBoxDelayMls.Text = _polygon.DelayMls.ToString();
            TextBoxDelayMls.TextChanged += TextBoxDelayMls_TextChanged;

            TextBoxLimitQtyStart.Text = _polygon.QtyStart.ToString();
            TextBoxLimitQtyStart.TextChanged += TextBoxLimitQtyStart_TextChanged;

            TextBoxLimitSlippage.Text = _polygon.SlippagePercent.ToString();
            TextBoxLimitSlippage.TextChanged += TextBoxLimitSlippage_TextChanged;

            TextBoxProfitToSignal.Text = _polygon.ProfitToSignal.ToString();
            TextBoxProfitToSignal.TextChanged += TextBoxProfitToSignal_TextChanged;

            ComboBoxActionOnSignalType.Items.Add(PolygonActionOnSignalType.Bot_Event.ToString());
            ComboBoxActionOnSignalType.Items.Add(PolygonActionOnSignalType.All.ToString());
            ComboBoxActionOnSignalType.Items.Add(PolygonActionOnSignalType.Alert.ToString());
            ComboBoxActionOnSignalType.Items.Add(PolygonActionOnSignalType.None.ToString());

            ComboBoxActionOnSignalType.SelectedItem = _polygon.ActionOnSignalType.ToString();
            ComboBoxActionOnSignalType.SelectionChanged += ComboBoxActionOnSignalType_SelectionChanged;

            ComboBoxOrderPriceType.Items.Add(OrderPriceType.Limit.ToString());
            ComboBoxOrderPriceType.Items.Add(OrderPriceType.Market.ToString());
            ComboBoxOrderPriceType.SelectedItem = _polygon.OrderPriceType.ToString();
            ComboBoxOrderPriceType.SelectionChanged += ComboBoxOrderPriceType_SelectionChanged;

            PaintSecNames();

            // Localization

            Title = OsLocalization.Trader.Label313;

            LabelStartSecutiySettings.Content = OsLocalization.Trader.Label315;
            LabelComissionSettings.Content = OsLocalization.Trader.Label316;
            LabelBaseCurrency.Content = OsLocalization.Trader.Label317;
            LabelSeparator.Content = OsLocalization.Trader.Label319;
            LabelComissionType.Content = OsLocalization.Trader.Label320;
            LabelComissionValue.Content = OsLocalization.Trader.Label321;
            CheckBoxCommisionIsSubstract.Content = OsLocalization.Trader.Label322;

            LabelQtyStartLimit.Content = OsLocalization.Trader.Label325;
            LabelSlippageLimit.Content = OsLocalization.Trader.Label326;
            ButtonBuyLimit.Content = OsLocalization.Trader.Label311;

            LabelExecutionSettings.Content = OsLocalization.Trader.Label329;
            LabelDelay.Content = OsLocalization.Trader.Label330;
            LabelInterval.Content = OsLocalization.Trader.Label331;
            LabelLog.Content = OsLocalization.Trader.Label332;

            LabelProfitToSignal.Content = OsLocalization.Trader.Label335;
            LabelActionOnSignalType.Content = OsLocalization.Trader.Label336;

            LabelExecution.Content = OsLocalization.Trader.Label337;
            LabelOrderPriceType.Content = OsLocalization.Trader.Label338;

            _marketDepthPainter1 = new MarketDepthPainter(_polygon.Tab1.TabName + "Ui");
            _marketDepthPainter1.ProcessMarketDepth(_polygon.Tab1.MarketDepth);
            _marketDepthPainter1.StartPaint(HostSec1, null, null);
            _polygon.Tab1.MarketDepthUpdateEvent += Tab1_MarketDepthUpdateEvent;

            _marketDepthPainter2 = new MarketDepthPainter(_polygon.Tab2.TabName + "Ui");
            _marketDepthPainter2.ProcessMarketDepth(_polygon.Tab2.MarketDepth);
            _marketDepthPainter2.StartPaint(HostSec2, null, null);
            _polygon.Tab2.MarketDepthUpdateEvent += Tab2_MarketDepthUpdateEvent;

            _marketDepthPainter3 = new MarketDepthPainter(_polygon.Tab3.TabName + "Ui");
            _marketDepthPainter3.ProcessMarketDepth(_polygon.Tab3.MarketDepth);
            _marketDepthPainter3.StartPaint(HostSec3, null, null);
            _polygon.Tab3.MarketDepthUpdateEvent += Tab3_MarketDepthUpdateEvent;

            CreateGrid();

            this.Closed += BotTabPolygonUi_Closed;

            Thread painterThread = new Thread(PainterThread);
            painterThread.Start();

            if (_polygon.ShowTradePanelOnChart == false)
            {
                ButtonHideShowRightPanel_Click(null, null);
            }

            ButtonHideShowRightPanel.Click += ButtonHideShowRightPanel_Click;
            ButtonBuyLimit.Click += ButtonBuyLimit_Click;
            ButtonSec1.Click += ButtonSec1_Click;
            ButtonSec2.Click += ButtonSec2_Click;
            ButtonSec3.Click += ButtonSec3_Click;

            _polygon.StartPaintLog(HostLog);
        }

        private void BotTabPolygonUi_Closed(object sender, EventArgs e)
        {
            _uiClosed = true;

            ButtonHideShowRightPanel.Click -= ButtonHideShowRightPanel_Click;
            ButtonBuyLimit.Click -= ButtonBuyLimit_Click;
            ButtonSec1.Click -= ButtonSec1_Click;
            ButtonSec2.Click -= ButtonSec2_Click;
            ButtonSec3.Click -= ButtonSec3_Click;

            TextBoxBaseCurrency.TextChanged -= TextBoxBaseCurrency_TextChanged;
            TextBoxSeparatorToSecurities.TextChanged -= TextBoxSeparatorToSecurities_TextChanged;
            ComboBoxComissionType.SelectionChanged -= ComboBoxComissionType_SelectionChanged;
            TextBoxComissionValue.TextChanged -= TextBoxComissionValue_TextChanged;
            CheckBoxCommisionIsSubstract.Click -= CheckBoxCommisionIsSubstract_Click;
            ComboBoxDelayType.SelectionChanged -= ComboBoxDelayType_SelectionChanged;
            TextBoxDelayMls.TextChanged -= TextBoxDelayMls_TextChanged;
            TextBoxLimitQtyStart.TextChanged -= TextBoxLimitQtyStart_TextChanged;
            TextBoxLimitSlippage.TextChanged -= TextBoxLimitSlippage_TextChanged;
            TextBoxProfitToSignal.TextChanged -= TextBoxProfitToSignal_TextChanged;
            ComboBoxActionOnSignalType.SelectionChanged -= ComboBoxActionOnSignalType_SelectionChanged;
            ComboBoxOrderPriceType.SelectionChanged -= ComboBoxOrderPriceType_SelectionChanged;

            if (_marketDepthPainter1 != null)
            {
                _marketDepthPainter1.Delete();
                _marketDepthPainter1 = null;
            }

            if (_marketDepthPainter2 != null)
            {
                _marketDepthPainter2.Delete();
                _marketDepthPainter2 = null;
            }

            if (_marketDepthPainter3 != null)
            {
                _marketDepthPainter3.Delete();
                _marketDepthPainter3 = null;
            }

            _polygon.Tab1.MarketDepthUpdateEvent -= Tab1_MarketDepthUpdateEvent;
            _polygon.Tab2.MarketDepthUpdateEvent -= Tab2_MarketDepthUpdateEvent;
            _polygon.Tab3.MarketDepthUpdateEvent -= Tab3_MarketDepthUpdateEvent;
            _polygon.StopPaintLog();
            _polygon = null;

            DataGridFactory.ClearLinks(_grid);
            _grid.Rows.Clear();
            _grid.Columns.Clear();
            _grid = null;
            HostSequence.Child = null;

            HostLog.Child = null;
            HostSec1.Child = null;
            HostSec2.Child = null;
            HostSec3.Child = null;
        }

        private void Tab1_MarketDepthUpdateEvent(MarketDepth md)
        {
            if (_uiClosed)
            {
                return;
            }
            _marketDepthPainter1.ProcessMarketDepth(md);
        }

        private void Tab3_MarketDepthUpdateEvent(MarketDepth md)
        {
            if (_uiClosed)
            {
                return;
            }
            _marketDepthPainter3.ProcessMarketDepth(md);
        }

        private void Tab2_MarketDepthUpdateEvent(MarketDepth md)
        {
            if (_uiClosed)
            {
                return;
            }
            _marketDepthPainter2.ProcessMarketDepth(md);
        }

        private void PaintSecNames()
        {
            if (string.IsNullOrEmpty(_polygon.Tab1.Connector.SecurityName) == false)
            {
                ButtonSec1.Content = _polygon.Tab1.Connector.SecurityName + "  " + _polygon.Tab1TradeSide;
            }
            else
            {
                ButtonSec1.Content = OsLocalization.Trader.Label314;
            }

            if (string.IsNullOrEmpty(_polygon.Tab2.Connector.SecurityName) == false)
            {
                ButtonSec2.Content = _polygon.Tab2.Connector.SecurityName + "  " + _polygon.Tab2TradeSide;
            }
            else
            {
                ButtonSec2.Content = OsLocalization.Trader.Label314;
            }

            if (string.IsNullOrEmpty(_polygon.Tab3.Connector.SecurityName) == false)
            {
                ButtonSec3.Content = _polygon.Tab3.Connector.SecurityName + "  " + _polygon.Tab3TradeSide;
            }
            else
            {
                ButtonSec3.Content = OsLocalization.Trader.Label314;
            }
        }

        private bool _uiClosed;

        MarketDepthPainter _marketDepthPainter1;

        MarketDepthPainter _marketDepthPainter2;

        MarketDepthPainter _marketDepthPainter3;

        private void ComboBoxOrderPriceType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxOrderPriceType.SelectedItem.ToString(), out _polygon.OrderPriceType);
                _polygon.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void ComboBoxActionOnSignalType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxActionOnSignalType.SelectedItem.ToString(), out _polygon.ActionOnSignalType);
                _polygon.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxProfitToSignal_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _polygon.ProfitToSignal = TextBoxProfitToSignal.Text.ToString().ToDecimal();
                _polygon.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxLimitSlippage_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _polygon.SlippagePercent = TextBoxLimitSlippage.Text.ToString().ToDecimal();
                _polygon.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxLimitQtyStart_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _polygon.QtyStart = TextBoxLimitQtyStart.Text.ToString().ToDecimal();
                _polygon.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxDelayMls_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _polygon.DelayMls = Convert.ToInt32(TextBoxDelayMls.Text.ToString());
                _polygon.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void ComboBoxDelayType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxDelayType.SelectedItem.ToString(), out _polygon.DelayType);
                _polygon.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxCommisionIsSubstract_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _polygon.CommisionIsSubstract = CheckBoxCommisionIsSubstract.IsChecked.Value;
                _polygon.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxComissionValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _polygon.ComissionValue = TextBoxComissionValue.Text.ToString().ToDecimal();
                _polygon.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void ComboBoxComissionType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxComissionType.SelectedItem.ToString(), out _polygon.ComissionType);
                _polygon.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxSeparatorToSecurities_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _polygon.SeparatorToSecurities = TextBoxSeparatorToSecurities.Text;
                _polygon.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxBaseCurrency_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _polygon.BaseCurrency = TextBoxBaseCurrency.Text;
                _polygon.Save();

                _polygon.CheckSequence();

                PaintSecNames();
            }
            catch
            {
                // ignore
            }
        }

        private void ButtonSec1_Click(object sender, RoutedEventArgs e)
        {
            string baseCurrency = _polygon.BaseCurrency;

            if (string.IsNullOrEmpty(baseCurrency))
            {
                CustomMessageBoxUi messageUi = new CustomMessageBoxUi(OsLocalization.Trader.Label333);
                messageUi.ShowDialog();
                return;
            }

            BotTabPoligonSecurityAddUi ui
                = new BotTabPoligonSecurityAddUi(_polygon.Tab1.Connector, baseCurrency, _polygon.Tab1TradeSide);
            ui.ShowDialog();

            List<IServer> servers = ServerMaster.GetServers();

            if (servers == null ||
                servers.Count == 0)
            {
                return;
            }

            if (_polygon.Tab1.Connector.SecurityName != null)
            {
                _polygon.Tab1TradeSide = ui.OperationSide;
                ButtonSec1.Content = _polygon.Tab1.Connector.SecurityName + "  " + _polygon.Tab1TradeSide;
                _polygon.Save();

                _polygon.CheckSequence();
                PaintSecNames();
            }

        }

        private void ButtonSec2_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_polygon.Tab1.Connector.SecurityName))
            {
                CustomMessageBoxUi messageUi = new CustomMessageBoxUi(OsLocalization.Trader.Label334);
                messageUi.ShowDialog();
                return;
            }

            string lastCurrency = _polygon.Tab1.Connector.SecurityName.ToLower().Replace(".txt", "");

            lastCurrency = lastCurrency.Replace(_polygon.BaseCurrency.ToLower(), "");

            Side side = Side.Buy;

            if (_polygon.Tab1TradeSide == Side.Buy)
            {
                side = Side.Sell;
            }

            List<IServer> servers = ServerMaster.GetServers();

            if (servers == null ||
                servers.Count == 0)
            {
                return;
            }

            BotTabPoligonSecurityAddUi ui
    = new BotTabPoligonSecurityAddUi(_polygon.Tab2.Connector, lastCurrency, side);
            ui.ShowDialog();

            if (_polygon.Tab2.Connector.SecurityName != null)
            {
                _polygon.Tab2TradeSide = ui.OperationSide;
                ButtonSec2.Content = _polygon.Tab2.Connector.SecurityName + "  " + _polygon.Tab2TradeSide;
                _polygon.Save();

                _polygon.CheckSequence();
                PaintSecNames();

            }
        }

        private void ButtonSec3_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_polygon.Tab2.Connector.SecurityName))
            {
                CustomMessageBoxUi messageUi = new CustomMessageBoxUi(OsLocalization.Trader.Label334);
                messageUi.ShowDialog();
                return;
            }

            string lastCurrency = _polygon.EndCurrencyTab2;

            Side side = Side.Buy;

            if (_polygon.Tab2TradeSide == Side.Buy)
            {
                side = Side.Sell;
            }

            List<IServer> servers = ServerMaster.GetServers();

            if (servers == null ||
                servers.Count == 0)
            {
                return;
            }

            BotTabPoligonSecurityAddUi ui
    = new BotTabPoligonSecurityAddUi(_polygon.Tab3.Connector, lastCurrency, side);
            ui.ShowDialog();

            if (_polygon.Tab3.Connector.SecurityName != null)
            {
                _polygon.Tab3TradeSide = ui.OperationSide;
                ButtonSec3.Content = _polygon.Tab3.Connector.SecurityName + "  " + _polygon.Tab3TradeSide;
                _polygon.Save();

                _polygon.CheckSequence();
                PaintSecNames();
            }
        }

        // DataGridPaint

        private DataGridView _grid;

        private void CreateGrid()
        {
            DataGridView newGrid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect, DataGridViewAutoSizeRowsMode.AllCells);

            newGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            newGrid.ScrollBars = ScrollBars.Vertical;
            DataGridViewCellStyle style = newGrid.DefaultCellStyle;
            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = style;

            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = OsLocalization.Trader.Label341;  //"Step";
            colum0.ReadOnly = true;
            colum0.Width = 70;
            newGrid.Columns.Add(colum0);

            DataGridViewColumn colum1 = new DataGridViewColumn();
            colum1.CellTemplate = cell0;
            colum1.HeaderText = OsLocalization.Trader.Label342; //"Pair";
            colum1.ReadOnly = true;
            colum1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum1);

            DataGridViewColumn colum2 = new DataGridViewColumn();
            colum2.CellTemplate = cell0;
            colum2.HeaderText = OsLocalization.Trader.Label343; //"Operation";
            colum2.ReadOnly = true;
            colum2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum2);

            DataGridViewColumn colum3 = new DataGridViewColumn();
            colum3.CellTemplate = cell0;
            colum3.HeaderText = OsLocalization.Trader.Label344; //"Bid";
            colum3.ReadOnly = true;
            colum3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum3);

            DataGridViewColumn colum4 = new DataGridViewColumn();
            colum4.CellTemplate = cell0;
            colum4.HeaderText = OsLocalization.Trader.Label345; //"Ask";
            colum4.ReadOnly = true;
            colum4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum4);

            DataGridViewColumn colum5 = new DataGridViewColumn();
            colum5.CellTemplate = cell0;
            colum5.HeaderText = OsLocalization.Trader.Label346; //"Currency start";
            colum5.ReadOnly = true;
            colum5.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum5);

            DataGridViewColumn colum6 = new DataGridViewColumn();
            colum6.CellTemplate = cell0;
            colum6.HeaderText = OsLocalization.Trader.Label347; //"Qty start";
            colum6.ReadOnly = true;
            colum6.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum6);

            DataGridViewColumn colum7 = new DataGridViewColumn();
            colum7.CellTemplate = cell0;
            colum7.HeaderText = OsLocalization.Trader.Label348; //"Currency end";
            colum7.ReadOnly = true;
            colum7.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum7);

            DataGridViewColumn colum8 = new DataGridViewColumn();
            colum8.CellTemplate = cell0;
            colum8.HeaderText = OsLocalization.Trader.Label349;//"Qty end";
            colum8.ReadOnly = true;
            colum8.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum8);

            _grid = newGrid;

            HostSequence.Child = _grid;
        }

        private void PainterThread()
        {
            while (true)
            {
                Thread.Sleep(1000);
                if (_uiClosed)
                {
                    return;
                }

                if (MainWindow.ProccesIsWorked == false)
                {
                    return;
                }

                PaintGrid();
            }
        }

        private void PaintGrid()
        {
            try
            {
                List<DataGridViewRow> rows = GetRowsToGrid();

                if (rows == null)
                {
                    return;
                }

                if (rows.Count != _grid.Rows.Count)
                {// 1 кол-во строк изменилось - перерисовываем полностью
                    RePaintGrid();
                    return;
                }

                if (_grid.Rows.Count == 0)
                {
                    return;
                }

                DataGridViewRow firstOldRow = _grid.Rows[0];

                for (int i = 0; i < rows.Count; i++)
                {
                    TryRePaintRow(_grid.Rows[i], rows[i]);
                }
            }
            catch (Exception ex)
            {
                // ignore
            }
        }

        /// <summary>
        /// Redraw the row in the table
        /// </summary>
        private void TryRePaintRow(DataGridViewRow rowInGrid, DataGridViewRow rowInArray)
        {
            try
            {
                if (_grid.InvokeRequired)
                {
                    _grid?.Invoke(new Action<DataGridViewRow, DataGridViewRow>(TryRePaintRow), rowInGrid, rowInArray);
                    return;
                }

                if (rowInGrid.Cells[1].Value != null &&
                    rowInGrid.Cells[1].Value.ToString() != rowInArray.Cells[1].Value.ToString())
                {
                    rowInGrid.Cells[1].Value = rowInArray.Cells[1].Value.ToString();
                }

                if (rowInGrid.Cells[2].Value != null &&
                    rowInGrid.Cells[2].Value.ToString() != rowInArray.Cells[2].Value.ToString())
                {
                    rowInGrid.Cells[2].Value = rowInArray.Cells[2].Value.ToString();
                }

                if (rowInGrid.Cells[3].Value != null &&
                    rowInGrid.Cells[3].Value.ToString() != rowInArray.Cells[3].Value.ToString())
                {
                    rowInGrid.Cells[3].Value = rowInArray.Cells[3].Value.ToString();
                }

                if (rowInGrid.Cells[4].Value != null &&
                    rowInGrid.Cells[4].Value.ToString() != rowInArray.Cells[4].Value.ToString())
                {
                    rowInGrid.Cells[4].Value = rowInArray.Cells[4].Value.ToString();
                }

                if (rowInGrid.Cells[5].Value != null &&
                    rowInGrid.Cells[5].Value.ToString() != rowInArray.Cells[5].Value.ToString())
                {
                    rowInGrid.Cells[5].Value = rowInArray.Cells[5].Value.ToString();
                }

                if (rowInGrid.Cells[6].Value != null &&
                    rowInGrid.Cells[6].Value.ToString() != rowInArray.Cells[6].Value.ToString())
                {
                    rowInGrid.Cells[6].Value = rowInArray.Cells[6].Value.ToString();
                }

                if (rowInGrid.Cells[7].Value != null &&
                    rowInGrid.Cells[7].Value.ToString() != rowInArray.Cells[7].Value.ToString())
                {
                    rowInGrid.Cells[7].Value = rowInArray.Cells[7].Value.ToString();
                }

                if (rowInGrid.Cells[8].Value != null &&
                    rowInGrid.Cells[8].Value.ToString() != rowInArray.Cells[8].Value.ToString())
                {
                    rowInGrid.Cells[8].Value = rowInArray.Cells[8].Value.ToString();
                }
            }
            catch (Exception error)
            {

            }
        }

        /// <summary>
        /// The method of full redrawing of the table with pairs
        /// </summary>
        private void RePaintGrid()
        {
            try
            {
                if (_grid.InvokeRequired)
                {
                    _grid.Invoke(new Action(RePaintGrid));
                    return;
                }

                _grid.Rows.Clear();

                List<DataGridViewRow> rows = GetRowsToGrid();

                if (rows == null)
                {
                    return;
                }

                for (int i = 0; i < rows.Count; i++)
                {
                    _grid.Rows.Add(rows[i]);
                }
            }
            catch (Exception error)
            {
                _polygon.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Calculate all rows from the table of pairs
        /// </summary>
        private List<DataGridViewRow> GetRowsToGrid()
        {
            try
            {
                List<DataGridViewRow> rows = new List<DataGridViewRow>();

                if (_polygon.BaseCurrency == null)
                {
                    return null;
                }

                string firstCurrency = _polygon.BaseCurrency.ToLower();
                string endCurrency = "";


                if (_polygon.Tab1.Connector.IsConnected)
                {
                    DataGridViewRow row1 =
                        GetTabRow(1, _polygon.Tab1.Connector.SecurityName, _polygon.Tab1TradeSide,
                        _polygon.Tab1.PriceBestBid, _polygon.Tab1.PriceBestAsk,
                        _polygon.BaseCurrency.ToLower(), _polygon.QtyStart,
                        _polygon.EndCurrencyTab1, _polygon.EndQtyTab1);
                    rows.Add(row1);

                    endCurrency = _polygon.EndCurrencyTab1;
                }

                if (_polygon.Tab2.Connector.IsConnected)
                {
                    DataGridViewRow row2 =
                    GetTabRow(2, _polygon.Tab2.Connector.SecurityName, _polygon.Tab2TradeSide,
                    _polygon.Tab2.PriceBestBid, _polygon.Tab2.PriceBestAsk,
                    _polygon.EndCurrencyTab1, _polygon.EndQtyTab1,
                    _polygon.EndCurrencyTab2, _polygon.EndQtyTab2);
                    rows.Add(row2);
                    endCurrency = _polygon.EndCurrencyTab2;
                }

                if (_polygon.Tab3.Connector.IsConnected)
                {
                    DataGridViewRow row3 =
                   GetTabRow(3, _polygon.Tab3.Connector.SecurityName, _polygon.Tab3TradeSide,
                  _polygon.Tab3.PriceBestBid, _polygon.Tab3.PriceBestAsk,
                  _polygon.EndCurrencyTab2, _polygon.EndQtyTab2,
                  _polygon.EndCurrencyTab3, _polygon.EndQtyTab3);
                    rows.Add(row3);
                    endCurrency = _polygon.EndCurrencyTab3;
                }

                if (firstCurrency == endCurrency)
                {
                    DataGridViewRow row5 = GetRowProfitAbs();
                    rows.Add(row5);

                    DataGridViewRow row6 = GetRowProfitPercent();
                    rows.Add(row6);
                }

                return rows;
            }
            catch (Exception error)
            {
                _polygon.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        private DataGridViewRow GetTabRow(
            int step, string pairName, Side operation, decimal sell, decimal buy,
            string currencyOnStart, decimal qtyStart, string currencyEnd, decimal qtyEnd)
        {
            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[nRow.Cells.Count - 1].Value = step.ToString();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[nRow.Cells.Count - 1].Value = pairName;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[nRow.Cells.Count - 1].Value = operation.ToString();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[nRow.Cells.Count - 1].Value = sell.ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[nRow.Cells.Count - 1].Value = buy.ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[nRow.Cells.Count - 1].Value = currencyOnStart;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[nRow.Cells.Count - 1].Value = qtyStart.ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[nRow.Cells.Count - 1].Value = currencyEnd;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[nRow.Cells.Count - 1].Value = qtyEnd.ToStringWithNoEndZero();

            return nRow;
        }

        private DataGridViewRow GetRowProfitAbs()
        {
            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[nRow.Cells.Count - 1].Value = OsLocalization.Trader.Label339;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[nRow.Cells.Count - 1].Value = _polygon.ProfitToDealAbs;

            return nRow;
        }

        private DataGridViewRow GetRowProfitPercent()
        {
            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[nRow.Cells.Count - 1].Value = OsLocalization.Trader.Label340;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[nRow.Cells.Count - 1].Value = _polygon.ProfitToDealPercent;

            return nRow;
        }

        private void ButtonHideShowRightPanel_Click(object sender, RoutedEventArgs e)
        {
            bool showTradePanel = false;

            if (GridTradePanel.Width == 0)
            {
                GridTradePanel.Visibility = Visibility.Visible;
                GridLog.Width = 600;
                GridTradePanel.Width = 600;
                ButtonHideShowRightPanel.Content = ">";
                GreedChartPanel.Margin = new Thickness(0, 0, 600, 0);
                MinWidth = 700;
                showTradePanel = true;
            }
            else
            {
                GridTradePanel.Visibility = Visibility.Hidden;
                GridLog.Width = 0;
                GridTradePanel.Width = 0;
                ButtonHideShowRightPanel.Content = "<";
                GreedChartPanel.Margin = new Thickness(0, 0, 15, 0);
                MinWidth = 200;
            }

            if (sender != null)
            {
                _polygon.ShowTradePanelOnChart = showTradePanel;
            }
        }

        private void ButtonBuyLimit_Click(object sender, RoutedEventArgs e)
        {
            Thread worker = new Thread(_polygon.TradeLogic);
            worker.Start();
        }
    }
}