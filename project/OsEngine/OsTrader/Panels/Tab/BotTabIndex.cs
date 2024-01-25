﻿/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Alerts;
using OsEngine.Charts.CandleChart;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Connectors;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Shapes;

namespace OsEngine.OsTrader.Panels.Tab
{
    /// <summary>
    /// Tab - spread of candlestick data in the form of a candlestick chart
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
        /// Program that created the robot
        /// </summary>
        private StartProgram _startProgram;

        /// <summary>
        /// Unique robot name
        /// </summary>
        public string TabName { get; set; }

        /// <summary>
        /// Tab number
        /// </summary>
        public int TabNum { get; set; }

        /// <summary>
        /// custom name robot
        /// пользовательское имя робота
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
        /// Is the emulator enabled
        /// </summary>
        public bool EmulatorIsOn { get; set; }

        /// <summary>
        /// Clear
        /// </summary>
        public void Clear()
        {
            _valuesToFormula = new List<ValueSave>();
            _chartMaster.Clear();
        }

        /// <summary>
        /// Remove tab and all child structures
        /// </summary>
        public void Delete()
        {
            _chartMaster.Delete();

            if (File.Exists(@"Engine\" + TabName + @"SpreadSet.txt"))
            {
                File.Delete(@"Engine\" + TabName + @"SpreadSet.txt");
            }

            for (int i = 0; Tabs != null && i < Tabs.Count; i++)
            {
                Tabs[i].Delete();
            }

            if (TabDeletedEvent != null)
            {
                TabDeletedEvent();
            }
        }

        /// <summary>
        /// Whether the submission of events to the top is enabled or not
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
        /// Whether the tab is connected to download data
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
        /// Time of the last update of the candle
        /// </summary>
        public DateTime LastTimeCandleUpdate { get; set; }

        #endregion

        #region Controls

        /// <summary>
        /// Show GUI
        /// </summary>
        public void ShowDialog()
        {
            if (ServerMaster.GetServers() == null ||
                ServerMaster.GetServers().Count == 0)
            {
                SendNewLogMessage(OsLocalization.Market.Message1, LogMessageType.Error);
                return;
            }

            BotTabIndexUi ui = new BotTabIndexUi(this);
            ui.ShowDialog();

            if (Tabs.Count != 0)
            {
                _chartMaster.SetNewSecurity("Index on: " + _userFormula, Tabs[0].TimeFrameBuilder, null, Tabs[0].ServerType);
            }
            else
            {
                _chartMaster.Clear();
            }
        }

        /// <summary>
        /// Show connector GUI
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
        /// Add new security to the list
        /// </summary>
        public void ShowNewSecurityDialog()
        {
            MassSourcesCreator creator = GetCurrentCreator();

            MassSourcesCreateUi ui = new MassSourcesCreateUi(creator);
            ui.ShowDialog();

            creator = ui.SourcesCreator;

            if (creator.SecuritiesNames != null &&
                creator.SecuritiesNames.Count != 0)
            {
                for (int i = 0; i < creator.SecuritiesNames.Count; i++)
                {
                    TryRunSecurity(creator.SecuritiesNames[i], creator);
                }

                Save();
            }
            ui.SourcesCreator = null;
        }

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
                creator.TimeFrame = connector.TimeFrame;
                creator.EmulatorIsOn = connector.EmulatorIsOn;
                creator.CandleCreateMethodType = connector.CandleCreateMethodType;
                creator.CandleMarketDataType = connector.CandleMarketDataType;
                creator.SetForeign = connector.SetForeign;
                creator.CountTradeInCandle = connector.CountTradeInCandle;
                creator.VolumeToCloseCandleInVolumeType = connector.VolumeToCloseCandleInVolumeType;
                creator.RencoPunktsToCloseCandleInRencoType = connector.RencoPunktsToCloseCandleInRencoType;
                creator.RencoIsBuildShadows = connector.RencoIsBuildShadows;
                creator.DeltaPeriods = connector.DeltaPeriods;
                creator.RangeCandlesPunkts = connector.RangeCandlesPunkts;
                creator.ReversCandlesPunktsMinMove = connector.ReversCandlesPunktsMinMove;
                creator.ReversCandlesPunktsBackMove = connector.ReversCandlesPunktsBackMove;
                creator.ComissionType = connector.ComissionType;
                creator.ComissionValue = connector.ComissionValue;
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
        /// Chart
        /// </summary>
        private ChartCandleMaster _chartMaster;

        /// <summary>
        /// Start drawing this robot
        /// </summary> 
        public void StartPaint(Grid grid, WindowsFormsHost host, Rectangle rectangle)
        {
            _chartMaster.StartPaint(grid, host, rectangle);
        }

        /// <summary>
        /// Stop drawing this robot
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

        #endregion

        #region Storage, creation and deletion of securities in index

        /// <summary>
        /// Connectors array
        /// </summary>
        public List<ConnectorCandles> Tabs = new List<ConnectorCandles>();

        /// <summary>
        /// try to connect security
        /// </summary>
        /// <param name="security">security</param>
        /// <param name="creator">mass source creator</param>
        private void TryRunSecurity(ActivatedSecurity security, MassSourcesCreator creator)
        {
            for (int i = 0; i < Tabs.Count; i++)
            {
                if (Tabs[i].SecurityName == security.SecurityName &&
                    Tabs[i].ServerType == creator.ServerType)
                {
                    return;
                }
            }

            int num = Tabs.Count;

            while (true)
            {
                bool isNotInArray = true;

                for (int i = 0; i < Tabs.Count; i++)
                {
                    if (Tabs[i].UniqName == TabName + num)
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
            connector.SecurityName = security.SecurityName;
            connector.SecurityClass = security.SecurityClass;
            connector.TimeFrame = creator.TimeFrame;
            connector.EmulatorIsOn = creator.EmulatorIsOn;
            connector.NeadToLoadServerData = false;
            connector.CandleCreateMethodType = creator.CandleCreateMethodType;
            connector.CandleMarketDataType = creator.CandleMarketDataType;
            connector.SetForeign = creator.SetForeign;
            connector.CountTradeInCandle = creator.CountTradeInCandle;
            connector.VolumeToCloseCandleInVolumeType = creator.VolumeToCloseCandleInVolumeType;
            connector.RencoPunktsToCloseCandleInRencoType = creator.RencoPunktsToCloseCandleInRencoType;
            connector.RencoIsBuildShadows = creator.RencoIsBuildShadows;
            connector.DeltaPeriods = creator.DeltaPeriods;
            connector.RangeCandlesPunkts = creator.RangeCandlesPunkts;
            connector.ReversCandlesPunktsMinMove = creator.ReversCandlesPunktsMinMove;
            connector.ReversCandlesPunktsBackMove = creator.ReversCandlesPunktsBackMove;
            connector.ComissionType = creator.ComissionType;
            connector.ComissionValue = creator.ComissionValue;

            Tabs.Add(connector);
            Tabs[Tabs.Count - 1].NewCandlesChangeEvent += BotTabIndex_NewCandlesChangeEvent;
        }

        /// <summary>
        /// Make a new adapter to connect data
        /// </summary>
        public void CreateNewSecurityConnector()
        {
            ConnectorCandles connector = new ConnectorCandles(TabName + Tabs.Count, _startProgram, false);
            connector.SaveTradesInCandles = false;
            Tabs.Add(connector);
            Tabs[Tabs.Count - 1].NewCandlesChangeEvent += BotTabIndex_NewCandlesChangeEvent;
        }

        /// <summary>
        /// Remove selected security from list
        /// </summary>
        public void DeleteSecurityTab(int index)
        {
            if (Tabs == null || Tabs.Count <= index)
            {
                return;
            }
            Tabs[index].NewCandlesChangeEvent -= BotTabIndex_NewCandlesChangeEvent;
            Tabs[index].Delete();
            Tabs.RemoveAt(index);

            Save();
        }

        /// <summary>
        /// Save
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
                        save += Tabs[i].UniqName + "#";
                    }
                    writer.WriteLine(save);

                    writer.WriteLine(_userFormula);
                    writer.WriteLine(EventsIsOn);
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
        /// Load settings from file
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
                            newConnector.NeadToLoadServerData = false;
                        }

                        Tabs.Add(newConnector);
                        Tabs[Tabs.Count - 1].NewCandlesChangeEvent += BotTabIndex_NewCandlesChangeEvent;
                    }

                    UserFormula = reader.ReadLine();

                    if (reader.EndOfStream == false)
                    {
                        _eventsIsOn = Convert.ToBoolean(reader.ReadLine());
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
                // ignore
            }
            _isLoaded = false;
        }
        bool _isLoaded = false;

        /// <summary>
        /// Source removed
        /// </summary>
        public event Action TabDeletedEvent;

        #endregion

        #region Formula calculation

        /// <summary>
        /// Formula
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

                _valuesToFormula = new List<ValueSave>();
                Candles = new List<Candle>();
                _chartMaster.Clear();

                if (Tabs == null || Tabs.Count == 0)
                {
                    return;
                }

                ConvertedFormula = ConvertFormula(_userFormula);

                string nameArray = Calculate(ConvertedFormula);

                if (_valuesToFormula != null && !string.IsNullOrWhiteSpace(nameArray))
                {
                    ValueSave val = _valuesToFormula.Find(v => v.Name == nameArray);

                    if (val != null)
                    {
                        Candles = val.ValueCandles;

                        _chartMaster.SetCandles(Candles);

                        if (SpreadChangeEvent != null && EventsIsOn == true)
                        {
                            SpreadChangeEvent(Candles);
                        }
                    }
                }
            }
        }
        private string _userFormula;


        #endregion

        #region Index calculation

        /// <summary>
        /// spread candles
        /// </summary>
        public List<Candle> Candles;

        /// <summary>
        /// Formula reduced to program format
        /// </summary>
        public string ConvertedFormula;

        /// <summary>
        /// Array of objects for storing intermediate arrays of candles
        /// </summary>
        private List<ValueSave> _valuesToFormula;

        /// <summary>
        /// Check the formula for errors and lead to the appearance of the program
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
                    && formula[i] != '.')
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

        /// <summary>
        /// Recalculate an arra
        /// </summary>
        /// <param name="formula">formula</param>
        /// <returns>the name of the array in which the final value lies</returns>
        public string Calculate(string formula)
        {
            try
            {
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
        /// Calculate values
        /// </summary>
        /// <param name="valOne">value one</param>
        /// <param name="valTwo">value two</param>
        /// <param name="sign">sign</param>
        private string Concate(string valOne, string valTwo, string sign)
        {
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
                decimal one = Convert.ToDecimal(valOne, new CultureInfo("en-US"));
                decimal two = Convert.ToDecimal(valTwo, new CultureInfo("en-US"));

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
        /// Add numbers
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
        /// Count arrays of candles
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
            }
            if (candlesOne == null)
            {
                ValueSave value = _valuesToFormula.Find(v => v.Name == valOne);
                if (value == null)
                {
                    return "";
                }

                candlesOne = value.ValueCandles;
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
            }
            if (candlesTwo == null)
            {
                ValueSave value = _valuesToFormula.Find(v => v.Name == valTwo);
                if (value == null)
                {
                    return "";
                }
                candlesTwo = value.ValueCandles;
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
                    catch (Exception e)
                    {

                    }
                }
                exitVal.ValueCandles = exitCandles;
            }

            return exitVal.Name;
        }

        /// <summary>
        /// count an array of candles and a number
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
                if (candlesOne == null)
                {
                    return valOne;
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
            }

            decimal valueTwo = Convert.ToDecimal(valTwo, new CultureInfo("en-US"));

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

            List<Candle> exitCandles = exitVal.ValueCandles;

            int lastOper = -1;

            if (exitCandles.Count != 0 &&
                candlesOne[candlesOne.Count - 1].TimeStart == exitCandles[exitCandles.Count - 1].TimeStart)
            {
                // need to update only the last candle
                lastOper = 1;
                exitCandles[exitCandles.Count - 1] = (GetCandle(exitCandles[exitCandles.Count - 1], candlesOne[candlesOne.Count - 1], valueTwo, sign));
            }
            else if (exitCandles.Count != 0 &&
                candlesOne[candlesOne.Count - 2].TimeStart == exitCandles[exitCandles.Count - 1].TimeStart)
            {
                lastOper = 2;

                // need to add one candle
                exitCandles.Add(GetCandle(null, candlesOne[candlesOne.Count - 1], valueTwo, sign));
            }
            else
            {
                lastOper = 3;

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
        /// count number and array
        /// </summary>
        private string ConcateDecimalAndCandle(string valOne, string valTwo, string sign)
        {
            // take the first value
            decimal valueOne = Convert.ToDecimal(valOne, new CultureInfo("en-US"));

            // take the second value
            List<Candle> candlesTwo = null;

            if (valTwo[0] == 'A')
            {
                int iOne = Convert.ToInt32(valTwo.Split('A')[1]);
                candlesTwo = Tabs[iOne].Candles(true);
            }
            if (candlesTwo == null)
            {
                ValueSave value = _valuesToFormula.Find(v => v.Name == valTwo);
                if (value == null)
                {
                    return "";
                }
                candlesTwo = value.ValueCandles;
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
        /// Create index candle
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
        /// New data came from the connector
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

            DateTime time = Tabs[0].Candles(true)[Tabs[0].Candles(true).Count - 1].TimeStart;

            for (int i = 0; i < Tabs.Count; i++)
            {
                List<Candle> myCandles = Tabs[i].Candles(true);
                if (myCandles[myCandles.Count - 1].TimeStart != time)
                {
                    return;
                }
            }
            // loop to collect all the candles in one array

            if (string.IsNullOrWhiteSpace(ConvertedFormula))
            {
                return;
            }

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
                    if (_startProgram == StartProgram.IsOsTrader && Tabs.Count > 0)
                    {
                        var candlesToKeep = ((OsEngine.Market.Servers.AServer)Tabs[0].MyServer)._neadToSaveCandlesCountParam.Value;
                        var needToRemove = ((OsEngine.Market.Servers.AServer)Tabs[0].MyServer)._needToRemoveCandlesFromMemory.Value;

                        if (needToRemove
                            && Candles[Candles.Count - 1].TimeStart.Minute % 15 == 0
                            && Candles[Candles.Count - 1].TimeStart.Second == 0
                            && Candles.Count > candlesToKeep)
                        {
                            Candles.RemoveRange(0, Candles.Count - 1 - candlesToKeep);
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

        /// <summary>
        /// Spread change event
        /// </summary>
        public event Action<List<Candle>> SpreadChangeEvent;

        #endregion

        #region Indicators

        /// <summary>
        /// Create indicator
        /// </summary>
        /// <param name="indicator"> indicator</param>
        /// <param name="nameArea">name of the area where it will be placed</param>
        public IIndicator CreateCandleIndicator(IIndicator indicator, string nameArea)
        {
            return _chartMaster.CreateIndicator(indicator, nameArea);
        }

        /// <summary>
        /// Remove indicator from candlestick chart
        /// </summary>
        /// <param name="indicator">indicator</param>
        public void DeleteCandleIndicator(IIndicator indicator)
        {
            _chartMaster.DeleteIndicator(indicator);
        }

        /// <summary>
        /// indicators available on the index
        /// </summary>
        public List<IIndicator> Indicators
        {
            get { return _chartMaster.Indicators; }
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
        /// New log message event
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }

    /// <summary>
    /// object to store intermediate data by index
    /// </summary>
    public class ValueSave
    {
        /// <summary>
        /// name
        /// </summary>
        public string Name;

        /// <summary>
        /// candles
        /// </summary>
        public List<Candle> ValueCandles;
    }
}
