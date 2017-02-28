/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Windows.Forms;

namespace OsEngine.Entity
{
    /// <summary>
    /// Логика взаимодействия для CumulativeDeltaTimeFrameUi.xaml
    /// </summary>
    public partial class CumulativeDeltaTimeFrameUi
    {
        private CumulativeDeltaPeriods _deltaPeriods;
        public CumulativeDeltaTimeFrameUi(CumulativeDeltaPeriods deltaPeriods)
        {
            InitializeComponent();
            _deltaPeriods = deltaPeriods;
            CreateTable();
            PaintPeriods();
        }

        /// <summary>
        /// таблица для прорисовки бумаг
        /// </summary>
        private DataGridView _grid;

        /// <summary>
        /// создать таблицу
        /// </summary>
        private void CreateTable()
        {
            _grid = new DataGridView();
            HostDeltaPeriods.Child = _grid;

            _grid.AllowUserToOrderColumns = false;
            _grid.AllowUserToResizeRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToAddRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;

            DataGridViewCellStyle style = new DataGridViewCellStyle();
            style.Alignment = DataGridViewContentAlignment.BottomRight;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = style;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = @"Начало периода";
            column0.ReadOnly = true;
            column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _grid.Columns.Add(column0);

            DataGridViewColumn column = new DataGridViewColumn();
            column.CellTemplate = cell0;
            column.HeaderText = @"Конец периода";
            column.ReadOnly = true;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _grid.Columns.Add(column);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = @"Отклонение для закрытия свечи";
            column1.ReadOnly = false;
            column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _grid.Columns.Add(column1);

            HostDeltaPeriods.Child.Show();
            HostDeltaPeriods.Child.Refresh();
            _grid.CellValueChanged += _grid_CellValueChanged;

        }

        /// <summary>
        /// прорисовать периоды на чарте
        /// </summary>
        private void PaintPeriods()
        {
            if (_grid.InvokeRequired)
            {
                _grid.Invoke(new Action(PaintPeriods));
                return;
            }

            for (int index = 0; index < _deltaPeriods.Periods.Count; index++)
            {
                DataGridViewRow nRow = new DataGridViewRow();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[0].Value = _deltaPeriods.Periods[index].HourStart;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[1].Value = _deltaPeriods.Periods[index].HourEnd;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[2].Value = _deltaPeriods.Periods[index].DeltaStep;

                _grid.Rows.Add(nRow);
            }
        }

        /// <summary>
        /// сохранить периоды с чарта
        /// </summary>
        private void SavePeriods()
        {
            for (int index = 0; index < _deltaPeriods.Periods.Count; index++)
            {
                _deltaPeriods.Periods[index].DeltaStep = Convert.ToInt32(_grid.Rows[index].Cells[2].Value.ToString());
            }
        }

        void _grid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                SavePeriods();
            }
            catch (Exception)
            { 
               PaintPeriods();
            }
        }

    }
}
