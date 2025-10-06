/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using OsEngine.Language;
using OsEngine.Market.Servers;
using System.IO;
using OsEngine.Market;
using OsEngine.Logging;
using System.Windows.Input;
using System.Windows;

namespace OsEngine.Entity
{
    /// <summary>
    /// server securities settings window
    /// </summary>
    public partial class SecuritiesUi
    {
        /// <summary>
        /// the server that owns the securities
        /// </summary>
        private IServer _server;

        public SecuritiesUi(IServer server)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            UpdateClassComboBox(server.Securities);

            CreateTable();

            ComboBoxClass.SelectionChanged += ComboBoxClass_SelectionChanged;
            PaintSecurities(server.Securities);

            _server = server;
            _server.SecuritiesChangeEvent += _server_SecuritiesChangeEvent;

            Title = OsLocalization.Entity.TitleSecuritiesUi + " " + _server.ServerType;
            LabelClass.Content = OsLocalization.Entity.SecuritiesColumn11;
            TextBoxSearchSecurity.Text = OsLocalization.Market.Label64;

            this.Activate();
            this.Focus();

            this.Closed += SecuritiesUi_Closed;

            TextBoxSearchSecurity.MouseEnter += TextBoxSearchSecurity_MouseEnter;
            TextBoxSearchSecurity.TextChanged += TextBoxSearchSecurity_TextChanged;
            TextBoxSearchSecurity.MouseLeave += TextBoxSearchSecurity_MouseLeave;
            TextBoxSearchSecurity.LostKeyboardFocus += TextBoxSearchSecurity_LostKeyboardFocus;
            TextBoxSearchSecurity.KeyDown += TextBoxSearchSecurity_KeyDown;
            ButtonRightInSearchResults.Click += ButtonRightInSearchResults_Click;
            ButtonLeftInSearchResults.Click += ButtonLeftInSearchResults_Click;
        }

        private void SecuritiesUi_Closed(object sender, EventArgs e)
        {
            try
            {
                _server.SecuritiesChangeEvent -= _server_SecuritiesChangeEvent;
                _server = null;

                _gridSecurities.CellValueChanged -= _grid_CellValueChanged;
                _gridSecurities.DataError -= _gridSecurities_DataError;
                DataGridFactory.ClearLinks(_gridSecurities);
                _gridSecurities.Columns.Clear();
                _gridSecurities.DataSource = null;
                _gridSecurities.Dispose();
                _gridSecurities = null;
                HostSecurities.Child = null;

                TextBoxSearchSecurity.MouseEnter -= TextBoxSearchSecurity_MouseEnter;
                TextBoxSearchSecurity.TextChanged -= TextBoxSearchSecurity_TextChanged;
                TextBoxSearchSecurity.MouseLeave -= TextBoxSearchSecurity_MouseLeave;
                TextBoxSearchSecurity.LostKeyboardFocus -= TextBoxSearchSecurity_LostKeyboardFocus;
                TextBoxSearchSecurity.KeyDown -= TextBoxSearchSecurity_KeyDown;
                ButtonRightInSearchResults.Click -= ButtonRightInSearchResults_Click;
                ButtonLeftInSearchResults.Click -= ButtonLeftInSearchResults_Click;
            }
            catch
            {

            }
        }

