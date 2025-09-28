/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Market;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using System;
using System.Windows.Forms;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Layout;

namespace OsEngine.OsTrader.ClientManagement.Gui
{
    /// <summary>
    /// Interaction logic for ClientConnector.xaml
    /// </summary>
    public partial class ClientConnectorUi : Window
    {
        public int ClientNumber;

        private TradeClientConnector _connectorSettings;

        private TradeClient _client;

        public ClientConnectorUi(TradeClientConnector connectorSettings, TradeClient client)
        {
            InitializeComponent();

            StickyBorders.Listen(this);
            GlobalGUILayout.Listen(this, "TradeClientConnectorUi" + client.Number + connectorSettings.Number);

            ClientNumber = connectorSettings.Number;
            _connectorSettings = connectorSettings;
            _connectorSettings.NewParameterCreateEvent += _connectorSettings_NewParameterCreateEvent;
            _connectorSettings.ParameterDeleteEvent += _connectorSettings_ParameterDeleteEvent;
            _client = client;

            List<string> serverTypes = ServerMaster.ServersTypesStringSorted;

            ComboBoxServerType.Items.Add(ServerType.None.ToString());

            for (int i = 0;i < serverTypes.Count;i++)
            {
                ComboBoxServerType.Items.Add(serverTypes[i].ToString());
            }

            ComboBoxServerType.SelectedItem = connectorSettings.ServerType.ToString();
            ComboBoxServerType.SelectionChanged += ComboBoxServerType_SelectionChanged;

            CreateParametersGrid();
            PaintParametersGrid();

            this.Closed += ClientConnectorUi_Closed;


            this.Title = OsLocalization.Trader.Label595 + " #" + _connectorSettings.Number;
            LabelServerType.Content = OsLocalization.Trader.Label596;
            TabItemParametersCustom.Header = OsLocalization.Trader.Label597;
        }

        private void ClientConnectorUi_Closed(object sender, System.EventArgs e)
        {
            _connectorSettings.NewParameterCreateEvent -= _connectorSettings_NewParameterCreateEvent;
            _connectorSettings.ParameterDeleteEvent -= _connectorSettings_ParameterDeleteEvent;
            _connectorSettings = null;

            _client = null;

            _clientConnectorsGrid.DataError -= _clientConnectorsGrid_DataError;
            _clientConnectorsGrid.CellClick -= _clientConnectorsGrid_CellClick;
            _clientConnectorsGrid.CellValueChanged -= _clientConnectorsGrid_CellValueChanged;
            DataGridFactory.ClearLinks(_clientConnectorsGrid);
            _clientConnectorsGrid = null;

            HostSettingsCustom.Child = null;

        }

        private void ComboBoxServerType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        { 
            try
            {
                ServerType newServerType;

                if(Enum.TryParse(ComboBoxServerType.SelectedItem.ToString(), out newServerType))
                {
                    _connectorSettings.ServerType = newServerType;
                    _client.Save();
                }
            }
            catch
            {
                // ignore
            }
        }

        #region Custom parameters

        private DataGridView _clientConnectorsGrid;

        private void CreateParametersGrid()
        {
            _clientConnectorsGrid =
            DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect,
            DataGridViewAutoSizeRowsMode.AllCells);

            _clientConnectorsGrid.ScrollBars = ScrollBars.Vertical;

            _clientConnectorsGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _clientConnectorsGrid.DefaultCellStyle;

            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = "#"; //"Num";
            colum0.ReadOnly = true;
            colum0.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            _clientConnectorsGrid.Columns.Add(colum0);

            DataGridViewColumn colum2 = new DataGridViewColumn();
            colum2.CellTemplate = cell0;
            colum2.HeaderText = OsLocalization.Trader.Label61;// "Name";
            colum2.ReadOnly = false;
            colum2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _clientConnectorsGrid.Columns.Add(colum2);

            DataGridViewColumn colum3 = new DataGridViewColumn();
            colum3.CellTemplate = cell0;
            colum3.HeaderText = OsLocalization.Trader.Label321; //"Value";
            colum3.ReadOnly = false;
            colum3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _clientConnectorsGrid.Columns.Add(colum3);

            DataGridViewColumn column10 = new DataGridViewColumn();
            column10.HeaderText = ""; // "Delete";
            column10.CellTemplate = cell0;
            column10.ReadOnly = true;
            column10.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            column10.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            column10.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _clientConnectorsGrid.Columns.Add(column10);

