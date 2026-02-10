/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Bybit.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;

namespace OsEngine.Entity
{  
    public partial class SetLeverageUi : Window
    {
        #region Constructor

        private IServer _server;

        private string _serverNameUnique;

        private IServerPermission _serverPermission;

        private Dictionary<string, ClassLeverageData> _listLeverageData = new();

        private decimal _textBoxLeverage;

        public event Action<SecurityLeverageData> SecurityLeverageDataEvent;

        public SetLeverageUi(IServer server, IServerRealization serverRealization, string serverNameUnique)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            _server = server;
            _serverNameUnique = serverNameUnique;

            _serverPermission = ServerMaster.GetServerPermission(_server.ServerType);

            _listLeverageData = _server.ListLeverageData;

            Title = OsLocalization.Entity.TitleSetLeverageUi + " " + serverNameUnique;

            TabItemCommonSettings.Header = OsLocalization.Entity.TabItemCommonSettings;
            TabItemInstrumentSettings.Header = OsLocalization.Entity.TabItemInstrumentSettings;

            LabelCommonHedgeMode.Content = OsLocalization.Entity.LabelHedgeMode;
            LabelCommonLeverage.Content = OsLocalization.Entity.LabelLeverage;
            LabelCommonMarginMode.Content = OsLocalization.Entity.LabelMarginMode;

            ComboBoxCommonMarginMode.SelectionChanged += ComboBoxCommonMarginMode_SelectionChanged;
            ComboBoxCommonHedgeMode.SelectionChanged += ComboBoxCommonHedgeMode_SelectionChanged;
            
            ButtonCommonHedgeMode.Content = OsLocalization.Entity.ButtonAccept;
            ButtonCommonHedgeMode.Click += ButtonCommonHedgeMode_Click;

            ButtonCommonLeverage.Content = OsLocalization.Entity.ButtonAccept;
            ButtonCommonLeverage.Click += ButtonCommonLeverage_Click;

            ButtonCommonMarginMode.Content = OsLocalization.Entity.ButtonAccept;
            ButtonCommonMarginMode.Click += ButtonCommonMarginMode_Click;

            TextBoxSearchLeverage.Text = OsLocalization.Market.Label64;
            LabelLeverage.Content = OsLocalization.Entity.LabelLeverage;

            TextBoxLeverage.Text = _textBoxLeverage.ToString();
            TextBoxLeverage.TextChanged += TextBoxLeverage_TextChanged;

            LabelHedge.Content = OsLocalization.Entity.LabelHedgeMode;
            ComboBoxHedge.SelectionChanged += ComboBoxHedge_SelectionChanged;

            LabelMargin.Content = OsLocalization.Entity.LabelMarginMode;
            ComboBoxMargin.SelectionChanged += ComboBoxMargin_SelectionChanged;

            ButtonLoad.Content = OsLocalization.Entity.ButtonLoad;
            ButtonLoad.Click += ButtonLoad_Click;

            ButtonAcceptAll.Content = OsLocalization.ConvertToLocString("Eng:Accept all_Ru:Принять всё_");
            ButtonAcceptAll.Click += ButtonAcceptAll_Click;
                       
            CreateTable();
            PaintLeverageTable();

            LabelClass.Content = OsLocalization.Entity.SecuritiesColumn11;
            ComboBoxClass.SelectionChanged += ComboBoxClass_SelectionChanged;
            UpdateClassComboBox();

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