        private void ComboBoxClass_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            PaintSecurities(_server.Securities);
        }

        private void UpdateClassComboBox(List<Security> securities)
        {
            try
            {
                if (ComboBoxClass.Dispatcher.CheckAccess() == false)
                {
                    ComboBoxClass.Dispatcher.Invoke(new Action<List<Security>>(UpdateClassComboBox), securities);
                    return;
                }

                string startClass = null;

                if (ComboBoxClass.SelectedItem != null)
                {
                    startClass = ComboBoxClass.SelectedItem.ToString();
                }

                List<string> classes = new List<string>();

                classes.Add("All");

                for (int i = 0; securities != null && i < securities.Count; i++)
                {
                    string curClass = securities[i].NameClass;

                    if (string.IsNullOrEmpty(curClass))
                    {
                        continue;
                    }

                    bool isInArray = false;

                    for (int i2 = 0; i2 < classes.Count; i2++)
                    {
                        if (classes[i2] == curClass)
                        {
                            isInArray = true;
                            break;
                        }
                    }

                    if (isInArray == false)
                    {
                        classes.Add(curClass);
                    }
                }

                ComboBoxClass.Items.Clear();

                for (int i = 0; i < classes.Count; i++)
                {
                    ComboBoxClass.Items.Add(classes[i]);
                }

                if (ComboBoxClass.SelectedItem == null)
                {
                    ComboBoxClass.SelectedItem = classes[0];
                }

                if (startClass != null)
                {
                    ComboBoxClass.SelectedItem = startClass;
                }

                if (ComboBoxClass.SelectedItem.ToString() == "All"
                    && securities.Count > 10000
                    && classes.Count > 1)
                {
                    ComboBoxClass.SelectedItem = classes[1];
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private DataGridView _gridSecurities;

        private void CreateTable()
        {
            _gridSecurities = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
    DataGridViewAutoSizeRowsMode.AllCells);
            _gridSecurities.ScrollBars = ScrollBars.Vertical;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _gridSecurities.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = "#"; // num
            column0.ReadOnly = true;
            column0.Width = 70;
            _gridSecurities.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = OsLocalization.Entity.SecuritiesColumn1; // Name
            column1.ReadOnly = true;
            column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridSecurities.Columns.Add(column1);

            DataGridViewColumn column2 = new DataGridViewColumn();
            column2.CellTemplate = cell0;
            column2.HeaderText = OsLocalization.Entity.SecuritiesColumn9; // Name Full
            column2.ReadOnly = true;
            column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridSecurities.Columns.Add(column2);

            DataGridViewColumn column3 = new DataGridViewColumn();
            column3.CellTemplate = cell0;
            column3.HeaderText = OsLocalization.Entity.SecuritiesColumn10; // Name ID
            column3.ReadOnly = true;
            column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridSecurities.Columns.Add(column3);

            DataGridViewColumn column4 = new DataGridViewColumn();
            column4.CellTemplate = cell0;
            column4.HeaderText = OsLocalization.Entity.SecuritiesColumn11; // Class
            column4.ReadOnly = true;
            column4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridSecurities.Columns.Add(column4);

            DataGridViewColumn column5 = new DataGridViewColumn();
            column5.CellTemplate = cell0;
            column5.HeaderText = OsLocalization.Entity.SecuritiesColumn2; // Type
            column5.ReadOnly = true;
            column5.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridSecurities.Columns.Add(column5);

            DataGridViewColumn column6 = new DataGridViewColumn();
            column6.CellTemplate = cell0;
            column6.HeaderText = OsLocalization.Entity.SecuritiesColumn3; // Lot
            column6.ReadOnly = false;
            column6.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridSecurities.Columns.Add(column6);

            DataGridViewColumn column7 = new DataGridViewColumn();
            column7.CellTemplate = cell0;
            column7.HeaderText = OsLocalization.Entity.SecuritiesColumn4; // Price step
            column7.ReadOnly = false;
            column7.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridSecurities.Columns.Add(column7);

            DataGridViewColumn column8 = new DataGridViewColumn();
            column8.CellTemplate = cell0;
            column8.HeaderText = OsLocalization.Entity.SecuritiesColumn5; // Price step cost
            column8.ReadOnly = false;
            column8.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridSecurities.Columns.Add(column8);

            DataGridViewColumn column9 = new DataGridViewColumn();
            column9.CellTemplate = cell0;
            column9.HeaderText = OsLocalization.Entity.SecuritiesColumn8; // Price decimals
            column9.ReadOnly = false;
            column9.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridSecurities.Columns.Add(column9);

            DataGridViewColumn column10 = new DataGridViewColumn();
            column10.CellTemplate = cell0;
            column10.HeaderText = OsLocalization.Entity.SecuritiesColumn7; // Volume decimals
            column10.ReadOnly = false;
            column10.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridSecurities.Columns.Add(column10);

            DataGridViewColumn column11_0 = new DataGridViewColumn();
            column11_0.CellTemplate = cell0;
            column11_0.HeaderText = OsLocalization.Entity.SecuritiesColumn20; // Min volume type
            column11_0.ReadOnly = false;
            column11_0.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            _gridSecurities.Columns.Add(column11_0);

            DataGridViewColumn column11 = new DataGridViewColumn();
            column11.CellTemplate = cell0;
            column11.HeaderText = OsLocalization.Entity.SecuritiesColumn12; // Min volume
            column11.ReadOnly = false;
            column11.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridSecurities.Columns.Add(column11);

            DataGridViewColumn column11_2 = new DataGridViewColumn();
            column11_2.CellTemplate = cell0;
            column11_2.HeaderText = OsLocalization.Entity.SecuritiesColumn19; // Volume step
            column11_2.ReadOnly = false;
            column11_2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridSecurities.Columns.Add(column11_2);

            DataGridViewColumn column12 = new DataGridViewColumn();
            column12.CellTemplate = cell0;
            column12.HeaderText = OsLocalization.Entity.SecuritiesColumn13; // Price limit High
            column12.ReadOnly = false;
            column12.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridSecurities.Columns.Add(column12);

            DataGridViewColumn column13 = new DataGridViewColumn();
            column13.CellTemplate = cell0;
            column13.HeaderText = OsLocalization.Entity.SecuritiesColumn14; // Price limit Low
            column13.ReadOnly = false;
            column13.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridSecurities.Columns.Add(column13);

            DataGridViewColumn column14 = new DataGridViewColumn();
            column14.CellTemplate = cell0;
            column14.HeaderText = OsLocalization.Entity.SecuritiesColumn15; // Collateral / ру: ГО
            column14.ReadOnly = false;
            column14.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridSecurities.Columns.Add(column14);

            DataGridViewColumn column15 = new DataGridViewColumn();
            column15.CellTemplate = cell0;
            column15.HeaderText = OsLocalization.Entity.SecuritiesColumn16; // Option type
            column15.ReadOnly = true;
            column15.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridSecurities.Columns.Add(column15);

            DataGridViewColumn column16 = new DataGridViewColumn();
            column16.CellTemplate = cell0;
            column16.HeaderText = OsLocalization.Entity.SecuritiesColumn17; // Strike
            column16.ReadOnly = false;
            column16.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridSecurities.Columns.Add(column16);

            DataGridViewColumn column17 = new DataGridViewColumn();
            column17.CellTemplate = cell0;
            column17.HeaderText = OsLocalization.Entity.SecuritiesColumn18; // Expiration
            column17.ReadOnly = true;
            column17.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridSecurities.Columns.Add(column17);

            HostSecurities.Child = _gridSecurities;
            HostSecurities.Child.Show();
            HostSecurities.Child.Refresh();
            _gridSecurities.CellValueChanged += _grid_CellValueChanged;
            _gridSecurities.DataError += _gridSecurities_DataError;
        }

        private void _gridSecurities_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            ServerMaster.SendNewLogMessage(e.ToString(), Logging.LogMessageType.Error);
        }

        void _grid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                SaveFromTable(e.RowIndex);
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void _server_SecuritiesChangeEvent(List<Security> securities)
        {
            try
            {
                UpdateClassComboBox(securities);
                PaintSecurities(securities);
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void PaintSecurities(List<Security> securities)
        {
            // 0 num
            // 1 Name
            // 2 Name Full
            // 3 Name ID
            // 4 Class
            // 5 Type
            // 6 Lot
            // 7 Price step
            // 8 Price step cost
            // 9 Price decimals
            // 10 Volume decimals
            // 11 Min volume type
            // 12 Min volume
            // 13 Volume step
            // 14 Price limit High
            // 15 Price limit Low
            // 16 Collateral
            // 17 Option type
            // 18 Strike
            // 19 Expiration

            try
            {
                if (securities == null)
                {
                    return;
                }

                if (_gridSecurities == null)
                {
                    return;
                }

                if (_gridSecurities.InvokeRequired)
                {
                    _gridSecurities.Invoke(new Action<List<Security>>(PaintSecurities), securities);
                    return;
                }

                if (ComboBoxClass.SelectedItem == null)
                {
                    return;
                }

                int num = 1;

                string selectedClass = ComboBoxClass.SelectedItem.ToString();

                List<DataGridViewRow> rows = new List<DataGridViewRow>();

                for (int i = 0; i < securities.Count; i++)
                {
                    Security curSec = securities[i];

                    if (selectedClass != "All"
                        && curSec.NameClass != selectedClass)
                    {
                        continue;
                    }

                    DataGridViewRow nRow = new DataGridViewRow();

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[0].Value = num; num++;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[1].Value = curSec.Name;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[2].Value = curSec.NameFull;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[3].Value = curSec.NameId;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[4].Value = curSec.NameClass;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[5].Value = curSec.SecurityType;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[6].Value = curSec.Lot;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[7].Value = curSec.PriceStep.ToStringWithNoEndZero();

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[8].Value = curSec.PriceStepCost.ToStringWithNoEndZero();

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[9].Value = curSec.Decimals;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[10].Value = curSec.DecimalsVolume;

                    DataGridViewComboBoxCell comboBoxCell = new DataGridViewComboBoxCell();
                    comboBoxCell.Items.Add(MinTradeAmountType.Contract.ToString());
                    comboBoxCell.Items.Add(MinTradeAmountType.C_Currency.ToString());
                    nRow.Cells.Add(comboBoxCell);
                    nRow.Cells[11].Value = curSec.MinTradeAmountType.ToString();

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[12].Value = curSec.MinTradeAmount.ToStringWithNoEndZero();

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[13].Value = curSec.VolumeStep.ToStringWithNoEndZero();

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[14].Value = curSec.PriceLimitHigh.ToStringWithNoEndZero();

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[15].Value = curSec.PriceLimitLow.ToStringWithNoEndZero();

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[16].Value = curSec.Go.ToStringWithNoEndZero();

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[17].Value = curSec.OptionType;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    if (curSec.OptionType != OptionType.None)
                    {
                        nRow.Cells[18].Value = curSec.Strike.ToStringWithNoEndZero();
                    }
                    else
                    {
                        nRow.Cells[18].ReadOnly = true;
                    }

                    nRow.Cells.Add(new DataGridViewTextBoxCell());

                    if (curSec.Expiration != DateTime.MinValue)
                    {
                        nRow.Cells[19].Value = curSec.Expiration.ToString(OsLocalization.CurCulture);
                    }

                    rows.Add(nRow);
                }

                HostSecurities.Child = null;

                _gridSecurities.Rows.Clear();

                if (rows.Count > 0)
                {
                    _gridSecurities.Rows.AddRange(rows.ToArray());
                }

                HostSecurities.Child = _gridSecurities;

            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }

            UpdateSearchResults();
            UpdateSearchPanel();
        }

        private void SaveFromTable(int rowIndex)
        {
            // 0 num
            // 1 Name
            // 2 Name Full
            // 3 Name ID
            // 4 Class
            // 5 Type
            // 6 Lot
            // 7 Price step
            // 8 Price step cost
            // 9 Price decimals
            // 10 Volume decimals
            // 11 Min volume type
            // 12 Min volume
            // 13 Volume step
            // 14 Price limit High
            // 15 Price limit Low
            // 16 Collateral
            // 17 Option type
            // 18 Strike
            // 19 Expiration

            List<Security> securities = _server.Securities;

            if (securities == null)
            {
                return;
            }

            DataGridViewRow row = _gridSecurities.Rows[rowIndex];

            string secName = row.Cells[1].Value.ToString();
            string secFullName = row.Cells[2].Value.ToString();
            string secId = row.Cells[3].Value.ToString();
            string secClass = row.Cells[4].Value.ToString();
            string secType = row.Cells[5].Value.ToString();
            decimal lot = row.Cells[6].Value.ToString().ToDecimal();
            decimal priceStep = row.Cells[7].Value.ToString().ToDecimal();
            decimal priceStepCost = row.Cells[8].Value.ToString().ToDecimal();
            int priceDecimals = Convert.ToInt32(row.Cells[9].Value);
            int volumeDecimals = Convert.ToInt32(row.Cells[10].Value);
            MinTradeAmountType minVolumeType;
            Enum.TryParse(row.Cells[11].Value.ToString(), out minVolumeType);
            decimal minVolume = row.Cells[12].Value.ToString().ToDecimal();
            decimal volumeStep = row.Cells[13].Value.ToString().ToDecimal();
            decimal priceLimitHigh = row.Cells[14].Value.ToString().ToDecimal();
            decimal priceLimitLow = row.Cells[15].Value.ToString().ToDecimal();
            decimal collateral = row.Cells[16].Value.ToString().ToDecimal();

            // 15 Option type

            decimal strike = 0;

            if (row.Cells[18].Value != null)
            {
                strike = row.Cells[18].Value.ToString().ToDecimal();
            }
            // 17 Expiration

            Security mySecurity = null;

            for (int i = 0; i < securities.Count; i++)
            {
                if (securities[i].Name == secName
                    && securities[i].NameFull == secFullName
                    && securities[i].NameId == secId
                    && securities[i].NameClass == secClass
                    && securities[i].SecurityType.ToString() == secType)
                {
                    mySecurity = securities[i];
                    break;
                }
            }

            if (mySecurity == null)
            {
                return;
            }

            mySecurity.Lot = lot;
            mySecurity.PriceStep = priceStep;
            mySecurity.PriceStepCost = priceStepCost;
            mySecurity.Decimals = priceDecimals;
            mySecurity.DecimalsVolume = volumeDecimals;
            mySecurity.MinTradeAmountType = minVolumeType;
            mySecurity.MinTradeAmount = minVolume;
            mySecurity.PriceLimitHigh = priceLimitHigh;
            mySecurity.PriceLimitLow = priceLimitLow;
            mySecurity.Go = collateral;
            mySecurity.Strike = strike;
            mySecurity.VolumeStep = volumeStep;

            if (Directory.Exists(@"Engine\ServerDopSettings") == false)
            {
                Directory.CreateDirectory(@"Engine\ServerDopSettings");
            }

            if (Directory.Exists(@"Engine\ServerDopSettings\" + _server.ServerType) == false)
            {
                Directory.CreateDirectory(@"Engine\ServerDopSettings\" + _server.ServerType);
            }

            string fileName = mySecurity.Name.RemoveExcessFromSecurityName();

            if (string.IsNullOrEmpty(mySecurity.NameId) == false)
            {
                fileName += "_" + mySecurity.NameId.RemoveExcessFromSecurityName();
            }

            if (string.IsNullOrEmpty(mySecurity.NameClass) == false)
            {
                fileName += "_" + mySecurity.NameClass.RemoveExcessFromSecurityName();
            }

            fileName += "_" + mySecurity.SecurityType.ToString().RemoveExcessFromSecurityName();


            string filePath = @"Engine\ServerDopSettings\" + _server.ServerType + "\\" + fileName + ".txt";

            try
            {
                using (StreamWriter writer = new StreamWriter(filePath, false)
                    )
                {
                    writer.WriteLine(mySecurity.GetSaveStr());

                    writer.Close();
                }
            }
            catch (Exception)
            {

            }
        }

        #region Search in securities grid

        private void TextBoxSearchSecurity_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            try
            {
                if (TextBoxSearchSecurity.Text == ""
                    && TextBoxSearchSecurity.IsKeyboardFocused == false)
                {
                    TextBoxSearchSecurity.Text = OsLocalization.Market.Label64;
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void TextBoxSearchSecurity_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            try
            {
                if (TextBoxSearchSecurity.Text == OsLocalization.Market.Label64)
                {
                    TextBoxSearchSecurity.Text = "";
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void TextBoxSearchSecurity_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            try
            {
                if (TextBoxSearchSecurity.Text == "")
                {
                    TextBoxSearchSecurity.Text = OsLocalization.Market.Label64;
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private List<int> _searchResults = new List<int>();

        private void TextBoxSearchSecurity_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateSearchResults();
            UpdateSearchPanel();
        }

        private void UpdateSearchResults()
        {
            try
            {
                _searchResults.Clear();

                string key = TextBoxSearchSecurity.Text;

                if (key == "")
                {
                    UpdateSearchPanel();
                    return;
                }

                key = key.ToLower();

                int indexFirstSec = int.MaxValue;

                for (int i = 0; i < _gridSecurities.Rows.Count; i++)
                {
                    string security = "";
                    string securityFullName = "";
                    string securityId = "";

                    if (_gridSecurities.Rows[i].Cells[1].Value != null)
                    {
                        security = _gridSecurities.Rows[i].Cells[1].Value.ToString();
                    }
                    if (_gridSecurities.Rows[i].Cells[2].Value != null)
                    {
                        securityFullName = _gridSecurities.Rows[i].Cells[2].Value.ToString();
                    }
                    if (_gridSecurities.Rows[i].Cells[3].Value != null)
                    {
                        securityId = _gridSecurities.Rows[i].Cells[3].Value.ToString();
                    }

                    security = security.ToLower();
                    securityFullName = securityFullName.ToLower();
                    securityId = securityId.ToLower();

                    if (security.Contains(key) || securityFullName.Contains(key) || securityId.Contains(key))
                    {
                        if (security.IndexOf(key) == 0 || securityFullName.IndexOf(key) == 0 || securityId.IndexOf(key) == 0)
                        {
                            indexFirstSec = i;
                        }

                        _searchResults.Add(i);
                    }
                }

                if (_searchResults.Count > 1 && _searchResults.Contains(indexFirstSec) && _searchResults.IndexOf(indexFirstSec) != 0)
                {
                    int index = _searchResults.IndexOf(indexFirstSec);
                    _searchResults.RemoveAt(index);
                    _searchResults.Insert(0, indexFirstSec);
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateSearchPanel()
        {
            try
            {
                if (_searchResults.Count == 0)
                {
                    ButtonRightInSearchResults.Visibility = Visibility.Hidden;
                    ButtonLeftInSearchResults.Visibility = Visibility.Hidden;
                    LabelCurrentResultShow.Visibility = Visibility.Hidden;
                    LabelCommasResultShow.Visibility = Visibility.Hidden;
                    LabelCountResultsShow.Visibility = Visibility.Hidden;
                    return;
                }

                int firstRow = _searchResults[0];

                _gridSecurities.Rows[firstRow].Selected = true;
                _gridSecurities.FirstDisplayedScrollingRowIndex = firstRow;

                if (_searchResults.Count < 2)
                {
                    ButtonRightInSearchResults.Visibility = Visibility.Hidden;
                    ButtonLeftInSearchResults.Visibility = Visibility.Hidden;
                    LabelCurrentResultShow.Visibility = Visibility.Hidden;
                    LabelCommasResultShow.Visibility = Visibility.Hidden;
                    LabelCountResultsShow.Visibility = Visibility.Hidden;
                    return;
                }

                LabelCurrentResultShow.Content = 1.ToString();
                LabelCountResultsShow.Content = (_searchResults.Count).ToString();

                ButtonRightInSearchResults.Visibility = Visibility.Visible;
                ButtonLeftInSearchResults.Visibility = Visibility.Visible;
                LabelCurrentResultShow.Visibility = Visibility.Visible;
                LabelCommasResultShow.Visibility = Visibility.Visible;
                LabelCountResultsShow.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonLeftInSearchResults_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int indexRow = Convert.ToInt32(LabelCurrentResultShow.Content) - 1;

                int maxRowIndex = Convert.ToInt32(LabelCountResultsShow.Content);

                if (indexRow <= 0)
                {
                    indexRow = maxRowIndex;
                    LabelCurrentResultShow.Content = maxRowIndex.ToString();
                }
                else
                {
                    LabelCurrentResultShow.Content = (indexRow).ToString();
                }

                int realInd = _searchResults[indexRow - 1];

                _gridSecurities.Rows[realInd].Selected = true;
                _gridSecurities.FirstDisplayedScrollingRowIndex = realInd;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonRightInSearchResults_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int indexRow = Convert.ToInt32(LabelCurrentResultShow.Content) - 1 + 1;

                int maxRowIndex = Convert.ToInt32(LabelCountResultsShow.Content);

                if (indexRow >= maxRowIndex)
                {
                    indexRow = 0;
                    LabelCurrentResultShow.Content = 1.ToString();
                }
                else
                {
                    LabelCurrentResultShow.Content = (indexRow + 1).ToString();
                }

                int realInd = _searchResults[indexRow];

                _gridSecurities.Rows[realInd].Selected = true;
                _gridSecurities.FirstDisplayedScrollingRowIndex = realInd;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void TextBoxSearchSecurity_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            try
            {
                if (e.Key == Key.Enter)
                {
                    int rowIndex = 0;
                    for (int i = 0; i < _gridSecurities.Rows.Count; i++)
                    {
                        if (_gridSecurities.Rows[i].Selected == true)
                        {
                            rowIndex = i;
                            break;
                        }
                        if (i == _gridSecurities.Rows.Count - 1)
                        {
                            return;
                        }
                    }

                    DataGridViewCheckBoxCell checkBox;
                    for (int i = 0; i < _gridSecurities.Rows.Count; i++)
                    {
                        checkBox = (DataGridViewCheckBoxCell)_gridSecurities.Rows[i].Cells[4];

                        if (checkBox.Value == null)
                        {
                            continue;
                        }
                        if (i == rowIndex)
                        {
                            continue;
                        }
                        if (Convert.ToBoolean(checkBox.Value) == true)
                        {
                            checkBox.Value = false;
                            break;
                        }
                    }

                    checkBox = (DataGridViewCheckBoxCell)_gridSecurities.Rows[rowIndex].Cells[4];
                    if (Convert.ToBoolean(checkBox.Value) == false)
                    {
                        checkBox.Value = true;
                        TextBoxSearchSecurity.Text = "";
                    }
                    else
                    {
                        checkBox.Value = false;
                        TextBoxSearchSecurity.Text = "";
                    }
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

    }
}