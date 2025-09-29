/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.OsTrader.ClientManagement.Gui;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Robots;
using System;
using System.Collections.Generic;
using System.Runtime.Intrinsics.Arm;

namespace OsEngine.OsTrader.ClientManagement
{
    public class TradeClientRobot
    {
        public TradeClientRobot(string clientName)
        {
            ClientUid = clientName;
        }

        public int Number;

        public bool IsOn;

        public string BotClassName
        {
            get {  return _botClassName; }
            set
            {
                if(_botClassName == value)
                {
                    return;
                }

                CheckBotPosition(value);

                if (CheckBotSources(value) == false)
                {
                    _botClassName = "None";

                    if (Parameters != null)
                    {
                        Parameters.Clear();
                    }

                    if(SourceSettings != null)
                    {
                        SourceSettings.Clear();
                    }

                    return;
                }

                _botClassName = value;
                LoadParametersByBotClass();
                LoadSourcesByBotClass();
            }
        }
        private string _botClassName = "None";

        public bool IsScript = false;

        public string DeployStatus
        {
            get
            {
                return "Unknown";
            }
        }

        public bool RobotsIsOn
        {
            get
            {
                return false;
            }
        }

        public bool EmulatorIsOn
        {
            get
            {
                return false;
            }
        }

        public string ClientUid;

        public string GetSaveString()
        {
            string saveStr = "";

            saveStr += Number + "&";
            saveStr += IsOn + "&";
            saveStr += BotClassName + "&";
            saveStr += ClientUid + "&";
            saveStr += "&";
            saveStr += "&";
            saveStr += "&";

            for (int i = 0; i < Parameters.Count; i++)
            {
                saveStr += Parameters[i].Type.ToString() + "^" + Parameters[i].GetStringToSave() + "^";
            }

            saveStr += "&";

            for (int i = 0; i < SourceSettings.Count; i++)
            {
                saveStr += SourceSettings[i].GetSaveString() + "^";
            }

            saveStr += "&";



            return saveStr;
        }

        public void LoadFromString(string saveString)
        {
            string[] saveValues = saveString.Split("&");

            Number = Convert.ToInt32(saveValues[0]);
            IsOn = Convert.ToBoolean(saveValues[1]);
            _botClassName = saveValues[2];
            ClientUid = saveValues[3];

            string[] parameters = saveValues[7].Split("^");

            for(int i = 0;i < parameters.Length;i+=2)
            {
                if(i + 1 ==  parameters.Length)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(parameters[i]) == true
                    || string.IsNullOrEmpty(parameters[i+1]) == true)
                {
                    continue;
                }

                StrategyParameterType type;

                if(Enum.TryParse(parameters[i], out type) == false)
                {
                    continue;
                }

                string[] saveArray = parameters[i + 1].Split('#');

                string name = saveArray[0];

                IIStrategyParameter param = null;
                if (type == StrategyParameterType.Bool)
                {
                    param = new StrategyParameterBool(name, false);
                    param.LoadParamFromString(saveArray);
                }
                else if (type == StrategyParameterType.Decimal)
                {
                    param = new StrategyParameterDecimal(name, 0, 0, 0, 0);
                    param.LoadParamFromString(saveArray);
                }
                else if (type == StrategyParameterType.Int)
                {
                    param = new StrategyParameterInt(name, 0, 0, 0, 0);
                    param.LoadParamFromString(saveArray);
                }
                else if (type == StrategyParameterType.String)
                {
                    param = new StrategyParameterString(name, "", null);
                    param.LoadParamFromString(saveArray);
                }
                else if (type == StrategyParameterType.TimeOfDay)
                {
                    param = new StrategyParameterTimeOfDay(name, 0, 0, 0, 0);
                    param.LoadParamFromString(saveArray);
                }
                else if (type == StrategyParameterType.Button)
                {
                    param = new StrategyParameterButton(name);
                    param.LoadParamFromString(saveArray);
                }
                else if (type == StrategyParameterType.Label)
                {
                    param = new StrategyParameterLabel(name, "", "", 0, 0, System.Drawing.Color.White);
                    param.LoadParamFromString(saveArray);
                }
                else if (type == StrategyParameterType.CheckBox)
                {
                    param = new StrategyParameterCheckBox(name, false);
                    param.LoadParamFromString(saveArray);
                }
                else if (type == StrategyParameterType.DecimalCheckBox)
                {
                    param = new StrategyParameterDecimalCheckBox(name, 0, 0, 0, 0, false);
                    param.LoadParamFromString(saveArray);
                }

                Parameters.Add(param);
            }

