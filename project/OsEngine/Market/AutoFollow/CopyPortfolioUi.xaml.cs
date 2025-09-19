/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Journal;
using OsEngine.Language;
using OsEngine.Layout;
using OsEngine.Market.Servers;
using OsEngine.OsData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;

namespace OsEngine.Market.AutoFollow
{
    public partial class CopyPortfolioUi : Window
    {
        private PortfolioToCopy _portfolioToCopy;

        private CopyTrader _copyTrader;

        public string UniqueName;

        public CopyPortfolioUi(PortfolioToCopy portfolioToCopy, CopyTrader copyTrader)
        {
            InitializeComponent();

            OsEngine.Layout.StickyBorders.Listen(this);
            GlobalGUILayout.Listen(this, "copyPortfolioUi " + portfolioToCopy.NameUnique);

            _portfolioToCopy = portfolioToCopy;
            _portfolioToCopy.MyJournal.StartPaint(HostActivePoses, HostHistoricalPoses);
            _copyTrader = copyTrader;

            _portfolioToCopy.LogCopyTrader.StartPaint(HostLog);
            
            UniqueName = _portfolioToCopy.NameUnique;

            ComboBoxIsOn.Items.Add(true.ToString());
            ComboBoxIsOn.Items.Add(false.ToString());
            ComboBoxIsOn.SelectedItem = _portfolioToCopy.IsOn.ToString();
            ComboBoxIsOn.SelectionChanged += ComboBoxIsOn_SelectionChanged;

            ComboBoxOrderType.Items.Add(CopyTraderOrdersType.Market.ToString());
            ComboBoxOrderType.Items.Add(CopyTraderOrdersType.Iceberg.ToString());
            ComboBoxOrderType.SelectedItem = _portfolioToCopy.OrderType.ToString();
            ComboBoxOrderType.SelectionChanged += ComboBoxOrderType_SelectionChanged;

            for(int i = 0; i < 31; i++)
            {
                ComboBoxIcebergCount.Items.Add(i.ToString());
            }
            ComboBoxIcebergCount.SelectedItem = _portfolioToCopy.IcebergCount.ToString();
            ComboBoxIcebergCount.SelectionChanged += ComboBoxIcebergCount_SelectionChanged;

            TextBoxIcebergMillisecondsDelay.Text = _portfolioToCopy.IcebergMillisecondsDelay.ToString();
            TextBoxIcebergMillisecondsDelay.TextChanged += TextBoxIcebergMillisecondsDelay_TextChanged;

            TextBoxMinCurrencyQty.Text = _portfolioToCopy.MinCurrencyQty.ToString();
            TextBoxMinCurrencyQty.TextChanged += TextBoxMinCurrencyQty_TextChanged;

            ComboBoxVolumeType.Items.Add(CopyTraderVolumeType.DepoProportional.ToString());
            ComboBoxVolumeType.Items.Add(CopyTraderVolumeType.Simple.ToString());
            ComboBoxVolumeType.SelectedItem = _portfolioToCopy.VolumeType.ToString();
            ComboBoxVolumeType.SelectionChanged += ComboBoxVolumeType_SelectionChanged;

            TextBoxVolumeMult.Text = _portfolioToCopy.VolumeMult.ToString();
            TextBoxVolumeMult.TextChanged += TextBoxVolumeMult_TextChanged;

            TextBoxMasterAsset.Text = _portfolioToCopy.MasterAsset.ToString();
            TextBoxMasterAsset.TextChanged += TextBoxMasterAsset_TextChanged;

            TextBoxSlaveAsset.Text = _portfolioToCopy.SlaveAsset.ToString();
            TextBoxSlaveAsset.TextChanged += TextBoxSlaveAsset_TextChanged;

            CheckBoxFailOpenOrdersReactionIsOn.IsChecked = _portfolioToCopy.FailOpenOrdersReactionIsOn;
            CheckBoxFailOpenOrdersReactionIsOn.Checked += CheckBoxFailOpenOrdersReactionIsOn_Checked;
            CheckBoxFailOpenOrdersReactionIsOn.Unchecked += CheckBoxFailOpenOrdersReactionIsOn_Checked;

            TextBoxFailOpenOrdersCountToReaction.Text = _portfolioToCopy.FailOpenOrdersCountToReaction.ToString();
            TextBoxFailOpenOrdersCountToReaction.TextChanged += TextBoxFailOpenOrdersCountToReaction_TextChanged;

            // localization

            LabelIsOn.Content = OsLocalization.Market.Label182;
            LabelOrderType.Content = OsLocalization.Market.Label217;
            LabelIcebergCount.Content = OsLocalization.Market.Label218;
            LabelIcebergMillisecondsDelay.Content = OsLocalization.Market.Label235;

            LabelMinCurrencyQTY.Content = OsLocalization.Market.Label227;
            LabelVolumeType.Content = OsLocalization.Market.Label212;
            LabelVolumeMult.Content = OsLocalization.Market.Label213;
            LabelMasterAsset.Content = OsLocalization.Market.Label214;
            LabelSlaveAsset.Content = OsLocalization.Market.Label215;
            LabelLog.Content = OsLocalization.Market.Label23;
            LabelSecurities.Content = OsLocalization.Market.Label210;
            ButtonShowJournal.Content = OsLocalization.Market.Label229;
            ((TabItem)TabControlVolumeSettings.Items[0]).Header = OsLocalization.Market.Label230;
            ((TabItem)TabControlVolumeSettings.Items[1]).Header = OsLocalization.Market.Label231;

            CheckBoxFailOpenOrdersReactionIsOn.Content = OsLocalization.Market.Label234;
            LabelFailOpenOrdersCountToReaction.Content = OsLocalization.Market.Label232;
            LabelFailOpenOrdersCountFact.Content = OsLocalization.Market.Label233;

            LabelJournalGrid.Content = OsLocalization.Market.Label211;

            TabActivePos.Header = OsLocalization.Trader.Label187;
            TabHistoricalPos.Header = OsLocalization.Trader.Label188;

            ButtonDeleteAllPositions.Content = OsLocalization.Market.Label239;
            ButtonCLoseAllPositions.Content = OsLocalization.Market.Label240;
            ButtonNonTradePeriods.Content = OsLocalization.Market.Label241;

            Title = OsLocalization.Market.Label201 + " # " + _copyTrader.Number
                + " " + OsLocalization.Market.Label219 +": " + portfolioToCopy.ServerName
                + " " + OsLocalization.Market.Label140 + ": " + portfolioToCopy.PortfolioName;

            this.Closed += CopyPortfolioUi_Closed;

            CreateSecuritiesGrid();
            UpdateGridSecurities();

            LoadPanelsPositions();

            Thread worker = new Thread(PaintFormThreadArea);
            worker.Start();
        }

