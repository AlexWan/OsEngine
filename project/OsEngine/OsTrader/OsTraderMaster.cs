/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Alerts;
using OsEngine.Entity;
using OsEngine.Journal;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Connectors;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Hitbtc;
using OsEngine.Market.Servers.Tester;
using OsEngine.OsTrader.AdminPanelApi;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.RiskManager;
using OsEngine.PrimeSettings;
using OsEngine.Robots;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Shapes;
using Grid = System.Windows.Controls.Grid;

namespace OsEngine.OsTrader
{
    /// <summary>
    /// Class manager for robots
    /// </summary>
    public class OsTraderMaster
    {
        #region Static Part

        public static OsTraderMaster Master;
        public static AdminApiMaster ApiMaster;

        #endregion

        /// <summary>
        /// Create a robot manager
        /// </summary>
        /// <param name="gridChart">chart area wpf</param>
        /// <param name="hostChart">chart area windows forms</param>
        /// <param name="hostGlass">market depth area</param>
        /// <param name="hostOpenDeals">open positions table area</param>
        /// <param name="hostCloseDeals">closed positions table area</param>
        /// <param name="hostAllDeals">area of all positions</param>
        /// <param name="hostLogBot">bot log area</param>
        /// <param name="hostLogPrime">prime log area</param>
        /// <param name="rectangleAroundChart">square by chart</param>
        /// <param name="hostAlerts">area for alerts</param>
        /// <param name="tabPanel">bots tabControl</param>
        /// <param name="tabBotTab">toolbar robot panel</param>
        /// <param name="textBoxLimitPrice">Textbox with limit price when entering an position</param>
        /// <param name="gridChartControlPanel">grid for chart control panel</param>
        /// <param name="startProgram">type of program that requested class creation</param>
        public OsTraderMaster(Grid gridChart, WindowsFormsHost hostChart, WindowsFormsHost hostGlass, WindowsFormsHost hostOpenDeals,
            WindowsFormsHost hostCloseDeals, WindowsFormsHost hostLogBot, WindowsFormsHost hostLogPrime, Rectangle rectangleAroundChart,
            WindowsFormsHost hostAlerts,
            TabControl tabPanel, TabControl tabBotTab, TextBox textBoxLimitPrice, Grid gridChartControlPanel, StartProgram startProgram)
        {
            NumberGen.GetNumberOrder(startProgram);
            _startProgram = startProgram;

            if (_startProgram == StartProgram.IsTester)
            {
                _typeWorkKeeper = ConnectorWorkType.Tester;
                ((TesterServer)ServerMaster.GetServers()[0]).TestingStartEvent += StrategyKeeper_TestingStartEvent;
                ((TesterServer)ServerMaster.GetServers()[0]).TestingFastEvent += StrategyKeeper_TestingFastEvent;
                ((TesterServer)ServerMaster.GetServers()[0]).TestingEndEvent += StrategyKeeper_TestingEndEvent;
            }

            if (_startProgram != StartProgram.IsTester)
            {
                ServerMaster.ActivateAutoConnection();
            }

            ServerMaster.LogMessageEvent += SendNewLogMessage;

            _tabBotTab = tabBotTab;

            if (_tabBotTab != null &&
                _tabBotTab.Items != null)
            {
                _tabBotTab.Items.Clear();
            }

            _gridChart = gridChart;
            _textBoxLimitPrice = textBoxLimitPrice;
            _hostChart = hostChart;
            _hostGlass = hostGlass;
            _hostOpenDeals = hostOpenDeals;
            _hostCloseDeals = hostCloseDeals;
            _hostboxLog = hostLogBot;
            _rectangleAroundChart = rectangleAroundChart;
            _hostAlerts = hostAlerts;
            _gridChartControlPanel = gridChartControlPanel;

            _tabBotNames = tabPanel;

            if(_tabBotNames != null)
            {
                _tabBotNames.Items.Clear();
            }

            _riskManager = new RiskManager.RiskManager("GlobalRiskManager", _startProgram);
            _riskManager.RiskManagerAlarmEvent += _riskManager_RiskManagerAlarmEvent;
            _riskManager.LogMessageEvent += SendNewLogMessage;

            _log = new Log("Prime", _startProgram);
            _log.StartPaint(hostLogPrime);
            _log.Listen(this);
            _hostLogPrime = hostLogPrime;

            SendNewLogMessage(OsLocalization.Trader.Label1, LogMessageType.User);

            Load();

            if(_tabBotNames != null)
            {
                _tabBotNames.SelectionChanged += _tabBotControl_SelectionChanged;
            }
            
            ReloadRiskJournals();

            Master = this;

            if (_startProgram == StartProgram.IsOsTrader && PrimeSettingsMaster.AutoStartApi)
            {
                ApiMaster = new AdminApiMaster(Master);
            }

            if (CriticalErrorHandler.ErrorInStartUp && CriticalErrorEvent != null)
            {
                try
                {
                    CriticalErrorEvent();
                }
                catch (Exception error)
                {
                    SendNewLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
                }
            }

            ServerMaster.ClearPositionOnBoardEvent += ServerMaster_ClearPositionOnBoardEvent;
        }

        public static event System.Action CriticalErrorEvent;

