using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using OsEngine.Entity;
using OsEngine.Logging;
using Point = System.Drawing.Point;

namespace OsEngine.Market.Servers
{
    /// <summary>
    /// класс отвечающий за прорисовку всех портфелей
    /// и всех ордеров открытых за текущую сессию на развёрнутых серверах
    /// </summary>
    public class ServerMasterPortfoliosPainter
    {
        public ServerMasterPortfoliosPainter()
        {
            ServerMaster.ServerCreateEvent += ServerMaster_ServerCreateEvent;

            Thread painter = new Thread(PainterThreadArea);
            painter.IsBackground = true;
            painter.Name = "ServerMasterPortfoliosPainterThread";
            painter.Start();
        }

        /// <summary>
        /// входящее событие. В сервермастере был развёрнут новый сервер
        /// </summary>
        private void ServerMaster_ServerCreateEvent()
        {
            List<IServer> servers = ServerMaster.GetServers();

            for (int i = 0; i < servers.Count; i++)
            {
                servers[i].PortfoliosChangeEvent -= _server_PortfoliosChangeEvent;
                servers[i].NewOrderIncomeEvent -= _server_NewOrderIncomeEvent;
                servers[i].NewMyTradeEvent -= serv_NewMyTradeEvent;

                servers[i].PortfoliosChangeEvent += _server_PortfoliosChangeEvent;
                servers[i].NewOrderIncomeEvent += _server_NewOrderIncomeEvent;
                servers[i].NewMyTradeEvent += serv_NewMyTradeEvent;
            }
        }

        /// <summary>
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
        /// остановить прорисовку контролов класса 
        /// </summary>
        public void StopPaint()
        {
            _positionHost.Child = null;
            _ordersHost.Child = null;
        }

