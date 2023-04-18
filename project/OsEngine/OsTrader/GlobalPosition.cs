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
using System.Drawing;
using System.IO;
using System.Text;
using OsEngine.Language;
using OsEngine.Alerts;

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

            _gridOpenDeal = CreateNewTable();

            _host.Child = _gridOpenDeal;
            _host.Child.Show();

            _gridOpenDeal.Click += _gridAllPositions_Click;

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

                for(int i = 0;i < _journals.Count;i++)
                {
                    if(_journals[i].Name == journal.Name)
                    {
                        return;
                    }
                }

                _journals.Add(journal);

            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public Position GetPositionForNumber(int number)
        {
            List<Position> deals = new List<Position>();

            for(int i = 0;i < _journals.Count;i++)
            {
                if(_journals[i] != null)
                {
                    List<Position> curPoses = _journals[i].OpenPositions;

                    deals.AddRange(curPoses);
                }
            }

            return deals.Find(position => position.Number == number);
        }

        /// <summary>
        /// clear previously loaded journals
        /// очистить от ранее загруженых журналов
        /// </summary>
        public void ClearJournals()
        {
            try
            {
                if (_gridOpenDeal.InvokeRequired)
                {
                    _gridOpenDeal.Invoke(new Action(ClearJournals));
                    return;
                }

                _journals = null;
                _gridOpenDeal.Rows.Clear();
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
        private DataGridView _gridOpenDeal;

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
                _host.Child = _gridOpenDeal;
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

                decimal openPrice = position.EntryPrice;

                if (openPrice == 0)
                {
                    if (position.OpenOrders != null &&
                        position.OpenOrders.Count != 0 &&
                        position.State != PositionStateType.OpeningFail)
                    {
                        openPrice = position.OpenOrders[position.OpenOrders.Count - 1].Price;
                    }
                }

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[10].Value = openPrice.ToStringWithNoEndZero();

                decimal closePrice = position.ClosePrice;

                if (closePrice == 0)
                {
                    if (position.CloseOrders != null &&
                        position.CloseOrders.Count != 0 &&
                        position.State != PositionStateType.ClosingFail)
                    {
                        closePrice = position.CloseOrders[position.CloseOrders.Count - 1].Price;
                    }
                }

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[11].Value = closePrice.ToStringWithNoEndZero();

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

        private void TryRePaint(Position position, DataGridViewRow nRow)
        {
            if (nRow.Cells[1].Value == null
                || nRow.Cells[1].Value.ToString() != position.TimeCreate.ToString())// == false) //AVP убрал, потому что  во вкладке все позиции, дату позиции не обновляло
            {
                nRow.Cells[1].Value = position.TimeCreate.ToString();
            }

            if (nRow.Cells[2].Value == null
                || nRow.Cells[2].Value.ToString() != position.TimeClose.ToString())// == false) //AVP убрал потому что во вкладке все позиции, дату позиции не обновляло
            {
                nRow.Cells[2].Value = position.TimeClose.ToString();
            }

            if (nRow.Cells[6].Value == null
                || nRow.Cells[6].Value.ToString() != position.State.ToString())
            {
                nRow.Cells[6].Value = position.State;
            }

            if (nRow.Cells[7].Value == null
                || nRow.Cells[7].Value.ToString() != position.MaxVolume.ToStringWithNoEndZero())
            {
                nRow.Cells[7].Value = position.MaxVolume.ToStringWithNoEndZero();
            }

            if (nRow.Cells[8].Value == null
                || nRow.Cells[8].Value.ToString() != position.OpenVolume.ToStringWithNoEndZero())
            {
                nRow.Cells[8].Value = position.OpenVolume.ToStringWithNoEndZero();
            }

            if (nRow.Cells[9].Value == null
                || nRow.Cells[9].Value.ToString() != position.WaitVolume.ToStringWithNoEndZero())
            {
                nRow.Cells[9].Value = position.WaitVolume.ToStringWithNoEndZero();
            }

            decimal openPrice = position.EntryPrice;

            if (openPrice == 0)
            {
                if (position.OpenOrders != null &&
                    position.OpenOrders.Count != 0 &&
                    position.State != PositionStateType.OpeningFail)
                {
                    openPrice = position.OpenOrders[position.OpenOrders.Count - 1].Price;
                }
            }

            if (nRow.Cells[10].Value == null
                || nRow.Cells[10].Value.ToString() != openPrice.ToStringWithNoEndZero())
            {
                nRow.Cells[10].Value = openPrice.ToStringWithNoEndZero();
            }

            decimal closePrice = position.ClosePrice;

            if (closePrice == 0)
            {
                if (position.CloseOrders != null &&
                    position.CloseOrders.Count != 0 &&
                    position.State != PositionStateType.ClosingFail)
                {
                    closePrice = position.CloseOrders[position.CloseOrders.Count - 1].Price;
                }
            }

            if (nRow.Cells[11].Value == null
                || nRow.Cells[11].Value.ToString() != closePrice.ToStringWithNoEndZero())
            {
                nRow.Cells[11].Value = closePrice.ToStringWithNoEndZero();
            }

            if (nRow.Cells[12].Value == null
                || nRow.Cells[12].Value.ToString() != position.ProfitPortfolioPunkt.ToStringWithNoEndZero())
            {
                nRow.Cells[12].Value = position.ProfitPortfolioPunkt.ToStringWithNoEndZero();
            }
            if (nRow.Cells[13].Value == null ||
                nRow.Cells[13].Value.ToString() != position.StopOrderRedLine.ToStringWithNoEndZero())
            {
                nRow.Cells[13].Value = position.StopOrderRedLine.ToStringWithNoEndZero();
            }

            if (nRow.Cells[14].Value == null
                || nRow.Cells[14].Value.ToString() != position.StopOrderPrice.ToStringWithNoEndZero())
            {
                nRow.Cells[14].Value = position.StopOrderPrice.ToStringWithNoEndZero();
            }
            if (nRow.Cells[15].Value == null ||
                 nRow.Cells[15].Value.ToString() != position.ProfitOrderRedLine.ToStringWithNoEndZero())
            {
                nRow.Cells[15].Value = position.ProfitOrderRedLine.ToStringWithNoEndZero();
            }
            if (nRow.Cells[16].Value != null ||
                nRow.Cells[16].Value.ToString() != position.ProfitOrderPrice.ToStringWithNoEndZero())
            {
                nRow.Cells[16].Value = position.ProfitOrderPrice.ToStringWithNoEndZero();
            }

        }

        /// <summary>
        /// place of work that keeps logs
        /// место работы потока который сохраняет логи
        /// </summary>
        private async void WatcherHome()
        {
            if(_startProgram != StartProgram.IsTester &&
                _startProgram != StartProgram.IsOsTrader)
            {
                return;
            }

            while (true)
            {
                await Task.Delay(5000);

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
            if (_gridOpenDeal.InvokeRequired)
            {
                _gridOpenDeal.Invoke(new Action(CheckPosition));
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
                    
                    bool isIn = false;
                    for (int i = 0; i < _gridOpenDeal.Rows.Count; i++)
                    {
                        if (_gridOpenDeal.Rows[i].Cells[0].Value != null &&
                            _gridOpenDeal.Rows[i].Cells[0].Value.ToString() == position.Number.ToString())
                        {
                            TryRePaint(position, _gridOpenDeal.Rows[i]);
                            isIn = true;
                            break;
                        }
                    }
                    
                    if (isIn == false)
                    {
                        DataGridViewRow row = GetRow(position);

                        if(row != null)
                        {
                            _gridOpenDeal.Rows.Add(row);
                        }
                    }
                }

                for (int i = 0; i < _gridOpenDeal.Rows.Count; i++)
                {
                    if (openPositions.Find(pos => pos.Number == (int)_gridOpenDeal.Rows[i].Cells[0].Value) == null)
                    {
                        _gridOpenDeal.Rows.Remove(_gridOpenDeal.Rows[i]);
                    }
                }

            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        // вызов закрытия всех позиций у всех роботов

        private void _gridAllPositions_Click(object sender, EventArgs e)
        {
            MouseEventArgs mouse = (MouseEventArgs)e;
            if (mouse.Button != MouseButtons.Right)
            {
                return;
            }

            try
            {
                MenuItem[] items = new MenuItem[6];

                items[0] = new MenuItem { Text = OsLocalization.Journal.PositionMenuItem1 };
                items[0].Click += PositionCloseAll_Click;

                items[1] = new MenuItem { Text = OsLocalization.Journal.PositionMenuItem3 };
                items[1].Click += PositionCloseForNumber_Click;

                items[2] = new MenuItem { Text = OsLocalization.Journal.PositionMenuItem4 };
                items[2].Click += PositionModificationForNumber_Click;

                items[3] = new MenuItem { Text = OsLocalization.Journal.PositionMenuItem5 };
                items[3].Click += PositionNewStop_Click;

                items[4] = new MenuItem { Text = OsLocalization.Journal.PositionMenuItem6 };
                items[4].Click += PositionNewProfit_Click;

                items[5] = new MenuItem { Text = OsLocalization.Journal.PositionMenuItem7 };
                items[5].Click += PositionClearDelete_Click;

                ContextMenu menu = new ContextMenu(items);

                _gridOpenDeal.ContextMenu = menu;
                _gridOpenDeal.ContextMenu.Show(_gridOpenDeal, new Point(mouse.X, mouse.Y));
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }

        }

        /// <summary>
        /// the user has ordered the closing of all positions
        /// пользователь заказал закрытие всех позиций
        /// </summary>
        void PositionCloseAll_Click(object sender, EventArgs e)
        {
            try
            {
                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Journal.Message5);
                ui.ShowDialog();
                
                if(ui.UserAcceptActioin == false)
                {
                    return;
                }

                if (UserSelectActionEvent != null)
                {
                    UserSelectActionEvent(null, SignalType.CloseAll);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// the user has ordered the closing of the transaction by number
        /// пользователь заказал закрытие сделки по номеру
        /// </summary>
        void PositionCloseForNumber_Click(object sender, EventArgs e)
        {
            try
            {
                int number;
                try
                {
                    if(_gridOpenDeal.CurrentCell == null)
                    {
                        return;
                    }
                    number = Convert.ToInt32(_gridOpenDeal.Rows[_gridOpenDeal.CurrentCell.RowIndex].Cells[0].Value);
                }
                catch (Exception)
                {
                    return;
                }


                if (UserSelectActionEvent != null)
                {
                    UserSelectActionEvent(GetPositionForNumber(number), SignalType.CloseOne);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// the user has ordered a position modification
        /// пользователь заказал модификацию позиции
        /// </summary>
        void PositionModificationForNumber_Click(object sender, EventArgs e)
        {
            try
            {
                int number;
                try
                {
                    if(_gridOpenDeal.CurrentCell == null)
                    {
                        return;
                    }
                    number = Convert.ToInt32(_gridOpenDeal.Rows[_gridOpenDeal.CurrentCell.RowIndex].Cells[0].Value);
                }
                catch (Exception)
                {
                    return;
                }


                if (UserSelectActionEvent != null)
                {
                    UserSelectActionEvent(GetPositionForNumber(number), SignalType.Modificate);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// the user has ordered a new stop for the position
        /// пользователь заказал новый стоп для позиции
        /// </summary>
        void PositionNewStop_Click(object sender, EventArgs e)
        {
            try
            {
                int number;
                try
                {
                    if(_gridOpenDeal.CurrentCell == null)
                    {
                        return;
                    }
                    number = Convert.ToInt32(_gridOpenDeal.Rows[_gridOpenDeal.CurrentCell.RowIndex].Cells[0].Value);
                }
                catch (Exception)
                {
                    return;
                }

                if (UserSelectActionEvent != null)
                {
                    UserSelectActionEvent(GetPositionForNumber(number), SignalType.ReloadStop);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// the user has ordered a new profit for the position
        /// пользователь заказал новый профит для позиции
        /// </summary>
        void PositionNewProfit_Click(object sender, EventArgs e)
        {
            try
            {
                int number;
                try
                {
                    if(_gridOpenDeal.CurrentCell == null)
                    {
                        return;
                    }
                    number = Convert.ToInt32(_gridOpenDeal.Rows[_gridOpenDeal.CurrentCell.RowIndex].Cells[0].Value);
                }
                catch (Exception)
                {
                    return;
                }

                if (UserSelectActionEvent != null)
                {
                    UserSelectActionEvent(GetPositionForNumber(number), SignalType.ReloadProfit);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// the user has ordered the deletion of a position
        /// пользователь заказал удаление позиции
        /// </summary>
        void PositionClearDelete_Click(object sender, EventArgs e)
        {
            try
            {
                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Journal.Message3);
                ui.ShowDialog();

                if (ui.UserAcceptActioin == false)
                {
                    return;
                }

                int number;
                try
                {
                    if(_gridOpenDeal.CurrentCell == null)
                    {
                        return;
                    }
                    number = Convert.ToInt32(_gridOpenDeal.Rows[_gridOpenDeal.CurrentCell.RowIndex].Cells[0].Value);
                }
                catch (Exception)
                {
                    return;
                }

                if (UserSelectActionEvent != null)
                {
                    UserSelectActionEvent(GetPositionForNumber(number), SignalType.DeletePos);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public event Action<Position, SignalType> UserSelectActionEvent;

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
