/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;

namespace OsEngine.Entity
{  
    public partial class SetLeverageUi : Window
    {
        private IServer _server;

        private IServerRealization _serverRealization;

        string _serverNameUnique;

        private decimal _defaultLeverage;

        public event Action<SecurityLeverageData> SecurityLeverageDataEvent;

        public SetLeverageUi(IServer server, IServerRealization serverRealization, string serverNameUnique)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            _server = server;
            _serverRealization = serverRealization;
            _serverNameUnique = serverNameUnique;
                        
            GetDefaultLeverageParameters();

            TextBoxLeverage.Text = _defaultLeverage.ToString();
            TextBoxLeverage.TextChanged += TextBoxLeverage_TextChanged;

            ButtonLoad.Content = OsLocalization.Entity.ButtonLoad;
            ButtonLoad.Click += ButtonLoad_Click;

            UpdateClassComboBox();
            ComboBoxClass.SelectionChanged += ComboBoxClass_SelectionChanged;

            CreateTable();
            PaintLeverageTable();

            Title = OsLocalization.Entity.TitleSetLeverageUi + " " + _server.ServerType;
            LabelClass.Content = OsLocalization.Entity.SecuritiesColumn11;
            TextBoxSearchLeverage.Text = OsLocalization.Market.Label64;

            this.Activate();
            this.Focus();

            this.Closed += SetLeverageUi_Closed;

            TextBoxSearchLeverage.MouseEnter += TextBoxSearchLeverage_MouseEnter;
            TextBoxSearchLeverage.TextChanged += TextBoxSearchLeverage_TextChanged;
            TextBoxSearchLeverage.MouseLeave += TextBoxSearchLeverage_MouseLeave;
            TextBoxSearchLeverage.LostKeyboardFocus += TextBoxSearchLeverage_LostKeyboardFocus;
            TextBoxSearchLeverage.KeyDown += TextBoxSearchLeverage_KeyDown;
            ButtonRightInSearchResults.Click += ButtonRightInSearchResults_Click;
            ButtonLeftInSearchResults.Click += ButtonLeftInSearchResults_Click;
        }

