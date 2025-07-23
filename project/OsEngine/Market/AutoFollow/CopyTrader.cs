/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.AutoFollow
{

    public enum CopyTraderType
    {
        None,
        Portfolio,
        Robot
    }

    public class CopyTrader
    {
        public CopyTrader(string saveStr)
        {
            Number = Convert.ToInt32(saveStr.Split('%')[0]);
            Name = saveStr.Split('%')[1];
            Enum.TryParse(saveStr.Split('%')[2], out WorkType);
            IsOn = Convert.ToBoolean(saveStr.Split('%')[3]);

            LogCopyTrader = new Log("CopyTrader" + Number, Entity.StartProgram.IsOsTrader);
            LogCopyTrader.Listen(this);
        }

        public CopyTrader(int number)
        {
            Number = number;
            LogCopyTrader = new Log("CopyTrader" + Number, Entity.StartProgram.IsOsTrader);
            LogCopyTrader.Listen(this);
        }

        private CopyTrader()
        {

        }

        public int Number;

        public string Name;

        public CopyTraderType WorkType;

        public bool IsOn;

        public string GetStringToSave()
        {
            string result = Number + "%";
            result += Name + "%";
            result += WorkType + "%";
            result += IsOn + "%";

            return result;
        }

        public void ClearDelete()
        {
            if(DeleteEvent != null)
            {
                DeleteEvent();
            }
        }

        public event Action DeleteEvent;

        #region Log

        public Log LogCopyTrader;

        public event Action<string, LogMessageType> LogMessageEvent;

        public void SendLogMessage(string message, LogMessageType messageType)
        {
            message = "Copy trader.  Num:" + Number + " " + message;
            LogMessageEvent?.Invoke(message, messageType);
        }

        #endregion
    }

}
