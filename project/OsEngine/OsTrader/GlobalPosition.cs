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
using OsEngine.Market;

namespace OsEngine.OsTrader
{
    /// <summary>
    /// class responsible for drawing the global position of all robots in the main window
    /// класс отвечающий за прорисовку глобальной позиции всех роботов в главном окне
    /// </summary>
    public class GlobalPosition
    {

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="allPositionHost">the host on which we will draw the date grid / хост на который будем рисовать дата грид</param>
        /// <param name="startProgram">program running class / программа запустившая класс</param>
        public GlobalPosition(WindowsFormsHost allPositionHost, StartProgram startProgram)
        {
            _startProgram = startProgram;

            _host = allPositionHost;

            _grid = CreateNewTable();

            _host.Child = _grid;
            _host.Child.Show();

            Task task = new Task(WatcherHome);
            task.Start();
       
        }

        /// <summary>
        /// add another magazine to the collection to draw his deals
        /// добавить ещё один журнал в коллекцию для прорисовки его сделок
        /// </summary>
        /// <param name="journal">new journal / новый журнал</param>
        public void SetJournal(Journal.Journal journal)
        {
            try
            {
                if (_journals == null)
                {
                    _journals = new List<Journal.Journal>();
                }

                if (_journals.Find(journal1 => journal1.Name == journal.Name) == null)
                {
                    _journals.Add(journal);
                    journal.PositionStateChangeEvent += journal_PositionChangeEvent;

                    List<Position> openPositions = journal.OpenPositions;

                    for (int i = 0; openPositions != null && i < openPositions.Count; i++)
                    {
                        journal_PositionChangeEvent(openPositions[i]);
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// clear previously loaded journals
        /// очистить от ранее загруженых журналов
        /// </summary>
        public void ClearJournals()
        {
            try
            {
                if (_grid.InvokeRequired)
                {
                    _grid.Invoke(new Action(ClearJournals));
                    return;
                }

                for (int i = 0; _journals != null && i < _journals.Count; i++)
                {
                    _journals[i].PositionStateChangeEvent -= journal_PositionChangeEvent;
                }

                _journals = null;
                _grid.Rows.Clear();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// journals we follow
        /// журналы за которыми мы следим
        /// </summary>
        private List<Journal.Journal> _journals;

        /// <summary>
        /// the host on which the table is displayed
        /// хост на котором отображаем таблицу
        /// </summary>
        private WindowsFormsHost _host;

        /// <summary>
        /// таблица для прорисовки позиций
        /// </summary>
        private DataGridView _grid;

        /// <summary>
        /// table for drawing positions
        /// программа запустившая класс
        /// </summary>
        private StartProgram _startProgram;

//drawing / прорисовка

        /// <summary>
        /// stop drawing elements
        /// остановить прорисовку элементов 
        /// </summary>
        public void StopPaint()
        {
            try
            {
                if (!_host.CheckAccess())
                {
                    _host.Dispatcher.Invoke(StopPaint);
                    return;
                }
                _host.Child = null;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// start drawing elements
        /// запустить прорисовку элементов
        /// </summary>
        public void StartPaint()
        {
            try
            {
                if (!_host.CheckAccess())
                {
                    _host.Dispatcher.Invoke(StartPaint);
                    return;
                }
                _host.Child = _grid;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// create a table
        /// создать таблицу
        /// </summary>
        private DataGridView CreateNewTable()
        {
            try
            {
                DataGridView newGrid = DataGridFactory.GetDataGridPosition();

                return newGrid;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        /// <summary>
        /// position has changed in the journals
        /// в журнале изменилась позиция
        /// </summary>
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        public void journal_PositionChangeEvent(Position position)
        {
            if (_startProgram != StartProgram.IsTester)
            {
                return;
            }

            try
            {
                if (_grid.InvokeRequired)
                {
                    _grid.Invoke(new Action<Position>(journal_PositionChangeEvent), position);
                    return;
                }

                if (position.State != PositionStateType.Open && position.State != PositionStateType.Opening &&
                    position.State != PositionStateType.Closing && position.State != PositionStateType.ClosingFail)
                {
                    for (int i = 0; i < _grid.Rows.Count; i++)
                    {
                        if ((int)_grid.Rows[i].Cells[0].Value == position.Number)
                        {
                            _grid.Rows.Remove(_grid.Rows[i]);
                            return;
                        }
                    }
                }
                else
                { 
                    for (int i = 0; i < _grid.Rows.Count; i++)
                    {
                        if ((int)_grid.Rows[i].Cells[0].Value == position.Number)
                        {
                            _grid.Rows.Remove(_grid.Rows[i]);
                            DataGridViewRow row1 = GetRow(position);
                            if (row1 != null)
                            {
                                _grid.Rows.Insert(i, row1);
                            }

                            return;
                        }
                    }
                    DataGridViewRow row = GetRow(position);
                    if (row != null)
                    {
                        _grid.Rows.Add(row);
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// take a row for the table representing the position
        /// взять строку для таблицы представляющую позицию
        /// </summary>
        private DataGridViewRow GetRow(Position position)
        {
            if (position == null)
            {
                return null;
            }

            try
            {
                DataGridViewRow nRow = new DataGridViewRow();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[0].Value = position.Number;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[1].Value = position.TimeCreate;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[2].Value = position.TimeClose;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[3].Value = position.NameBot;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[4].Value = position.SecurityName;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[5].Value = position.Direction;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[6].Value = position.State;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[7].Value = position.MaxVolume.ToStringWithNoEndZero();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[8].Value = position.OpenVolume.ToStringWithNoEndZero();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[9].Value = position.WaitVolume.ToStringWithNoEndZero();

                if (position.EntryPrice != 0)
                {
                    nRow.Cells.Add(new DataGridViewTextBoxCell());

                    nRow.Cells[10].Value = position.EntryPrice.ToStringWithNoEndZero();
                }
                else
                {
                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    if(position.OpenOrders != null &&
                       position.OpenOrders.Count != 0 &&
                       position.State != PositionStateType.OpeningFail)
                    {
                        nRow.Cells[10].Value = position.OpenOrders[position.OpenOrders.Count-1].Price.ToStringWithNoEndZero();
                    }
                }

                if (position.ClosePrice != 0)
                {
                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[11].Value = position.ClosePrice.ToStringWithNoEndZero();
                }
                else
                {
                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    if (position.CloseOrders != null &&
                        position.CloseOrders.Count != 0 &&
                        position.State != PositionStateType.ClosingFail)
                    {
                        nRow.Cells[11].Value = position.CloseOrders[position.CloseOrders.Count - 1].Price.ToStringWithNoEndZero();
                    }
                }

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[12].Value = position.ProfitPortfolioPunkt.ToStringWithNoEndZero();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[13].Value = position.StopOrderRedLine.ToStringWithNoEndZero();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[14].Value = position.StopOrderPrice.ToStringWithNoEndZero();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[15].Value = position.ProfitOrderRedLine.ToStringWithNoEndZero();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[16].Value = position.ProfitOrderPrice.ToStringWithNoEndZero();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[17].Value = position.SignalTypeOpen;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[18].Value = position.SignalTypeClose;

                return nRow;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        /// <summary>
        /// place of work that keeps logs
        /// место работы потока который сохраняет логи
        /// </summary>
        private async void WatcherHome()
        {
            if (_startProgram != StartProgram.IsOsTrader)
            {
                return;
            }

            while (true)
            {
               await Task.Delay(2000);

                CheckPosition();

                if (!MainWindow.ProccesIsWorked)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// check the position on the correctness of drawing
        /// проверить позиции на правильность прорисовки
        /// </summary>
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptions]
        private void CheckPosition()
        {
            if (_grid.InvokeRequired)
            {
                _grid.Invoke(new Action(CheckPosition));
                return;
            }
            try
            {
                List<Position> openPositions = new List<Position>();

                for (int i = 0; _journals != null && i < _journals.Count; i++)
                {
                    if (_journals[i].OpenPositions != null && _journals[i].OpenPositions.Count != 0)
                    {
                        openPositions.AddRange(_journals[i].OpenPositions);
                    }
                }

                for (int i1 = 0; i1 < openPositions.Count; i1++)
                {
                    Position position = openPositions[i1];
                    DataGridViewRow row = GetRow(position);
                    bool isIn = false;
                    for (int i = 0; i < _grid.Rows.Count; i++)
                    {
                        if (_grid.Rows[i].Cells[0].Value != null &&
                            (int) _grid.Rows[i].Cells[0].Value == position.Number)
                        {
                            _grid.Rows.Remove(_grid.Rows[i]);
                            DataGridViewRow row1 = GetRow(position);
                            if (row1 != null)
                            {
                                _grid.Rows.Add(row1);
                            }
                            isIn = true;
                            break;
                        }
                    }

                    if (isIn == false && row != null)
                    {
                        _grid.Rows.Add(row);
                    }
                }

                for (int i = 0; i < _grid.Rows.Count; i++)
                {
                    if (openPositions.Find(pos => pos.Number == (int) _grid.Rows[i].Cells[0].Value) == null)
                    {
                        _grid.Rows.Remove(_grid.Rows[i]);
                    }
                }

            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
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
