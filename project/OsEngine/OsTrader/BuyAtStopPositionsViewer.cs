/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.OsTrader.Panels.Tab;
using System.Drawing;
using OsEngine.Alerts;
using System.Globalization;
using OsEngine.Market;

namespace OsEngine.OsTrader
{
    public class BuyAtStopPositionsViewer
    {
        public BuyAtStopPositionsViewer(WindowsFormsHost positionHost, StartProgram startProgram)
        {
            _positionHost = positionHost;
            _startProgram = startProgram;

            _grid = CreateNewTable();
            _positionHost.Child = _grid;
            _positionHost.Child.Show();
            _grid.Click += _grid_Click;
            _grid.DoubleClick += _gridOpenPoses_DoubleClick;
            _grid.DataError += _grid_DataError;
            _currentCulture = OsLocalization.CurCulture;

            Task task = new Task(WatcherThreadWorkArea);
            task.Start();
        }

        private void _grid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            SendNewLogMessage(e.ToString(), LogMessageType.Error);
        }

        CultureInfo _currentCulture;

        public void LoadTabToWatch(List<BotTabSimple> tabs)
        {
            _tabsToWatch = tabs;
        }

        private List<BotTabSimple> _tabsToWatch = new List<BotTabSimple>();

        public void StopPaint()
        {
            try
            {
                if (!_positionHost.CheckAccess())
                {
                    _positionHost.Dispatcher.Invoke(StopPaint);
                    return;
                }

                if (_positionHost != null)
                {
                    _positionHost.Child = null;
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void StartPaint()
        {
            try
            {
                if (!_positionHost.CheckAccess())
                {
                    _positionHost.Dispatcher.Invoke(StartPaint);
                    return;
                }

                if (_positionHost != null)
                {
                    _positionHost.Child = _grid;
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void ClearDelete()
        {
            try
            {
                _tabsToWatch.Clear();
                _isDeleted = true;

                if (_positionHost != null)
                {
                    _positionHost.Child = null;
                    _positionHost = null;
                }

                if (_grid != null)
                {
                    DataGridFactory.ClearLinks(_grid);
                    _grid.Click -= _grid_Click;
                    _grid.DoubleClick -= _gridOpenPoses_DoubleClick;
                    _grid.DataError -= _grid_DataError;
                    _grid = null;
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        WindowsFormsHost _positionHost;

        private DataGridView _grid;

        StartProgram _startProgram;

        private DataGridView CreateNewTable()
        {
            try
            {
                DataGridView newGrid = DataGridFactory.GetDataGridBuyAtStopPositions();
                newGrid.ScrollBars = ScrollBars.Vertical;
                return newGrid;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        #region drawing a double-clicked order

        private void _gridOpenPoses_DoubleClick(object sender, EventArgs e)
        {
            PaintPos(_grid);
        }

        private void PaintPos(DataGridView grid)
        {
            string botTabName;
            int numberRow;

            try
            {
                if (grid.CurrentCell == null)
                {
                    return;
                }

                numberRow = grid.CurrentCell.RowIndex;

                botTabName = grid.Rows[grid.CurrentCell.RowIndex].Cells[2].Value.ToString();
            }
            catch (Exception)
            {
                return;
            }

            if (UserClickOnPositionShowBotInTableEvent != null)
            {
                UserClickOnPositionShowBotInTableEvent(botTabName);
            }

            _rowToPaintInOpenPoses = numberRow;
            _lastClickGrid = grid;

            Task.Run(PaintPos);
        }

        DataGridView _lastClickGrid;

        int _rowToPaintInOpenPoses;

        private async void PaintPos()
        {
            await Task.Delay(200);
            ColoredRow(Color.LightSlateGray);
            await Task.Delay(600);
            ColoredRow(Color.FromArgb(17, 18, 23));
        }

        private void ColoredRow(Color color)
        {
            if (_lastClickGrid.InvokeRequired)
            {
                _lastClickGrid.Invoke(new Action<Color>(ColoredRow), color);
                return;
            }
            try
            {
                _lastClickGrid.Rows[_rowToPaintInOpenPoses].DefaultCellStyle.SelectionBackColor = color;
            }
            catch
            {
                return;
            }
        }

        public event Action<string> UserClickOnPositionShowBotInTableEvent;

        #endregion

        #region clicks on the table

        private void _grid_Click(object sender, EventArgs e)
        {
            MouseEventArgs mouse = (MouseEventArgs)e;

            if (mouse.Button != MouseButtons.Right)
            {
                if (_grid.ContextMenuStrip != null)
                {
                    _grid.ContextMenuStrip = null;
                }
                return;
            }

            try
            {
                ToolStripMenuItem[] items = new ToolStripMenuItem[2];

                items[0] = new ToolStripMenuItem { Text = OsLocalization.Trader.Label213 };
                items[0].Click += PositionCloseAll_Click;

                items[1] = new ToolStripMenuItem { Text = OsLocalization.Trader.Label214 };
                items[1].Click += PositionCloseForNumber_Click;

                ContextMenuStrip menu = new ContextMenuStrip(); menu.Items.AddRange(items);

                _grid.ContextMenuStrip = menu;
                _grid.ContextMenuStrip.Show(_grid, new Point(mouse.X, mouse.Y));
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        void PositionCloseAll_Click(object sender, EventArgs e)
        {
            try
            {
                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label215);
                ui.ShowDialog();

                if (ui.UserAcceptAction == false)
                {
                    return;
                }

                if (UserSelectActionEvent != null)
                {
                    UserSelectActionEvent(-1, SignalType.DeleteAllPoses);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        void PositionCloseForNumber_Click(object sender, EventArgs e)
        {
            try
            {
                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label216);
                ui.ShowDialog();

                if (ui.UserAcceptAction == false)
                {
                    return;
                }

                int number;
                try
                {
                    if (_grid.CurrentCell == null)
                    {
                        return;
                    }
                    number = Convert.ToInt32(_grid.Rows[_grid.CurrentCell.RowIndex].Cells[0].Value);
                }
                catch (Exception)
                {
                    return;
                }

                if (UserSelectActionEvent != null)
                {
                    UserSelectActionEvent(number, SignalType.DeletePos);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public event Action<int, SignalType> UserSelectActionEvent;

        #endregion

        // drawing

        bool _isDeleted;

        private async void WatcherThreadWorkArea()
        {
            if (_startProgram != StartProgram.IsTester &&
                _startProgram != StartProgram.IsOsTrader)
            {
                return;
            }

            while (true)
            {
                try
                {
                    await Task.Delay(3000);

                    if (_isDeleted)
                    {
                        return;
                    }

                    List<PositionOpenerToStopLimit> stopLimits = new List<PositionOpenerToStopLimit>();

                    for (int i = 0; i < _tabsToWatch.Count; i++)
                    {
                        if (_tabsToWatch[i].PositionOpenerToStop != null &&
                            _tabsToWatch[i].PositionOpenerToStop.Count != 0)
                        {
                            stopLimits.AddRange(_tabsToWatch[i].PositionOpenerToStop);
                        }
                    }

                    CheckPosition(_grid, stopLimits);
                }
                catch (Exception error)
                {
                    ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        private void CheckPosition(DataGridView grid, List<PositionOpenerToStopLimit> positions)
        {
            if (grid == null)
            {
                return;
            }
            if (grid.InvokeRequired)
            {
                grid.Invoke(new Action<DataGridView, List<PositionOpenerToStopLimit>>(CheckPosition), grid, positions);
                return;
            }
            try
            {
                for (int i1 = 0; i1 < positions.Count; i1++)
                {
                    PositionOpenerToStopLimit position = positions[i1];
                    bool isIn = false;
                    for (int i = 0; i < grid.Rows.Count; i++)
                    {
                        if (grid.Rows[i].Cells[0].Value != null &&
                            grid.Rows[i].Cells[0].Value.ToString() == position.Number.ToString())
                        {
                            isIn = true;
                            break;
                        }
                    }

                    if (isIn == false)
                    {
                        DataGridViewRow row = GetRow(position);

                        if (row != null)
                        {
                            grid.Rows.Insert(0, row);
                        }
                    }
                }

                for (int i = 0; i < grid.Rows.Count; i++)
                {
                    if (positions.Find(pos => pos.Number == (int)grid.Rows[i].Cells[0].Value) == null)
                    {
                        grid.Rows.Remove(grid.Rows[i]);
                    }
                }

            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private DataGridViewRow GetRow(PositionOpenerToStopLimit position)
        {
            if (position == null)
            {
                return null;
            }

            try
            {
                /*  
positionOpener.Number
positionOpener.TimeCreate
positionOpener.TabName
positionOpener.Securit
positionOpener.Volume = volume;
positionOpener.Side
positionOpener.ActivateType
positionOpener.PriceRedLine
positionOpener.PriceOrder
positionOpener.ExpiresBars
positionOpener.LifeTimeType

*/


                DataGridViewRow nRow = new DataGridViewRow();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[0].Value = position.Number;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[1].Value = position.TimeCreate.ToString(_currentCulture);

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[2].Value = position.TabName;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[3].Value = position.Security;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[4].Value = position.Volume;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[5].Value = position.Side;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[6].Value = position.ActivateType.ToString();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[7].Value = position.PriceRedLine.ToStringWithNoEndZero();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[8].Value = position.PriceOrder.ToStringWithNoEndZero();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[9].Value = position.ExpiresBars.ToString();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[10].Value = position.LifeTimeType.ToString();

                return nRow;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        // messages in log

        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            {
                System.Windows.MessageBox.Show(message);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;
    }
}