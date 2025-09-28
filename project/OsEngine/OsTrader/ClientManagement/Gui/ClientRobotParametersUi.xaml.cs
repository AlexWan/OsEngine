/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Layout;
using OsEngine.Robots;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;

namespace OsEngine.OsTrader.ClientManagement.Gui
{
    public partial class ClientRobotParametersUi : Window
    {
        private TradeClientRobot _robot;

        private TradeClient _client;

        public ClientRobotParametersUi(TradeClientRobot robot, TradeClient client)
        {
            InitializeComponent();
            _robot = robot;
            _client = client;

            StickyBorders.Listen(this);
            GlobalGUILayout.Listen(this, "TradeClientRobot" + robot.Number);

            ComboBoxRobotType.Items.Add("None");
            List<string> scriptsNames = BotFactory.GetScriptsNamesStrategy();

            List<string> includeNames = BotFactory.GetIncludeNamesStrategy();

            if(includeNames.Count > 0)
            {
                scriptsNames.AddRange(includeNames);
                scriptsNames.Sort();
            }

            for(int i = 0; i < scriptsNames.Count; i++)
            {
                ComboBoxRobotType.Items.Add(scriptsNames[i]);
            }

            ComboBoxRobotType.SelectedItem = _robot.BotClassName;
            ComboBoxRobotType.SelectionChanged += ComboBoxRobotType_SelectionChanged;

            this.Closed += ClientRobotParametersUi_Closed;

            CreateParametersGrid();
            RePaintParametersGrid();

            LabelRobotType.Content = OsLocalization.Trader.Label166;
            this.Title = OsLocalization.Trader.Label608;

        }

        private void ClientRobotParametersUi_Closed(object sender, EventArgs e)
        {
            HostSettings.Child = null;
            _parametersGrid.DataError -= _parametersGrid_DataError;
            _parametersGrid.CellEndEdit -= _parametersGrid_CellEndEdit;
            DataGridFactory.ClearLinks(_parametersGrid);
            _parametersGrid = null;

            _client = null;
            _robot = null;
        }