        private void GetDefaultLeverageParameters()
        {
            try
            {
                IServerPermission serverPermission = ServerMaster.GetServerPermission(_server.ServerType);

                if (!decimal.TryParse(serverPermission.Leverage_StandardValue.ToString().Replace(",", "."), out _defaultLeverage))
                {
                    _defaultLeverage = 1;
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void SetLeverageUi_Closed(object sender, EventArgs e)
        {
            this.Closed -= SetLeverageUi_Closed;

            TextBoxSearchLeverage.MouseEnter -= TextBoxSearchLeverage_MouseEnter;
            TextBoxSearchLeverage.TextChanged -= TextBoxSearchLeverage_TextChanged;
            TextBoxSearchLeverage.MouseLeave -= TextBoxSearchLeverage_MouseLeave;
            TextBoxSearchLeverage.LostKeyboardFocus -= TextBoxSearchLeverage_LostKeyboardFocus;
            TextBoxSearchLeverage.KeyDown -= TextBoxSearchLeverage_KeyDown;
            ButtonRightInSearchResults.Click -= ButtonRightInSearchResults_Click;
            ButtonLeftInSearchResults.Click -= ButtonLeftInSearchResults_Click;
            TextBoxLeverage.TextChanged -= TextBoxLeverage_TextChanged;
            ButtonLoad.Click -= ButtonLoad_Click;

            _dgv.Rows.Clear();
            _dgv.CellValueChanged -= _dgv_CellValueChanged;
            _dgv.DataError -= _dgv_DataError;
            _dgv.CellClick -= _dgv_CellClick;
            _dgv = null;
            HostLeverage.Child = null;
            _server = null;
            _serverRealization = null;
        }

        private void TextBoxLeverage_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                decimal.TryParse(TextBoxLeverage.Text.ToString().Replace(".", ","), out _defaultLeverage);

                for (int i = 1; i < _dgv.Rows.Count; i++)
                {
                    _dgv.Rows[i].Cells[6].Value = _defaultLeverage;
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ButtonLoad_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_dgv == null)
                {
                    return;
                }

                LoadLeverageFromFile();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }


        private DataGridView _dgv;

        private void CreateTable()
        {
            try
            {
                _dgv = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.AllCells);

                _dgv.Dock = DockStyle.Fill;
                _dgv.ScrollBars = ScrollBars.Both;
                _dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
                _dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
              
                _dgv.ColumnCount = 7;
                _dgv.RowCount = 0;

                _dgv.Columns[0].HeaderText = "#";
                _dgv.Columns[1].HeaderText = OsLocalization.Entity.SecuritiesColumn1; // Name
                _dgv.Columns[2].HeaderText = OsLocalization.Entity.SecuritiesColumn9; // Name Full
                _dgv.Columns[3].HeaderText = OsLocalization.Entity.SecuritiesColumn10; // Name ID
                _dgv.Columns[4].HeaderText = OsLocalization.Entity.SecuritiesColumn11; // Class
                _dgv.Columns[5].HeaderText = OsLocalization.Entity.SecuritiesColumn2; // Type
                _dgv.Columns[6].HeaderText = OsLocalization.Entity.LeverageColumn; // Leverage

                DataGridViewButtonColumn button = new();
                _dgv.Columns.Add(button);

                foreach (DataGridViewColumn column in _dgv.Columns)
                {
                    column.SortMode = DataGridViewColumnSortMode.NotSortable;
                    column.ReadOnly = true;
                }

                _dgv.Columns[6].ReadOnly = false;

                HostLeverage.Child = _dgv;
                HostLeverage.Child.Show();
                HostLeverage.Child.Refresh();

                _dgv.CellValueChanged += _dgv_CellValueChanged;
                _dgv.DataError += _dgv_DataError;
                _dgv.CellClick += _dgv_CellClick;
            }
            catch
            {                
            }
        }

        private void _dgv_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {                
                if (e.RowIndex > 0 && e.ColumnIndex == 7)
                {
                    decimal leverage = _defaultLeverage;

                    if (!decimal.TryParse(_dgv.Rows[e.RowIndex].Cells[6].Value.ToString().Replace(".", ","), out leverage))
                    {
                        _dgv.Rows[e.RowIndex].Cells[6].Value = _defaultLeverage;
                        leverage = _defaultLeverage;
                    }

                    for (int i = 0; i < _server.Securities.Count; i++)
                    {
                        Security sec = _server.Securities[i];

                        if (sec.Name == _dgv.Rows[e.RowIndex].Cells[1].Value.ToString() &&
                            sec.NameClass == _dgv.Rows[e.RowIndex].Cells[4].Value.ToString())
                        {
                            SecurityLeverageData data = new();
                            data.Security = sec;
                            data.Leverage = leverage;

                            SecurityLeverageDataEvent(data);
                        }
                    }
                }

                if (e.RowIndex == 0 && e.ColumnIndex == 7)
                {
                    for (int i = 1; i < _dgv.Rows.Count; i++)
                    {
                        string name = _dgv.Rows[i].Cells[1].Value.ToString();
                        string nameClass = _dgv.Rows[i].Cells[4].Value.ToString();

                        decimal leverage = _defaultLeverage;

                        if (!decimal.TryParse(_dgv.Rows[i].Cells[6].Value.ToString().Replace(".", ","), out leverage))
                        {
                            leverage = _defaultLeverage;
                        }

                        int index = _server.Securities.FindIndex(x => x.Name == name && x.NameClass == nameClass);

                        if (index < 0)
                        {
                            continue;
                        }

                        SecurityLeverageData data = new();
                        data.Security = _server.Securities[index];
                        data.Leverage = leverage;

                        SecurityLeverageDataEvent(data);
                    }
                }               
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void _dgv_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            ServerMaster.SendNewLogMessage(e.ToString(), Logging.LogMessageType.Error);
        }

        private void _dgv_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                if (e.ColumnIndex == 6 && e.RowIndex >= 0)
                {                    
                    decimal leverage = _defaultLeverage;
                    if (!decimal.TryParse(_dgv.Rows[e.RowIndex].Cells[6].Value.ToString().Replace(".", ","), out leverage))
                    {
                        _dgv.Rows[e.RowIndex].Cells[6].Value = _defaultLeverage;
                        return;
                    }                    
                }                
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void PaintLeverageTable()
        {
            try
            {
                if (_server.ListLeverageData == null || _server.ListLeverageData.Count == 0)
                {
                    return;
                }

                if (_dgv == null)
                {
                    return;
                }

                if (_dgv.InvokeRequired)
                {
                    _dgv.Invoke(PaintLeverageTable);
                    return;
                }

                if (ComboBoxClass.SelectedItem == null)
                {
                    return;
                }

                int num = 1;

                string selectedClass = ComboBoxClass.SelectedItem.ToString();

                List<DataGridViewRow> rows = new List<DataGridViewRow>();

                DataGridViewRow row = new DataGridViewRow();

                for (int i = 0; i < 7; i++)
                {
                    row.Cells.Add(new DataGridViewTextBoxCell());
                    row.Cells[i].Value = "";
                    row.ReadOnly = true;
                }

                row.Cells.Add(new DataGridViewButtonCell());
                row.Cells[7].Value = OsLocalization.ConvertToLocString("Eng:All accept_Ru:Принять всё_");

                rows.Add(row);

                for (int i = 0; i < _server.ListLeverageData.Count; i++)
                {
                    SecurityLeverageData curSec = _server.ListLeverageData[i];
                                        
                    if (selectedClass != "All"
                        && curSec.Security.NameClass != selectedClass)
                    {
                        continue;
                    }
                                        
                    DataGridViewRow nRow = new DataGridViewRow();

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[0].Value = num; num++;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[1].Value = curSec.Security.Name;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[2].Value = curSec.Security.NameFull;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[3].Value = curSec.Security.NameId;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[4].Value = curSec.Security.NameClass;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[5].Value = curSec.Security.SecurityType;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[6].Value = curSec.Leverage;

                    nRow.Cells.Add(new DataGridViewButtonCell());
                    nRow.Cells[7].Value = OsLocalization.Entity.ButtonAccept;

                    rows.Add(nRow);
                }

                HostLeverage.Child = null;

                _dgv.Rows.Clear();

                if (rows.Count > 0)
                {
                    _dgv.Rows.AddRange(rows.ToArray());
                }

                HostLeverage.Child = _dgv;

                UpdateSearchResults();
                UpdateSearchPanel();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ComboBoxClass_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            PaintLeverageTable();
        }

        private void UpdateClassComboBox()
        {
            try
            {
                if (ComboBoxClass.Dispatcher.CheckAccess() == false)
                {
                    ComboBoxClass.Dispatcher.Invoke(UpdateClassComboBox);
                    return;
                }

                string startClass = null;

                if (ComboBoxClass.SelectedItem != null)
                {
                    startClass = ComboBoxClass.SelectedItem.ToString();
                }

                List<string> classes = new List<string>();

                classes.Add("All");

                List<SecurityLeverageData> data = _server.ListLeverageData;

                for (int i = 0; data != null && i < data.Count; i++)
                {                    
                    string curClass = data[i].Security.NameClass;

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
                    && data.Count > 10000
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

        private void LoadLeverageFromFile()
        {
            try
            {
                List<SecurityLeverageData> listLeverageFromFile = new ();

                string fileName = _serverNameUnique + "_SecuritiesLeverage";

                string filePath = @"Engine\ServerDopSettings\" + fileName + ".txt";

                if (!File.Exists(filePath))
                {
                    return;
                }

                using (StreamReader reader = new StreamReader(filePath))
                {
                    string line;

                    while ((line = reader.ReadLine()) != null)
                    {
                        if (!string.IsNullOrEmpty(line))
                        {
                            string[] split = line.Split('|');

                            decimal leverage = 0;

                            if (!decimal.TryParse(split[2].Replace(",", "."), out leverage))
                            {
                                leverage = _defaultLeverage;
                            }

                            int index = _server.Securities.FindIndex(x => x.Name == split[0] && x.NameClass == split[1]);

                            if (index >= 0)
                            {
                                SecurityLeverageData list = new();
                                list.Security = _server.Securities[index];
                                list.Leverage = leverage;

                                listLeverageFromFile.Add(list);
                            }
                        }
                    }
                    reader.Close();
                }                

                for (int i = 1; i < _dgv.Rows.Count; i++)
                {
                    string name = _dgv.Rows[i].Cells[1].Value.ToString();
                    string nameClass = _dgv.Rows[i].Cells[4].Value.ToString();

                    int index = listLeverageFromFile.FindIndex(x => x.Security.Name == name && x.Security.NameClass == nameClass);

                    if (index < 0)
                    {
                        continue;
                    }

                    _dgv.Rows[i].Cells[6].Value = listLeverageFromFile[index].Leverage;
                }

            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #region Search

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

                _dgv.Rows[realInd].Selected = true;
                _dgv.FirstDisplayedScrollingRowIndex = realInd;
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

                _dgv.Rows[realInd].Selected = true;
                _dgv.FirstDisplayedScrollingRowIndex = realInd;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void TextBoxSearchLeverage_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            try
            {
                if (e.Key == Key.Enter)
                {
                    int rowIndex = 0;
                    for (int i = 0; i < _dgv.Rows.Count; i++)
                    {
                        if (_dgv.Rows[i].Selected == true)
                        {
                            rowIndex = i;
                            break;
                        }
                        if (i == _dgv.Rows.Count - 1)
                        {
                            return;
                        }
                    }

                    DataGridViewCheckBoxCell checkBox;
                    for (int i = 0; i < _dgv.Rows.Count; i++)
                    {
                        checkBox = (DataGridViewCheckBoxCell)_dgv.Rows[i].Cells[4];

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

                    checkBox = (DataGridViewCheckBoxCell)_dgv.Rows[rowIndex].Cells[4];
                    if (Convert.ToBoolean(checkBox.Value) == false)
                    {
                        checkBox.Value = true;
                        TextBoxSearchLeverage.Text = "";
                    }
                    else
                    {
                        checkBox.Value = false;
                        TextBoxSearchLeverage.Text = "";
                    }
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void TextBoxSearchLeverage_LostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            try
            {
                if (TextBoxSearchLeverage.Text == "")
                {
                    TextBoxSearchLeverage.Text = OsLocalization.Market.Label64;
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void TextBoxSearchLeverage_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            try
            {
                if (TextBoxSearchLeverage.Text == ""
                    && TextBoxSearchLeverage.IsKeyboardFocused == false)
                {
                    TextBoxSearchLeverage.Text = OsLocalization.Market.Label64;
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private List<int> _searchResults = new List<int>();

        private void TextBoxSearchLeverage_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateSearchResults();
            UpdateSearchPanel();
        }

        private void UpdateSearchResults()
        {
            try
            {
                _searchResults.Clear();

                string key = TextBoxSearchLeverage.Text;

                if (key == "")
                {
                    UpdateSearchPanel();
                    return;
                }

                key = key.ToLower();

                int indexFirstSec = int.MaxValue;

                for (int i = 0; i < _dgv.Rows.Count; i++)
                {
                    string security = "";
                    string securityFullName = "";
                    string securityId = "";

                    if (_dgv.Rows[i].Cells[1].Value != null)
                    {
                        security = _dgv.Rows[i].Cells[1].Value.ToString();
                    }
                    if (_dgv.Rows[i].Cells[2].Value != null)
                    {
                        securityFullName = _dgv.Rows[i].Cells[2].Value.ToString();
                    }
                    if (_dgv.Rows[i].Cells[3].Value != null)
                    {
                        securityId = _dgv.Rows[i].Cells[3].Value.ToString();
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

                _dgv.Rows[firstRow].Selected = true;
                _dgv.FirstDisplayedScrollingRowIndex = firstRow;

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

        private void TextBoxSearchLeverage_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            try
            {
                if (TextBoxSearchLeverage.Text == OsLocalization.Market.Label64)
                {
                    TextBoxSearchLeverage.Text = "";
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion    
    }

    public class SecurityLeverageData
    {
        public decimal Leverage;
        public Security Security;
    }
}
