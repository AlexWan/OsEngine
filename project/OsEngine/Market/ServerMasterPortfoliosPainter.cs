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
using System.Linq;

namespace OsEngine.Market
{

    /// <summary>
    /// class responsible for drawing all portfolios and all orders open for current session on deployed servers
    /// </summary>
    public class ServerMasterPortfoliosPainter
    {
        #region Service

        /// <summary>
        /// constructor
        /// </summary>
        public ServerMasterPortfoliosPainter()
        {
            ServerMaster.ServerCreateEvent += ServerMaster_ServerCreateEvent;
            ServerMaster.ServerDeleteEvent += ServerMaster_ServerDeleteEvent;

            Task task = new Task(PainterThreadArea);
            task.Start();
        }

        private void ServerMaster_ServerDeleteEvent(IServer server)
        {
            try
            {
                server.PortfoliosChangeEvent -= _server_PortfoliosChangeEvent;

                lock (_lockerPortfolio)
                {
                    if (_portfolios == null || _portfolios.Count == 0)
                    {
                        return;
                    }

                    if (_portfolios == null)
                    {
                        _portfolios = new List<Portfolio>();
                    }

                    for (int i = 0; i < _portfolios.Count; i++)
                    {
                        if (_portfolios[i] == null)
                        {
                            continue;
                        }

                        if (_portfolios[i].ServerType == server.ServerType
                            && _portfolios[i].ServerUniqueName == server.ServerNameAndPrefix)
                        {
                            _portfolios.Remove(_portfolios[i]);
                            break;
                        }
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            _needToPaintPortfolio = true;
        }

        /// <summary>
        /// incoming events. a new server has been deployed in server-master
        /// </summary>
        private void ServerMaster_ServerCreateEvent(IServer server)
        {
            if (server.ServerType == ServerType.Optimizer)
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
        /// </summary>
        public void StartPaint()
        {
            if (_hostPortfolio.Dispatcher.CheckAccess() == false)
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

        /// <summary>
        /// stop drawing class control
        /// </summary>
        public void StopPaint()
        {
            _hostPortfolio.Child = null;
        }

        /// <summary>
        /// load a control for drawing into the object
        /// </summary>
        public void SetHostTable(WindowsFormsHost hostPortfolio)
        {
            try
            {
                if (_gridPortfolio == null)
                {
                    _gridPortfolio = DataGridFactory.GetDataGridPortfolios();
                    _gridPortfolio.CellClick += _gridPortfolio_CellClick;
                    _gridPortfolio.DataError += _gridPortfolio_DataError;
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

        private void _gridPortfolio_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            SendNewLogMessage(e.ToString(), LogMessageType.Error);
        }

        #endregion

        #region Work of the flow drawing portfolios and orders

        /// <summary>
        /// method in which the thread that draws controls works
        /// </summary>
        private async void PainterThreadArea()
        {
            while (true)
            {
                try
                {
                    await Task.Delay(5000);

                    if (MainWindow.ProccesIsWorked == false)
                    {
                        return;
                    }

                    if (_needToPaintPortfolio)
                    {
                        RePaintPortfolio();
                        _needToPaintPortfolio = false;
                    }
                }
                catch (Exception ex)
                {
                    SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                }
            }
        }

        /// <summary>
        /// shows whether state of the portfolio has changed and you need to redraw it
        /// </summary>
        private bool _needToPaintPortfolio;

        #endregion

        #region Portfolio drawing

        /// <summary>
        /// table for drawing portfolios
        /// </summary>
        private DataGridView _gridPortfolio;

        /// <summary>
        /// area for drawing portfolios
        /// </summary>
        private WindowsFormsHost _hostPortfolio;

        /// <summary>
        /// redraw the portfolio table
        /// </summary>
        private void RePaintPortfolio()
        {
            try
            {
                // 1 костыли

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
                        if (_portfolios[i] == null)
                        {
                            _portfolios.RemoveAt(i);
                            i--;
                            continue;
                        }

                        List<Portfolio> portfolios =
                            _portfolios.FindAll(p =>
                            p != null
                            && p.Number == _portfolios[i].Number
                            && p.ServerUniqueName == _portfolios[i].ServerUniqueName
                            );

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

                // 2 Sort

                if (_portfolios.Count > 1)
                {
                    _portfolios = _portfolios.OrderBy(x => x.ServerUniqueName).ToList();
                }

                // 3 Paint

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

                for (int i = 0; _portfolios != null && i < _portfolios.Count; i++)
                {
                    try
                    {
                        PaintPortfolio(_portfolios[i]);
                    }
                    catch
                    {
                        // ignore
                    }
                }

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
                secondRow.Cells[secondRow.Cells.Count - 1].Value = portfolio.ServerUniqueName;

                secondRow.Cells.Add(new DataGridViewTextBoxCell());
                secondRow.Cells[secondRow.Cells.Count - 1].Value = portfolio.Number;

                secondRow.Cells.Add(new DataGridViewTextBoxCell());
                secondRow.Cells[secondRow.Cells.Count - 1].Value = portfolio.ValueBegin.ToStringWithNoEndZero().ToDecimal();

                secondRow.Cells.Add(new DataGridViewTextBoxCell());
                secondRow.Cells[secondRow.Cells.Count - 1].Value = portfolio.ValueCurrent.ToStringWithNoEndZero().ToDecimal();

                secondRow.Cells.Add(new DataGridViewTextBoxCell());
                secondRow.Cells[secondRow.Cells.Count - 1].Value = portfolio.ValueBlocked.ToStringWithNoEndZero().ToDecimal();

                secondRow.Cells.Add(new DataGridViewTextBoxCell());
                secondRow.Cells[secondRow.Cells.Count - 1].Value = portfolio.UnrealizedPnl.ToStringWithNoEndZero().ToDecimal();

                if (portfolio.ServerType != ServerType.None)
                {
                    secondRow.Cells.Add(new DataGridViewTextBoxCell());
                    secondRow.Cells.Add(new DataGridViewTextBoxCell());
                    secondRow.Cells.Add(new DataGridViewTextBoxCell());
                    secondRow.Cells.Add(new DataGridViewTextBoxCell());
                    secondRow.Cells.Add(new DataGridViewTextBoxCell());

                    secondRow.Cells.Add(new DataGridViewButtonCell());
                    secondRow.Cells[secondRow.Cells.Count - 1].Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    secondRow.Cells[secondRow.Cells.Count - 1].Value = OsLocalization.Market.Label135;
                }

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
                        nRow.Cells.Add(new DataGridViewTextBoxCell());

                        nRow.Cells.Add(new DataGridViewTextBoxCell());
                        nRow.Cells[nRow.Cells.Count - 1].Value = positionsOnBoard[i].SecurityNameCode;

                        nRow.Cells.Add(new DataGridViewTextBoxCell());
                        nRow.Cells[nRow.Cells.Count - 1].Value = positionsOnBoard[i].ValueBegin.ToStringWithNoEndZero().ToDecimal();

                        nRow.Cells.Add(new DataGridViewTextBoxCell());
                        nRow.Cells[nRow.Cells.Count - 1].Value = positionsOnBoard[i].ValueCurrent.ToStringWithNoEndZero().ToDecimal();

                        nRow.Cells.Add(new DataGridViewTextBoxCell());
                        nRow.Cells[nRow.Cells.Count - 1].Value = positionsOnBoard[i].ValueBlocked.ToStringWithNoEndZero().ToDecimal();

                        nRow.Cells.Add(new DataGridViewTextBoxCell());
                        nRow.Cells[nRow.Cells.Count - 1].Value = positionsOnBoard[i].UnrealizedPnl.ToStringWithNoEndZero().ToDecimal();

                        if (HaveClosePosButton(portfolio, positionsOnBoard[i]))
                        {
                            nRow.Cells.Add(new DataGridViewButtonCell());
                            nRow.Cells[nRow.Cells.Count - 1].Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                            nRow.Cells[nRow.Cells.Count - 1].Value = OsLocalization.Market.Label82;
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
        /// </summary>
        private List<Portfolio> _portfolios;

        /// <summary>
        /// multi-thread locker to portfolios
        /// </summary>
        private string _lockerPortfolio = "portfolio_locker";

        /// <summary>
        /// portfolios changed in the server
        /// </summary>
        private void _server_PortfoliosChangeEvent(List<Portfolio> portfolios)
        {
            try
            {
                lock (_lockerPortfolio)
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

                        string currentServerName = portfolios[i].ServerUniqueName;

                        if (currentServerName == null)
                        {
                            currentServerName = "";
                        }

                        if (currentServerName.Split('_').Length == 3)
                        {
                            currentServerName = currentServerName.Split('_')[0] + "_" + currentServerName.Split('_')[1];
                        }

                        Portfolio portf = _portfolios.Find(
                            portfolio =>
                            portfolio != null
                            && portfolio.Number == portfolios[i].Number
                            && portfolio.ServerUniqueName.Contains(portfolios[i].ServerUniqueName)
                            );

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
            _needToPaintPortfolio = true;
        }

        #endregion

        #region Delete a position on the exchange at the press of a button from the interface

        /// <summary>
        /// Whether closing positions is allowed for this exchange
        /// </summary>
        private bool HaveClosePosButton(Portfolio portfolio, PositionOnBoard positionOnBoard)
        {
            IServer myServer = GetServerByPortfolioName(portfolio.Number, portfolio.ServerUniqueName);

            if (myServer == null)
            {
                return false;
            }

            if (myServer.ServerType == ServerType.Tester)
            {
                return true;
            }

            IServerPermission permission = ServerMaster.GetServerPermission(myServer.ServerType);

            if (permission != null)
            {
                if (permission.ManuallyClosePositionOnBoard_IsOn == false)
                {
                    return false;
                }

                string[] exceptionValues = permission.ManuallyClosePositionOnBoard_ExceptionPositionNames;

                for (int i = 0; exceptionValues != null && i < exceptionValues.Length; i++)
                {
                    string curName = exceptionValues[i];

                    if (positionOnBoard.SecurityNameCode.Equals(curName))
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// trim the name of the security to what it looks like on the stock exchange
        /// </summary>
        private string TrimmSecName(string secName, IServer server)
        {
            string trueNameSec = secName;

            if (server.ServerType == ServerType.Tester)
            {
                return trueNameSec;
            }

            IServerPermission permission = ServerMaster.GetServerPermission(server.ServerType);


            if (permission != null)
            {
                string[] trimValues = permission.ManuallyClosePositionOnBoard_ValuesForTrimmingName;

                for (int i = 0; trimValues != null && i < trimValues.Length; i++)
                {
                    string value = trimValues[i];

                    if (string.IsNullOrEmpty(value))
                    {
                        continue;
                    }

                    trueNameSec = trueNameSec.Replace(value, "");
                }
            }

            return trueNameSec;
        }

        /// <summary>
        /// server by portfolio name
        /// </summary>
        private IServer GetServerByPortfolioName(string portfolioName, string serverName)
        {
            List<IServer> servers = ServerMaster.GetServers();

            if (serverName.Split('_').Length == 3)
            {
                string newName = serverName.Split('_')[0] + "_" + serverName.Split('_')[1];

                serverName = newName;
            }


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

                    if (servers[i].ServerNameAndPrefix.Contains(serverName) == false)
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

        /// <summary>
        /// the user clicked on the table of positions on the exchange
        /// </summary>
        private void _gridPortfolio_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                int rowInd = e.RowIndex;
                int colInd = e.ColumnIndex;

                if (colInd != 11)
                {
                    return;
                }

                if (_gridPortfolio.Rows[rowInd].Cells.Count < 10 ||
                    _gridPortfolio.Rows[rowInd].Cells[colInd] == null ||
                    _gridPortfolio.Rows[rowInd].Cells[colInd].Value == null)
                {
                    return;
                }

                if (_gridPortfolio.Rows[rowInd].Cells[colInd].Value.ToString() == OsLocalization.Market.Label82)
                {
                    // закрытие позиции на бирже

                    ClosePositionOnBoardClick(sender, e);
                }
                else if (_gridPortfolio.Rows[rowInd].Cells[colInd].Value.ToString() == OsLocalization.Market.Label135)
                {
                    // вызывать окно сравнения позиций в роботе и в журнале
                    ShowPositionsCompareUi(sender, e);
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ShowPositionsCompareUi(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                int rowInd = e.RowIndex;
                int colInd = e.ColumnIndex;

                string portfolioName = _gridPortfolio.Rows[rowInd].Cells[1].Value.ToString();

                string serverUniqueName = _gridPortfolio.Rows[rowInd].Cells[0].Value.ToString();

                IServer myServer = GetServerByPortfolioName(portfolioName, serverUniqueName);

                if (myServer == null)
                {
                    return;
                }

                AServer aServer = null;

                try
                {
                    aServer = (AServer)myServer;
                }
                catch
                {
                    return;
                }

                aServer.ShowComparePositionsModuleDialog(portfolioName);
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ClosePositionOnBoardClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                int rowInd = e.RowIndex;
                int colInd = e.ColumnIndex;

                string secName = _gridPortfolio.Rows[rowInd].Cells[6].Value.ToString();

                if (String.IsNullOrEmpty(secName))
                {
                    return;
                }

                string secVol = _gridPortfolio.Rows[rowInd].Cells[8].Value.ToString();

                AcceptDialogUi ui = new AcceptDialogUi(secName + OsLocalization.Market.Label83);

                ui.ShowDialog();

                if (ui.UserAcceptAction == false)
                {
                    return;
                }

                string portfolioName = "";
                string serverName = "";

                for (int i = rowInd; i >= 0; i--)
                {
                    if (_gridPortfolio.Rows[i].Cells[1] == null
                        || _gridPortfolio.Rows[i].Cells[0] == null)
                    {
                        continue;
                    }
                    if (_gridPortfolio.Rows[i].Cells[1].Value == null
                        || _gridPortfolio.Rows[i].Cells[0].Value == null)
                    {
                        continue;
                    }
                    if (_gridPortfolio.Rows[i].Cells[1].Value.ToString() == ""
                        || _gridPortfolio.Rows[i].Cells[0].Value.ToString() == "")
                    {
                        continue;
                    }

                    portfolioName = _gridPortfolio.Rows[i].Cells[1].Value.ToString();
                    serverName = _gridPortfolio.Rows[i].Cells[0].Value.ToString();
                    break;
                }

                IServer myServer = GetServerByPortfolioName(portfolioName, serverName);

                if (myServer == null)
                {
                    return;
                }

                string trimmedSecName = TrimmSecName(secName, myServer);

                if (ClearPositionOnBoardEvent != null)
                {
                    Task.Run(() => ClearPositionOnBoardEvent(trimmedSecName, myServer, secName));
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// outgoing event: it is necessary to close a position on the stock exchange
        /// </summary>
        public event Action<string, IServer, string> ClearPositionOnBoardEvent;

        #endregion

        #region Log

        /// <summary>
        /// send a new message to up
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
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }
}