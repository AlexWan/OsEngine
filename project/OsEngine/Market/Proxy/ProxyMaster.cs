/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Proxy
{
    public class ProxyMaster
    {
        public void Activate()
        {
            Load();

            SendLogMessage("Proxy master activated. Proxy count: " 
                + _proxies.Count, LogMessageType.System);
        }

        private void Load()
        {


        }

        public void Save()
        {


        }

        public void ShowDialog()
        {


        }

        public bool AutoPingIsOn;

        public DateTime AutoPingLastTime;

        public bool AutoLocationIsOn;

        #region Proxy hub

        private List<ProxyOsa> _proxies = new List<ProxyOsa>();

        public WebProxy GetProxy(ServerType serverType, string serverName)
        {
            if(_proxies.Count == 0)
            {
                return null;
            }



            return null;
        }

        #endregion

        #region Log

        public event Action<string, LogMessageType> LogMessageEvent;

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            message = "Proxy master.  " + message;
            LogMessageEvent?.Invoke(message, messageType);
        }

        #endregion

    }
}