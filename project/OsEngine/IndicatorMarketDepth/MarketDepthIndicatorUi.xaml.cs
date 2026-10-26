/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using System;
using System.Windows;
using System.Windows.Forms;

namespace OsEngine.IndicatorMarketDepth
{
    public partial class MarketDepthIndicatorUi : Window
    {
        private AMarketDepth _indicator;

        private DataGridView _gridParam;

        private DataGridView _gridVisual;

        public bool IsAccepted;

        public MarketDepthIndicatorUi(AMarketDepth indicator)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            _indicator = indicator;

            CreateGridParam();
            UpdateGridParam();

            CreateGridVisual();
            UpdateGridVisual();

            Title = indicator.GetType().Name + " " + OsLocalization.ConvertToLocString("Eng:settings_Ru:настройки_");
            TabItemParam.Header = OsLocalization.ConvertToLocString("Eng:Parameters_Ru:Параметры_");
            TabItemVisual.Header = OsLocalization.ConvertToLocString("Eng:Visual_Ru:Визуал_");
            ButtonAccept.Content = OsLocalization.Entity.ButtonAccept;
            ButtonCancel.Content = OsLocalization.Entity.ButtonCancel1;
            ButtonDefault.Content = OsLocalization.ConvertToLocString("Eng:Default_Ru:По умолчанию_");

            Closed += MarketDepthIndicatorUi_Closed;

            this.Activate();
            this.Focus();
        }

        private void MarketDepthIndicatorUi_Closed(object sender, EventArgs e)
        {
            try
            {
                Closed -= MarketDepthIndicatorUi_Closed;

                _gridParam.DataError -= _gridParam_DataError;
                HostParameters.Child = null;
                DataGridFactory.ClearLinks(_gridParam);
                _gridParam.Rows.Clear();
                _gridParam.Columns.Clear();
                _gridParam.DataSource = null;
                _gridParam.Dispose();
                _gridParam = null;
                HostParameters.Dispose();

                _gridVisual.Click -= _gridVisual_Click;
                _gridVisual.DataError -= _gridParam_DataError;
                HostVisual.Child = null;
                DataGridFactory.ClearLinks(_gridVisual);
                _gridVisual.Rows.Clear();
                _gridVisual.Columns.Clear();
                _gridVisual.DataSource = null;
                _gridVisual.Dispose();
                _gridVisual = null;
                HostVisual.Dispose();

                _indicator = null;
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _gridParam_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            ServerMaster.SendNewLogMessage(e.ToString(), LogMessageType.Error);
        }

        private void CreateGridParam()
        {
            _gridParam = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
                DataGridViewAutoSizeRowsMode.AllCells);

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _gridParam.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = OsLocalization.ConvertToLocString("Eng:Param name_Ru:Параметр_");
            column0.ReadOnly = true;
            column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridParam.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = OsLocalization.ConvertToLocString("Eng:Value_Ru:Значение_");
            column1.ReadOnly = false;
            column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridParam.Columns.Add(column1);

            _gridParam.Rows.Add(null, null);
            HostParameters.Child = _gridParam;
            _gridParam.DataError += _gridParam_DataError;
        }

        private void UpdateGridParam()
        {
            _gridParam.Rows.Clear();

            for (int i = 0; i < _indicator.Parameters.Count; i++)
            {
                IParameterMarketDepth parameter = _indicator.Parameters[i];
                DataGridViewRow row = new DataGridViewRow();

                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[0].Value = parameter.Name;

                if (parameter.Type == MarketDepthParameterType.Bool)
                {
                    ParameterMarketDepthBool p = (ParameterMarketDepthBool)parameter;
                    DataGridViewComboBoxCell cell = new DataGridViewComboBoxCell();
                    cell.Items.Add("False");
                    cell.Items.Add("True");
                    cell.Value = p.ValueBool.ToString();
                    row.Cells.Add(cell);
                }
                else if (parameter.Type == MarketDepthParameterType.String)
                {
                    ParameterMarketDepthString p = (ParameterMarketDepthString)parameter;

                    if (p.ValuesString.Count > 1)
                    {
                        DataGridViewComboBoxCell cell = new DataGridViewComboBoxCell();

                        for (int i2 = 0; i2 < p.ValuesString.Count; i2++)
                        {
                            cell.Items.Add(p.ValuesString[i2]);
                        }

                        cell.Value = p.ValueString;
                        row.Cells.Add(cell);
                    }
                    else
                    {
                        DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();
                        cell.Value = p.ValueString;
                        row.Cells.Add(cell);
                    }
                }
                else if (parameter.Type == MarketDepthParameterType.Int)
                {
                    ParameterMarketDepthInt p = (ParameterMarketDepthInt)parameter;
                    DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();
                    cell.Value = p.ValueInt.ToString();
                    row.Cells.Add(cell);
                }
                else if (parameter.Type == MarketDepthParameterType.Decimal)
                {
                    ParameterMarketDepthDecimal p = (ParameterMarketDepthDecimal)parameter;
                    DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();
                    cell.Value = p.ValueDecimal.ToString();
                    row.Cells.Add(cell);
                }

                _gridParam.Rows.Add(row);
            }
        }

