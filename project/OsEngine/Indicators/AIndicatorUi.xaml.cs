using System;
using System.Windows;
using System.Windows.Forms;
using OsEngine.Entity;
using OsEngine.Language;
using MessageBox = System.Windows.MessageBox;

namespace OsEngine.Indicators
{
    /// <summary>
    /// Interaction logic for AIndicatorUi.xaml
    /// </summary>
    public partial class AIndicatorUi : Window
    {
        private Aindicator _indicator;

        public AIndicatorUi(Aindicator indicator)
        {
            InitializeComponent();
            Title = indicator.GetType().Name + " " + OsLocalization.Charts.Label1;
            _indicator = indicator;

            CreateGridParam();
            UpdateGridParam();

            CreateGridVisual();
            UpdateGridVisual();

            CreateGridIndicators();
            UpdateGridIndicators();

            TabItemParam.Header = OsLocalization.Charts.Label2;
            TabItemVisual.Header = OsLocalization.Charts.Label3;
            TabItemIncludeInd.Header = OsLocalization.Charts.Label4;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;
            ButtonDefault.Content = OsLocalization.Charts.Label5;

        }

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            SaveParam();
            SaveVisual();

            IsAccepted = true;
            Close();
        }

        public bool IsAccepted;

        // parameters

        private DataGridView _gridParam;

        private void CreateGridParam()
        {
            _gridParam = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
                DataGridViewAutoSizeRowsMode.None);

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _gridParam.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = "Param Name";
            column0.ReadOnly = true;
            column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _gridParam.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = "Value";
            column1.ReadOnly = false;
            column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridParam.Columns.Add(column1);

            _gridParam.Rows.Add(null, null);