            string[] source = saveValues[8].Split("^");

            for (int i = 0; i < source.Length; i++)
            {
                if (string.IsNullOrEmpty(source[i]) == true)
                {
                    continue;
                }

                TradeClientSourceSettings newParameter = new TradeClientSourceSettings();
                newParameter.SetFromString(source[i]);
                SourceSettings.Add(newParameter);
            }

        }

        public string UniqueNameFull
        {
            get
            {
                string result = ClientUid + _botClassName + Number;

                return result;
            }
        }

        #region Parameters

        public List<IIStrategyParameter> Parameters = new List<IIStrategyParameter>();

        private ClientRobotParametersUi _parametersUi;

        public void ShowParametersDialog(TradeClient client)
        {

            if(_parametersUi == null)
            {
                _parametersUi = new ClientRobotParametersUi(this, client);
                _parametersUi.Show();
                _parametersUi.Closed += _parametersUi_Closed;
            }
            else
            {
                if(_parametersUi.WindowState == System.Windows.WindowState.Minimized)
                {
                    _parametersUi.WindowState = System.Windows.WindowState.Normal;
                }

                _parametersUi.Activate();
            }
        }

        private void _parametersUi_Closed(object sender, EventArgs e)
        {
            _parametersUi = null;
        }

        private void LoadParametersByBotClass()
        {
            if(string.IsNullOrEmpty(_botClassName))
            {
                return;
            }

            CheckBotPosition(_botClassName);

            BotPanel bot = BotFactory.GetStrategyForName(_botClassName, "", StartProgram.IsOsOptimizer, IsScript);

            if (bot == null)
            {
                return;
            }

            if (bot.Parameters == null ||
                bot.Parameters.Count == 0)
            {
                return;
            }

            if (Parameters != null)
            {
                Parameters.Clear();
            }

            for (int i = 0; i < bot.Parameters.Count; i++)
            {
                Parameters.Add(bot.Parameters[i]);
            }

            bot.Delete();
        }

        public void CheckBotPosition(string botClassName)
        {
            bool isInArray = false;

            List<string> scriptsNames = BotFactory.GetScriptsNamesStrategy();

            for (int i = 0; i < scriptsNames.Count; i++)
            {
                if (scriptsNames[i] == botClassName)
                {
                    isInArray = true;
                    IsScript = true;
                    break;
                }
            }

            if (isInArray == false)
            {
                IsScript = false;
            }
        }