        private void SaveParam()
        {
            for (int i = 0; i < _indicator.Parameters.Count; i++)
            {
                IParameterMarketDepth parameter = _indicator.Parameters[i];
                string value = _gridParam.Rows[i].Cells[1].EditedFormattedValue.ToString();

                if (parameter.Type == MarketDepthParameterType.String)
                {
                    ((ParameterMarketDepthString)parameter).ValueString = value;
                }
                else if (parameter.Type == MarketDepthParameterType.Int)
                {
                    ((ParameterMarketDepthInt)parameter).ValueInt = Convert.ToInt32(value);
                }
                else if (parameter.Type == MarketDepthParameterType.Bool)
                {
                    ((ParameterMarketDepthBool)parameter).ValueBool = Convert.ToBoolean(value);
                }
                else if (parameter.Type == MarketDepthParameterType.Decimal)
                {
                    ((ParameterMarketDepthDecimal)parameter).ValueDecimal = value.ToDecimal();
                }
            }
        }

        private void CreateGridVisual()
        {
            _gridVisual = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect,
                DataGridViewAutoSizeRowsMode.AllCells);

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _gridVisual.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = OsLocalization.ConvertToLocString("Eng:Series_Ru:Серия_");
            column0.ReadOnly = true;
            column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridVisual.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = OsLocalization.ConvertToLocString("Eng:Color_Ru:Цвет_");
            column1.ReadOnly = false;
            column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridVisual.Columns.Add(column1);

            DataGridViewColumn column2 = new DataGridViewColumn();
            column2.CellTemplate = cell0;
            column2.HeaderText = OsLocalization.ConvertToLocString("Eng:Type_Ru:Тип_");
            column2.ReadOnly = false;
            column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridVisual.Columns.Add(column2);

            DataGridViewColumn column3 = new DataGridViewColumn();
            column3.CellTemplate = cell0;
            column3.HeaderText = OsLocalization.ConvertToLocString("Eng:Is paint_Ru:Рисовать_");
            column3.ReadOnly = false;
            column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridVisual.Columns.Add(column3);

            DataGridViewColumn column4 = new DataGridViewColumn();
            column4.CellTemplate = cell0;
            column4.HeaderText = OsLocalization.ConvertToLocString("Eng:Line width_Ru:Толщина_");
            column4.ReadOnly = false;
            column4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridVisual.Columns.Add(column4);

            DataGridViewColumn column5 = new DataGridViewColumn();
            column5.CellTemplate = cell0;
            column5.HeaderText = OsLocalization.ConvertToLocString("Eng:Zero gap_Ru:Ноль разрыв_");
            column5.ReadOnly = false;
            column5.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridVisual.Columns.Add(column5);

