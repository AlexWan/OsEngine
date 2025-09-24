/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers.Entity;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.OsTrader.ClientManagement
{
    public class TradeClientRobot
    {
        public int Number;

        public bool IsOn;

        public string BotClassName;

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

        public List<TradeClientRobotsParameter> Parameters;

        public List<TradeClientSourceSettings> SourceSettings;

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
                saveStr += Parameters[i].GetSaveString() + "^";
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
            BotClassName = saveValues[2];

            string[] parameters = saveValues[7].Split("^");

            for(int i = 0;i < parameters.Length;i++)
            {
                if (string.IsNullOrEmpty(parameters[i]) == true)
                {
                    continue;
                }

                TradeClientRobotsParameter newParameter = new TradeClientRobotsParameter();
                newParameter.SetFromString(parameters[i]);
                Parameters.Add(newParameter);
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
        public string GetSaveString()
        {
            return "";
        }

        public void SetFromString(string saveString)
        {

        }
    }
}
