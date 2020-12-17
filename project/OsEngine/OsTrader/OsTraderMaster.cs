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
using OsEngine.Market.Servers.Tester;
using OsEngine.OsTrader.AdminPanelApi;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.RiskManager;
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
using OsEngine.PrimeSettings;
using Grid = System.Windows.Controls.Grid;

namespace OsEngine.OsTrader
{

    /// <summary>
    /// class manager for robots
    /// класс менеджер управления для роботов
    /// </summary>
    public class OsTraderMaster
    {
        #region Static Part

        public static OsTraderMaster Master;
        public static AdminApiMaster ApiMaster;

        #endregion

        /// <summary>
        /// create a robot manager
        /// создать менеджера роботов
        /// </summary>
        /// <param name="gridChart">chart area wpf / область для чарта</param>
        /// <param name="hostChart">chart area windows forms / область для чарта</param>
        /// <param name="hostGlass">market depth area / область для стакана</param>
        /// <param name="hostOpenDeals">open positions table area / область для таблицы открытых сделок</param>
        /// <param name="hostCloseDeals">closed positions table area / область для таблицы закрытых сделок</param>
        /// <param name="hostAllDeals">area of all positions / область всех сделок</param>
        /// <param name="hostLogBot">bot log area / область для бот лога</param>
        /// <param name="hostLogPrime">prime log area / область для прайм лога</param>
        /// <param name="rectangleAroundChart">square by chart / квадрат за чартом</param>
        /// <param name="hostAlerts">area for alerts / область для алертов</param>
        /// <param name="tabPanel">bots tabControl / панель с роботами</param>
        /// <param name="tabBotTab">toolbar robot panel / панель робота с вкладками инструментов</param>
        /// <param name="textBoxLimitPrice">Textbox with limit price when entering an position / текстБокс с ценой лимитника при вводе заявки</param>
        /// <param name="gridChartControlPanel">grid for chart control panel / грид для панели управления чартом</param>
        /// <param name="startProgram">type of program that requested class creation / тип программы который запросил создание класса</param>
        public OsTraderMaster(Grid gridChart, WindowsFormsHost hostChart, WindowsFormsHost hostGlass, WindowsFormsHost hostOpenDeals,
            WindowsFormsHost hostCloseDeals, WindowsFormsHost hostAllDeals, WindowsFormsHost hostLogBot, WindowsFormsHost hostLogPrime, Rectangle rectangleAroundChart,
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

            if (_tabBotTab.Items != null)
            {
                _tabBotTab.Items.Clear();
            }

            _gridChart = gridChart;
            _textBoxLimitPrice = textBoxLimitPrice;
            _hostChart = hostChart;
            _hostGlass = hostGlass;
            _hostOpenDeals = hostOpenDeals;
            _hostCloseDeals = hostCloseDeals;
            _hostAllDeals = hostAllDeals;
            _hostboxLog = hostLogBot;
            _rectangleAroundChart = rectangleAroundChart;
            _hostAlerts = hostAlerts;
            _gridChartControlPanel = gridChartControlPanel;

            _tabBotNames = tabPanel;
            _tabBotNames.Items.Clear();

            _riskManager = new RiskManager.RiskManager("GlobalRiskManager", _startProgram);
            _riskManager.RiskManagerAlarmEvent += _riskManager_RiskManagerAlarmEvent;
            _riskManager.LogMessageEvent += SendNewLogMessage;
            _globalController = new GlobalPosition(_hostAllDeals, _startProgram);
            _globalController.LogMessageEvent += SendNewLogMessage;

            _log = new Log("Prime", _startProgram);
            _log.StartPaint(hostLogPrime);
            _log.Listen(this);
            _hostLogPrime = hostLogPrime;

            SendNewLogMessage(OsLocalization.Trader.Label1, LogMessageType.User);

            Load();
            _tabBotNames.SelectionChanged += _tabBotControl_SelectionChanged;
            ReloadRiskJournals();
            _globalController.StartPaint();

            Master = this;

            if (_startProgram == StartProgram.IsOsTrader && PrimeSettingsMaster.AutoStartApi)
            {
                ApiMaster = new AdminApiMaster(Master);
            }
        }

        private WindowsFormsHost _hostLogPrime;
        private WindowsFormsHost _hostChart;
        private Grid _gridChart;
        private WindowsFormsHost _hostGlass;
        private WindowsFormsHost _hostOpenDeals;
        private WindowsFormsHost _hostCloseDeals;
        private WindowsFormsHost _hostAllDeals;
        private WindowsFormsHost _hostboxLog;
        private Rectangle _rectangleAroundChart;
        private WindowsFormsHost _hostAlerts;
        private TabControl _tabBotNames;
        private TabControl _tabBotTab;
        private ConnectorWorkType _typeWorkKeeper;
        private TextBox _textBoxLimitPrice;
        private Grid _gridChartControlPanel;

        /// <summary>
        /// type of program that requested class creation
        /// какая программа запустила класс
        /// </summary>
        private StartProgram _startProgram;

        /// <summary>
        /// bots array
        /// массив роботов
        /// </summary>
        public List<BotPanel> PanelsArray;

