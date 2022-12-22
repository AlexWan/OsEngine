/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;

namespace OsEngine.Entity
{
    /// <summary>
    /// Interaction logic for ParemetrsUi.xaml
    /// Логика взаимодействия для ParemetrsUi.xaml
    /// </summary>
    public partial class ParemetrsUi
    {
        private List<IIStrategyParameter> _parameters;

        public ParemetrsUi(List<IIStrategyParameter> parameters, ParamGuiSettings settings)
        {
            InitializeComponent();

            Height = (double)settings.Height;
            Width = (double)settings.Width;

            _parameters = parameters;

            ButtonAccept.Content = OsLocalization.Entity.ButtonAccept;

            if(string.IsNullOrEmpty(settings.Title))
            {
                Title = OsLocalization.Entity.TitleParametersUi;
            }
            else
            {
                Title = settings.Title;
            }
            

            List<List<IIStrategyParameter>> sorted = GetParamSortedByTabName();

            for(int i = 0;i < sorted.Count;i++)
            {
                if(sorted[i][0].TabName == null)
                {
                    CreateTab(sorted[i], settings.FirstTabLabel);
                }
                else
                {
                    CreateTab(sorted[i], sorted[i][0].TabName);
                }
            }
            
            for(int i = 0;i < settings.CustomTabs.Count;i++)
            {
                CreateCustomTab(settings.CustomTabs[i]);
            }

            this.Closed += ParemetrsUi_Closed;

            this.Activate();
            this.Focus();
        }

        private void ParemetrsUi_Closed(object sender, EventArgs e)
        {
            this.Closed -= ParemetrsUi_Closed;
            _parameters = null;

            if(_tabs != null)
            {
                for (int i = 0;i < _tabs.Count; i++)
                {
                    _tabs[i].Dispose();
                }
                _tabs.Clear();
                _tabs = null;
            }


        }

        List<List<IIStrategyParameter>> GetParamSortedByTabName()
        {
            List<List<IIStrategyParameter>> sorted = new List<List<IIStrategyParameter>>();

            for(int i = 0;i < _parameters.Count;i++)
            {
                List<IIStrategyParameter> myList = sorted.Find(s => s[0].TabName == _parameters[i].TabName);

                if(myList != null)
                {
                    myList.Add(_parameters[i]);
                }
                else
                {
                    List<IIStrategyParameter> newItem = new List<IIStrategyParameter>();
                    newItem.Add(_parameters[i]);
                    sorted.Add(newItem);
                }
            }

            for(int i = 0;i < sorted.Count;i++)
            {// переставляем принудительно параметры без имени вкладки в первый слот вкладок
                if(sorted[i][0].TabName == null && i != 0)
                {
                    List<IIStrategyParameter> par = sorted[i];
                    sorted.RemoveAt(i);
                    sorted.Insert(0, par);
                    break;
                }
            }

            return sorted;
        }

        private void CreateCustomTab(CustomTabToParametersUi tab)
        {
            TabItem item = new TabItem();
            item.Header = tab.Label;
            item.Content = tab.GridToPaint;

            TabControlSettings.Items.Add(item);
        }

        private void CreateTab(List<IIStrategyParameter> par, string tabName)
        {
            ParamTabPainter painter = new ParamTabPainter(par, tabName, TabControlSettings);
            _tabs.Add(painter);
        }

        List<ParamTabPainter> _tabs = new List<ParamTabPainter>();

        private void ButtonAccept_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                _tabs[i].Save();
            }