        private bool CheckBotSources(string botClassName)
        {
            BotPanel bot = BotFactory.GetStrategyForName(botClassName, "", StartProgram.IsOsOptimizer, IsScript);

            if (bot == null)
            {
                return false;
            }

            List<IIBotTab> tabs = bot.GetTabs();

            if (tabs == null ||
                tabs.Count == 0)
            {
                SendNewLogMessage("Can`t create script " + botClassName + ". Source is none", LogMessageType.Error);
                return false;
            }

            for(int i = 0;i < tabs.Count;i++)
            {

                if (tabs[i].TabType == BotTabType.Simple)
                {
                    // ok
                }
                else if (tabs[i].TabType == BotTabType.Screener)
                {
                    // ok
                }
                else if (tabs[i].TabType == BotTabType.Index)
                {
                    // ok
                }
                else if (tabs[i].TabType == BotTabType.Polygon)
                {
                    SendNewLogMessage("Can`t create script " + botClassName + ". Source don`t support: " + BotTabType.Polygon, LogMessageType.Error);
                    return false;
                }

                else if (tabs[i].TabType == BotTabType.Cluster)
                {
                    SendNewLogMessage("Can`t create script " + botClassName + ". Source don`t support: " + BotTabType.Cluster, LogMessageType.Error);
                    return false;
                }
                else if (tabs[i].TabType == BotTabType.News)
                {
                    SendNewLogMessage("Can`t create script " + botClassName + ". Source don`t support: " + BotTabType.News, LogMessageType.Error);
                    return false;
                }
                else if (tabs[i].TabType == BotTabType.Options)
                {
                    SendNewLogMessage("Can`t create script " + botClassName + ". Source don`t support: " + BotTabType.Options, LogMessageType.Error);
                    return false;
                }
                else if (tabs[i].TabType == BotTabType.Pair)
                {
                    SendNewLogMessage("Can`t create script " + botClassName + ". Source don`t support: " + BotTabType.Pair, LogMessageType.Error);
                    return false;
                }
            }

            bot.Delete();


            return true;
        }

        #endregion

        #region Sources

        public List<TradeClientSourceSettings> SourceSettings = new List<TradeClientSourceSettings>();

        private ClientRobotSourcesUi _sourcesUi;

        public void ShowSourcesDialog(TradeClient client)
        {
            if(BotClassName == "None")
            {
                return;
            }

            if (_sourcesUi == null)
            {
                if (SourceSettings == null
                   || SourceSettings.Count == 0)
                {
                    return;
                }

                _sourcesUi = new ClientRobotSourcesUi(this, client);
                _sourcesUi.Show();
                _sourcesUi.Closed += _sourcesUi_Closed;
            }
            else
            {
                if (_sourcesUi.WindowState == System.Windows.WindowState.Minimized)
                {
                    _sourcesUi.WindowState = System.Windows.WindowState.Normal;
                }

                _sourcesUi.Activate();
            }
        }

        private void _sourcesUi_Closed(object sender, EventArgs e)
        {
            _sourcesUi = null;
        }

        private void LoadSourcesByBotClass()
        {
            if (string.IsNullOrEmpty(_botClassName))
            {
                return;
            }

            CheckBotPosition(_botClassName);

            BotPanel bot = BotFactory.GetStrategyForName(_botClassName, "", StartProgram.IsOsOptimizer, IsScript);

            if (bot == null)
            {
                return;
            }

            List<IIBotTab> tabs = bot.GetTabs();

            if (tabs == null ||
                tabs.Count == 0)
            {
                return;
            }

            if (SourceSettings != null)
            {
                SourceSettings.Clear();
            }

            for (int i = 0; i < tabs.Count; i++)
            {
                SourceSettings.Add(GetSettingsFromSource(tabs[i]));
            }

            bot.Delete();
        }

        private TradeClientSourceSettings GetSettingsFromSource(IIBotTab tab)
        {
            TradeClientSourceSettings newSourceSettings = new TradeClientSourceSettings();
            newSourceSettings.BotTabType = tab.TabType;

            if(tab.TabType == BotTabType.Simple)
            {
                newSourceSettings.Securities.Add(new TradeClientSecurity());
            }

            return newSourceSettings;
        }

        #endregion

        #region Log

        public void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            {
                ServerMaster.SendNewLogMessage(message, type);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion

    }

    public class TradeClientSourceSettings
    {
        // Common

        public int ClientServerNum;

        public string PortfolioName = "";

        public BotTabType BotTabType;

        public CommissionType CommissionType = CommissionType.None;

        public decimal CommissionValue = 0;

        public CandleMarketDataType CandleMarketDataType = CandleMarketDataType.Tick;

        public bool SaveTradesInCandle = false;

        public TimeFrame TimeFrame = TimeFrame.Min15;

        // Index

        public string UserFormula = "";

        public int CalculationDepth = 1500;

        public bool PercentNormalization = false;

        public IndexAutoFormulaBuilderRegime RegimeIndexBuilder = IndexAutoFormulaBuilderRegime.Off;

