/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Gui;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Point = System.Drawing.Point;

namespace OsEngine.Market
{
    public enum StartUiToPainter
    {
        IsTester,
        IsTesterLight,
        IsOsTrader,
        IsOsTraderLight
    }

    public class ServerMasterOrdersPainter
    {
        public ServerMasterOrdersPainter()
        {
            _currentCulture = OsLocalization.CurCulture;
            ServerMaster.ServerCreateEvent += ServerMaster_ServerCreateEvent;
            ServerMaster.ServerDeleteEvent += ServerMaster_ServerDeleteEvent;

            Task task = new Task(PainterThreadArea);
            task.Start();
        }

        private StartUiToPainter _startAllProgram;

        private CultureInfo _currentCulture;


        private void ServerMaster_ServerDeleteEvent(IServer server)
        {
            try
            {
                if (server.ServerType == ServerType.Optimizer)
                {
                    return;
                }

                server.NewOrderIncomeEvent -= server_NewOrderIncomeEvent;
                server.NewMyTradeEvent -= server_NewMyTradeEvent;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

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
                    servers[i].NewOrderIncomeEvent -= server_NewOrderIncomeEvent;
                    servers[i].NewMyTradeEvent -= server_NewMyTradeEvent;

                    servers[i].NewOrderIncomeEvent += server_NewOrderIncomeEvent;
                    servers[i].NewMyTradeEvent += server_NewMyTradeEvent;
                }
                catch
                {
                    // ignore
                }
            }
        }

