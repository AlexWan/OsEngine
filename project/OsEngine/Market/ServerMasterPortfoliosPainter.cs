/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers;
using Point = System.Drawing.Point;

namespace OsEngine.Market
{

    /// <summary>
    /// class responsible for drawing all portfolios and all orders open for current session on deployed servers
    /// класс отвечающий за прорисовку всех портфелей и всех ордеров открытых за текущую сессию на развёрнутых серверах
    /// </summary>
    public class ServerMasterPortfoliosPainter
    {
        public ServerMasterPortfoliosPainter()
        {
            ServerMaster.ServerCreateEvent += ServerMaster_ServerCreateEvent;

            Task task = new Task(PainterThreadArea);
            task.Start();
        }

        /// <summary>
        /// incoming events. a new server has been deployed in server-master
        /// входящее событие. В сервермастере был развёрнут новый сервер
        /// </summary>
        private void ServerMaster_ServerCreateEvent(IServer server)
        {
            if(server.ServerType == ServerType.Optimizer)
            {
                return;
            }

            List<IServer> servers = ServerMaster.GetServers();

            for (int i = 0; i < servers.Count; i++)
            {
                try
                {
                    if (servers[i] == null)
                    {
                        continue;
                    }
                    if (servers[i].ServerType == ServerType.Optimizer)
                    {
                        continue;
                    }
                    servers[i].PortfoliosChangeEvent -= _server_PortfoliosChangeEvent;
                    servers[i].PortfoliosChangeEvent += _server_PortfoliosChangeEvent;

                }
                catch
                {
                    // ignore
                }

            }
        }