        private void SetLeverageUi_Closed(object sender, EventArgs e)
        {
            this.Closed -= SetLeverageUi_Closed;

            ComboBoxCommonMarginMode.SelectionChanged -= ComboBoxCommonMarginMode_SelectionChanged;
            ComboBoxCommonHedgeMode.SelectionChanged -= ComboBoxCommonHedgeMode_SelectionChanged;

            ButtonCommonHedgeMode.Click -= ButtonCommonHedgeMode_Click;
            ButtonCommonLeverage.Click -= ButtonCommonLeverage_Click;
            ButtonCommonMarginMode.Click -= ButtonCommonMarginMode_Click;

            TextBoxLeverage.TextChanged -= TextBoxLeverage_TextChanged;
            ComboBoxHedge.SelectionChanged -= ComboBoxHedge_SelectionChanged;
            ComboBoxMargin.SelectionChanged -= ComboBoxMargin_SelectionChanged;

            ButtonLoad.Click -= ButtonLoad_Click;
            ButtonAcceptAll.Click -= ButtonAcceptAll_Click;

            ComboBoxClass.SelectionChanged -= ComboBoxClass_SelectionChanged;

            TextBoxSearchLeverage.MouseEnter -= TextBoxSearchLeverage_MouseEnter;
            TextBoxSearchLeverage.TextChanged -= TextBoxSearchLeverage_TextChanged;
            TextBoxSearchLeverage.MouseLeave -= TextBoxSearchLeverage_MouseLeave;
            TextBoxSearchLeverage.LostKeyboardFocus -= TextBoxSearchLeverage_LostKeyboardFocus;
            TextBoxSearchLeverage.KeyDown -= TextBoxSearchLeverage_KeyDown;
            ButtonRightInSearchResults.Click -= ButtonRightInSearchResults_Click;
            ButtonLeftInSearchResults.Click -= ButtonLeftInSearchResults_Click;      

            _dgv.Rows.Clear();
            _dgv.CellValueChanged -= _dgv_CellValueChanged;
            _dgv.DataError -= _dgv_DataError;
            _dgv.CellClick -= _dgv_CellClick;
            _dgv = null;
            HostLeverage.Child = null;
            _server = null;
        }

        #endregion

        #region Common settings

        private void ComboBoxClass_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                RepaintTable();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ComboBoxCommonMarginMode_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                string selectedClass = ComboBoxClass.SelectedItem.ToString();
                string selectedMargin = ComboBoxCommonMarginMode.SelectedItem.ToString();

                _listLeverageData[selectedClass].CommmonMarginMode = selectedMargin;

                if (_serverPermission.Leverage_Permission.ContainsKey(selectedClass))
                {
                    if (_serverPermission.Leverage_Permission[selectedClass].Leverage_CantBeLeverage.Contains(selectedMargin))
                    {
                        for (int i = 0; i < _listLeverageData[selectedClass].SecurityData.Count; i++)
                        {
                            SecurityLeverageData data = _listLeverageData[selectedClass].SecurityData[i];

                            data.Leverage = "";
                            data.LeverageLong = "";
                            data.LeverageShort = "";
                        }
                    }
                    else
                    {
                        for (int i = 0; i < _listLeverageData[selectedClass].SecurityData.Count; i++)
                        {
                            SecurityLeverageData data = _listLeverageData[selectedClass].SecurityData[i];

                            data.Leverage = _serverPermission.Leverage_Permission[selectedClass].Leverage_StandardValue.ToString();
                            data.LeverageLong = "";
                            data.LeverageShort = "";

                            if (_serverPermission.Leverage_Permission[selectedClass].Leverage_IndividualLongShort)
                            {
                                if (_serverPermission.Leverage_Permission[selectedClass].Leverage_SupportClassesIndividualLongShort.Contains(selectedMargin))
                                {
                                    data.Leverage = "";
                                    data.LeverageLong = _serverPermission.Leverage_Permission[selectedClass].Leverage_StandardValue.ToString();
                                    data.LeverageShort = _serverPermission.Leverage_Permission[selectedClass].Leverage_StandardValue.ToString();
                                }
                            }
                        }
                    }
                }

                if (_serverPermission.HedgeMode_Permission.ContainsKey(selectedClass))
                {
                    if (_serverPermission.HedgeMode_Permission[selectedClass].HedgeMode_CantBeHedgeMode.Contains(selectedMargin))
                    {
                        for (int i = 0; i < _listLeverageData[selectedClass].SecurityData.Count; i++)
                        {
                            SecurityLeverageData data = _listLeverageData[selectedClass].SecurityData[i];

                            data.HedgeMode = "";                            
                        }

                        _listLeverageData[selectedClass].CommonHedgeMode = "";
                    }
                    else
                    {
                        if (_serverPermission.HedgeMode_Permission[selectedClass].HedgeMode_CommonMode)
                        {
                            for (int i = 0; i < _listLeverageData[selectedClass].SecurityData.Count; i++)
                            {
                                SecurityLeverageData data = _listLeverageData[selectedClass].SecurityData[i];

                                data.HedgeMode = "";
                            }

                            ComboBoxCommonHedgeMode.SelectedItem = _serverPermission.HedgeMode_Permission[selectedClass].HedgeMode_StandardValue;
                            _listLeverageData[selectedClass].CommonHedgeMode = _serverPermission.HedgeMode_Permission[selectedClass].HedgeMode_StandardValue;
                        }
                        else
                        {
                            for (int i = 0; i < _listLeverageData[selectedClass].SecurityData.Count; i++)
                            {
                                SecurityLeverageData data = _listLeverageData[selectedClass].SecurityData[i];

                                data.HedgeMode = _serverPermission.HedgeMode_Permission[selectedClass].HedgeMode_StandardValue;
                            }

                            _listLeverageData[selectedClass].CommonHedgeMode = "";
                        }
                    }
                }
               
