/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Layout;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Threading;

namespace OsEngine.OsTrader.ClientManagement.Gui
{
    public partial class ClientUi : Window
    {
        public int ClientNumber;

        private TradeClient _client;

        public ClientUi(TradeClient client)
        {
            InitializeComponent();

            StickyBorders.Listen(this);
            GlobalGUILayout.Listen(this, "TradeClient" + client.Number);

            ClientNumber = client.Number;
            _client = client;
            _client.Log.StartPaint(HostClientLog);
            _client.NewConnectorEvent += _client_NewConnectorEvent;
            _client.DeleteConnectorEvent += _client_DeleteConnectorEvent;
            _client.NewRobotEvent += _client_NewRobotEvent;
            _client.DeleteRobotEvent += _client_DeleteRobotEvent;

            TextBoxClientName.Text = _client.Name;
            TextBoxClientName.TextChanged += TextBoxClientName_TextChanged;

            ComboBoxRegime.Items.Add(TradeClientRegime.Manual.ToString());
            ComboBoxRegime.Items.Add(TradeClientRegime.Auto.ToString());
            ComboBoxRegime.SelectedItem = _client.Regime.ToString();
            ComboBoxRegime.SelectionChanged += ComboBoxRegime_SelectionChanged;

            TextBoxUid.Text= _client.ClientUid.ToString();

            CreateConnectorsGrid();
            RePaintConnectorsGrid();

            CreateRobotsGrid();
            RePaintRobotsGrid();

            // localization

            this.Title = OsLocalization.Trader.Label592 + " " + _client.Number + " " + _client.Name;

            LabelName.Content = OsLocalization.Trader.Label61;
            LabelRegime.Content = OsLocalization.Trader.Label468;

            TabItem1.Header = OsLocalization.Trader.Label585;
            TabItem2.Header = OsLocalization.Trader.Label587;
            TabItem3.Header = OsLocalization.Trader.Label332;

            LabelUid.Content = "";

            this.Closed += ClientUi_Closed;

            Thread worker = new Thread(PainterThread);
            worker.Start();
        }

        private void ClientUi_Closed(object sender, EventArgs e)
        {
            _isClosed = true;

            _client.NewConnectorEvent -= _client_NewConnectorEvent;
            _client.DeleteConnectorEvent -= _client_DeleteConnectorEvent;
            _client.NewRobotEvent -= _client_NewRobotEvent;
            _client.DeleteRobotEvent -= _client_DeleteRobotEvent;

            _client.Log.StopPaint();
            _client = null;

            HostClientConnectors.Child = null;
            _clientConnectorsGrid.CellClick -= _clientConnectorsGrid_CellClick;
            _clientConnectorsGrid.DataError -= _clientConnectorsGrid_DataError;
            DataGridFactory.ClearLinks(_clientConnectorsGrid);
            _clientConnectorsGrid = null;

            HostClientRobots.Child = null;
            _clientRobotsGrid.CellClick -= _clientRobotsGrid_CellClick;
            _clientRobotsGrid.DataError -= _clientRobotsGrid_DataError;
            DataGridFactory.ClearLinks(_clientRobotsGrid);
            _clientRobotsGrid = null;

        }

        private bool _isClosed;

