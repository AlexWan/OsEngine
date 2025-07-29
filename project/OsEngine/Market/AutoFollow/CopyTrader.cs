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
            string[] save = saveStr.Split('%');
            Number = Convert.ToInt32(save[0]);
            Name = save[1];
            Enum.TryParse(save[2], out WorkType);
            IsOn = Convert.ToBoolean(save[3]);
            PanelsPosition = save[4];

            if (save[5].Split('!').Length > 1)
            {
                OnRobotsNames = save[5].Split('!').ToList();
            }

            LoadPortfolios(save[6]);

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

        public string PanelsPosition = "1,1,1,1,1";

        public string GetStringToSave()
        {
            string result = Number + "%";
            result += Name + "%";
            result += WorkType + "%";
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
            PortfolioToCopy.Add(portfolio);
            
            if(NeedToSaveEvent != null)
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
        public string ServerName;

        public string PortfolioName;

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
        }
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

}
