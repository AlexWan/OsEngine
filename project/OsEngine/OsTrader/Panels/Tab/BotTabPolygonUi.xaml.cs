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
        public PolygonToTrade Polygon;

        public string NameElement;

        public BotTabPolygonUi(PolygonToTrade polygon)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            GlobalGUILayout.Listen(this, "botTabPairUi_" + polygon.Name);
            Polygon = polygon;

            NameElement = polygon.Name;

            TextBoxBaseCurrency.Text = Polygon.BaseCurrency;
            TextBoxBaseCurrency.TextChanged += TextBoxBaseCurrency_TextChanged;

            TextBoxSeparatorToSecurities.Text = Polygon.SeparatorToSecurities;
            TextBoxSeparatorToSecurities.TextChanged += TextBoxSeparatorToSecurities_TextChanged;

            ComboBoxCommissionType.Items.Add(CommissionPolygonType.None.ToString());
            ComboBoxCommissionType.Items.Add(CommissionPolygonType.Percent.ToString());
            ComboBoxCommissionType.SelectedItem = Polygon.CommissionType.ToString();
            ComboBoxCommissionType.SelectionChanged += ComboBoxCommissionType_SelectionChanged;

            TextBoxCommissionValue.Text = Polygon.CommissionValue.ToString();
            TextBoxCommissionValue.TextChanged += TextBoxCommissionValue_TextChanged;

            CheckBoxCommisionIsSubstract.IsChecked = Polygon.CommissionIsSubstract;
            CheckBoxCommisionIsSubstract.Click += CheckBoxCommisionIsSubstract_Click;

            ComboBoxDelayType.Items.Add(DelayPolygonType.ByExecution.ToString());
            ComboBoxDelayType.Items.Add(DelayPolygonType.InMLS.ToString());
            ComboBoxDelayType.Items.Add(DelayPolygonType.Instantly.ToString());
            ComboBoxDelayType.SelectedItem = Polygon.DelayType.ToString();
            ComboBoxDelayType.SelectionChanged += ComboBoxDelayType_SelectionChanged;

            TextBoxDelayMls.Text = Polygon.DelayMls.ToString();
            TextBoxDelayMls.TextChanged += TextBoxDelayMls_TextChanged;

            TextBoxLimitQtyStart.Text = Polygon.QtyStart.ToString();
            TextBoxLimitQtyStart.TextChanged += TextBoxLimitQtyStart_TextChanged;

            TextBoxLimitSlippage.Text = Polygon.SlippagePercent.ToString();
            TextBoxLimitSlippage.TextChanged += TextBoxLimitSlippage_TextChanged;

            TextBoxProfitToSignal.Text = Polygon.ProfitToSignal.ToString();
            TextBoxProfitToSignal.TextChanged += TextBoxProfitToSignal_TextChanged;

            ComboBoxActionOnSignalType.Items.Add(PolygonActionOnSignalType.Bot_Event.ToString());
            ComboBoxActionOnSignalType.Items.Add(PolygonActionOnSignalType.All.ToString());
            ComboBoxActionOnSignalType.Items.Add(PolygonActionOnSignalType.Alert.ToString());
            ComboBoxActionOnSignalType.Items.Add(PolygonActionOnSignalType.None.ToString());

            ComboBoxActionOnSignalType.SelectedItem = Polygon.ActionOnSignalType.ToString();
            ComboBoxActionOnSignalType.SelectionChanged += ComboBoxActionOnSignalType_SelectionChanged;

            ComboBoxOrderPriceType.Items.Add(OrderPriceType.Limit.ToString());
            ComboBoxOrderPriceType.Items.Add(OrderPriceType.Market.ToString());
            ComboBoxOrderPriceType.SelectedItem = Polygon.OrderPriceType.ToString();
            ComboBoxOrderPriceType.SelectionChanged += ComboBoxOrderPriceType_SelectionChanged;

            PaintSecNames();

            // Localization

            Title = OsLocalization.Trader.Label313;

            LabelStartSecutiySettings.Content = OsLocalization.Trader.Label315;
            LabelCommissionSettings.Content = OsLocalization.Trader.Label316;
            LabelBaseCurrency.Content = OsLocalization.Trader.Label317;
            LabelSeparator.Content = OsLocalization.Trader.Label319;
            LabelCommissionType.Content = OsLocalization.Trader.Label320;
            LabelCommissionValue.Content = OsLocalization.Trader.Label321;
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

            _marketDepthPainter1 = new MarketDepthPainter(Polygon.Tab1.TabName + "Ui", Polygon.Tab1.Connector);
            _marketDepthPainter1.ProcessMarketDepth(Polygon.Tab1.MarketDepth);
            _marketDepthPainter1.StartPaint(HostSec1, null, null);
            Polygon.Tab1.MarketDepthUpdateEvent += Tab1_MarketDepthUpdateEvent;

            _marketDepthPainter2 = new MarketDepthPainter(Polygon.Tab2.TabName + "Ui", Polygon.Tab2.Connector);
            _marketDepthPainter2.ProcessMarketDepth(Polygon.Tab2.MarketDepth);
            _marketDepthPainter2.StartPaint(HostSec2, null, null);
            Polygon.Tab2.MarketDepthUpdateEvent += Tab2_MarketDepthUpdateEvent;

            _marketDepthPainter3 = new MarketDepthPainter(Polygon.Tab3.TabName + "Ui", Polygon.Tab3.Connector);
            _marketDepthPainter3.ProcessMarketDepth(Polygon.Tab3.MarketDepth);
            _marketDepthPainter3.StartPaint(HostSec3, null, null);
            Polygon.Tab3.MarketDepthUpdateEvent += Tab3_MarketDepthUpdateEvent;

            CreateGrid();

            this.Closed += BotTabPolygonUi_Closed;

            Thread painterThread = new Thread(PainterThread);
            painterThread.Start();

            if (Polygon.ShowTradePanelOnChart == false)
            {
                ButtonHideShowRightPanel_Click(null, null);
            }

            ButtonHideShowRightPanel.Click += ButtonHideShowRightPanel_Click;
            ButtonBuyLimit.Click += ButtonBuyLimit_Click;
            ButtonSec1.Click += ButtonSec1_Click;
            ButtonSec2.Click += ButtonSec2_Click;
            ButtonSec3.Click += ButtonSec3_Click;

            Polygon.StartPaintLog(HostLog);
        }

        private void BotTabPolygonUi_Closed(object sender, EventArgs e)
        {
            try
            {
                _uiClosed = true;

                ButtonHideShowRightPanel.Click -= ButtonHideShowRightPanel_Click;
                ButtonBuyLimit.Click -= ButtonBuyLimit_Click;
                ButtonSec1.Click -= ButtonSec1_Click;
                ButtonSec2.Click -= ButtonSec2_Click;
                ButtonSec3.Click -= ButtonSec3_Click;

                TextBoxBaseCurrency.TextChanged -= TextBoxBaseCurrency_TextChanged;
                TextBoxSeparatorToSecurities.TextChanged -= TextBoxSeparatorToSecurities_TextChanged;
                ComboBoxCommissionType.SelectionChanged -= ComboBoxCommissionType_SelectionChanged;
                TextBoxCommissionValue.TextChanged -= TextBoxCommissionValue_TextChanged;
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

                if (Polygon.Tab1 != null)
                {
                    Polygon.Tab1.MarketDepthUpdateEvent -= Tab1_MarketDepthUpdateEvent;
                }

                if (Polygon.Tab2 != null)
                {
                    Polygon.Tab2.MarketDepthUpdateEvent -= Tab2_MarketDepthUpdateEvent;
                }

                if (Polygon.Tab3 != null)
                {
                    Polygon.Tab3.MarketDepthUpdateEvent -= Tab3_MarketDepthUpdateEvent;
                }

                Polygon.StopPaintLog();

                Polygon = null;

                if (HostSequence != null)
                {
                    HostSequence.Child = null;
                }

                if (_grid != null)
                {
                    DataGridFactory.ClearLinks(_grid);
                    _grid.Rows.Clear();
                    _grid.Columns.Clear();
                    _grid.DataError -= _grid_DataError;
                    _grid = null;
                }

                HostLog.Child = null;
                HostSec1.Child = null;
                HostSec2.Child = null;
                HostSec3.Child = null;
            }
            catch
            {
                // ignore
            }
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
            try
            {
                if(Polygon.Tab1.Connector == null ||
                    Polygon.Tab2.Connector == null ||
                    Polygon.Tab3.Connector == null)
                {
                    return;
                }

                if (string.IsNullOrEmpty(Polygon.Tab1.Connector.SecurityName) == false)
                {
                    ButtonSec1.Content = Polygon.Tab1.Connector.SecurityName + "  " + Polygon.Tab1TradeSide;
                }
                else
                {
                    ButtonSec1.Content = OsLocalization.Trader.Label314;
                }

                if (string.IsNullOrEmpty(Polygon.Tab2.Connector.SecurityName) == false)
                {
                    ButtonSec2.Content = Polygon.Tab2.Connector.SecurityName + "  " + Polygon.Tab2TradeSide;
                }
                else
                {
                    ButtonSec2.Content = OsLocalization.Trader.Label314;
                }

                if (string.IsNullOrEmpty(Polygon.Tab3.Connector.SecurityName) == false)
                {
                    ButtonSec3.Content = Polygon.Tab3.Connector.SecurityName + "  " + Polygon.Tab3TradeSide;
                }
                else
                {
                    ButtonSec3.Content = OsLocalization.Trader.Label314;
                }
            }
            catch (Exception ex)
            {
                Polygon?.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
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
                Enum.TryParse(ComboBoxOrderPriceType.SelectedItem.ToString(), out Polygon.OrderPriceType);
                Polygon.Save();
            }
            catch (Exception ex)
            {
                Polygon?.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ComboBoxActionOnSignalType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxActionOnSignalType.SelectedItem.ToString(), out Polygon.ActionOnSignalType);
                Polygon.Save();
            }
            catch (Exception ex)
            {
                Polygon?.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void TextBoxProfitToSignal_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                Polygon.ProfitToSignal = TextBoxProfitToSignal.Text.ToString().ToDecimal();
                Polygon.Save();
            }
            catch (Exception ex)
            {
                Polygon?.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void TextBoxLimitSlippage_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                Polygon.SlippagePercent = TextBoxLimitSlippage.Text.ToString().ToDecimal();
                Polygon.Save();
            }
            catch (Exception ex)
            {
                Polygon?.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void TextBoxLimitQtyStart_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                Polygon.QtyStart = TextBoxLimitQtyStart.Text.ToString().ToDecimal();
                Polygon.Save();
            }
            catch (Exception ex)
            {
                Polygon?.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void TextBoxDelayMls_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                Polygon.DelayMls = Convert.ToInt32(TextBoxDelayMls.Text.ToString());
                Polygon.Save();
            }
            catch (Exception ex)
            {
                Polygon?.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ComboBoxDelayType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxDelayType.SelectedItem.ToString(), out Polygon.DelayType);
                Polygon.Save();
            }
            catch (Exception ex)
            {
                Polygon?.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void CheckBoxCommisionIsSubstract_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Polygon.CommissionIsSubstract = CheckBoxCommisionIsSubstract.IsChecked.Value;
                Polygon.Save();
            }
            catch (Exception ex)
            {
                Polygon?.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void TextBoxCommissionValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                Polygon.CommissionValue = TextBoxCommissionValue.Text.ToString().ToDecimal();
                Polygon.Save();
            }
            catch (Exception ex)
            {
                Polygon?.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ComboBoxCommissionType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxCommissionType.SelectedItem.ToString(), out Polygon.CommissionType);
                Polygon.Save();
            }
            catch (Exception ex)
            {
                Polygon?.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void TextBoxSeparatorToSecurities_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                Polygon.SeparatorToSecurities = TextBoxSeparatorToSecurities.Text;
                Polygon.Save();
            }
            catch (Exception ex)
            {
                Polygon?.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void TextBoxBaseCurrency_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                Polygon.BaseCurrency = TextBoxBaseCurrency.Text;
                Polygon.Save();

                Polygon.CheckSequence();

                PaintSecNames();
            }
            catch (Exception ex) 
            {
                Polygon?.SendNewLogMessage(ex.ToString(),LogMessageType.Error);
            }
        }

        private void ButtonSec1_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string baseCurrency = Polygon.BaseCurrency;

                if (string.IsNullOrEmpty(baseCurrency))
                {
                    CustomMessageBoxUi messageUi = new CustomMessageBoxUi(OsLocalization.Trader.Label333);
                    messageUi.ShowDialog();
                    return;
                }

                BotTabPoligonSecurityAddUi ui
                    = new BotTabPoligonSecurityAddUi(Polygon.Tab1.Connector, baseCurrency, Polygon.Tab1TradeSide);
                ui.LogMessageEvent += SendNewLogMessage;
                ui.ShowDialog();
                ui.LogMessageEvent -= SendNewLogMessage;

                List<IServer> servers = ServerMaster.GetServers();

                if (servers == null ||
                    servers.Count == 0)
                {
                    return;
                }

                if (Polygon.Tab1.Connector.SecurityName != null)
                {
                    Polygon.Tab1TradeSide = ui.OperationSide;
                    ButtonSec1.Content = Polygon.Tab1.Connector.SecurityName + "  " + Polygon.Tab1TradeSide;
                    Polygon.Save();

                    Polygon.CheckSequence();
                    PaintSecNames();
                }
            }
            catch (Exception ex)
            {
                Polygon?.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonSec2_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(Polygon.Tab1.Connector.SecurityName))
                {
                    CustomMessageBoxUi messageUi = new CustomMessageBoxUi(OsLocalization.Trader.Label334);
                    messageUi.ShowDialog();
                    return;
                }

                string lastCurrency = Polygon.Tab1.Connector.SecurityName.ToLower().Replace(".txt", "");

                lastCurrency = lastCurrency.Replace(Polygon.BaseCurrency.ToLower(), "");

                if (string.IsNullOrEmpty(Polygon.SeparatorToSecurities) == false)
                {
                    lastCurrency = lastCurrency.Replace(Polygon.SeparatorToSecurities, "");
                }

                Side side = Side.Buy;

                if (Polygon.Tab1TradeSide == Side.Buy)
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
        = new BotTabPoligonSecurityAddUi(Polygon.Tab2.Connector, lastCurrency, side);

                ui.LogMessageEvent += SendNewLogMessage;
                ui.ShowDialog();
                ui.LogMessageEvent -= SendNewLogMessage;

                if (Polygon.Tab2.Connector.SecurityName != null)
                {
                    Polygon.Tab2TradeSide = ui.OperationSide;
                    ButtonSec2.Content = Polygon.Tab2.Connector.SecurityName + "  " + Polygon.Tab2TradeSide;
                    Polygon.Save();

                    Polygon.CheckSequence();
                    PaintSecNames();

                }
            }
            catch (Exception ex)
            {
                Polygon?.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonSec3_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(Polygon.Tab2.Connector.SecurityName))
                {
                    CustomMessageBoxUi messageUi = new CustomMessageBoxUi(OsLocalization.Trader.Label334);
                    messageUi.ShowDialog();
                    return;
                }

                string lastCurrency = Polygon.EndCurrencyTab2;

                Side side = Side.Buy;

                if (Polygon.Tab2TradeSide == Side.Buy)
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
        = new BotTabPoligonSecurityAddUi(Polygon.Tab3.Connector, lastCurrency, side);

                ui.LogMessageEvent += SendNewLogMessage;
                ui.ShowDialog();
                ui.LogMessageEvent -= SendNewLogMessage;

                if (Polygon.Tab3.Connector.SecurityName != null)
                {
                    Polygon.Tab3TradeSide = ui.OperationSide;
                    ButtonSec3.Content = Polygon.Tab3.Connector.SecurityName + "  " + Polygon.Tab3TradeSide;
                    Polygon.Save();

                    Polygon.CheckSequence();
                    PaintSecNames();
                }
            }
            catch (Exception ex)
            {
                Polygon?.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonHideShowRightPanel_Click(object sender, RoutedEventArgs e)
        {
            try
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
                    Polygon.ShowTradePanelOnChart = showTradePanel;
                }
            }
            catch (Exception ex)
            {
                Polygon?.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonBuyLimit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Thread worker = new Thread(Polygon.TradeLogic);
                worker.Start();
            }
            catch (Exception ex)
            {
                Polygon?.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
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

            _grid.DataError += _grid_DataError;
        }

        private void _grid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            Polygon.SendNewLogMessage(e.ToString(), LogMessageType.Error);
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
                if(_grid == null)
                {
                    return;
                }

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
            catch
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
                if (_grid == null)
                {
                    return;
                }

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
            catch
            {
                // ignore
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
                Polygon.SendNewLogMessage(error.ToString(), LogMessageType.Error);
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

                if (Polygon.BaseCurrency == null)
                {
                    return null;
                }

                string firstCurrency = Polygon.BaseCurrency.ToLower();
                string endCurrency = "";


                if (Polygon.Tab1.Connector != null &&
                    Polygon.Tab1.Connector.IsConnected)
                {
                    DataGridViewRow row1 =
                        GetTabRow(1, Polygon.Tab1.Connector.SecurityName, Polygon.Tab1TradeSide,
                        Polygon.Tab1.PriceBestBid, Polygon.Tab1.PriceBestAsk,
                        Polygon.BaseCurrency.ToLower(), Polygon.QtyStart,
                        Polygon.EndCurrencyTab1, Polygon.EndQtyTab1);
                    rows.Add(row1);

                    endCurrency = Polygon.EndCurrencyTab1;
                }

                if (Polygon.Tab2.Connector != null &&
                    Polygon.Tab2.Connector.IsConnected)
                {
                    DataGridViewRow row2 =
                    GetTabRow(2, Polygon.Tab2.Connector.SecurityName, Polygon.Tab2TradeSide,
                    Polygon.Tab2.PriceBestBid, Polygon.Tab2.PriceBestAsk,
                    Polygon.EndCurrencyTab1, Polygon.EndQtyTab1,
                    Polygon.EndCurrencyTab2, Polygon.EndQtyTab2);
                    rows.Add(row2);
                    endCurrency = Polygon.EndCurrencyTab2;
                }

                if (Polygon.Tab3.Connector != null &&
                    Polygon.Tab3.Connector.IsConnected)
                {
                    DataGridViewRow row3 =
                   GetTabRow(3, Polygon.Tab3.Connector.SecurityName, Polygon.Tab3TradeSide,
                  Polygon.Tab3.PriceBestBid, Polygon.Tab3.PriceBestAsk,
                  Polygon.EndCurrencyTab2, Polygon.EndQtyTab2,
                  Polygon.EndCurrencyTab3, Polygon.EndQtyTab3);
                    rows.Add(row3);
                    endCurrency = Polygon.EndCurrencyTab3;
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
                Polygon.SendNewLogMessage(error.ToString(), LogMessageType.Error);
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
            nRow.Cells[nRow.Cells.Count - 1].Value = Polygon.ProfitToDealAbs;

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
            nRow.Cells[nRow.Cells.Count - 1].Value = Polygon.ProfitToDealPercent;

            return nRow;
        }

        #region Logging

        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion

    }
}