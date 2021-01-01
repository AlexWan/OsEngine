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
            _server = server;

            _server.Log.StartPaint(HostLog);

            CreateParamDataGrid();
            UpdateParamDataGrid();
            LabelStatus.Content = server.ServerStatus;
            server.ConnectStatusChangeEvent += Server_ConnectStatusChangeEvent;

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
        }

        private void Server_ConnectStatusChangeEvent(string s)
        {
            if (LabelStatus.Dispatcher.CheckAccess() == false)
            {
                LabelStatus.Dispatcher.Invoke(new Action<string>(Server_ConnectStatusChangeEvent), s);
                return;
            }
            LabelStatus.Content = _server.ServerStatus;
        }

        private DataGridView _newGrid;

        public void CreateParamDataGrid()
        {
            _newGrid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect, DataGridViewAutoSizeRowsMode.None);

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _newGrid.DefaultCellStyle;

            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = OsLocalization.Market.GridColumn1;
            colum0.ReadOnly = true;
            colum0.Width = 200;
            _newGrid.Columns.Add(colum0);

            DataGridViewColumn colu = new DataGridViewColumn();
            colu.CellTemplate = cell0;
            colu.HeaderText = OsLocalization.Market.GridColumn2;
            colu.ReadOnly = false;

            colu.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _newGrid.Columns.Add(colu);

            DataGridViewColumn colum1 = new DataGridViewColumn();
            colum1.CellTemplate = cell0;
            colum1.HeaderText = @"";
            colum1.ReadOnly = true;
            colum1.Width = 100;

            _newGrid.Columns.Add(colum1);
            HostSettings.Child = _newGrid;

            _newGrid.CellValueChanged += _newGrid_CellValueChanged;

            _newGrid.Click += _newGrid_Click;
        }

        public void UpdateParamDataGrid()
        {
            List<IServerParameter> param = _server.ServerParameters;

            _newGrid.Rows.Clear();

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

                _newGrid.Rows.Add(newRow);
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

            return nRow;
        }

        private DataGridViewRow GetPasswordParamRow(ServerParameterPassword param)
        {
            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[0].Value = param.Name;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[1].Value = param.Value;

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
            button.Value = "Настроить";

            nRow.Cells.Add(button);
            nRow.Cells[1].Value = param.Value;

            return nRow;
        }

        private DataGridViewRow GetStringParamRow(ServerParameterString param)
        {
            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[0].Value = param.Name;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[1].Value = param.Value;

            return nRow;
        }

        private DataGridViewRow GetIntParamRow(ServerParameterInt param)
        {
            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[0].Value = param.Name;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[1].Value = param.Value;

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
            // nRow.Cells[1].Value = param.Value;

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

            int clickRow = _newGrid.SelectedCells[0].RowIndex;

            int clickColumn = _newGrid.SelectedCells[0].ColumnIndex;

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
                if (_newGrid.Rows[i].Cells[1].Value == null)
                {
                    _newGrid.Rows[i].Cells[1].Value = "";
                }

                if (param[i].Type == ServerParameterType.String)
                {
                    ((ServerParameterString)param[i]).Value = _newGrid.Rows[i].Cells[1].Value.ToString();
                }
                else if (param[i].Type == ServerParameterType.Password)
                {
                    string str = _newGrid.Rows[i].Cells[1].Value.ToString().Replace("*", "");
                    if (str != "")
                    {
                        ((ServerParameterPassword)param[i]).Value = str;
                    }
                }
                else if (param[i].Type == ServerParameterType.Enum)
                {
                    ((ServerParameterEnum)param[i]).Value = _newGrid.Rows[i].Cells[1].Value.ToString();
                }

                else if (param[i].Type == ServerParameterType.Bool)
                {
                    string str = _newGrid.Rows[i].Cells[1].Value.ToString();
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
                    if (_newGrid.Rows[i].Cells[1].Value.ToString() == "")
                    {
                        _newGrid.Rows[i].Cells[1].Value = "0";
                    }
                    string str = _newGrid.Rows[i].Cells[1].Value.ToString();
                    ((ServerParameterDecimal)param[i]).Value =
                        Convert.ToDecimal(str.Replace(".",
                            CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator));

                }
                else if (param[i].Type == ServerParameterType.Int)
                {
                    if (_newGrid.Rows[i].Cells[1].Value.ToString() == "")
                    {
                        _newGrid.Rows[i].Cells[1].Value = "0";
                    }

                    string str = _newGrid.Rows[i].Cells[1].Value.ToString();
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