        /// <summary>
        /// start drawing class control
        /// начать прорисовывать контролы класса 
        /// </summary>
        public void StartPaint()
        {
            if(_positionHost.Dispatcher.CheckAccess() == false)
            {
                _positionHost.Dispatcher.Invoke(new Action(StartPaint));
                return;
            }

            try
            {
                _positionHost.Child = _gridPosition;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void StopPaint()
        {
            _positionHost.Child = null;
        }

      
        public void SetHostTable(WindowsFormsHost hostPortfolio)
        {
            try
            {
                _gridPosition = DataGridFactory.GetDataGridPortfolios();

                _positionHost = hostPortfolio;
                _positionHost.Child = _gridPosition;
                _positionHost.Child.Show();
                _positionHost.Child.Refresh();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// multi-thread locker to portfolios
        /// блокиратор многопоточного доступа к портфелям
        /// </summary>
        private object lockerPortfolio = new object();

        /// <summary>
        /// portfolios changed in the server
        /// в сервере изменились портфели
        /// </summary>
        /// <param name="portfolios">портфели</param>
        private void _server_PortfoliosChangeEvent(List<Portfolio> portfolios)
        {
            try
            {
                lock (lockerPortfolio)
                {
                    if (portfolios == null || portfolios.Count == 0)
                    {
                        return;
                    }

                    if (_portfolios == null)
                    {
                        _portfolios = new List<Portfolio>();
                    }

                    for (int i = 0; i < portfolios.Count; i++)
                    {
                        if (portfolios[i] == null)
                        {
                            continue;
                        }

                        Portfolio portf = _portfolios.Find(
                            portfolio => portfolio != null && portfolio.Number == portfolios[i].Number);

                        if (portf != null)
                        {
                            _portfolios.Remove(portf);
                        }

                        _portfolios.Add(portfolios[i]);
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            _neadToPaintPortfolio = true;
        }

        #region работа потока прорисовывающего портфели и ордера

        private async void PainterThreadArea()
        {
            while (true)
            {
               await Task.Delay(5000);

                if (MainWindow.ProccesIsWorked == false)
                {
                    return;
                }

                if (_neadToPaintPortfolio)
                {
                    RePaintPortfolio();
                    _neadToPaintPortfolio = false;
                }
            }
        }

        /// <summary>
        /// shows whether state of the portfolio has changed and you need to redraw it
        /// флаг, означающий что состояние портфеля изменилось и нужно его перерисовать
        /// </summary>
        private bool _neadToPaintPortfolio;

        #endregion

        #region прорисовка портфеля

        /// <summary>
        /// table for drawing portfolios
        /// таблица для прорисовки портфелей
        /// </summary>
        private DataGridView _gridPosition;

        /// <summary>
        /// area for drawing portfolios
        /// область для прорисовки портфелей
        /// </summary>
        private WindowsFormsHost _positionHost;

        /// <summary>
        /// redraw the portfolio table
        /// перерисовать таблицу портфелей
        /// </summary>
        private void RePaintPortfolio()
        {
            try
            {
                if (_positionHost.Child == null)
                {
                    return;
                }

                if (_portfolios == null)
                {
                    return;
                }

                try
                {
                    for (int i = 0; i < _portfolios.Count; i++)
                    {
                        List<Portfolio> portfolios =
                            _portfolios.FindAll(p => p.Number == _portfolios[i].Number);

                        if (portfolios.Count > 1)
                        {
                            _portfolios.RemoveAt(i);
                            break;
                        }
                    }
                }
                catch
                {
                    // ignore
                }

                while (_portfolios != null && _portfolios.Count > 250)
                {
                    _portfolios.RemoveAt(500);
                }

                for(int i = 0;i < _portfolios.Count;i++)
                {
                    Portfolio port = _portfolios[i];

                    if (port == null)
                    {
                        continue;
                    }
                    List<PositionOnBoard> poses = port.GetPositionOnBoard();
                    
                    while (poses != null &&
                        poses.Count > 500)
                    {
                        poses.RemoveAt(500);
                    }
                }


                if (!_positionHost.CheckAccess())
                {
                    _positionHost.Dispatcher.Invoke(RePaintPortfolio);
                    return;
                }

                if (_portfolios == null)
                {
                    _gridPosition.Rows.Clear();
                    return;
                }

                int curUpRow = 0;
                int curSelectRow = 0;

                if (_gridPosition.RowCount != 0)
                {
                    curUpRow = _gridPosition.FirstDisplayedScrollingRowIndex;
                }

                if (_gridPosition.SelectedRows.Count != 0)
                {
                    curSelectRow = _gridPosition.SelectedRows[0].Index;
                }

                _gridPosition.Rows.Clear();

                // send portfolios to draw
                // отправляем портфели на прорисовку
                for (int i = 0; _portfolios != null && i < _portfolios.Count; i++)
                {
                    try
                    {
                        PaintPortfolio(_portfolios[i]);
                    }
                    catch (Exception)
                    {
                        
                    }
                }

               /* int curUpRow = 0;
                int curSelectRow = 0;*/

               if (curUpRow != 0 && curUpRow != -1)
               {
                   _gridPosition.FirstDisplayedScrollingRowIndex = curUpRow;
               }

               if (curSelectRow != 0 &&
                   _gridPosition.Rows.Count > curSelectRow
                   && curSelectRow != -1)
               {
                   _gridPosition.Rows[curSelectRow].Selected = true;
               }

            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// draw portfolio
        /// прорисовать портфель
        /// </summary>
        private void PaintPortfolio(Portfolio portfolio)
        {
            try
            {
                if (portfolio.ValueBegin == 0
                    && portfolio.ValueCurrent == 0 
                    && portfolio.ValueBlocked == 0)
                {
                    List<PositionOnBoard> poses = portfolio.GetPositionOnBoard();

                    if (poses == null)
                    {
                        return;
                    }

                    bool haveNoneZeroPoses = false;

                    for (int i = 0; i < poses.Count; i++)
                    {
                        if (poses[i].ValueCurrent != 0)
                        {
                            haveNoneZeroPoses = true;
                            break;
                        }
                    }

                    if (haveNoneZeroPoses == false)
                    {
                        return;
                    }
                }

                DataGridViewRow secondRow = new DataGridViewRow();
                secondRow.Cells.Add(new DataGridViewTextBoxCell());
                secondRow.Cells[0].Value = portfolio.Number;

                secondRow.Cells.Add(new DataGridViewTextBoxCell());
                secondRow.Cells[1].Value = portfolio.ValueBegin.ToString().ToDecimal();

                secondRow.Cells.Add(new DataGridViewTextBoxCell());
                secondRow.Cells[2].Value = portfolio.ValueCurrent.ToString().ToDecimal();

                secondRow.Cells.Add(new DataGridViewTextBoxCell());
                secondRow.Cells[3].Value = portfolio.ValueBlocked.ToString().ToDecimal();

                _gridPosition.Rows.Add(secondRow);

                List<PositionOnBoard> positionsOnBoard = portfolio.GetPositionOnBoard();

                if (positionsOnBoard == null || positionsOnBoard.Count == 0)
                {
                    DataGridViewRow nRow = new DataGridViewRow();
                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[nRow.Cells.Count - 1].Value = "No positions";

                    _gridPosition.Rows.Add(nRow);
                }
                else
                {
                    bool havePoses = false;

                    for (int i = 0; i < positionsOnBoard.Count; i++)
                    {
                        PositionOnBoard pos = positionsOnBoard[i];

                        if (positionsOnBoard[i].ValueBegin == 0 &&
                            positionsOnBoard[i].ValueCurrent == 0 &&
                            positionsOnBoard[i].ValueBlocked == 0)
                        {
                            continue;
                        }

                        havePoses = true;
                        DataGridViewRow nRow = new DataGridViewRow();
                        nRow.Cells.Add(new DataGridViewTextBoxCell());
                        nRow.Cells.Add(new DataGridViewTextBoxCell());
                        nRow.Cells.Add(new DataGridViewTextBoxCell());
                        nRow.Cells.Add(new DataGridViewTextBoxCell());

                        nRow.Cells.Add(new DataGridViewTextBoxCell());
                        nRow.Cells[4].Value = positionsOnBoard[i].SecurityNameCode;

                        nRow.Cells.Add(new DataGridViewTextBoxCell());
                        nRow.Cells[5].Value = positionsOnBoard[i].ValueBegin.ToString().ToDecimal();

                        nRow.Cells.Add(new DataGridViewTextBoxCell());
                        nRow.Cells[6].Value = positionsOnBoard[i].ValueCurrent.ToString().ToDecimal();

                        nRow.Cells.Add(new DataGridViewTextBoxCell());
                        nRow.Cells[7].Value = positionsOnBoard[i].ValueBlocked.ToString().ToDecimal();

                        _gridPosition.Rows.Add(nRow);
                    }

                    if (havePoses == false)
                    {
                        DataGridViewRow nRow = new DataGridViewRow();
                        nRow.Cells.Add(new DataGridViewTextBoxCell());
                        nRow.Cells.Add(new DataGridViewTextBoxCell());
                        nRow.Cells.Add(new DataGridViewTextBoxCell());
                        nRow.Cells.Add(new DataGridViewTextBoxCell());
                        nRow.Cells.Add(new DataGridViewTextBoxCell());
                        nRow.Cells[nRow.Cells.Count - 1].Value = "No positions";

                        _gridPosition.Rows.Add(nRow);
                    }
                }
            }
            catch
            {   
                // ignore. Let us sometimes face with null-value, when deleting the original order or modification, but don't break work of mail thread
                // игнорим. Пусть иногда натыкаемся на налл, при удалении исходного ордера или модификации
                // зато не мешаем основному потоку работать
                //SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// all portfolios
        /// все портфели
        /// </summary>
        private List<Portfolio> _portfolios;

        #endregion


        // log message
        // сообщения в лог

        /// <summary>
        /// send a new message to up
        /// выслать новое сообщение на верх
        /// </summary>
        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            { // if nobody is substribed to us and there is a log error / если на нас никто не подписан и в логе ошибка
                MessageBox.Show(message);
            }
        }

        /// <summary>
        /// outgoing log message
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }
}
