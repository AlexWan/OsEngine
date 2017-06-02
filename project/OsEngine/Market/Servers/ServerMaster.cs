/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.AstsBridge;
using OsEngine.Market.Servers.Finam;
using OsEngine.Market.Servers.InteractivBrokers;
using OsEngine.Market.Servers.Plaza;
using OsEngine.Market.Servers.Quik;
using OsEngine.Market.Servers.SmartCom;
using OsEngine.Market.Servers.Tester;
using MessageBox = System.Windows.MessageBox;
using Point = System.Drawing.Point;

namespace OsEngine.Market.Servers
{
    /// <summary>
    /// класс менеджер серверов подключения к бирже
    /// </summary>
    public class ServerMaster
    {
// сервис

        /// <summary>
        /// массив развёрнутых серверов
        /// </summary>
        private static List<IServer> _servers;

        /// <summary>
        /// является ли текущее подключение тестовым
        /// </summary>
        public static bool IsTester;

        /// <summary>
        /// является ли текущее подключение вызванным из OsData
        /// </summary>
        public static bool IsOsData;

        /// <summary>
        /// отключить все сервера
        /// </summary>
        public static void AbortAll()
        {
            try
            {
                for (int i = 0; _servers != null && i < _servers.Count; i++)
                {
                    _servers[i].StopServer();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(),LogMessageType.Error);
            }
        }

        /// <summary>
        /// показать настройки
        /// </summary>
        public static void ShowDialog() 
        {
            if (IsTester)
            {
                ServerMasterUi ui = new ServerMasterUi();
            }
            else
            {
                ServerMasterUi ui = new ServerMasterUi();
                ui.ShowDialog();
            }

        }

