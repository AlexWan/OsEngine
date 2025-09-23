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
            _client.NewConnectorEvent += _client_NewConnectorEvent;
            _client.DeleteConnectorEvent += _client_DeleteConnectorEvent;

            TextBoxClientName.Text = _client.Name;
            TextBoxClientName.TextChanged += TextBoxClientName_TextChanged;

            TextBoxState.Text = _client.Status;

            ComboBoxRegime.Items.Add(TradeClientRegime.Manual.ToString());
            ComboBoxRegime.Items.Add(TradeClientRegime.Auto.ToString());
            ComboBoxRegime.SelectedItem = _client.Regime.ToString();
            ComboBoxRegime.SelectionChanged += ComboBoxRegime_SelectionChanged;

            CreateConnectorsGrid();
            RePaintConnectorsGrid();

            // localization

            this.Title = OsLocalization.Trader.Label592 + " " + _client.Number + " " + _client.Name;

            LabelName.Content = OsLocalization.Trader.Label61;
            LabelState.Content = OsLocalization.Trader.Label224;
            LabelRegime.Content = OsLocalization.Trader.Label468;

            TabItem1.Header = OsLocalization.Trader.Label585;
            TabItem2.Header = OsLocalization.Trader.Label587;

            this.Closed += ClientUi_Closed;

            Thread worker = new Thread(PainterThread);
            worker.Start();
        }

        private void ClientUi_Closed(object sender, EventArgs e)
        {
            _isClosed = true;

            _client.NewConnectorEvent -= _client_NewConnectorEvent;
            _client.DeleteConnectorEvent -= _client_DeleteConnectorEvent;
            _client = null;

            HostClientConnectors.Child = null;

            _clientConnectorsGrid.CellClick -= _clientConnectorsGrid_CellClick;
            _clientConnectorsGrid.DataError -= _clientConnectorsGrid_DataError;


            DataGridFactory.ClearLinks(_clientConnectorsGrid);
            _clientConnectorsGrid = null;

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

                if(_client.ClientConnectorsSettings.Count != _clientConnectorsGrid.Rows.Count)
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

                for (int i = 0; i < _client.ClientConnectorsSettings.Count; i++)
                {
                    TradeClientConnector client = _client.ClientConnectorsSettings[i];

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

                if (rowIndex == _client.ClientConnectorsSettings.Count
                    && columnIndex == 10)
                { // Add new
                    _client.AddNewConnector();
                }
                else if (rowIndex < _client.ClientConnectorsSettings.Count
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
                else if (rowIndex < _client.ClientConnectorsSettings.Count
                   && columnIndex == 2)
                { // Parameters
                    int number = Convert.ToInt32(_clientConnectorsGrid.Rows[rowIndex].Cells[0].Value.ToString());
                    ShowConnectorDialog(number);
                }
                else if (rowIndex < _client.ClientConnectorsSettings.Count
                   && columnIndex == 4)
                { // Deploy
                    int number = Convert.ToInt32(_clientConnectorsGrid.Rows[rowIndex].Cells[0].Value.ToString());
                    Deploy(number);
                    RePaintConnectorsGrid();
                }
                else if (rowIndex < _client.ClientConnectorsSettings.Count
                   && columnIndex == 5)
                { // Collapse
                    int number = Convert.ToInt32(_clientConnectorsGrid.Rows[rowIndex].Cells[0].Value.ToString());
                    Collapse(number);
                    RePaintConnectorsGrid();
                }
                else if (rowIndex < _client.ClientConnectorsSettings.Count
                   && columnIndex == 6)
                { // GUI

                    int number = Convert.ToInt32(_clientConnectorsGrid.Rows[rowIndex].Cells[0].Value.ToString());
                    ShowGui(number);
                    RePaintConnectorsGrid();
                }
                else if (rowIndex < _client.ClientConnectorsSettings.Count
                    && columnIndex == 8)
                { //8 "Connect";

                    int number = Convert.ToInt32(_clientConnectorsGrid.Rows[rowIndex].Cells[0].Value.ToString());
                    Connect(number);
                    RePaintConnectorsGrid();
                }
                else if (rowIndex < _client.ClientConnectorsSettings.Count
                   && columnIndex == 9)
                { //9 "Disconnect";

                    int number = Convert.ToInt32(_clientConnectorsGrid.Rows[rowIndex].Cells[0].Value.ToString());
                    Disconnect(number);
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

                for (int i = 0; i < _client.ClientConnectorsSettings.Count; i++)
                {
                    TradeClientConnector client = _client.ClientConnectorsSettings[i];

                    if (client == null)
                    {
                        continue;
                    }

                    _clientConnectorsGrid.Rows.Add(GetConnectorRow(client));
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

            for (int i = 0; i < _client.ClientConnectorsSettings.Count; i++)
            {
                if (_client.ClientConnectorsSettings[i].Number == connectorNumber)
                {
                    connector = _client.ClientConnectorsSettings[i];
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

        #region Client server controls

        private void Deploy(int  connectorNumber)
        {
            if(connectorNumber >= _client.ClientConnectorsSettings.Count)
            {
                return;
            }

            TradeClientConnector connector = _client.ClientConnectorsSettings[connectorNumber];

            connector.Deploy();
        }

        private void Collapse(int connectorNumber)
        {
            if (connectorNumber >= _client.ClientConnectorsSettings.Count)
            {
                return;
            }

            TradeClientConnector connector = _client.ClientConnectorsSettings[connectorNumber];

            connector.Collapse();

        }

        private void ShowGui(int connectorNumber)
        {
            if (connectorNumber >= _client.ClientConnectorsSettings.Count)
            {
                return;
            }

            TradeClientConnector connector = _client.ClientConnectorsSettings[connectorNumber];

            connector.ShowGui();

        }

        private void Connect(int connectorNumber)
        {
            if (connectorNumber >= _client.ClientConnectorsSettings.Count)
            {
                return;
            }

            TradeClientConnector connector = _client.ClientConnectorsSettings[connectorNumber];

            connector.Connect();

        }

        private void Disconnect(int connectorNumber)
        {
            if (connectorNumber >= _client.ClientConnectorsSettings.Count)
            {
                return;
            }

            TradeClientConnector connector = _client.ClientConnectorsSettings[connectorNumber];

            connector.Disconnect();

        }

        #endregion

    }
}