        private void ComboBoxRegime_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxRegime.SelectedItem.ToString(),out _client.Regime);
                _client.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxClientName_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _client.Name = TextBoxClientName.Text;
                _client.Save();
                this.Title = OsLocalization.Trader.Label592 + " " + _client.Number + " " + _client.Name;
            }
            catch
            {
                // ignore
            }
        }

        #region Connectors grid

        private void PainterThread()
        {
            while(true)
            {
                Thread.Sleep(2000);

                if(_isClosed == true)
                {
                    return;
                }

                TryUpdateStatus();
            }
        }

        private void TryUpdateStatus()
        {
            try
            {
                if (_clientConnectorsGrid.InvokeRequired)
                {
                    _clientConnectorsGrid.Invoke(new Action(RePaintConnectorsGrid));
                    return;
                }

                if(_client.ConnectorsSettings.Count != _clientConnectorsGrid.Rows.Count)
                {
                    return;
                }

                //"Num";
                //"Type"
                //"Parameters";
                //"Deploy status";
                //"Deploy";
                //"Сollapse";
                //"GUI";
                //"Server status";
                //"Connect";
                //"Disconnect";
                // Delete

                for (int i = 0; i < _client.ConnectorsSettings.Count; i++)
                {
                    TradeClientConnector client = _client.ConnectorsSettings[i];

                    if (client == null)
                    {
                        continue;
                    }

                    DataGridViewRow row = _clientConnectorsGrid.Rows[i];

                    if (row.Cells[3].Value != null
                        && row.Cells[3].Value.ToString() != client.DeployStatus)
                    {
                        row.Cells[3].Value = client.DeployStatus;
                    }

                    if (row.Cells[7].Value != null
                        && row.Cells[7].Value.ToString() != client.ServerStatus)
                    {
                        row.Cells[7].Value = client.ServerStatus;
                    }
                }
            }
            catch (Exception error)
            {
                _client.SendNewLogMessage(error.ToString(), Logging.LogMessageType.Error);
            }


        }

        private DataGridView _clientConnectorsGrid;

        private void CreateConnectorsGrid()
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

            DataGridViewColumn colum1 = new DataGridViewColumn();
            colum1.CellTemplate = cell0;
            colum1.HeaderText = OsLocalization.Trader.Label167; //"Type";
            colum1.ReadOnly = true;
            colum1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _clientConnectorsGrid.Columns.Add(colum1);

            DataGridViewColumn colum2 = new DataGridViewColumn();
            colum2.CellTemplate = cell0;
            colum2.HeaderText = "";// "Parameters"; //"Parameters";
            colum2.ReadOnly = false;
            colum2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _clientConnectorsGrid.Columns.Add(colum2);

            DataGridViewColumn colum3 = new DataGridViewColumn();
            colum3.CellTemplate = cell0;
            colum3.HeaderText = OsLocalization.Trader.Label598; //"Deploy status";
            colum3.ReadOnly = false;
            colum3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _clientConnectorsGrid.Columns.Add(colum3);

            DataGridViewColumn colum4 = new DataGridViewColumn();
            colum4.CellTemplate = cell0;
            colum4.HeaderText = "";// "Deploy"; //"Deploy";
            colum4.ReadOnly = false;
            colum4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _clientConnectorsGrid.Columns.Add(colum4);

            DataGridViewColumn colum5 = new DataGridViewColumn();
            colum5.CellTemplate = cell0;
            colum5.HeaderText = "";//"Сollapse"; //"Сollapse";
            colum5.ReadOnly = false;
            colum5.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _clientConnectorsGrid.Columns.Add(colum5);

            DataGridViewColumn colum6 = new DataGridViewColumn();
            colum6.CellTemplate = cell0;
            colum6.HeaderText = "";// "GUI"; //"GUI";
            colum6.ReadOnly = false;
            colum6.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _clientConnectorsGrid.Columns.Add(colum6);

            DataGridViewColumn colum7 = new DataGridViewColumn();
            colum7.CellTemplate = cell0;
            colum7.HeaderText = OsLocalization.Trader.Label599; //"Server status";
            colum7.ReadOnly = false;
            colum7.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _clientConnectorsGrid.Columns.Add(colum7);

            DataGridViewColumn colum8 = new DataGridViewColumn();
            colum8.CellTemplate = cell0;
            colum8.HeaderText = "";// "Connect"; //"Connect";
            colum8.ReadOnly = false;
            colum8.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _clientConnectorsGrid.Columns.Add(colum8);

            DataGridViewColumn colum9 = new DataGridViewColumn();
            colum9.CellTemplate = cell0;
            colum9.HeaderText = "";// "Disconnect"; //"Disconnect";
            colum9.ReadOnly = false;
            colum9.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _clientConnectorsGrid.Columns.Add(colum9);

            DataGridViewColumn column10 = new DataGridViewColumn();
            column10.HeaderText = "";// "Delete"; // Delete
            column10.CellTemplate = cell0;
            column10.ReadOnly = true;
            column10.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            column10.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            column10.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _clientConnectorsGrid.Columns.Add(column10);

            HostClientConnectors.Child = _clientConnectorsGrid;
            _clientConnectorsGrid.CellClick += _clientConnectorsGrid_CellClick;
            _clientConnectorsGrid.DataError += _clientConnectorsGrid_DataError;
        }

        private void _clientConnectorsGrid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            _client.SendNewLogMessage(e.Exception.ToString(), Logging.LogMessageType.Error);
        }

        private void _clientConnectorsGrid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                //0 "Num";
                //1 "Type"
                //2 "Parameters";
                //3 "Deploy status";
                //4 "Deploy";
                //5 "Сollapse";
                //6 "GUI";
                //7 "Server status";
                //8 "Connect";
                //9 "Disconnect";
                //10 "Delete"

                int columnIndex = e.ColumnIndex;
                int rowIndex = e.RowIndex;

                if (rowIndex == -1)
                {
                    return;
                }

                if (rowIndex == _client.ConnectorsSettings.Count
                    && columnIndex == 10)
                { // Add new
                    _client.AddNewConnector();
                }
                else if (rowIndex < _client.ConnectorsSettings.Count
                   && columnIndex == 10)
                { // Delete
                    int number = Convert.ToInt32(_clientConnectorsGrid.Rows[rowIndex].Cells[0].Value.ToString());

                    AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label593);

                    ui.ShowDialog();

                    if (ui.UserAcceptAction == false)
                    {
                        return;
                    }

                   _client.RemoveConnectorAtNumber(number);
                }
                else if (rowIndex < _client.ConnectorsSettings.Count
                   && columnIndex == 2)
                { // Parameters
                    int number = Convert.ToInt32(_clientConnectorsGrid.Rows[rowIndex].Cells[0].Value.ToString());
                    ShowConnectorDialog(number);
                }
                else if (rowIndex < _client.ConnectorsSettings.Count
                   && columnIndex == 4)
                { // Deploy
                    int number = Convert.ToInt32(_clientConnectorsGrid.Rows[rowIndex].Cells[0].Value.ToString());
                    _client.DeployServer(number);
                    RePaintConnectorsGrid();
                }
                else if (rowIndex < _client.ConnectorsSettings.Count
                   && columnIndex == 5)
                { // Collapse
                    int number = Convert.ToInt32(_clientConnectorsGrid.Rows[rowIndex].Cells[0].Value.ToString());
                    _client.CollapseSever(number);
                    RePaintConnectorsGrid();
                }
                else if (rowIndex < _client.ConnectorsSettings.Count
                   && columnIndex == 6)
                { // GUI

                    int number = Convert.ToInt32(_clientConnectorsGrid.Rows[rowIndex].Cells[0].Value.ToString());
                    _client.ShowGuiServer(number);
                    RePaintConnectorsGrid();
                }
                else if (rowIndex < _client.ConnectorsSettings.Count
                    && columnIndex == 8)
                { //8 "Connect";

                    int number = Convert.ToInt32(_clientConnectorsGrid.Rows[rowIndex].Cells[0].Value.ToString());
                    _client.ConnectServer(number);
                    RePaintConnectorsGrid();
                }
                else if (rowIndex < _client.ConnectorsSettings.Count
                   && columnIndex == 9)
                { //9 "Disconnect";

                    int number = Convert.ToInt32(_clientConnectorsGrid.Rows[rowIndex].Cells[0].Value.ToString());
                    _client.DisconnectServer(number);
                    RePaintConnectorsGrid();
                }
            }
            catch (Exception ex)
            {
                _client.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void RePaintConnectorsGrid()
        {
            try
            {
                if (_clientConnectorsGrid.InvokeRequired)
                {
                    _clientConnectorsGrid.Invoke(new Action(RePaintConnectorsGrid));
                    return;
                }

                //"Num";
                //"Type"
                //"Parameters";
                //"Deploy status";
                //"Deploy";
                //"Сollapse";
                //"GUI";
                //"Server status";
                //"Connect";
                //"Disconnect";
                // Delete

                _clientConnectorsGrid.Rows.Clear();

                for (int i = 0; i < _client.ConnectorsSettings.Count; i++)
                {
                    TradeClientConnector client = _client.ConnectorsSettings[i];

                    if (client == null)
                    {
                        continue;
                    }

                    _clientConnectorsGrid.Rows.Add(GetConnectorRow(client));
                }

                _clientConnectorsGrid.Rows.Add(GetAddRowConnector());

            }
            catch (Exception error)
            {
                _client.SendNewLogMessage(error.ToString(), Logging.LogMessageType.Error);
            }
        }

        private DataGridViewRow GetAddRowConnector()
        {
            //1 "Num";
            //2 "Type"
            //3 "Parameters";
            //4 "Deploy status";
            //5 "Deploy";
            //6 "Сollapse";
            //7 "GUI";
            //8 "Server status";
            //9 "Connect";
            //10 "Disconnect";
            //11 Delete

            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());
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

        private DataGridViewRow GetConnectorRow(TradeClientConnector connector)
        {
            //1 "Num";
            //2 "Type"
            //3 "Parameters";
            //4 "Deploy status";
            //5 "Deploy";
            //6 "Сollapse";
            //7 "GUI";
            //8 "Status";
            //9 "Connect";
            //10 "Disconnect";
            //11 "Delete";

            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[^1].Value = connector.Number;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[^1].Value = connector.ServerType.ToString();

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].Value = OsLocalization.Trader.Label45;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[^1].Value = connector.DeployStatus;

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].Value = OsLocalization.Trader.Label588;

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].Value = OsLocalization.Trader.Label600;

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].Value = OsLocalization.Trader.Label601;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[^1].Value = connector.ServerStatus;

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].Value = OsLocalization.Trader.Label602;

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].Value = OsLocalization.Trader.Label603;

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].Value = OsLocalization.Trader.Label39;

            return row;
        }

        private void _client_DeleteConnectorEvent(TradeClientConnector obj)
        {
            RePaintConnectorsGrid();
        }

        private void _client_NewConnectorEvent(TradeClientConnector obj)
        {
            RePaintConnectorsGrid();
        }

        #endregion

        #region Client connector parameters

        List<ClientConnectorUi> _connectorsUi = new List<ClientConnectorUi>();

        private void ShowConnectorDialog(int connectorNumber)
        {
            TradeClientConnector connector = null;

            for (int i = 0; i < _client.ConnectorsSettings.Count; i++)
            {
                if (_client.ConnectorsSettings[i].Number == connectorNumber)
                {
                    connector = _client.ConnectorsSettings[i];
                    break;
                }
            }

            if (connector == null)
            {
                return;
            }

            ClientConnectorUi connectorUi = null;

            for (int i = 0; i < _connectorsUi.Count; i++)
            {
                if (_connectorsUi[i].ClientNumber == connectorNumber)
                {
                    connectorUi = _connectorsUi[i];
                    break;
                }
            }
            if (connectorUi == null)
            {
                connectorUi = new ClientConnectorUi(connector,_client);
                connectorUi.Closed += ConnectorUi_Closed;
                connectorUi.Show();
                _connectorsUi.Add(connectorUi);
            }
            else
            {
                if (connectorUi.WindowState == System.Windows.WindowState.Minimized)
                {
                    connectorUi.WindowState = System.Windows.WindowState.Normal;
                }

                connectorUi.Activate();
            }
        }

        private void ConnectorUi_Closed(object sender, EventArgs e)
        {
            ClientConnectorUi clientUi = (ClientConnectorUi)sender;

            for (int i = 0; i < _connectorsUi.Count; i++)
            {
                if (_connectorsUi[i].ClientNumber == clientUi.ClientNumber)
                {
                    _connectorsUi[i].Closed -= ConnectorUi_Closed;
                    _connectorsUi.RemoveAt(i);
                    break;
                }
            }
        }

        #endregion

        #region Client robots grid

        private DataGridView _clientRobotsGrid;

        private void CreateRobotsGrid()
        {
            _clientRobotsGrid =
             DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect,
             DataGridViewAutoSizeRowsMode.AllCells);

            _clientRobotsGrid.ScrollBars = ScrollBars.Vertical;

            _clientRobotsGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _clientRobotsGrid.DefaultCellStyle;

            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = "#"; //"Num";
            colum0.ReadOnly = true;
            colum0.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            _clientRobotsGrid.Columns.Add(colum0);

            DataGridViewColumn colum1 = new DataGridViewColumn();
            colum1.CellTemplate = cell0;
            colum1.HeaderText = OsLocalization.Trader.Label166; //"Class";
            colum1.ReadOnly = true;
            colum1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _clientRobotsGrid.Columns.Add(colum1);

            DataGridViewColumn colum2 = new DataGridViewColumn();
            colum2.CellTemplate = cell0;
            colum2.HeaderText = ""; //"Parameters";
            colum2.ReadOnly = false;
            colum2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _clientRobotsGrid.Columns.Add(colum2);

            DataGridViewColumn colum3 = new DataGridViewColumn();
            colum3.CellTemplate = cell0;
            colum3.HeaderText = ""; //"Sources";
            colum3.ReadOnly = false;
            colum3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _clientRobotsGrid.Columns.Add(colum3);

            DataGridViewColumn colum4 = new DataGridViewColumn();
            colum4.CellTemplate = cell0;
            colum4.HeaderText = OsLocalization.Trader.Label598; //"Deploy status"
            colum4.ReadOnly = false;
            colum4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _clientRobotsGrid.Columns.Add(colum4);

            DataGridViewColumn colum5 = new DataGridViewColumn();
            colum5.CellTemplate = cell0;
            colum5.HeaderText = ""; //"Deploy";
            colum5.ReadOnly = false;
            colum5.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _clientRobotsGrid.Columns.Add(colum5);

            DataGridViewColumn colum6 = new DataGridViewColumn();
            colum6.CellTemplate = cell0;
            colum6.HeaderText = ""; // "Collapse";
            colum6.ReadOnly = false;
            colum6.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _clientRobotsGrid.Columns.Add(colum6);

            DataGridViewColumn colum7 = new DataGridViewColumn();
            colum7.CellTemplate = cell0;
            colum7.HeaderText = OsLocalization.Trader.Label184; //"On/Off";
            colum7.ReadOnly = false;
            colum7.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colum7.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _clientRobotsGrid.Columns.Add(colum7);

            DataGridViewColumn colum8 = new DataGridViewColumn();
            colum8.CellTemplate = cell0;
            colum8.HeaderText = OsLocalization.Trader.Label185; // "Emulator";
            colum8.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colum8.ReadOnly = false;
            colum8.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _clientRobotsGrid.Columns.Add(colum8);

            DataGridViewColumn colum9 = new DataGridViewColumn();
            colum9.CellTemplate = cell0;
            colum9.HeaderText = ""; //"Chart";
            colum9.ReadOnly = false;
            colum9.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _clientRobotsGrid.Columns.Add(colum9);

            DataGridViewColumn column10 = new DataGridViewColumn();
            column10.HeaderText = ""; // Delete
            column10.CellTemplate = cell0;
            column10.ReadOnly = true;
            column10.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _clientRobotsGrid.Columns.Add(column10);

            HostClientRobots.Child = _clientRobotsGrid;
            _clientRobotsGrid.CellClick += _clientRobotsGrid_CellClick;
            _clientRobotsGrid.DataError += _clientRobotsGrid_DataError;
        }

        private void _clientRobotsGrid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            _client.SendNewLogMessage(e.Exception.ToString(), Logging.LogMessageType.Error);
        }

        private void _clientRobotsGrid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                //0 "Num";
                //1 "Class";
                //2 "Parameters";
                //3 "Sources";
                //4 "Deploy status"
                //5 "Deploy";
                //6 "Collapse";
                //7 "On/Off";
                //8 "Emulator";
                //9 "Chart";
                //10 Delete / Add new

                int columnIndex = e.ColumnIndex;
                int rowIndex = e.RowIndex;

                if (rowIndex == -1)
                {
                    return;
                }

                if (rowIndex == _client.RobotsSettings.Count
                    && columnIndex == 10)
                { // Add new
                    _client.AddNewRobot();
                }
                else if (rowIndex < _client.RobotsSettings.Count
                   && columnIndex == 10)
                { // Delete
                    int number = Convert.ToInt32(_clientRobotsGrid.Rows[rowIndex].Cells[0].Value.ToString());

                    AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label604);

                    ui.ShowDialog();

                    if (ui.UserAcceptAction == false)
                    {
                        return;
                    }

                    _client.RemoveRobotAtNumber(number);
                }
                else if (rowIndex < _client.RobotsSettings.Count
                   && columnIndex == 2)
                { // Parameters
                    int number = Convert.ToInt32(_clientRobotsGrid.Rows[rowIndex].Cells[0].Value.ToString());
                    ShowRobotsParametersDialog(number);
                }
                else if (rowIndex < _client.RobotsSettings.Count
                   && columnIndex == 3)
                { // Sources
                    int number = Convert.ToInt32(_clientRobotsGrid.Rows[rowIndex].Cells[0].Value.ToString());
                    ShowRobotsSourcesDialog(number);
                }
                else if (rowIndex < _client.RobotsSettings.Count
                   && columnIndex == 5)
                { // Deploy
                    int number = Convert.ToInt32(_clientRobotsGrid.Rows[rowIndex].Cells[0].Value.ToString());
                    DeployRobot(number);
                    RePaintRobotsGrid();
                }
                else if (rowIndex < _client.RobotsSettings.Count
                   && columnIndex == 6)
                { // Collapse
                    int number = Convert.ToInt32(_clientRobotsGrid.Rows[rowIndex].Cells[0].Value.ToString());
                    CollapseRobot(number);
                    RePaintRobotsGrid();
                }
                else if (rowIndex < _client.RobotsSettings.Count
                    && columnIndex == 9)
                { //8 "Chart";

                    int number = Convert.ToInt32(_clientRobotsGrid.Rows[rowIndex].Cells[0].Value.ToString());
                    ShowRobotsChartDialog(number);
                }
            }
            catch (Exception ex)
            {
                _client.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void RePaintRobotsGrid()
        {
            try
            {
                if (_clientRobotsGrid.InvokeRequired)
                {
                    _clientRobotsGrid.Invoke(new Action(RePaintRobotsGrid));
                    return;
                }

                //0 "Num";
                //1 "Class";
                //2 "Parameters";
                //3 "Sources";
                //4 "Deploy status"
                //5 "Deploy";
                //6 "Collapse";
                //7 On/Off";
                //8 "Emulator";
                //9 "Chart";
                //10 Delete

                _clientRobotsGrid.Rows.Clear();

                for (int i = 0; i < _client.RobotsSettings.Count; i++)
                {
                    TradeClientRobot robot = _client.RobotsSettings[i];

                    if (robot == null)
                    {
                        continue;
                    }

                    _clientRobotsGrid.Rows.Add(GetRobotsRow(robot));
                }

                _clientRobotsGrid.Rows.Add(GetAddRowRobots());

            }
            catch (Exception error)
            {
                _client.SendNewLogMessage(error.ToString(), Logging.LogMessageType.Error);
            }

        }

        private DataGridViewRow GetAddRowRobots()
        {
            //0 "Num";
            //1 "Class";
            //2 "Parameters";
            //3 "Sources";
            //4 "Deploy status"
            //5 "Deploy";
            //6 "Collapse";
            //7 On/Off";
            //8 "Emulator";
            //9 "Chart";
            //10 Delete

            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewTextBoxCell());// 0
            row.Cells.Add(new DataGridViewTextBoxCell());// 1
            row.Cells.Add(new DataGridViewTextBoxCell());// 2
            row.Cells.Add(new DataGridViewTextBoxCell());// 3
            row.Cells.Add(new DataGridViewTextBoxCell());// 4
            row.Cells.Add(new DataGridViewTextBoxCell());// 5
            row.Cells.Add(new DataGridViewTextBoxCell());// 6
            row.Cells.Add(new DataGridViewTextBoxCell());// 7
            row.Cells.Add(new DataGridViewTextBoxCell());// 8
            row.Cells.Add(new DataGridViewTextBoxCell());// 9
            row.Cells.Add(new DataGridViewButtonCell());// 10

            row.Cells[^1].Value = OsLocalization.Trader.Label589;

            for (int i = 0; i < row.Cells.Count; i++)
            {
                row.Cells[i].ReadOnly = true;
            }

            return row;
        }

        private DataGridViewRow GetRobotsRow(TradeClientRobot robot)
        {
            //0 "Num";
            //1 "Class";
            //2 "Parameters";
            //3 "Sources";
            //4 "Deploy status"
            //5 "Deploy";
            //6 "Collapse";
            //7 On/Off";
            //8 "Emulator";
            //9 "Chart";
            //10 Delete

            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[^1].Value = robot.Number;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[^1].Value = robot.BotClassName;

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].Value = OsLocalization.Trader.Label45;

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].Value = OsLocalization.Trader.Label296;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[^1].Value = robot.DeployStatus;

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].Value = OsLocalization.Trader.Label588;

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].Value = OsLocalization.Trader.Label600;

            row.Cells.Add(new DataGridViewCheckBoxCell());
            row.Cells[^1].Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            row.Cells[^1].Value = robot.RobotsIsOn;

            row.Cells.Add(new DataGridViewCheckBoxCell());
            row.Cells[^1].Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            row.Cells[^1].Value = robot.EmulatorIsOn;

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].Value = OsLocalization.Trader.Label172;

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].Value = OsLocalization.Trader.Label470;

            return row;
        }

        private void _client_DeleteRobotEvent(TradeClientRobot obj)
        {
            RePaintRobotsGrid();
        }

        private void _client_NewRobotEvent(TradeClientRobot obj)
        {
            RePaintRobotsGrid();
        }

        #endregion

        #region Client robots controls

        private void ShowRobotsParametersDialog(int robotNumber)
        {
            TradeClientRobot bot = null;

            for (int i = 0; i < _client.RobotsSettings.Count; i++)
            {
                if (_client.RobotsSettings[i].Number == robotNumber)
                {
                    bot = _client.RobotsSettings[i];
                    break;
                }
            }

            if(bot == null)
            {
                return;
            }

            bot.ShowParametersDialog(_client);
        }

        private void ShowRobotsSourcesDialog(int robotNumber)
        {
            TradeClientRobot bot = null;

            for (int i = 0; i < _client.RobotsSettings.Count; i++)
            {
                if (_client.RobotsSettings[i].Number == robotNumber)
                {
                    bot = _client.RobotsSettings[i];
                    break;
                }
            }

            if (bot == null)
            {
                return;
            }

            bot.ShowSourcesDialog(_client);
        }

        private void DeployRobot(int robotNumber)
        {
            string error = "";

            _client.DeployOrUpdateRobot(robotNumber, out error);

            if(error != "Success")
            {
                _client.SendNewLogMessage(error,Logging.LogMessageType.Error);
            }
        }

        private void CollapseRobot(int robotNumber)
        {
            string error = "";

            _client.CollapseRobot(robotNumber, out error);

            if (error != "Success")
            {
                _client.SendNewLogMessage(error, Logging.LogMessageType.Error);
            }
        }

        private void ShowRobotsChartDialog(int robotNumber)
        {
            _client.ShowRobotsChartDialog(robotNumber);
        }

        #endregion

    }
}
