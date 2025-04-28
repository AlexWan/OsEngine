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
            string address = Ip + ":" + Port.ToString();

            WebProxy newProxy = new WebProxy(address);
            newProxy.Credentials = new NetworkCredential(Login, UserPassword);

            return newProxy;
        }

        public string UniqueName
        {
            get
            {
                string result = Number.ToString();

                if(string.IsNullOrEmpty(Prefix) == false)
                {
                    result = result + "_" + Prefix;
                }

                return result;
            }
        }

        public int Number;

        public string Prefix;

        public bool IsOn = false;

        public string Ip;

        public int Port;

        public string Login;

        public string UserPassword;

        public string Location = "Unknown";

        public int MaxConnectorsOnThisProxy = 5;

        public string AutoPingLastStatus = "Unknown";

        public string PingWebAddress = "https://www.moex.com";

        public string AllowConnectionCount
        {
            get
            {
                return AllowConnection.Count.ToString();
            }
        }
        public List<ServerType> AllowConnection = new List<ServerType>();

        public string UseConnectionCount
        {
            get
            {
                return UseConnection.Count.ToString();
            }
        }
        public List<string> UseConnection = new List<string>();

        public string GetStringToSave()
        {
            string result = IsOn + "%";
            result += Number + "%";
            result += Prefix + "%";
            result += Location + "%";
            result += Ip + "%";
            result += Port + "%";
            result += Login + "%";
            result += UserPassword + "%";
            result += AutoPingLastStatus + "%";
            result += PingWebAddress + "%";
            result += MaxConnectorsOnThisProxy ;

            return result;
        }

        public void LoadFromString(string saveStr)
        {
            IsOn = Convert.ToBoolean(saveStr.Split('%')[0]);
            Number = Convert.ToInt32(saveStr.Split('%')[1]);
            Prefix = saveStr.Split('%')[2];
            Location = saveStr.Split('%')[3];
            Ip = saveStr.Split('%')[4];
            Port = int.Parse(saveStr.Split('%')[5]);
            Login = saveStr.Split('%')[6];
            UserPassword = saveStr.Split('%')[7];
            AutoPingLastStatus = saveStr.Split('%')[8];
            PingWebAddress = saveStr.Split('%')[9];
            MaxConnectorsOnThisProxy = Convert.ToInt32(saveStr.Split('%')[10]);
        }
    }
}