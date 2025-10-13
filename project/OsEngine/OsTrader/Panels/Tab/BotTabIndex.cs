/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Charts.CandleChart;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Connectors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Shapes;
using OsEngine.Charts.CandleChart.Elements;

namespace OsEngine.OsTrader.Panels.Tab
{
    /// <summary>
    /// Tab - spread (or index) of candlestick data
    /// </summary>
    public class BotTabIndex : IIBotTab
    {
        #region Service. Constructor. Override for the interface

        public BotTabIndex(string name, StartProgram startProgram)
        {
            TabName = name;
            _startProgram = startProgram;

            _valuesToFormula = new List<ValueSave>();
            _chartMaster = new ChartCandleMaster(TabName, _startProgram);

            Load();

            AutoFormulaBuilder = new IndexFormulaBuilder(this, TabName, _startProgram);
            AutoFormulaBuilder.LogMessageEvent += SendNewLogMessage;
        }

        /// <summary>
        /// source type
        /// </summary>
        public BotTabType TabType
        {
            get
            {
                return BotTabType.Index;
            }
        }

        /// <summary>
        /// program that created the source
        /// </summary>
        private StartProgram _startProgram;

        /// <summary>
        /// unique robot name
        /// </summary>
        public string TabName { get; set; }

        /// <summary>
        /// tab number
        /// </summary>
        public int TabNum { get; set; }

        /// <summary>
        /// custom name robot
        /// </summary>
        public string NameStrategy
        {
            get
            {
                if (TabName.Contains("tab"))
                {
                    return TabName.Remove(TabName.LastIndexOf("tab"), TabName.Length - TabName.LastIndexOf("tab"));
                }
                return "";
            }
        }

        /// <summary>
        /// is the emulator enabled
        /// </summary>
        public bool EmulatorIsOn { get; set; }

        /// <summary>
        /// clear memory to initial state
        /// </summary>
        public void Clear()
        {
            _valuesToFormula = new List<ValueSave>();
            _chartMaster.Clear();
        }

