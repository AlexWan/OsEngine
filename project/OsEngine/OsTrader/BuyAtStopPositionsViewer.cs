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
using OsEngine.Logging;
using OsEngine.OsTrader.Panels.Tab;

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

            Task task = new Task(WatcherThreadWorkArea);
            task.Start();
        }

        public void LoadTabToWatch(List<BotTabSimple> tabs)
        {
            DeleteDontActiveTabs(tabs);
            CreateActiveTabs(tabs);
        }

        private void CreateActiveTabs(List<BotTabSimple> actualTabs)
        {
            if (actualTabs == null
                || actualTabs.Count == 0)
            {
                return;
            }

            for (int i = 0; i < actualTabs.Count; i++)
            {
                if (_tabsToWatch.Find(t => t.TabName == actualTabs[i].TabName) == null)
                {
                    _tabsToWatch.Add(actualTabs[i]);
                }
            }
        }

        private void DeleteDontActiveTabs(List<BotTabSimple> actualTabs)
        {
            if(actualTabs == null 
                ||
                actualTabs.Count == 0)
            {
                _tabsToWatch.Clear();
                return;
            }

            for(int i = 0;i < _tabsToWatch.Count;i++)
            {
                if(actualTabs.Find(t => t.TabName == _tabsToWatch[i].TabName) == null)
                {
                    _tabsToWatch.RemoveAt(i);
                    i--;
                }
            }
        }

        private List<BotTabSimple> _tabsToWatch = new List<BotTabSimple>();

        public void StopPaint()
        {
            try
            {
               if(!_positionHost.CheckAccess())
                {
                    _positionHost.Dispatcher.Invoke(StopPaint);
                    return;
                }

                if(_positionHost != null)
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
            _tabsToWatch.Clear();
            _isDeleted = true;
            _positionHost.Child = null;
            _positionHost = null;

            DataGridFactory.ClearLinks(_grid); 
            _grid = null;
        }

        WindowsFormsHost _positionHost;

        private DataGridView _grid;

        StartProgram _startProgram;

        // создание таблицы

        private DataGridView CreateNewTable()
        {
            try
            {
                DataGridView newGrid = DataGridFactory.GetDataGridBuyAtStopPositions();
                newGrid.ScrollBars = ScrollBars.Vertical;

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

    */



                return newGrid;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        // прорисовка

        bool _isDeleted;

        private async void WatcherThreadWorkArea()
        {
            if (_startProgram != StartProgram.IsTester &&
                _startProgram != StartProgram.IsOsTrader)
            {
                return;
            }

            while(true)
            {
                if (_isDeleted)
                {
                    return;
                }

                await Task.Delay(3000);

                List<PositionOpenerToStop> stopLimits = new List<PositionOpenerToStop>();

                for(int i = 0;i < _tabsToWatch.Count;i++)
                {
                    if (_tabsToWatch[i]._stopsOpener != null &&
                        _tabsToWatch[i]._stopsOpener.Count != 0)
                    {
                        stopLimits.AddRange(_tabsToWatch[i]._stopsOpener);
                    }
                }

                CheckPosition(_grid, stopLimits);

            }
        }

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptions]
        private void CheckPosition(DataGridView grid, List<PositionOpenerToStop> positions)
        {
            if (grid.InvokeRequired)
            {
                grid.Invoke(new Action<DataGridView, List<PositionOpenerToStop>>(CheckPosition), grid, positions);
                return;
            }
            try
            {
                for (int i1 = 0; i1 < positions.Count; i1++)
                {
                    PositionOpenerToStop position = positions[i1];
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

        private DataGridViewRow GetRow(PositionOpenerToStop position)
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

*/


                DataGridViewRow nRow = new DataGridViewRow();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[0].Value = position.Number;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[1].Value = position.TimeCreate;

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



                return nRow;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        // messages in log / сообщения в лог 

        /// <summary>
        /// send a new message to the top
        /// выслать новое сообщение на верх
        /// </summary>
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

        /// <summary>
        /// outgoing message for log
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

    }
}