        public void StartPaint()
        {
            try
            {
                if (_hostActiveOrders.Dispatcher.CheckAccess() == false)
                {
                    _hostActiveOrders.Dispatcher.Invoke(new Action(StartPaint));
                    return;
                }

                if (_hostActiveOrders != null)
                {
                    _hostActiveOrders.Child = _gridActiveOrders;
                }

                if (_hostHistoricalOrders != null)
                {
                    _hostHistoricalOrders.Child = _gridHistoricalOrders;
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void StopPaint()
        {
            try
            {
                if (_hostActiveOrders != null)
                {
                    _hostActiveOrders.Child = null;
                }

                if (_hostHistoricalOrders != null)
                {
                    _hostHistoricalOrders.Child = null;
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void SetHostTable(WindowsFormsHost hostActiveOrders, 
            WindowsFormsHost hostHistoricalOrders, 
            StartUiToPainter startFourProgram,
            System.Windows.Controls.ComboBox comboBoxActiveOrders,
            System.Windows.Controls.Button buttonLeftActiveOrders,
            System.Windows.Controls.Button buttonRightActiveOrders,
            System.Windows.Controls.ComboBox comboBoxHistoryOrders,
            System.Windows.Controls.Button buttonLeftHistoryOrders,
            System.Windows.Controls.Button buttonRightHistoryOrders)
        {
            try
            {
                _startAllProgram = startFourProgram;

                if (hostActiveOrders.Dispatcher.CheckAccess() == false)
                {
                    hostActiveOrders.Dispatcher.Invoke(
                        new Action<WindowsFormsHost,
                        WindowsFormsHost,
                        StartUiToPainter,
                        System.Windows.Controls.ComboBox,
                        System.Windows.Controls.Button,
                        System.Windows.Controls.Button,
                        System.Windows.Controls.ComboBox,
                        System.Windows.Controls.Button,
                        System.Windows.Controls.Button>(SetHostTable),
                        hostActiveOrders, hostHistoricalOrders, startFourProgram,
                        comboBoxActiveOrders, buttonLeftActiveOrders, buttonRightActiveOrders,
                        comboBoxHistoryOrders,buttonLeftHistoryOrders, buttonRightHistoryOrders);
 
                    return;
                }

                if(comboBoxHistoryOrders != null)
                {
                    comboBoxHistoryOrders.Items.Add("20");
                    comboBoxHistoryOrders.Items.Add("50");
                    comboBoxHistoryOrders.Items.Add("100");
                    comboBoxHistoryOrders.Items.Add("150");
                    comboBoxHistoryOrders.Items.Add("200");
                    comboBoxHistoryOrders.Items.Add("250");
                    comboBoxHistoryOrders.SelectedIndex = 0;

                    comboBoxHistoryOrders.SelectionChanged += OnComboBoxSelectionItem;
                }

                if(comboBoxActiveOrders!= null)
                {
                    comboBoxActiveOrders.Items.Add("20");
                    comboBoxActiveOrders.Items.Add("50");
                    comboBoxActiveOrders.Items.Add("100");
                    comboBoxActiveOrders.Items.Add("150");
                    comboBoxActiveOrders.Items.Add("200");
                    comboBoxActiveOrders.Items.Add("250");
                    comboBoxActiveOrders.SelectedIndex = 0;
                    comboBoxActiveOrders.SelectionChanged += OnComboBoxSelectionItem;
                }

                if(buttonLeftActiveOrders != null)
                {
                    buttonLeftActiveOrders.Click += OnBackPageClickActive;
                }
               
                if(buttonRightActiveOrders != null)
                {
                    buttonRightActiveOrders.Click += OnNextPageClickActive;
                }
                
                if(buttonLeftHistoryOrders != null)
                {
                    buttonLeftHistoryOrders.Click += OnBackPageClickHistorical;
                }
                
                if(buttonRightHistoryOrders != null)
                {
                    buttonRightHistoryOrders.Click += OnNextPageClickHistorical;
                }

                if (hostActiveOrders != null)
                {
                    _gridActiveOrders = DataGridFactory.GetDataGridOrder();
                    _gridActiveOrders.ScrollBars = ScrollBars.Vertical;
                    for (int i = 1; i < _gridActiveOrders.Columns.Count; i++)
                    {
                        _gridActiveOrders.Columns[i].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    }

                    _hostActiveOrders = hostActiveOrders;
                    _hostActiveOrders.Child = _gridActiveOrders;
                    _gridActiveOrders.Click += _gridOrders_Click;
                    _gridActiveOrders.DataError += _gridOrders_DataError; 
                }

                if (hostHistoricalOrders != null)
                {
                    _hostHistoricalOrders = hostHistoricalOrders;
                    _gridHistoricalOrders = DataGridFactory.GetDataGridOrder();
                    _gridHistoricalOrders.ScrollBars = ScrollBars.Vertical;

                    for (int i = 1; i < _gridHistoricalOrders.Columns.Count; i++)
                    {
                        _gridHistoricalOrders.Columns[i].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    }

                    _hostHistoricalOrders.Child = _gridHistoricalOrders;
                    _gridHistoricalOrders.DataError += _gridOrders_DataError;
                }

            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void InsertOrder(Order order)
        {
            server_NewOrderIncomeEvent(order);
        }

        #region Drawing order thread

        private async void PainterThreadArea()
        {
            while (true)
            {
                await Task.Delay(500);

                try
                {

                    if (MainWindow.ProccesIsWorked == false)
                    {
                        return;
                    }

                    if (_orders == null)
                    {
                        _needToPaintOrders = false;
                    }

                    if (_needToPaintOrders)
                    {
                        _needToPaintOrders = false;

                        //_orders

                        List<Order> activeOrders = new List<Order>();

                        List<Order> historicalOrders = new List<Order>();

                        for (int i = 0; i < _orders.Count; i++)
                        {
                            Order order = null;

                            try
                            {
                                order = _orders[i];
                            }
                            catch
                            {
                                // ignore
                            }

                            if (order == null)
                            {
                                continue;
                            }

                            if (order.State == OrderStateType.Active
                                || order.State == OrderStateType.Pending
                                || order.State == OrderStateType.None)
                            {
                                activeOrders.Add(order);
                            }
                            else
                            {
                                historicalOrders.Add(order);
                            }
                        }

                        SortOrders(activeOrders);
                        SortOrders(historicalOrders);

                        // высылаем на прорисовку отдельно

                        if (_gridActiveOrders != null)
                        {
                            int ActivePage = 1;
                            int ActivePageSize = 20;

                            // active orders BotStation Light
                            if (_startAllProgram == StartUiToPainter.IsOsTraderLight)
                            {
                                RobotUiLight.Instance?.Dispatcher.Invoke(() =>
                                {
                                    try
                                    {
                                        ActivePage = Convert.ToInt32(RobotUiLight.Instance.LabelNumberThisPageActive.Content);
                                        ActivePageSize = Convert.ToInt32(RobotUiLight.Instance.ComboBoxQuantityPerPageActive.SelectedValue);

                                        if (ActivePage > GetTotalPages(activeOrders.Count, ActivePageSize))
                                        {
                                            RobotUiLight.Instance.LabelNumberThisPageActive.Content = "1";
                                        }

                                        ActivePage = Convert.ToInt32(RobotUiLight.Instance.LabelNumberThisPageActive.Content);
                                        ActivePageSize = Convert.ToInt32(RobotUiLight.Instance.ComboBoxQuantityPerPageActive.SelectedValue);

                                        RobotUiLight.Instance.LabelNumberAllPageActive.Content = GetTotalPages(activeOrders.Count, ActivePageSize).ToString();
                                    }
                                    catch (Exception error)
                                    {
                                        SendNewLogMessage(error.ToString(), LogMessageType.Error);
                                    }
                                });

                                PaintOrders(activeOrders, _gridActiveOrders, _hostActiveOrders, ActivePage, ActivePageSize);
                            }

                            // active orders BotStation
                            if (_startAllProgram == StartUiToPainter.IsOsTrader)
                            {
                                RobotUi.Instance?.Dispatcher.Invoke(() =>
                                {
                                    try
                                    {
                                        ActivePage = Convert.ToInt32(RobotUi.Instance.LabelNumberThisPageActive.Content);
                                        ActivePageSize = Convert.ToInt32(RobotUi.Instance.ComboBoxQuantityPerPageActive.SelectedValue);

                                        if (ActivePage > GetTotalPages(activeOrders.Count, ActivePageSize))
                                        {
                                            RobotUi.Instance.LabelNumberThisPageActive.Content = "1";
                                        }

                                        ActivePage = Convert.ToInt32(RobotUi.Instance.LabelNumberThisPageActive.Content);
                                        ActivePageSize = Convert.ToInt32(RobotUi.Instance.ComboBoxQuantityPerPageActive.SelectedValue);

                                        RobotUi.Instance.LabelNumberAllPageActive.Content = GetTotalPages(activeOrders.Count, ActivePageSize).ToString();
                                    }
                                    catch (Exception error)
                                    {
                                        SendNewLogMessage(error.ToString(), LogMessageType.Error);
                                    }
                                });
                            
                                PaintOrders(activeOrders, _gridActiveOrders, _hostActiveOrders, ActivePage, ActivePageSize);
                            }

                            // active orders Tester Light
                            if (_startAllProgram == StartUiToPainter.IsTesterLight)
                            {
                                TesterUiLight.Instance?.Dispatcher.Invoke(() =>
                                {
                                    try
                                    {
                                        ActivePage = Convert.ToInt32(TesterUiLight.Instance.LabelNumberThisPageActive.Content);
                                        ActivePageSize = Convert.ToInt32(TesterUiLight.Instance.ComboBoxQuantityPerPageActive.SelectedValue);

                                        if (ActivePage > GetTotalPages(activeOrders.Count, ActivePageSize))
                                        {
                                            TesterUiLight.Instance.LabelNumberThisPageActive.Content = "1";
                                        }

                                        ActivePage = Convert.ToInt32(TesterUiLight.Instance.LabelNumberThisPageActive.Content);
                                        ActivePageSize = Convert.ToInt32(TesterUiLight.Instance.ComboBoxQuantityPerPageActive.SelectedValue);

                                        TesterUiLight.Instance.LabelNumberAllPageActive.Content = GetTotalPages(activeOrders.Count, ActivePageSize).ToString();
                                    }
                                    catch (Exception error)
                                    {
                                        SendNewLogMessage(error.ToString(), LogMessageType.Error);
                                    }
                                });

                                PaintOrders(activeOrders, _gridActiveOrders, _hostActiveOrders, ActivePage, ActivePageSize);
                            }

                            // active orders Tester
                            if (_startAllProgram == StartUiToPainter.IsTester)
                            {
                                TesterUi.Instance?.Dispatcher.Invoke(() =>
                                {
                                    try
                                    {
                                        ActivePage = Convert.ToInt32(TesterUi.Instance.LabelNumberThisPageActive.Content);
                                        ActivePageSize = Convert.ToInt32(TesterUi.Instance.ComboBoxQuantityPerPageActive.SelectedValue);

                                        if (ActivePage > GetTotalPages(activeOrders.Count, ActivePageSize))
                                        {
                                            TesterUi.Instance.LabelNumberThisPageActive.Content = "1";
                                        }

                                        ActivePage = Convert.ToInt32(TesterUi.Instance.LabelNumberThisPageActive.Content);
                                        ActivePageSize = Convert.ToInt32(TesterUi.Instance.ComboBoxQuantityPerPageActive.SelectedValue);

                                        TesterUi.Instance.LabelNumberAllPageActive.Content = GetTotalPages(activeOrders.Count, ActivePageSize).ToString();
                                    }
                                    catch (Exception error)
                                    {
                                        SendNewLogMessage(error.ToString(), LogMessageType.Error);
                                    }
                                });

                                PaintOrders(activeOrders, _gridActiveOrders, _hostActiveOrders, ActivePage, ActivePageSize);
                            }
                        }

                        if (_gridHistoricalOrders != null)
                        {
                            int HistoricalPage = 1;
                            int HistoricalPageSize = 20;

                            // history orders BotStation Light
                            if (_startAllProgram == StartUiToPainter.IsOsTraderLight)
                            {
                                RobotUiLight.Instance?.Dispatcher.Invoke(() =>
                                {
                                    try
                                    {
                                        HistoricalPage = Convert.ToInt32(RobotUiLight.Instance.LabelNumberThisPageHistorical.Content);
                                        HistoricalPageSize = Convert.ToInt32(RobotUiLight.Instance.ComboBoxQuantityPerPageHistorical.SelectedValue);

                                        if (HistoricalPage > GetTotalPages(historicalOrders.Count, HistoricalPageSize))
                                        {
                                            RobotUiLight.Instance.LabelNumberThisPageHistorical.Content = "1";
                                        }

                                        HistoricalPage = Convert.ToInt32(RobotUiLight.Instance.LabelNumberThisPageHistorical.Content);
                                        HistoricalPageSize = Convert.ToInt32(RobotUiLight.Instance.ComboBoxQuantityPerPageHistorical.SelectedValue);

                                        RobotUiLight.Instance.LabelNumberAllPageHistorical.Content = GetTotalPages(historicalOrders.Count, HistoricalPageSize).ToString();
                                    }
                                    catch (Exception error)
                                    {
                                        SendNewLogMessage(error.ToString(), LogMessageType.Error);
                                    }
                                });

                                PaintOrders(historicalOrders, _gridHistoricalOrders, _hostHistoricalOrders, HistoricalPage, HistoricalPageSize);
                            }

                            // history orders Tester Light
                            if (_startAllProgram == StartUiToPainter.IsTesterLight)
                            {
                                TesterUiLight.Instance?.Dispatcher.Invoke(() =>
                                {
                                    try
                                    {
                                        HistoricalPage = Convert.ToInt32(TesterUiLight.Instance.LabelNumberThisPageHistorical.Content);
                                        HistoricalPageSize = Convert.ToInt32(TesterUiLight.Instance.ComboBoxQuantityPerPageHistorical.SelectedValue);

                                        if (HistoricalPage > GetTotalPages(historicalOrders.Count, HistoricalPageSize))
                                        {
                                            TesterUiLight.Instance.LabelNumberThisPageHistorical.Content = "1";
                                        }

                                        HistoricalPage = Convert.ToInt32(TesterUiLight.Instance.LabelNumberThisPageHistorical.Content);
                                        HistoricalPageSize = Convert.ToInt32(TesterUiLight.Instance.ComboBoxQuantityPerPageHistorical.SelectedValue);

                                        TesterUiLight.Instance.LabelNumberAllPageHistorical.Content = GetTotalPages(historicalOrders.Count, HistoricalPageSize).ToString();
                                    }
                                    catch (Exception error)
                                    {
                                        SendNewLogMessage(error.ToString(), LogMessageType.Error);
                                    }
                                });

                                PaintOrders(historicalOrders, _gridHistoricalOrders, _hostHistoricalOrders, HistoricalPage, HistoricalPageSize);
                            }
                        }
                    }

                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        public void OnBackPageClickActive(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_startAllProgram == StartUiToPainter.IsTesterLight)
                {
                    if (Convert.ToInt32(TesterUiLight.Instance.LabelNumberThisPageActive.Content) > 1)
                    {
                        TesterUiLight.Instance.LabelNumberThisPageActive.Content =
                            Convert.ToInt32(TesterUiLight.Instance.LabelNumberThisPageActive.Content) - 1;

                        ForcePaint();
                    }
                }

                if (_startAllProgram == StartUiToPainter.IsTester)
                {
                    if (Convert.ToInt32(TesterUi.Instance.LabelNumberThisPageActive.Content) > 1)
                    {
                        TesterUi.Instance.LabelNumberThisPageActive.Content =
                            Convert.ToInt32(TesterUi.Instance.LabelNumberThisPageActive.Content) - 1;

                        ForcePaint();
                    }
                }

                if (_startAllProgram == StartUiToPainter.IsOsTraderLight)
                {
                    if (Convert.ToInt32(RobotUiLight.Instance.LabelNumberThisPageActive.Content) > 1)
                    {
                        RobotUiLight.Instance.LabelNumberThisPageActive.Content =
                            Convert.ToInt32(RobotUiLight.Instance.LabelNumberThisPageActive.Content) - 1;

                        ForcePaint();
                    }
                }

                if (_startAllProgram == StartUiToPainter.IsOsTrader)
                {
                    if (Convert.ToInt32(RobotUi.Instance.LabelNumberThisPageActive.Content) > 1)
                    {
                        RobotUi.Instance.LabelNumberThisPageActive.Content =
                            Convert.ToInt32(RobotUi.Instance.LabelNumberThisPageActive.Content) - 1;

                        ForcePaint();
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void OnNextPageClickActive(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_startAllProgram == StartUiToPainter.IsTesterLight)
                {
                    if (Convert.ToInt32(TesterUiLight.Instance.LabelNumberThisPageActive.Content) !=
                        Convert.ToInt32(TesterUiLight.Instance.LabelNumberAllPageActive.Content))
                    {
                        TesterUiLight.Instance.LabelNumberThisPageActive.Content =
                            Convert.ToInt32(TesterUiLight.Instance.LabelNumberThisPageActive.Content) + 1;

                        ForcePaint();
                    }
                }

                if (_startAllProgram == StartUiToPainter.IsTester)
                {
                    if (Convert.ToInt32(TesterUi.Instance.LabelNumberThisPageActive.Content) !=
                        Convert.ToInt32(TesterUi.Instance.LabelNumberAllPageActive.Content))
                    {
                        TesterUi.Instance.LabelNumberThisPageActive.Content =
                            Convert.ToInt32(TesterUi.Instance.LabelNumberThisPageActive.Content) + 1;

                        ForcePaint();
                    }
                }

                if (_startAllProgram == StartUiToPainter.IsOsTraderLight)
                {
                    if (Convert.ToInt32(RobotUiLight.Instance.LabelNumberThisPageActive.Content) !=
                        Convert.ToInt32(RobotUiLight.Instance.LabelNumberAllPageActive.Content))
                    {
                        RobotUiLight.Instance.LabelNumberThisPageActive.Content =
                            Convert.ToInt32(RobotUiLight.Instance.LabelNumberThisPageActive.Content) + 1;

                        ForcePaint();
                    }
                }

                if (_startAllProgram == StartUiToPainter.IsOsTrader)
                {
                    if (Convert.ToInt32(RobotUi.Instance.LabelNumberThisPageActive.Content) !=
                        Convert.ToInt32(RobotUi.Instance.LabelNumberAllPageActive.Content))
                    {
                        RobotUi.Instance.LabelNumberThisPageActive.Content =
                            Convert.ToInt32(RobotUi.Instance.LabelNumberThisPageActive.Content) + 1;

                        ForcePaint();
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void OnBackPageClickHistorical(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_startAllProgram == StartUiToPainter.IsTesterLight)
                {
                    if (Convert.ToInt32(TesterUiLight.Instance.LabelNumberThisPageHistorical.Content) > 1)
                    {
                        TesterUiLight.Instance.LabelNumberThisPageHistorical.Content =
                            Convert.ToInt32(TesterUiLight.Instance.LabelNumberThisPageHistorical.Content) - 1;

                        ForcePaint();
                    }
                }

                if (_startAllProgram == StartUiToPainter.IsOsTraderLight)
                {
                    if (Convert.ToInt32(RobotUiLight.Instance.LabelNumberThisPageHistorical.Content) > 1)
                    {
                        RobotUiLight.Instance.LabelNumberThisPageHistorical.Content =
                            Convert.ToInt32(RobotUiLight.Instance.LabelNumberThisPageHistorical.Content) - 1;

                        ForcePaint();
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void OnNextPageClickHistorical(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_startAllProgram == StartUiToPainter.IsTesterLight)
                {
                    if (Convert.ToInt32(TesterUiLight.Instance.LabelNumberThisPageHistorical.Content) !=
                        Convert.ToInt32(TesterUiLight.Instance.LabelNumberAllPageHistorical.Content))
                    {
                        TesterUiLight.Instance.LabelNumberThisPageHistorical.Content =
                            Convert.ToInt32(TesterUiLight.Instance.LabelNumberThisPageHistorical.Content) + 1;

                        ForcePaint();
                    }
                }

                if (_startAllProgram == StartUiToPainter.IsOsTraderLight)
                {
                    if (Convert.ToInt32(RobotUiLight.Instance.LabelNumberThisPageHistorical.Content) !=
                        Convert.ToInt32(RobotUiLight.Instance.LabelNumberAllPageHistorical.Content))
                    {
                        RobotUiLight.Instance.LabelNumberThisPageHistorical.Content =
                            Convert.ToInt32(RobotUiLight.Instance.LabelNumberThisPageHistorical.Content) + 1;

                        ForcePaint();
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void OnComboBoxSelectionItem(object sender, RoutedEventArgs e)
        {
            ForcePaint();
        }

        public static int GetTotalPages(int totalItems, int pageSize)
        {
            if (pageSize <= 0) return 1;
            if (totalItems <= 0) return 1;
            return (int)Math.Ceiling((double)totalItems / pageSize);
        }

        private void SortOrders(List<Order> orders)
        {
            if (orders.Count > 1)
            { // Ура, пузырик!

                for (int i = 0; i < orders.Count; i++)
                {
                    for (int i2 = 1; i2 < orders.Count; i2++)
                    {
                        if (orders[i2].NumberUser < orders[i2 - 1].NumberUser)
                        {
                            Order order = orders[i2];
                            orders[i2] = orders[i2 - 1];
                            orders[i2 - 1] = order;
                        }
                    }
                }
            }
        }

        private bool _needToPaintOrders;

        #endregion

        #region Drawing work

        private DataGridView _gridActiveOrders;

        private WindowsFormsHost _hostActiveOrders;

        private DataGridView _gridHistoricalOrders;

        private WindowsFormsHost _hostHistoricalOrders;

        private object _lockerOrders = new Object();

        private void server_NewOrderIncomeEvent(Order order)
        {
            try
            {
                if(order == null)
                {
                    return;
                }

                if (order.ServerType == ServerType.Optimizer ||
                order.ServerType == ServerType.Miner)
                {
                    return;
                }

                if (_orders == null)
                {
                    _orders = new List<Order>();
                }

                lock (_lockerOrders)
                {
                    Order myOrder = null;

                    for (int i = 0; i < _orders.Count; i++)
                    {
                        Order curOrder = _orders[i];

                        if (curOrder == null)
                        {
                            continue;
                        }

                        if (curOrder.NumberUser != 0 &&
                            order.NumberUser != 0
                            && curOrder.NumberUser == order.NumberUser)
                        {
                            myOrder = curOrder;
                            break;
                        }
                        if (string.IsNullOrEmpty(curOrder.NumberMarket) == false &&
                            string.IsNullOrEmpty(order.NumberMarket) == false &&
                            curOrder.NumberMarket == order.NumberMarket)
                        {
                            myOrder = curOrder;
                            break;
                        }
                    }

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
                        if (myOrder.TimeCreate == DateTime.MinValue)    //AVP
                        {
                            myOrder.TimeCreate = order.TimeCreate;
                        }
                    }

                    if (_startAllProgram == StartUiToPainter.IsTester
                       || _startAllProgram == StartUiToPainter.IsTesterLight)
                    {
                        if (_orders.Count > 100)
                        {
                            _orders.RemoveAt(0);
                        }
                    }
                    else
                    {
                        if (_orders.Count > 1000)
                        {
                            _orders.RemoveAt(0);
                        }
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

        private void server_NewMyTradeEvent(MyTrade trade)
        {
            try
            {
                if (_orders == null || _orders.Count == 0)
                {
                    return;
                }

                if (_startAllProgram != StartUiToPainter.IsOsTrader
                    && _startAllProgram != StartUiToPainter.IsOsTraderLight)
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

                    _needToPaintOrders = true;
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private List<Order> _orders;

        private void PaintOrders(List<Order> ordersToPaint, DataGridView gridToPaint, WindowsFormsHost host, int pageNumber, int pageSize)
        {
            try
            {
                if (gridToPaint == null 
                    || host == null
                    || ordersToPaint == null)
                {
                    return;
                }

                if (host.Dispatcher.CheckAccess() == false)
                {
                    host.Dispatcher.Invoke(
                        new Action<List<Order>, DataGridView, WindowsFormsHost, int, int>(PaintOrders),
                        ordersToPaint, gridToPaint, host, pageNumber, pageSize);

                    return;
                }

                int visibleRow = 0;

                if (gridToPaint.FirstDisplayedScrollingRowIndex > 0)
                {
                    visibleRow = gridToPaint.FirstDisplayedScrollingRowIndex;
                }

                if(gridToPaint.Rows.Count == 0 
                    && ordersToPaint.Count == 0)
                {
                    return;
                }

                gridToPaint.Rows.Clear();

                if (ordersToPaint.Count == 0)
                {
                    host.Child = gridToPaint;
                    return;
                }

                // всего страниц
                int totalPages = (int)Math.Ceiling((double)ordersToPaint.Count / pageSize);
                if (pageNumber < 1) pageNumber = 1;
                if (pageNumber > totalPages) pageNumber = totalPages;

                // какие записи брать
                int skip = (pageNumber - 1) * pageSize;
                List<Order> pageData = ordersToPaint
                    .OrderByDescending(o => o.TimeCreate) // чтобы последние были сверху, как у тебя было
                    .Skip(skip)
                    .Take(pageSize)
                    .ToList();

                List<DataGridViewRow> rows = new List<DataGridViewRow>();
                TimeSpan zero = new TimeSpan(0, 0, 0, 0);

                foreach (var order in pageData)
                {
                    DataGridViewRow nRow = new DataGridViewRow();

                    nRow.Cells.Add(new DataGridViewTextBoxCell { Value = order.NumberUser });
                    nRow.Cells.Add(new DataGridViewTextBoxCell { Value = order.NumberMarket });
                    nRow.Cells.Add(new DataGridViewTextBoxCell { Value = order.TimeCreate.ToString(_currentCulture) });
                    nRow.Cells.Add(new DataGridViewTextBoxCell { Value = order.SecurityNameCode });
                    nRow.Cells.Add(new DataGridViewTextBoxCell { Value = order.PortfolioNumber });
                    nRow.Cells.Add(new DataGridViewTextBoxCell { Value = order.Side });
                    nRow.Cells.Add(new DataGridViewTextBoxCell { Value = order.State });
                    nRow.Cells.Add(new DataGridViewTextBoxCell { Value = order.Price.ToStringWithNoEndZero() });
                    nRow.Cells.Add(new DataGridViewTextBoxCell { Value = order.PriceReal.ToStringWithNoEndZero() });
                    nRow.Cells.Add(new DataGridViewTextBoxCell { Value = order.Volume.ToStringWithNoEndZero() });
                    nRow.Cells.Add(new DataGridViewTextBoxCell { Value = order.TypeOrder });
                    nRow.Cells.Add(new DataGridViewTextBoxCell { Value = order.TimeRoundTrip > zero ? order.TimeRoundTrip.ToString() : "" });

                    rows.Add(nRow);
                }

                if (rows.Count > 0)
                {
                    gridToPaint.Rows.AddRange(rows.ToArray());
                }

                if (visibleRow > 0 && visibleRow < rows.Count)
                {
                    gridToPaint.FirstDisplayedScrollingRowIndex = visibleRow;
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                host.Child = gridToPaint;
            }
        }

        public void ForcePaint()
        {
            _needToPaintOrders = true;
        }

        private void _gridOrders_Click(object sender, EventArgs e)
        {
            try
            {
                MouseEventArgs mouse = (MouseEventArgs)e;

                if (mouse.Button != MouseButtons.Right)
                {
                    return;
                }

                ToolStripMenuItem[] items = new ToolStripMenuItem[2];

                items[0] = new ToolStripMenuItem { Text = OsLocalization.Market.Message4 };

                items[0].Click += OrdersCloseAll_Click;

                items[1] = new ToolStripMenuItem { Text = OsLocalization.Market.Message5 };
                items[1].Click += PositionCloseForNumber_Click;

                ContextMenuStrip menu = new ContextMenuStrip(); menu.Items.AddRange(items);

                _gridActiveOrders.ContextMenuStrip = menu;
                _gridActiveOrders.ContextMenuStrip.Show(_gridActiveOrders, new Point(mouse.X, mouse.Y));
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void ClearOrders()
        {
            _orders = new List<Order>();
        }

        private void OrdersCloseAll_Click(object sender, EventArgs e)
        {
            AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Market.Label67);
            ui.ShowDialog();

            if (ui.UserAcceptAction == false)
            {
                return;
            }

            new Task(() =>
            {
                try
                {
                    if (_orders != null)
                    {
                        for (int i = 0; i < _orders.Count; i++)
                        {
                            Order order = _orders[i];

                            if (order.State == OrderStateType.Active &&
                                !string.IsNullOrEmpty(order.PortfolioNumber))
                            {
                                if (order.PortfolioNumber == "Emulator")
                                {
                                    if(RevokeOrderToEmulatorEvent != null)
                                    {
                                        RevokeOrderToEmulatorEvent(order);
                                    }
                                }
                                else
                                {
                                    if (ServerMaster.GetServers() ==  null)
                                    {
                                        continue;
                                    }

                                    IServer server = null;

                                    if (string.IsNullOrEmpty(order.ServerName) == false)
                                    {
                                        server = ServerMaster.GetServers().Find(server1 =>
                                        server1.ServerNameAndPrefix == order.ServerName);
                                    }
                                    else
                                    {
                                        server = ServerMaster.GetServers().Find(server1 =>
                                        server1.ServerType == order.ServerType);
                                    }

                                    if (server != null)
                                    {
                                        server.CancelOrder(order);
                                    }
                                }
                            }
                        }
                    }

                    List<IServer> servers = ServerMaster.GetServers();

                    for (int i = 0; servers != null && i < servers.Count; i++)
                    {
                        IServer server = servers[i];

                        if(server.ServerStatus != ServerConnectStatus.Connect)
                        {
                            continue;
                        }

                        server.CancelAllOrders();
                    }

                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
            }).Start();
        }

        private void PositionCloseForNumber_Click(object sender, EventArgs e)
        {
            AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Market.Label68);
            ui.ShowDialog();

            if (ui.UserAcceptAction == false)
            {
                return;
            }

            new Task(() =>
            {
                try
                {
                    if (_orders == null || _orders.Count == 0)
                    {
                        return;
                    }

                    if (_gridActiveOrders.Rows == null ||
                        _gridActiveOrders.Rows.Count == 0 ||
                        _gridActiveOrders.CurrentCell == null)
                    {
                        return;
                    }

                    Order order = _orders[(_orders.Count - 1 - _gridActiveOrders.CurrentCell.RowIndex)];    // иногда ошибается, не тот ордер возвращает
                    try   //AVP
                    {
                        int ordNumber = (int)_gridActiveOrders.Rows[_gridActiveOrders.CurrentCell.RowIndex].Cells[0].Value;    
                        if (order.NumberUser != ordNumber)
                        {
                            for (int i = 0; i < _orders.Count; i++)
                            {
                                if (_orders[i].NumberUser == ordNumber)
                                {
                                    order = _orders[i];
                                    break;
                                }
                            }
                        }
                    }
                    catch 
                    {
                        if (order is null)
                        {
                            return;
                        }
                    }
                    if ((order.State == OrderStateType.Active || order.State == OrderStateType.Pending)
                        &&
                            !string.IsNullOrEmpty(order.PortfolioNumber))
                    {
                        if (order.PortfolioNumber == "Emulator")
                        {
                            if (RevokeOrderToEmulatorEvent != null)
                            {
                                RevokeOrderToEmulatorEvent(order);
                            }
                        }
                        else
                        {
                            IServer server = null;

                            if(string.IsNullOrEmpty(order.ServerName) == false)
                            {
                                server = ServerMaster.GetServers().Find(server1 => 
                                server1.ServerNameAndPrefix == order.ServerName);
                            }
                            else
                            {
                                server = ServerMaster.GetServers().Find(server1 => 
                                server1.ServerType == order.ServerType);
                            }

                            if (server != null)
                            {
                                server.CancelOrder(order);
                            }
                        }
                    }
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
            }).Start();

        }

        public event Action<Order> RevokeOrderToEmulatorEvent;

        #endregion

        #region Log

        private void _gridOrders_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            if(e.Exception!= null)
            {
                SendNewLogMessage("ServerMaster painter error. \n" + e.Exception.ToString(), LogMessageType.Error);
            }
        }

        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            {
                ServerMaster.SendNewLogMessage(message, LogMessageType.Error);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }
}