        /// <summary>
        /// remove source and all child structures
        /// </summary>
        public void Delete()
        {
            if (_ui != null)
            {
                _ui.Close();
            }

            _chartMaster.Delete();
            _chartMaster = null;

            if (File.Exists(@"Engine\" + TabName + @"SpreadSet.txt"))
            {
                File.Delete(@"Engine\" + TabName + @"SpreadSet.txt");
            }

            for (int i = 0; Tabs != null && i < Tabs.Count; i++)
            {
                Tabs[i].Delete();
            }

            AutoFormulaBuilder.Delete();
            AutoFormulaBuilder.LogMessageEvent -= SendNewLogMessage;
            AutoFormulaBuilder = null;

            if (TabDeletedEvent != null)
            {
                TabDeletedEvent();
            }

            if(UiSecuritiesSelection != null)
            {
                UiSecuritiesSelection.Close();
            }
        }

        /// <summary>
        /// whether the submission of events to the top is enabled or not
        /// </summary>
        public bool EventsIsOn
        {
            get
            {
                return _eventsIsOn;
            }
            set
            {
                if (_eventsIsOn == value)
                {
                    return;
                }
                _eventsIsOn = value;
                Save();
            }
        }
        private bool _eventsIsOn = true;

        /// <summary>
        /// whether the tab is connected to download data
        /// </summary>
        public bool IsConnected
        {
            get
            {
                if (Tabs == null)
                {
                    return false;
                }
                for (int i = 0; i < Tabs.Count; i++)
                {
                    if (Tabs[i].IsConnected == false)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        /// <summary>
        /// time of the last update of the candle
        /// </summary>
        public DateTime LastTimeCandleUpdate { get; set; }

        #endregion

        #region Controls

        /// <summary>
        /// show the user interface for configuring the source
        /// </summary>
        public void ShowDialog()
        {
            if (ServerMaster.GetServers() == null ||
                ServerMaster.GetServers().Count == 0)
            {
                SendNewLogMessage(OsLocalization.Market.Message1, LogMessageType.Error);
                return;
            }

            if(_ui == null)
            {
                _ui = new BotTabIndexUi(this);
                _ui.Closed += _ui_Closed;
                _ui.Show();
            }
            else
            {
                if (_ui.WindowState == System.Windows.WindowState.Minimized)
                {
                    _ui.WindowState = System.Windows.WindowState.Normal;
                }

                _ui.Activate();
            }
        }

        private void _ui_Closed(object sender, EventArgs e)
        {
            try
            {
                if (Tabs.Count == 0)
                {
                    _chartMaster.Clear();
                }

                _ui.Closed -= _ui_Closed;
                _ui = null;

                if(DialogClosed != null)
                {
                    DialogClosed();
                }
            }
            catch(Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        public event Action DialogClosed;

        private BotTabIndexUi _ui;

        /// <summary>
        /// show connector GUI
        /// </summary>
        public void ShowIndexConnectorIndexDialog(int index)
        {
            if (index >= Tabs.Count)
            {
                return;
            }

            Tabs[index].ShowDialog(false);
            Save();
        }

        /// <summary>
        /// add new security to the list
        /// </summary>
        public bool ShowNewSecurityDialog()
        {
            if(UiSecuritiesSelection == null)
            {
                Creator = GetCurrentCreator();
                UiSecuritiesSelection = new MassSourcesCreateUi(Creator);
                UiSecuritiesSelection.LogMessageEvent += SendNewLogMessage;
                UiSecuritiesSelection.Closed += _uiSecuritiesSelection_Closed;
                UiSecuritiesSelection.Show();
                return true;
            }
            else
            {
                UiSecuritiesSelection.Activate();
            }

            return false;
        }

        public void SetNewSecuritiesList(List<ActivatedSecurity> securitiesList)
        {
            // 1 удаляем старые источники

            bool isDeleteTab = false;

            ConnectorCandles[] connectors = Tabs.ToArray();

            for (int i = 0; i < connectors.Length; i++)
            {
                connectors[i].Delete();
                isDeleteTab = true;
            }

            if (isDeleteTab == true)
            {
                Save();
            }

            // 2 создаём новые источники

            for (int i = 0; i < securitiesList.Count; i++)
            {
                TryRunSecurity(securitiesList[i], Creator);
            }

            Save();
        }

        private void _uiSecuritiesSelection_Closed(object sender, EventArgs e)
        {
            try
            {
                UiSecuritiesSelection.LogMessageEvent -= SendNewLogMessage;
                UiSecuritiesSelection.Closed -= _uiSecuritiesSelection_Closed;

                if (UiSecuritiesSelection.IsAccepted == false)
                {
                    UiSecuritiesSelection = null;
                    return;
                }

                // 1 удаляем источники с другим ТФ, от того что сейчас выбрал юзер

                bool isDeleteTab = false;

                ConnectorCandles[] connectors = Tabs.ToArray();

                for (int i = 0; i < connectors.Length; i++)
                {
                    if (connectors[i].TimeFrame != Creator.TimeFrame)
                    {
                        connectors[i].Delete();
                        isDeleteTab = true;
                    }
                }

                if (isDeleteTab == true)
                {
                    Save();
                }

                // 2 создаём источники которые выбрал пользователь

                Creator = UiSecuritiesSelection.SourcesCreator;

                if (Creator.SecuritiesNames != null &&
                    Creator.SecuritiesNames.Count != 0)
                {
                    for (int i = 0; i < Creator.SecuritiesNames.Count; i++)
                    {
                        TryRunSecurity(Creator.SecuritiesNames[i], Creator);
                    }

                    Save();
                }

                UiSecuritiesSelection.SourcesCreator = null;
            }
            catch (Exception ex) 
            {
                SendNewLogMessage(ex.ToString(),LogMessageType.Error);
            }

            UiSecuritiesSelection = null;
        }

        public MassSourcesCreateUi UiSecuritiesSelection;

        public MassSourcesCreator Creator;

        /// <summary>
        /// request a class that stores a list of sources to be deployed for the index
        /// </summary>
        private MassSourcesCreator GetCurrentCreator()
        {
            MassSourcesCreator creator = new MassSourcesCreator(_startProgram);

            if (Tabs.Count == 0)
            {
                return creator;
            }

            if (Tabs.Count > 0)
            {
                if (Tabs[0] == null)
                {
                    return creator;
                }

                ConnectorCandles connector = Tabs[0];
                creator.ServerType = connector.ServerType;
                creator.ServerName = connector.ServerFullName;
                creator.TimeFrame = connector.TimeFrame;
                creator.EmulatorIsOn = connector.EmulatorIsOn;
                creator.SecuritiesClass = connector.SecurityClass;
                creator.PortfolioName = connector.PortfolioName;
                creator.SaveTradesInCandles = connector.SaveTradesInCandles;

                creator.CandleCreateMethodType = connector.CandleCreateMethodType;
                creator.CandleMarketDataType = connector.CandleMarketDataType;
                creator.CommissionType = connector.CommissionType;
                creator.CommissionValue = connector.CommissionValue;
                creator.CandleSeriesRealization.SetSaveString(connector.TimeFrameBuilder.CandleSeriesRealization.GetSaveString());
            }

            for (int i = 0; i < Tabs.Count; i++)
            {
                if (Tabs[i] == null)
                {
                    continue;
                }

                ConnectorCandles connector = Tabs[i];

                if (string.IsNullOrEmpty(connector.SecurityName) == true)
                {
                    continue;
                }

                ActivatedSecurity activatedSecurity = new ActivatedSecurity();
                activatedSecurity.SecurityName = connector.SecurityName;
                activatedSecurity.SecurityClass = connector.SecurityClass;
                activatedSecurity.IsOn = true;
                creator.SecuritiesNames.Add(activatedSecurity);
            }

            return creator;
        }

        /// <summary>
        /// chart
        /// </summary>
        private ChartCandleMaster _chartMaster;

        /// <summary>
        /// start drawing this source
        /// </summary> 
        public void StartPaint(Grid grid, WindowsFormsHost host, Rectangle rectangle)
        {
            _chartMaster.StartPaint(grid, host, rectangle);
        }

        /// <summary>
        /// stop drawing this source
        /// </summary>
        public void StopPaint()
        {
            _chartMaster.StopPaint();
        }

        /// <summary>
        /// get chart information
        /// </summary>
        public string GetChartLabel()
        {
            return _chartMaster.GetChartLabel();
        }

        /// <summary>
        /// Add custom element to the chart
        /// </summary>
        public void SetChartElement(IChartElement element)
        {
            _chartMaster.SetChartElement(element);
        }

        #endregion

        #region Storage, creation and deletion of securities in index

        /// <summary>
        /// connectors array. Data sources from which the index will be built
        /// </summary>
        public List<ConnectorCandles> Tabs = new List<ConnectorCandles>();

        /// <summary>
        /// try to connect security
        /// </summary>
        private void TryRunSecurity(ActivatedSecurity security, MassSourcesCreator creator)
        {
            for (int i = 0; i < Tabs.Count; i++)
            {
                if (Tabs[i].SecurityName == security.SecurityName &&
                    Tabs[i].ServerType == creator.ServerType &&
                    Tabs[i].ServerFullName == creator.ServerName &&
                    Tabs[i].TimeFrame == creator.TimeFrame)
                {
                    return;
                }

                if (Tabs[i].SecurityName == security.SecurityName &&
                    Tabs[i].ServerType == creator.ServerType &&
                    Tabs[i].TimeFrame != creator.TimeFrame)
                {
                    Tabs[i].Delete();
                    Tabs.RemoveAt(i);
                }
            }

            int num = Tabs.Count;

            while (true)
            {
                bool isNotInArray = true;

                for (int i = 0; i < Tabs.Count; i++)
                {
                    if (Tabs[i].UniqueName == TabName + num)
                    {
                        num++;
                        isNotInArray = false;
                        break;
                    }
                }

                if (isNotInArray == true)
                {
                    break;
                }
            }

            ConnectorCandles connector = new ConnectorCandles(TabName + num, _startProgram, false);
            connector.SaveTradesInCandles = false;

            connector.ServerType = creator.ServerType;
            connector.ServerFullName = creator.ServerName;
            connector.SecurityName = security.SecurityName;
            connector.SecurityClass = security.SecurityClass;
            connector.TimeFrame = creator.TimeFrame;
            connector.EmulatorIsOn = creator.EmulatorIsOn;
            connector.NeedToLoadServerData = false;
            connector.CandleCreateMethodType = creator.CandleCreateMethodType;
            connector.CandleMarketDataType = creator.CandleMarketDataType;
            connector.TimeFrameBuilder.CandleSeriesRealization.SetSaveString(creator.CandleSeriesRealization.GetSaveString());
            connector.TimeFrameBuilder.Save();

            connector.CommissionType = creator.CommissionType;
            connector.CommissionValue = creator.CommissionValue;

            Tabs.Add(connector);
            Tabs[Tabs.Count - 1].NewCandlesChangeEvent += BotTabIndex_NewCandlesChangeEvent;
            Tabs[Tabs.Count - 1].LogMessageEvent += SendNewLogMessage;
        }

        /// <summary>
        /// make a new adapter to connect data
        /// </summary>
        public void CreateNewSecurityConnector()
        {
            ConnectorCandles connector = new ConnectorCandles(TabName + Tabs.Count, _startProgram, false);
            connector.SaveTradesInCandles = false;
            Tabs.Add(connector);
            Tabs[Tabs.Count - 1].NewCandlesChangeEvent += BotTabIndex_NewCandlesChangeEvent;
            Tabs[Tabs.Count - 1].LogMessageEvent += SendNewLogMessage;
        }

        /// <summary>
        /// remove selected security from list
        /// </summary>
        public void DeleteSecurityTab(int index)
        {
            if (Tabs == null || Tabs.Count <= index)
            {
                return;
            }
            Tabs[index].NewCandlesChangeEvent -= BotTabIndex_NewCandlesChangeEvent;
            Tabs[index].LogMessageEvent -= SendNewLogMessage;
            Tabs[index].Delete();
            Tabs.RemoveAt(index);

            Save();
        }

        /// <summary>
        /// save the settings to the file system
        /// </summary>
        public void Save()
        {
            try
            {
                if (_isLoaded)
                {
                    return;
                }

                using (StreamWriter writer = new StreamWriter(@"Engine\" + TabName + @"SpreadSet.txt", false))
                {
                    string save = "";
                    for (int i = 0; i < Tabs.Count; i++)
                    {
                        save += Tabs[i].UniqueName + "#";
                    }
                    writer.WriteLine(save);

                    writer.WriteLine(_userFormula);
                    writer.WriteLine(EventsIsOn);
                    writer.WriteLine(CalculationDepth);
                    writer.WriteLine(PercentNormalization);
                    writer.Close();
                }
            }
            catch (Exception)
            {
                EventsIsOn = true;
                // ignore
            }
        }

        /// <summary>
        /// load settings from the file system
        /// </summary>
        public void Load()
        {
            _isLoaded = true;

            if (!File.Exists(@"Engine\" + TabName + @"SpreadSet.txt"))
            {
                _isLoaded = false;
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + TabName + @"SpreadSet.txt"))
                {
                    string[] save2 = reader.ReadLine().Split('#');
                    for (int i = 0; i < save2.Length - 1; i++)
                    {
                        ConnectorCandles newConnector = new ConnectorCandles(save2[i], _startProgram, false);
                        newConnector.SaveTradesInCandles = false;


                        if (newConnector.CandleMarketDataType != CandleMarketDataType.MarketDepth)
                        {
                            newConnector.NeedToLoadServerData = false;
                        }

                        Tabs.Add(newConnector);
                        Tabs[Tabs.Count - 1].NewCandlesChangeEvent += BotTabIndex_NewCandlesChangeEvent;
                        Tabs[Tabs.Count - 1].LogMessageEvent += SendNewLogMessage;
                    }

                    UserFormula = reader.ReadLine();

                    if (reader.EndOfStream == false)
                    {
                        _eventsIsOn = Convert.ToBoolean(reader.ReadLine());
                        CalculationDepth = Convert.ToInt32(reader.ReadLine());
                        PercentNormalization = Convert.ToBoolean(reader.ReadLine());
                    }
                    else
                    {
                        _eventsIsOn = true;
                    }

                    reader.Close();
                }
            }
            catch (Exception)
            {
                _eventsIsOn = true;
                _isLoaded = false;
                CalculationDepth = 1000;
                // ignore
            }

            if (CalculationDepth == 0)
            {
                CalculationDepth = 1000;
            }

            _isLoaded = false;
        }

        bool _isLoaded = false;

        /// <summary>
        /// one of the sources has been removed
        /// </summary>
        public event Action TabDeletedEvent;

        #endregion

        #region Formula

        /// <summary>
        /// object responsible for calculation of the index formula in automatic mode
        /// </summary>
        public IndexFormulaBuilder AutoFormulaBuilder;

        /// <summary>
        /// formula
        /// </summary>
        public string UserFormula
        {
            get { return _userFormula; }
            set
            {
                if (_userFormula == value)
                {
                    return;
                }
                _userFormula = value;
                Save();
                FullRecalculateIndex();
            }
        }
        private string _userFormula;

        public List<Security> SecuritiesInIndex = new List<Security>();

        private void TryAddTradeSecurity(Security sec)
        {
            if (sec == null)
            {
                return;
            }
            bool isInArray = false;

            for (int i = 0; i < SecuritiesInIndex.Count; i++)
            {
                if (SecuritiesInIndex[i].Name == sec.Name)
                {
                    isInArray = true;
                    break;
                }
            }

            if (isInArray == false)
            {
                SecuritiesInIndex.Add(sec);
            }
        }

        /// <summary>
        /// formula reduced to program format
        /// </summary>
        public string ConvertedFormula;

        /// <summary>
        /// check the formula for errors and lead to the appearance of the program
        /// </summary>
        public string ConvertFormula(string formula)
        {
            if (string.IsNullOrEmpty(formula))
            {
                return "";
            }

            // delete spaces

            formula = formula.Replace(" ", "");

            // check the formula for validity

            for (int i = 0; i < formula.Length; i++)
            {
                if (formula[i] != '/' && formula[i] != '*' && formula[i] != '+' && formula[i] != '-'
                    && formula[i] != '(' && formula[i] != ')' && formula[i] != 'A' && formula[i] != '1' && formula[i] != '0'
                    && formula[i] != '2' && formula[i] != '3' && formula[i] != '4' && formula[i] != '5'
                    && formula[i] != '6' && formula[i] != '7' && formula[i] != '8' && formula[i] != '9'
                    && formula[i] != '.' && formula[i] != ',')
                { // incomprehensible characters
                    SendNewLogMessage(OsLocalization.Trader.Label76, LogMessageType.Error);
                    return "";
                }
            }

            for (int i = 1; i < formula.Length; i++)
            {
                if ((formula[i] == '/' || formula[i] == '*' || formula[i] == '+' || formula[i] == '-') &&
                    (formula[i - 1] == '/' || formula[i - 1] == '*' || formula[i - 1] == '+' || formula[i - 1] == '-'))
                { // two signs in a row
                    SendNewLogMessage(OsLocalization.Trader.Label76,
                        LogMessageType.Error);
                    return "";
                }
            }

            // so good A0 / (0.033 * A1 + 0.013 * A2 + 0.021 * A3)
            // so bad (A0) / (0.033 * A1 + 0.013 * A2 + 0.021 * A3)
            // delete this design (A0)
            for (int i = 3; i < formula.Length; i++)
            {
                if (formula[i - 3] == '(' && formula[i] == ')')
                {
                    formula = formula.Remove(i, 1);
                    formula = formula.Remove(i - 3, 1);
                }
            }

            return formula;
        }

        #endregion

        #region Index calculation

        public void RebuildHard()
        {
            _normalizeCandles.Clear();
            AutoFormulaBuilder.RebuildHard();

            if (_lastRecalculateTime.AddSeconds(1) < DateTime.Now)
            {
                FullRecalculateIndex();
            }
        }

        private void FullRecalculateIndex()
        {
            _valuesToFormula = new List<ValueSave>();
            Candles = new List<Candle>();
            _chartMaster.Clear();

            if (_startProgram == StartProgram.IsOsTrader)
            {
                Thread.Sleep(1000);
            }

            if (Tabs == null || Tabs.Count == 0)
            {
                return;
            }

            ConvertedFormula = ConvertFormula(_userFormula);

            SecuritiesInIndex.Clear();

            _iteration = 0;

            string nameArray = Calculate(ConvertedFormula);

            if (_valuesToFormula != null
                && !string.IsNullOrWhiteSpace(nameArray))
            {
                ValueSave val = _valuesToFormula.Find(v => v.Name == nameArray);

                if (val != null)
                {
                    Candles = val.ValueCandles;

                    _chartMaster.SetCandles(Candles);

                    if (_startProgram == StartProgram.IsOsTrader)
                    {
                        Thread.Sleep(1000);
                    }

                    _chartMaster.SetCandles(Candles);

                    if (SpreadChangeEvent != null && EventsIsOn == true)
                    {
                        try
                        {
                            SpreadChangeEvent(Candles);
                        }
                        catch (Exception ex)
                        {
                            SendNewLogMessage(ex.ToString(),LogMessageType.Error);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// spread candles
        /// </summary>
        public List<Candle> Candles;

        /// <summary>
        /// array of objects for storing intermediate arrays of candles
        /// </summary>
        private List<ValueSave> _valuesToFormula;

        /// <summary>
        /// Index calculation depth
        /// </summary>
        public int CalculationDepth = 1500;

        /// <summary>
        /// Whether candlestick data should be normalized in percentages
        /// </summary>
        public bool PercentNormalization;

        /// <summary>
        /// new data came from the connector
        /// </summary>
        private void BotTabIndex_NewCandlesChangeEvent(List<Candle> candles)
        {
            if (candles == null || candles.Count == 0)
            {
                return;
            }

            LastTimeCandleUpdate = Tabs[0].MarketTime;

            DateTime timeCandle = candles[candles.Count - 1].TimeStart;

            for (int i = 0; i < Tabs.Count; i++)
            {
                List<Candle> myCandles = Tabs[i].Candles(true);

                if (myCandles == null || myCandles.Count < 10)
                {
                    return;
                }

                if (timeCandle != myCandles[myCandles.Count - 1].TimeStart)
                {
                    return;
                }
            }

            if (AutoFormulaBuilder.TryRebuidFormula(timeCandle, false))
            {
                _normalizeCandles.Clear();
            }

            // loop to collect all the candles in one array

            if (string.IsNullOrWhiteSpace(ConvertedFormula))
            {
                return;
            }
            _iteration = 0;
            string nameArray = Calculate(ConvertedFormula);

            if (_valuesToFormula != null && !string.IsNullOrWhiteSpace(nameArray))
            {
                ValueSave val = _valuesToFormula.Find(v => v.Name == nameArray);

                if (val != null)
                {
                    Candles = val.ValueCandles;

                    if (Candles.Count > 1 &&
                        Candles[Candles.Count - 1].TimeStart == Candles[Candles.Count - 2].TimeStart)
                    {
                        try
                        {
                            Candles.RemoveAt(Candles.Count - 1);
                        }
                        catch
                        {
                            // ignore
                        }
                    }

                    _chartMaster.SetCandles(Candles);

                    if (SpreadChangeEvent != null && EventsIsOn == true)
                    {
                        SpreadChangeEvent(Candles);
                    }
                }
            }
        }

        private int _iteration = 0;

        DateTime _lastRecalculateTime = DateTime.MinValue;

        /// <summary>
        /// recalculate values to index. Recursive function that parses the formula and calculates the index.
        /// </summary>
        /// <param name="formula">formula</param>
        /// <returns>the name of the array in which the final value lies</returns>
        public string Calculate(string formula)
        {
            try
            {
                _lastRecalculateTime = DateTime.Now;

                _iteration++;

                if (_iteration > 1000)
                {
                    return "";
                }

                if (string.IsNullOrWhiteSpace(formula))
                {
                    return "";
                }

                if (formula == "-+")
                {
                    return "";
                }



                if (formula == "+" ||
                    formula == "-" ||
                    formula == "*" ||
                    formula == "/")
                {
                    return "";
                }

                if (formula == "+-")
                {
                    return "";
                }

                string inside = "";
                string s = formula;
                int startindex = -1;
                int finishindex;

                // 1 break into brackets
                for (int i = 0; i < s.Length; i++)
                {
                    if (s[i] == '(')
                    {
                        startindex = i;
                        inside = "";
                    }
                    else if (s[i] == ')' && startindex != -1)
                    {
                        finishindex = i;

                        string partOne = "";
                        string partTwo = "";

                        for (int j = 0; j < startindex; j++)
                        {
                            partOne += s[j];
                        }
                        for (int j = finishindex + 1; j < s.Length; j++)
                        {
                            partTwo += s[j];
                        }

                        return Calculate(partOne + Calculate(inside) + partTwo);
                    }
                    else if (startindex != -1)
                    {
                        inside += s[i];
                    }
                }

                // 2 split into two values
                bool haveDevide = false;
                int znakCount = 0;
                for (int i = 0; i < s.Length; i++)
                {
                    if (s[i] == '/' || s[i] == '*')
                    {
                        haveDevide = true;
                    }
                    if (s[i] == '/' || s[i] == '*' || s[i] == '-' || s[i] == '+')
                    {
                        znakCount++;
                    }
                }

                if (znakCount > 1 && haveDevide)
                {
                    int indexStart = 0;
                    int indexEnd = s.Length;

                    bool devadeFound = false;

                    for (int i = 0; i < s.Length; i++)
                    {
                        if (devadeFound == false &&
                            (s[i] != '/' && s[i] != '*' && s[i] != '-' && s[i] != '+'))
                        {
                            continue;
                        }
                        else if (devadeFound == false &&
                                 (s[i] == '-' || s[i] == '+'))
                        {
                            indexStart = i + 1;
                        }
                        else if (devadeFound == false &&
                                 (s[i] == '*' || s[i] == '/'))
                        {
                            devadeFound = true;
                        }
                        else if (devadeFound == true &&
                                 (s[i] != '/' && s[i] != '*' && s[i] != '-' && s[i] != '+'))
                        {

                        }
                        else if (devadeFound == true &&
                                 (s[i] == '/' || s[i] == '*' || s[i] == '-' || s[i] == '+'))
                        {
                            indexEnd = i;
                            break;
                        }
                    }

                    string partOne = "";
                    string partTwo = "";
                    string value = "";

                    for (int i = 0; i < s.Length; i++)
                    {
                        if (i < indexStart)
                        {
                            partOne += s[i];
                        }
                        else if (i >= indexStart && i < indexEnd)
                        {
                            value += s[i];
                        }
                        else if (i >= indexEnd)
                        {
                            partTwo += s[i];
                        }
                    }

                    string result = partOne + Calculate(value) + partTwo;

                    return Calculate(result);
                }
                else if (znakCount > 1 && haveDevide == false)
                {
                    int indexStart = 0;
                    int indexEnd = s.Length;

                    bool devadeFound = false;

                    for (int i = 0; i < s.Length; i++)
                    {
                        if (indexStart == 0 &&
                            (s[i] == '/' || s[i] == '*' || s[i] == '-' || s[i] == '+'))
                        {
                            indexStart = i;
                            continue;
                        }
                        if (indexStart == 0)
                        {
                            continue;
                        }

                        if (devadeFound == false &&
                            (s[i] == '/' || s[i] == '*' || s[i] == '-' || s[i] == '+'))
                        {
                            devadeFound = true;
                        }
                        else if (devadeFound == true &&
                                 (s[i] == '/' || s[i] == '*' || s[i] == '-' || s[i] == '+'))
                        {
                            indexEnd = i;
                            break;
                        }
                    }

                    string partOne = "";
                    string partTwo = "";
                    string value = "";

                    for (int i = 0; i < s.Length; i++)
                    {
                        if (i <= indexStart)
                        {
                            partOne += s[i];
                        }
                        else if (i > indexStart && i < indexEnd)
                        {
                            value += s[i];
                        }
                        else if (i >= indexEnd)
                        {
                            partTwo += s[i];
                        }
                    }

                    string result = partOne + Calculate(value) + partTwo;

                    return Calculate(result);
                }

                // search for variables
                string valueOne = "";
                string valueTwo = "";
                string znak = "";

                for (int i = 0; i < s.Length; i++)
                {
                    if (s[i] == '/' || s[i] == '*' || s[i] == '-' || s[i] == '+')
                    {
                        znak += s[i];
                    }
                    else if (znak == "")
                    {
                        valueOne += s[i];
                    }
                    else if (znak != "")
                    {
                        valueTwo += s[i];
                    }
                }

                return Concate(valueOne, valueTwo, znak);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }

            return "";
        }

        /// <summary>
        /// merge some values
        /// </summary>
        /// <param name="valOne">value one</param>
        /// <param name="valTwo">value two</param>
        /// <param name="sign">sign</param>
        private string Concate(string valOne, string valTwo, string sign)
        {
            if (string.IsNullOrWhiteSpace(valOne) == false
                && string.IsNullOrWhiteSpace(valTwo) == true
                && string.IsNullOrWhiteSpace(sign) == true
                && (valOne.Length == 2 || valOne.Length == 3)
                && valOne[0] == 'A')
            {// выбран один инструмент в качестве индекса
                ValueSave exitVal = _valuesToFormula.Find(val => val.Name == valOne);

                if (exitVal == null)
                {
                    exitVal = new ValueSave();
                    exitVal.Name = valOne;
                    exitVal.ValueCandles = new List<Candle>();
                    _valuesToFormula.Add(exitVal);
                }

                if (valOne[0] == 'A')
                {
                    int iOne = Convert.ToInt32(valOne.Split('A')[1]);

                    if (iOne >= Tabs.Count)
                    {
                        return "";
                    }

                    List<Candle> candlesOne = Tabs[iOne].Candles(true);

                    if (candlesOne != null
                        && candlesOne.Count > CalculationDepth)
                    {
                        candlesOne = candlesOne.GetRange(candlesOne.Count - CalculationDepth, CalculationDepth);
                    }

                    TryAddTradeSecurity(Tabs[iOne].Security);

                    if (PercentNormalization)
                    {
                        candlesOne = Normalization(candlesOne, valOne);
                    }

                    exitVal.ValueCandles = candlesOne;
                }

                return valOne;
            }

            if (string.IsNullOrWhiteSpace(valOne))
            {
                return valTwo;
            }
            if (string.IsNullOrWhiteSpace(valTwo))
            {
                return valOne;
            }

            if (valOne[0] != 'A' && valTwo[0] != 'A' &&
                valOne[0] != 'B' && valTwo[0] != 'B')
            {
                // both digit values
                decimal one = valOne.ToDecimal();
                decimal two = valTwo.ToDecimal();

                return ConcateDecimals(one, two, sign);
            }
            else if ((valOne[0] == 'A' || valOne[0] == 'B')
                    && (valTwo[0] == 'A' || valTwo[0] == 'B'))
            {
                // both value arrays
                return ConcateCandles(valOne, valTwo, sign);
            }
            else if ((valOne[0] == 'A' || valOne[0] == 'B')
                     && valTwo[0] != 'A' && valTwo[0] != 'B')
            {
                // first value array
                return ConcateCandleAndDecimal(valOne, valTwo, sign);
            }
            else if (valOne[0] != 'A' && valOne[0] != 'B'
                     && (valTwo[0] == 'A' || valTwo[0] == 'B'))
            {
                // second value array
                return ConcateDecimalAndCandle(valOne, valTwo, sign);
            }
            return "";
        }

        /// <summary>
        /// merge two decimal arrays
        /// </summary>
        private string ConcateDecimals(decimal valOne, decimal valTwo, string sign)
        {
            if (sign == "+")
            {
                return (valOne + valTwo).ToString();
            }
            else if (sign == "-")
            {
                return (valOne - valTwo).ToString();
            }
            else if (sign == "*")
            {
                return (valOne * valTwo).ToString();
            }
            else if (sign == "/")
            {
                if (valTwo == 0)
                {
                    return "0";
                }
                return (valOne / valTwo).ToString();
            }
            return "";
        }

        /// <summary>
        /// merge two candlex arrays
        /// </summary>
        private string ConcateCandles(string valOne, string valTwo, string sign)
        {
            // take the first value
            List<Candle> candlesOne = null;

            if (valOne[0] == 'A')
            {
                int iOne = Convert.ToInt32(valOne.Split('A')[1]);
                if (iOne >= Tabs.Count)
                {
                    return "";
                }
                candlesOne = Tabs[iOne].Candles(true);

                if (candlesOne != null
                    && candlesOne.Count > CalculationDepth)
                {
                    candlesOne = candlesOne.GetRange(candlesOne.Count - CalculationDepth, CalculationDepth);
                }

                TryAddTradeSecurity(Tabs[iOne].Security);

                if (PercentNormalization)
                {
                    candlesOne = Normalization(candlesOne, valOne);
                }
            }
            if (candlesOne == null)
            {
                ValueSave value = _valuesToFormula.Find(v => v.Name == valOne);
                if (value == null)
                {
                    return "";
                }

                candlesOne = value.ValueCandles;

                if (candlesOne != null
                    && candlesOne.Count > CalculationDepth)
                {
                    candlesOne = candlesOne.GetRange(candlesOne.Count - CalculationDepth, CalculationDepth);
                }
            }



            // take the second value
            List<Candle> candlesTwo = null;

            if (valTwo[0] == 'A')
            {
                int iOne = Convert.ToInt32(valTwo.Split('A')[1]);

                if (iOne >= Tabs.Count)
                {
                    return "";
                }
                candlesTwo = Tabs[iOne].Candles(true);

                if (candlesTwo != null
                    && candlesTwo.Count > CalculationDepth)
                {
                    candlesTwo = candlesTwo.GetRange(candlesTwo.Count - CalculationDepth, CalculationDepth);
                }

                TryAddTradeSecurity(Tabs[iOne].Security);

                if (PercentNormalization)
                {
                    candlesTwo = Normalization(candlesTwo, valTwo);
                }

            }
            if (candlesTwo == null)
            {
                ValueSave value = _valuesToFormula.Find(v => v.Name == valTwo);
                if (value == null)
                {
                    return "";
                }
                candlesTwo = value.ValueCandles;

                if (candlesTwo != null
                    && candlesTwo.Count > CalculationDepth)
                {
                    candlesTwo = candlesTwo.GetRange(candlesTwo.Count - CalculationDepth, CalculationDepth);
                }
            }

            // take outgoing value
            string znak = "";

            if (sign == "+")
            {
                znak = "plus";
            }
            else if (sign == "-")
            {
                znak = "minus";
            }
            else if (sign == "*")
            {
                znak = "multiply";
            }
            else if (sign == "/")
            {
                znak = "devide";
            }

            ValueSave exitVal = _valuesToFormula.Find(val => val.Name == "B" + valOne + znak + valTwo);

            if (exitVal == null)
            {
                exitVal = new ValueSave();
                exitVal.Name = "B" + valOne + znak + valTwo;
                exitVal.ValueCandles = new List<Candle>();
                _valuesToFormula.Add(exitVal);
            }

            if (PercentNormalization == true
                && exitVal.ValueCandles != null &&
                exitVal.ValueCandles.Count > 0)
            {
                exitVal.ValueCandles.Clear();
            }

            List<Candle> exitCandles = exitVal.ValueCandles;

            if (exitCandles.Count != 0 &&
                candlesOne[candlesOne.Count - 1].TimeStart == candlesTwo[candlesTwo.Count - 1].TimeStart &&
                candlesOne[candlesOne.Count - 1].TimeStart == exitCandles[exitCandles.Count - 1].TimeStart)
            {
                // need to update only the last candle
                exitCandles[exitCandles.Count - 1] = (GetCandle(exitCandles[exitCandles.Count - 1], candlesOne[candlesOne.Count - 1], candlesTwo[candlesTwo.Count - 1], sign));
            }
            else if (exitCandles.Count != 0 &&
                candlesOne[candlesOne.Count - 1].TimeStart == candlesTwo[candlesTwo.Count - 1].TimeStart &&
                candlesOne[candlesOne.Count - 2].TimeStart == exitCandles[exitCandles.Count - 1].TimeStart)
            {
                // need to add one candle
                exitCandles.Add(GetCandle(null, candlesOne[candlesOne.Count - 1], candlesTwo[candlesTwo.Count - 1], sign));
            }
            else
            {
                // need to update everything
                int indexStartFirst = 0;
                int indexStartSecond = 0;

                exitCandles = new List<Candle>();

                for (int i = 1; i < candlesOne.Count; i++)
                {
                    for (int i2 = 0; i2 < candlesTwo.Count; i2++)
                    {
                        if (candlesTwo[i2].TimeStart >= candlesOne[i].TimeStart)
                        {
                            indexStartFirst = i;
                            indexStartSecond = i2;
                            break;
                        }
                    }

                    if (indexStartSecond != 0)
                    {
                        break;
                    }
                }

                for (int i1 = indexStartFirst, i2 = indexStartSecond; i1 < candlesOne.Count && i2 < candlesTwo.Count; i2++, i1++)
                {
                    if (candlesOne[i1] == null)
                    {
                        candlesOne.RemoveAt(i1);
                        i2--; i1--;
                        continue;
                    }
                    if (candlesTwo[i2] == null)
                    {
                        candlesTwo.RemoveAt(i2);
                        i2--; i1--;
                        continue;
                    }
                    Candle candleOne = candlesOne[i1];
                    Candle candleTwo = candlesTwo[i2];

                    try
                    {
                        if (candlesOne[i1].TimeStart == candlesTwo[i2].TimeStart)
                        {
                            exitCandles.Add(GetCandle(null, candlesOne[i1], candlesTwo[i2], sign));
                        }
                        else if (candlesOne[i1].TimeStart > candlesTwo[i2].TimeStart)
                        {
                            i1--;
                        }
                        else if (candlesOne[i1].TimeStart < candlesTwo[i2].TimeStart)
                        {
                            i2--;
                        }
                    }
                    catch
                    {
                        // ignore
                    }
                }
                exitVal.ValueCandles = exitCandles;
            }

            return exitVal.Name;
        }

        /// <summary>
        /// merge candle and decimal arrays
        /// </summary>
        private string ConcateCandleAndDecimal(string valOne, string valTwo, string sign)
        {
            List<Candle> candlesOne = null;

            if (valOne[0] == 'A')
            {
                int iOne = Convert.ToInt32(valOne.Split('A')[1]);

                if (iOne >= Tabs.Count)
                {
                    return "";
                }
                candlesOne = Tabs[iOne].Candles(true);

                if (candlesOne != null &&
                    candlesOne.Count > CalculationDepth)
                {
                    candlesOne = candlesOne.GetRange(candlesOne.Count - CalculationDepth, CalculationDepth);
                }

                TryAddTradeSecurity(Tabs[iOne].Security);
                if (candlesOne == null)
                {
                    return valOne;
                }
                if (PercentNormalization)
                {
                    candlesOne = Normalization(candlesOne, valOne);
                }
            }
            if (candlesOne == null)
            {
                ValueSave value = _valuesToFormula.Find(v => v.Name == valOne);
                if (value == null)
                {
                    return "";
                }

                candlesOne = value.ValueCandles;

                if (candlesOne != null &&
                    candlesOne.Count > CalculationDepth)
                {
                    candlesOne = candlesOne.GetRange(candlesOne.Count - CalculationDepth, CalculationDepth);
                }
            }

            decimal valueTwo = valTwo.ToDecimal();

            string znak = "";

            if (sign == "+")
            {
                znak = "plus";
            }
            else if (sign == "-")
            {
                znak = "minus";
            }
            else if (sign == "*")
            {
                znak = "multiply";
            }
            else if (sign == "/")
            {
                znak = "devide";
            }

            ValueSave exitVal = _valuesToFormula.Find(val => val.Name == "B" + valOne + znak + valTwo);

            if (exitVal == null)
            {
                exitVal = new ValueSave();
                exitVal.Name = "B" + valOne + znak + valTwo;
                exitVal.ValueCandles = new List<Candle>();
                _valuesToFormula.Add(exitVal);
            }

            if (PercentNormalization == true
                && exitVal.ValueCandles != null &&
                exitVal.ValueCandles.Count > 0)
            {
                exitVal.ValueCandles.Clear();
            }

            List<Candle> exitCandles = exitVal.ValueCandles;

            if (exitCandles.Count != 0 &&
                candlesOne[candlesOne.Count - 1].TimeStart == exitCandles[exitCandles.Count - 1].TimeStart)
            {
                // need to update only the last candle
                exitCandles[exitCandles.Count - 1] = (GetCandle(exitCandles[exitCandles.Count - 1], candlesOne[candlesOne.Count - 1], valueTwo, sign));
            }
            else if (exitCandles.Count != 0 &&
                candlesOne[candlesOne.Count - 2].TimeStart == exitCandles[exitCandles.Count - 1].TimeStart)
            {
                // need to add one candle
                exitCandles.Add(GetCandle(null, candlesOne[candlesOne.Count - 1], valueTwo, sign));
            }
            else
            {
                // need to update everything
                int indexStartFirst = 0;

                for (int i1 = candlesOne.Count - 1; exitCandles.Count != 0 && i1 > -1; i1--)
                {
                    if (candlesOne[i1].TimeStart <= exitCandles[exitCandles.Count - 1].TimeStart &&
                        indexStartFirst == 0)
                    {
                        indexStartFirst = i1 + 1;
                        break;
                    }
                }

                for (int i1 = indexStartFirst; i1 < candlesOne.Count; i1++)
                {
                    exitCandles.Add(GetCandle(null, candlesOne[i1], valueTwo, sign));
                }
                exitVal.ValueCandles = exitCandles;
            }

            return exitVal.Name;
        }

        /// <summary>
        /// merge decimal and candles arrays
        /// </summary>
        private string ConcateDecimalAndCandle(string valOne, string valTwo, string sign)
        {
            // take the first value
            decimal valueOne = valOne.ToDecimal();

            // take the second value
            List<Candle> candlesTwo = null;

            if (valTwo[0] == 'A')
            {
                int iOne = Convert.ToInt32(valTwo.Split('A')[1]);
                candlesTwo = Tabs[iOne].Candles(true);

                if (candlesTwo != null &&
                    candlesTwo.Count > CalculationDepth)
                {
                    candlesTwo = candlesTwo.GetRange(candlesTwo.Count - CalculationDepth, CalculationDepth);
                }

                TryAddTradeSecurity(Tabs[iOne].Security);

                if (PercentNormalization)
                {
                    candlesTwo = Normalization(candlesTwo, valTwo);
                }
            }
            if (candlesTwo == null)
            {
                ValueSave value = _valuesToFormula.Find(v => v.Name == valTwo);
                if (value == null)
                {
                    return "";
                }
                candlesTwo = value.ValueCandles;

                if (candlesTwo != null &&
                    candlesTwo.Count > CalculationDepth)
                {
                    candlesTwo = candlesTwo.GetRange(candlesTwo.Count - CalculationDepth, CalculationDepth);
                }
            }

            // take outgoing value

            string znak = "";

            if (sign == "+")
            {
                znak = "plus";
            }
            else if (sign == "-")
            {
                znak = "minus";
            }
            else if (sign == "*")
            {
                znak = "multiply";
            }
            else if (sign == "/")
            {
                znak = "devide";
            }

            ValueSave exitVal = _valuesToFormula.Find(val => val.Name == "B" + valOne + znak + valTwo);

            if (exitVal == null)
            {
                exitVal = new ValueSave();
                exitVal.Name = "B" + valOne + znak + valTwo;
                exitVal.ValueCandles = new List<Candle>();
                _valuesToFormula.Add(exitVal);
            }

            if (PercentNormalization == true
                && exitVal.ValueCandles != null &&
                exitVal.ValueCandles.Count > 0)
            {
                exitVal.ValueCandles.Clear();
            }

            List<Candle> exitCandles = exitVal.ValueCandles;

            if (exitCandles.Count != 0 &&
                candlesTwo[candlesTwo.Count - 1].TimeStart == exitCandles[exitCandles.Count - 1].TimeStart)
            {
                // need to update only the last candle
                exitCandles[exitCandles.Count - 1] = (GetCandle(exitCandles[exitCandles.Count - 1], valueOne, candlesTwo[candlesTwo.Count - 1], sign));
            }
            else if (exitCandles.Count != 0 &&
                candlesTwo[candlesTwo.Count - 2].TimeStart == exitCandles[exitCandles.Count - 1].TimeStart)
            {
                // need to add one candle
                exitCandles.Add(GetCandle(null, valueOne, candlesTwo[candlesTwo.Count - 1], sign));
            }
            else
            {
                // need to update everything
                int indexStartSecond = 0;

                for (int i2 = candlesTwo.Count - 1; exitCandles.Count != 0 && i2 > -1; i2--)
                {
                    if (candlesTwo[i2].TimeStart <= exitCandles[exitCandles.Count - 1].TimeStart &&
                        indexStartSecond == 0)
                    {
                        indexStartSecond = i2 + 1;
                        break;
                    }
                }

                for (int i2 = indexStartSecond; i2 < candlesTwo.Count; i2++)
                {
                    exitCandles.Add(GetCandle(null, valueOne, candlesTwo[i2], sign));
                }
                exitVal.ValueCandles = exitCandles;
            }

            return exitVal.Name;
        }

        /// <summary>
        /// merge candle with decimal by math sign
        /// </summary>
        private Candle GetCandle(Candle oldCandle, Candle candleOne, decimal valueTwo, string sign)
        {
            if (oldCandle == null)
            {
                oldCandle = new Candle();
                oldCandle.TimeStart = candleOne.TimeStart;
            }

            if (sign == "+")
            {
                decimal o = Math.Round(candleOne.Open + valueTwo, 8);
                decimal h = Math.Round(candleOne.High + valueTwo, 8);
                decimal l = Math.Round(candleOne.Low + valueTwo, 8);
                decimal c = Math.Round(candleOne.Close + valueTwo, 8);

                oldCandle.High = Math.Max(Math.Max(o, h), Math.Max(l, c));
                oldCandle.Low = Math.Min(Math.Min(o, h), Math.Min(l, c));
                oldCandle.Open = o;
                oldCandle.Close = c;
            }
            else if (sign == "-")
            {
                decimal o = Math.Round(candleOne.Open - valueTwo, 8);
                decimal h = Math.Round(candleOne.High - valueTwo, 8);
                decimal l = Math.Round(candleOne.Low - valueTwo, 8);
                decimal c = Math.Round(candleOne.Close - valueTwo, 8);

                oldCandle.High = Math.Max(Math.Max(o, h), Math.Max(l, c));
                oldCandle.Low = Math.Min(Math.Min(o, h), Math.Min(l, c));
                oldCandle.Open = o;
                oldCandle.Close = c;
            }
            else if (sign == "*")
            {
                decimal o = Math.Round(candleOne.Open * valueTwo, 8);
                decimal h = Math.Round(candleOne.High * valueTwo, 8);
                decimal l = Math.Round(candleOne.Low * valueTwo, 8);
                decimal c = Math.Round(candleOne.Close * valueTwo, 8);

                oldCandle.High = Math.Max(Math.Max(o, h), Math.Max(l, c));
                oldCandle.Low = Math.Min(Math.Min(o, h), Math.Min(l, c));
                oldCandle.Open = o;
                oldCandle.Close = c;
            }
            else if (sign == "/")
            {
                decimal o = Math.Round(candleOne.Open / valueTwo, 8);
                decimal h = Math.Round(candleOne.High / valueTwo, 8);
                decimal l = Math.Round(candleOne.Low / valueTwo, 8);
                decimal c = Math.Round(candleOne.Close / valueTwo, 8);

                oldCandle.High = Math.Max(Math.Max(o, h), Math.Max(l, c));
                oldCandle.Low = Math.Min(Math.Min(o, h), Math.Min(l, c));
                oldCandle.Open = o;
                oldCandle.Close = c;
            }

            return oldCandle;
        }

        /// <summary>
        /// merge decimal with candle by math sign
        /// </summary>
        private Candle GetCandle(Candle oldCandle, decimal valOne, Candle candleTwo, string sign)
        {
            if (oldCandle == null)
            {
                oldCandle = new Candle();
                oldCandle.TimeStart = candleTwo.TimeStart;
            }

            if (sign == "+")
            {
                decimal o = Math.Round(valOne + candleTwo.Open, 8);
                decimal h = Math.Round(valOne + candleTwo.High, 8);
                decimal l = Math.Round(valOne + candleTwo.Low, 8);
                decimal c = Math.Round(valOne + candleTwo.Close, 8);

                oldCandle.High = Math.Max(Math.Max(o, h), Math.Max(l, c));
                oldCandle.Low = Math.Min(Math.Min(o, h), Math.Min(l, c));
                oldCandle.Open = o;
                oldCandle.Close = c;
            }
            else if (sign == "-")
            {
                decimal o = Math.Round(valOne - candleTwo.Open, 8);
                decimal h = Math.Round(valOne - candleTwo.High, 8);
                decimal l = Math.Round(valOne - candleTwo.Low, 8);
                decimal c = Math.Round(valOne - candleTwo.Close, 8);

                oldCandle.High = Math.Max(Math.Max(o, h), Math.Max(l, c));
                oldCandle.Low = Math.Min(Math.Min(o, h), Math.Min(l, c));
                oldCandle.Open = o;
                oldCandle.Close = c;
            }
            else if (sign == "*")
            {
                decimal o = Math.Round(valOne * candleTwo.Open, 8);
                decimal h = Math.Round(valOne * candleTwo.High, 8);
                decimal l = Math.Round(valOne * candleTwo.Low, 8);
                decimal c = Math.Round(valOne * candleTwo.Close, 8);

                oldCandle.High = Math.Max(Math.Max(o, h), Math.Max(l, c));
                oldCandle.Low = Math.Min(Math.Min(o, h), Math.Min(l, c));
                oldCandle.Open = o;
                oldCandle.Close = c;
            }
            else if (sign == "/")
            {
                decimal o = Math.Round(valOne / candleTwo.Open, 8);
                decimal h = Math.Round(valOne / candleTwo.High, 8);
                decimal l = Math.Round(valOne / candleTwo.Low, 8);
                decimal c = Math.Round(valOne / candleTwo.Close, 8);

                oldCandle.High = Math.Max(Math.Max(o, h), Math.Max(l, c));
                oldCandle.Low = Math.Min(Math.Min(o, h), Math.Min(l, c));
                oldCandle.Open = o;
                oldCandle.Close = c;
            }

            return oldCandle;
        }

        /// <summary>
        /// merge two candles by math sign
        /// </summary>
        private Candle GetCandle(Candle oldCandle, Candle candleOne, Candle candleTwo, string sign)
        {
            if (oldCandle == null)
            {
                oldCandle = new Candle();
                oldCandle.TimeStart = candleOne.TimeStart;
            }

            if (sign == "+")
            {
                decimal o = Math.Round(candleOne.Open + candleTwo.Open, 8);
                decimal h = Math.Round(candleOne.High + candleTwo.High, 8);
                decimal l = Math.Round(candleOne.Low + candleTwo.Low, 8);
                decimal c = Math.Round(candleOne.Close + candleTwo.Close, 8);

                oldCandle.High = Math.Max(Math.Max(o, h), Math.Max(l, c));
                oldCandle.Low = Math.Min(Math.Min(o, h), Math.Min(l, c));
                oldCandle.Open = o;
                oldCandle.Close = c;
            }
            else if (sign == "-")
            {
                decimal o = Math.Round(candleOne.Open - candleTwo.Open, 8);
                decimal h = Math.Round(candleOne.High - candleTwo.High, 8);
                decimal l = Math.Round(candleOne.Low - candleTwo.Low, 8);
                decimal c = Math.Round(candleOne.Close - candleTwo.Close, 8);

                oldCandle.High = Math.Max(Math.Max(o, h), Math.Max(l, c));
                oldCandle.Low = Math.Min(Math.Min(o, h), Math.Min(l, c));
                oldCandle.Open = o;
                oldCandle.Close = c;
            }
            else if (sign == "*")
            {
                decimal o = Math.Round(candleOne.Open * candleTwo.Open, 8);
                decimal h = Math.Round(candleOne.High * candleTwo.High, 8);
                decimal l = Math.Round(candleOne.Low * candleTwo.Low, 8);
                decimal c = Math.Round(candleOne.Close * candleTwo.Close, 8);

                oldCandle.High = Math.Max(Math.Max(o, h), Math.Max(l, c));
                oldCandle.Low = Math.Min(Math.Min(o, h), Math.Min(l, c));
                oldCandle.Open = o;
                oldCandle.Close = c;
            }
            else if
                (sign == "/")
            {
                decimal o = Math.Round(candleOne.Open / candleTwo.Open, 8);
                decimal h = Math.Round(candleOne.High / candleTwo.High, 8);
                decimal l = Math.Round(candleOne.Low / candleTwo.Low, 8);
                decimal c = Math.Round(candleOne.Close / candleTwo.Close, 8);

                oldCandle.High = Math.Max(Math.Max(o, h), Math.Max(l, c));
                oldCandle.Low = Math.Min(Math.Min(o, h), Math.Min(l, c));
                oldCandle.Open = o;
                oldCandle.Close = c;
            }

            if (oldCandle.Open > oldCandle.High)
            {
                oldCandle.Open = oldCandle.High;
            }
            if (oldCandle.Open < oldCandle.Low)
            {
                oldCandle.Open = oldCandle.Low;
            }
            if (oldCandle.Close > oldCandle.High)
            {
                oldCandle.Close = oldCandle.High;
            }
            if (oldCandle.Close < oldCandle.Low)
            {
                oldCandle.Close = oldCandle.Low;
            }

            return oldCandle;
        }

        /// <summary>
        /// index change event
        /// </summary>
        public event Action<List<Candle>> SpreadChangeEvent;

        #endregion

        #region Indicators

        /// <summary>
        /// add an indicator to the index
        /// </summary>
        /// <param name="indicator"> indicator</param>
        /// <param name="nameArea">name of the area where it will be placed</param>
        public IIndicator CreateCandleIndicator(IIndicator indicator, string nameArea)
        {
            return _chartMaster.CreateIndicator(indicator, nameArea);
        }

        /// <summary>
        /// remove the indicator from the index
        /// </summary>
        /// <param name="indicator">indicator</param>
        public void DeleteCandleIndicator(IIndicator indicator)
        {
            _chartMaster.DeleteIndicator(indicator);
        }

        /// <summary>
        /// list of previously created indicators for the source
        /// </summary>
        public List<IIndicator> Indicators
        {
            get { return _chartMaster.Indicators; }
        }

        #endregion

        #region Normalization

        private List<Candle> Normalization(List<Candle> candles, string name)
        {
            if (candles == null ||
                candles.Count == 0)
            {
                return null;
            }

            ValueSave myCandles = null;

            for (int i = 0; i < _normalizeCandles.Count; i++)
            {
                if (_normalizeCandles[i].Name == name)
                {
                    myCandles = _normalizeCandles[i];
                    break;
                }
            }

            if (myCandles == null)
            {
                myCandles = new ValueSave();
                myCandles.Name = name;
                _normalizeCandles.Add(myCandles);
            }

            if (myCandles.ValueCandles == null
                || myCandles.ValueCandles.Count == 0
                || myCandles.ValueCandles.Count == CalculationDepth)
            {// 1 normalization from zero
                List<Candle> result = new List<Candle>();

                decimal curValue = 100;

                for (int i = 0; i < candles.Count; i++)
                {
                    Candle newCandle = new Candle();
                    newCandle.TimeStart = candles[i].TimeStart;
                    newCandle.State = candles[i].State;
                    newCandle.Volume = candles[i].Volume;
                    newCandle.Open = curValue;

                    decimal curMovement = candles[i].Close - candles[i].Open;
                    decimal curMovementPercent = curMovement / (candles[i].Open / 100);

                    curValue += curMovementPercent;

                    newCandle.Close = curValue;

                    if (newCandle.Close >= newCandle.Open)
                    {
                        newCandle.Low = newCandle.Open;
                        newCandle.High = newCandle.Close;
                    }
                    else
                    {
                        newCandle.Low = newCandle.Close;
                        newCandle.High = newCandle.Open;
                    }
                    result.Add(newCandle);
                }
                myCandles.ValueCandles = result;

                return result;
            }
            else
            {
                List<Candle> result = myCandles.ValueCandles;

                decimal curValue = result[result.Count - 1].Close;

                int startIndex = 0;

                for (int i = candles.Count - 1; i >= 0; i--)
                {
                    if (candles[i].TimeStart <= result[result.Count - 1].TimeStart)
                    {
                        startIndex = i + 1;
                        break;
                    }
                }

                for (int i = startIndex; i < candles.Count; i++)
                {
                    Candle newCandle = new Candle();
                    newCandle.TimeStart = candles[i].TimeStart;
                    newCandle.State = candles[i].State;
                    newCandle.Volume = candles[i].Volume;
                    newCandle.Open = curValue;

                    decimal curMovement = candles[i].Close - candles[i].Open;
                    decimal curMovementPercent = curMovement / (candles[i].Open / 100);

                    curValue += curMovementPercent;

                    newCandle.Close = curValue;

                    if (newCandle.Close >= newCandle.Open)
                    {
                        newCandle.Low = newCandle.Open;
                        newCandle.High = newCandle.Close;
                    }
                    else
                    {
                        newCandle.Low = newCandle.Close;
                        newCandle.High = newCandle.Open;
                    }
                    result.Add(newCandle);
                }

                while (result.Count > CalculationDepth)
                {
                    result.RemoveAt(0);
                }

                return result;
            }
        }

        private List<ValueSave> _normalizeCandles = new List<ValueSave>();

        #endregion

        #region Log

        /// <summary>
        /// send log message
        /// </summary>
        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            {
                System.Windows.MessageBox.Show(message);
            }
        }

        /// <summary>
        /// new log message event
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }

    public class IndexFormulaBuilder
    {
        #region Service. Constructor

        public IndexFormulaBuilder(BotTabIndex tabIndex,
            string botUniqName, StartProgram startProgram)
        {
            _index = tabIndex;
            _botUniqName = botUniqName;
            _startProgram = startProgram;

            Load();
        }

        /// <summary>
        /// custom name robot
        /// </summary>
        private string _botUniqName;

        /// <summary>
        /// program that created the source
        /// </summary>
        private StartProgram _startProgram;

        /// <summary>
        /// object of the index source for which the formula is calculated
        /// </summary>
        private BotTabIndex _index;

        /// <summary>
        /// load settings from the file system
        /// </summary>
        private void Load()
        {
            if (_startProgram == StartProgram.IsOsOptimizer)
            {
                return;
            }

            if (!File.Exists(@"Engine\" + _botUniqName + @"IndexAutoFormulaSettings.txt"))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + _botUniqName + @"IndexAutoFormulaSettings.txt"))
                {
                    Enum.TryParse(reader.ReadLine(), out _regime);
                    Enum.TryParse(reader.ReadLine(), out _dayOfWeekToRebuildIndex);
                    _hourInDayToRebuildIndex = Convert.ToInt32(reader.ReadLine());
                    _indexSecCount = Convert.ToInt32(reader.ReadLine());
                    _daysLookBackInBuilding = Convert.ToInt32(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), out _indexMultType);
                    Enum.TryParse(reader.ReadLine(), out _indexSortType);
                    _lastTimeUpdateIndex = reader.ReadLine();
                    _writeLogMessageOnRebuild = Convert.ToBoolean(reader.ReadLine());

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        /// <summary>
        /// save the settings to the file system
        /// </summary>
        private void Save()
        {
            if (_startProgram == StartProgram.IsOsOptimizer)
            {
                return;
            }

            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + _botUniqName + @"IndexAutoFormulaSettings.txt", false))
                {
                    writer.WriteLine(_regime.ToString());
                    writer.WriteLine(_dayOfWeekToRebuildIndex.ToString());
                    writer.WriteLine(_hourInDayToRebuildIndex);
                    writer.WriteLine(_indexSecCount);
                    writer.WriteLine(_daysLookBackInBuilding);
                    writer.WriteLine(_indexMultType.ToString());
                    writer.WriteLine(_indexSortType.ToString());
                    writer.WriteLine(_lastTimeUpdateIndex);
                    writer.WriteLine(_writeLogMessageOnRebuild);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        /// <summary>
        /// remove object and all child structures
        /// </summary>
        public void Delete()
        {
            if (_startProgram != StartProgram.IsOsOptimizer)
            {
                if (!File.Exists(@"Engine\" + _botUniqName + @"IndexAutoFormulaSettings.txt"))
                {
                    return;
                }
            }

            _index = null;
        }

        #endregion

        #region Settings

        /// <summary>
        /// work mode
        /// </summary>
        public IndexAutoFormulaBuilderRegime Regime
        {
            get
            {
                return _regime;
            }
            set
            {
                if (_regime == value)
                {
                    return;
                }
                _regime = value;
                Save();
            }
        }
        private IndexAutoFormulaBuilderRegime _regime;

        /// <summary>
        /// day of the week for index formula recalculation
        /// </summary>
        public DayOfWeek DayOfWeekToRebuildIndex
        {
            get
            {
                return _dayOfWeekToRebuildIndex;
            }
            set
            {
                if (_dayOfWeekToRebuildIndex == value)
                {
                    return;
                }
                _dayOfWeekToRebuildIndex = value;
                Save();
            }
        }
        private DayOfWeek _dayOfWeekToRebuildIndex = DayOfWeek.Monday;

        /// <summary>
        /// hour intraday for index formula recalculation
        /// </summary>
        public int HourInDayToRebuildIndex
        {
            get
            {
                return _hourInDayToRebuildIndex;
            }
            set
            {
                if (_hourInDayToRebuildIndex == value)
                {
                    return;
                }
                _hourInDayToRebuildIndex = value;
                Save();
            }
        }
        private int _hourInDayToRebuildIndex = 10;

        /// <summary>
        /// number of securities in the index formula
        /// </summary>
        public int IndexSecCount
        {
            get
            {
                return _indexSecCount;
            }
            set
            {
                if (_indexSecCount == value)
                {
                    return;
                }
                _indexSecCount = value;
                Save();
            }
        }
        private int _indexSecCount = 5;

        /// <summary>
        /// for how many days the trading volumes in the securities will be taken when selecting securities for the formula
        /// </summary>
        public int DaysLookBackInBuilding
        {
            get
            {
                return _daysLookBackInBuilding;
            }
            set
            {
                if (_daysLookBackInBuilding == value)
                {
                    return;
                }
                _daysLookBackInBuilding = value;
                Save();
            }
        }
        private int _daysLookBackInBuilding = 20;

        /// <summary>
        /// type of securities sorting for the index formula
        /// </summary>
        public SecuritySortType IndexSortType
        {
            get
            {
                return _indexSortType;
            }
            set
            {
                if (_indexSortType == value)
                {
                    return;
                }
                _indexSortType = value;
                Save();
            }
        }
        private SecuritySortType _indexSortType;

        /// <summary>
        /// type of weighting of securities within the index formula
        /// </summary>
        public IndexMultType IndexMultType
        {
            get
            {
                return _indexMultType;
            }
            set
            {
                if (_indexMultType == value)
                {
                    return;
                }
                _indexMultType = value;
                Save();
            }
        }
        private IndexMultType _indexMultType;

        /// <summary>
        /// make a message in the emergency log after recalculation of the index formula
        /// </summary>
        public bool WriteLogMessageOnRebuild
        {
            get
            {
                return _writeLogMessageOnRebuild;
            }
            set
            {
                if (_writeLogMessageOnRebuild == value)
                {
                    return;
                }
                _writeLogMessageOnRebuild = value;
                Save();
            }
        }
        private bool _writeLogMessageOnRebuild = true;

        /// <summary>
        /// recent time updates to the index formula
        /// </summary>
        private string _lastTimeUpdateIndex;

        #endregion

        #region Logic

        /// <summary>
        /// query to instantly recalculate the index formula at the moment. 
        /// </summary>
        public void RebuildHard()
        {
            try
            {
                List<ConnectorCandles> tabsInIndex = _index.Tabs;

                // 2 проверяем чтобы было больше одной бумаги

                if (tabsInIndex == null ||
                    tabsInIndex.Count <= 1)
                {
                    SendNewLogMessage(OsLocalization.Trader.Label392, LogMessageType.Error);
                    return;
                }

                DateTime endTime = DateTime.MinValue;

                for (int i = 0; i < tabsInIndex.Count; i++)
                {
                    List<Candle> curCandles = tabsInIndex[i].Candles(true);

                    if (curCandles == null ||
                        curCandles.Count == 0)
                    {
                        SendNewLogMessage(OsLocalization.Trader.Label393 + tabsInIndex[i].SecurityName, LogMessageType.Error);

                        if (_startProgram == StartProgram.IsTester)
                        {
                            SendNewLogMessage(OsLocalization.Trader.Label394, LogMessageType.Error);
                        }

                        return;
                    }

                    if (curCandles[curCandles.Count - 1].TimeStart > endTime)
                    {
                        endTime = curCandles[curCandles.Count - 1].TimeStart;
                    }
                }

                if (endTime == DateTime.MinValue)
                {
                    SendNewLogMessage(OsLocalization.Trader.Label395, LogMessageType.Error);
                    return;
                }

                TryRebuidFormula(endTime, true);
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// recent time updates to the index formula
        /// </summary>
        private DateTime _lastTimeUpdate = DateTime.MinValue;

        /// <summary>
        /// standard query to try to rebuild the index formula
        /// </summary>
        public bool TryRebuidFormula(DateTime timeCandle, bool isHardRebiuld)
        {
            if (_regime == IndexAutoFormulaBuilderRegime.Off)
            {
                return false;
            }

            if (isHardRebiuld == false)
            { // проверка возможности перестроить индекс исходя из времени
                if (_lastTimeUpdate == DateTime.MinValue &&
                string.IsNullOrEmpty(_lastTimeUpdateIndex) == false)
                {
                    try
                    {
                        _lastTimeUpdate = Convert.ToDateTime(_lastTimeUpdateIndex);
                    }
                    catch
                    {

                    }
                }

                // проверка времени. Чтобы уже прошло время для пересчёта индекса

                if (_regime == IndexAutoFormulaBuilderRegime.OncePerHour)
                {
                    if (_lastTimeUpdate != DateTime.MinValue
                        && _lastTimeUpdate.Hour == timeCandle.Hour)
                    {
                        return false;
                    }
                }
                else if (_regime == IndexAutoFormulaBuilderRegime.OncePerDay)
                {
                    if (_lastTimeUpdate != DateTime.MinValue
                        && _lastTimeUpdate.Date == timeCandle.Date)
                    {
                        return false;
                    }

                    if (timeCandle.Hour != _hourInDayToRebuildIndex)
                    {
                        return false;
                    }
                }
                else if (_regime == IndexAutoFormulaBuilderRegime.OncePerWeek)
                {
                    if (_lastTimeUpdate != DateTime.MinValue
                        && _lastTimeUpdate.Date == timeCandle.Date)
                    {
                        return false;
                    }

                    if (_dayOfWeekToRebuildIndex != timeCandle.DayOfWeek)
                    {
                        return false;
                    }

                    if (timeCandle.Hour != _hourInDayToRebuildIndex)
                    {
                        return false;
                    }
                }
            }


            // дальше логика

            List<ConnectorCandles> tabsInIndex = _index.Tabs;

            // 2 проверяем чтобы было больше одной бумаги

            if (tabsInIndex.Count <= 1)
            {
                return false;
            }

            // 3 берём бумаги которые должны войти в индекс

            List<SecurityInIndex> secInIndex = GetSecuritiesToIndex(tabsInIndex, _daysLookBackInBuilding);

            // 4 рассчитываем у бумаг мультипликаторы

            if (_indexMultType == IndexMultType.PriceWeighted)
            {
                SetFormulaPriceWeighted(secInIndex, _daysLookBackInBuilding);
            }
            else if (_indexMultType == IndexMultType.EqualWeighted)
            {
                SetFormulaEqualWeighted(secInIndex, _daysLookBackInBuilding);
            }
            else if (_indexMultType == IndexMultType.VolumeWeighted)
            {
                SetFormulaVolumeWeighted(secInIndex, _daysLookBackInBuilding);
            }
            else if (_indexMultType == IndexMultType.Cointegration)
            {
                SetFormulaCointegrationWeighted(secInIndex, _daysLookBackInBuilding);
            }

            _lastTimeUpdate = timeCandle;

            _lastTimeUpdateIndex = _lastTimeUpdate.ToString();

            if (_startProgram == StartProgram.IsOsTrader)
            {
                Save();
            }

            if (_writeLogMessageOnRebuild)
            {
                string message = _indexMultType.ToString() + " "
                    + OsLocalization.Trader.Label396 + _lastTimeUpdateIndex;

                message += OsLocalization.Trader.Label397 + _index.UserFormula;

                SendNewLogMessage(message, LogMessageType.Error);
            }

            return true;
        }

        private void SetFormulaPriceWeighted(List<SecurityInIndex> secInIndex, int daysLookBack)
        {
            string formula = "";

            for (int i = 0; i < secInIndex.Count; i++)
            {
                formula += secInIndex[i].Name;

                if (i + 1 < secInIndex.Count)
                {
                    formula += "+";
                }
            }

            _index.UserFormula = formula;
        }

        private void SetFormulaEqualWeighted(List<SecurityInIndex> secInIndex, int daysLookBack)
        {
            decimal maxPriceInSecs = 0;

            for (int i = 0; i < secInIndex.Count; i++)
            {
                if (maxPriceInSecs < secInIndex[i].LastPrice)
                {
                    maxPriceInSecs = secInIndex[i].LastPrice;
                }
            }

            for (int i = 0; i < secInIndex.Count; i++)
            {
                if (secInIndex[i].LastPrice == 0)
                {
                    continue;
                }
                secInIndex[i].Mult = maxPriceInSecs / secInIndex[i].LastPrice;
                secInIndex[i].Name = secInIndex[i].Name + "*" + Math.Round(secInIndex[i].Mult, 8).ToString();
            }

            string formula = "";

            for (int i = 0; i < secInIndex.Count; i++)
            {
                formula += "(" + secInIndex[i].Name + ")";

                if (i + 1 < secInIndex.Count)
                {
                    formula += "+";
                }
            }

            _index.UserFormula = formula;
        }

        private void SetFormulaVolumeWeighted(List<SecurityInIndex> secInIndex, int daysLookBack)
        {

            if (_index.PercentNormalization == false)
            {// 1. Делаем всё равномерно, если нормализация отключена
                decimal maxPriceInSecs = 0;

                for (int i = 0; i < secInIndex.Count; i++)
                {
                    if (maxPriceInSecs < secInIndex[i].LastPrice)
                    {
                        maxPriceInSecs = secInIndex[i].LastPrice;
                    }
                }

                for (int i = 0; i < secInIndex.Count; i++)
                {
                    if (secInIndex[i].LastPrice == 0)
                    {
                        continue;
                    }
                    secInIndex[i].Mult = Math.Round(maxPriceInSecs / secInIndex[i].LastPrice, 8);
                }
            }
            else if (_index.PercentNormalization == true)
            {
                for (int i = 0; i < secInIndex.Count; i++)
                {
                    secInIndex[i].Mult = 1;
                }
            }

            // 2. считаем для каждого инструмента объём

            for (int i = 0; i < secInIndex.Count; i++)
            {
                SetVolume(secInIndex[i], daysLookBack);
            }

            // 3. считаем суммарный объём

            decimal summVolume = 0;

            for (int i = 0; i < secInIndex.Count; i++)
            {
                summVolume += secInIndex[i].SummVolume;
            }

            // 4. рассчитываем долю объёмов у всех инструментов в этих объёмах

            for (int i = 0; i < secInIndex.Count; i++)
            {
                decimal partInIndex = secInIndex[i].SummVolume / (summVolume / 100);

                partInIndex = Math.Round(partInIndex, 8);

                if (partInIndex <= 0)
                {
                    partInIndex = 1;
                }

                secInIndex[i].Name = "(" + secInIndex[i].Name + "*" + secInIndex[i].Mult + ")";
                secInIndex[i].Name += "*" + partInIndex;
            }

            string formula = "";

            for (int i = 0; i < secInIndex.Count; i++)
            {
                formula += "(" + secInIndex[i].Name + ")";

                if (i + 1 < secInIndex.Count)
                {
                    formula += "+";
                }
            }

            _index.UserFormula = formula;
        }

        private void SetFormulaCointegrationWeighted(List<SecurityInIndex> secInIndex, int daysLookBack)
        {
            if (secInIndex.Count < 2)
            {
                return;
            }

            SecurityInIndex sec1 = secInIndex[0];
            SecurityInIndex sec2 = secInIndex[1];

            if (sec1.Candles == null ||
                sec1.Candles.Count == 0 ||
                sec2.Candles == null ||
                sec2.Candles.Count == 0)
            {
                return;
            }

            int candleCount = 0;

            DateTime startTimeLastDay = sec1.Candles[sec1.Candles.Count - 1].TimeStart;

            int daysCount = 0;

            for (int i = sec1.Candles.Count - 1; i > 0; i--)
            {
                if (sec1.Candles[i].TimeStart.Day != startTimeLastDay.Day)
                {
                    daysCount++;
                    startTimeLastDay = sec1.Candles[i].TimeStart;

                    if (daysCount >= daysLookBack)
                    {
                        break;
                    }
                }
                candleCount++;
            }

            CointegrationBuilder builder = new CointegrationBuilder();
            builder.CointegrationLookBack = candleCount;
            builder.ReloadCointegration(sec1.Candles, sec2.Candles, false);

            decimal mult1 = 1;
            decimal mult2 = builder.CointegrationMult;

            if (mult2 == 0)
            {
                return;
            }

            sec1.Mult = mult1;
            sec2.Mult = mult2;

            if (secInIndex.Count < 2
                  || secInIndex[0].Candles == null
                  || secInIndex[0].Candles.Count < 10
                   || secInIndex[1].Candles == null
                  || secInIndex[1].Candles.Count < 10)
            {
                return;
            }

            if (secInIndex[0].Mult == 0 ||
                secInIndex[1].Mult == 0)
            {
                return;
            }

            string formula = "";

            formula = "(" + secInIndex[0].Name + "*" + secInIndex[0].Mult + ")";
            formula += "-(" + secInIndex[1].Name + "*" + secInIndex[1].Mult + ")";

            _index.UserFormula = formula;
        }

        /// <summary>
        /// select securities for the index formula
        /// </summary>
        private List<SecurityInIndex> GetSecuritiesToIndex(List<ConnectorCandles> tabsInIndex,
            int daysLookBack)
        {
            List<SecurityInIndex> secInIndex = new List<SecurityInIndex>();

            if (_indexSortType == SecuritySortType.FirstInArray)
            {
                for (int i = 0; i < tabsInIndex.Count && i < _indexSecCount; i++)
                {
                    List<Candle> candles = tabsInIndex[i].Candles(false);

                    if (_index.PercentNormalization == true)
                    {
                        if (candles.Count > _index.CalculationDepth)
                        {
                            candles = candles.GetRange(candles.Count - 1 - _index.CalculationDepth, _index.CalculationDepth);
                        }

                        candles = Normalization(candles);
                    }

                    SecurityInIndex newIndex = new SecurityInIndex();

                    newIndex.Name = "A" + i;
                    newIndex.Candles = candles;
                    newIndex.Security = tabsInIndex[i].Security;

                    secInIndex.Add(newIndex);
                }
            }
            else if (_indexSortType == SecuritySortType.VolumeWeighted)
            {
                for (int i = 0; i < tabsInIndex.Count; i++)
                {
                    List<Candle> candles = tabsInIndex[i].Candles(false);

                    if (_index.PercentNormalization == true)
                    {

                        if (candles.Count > _index.CalculationDepth)
                        {
                            candles = candles.GetRange(candles.Count - 1 - _index.CalculationDepth, _index.CalculationDepth);
                        }

                        candles = Normalization(candles);
                    }

                    SecurityInIndex newIndex = new SecurityInIndex();

                    newIndex.Name = "A" + i;
                    newIndex.SecName = tabsInIndex[i].Security.Name;
                    newIndex.Candles = candles;
                    newIndex.Security = tabsInIndex[i].Security;
                    SetVolume(newIndex, daysLookBack);

                    secInIndex.Add(newIndex);
                }

                for (int i2 = 0; i2 < secInIndex.Count; i2++)
                {
                    for (int i = 1; i < secInIndex.Count; i++)
                    {
                        if (secInIndex[i].SummVolume > secInIndex[i - 1].SummVolume)
                        {
                            SecurityInIndex sec = secInIndex[i - 1];
                            secInIndex[i - 1] = secInIndex[i];
                            secInIndex[i] = sec;
                        }
                    }
                }

                if (secInIndex.Count >= 100)
                {
                    string res = "";

                    for (int i = 1; i < secInIndex.Count; i++)
                    {
                        res += secInIndex[i].SecName + "\n";
                    }

                    //_tab.TabsSimple[0].SetNewLogMessage(res, LogMessageType.Error);
                }

                while (secInIndex.Count > _indexSecCount)
                {
                    secInIndex.RemoveAt(secInIndex.Count - 1);
                }
            }
            else if (_indexSortType == SecuritySortType.MaxVolatilityWeighted
                || _indexSortType == SecuritySortType.MinVolatilityWeighted)
            {
                for (int i = 0; i < tabsInIndex.Count; i++)
                {
                    List<Candle> candles = tabsInIndex[i].Candles(false);

                    if (_index.PercentNormalization == true)
                    {
                        if (candles.Count > _index.CalculationDepth)
                        {
                            candles = candles.GetRange(candles.Count - 1 - _index.CalculationDepth, _index.CalculationDepth);
                        }
                        candles = Normalization(candles);
                    }

                    SecurityInIndex newIndex = new SecurityInIndex();

                    newIndex.Name = "A" + i;
                    newIndex.SecName = tabsInIndex[i].Security.Name;
                    newIndex.Candles = candles;
                    newIndex.Security = tabsInIndex[i].Security;
                    SetVolatility(newIndex, daysLookBack);

                    secInIndex.Add(newIndex);
                }

                for (int i2 = 0; i2 < secInIndex.Count; i2++)
                {
                    for (int i = 1; i < secInIndex.Count; i++)
                    {
                        if (_indexSortType == SecuritySortType.MaxVolatilityWeighted)
                        {
                            if (secInIndex[i].VolatylityDayPercent > secInIndex[i - 1].VolatylityDayPercent)
                            {
                                SecurityInIndex sec = secInIndex[i - 1];
                                secInIndex[i - 1] = secInIndex[i];
                                secInIndex[i] = sec;
                            }
                        }
                        else if (_indexSortType == SecuritySortType.MinVolatilityWeighted)
                        {
                            if (secInIndex[i].VolatylityDayPercent < secInIndex[i - 1].VolatylityDayPercent)
                            {
                                SecurityInIndex sec = secInIndex[i - 1];
                                secInIndex[i - 1] = secInIndex[i];
                                secInIndex[i] = sec;
                            }
                        }
                    }
                }

                while (secInIndex.Count > _indexSecCount)
                {
                    secInIndex.RemoveAt(secInIndex.Count - 1);
                }
            }

            return secInIndex;
        }

        /// <summary>
        /// calculate security volume for a certain number of days
        /// </summary>
        private void SetVolume(SecurityInIndex security, int daysLookBack)
        {
            // 1 берём свечки за последнюю неделю

            List<Candle> allCandles = security.Candles;

            if (allCandles == null || allCandles.Count == 0)
            {
                return;
            }

            List<Candle> candlesToVol = new List<Candle>();

            DateTime startTime = allCandles[allCandles.Count - 1].TimeStart.AddDays(-daysLookBack);

            for (int i = allCandles.Count - 1; i > 0; i--)
            {
                candlesToVol.Add(allCandles[i]);

                if (allCandles[i].TimeStart < startTime)
                {
                    break;
                }
            }

            // 2 считаем в них объём и складываем в переменную

            decimal allVolume = 0;

            for (int i = 0; i < candlesToVol.Count; i++)
            {
                allVolume += candlesToVol[i].Center * candlesToVol[i].Volume;
            }

            if (security.Security.Lot > 1)
            {
                allVolume = allVolume * security.Security.Lot;
            }

            security.SummVolume = allVolume;
        }

        /// <summary>
        /// calculate security volatility for a certain number of days
        /// </summary>
        private void SetVolatility(SecurityInIndex security, int len)
        {
            List<Candle> candles = security.Candles;

            List<decimal> curDaysVola = new List<decimal>();

            decimal curMinInDay = decimal.MaxValue;
            decimal curMaxInDay = 0;
            int curDay = candles[candles.Count - 1].TimeStart.Day;
            int daysCount = 1;


            for (int i = candles.Count - 1; i > 0; i--)
            {
                Candle curCandle = candles[i];

                if (curDay != curCandle.TimeStart.Day)
                {
                    if (curMaxInDay != 0 &&
                        curMinInDay != decimal.MaxValue)
                    {
                        decimal moveInDay = curMaxInDay - curMinInDay;
                        decimal percentMove = moveInDay / (curMinInDay / 100);
                        curDaysVola.Add(percentMove);
                    }

                    curMinInDay = decimal.MaxValue;
                    curMaxInDay = 0;
                    curDay = candles[i].TimeStart.Day;

                    daysCount++;

                    if (len == daysCount)
                    {
                        break;
                    }
                }

                if (curCandle.High > curMaxInDay)
                {
                    curMaxInDay = curCandle.High;
                }
                if (curCandle.Low < curMinInDay)
                {
                    curMinInDay = curCandle.Low;
                }
            }

            if (curDaysVola.Count == 0)
            {
                return;
            }

            decimal result = 0;

            for (int i = 0; i < curDaysVola.Count; i++)
            {
                result += curDaysVola[i];
            }

            security.VolatylityDayPercent = result / curDaysVola.Count;
        }

        private List<Candle> Normalization(List<Candle> candles)
        {
            List<Candle> result = new List<Candle>();

            decimal curValue = 100;

            for (int i = 0; i < candles.Count; i++)
            {
                Candle newCandle = new Candle();
                newCandle.TimeStart = candles[i].TimeStart;
                newCandle.State = candles[i].State;
                newCandle.Volume = candles[i].Volume;
                newCandle.Open = curValue;

                decimal curMovement = candles[i].Close - candles[i].Open;
                decimal curMovementPercent = curMovement / (candles[i].Open / 100);

                curValue += curMovementPercent;

                newCandle.Close = curValue;

                if (newCandle.Close >= newCandle.Open)
                {
                    newCandle.Low = newCandle.Open;
                    newCandle.High = newCandle.Close;
                }
                else
                {
                    newCandle.Low = newCandle.Close;
                    newCandle.High = newCandle.Open;
                }
                result.Add(newCandle);
            }

            return result;
        }

        #endregion

        #region Log

        /// <summary>
        /// send log message
        /// </summary>
        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            {
                System.Windows.MessageBox.Show(message);
            }
        }

        /// <summary>
        /// new log message event
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }

    /// <summary>
    /// auto-formula builder work mode
    /// </summary>
    public enum IndexAutoFormulaBuilderRegime
    {
        Off,
        OncePerDay,
        OncePerWeek,
        OncePerHour,
    }

    /// <summary>
    /// type of weighting of securities within the index formula
    /// </summary>
    public enum IndexMultType
    {
        PriceWeighted,

        VolumeWeighted,

        EqualWeighted,

        Cointegration
    }

    /// <summary>
    /// type of securities sorting for the index formula
    /// </summary>
    public enum SecuritySortType
    {
        FirstInArray,
        VolumeWeighted,
        MaxVolatilityWeighted,
        MinVolatilityWeighted,
    }

    /// <summary>
    /// object for storing market data on security, during the calculation of the formula
    /// </summary>
    public class SecurityInIndex
    {
        public string SecName;

        public string Name;

        public decimal Mult;

        public decimal SummVolume;

        public decimal VolatylityDayPercent;

        public List<Candle> Candles;

        public Security Security;

        public decimal LastPrice
        {
            get
            {
                if (Candles == null || Candles.Count == 0)
                {
                    return 0;
                }
                return Candles[Candles.Count - 1].Close;
            }
        }
    }

    /// <summary>
    /// object to store intermediate data by index
    /// </summary>
    public class ValueSave
    {
        public string Name;

        public List<Candle> ValueCandles;
    }

}
