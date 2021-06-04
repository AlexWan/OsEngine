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
                    servers[i].NewOrderIncomeEvent -= _server_NewOrderIncomeEvent;
                    servers[i].NewMyTradeEvent -= serv_NewMyTradeEvent;

                    servers[i].PortfoliosChangeEvent += _server_PortfoliosChangeEvent;
                    servers[i].NewOrderIncomeEvent += _server_NewOrderIncomeEvent;
                    servers[i].NewMyTradeEvent += serv_NewMyTradeEvent;
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
            try
            {
                _positionHost.Child = _gridPosition;
                _ordersHost.Child = _gridOrders;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// stop drawing class control
        /// остановить прорисовку контролов класса 
        /// </summary>
        public void StopPaint()
        {
            _positionHost.Child = null;
            _ordersHost.Child = null;
        }

        /// <summary>
        /// add items for drawing portfolios and orders
        /// добавить элементы, на котором будут прорисовываться портфели и ордера
        /// </summary>
        public void SetHostTable(WindowsFormsHost hostPortfolio, WindowsFormsHost hostOrders)
        {
            try
            {
                _gridPosition = DataGridFactory.GetDataGridPortfolios();

                _positionHost = hostPortfolio;
                _positionHost.Child = _gridPosition;
                _positionHost.Child.Show();
                _positionHost.Child.Refresh();

                _gridOrders = DataGridFactory.GetDataGridOrder();
                _ordersHost = hostOrders;
                _ordersHost.Child = _gridOrders;
                _gridOrders.Click += _gridOrders_Click;
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

// work of thread that draws portfolios and orders
// работа потока прорисовывающего портфели и ордера

        private async void PainterThreadArea()
        {
            while (true)
            {
               await Task.Delay(1000);

                if (MainWindow.ProccesIsWorked == false)
                {
                    return;
                }

                if (_needToPaintOrders)
                {
                    _needToPaintOrders = false;
                    PaintOrders();
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

        /// <summary>
        /// shows whether orders have changed on the exchange and you need to redraw
        /// флаг, означающий что ордера на бирже изменились и нужно их перерисовать
        /// </summary>
        private bool _needToPaintOrders;

// portfolio drawing
// прорисовка портфеля

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

// orders
// ордера

        /// <summary>
        /// table for drawing orders
        /// таблица для прорисовки ордеров
        /// </summary>
        private DataGridView _gridOrders;

        /// <summary>
        /// area for drawing orders
        /// область для прорисовки ордеров
        /// </summary>
        private WindowsFormsHost _ordersHost;

        private object _lockerOrders = new Object();

        /// <summary>
        /// new order on the server
        /// новый ордер в сервере
        /// </summary>
        private void _server_NewOrderIncomeEvent(Order order)
        {
            if (order.ServerType == ServerType.Tester ||
                order.ServerType == ServerType.Optimizer ||
                order.ServerType == ServerType.Miner)
            {
                return;
            }

            try
            {
                if (_orders == null)
                {
                    _orders = new List<Order>();
                }

                lock (_lockerOrders)
                {
                    Order myOrder = _orders.Find(order1 => order1.NumberUser == order.NumberUser);

                    if (myOrder == null)
                    {
                        _orders.Add(order);
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(order.NumberMarket))
                        {
                            myOrder.NumberMarket = order.NumberMarket;
                        }

                        if (order.Price != 0)
                        {
                            myOrder.Price = order.Price;
                        }

                        if (order.Side != Side.None)
                        {
                            myOrder.Side = order.Side;
                        }

                        if (!string.IsNullOrWhiteSpace(order.PortfolioNumber))
                        {
                            myOrder.PortfolioNumber = order.PortfolioNumber;
                        }

                        if (order.Volume != 0)
                        {
                            myOrder.Volume = order.Volume;
                        }

                        if (order.VolumeExecute != 0)
                        {
                            myOrder.VolumeExecute = order.VolumeExecute;
                        }

                        if (order.State != OrderStateType.None)
                        {
                            myOrder.State = order.State;
                        }

                        if (string.IsNullOrWhiteSpace(myOrder.SecurityNameCode))
                        {
                            myOrder.SecurityNameCode = order.SecurityNameCode;
                        }
                        if (myOrder.TimeCallBack == DateTime.MinValue)
                        {
                            myOrder.TimeCallBack = order.TimeCallBack;
                        }
                    }
                    if (_orders.Count > 1000)
                    {
                        _orders.RemoveAt(0);
                    }
                }
            }
            catch (Exception error)
            {
                _orders.Clear();
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            _needToPaintOrders = true;
        }

        private object _lockerTrades = new Object();

        /// <summary>
        /// my new trade on the server
        /// новый мой трейд в сервере
        /// </summary>
        /// <param name="trade"></param>
        private void serv_NewMyTradeEvent(MyTrade trade)
        {
            if (_orders == null || _orders.Count == 0)
            {
                return;
            }

            lock (_lockerTrades)
            {
                Order myOrder = _orders.Find(order1 => order1.NumberMarket == trade.NumberOrderParent);

                if (myOrder == null)
                {
                    return;
                }

                if (myOrder.ServerType == ServerType.Tester ||
                    myOrder.ServerType == ServerType.Optimizer ||
                    myOrder.ServerType == ServerType.Miner)
                {
                    return;
                }

                _orders.Remove(myOrder);
                _needToPaintOrders = true;
            }
        }

        /// <summary>
        /// all orders
        /// все ордера
        /// </summary>
        private List<Order> _orders;

        /// <summary>
        /// draw orders
        /// прорисовать ордера
        /// </summary>
        private void PaintOrders()
        {
            try
            {
                if (_positionHost.Child == null)
                {
                    return;
                }

                if (!_positionHost.CheckAccess())
                {
                    _positionHost.Dispatcher.Invoke((PaintOrders));
                    return;
                }
                _gridOrders.Rows.Clear();

                if (_orders == null)
                {
                    return;
                }

                for (int i = _orders.Count - 1; _orders != null && _orders.Count != 0 && i > -1; i--)
                {
                    if ((_orders[i].State != OrderStateType.Activ &&
                        _orders[i].State != OrderStateType.Pending)
                      || _orders[i].Side == Side.None)
                    {
                        continue;
                    }

                    DataGridViewRow nRow = new DataGridViewRow();

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[0].Value = _orders[i].NumberUser;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[1].Value = _orders[i].NumberMarket;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[2].Value = _orders[i].TimeCreate;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[3].Value = _orders[i].SecurityNameCode;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[4].Value = _orders[i].PortfolioNumber;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[5].Value = _orders[i].Side;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[6].Value = _orders[i].State;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[7].Value = _orders[i].Price.ToStringWithNoEndZero();

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[8].Value = _orders[i].PriceReal.ToStringWithNoEndZero();

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[9].Value = _orders[i].Volume.ToStringWithNoEndZero();

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[10].Value = _orders[i].TypeOrder;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[11].Value = _orders[i].TimeRoundTrip;

                    _gridOrders.Rows.Add(nRow);
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

// user clicks the popup menu       
// пользователь кликает по всплывающему меню

        /// <summary>
        /// user clicked the table with all orders
        /// пользователь кликнул на таблицу всех ордеров
        /// </summary>
        private void _gridOrders_Click(object sender, EventArgs e)
        {
            try
            {
                MouseEventArgs mouse = (MouseEventArgs)e;
                if (mouse.Button != MouseButtons.Right)
                {
                    return;
                }

                MenuItem[] items = new MenuItem[2];

                items[0] = new MenuItem { Text = OsLocalization.Market.Message4 };

                items[0].Click += OrdersCloseAll_Click;

                items[1] = new MenuItem { Text = OsLocalization.Market.Message5 };
                items[1].Click += PositionCloseForNumber_Click;

                ContextMenu menu = new ContextMenu(items);

                _gridOrders.ContextMenu = menu;
                _gridOrders.ContextMenu.Show(_gridOrders, new Point(mouse.X, mouse.Y));
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// clear order list
        /// очистить список ордеров
        /// </summary>
        public void ClearOrders()
        {
            _orders = new List<Order>();
        }

        /// <summary>
        /// user requested closing all orders
        /// пользователь запросил закрытие всех ордеров
        /// </summary>
        private void OrdersCloseAll_Click(object sender, EventArgs e)
        {
            try
            {
                if (_orders == null || _orders.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < _orders.Count; i++)
                {
                    if (_orders[i].State == OrderStateType.Activ &&
                        !string.IsNullOrEmpty(_orders[i].PortfolioNumber))
                    {
                        IServer server = ServerMaster.GetServers().Find(server1 => server1.ServerType == _orders[i].ServerType);
                        if (server != null)
                        {
                            server.CancelOrder(_orders[i]);
                        }
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// user requested closing order by number
        /// пользователь запросил закрытие ордера по номеру
        /// </summary>
        private void PositionCloseForNumber_Click(object sender, EventArgs e)
        {
            try
            {
                if (_orders == null || _orders.Count == 0)
                {
                    return;
                }

                Order order = _orders[(_orders.Count - 1 - _gridOrders.CurrentCell.RowIndex)];

                if ((order.State == OrderStateType.Activ || order.State == OrderStateType.Pending)
                    &&
                        !string.IsNullOrEmpty(order.PortfolioNumber))
                {
                    IServer server = ServerMaster.GetServers().Find(server1 => server1.ServerType == order.ServerType);
                    if (server != null)
                    {
                        server.CancelOrder(order);
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

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