            HostParameters.Child = _gridParam;

        }

        private void UpdateGridParam()
        {
            _gridParam.Rows.Clear();

            for (int i = 0; i < _indicator.Parameters.Count; i++)
            {
                DataGridViewRow row = new DataGridViewRow();

                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[0].Value = _indicator.Parameters[i].Name;

                if (_indicator.Parameters[i].Type == IndicatorParameterType.Bool)
                {
                    DataGridViewComboBoxCell cell = new DataGridViewComboBoxCell();

                    cell.Items.Add("False");
                    cell.Items.Add("True");
                    cell.Value = ((IndicatorParameterBool)_indicator.Parameters[i]).ValueBool.ToString();
                    row.Cells.Add(cell);
                }
                else if (_indicator.Parameters[i].Type == IndicatorParameterType.String)
                {
                    IndicatorParameterString param = (IndicatorParameterString)_indicator.Parameters[i];

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
                    else
                    {
                        DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();
                        cell.Value = param.ValueString.ToString();
                        row.Cells.Add(cell);
                    }
                }
                else if (_indicator.Parameters[i].Type == IndicatorParameterType.Int)
                {
                    DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();

                    IndicatorParameterInt param = (IndicatorParameterInt)_indicator.Parameters[i];

                    cell.Value = param.ValueInt.ToString();
                    row.Cells.Add(cell);
                }
                else if (_indicator.Parameters[i].Type == IndicatorParameterType.Decimal)
                {
                    DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();

                    IndicatorParameterDecimal param = (IndicatorParameterDecimal)_indicator.Parameters[i];

                    cell.Value = param.ValueDecimal.ToString();
                    row.Cells.Add(cell);
                }

                _gridParam.Rows.Add(row);
            }
        }

        private void SaveParam()
        {
            for (int i = 0; i < _indicator.Parameters.Count; i++)
            {
                try
                {
                    if (_indicator.Parameters[i].Type == IndicatorParameterType.String)
                    {
                        ((IndicatorParameterString)_indicator.Parameters[i]).ValueString = _gridParam.Rows[i].Cells[1].EditedFormattedValue.ToString();
                    }
                    else if (_indicator.Parameters[i].Type == IndicatorParameterType.Int)
                    {
                        ((IndicatorParameterInt)_indicator.Parameters[i]).ValueInt = Convert.ToInt32(_gridParam.Rows[i].Cells[1].EditedFormattedValue.ToString());
                    }
                    else if (_indicator.Parameters[i].Type == IndicatorParameterType.Bool)
                    {
                        ((IndicatorParameterBool)_indicator.Parameters[i]).ValueBool = Convert.ToBoolean(_gridParam.Rows[i].Cells[1].EditedFormattedValue.ToString());
                    }
                    else if (_indicator.Parameters[i].Type == IndicatorParameterType.Decimal)
                    {
                        ((IndicatorParameterDecimal)_indicator.Parameters[i]).ValueDecimal = _gridParam.Rows[i].Cells[1].EditedFormattedValue.ToString().ToDecimal();
                    }
                }
                catch
                {
                    System.Windows.Forms.MessageBox.Show("Error. One of field have note valid param");
                    return;
                }

            }
        }

        // visual

        private DataGridView _gridVisual;

        private void CreateGridVisual()
        {
            //_series = CreateSeries("Ma", Color.DodgerBlue, IndicatorChartPaintType.Line, false);

            _gridVisual = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect,
                DataGridViewAutoSizeRowsMode.None);

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _gridVisual.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = "Name";
            column0.ReadOnly = true;
            column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _gridVisual.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = "Color";
            column1.ReadOnly = false;
            column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridVisual.Columns.Add(column1);

            DataGridViewColumn column2 = new DataGridViewColumn();
            column2.CellTemplate = cell0;
            column2.HeaderText = "Type";
            column2.ReadOnly = false;
            column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridVisual.Columns.Add(column2);

            DataGridViewColumn column3 = new DataGridViewColumn();
            column3.CellTemplate = cell0;
            column3.HeaderText = "Is Paint";
            column3.ReadOnly = false;
            column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridVisual.Columns.Add(column3);

            _gridVisual.Rows.Add(null, null);

            HostVisual.Child = _gridVisual;
            _gridVisual.Click += _gridVisual_Click;
        }

        private void UpdateGridVisual()
        {
            _gridVisual.Rows.Clear();

            for (int i = 0; i < _indicator.DataSeries.Count; i++)
            {
                DataGridViewRow row = new DataGridViewRow();

                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[0].Value = _indicator.DataSeries[i].Name;

                //_series = CreateSeries("Ma", Color.DodgerBlue, IndicatorChartPaintType.Line, false);

                DataGridViewButtonCell buttonColor = new DataGridViewButtonCell();
                row.Cells.Add(buttonColor);
                buttonColor.Value = "Color";
                buttonColor.Style.ForeColor = _indicator.DataSeries[i].Color;
                buttonColor.Style.SelectionForeColor = _indicator.DataSeries[i].Color;

                DataGridViewComboBoxCell cell = new DataGridViewComboBoxCell();
                cell.Items.Add(IndicatorChartPaintType.Line.ToString());
                cell.Items.Add(IndicatorChartPaintType.Point.ToString());
                cell.Items.Add(IndicatorChartPaintType.Column.ToString());
                cell.Value = _indicator.DataSeries[i].ChartPaintType.ToString();
                row.Cells.Add(cell);

                DataGridViewComboBoxCell cell2 = new DataGridViewComboBoxCell();

                cell2.Items.Add("False");
                cell2.Items.Add("True");
                cell2.Value = _indicator.DataSeries[i].IsPaint.ToString();
                row.Cells.Add(cell2);

                _gridVisual.Rows.Add(row);
            }
        }

        private void _gridVisual_Click(object sender, EventArgs e)
        {
            try
            {
                int rowIndex = _gridVisual.SelectedCells[0].RowIndex;
                int cellIndex = _gridVisual.SelectedCells[0].ColumnIndex;

                if (cellIndex == 1)
                {
                    ColorDialog dialog = new ColorDialog();
                    dialog.Color = _gridVisual.Rows[rowIndex].Cells[1].Style.ForeColor;
                    dialog.ShowDialog();

                    _gridVisual.Rows[rowIndex].Cells[1].Style.ForeColor = dialog.Color;
                    _gridVisual.Rows[rowIndex].Cells[1].Style.SelectionForeColor = dialog.Color;
                }
            }
            catch (Exception exception)
            {

            }
        }

        private void SaveVisual()
        {
            for (int i = 0; i < _indicator.DataSeries.Count; i++)
            {
                try
                {
                    _indicator.DataSeries[i].Color = _gridVisual.Rows[i].Cells[1].Style.ForeColor;
                    Enum.TryParse(_gridVisual.Rows[i].Cells[2].Value.ToString(), out _indicator.DataSeries[i].ChartPaintType);
                    _indicator.DataSeries[i].IsPaint = Convert.ToBoolean(_gridVisual.Rows[i].Cells[3].Value.ToString());
                }
                catch
                {
                    System.Windows.Forms.MessageBox.Show("Error. One of field have note valid param");
                    return;
                }

            }
        }

        // include indicators

        private DataGridView _gridIndicators;

        private void CreateGridIndicators()
        {
            //_series = CreateSeries("Ma", Color.DodgerBlue, IndicatorChartPaintType.Line, false);

            _gridIndicators = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect,
                DataGridViewAutoSizeRowsMode.None);

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _gridIndicators.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = "Name";
            column0.ReadOnly = true;
            column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _gridIndicators.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = "Type";
            column1.ReadOnly = true;
            column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridIndicators.Columns.Add(column1);

            DataGridViewColumn column2 = new DataGridViewColumn();
            column2.CellTemplate = cell0;
            column2.ReadOnly = false;
            column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridIndicators.Columns.Add(column2);

            _gridIndicators.Rows.Add(null, null);

            HostIndicators.Child = _gridIndicators;
            _gridIndicators.Click += _gridIndicators_Click;
        }

        private void UpdateGridIndicators()
        {
            _gridIndicators.Rows.Clear();

            for (int i = 0; i < _indicator.IncludeIndicators.Count; i++)
            {
                DataGridViewRow row = new DataGridViewRow();

                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[0].Value = _indicator.IncludeIndicatorsName[i];

                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[1].Value = _indicator.IncludeIndicators[i].GetType().Name;

                DataGridViewButtonCell buttonColor = new DataGridViewButtonCell();
                row.Cells.Add(buttonColor);
                buttonColor.Value = "Settings";

                _gridIndicators.Rows.Add(row);
            }
        }

        private void _gridIndicators_Click(object sender, EventArgs e)
        {
            try
            {
                int rowIndex = _gridIndicators.SelectedCells[0].RowIndex;
                int cellIndex = _gridIndicators.SelectedCells[0].ColumnIndex;

                if (cellIndex == 2)
                {
                    _indicator.IncludeIndicators[rowIndex].ShowDialog();
                    _indicator.Reload();
                    UpdateGridParam();
                }
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }
        }

        private void ButtonDefault_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < _indicator.Parameters.Count; i++)
            {
                _indicator.Parameters[i].DoDefault();
            }
            UpdateGridParam();
        }
    }
}
