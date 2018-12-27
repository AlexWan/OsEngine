/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Shapes;
using OsEngine.Alerts;
using OsEngine.Entity;
using OsEngine.Journal.Internal;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.RiskManager;

namespace OsEngine.OsTrader.Panels
{

    /// <summary>
    /// типы вкладок для панели
    /// </summary>
    public enum BotTabType
    {
        /// <summary>
        /// простая для торговли одного инструмента
        /// </summary>
        Simple,

        /// <summary>
        /// индекс
        /// </summary>
        Index
    }

    public abstract class BotPanel
    {

        /// <summary>
        /// конструктор
        /// </summary>
        protected BotPanel(string name, StartProgram startProgram)
        {
            NameStrategyUniq = name;
            StartProgram = startProgram;

            ReloadTab();

            _riskManager = new RiskManager.RiskManager(NameStrategyUniq, startProgram);
            _riskManager.RiskManagerAlarmEvent += _riskManager_RiskManagerAlarmEvent;

            _log = new Log(name, startProgram);
            _log.Listen(this);
        }

        /// <summary>
        /// уникальный номер/имя робота. Передаётся в конструктор. Участвует в процессе сохранения всех данных связанных с ботом
        /// </summary>
        public string NameStrategyUniq;

        /// <summary>
        /// программа которая запустила робота. Тестер / Робот / Оптимизатор
        /// </summary>
        public StartProgram StartProgram;

// управление

        /// <summary>
        /// взять журналы панели
        /// </summary>
        public List<Journal.Journal> GetJournals()
        {
            List<Journal.Journal> journals = new List<Journal.Journal>();

            for (int i = 0; _botTabs != null && i < _botTabs.Count; i++)
            {
                if (_botTabs[i].GetType().Name == "BotTabSimple")
                {
                    journals.Add(((BotTabSimple)_botTabs[i]).GetJournal());
                }
            }

            return journals;
        }

        /// <summary>
        /// показать окно графиков со сделками
        /// </summary>
        public void ShowChartDialog()
        {
            if (_chartUi == null)
            {
                _chartUi = new BotPanelChartUi(this);
                _chartUi.Show();
            }
            _chartUi.Closed += _chartUi_Closed;
        }


        private BotPanelChartUi _chartUi;
        void _chartUi_Closed(object sender, EventArgs e)
        {
            _chartUi = null;
        }


        /// <summary>
        /// включена ли прорисовка 
        /// </summary>
        private bool _isPainting;