        /// <summary>
        /// Create a robot manager
        /// </summary>
        /// <param name="startProgram">type of program that requested class creation</param>
        /// <param name="hostLogPrime">prime log area</param>
        public OsTraderMaster(StartProgram startProgram, WindowsFormsHost hostLogPrime)
        {
            NumberGen.GetNumberOrder(startProgram);
            _startProgram = startProgram;

            if (_startProgram == StartProgram.IsTester)
            {
                _typeWorkKeeper = ConnectorWorkType.Tester;
                ((TesterServer)ServerMaster.GetServers()[0]).TestingStartEvent += StrategyKeeper_TestingStartEvent;
                ((TesterServer)ServerMaster.GetServers()[0]).TestingFastEvent += StrategyKeeper_TestingFastEvent;
                ((TesterServer)ServerMaster.GetServers()[0]).TestingEndEvent += StrategyKeeper_TestingEndEvent;
            }

            if (_startProgram != StartProgram.IsTester)
            {
                ServerMaster.ActivateAutoConnection();
            }

            ServerMaster.LogMessageEvent += SendNewLogMessage;

            _riskManager = new RiskManager.RiskManager("GlobalRiskManager", _startProgram);
            _riskManager.RiskManagerAlarmEvent += _riskManager_RiskManagerAlarmEvent;
            _riskManager.LogMessageEvent += SendNewLogMessage;

            _log = new Log("Prime", _startProgram);
            _log.StartPaint(hostLogPrime);
            _log.Listen(this);
            _hostLogPrime = hostLogPrime;

            SendNewLogMessage(OsLocalization.Trader.Label1, LogMessageType.User);

            Load();
            _tabBotNames.SelectionChanged += _tabBotControl_SelectionChanged;
            ReloadRiskJournals();
            
            Master = this;

            if (_startProgram == StartProgram.IsOsTrader && PrimeSettingsMaster.AutoStartApi)
            {
                ApiMaster = new AdminApiMaster(Master);
            }

            ServerMaster.ClearPositionOnBoardEvent += ServerMaster_ClearPositionOnBoardEvent;
        }

        private WindowsFormsHost _hostLogPrime;
        private WindowsFormsHost _hostChart;
        private Grid _gridChart;
        private WindowsFormsHost _hostGlass;
        private WindowsFormsHost _hostOpenDeals;
        private WindowsFormsHost _hostCloseDeals;
        private WindowsFormsHost _hostboxLog;
        private Rectangle _rectangleAroundChart;
        private WindowsFormsHost _hostAlerts;
        private TabControl _tabBotNames;
        private TabControl _tabBotTab;
        private ConnectorWorkType _typeWorkKeeper;
        private TextBox _textBoxLimitPrice;
        private TextBox _textBoxVolume;
        private Grid _gridChartControlPanel;

        /// <summary>
        /// Type of program that requested class creation
        /// </summary>
        public StartProgram _startProgram;

        /// <summary>
        /// Bots array
        /// </summary>
        public List<BotPanel> PanelsArray;

        /// <summary>
        /// The bot to which the interface is currently connected
        /// </summary>
        private BotPanel _activPanel;

        /// <summary>
        /// Load robots with saved names
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\Settings" + _typeWorkKeeper + "Keeper.txt"))
            { 
                // if there is no file we need. Just go out
                return;
            }


            int botCount = 0;
            using (StreamReader reader = new StreamReader(@"Engine\Settings" + _typeWorkKeeper + "Keeper.txt"))
            {
                while (!reader.EndOfStream)
                {
                    if (!string.IsNullOrWhiteSpace(reader.ReadLine()))
                    {
                        botCount++;
                    }
                }
            }

            if (botCount == 0)
            {
                return;
            }

            PanelsArray = new List<BotPanel>();

            int botIterator = 0;
            using (StreamReader reader = new StreamReader(@"Engine\Settings" + _typeWorkKeeper + "Keeper.txt"))
            {
                while (!reader.EndOfStream)
                {
                    string[] names = reader.ReadLine().Split('@');

                    BotPanel bot = null;

                    if (names.Length > 2)
                    {
                        try
                        {
                            bot = BotFactory.GetStrategyForName(names[1], names[0], _startProgram, Convert.ToBoolean(names[2]));
                        }
                        catch (Exception e)
                        {
                            MessageBox.Show(" Error on bot creation. Bot Name: " + names[1] + " \n" + e.ToString());
                            continue;
                        }
                    }
                    else
                    {
                        bot = BotFactory.GetStrategyForName(names[1], names[0], _startProgram, false);
                    }

                    if(names.Length >= 4)
                    {
                        if(string.IsNullOrEmpty(names[3]) == false)
                        {
                            bot.PublicName = names[3];
                        }
                    }

                    if (bot != null)
                    {
                        PanelsArray.Add(bot);

                        if (BotCreateEvent != null)
                        {
                            BotCreateEvent(bot);
                        }

                        if (_tabBotNames != null)
                        {
                            _tabBotNames.Items.Add(" " + PanelsArray[botIterator].NameStrategyUniq + " ");
                            SendNewLogMessage(OsLocalization.Trader.Label2 + PanelsArray[botIterator].NameStrategyUniq,
                                LogMessageType.System);
                        }

                        botIterator++;

                        bot.NewTabCreateEvent += () =>
                        {
                            ReloadRiskJournals();
                        };
                    }
                }
            }
            if (PanelsArray.Count != 0)
            {
                ReloadActivBot(PanelsArray[0]);
            }
        }