            HostSettingsCustom.Child = _clientConnectorsGrid;
            _clientConnectorsGrid.DataError += _clientConnectorsGrid_DataError;
            _clientConnectorsGrid.CellClick += _clientConnectorsGrid_CellClick;
            _clientConnectorsGrid.CellValueChanged += _clientConnectorsGrid_CellValueChanged;
        }

        private void _clientConnectorsGrid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                for(int i = 0;i < _clientConnectorsGrid.Rows.Count;i++)
                {
                    if(_clientConnectorsGrid.Rows[i].Cells[1].Value != null)
                    {
                        _connectorSettings.ServerParameters[i].ParameterName = _clientConnectorsGrid.Rows[i].Cells[1].Value.ToString();
                    }

                    if (_clientConnectorsGrid.Rows[i].Cells[2].Value != null)
                    {
                        _connectorSettings.ServerParameters[i].ParameterValue = _clientConnectorsGrid.Rows[i].Cells[2].Value.ToString();
                    }
                }

                _client.Save();
            }
            catch (Exception ex)
            {
                _client.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void _clientConnectorsGrid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                // 0 "Num";
                // 1 "Name";
                // 2 "Value";
                // 3 "Delete";

                int columnIndex = e.ColumnIndex;
                int rowIndex = e.RowIndex;

                if (rowIndex == -1)
                {
                    return;
                }

                if (rowIndex == _connectorSettings.ServerParameters.Count
                    && columnIndex == 3)
                { // Add new
                    _connectorSettings.AddNewParameter();
                    _client.Save();
                }
                else if (rowIndex < _connectorSettings.ServerParameters.Count
                   && columnIndex == 3)
                { // Delete
                    int number = Convert.ToInt32(_clientConnectorsGrid.Rows[rowIndex].Cells[0].Value.ToString());

                    AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label594);

                    ui.ShowDialog();

                    if (ui.UserAcceptAction == false)
                    {
                        return;
                    }

                    _connectorSettings.RemoveParameterAt(number);
                    _client.Save();
                }
            }
            catch (Exception ex)
            {
                _client.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void _clientConnectorsGrid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            _client.SendNewLogMessage(e.Exception.ToString(), Logging.LogMessageType.Error);
        }

        private void PaintParametersGrid()
        {
            try
            {
                if (_clientConnectorsGrid.InvokeRequired)
                {
                    _clientConnectorsGrid.Invoke(new Action(PaintParametersGrid));
                    return;
                }

                // 0 "Num";
                // 1 "Name";
                // 2 "Value";
                // 3 "Delete";

                _clientConnectorsGrid.Rows.Clear();

                for (int i = 0; i < _connectorSettings.ServerParameters.Count; i++)
                {
                    TradeClientConnectorParameter parameter = _connectorSettings.ServerParameters[i];

                    if (parameter == null)
                    {
                        continue;
                    }

                    _clientConnectorsGrid.Rows.Add(GetParameterRow(parameter));
                }

                _clientConnectorsGrid.Rows.Add(GetAddRow());

            }
            catch (Exception error)
            {
                _client.SendNewLogMessage(error.ToString(), Logging.LogMessageType.Error);
            }
        }

        private DataGridViewRow GetAddRow()
        {
            // 0 "Num";
            // 1 "Name";
            // 2 "Value";
            // 3 "Delete";

            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewButtonCell());

            row.Cells[^1].Value = OsLocalization.Trader.Label589;

            for (int i = 0; i < row.Cells.Count; i++)
            {
                row.Cells[i].ReadOnly = true;
            }

            return row;
        }

        private DataGridViewRow GetParameterRow(TradeClientConnectorParameter parameter)
        {
            // 0 "Num";
            // 1 "Name";
            // 2 "Value";
            // 3 "Delete";

            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[^1].Value = parameter.Number;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[^1].Value = parameter.ParameterName;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[^1].Value = parameter.ParameterValue;

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].Value = OsLocalization.Trader.Label39;

            return row;
        }

        private void _connectorSettings_ParameterDeleteEvent()
        {
            PaintParametersGrid();
        }

        private void _connectorSettings_NewParameterCreateEvent()
        {
            PaintParametersGrid();
        }

        #endregion

    }
}