        private void CopyPortfolioUi_Closed(object sender, EventArgs e)
        {
            _formIsClosed = true;

            ComboBoxIsOn.SelectionChanged -= ComboBoxIsOn_SelectionChanged;
            ComboBoxOrderType.SelectionChanged -= ComboBoxOrderType_SelectionChanged;
            ComboBoxIcebergCount.SelectionChanged -= ComboBoxIcebergCount_SelectionChanged;
            ComboBoxVolumeType.SelectionChanged -= ComboBoxVolumeType_SelectionChanged;
            TextBoxVolumeMult.TextChanged -= TextBoxVolumeMult_TextChanged;
            TextBoxMasterAsset.TextChanged -= TextBoxMasterAsset_TextChanged;
            TextBoxSlaveAsset.TextChanged -= TextBoxSlaveAsset_TextChanged;
            CheckBoxFailOpenOrdersReactionIsOn.Checked -= CheckBoxFailOpenOrdersReactionIsOn_Checked;
            CheckBoxFailOpenOrdersReactionIsOn.Unchecked -= CheckBoxFailOpenOrdersReactionIsOn_Checked;
            TextBoxFailOpenOrdersCountToReaction.TextChanged -= TextBoxFailOpenOrdersCountToReaction_TextChanged;
            TextBoxIcebergMillisecondsDelay.TextChanged -= TextBoxIcebergMillisecondsDelay_TextChanged;

            _gridSecurities.CellValueChanged -= _gridSecurities_CellValueChanged;
            _gridSecurities.CellClick -= _gridSecurities_CellClick;
            _gridSecurities.DataError -= _gridSecurities_DataError;
            HostSecurities.Child = null;
            _gridSecurities.Rows.Clear();
            DataGridFactory.ClearLinks(_gridSecurities);

            _portfolioToCopy.LogCopyTrader.StopPaint();
            if(_portfolioToCopy.MyJournal != null)
            {
                _portfolioToCopy.MyJournal.StopPaint();
            }
           
            _portfolioToCopy = null;

            _copyTrader = null;
        }