            _gridVisual.Rows.Add(null, null);
            HostVisual.Child = _gridVisual;
            _gridVisual.Click += _gridVisual_Click;
            _gridVisual.DataError += _gridParam_DataError;
        }

        private void UpdateGridVisual()
        {
            _gridVisual.Rows.Clear();

            for (int i = 0; i < _indicator.DataSeries.Count; i++)
            {
                IndicatorDataSeries series = _indicator.DataSeries[i];
                DataGridViewRow row = new DataGridViewRow();

                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[0].Value = series.Name;

                DataGridViewButtonCell buttonColor = new DataGridViewButtonCell();
                row.Cells.Add(buttonColor);
                buttonColor.Value = "";
                buttonColor.Style.BackColor = series.Color;
                buttonColor.Style.SelectionBackColor = series.Color;
                buttonColor.Style.ForeColor = series.Color;
                buttonColor.Style.SelectionForeColor = series.Color;

                DataGridViewComboBoxCell cell = new DataGridViewComboBoxCell();
                cell.Items.Add(IndicatorChartPaintType.Line.ToString());
                cell.Items.Add(IndicatorChartPaintType.Point.ToString());
                cell.Items.Add(IndicatorChartPaintType.Column.ToString());
                cell.Value = series.ChartPaintType.ToString();
                row.Cells.Add(cell);

                DataGridViewComboBoxCell cell2 = new DataGridViewComboBoxCell();
                cell2.Items.Add("False");
                cell2.Items.Add("True");
                cell2.Value = series.IsPaint.ToString();
                row.Cells.Add(cell2);

                DataGridViewTextBoxCell cell3 = new DataGridViewTextBoxCell();
                cell3.Value = series.LineWidth.ToString();
                row.Cells.Add(cell3);

                DataGridViewComboBoxCell cell4 = new DataGridViewComboBoxCell();
                cell4.Items.Add("False");
                cell4.Items.Add("True");
                cell4.Value = series.ZeroIsGap.ToString();
                row.Cells.Add(cell4);

                _gridVisual.Rows.Add(row);
            }
        }

        private void _gridVisual_Click(object sender, EventArgs e)
        {
            try
            {
                if (_gridVisual.SelectedCells.Count == 0)
                {
                    return;
                }

                int rowIndex = _gridVisual.SelectedCells[0].RowIndex;
                int cellIndex = _gridVisual.SelectedCells[0].ColumnIndex;

                if (cellIndex == 1)
                {
                    ColorCustomDialog dialog = new ColorCustomDialog();
                    dialog.Color = _gridVisual.Rows[rowIndex].Cells[1].Style.ForeColor;
                    dialog.ShowDialog();

                    _gridVisual.Rows[rowIndex].Cells[1].Style.ForeColor = dialog.Color;
                    _gridVisual.Rows[rowIndex].Cells[1].Style.SelectionForeColor = dialog.Color;
                    _gridVisual.Rows[rowIndex].Cells[1].Style.BackColor = dialog.Color;
                    _gridVisual.Rows[rowIndex].Cells[1].Style.SelectionBackColor = dialog.Color;
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void SaveVisual()
        {
            for (int i = 0; i < _indicator.DataSeries.Count; i++)
            {
                _indicator.DataSeries[i].Color = _gridVisual.Rows[i].Cells[1].Style.ForeColor;
                Enum.TryParse(_gridVisual.Rows[i].Cells[2].Value.ToString(), out _indicator.DataSeries[i].ChartPaintType);
                _indicator.DataSeries[i].IsPaint = Convert.ToBoolean(_gridVisual.Rows[i].Cells[3].Value.ToString());

                int lineWidth = 1;

                if (int.TryParse(_gridVisual.Rows[i].Cells[4].Value?.ToString(), out int parsedLineWidth))
                {
                    lineWidth = parsedLineWidth;
                }

                if (lineWidth < 1)
                {
                    lineWidth = 1;
                }

                _indicator.DataSeries[i].LineWidth = lineWidth;

                bool zeroIsGap = false;

                if (bool.TryParse(_gridVisual.Rows[i].Cells[5].Value?.ToString(), out bool parsedZeroIsGap))
                {
                    zeroIsGap = parsedZeroIsGap;
                }

                _indicator.DataSeries[i].ZeroIsGap = zeroIsGap;
            }
        }

        private void ButtonDefault_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                for (int i = 0; i < _indicator.Parameters.Count; i++)
                {
                    _indicator.Parameters[i].DoDefault();
                }

                UpdateGridParam();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonDescription_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string description = _indicator != null ? _indicator.Description : "";

                if (string.IsNullOrEmpty(description))
                {
                    description = OsLocalization.ConvertToLocString("Eng:No description_Ru:Нет описания_");
                }

                CustomMessageBoxUi ui = new CustomMessageBoxUi(description);
                ui.ShowDialog();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveParam();
                SaveVisual();

                _indicator.Save();
                _indicator.Recalculate();

                IsAccepted = true;
                Close();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Close();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }
    }
}
