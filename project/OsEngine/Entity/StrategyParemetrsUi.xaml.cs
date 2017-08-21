
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace OsEngine.Entity
{
    /// <summary>
    /// Логика взаимодействия для ParemetrsUi.xaml
    /// </summary>
    public partial class ParemetrsUi 
    {

        private List<StrategyParameter> _parameters;

        public ParemetrsUi(List<StrategyParameter> parameters)
        {
            InitializeComponent();
            _parameters = parameters;

            CreateTable();
            PaintTable();
        }

        private DataGridView _grid;

        private void CreateTable()
        {
            _grid = new DataGridView();

            _grid.AllowUserToOrderColumns = true;
            _grid.AllowUserToResizeRows = true;
            _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToAddRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            _grid.MultiSelect = false;

            DataGridViewCellStyle style = new DataGridViewCellStyle();
            style.Alignment = DataGridViewContentAlignment.TopLeft;
            style.WrapMode = DataGridViewTriState.True;
            _grid.DefaultCellStyle = style;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = style;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = @"Название параметра";
            column0.ReadOnly = true;
            column0.Width = 150;

            _grid.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = @"Текущее значение";
            column1.ReadOnly = false;
            column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column1);

            _grid.Rows.Add(null, null);

            HostParametrs.Child = _grid;
        }

        private void PaintTable()
        {
            _grid.Rows.Clear();

            for (int i = 0; i < _parameters.Count; i++)
            {
                DataGridViewRow row = new DataGridViewRow();

                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[0].Value = _parameters[i].Name;

                DataGridViewComboBoxCell cell = new DataGridViewComboBoxCell();

                if (_parameters[i].Type == StrategyParameterType.Bool)
                {
                    cell.Items.Add("False");
                    cell.Items.Add("True");
                    cell.Value = _parameters[i].ValueBool.ToString();
                }
                else if (_parameters[i].Type == StrategyParameterType.String)
                {
                    for (int i2 = 0; i2 < _parameters[i].ValuesString.Count; i2++)
                    {
                        cell.Items.Add(_parameters[i].ValuesString[i2]);
                    }
                    cell.Value = _parameters[i].ValueString;
                }
                else if (_parameters[i].Type == StrategyParameterType.Int)
                {
                    cell.Items.Add(_parameters[i].ValueInt.ToString());
                    int valueCurrent = _parameters[i].ValueIntStart;
                    for (int i2 = 0; valueCurrent < _parameters[i].ValueIntStop; i2++)
                    {
                        cell.Items.Add(valueCurrent.ToString());
                        valueCurrent += _parameters[i].ValueIntStep;
                    }
                    cell.Items.Add(_parameters[i].ValueIntStop.ToString());
                    cell.Value = _parameters[i].ValueInt.ToString();
                }
                else if (_parameters[i].Type == StrategyParameterType.Decimal)
                {
                    cell.Items.Add(_parameters[i].ValueDecimal.ToString());
                    decimal valueCurrent = _parameters[i].ValueDecimalStart;
                    for (int i2 = 0; valueCurrent < _parameters[i].ValueDecimalStop; i2++)
                    {
                        cell.Items.Add(valueCurrent.ToString());
                        valueCurrent += _parameters[i].ValueDecimalStep;
                    }
                    cell.Items.Add(_parameters[i].ValueDecimalStop.ToString());
                    cell.Value = _parameters[i].ValueDecimal.ToString();
                }
                row.Cells.Add(cell);

                _grid.Rows.Add(row);
            }
        }

        private void ButtonAccept_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            for (int i = 0; i < _parameters.Count; i++)
            {
                if (_parameters[i].Type == StrategyParameterType.String)
                {
                    _parameters[i].ValueString = _grid.Rows[i].Cells[1].Value.ToString();
                }
                else if (_parameters[i].Type == StrategyParameterType.Int)
                {
                    _parameters[i].ValueInt = Convert.ToInt32(_grid.Rows[i].Cells[1].Value.ToString());
                }
                else if (_parameters[i].Type == StrategyParameterType.Bool)
                {
                    _parameters[i].ValueBool = Convert.ToBoolean(_grid.Rows[i].Cells[1].Value.ToString());
                }
                else if (_parameters[i].Type == StrategyParameterType.Decimal)
                {
                    _parameters[i].ValueDecimal = Convert.ToDecimal(_grid.Rows[i].Cells[1].Value.ToString());
                }
            }

            Close();
        }
    }
}