                RepaintTable();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ComboBoxCommonHedgeMode_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                string selectedClass = ComboBoxClass.SelectedItem.ToString();
                string selectedHedge = ComboBoxCommonHedgeMode.SelectedItem.ToString();
                string selectedMargin = ComboBoxCommonMarginMode.SelectedItem?.ToString();

                _listLeverageData[selectedClass].CommonHedgeMode = selectedHedge;

                if (!_serverPermission.HedgeMode_Permission.ContainsKey(selectedClass))
                {
                    return;
                }

                if (_serverPermission.HedgeMode_Permission[selectedClass].HedgeMode_CantBeHedgeMode.Contains(selectedHedge))
                {
                    for (int i = 0; i < _listLeverageData[selectedClass].SecurityData.Count; i++)
                    {
                        SecurityLeverageData data = _listLeverageData[selectedClass].SecurityData[i];

                        data.Leverage = "";
                        data.LeverageLong = "";
                        data.LeverageShort = "";
                    }
                }
                else
                {                    
                    for (int i = 0; i < _listLeverageData[selectedClass].SecurityData.Count; i++)
                    {
                        SecurityLeverageData data = _listLeverageData[selectedClass].SecurityData[i];

                        data.Leverage = _serverPermission.Leverage_Permission[selectedClass].Leverage_StandardValue.ToString();
                        data.LeverageLong = "";
                        data.LeverageShort = "";

                        if (_serverPermission.Leverage_Permission[selectedClass].Leverage_IndividualLongShort)
                        {                           
                            if (_serverPermission.Leverage_Permission[selectedClass].Leverage_SupportClassesIndividualLongShort.Contains(selectedMargin) ||
                                _serverPermission.Leverage_Permission[selectedClass].Leverage_SupportClassesIndividualLongShort.Contains(data.MarginMode))
                            {
                                if (selectedHedge == "On")
                                {
                                    data.Leverage = "";
                                    data.LeverageLong = _serverPermission.Leverage_Permission[selectedClass].Leverage_StandardValue.ToString();
                                    data.LeverageShort = _serverPermission.Leverage_Permission[selectedClass].Leverage_StandardValue.ToString();
                                }
                            }                            
                        }
                    }                    
                }

                RepaintTable();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ButtonCommonMarginMode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string selectedClass = ComboBoxClass.SelectedItem.ToString();

                string marginMode = ComboBoxCommonMarginMode.Text;

                if (CheckOpenPositions("", selectedClass))
                {
                    return;
                }

                _server.SetCommonMarginMode(selectedClass, marginMode);
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ButtonCommonLeverage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string selectedClass = ComboBoxClass.SelectedItem.ToString();

                string leverage = TextBoxCommonLeverage.Text;

                if (CheckOpenPositions("", selectedClass))
                {
                    return;
                }

                _server.SetCommonLeverage(selectedClass, leverage);
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ButtonCommonHedgeMode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string selectedClass = ComboBoxClass.SelectedItem.ToString();

                string hedgeMode = ComboBoxCommonHedgeMode.Text;

                if (CheckOpenPositions("", selectedClass))
                {
                    return;
                }

                _server.SetCommonHedgeMode(selectedClass, hedgeMode);
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion

        #region Individual settings

        private void TextBoxLeverage_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                decimal.TryParse(TextBoxLeverage.Text.ToString().Replace(".", ","), out _textBoxLeverage);

                string selectedClass = ComboBoxClass.SelectedItem.ToString();