        private void ComboBoxRobotType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                _robot.BotClassName = ComboBoxRobotType.SelectedItem.ToString();
                _client.Save();
                RePaintParametersGrid();
                _client.UpdateInfo();
            }
            catch
            {
                // ignore
            }
        }

        #region Parameters grid

        private DataGridView _parametersGrid;

        private void CreateParametersGrid()
        {
            _parametersGrid =
             DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect,
             DataGridViewAutoSizeRowsMode.AllCells);

            _parametersGrid.ScrollBars = ScrollBars.Vertical;

            _parametersGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _parametersGrid.DefaultCellStyle;

            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = "#"; //"Num";
            colum0.ReadOnly = true;
            colum0.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            _parametersGrid.Columns.Add(colum0);

            DataGridViewColumn colum1 = new DataGridViewColumn();
            colum1.CellTemplate = cell0;
            colum1.HeaderText = OsLocalization.Trader.Label61; //"Name";
            colum1.ReadOnly = true;
            colum1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _parametersGrid.Columns.Add(colum1);

            DataGridViewColumn colum2 = new DataGridViewColumn();
            colum2.CellTemplate = cell0;
            colum2.HeaderText = OsLocalization.Trader.Label167; //"Type";
            colum2.ReadOnly = false;
            colum2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _parametersGrid.Columns.Add(colum2);

            DataGridViewColumn colum3 = new DataGridViewColumn();
            colum3.CellTemplate = cell0;
            colum3.HeaderText = OsLocalization.Trader.Label321; //"Value";
            colum3.ReadOnly = false;
            colum3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _parametersGrid.Columns.Add(colum3);

            HostSettings.Child = _parametersGrid;
            _parametersGrid.DataError += _parametersGrid_DataError;
            _parametersGrid.CellEndEdit += _parametersGrid_CellEndEdit;
        }

        private void RePaintParametersGrid()
        {
            try
            {
                if (_parametersGrid.InvokeRequired)
                {
                    _parametersGrid.Invoke(new Action(RePaintParametersGrid));
                    return;
                }

                //0 "Num";
                //1 "Name";
                //2 "Type";
                //3 "Value";

                _parametersGrid.Rows.Clear();

                for (int i = 0; i < _robot.Parameters.Count; i++)
                {
                    IIStrategyParameter parameter = _robot.Parameters[i];

                    if (parameter == null)
                    {
                        continue;
                    }

                    _parametersGrid.Rows.Add(GetParameterRow(parameter,i));
                }
            }
            catch (Exception error)
            {
                _client.SendNewLogMessage(error.ToString(), Logging.LogMessageType.Error);
            }
        }

        private DataGridViewRow GetParameterRow(IIStrategyParameter parameter, int number)
        {
            //0 "Num";
            //1 "Name";
            //2 "Type";
            //3 "Value";

            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[^1].Value = number;
            row.Cells[^1].ReadOnly = true;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[^1].Value = parameter.Name;
            row.Cells[^1].ReadOnly = true;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[^1].Value = parameter.Type.ToString();
            row.Cells[^1].ReadOnly = true;

            if (parameter.Type == StrategyParameterType.Bool
                || parameter.Type == StrategyParameterType.CheckBox)
            {
                DataGridViewComboBoxCell comboBox = new DataGridViewComboBoxCell();
                comboBox.Items.Add("True");
                comboBox.Items.Add("False");
                row.Cells.Add(comboBox);

                if (parameter.Type == StrategyParameterType.Bool)
                {
                    row.Cells[^1].Value = ((StrategyParameterBool)parameter).ValueBool.ToString();
                }
                else if (parameter.Type == StrategyParameterType.CheckBox)
                {
                    row.Cells[^1].Value = ((StrategyParameterCheckBox)parameter).CheckState.ToString();
                }
            }
            else if (parameter.Type == StrategyParameterType.Decimal)
            {
                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[^1].Value = ((StrategyParameterDecimal)parameter).ValueDecimal;
            }
            else if (parameter.Type == StrategyParameterType.Int)
            {
                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[^1].Value = ((StrategyParameterInt)parameter).ValueInt;
            }
            else if (parameter.Type == StrategyParameterType.String)
            {
                StrategyParameterString stringParam = (StrategyParameterString)parameter;

                if(stringParam.ValuesString != null
                    && stringParam.ValuesString.Count > 1)
                {
                    DataGridViewComboBoxCell comboBox = new DataGridViewComboBoxCell();
                    
                    for(int i = 0;i < stringParam.ValuesString.Count;i++)
                    {
                        comboBox.Items.Add(stringParam.ValuesString[i]);
                    }

                    comboBox.Value = stringParam.ValueString;

                    row.Cells.Add(comboBox);
                }
                else
                {
                    row.Cells.Add(new DataGridViewTextBoxCell());
                    row.Cells[^1].Value = ((StrategyParameterString)parameter).ValueString;
                }
            }
            else if (parameter.Type == StrategyParameterType.TimeOfDay)
            {
                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[^1].Value = ((StrategyParameterTimeOfDay)parameter).Value.ToString();
            }
            else if (parameter.Type == StrategyParameterType.DecimalCheckBox)
            {

            }
            else
            {

            }

            return row;
        }

        private void _parametersGrid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            _client.SendNewLogMessage(e.Exception.ToString(), Logging.LogMessageType.Error);
        }

        private void _parametersGrid_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                //0 "Num";
                //1 "Name";
                //2 "Type";
                //3 "Value";

                for (int i = 0; i < _parametersGrid.Rows.Count; i++)
                {
                    string name = _parametersGrid.Rows[i].Cells[1].Value.ToString();
                    string type = _parametersGrid.Rows[i].Cells[2].Value.ToString();
                    string value = _parametersGrid.Rows[i].Cells[3].Value.ToString();

                    StrategyParameterType typeEnum;

                    if (Enum.TryParse(type, out typeEnum) == false)
                    {
                        continue;
                    }

                    for (int i2 = 0; i2 < _robot.Parameters.Count; i2++)
                    {
                        IIStrategyParameter parameter = _robot.Parameters[i];

                        if (parameter == null)
                        {
                            continue;
                        }

                        if (parameter.Name != name
                            && parameter.Type != typeEnum)
                        {
                            continue;
                        }

                        if(parameter.Type == StrategyParameterType.Int)
                        {
                            if(string.IsNullOrEmpty(value))
                            {
                                continue;
                            }
                            try
                            {
                                ((StrategyParameterInt)parameter).ValueInt = Convert.ToInt32(value);
                            }
                            catch
                            {
                                // ignore
                            }
                        }
                        else if (parameter.Type == StrategyParameterType.String)
                        {
                            try
                            {
                                ((StrategyParameterString)parameter).ValueString = value;
                            }
                            catch
                            {
                                // ignore
                            }
                        }
                        else if (parameter.Type == StrategyParameterType.Decimal)
                        {
                            if (string.IsNullOrEmpty(value))
                            {
                                continue;
                            }
                            try
                            {
                                ((StrategyParameterDecimal)parameter).ValueDecimal = value.ToDecimal();
                            }
                            catch
                            {
                                // ignore
                            }
                        }
                        else if (parameter.Type == StrategyParameterType.TimeOfDay)
                        {
                            if (string.IsNullOrEmpty(value))
                            {
                                continue;
                            }
                            try
                            {
                                TimeOfDay tD = new TimeOfDay();
                                tD.LoadFromString(value);

                                ((StrategyParameterTimeOfDay)parameter).Value = tD;
                            }
                            catch
                            {
                                // ignore
                            }
                        }
                        else if (parameter.Type == StrategyParameterType.CheckBox)
                        {
                            try
                            {
                                bool isChecked = Convert.ToBoolean(value);

                                if (isChecked)
                                {
                                    ((StrategyParameterCheckBox)parameter).CheckState = CheckState.Checked;
                                }
                                else
                                {
                                    ((StrategyParameterCheckBox)parameter).CheckState = CheckState.Unchecked;
                                }
                            }
                            catch
                            {
                                // ignore
                            }
                        }
                        else if (parameter.Type == StrategyParameterType.Bool)
                        {
                            ((StrategyParameterBool)parameter).ValueBool = Convert.ToBoolean(value);
                        }
                    }


                }
            }
            catch (Exception error)
            {
                _client.SendNewLogMessage(error.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion
    }
}
