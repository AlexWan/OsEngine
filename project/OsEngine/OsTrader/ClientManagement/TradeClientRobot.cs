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

namespace OsEngine.OsTrader.ClientManagement
{
    public class TradeClientRobot
    {
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

                _botClassName = value;
                LoadParametersByBotClass();
                LoadSourcesByBotClass();
            }
        }
        private string _botClassName = "None";

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

        public string GetSaveString()
        {
            string saveStr = "";

            saveStr += Number + "&";
            saveStr += IsOn + "&";
            saveStr += BotClassName + "&";
            saveStr += "&";
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

            BotPanel bot = BotFactory.GetStrategyForName(_botClassName, "", StartProgram.IsOsOptimizer, true);

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

            BotPanel bot = BotFactory.GetStrategyForName(_botClassName, "", StartProgram.IsOsOptimizer, true);

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

    public class TradeClientRobotsParameter
    {
        public string Name;

        public StrategyParameterType Type;

        public string Value;

        public List<string> ValuesEnum = new List<string>();

        public string GetSaveString()
        {




            return "";
        }

        public void SetFromString(string saveString)
        {





        }
    }

    public class TradeClientSourceSettings
    {
        public int ClientServerNum;

        public BotTabType BotTabType;

        public CommissionType CommissionType = CommissionType.None;

        public decimal CommissionValue = 0;

        public CandleMarketDataType CandleMarketDataType = CandleMarketDataType.Tick;

        public bool SaveTradesInCandle = false;

        public TimeFrame TimeFrame = TimeFrame.Min15;

        #region Pair



        #endregion

        #region Index



        #endregion

        public string GetSaveString()
        {
            return "";
        }

        public void SetFromString(string saveString)
        {

        }
    }

    public class TradeClientCustomCandlesSettings
    {
        public string Name;

        public string Type;

        public string Value;

    }

}
