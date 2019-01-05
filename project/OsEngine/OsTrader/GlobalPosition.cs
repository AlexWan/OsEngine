/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;

namespace OsEngine.OsTrader
{
    /// <summary>
    /// класс отвечающий за прорисовку глобальной позиции всех роботов в главном окне
    /// </summary>
    public class GlobalPosition
    {

// сервис

        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="allPositionHost">хост на который будем рисовать дата грид</param>
        /// <param name="startProgram">программа запустившая класс</param>
        public GlobalPosition(WindowsFormsHost allPositionHost, StartProgram startProgram)
        {
            _startProgram = startProgram;

            _host = allPositionHost;

            _grid = CreateNewTable();

            _host.Child = _grid;
            _host.Child.Show();

            if (Watcher == null)
            {
                Watcher = new Thread(WatcherHome);
                Watcher.IsBackground = true;
                Watcher.Name = "GlobalPositionThread";
                Watcher.Start();
            }
        }

        /// <summary>
        /// добавить ещё один журнал в коллекцию для прорисовки его сделок
        /// </summary>
        /// <param name="journal">новый журнал</param>
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
                { // отписываемся от обновления позиции
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
        /// журналы за которыми мы следим
        /// </summary>
        private List<Journal.Journal> _journals;

        /// <summary>
        /// хост на котором отображаем таблицу
        /// </summary>
        private WindowsFormsHost _host;

        /// <summary>
        /// таблица для прорисовки позиций
        /// </summary>
        private DataGridView _grid;

        /// <summary>
        /// программа запустившая класс
        /// </summary>
        private StartProgram _startProgram;

//прорисовка

        /// <summary>
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
        /// создать таблицу
        /// </summary>
        /// <returns>таблица для прорисовки на ней позиций</returns>
        private DataGridView CreateNewTable()
        {
            try
            {
                DataGridView newGrid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.None);

                DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
                cell0.Style = newGrid.DefaultCellStyle;

                DataGridViewColumn colum0 = new DataGridViewColumn();
                colum0.CellTemplate = cell0;
                colum0.HeaderText = @"Номер";
                colum0.ReadOnly = true;
                colum0.Width = 50;
                newGrid.Columns.Add(colum0);

                DataGridViewColumn colum01 = new DataGridViewColumn();
                colum01.CellTemplate = cell0;
                colum01.HeaderText = @"Время";
                colum01.ReadOnly = true;
                colum01.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                newGrid.Columns.Add(colum01);

                DataGridViewColumn colu = new DataGridViewColumn();
                colu.CellTemplate = cell0;
                colu.HeaderText = @"Бот";
                colu.ReadOnly = true;
                colu.Width = 70;

                newGrid.Columns.Add(colu);

                DataGridViewColumn colum1 = new DataGridViewColumn();
                colum1.CellTemplate = cell0;
                colum1.HeaderText = @"Инструмент";
                colum1.ReadOnly = true;
                colum1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                newGrid.Columns.Add(colum1);

                DataGridViewColumn colum2 = new DataGridViewColumn();
                colum2.CellTemplate = cell0;
                colum2.HeaderText = @"Напр.";
                colum2.ReadOnly = true;
                colum2.Width = 40;

                newGrid.Columns.Add(colum2);

                DataGridViewColumn colum3 = new DataGridViewColumn();
                colum3.CellTemplate = cell0;
                colum3.HeaderText = @"Cостояние";
                colum3.ReadOnly = true;
                colum3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                newGrid.Columns.Add(colum3);

                DataGridViewColumn colum4 = new DataGridViewColumn();
                colum4.CellTemplate = cell0;
                colum4.HeaderText = @"Объём";
                colum4.ReadOnly = true;
                colum4.Width = 60;

                newGrid.Columns.Add(colum4);

                DataGridViewColumn colum45 = new DataGridViewColumn();
                colum45.CellTemplate = cell0;
                colum45.HeaderText = @"Текущий";
                colum45.ReadOnly = true;
                colum45.Width = 60;

                newGrid.Columns.Add(colum45);

                DataGridViewColumn colum5 = new DataGridViewColumn();
                colum5.CellTemplate = cell0;
                colum5.HeaderText = @"Ожидает";
                colum5.ReadOnly = true;
                colum5.Width = 60;

                newGrid.Columns.Add(colum5);

                DataGridViewColumn colum6 = new DataGridViewColumn();
                colum6.CellTemplate = cell0;
                colum6.HeaderText = @"Цена входа";
                colum6.ReadOnly = true;
                colum6.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                newGrid.Columns.Add(colum6);

                DataGridViewColumn colum61 = new DataGridViewColumn();
                colum61.CellTemplate = cell0;
                colum61.HeaderText = @"Цена выхода";
                colum61.ReadOnly = true;
                colum61.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                newGrid.Columns.Add(colum61);

                DataGridViewColumn colum8 = new DataGridViewColumn();
                colum8.CellTemplate = cell0;
                colum8.HeaderText = @"Прибыль";
                colum8.ReadOnly = true;
                colum8.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                newGrid.Columns.Add(colum8);

                DataGridViewColumn colum9 = new DataGridViewColumn();
                colum9.CellTemplate = cell0;
                colum9.HeaderText = @"СтопАктивация";
                colum9.ReadOnly = true;
                colum9.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                newGrid.Columns.Add(colum9);

                DataGridViewColumn colum10 = new DataGridViewColumn();
                colum10.CellTemplate = cell0;
                colum10.HeaderText = @"СтопЦена";
                colum10.ReadOnly = true;
                colum10.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                newGrid.Columns.Add(colum10);

                DataGridViewColumn colum11 = new DataGridViewColumn();
                colum11.CellTemplate = cell0;
                colum11.HeaderText = @"ПрофитАктивация";
                colum11.ReadOnly = true;
                colum11.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                newGrid.Columns.Add(colum11);

                DataGridViewColumn colum12 = new DataGridViewColumn();
                colum12.CellTemplate = cell0;
                colum12.HeaderText = @"ПрофитЦена";
                colum12.ReadOnly = true;
                colum12.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                newGrid.Columns.Add(colum12);

                return newGrid;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        /// <summary>
        /// в журнале изменилась позиция
        /// </summary>
        /// <param name="position">позиция</param>
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        public void journal_PositionChangeEvent(Position position)
        {
            // В ТЕСТЕРЕ позиции прорисоываются по очереди, В реале, в методе ThreadWatcher()
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
                {// сделка была удалена. Надо её удалить отовсюду
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
                { // сделкка должна быть прорисована в таблице

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
        /// взять строку для таблицы представляющую позицию
        /// </summary>
        /// <param name="position">позиция</param>
        /// <returns>строка для таблицы</returns>
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
                nRow.Cells[2].Value = position.NameBot;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[3].Value = position.SecurityName;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[4].Value = position.Direction;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[5].Value = position.State;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[6].Value = position.MaxVolume;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[7].Value = position.OpenVolume;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[8].Value = position.WaitVolume;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[9].Value = position.EntryPrice;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[10].Value = position.ClosePrice;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[11].Value = position.ProfitPortfolioPunkt;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[12].Value = position.StopOrderRedLine;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[13].Value = position.StopOrderPrice;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[14].Value = position.ProfitOrderRedLine;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[15].Value = position.ProfitOrderPrice;

                return nRow;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        /// <summary>
        /// поток 
        /// </summary>
        private Thread Watcher;

        /// <summary>
        /// место работы потока который сохраняет логи
        /// </summary>
        private void WatcherHome()
        {
            if (_startProgram != StartProgram.IsOsTrader)
            {
                return;
            }

            while (true)
            {
                Thread.Sleep(2000);

                CheckPosition();

                if (!MainWindow.ProccesIsWorked)
                {
                    return;
                }
            }
        }


        /// <summary>
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

// сообщения в лог 

        /// <summary>
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
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

    }
}
