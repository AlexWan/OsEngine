/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows;
using System.Windows.Forms;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market.Servers.Entity;
using System.Threading;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers
{
    public partial class AServerParameterUi
    {
        #region Service

        public AServerParameterUi(List<AServer> servers, int numberServerToShow)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            int numServerInArray = 0;

            if (numberServerToShow != 0)
            {
                for (int i = 0; i < servers.Count; i++)
                {
                    if (servers[i].ServerNum == numberServerToShow)
                    {
                        numServerInArray = i;
                        break;
                    }
                }
            }

            _serversArray = servers;
            _serverType = servers[numServerInArray].ServerType;

            if (servers[numServerInArray].CanDoMultipleConnections == false)
            {
                _server = servers[numServerInArray];
                _server.Log.StartPaint(HostLog);
                LabelStatus.Content = _server.ServerStatus;
                _server.ConnectStatusChangeEvent += Server_ConnectStatusChangeEvent;
            }

            CreateGridServerParameters();
            PaintGridServerParameters();

            TabItemParameters.Header = OsLocalization.Market.TabItem3;
            TabItemLog.Header = OsLocalization.Market.TabItem4;
            Label21.Content = OsLocalization.Market.Label21;
            ButtonConnect.Content = OsLocalization.Market.ButtonConnect;
            ButtonAbort.Content = OsLocalization.Market.ButtonDisconnect;
            LabelCurrentConnectionName.Content = OsLocalization.Market.Label164;
            LabelPreConfiguredConnection.Content = OsLocalization.Market.Label166;

            if (servers[numServerInArray].NeedToHideParameters == true)
            {
                TabItemParameters.Visibility = Visibility.Hidden;
                TabItemLog.IsSelected = true;
            }

            if (servers[numServerInArray].CanDoMultipleConnections == false)
            {
                SetGuiNoMultipleConnect();
            }
            else
            {
                CreateGridConnectionsInstance();
                PaintGridConnectionsInstance();
                ChangeActiveServer(numServerInArray);

                Thread worker = new Thread(UpdateStatusThread);
                worker.Start();
            }

            Title = OsLocalization.Market.TitleAServerParametrUi + _server.ServerType;

            this.Activate();
            this.Focus();

            this.Closed += AServerParameterUi_Closed;

            ServerMaster.ServerDeleteEvent += ServerMaster_ServerDeleteEvent;
        }

        private void AServerParameterUi_Closed(object sender, EventArgs e)
        {
            _uiIsClosed = true;
            this.Closed -= AServerParameterUi_Closed;
            ServerMaster.ServerDeleteEvent -= ServerMaster_ServerDeleteEvent;

            _server.Log.StopPaint();
            _server.ConnectStatusChangeEvent -= Server_ConnectStatusChangeEvent;
            _server = null;

            _serversArray = null;

            if (_gridServerParameters != null)
            {
                _gridServerParameters.CellValueChanged -= _gridServerParameters_CellValueChanged;
                _gridServerParameters.Click -= _gridServerParameters_Click;
                _gridServerParameters.CellClick -= _gridServerParameters_CellClick;
                _gridServerParameters.DataError -= _gridServerParameters_DataError;
                _gridServerParameters.Rows.Clear();
                DataGridFactory.ClearLinks(_gridServerParameters);
                _gridServerParameters = null;
            }

            if (_gridConnections != null)
            {
                _gridConnections.CellClick -= _gridConnections_CellClick;
                _gridConnections.CellEndEdit -= _gridConnections_CellEndEdit;
                _gridConnections.DataError -= _gridConnections_DataError;
                _gridConnections.Columns.Clear();
                DataGridFactory.ClearLinks(_gridConnections);
                _gridConnections = null;
            }

            HostPreConfiguredConnections.Child = null;
            HostSettings.Child = null;
        }

        private void ServerMaster_ServerDeleteEvent(IServer server)
        {
            UpdateServersCount();
            PaintGridConnectionsInstance();
        }

        public void SetGuiNoMultipleConnect()
        {
            LabelPreConfiguredConnection.Visibility = Visibility.Hidden;
            HostPreConfiguredConnections.Visibility = Visibility.Hidden;
            LabelCurrentConnectionName.Visibility = Visibility.Hidden;
            GridPrime.RowDefinitions[0].Height = new GridLength(0);
        }

        private ServerType _serverType;

        private bool _uiIsClosed;

        #endregion

        #region Multiple connection grid

        private List<AServer> _serversArray;

        private void UpdateServersCount()
        {
            _serversArray.Clear();

            List<IServer> servers = ServerMaster.GetServers();

            for (int i = 0; i < servers.Count; i++)
            {
                if (servers[i].ServerType == _serverType)
                {
                    _serversArray.Add((AServer)servers[i]);
                }
            }
        }

        private DataGridView _gridConnections;

        private void CreateGridConnectionsInstance()
        {
            _gridConnections = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect,
                DataGridViewAutoSizeRowsMode.AllCells);
            _gridConnections.ScrollBars = ScrollBars.Vertical;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _gridConnections.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = OsLocalization.Market.Label164; // Server name (Unique)
            column0.ReadOnly = true;
            column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridConnections.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = OsLocalization.Market.Label167; // Server number
            column1.ReadOnly = true;
            column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridConnections.Columns.Add(column1);

            DataGridViewColumn column2 = new DataGridViewColumn();
            column2.CellTemplate = cell0;
            column2.HeaderText = OsLocalization.Market.Label168; // Server prefix
            column2.ReadOnly = false;
            column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridConnections.Columns.Add(column2);

            DataGridViewColumn column3 = new DataGridViewColumn();
            column3.CellTemplate = cell0;
            column3.HeaderText = OsLocalization.Market.Label169; // Server state
            column3.ReadOnly = true;
            column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridConnections.Columns.Add(column3);

            DataGridViewColumn column4 = new DataGridViewColumn();
            column4.CellTemplate = cell0;
            //column4.HeaderText = @"Connect"; // Button "Connect"
            column4.ReadOnly = false;
            column4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridConnections.Columns.Add(column4);

            DataGridViewColumn column5 = new DataGridViewColumn();
            column5.CellTemplate = cell0;
            //column5.HeaderText = @"Disconnect"; // Button "Disconnect"
            column5.ReadOnly = false;
            column5.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridConnections.Columns.Add(column5);

            DataGridViewColumn column6 = new DataGridViewColumn();
            column6.CellTemplate = cell0;
            //column6.HeaderText = @"Delete"; // Button "Delete"
            column6.ReadOnly = false;
            column6.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridConnections.Columns.Add(column6);

            HostPreConfiguredConnections.Child = _gridConnections;

            _gridConnections.CellClick += _gridConnections_CellClick;
            _gridConnections.CellEndEdit += _gridConnections_CellEndEdit;
            _gridConnections.DataError += _gridConnections_DataError;
        }

        private void _gridConnections_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            ServerMaster.SendNewLogMessage(e.ToString(), Logging.LogMessageType.Error);
        }

        private void _gridConnections_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                for (int i = 1; i < _gridConnections.Rows.Count && i < _serversArray.Count; i++)
                {
                    if (_gridConnections.Rows[i].Cells[2].Value != null)
                    {
                        string value = _gridConnections.Rows[i].Cells[2].Value.ToString();

                        value = value.RemoveExcessFromSecurityName();

                        if (value != _gridConnections.Rows[i].Cells[2].Value.ToString())
                        {
                            _gridConnections.Rows[i].Cells[2].Value = value;
                        }

                        _serversArray[i].ServerPrefix = value;
                    }
                    else
                    {
                        _serversArray[i].ServerPrefix = "";
                    }

                    _gridConnections.Rows[i].Cells[0].Value = _serversArray[i].ServerNameAndPrefix;
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void _gridConnections_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                int row = e.RowIndex;
                int column = e.ColumnIndex;

                if (row > -1 &&
                    row < _gridConnections.Rows.Count - 1)
                {
                    if (row >= _serversArray.Count)
                    {
                        return;
                    }
                    ChangeActiveServer(row);
                }

                if (column == 6
                    && row == _gridConnections.Rows.Count - 1)
                {// Add new
                    CreateNewConnector();
                    UpdateServersCount();
                    PaintGridConnectionsInstance();
                    ChangeActiveServer(row);
                    ServerMaster.SaveServerInstanceByType(_server.ServerType);
                }
                else if (column == 6
                    && row != 0)
                {// Delete
                    if (row >= _serversArray.Count)
                    {
                        return;
                    }
                    DeleteServer(row);
                    UpdateServersCount();
                    PaintGridConnectionsInstance();
                    ChangeActiveServer(0);
                }
                else if (column == 5
                    && row < _gridConnections.Rows.Count - 1)
                {// Disconnect
                    ChangeActiveServer(row);
                    _server.StopServer();
                }
                else if (column == 4
                    && row < _gridConnections.Rows.Count - 1)
                {// Connect
                    ChangeActiveServer(row);
                    _server.StartServer();
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void DeleteServer(int rowIndex)
        {
            AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Market.Label165);

            ui.ShowDialog();

            if (ui.UserAcceptAction == false)
            {
                return;
            }

            AServer server = _serversArray[rowIndex];

            new Task(() =>
            {
                try
                {
                    ServerMaster.DeleteServer(server.ServerType, server.ServerNum);
                }
                catch (Exception ex)
                {
                    ServerMaster.SendNewLogMessage(ex.Message, Logging.LogMessageType.Error);
                }
            }).Start();
        }

        private void ChangeActiveServer(int number)
        {
            if (_server != null)
            {
                if (_server.ServerNum == _serversArray[number].ServerNum)
                {
                    return;
                }
                _server.Log.StopPaint();
                _server.ConnectStatusChangeEvent -= Server_ConnectStatusChangeEvent;
            }

            _server = _serversArray[number];
            PaintGridServerParameters();

            string label = OsLocalization.Market.Label164 + ": " + _server.ServerNameAndPrefix;

            label = label.Replace("_", "-");

            LabelCurrentConnectionName.Content = label;

            for (int i2 = 0; _gridConnections != null && i2 < _gridConnections.Rows.Count; i2++)
            {
                DataGridViewRow row = _gridConnections.Rows[i2];

                if (i2 == number)
                {
                    for (int i = 0; i < row.Cells.Count; i++)
                    {
                        if (i == 3)
                        {
                            continue;
                        }
                        row.Cells[i].Style.ForeColor = Color.FromArgb(255, 83, 0);
                        row.Cells[i].Style.SelectionForeColor = Color.FromArgb(255, 83, 0);
                    }
                }
                else
                {
                    for (int i = 0; i < row.Cells.Count; i++)
                    {
                        if (i == 3)
                        {
                            continue;
                        }
                        row.Cells[i].Style = _gridConnections.DefaultCellStyle;
                    }
                }
            }

            _server.Log.StartPaint(HostLog);
            LabelStatus.Content = _server.ServerStatus;
            _server.ConnectStatusChangeEvent += Server_ConnectStatusChangeEvent;
        }

        private void CreateNewConnector()
        {
            // 1 server number

            int number = -1;

            for (int i = 0; i < _serversArray.Count; i++)
            {
                if (_serversArray[i].ServerNum > number)
                {
                    number = _serversArray[i].ServerNum;
                }
            }

            number++;

            // 2 create server

            ServerType serverType = _server.ServerType;

            ServerMaster.CreateServer(serverType, false, number);

        }

        private void PaintGridConnectionsInstance()
        {
            if (LabelStatus.Dispatcher.CheckAccess() == false)
            {
                LabelStatus.Dispatcher.Invoke(new Action(PaintGridConnectionsInstance));
                return;
            }

            _gridConnections.CellEndEdit -= _gridConnections_CellEndEdit;

            _gridConnections.Rows.Clear();

            for (int i = 0; i < _serversArray.Count; i++)
            {
                AServer currentServer = _serversArray[i];

                DataGridViewRow newRow = GetServerRow(currentServer);

                _gridConnections.Rows.Add(newRow);
            }

            _gridConnections.Rows.Add(GetEndRowToGridConnections());

            _gridConnections.CellEndEdit += _gridConnections_CellEndEdit;
        }

        private DataGridViewRow GetServerRow(AServer server)
        {
            // Server name (Unique)
            // Server number
            // Server prefix
            // Server state
            // Button "Connect"
            // Button "Disconnect"
            // Button "Delete"

            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[0].Value = server.ServerNameAndPrefix;         // Server name

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[1].Value = server.ServerNum;                   // Server number

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[2].Value = server.ServerPrefix;                // Server prefix

            if (server.ServerNum == 0)
            {
                nRow.Cells[2].ReadOnly = true;
            }

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[3].Value = server.ServerStatus.ToString();     // Server state

            DataGridViewButtonCell button1 = new DataGridViewButtonCell();
            button1.Value = OsLocalization.Market.ButtonConnect;            // Button "Connect"
            nRow.Cells.Add(button1);

            DataGridViewButtonCell button2 = new DataGridViewButtonCell();
            button2.Value = OsLocalization.Market.ButtonDisconnect;         // Button "Disconnect"
            nRow.Cells.Add(button2);

            if (server.ServerNum != 0)
            {
                DataGridViewButtonCell button3 = new DataGridViewButtonCell();
                button3.Value = OsLocalization.Market.Label47;                                 // Button "Delete"
                nRow.Cells.Add(button3);
            }
            else
            {
                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[nRow.Cells.Count - 1].ReadOnly = true;
            }

            return nRow;
        }

        private DataGridViewRow GetEndRowToGridConnections()
        {
            // Server name (Unique)
            // Server number
            // Server prefix
            // Server state
            // Button "Connect"
            // Button "Disconnect"
            // Button "Delete"

            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());// Server name                   
            nRow.Cells[nRow.Cells.Count - 1].ReadOnly = true;

            nRow.Cells.Add(new DataGridViewTextBoxCell());  // Server number                
            nRow.Cells[nRow.Cells.Count - 1].ReadOnly = true;

            nRow.Cells.Add(new DataGridViewTextBoxCell()); // Server prefix         
            nRow.Cells[nRow.Cells.Count - 1].ReadOnly = true;

            nRow.Cells.Add(new DataGridViewTextBoxCell()); // Server state
            nRow.Cells[nRow.Cells.Count - 1].ReadOnly = true;

            nRow.Cells.Add(new DataGridViewTextBoxCell()); // Button "Connect"
            nRow.Cells[nRow.Cells.Count - 1].ReadOnly = true;

            nRow.Cells.Add(new DataGridViewTextBoxCell()); // Button "Disconnect"
            nRow.Cells[nRow.Cells.Count - 1].ReadOnly = true;

            DataGridViewButtonCell button1 = new DataGridViewButtonCell();
            button1.Value = OsLocalization.Market.Label170; // Button "Add new"
            nRow.Cells.Add(button1);

            return nRow;
        }

        private void UpdateStatusThread()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(1000);

                    if (_uiIsClosed)
                    {
                        return;
                    }

                    if (MainWindow.ProccesIsWorked == false)
                    {
                        return;
                    }

                    TryRepaintConnectionStatus();
                }
                catch (Exception ex)
                {
                    ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void TryRepaintConnectionStatus()
        {
            try
            {
                if (HostPreConfiguredConnections.Dispatcher.CheckAccess() == false)
                {
                    HostPreConfiguredConnections.Dispatcher.Invoke(new Action(TryRepaintConnectionStatus));
                    return;
                }

                for (int i = 0; _serversArray != null && i < _serversArray.Count && i < _gridConnections.Rows.Count; i++)
                {
                    string curState = _serversArray[i].ServerStatus.ToString();

                    string stateInTable = null;

                    if (_gridConnections.Rows[i].Cells[3].Value != null)
                    {
                        stateInTable = _gridConnections.Rows[i].Cells[3].Value.ToString();
                    }

                    if (curState != stateInTable)
                    {
                        _gridConnections.Rows[i].Cells[3].Value = curState;
                    }

                    if (_serversArray[i].ServerStatus == ServerConnectStatus.Connect &&
                        _gridConnections.Rows[i].Cells[3].Style.ForeColor != Color.Green)
                    { // заливаем зелёным
                        _gridConnections.Rows[i].Cells[3].Style.ForeColor = Color.Green;
                        _gridConnections.Rows[i].Cells[3].Style.SelectionForeColor = Color.Green;
                    }
                    else if (_serversArray[i].ServerStatus == ServerConnectStatus.Disconnect
                        && _gridConnections.Rows[i].Cells[3].Style.ForeColor != Color.Red)
                    { // текст стандартный
                        _gridConnections.Rows[i].Cells[3].Style.ForeColor = Color.Red;
                        _gridConnections.Rows[i].Cells[3].Style.SelectionForeColor = Color.Red;
                    }
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion

        #region Select connection grid

        private AServer _server;

        private DataGridView _gridServerParameters;

        private void Server_ConnectStatusChangeEvent(string s)
        {
            try
            {
                if (LabelStatus.Dispatcher.CheckAccess() == false)
                {
                    LabelStatus.Dispatcher.Invoke(new Action<string>(Server_ConnectStatusChangeEvent), s);
                    return;
                }

                if (_server == null)
                {
                    return;
                }

                LabelStatus.Content = _server.ServerStatus;
            }
            catch
            {
                // ignore
            }
        }

        public void CreateGridServerParameters()
        {
            _gridServerParameters = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect,
                DataGridViewAutoSizeRowsMode.AllCells);
            _gridServerParameters.ScrollBars = ScrollBars.Vertical;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _gridServerParameters.DefaultCellStyle;

            DataGridViewColumn colum1 = new DataGridViewColumn();
            colum1.CellTemplate = cell0;
            colum1.HeaderText = OsLocalization.Market.GridColumn1;
            colum1.ReadOnly = true;
            colum1.Width = 300;
            _gridServerParameters.Columns.Add(colum1);

            DataGridViewColumn colum2 = new DataGridViewColumn();
            colum2.CellTemplate = cell0;
            colum2.HeaderText = OsLocalization.Market.GridColumn2;
            colum2.ReadOnly = false;

            colum2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _gridServerParameters.Columns.Add(colum2);

            DataGridViewColumn colum3 = new DataGridViewColumn();
            colum3.CellTemplate = cell0;
            colum3.HeaderText = @"";
            colum3.ReadOnly = true;
            colum3.Width = 100;

            _gridServerParameters.Columns.Add(colum3);

            DataGridViewColumn colum4 = new DataGridViewColumn();
            colum4.CellTemplate = cell0;
            colum4.HeaderText = @"";
            colum4.ReadOnly = true;
            colum4.Width = 100;

            _gridServerParameters.Columns.Add(colum4);

            HostSettings.Child = _gridServerParameters;

            _gridServerParameters.CellValueChanged += _gridServerParameters_CellValueChanged;
            _gridServerParameters.Click += _gridServerParameters_Click;
            _gridServerParameters.CellClick += _gridServerParameters_CellClick;
            _gridServerParameters.DataError += _gridServerParameters_DataError;
        }

        public void PaintGridServerParameters()
        {
            if (_gridServerParameters == null
                || _server == null)
            {
                return;
            }

            List<IServerParameter> param = _server.ServerParameters;

            _gridServerParameters.Rows.Clear();

            for (int i = 0; i < param.Count; i++)
            {
                DataGridViewRow newRow = null;

                if (param[i].Type == ServerParameterType.String)
                {
                    newRow = GetStringParamRow((ServerParameterString)param[i]);
                }
                else if (param[i].Type == ServerParameterType.Password)
                {
                    newRow = GetPasswordParamRow((ServerParameterPassword)param[i]);
                }
                else if (param[i].Type == ServerParameterType.Bool)
                {
                    newRow = GetBooleanParamRow((ServerParameterBool)param[i]);
                }
                else if (param[i].Type == ServerParameterType.Int)
                {
                    newRow = GetIntParamRow((ServerParameterInt)param[i]);
                }
                else if (param[i].Type == ServerParameterType.Path)
                {
                    newRow = GetPathParamRow((ServerParameterPath)param[i]);
                }
                else if (param[i].Type == ServerParameterType.Enum)
                {
                    newRow = GetEnumParamRow((ServerParameterEnum)param[i]);
                }
                else if (param[i].Type == ServerParameterType.Button)
                {
                    newRow = GetButtonParamRow((ServerParameterButton)param[i]);
                }

                _gridServerParameters.Rows.Add(newRow);
            }
        }

        private void _gridServerParameters_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            ServerMaster.SendNewLogMessage(e.Exception.ToString(), Logging.LogMessageType.Error);
        }

        private void _gridServerParameters_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                int row = e.RowIndex;
                int col = e.ColumnIndex;

                if (col != 3)
                {
                    return;
                }

                if (row >= _gridServerParameters.Rows.Count)
                {
                    return;
                }

                if (string.IsNullOrEmpty(_server.ServerParameters[row].Comment))
                {
                    return;
                }

                CustomMessageBoxUi ui = new CustomMessageBoxUi(_server.ServerParameters[row].Comment);
                ui.ShowDialog();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void _gridServerParameters_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                SaveParam();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void _gridServerParameters_Click(object sender, EventArgs e)
        {
            try
            {
                var s = e.GetType();

                var mouse = (MouseEventArgs)e;

                int clickRow = _gridServerParameters.SelectedCells[0].RowIndex;

                int clickColumn = _gridServerParameters.SelectedCells[0].ColumnIndex;

                List<IServerParameter> param =
                _server.ServerParameters;

                if (clickRow >= param.Count)
                {
                    return;
                }

                if (param[clickRow].Type == ServerParameterType.Path &&
                    clickColumn == 2)
                {
                    ((ServerParameterPath)param[clickRow]).ShowPathDialog();
                    PaintGridServerParameters();
                }
                else if (param[clickRow].Type == ServerParameterType.Button &&
                    clickColumn == 1)
                {
                    ((ServerParameterButton)param[clickRow]).ActivateButtonClick();
                }
                else if (param[clickRow].Type == ServerParameterType.Password &&
                clickColumn == 2)
                {
                    _gridServerParameters.CellValueChanged -= _gridServerParameters_CellValueChanged;
                    _gridServerParameters.Rows[clickRow].Cells[1].Value = ((ServerParameterPassword)param[clickRow]).Value;
                    _gridServerParameters.Rows[clickRow].Cells[1].ReadOnly = false;
                    _gridServerParameters.Rows[clickRow].Cells[2] = new DataGridViewTextBoxCell();
                    _gridServerParameters.CellValueChanged += _gridServerParameters_CellValueChanged;
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private DataGridViewRow GetButtonParamRow(ServerParameterButton param)
        {
            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[0].Value = "";

            DataGridViewButtonCell comboBox = new DataGridViewButtonCell();

            nRow.Cells.Add(comboBox);
            nRow.Cells[1].Value = param.Name;

            if (param.Comment != null)
            {
                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[2].Value = "";

                DataGridViewButtonCell button = new DataGridViewButtonCell();
                button.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                button.Value = OsLocalization.Market.Label86;

                nRow.Cells.Add(button);
            }

            return nRow;
        }

        private DataGridViewRow GetEnumParamRow(ServerParameterEnum param)
        {
            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[0].Value = param.Name;

            DataGridViewComboBoxCell comboBox = new DataGridViewComboBoxCell();

            for (int i = 0; i < param.EnumValues.Count; i++)
            {
                comboBox.Items.Add(param.EnumValues[i]);
            }

            nRow.Cells.Add(comboBox);
            nRow.Cells[1].Value = param.Value;

            if (param.Comment != null)
            {
                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[2].Value = "";

                DataGridViewButtonCell button = new DataGridViewButtonCell();
                button.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                button.Value = OsLocalization.Market.Label86;

                nRow.Cells.Add(button);
            }

            return nRow;
        }

        private DataGridViewRow GetPasswordParamRow(ServerParameterPassword param)
        {
            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[0].Value = param.Name;

            if (string.IsNullOrEmpty(param.Value))
            {
                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[1].Value = param.Value;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[2].Value = "";
            }
            else
            {
                string value = "";

                for (int i = 0; i < param.Value.Length; i++)
                {
                    value += "*";
                }
                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[1].ReadOnly = true;
                nRow.Cells[1].Value = value;

                DataGridViewImageCell imageCell = new DataGridViewImageCell();
                Bitmap bmp = new Bitmap(System.Windows.Forms.Application.StartupPath + "\\Images\\eye.png");
                imageCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                imageCell.Value = bmp;

                nRow.Cells.Add(imageCell);
            }

            if (param.Comment != null)
            {


                DataGridViewButtonCell button = new DataGridViewButtonCell();
                button.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                button.Value = OsLocalization.Market.Label86;

                nRow.Cells.Add(button);
            }

            return nRow;
        }

        private DataGridViewRow GetPathParamRow(ServerParameterPath param)
        {
            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[0].Value = param.Name;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[1].Value = param.Value;

            DataGridViewButtonCell button = new DataGridViewButtonCell();
            button.Value = OsLocalization.Market.Label85;

            nRow.Cells.Add(button);
            nRow.Cells[1].Value = param.Value;

            if (param.Comment != null)
            {
                DataGridViewButtonCell button2 = new DataGridViewButtonCell();
                button2.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                button2.Value = OsLocalization.Market.Label86;

                nRow.Cells.Add(button2);
            }

            return nRow;
        }

        private DataGridViewRow GetStringParamRow(ServerParameterString param)
        {
            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[0].Value = param.Name;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[1].Value = param.Value;

            if (param.Comment != null)
            {
                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[2].Value = "";

                DataGridViewButtonCell button = new DataGridViewButtonCell();
                button.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                button.Value = OsLocalization.Market.Label86;

                nRow.Cells.Add(button);
            }

            return nRow;
        }

        private DataGridViewRow GetIntParamRow(ServerParameterInt param)
        {
            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[0].Value = param.Name;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[1].Value = param.Value;

            if (param.Comment != null)
            {
                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[2].Value = "";

                DataGridViewButtonCell button = new DataGridViewButtonCell();
                button.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                button.Value = OsLocalization.Market.Label86;

                nRow.Cells.Add(button);
            }

            return nRow;
        }

        private DataGridViewRow GetBooleanParamRow(ServerParameterBool param)
        {
            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[0].Value = param.Name;

            DataGridViewComboBoxCell checkBox = new DataGridViewComboBoxCell();
            checkBox.Items.Add("True");
            checkBox.Items.Add("False");

            if (param.Value)
            {
                checkBox.Value = "True";
            }
            else
            {
                checkBox.Value = "False";
            }


            nRow.Cells.Add(checkBox);

            if (param.Comment != null)
            {
                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[2].Value = "";

                DataGridViewButtonCell button = new DataGridViewButtonCell();
                button.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                button.Value = OsLocalization.Market.Label86;

                nRow.Cells.Add(button);
            }

            return nRow;
        }

        #endregion

        public void SaveParam()
        {
            List<IServerParameter> param = _server.ServerParameters;

            for (int i = 0; i < param.Count; i++)
            {
                if (_gridServerParameters.Rows[i].Cells[1].Value == null)
                {
                    _gridServerParameters.Rows[i].Cells[1].Value = "";
                }

                if (param[i].Type == ServerParameterType.String)
                {
                    ((ServerParameterString)param[i]).Value = _gridServerParameters.Rows[i].Cells[1].Value.ToString();
                }
                else if (param[i].Type == ServerParameterType.Password)
                {
                    string str = _gridServerParameters.Rows[i].Cells[1].Value.ToString().Replace("*", "");
                    if (str != "")
                    {
                        ((ServerParameterPassword)param[i]).Value = _gridServerParameters.Rows[i].Cells[1].Value.ToString();
                    }
                }
                else if (param[i].Type == ServerParameterType.Enum)
                {
                    ((ServerParameterEnum)param[i]).Value = _gridServerParameters.Rows[i].Cells[1].Value.ToString();
                }

                else if (param[i].Type == ServerParameterType.Bool)
                {
                    string str = _gridServerParameters.Rows[i].Cells[1].Value.ToString();
                    if (str == "True")
                    {
                        ((ServerParameterBool)param[i]).Value = true;
                    }
                    else
                    {
                        ((ServerParameterBool)param[i]).Value = false;
                    }
                }
                else if (param[i].Type == ServerParameterType.Decimal)
                {
                    if (_gridServerParameters.Rows[i].Cells[1].Value.ToString() == "")
                    {
                        _gridServerParameters.Rows[i].Cells[1].Value = "0";
                    }
                    string str = _gridServerParameters.Rows[i].Cells[1].Value.ToString();
                    ((ServerParameterDecimal)param[i]).Value =
                        Convert.ToDecimal(str.Replace(".",
                            CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator));

                }
                else if (param[i].Type == ServerParameterType.Int)
                {
                    if (_gridServerParameters.Rows[i].Cells[1].Value.ToString() == "")
                    {
                        _gridServerParameters.Rows[i].Cells[1].Value = "0";
                    }

                    string str = _gridServerParameters.Rows[i].Cells[1].Value.ToString();
                    ((ServerParameterInt)param[i]).Value = Convert.ToInt32(str);
                }

            }
        }

        private void ButtonConnect_Click(object sender, RoutedEventArgs e)
        {
            _server.StartServer();
        }

        private void ButtonAbort_Click(object sender, RoutedEventArgs e)
        {
            _server.StopServer();
        }
    }
}