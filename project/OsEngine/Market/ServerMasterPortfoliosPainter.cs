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
using OsEngine.Market.Servers.Transaq.TransaqEntity;
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
            if(_hostPortfolio.Dispatcher.CheckAccess() == false)
            {
                _hostPortfolio.Dispatcher.Invoke(new Action(StartPaint));
                return;
            }

            try
            {
                _hostPortfolio.Child = _gridPortfolio;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void StopPaint()
        {
            _hostPortfolio.Child = null;
        }

      
        public void SetHostTable(WindowsFormsHost hostPortfolio)
        {
            try
            {
                if(_gridPortfolio == null)
                {
                    _gridPortfolio = DataGridFactory.GetDataGridPortfolios();
                    _gridPortfolio.CellClick += _gridPortfolio_CellClick;
                }

                _hostPortfolio = hostPortfolio;
                _hostPortfolio.Child = _gridPortfolio;
                _hostPortfolio.Child.Show();
                _hostPortfolio.Child.Refresh();
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
        private DataGridView _gridPortfolio;

        /// <summary>
        /// area for drawing portfolios
        /// область для прорисовки портфелей
        /// </summary>
        private WindowsFormsHost _hostPortfolio;

        /// <summary>
        /// redraw the portfolio table
        /// перерисовать таблицу портфелей
        /// </summary>
        private void RePaintPortfolio()
        {
            try
            {
                if (_hostPortfolio.Child == null)
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


                if (!_hostPortfolio.CheckAccess())
                {
                    _hostPortfolio.Dispatcher.Invoke(RePaintPortfolio);
                    return;
                }

                if (_portfolios == null)
                {
                    _gridPortfolio.Rows.Clear();
                    return;
                }

                int curUpRow = 0;
                int curSelectRow = 0;

                if (_gridPortfolio.RowCount != 0)
                {
                    curUpRow = _gridPortfolio.FirstDisplayedScrollingRowIndex;
                }

                if (_gridPortfolio.SelectedRows.Count != 0)
                {
                    curSelectRow = _gridPortfolio.SelectedRows[0].Index;
                }

                _gridPortfolio.Rows.Clear();

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
                   _gridPortfolio.FirstDisplayedScrollingRowIndex = curUpRow;
               }

               if (curSelectRow != 0 &&
                   _gridPortfolio.Rows.Count > curSelectRow
                   && curSelectRow != -1)
               {
                   _gridPortfolio.Rows[curSelectRow].Selected = true;
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

                _gridPortfolio.Rows.Add(secondRow);

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

                    _gridPortfolio.Rows.Add(nRow);
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

                        if(HaveClosePosButton(portfolio, positionsOnBoard[i]))
                        {
                            nRow.Cells.Add(new DataGridViewButtonCell());
                            nRow.Cells[8].Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                            nRow.Cells[8].Value = OsLocalization.Market.Label82;
                        }

                        _gridPortfolio.Rows.Add(nRow);
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

                        _gridPortfolio.Rows.Add(nRow);
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

        #region кнопка удалить позицию

        private bool HaveClosePosButton(Portfolio portfolio, PositionOnBoard positionOnBoard)
        {
            IServer myServer = GetServerByPortfolioName(portfolio.Number);

            if (myServer == null)
            {
                return false;
            }

            if (myServer.ServerType == ServerType.Tester)
            {
                return true;
            }

            if(myServer.ServerType == ServerType.BinanceFutures)
            {
                if(positionOnBoard.SecurityNameCode.EndsWith("_LONG")
                    || positionOnBoard.SecurityNameCode.EndsWith("_SHORT")
                    || positionOnBoard.SecurityNameCode.EndsWith("_BOTH"))
                {
                    return true;
                }
            }

            if (myServer.ServerType == ServerType.BitGetFutures)
            {
                if (positionOnBoard.SecurityNameCode.EndsWith("_long")
                    || positionOnBoard.SecurityNameCode.EndsWith("_short")
                    || positionOnBoard.SecurityNameCode.EndsWith("_both"))
                {
                    return true;
                }
            }

            return false;
        }

        private string TrimmSecName(string secName, IServer server)
        {
            string trueNameSec = secName;

            if(server.ServerType == ServerType.Tester)
            {
                return trueNameSec;
            }
            if(server.ServerType == ServerType.BinanceFutures)
            {
                trueNameSec = trueNameSec.Replace("_LONG", "");
                trueNameSec = trueNameSec.Replace("_SHORT", "");
                trueNameSec = trueNameSec.Replace("_BOTH", "");
            }

            if (server.ServerType == ServerType.BitGetFutures)
            {
                trueNameSec = trueNameSec.Replace("_long", "");
                trueNameSec = trueNameSec.Replace("_short", "");
                trueNameSec = trueNameSec.Replace("_both", "");
            }

            return trueNameSec;
        }

        private IServer GetServerByPortfolioName(string portfolioName)
        {
            List<IServer> servers = ServerMaster.GetServers();

            IServer myServer = null;

            for (int i = 0; servers != null && i < servers.Count; i++)
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

                    List<Portfolio> portfolios = servers[i].Portfolios;

                    for (int j = 0; portfolios != null && j < portfolios.Count; j++)
                    {
                        if (portfolios[j].Number == portfolioName)
                        {
                            myServer = servers[i];
                            break;
                        }
                    }

                    if (myServer != null)
                    {
                        break;
                    }
                }
                catch
                {
                    // ignore
                }
            }

            return myServer;
        }

        private void _gridPortfolio_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            int rowInd = e.RowIndex;
            int colInd = e.ColumnIndex;

            if(colInd != 8)
            {
                return;
            }

            if (_gridPortfolio.Rows[rowInd].Cells.Count < 9 ||
                _gridPortfolio.Rows[rowInd].Cells[colInd] == null ||
                _gridPortfolio.Rows[rowInd].Cells[colInd].Value == null ||
                _gridPortfolio.Rows[rowInd].Cells[colInd].Value.ToString() != OsLocalization.Market.Label82)
            {
                return;
            }

            string secName = _gridPortfolio.Rows[rowInd].Cells[4].Value.ToString();

            if (String.IsNullOrEmpty(secName))
            {
                return;
            }

            string secVol = _gridPortfolio.Rows[rowInd].Cells[6].Value.ToString();

            AcceptDialogUi ui = new AcceptDialogUi( secName + OsLocalization.Market.Label83);

            ui.ShowDialog();

            if(ui.UserAcceptActioin == false)
            {
                return;
            }

            string portfolioName = "";

            for(int i = rowInd; i >= 0; i--)
            {
                if(_gridPortfolio.Rows[i].Cells[0] == null)
                {
                    continue;
                }
                if (_gridPortfolio.Rows[i].Cells[0].Value == null)
                {
                    continue;
                }
                if (_gridPortfolio.Rows[i].Cells[0].Value.ToString() == "")
                {
                    continue;
                }

                portfolioName = _gridPortfolio.Rows[i].Cells[0].Value.ToString();
                break;
            }

            IServer myServer = GetServerByPortfolioName(portfolioName);

            if(myServer == null)
            {
                return;
            }

            string trimmedSecName = TrimmSecName(secName, myServer);


            if (ClearPositionOnBoardEvent != null)
            {
                ClearPositionOnBoardEvent(trimmedSecName, myServer, secName);
            }
        }

        public event Action<string, IServer, string> ClearPositionOnBoardEvent;

        #endregion

        // log message

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