                if (!_serverPermission.Leverage_Permission[selectedClass].Leverage_CommonMode)
                {
                    for (int i = 0; i < _dgv.Rows.Count; i++)
                    {
                        if (_dgv.Rows[i].Cells[6].Value != null && _dgv.Rows[i].Cells[6].Value.ToString() != "")
                        {
                            _dgv.Rows[i].Cells[6].Value = _textBoxLeverage;
                        }

                        if (_dgv.Rows[i].Cells[7].Value != null && _dgv.Rows[i].Cells[7].Value.ToString() != "")
                        {
                            _dgv.Rows[i].Cells[7].Value = _textBoxLeverage;
                        }

                        if (_dgv.Rows[i].Cells[8].Value != null && _dgv.Rows[i].Cells[8].Value.ToString() != "")
                        {
                            _dgv.Rows[i].Cells[8].Value = _textBoxLeverage;
                        }
                    }
                }          
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ComboBoxMargin_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                string value = ComboBoxMargin.SelectedItem.ToString();
              
                for (int i = 0; i < _dgv.Rows.Count; i++)
                {
                    _dgv.Rows[i].Cells["MarginModeColumn"].Value = value;
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ComboBoxHedge_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                string value = ComboBoxHedge.SelectedItem.ToString();

                for (int i = 0; i < _dgv.Rows.Count; i++)
                {
                    _dgv.Rows[i].Cells["HedgeModeColumn"].Value = value;
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ButtonAcceptAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_dgv == null)
                {
                    return;
                }

                for (int i = 0; i < _dgv.Rows.Count; i++)
                {
                    string leverage = _dgv.Rows[i].Cells["CommonLeverageColumn"].Value.ToString();
                    string leverageLong = _dgv.Rows[i].Cells["LongLeverageColumn"].Value.ToString();
                    string leverageShort = _dgv.Rows[i].Cells["ShortLeverageColumn"].Value.ToString();
                    string hedgeMode = _dgv.Rows[i].Cells["HedgeModeColumn"].Value.ToString();
                    string marginMode = _dgv.Rows[i].Cells["MarginModeColumn"].Value.ToString();

                    SecurityLeverageData data = new();
                    data.Leverage = leverage;
                    data.LeverageLong = leverageLong;
                    data.LeverageShort = leverageShort;
                    data.HedgeMode = hedgeMode;
                    data.MarginMode = marginMode;
                    data.SecurityName = _dgv.Rows[i].Cells[1].Value.ToString();
                    data.ClassName = _dgv.Rows[i].Cells[4].Value.ToString();

                    if (CheckOpenPositions(data.SecurityName, data.ClassName))
                    {
                        return;
                    }

                    SecurityLeverageDataEvent(data);
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
                _dgv.AllowUserToOrderColumns = false;

                _dgv.ColumnCount = 9;
                _dgv.RowCount = 0;

                _dgv.Columns[0].HeaderText = "#";
                _dgv.Columns[1].HeaderText = OsLocalization.Entity.SecuritiesColumn1; // Name
                _dgv.Columns[2].HeaderText = OsLocalization.Entity.SecuritiesColumn9; // Name Full
                _dgv.Columns[3].HeaderText = OsLocalization.Entity.SecuritiesColumn10; // Name ID
                _dgv.Columns[4].HeaderText = OsLocalization.Entity.SecuritiesColumn11; // Class
                _dgv.Columns[5].HeaderText = OsLocalization.Entity.SecuritiesColumn2; // Type
                _dgv.Columns[6].HeaderText = OsLocalization.Entity.LeverageCommonColumn; // Leverage
                _dgv.Columns[6].Name = "CommonLeverageColumn";
                _dgv.Columns[7].HeaderText = OsLocalization.Entity.LeverageLongColumn; // Leverage Long
                _dgv.Columns[7].Name = "LongLeverageColumn";
                _dgv.Columns[8].HeaderText = OsLocalization.Entity.LeverageShortColumn; // Leverage Short
                _dgv.Columns[8].Name = "ShortLeverageColumn";
               
                DataGridViewComboBoxColumn cb = new();
                cb.HeaderText = OsLocalization.Entity.HedgeModeColumn; // HedgeMode
                cb.Name = "HedgeModeColumn";
                _dgv.Columns.Add(cb);

                cb = new();
                cb.HeaderText = OsLocalization.Entity.MarginModeColumn; // MarginMode
                cb.Name = "MarginModeColumn";
                _dgv.Columns.Add(cb);

                DataGridViewButtonColumn button = new();
                _dgv.Columns.Add(button);

                foreach (DataGridViewColumn column in _dgv.Columns)
                {
                    column.SortMode = DataGridViewColumnSortMode.NotSortable;
                }

                _dgv.Columns[0].ReadOnly = true;
                _dgv.Columns[1].ReadOnly = true;
                _dgv.Columns[2].ReadOnly = true;
                _dgv.Columns[3].ReadOnly = true;
                _dgv.Columns[4].ReadOnly = true;
                _dgv.Columns[5].ReadOnly = true;
                _dgv.Columns[9].ReadOnly = true;
                _dgv.Columns[10].ReadOnly = true;

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
                if (e.RowIndex > -1 && e.ColumnIndex == 11)
                {
                    string leverage = _dgv.Rows[e.RowIndex].Cells["CommonLeverageColumn"].Value.ToString();
                    string leverageLong = _dgv.Rows[e.RowIndex].Cells["LongLeverageColumn"].Value.ToString();
                    string leverageShort = _dgv.Rows[e.RowIndex].Cells["ShortLeverageColumn"].Value.ToString();
                    string hedgeMode = _dgv.Rows[e.RowIndex].Cells["HedgeModeColumn"].Value.ToString();
                    string marginMode = _dgv.Rows[e.RowIndex].Cells["MarginModeColumn"].Value.ToString();

                    if (leverage == "" &&
                        leverageLong == "" &&
                        leverageShort == "" &&
                        hedgeMode == "" &&
                        marginMode == "")
                    {
                        return;
                    }

                    SecurityLeverageData data = new();
                    data.Leverage = leverage;
                    data.LeverageLong = leverageLong;
                    data.LeverageShort = leverageShort;
                    data.HedgeMode = hedgeMode;
                    data.MarginMode = marginMode;
                    data.SecurityName = _dgv.Rows[e.RowIndex].Cells[1].Value.ToString();
                    data.ClassName = _dgv.Rows[e.RowIndex].Cells[4].Value.ToString();

                    if (CheckOpenPositions(data.SecurityName, data.ClassName))
                    {
                        return;
                    }

                    SecurityLeverageDataEvent(data);
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private bool CheckOpenPositions(string securityName, string className)
        {            
            for (int i = 0; i < _server.Portfolios.Count; i++)
            {
                List<PositionOnBoard> positions = _server.Portfolios[i].GetPositionOnBoard();

                for (int j = 0; j < positions.Count; j++)
                {
                    string pos = positions[j].SecurityNameCode;

                    if (positions[j].SecurityNameCode.Contains("_LONG"))
                    {
                        pos = positions[j].SecurityNameCode.Split("_LONG")[0];
                    }

                    if (positions[j].SecurityNameCode.Contains("_SHORT"))
                    {
                        pos = positions[j].SecurityNameCode.Split("_SHORT")[0];
                    }

                    if (positions[j].SecurityNameCode.Contains("_BOTH"))
                    {
                        pos = positions[j].SecurityNameCode.Split("_BOTH")[0];
                    }

                    if (pos == securityName)
                    {
                        if (positions[j].ValueCurrent != 0)
                        {
                            if (_serverPermission.Leverage_Permission[className].Leverage_CheckOpenPosition)
                            {
                                ServerMaster.SendNewLogMessage(
                                    OsLocalization.ConvertToLocString($"Eng:Unable to change Leverage for instrument {securityName}, because it has open positions._" +
                                    $"Ru:Невозможно поменять плечо у инструмента {securityName}, т.к. у него есть открытые позиции._"), Logging.LogMessageType.Error);
                                return true;
                            }

                            if (_serverPermission.HedgeMode_Permission[className].HedgeMode_CheckOpenPosition)
                            {
                                ServerMaster.SendNewLogMessage(
                                    OsLocalization.ConvertToLocString($"Eng:Unable to change HedgeMode for instrument {securityName}, because it has open positions._" +
                                    $"Ru:Невозможно поменять хедж режим у инструмента {securityName}, т.к. у него есть открытые позиции._"), Logging.LogMessageType.Error);
                                return true;
                            }

                            if (_serverPermission.MarginMode_Permission[className].MarginMode_CheckOpenPosition)
                            {
                                ServerMaster.SendNewLogMessage(
                                    OsLocalization.ConvertToLocString($"Eng:Unable to change MarginMode for instrument {securityName}, because it has open positions._" +
                                    $"Ru:Невозможно поменять режим маржи у инструмента {securityName}, т.к. у него есть открытые позиции._"), Logging.LogMessageType.Error);
                                return true;
                            }
                        }
                    }

                    if (securityName == "")
                    {
                        int index = _server.Securities.FindIndex(x => x.Name == pos);

                        if (index < 0)
                        {
                            continue;
                        }

                        if (_server.Securities[index].NameClass != className)
                        {
                            continue;
                        }

                        if (positions[j].ValueCurrent != 0)
                        {
                            if (_serverPermission.Leverage_Permission[className].Leverage_CheckOpenPosition)
                            {
                                ServerMaster.SendNewLogMessage(
                                    OsLocalization.ConvertToLocString($"Eng:Unable to change Leverage for class {className}, because it has open positions._" +
                                    $"Ru:Невозможно поменять плечо у класса {className}, т.к. у него есть открытые позиции._"), Logging.LogMessageType.Error);
                                return true;
                            }

                            if (_serverPermission.HedgeMode_Permission[className].HedgeMode_CheckOpenPosition)
                            {
                                ServerMaster.SendNewLogMessage(
                                    OsLocalization.ConvertToLocString($"Eng:Unable to change HedgeMode for class {className}, because it has open positions._" +
                                    $"Ru:Невозможно поменять хедж режим у класса {className}, т.к. у него есть открытые позиции._"), Logging.LogMessageType.Error);
                                return true;
                            }

                            if (_serverPermission.MarginMode_Permission[className].MarginMode_CheckOpenPosition)
                            {
                                ServerMaster.SendNewLogMessage(
                                    OsLocalization.ConvertToLocString($"Eng:Unable to change MarginMode for class {className}, because it has open positions._" +
                                    $"Ru:Невозможно поменять режим маржи у класса {className}, т.к. у него есть открытые позиции._"), Logging.LogMessageType.Error);
                                return true;
                            }
                        }
                    }
                }                    
            }
                
            return false; 
        }

        private void _dgv_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            ServerMaster.SendNewLogMessage(e.ToString(), Logging.LogMessageType.Error);
        }

        private void _dgv_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                if (e.ColumnIndex == 6 ||
                    e.ColumnIndex == 7 ||
                    e.ColumnIndex == 8)
                {
                    if (e.RowIndex >= 0)
                    {
                        if (!decimal.TryParse(_dgv.Rows[e.RowIndex].Cells[e.ColumnIndex].Value.ToString().Replace(".", ","), out decimal leverage))
                        {
                            _dgv.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = "";
                        }
                    }
                }

                if (e.ColumnIndex == 10 && e.RowIndex >= 0)
                {
                    string selectedClass = ComboBoxClass.SelectedItem.ToString();
                    string selectedMargin = _dgv.Rows[e.RowIndex].Cells[e.ColumnIndex].Value.ToString();

                    if (_serverPermission.Leverage_Permission[selectedClass].Leverage_SupportClassesIndividualLongShort.Contains(selectedMargin))
                    {
                        if (_listLeverageData[selectedClass].CommonHedgeMode == "On" || _dgv.Rows[e.RowIndex].Cells[e.ColumnIndex].Value.ToString() == "On")
                        {
                            _dgv.Rows[e.RowIndex].Cells[6].Value = "";
                            _dgv.Rows[e.RowIndex].Cells[7].Value = _textBoxLeverage;
                            _dgv.Rows[e.RowIndex].Cells[8].Value = _textBoxLeverage;

                            _dgv.Rows[e.RowIndex].Cells[6].ReadOnly = true;
                            _dgv.Rows[e.RowIndex].Cells[7].ReadOnly = false;
                            _dgv.Rows[e.RowIndex].Cells[8].ReadOnly = false;
                        }
                        else
                        {
                            _dgv.Rows[e.RowIndex].Cells[6].Value = _textBoxLeverage;
                            _dgv.Rows[e.RowIndex].Cells[7].Value = "";
                            _dgv.Rows[e.RowIndex].Cells[8].Value = "";

                            _dgv.Rows[e.RowIndex].Cells[6].ReadOnly = false;
                            _dgv.Rows[e.RowIndex].Cells[7].ReadOnly = true;
                            _dgv.Rows[e.RowIndex].Cells[8].ReadOnly = true;
                        }
                    }
                    else
                    {
                        _dgv.Rows[e.RowIndex].Cells[6].Value = _textBoxLeverage;
                        _dgv.Rows[e.RowIndex].Cells[7].Value = "";
                        _dgv.Rows[e.RowIndex].Cells[8].Value = "";

                        _dgv.Rows[e.RowIndex].Cells[6].ReadOnly = false;
                        _dgv.Rows[e.RowIndex].Cells[7].ReadOnly = true;
                        _dgv.Rows[e.RowIndex].Cells[8].ReadOnly = true;
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
                if (_listLeverageData == null || _listLeverageData.Count == 0)
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

                for (int i = 0; i < _listLeverageData[selectedClass].SecurityData.Count; i++)
                {
                    SecurityLeverageData curSec = _listLeverageData[selectedClass].SecurityData[i];

                    int index = _server.Securities.FindIndex(x => x.Name == curSec.SecurityName);

                    if (index < 0)
                    {
                        continue;
                    }

                    DataGridViewRow nRow = new DataGridViewRow();

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[0].Value = num; 
                    num++;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[1].Value = _server.Securities[index].Name;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[2].Value = _server.Securities[index].NameFull;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[3].Value = _server.Securities[index].NameId;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[4].Value = _server.Securities[index].NameClass;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[5].Value = _server.Securities[index].SecurityType;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[6].Value = curSec.Leverage;

                    if (curSec.Leverage == "")
                    {
                        nRow.Cells[6].ReadOnly = true;
                    }
                    else
                    {
                        nRow.Cells[6].ReadOnly = false;
                    }

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[7].Value = curSec.LeverageLong;

                    if (curSec.LeverageLong == "")
                    {
                        nRow.Cells[7].ReadOnly = true;
                    }
                    else
                    {
                        nRow.Cells[7].ReadOnly = false;
                    }

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[8].Value = curSec.LeverageShort;

                    if (curSec.LeverageShort == "")
                    {
                        nRow.Cells[8].ReadOnly = true;
                    }
                    else
                    {
                        nRow.Cells[8].ReadOnly = false;
                    }

                    DataGridViewComboBoxCell comboBoxCell = new();

                    /*if (_serverPermission.HedgeMode_Permission.ContainsKey(selectedClass))
                    {
                        comboBoxCell.Items.AddRange(_serverPermission.HedgeMode_Permission[selectedClass].HedgeMode_SupportMode);
                        comboBoxCell.Value = curSec.HedgeMode;
                    }
                    else
                    {
                        comboBoxCell.Value = "";
                    }*/

                    if (curSec.HedgeMode == "")
                    {
                        comboBoxCell.Items.Clear();
                        comboBoxCell.Value = "";
                    }
                    else
                    {
                        comboBoxCell.Items.AddRange(_serverPermission.HedgeMode_Permission[selectedClass].HedgeMode_SupportMode);
                        comboBoxCell.Value = curSec.HedgeMode;
                    }

                    nRow.Cells.Add(comboBoxCell);

                    comboBoxCell = new();

                    if (_serverPermission.MarginMode_Permission.ContainsKey(selectedClass))
                    {
                        comboBoxCell.Items.AddRange(_serverPermission.MarginMode_Permission[selectedClass].MarginMode_SupportMode);
                        comboBoxCell.Value = curSec.MarginMode;
                    }
                    else
                    {
                        comboBoxCell.Value = "";
                    }

                    nRow.Cells.Add(comboBoxCell);

                    nRow.Cells.Add(new DataGridViewButtonCell());
                    nRow.Cells[^1].Value = OsLocalization.Entity.ButtonAccept;

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

        private void RepaintTable()
        {
            SetSettingsTable();
            PaintLeverageTable();
        }

        private void SetSettingsTable()
        {
            if (MainWindow.GetDispatcher.CheckAccess() == false)
            {
                MainWindow.GetDispatcher.Invoke(new Action(SetSettingsTable));
                return;
            }

            TextBoxCommonLeverage.IsEnabled = false;
            ButtonCommonLeverage.IsEnabled = false;
            TextBoxLeverage.IsEnabled = false;

            string selectedClass = ComboBoxClass.SelectedItem.ToString();

            if (_serverPermission.Leverage_IsSupports)
            {
                if (_serverPermission.Leverage_Permission.ContainsKey(selectedClass))
                {
                    LeveragePermission permissionLeverage = _serverPermission.Leverage_Permission[selectedClass];

                    _textBoxLeverage = permissionLeverage.Leverage_StandardValue;

                    if (permissionLeverage.Leverage_CommonMode)
                    {
                        TextBoxCommonLeverage.Text = _listLeverageData[selectedClass].CommonLeverage;
                        TextBoxCommonLeverage.IsEnabled = true;
                        ButtonCommonLeverage.IsEnabled = true;
                    }
                    else
                    {
                        TextBoxLeverage.IsEnabled = true;
                        TextBoxLeverage.Text = permissionLeverage.Leverage_StandardValue.ToString();
                    }
                }
            }

            ComboBoxCommonHedgeMode.IsEnabled = false;
            ButtonCommonHedgeMode.IsEnabled = false;
            ComboBoxHedge.IsEnabled = false;
            _dgv.Columns["HedgeModeColumn"].ReadOnly = true;

            if (_serverPermission.HedgeMode_IsSupports)
            {
                if (_serverPermission.HedgeMode_Permission.ContainsKey(selectedClass))
                {
                    HedgeModePermission permission = _serverPermission.HedgeMode_Permission[selectedClass];

                    string[] hedgeComboBoxItems = permission.HedgeMode_SupportMode;

                    if (permission.HedgeMode_CommonMode)
                    {
                        if (hedgeComboBoxItems != null && hedgeComboBoxItems.Length != 0)
                        {
                            ComboBoxCommonHedgeMode.ItemsSource = hedgeComboBoxItems;
                            ComboBoxCommonHedgeMode.SelectedItem = _listLeverageData[selectedClass].CommonHedgeMode;
                            ComboBoxCommonHedgeMode.IsEnabled = true;
                            ButtonCommonHedgeMode.IsEnabled = true;
                        }
                    }
                    else
                    {
                        ComboBoxHedge.IsEnabled = true;
                        ComboBoxHedge.ItemsSource = hedgeComboBoxItems;
                        ComboBoxHedge.SelectedItem = permission.HedgeMode_StandardValue;
                        _dgv.Columns["HedgeModeColumn"].ReadOnly = false;
                    }

                    if (_listLeverageData[selectedClass].CommonHedgeMode == "")
                    {
                        ComboBoxCommonHedgeMode.IsEnabled = false;
                    }
                    else
                    {
                        ComboBoxCommonHedgeMode.IsEnabled = true;
                    }
                }
            }

            ComboBoxCommonMarginMode.IsEnabled = false;
            ButtonCommonMarginMode.IsEnabled = false;
            ComboBoxMargin.IsEnabled = false;
            _dgv.Columns["MarginModeColumn"].ReadOnly = true;

            if (_serverPermission.MarginMode_IsSupports)
            {
                if (_serverPermission.MarginMode_Permission.ContainsKey(selectedClass))
                {
                    MarginModePermission permissions = _serverPermission.MarginMode_Permission[selectedClass];

                    string[] marginComboBoxItems = permissions.MarginMode_SupportMode;

                    if (permissions.MarginMode_CommonMode)
                    {
                        if (marginComboBoxItems != null && marginComboBoxItems.Length != 0)
                        {
                            ComboBoxCommonMarginMode.ItemsSource = marginComboBoxItems;
                            ComboBoxCommonMarginMode.SelectedItem = _listLeverageData[selectedClass].CommmonMarginMode.ToString();
                            ComboBoxCommonMarginMode.IsEnabled = true;
                            ButtonCommonMarginMode.IsEnabled = true;
                        }
                    }
                    else
                    {
                        ComboBoxMargin.IsEnabled = true;
                        ComboBoxMargin.ItemsSource = marginComboBoxItems;
                        ComboBoxMargin.SelectedItem = permissions.MarginMode_StandardValue;
                        _dgv.Columns["MarginModeColumn"].ReadOnly = false;
                    }
                }
            }
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

                foreach (string key in _listLeverageData.Keys)
                {
                    classes.Add(key.ToString());
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
                string fileName = _serverNameUnique + "_SecuritiesLeverage";

                string filePath = @"Engine\ServerDopSettings\" + fileName + ".json";

                if (!File.Exists(filePath))
                {
                    return;
                }

                string json = File.ReadAllText(filePath);

                _listLeverageData = JsonConvert.DeserializeObject<Dictionary<string, ClassLeverageData>>(json);

                RepaintTable();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion

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

    public class ClassLeverageData
    {
        public string CommonLeverage = "";
        public string CommonHedgeMode = "";
        public string CommmonMarginMode = "";
        public List<SecurityLeverageData> SecurityData = new();
    }

    public class SecurityLeverageData
    {
        public string SecurityName;
        public string ClassName;
        public string Leverage;
        public string LeverageLong;
        public string LeverageShort;
        public string HedgeMode;
        public string MarginMode;        
    }
}