            Close();
        }
    }

    public class ParamTabPainter
    {
        public ParamTabPainter(List<IIStrategyParameter> parameters, string tabName, System.Windows.Controls.TabControl tabControl)
        {
            TabItem item = new TabItem();
            item.Header = tabName;
            _host = new WindowsFormsHost();

            item.Content = _host;

            tabControl.Items.Add(item);
            _parameters = parameters;
            _tabControl = tabControl;

            CreateTable();
            PaintTable();
        }

        public void Dispose()
        {
            if(_grid != null 
                && _grid.InvokeRequired)
            {
                _grid.Invoke(new Action(Dispose));
                return;
            }

             _parameters = null;

            if(_host != null)
            {
                _host.Child = null;
                _host = null;
            }

            if(_grid != null)
            {
                _grid.CellValueChanged -= _grid_CellValueChanged;
                _grid.CellClick -= _grid_Click;
                _grid.Rows.Clear();
                DataGridFactory.ClearLinks(_grid);
                _grid = null;
            }

        }

        List<IIStrategyParameter> _parameters;

        private WindowsFormsHost _host;

        private System.Windows.Controls.TabControl _tabControl;

        private DataGridView _grid;

        private void CreateTable()
        {
            _grid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect,
                DataGridViewAutoSizeRowsMode.None);
            _grid.ScrollBars = ScrollBars.Vertical;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _grid.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = OsLocalization.Entity.ParametersColumn1;
            column0.ReadOnly = true;
            column0.Width = 250;

            _grid.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = OsLocalization.Entity.ParametersColumn2;
            column1.ReadOnly = false;
            column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column1);

            _grid.Rows.Add(null, null);

            _grid.CellValueChanged += _grid_CellValueChanged;
            _grid.CellClick += _grid_Click;

            _host.Child = _grid;
        }

        private void PaintTable()
        {
            _grid.Rows.Clear();

            for (int i = 0; i < _parameters.Count; i++)
            {
                DataGridViewRow row = new DataGridViewRow();

                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[0].Value = _parameters[i].Name;

                if (_parameters[i].Type == StrategyParameterType.Bool)
                {
                    DataGridViewComboBoxCell cell = new DataGridViewComboBoxCell();

                    cell.Items.Add("False");
                    cell.Items.Add("True");
                    cell.Value = ((StrategyParameterBool)_parameters[i]).ValueBool.ToString();
                    row.Cells.Add(cell);
                }
                else if (_parameters[i].Type == StrategyParameterType.String)
                {
                    StrategyParameterString param = (StrategyParameterString)_parameters[i];

                    if (param.ValuesString.Count > 1)
                    {
                        DataGridViewComboBoxCell cell = new DataGridViewComboBoxCell();

                        for (int i2 = 0; i2 < param.ValuesString.Count; i2++)
                        {
                            cell.Items.Add(param.ValuesString[i2]);
                        }
                        cell.Value = param.ValueString;
                        row.Cells.Add(cell);
                    }
                    else if (param.ValuesString.Count == 1
                        || (param.ValuesString.Count == 0 && param.ValueString != null))
                    {
                        DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();
                        cell.Value = param.ValueString;
                        row.Cells.Add(cell);
                    }
                }
                else if (_parameters[i].Type == StrategyParameterType.Int)
                {
                    DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();

                    StrategyParameterInt param = (StrategyParameterInt)_parameters[i];

                    cell.Value = param.ValueInt.ToString();
                    row.Cells.Add(cell);
                }
                else if (_parameters[i].Type == StrategyParameterType.Decimal)
                {
                    DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();

                    StrategyParameterDecimal param = (StrategyParameterDecimal)_parameters[i];

                    cell.Value = param.ValueDecimal.ToString();
                    row.Cells.Add(cell);
                }
                else if (_parameters[i].Type == StrategyParameterType.TimeOfDay)
                {
                    DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();

                    StrategyParameterTimeOfDay param = (StrategyParameterTimeOfDay)_parameters[i];

                    cell.Value = param.Value.ToString();
                    row.Cells.Add(cell);
                }
                else if (_parameters[i].Type == StrategyParameterType.Button)
                {
                    DataGridViewButtonCell cell = new DataGridViewButtonCell();
                    row.Cells[0].Value = "";
                    cell.Value = _parameters[i].Name;
                    // StrategyParameterButton param = (StrategyParameterButton)_parameters[i];

                    row.Cells.Add(cell);
                }
                else if (_parameters[i].Type == StrategyParameterType.Label)
                {
                    DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();
                    StrategyParameterLabel param = (StrategyParameterLabel)_parameters[i];

                    if (param.RowHeight == 0)
                    {
                        param.RowHeight = 25;
                    }

                    row.Cells[0].Value = param.Label;
                    
                    row.Height = param.RowHeight;
                    
                    row.Cells[0].Style.Font = new System.Drawing.Font("Areal", param.TextHeight);
                    row.Cells[0].Style.ForeColor = param.Color;

                    row.Cells.Add(cell);
                    row.Cells[1].Value = param.Value;
                    row.Cells[1].Style.Font = new System.Drawing.Font("Areal", param.TextHeight);
                    row.Cells[1].Style.ForeColor = param.Color;

                    
                }

                _grid.Rows.Add(row);
            }
        }

        private void _grid_Click(object sender, EventArgs e)
        {
            int index = 0;

            try
            {
                int cellIndex = _grid.SelectedCells[0].ColumnIndex;

                if (cellIndex != 1)
                {
                    return;
                }

                index = _grid.SelectedCells[0].RowIndex;
                if (_parameters[index].Type != StrategyParameterType.Button)
                {
                    return;
                }
            }
            catch
            {
                return;
            }

            StrategyParameterButton param = (StrategyParameterButton)_parameters[index];
            param.Click();
        }

        private void _grid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            int index = e.RowIndex;

            if (_parameters[index].Type != StrategyParameterType.TimeOfDay)
            {
                return;
            }

            StrategyParameterTimeOfDay param = new StrategyParameterTimeOfDay("temp", 0, 0, 0, 0);

            try
            {
                string[] array = new[] { "", _grid.Rows[index].Cells[1].EditedFormattedValue.ToString() };
                param.LoadParamFromString(array);
            }
            catch (Exception exception)
            {

                _grid.Rows[index].Cells[1].Value = ((StrategyParameterTimeOfDay)_parameters[index]).Value.ToString();
            }
        }

        public void Save()
        {
            for (int i = 0; i < _parameters.Count; i++)
            {
                try
                {
                    if (_parameters[i].Type == StrategyParameterType.String)
                    {
                        ((StrategyParameterString)_parameters[i]).ValueString = _grid.Rows[i].Cells[1].EditedFormattedValue.ToString();
                    }
                    else if (_parameters[i].Type == StrategyParameterType.Int)
                    {
                        ((StrategyParameterInt)_parameters[i]).ValueInt = Convert.ToInt32(_grid.Rows[i].Cells[1].EditedFormattedValue.ToString());
                    }
                    else if (_parameters[i].Type == StrategyParameterType.Bool)
                    {
                        ((StrategyParameterBool)_parameters[i]).ValueBool = Convert.ToBoolean(_grid.Rows[i].Cells[1].EditedFormattedValue.ToString());
                    }
                    else if (_parameters[i].Type == StrategyParameterType.Decimal)
                    {
                        ((StrategyParameterDecimal)_parameters[i]).ValueDecimal = _grid.Rows[i].Cells[1].EditedFormattedValue.ToString().ToDecimal();
                    }
                    else if (_parameters[i].Type == StrategyParameterType.TimeOfDay)
                    {
                        string[] array = new[] { "", _grid.Rows[i].Cells[1].EditedFormattedValue.ToString() };
                        ((StrategyParameterTimeOfDay)_parameters[i]).LoadParamFromString(array);
                    }
                }
                catch
                {
                    MessageBox.Show("Error. One of field have not valid param");
                    return;
                }

            }
        }
    }
}
