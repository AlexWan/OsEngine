/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Forms;

namespace OsEngine.Instructions
{
    public partial class InstructionsUi : Window
    {
        public InstructionsUi(List<Instruction> instructions, string description)
        {
            InitializeComponent();

            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            _instructions = instructions;
            _description = description;

            if(string.IsNullOrEmpty(_description))
            {
                GridPrime.RowDefinitions[0].Height = new GridLength(0, GridUnitType.Pixel);
            }
            else
            {
                TextBlockDesctiption.Text = _description;
            }

            CreateLinksGrid();
            RePaintGridTable();

            Title = OsLocalization.Trader.Label643;
        }

        public List<Instruction> _instructions;

        public string _description;

        private DataGridView _gridDataGrid;

        private void CreateLinksGrid()
        {
            try
            {
                if (MainWindow.GetDispatcher.CheckAccess() == false)
                {
                    MainWindow.GetDispatcher.Invoke(new Action(CreateLinksGrid));
                    return;
                }

                _gridDataGrid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
                       DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders);
                _gridDataGrid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
                _gridDataGrid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                _gridDataGrid.RowsDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                _gridDataGrid.ScrollBars = ScrollBars.Vertical;

                DataGridViewTextBoxCell cellParam0 = new DataGridViewTextBoxCell();
                cellParam0.Style = _gridDataGrid.DefaultCellStyle;
                cellParam0.Style.WrapMode = DataGridViewTriState.True;

                DataGridViewColumn newColumn0 = new DataGridViewColumn();
                newColumn0.CellTemplate = cellParam0;
                newColumn0.HeaderText = "#";                            // 0 номер
                _gridDataGrid.Columns.Add(newColumn0);
                newColumn0.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;

                DataGridViewColumn newColumn1 = new DataGridViewColumn();
                newColumn1.CellTemplate = cellParam0;
                newColumn1.HeaderText = OsLocalization.Trader.Label640;  // 1 описание
                _gridDataGrid.Columns.Add(newColumn1);
                newColumn1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn2 = new DataGridViewColumn();
                newColumn2.CellTemplate = cellParam0;
                newColumn2.HeaderText = OsLocalization.Trader.Label167;  // 2 тип
                _gridDataGrid.Columns.Add(newColumn2);
                newColumn2.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;

                DataGridViewColumn newColumn4 = new DataGridViewColumn();
                newColumn4.CellTemplate = cellParam0;
                newColumn4.HeaderText = "";                              // 4 кнопка "Перейти"
                _gridDataGrid.Columns.Add(newColumn4);
                newColumn4.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;

                HostLinks.Child = _gridDataGrid; 

                _gridDataGrid.DataError += _gridDataGrid_DataError;
                _gridDataGrid.CellClick += _gridDataGrid_CellClick;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error); 
            }
        }

        private void RePaintGridTable()
        {
            try
            {
                // 0 номер
                // 1 описание
                // 2 тип
                // 3 кнопка "Перейти"

                if (_gridDataGrid == null)
                {
                    return;
                }

                if (_gridDataGrid.InvokeRequired)
                {
                    _gridDataGrid.Invoke(new Action(RePaintGridTable));
                    return;
                }

                _gridDataGrid.Rows.Clear();

                for (int i = 0; i < _instructions.Count; i++)
                {
                    Instruction curLine = _instructions[i];

                    DataGridViewRow rowLine = new DataGridViewRow();

                    rowLine.Cells.Add(new DataGridViewTextBoxCell());
                    rowLine.Cells[^1].Value = i + 1;
                    rowLine.Cells[^1].ReadOnly = true;

                    rowLine.Cells.Add(new DataGridViewTextBoxCell());
                    rowLine.Cells[^1].Value = curLine.Description;
                    rowLine.Cells[^1].ReadOnly = true;

                    rowLine.Cells.Add(new DataGridViewTextBoxCell());
                    rowLine.Cells[^1].Value = curLine.Type.ToString();
                    rowLine.Cells[^1].ReadOnly = false;

                    rowLine.Cells.Add(new DataGridViewButtonCell());
                    rowLine.Cells[^1].Value = OsLocalization.Trader.Label642;
                    rowLine.Cells[^1].ReadOnly = false;

                    _gridDataGrid.Rows.Add(rowLine);
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
            }
        }

        private void _gridDataGrid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                int row = e.RowIndex;
                int column = e.ColumnIndex;

                if (row == -1 || column == -1) { return; }

                if (row >= _instructions.Count) { return; }

                if (column == 3)
                { // кнопка "Перейти"
                    Instruction instruction = _instructions[row];

                    Process.Start(new ProcessStartInfo(instruction.PostLink) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
            }
        }

        private void _gridDataGrid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            ServerMaster.SendNewLogMessage(e.ToString(), OsEngine.Logging.LogMessageType.Error);
        }
    }
}
