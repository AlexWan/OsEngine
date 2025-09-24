/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Logging;
using OsEngine.Market;
using System;
using System.Collections.Generic;
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


    }

    public class TradeClientSourceSettings
    {

    }
}
