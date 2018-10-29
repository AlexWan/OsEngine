/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Shapes;
using OsEngine.Alerts;
using OsEngine.Entity;
using OsEngine.Journal;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Connectors;
using OsEngine.Market.Servers.Tester;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.RiskManager;

namespace OsEngine.OsTrader
{

    /// <summary>
    /// класс менеджер управления для роботов
    /// </summary>
    public class OsTraderMaster
    {
        /// <summary>
        /// создать менеджера роботов
        /// </summary>
        /// <param name="hostChart">область для чарта</param>
        /// <param name="hostGlass">область для стакана</param>
        /// <param name="hostOpenDeals">область для таблицы открытых сделок</param>
        /// <param name="hostCloseDeals">область для таблицы закрытых сделок</param>
        /// <param name="hostAllDeals">область всех сделок</param>
        /// <param name="hostLogBot">область для бот лога</param>
        /// <param name="hostLogPrime">область для прайм лога</param>
        /// <param name="rectangleAroundChart">квадрат за чартом</param>
        /// <param name="hostAlerts">область для алертов</param>
        /// <param name="tabPanel">панель с роботами</param>
        /// <param name="tabBotTab">панель робота с вкладками инструментов</param>
        /// <param name="textBoxLimitPrice">текстБокс с ценой лимитника при вводе заявки</param>
        /// <param name="gridChartControlPanel">грид для панели управления чартом</param>
        /// <param name="startProgram">тип программы который запросил создание класса</param>
        public OsTraderMaster(WindowsFormsHost hostChart, WindowsFormsHost hostGlass, WindowsFormsHost hostOpenDeals,
            WindowsFormsHost hostCloseDeals, WindowsFormsHost hostAllDeals, WindowsFormsHost hostLogBot, WindowsFormsHost hostLogPrime, Rectangle rectangleAroundChart, WindowsFormsHost hostAlerts,
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
            _globalController = new GlobalPosition(_hostAllDeals,_startProgram);
            _globalController.LogMessageEvent += SendNewLogMessage;

            _log = new Log("Prime",_startProgram);
            _log.StartPaint(hostLogPrime);
            _log.Listen(this);
            _hostLogPrime = hostLogPrime;

            SendNewLogMessage("Запуск OsTraderMaster. Включение программы.",LogMessageType.User);

            Load();
            _tabBotNames.SelectionChanged += _tabBotControl_SelectionChanged;
            ReloadRiskJournals();
            _globalController.StartPaint();
        }

        private WindowsFormsHost _hostLogPrime;
        private WindowsFormsHost _hostChart;
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
        /// какая программа запустила класс
        /// </summary>
        private StartProgram _startProgram;

        /// <summary>
        /// массив роботов
        /// </summary>
        private List<BotPanel> _panelsArray;

        /// <summary>
        /// бот, к которому сейчас подключен интерфейс
        /// </summary>
        private BotPanel _activPanel;

