/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Forms;

namespace OsEngine.Market.Servers.Optimizer
{
    public partial class OptimizerMarginRatesEditUi
    {
        public OptimizerMarginRatesEditUi(OptimizerDataStorage server, List<ListTableSumm> list, int year)
        {
            InitializeComponent();

            _year = year;
            _listTableSumm = list;
            _server = server;

            Title = OsLocalization.ConvertToLocString($"Eng:Margin rates {year} year_Ru:Ставки маржи {year} год_");
            ButtonAccept.Content = OsLocalization.ConvertToLocString("Eng:Accept_Ru:Принять_");
            ButtonAccept.Click += ButtonAccept_Click;

            CreateGrid();
            FillTableSumm();

            Closed += OptimizerMarginRatesEditUi_Closed;

            Activate();
            Focus();
            Show();
        }

        private DataGridView _dgv;
        private List<ListTableSumm> _listTableSumm;
        private int _year;
        private OptimizerDataStorage _server;

        private void CreateGrid()
        {
            try
            {
                _dgv = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.AllCells);

                _dgv.Dock = DockStyle.Fill;
                _dgv.ScrollBars = ScrollBars.Vertical;
                _dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;

                _dgv.ColumnCount = 3;
                _dgv.RowCount = 0;

                _dgv.Columns[0].HeaderText = OsLocalization.ConvertToLocString("Eng:Amount margin_" + "Ru:Сумма непокрытой позиции_");
                _dgv.Columns[1].HeaderText = OsLocalization.ConvertToLocString("Eng:Type rate_" + "Ru:Вид ставки_");
                _dgv.Columns[2].HeaderText = OsLocalization.ConvertToLocString("Eng:Rate_" + "Ru:Ставка_");

                _dgv.Columns[0].ReadOnly = true;
                _dgv.Columns[1].ReadOnly = true;

                foreach (DataGridViewColumn column in _dgv.Columns)
                {
                    column.SortMode = DataGridViewColumnSortMode.NotSortable;
                    column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                }

                _dgv.Columns[0].FillWeight = 50;
                _dgv.Columns[1].FillWeight = 25;
                _dgv.Columns[2].FillWeight = 25;

                _dgv.CellValueChanged += _dgv_CellValueChanged;
                _dgv.DataError += _dgv_DataError;

                HostTable.Child = _dgv;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void FillTableSumm()
        {
            try
            {
                for (int i = 0; i < _listTableSumm.Count; i++)
                {
                    DataGridViewRow row = new DataGridViewRow();

                    if (i == _listTableSumm.Count - 1)
                    {
                        row.Cells.Add(new DataGridViewTextBoxCell() { Value = $"более {_listTableSumm[i - 1].Summ.ToString("N0", new CultureInfo("ru-RU"))} Р" });
                    }
                    else
                    {
                        row.Cells.Add(new DataGridViewTextBoxCell() { Value = $"до {_listTableSumm[i].Summ.ToString("N0", new CultureInfo("ru-RU"))} Р" });
                    }

                    row.Cells.Add(new DataGridViewTextBoxCell() { Value = _listTableSumm[i].TypeValue });
                    row.Cells.Add(new DataGridViewTextBoxCell() { Value = _listTableSumm[i].Rate });

                    _dgv.Rows.Add(row);
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _dgv_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                if (e.RowIndex < 0
                    || e.RowIndex >= _listTableSumm.Count)
                {
                    return;
                }

                if (e.ColumnIndex == 2)
                {
                    ListTableSumm list = new ListTableSumm();

                    list.Summ = _listTableSumm[e.RowIndex].Summ;
                    list.TypeValue = _dgv.Rows[e.RowIndex].Cells[1].Value?.ToString() == TypeValueTableSumm.Absolute.ToString() ? TypeValueTableSumm.Absolute : TypeValueTableSumm.Percent;
                    list.Rate = _dgv.Rows[e.RowIndex].Cells[2].Value?.ToString().ToDecimal() ?? 0;

                    _listTableSumm[e.RowIndex] = list;
                    _server.SetMarginTableSumm(_year, _listTableSumm);
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _dgv_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            ServerMaster.SendNewLogMessage(e.ToString(), LogMessageType.Error);
        }

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _server.SetMarginTableSumm(_year, _listTableSumm);
                Close();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void OptimizerMarginRatesEditUi_Closed(object sender, EventArgs e)
        {
            try
            {
                Closed -= OptimizerMarginRatesEditUi_Closed;
                ButtonAccept.Click -= ButtonAccept_Click;

                if (_dgv != null)
                {
                    _dgv.CellValueChanged -= _dgv_CellValueChanged;
                    _dgv.DataError -= _dgv_DataError;

                    DataGridFactory.ClearLinks(_dgv);

                    _dgv.Rows.Clear();
                    _dgv.Columns.Clear();
                    _dgv.DataSource = null;
                    _dgv.Dispose();
                    _dgv = null;
                }

                HostTable.Child = null;

                _listTableSumm = null;
                _server = null;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }
    }
}
