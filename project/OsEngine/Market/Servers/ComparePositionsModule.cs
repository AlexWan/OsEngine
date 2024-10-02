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

            Thread worker = new Thread(UpdaterThreadWorker);
            worker.Start();
        }

        public AServer Server;

        public bool AutoLogMessageOnError
        {
            get
            {
                return _autoLogMessageOnError;
            }
            set
            {
                if(_autoLogMessageOnError == value)
                {
                    return;
                }

                _autoLogMessageOnError = value;
                Save();
            }
        }
        private bool _autoLogMessageOnError;

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

        private void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + Server.ServerType.ToString() + @"CompareModule.txt", false))
                {
                    writer.WriteLine(_autoLogMessageOnError);
                    writer.WriteLine(_verificationPeriod);

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
            if (!File.Exists(@"Engine\" + Server.ServerType.ToString() + @"CompareModule.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + Server.ServerType.ToString() + @"CompareModule.txt"))
                {
                    _autoLogMessageOnError = Convert.ToBoolean(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), out _verificationPeriod);

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

                    if(_autoLogMessageOnError == false)
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

                    List<ComparePositionsPortfolio> portfolios = UpdateCompareData();

                    for(int i = 0; portfolios != null && i < portfolios.Count; i++)
                    {
                        CheckPortfolio(portfolios[i]);
                    }

                }
                catch (Exception e)
                {
                    SendLogMessage(e.ToString(),LogMessageType.Error);
                    Thread.Sleep(10000);
                }
            }
        }

        private void CheckPortfolio(ComparePositionsPortfolio portfolio)
        {
            if(portfolio == null)
            {
                return;
            }

            if(portfolio.CompareSecurities == null 
                || portfolio.CompareSecurities .Count == 0)
            {
                return;
            }

            string message = Server.ServerType + ". Error on compare securities in robot and portfolio \n";
            bool haveError = false;

            for(int i = 0;i <  portfolio.CompareSecurities.Count;i++)
            {
                ComparePositionsSecurity security = portfolio.CompareSecurities[i];

                if(security.Status == ComparePositionsStatus.Normal)
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
                SendLogMessage(message, LogMessageType.Error);
            }
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

            List<string> securities = GetSecuritiesWithPositions(portfolioName);

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

                if (curPositions == null)
                {
                    continue;
                }

                for (int j = 0; j < curPositions.Count; j++)
                {
                    string pName = curPositions[j].PortfolioName;

                    if (pName != null
                        && pName == portfolioName
                        && curPositions[j].SecurityName == securityName)
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

        private List<string> GetSecuritiesWithPositions(string portfolioName)
        {
            List<string> result = new List<string>();

            // 1 берём бумаги по которым есть позиции у роботов

            List<Position> botsPositions = GetPositionsInPortfolioByRobots(portfolioName);

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

        private List<Position> GetPositionsInPortfolioByRobots(string portfolioName)
        {
            List<BotPanel> bots = OsTraderMaster.Master.PanelsArray;

            List<Position> openPositions = new List<Position>();

            for (int i = 0; i < bots.Count; i++)
            {
                List<Position> curPositions = bots[i].OpenPositions;

                if (curPositions == null)
                {
                    continue;
                }

                for (int j = 0; j < curPositions.Count; j++)
                {
                    string pName = curPositions[j].PortfolioName;

                    if (pName != null
                        && pName == portfolioName)
                    {
                        openPositions.Add(curPositions[j]);
                    }
                }
            }

            return openPositions;
        }

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