        /// <summary>
        /// загрузить роботов по сохранённым именам
        /// </summary>
        private void Load() 
        {
            if (!File.Exists(@"Engine\Settings"+ _typeWorkKeeper+"Keeper.txt"))
            { // если нет нужного нам файла. Просто выходим
                return;
            }

            //try
            //{
            // 1 считаем кол-во сохранённых ботов
            int botCount = 0;
            using (StreamReader reader = new StreamReader(@"Engine\Settings" + _typeWorkKeeper + "Keeper.txt"))
            {// если файл есть. Подключаемся к нему и качаем данные
                // индикаторы
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
            // создаём массив и роботов
            _panelsArray = new List<BotPanel>();

            int botIterator = 0;
            using (StreamReader reader = new StreamReader(@"Engine\Settings" + _typeWorkKeeper + "Keeper.txt"))
            {// если файл есть. Подключаемся к нему и качаем данные
                // индикаторы
                while (!reader.EndOfStream)
                {
                    string[] names = reader.ReadLine().Split('@');
                    BotPanel bot = PanelCreator.GetStrategyForName(names[1], names[0],_startProgram);
                    if (bot != null)
                    {
                        _panelsArray.Add(bot);
                        _tabBotNames.Items.Add(_panelsArray[botIterator].NameStrategyUniq);
                        SendNewLogMessage("Создан новый бот " + _panelsArray[botIterator].NameStrategyUniq,
                            LogMessageType.System);
                        botIterator++;
                    }

                }
            }
            if (_panelsArray.Count != 0)
            {
                ReloadActivBot(_panelsArray[0]);
            }
            

            // }
            // catch
            // {
            // ignored
            // }


        }

        /// <summary>
        /// сохранить имена роботов
        /// </summary>
        private void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\Settings" + _typeWorkKeeper + "Keeper.txt", false))
                { // создаём файл и записываем в него данные настроек

                    for (int i = 0; _panelsArray != null && i < _panelsArray.Count; i++)
                    {
                        writer.WriteLine(_panelsArray[i].NameStrategyUniq + "@" + _panelsArray[i].GetNameStrategyType());
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
                    Thread worker = new Thread(TabEnadler);
                    worker.CurrentCulture = new CultureInfo("ru-RU");
                    worker.IsBackground = true;
                    worker.Start();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// метод разрешающий трогать вкладки с именами роботов, после перерисовки окна со свечками
        /// </summary>
        private void TabEnadler()
        {
            try
            {
                if (_tabBotNames != null && !_tabBotNames.Dispatcher.CheckAccess())
                {
                    Thread.Sleep(1000);
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

                _activPanel.StartPaint(_hostChart, _hostGlass, _hostOpenDeals, _hostCloseDeals, _hostboxLog,
                    _rectangleAroundChart, _hostAlerts, _tabBotTab, _textBoxLimitPrice, _gridChartControlPanel);



                _tabBotNames.SelectionChanged -= _tabBotControl_SelectionChanged;

                _tabBotNames.SelectedItem = _activPanel.NameStrategyUniq;

                if (_tabBotNames.SelectedItem == null || _tabBotNames.SelectedItem.ToString() != _activPanel.NameStrategyUniq)
                {
                    _tabBotNames.Items.Add(_activPanel.NameStrategyUniq);
                    _tabBotNames.SelectedItem = _activPanel.NameStrategyUniq;
                }

                _tabBotNames.SelectionChanged += _tabBotControl_SelectionChanged;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// интерфейс для смены активного бота с формы
        /// </summary>
        private void SetNewActivBotFromName(string newBotName)
        {
            try
            {
                if (_panelsArray != null)
                {
                    for (int i = 0; i < _panelsArray.Count; i++)
                    {
                        if (_panelsArray[i].NameStrategyUniq == newBotName)
                        {
                            ReloadActivBot(_panelsArray[i]);
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

// Глобальный Риск Менеджер

        /// <summary>
        /// риск менеджер
        /// </summary>
        private RiskManager.RiskManager _riskManager;

        /// <summary>
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
        /// перезагрузить риск менеджеру журналы
        /// </summary>
        private void ReloadRiskJournals()
        {
            try
            {
                _riskManager.ClearJournals();
                _globalController.ClearJournals();

                if (_panelsArray != null)
                {
                    for (int i = 0; i < _panelsArray.Count; i++)
                    {
                        List<Journal.Journal> journals = _panelsArray[i].GetJournals();

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
        /// закрыть все позиции и выключить робота
        /// </summary>
        private void RiskManagerCloseAndOff()
        {
            try
            {
                if (_panelsArray == null)
                {
                    return;
                }

                for (int i = 0; i < _panelsArray.Count; i++)
                {
                    _panelsArray[i].CloseAndOffAllToMarket();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
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

                AlertMessageSimpleUi ui = new AlertMessageSimpleUi("Риск менеджер предупреждает о превышении дневного лимита убытков!");
                ui.Show();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
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

// Общая позиция по ботам

        /// <summary>
        /// менеджер общей позиции роботов
        /// </summary>
        private GlobalPosition _globalController;

        /// <summary>
        /// окно журнала
        /// </summary>
        private JournalUi _journalUi;

        /// <summary>
        /// показать журнал по всем роботам
        /// </summary>
        public void ShowCommunityJournal()
        {
            try
            {
                if (_panelsArray == null ||
                _panelsArray.Count == 0)
                {
                    return;
                }

                if (_journalUi != null)
                {
                    _journalUi.Activate();
                    return;
                }

                List<BotPanelJournal> panelsJournal = new List<BotPanelJournal>();

                for (int i = 0; i < _panelsArray.Count; i++)
                {
                    List<Journal.Journal> journals = _panelsArray[i].GetJournals();

                    if (journals == null)
                    {
                        continue;
                    }

                    BotPanelJournal botPanel = new BotPanelJournal();
                    botPanel.BotName = _panelsArray[i].NameStrategyUniq;
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

                _journalUi = new JournalUi(panelsJournal,_startProgram);
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

// Логироавние работы

        /// <summary>
        /// лог
        /// </summary>
        private Log _log;

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

// Дополнительные события из тестового сервера

        /// <summary>
        /// включена ли перемотка в тестере
        /// </summary>
        private bool _fastRegimeOn;

        /// <summary>
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
        /// из тестера пришёл сигнал что надо всё зачистить
        /// </summary>
        void StrategyKeeper_TestingStartEvent()
        {
            try
            {
                if (_activPanel != null)
                {
                    _activPanel.StartPaint(_hostChart, _hostGlass, _hostOpenDeals, _hostCloseDeals, _hostboxLog,
                        _rectangleAroundChart, _hostAlerts, _tabBotTab, _textBoxLimitPrice, _gridChartControlPanel);
                }

                ReloadRiskJournals();

                _fastRegimeOn = false;

                if (_panelsArray != null)
                {
                    for (int i = 0; i < _panelsArray.Count; i++)
                    {
                        _panelsArray[i].Clear();
                    }
                }
                if (_panelsArray != null)
                {
                    ((TesterServer)ServerMaster.GetServers()[0]).SynhSecurities(_panelsArray.ToList());
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

// Отключение / включение интерфейса

        /// <summary>
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
        /// вкллючить прорисовку интерфейса
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
                        _activPanel.StartPaint(_hostChart, _hostGlass, _hostOpenDeals, _hostCloseDeals, _hostboxLog,
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

// Управление хранилищем

        /// <summary>
        /// удалить активного бота
        /// </summary>
        public void DeleteActiv()
        {
            try
            {
                if (_panelsArray == null ||
               _activPanel == null)
                {
                    return;
                }

                AcceptDialogUi ui = new AcceptDialogUi("Вы собираетесь удалить робота. Вы уверены?");
                ui.ShowDialog();

                if (ui.UserAcceptActioin == false)
                {
                    return;
                }

                // 1 отменяем прорисовку текущего бота
                _activPanel.StopPaint();

                _activPanel.Delete();

                // 2 удаляем

                SendNewLogMessage("Бот удалён " + _activPanel.NameStrategyUniq, LogMessageType.System);

                _panelsArray.Remove(_activPanel);

                _activPanel = null;

                // 3 сохраняем

                Save();

                _tabBotNames.Items.Clear();

                // 4 если массив с роботами не пустой. Назначаем нового активного бота

                if (_panelsArray != null && _panelsArray.Count != 0)
                {
                    for (int i = 0; i < _panelsArray.Count; i++)
                    {
                        _tabBotNames.Items.Add(_panelsArray[i].NameStrategyUniq);
                    }

                    ReloadActivBot(_panelsArray[0]);
                }

                // перегружаем риск менеджер
                ReloadRiskJournals();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// создать бота
        /// </summary>
        public void CreateNewBot()
        {
            try
            {
                // 1 вызываем диалог для выбора имени робота
                PanelCreateUi ui = new PanelCreateUi();
                ui.ShowDialog();

                if (ui.IsAccepted == false)
                {
                    return;
                }

                if (ui.NameStrategy == "Martingale")
                {
                    if (ui.NameBot.Split('h').Length != 1)
                    {
                        MessageBox.Show("Невозможно завершить создание робота. Символ h зарезервирован для ситсемы");
                        return;
                    }
                    if (ui.NameBot.Split('l').Length != 1)
                    {
                        MessageBox.Show("Невозможно завершить создание робота. Символ l зарезервирован для ситсемы");
                        return;
                    }
                }

                // 2 проверяем, что имя робота не нарушает никаких правил

                if (File.Exists(@"Engine\" + @"SettingsRealKeeper.txt"))
                {
                    using (StreamReader reader = new StreamReader(@"Engine\" + @"SettingsRealKeeper.txt"))
                    {
                        while (!reader.EndOfStream)
                        {
                            string[] str = reader.ReadLine().Split('@');

                            if (str[0] == ui.NameBot)
                            {
                                MessageBox.Show("Не возможно завершить создание робота. Робот с таким именем уже существует.");
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
                                MessageBox.Show("Не возможно завершить создание робота. Робот с таким именем уже существует.");
                                return;
                            }
                        }
                    }
                }

                // 3 создаём робота и сохраняем

                BotPanel newRobot = PanelCreator.GetStrategyForName(ui.NameStrategy, ui.NameBot, _startProgram);

                if (_panelsArray == null)
                {
                    _panelsArray = new List<BotPanel>();
                }
                _panelsArray.Add(newRobot);

                SendNewLogMessage("Создан новый бот " + newRobot.NameStrategyUniq, LogMessageType.System);

                ReloadActivBot(newRobot);
                Save();

                // перегружаем риск менеджер
                ReloadRiskJournals();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

// Управление роботом

        /// <summary>
        /// показать настройки сопровождения позиции для робота
        /// </summary>
        public void BotManualSettingsDialog()
        {
            try
            {
                if (_activPanel == null)
                {
                    MessageBox.Show("Операция не может быть завершена, т.к. бот не активен");
                    return;
                }

                if (_activPanel.ActivTab != null &&
                    _activPanel.ActivTab.GetType().Name == "BotTabSimple")
                {
                    ((BotTabSimple)_activPanel.ActivTab).ShowManualControlDialog();
                }
                else
                {
                    MessageBox.Show("Данная функция доступна только у вкладки с инструментом для торговли");
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// показать индивидуальные настройки робота
        /// </summary>
        public void BotIndividualSettings()
        {
            try
            {
                if (_activPanel == null)
                {
                    MessageBox.Show("Операция не может быть завершена, т.к. бот не активен");
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
        /// показать настройки подключения инструмента для робота
        /// </summary>
        public void BotTabConnectorDialog()
        {
            try
            {
                if (_activPanel == null)
                {
                    MessageBox.Show("Операция не может быть завершена, т.к. бот не активен");
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
                else
                {
                    MessageBox.Show("Данная функция у данной вкладки не доступна");
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// показать настройки риск менеджера для робота
        /// </summary>
        public void BotShowRiskManager()
        {
            try
            {
                if (_activPanel == null)
                {
                    MessageBox.Show("Операция не может быть завершена, т.к. бот не активен");
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
        /// показать окно настроек параметров для робота
        /// </summary>
        public void BotShowParametrsDialog()
        {
            try
            {
                if (_activPanel == null)
                {
                    MessageBox.Show("Операция не может быть завершена, т.к. бот не активен");
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
        /// купить по маркету, для активного бота
        /// </summary>
        /// <param name="volume">объём</param>
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
        /// продать по маркету, для активного бота
        /// </summary>
        /// <param name="volume">объём</param>
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
        /// купить лимитом, для активного бота
        /// </summary>
        /// <param name="volume">объём</param>
        /// <param name="price">цена</param>
        public void BotBuyLimit(decimal volume,decimal price)
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
        /// продать лимитом, для активного бота
        /// </summary>
        /// <param name="volume">объём</param>
        /// <param name="price">цена</param>
        public void BotSellLimit(decimal volume,decimal price)
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
        /// есть ли актвиный бот сейчас
        /// </summary>
        private bool IsActiv()
        {
            if (_activPanel == null)
            {
                MessageBox.Show("Операция не может быть завершена, т.к. бот не активен");
                return false;
            }
            return true;
        }

    }
}
