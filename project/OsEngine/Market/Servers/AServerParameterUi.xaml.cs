/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Forms;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market.Servers.Entity;
using Color = System.Drawing.Color;

namespace OsEngine.Market.Servers
{
    /// <summary>
    /// interaction logic for AServerParameterUi.xaml
    /// Логика взаимодействия для AServerParameterUi.xaml
    /// </summary>
    public partial class AServerParameterUi
    {
        private AServer _server;
        public AServerParameterUi(AServer server)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            _server = server;

            _server.Log.StartPaint(HostLog);

            CreateParamDataGrid();
            UpdateParamDataGrid();
            LabelStatus.Content = server.ServerStatus;
            _server.ConnectStatusChangeEvent += Server_ConnectStatusChangeEvent;

            Title = OsLocalization.Market.TitleAServerParametrUi + _server.ServerType;
            TabItemParams.Header = OsLocalization.Market.TabItem3;
            TabItemLog.Header = OsLocalization.Market.TabItem4;
            Label21.Content = OsLocalization.Market.Label21;
            ButtonConnect.Content = OsLocalization.Market.ButtonConnect;
            ButtonAbort.Content = OsLocalization.Market.ButtonDisconnect;

            if (_server.NeedToHideParams == true)
            {
                TabItemParams.Visibility = Visibility.Hidden;
                TabItemLog.IsSelected = true;
            }
            this.Closed += AServerParameterUi_Closed;

            this.Activate();
            this.Focus();
        }

        public void Dispose()
        {
            _server.Log.StopPaint();
            _server.ConnectStatusChangeEvent -= Server_ConnectStatusChangeEvent;
            _server = null;

            _paramsGrid.CellValueChanged -= _newGrid_CellValueChanged;
            _paramsGrid.Click -= _newGrid_Click;
            _paramsGrid.Rows.Clear();
            DataGridFactory.ClearLinks(_paramsGrid);
            _paramsGrid = null;

          
        }

        private void AServerParameterUi_Closed(object sender, EventArgs e)
        {
            this.Closed -= AServerParameterUi_Closed;
            Dispose();
        }

        private void Server_ConnectStatusChangeEvent(string s)
        {
            try
            {
                if (LabelStatus.Dispatcher.CheckAccess() == false)
                {
                    LabelStatus.Dispatcher.Invoke(new Action<string>(Server_ConnectStatusChangeEvent), s);
                    return;
                }

                if(_server == null)
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

        private DataGridView _paramsGrid;

        public void CreateParamDataGrid()
        {
            _paramsGrid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect, 
                DataGridViewAutoSizeRowsMode.AllCells);
            _paramsGrid.ScrollBars = ScrollBars.Vertical;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _paramsGrid.DefaultCellStyle;

            DataGridViewColumn colum1 = new DataGridViewColumn();
            colum1.CellTemplate = cell0;
            colum1.HeaderText = OsLocalization.Market.GridColumn1;
            colum1.ReadOnly = true;
            colum1.Width = 300;
            _paramsGrid.Columns.Add(colum1);

            DataGridViewColumn colum2 = new DataGridViewColumn();
            colum2.CellTemplate = cell0;
            colum2.HeaderText = OsLocalization.Market.GridColumn2;
            colum2.ReadOnly = false;

            colum2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _paramsGrid.Columns.Add(colum2);

            DataGridViewColumn colum3 = new DataGridViewColumn();
            colum3.CellTemplate = cell0;
            colum3.HeaderText = @"";
            colum3.ReadOnly = true;
            colum3.Width = 100;

            _paramsGrid.Columns.Add(colum3);

            DataGridViewColumn colum4 = new DataGridViewColumn();
            colum4.CellTemplate = cell0;
            colum4.HeaderText = @"";
            colum4.ReadOnly = true;
            colum4.Width = 100;

            _paramsGrid.Columns.Add(colum4);

            HostSettings.Child = _paramsGrid;
            _paramsGrid.CellValueChanged += _newGrid_CellValueChanged;
            _paramsGrid.Click += _newGrid_Click;
            _paramsGrid.CellClick += _paramsGrid_CellClick;
        }

        private void _paramsGrid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            int row = e.RowIndex;
            int col = e.ColumnIndex;

            if(col != 3)
            {
                return;
            }

            if (row >= _paramsGrid.Rows.Count)
            {
                return;
            }

            if(string.IsNullOrEmpty(_server.ServerParameters[row].Comment))
            {
                return;
            }

            CustomMessageBoxUi ui = new CustomMessageBoxUi(_server.ServerParameters[row].Comment);
            ui.ShowDialog();
        }

        public void UpdateParamDataGrid()
        {
            List<IServerParameter> param = _server.ServerParameters;

            _paramsGrid.Rows.Clear();

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

                _paramsGrid.Rows.Add(newRow);
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

        void _newGrid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            SaveParam();
        }

        void _newGrid_Click(object sender, EventArgs e)
        {
            var s = e.GetType();

            var mouse = (MouseEventArgs)e;

            int clickRow = _paramsGrid.SelectedCells[0].RowIndex;

            int clickColumn = _paramsGrid.SelectedCells[0].ColumnIndex;

            List<IServerParameter> param =
            _server.ServerParameters;

            if (clickRow < param.Count &&
                param[clickRow].Type == ServerParameterType.Path &&
                clickColumn == 2)
            {
                ((ServerParameterPath)param[clickRow]).ShowPathDialog();
                UpdateParamDataGrid();
            }
            if (clickRow < param.Count &&
                param[clickRow].Type == ServerParameterType.Button &&
                clickColumn == 1)
            {
                ((ServerParameterButton)param[clickRow]).ActivateButtonClick();
            }
        }

        public void SaveParam()
        {
            List<IServerParameter> param = _server.ServerParameters;

            for (int i = 0; i < param.Count; i++)
            {
                if (_paramsGrid.Rows[i].Cells[1].Value == null)
                {
                    _paramsGrid.Rows[i].Cells[1].Value = "";
                }

                if (param[i].Type == ServerParameterType.String)
                {
                    ((ServerParameterString)param[i]).Value = _paramsGrid.Rows[i].Cells[1].Value.ToString();
                }
                else if (param[i].Type == ServerParameterType.Password)
                {
                    string str = _paramsGrid.Rows[i].Cells[1].Value.ToString().Replace("*", "");
                    if (str != "")
                    {
                        ((ServerParameterPassword)param[i]).Value = str;
                    }
                }
                else if (param[i].Type == ServerParameterType.Enum)
                {
                    ((ServerParameterEnum)param[i]).Value = _paramsGrid.Rows[i].Cells[1].Value.ToString();
                }

                else if (param[i].Type == ServerParameterType.Bool)
                {
                    string str = _paramsGrid.Rows[i].Cells[1].Value.ToString();
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
                    if (_paramsGrid.Rows[i].Cells[1].Value.ToString() == "")
                    {
                        _paramsGrid.Rows[i].Cells[1].Value = "0";
                    }
                    string str = _paramsGrid.Rows[i].Cells[1].Value.ToString();
                    ((ServerParameterDecimal)param[i]).Value =
                        Convert.ToDecimal(str.Replace(".",
                            CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator));

                }
                else if (param[i].Type == ServerParameterType.Int)
                {
                    if (_paramsGrid.Rows[i].Cells[1].Value.ToString() == "")
                    {
                        _paramsGrid.Rows[i].Cells[1].Value = "0";
                    }

                    string str = _paramsGrid.Rows[i].Cells[1].Value.ToString();
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