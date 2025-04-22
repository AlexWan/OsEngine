/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Net;

namespace OsEngine.Market.Proxy
{
    public class ProxyOsa
    {
        public WebProxy GetWebProxy()
        {
            string address = Ip.Replace(":" + Port.ToString(), "");

            WebProxy newProxy = new WebProxy(address);
            newProxy.Credentials = new NetworkCredential(UserName, UserPassword);

            return newProxy;
        }

        public bool IsOn;

        public string Prefix = "Unknown";

        public string Location = "Unknown";

        public string Ip;

        public int Port;

        public string UserName;

        public string UserPassword;

        public string AutoPingLastStatus;

        public int MaxConnectorsOnThisProxy = 5;

        public string PingWebAddress = "https://www.moex.com";

        public List<ServerType> AllowConnection = new List<ServerType>();

        public List<string> UseConnection = new List<string>();

        public string GetStringToSave()
        {
            string result = IsOn + "%";
            result += Prefix + "%";
            result += Location + "%";
            result += Ip + "%";
            result += Port + "%";
            result += UserName + "%";
            result += UserPassword + "%";
            result += AutoPingLastStatus + "%";
            result += PingWebAddress + "%";
            result += MaxConnectorsOnThisProxy ;

            return result;
        }

        public void LoadFromString(string saveStr)
        {
            IsOn = Convert.ToBoolean(saveStr.Split('%')[0]);
            Prefix = saveStr.Split('%')[1];
            Location = saveStr.Split('%')[2];
            Ip = saveStr.Split('%')[3];
            Port = int.Parse(saveStr.Split('%')[4]);
            UserName = saveStr.Split('%')[5];
            UserPassword = saveStr.Split('%')[6];
            AutoPingLastStatus = saveStr.Split('%')[7];
            PingWebAddress = saveStr.Split('%')[8];
            MaxConnectorsOnThisProxy = Convert.ToInt32(saveStr.Split('%')[9]);
        }
    }
}