        /// <summary>
        /// создать сервер
        /// </summary>
        /// <param name="type"> тип сервера</param>
        /// <param name="neadLoadTicks">нужно ли подгружать тики из хранилища. Актуально в режиме робота для серверов Квик, Плаза 2</param>
        public static void CreateServer(ServerType type, bool neadLoadTicks)
        {
            try
            {
                if (_servers == null)
                {
                    _servers = new List<IServer>();
                }

                if (type == ServerType.Quik)
                {
                    if (_servers.Find(server => server.ServerType == ServerType.Quik) != null)
                    {
                        return;
                    }

                    QuikServer serv = new QuikServer(neadLoadTicks);
                    _servers.Add(serv);
                    serv.PortfoliosChangeEvent += _server_PortfoliosChangeEvent;
                    serv.NewOrderIncomeEvent += _server_NewOrderIncomeEvent;
                    serv.NewMyTradeEvent += serv_NewMyTradeEvent;

                    if (ServerCreateEvent != null)
                    {
                        ServerCreateEvent();
                    }
                }
                if (type == ServerType.InteractivBrokers)
                {
                    if (_servers.Find(server => server.ServerType == ServerType.InteractivBrokers) != null)
                    {
                        return;
                    }

                    InteractivBrokersServer serv = new InteractivBrokersServer();
                    _servers.Add(serv);
                    serv.PortfoliosChangeEvent += _server_PortfoliosChangeEvent;
                    serv.NewOrderIncomeEvent += _server_NewOrderIncomeEvent;
                    serv.NewMyTradeEvent += serv_NewMyTradeEvent;

                    if (ServerCreateEvent != null)
                    {
                        ServerCreateEvent();
                    }
                }
                else if (type == ServerType.SmartCom)
                {
                    if (_servers.Find(server => server.ServerType == ServerType.SmartCom) != null)
                    {
                        return;
                    }
                    try
                    {
                        SmartComServer serv = new SmartComServer();
                        _servers.Add(serv);
                        serv.PortfoliosChangeEvent += _server_PortfoliosChangeEvent;
                        serv.NewOrderIncomeEvent += _server_NewOrderIncomeEvent;
                        serv.NewMyTradeEvent += serv_NewMyTradeEvent;
                    }
                    catch (Exception)
                    {
                        MessageBox.Show(
                            "Произошла ошибка создания сервера СмартКом. Вероятно у Вас не установлена соответствующая программа. SmartCOM_Setup_3.0.146.msi ");
                        return;
                    }

                    if (ServerCreateEvent != null)
                    {
                        ServerCreateEvent();
                    }
                }
                else if (type == ServerType.Plaza)
                {
                    if (_servers.Find(server => server.ServerType == ServerType.Plaza) != null)
                    {
                        return;
                    }
                    try
                    {
                        PlazaServer serv = new PlazaServer(neadLoadTicks);
                        _servers.Add(serv);
                        serv.PortfoliosChangeEvent += _server_PortfoliosChangeEvent;
                        serv.NewOrderIncomeEvent += _server_NewOrderIncomeEvent;
                        serv.NewMyTradeEvent += serv_NewMyTradeEvent;
                    }
                    catch (Exception)
                    {
                        MessageBox.Show(
                            "Произошла ошибка создания сервера Плаза. Вероятно у Вас не установлено соответствующее программное обеспечение.");
                        return;
                    }

                    if (ServerCreateEvent != null)
                    {
                        ServerCreateEvent();
                    }
                }
                else if (type == ServerType.AstsBridge)
                {
                    if (_servers.Find(server => server.ServerType == ServerType.AstsBridge) != null)
                    {
                        return;
                    }
                    try
                    {
                        AstsBridgeServer serv = new AstsBridgeServer(neadLoadTicks);
                        _servers.Add(serv);
                        serv.PortfoliosChangeEvent += _server_PortfoliosChangeEvent;
                        serv.NewOrderIncomeEvent += _server_NewOrderIncomeEvent;
                        serv.NewMyTradeEvent += serv_NewMyTradeEvent;
                    }
                    catch (Exception)
                    {
                        MessageBox.Show(
                            "Произошла ошибка создания сервера Плаза. Вероятно у Вас не установлено соответствующее программное обеспечение.");
                        return;
                    }

                    if (ServerCreateEvent != null)
                    {
                        ServerCreateEvent();
                    }
                }
                else if (type == ServerType.Tester)
                {
                    try
                    {
                        TesterServer serv = new TesterServer();
                        _servers.Add(serv);
                        serv.PortfoliosChangeEvent += _server_PortfoliosChangeEvent;
                        serv.NewOrderIncomeEvent += _server_NewOrderIncomeEvent;
                    }
                    catch (Exception)
                    {
                        MessageBox.Show(
                            "Произошла ошибка создания тестового сервера.");
                        return;
                    }

                    if (ServerCreateEvent != null)
                    {
                        ServerCreateEvent();
                    }
                }
                else if (type == ServerType.Finam)
                {
                    try
                    {
                        FinamServer serv = new FinamServer();
                        _servers.Add(serv);
                        serv.PortfoliosChangeEvent += _server_PortfoliosChangeEvent;
                        serv.NewOrderIncomeEvent += _server_NewOrderIncomeEvent;
                    }
                    catch (Exception)
                    {
                        MessageBox.Show(
                            "Произошла ошибка создания тестового сервера.");
                        return;
                    }

                    if (ServerCreateEvent != null)
                    {
                        ServerCreateEvent();
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// взять сервер
        /// </summary>
        public static List<IServer> GetServers()
        {
            return _servers;
        }

        /// <summary>
        /// создан новый сервер
        /// </summary>
        public static event Action ServerCreateEvent;

        // создание серверов автоматически

        /// <summary>
        /// загрузить настройки сервера
        /// </summary>
        public static void Activate()
        {
            Load();

            Thread starterThread = new Thread(ThreadStarterWorkArea);
            starterThread.IsBackground = true;
            starterThread.Start();
        }

        /// <summary>
        /// сохранить настройки
        /// </summary>
        public static void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + @"ServerMaster.txt", false))
                {
                    writer.WriteLine(NeadToConnectAuto);
                    writer.Close();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// загрузить настройки
        /// </summary>
        public static void Load()
        {
            if (!File.Exists(@"Engine\" + @"ServerMaster.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + @"ServerMaster.txt"))
                {
                    NeadToConnectAuto = Convert.ToBoolean(reader.ReadLine());
                    reader.Close();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// можно ли сервер мастеру разворачивать сервера в автоматическом режиме
        /// </summary>
        public static bool NeadToConnectAuto;

        /// <summary>
        /// заказать на подключение определённый тип сервера
        /// </summary>
        public static void SetNeedServer(ServerType type)
        {
            if (_needServerTypes == null)
            {
                _needServerTypes = new List<ServerType>();
            }

            for (int i = 0; i < _needServerTypes.Count; i++)
            {
                if (_needServerTypes[i] == type)
                {
                    return;
                }
            }

            _needServerTypes.Add(type);
        }

        /// <summary>
        /// сервера, которые заказали роботы
        /// </summary>
        private static List<ServerType> _needServerTypes;

        /// <summary>
        /// серверы которые мы уже пытались подключить
        /// </summary>
        private static List<ServerType> _tryActivateServerTypes;

        /// <summary>
        /// место работы потока который подключает наши сервера в авто режиме
        /// </summary>
        private static void ThreadStarterWorkArea()
        {
            if (IsTester)
            {
                return;
            }
            while (true)
            {
                Thread.Sleep(5000);

                if (NeadToConnectAuto == false)
                {
                    continue;
                }

                if (_tryActivateServerTypes == null)
                {
                    _tryActivateServerTypes = new List<ServerType>();
                }

                for (int i = 0; _needServerTypes != null && i < _needServerTypes.Count; i++)
                {
                    TryStartThisSevrverInAutoType(_needServerTypes[i]);
                }
            }
        }
        
        /// <summary>
        /// Попробовать запустить данный сервер
        /// </summary>
        private static void TryStartThisSevrverInAutoType(ServerType type)
        {
            for (int i = 0; i < _tryActivateServerTypes.Count; i++)
            {
                if (_tryActivateServerTypes[i] == type)
                {
                    return;
                }
            }

            _tryActivateServerTypes.Add(type);

            if (GetServers() == null || GetServers().Find(server1 => server1.ServerType == type) == null)
            { // если у нас нашего сервера нет - создаём его
                CreateServer(type,true);
            }

            List<IServer> servers = GetServers();

            if (servers == null)
            { // что-то пошло не так
                return;
            }

            IServer server = servers.Find(server1 => server1.ServerType == type);

            if (server == null)
            {
                return;
            }

            if (server.ServerStatus != ServerConnectStatus.Connect)
            {
                server.StartServer();
            }
        }

// доступ к портфелю и его прорисовка

        /// <summary>
        /// начать прорисовывать контролы класса 
        /// </summary>
        public static void StartPaint()
        {
            try
            {
                _isPainting = true;

                _positionHost.Child = _gridPosition;
                _ordersHost.Child = _gridOrders;
                PaintOrders();
                RePaintPortfolio();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// остановить прорисовку контролов класса 
        /// </summary>
        public static void StopPaint()
        {
            _isPainting = false;
            _positionHost.Child = null;
            _ordersHost.Child = null;
        }

        /// <summary>
        /// включена ли прорисовка
        /// </summary>
        private static bool _isPainting = true;

        /// <summary>
        /// добавить элементы, на котором будут прорисовываться портфели и ордера
        /// </summary>
        public static void SetHostTable(WindowsFormsHost hostPortfolio, WindowsFormsHost hostOrders)
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
        private static object lockerPortfolio = new object();

        /// <summary>
        /// в сервере изменились портфели
        /// </summary>
        /// <param name="portfolios">портфели</param>
        private static void _server_PortfoliosChangeEvent(List<Portfolio> portfolios)
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

                    if (_isPainting == false)
                    {
                        return;
                    }

                    RePaintPortfolio();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// таблица для прорисовки портфелей
        /// </summary>
        private static DataGridView _gridPosition;

        /// <summary>
        /// область для прорисовки портфелей
        /// </summary>
        private static WindowsFormsHost _positionHost;

        /// <summary>
        /// таблица для прорисовки ордеров
        /// </summary>
        private static DataGridView _gridOrders;

        /// <summary>
        /// область для прорисовки ордеров
        /// </summary>
        private static WindowsFormsHost _ordersHost;

        /// <summary>
        /// перерисовать таблицу портфелей
        /// </summary>
        private static void RePaintPortfolio()
        {
            try
            {
                if (_gridPosition == null)
                {
                    return;
                }

                if (!_positionHost.CheckAccess())
                {
                    _positionHost.Dispatcher.Invoke(new Action(RePaintPortfolio));
                    return;
                }

                // очищаем старые данные с грида

                _gridPosition.Rows.Clear();

                if (_portfolios == null)
                {
                    return;
                }

                // отправляем портфели на прорисовку
                for (int i = 0; i < _portfolios.Count; i++)
                {
                    PaintPortfolio(_portfolios[i]);
                }
                _positionHost.Child.Refresh();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// прорисовать портфель
        /// </summary>
        private static void PaintPortfolio(Portfolio portfolio)
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
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// все портфели
        /// </summary>
        private static List<Portfolio> _portfolios;

        /// <summary>
        /// взять позицию на бирже по бумаге
        /// </summary>
        /// <param name="security">бумага по которой смотрим позицию на бирже</param>
        /// <param name="portfolioName">номер портфеля по которому ищем </param>
        /// <returns></returns>
        public static PositionOnBoard GetPositionOnBoard(Security security, string portfolioName)
        {
            try
            {
                if (_portfolios == null || _portfolios.Count == 0)
                {
                    return null;
                }

                for (int i = 0; i < _portfolios.Count; i++)
                {
                    List<PositionOnBoard> positionsOnBoard = _portfolios[i].GetPositionOnBoard();

                    if (positionsOnBoard != null && positionsOnBoard.Count != 0 &&
                        positionsOnBoard.Find(pose => pose.PortfolioName == portfolioName && pose.SecurityNameCode == security.Name) != null)
                    {
                        return positionsOnBoard.Find(pose => pose.SecurityNameCode == security.Name);
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }

            return null;
        }

// ордера

        /// <summary>
        /// новый ордер в сервере
        /// </summary>
        static void _server_NewOrderIncomeEvent(Order order)
        {
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

                if (_isPainting == false)
                {
                    return;
                }

                PaintOrders();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        static void serv_NewMyTradeEvent(MyTrade trade)
        {
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
        }

        /// <summary>
        /// прорисовать ордера
        /// </summary>
        private static void PaintOrders()
        {
            try
            {
                if (_positionHost == null)
                {
                    return;
                }
                if (!_positionHost.CheckAccess())
                {
                    _positionHost.Dispatcher.Invoke(new Action(PaintOrders));
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
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// все ордера
        /// </summary>
        private static List<Order> _orders;

        /// <summary>
        /// очистить список ордеров
        /// </summary>
        public static void ClearOrders()
        {
            _orders = new List<Order>();
            PaintOrders();
        }

        /// <summary>
        /// пользователь кликнул на таблицу всех ордеров
        /// </summary>
        static void _gridOrders_Click(object sender, EventArgs e)
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
        /// пользователь запросил закрытие всех ордеров
        /// </summary>
        private static void OrdersCloseAll_Click(object sender, EventArgs e)
        {
            try
            {
                if (_orders == null || _orders.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < _orders.Count; i++)
                {
                    if (_orders[i].State == OrderStateType.Activ)
                    {
                        IServer server = _servers.Find(server1 => server1.ServerType == _orders[i].ServerType);
                        if (server != null)
                        {
                            server.CanselOrder(_orders[i]);
                        }
                        else
                        {
                            for (int i2 = 0; i2 < _servers.Count; i2++)
                            {
                                _servers[i2].CanselOrder(_orders[i]);
                            }
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
        private static void PositionCloseForNumber_Click(object sender, EventArgs e)
        {
            try
            {
                if (_orders == null || _orders.Count == 0)
                {
                    return;
                }

                Order order = _orders[(_orders.Count - 1 - _gridOrders.CurrentCell.RowIndex)];

                if (order.State == OrderStateType.Activ)
                {
                    IServer server = _servers.Find(server1 => server1.ServerType == order.ServerType);
                    if (server != null)
                    {
                        server.CanselOrder(order);
                    }
                    else
                    {
                        for (int i2 = 0; i2 < _servers.Count; i2++)
                        {
                            _servers[i2].CanselOrder(order);
                        }
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
        private static void SendNewLogMessage(string message, LogMessageType type)
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
        public static event Action<string, LogMessageType> LogMessageEvent;

    }

    /// <summary>
    /// тип подключения к торгам. Тип сервера
    /// </summary>
    public enum ServerType
    {
        /// <summary>
        /// Квик
        /// </summary>
        Quik,

        /// <summary>
        /// Смарт-Ком
        /// </summary>
        SmartCom,

        /// <summary>
        /// Плаза 2
        /// </summary>
        Plaza,

        /// <summary>
        /// Тестер
        /// </summary>
        Tester,

        /// <summary>
        /// IB
        /// </summary>
        InteractivBrokers,

        /// <summary>
        /// Финам
        /// </summary>
        Finam,

        /// <summary>
        /// AstsBridge, он же ШЛЮЗ, он же TEAP 
        /// </summary>
        AstsBridge,

        /// <summary>
        /// Тип сервера не назначен
        /// </summary>
        Unknown
    }
}