        /// <summary>
        /// the bot to which the interface is currently connected
        /// бот, к которому сейчас подключен интерфейс
        /// </summary>
        private BotPanel _activPanel;

        /// <summary>
        /// load robots with saved names
        /// загрузить роботов по сохранённым именам
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\Settings" + _typeWorkKeeper + "Keeper.txt"))
            { // if there is no file we need. Just go out
              // если нет нужного нам файла. Просто выходим
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

                    if (bot != null)
                    {
                        PanelsArray.Add(bot);
                        _tabBotNames.Items.Add(" " + PanelsArray[botIterator].NameStrategyUniq + " ");
                        SendNewLogMessage(OsLocalization.Trader.Label2 + PanelsArray[botIterator].NameStrategyUniq,
                            LogMessageType.System);
                        botIterator++;
                    }

                }
            }
            if (PanelsArray.Count != 0)
            {
                ReloadActivBot(PanelsArray[0]);
            }
        }

        /// <summary>
        /// save robots names
        /// сохранить имена роботов
        /// </summary>
        private void Save()
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
                                              "@" + false);
                        }
                        else
                        {
                            writer.WriteLine(PanelsArray[i].NameStrategyUniq + "@" +
                            PanelsArray[i].FileName +
                            "@" + true);
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
        /// changed selected item
        /// изменился выделенный элемент
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
        /// the method allows to touch the tabs with the names of robots, after redrawing the window with candles
        /// метод разрешающий трогать вкладки с именами роботов, после перерисовки окна со свечками
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
        /// assign draw new active bot
        /// назначить прорисовать нового активного бота
        /// </summary>
        private void ReloadActivBot(BotPanel newActivBot)
        {
            try
            {
                if (_activPanel != null)
                {
                    _activPanel.StopPaint();
                }

                _activPanel = newActivBot;

                _activPanel.StartPaint(_gridChart, _hostChart, _hostGlass, _hostOpenDeals, _hostCloseDeals, _hostboxLog,
                    _rectangleAroundChart, _hostAlerts, _tabBotTab, _textBoxLimitPrice, _gridChartControlPanel);

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
        /// interface for changing the active bot from the form
        /// интерфейс для смены активного бота с формы
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

        // Global Risk Manager
        // Глобальный Риск Менеджер

        /// <summary>
        /// risk Manager
        /// риск менеджер
        /// </summary>
        private RiskManager.RiskManager _riskManager;

        /// <summary>
        /// risk manager threw the message
        /// риск менеджер выбросил сообщение
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
        /// reload risk manager logs
        /// перезагрузить риск менеджеру журналы
        /// </summary>
        private void ReloadRiskJournals()
        {
            try
            {
                _riskManager.ClearJournals();
                _globalController.ClearJournals();

                if (PanelsArray != null)
                {
                    for (int i = 0; i < PanelsArray.Count; i++)
                    {
                        List<Journal.Journal> journals = PanelsArray[i].GetJournals();

                        for (int i2 = 0; journals != null && i2 < journals.Count; i2++)
                        {
                            _riskManager.SetNewJournal(journals[i2]);
                            _globalController.SetJournal(journals[i2]);
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
        /// close all positions and turn off the robot
        /// закрыть все позиции и выключить робота
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
        /// throw a window with an alert risk manager
        /// выбросить окно с оповещением риск менеджера
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
        /// show risk manager settings
        /// показать настройки риск менеджера
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

        // Common position on bots / Общая позиция по ботам

        /// <summary>
        /// general robot position manager
        /// менеджер общей позиции роботов
        /// </summary>
        private GlobalPosition _globalController;

        /// <summary>
        /// journal window
        /// </summary>
        private JournalUi _journalUi;

        /// <summary>
        /// show journal for all robots
        /// показать журнал по всем роботам
        /// </summary>
        public void ShowCommunityJournal()
        {
            try
            {
                if (PanelsArray == null ||
                PanelsArray.Count == 0)
                {
                    return;
                }

                if (_journalUi != null)
                {
                    _journalUi.Activate();
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

                _journalUi = new JournalUi(panelsJournal, _startProgram);
                _journalUi.LogMessageEvent += SendNewLogMessage;
                _journalUi.Closed += _journalUi_Closed;
                _journalUi.Show();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        void _journalUi_Closed(object sender, EventArgs e)
        {
            _journalUi.IsErase = true;
            _journalUi = null;
        }

        // log / логироавние

        /// <summary>
        /// log
        /// лог
        /// </summary>
        private Log _log;

        /// <summary>
        /// send a new message 
        /// выслать новое сообщение на верх
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
        /// outgoing message for log
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        // events from the test server / события из тестового сервера

        /// <summary>
        /// is rewind enabled in the tester
        /// включена ли перемотка в тестере
        /// </summary>
        private bool _fastRegimeOn;

        /// <summary>
        /// a signal came from the tester that the user wants to speed up the testing process
        /// из тестера пришёл сигнал что пользователь хочет ускорить процесс тестирования
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
            }
        }

        /// <summary>
        /// from the tester came the signal that everything should be cleaned
        /// из тестера пришёл сигнал что надо всё зачистить
        /// </summary>
        void StrategyKeeper_TestingStartEvent()
        {
            try
            {
                if (_activPanel != null)
                {
                    _activPanel.StartPaint(_gridChart, _hostChart, _hostGlass, _hostOpenDeals, _hostCloseDeals, _hostboxLog,
                        _rectangleAroundChart, _hostAlerts, _tabBotTab, _textBoxLimitPrice, _gridChartControlPanel);
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
        /// п
        /// </summary>
        void StrategyKeeper_TestingEndEvent()
        {
            StartPaint();
        }

        // Disable/Enable Interface / Отключение/включение интерфейса

        /// <summary>
        /// stop drawing the interface
        /// остановить прорисовку интерфейса
        /// </summary>
        public void StopPaint()
        {
            try
            {
                if (!_tabBotNames.Dispatcher.CheckAccess())
                {
                    _tabBotNames.Dispatcher.Invoke(StrategyKeeper_TestingFastEvent);
                    return;
                }
                if (_activPanel != null)
                {
                    _activPanel.StopPaint();
                }

                _fastRegimeOn = true;
                ServerMaster.StopPaint();
                _globalController.StopPaint();
                _tabBotNames.IsEnabled = false;
                _log.StopPaint();

            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// enable interface drawing
        /// включить прорисовку интерфейса
        /// </summary>
        public void StartPaint()
        {
            try
            {
                if (!_tabBotNames.Dispatcher.CheckAccess())
                {
                    _tabBotNames.Dispatcher.Invoke(StrategyKeeper_TestingEndEvent);
                    return;
                }

                _tabBotNames.IsEnabled = true;
                if (_fastRegimeOn)
                {
                    _globalController.StartPaint();

                    if (_activPanel != null)
                    {
                        _activPanel.StartPaint(_gridChart, _hostChart, _hostGlass, _hostOpenDeals, _hostCloseDeals, _hostboxLog,
                            _rectangleAroundChart, _hostAlerts, _tabBotTab, _textBoxLimitPrice, _gridChartControlPanel);
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

        // Storage Management / Управление хранилищем

        /// <summary>
        /// remove active bot
        /// удалить активного бота
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

                _activPanel = null;

                Save();

                _tabBotNames.Items.Clear();

                if (PanelsArray != null && PanelsArray.Count != 0)
                {
                    for (int i = 0; i < PanelsArray.Count; i++)
                    {
                        _tabBotNames.Items.Add(" " + PanelsArray[i].NameStrategyUniq + " ");
                    }

                    ReloadActivBot(PanelsArray[0]);
                }

                ReloadRiskJournals();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// create bot
        /// создать бота
        /// </summary>
        public void CreateNewBot()
        {
            try
            {
                BotCreateUi ui = new BotCreateUi(BotFactory.GetNamesStrategy(),
                    BotFactory.GetScriptsNamesStrategy(), StartProgram.IsOsTrader);

                ui.ShowDialog();

                if (ui.IsAccepted == false)
                {
                    return;
                }

                if (ui.NameStrategy == "Martingale")
                {
                    if (ui.NameBot.Split('h').Length != 1)
                    {
                        MessageBox.Show(OsLocalization.Trader.Label6);
                        return;
                    }
                    if (ui.NameBot.Split('l').Length != 1)
                    {
                        MessageBox.Show(OsLocalization.Trader.Label7);
                        return;
                    }
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
                                MessageBox.Show(OsLocalization.Trader.Label8);
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
                                MessageBox.Show(OsLocalization.Trader.Label8);
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

        // Robot control / Управление роботом

        /// <summary>
        /// show the position tracking settings for the robot
        /// показать настройки сопровождения позиции для робота
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
        /// show individual robot settings
        /// показать индивидуальные настройки робота
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
        /// show the tool connection settings for the robot
        /// показать настройки подключения инструмента для робота
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
        /// show the risk manager settings for the robot
        /// показать настройки риск менеджера для робота
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
        /// show the settings window for the robot
        /// показать окно настроек параметров для робота
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
        /// buy on the market, for active bot
        /// купить по маркету, для активного бота
        /// </summary>
        /// <param name="volume">volume / объём</param>
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
        /// sell on the market, for active bot
        /// продать по маркету, для активного бота
        /// </summary>
        /// <param name="volume">volume / объём</param>
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
        /// buy limit for active bot
        /// купить лимитом, для активного бота
        /// </summary>
        /// <param name="volume">volume / объём</param>
        /// <param name="price">price / цена</param>
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
        /// sell limit for active bot
        /// продать лимитом, для активного бота
        /// </summary>
        /// <param name="volume">volume / объём</param>
        /// <param name="price">price / цена</param>
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
        /// withdraw all active orders
        /// отозвать все активные ордера
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
        /// is there an actin bot now
        /// есть ли актвиный бот сейчас
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
        /// получить панель(бота) по имени бота
        /// </summary>
        /// <param name="botName">bot name / имя бота</param>
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
    }
}