        /// <summary>
        /// добавить элементы, на котором будут прорисовываться портфели и ордера
        /// </summary>
        public void SetHostTable(WindowsFormsHost hostPortfolio, WindowsFormsHost hostOrders)
        {
            try
            {
                _gridPosition = new DataGridView();
                _positionHost = hostPortfolio;
                _positionHost.Child = _gridPosition;

                _gridPosition.AllowUserToOrderColumns = false;
                _gridPosition.AllowUserToResizeRows = false;
                _gridPosition.AllowUserToDeleteRows = false;
                _gridPosition.AllowUserToAddRows = false;
                _gridPosition.RowHeadersVisible = false;
                _gridPosition.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                _gridPosition.MultiSelect = false;

                DataGridViewCellStyle style = new DataGridViewCellStyle();
                style.Alignment = DataGridViewContentAlignment.BottomRight;

                DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
                cell0.Style = style;

                DataGridViewColumn column0 = new DataGridViewColumn();
                column0.CellTemplate = cell0;
                column0.HeaderText = @"Портфель";
                column0.ReadOnly = true;
                column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                _gridPosition.Columns.Add(column0);

                DataGridViewColumn column = new DataGridViewColumn();
                column.CellTemplate = cell0;
                column.HeaderText = @"Средства входящие";
                column.ReadOnly = true;
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                _gridPosition.Columns.Add(column);

                DataGridViewColumn column1 = new DataGridViewColumn();
                column1.CellTemplate = cell0;
                column1.HeaderText = @"Средства сейчас";
                column1.ReadOnly = true;
                column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                _gridPosition.Columns.Add(column1);

                DataGridViewColumn column3 = new DataGridViewColumn();
                column3.CellTemplate = cell0;
                column3.HeaderText = @"Средства блок.";
                column3.ReadOnly = true;
                column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                _gridPosition.Columns.Add(column3);

                DataGridViewColumn column4 = new DataGridViewColumn();
                column4.CellTemplate = cell0;
                column4.HeaderText = @"Инструмент";
                column4.ReadOnly = true;
                column4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                _gridPosition.Columns.Add(column4);

                DataGridViewColumn column5 = new DataGridViewColumn();
                column5.CellTemplate = cell0;
                column5.HeaderText = @"Объём входящий";
                column5.ReadOnly = true;
                column5.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                _gridPosition.Columns.Add(column5);

                DataGridViewColumn column6 = new DataGridViewColumn();
                column6.CellTemplate = cell0;
                column6.HeaderText = @"Объём сейчас";
                column6.ReadOnly = true;
                column6.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                _gridPosition.Columns.Add(column6);

                DataGridViewColumn column7 = new DataGridViewColumn();
                column7.CellTemplate = cell0;
                column7.HeaderText = @"Объём блокирован";
                column7.ReadOnly = true;
                column7.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                _gridPosition.Columns.Add(column7);

                _positionHost.Child.Show();
                _positionHost.Child.Refresh();


                _gridOrders = new DataGridView();
                _ordersHost = hostOrders;
                _ordersHost.Child = _gridOrders;

                _gridOrders.AllowUserToOrderColumns = false;
                _gridOrders.AllowUserToResizeRows = false;
                _gridOrders.AllowUserToDeleteRows = false;
                _gridOrders.AllowUserToAddRows = false;
                _gridOrders.RowHeadersVisible = false;
                _gridOrders.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                _gridOrders.MultiSelect = false;

                DataGridViewColumn colu = new DataGridViewColumn();
                colu.CellTemplate = cell0;
                colu.HeaderText = @"Время";
                colu.ReadOnly = true;
                colu.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                _gridOrders.Columns.Add(colu);

                DataGridViewColumn colum1 = new DataGridViewColumn();
                colum1.CellTemplate = cell0;
                colum1.HeaderText = @"Инструмент";
                colum1.ReadOnly = true;
                colum1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                _gridOrders.Columns.Add(colum1);

                DataGridViewColumn colum2 = new DataGridViewColumn();
                colum2.CellTemplate = cell0;
                colum2.HeaderText = @"Направление";
                colum2.ReadOnly = true;
                colum2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                _gridOrders.Columns.Add(colum2);

                DataGridViewColumn colum3 = new DataGridViewColumn();
                colum3.CellTemplate = cell0;
                colum3.HeaderText = @"Статус";
                colum3.ReadOnly = true;
                colum3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                _gridOrders.Columns.Add(colum3);

                DataGridViewColumn colum4 = new DataGridViewColumn();
                colum4.CellTemplate = cell0;
                colum4.HeaderText = @"Цена";
                colum4.ReadOnly = true;
                colum4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                _gridOrders.Columns.Add(colum4);

                DataGridViewColumn colum5 = new DataGridViewColumn();
                colum5.CellTemplate = cell0;
                colum5.HeaderText = @"Объём";
                colum5.ReadOnly = true;
                colum5.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                _gridOrders.Columns.Add(colum5);

                DataGridViewColumn colum6 = new DataGridViewColumn();
                colum6.CellTemplate = cell0;
                colum6.HeaderText = @"Ожидает";
                colum6.ReadOnly = true;
                colum6.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                _gridOrders.Columns.Add(colum6);

                _gridOrders.Click += _gridOrders_Click;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// блокиратор многопоточного доступа к портфелям
        /// </summary>
        private object lockerPortfolio = new object();

        /// <summary>
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
                        Portfolio portf = _portfolios.Find(portfolio => portfolio.Number == portfolios[i].Number);

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

// работа потока прорисовывающего портфели и ордера

        private void PainterThreadArea()
        {
            while (true)
            {
               Thread.Sleep(1000);

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
        /// флаг, означающий что состояние портфеля изменилось и нужно его перерисовать
        /// </summary>
        private bool _neadToPaintPortfolio;

        /// <summary>
        /// флаг, означающий что ордера на бирже изменились и нужно их перерисовать
        /// </summary>
        private bool _needToPaintOrders;

// прорисовка портфеля

        /// <summary>
        /// таблица для прорисовки портфелей
        /// </summary>
        private DataGridView _gridPosition;

        /// <summary>
        /// область для прорисовки портфелей
        /// </summary>
        private WindowsFormsHost _positionHost;

        /// <summary>
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

                if (!_positionHost.CheckAccess())
                {
                    _positionHost.Dispatcher.Invoke(RePaintPortfolio);
                    return;
                }

                // очищаем старые данные с грида

                _gridPosition.Rows.Clear();

                if (_portfolios == null)
                {
                    return;
                }

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
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// прорисовать портфель
        /// </summary>
        private void PaintPortfolio(Portfolio portfolio)
        {
            try
            {
                DataGridViewRow secondRow = new DataGridViewRow();
                secondRow.Cells.Add(new DataGridViewTextBoxCell());
                secondRow.Cells[0].Value = portfolio.Number;

                secondRow.Cells.Add(new DataGridViewTextBoxCell());
                secondRow.Cells[1].Value = portfolio.ValueBegin;

                secondRow.Cells.Add(new DataGridViewTextBoxCell());
                secondRow.Cells[2].Value = portfolio.ValueCurrent;

                secondRow.Cells.Add(new DataGridViewTextBoxCell());
                secondRow.Cells[3].Value = portfolio.ValueBlocked;

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
                    nRow.Cells[nRow.Cells.Count - 1].Value = "Нет позиций";

                    _gridPosition.Rows.Add(nRow);
                }
                else
                {
                    for (int i = 0; i < positionsOnBoard.Count; i++)
                    {
                        DataGridViewRow nRow = new DataGridViewRow();
                        nRow.Cells.Add(new DataGridViewTextBoxCell());
                        nRow.Cells.Add(new DataGridViewTextBoxCell());
                        nRow.Cells.Add(new DataGridViewTextBoxCell());
                        nRow.Cells.Add(new DataGridViewTextBoxCell());

                        nRow.Cells.Add(new DataGridViewTextBoxCell());
                        nRow.Cells[4].Value = positionsOnBoard[i].SecurityNameCode;

                        nRow.Cells.Add(new DataGridViewTextBoxCell());
                        nRow.Cells[5].Value = positionsOnBoard[i].ValueBegin;

                        nRow.Cells.Add(new DataGridViewTextBoxCell());
                        nRow.Cells[6].Value = positionsOnBoard[i].ValueCurrent;

                        nRow.Cells.Add(new DataGridViewTextBoxCell());
                        nRow.Cells[7].Value = positionsOnBoard[i].ValueBlocked;

                        _gridPosition.Rows.Add(nRow);
                    }
                }
            }
            catch
            {   
                // игнорим. Пусть иногда натыкаемся на налл, при удалении исходного ордера или модификации
                // зато не мешаем основному потоку работать
                //SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// все портфели
        /// </summary>
        private List<Portfolio> _portfolios;

// ордера

        /// <summary>
        /// таблица для прорисовки ордеров
        /// </summary>
        private DataGridView _gridOrders;

        /// <summary>
        /// область для прорисовки ордеров
        /// </summary>
        private WindowsFormsHost _ordersHost;

        /// <summary>
        /// новый ордер в сервере
        /// </summary>
        private void _server_NewOrderIncomeEvent(Order order)
        {
            if (ServerMaster.StartProgram != ServerStartProgramm.IsOsTrader)
            {
                return;
            }

            try
            {
                if (_orders == null)
                {
                    _orders = new List<Order>();
                }

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

                    if (order.Side != Side.UnKnown)
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
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            _needToPaintOrders = true;
        }

        /// <summary>
        /// новый мой трейд в сервере
        /// </summary>
        /// <param name="trade"></param>
        private void serv_NewMyTradeEvent(MyTrade trade)
        {
            if (ServerMaster.StartProgram != ServerStartProgramm.IsOsTrader)
            {
                return;
            }

            if (_orders == null || _orders.Count == 0)
            {
                return;
            }

            Order myOrder = _orders.Find(order1 => order1.NumberMarket == trade.NumberOrderParent);

            if (myOrder == null)
            {
                return;
            }

            _orders.Remove(myOrder);
            _needToPaintOrders = true;
        }

        /// <summary>
        /// все ордера
        /// </summary>
        private List<Order> _orders;

        /// <summary>
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
                    if (_orders[i].State != OrderStateType.Activ
                      || _orders[i].Side == Side.UnKnown)
                    {
                        continue;
                    }

                    DataGridViewRow nRow = new DataGridViewRow();

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[0].Value = _orders[i].TimeCallBack;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[1].Value = _orders[i].SecurityNameCode;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[2].Value = _orders[i].Side;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[3].Value = _orders[i].State;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[4].Value = _orders[i].Price;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[5].Value = _orders[i].Volume;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[6].Value = _orders[i].Volume - _orders[i].VolumeExecute;

                    _gridOrders.Rows.Add(nRow);
                }
            }
            catch
            {
                // игнорим. Пусть иногда натыкаемся на налл, при удалении исходного ордера или модификации
                // зато не мешаем основному потоку работать
                //SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

// пользователь кликает по всплывающему меню

        /// <summary>
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

                items[0] = new MenuItem { Text = @"Отозвать все активные заявки" };

                items[0].Click += OrdersCloseAll_Click;

                items[1] = new MenuItem { Text = @"Отозвать текущую" };
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
        /// очистить список ордеров
        /// </summary>
        public void ClearOrders()
        {
            _orders = new List<Order>();
        }

        /// <summary>
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
                            server.CanselOrder(_orders[i]);
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

                if (order.State == OrderStateType.Activ &&
                        !string.IsNullOrEmpty(order.PortfolioNumber))
                {
                    IServer server = ServerMaster.GetServers().Find(server1 => server1.ServerType == order.ServerType);
                    if (server != null)
                    {
                        server.CanselOrder(order);
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
            { // если на нас никто не подписан и в логе ошибка
                MessageBox.Show(message);
            }
        }

        /// <summary>
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }
}
