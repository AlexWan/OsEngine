/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.OsTrader.Panels;
using System;
using System.Collections.Generic;
using System.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace OsEngine.Market.AutoFollow
{

    public class CopyTrader
    {
        public CopyTrader(string saveStr)
        {
            string[] save = saveStr.Split('%');
            Number = Convert.ToInt32(save[0]);
            Name = save[1];
            IsOn = Convert.ToBoolean(save[2]);
            PanelsPosition = save[3];

            if (save[4].Split('!').Length > 1)
            {
                OnRobotsNames = save[4].Split('!').ToList();
            }

            LoadPortfolios(save[5]);

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

        public bool IsOn;

        public string PanelsPosition = "1,1,1,1,1";

        public string GetStringToSave()
        {
            string result = Number + "%";
            result += Name + "%";
            result += IsOn + "%";
            result += PanelsPosition + "%";
            result += OnRobotsNamesInString + "%";
            result += GetStringToSavePortfolios() + "%";

            return result;
        }

        public void ClearDelete()
        {
            if(DeleteEvent != null)
            {
                DeleteEvent();
            }
        }

        public void Save()
        {
            if(NeedToSaveEvent != null)
            {
                NeedToSaveEvent();
            }
        }

        public event Action DeleteEvent;

        public event Action NeedToSaveEvent;

        #region Robots for auto follow

        public List<string> OnRobotsNames = new List<string>();

        private string OnRobotsNamesInString
        {
            get
            {
                string result = "";

                for(int i = 0;i < OnRobotsNames.Count;i++)
                {
                    result += OnRobotsNames[i] + "!";
                }

                return result;
            }
        }

        public bool BotIsOnToCopy(BotPanel bot)
        {
            for(int i = 0;i < OnRobotsNames.Count;i++)
            {
                if (OnRobotsNames[i] == bot.NameStrategyUniq)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Portfolios to copy

        public List<PortfolioToCopy> PortfolioToCopy = new List<PortfolioToCopy>();

        public PortfolioToCopy GetPortfolioByName(string serverName, string portfolioName)
        {
            for(int i = 0;i <PortfolioToCopy.Count;i++)
            {
                if (PortfolioToCopy[i].ServerName == serverName
                    && PortfolioToCopy[i].PortfolioName == portfolioName)
                {
                    return PortfolioToCopy[i];
                }
            }

            PortfolioToCopy portfolio = new PortfolioToCopy();
            portfolio.ServerName = serverName;
            portfolio.PortfolioName = portfolioName;
            portfolio.LogMessageEvent += SendLogMessage;
            PortfolioToCopy.Add(portfolio);
            
            if (NeedToSaveEvent != null)
            {
                NeedToSaveEvent();
            }

            return portfolio;
        }

        private string GetStringToSavePortfolios()
        {
            string result = "";

            for(int i = 0;i < PortfolioToCopy.Count;i++)
            {
                result += PortfolioToCopy[i].GetSaveString() + "&";
            }

            return result;
        }

        private void LoadPortfolios(string saveStr)
        {
            string[] saveArray = saveStr.Split('&');

            for(int i = 0;i < saveArray.Length;i++)
            {
                if (string.IsNullOrEmpty(saveArray[i]))
                {
                    continue;
                }
                PortfolioToCopy portfolio = new PortfolioToCopy();
                portfolio.SetFromString(saveArray[i]);
                PortfolioToCopy.Add(portfolio);
            }
        }

        #endregion

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

    public class PortfolioToCopy
    {
        #region Settings

        public string ServerName;

        public ServerType ServerType
        {
            get
            {
                if (string.IsNullOrEmpty(ServerName))
                {
                    return ServerType.None;
                }

                ServerType serverType;

                Enum.TryParse(ServerName.Split('_')[0], out serverType);

                return serverType;
            }
        }

        public string PortfolioName;

        public string UniqueName
        {
            get 
            { 
                return ServerName + "~" + PortfolioName; 
            }
        }

        public bool IsOn = false;

        public CopyTraderVolumeType VolumeType;

        public decimal VolumeMult = 1;

        public string MasterAsset = "Prime";

        public string SlaveAsset = "Prime";

        public CopyTraderCopyType CopyType;

        public CopyTraderOrdersType OrderType = CopyTraderOrdersType.Market;

        public int IcebergCount = 2;

        public string GetSaveString()
        {
            string result = "";
            result += ServerName + "#";
            result += PortfolioName + "#";
            result += IsOn + "#";
            result += VolumeType + "#";
            result += VolumeMult + "#";
            result += MasterAsset + "#";
            result += SlaveAsset + "#";
            result += CopyType + "#";
            result += OrderType + "#";
            result += IcebergCount + "#";
            result += GetSecuritiesSaveString() + "#";

            return result;       
        }

        public void SetFromString(string str)
        {
            string[] saveArray = str.Split('#');
            ServerName = saveArray[0];

            PortfolioName = saveArray[1];
            IsOn = Convert.ToBoolean(saveArray[2]);
            Enum.TryParse(saveArray[3], out VolumeType);

            VolumeMult = saveArray[4].ToDecimal();
            MasterAsset = saveArray[5];
            SlaveAsset = saveArray[6];

            Enum.TryParse(saveArray[7], out CopyType);
            Enum.TryParse(saveArray[8], out OrderType);
            IcebergCount = Convert.ToInt32(saveArray[9]);
            LoadSecuritiesFromString(saveArray[10]);
        }

        #endregion

        #region Securities

        public List<SecurityToCopy> SecurityToCopy = new List<SecurityToCopy>();

        private string GetSecuritiesSaveString()
        {
            string result = "";

            for(int i = 0;i < SecurityToCopy.Count;i++)
            {
                result += SecurityToCopy[i].GetSaveString() + "*";
            }

            return result;
        }

        private void LoadSecuritiesFromString(string str)
        {
            string[] array = str.Split('*');

            for(int i = 0;i < array.Length;i++)
            {
                if (string.IsNullOrEmpty(array[i]))
                {
                    continue;
                }

                SecurityToCopy securityToCopy = new SecurityToCopy();
                securityToCopy.SetSaveString(array[i]);
                SecurityToCopy.Add(securityToCopy);
            }
        }

        #endregion

        #region Journal




        #endregion

        #region Copy position logic




        #endregion

        #region Log

        public Log LogCopyTrader;

        public event Action<string, LogMessageType> LogMessageEvent;

        public void SendLogMessage(string message, LogMessageType messageType)
        {
            message = "Copy portfolio.  server:" + ServerName + " portfolio: " + PortfolioName + " message: \n" + message;
            LogMessageEvent?.Invoke(message, messageType);
        }

        #endregion
    }

    public enum CopyTraderVolumeType
    {
        QtyMultiplicator,
        DepoProportional
    }

    public enum CopyTraderCopyType
    {
        FullCopy,
        Absolute
    }

    public enum CopyTraderOrdersType
    {
        Market,
        Iceberg
    }

    public class SecurityToCopy
    {
        public string MasterSecurityName;

        public string MasterSecurityClass;

        public string SlaveSecurityName;

        public string SlaveSecurityClass;

        public string GetSaveString()
        {
            string result = MasterSecurityName + "^";
            result += MasterSecurityClass + "^";
            result += SlaveSecurityName + "^";
            result += SlaveSecurityClass + "^";

            return result;
        }

        public void SetSaveString(string str)
        {
            string[] saveArray = str.Split('^');

            MasterSecurityName = saveArray[0];
            MasterSecurityClass = saveArray[1];
            SlaveSecurityName = saveArray[2];
            SlaveSecurityClass = saveArray[3];

        }

    }

}
