/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Logging;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader;
using System;
using System.Collections.Generic;
using System.Threading;
using OsEngine.Entity;
using System.IO;

namespace OsEngine.Market.Servers
{
    public class ComparePositionsModule
    {
        public ComparePositionsModule(AServer server)
        {
            Server = server;

            Load();
            
            LoadIgnoredSecurities();

            Thread worker = new Thread(UpdaterThreadWorker);
            worker.Start();
        }

        public AServer Server;

        public ComparePositionsVerificationPeriod VerificationPeriod
        {
            get
            {
                return _verificationPeriod;
            }
            set
            {
                if (_verificationPeriod == value)
                {
                    return;
                }

                _verificationPeriod = value;
                Save();
            }
        }
        private ComparePositionsVerificationPeriod _verificationPeriod;

        public int TimeDelaySeconds = 20;

        public List<string> PortfoliosToWatch = new List<string>();

        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + Server.ServerNameUnique + @"CompareModule.txt", false))
                {
                    writer.WriteLine(_verificationPeriod + "#" + TimeDelaySeconds);

                    for(int i = 0;i < PortfoliosToWatch.Count;i++)
                    {
                        writer.WriteLine(PortfoliosToWatch[i]);
                    }

                    writer.Close();
                }
            }
            catch
            {
                // ignore
            }
        }

        private void Load()
        {
            if (!File.Exists(@"Engine\" + Server.ServerNameUnique + @"CompareModule.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + Server.ServerNameUnique + @"CompareModule.txt"))
                {
                    string[] firstSaveStr = reader.ReadLine().Split('#');

                    Enum.TryParse(firstSaveStr[0], out _verificationPeriod);

                    if(firstSaveStr.Length > 1 
                        && string.IsNullOrEmpty(firstSaveStr[1]) == false)
                    {
                        TimeDelaySeconds = Convert.ToInt32(firstSaveStr[1]);
                    }

                    while(reader.EndOfStream == false)
                    {
                        string portfolio = reader.ReadLine();

                        if(string.IsNullOrEmpty(portfolio)  == false)
                        {
                            PortfoliosToWatch.Add(portfolio);
                        }
                    }

                    reader.Close();
                }
            }
            catch
            {
                // ignore
            }
        }

        public List<ComparePositionsPortfolio> ComparePortfolios = new List<ComparePositionsPortfolio>();

        private DateTime _lastCheckTime = DateTime.MinValue;

        private void UpdaterThreadWorker()
        {
            while(true)
            {
                try
                {
                    Thread.Sleep(3000);

                    if (MainWindow.ProccesIsWorked == false)
                    {
                        return;
                    }

                    if (Server.ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        continue;
                    }

                    if(PortfoliosToWatch.Count == 0)
                    {
                        continue;
                    }

                    if(Server.LastStartServerTime.AddMinutes(1) > DateTime.Now)
                    {
                        continue;
                    }

                    int minutesAwait = 1;

                    if(_verificationPeriod == ComparePositionsVerificationPeriod.Min5)
                    {
                        minutesAwait = 5;
                    }
                    else if (_verificationPeriod == ComparePositionsVerificationPeriod.Min10)
                    {
                        minutesAwait = 10;
                    }
                    else if (_verificationPeriod == ComparePositionsVerificationPeriod.Min30)
                    {
                        minutesAwait = 30;
                    }

                    if (_lastCheckTime.AddMinutes(minutesAwait) > DateTime.Now)
                    {
                        continue;
                    }

                    _lastCheckTime = DateTime.Now;

                    // 1 первая проверка. Пока не высылаем ошибку.

                    bool haveErrorInSomePortfolio = false;

                    List<ComparePositionsPortfolio> portfolios = UpdateCompareData();

                    for(int i = 0; portfolios != null && i < portfolios.Count; i++)
                    {
                        for(int j = 0;j < PortfoliosToWatch.Count; j++)
                        {
                            if (PortfoliosToWatch[j] == portfolios[i].PortfolioName)
                            {
                                bool haveError = HaveErrorInPortfolio(portfolios[i], false);

                                if (haveError)
                                {
                                    haveErrorInSomePortfolio = true;
                                }
                            }
                        }
                    }

                    // 2 вторая проверка. Через N секунд. Если и тут ошибка - то высылаем

                    if(haveErrorInSomePortfolio == true)
                    {
                        Thread.Sleep(TimeDelaySeconds);

                        portfolios = UpdateCompareData();

                        for (int i = 0; portfolios != null && i < portfolios.Count; i++)
                        {
                            for (int j = 0; j < PortfoliosToWatch.Count; j++)
                            {
                                if (PortfoliosToWatch[j] == portfolios[i].PortfolioName)
                                {
                                    bool haveError = HaveErrorInPortfolio(portfolios[i], true);

                                    if (haveError)
                                    {
                                        haveErrorInSomePortfolio = true;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    SendLogMessage(e.ToString(),LogMessageType.Error);
                    Thread.Sleep(10000);
                }
            }
        }

        private bool HaveErrorInPortfolio(ComparePositionsPortfolio portfolio, bool canSendErrorMessage)
        {
            if(portfolio == null)
            {
                return false;
            }

            if(portfolio.CompareSecurities == null 
                || portfolio.CompareSecurities .Count == 0)
            {
                return false;
            }

            string message = Server.ServerNameUnique + ". Error on compare securities in robot and portfolio \n";
            bool haveError = false;

            for(int i = 0;i <  portfolio.CompareSecurities.Count;i++)
            {
                ComparePositionsSecurity security = portfolio.CompareSecurities[i];

                if(security.Status == ComparePositionsStatus.Normal)
                {
                    continue;
                }

                if(security.IsIgnored == true)
                {
                    continue;
                }

                haveError = true;

                string securityError = "Security: " + security.Security + "\n";
                securityError += "botsLong: " + security.RobotsLong;
                securityError += " botsShort: " + security.RobotsShort;
                securityError += " botsCommon: " + security.RobotsCommon + "\n";
                securityError += "portfolioLong: " + security.PortfolioLong;
                securityError += " portfolioShort: " + security.PortfolioShort;
                securityError += " portfolioCommon: " + security.PortfolioCommon;

                message += securityError;
            }

            if(haveError)
            {
                if(canSendErrorMessage)
                {
                    SendLogMessage(message, LogMessageType.Error);
                }

                return true;
            }

            return false;
        }

        public List<ComparePositionsPortfolio> UpdateCompareData()
        {
            try
            {
                List<ComparePositionsPortfolio> result = new List<ComparePositionsPortfolio>();

                List<string> portfolios = GetPortfolios();

                if (portfolios.Count == 0)
                {
                    return result;
                }

                for (int i = 0; i < portfolios.Count; i++)
                {
                    ComparePositionsPortfolio newPortfolio = new ComparePositionsPortfolio();
                    newPortfolio.PortfolioName = portfolios[i];
                    newPortfolio.CompareSecurities = GetSecuritiesToPortfolio(newPortfolio.PortfolioName);

                    result.Add(newPortfolio);
                }

                return result;

            }
            catch (Exception e)
            {
                SendLogMessage(e.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private List<string> GetPortfolios()
        {
            List<string> result = new List<string>();

            List<Portfolio> portfolios = Server.Portfolios;

            if(portfolios == null ||
                portfolios.Count == 0)
            {
                return result;
            }

            for(int i =0;i < portfolios.Count;i++)
            {
                result.Add(portfolios[i].Number);
            }

            return result;
        }

        private List<ComparePositionsSecurity> GetSecuritiesToPortfolio(string portfolioName)
        {
            List<ComparePositionsSecurity> result = new List<ComparePositionsSecurity>();

            List<string> securities = GetSecuritiesWithPositions(portfolioName, Server.ServerNameUnique);

            IServerPermission permission = ServerMaster.GetServerPermission(Server.ServerType);

            if(permission != null)
            {
                string[] ignoreNames = permission.ManuallyClosePositionOnBoard_ExceptionPositionNames;

                for(int i = 0; ignoreNames != null && i < ignoreNames.Length;i++)
                {
                    for(int j = 0;j < securities.Count;j++)
                    {
                        if (securities[j] == ignoreNames[i])
                        {
                            securities.RemoveAt(j);
                            break;
                        }
                    }
                }
            }

            for(int i = 0;i < securities.Count;i++)
            {
                ComparePositionsSecurity newSecurity = CompareSecurity(securities[i], portfolioName);

                if(newSecurity.RobotsLong == 0 &&
                    newSecurity.RobotsShort == 0 &&
                    newSecurity.RobotsCommon == 0 &&
                    newSecurity.PortfolioLong == 0 &&
                    newSecurity.PortfolioShort == 0 &&
                    newSecurity.PortfolioCommon == 0)
                {
                    continue;
                }

                for(int j = 0;j < IgnoredSecurities.Count;j++)
                {
                    if (IgnoredSecurities[j].Security == newSecurity.Security)
                    {
                        newSecurity.IsIgnored = IgnoredSecurities[j].IsIgnored;
                    }
                }

                result.Add(newSecurity);
            }

            return result;
        }

        private ComparePositionsSecurity CompareSecurity(string securityName, string portfolioName)
        {
            ComparePositionsSecurity newSecurity = new ComparePositionsSecurity();
            newSecurity.Security = securityName;

            // 1 считаем позиции по роботам

            List<BotPanel> bots = OsTraderMaster.Master.PanelsArray;

            List<Position> openPositions = new List<Position>();

            for (int i = 0; i < bots.Count; i++)
            {
                List<Position> curPositions = bots[i].OpenPositions;

                if (curPositions == null 
                    || curPositions.Count == 0)
                {
                    continue;
                }

                for (int j = 0; j < curPositions.Count; j++)
                {
                    string pName = curPositions[j].PortfolioName;

                    if(string.IsNullOrEmpty(pName))
                    {
                        continue;
                    }

                    if(curPositions[j].SecurityName != securityName)
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(curPositions[j].ServerName) == false
                        && curPositions[j].ServerName.Contains(Server.ServerNameUnique) == false)
                    {
                        continue;
                    }

                    if (pName == portfolioName
                        || portfolioName.Contains(pName))
                    {
                        openPositions.Add(curPositions[j]);
                    }
                }
            }

            decimal botsLongPoses = 0;
            decimal botsShortPoses = 0;

            for(int i = 0;i < openPositions.Count;i++)
            {
                if (openPositions[i].OpenVolume == 0)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy)
                {
                    botsLongPoses += openPositions[i].OpenVolume;
                }
                else //if (openPositions[i].Direction == Side.Sell)
                {
                    botsShortPoses -= openPositions[i].OpenVolume;
                }
            }

            newSecurity.RobotsLong = botsLongPoses;
            newSecurity.RobotsShort = botsShortPoses;
            newSecurity.RobotsCommon = botsLongPoses - Math.Abs(botsShortPoses);

            // 2 считаем позиции по портфелям

            Portfolio myPortfolio = Server.GetPortfolioForName(portfolioName);

            decimal portfolioLongPoses = 0;
            decimal portfolioShortPoses = 0;
            decimal portfolioCommon = 0;

            if (myPortfolio != null)
            {
                List<PositionOnBoard> marketPositions = myPortfolio.GetPositionOnBoard();

                for (int i = 0; marketPositions != null && i < marketPositions.Count; i++)
                {
                    string curSecurity = marketPositions[i].SecurityNameCode;

                    if(curSecurity == securityName)
                    {
                        portfolioCommon = marketPositions[i].ValueCurrent;
                    }
                    else if(curSecurity.Replace("_SHORT", "") == securityName ||
                        curSecurity.Replace("_Short", "") == securityName)
                    {
                        portfolioShortPoses = marketPositions[i].ValueCurrent;
                    }
                    else if (curSecurity.Replace("_LONG", "") == securityName ||
                        curSecurity.Replace("_Long", "") == securityName)
                    {
                        portfolioLongPoses = marketPositions[i].ValueCurrent;
                    }
                    else if (curSecurity.Replace("_BOTH", "") == securityName ||
                         curSecurity.Replace("_Both", "") == securityName)
                    {
                        portfolioCommon = marketPositions[i].ValueCurrent;
                    }
                }

                newSecurity.PortfolioLong = portfolioLongPoses;
                newSecurity.PortfolioShort = portfolioShortPoses;
                newSecurity.PortfolioCommon = portfolioCommon;
            }

            // 3 сравниваем. Устанавливаем статусы

            if(newSecurity.PortfolioLong != 0
                || newSecurity.PortfolioShort != 0)
            { // биржа имеет внутри режим хеджирования, сверяем позиции учитывая это
                if(newSecurity.PortfolioShort != newSecurity.RobotsShort
                    || newSecurity.PortfolioLong != newSecurity.RobotsLong)
                {
                    newSecurity.Status = ComparePositionsStatus.Error;
                }
            }
            else
            {
                if (newSecurity.PortfolioCommon != newSecurity.RobotsCommon
                    || newSecurity.PortfolioCommon != newSecurity.RobotsCommon)
                {
                    newSecurity.Status = ComparePositionsStatus.Error;
                }
            }

            return newSecurity;
        }

        private List<string> GetSecuritiesWithPositions(string portfolioName, string serverName)
        {
            List<string> result = new List<string>();

            // 1 берём бумаги по которым есть позиции у роботов

            List<Position> botsPositions = GetPositionsInPortfolioByRobots(portfolioName, serverName);

            for(int i = 0;i < botsPositions.Count;i++)
            {
                bool isInArray = false;

                for(int j = 0;j < result.Count;j++)
                {
                    if (result[j] == botsPositions[i].SecurityName)
                    {
                        isInArray = true;
                    }
                }

                if(isInArray == false)
                {
                    result.Add(botsPositions[i].SecurityName);
                }
            }

            // 2 берём бумаги по которым есть позиции на бирже

            Portfolio myPortfolio = Server.GetPortfolioForName(portfolioName);

            if(myPortfolio != null)
            {
                List<PositionOnBoard> marketPositions = myPortfolio.GetPositionOnBoard();

                for (int i = 0; marketPositions != null && i < marketPositions.Count; i++)
                {
                    string curSecurity = marketPositions[i].SecurityNameCode;

                    curSecurity = curSecurity.Replace("_SHORT", "");
                    curSecurity = curSecurity.Replace("_Short", "");
                    curSecurity = curSecurity.Replace("_LONG", "");
                    curSecurity = curSecurity.Replace("_Long", "");
                    curSecurity = curSecurity.Replace("_Both", "");
                    curSecurity = curSecurity.Replace("_BOTH", "");

                    bool isInArray = false;

                    for (int j = 0; j < result.Count; j++)
                    {
                        if (result[j] == curSecurity)
                        {
                            isInArray = true;
                        }
                    }

                    if (isInArray == false)
                    {
                        result.Add(curSecurity);
                    }
                }
            }

            return result;
        }

        private List<Position> GetPositionsInPortfolioByRobots(string portfolioName, string serverName)
        {
            List<BotPanel> bots = OsTraderMaster.Master.PanelsArray;

            List<Position> openPositions = new List<Position>();

            if(bots == null)
            {
                return openPositions;
            }

            for (int i = 0; i < bots.Count; i++)
            {
                if (bots[i] == null)
                {
                    continue;
                }

                List<Position> curPositions = bots[i].OpenPositions;

                if (curPositions == null)
                {
                    continue;
                }

                for (int j = 0; j < curPositions.Count; j++)
                {
                    if (curPositions[j] == null)
                    {
                        continue;
                    }

                    if(string.IsNullOrEmpty(curPositions[j].ServerName) == true)
                    {
                        continue;
                    }

                    if (curPositions[j].ServerName.Contains(serverName) == false)
                    {
                        continue;
                    }

                    string pName = curPositions[j].PortfolioName;

                    if (string.IsNullOrEmpty(pName) == false
                        && 
                        (pName == portfolioName
                        || portfolioName.Contains(pName))
                        )
                    {
                        openPositions.Add(curPositions[j]);
                    }
                }
            }

            return openPositions;
        }

        #region Ignored securities selected by the user

        public List<ComparePositionsSecurity> IgnoredSecurities = new List<ComparePositionsSecurity>();

        public void SaveIgnoredSecurities()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + Server.ServerNameUnique + @"CompareModule_IgnoreSec.txt", false))
                {
                    for (int i = 0; i < IgnoredSecurities.Count; i++)
                    {
                        writer.WriteLine(IgnoredSecurities[i].GetSaveString());
                    }

                    writer.Close();
                }
            }
            catch
            {
                // ignore
            }
        }

        public void LoadIgnoredSecurities()
        {
            if (!File.Exists(@"Engine\" + Server.ServerNameUnique + @"CompareModule_IgnoreSec.txt"))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + Server.ServerNameUnique + @"CompareModule_IgnoreSec.txt"))
                {
                    while (reader.EndOfStream == false)
                    {
                        string sec = reader.ReadLine();

                        if (string.IsNullOrEmpty(sec) == true)
                        {
                            continue;
                        }

                        ComparePositionsSecurity newCompareSecurity = new ComparePositionsSecurity();
                        newCompareSecurity.LoadFromString(sec);
                        IgnoredSecurities.Add(newCompareSecurity);
                    }

                    reader.Close();
                }
            }
            catch
            {
                // ignore
            }
        }

        #endregion

        #region Log

        /// <summary>
        /// add a new message in the log
        /// </summary>
        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent("ComparePositionsModule: " + message, type);
            }
        }

        /// <summary>
        /// outgoing messages for the log event
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }

    public class ComparePositionsPortfolio
    {
        public string PortfolioName;

        public List<ComparePositionsSecurity> CompareSecurities = new List<ComparePositionsSecurity>();

    }

    public class ComparePositionsSecurity
    {
        public ComparePositionsStatus Status;

        public string Security;

        public decimal RobotsLong;

        public decimal RobotsShort;

        public decimal RobotsCommon;

        public decimal PortfolioLong;

        public decimal PortfolioShort;

        public decimal PortfolioCommon;

        public bool IsIgnored;

        public string GetSaveString()
        {
            string result = "";

            result += Security + "%";
            result += IsIgnored + "%";

            return result;
        }

        public void LoadFromString(string str)
        {
            string[] array = str.Split('%');

            Security = array[0];
            IsIgnored = Convert.ToBoolean(array[1]);
        }
    }

    public enum ComparePositionsStatus
    {
        Normal,

        Error
    }

    public enum ComparePositionsVerificationPeriod
    {
        Min5,
        Min1,
        Min10,
        Min30
    }
}