        /// <summary>
        /// Save robots names
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\Settings" + _typeWorkKeeper + "Keeper.txt", false))
                {
                    for (int i = 0; PanelsArray != null && i < PanelsArray.Count; i++)
                    {
                        if(PanelsArray[i].IsScript == false)
                        {
                            writer.WriteLine(PanelsArray[i].NameStrategyUniq + "@" +
                                             PanelsArray[i].GetNameStrategyType() +
                                              "@" + false
                                              + "@" + PanelsArray[i].PublicName);
                        }
                        else
                        {
                            writer.WriteLine(PanelsArray[i].NameStrategyUniq + "@" +
                            PanelsArray[i].FileName +
                            "@" + true
                             + "@" + PanelsArray[i].PublicName);
                        }
                    }

                    writer.Close();
                }
            }
            catch
            {
                // ignored
            }
        }

        /// <summary>
        /// Changed selected item
        /// </summary>
        void _tabBotControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_activPanel != null && _tabBotNames.SelectedItem.ToString() == _activPanel.NameStrategyUniq ||
                    _tabBotNames.SelectedItem == null)
                {
                    return;
                }

                SetNewActivBotFromName(_tabBotNames.SelectedItem.ToString());
                if (_activPanel != null)
                {
                    _tabBotNames.IsEnabled = false;

                    Task task = new Task(TabEnadler);
                    task.Start();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// The method allows to touch the tabs with the names of robots, after redrawing the window with candles
        /// </summary>
        private async void TabEnadler()
        {
            try
            {
                if (_tabBotNames != null && !_tabBotNames.Dispatcher.CheckAccess())
                {
                    await Task.Delay(1000);
                    _tabBotNames.Dispatcher.Invoke(TabEnadler);
                    return;
                }

                if (_tabBotNames != null)
                {
                    _tabBotNames.IsEnabled = true;
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Assign draw new active bot
        /// </summary>
        private void ReloadActivBot(BotPanel newActivBot)
        {
            try
            {
                if (_activPanel != null && _gridChart != null)
                {
                    _activPanel.StopPaint();
                }

                _activPanel = newActivBot;

                if (_tabBotNames == null)
                {
                    return;
                }

                _activPanel.StartPaint(_gridChart, _hostChart, _hostGlass, _hostOpenDeals, _hostCloseDeals, _hostboxLog,
                    _rectangleAroundChart, _hostAlerts, _tabBotTab, _textBoxLimitPrice, _gridChartControlPanel, _textBoxVolume);

                _tabBotNames.SelectionChanged -= _tabBotControl_SelectionChanged;

                _tabBotNames.SelectedItem = " " + _activPanel.NameStrategyUniq + " ";

                if (_tabBotNames.SelectedItem == null || _tabBotNames.SelectedItem.ToString() != " " + _activPanel.NameStrategyUniq + " ")
                {
                    _tabBotNames.Items.Add(" " + _activPanel.NameStrategyUniq + " ");
                    _tabBotNames.SelectedItem = " " + _activPanel.NameStrategyUniq + " ";
                }

                _tabBotNames.SelectionChanged += _tabBotControl_SelectionChanged;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Interface for changing the active bot from the form
        /// </summary>
        private void SetNewActivBotFromName(string newBotName)
        {
            try
            {
                if (PanelsArray != null)
                {
                    for (int i = 0; i < PanelsArray.Count; i++)
                    {
                        if (PanelsArray[i].NameStrategyUniq.Replace(" ", "") == newBotName.Replace(" ", ""))
                        {
                            ReloadActivBot(PanelsArray[i]);
                            return;
                        }
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #region Risk manager

        /// <summary>
        /// Risk Manager
        /// </summary>
        private RiskManager.RiskManager _riskManager;

        /// <summary>
        /// Risk manager threw the message
        /// </summary>
        void _riskManager_RiskManagerAlarmEvent(RiskManagerReactionType reactionType)
        {
            try
            {
                if (reactionType == RiskManagerReactionType.CloseAndOff)
                {
                    RiskManagerCloseAndOff();
                }
                else if (reactionType == RiskManagerReactionType.ShowDialog)
                {
                    ShowRiskManagerAlert();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }

        }

        /// <summary>
        /// Reload risk manager logs
        /// </summary>
        public void ReloadRiskJournals()
        {
            if(_startProgram == StartProgram.IsOsOptimizer)
            {
                return;
            }
            try
            {
                _riskManager.ClearJournals();

                if (PanelsArray != null)
                {
                    for (int i = 0; i < PanelsArray.Count; i++)
                    {
                        List<Journal.Journal> journals = PanelsArray[i].GetJournals();

                        for (int i2 = 0; journals != null && i2 < journals.Count; i2++)
                        {
                            _riskManager.SetNewJournal(journals[i2]);

                            if (_globalPositionViewer != null)
                            {
                                _globalPositionViewer.SetJournal(journals[i2]);
                            }
                        }
                    }

                    if (_buyAtStopPosViewer != null)
                    {
                        List<BotTabSimple> allTabs = new List<BotTabSimple>();

                        for (int i = 0; i < PanelsArray.Count; i++)
                        {
                            if (PanelsArray[i].TabsSimple != null)
                            {
                                allTabs.AddRange(PanelsArray[i].TabsSimple);
                            }

                            if (PanelsArray[i].TabsScreener != null)
                            {
                                for(int i2 = 0;i2 < PanelsArray[i].TabsScreener.Count; i2++)
                                {
                                    if(PanelsArray[i].TabsScreener[i2].Tabs != null)
                                    {
                                        allTabs.AddRange(PanelsArray[i].TabsScreener[i2].Tabs);
                                    }
                                }
                            }
                        }

                        _buyAtStopPosViewer.LoadTabToWatch(allTabs);
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Close all positions and turn off the robot
        /// </summary>
        private void RiskManagerCloseAndOff()
        {
            try
            {
                if (PanelsArray == null)
                {
                    return;
                }

                for (int i = 0; i < PanelsArray.Count; i++)
                {
                    PanelsArray[i].CloseAndOffAllToMarket();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Throw a window with an alert risk manager
        /// </summary>
        private void ShowRiskManagerAlert()
        {
            try
            {
                if (!_hostGlass.Dispatcher.CheckAccess())
                {
                    _hostGlass.Dispatcher.Invoke(ShowRiskManagerAlert);
                    return;
                }

                AlertMessageSimpleUi ui = new AlertMessageSimpleUi(OsLocalization.Trader.Label3);
                ui.Show();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Show risk manager settings
        /// </summary>
        public void ShowRiskManagerDialog()
        {
            try
            {
                _riskManager.ShowDialog();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region PositionOnBoard closing

        private void ServerMaster_ClearPositionOnBoardEvent(string secName, Market.Servers.IServer server, string fullName)
        {
            // важно!!!!
            // сервер должен работать и быть активным

            if(server.ServerStatus != Market.Servers.ServerConnectStatus.Connect)
            {
                if (LogMessageEvent != null)
                {
                    LogMessageEvent(OsLocalization.Market.Label84, LogMessageType.Error);
                }
                return;
            }

            try
            {
                StopBotsWhoTradeSecurity(secName, server);
                CanselOrdersWhithSecurity(secName, server);
                DeleteOpenPositionsWhithSecurity(secName, server);
                ClosePositionOnBoardWhithFakePoses(secName, server, fullName);
            }
            catch (Exception error)
            {
                if(LogMessageEvent != null)
                {
                    LogMessageEvent(error.ToString(), LogMessageType.Error);    
                }
            }
        }

        private void StopBotsWhoTradeSecurity(string secName, Market.Servers.IServer server)
        {
            for (int i = 0; i < PanelsArray.Count; i++)
            {
                BotPanel bot = PanelsArray[i];

                List<BotTabSimple> botTabSimples = GetTabsSimpleWhithMySecurity(secName, server, bot);

                if (botTabSimples != null &&
                    botTabSimples.Count > 0)
                {
                    bot.OnOffEventsInTabs = false;
                }
            }
        }

        private void CanselOrdersWhithSecurity(string secName, Market.Servers.IServer server)
        {

            List<BotTabSimple> tabsWithMySecInAllBots = new List<BotTabSimple>();

            for (int i = 0; i < PanelsArray.Count; i++)
            {
                BotPanel bot = PanelsArray[i];

                List<BotTabSimple> botTabSimples = GetTabsSimpleWhithMySecurity(secName, server, bot);

                if(botTabSimples != null &&
                    botTabSimples.Count > 0)
                {
                    tabsWithMySecInAllBots.AddRange(botTabSimples);
                }
            }

            for(int i = 0;i< tabsWithMySecInAllBots.Count;i++)
            {

                List<Position> poses = tabsWithMySecInAllBots[i].PositionsOpenAll;

                for(int j = 0; poses != null && j < poses.Count; j++)
                {
                    if (poses[j].SecurityName == secName)
                    {
                        tabsWithMySecInAllBots[i].CloseAllOrderToPosition(poses[j]);
                    }
                }
            }

            try
            {
                Security sec = null;

                if (tabsWithMySecInAllBots.Count > 0)
                {
                    sec = tabsWithMySecInAllBots[0].Securiti;
                }

                if(sec != null)
                {
                    AServer aServer = (AServer)server;
                    aServer.CancelAllOrdersToSecurity(sec);
                }
            }
            catch
            {
                // ignore
            }
        }

        private void DeleteOpenPositionsWhithSecurity(string secName, Market.Servers.IServer server)
        {
            List<BotTabSimple> tabsWithMySecInAllBots = new List<BotTabSimple>();

            for (int i = 0; i < PanelsArray.Count; i++)
            {
                BotPanel bot = PanelsArray[i];

                List<BotTabSimple> botTabSimples = GetTabsSimpleWhithMySecurity(secName, server, bot);

                if (botTabSimples != null &&
                    botTabSimples.Count > 0)
                {
                    tabsWithMySecInAllBots.AddRange(botTabSimples);
                }
            }

            for (int i = 0; i < tabsWithMySecInAllBots.Count; i++)
            {
                if(tabsWithMySecInAllBots[i].PositionsOpenAll == null)
                {
                    continue;
                }

                List<Position> poses = new List<Position>();

                poses.AddRange(tabsWithMySecInAllBots[i].PositionsOpenAll);

                for (int j = 0; poses != null && j < poses.Count; j++)
                {
                    if (poses[j].SecurityName == secName)
                    {
                        tabsWithMySecInAllBots[i]._journal.DeletePosition(poses[j]);
                    }
                }
            }
        }

        private void ClosePositionOnBoardWhithFakePoses(string secName, Market.Servers.IServer server, string fullName)
        {
            PositionOnBoard myPosOnBoards = null;

            for(int i = 0;i < server.Portfolios.Count;i++)
            {
                Portfolio portf = server.Portfolios[i];

                List<PositionOnBoard> posesInPortfolio = portf.GetPositionOnBoard();

                if(posesInPortfolio == null)
                {
                    continue;
                }

                for (int j = 0;j < posesInPortfolio.Count;j++)
                {
                    if (posesInPortfolio[j].SecurityNameCode == fullName)
                    {
                        myPosOnBoards = posesInPortfolio[j];
                        break;
                    }
                }
            }

            if(myPosOnBoards == null)
            {
                return;
            }

            if(myPosOnBoards.ValueCurrent == 0)
            {
                return;
            }

            Security sec = GetSecurityFromTabs(secName, server);

            if(sec == null)
            {
                return;
            }

            Order order = CreateOrderToClosePosition(secName, server, myPosOnBoards, sec);

            server.ExecuteOrder(order);
        }

        private Order CreateOrderToClosePosition(string secName, Market.Servers.IServer server, PositionOnBoard pos, Security sec)
        {
            Order newOrder = new Order();

            newOrder.NumberUser = NumberGen.GetNumberOrder(_startProgram);
            newOrder.State = OrderStateType.Activ;
            newOrder.Volume = Math.Abs(pos.ValueCurrent);
            newOrder.Price = 0;
            newOrder.TimeCreate = server.ServerTime;
            newOrder.TypeOrder = OrderPriceType.Market;
            newOrder.SecurityNameCode = secName;
            newOrder.SecurityClassCode = sec.NameClass;
            newOrder.PortfolioNumber = pos.PortfolioName;
            newOrder.ServerType = server.ServerType;
            newOrder.PositionConditionType = OrderPositionConditionType.Close;

            if(pos.ValueCurrent > 0)
            {
                newOrder.Side = Side.Sell;
            }
            else
            {
                newOrder.Side = Side.Buy;
            }

            return newOrder;
        }

        private List<BotTabSimple> GetTabsSimpleWhithMySecurity(string secName, Market.Servers.IServer server, BotPanel bot)
        {
            List<BotTabSimple> botTabSimples = new List<BotTabSimple>();

            if (bot.TabsSimple != null &&
                bot.TabsSimple.Count > 0)
            {
                for (int i = 0; i < bot.TabsSimple.Count; i++)
                {
                    if (bot.TabsSimple[i].Connector.SecurityName == secName 
                        && bot.TabsSimple[i].Connector.ServerType == server.ServerType)
                    {
                        botTabSimples.Add(bot.TabsSimple[i]);
                    }
                }

            }

            if (bot.TabsScreener != null &&
                bot.TabsScreener.Count > 0)
            {
                for (int j = 0; j < bot.TabsScreener.Count; j++)
                {
                    List<BotTabSimple> tabsInScreener = bot.TabsScreener[j].Tabs;

                    for (int i = 0; tabsInScreener != null && i < tabsInScreener.Count; i++)
                    {
                        if (tabsInScreener[i].Connector.SecurityName == secName
                            && tabsInScreener[i].Connector.ServerType == server.ServerType)
                        {
                            botTabSimples.Add(tabsInScreener[i]);
                        }
                    }
                }
            }

            return botTabSimples;
        }

        private Security GetSecurityFromTabs(string secName, Market.Servers.IServer server)
        {
            List<BotTabSimple> tabsWithMySecInAllBots = new List<BotTabSimple>();

            for (int i = 0; i < PanelsArray.Count; i++)
            {
                BotPanel bot = PanelsArray[i];

                List<BotTabSimple> botTabSimples = GetTabsSimpleWhithMySecurity(secName, server, bot);

                if (botTabSimples != null &&
                    botTabSimples.Count > 0)
                {
                    tabsWithMySecInAllBots.AddRange(botTabSimples);
                }
            }

            if(tabsWithMySecInAllBots == null || tabsWithMySecInAllBots.Count == 0)
            {
                return null;
            }

            Security sec = null;

            if (tabsWithMySecInAllBots.Count > 0)
            {
                sec = tabsWithMySecInAllBots[0].Securiti;
            }


            return sec;
        }


        #endregion


        #region Journal

        private JournalUi2 _journalUi2;

        private JournalUi _journalUi1;

        /// <summary>
        /// Show community journal
        /// </summary>
        /// <param name="journalVersion">journal version</param>
        /// <param name="top">top padding for window</param>
        /// <param name="left">left padding for the window</param>
        public void ShowCommunityJournal(int journalVersion, double top, double left)
        {
            try
            {
                if (PanelsArray == null ||
                PanelsArray.Count == 0)
                {
                    return;
                }

                if (_journalUi2 != null)
                {
                    _journalUi2.Activate();
                    return;
                }

                if (_journalUi1 != null)
                {
                    _journalUi1.Activate();
                    return;
                }

                List<BotPanelJournal> panelsJournal = new List<BotPanelJournal>();

                for (int i = 0; i < PanelsArray.Count; i++)
                {
                    List<Journal.Journal> journals = PanelsArray[i].GetJournals();

                    if (journals == null)
                    {
                        continue;
                    }

                    BotPanelJournal botPanel = new BotPanelJournal();
                    botPanel.BotName = PanelsArray[i].NameStrategyUniq;
                    botPanel.BotClass = PanelsArray[i].GetNameStrategyType();
                    botPanel._Tabs = new List<BotTabJournal>();

                    for (int i2 = 0; journals != null && i2 < journals.Count; i2++)
                    {
                        BotTabJournal botTabJournal = new BotTabJournal();
                        botTabJournal.TabNum = i2;
                        botTabJournal.Journal = journals[i2];
                        botPanel._Tabs.Add(botTabJournal);
                    }

                    panelsJournal.Add(botPanel);
                }

                if (journalVersion == 2)
                {
                    _journalUi2 = new JournalUi2(panelsJournal, _startProgram);
                    _journalUi2.LogMessageEvent += SendNewLogMessage;
                    _journalUi2.Closed += _journalUi_Closed;

                    if (top != 0 && left != 0)
                    {
                        _journalUi2.Top = top;
                        _journalUi2.Left = left;
                    }
                    else
                    {
                        _journalUi2.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    }

                    _journalUi2.Show();
                }
                if (journalVersion == 1)
                {
                    _journalUi1 = new JournalUi(panelsJournal, _startProgram);
                    _journalUi1.LogMessageEvent += SendNewLogMessage;
                    _journalUi1.Closed += _journalUi_Closed;

                    if (top != 0 && left != 0)
                    {
                        _journalUi1.Top = top;
                        _journalUi1.Left = left;
                    }
                    else
                    {
                        _journalUi1.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    }

                    _journalUi1.Show();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Window close event handler
        /// </summary>
        private void _journalUi_Closed(object sender, EventArgs e)
        {
            if (_journalUi2 != null)
            {
                _journalUi2.LogMessageEvent -= SendNewLogMessage;
                _journalUi2.Closed -= _journalUi_Closed;
                _journalUi2.IsErase = true;
                _journalUi2 = null;
            }

            if (_journalUi1 != null)
            {
                _journalUi1.LogMessageEvent -= SendNewLogMessage;
                _journalUi1.Closed -= _journalUi_Closed;
                _journalUi1.IsErase = true;
                _journalUi1 = null;
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        #endregion

        #region Global positions

        private GlobalPositionViewer _globalPositionViewer;

        /// <summary>
        /// Create a global table with positions
        /// </summary>
        public void CreateGlobalPositionController(WindowsFormsHost hostActivePoses)
        {
            _globalPositionViewer = new GlobalPositionViewer(hostActivePoses, null, _startProgram);
            _globalPositionViewer.LogMessageEvent += SendNewLogMessage;
            _globalPositionViewer.UserSelectActionEvent += _globalController_UserSelectActionEvent;
            _globalPositionViewer.UserClickOnPositionShowBotInTableEvent += _globalPositionViewer_UserClickOnPositionShowBotInTableEvent;
            _globalPositionViewer.StartPaint();
            ReloadRiskJournals();
        }

        /// <summary>
        /// Create a global table with positions
        /// </summary>
        public void CreateGlobalPositionController(WindowsFormsHost hostActivePoses, WindowsFormsHost hostHistoricalPoses)
        {
            _globalPositionViewer = new GlobalPositionViewer(hostActivePoses, hostHistoricalPoses, _startProgram);
            _globalPositionViewer.LogMessageEvent += SendNewLogMessage;
            _globalPositionViewer.UserSelectActionEvent += _globalController_UserSelectActionEvent;
            _globalPositionViewer.UserClickOnPositionShowBotInTableEvent += _globalPositionViewer_UserClickOnPositionShowBotInTableEvent;
            _globalPositionViewer.StartPaint();
            ReloadRiskJournals();
        }

        /// <summary>
        /// The user clicked on the table with positions
        /// </summary>
        /// <param name="botTabName">The name of the tab that was active when the event was generated</param>
        private void _globalPositionViewer_UserClickOnPositionShowBotInTableEvent(string botTabName)
        {
            if (UserClickOnPositionShowBotInTableEvent != null)
            {
                UserClickOnPositionShowBotInTableEvent(botTabName);
            }
        }

        public event Action<string> UserClickOnPositionShowBotInTableEvent;

        /// <summary>
        /// The user has selected a position
        /// </summary>
        /// <param name="pos">position</param>
        /// <param name="signal">Acktion signal</param>
        private void _globalController_UserSelectActionEvent(Position pos, SignalType signal)
        {
            for (int i = 0; i < PanelsArray.Count; i++)
            {
                PanelsArray[i].UserSetPositionAction(pos, signal);
            }
        }

        #endregion

        #region control and drawing of BuyAtStop / SellAtStop positions

        private BuyAtStopPositionsViewer _buyAtStopPosViewer;

        /// <summary>
        /// Create view for BuyAtStop positions
        /// </summary>
        public void CreateBuyAtStopPosViewer(WindowsFormsHost hostActivePoses)
        {
            _buyAtStopPosViewer = new BuyAtStopPositionsViewer(hostActivePoses, _startProgram);
            _buyAtStopPosViewer.LogMessageEvent += SendNewLogMessage;
            _buyAtStopPosViewer.UserSelectActionEvent += _buyAtStopPosViewer_UserSelectActionEvent;
            _buyAtStopPosViewer.UserClickOnPositionShowBotInTableEvent += _globalPositionViewer_UserClickOnPositionShowBotInTableEvent;
            ReloadRiskJournals();
        }

        /// <summary>
        /// The user has selected an action on the BuyAtStop position view
        /// </summary>
        /// <param name="ordNum">order number</param>
        /// <param name="signal">Action signal</param>
        private void _buyAtStopPosViewer_UserSelectActionEvent(int ordNum, SignalType signal)
        {
            try
            {
                if (PanelsArray != null)
                {
                    List<BotTabSimple> allTabs = new List<BotTabSimple>();

                    for (int i = 0; i < PanelsArray.Count; i++)
                    {
                        if(PanelsArray[i].TabsSimple != null)
                        {
                            allTabs.AddRange(PanelsArray[i].TabsSimple);
                        }
                      
                        if (PanelsArray[i].TabsScreener != null)
                        {
                            for (int i2 = 0; i2 < PanelsArray[i].TabsScreener.Count; i2++)
                            {
                                if (PanelsArray[i].TabsScreener[i2].Tabs != null)
                                {
                                    allTabs.AddRange(PanelsArray[i].TabsScreener[i2].Tabs);
                                }
                            }
                        }
                    }

                    for(int i = 0;i < allTabs.Count; i++)
                    {
                        if(signal == SignalType.DeleteAllPoses)
                        {
                            allTabs[i].BuyAtStopCancel();
                            allTabs[i].SellAtStopCancel();
                        }
                        else
                        {
                           for(int i2 = 0;i2 < allTabs[i].PositionOpenerToStop.Count;i2++)
                            {
                               if(allTabs[i].PositionOpenerToStop[i2].Number == ordNum)
                                {
                                    allTabs[i].PositionOpenerToStop.RemoveAt(i2);
                                    allTabs[i].UpdateStopLimits();
                                    return;
                                }
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

        #endregion

        #region log

        /// <summary>
        /// Log
        /// </summary>
        private Log _log;

        /// <summary>
        /// Send a new message 
        /// </summary>
        public void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            {
                MessageBox.Show(message);
            }
        }

        /// <summary>
        /// Outgoing message for log
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion

        // events from the test server

        /// <summary>
        /// Is rewind enabled in the tester
        /// </summary>
        private bool _fastRegimeOn;

        /// <summary>
        /// A signal came from the tester that the user wants to speed up the testing process
        /// </summary>
        void StrategyKeeper_TestingFastEvent()
        {
            if (_fastRegimeOn == false)
            {
                StopPaint();
            }
            else
            {
                StartPaint();
                if(_activPanel != null)
                {
                    _activPanel.MoveChartToTheRight();
                }
               
            }
        }

        /// <summary>
        /// From the tester came the signal that everything should be cleaned
        /// </summary>
        void StrategyKeeper_TestingStartEvent()
        {
            try
            {
                if (_activPanel != null)
                {
                    _activPanel.StartPaint(_gridChart, _hostChart, _hostGlass, _hostOpenDeals, _hostCloseDeals, _hostboxLog,
                        _rectangleAroundChart, _hostAlerts, _tabBotTab, _textBoxLimitPrice, _gridChartControlPanel, _textBoxVolume);
                }

                ReloadRiskJournals();

                _fastRegimeOn = false;

                if (PanelsArray != null)
                {
                    for (int i = 0; i < PanelsArray.Count; i++)
                    {
                        PanelsArray[i].Clear();
                    }
                }
                if (PanelsArray != null)
                {
                    ((TesterServer)ServerMaster.GetServers()[0]).SynhSecurities(PanelsArray.ToList());
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Testing aborted
        /// </summary>
        void StrategyKeeper_TestingEndEvent()
        {
            StartPaint();
        }

        // Disable/Enable Interface

        /// <summary>
        /// Stop drawing the interface
        /// </summary>
        public void StopPaint()
        {
            try
            {
                if (_tabBotNames != null &&
                    !_tabBotNames.Dispatcher.CheckAccess())
                {
                    _tabBotNames.Dispatcher.Invoke(StrategyKeeper_TestingFastEvent);
                    return;
                }

                _fastRegimeOn = true;
                ServerMaster.StopPaint();

                if (_globalPositionViewer != null)
                {
                    _globalPositionViewer.StopPaint();
                }

                if(_buyAtStopPosViewer != null)
                {
                    _buyAtStopPosViewer.StopPaint();
                }

                if(_tabBotNames != null)
                {
                    _tabBotNames.IsEnabled = false;

                    if (_activPanel != null)
                    {
                        _activPanel.StopPaint();
                    }
                }
                
                _log.StopPaint();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Enable interface drawing
        /// </summary>
        public void StartPaint()
        {
            try
            {
               if(_tabBotNames != null)
                {
                    if (!_tabBotNames.Dispatcher.CheckAccess())
                    {
                        _tabBotNames.Dispatcher.Invoke(StrategyKeeper_TestingEndEvent);
                        return;
                    }
                    _tabBotNames.IsEnabled = true;

                    if (_fastRegimeOn)
                    {
                        if (_activPanel != null)
                        {
                            _activPanel.StartPaint(_gridChart, _hostChart, _hostGlass, _hostOpenDeals, _hostCloseDeals, _hostboxLog,
                                _rectangleAroundChart, _hostAlerts, _tabBotTab, _textBoxLimitPrice, _gridChartControlPanel, _textBoxVolume);
                        }
                    }
                }

                if (_fastRegimeOn)
                {
                    if(_globalPositionViewer != null)
                    {
                        _globalPositionViewer.StartPaint();
                    }

                    if (_buyAtStopPosViewer != null)
                    {
                        _buyAtStopPosViewer.StartPaint();
                    }

                    _fastRegimeOn = false;
                    ServerMaster.StartPaint();
                    _log.StartPaint(_hostLogPrime);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Screenr Is Activ
        /// </summary>
        private bool TabsScreenrIsActiv()
        {
            if (_activPanel.TabsScreener == null ||
                _activPanel.TabsScreener.Count == 0)
            {
                return false;
            }
            return true;
        }

        // Storage Management

        /// <summary>
        /// Remove active bot
        /// </summary>
        public void DeleteActiv()
        {
            try
            {
                if (PanelsArray == null ||
               _activPanel == null)
                {
                    return;
                }

                if (TabsScreenrIsActiv())
                {
                    for (int i = 0; i < _activPanel.TabsScreener.Count; i++)
                    {
                        if (_activPanel.TabsScreener[i].IsLoadTabs == true)
                        {
                            SendNewLogMessage(OsLocalization.Trader.Label183, LogMessageType.Error);
                            return;
                        }
                    }
                }

                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label4);
                ui.ShowDialog();

                if (ui.UserAcceptActioin == false)
                {
                    return;
                }

                _activPanel.StopPaint();

                _activPanel.Delete();

                SendNewLogMessage(OsLocalization.Trader.Label5 + _activPanel.NameStrategyUniq, LogMessageType.System);

                PanelsArray.Remove(_activPanel);

                if (BotDeleteEvent != null)
                {
                    BotDeleteEvent(_activPanel);
                }

                _activPanel = null;

                Save();

                if(_tabBotNames != null)
                {
                    _tabBotNames.Items.Clear();

                    if (PanelsArray != null && PanelsArray.Count != 0)
                    {
                        for (int i = 0; i < PanelsArray.Count; i++)
                        {
                            _tabBotNames.Items.Add(" " + PanelsArray[i].NameStrategyUniq + " ");
                        }

                        ReloadActivBot(PanelsArray[0]);
                    }
                }

                ReloadRiskJournals();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Delete robot by index
        /// </summary>
        public void DeleteByNum(int index)
        {
            BotPanel botToDel = PanelsArray[index];

            ReloadActivBot(botToDel);

            DeleteActiv();
        }

        /// <summary>
        /// Create bot
        /// </summary>
        public void CreateNewBot()
        {
            try
            {
                BotCreateUi2 ui = new BotCreateUi2(BotFactory.GetNamesStrategy(),
                    BotFactory.GetScriptsNamesStrategy(), _startProgram);

                ui.ShowDialog();

                if (ui.IsAccepted == false)
                {
                    return;
                }

                if(string.IsNullOrEmpty(ui.NameStrategy))
                {
                    CustomMessageBoxUi box = new CustomMessageBoxUi(OsLocalization.Trader.Label304);
                    box.ShowDialog();
                    return;
                }

                if (File.Exists(@"Engine\" + @"SettingsRealKeeper.txt"))
                {
                    using (StreamReader reader = new StreamReader(@"Engine\" + @"SettingsRealKeeper.txt"))
                    {
                        while (!reader.EndOfStream)
                        {
                            string[] str = reader.ReadLine().Split('@');

                            if (str[0] == ui.NameBot)
                            {
                                CustomMessageBoxUi box = new CustomMessageBoxUi(OsLocalization.Trader.Label8);
                                box.ShowDialog();
                                return;
                            }
                        }
                    }
                }

                if (File.Exists(@"Engine\" + @"SettingsTesterKeeper.txt"))
                {
                    using (StreamReader reader = new StreamReader(@"Engine\" + @"SettingsTesterKeeper.txt"))
                    {
                        while (!reader.EndOfStream)
                        {
                            string[] str = reader.ReadLine().Split('@');

                            if (str[0] == ui.NameBot)
                            {
                                CustomMessageBoxUi box = new CustomMessageBoxUi(OsLocalization.Trader.Label8);
                                box.ShowDialog();
                                return;
                            }
                        }
                    }
                }

                BotPanel newRobot = BotFactory.GetStrategyForName(ui.NameStrategy, ui.NameBot, _startProgram, ui.IsScript);

                if (PanelsArray == null)
                {
                    PanelsArray = new List<BotPanel>();
                }
                PanelsArray.Add(newRobot);

                if(BotCreateEvent != null)
                {
                    BotCreateEvent(newRobot);
                }

                SendNewLogMessage(OsLocalization.Trader.Label9 + newRobot.NameStrategyUniq, LogMessageType.System);

                ReloadActivBot(newRobot);
                Save();

                ReloadRiskJournals();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Reload active robot
        /// </summary>
        public void HotUpdateActiveBot()
        {
            SendNewLogMessage(OsLocalization.Trader.Label161, LogMessageType.System);
            
            HotUpdateResult<BotPanel> result = HotUpdateManager.Instance.Update(_activPanel);
            if (HotUpdateResultStatus.Success == result.Status)
            {
                ReloadActivBot(result.UpdatedObject);
                Save();
                ReloadRiskJournals();
                SendNewLogMessage(OsLocalization.Trader.Label162, LogMessageType.System);
            }
            else
            {
                SendNewLogMessage(OsLocalization.Trader.Label163 + $". {result.ErrorMessage}.", LogMessageType.System);
            }
        }

        // Robot control

        /// <summary>
        /// Show the position tracking settings for the robot
        /// </summary>
        public void BotManualSettingsDialog()
        {
            try
            {
                if (_activPanel == null)
                {
                    MessageBox.Show(OsLocalization.Trader.Label10);
                    return;
                }

                if (_activPanel.ActivTab != null &&
                    _activPanel.ActivTab.GetType().Name == "BotTabSimple")
                {
                    ((BotTabSimple)_activPanel.ActivTab).ShowManualControlDialog();
                }
                else
                {
                    MessageBox.Show(OsLocalization.Trader.Label11);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Show individual robot settings
        /// </summary>
        public void BotIndividualSettings()
        {
            try
            {
                if (_activPanel == null)
                {
                    MessageBox.Show(OsLocalization.Trader.Label10);
                    return;
                }
                _activPanel.ShowIndividualSettingsDialog();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Show the tool connection settings for the robot
        /// </summary>
        public void BotTabConnectorDialog()
        {
            try
            {
                if (_activPanel == null)
                {
                    MessageBox.Show(OsLocalization.Trader.Label10);
                    return;
                }
                if (_activPanel.ActivTab != null &&
                    _activPanel.ActivTab.GetType().Name == "BotTabSimple")
                {
                    ((BotTabSimple)_activPanel.ActivTab).ShowConnectorDialog();
                }
                else if (_activPanel.ActivTab != null &&
                    _activPanel.ActivTab.GetType().Name == "BotTabIndex")
                {
                    ((BotTabIndex)_activPanel.ActivTab).ShowDialog();
                }
                else if (_activPanel.ActivTab != null &&
                         _activPanel.ActivTab.GetType().Name == "BotTabCluster")
                {
                    ((BotTabCluster)_activPanel.ActivTab).ShowDialog();
                }
                else if (_activPanel.ActivTab != null &&
                         _activPanel.ActivTab.GetType().Name == "BotTabScreener")
                {
                    ((BotTabScreener)_activPanel.ActivTab).ShowDialog();
                }
                else
                {
                    MessageBox.Show(OsLocalization.Trader.Label11);
                }

                ReloadRiskJournals();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Show the risk manager settings for the robot
        /// </summary>
        public void BotShowRiskManager()
        {
            try
            {
                if (_activPanel == null)
                {
                    MessageBox.Show(OsLocalization.Trader.Label10);
                    return;
                }
                _activPanel.ShowPanelRiskManagerDialog();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Show the settings window for the robot
        /// </summary>
        public void BotShowParametrsDialog()
        {
            try
            {
                if (_activPanel == null)
                {
                    MessageBox.Show(OsLocalization.Trader.Label10);
                    return;
                }
                _activPanel.ShowParametrDialog();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Buy on the market, for active bot
        /// </summary>
        /// <param name="volume">volume</param>
        public void BotBuyMarket(decimal volume)
        {
            try
            {
                if (IsActiv())
                {
                    if (_activPanel.ActivTab.GetType().Name == "BotTabSimple")
                    {
                        BotTabSimple tab = (BotTabSimple)_activPanel.ActivTab;
                        tab.BuyAtMarket(volume);
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Sell on the market, for active bot
        /// </summary>
        /// <param name="volume">volume</param>
        public void BotSellMarket(decimal volume)
        {
            try
            {
                if (IsActiv())
                {
                    if (_activPanel.ActivTab.GetType().Name == "BotTabSimple")
                    {
                        BotTabSimple tab = (BotTabSimple)_activPanel.ActivTab;
                        tab.SellAtMarket(volume);
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Buy limit for active bot
        /// </summary>
        /// <param name="volume">volume</param>
        /// <param name="price">price</param>
        public void BotBuyLimit(decimal volume, decimal price)
        {
            try
            {
                if (IsActiv())
                {
                    if (_activPanel.ActivTab.GetType().Name == "BotTabSimple")
                    {
                        BotTabSimple tab = (BotTabSimple)_activPanel.ActivTab;
                        tab.BuyAtLimit(volume, price);
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Sell limit for active bot
        /// </summary>
        /// <param name="volume">volume</param>
        /// <param name="price">price</param>
        public void BotSellLimit(decimal volume, decimal price)
        {
            try
            {
                if (IsActiv())
                {
                    if (_activPanel.ActivTab.GetType().Name == "BotTabSimple")
                    {
                        BotTabSimple tab = (BotTabSimple)_activPanel.ActivTab;
                        tab.SellAtLimit(volume, price);
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Withdraw all active orders
        /// </summary>
        public void CancelLimits()
        {
            try
            {
                if (IsActiv())
                {
                    if (_activPanel.ActivTab.GetType().Name == "BotTabSimple")
                    {
                        BotTabSimple tab = (BotTabSimple)_activPanel.ActivTab;
                        tab.CloseAllOrderInSystem();
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Is there an actin bot now
        /// </summary>
        private bool IsActiv()
        {
            if (_activPanel == null)
            {
                MessageBox.Show(OsLocalization.Trader.Label10);
                return false;
            }
            return true;
        }

        /// <summary>
        /// get panel(bot) by bot name
        /// </summary>
        /// <param name="botName">bot name</param>
        private BotPanel GetBotByName(string botName)
        {
            try
            {
                if (PanelsArray != null)
                {
                    for (int i = 0; i < PanelsArray.Count; i++)
                    {
                        if (PanelsArray[i].NameStrategyUniq.Replace(" ", "") == botName.Replace(" ", ""))
                        {
                            return PanelsArray[i];
                        }
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }

            return null;
        }

        // lightweight interface

        /// <summary>
        /// Event: Bot created
        /// </summary>
        public event Action<BotPanel> BotCreateEvent;

        /// <summary>
        /// Event: Bot removed
        /// </summary>
        public event Action<BotPanel> BotDeleteEvent;
    }
}