        /// <summary>
        /// начать прорисовку этого робота
        /// </summary> 
        public void StartPaint(WindowsFormsHost hostChart, WindowsFormsHost glass, WindowsFormsHost hostOpenDeals,
            WindowsFormsHost hostCloseDeals, WindowsFormsHost boxLog, Rectangle rectangle, WindowsFormsHost hostAlerts,
            TabControl tabBotTab, TextBox textBoxLimitPrice, Grid gridChartControlPanel)
        {
            if (_isPainting)
            {
                return;
            }

            _tabBotTab = tabBotTab;
            _hostChart = hostChart;
            _hostGlass = glass;
            _hostOpenDeals = hostOpenDeals;
            _hostCloseDeals = hostCloseDeals;
            _rectangle = rectangle;
            _hostAlerts = hostAlerts;
            _textBoxLimitPrice = textBoxLimitPrice;
            _gridChartControlPanel = gridChartControlPanel;
           
            try
            {
                if (!_tabBotTab.Dispatcher.CheckAccess())
                {
                    _tabBotTab.Dispatcher.Invoke(new Action<WindowsFormsHost,WindowsFormsHost,WindowsFormsHost,
                    WindowsFormsHost, WindowsFormsHost,Rectangle,WindowsFormsHost,TabControl,TextBox,Grid> 
                    (StartPaint),hostChart,glass,hostOpenDeals,hostCloseDeals,boxLog,rectangle,hostAlerts,tabBotTab,textBoxLimitPrice);
                    return;
                }

                _log.StartPaint(boxLog);

                _isPainting = true;

                ReloadTab();

                if (ActivTab != null)
                {
                    ChangeActivTab(ActivTab.TabNum);
                }
                else
                {
                    if (_tabBotTab != null
                        && _tabBotTab.Items.Count != 0
                        && _tabBotTab.SelectedItem != null)
                    {
                        ChangeActivTab(_tabBotTab.SelectedIndex);
                    }
                    else if (_tabBotTab != null
                             && _tabBotTab.Items.Count != 0
                             && _tabBotTab.SelectedItem == null)
                    {
                        ChangeActivTab(0);
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// остановить прорисовку этого робота
        /// </summary>
        public void StopPaint()
        {
            if (_isPainting == false)
            {
                return;
            }
            try
            {
                if (!_tabBotTab.Dispatcher.CheckAccess())
                {
                    _tabBotTab.Dispatcher.Invoke(StopPaint);
                    return;
                }

                for (int i = 0; _botTabs != null && i < _botTabs.Count; i++)
                {
                    _botTabs[i].StopPaint();
                    _log.StopPaint();
                }

                _tabBotTab = null;
                _tabBotTab = null;
                _hostChart = null;
                _hostGlass = null;
                _hostOpenDeals = null;
                _hostCloseDeals = null;
                _rectangle = null;
                _hostAlerts = null;
                _textBoxLimitPrice = null;
                _gridChartControlPanel = null;

                _isPainting = false;
                ReloadTab();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private WindowsFormsHost _hostChart;
        private WindowsFormsHost _hostGlass;
        private WindowsFormsHost _hostOpenDeals;
        private WindowsFormsHost _hostCloseDeals;
        private Rectangle _rectangle;
        private WindowsFormsHost _hostAlerts;
        private TextBox _textBoxLimitPrice;
        private Grid _gridChartControlPanel;

        /// <summary>
        /// название класса бота.
        /// </summary>
        public abstract string GetNameStrategyType();

        /// <summary>
        /// подключился ли робот к бирже всеми вкладкам
        /// </summary>
        public bool IsConnected
        {
            get
            {
                for (int i = 0; TabsSimple != null && i < TabsSimple.Count; i++)
                {
                    if (TabsSimple[i].IsConnected == false)
                    {
                        return false;
                    }
                }
                for (int i = 0; TabsIndex != null && i < TabsIndex.Count; i++)
                {
                    if (TabsIndex[i].IsConnected == false)
                    {
                        return false;
                    }
                }

                if (TabsSimple == null &&
                    TabsIndex == null)
                {
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// очистить журнал и графики
        /// </summary>
        public void Clear()
        {
            try
            {
                if (_botTabs == null
                || _botTabs.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < _botTabs.Count; i++)
                {
                    if (_botTabs[i].GetType().Name == "BotTabSimple")
                    {
                        BotTabSimple bot = (BotTabSimple)_botTabs[i];
                        bot.ClearAceberg();
                        bot.BuyAtStopCanсel();
                        bot.SellAtStopCanсel();
                        bot.Clear();
                    }
                    if (_botTabs[i].GetType().Name == "BotTabIndex")
                    {
                        BotTabIndex bot = (BotTabIndex)_botTabs[i];
                        bot.Clear();
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// удалить робота и все дочерние структуры
        /// </summary>
        public void Delete()
        {
            try
            {
                _riskManager.Delete();

                if (_botTabs != null)
                {
                    for (int i = 0; i < _botTabs.Count; i++)
                    {
                        _botTabs[i].Delete();
                    }
                }

                if (File.Exists(@"Engine\" + NameStrategyUniq + @"Parametrs.txt"))
                {
                    File.Delete(@"Engine\" + NameStrategyUniq + @"Parametrs.txt");
                }

                _log.Delete();

                if (DeleteEvent != null)
                {
                    DeleteEvent();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

// показатели торговли робота

        /// <summary>
        /// итоговая прибыль
        /// </summary>
        public decimal TotalProfitInPersent
        {
            get
            {
                List<Journal.Journal> journals = GetJournals();

                if (journals == null ||
                    journals.Count == 0)
                {
                    return 0;
                }

                decimal result = 0;

                for (int i = 0; i < journals.Count; i++)
                {
                    if (journals[i].AllPosition == null ||
                        journals[i].AllPosition.Count == 0)
                    {
                        continue;
                    }
                    result += PositionStaticticGenerator.GetAllProfitPersent(journals[i].AllPosition.ToArray());
                }
                return result;
            }
        }

        /// <summary>
        /// средняя прибыль со сделки
        /// </summary>
        public decimal MiddleProfitInPersent
        {
            get
            {
                List<Journal.Journal> journals = GetJournals();

                if (journals == null ||
                    journals.Count == 0)
                {
                    return 0;
                }

                decimal result = 0;

                for (int i = 0; i < journals.Count; i++)
                {
                    if (journals[i].AllPosition == null ||
                        journals[i].AllPosition.Count == 0)
                    {
                        continue;
                    }
                    result += PositionStaticticGenerator.GetMidleProfitInPersent(journals[i].AllPosition.ToArray());
                }
                return result;
            }
        }

        /// <summary>
        /// профит фактор
        /// </summary>
        public decimal ProfitFactor
        {
            get
            {
                List<Journal.Journal> journals = GetJournals();

                if (journals == null ||
                    journals.Count == 0)
                {
                    return 0;
                }

                decimal result = 0;

                for (int i = 0; i < journals.Count; i++)
                {
                    if (journals[i].AllPosition == null ||
                        journals[i].AllPosition.Count == 0)
                    {
                        continue;
                    }
                    result += PositionStaticticGenerator.GetProfitFactor(journals[i].AllPosition.ToArray());
                }
                return result;
            }
        }

        /// <summary>
        /// максимальная просадка
        /// </summary>
        public decimal MaxDrowDown
        {
            get
            {
                List<Journal.Journal> journals = GetJournals();

                if (journals == null ||
                    journals.Count == 0)
                {
                    return 0;
                }

                decimal result = 0;

                for (int i = 0; i < journals.Count; i++)
                {
                    if (journals[i].AllPosition == null ||
                        journals[i].AllPosition.Count == 0)
                    {
                        continue;
                    }
                    result += PositionStaticticGenerator.GetMaxDownPersent(journals[i].AllPosition.ToArray());
                }
                return result;
            }
        }

        /// <summary>
        /// кол-во выигранных сделок
        /// </summary>
        public decimal WinPositionPersent
        {
            get
            {
                List<Journal.Journal> journals = GetJournals();

                if (journals == null ||
                    journals.Count == 0)
                {
                    return 0;
                }

                decimal winPoses = 0;

                decimal allPoses = 0;

                for (int i = 0; i < journals.Count; i++)
                {
                    if (journals[i].AllPosition == null ||
                        journals[i].AllPosition.Count == 0)
                    {
                        continue;
                    }

                    allPoses += journals[i].AllPosition.Count;
                    List<Position> winPositions = journals[i].AllPosition.FindAll(pos => pos.ProfitOperationPunkt > 0);
                    winPoses += (winPositions.Count);
                }
                return winPoses / allPoses;
            }
        }

// работа с параметрами стратегии

        /// <summary>
        /// показать окно настроек параметров
        /// </summary>
        public void ShowParametrDialog()
        {
            if (_parameters == null ||
                _parameters.Count == 0)
            {
                MessageBox.Show("У данной стратегии нет встроенных параметров для настройки");
                return;
            }
            ParemetrsUi ui = new ParemetrsUi(_parameters);
            ui.ShowDialog();
        }

        /// <summary>
        /// создать параметр типа Decimal
        /// </summary>
        /// <param name="name">Имя параметра</param>
        /// <param name="value">Значение по умолчанию</param>
        /// <param name="start">Первое значение при оптимизации</param>
        /// <param name="stop">Последнее значение при оптимизации</param>
        /// <param name="step">Шаг изменения при оптимизации</param>
        public StrategyParameterDecimal CreateParameter(string name, decimal value, decimal start, decimal stop, decimal step) 
        {
            StrategyParameterDecimal newParameter = new StrategyParameterDecimal(name, value, start, stop, step);

            if (_parameters.Find(p => p.Name == name)!= null)
            {
                throw new Exception("Ахтунг! Параметр с таким именем уже существует!");
            }

            return (StrategyParameterDecimal)LoadParameterValues(newParameter);
        }

        /// <summary>
        /// создать параметр типа Int
        /// </summary>
        /// <param name="name">Имя параметра</param>
        /// <param name="value">Значение по умолчанию</param>
        /// <param name="start">Первое значение при оптимизации</param>
        /// <param name="stop">Последнее значение при оптимизации</param>
        /// <param name="step">Шаг изменения при оптимизации</param>
        public StrategyParameterInt CreateParameter(string name, int value, int start, int stop, int step)
        {
            StrategyParameterInt newParameter = new StrategyParameterInt(name, value, start, stop, step);

            if (_parameters.Find(p => p.Name == name) != null)
            {
                throw new Exception("Ахтунг! Параметр с таким именем уже существует!");
            }

            return (StrategyParameterInt)LoadParameterValues(newParameter);
        }

        /// <summary>
        /// создать параметр типа String
        /// </summary>
        /// <param name="name">Имя параметра</param>
        /// <param name="value">Значение по умолчанию</param>
        /// <param name="collection">Возможные значения для параметра</param>
        public StrategyParameterString CreateParameter(string name, string value, string[] collection)
        {
            StrategyParameterString newParameter = new StrategyParameterString(name, value, collection.ToList());

            if (_parameters.Find(p => p.Name == name) != null)
            {
                throw new Exception("Ахтунг! Параметр с таким именем уже существует!");
            }

            return (StrategyParameterString)LoadParameterValues(newParameter);
        }

        /// <summary>
        /// создать параметр типа Bool
        /// </summary>
        /// <param name="name">Имя параметра</param>
        /// <param name="value">Значение по умолчанию</param>
        public StrategyParameterBool CreateParameter(string name, bool value)
        {
            StrategyParameterBool newParameter = new StrategyParameterBool(name, value);

            if (_parameters.Find(p => p.Name == name) != null)
            {
                throw new Exception("Ахтунг! Параметр с таким именем уже существует!");
            }

            return (StrategyParameterBool)LoadParameterValues(newParameter);
        }

        /// <summary>
        /// загрузить настройки параметра
        /// </summary>
        /// <param name="newParameter">параметр настройки которого нужно загрузить</param>
        /// <returns></returns>
        private IIStrategyParameter LoadParameterValues(IIStrategyParameter  newParameter)
        {
            if (StartProgram != StartProgram.IsOsOptimizer)
            {// запущен тестер или робот, в котором можно менять параметры вручную
                GetValueParameterSaveByUser(newParameter);
            }

            newParameter.ValueChange += Parameter_ValueChange;

            _parameters.Add(newParameter);

            return newParameter;
        }

        /// <summary>
        /// загрузить настройки параметра из файла
        /// </summary>
        /// <param name="parameter"></param>
        private void GetValueParameterSaveByUser(IIStrategyParameter parameter)
        {
            if (!File.Exists(@"Engine\" + NameStrategyUniq + @"Parametrs.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"Parametrs.txt"))
                {
                    while (!reader.EndOfStream)
                    {
                        string[] save = reader.ReadLine().Split('#');

                        if (save[0] == parameter.Name)
                        {
                            parameter.LoadParamFromString(save);
                        }
                    }
                    reader.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// список параметров доступных у панели
        /// </summary>
        public List<IIStrategyParameter> Parameters
        {
            get { return _parameters; }
        } 
        private List<IIStrategyParameter> _parameters = new List<IIStrategyParameter>();

        /// <summary>
        /// у параметра изменились настройки
        /// </summary>
        void Parameter_ValueChange()
        {
            if (StartProgram != StartProgram.IsOsOptimizer)
            {
                SaveParametrs();
            }
            
            if (ParametrsChangeByUser != null)
            {
                ParametrsChangeByUser();
            }
        }

        /// <summary>
        /// сохранить значения параметров
        /// </summary>
        private void SaveParametrs()
        {
            if (_parameters == null ||
                _parameters.Count == 0)
            {
                return;
            }
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"Parametrs.txt", false)
                    )
                {
                    for (int i = 0; i < _parameters.Count; i++)
                    {
                        writer.WriteLine(_parameters[i].GetStringToSave());
                    }

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }


        }

        /// <summary>
        /// у параметра изменилось состояние
        /// </summary>
        public event Action ParametrsChangeByUser;

// риск менеджер панели

        /// <summary>
        /// риск менеджер
        /// </summary>
        private RiskManager.RiskManager _riskManager;

        /// <summary>
        /// пришло оповещение от риск менеджера
        /// </summary>
        void _riskManager_RiskManagerAlarmEvent(RiskManagerReactionType reactionType)
        {
            try
            {
                if (reactionType == RiskManagerReactionType.CloseAndOff)
                {
                    CloseAndOffAllToMarket();
                }
                else if (reactionType == RiskManagerReactionType.ShowDialog)
                {
                    string message = "Риск менеджер предупреждает о превышении дневного лимита убытков!";
                    ShowMessageInNewThread(message);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// прорисовать окошко с сообщением в новом потоке
        /// </summary>
        private void ShowMessageInNewThread(string message)
        {
            try
            {
                if (!_hostChart.CheckAccess())
                {
                    _hostChart.Dispatcher.Invoke(new Action<string>(ShowMessageInNewThread), message);
                    return;
                }

                AlertMessageSimpleUi ui = new AlertMessageSimpleUi(message);
                ui.Show();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// экстренное закрытие всех позиций
        /// </summary>
        public void CloseAndOffAllToMarket() 
        {
            try
            {
                string message = "Риск менеджер предупреждает о превышении дневного лимита убытков! Дальнейшие торги остановлены! Робот: " + NameStrategyUniq;
                ShowMessageInNewThread(message);

                for (int i = 0; i < _botTabs.Count; i++)
                {
                    if (_botTabs[i].GetType().Name == "BotTabSimple")
                    {
                        BotTabSimple bot = (BotTabSimple)_botTabs[i];
                        bot.CloseAllAtMarket();
                        bot.Portfolio = null;
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

// управление вкладками

        /// <summary>
        /// загруженые в панель вкладки
        /// </summary>
        private List<IIBotTab> _botTabs;

        /// <summary>
        /// активная вкладка
        /// </summary>
        public IIBotTab ActivTab;

        /// <summary>
        /// контрол на котором расположены вкладки
        /// </summary>
        private TabControl _tabBotTab;

        /// <summary>
        /// номер открытой вкладки
        /// </summary>
        public int ActivTabNumber
        {
            get
            {
                try
                {
                    if (ActivTab == null || _tabBotTab.Items.Count == 0)
                    {
                        return -1;
                    }
                    if (_tabBotTab.SelectedItem != null)
                    {
                        return Convert.ToInt32(_tabBotTab.SelectedItem.ToString());
                    }
                    return 0;
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
                return 0;
            }
        }

        /// <summary>
        /// простые вкладки для торговли
        /// </summary>
        public List<BotTabSimple> TabsSimple
        {
            get
            {
                try
                {
                    List<BotTabSimple> tabSimples = new List<BotTabSimple>();

                    for (int i = 0; _botTabs != null && i < _botTabs.Count; i++)
                    {
                        if (_botTabs[i].GetType().Name == "BotTabSimple")
                        {
                            tabSimples.Add((BotTabSimple)_botTabs[i]);
                        }
                    }

                    return tabSimples;
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
                return null;
            }
        }

        /// <summary>
        /// вкладки со спредами между инструментами
        /// </summary>
        public List<BotTabIndex> TabsIndex
        {
            get
            {
                try
                {
                    List<BotTabIndex> tabSpreads = new List<BotTabIndex>();

                    for (int i = 0; _botTabs != null && i < _botTabs.Count; i++)
                    {
                        if (_botTabs[i].GetType().Name == "BotTabIndex")
                        {
                            tabSpreads.Add((BotTabIndex)_botTabs[i]);
                        }
                    }

                    return tabSpreads;
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
                return null;
            }
        }

        /// <summary>
        /// пользователь переключил вкладки
        /// </summary>
        void _tabBotTab_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_tabBotTab != null && _tabBotTab.Items.Count != 0)
                {
                    ChangeActivTab(_tabBotTab.SelectedIndex);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// создать вкладку
        /// </summary>
        /// <param name="tabType">тип вкладки</param>
        public void TabCreate(BotTabType tabType)
        {
            try
            {
                int number;

                if (_botTabs == null || _botTabs.Count == 0)
                {
                    number = 0;
                }
                else
                {
                    number = _botTabs.Count;
                }

                string nameTab = NameStrategyUniq + "tab" + number;

                if (_botTabs != null && _botTabs.Find(strategy => strategy.TabName == nameTab) != null)
                {
                    // если мы создаём вкладку программно, то возможно это не первая загрузка и
                    // она уже подгрузилась из сохранения. И тогда мы просто выходим
                    return;
                }

                if (_botTabs == null)
                {
                    _botTabs = new List<IIBotTab>();
                }
                IIBotTab newTab;

                if (tabType == BotTabType.Simple)
                {
                    newTab = new BotTabSimple(nameTab, StartProgram);
                }
                else if (tabType == BotTabType.Index)
                {
                    newTab = new BotTabIndex(nameTab, StartProgram);
                }
                else
                {
                    return;
                }

                _botTabs.Add(newTab);
                newTab.LogMessageEvent += SendNewLogMessage;

                newTab.TabNum = _botTabs.Count - 1;

                ChangeActivTab(_botTabs.Count - 1);

                ReloadTab();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// удалить активную вкладку
        /// </summary>
        public void TabDelete()
        {
            try
            {
                if (ActivTab == null)
                {
                    return;
                }

                ActivTab.Delete();
                _botTabs.Remove(ActivTab);
                if (_botTabs != null && _botTabs.Count != 0)
                {
                    ChangeActivTab(0);
                }

                ReloadTab();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// установить новую активную вкладку
        /// </summary>
        /// <param name="tabNumber">номер вкладки</param>
        private void ChangeActivTab(int tabNumber)
        {
            try
            {
                if (!_isPainting)
                {
                    return;
                }

                if (!_tabBotTab.Dispatcher.CheckAccess())
                {
                    _tabBotTab.Dispatcher.Invoke(new Action<int>(ChangeActivTab), tabNumber);
                    return;
                }
                if (ActivTab != null)
                {
                    ActivTab.StopPaint();
                }

                if (_botTabs == null ||
                    _botTabs.Count <= tabNumber)
                {
                    return;
                }

                ActivTab = _botTabs[tabNumber];

                if (ActivTab.GetType().Name == "BotTabSimple")
                {
                    ((BotTabSimple) ActivTab).StartPaint(_hostChart, _hostGlass, _hostOpenDeals, _hostCloseDeals,
                        _rectangle, _hostAlerts, _textBoxLimitPrice, _gridChartControlPanel);
                }
                else if (ActivTab.GetType().Name == "BotTabIndex")
                {
                    ((BotTabIndex)ActivTab).StartPaint(_hostChart, _rectangle);
                }

                
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
           
        }

        /// <summary>
        /// перезагрузить вкладки на контроле
        /// </summary>
        private void ReloadTab()
        {
            try
            {
                if (_tabBotTab == null)
                {
                    return;
                }
                if (!_tabBotTab.Dispatcher.CheckAccess())
                {
                    _tabBotTab.Dispatcher.Invoke(ReloadTab);
                    return;
                }
                _tabBotTab.SelectionChanged -= _tabBotTab_SelectionChanged;


                _tabBotTab.Items.Clear();

                if (_isPainting)
                {
                    if (_botTabs != null && _botTabs.Count != 0)
                    {
                        for (int i = 0; i < _botTabs.Count; i++)
                        {
                            _tabBotTab.Items.Add(" "+ (i+1));
                         
                        }
                    }

                    if (ActivTab != null && _botTabs != null && _botTabs.Count != 0)
                    {
                        int index = _botTabs.FindIndex(tab => tab.TabName == ActivTab.TabName);

                        if (index >= 0)
                        {
                            _tabBotTab.SelectedIndex = index;
                        }
                    }

                    if (_tabBotTab.SelectedIndex == -1 && _botTabs != null && _botTabs.Count != 0)
                    {
                        _tabBotTab.SelectedIndex = 0;
                    }
                }

                _tabBotTab.SelectionChanged += _tabBotTab_SelectionChanged;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

// вызыв окон управления


        /// <summary>
        /// показать окно общего для панели рискМенеджера
        /// </summary>
        public void ShowPanelRiskManagerDialog()
        {
            try
            {
                if (ActivTab == null)
                {
                    return;
                }
                _riskManager.ShowDialog();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// показать индивидуальные настройки
        /// </summary>
        public abstract void ShowIndividualSettingsDialog();

        // сообщения в лог 

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
                System.Windows.MessageBox.Show(message);
            }
        }

        /// <summary>
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        /// <summary>
        /// событие удаления робота
        /// </summary>
        public event Action DeleteEvent;

    }
}