        private bool _formIsClosed = false;

        private void SavePanelsPosition()
        {
            try
            {
                string result = "";

                if (GridFollowSettings.RowDefinitions[1].Height.Value == 25)
                {// securities
                    result += "0,";
                }
                else
                {
                    result += "1,";
                }

                if (GridFollowSettings.RowDefinitions[2].Height.Value == 25)
                {// journal
                    result += "0,";
                }
                else
                {
                    result += "1,";
                }

                if (GridPrime.RowDefinitions[1].Height.Value == 25)
                {// log
                    result += "0,";
                }
                else if (GridPrime.RowDefinitions[1].Height.Value == 83)
                {// log
                    result += "1,";
                }
                else
                {
                    result += "2,";
                }

                _portfolioToCopy.PanelsPosition = result;
                _portfolioToCopy.Save();
            }
            catch (Exception ex)
            {
                _portfolioToCopy?.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void LoadPanelsPositions()
        {
            if (string.IsNullOrEmpty(_portfolioToCopy.PanelsPosition))
            {
                return;
            }

            string[] save = _portfolioToCopy.PanelsPosition.Split(',');

            if (save[0] == "0")
            {
                GridFollowSettings.RowDefinitions[1].Height = new GridLength(25, GridUnitType.Pixel);
                ButtonSecuritiesGridDown.IsEnabled = false;
            }
            else
            {
                ButtonSecuritiesGridUp.IsEnabled = false;
            }

            if (save[1] == "0")
            {
                GridFollowSettings.RowDefinitions[2].Height = new GridLength(25, GridUnitType.Pixel);
                ButtonJournalGridDown.IsEnabled = false;
                ButtonShowJournal.Visibility = Visibility.Collapsed;
            }
            else
            {
                ButtonJournalGridUp.IsEnabled = false;
            }

            if(save.Length > 2)
            {
                if (save[2] == "0")
                {
                    GridPrime.RowDefinitions[1].Height = new GridLength(25, GridUnitType.Pixel);
                    ButtonLogDown.IsEnabled = false;
                }
                else if(save[2] == "1")
                {
                    GridPrime.RowDefinitions[1].Height = new GridLength(83, GridUnitType.Star);

                }
                else
                {
                    GridPrime.RowDefinitions[1].Height = new GridLength(250, GridUnitType.Star);
                    ButtonLogUp.IsEnabled = false;
                }
            }
        }

        #region Painter thread

        private void PaintFormThreadArea()
        {
            while(true)
            {
                try
                {
                    Thread.Sleep(3000);

                    if (_formIsClosed == true)
                    {
                        return;
                    }

                    RePaintControls();
                }
                catch(Exception ex)
                {
                    _portfolioToCopy?.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
                }
            }
        }

        private void RePaintControls()
        {
            try
            {
                if (_gridSecurities.InvokeRequired)
                {
                    _gridSecurities.Invoke(new Action(RePaintControls));
                    return;
                }

                string masterValue = Math.Round(_portfolioToCopy.MasterAssetValue,3).ToString();
                string slaveValue = Math.Round(_portfolioToCopy.SlaveAssetValue,3).ToString();

                if(TextBoxMasterAssetValue.Text != masterValue)
                {
                    TextBoxMasterAssetValue.Text = masterValue;
                }
               
                if(TextBoxSlaveAssetValue.Text != slaveValue)
                {
                    TextBoxSlaveAssetValue.Text = slaveValue;
                }

                if(_portfolioToCopy.FailOpenOrdersCountFact.ToString() !=
                    TextBoxFailOpenOrdersCountFact.Text)
                {
                    TextBoxFailOpenOrdersCountFact.Text = _portfolioToCopy.FailOpenOrdersCountFact.ToString();
                }
            }
            catch (Exception ex)
            {
                _portfolioToCopy?.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion

        #region Settings

        private void TextBoxMinCurrencyQty_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxMinCurrencyQty.Text))
                {
                    return;
                }

                _portfolioToCopy.MinCurrencyQty = TextBoxMinCurrencyQty.Text.ToDecimal();
                _portfolioToCopy.Save();
            }
            catch (Exception ex)
            {
                _portfolioToCopy?.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TextBoxSlaveAsset_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _portfolioToCopy.SlaveAsset = TextBoxSlaveAsset.Text;
                _portfolioToCopy.Save();

            }
            catch (Exception ex)
            {
                _portfolioToCopy?.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TextBoxMasterAsset_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _portfolioToCopy.MasterAsset = TextBoxMasterAsset.Text;
                _portfolioToCopy.Save();

            }
            catch (Exception ex)
            {
                _portfolioToCopy?.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TextBoxVolumeMult_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if(string.IsNullOrEmpty(TextBoxVolumeMult.Text))
                {
                    return;
                }

                decimal volumeMult = TextBoxVolumeMult.Text.ToString().ToDecimal();

                _portfolioToCopy.VolumeMult = volumeMult;
                _portfolioToCopy.Save();

            }
            catch (Exception ex)
            {
                _portfolioToCopy?.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ComboBoxVolumeType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                CopyTraderVolumeType volumeType;

                if (Enum.TryParse(ComboBoxVolumeType.SelectedItem.ToString(), out volumeType))
                {
                    _portfolioToCopy.VolumeType = volumeType;
                    _portfolioToCopy.Save();
                }
            }
            catch (Exception ex)
            {
                _portfolioToCopy?.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ComboBoxIcebergCount_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                int ordersCount = Convert.ToInt32(ComboBoxIcebergCount.SelectedItem.ToString());

                _portfolioToCopy.IcebergCount = ordersCount;
                _portfolioToCopy.Save();

            }
            catch (Exception ex)
            {
                _portfolioToCopy?.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TextBoxIcebergMillisecondsDelay_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxIcebergMillisecondsDelay.Text))
                {
                    return;
                }

