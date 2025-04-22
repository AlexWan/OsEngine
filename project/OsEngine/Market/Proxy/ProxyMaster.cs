/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Logging;
using System;
using System.Collections.Generic;
using System.IO;
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
            LoadSettings();
            LoadProxy();

            SendLogMessage("Proxy master activated. Proxy count: " 
                + _proxies.Count, LogMessageType.System);
        }

        private void LoadSettings()
        {
            if (!File.Exists(@"Engine\" + @"ProxyMaster.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + @"ProxyMaster.txt"))
                {
                    AutoPingIsOn = Convert.ToBoolean(reader.ReadLine());
                    AutoPingLastTime = Convert.ToDateTime(reader.ReadLine());
                    AutoLocationIsOn = Convert.ToBoolean(reader.ReadLine());
                    AutoPingMinutes = Convert.ToInt32(reader.ReadLine());

                    reader.Close();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void SaveSettings()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + @"ProxyMaster.txt", false))
                {
                    writer.WriteLine(AutoPingIsOn);
                    writer.WriteLine(AutoPingLastTime);
                    writer.WriteLine(AutoLocationIsOn);
                    writer.WriteLine(AutoPingMinutes);

                    writer.Close();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void ShowDialog()
        {



        }

        public bool AutoPingIsOn = true;

        public DateTime AutoPingLastTime;

        public int AutoPingMinutes = 10;

        public bool AutoLocationIsOn = true;

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

        private void LoadProxy()
        {
            if (!File.Exists(@"Engine\" + @"ProxyHub.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + @"ProxyHub.txt"))
                {
                    while(reader.EndOfStream == false)
                    {
                        string line = reader.ReadLine();

                        if(string.IsNullOrEmpty(line))
                        {
                            continue;
                        }

                        ProxyOsa newProxy = new ProxyOsa();
                        newProxy.LoadFromString(line);
                        _proxies.Add(newProxy);
                    }

                    reader.Close();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void SaveProxy()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + @"ProxyHub.txt", false))
                {
                    for (int i = 0; i < _proxies.Count; i++)
                    {
                        writer.WriteLine(_proxies[i].GetStringToSave());
                    }

                    writer.Close();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Proxy ping




        #endregion

        #region Proxy location




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