/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Net;

namespace OsEngine.Market.Proxy
{
    public class ProxyOsa
    {
        public WebProxy GetWebProxy()
        {
            string address = Ip + ":" + Port.ToString();

            WebProxy newProxy = new WebProxy(address);
           
            newProxy.Credentials = new NetworkCredential(Login, UserPassword);

            return newProxy;
        }

        public int Number;

        public bool IsOn = false;

        public string Ip;

        public int Port;

        public string Login;

        public string UserPassword;

        public string Location = "Unknown";

        public string AutoPingLastStatus = "Unknown";

        public string PingWebAddress = "http://ipinfo.io/";

        public int UseConnectionCount;

        public string GetStringToSave()
        {
            string result = IsOn + "%";
            result += Number + "%";
            result += Location + "%";
            result += Ip + "%";
            result += Port + "%";
            result += Login + "%";
            result += UserPassword + "%";
            result += AutoPingLastStatus + "%";
            result += PingWebAddress + "%";

            return result;
        }

        public void LoadFromString(string saveStr)
        {
            IsOn = Convert.ToBoolean(saveStr.Split('%')[0]);
            Number = Convert.ToInt32(saveStr.Split('%')[1]);
            Location = saveStr.Split('%')[2];
            Ip = saveStr.Split('%')[3];
            Port = int.Parse(saveStr.Split('%')[4]);
            Login = saveStr.Split('%')[5];
            UserPassword = saveStr.Split('%')[6];
            AutoPingLastStatus = saveStr.Split('%')[7];
            PingWebAddress = saveStr.Split('%')[8];
        }
    }
}