                _portfolioToCopy.IcebergMillisecondsDelay = Convert.ToInt32(TextBoxIcebergMillisecondsDelay.Text);
                _portfolioToCopy.Save();
            }
            catch (Exception ex)
            {
                _portfolioToCopy?.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ComboBoxOrderType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                CopyTraderOrdersType orderType;

                if (Enum.TryParse(ComboBoxOrderType.SelectedItem.ToString(), out orderType))
                {
                    _portfolioToCopy.OrderType = orderType;
                    _portfolioToCopy.Save();
                }
            }
            catch (Exception ex)
            {
                _portfolioToCopy?.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ComboBoxIsOn_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                bool value = Convert.ToBoolean(ComboBoxIsOn.SelectedItem.ToString());
                _portfolioToCopy.IsOn = value;
                _portfolioToCopy.Save();
            }
            catch(Exception ex)
            {
                _portfolioToCopy?.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TextBoxFailOpenOrdersCountToReaction_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if(string.IsNullOrEmpty(TextBoxFailOpenOrdersCountToReaction.Text))
                {
                    return;
                }

                _portfolioToCopy.FailOpenOrdersCountToReaction = Convert.ToInt32(TextBoxFailOpenOrdersCountToReaction.Text);
                _portfolioToCopy.Save();
            }
            catch (Exception ex)
            {
                _portfolioToCopy?.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void CheckBoxFailOpenOrdersReactionIsOn_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                _portfolioToCopy.FailOpenOrdersReactionIsOn = CheckBoxFailOpenOrdersReactionIsOn.IsChecked.Value;
                _portfolioToCopy.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void ButtonCLoseAllPositions_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Market.Label237);

                ui.ShowDialog();

                if (ui.UserAcceptAction == false)
                {
                    return;
                }

                if(_portfolioToCopy.IsOn != false)
                {
                    _portfolioToCopy.IsOn = false;
                    _portfolioToCopy.Save();
                    ComboBoxIsOn.SelectedItem = _portfolioToCopy.IsOn.ToString();
                }

