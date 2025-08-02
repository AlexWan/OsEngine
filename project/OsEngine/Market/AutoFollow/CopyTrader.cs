/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.OsTrader.Panels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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
                MasterRobotsNames = save[4].Split('!').ToList();
            }

            LoadPortfolios(save[5]);

            LogCopyTrader = new Log("CopyTrader" + Number, Entity.StartProgram.IsOsTrader);
            LogCopyTrader.Listen(this);

            Task.Run(WorkThreadArea);
        }

        public CopyTrader(int number)
        {
            Number = number;
            LogCopyTrader = new Log("CopyTrader" + Number, Entity.StartProgram.IsOsTrader);
            LogCopyTrader.Listen(this);

            Task.Run(WorkThreadArea);
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

            for(int i = 0;i < PortfolioToCopy.Count;i++)
            {
                PortfolioToCopy[i].Delete();
            }
        }

        public void Save()
        {
            if(NeedToSaveEvent != null)
            {
                NeedToSaveEvent();
            }

            for(int i = 0;i < PortfolioToCopy.Count;i++)
            {
                PortfolioToCopy[i].Save();
            }
        }

        public event Action DeleteEvent;

        public event Action NeedToSaveEvent;

        #region Work thead

        private bool _objectIsDelete;

        private async void WorkThreadArea()
        {
            while (true)
            {
                try
                {
                    await Task.Delay(1000);

                    if (_objectIsDelete == true)
                    {
                        return;
                    }

                    for(int i = 0;i < PortfolioToCopy.Count;i++)
                    {
                        PortfolioToCopy[i].Process(MasterRobotsNames);
                    }
                }
                catch(Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                }
            }
        }

        #endregion

        #region Robots for auto follow

        public List<string> MasterRobotsNames = new List<string>();

        private string OnRobotsNamesInString
        {
            get
            {
                string result = "";

                for(int i = 0;i < MasterRobotsNames.Count;i++)
                {
                    result += MasterRobotsNames[i] + "!";
                }

                return result;
            }
        }

        public bool BotIsOnToCopy(BotPanel bot)
        {
            for(int i = 0;i < MasterRobotsNames.Count;i++)
            {
                if (MasterRobotsNames[i] == bot.NameStrategyUniq)
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

            PortfolioToCopy portfolio 
                = new PortfolioToCopy(
                    Number + "_PortfolioCopier_" + serverName + "_" + portfolioName);

            portfolio.ServerName = serverName;
            portfolio.PortfolioName = portfolioName;
            portfolio.Save();
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
                result += PortfolioToCopy[i].NameUnique + "&";
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

                PortfolioToCopy portfolio = new PortfolioToCopy(saveArray[i]);
                PortfolioToCopy.Add(portfolio);
            }
        }

        public void RemovePortfolioAt(int number)
        {
            if(number >= PortfolioToCopy.Count)
            {
                return;
            }
            PortfolioToCopy[number].Delete();
            PortfolioToCopy.RemoveAt(number);
            Save();
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
        public PortfolioToCopy(string name)
        {
            NameUnique = name;

            Load();

            _journal = new Journal.Journal(name, StartProgram.IsOsTrader);

            //_journal.PositionStateChangeEvent += _journal_PositionStateChangeEvent;
            //_journal.PositionNetVolumeChangeEvent += _journal_PositionNetVolumeChangeEvent;
            //_journal.UserSelectActionEvent += _journal_UserSelectActionEvent;
            _journal.LogMessageEvent += SendLogMessage;
        }

        public string NameUnique;

        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\CopyTrader\" + NameUnique + ".txt", false))
                {
                    writer.WriteLine(ServerName);
                    writer.WriteLine(PortfolioName);
                    writer.WriteLine(IsOn);
                    writer.WriteLine(VolumeType);
                    writer.WriteLine(VolumeMult);
                    writer.WriteLine(MasterAsset);
                    writer.WriteLine(SlaveAsset);
                    writer.WriteLine(CopyType);
                    writer.WriteLine(OrderType);
                    writer.WriteLine(IcebergCount);
                    writer.WriteLine(GetSecuritiesSaveString());
                    writer.Close();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void Load()
        {
            if (!File.Exists(@"Engine\CopyTrader\" + NameUnique + ".txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\CopyTrader\" + NameUnique + ".txt"))
                {
                    ServerName = reader.ReadLine();

                    PortfolioName = reader.ReadLine();
                    IsOn = Convert.ToBoolean(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), out VolumeType);

                    VolumeMult = reader.ReadLine().ToDecimal();
                    MasterAsset = reader.ReadLine();
                    SlaveAsset = reader.ReadLine();

                    Enum.TryParse(reader.ReadLine(), out CopyType);
                    Enum.TryParse(reader.ReadLine(), out OrderType);
                    IcebergCount = Convert.ToInt32(reader.ReadLine());
                    LoadSecuritiesFromString(reader.ReadLine());

                    reader.Close();
                }
            }
            catch
            {
                // ignore
            }

        }

        public void Delete()
        {
            try
            {
                if (File.Exists(@"Engine\CopyTrader\" + NameUnique + ".txt"))
                {
                    File.Delete(@"Engine\CopyTrader\" + NameUnique + ".txt");
                }
            }
            catch
            {
                // ignore
            }
        }

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

        public bool IsOn = false;

        public CopyTraderVolumeType VolumeType;

        public decimal VolumeMult = 1;

        public string MasterAsset = "Prime";

        public string SlaveAsset = "Prime";

        public CopyTraderCopyType CopyType;

        public CopyTraderOrdersType OrderType = CopyTraderOrdersType.Market;

        public int IcebergCount = 2;

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

        #region Copy position logic

        public void Process(List<string> masterRobots)
        {
            if(IsOn == false)
            {
                return;
            }

            if(masterRobots == null ||
                masterRobots.Count == 0)
            {
                return;
            }




        }

        #endregion

        #region Journal

        public Journal.Journal _journal;

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