        public DayOfWeek DayOfWeekToRebuildIndex = DayOfWeek.Monday;

        public int HourInDayToRebuildIndex = 10;

        public SecuritySortType IndexSortType = SecuritySortType.VolumeWeighted;

        public int IndexSecCount = 5;

        public IndexMultType IndexMultType = IndexMultType.VolumeWeighted;

        public int DaysLookBackInBuilding = 20;

        // Securities

        public List<TradeClientSecurity> Securities = new List<TradeClientSecurity>();

        public TradeClientSecurity AddNewSecurity()
        {
            TradeClientSecurity security = new TradeClientSecurity();

            Securities.Add(security);

            return security;
        }

        public void RemoveSecurityAt(int index)
        {
            if(index >= Securities.Count)
            {
                return;
            }

            Securities.RemoveAt(index);
        }

        public string GetSaveString()
        {
            string save = "";

            save += ClientServerNum + "#"; // 0
            save += BotTabType + "#"; // 1
            save += CommissionType + "#"; // 2
            save += CommissionValue + "#";// 3
            save += CandleMarketDataType + "#";// 4
            save += SaveTradesInCandle + "#";// 5
            save += TimeFrame + "#";// 6

            save += UserFormula + "#";// 7
            save += CalculationDepth + "#";// 8
            save += PercentNormalization + "#";// 9
            save += RegimeIndexBuilder + "#";// 10
            save += DayOfWeekToRebuildIndex + "#";// 11
            save += HourInDayToRebuildIndex + "#";// 12
            save += IndexSortType + "#";// 13
            save += IndexSecCount + "#";// 14
            save += IndexMultType + "#";// 15
            save += DaysLookBackInBuilding + "#";// 16
            save += PortfolioName + "#";// 17
            save += "#";// 18
            save += "#";// 19
            save += "#";// 20
            save += "#";// 21
            save += "#";// 22
            save += "#";// 23
            save += "#";// 24
            save += "#"; // 25

            for (int i = 0;i < Securities.Count;i++)
            {
                save += Securities[i].Name + "#";
                save += Securities[i].Class + "#";
            }

            return save;
        }

        public void SetFromString(string saveString)
        {
            string[] saveArray = saveString.Split('#');

            ClientServerNum = Convert.ToInt32(saveArray[0]);
            Enum.TryParse(saveArray[1], out BotTabType);
            Enum.TryParse(saveArray[2], out CommissionType);
            CommissionValue = saveArray[3].ToDecimal();
            Enum.TryParse(saveArray[4], out CandleMarketDataType);
            SaveTradesInCandle = Convert.ToBoolean(saveArray[5]);
            Enum.TryParse(saveArray[6], out TimeFrame);

            UserFormula = saveArray[7];
            CalculationDepth = Convert.ToInt32(saveArray[8]);
            PercentNormalization = Convert.ToBoolean(saveArray[9]);
            Enum.TryParse(saveArray[10], out RegimeIndexBuilder);
            Enum.TryParse(saveArray[11], out DayOfWeekToRebuildIndex);
            HourInDayToRebuildIndex = Convert.ToInt32(saveArray[12]);
            Enum.TryParse(saveArray[13], out IndexSortType);
            IndexSecCount = Convert.ToInt32(saveArray[14]);
            Enum.TryParse(saveArray[15], out IndexMultType);
            DaysLookBackInBuilding = Convert.ToInt32(saveArray[16]);
            PortfolioName = saveArray[17];

            for (int i = 26; i < saveArray.Length; i+=2)
            {
                if(i +1 == saveArray.Length)
                {
                    continue;
                }
                TradeClientSecurity newSec = new TradeClientSecurity();
                newSec.Name = saveArray[i];
                newSec.Class = saveArray[i + 1];
                Securities.Add(newSec);
            }
        }
    }

    public class TradeClientCustomCandlesSettings
    {
        public string Name;

        public string Type;

        public string Value;

    }

    public class TradeClientSecurity
    {
        public string Name;

        public string Class;

    }

}