                _portfolioToCopy.TryCloseMyPositionsRobots();
            }
            catch
            {
                // ignore
            }
        }

        private void ButtonDeleteAllPositions_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Market.Label238);

                ui.ShowDialog();

                if (ui.UserAcceptAction == false)
                {
                    return;
                }

                if (_portfolioToCopy.IsOn != false)
                {
                    _portfolioToCopy.IsOn = false;
                    _portfolioToCopy.Save();
                    ComboBoxIsOn.SelectedItem = _portfolioToCopy.IsOn.ToString();
                }

                _portfolioToCopy.MyJournal.Clear();
            }
            catch
            {
                // ignore
            }
        }

        #endregion

        #region Securities

        private DataGridView _gridSecurities;

        private void CreateSecuritiesGrid()
        {
            _gridSecurities = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect,
               DataGridViewAutoSizeRowsMode.AllCells);

            _gridSecurities.ScrollBars = ScrollBars.Vertical;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _gridSecurities.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = "#"; // num
            column0.ReadOnly = true;
            column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            _gridSecurities.Columns.Add(column0);

            DataGridViewColumn column2 = new DataGridViewColumn();
            column2.CellTemplate = cell0;
            column2.HeaderText = OsLocalization.Market.Label220; // Master Name
            column2.ReadOnly = false;
            column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridSecurities.Columns.Add(column2);

            DataGridViewColumn column3 = new DataGridViewColumn();
            column3.CellTemplate = cell0;
            column3.HeaderText = OsLocalization.Market.Label221; // Master Class
            column3.ReadOnly = false;
            column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridSecurities.Columns.Add(column3);

            DataGridViewColumn column4 = new DataGridViewColumn();
            column4.CellTemplate = cell0;
            column4.HeaderText = OsLocalization.Market.Label222; // Slave Name
            column4.ReadOnly = false;
            column4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            column4.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _gridSecurities.Columns.Add(column4);

            DataGridViewColumn column5 = new DataGridViewColumn();
            column5.CellTemplate = cell0;
            column5.HeaderText = OsLocalization.Market.Label223; // Slave Class
            column5.ReadOnly = false;
            column5.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridSecurities.Columns.Add(column5);

            DataGridViewColumn column6 = new DataGridViewColumn();
            column6.CellTemplate = cell0;
            //column6.HeaderText = OsLocalization.Market.Label7; // Delete
            column6.ReadOnly = true;
            column6.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridSecurities.Columns.Add(column6);

            HostSecurities.Child = _gridSecurities;

            _gridSecurities.CellValueChanged += _gridSecurities_CellValueChanged;
            _gridSecurities.CellClick += _gridSecurities_CellClick;
            _gridSecurities.DataError += _gridSecurities_DataError;
        }

        private void _gridSecurities_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            _portfolioToCopy?.SendLogMessage("_gridSecurities_DataError \n"
             + e.Exception.ToString(), Logging.LogMessageType.Error);
        }

        private void UpdateGridSecurities()
        {
            try
            {
                if (_gridSecurities.InvokeRequired)
                {
                    _gridSecurities.Invoke(new Action(UpdateGridSecurities));
                    return;
                }

                // 0 num
                // 1 Master Name
                // 2 Master Class
                // 3 Slave Name
                // 4 Slave Class
                // 5 Delete / Add

                List<SecurityToCopy> securities =  _portfolioToCopy.SecurityToCopy;

                List<DataGridViewRow> rowsNow = new List<DataGridViewRow>();

                for (int i = 0; i < securities.Count; i++)
                {
                    DataGridViewRow botRows = GetRowBySecurity(securities[i],i);

                    if (botRows != null)
                    {
                        rowsNow.Add(botRows);
                    }
                }

                rowsNow.Add(GetSecurityNullRow());

                DataGridViewRow endRow = GetRowSecurityEndRow();

                if (endRow != null)
                {
                    rowsNow.Add(endRow);
                }

                if (rowsNow.Count != _gridSecurities.Rows.Count)
                { // 1 перерисовываем целиком
                    _gridSecurities.Rows.Clear();

                    for (int i = 0; i < rowsNow.Count; i++)
                    {
                        _gridSecurities.Rows.Add(rowsNow[i]);
                    }
                }
                else
                { // 2 перерисовываем по линиям

                }
            }
            catch (Exception ex)
            {
                _portfolioToCopy?.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private DataGridViewRow GetRowBySecurity(SecurityToCopy security,int number)
        {
            // 0 num
            // 1 Master Name
            // 2 Master Class
            // 3 Slave Name
            // 4 Slave Class
            // 5 Delete / Add

            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[row.Cells.Count - 1].Value = number;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[row.Cells.Count - 1].Value = security.MasterSecurityName;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[row.Cells.Count - 1].Value = security.MasterSecurityClass;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[row.Cells.Count - 1].Value = security.SlaveSecurityName;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[row.Cells.Count - 1].Value = security.SlaveSecurityClass;

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[row.Cells.Count - 1].Value = OsLocalization.Market.Label47;


            return row;
        }

        private DataGridViewRow GetSecurityNullRow()
        {
            // 0 num
            // 1 Master Name
            // 2 Master Class
            // 3 Slave Name
            // 4 Slave Class
            // 5 Delete / Add

            DataGridViewRow row = new DataGridViewRow();
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());

            return row;
        }

        private DataGridViewRow GetRowSecurityEndRow()
        {
            // 0 num
            // 1 Master Name
            // 2 Master Class
            // 3 Slave Name
            // 4 Slave Class
            // 5 Delete / Add

            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewTextBoxCell());

            row.Cells.Add(new DataGridViewTextBoxCell());

            row.Cells.Add(new DataGridViewTextBoxCell());

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[row.Cells.Count - 1].Value = OsLocalization.Market.Label180;

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[row.Cells.Count - 1].Value = OsLocalization.Market.Label181;

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[row.Cells.Count - 1].Value = OsLocalization.Market.Label48;

            return row;
        }

        private void _gridSecurities_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                for (int i = 0; i < _gridSecurities.Rows.Count - 1 
                    && i < _portfolioToCopy.SecurityToCopy.Count; i++)
                {
                    SaveSecurityFromGrid(_gridSecurities.Rows[i], _portfolioToCopy.SecurityToCopy[i]);
                }

                _portfolioToCopy.Save();
            }
            catch (Exception ex)
            {
                _portfolioToCopy?.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void SaveSecurityFromGrid(DataGridViewRow nRow, SecurityToCopy security)
        {
            // 0 num
            // 1 Master Name
            // 2 Master Class
            // 3 Slave Name
            // 4 Slave Class
            // 5 Delete / Add

            try
            {
                if (nRow.Cells[1].Value != null)
                {
                    security.MasterSecurityName = nRow.Cells[1].Value.ToString();
                }

                if (nRow.Cells[2].Value != null)
                {
                    security.MasterSecurityClass = nRow.Cells[2].Value.ToString();
                }

                if (nRow.Cells[3].Value != null)
                {
                    security.SlaveSecurityName = nRow.Cells[3].Value.ToString();
                }

                if (nRow.Cells[4].Value != null)
                {
                    security.SlaveSecurityClass = nRow.Cells[4].Value.ToString();
                }
            }
            catch (Exception ex)
            {
                _portfolioToCopy?.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void _gridSecurities_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                int row = e.RowIndex;
                int col = e.ColumnIndex;

                if(row < _gridSecurities.Rows.Count - 1
                    && col == 5
                    && row < _portfolioToCopy.SecurityToCopy.Count)
                {// удалить данные по отдельной бумаге
                    int num = Convert.ToInt32(_gridSecurities.Rows[row].Cells[0].Value);
                    DeleteSecurity(num);
                }
                else if (row == _gridSecurities.Rows.Count - 1
                    && col == 5)
                {// добавить бумаги
                    AddNewSecurity();
                }
                else if (row == _gridSecurities.Rows.Count - 1
                 && col == 3)
                {// сохранить бумаги в файл
                    SaveSecuritiesInFile();
                }
                else if (row == _gridSecurities.Rows.Count - 1
                 && col == 4)
                {// загрузить бумаги из файла
                    LoadSecuritiesFromFile();
                }
            }
            catch (Exception ex)
            {
                _portfolioToCopy?.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        public void AddNewSecurity()
        {
            if (ServerMaster.GetServers() == null)
            {
                CustomMessageBoxUi uiNoConnections = new CustomMessageBoxUi(OsLocalization.Data.Label12);
                uiNoConnections.ShowDialog();
                return;
            }

            IServer myServer =
                ServerMaster.GetServers().Find(server => server.ServerNameAndPrefix.StartsWith(_portfolioToCopy.ServerName));

            if (myServer == null)
            {
                CustomMessageBoxUi uiNoConnections = new CustomMessageBoxUi(OsLocalization.Data.Label12);
                uiNoConnections.ShowDialog();
                return;
            }

            List<Security> securities = myServer.Securities;

            if (securities == null
                || securities.Count == 0)
            {
                CustomMessageBoxUi uiNoConnections = new CustomMessageBoxUi(OsLocalization.Data.Label13);
                uiNoConnections.ShowDialog();
                return;
            }

            NewSecurityUi ui = new NewSecurityUi(securities);
            ui.ShowDialog();

            if (ui.SelectedSecurity != null && ui.SelectedSecurity.Count != 0)
            {

                securities = ui.SelectedSecurity;

                for(int i = 0;i < securities.Count;i++)
                {
                    Security currentSecurity = securities[i];

                    bool isInArray = false;

                    for (int j = 0; j < _portfolioToCopy.SecurityToCopy.Count; j++)
                    {
                        if (_portfolioToCopy.SecurityToCopy[j].MasterSecurityName == currentSecurity.Name
                            &&
                            _portfolioToCopy.SecurityToCopy[j].MasterSecurityClass == currentSecurity.NameClass)
                        {
                            isInArray = true;
                            break;
                        }
                    }

                    if(isInArray == false)
                    {
                        SecurityToCopy securityToCopyNew = new SecurityToCopy();
                        securityToCopyNew.MasterSecurityName = currentSecurity.Name;
                        securityToCopyNew.MasterSecurityClass = currentSecurity.NameClass;
                        securityToCopyNew.SlaveSecurityName = currentSecurity.Name;
                        securityToCopyNew.SlaveSecurityClass = currentSecurity.NameClass;
                        _portfolioToCopy.SecurityToCopy.Add(securityToCopyNew);
                    }

                }
            }

            _portfolioToCopy.Save();
            UpdateGridSecurities();
        }

        public void DeleteSecurity(int index)
        {
            AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Market.Label196);

            ui.ShowDialog();
            if (ui.UserAcceptAction == false)
            {
                return;
            }

            _portfolioToCopy.SecurityToCopy.RemoveAt(index);

            _portfolioToCopy.Save();
            UpdateGridSecurities();
        }

        private void LoadSecuritiesFromFile()
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*";
                openFileDialog.InitialDirectory = System.Windows.Forms.Application.StartupPath;
                openFileDialog.RestoreDirectory = true;
                openFileDialog.ShowDialog();

                if (string.IsNullOrEmpty(openFileDialog.FileName))
                {
                    return;
                }

                string filePath = openFileDialog.FileName;

                if (File.Exists(filePath) == false)
                {
                    return;
                }

                try
                {
                    List<SecurityToCopy> securityToCopy = new List<SecurityToCopy>();

                    using (StreamReader reader = new StreamReader(filePath))
                    {
                        while(reader.EndOfStream == false)
                        {
                            string fileStr = reader.ReadLine();
                            SecurityToCopy security = new SecurityToCopy();
                            security.SetSaveString(fileStr);
                            securityToCopy.Add(security);
                        }
                    }

                    if(securityToCopy.Count >0)
                    {
                        _portfolioToCopy.SecurityToCopy = securityToCopy;
                        _portfolioToCopy.Save();
                        UpdateGridSecurities();
                    }
                }
                catch (Exception error)
                {
                    CustomMessageBoxUi ui = new CustomMessageBoxUi(error.ToString());
                    ui.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                _portfolioToCopy?.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void SaveSecuritiesInFile()
        {
            try
            {
                List<SecurityToCopy> securityToCopy = _portfolioToCopy.SecurityToCopy;

                if (securityToCopy.Count == 0)
                {
                    return;
                }

                SaveFileDialog saveFileDialog = new SaveFileDialog();
                saveFileDialog.InitialDirectory = System.Windows.Forms.Application.StartupPath;
                saveFileDialog.Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*";

                saveFileDialog.RestoreDirectory = true;
                saveFileDialog.ShowDialog();

                if (string.IsNullOrEmpty(saveFileDialog.FileName))
                {
                    return;
                }

                string filePath = saveFileDialog.FileName;

                if (File.Exists(filePath) == false)
                {
                    using (FileStream stream = File.Create(filePath))
                    {
                        // do nothin
                    }
                }
                try
                {
                    using (StreamWriter writer = new StreamWriter(filePath))
                    {
                         for(int i = 0;i < securityToCopy.Count;i++)
                        {
                            writer.WriteLine(securityToCopy[i].GetSaveString());
                        }
                    }
                }
                catch (Exception error)
                {
                    CustomMessageBoxUi ui = new CustomMessageBoxUi(error.ToString());
                    ui.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                _portfolioToCopy?.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ButtonSecuritiesGridDown_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ButtonSecuritiesGridUp.IsEnabled = true;
                GridFollowSettings.RowDefinitions[1].Height = new GridLength(25, GridUnitType.Pixel);
                ButtonSecuritiesGridDown.IsEnabled = false;
            }
            catch (Exception ex)
            {
                _portfolioToCopy?.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
            SavePanelsPosition();
        }

        private void ButtonSecuritiesGridUp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ButtonSecuritiesGridDown.IsEnabled = true;
                GridFollowSettings.RowDefinitions[1].Height = new GridLength(185, GridUnitType.Star);
                ButtonSecuritiesGridUp.IsEnabled = false;
            }
            catch (Exception ex)
            {
                _portfolioToCopy?.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
            SavePanelsPosition();
        }

        #endregion

        #region Journal

        private JournalUi2 _journalUi;

        private void ButtonShowJournal_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_journalUi != null)
                {
                    if (_journalUi.WindowState == WindowState.Minimized)
                    {
                        _journalUi.WindowState = WindowState.Normal;
                    }

                    _journalUi.Activate();
                    return;
                }

                List<BotPanelJournal> panelsJournal = new List<BotPanelJournal>();

                List<Journal.Journal> journals = new List<Journal.Journal>();
                journals.Add(_portfolioToCopy.MyJournal);

                BotPanelJournal botPanel = new BotPanelJournal();
                botPanel.BotName = journals[0].Name;
                botPanel.BotClass = "CopyJournal";

                botPanel._Tabs = new List<BotTabJournal>();

                for (int i2 = 0; journals != null && i2 < journals.Count; i2++)
                {
                    BotTabJournal botTabJournal = new BotTabJournal();
                    botTabJournal.TabNum = i2;
                    botTabJournal.Journal = journals[i2];
                    botPanel._Tabs.Add(botTabJournal);
                }

                panelsJournal.Add(botPanel);

                _journalUi = new JournalUi2(panelsJournal, StartProgram.IsOsTrader);
                _journalUi.Closed += _journalUi_Closed;
                _journalUi.LogMessageEvent += _portfolioToCopy.SendLogMessage;
                _journalUi.Show();
            }
            catch (Exception ex)
            {
                _portfolioToCopy?.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void _journalUi_Closed(object sender, EventArgs e)
        {
            try
            {
                _journalUi.Closed -= _journalUi_Closed;

                if(_portfolioToCopy != null)
                {
                    _journalUi.LogMessageEvent -= _portfolioToCopy.SendLogMessage;
                }
                
                _journalUi.IsErase = true;
                _journalUi = null;
            }
            catch (Exception ex)
            {
                _portfolioToCopy?.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ButtonJournalGridDown_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ButtonJournalGridUp.IsEnabled = true;
                GridFollowSettings.RowDefinitions[2].Height = new GridLength(25, GridUnitType.Pixel);
                ButtonJournalGridDown.IsEnabled = false;
                ButtonShowJournal.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                _portfolioToCopy?.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
            SavePanelsPosition();
        }

        private void ButtonJournalGridUp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ButtonJournalGridDown.IsEnabled = true;
                GridFollowSettings.RowDefinitions[2].Height = new GridLength(185, GridUnitType.Star);
                ButtonJournalGridUp.IsEnabled = false;
                ButtonShowJournal.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                _portfolioToCopy?.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
            SavePanelsPosition();
        }

        #endregion

        #region Log

        private void ButtonLogDown_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ButtonLogUp.IsEnabled = true;

                if (GridPrime.RowDefinitions[1].Height.Value == 250)
                {
                    GridPrime.RowDefinitions[1].Height = new GridLength(83, GridUnitType.Star);
                }
                else // if (GridPrime.RowDefinitions[1].Height.Value == 500)
                {
                    GridPrime.RowDefinitions[1].Height = new GridLength(25, GridUnitType.Pixel);

                    ButtonLogDown.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                _portfolioToCopy?.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
            SavePanelsPosition();
        }

        private void ButtonLogUp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ButtonLogDown.IsEnabled = true;

                if (GridPrime.RowDefinitions[1].Height.Value == 83)
                {
                    GridPrime.RowDefinitions[1].Height = new GridLength(250, GridUnitType.Star);
                    ButtonLogUp.IsEnabled = false;
                }
                else //if (GridPrime.RowDefinitions[1].Height.Value != 800)
                {
                    GridPrime.RowDefinitions[1].Height = new GridLength(83, GridUnitType.Star);
                }
            }
            catch (Exception ex)
            {
                _portfolioToCopy?.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
            SavePanelsPosition();
        }

        #endregion

        #region NonTradePeriods

        private void ButtonNonTradePeriods_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _portfolioToCopy.TradePeriodsSettings.ShowDialog();
            }
            catch (Exception ex)
            {
                _portfolioToCopy?.SendLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion
    }
}
