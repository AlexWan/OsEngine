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
using Grpc.Core;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market.Servers.Entity;

namespace OsEngine.Market.Servers
{
    public partial class AServerParameterUi
    {
        #region Service

        public AServerParameterUi(List<AServer> servers)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            _server = servers[0];
            _serversArray = servers;

            _server.Log.StartPaint(HostLog);

            CreateGridServerParameters();
            PaintGridServerParameters();
            LabelStatus.Content = _server.ServerStatus;
            _server.ConnectStatusChangeEvent += Server_ConnectStatusChangeEvent;

            Title = OsLocalization.Market.TitleAServerParametrUi + _server.ServerType;
            TabItemParameters.Header = OsLocalization.Market.TabItem3;
            TabItemLog.Header = OsLocalization.Market.TabItem4;
            Label21.Content = OsLocalization.Market.Label21;
            ButtonConnect.Content = OsLocalization.Market.ButtonConnect;
            ButtonAbort.Content = OsLocalization.Market.ButtonDisconnect;

            if (_server.NeedToHideParameters == true)
            {
                TabItemParameters.Visibility = Visibility.Hidden;
                TabItemLog.IsSelected = true;
            }
            
            if (_server.CanDoMultipleConnections == false)
            {
                SetGuiNoMultipleConnect();
            }
            else
            {
                CreateGridConnectionsInstance();
                PaintGridConnectionsInstance();
            }

            this.Activate();
            this.Focus();

            this.Closed += AServerParameterUi_Closed;
        }

        private void AServerParameterUi_Closed(object sender, EventArgs e)
        {
            this.Closed -= AServerParameterUi_Closed;

            _server.Log.StopPaint();
            _server.ConnectStatusChangeEvent -= Server_ConnectStatusChangeEvent;
            _server = null;

            _serversArray = null;

            _gridServerParameters.CellValueChanged -= _gridServerParameters_CellValueChanged;
            _gridServerParameters.Click -= _gridServerParameters_Click;
            _gridServerParameters.CellClick -= _gridServerParameters_CellClick;
            _gridServerParameters.DataError -= _gridServerParameters_DataError;
            _gridServerParameters.Rows.Clear();
            DataGridFactory.ClearLinks(_gridServerParameters);
            _gridServerParameters = null;

            HostPreConfiguredConnections.Child = null;
            HostSettings.Child = null;
        }

        public void SetGuiNoMultipleConnect()
        {
            LabelPreConfiguredConnection.Visibility = Visibility.Hidden;
            HostPreConfiguredConnections.Visibility = Visibility.Hidden;
            LabelCurrentConnectionName.Visibility = Visibility.Hidden;
            GridPrime.RowDefinitions[0].Height = new GridLength(0);
        }

        #endregion

        #region Multiple connection grid

        private List<AServer> _serversArray;

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
            column0.HeaderText = "Name"; // Server name (Unique)
            column0.ReadOnly = true;
            column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridConnections.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = "Number"; // Server number
            column1.ReadOnly = true;
            column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridConnections.Columns.Add(column1);

            DataGridViewColumn column2 = new DataGridViewColumn();
            column2.CellTemplate = cell0;
            column2.HeaderText = "Prefix"; // Server prefix
            column2.ReadOnly = false;
            column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridConnections.Columns.Add(column2);

            DataGridViewColumn column3 = new DataGridViewColumn();
            column3.CellTemplate = cell0;
            column3.HeaderText = "State"; // Server state
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

            DataGridViewColumn column7 = new DataGridViewColumn();
            column7.CellTemplate = cell0;
            //column7.HeaderText = @"Add new"; // Button "Add new"
            column7.ReadOnly = false;
            column7.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridConnections.Columns.Add(column7);

            HostPreConfiguredConnections.Child = _gridConnections;
        }

        private void PaintGridConnectionsInstance()
        {
            _gridConnections.Rows.Clear();

            for (int i = 0; i < _serversArray.Count; i++)
            {
                AServer currentServer = _serversArray[i];

                DataGridViewRow newRow = GetServerRow(currentServer);

                _gridConnections.Rows.Add(newRow);
            }

            _gridConnections.Rows.Add(GetEndRowToGridConnections());
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
            // Button "Add new"

            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[0].Value = server.ServerNameUnique;            // Server name (Unique)

            nRow.Cells.Add(new DataGridViewTextBoxCell()); 
            nRow.Cells[1].Value = server.ServerNum;                   // Server number

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[2].Value = server.ServerPrefix;                // Server prefix

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[3].Value = server.ServerStatus.ToString();     // Server state

            DataGridViewButtonCell button1 = new DataGridViewButtonCell();
            button1.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            button1.Value = "Connect";                                // Button "Connect"
            nRow.Cells.Add(button1);

            DataGridViewButtonCell button2 = new DataGridViewButtonCell();
            button2.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            button2.Value = "Disconnect";                             // Button "Disconnect"
            nRow.Cells.Add(button2);

            DataGridViewButtonCell button3 = new DataGridViewButtonCell();
            button3.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            button3.Value = "Delete";                                 // Button "Delete"
            nRow.Cells.Add(button3);

            nRow.Cells.Add(new DataGridViewTextBoxCell());            // Button "Add new"

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
            // Button "Add new"

            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());// Server name (Unique)                      

            nRow.Cells.Add(new DataGridViewTextBoxCell());  // Server number                

            nRow.Cells.Add(new DataGridViewTextBoxCell()); // Server prefix         

            nRow.Cells.Add(new DataGridViewTextBoxCell()); // Server state

            nRow.Cells.Add(new DataGridViewTextBoxCell()); // Button "Connect"

            nRow.Cells.Add(new DataGridViewTextBoxCell()); // Button "Disconnect"

            nRow.Cells.Add(new DataGridViewTextBoxCell()); // Button "Delete"

            DataGridViewButtonCell button1 = new DataGridViewButtonCell();
            button1.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            button1.Value = "Add new";                             // Button "Add new"
            nRow.Cells.Add(button1);

            return nRow;
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
            ServerMaster.SendNewLogMessage(e.Exception.ToString(),Logging.LogMessageType.Error);
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

            if(param.Comment != null)
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

                for(int i = 0; i< param.Value.Length; i++)
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