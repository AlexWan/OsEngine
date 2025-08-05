/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OsEngine.OsTrader.Panels.Tab.Internal;

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

                for(int i = 0;i < MasterRobotsNames.Count;i++)
                {
                    if (string.IsNullOrEmpty(MasterRobotsNames[i]))
                    {
                        MasterRobotsNames.RemoveAt(i);
                        i--;
                    }
                }
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

        #region Work thread

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

                    if(IsOn == false)
                    {
                        continue;
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

            MyJournal = new Journal.Journal(name, StartProgram.IsOsTrader);

            // _journal.PositionStateChangeEvent += _journal_PositionStateChangeEvent;
            // _journal.PositionNetVolumeChangeEvent += _journal_PositionNetVolumeChangeEvent;
            // _journal.UserSelectActionEvent += _journal_UserSelectActionEvent;
            MyJournal.LogMessageEvent += SendLogMessage;
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
                    writer.WriteLine(PanelsPosition);
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
                    PanelsPosition = reader.ReadLine();

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
                _isDelete = true;

                if (File.Exists(@"Engine\CopyTrader\" + NameUnique + ".txt"))
                {
                    File.Delete(@"Engine\CopyTrader\" + NameUnique + ".txt");
                }

                if(MyCopyServer != null)
                {
                    MyCopyServer.NewMyTradeEvent -= MyCopyServer_NewMyTradeEvent;
                    MyCopyServer.NewOrderIncomeEvent -= MyCopyServer_NewOrderIncomeEvent;
                    MyCopyServer = null;
                }

                if(MyJournal != null)
                {
                    MyJournal.Clear();
                    MyJournal.Delete();
                    MyJournal = null;
                }
            }
            catch
            {
                // ignore
            }
        }

        private bool _isDelete;

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

        public CopyTraderCopyType CopyType;

        public CopyTraderVolumeType VolumeType;

        public decimal VolumeMult = 1;

        public string MasterAsset = "Prime";

        public string SlaveAsset = "Prime";

        public CopyTraderOrdersType OrderType = CopyTraderOrdersType.Market;

        public int IcebergCount = 2;

        public string PanelsPosition = "1,1";

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

        private void ModifySecurityPositionToCopy(PositionToCopy positionToCopy)
        {
            for(int i = 0;i < SecurityToCopy.Count;i++)
            {
                if (SecurityToCopy[i].MasterSecurityName == positionToCopy.SecurityNameMaster)
                {
                    positionToCopy.SlaveSecurityName= SecurityToCopy[i].SlaveSecurityName;
                    positionToCopy.SlaveSecurityClass = SecurityToCopy[i].SlaveSecurityClass;
                    return;
                }
            }

            positionToCopy.SlaveSecurityName = positionToCopy.SecurityNameMaster;
            
        }

        #endregion

        #region Server and Journal 

        public Journal.Journal MyJournal;

        public AServer MyCopyServer;

        private PositionCreator _dealCreator = new PositionCreator();

        private void TryGetServer()
        {
            List<AServer> servers = ServerMaster.GetAServers();

            if (servers == null
                || servers.Count == 0)
            {
                return;
            }

            for (int i = 0; i < servers.Count; i++)
            {
                if (servers[i].ServerNameAndPrefix.StartsWith(this.ServerName))
                {
                    MyCopyServer = servers[i];

                    MyCopyServer.NewMyTradeEvent += MyCopyServer_NewMyTradeEvent;
                    MyCopyServer.NewOrderIncomeEvent += MyCopyServer_NewOrderIncomeEvent;

                    break;
                }
            }
        }

        private void MyCopyServer_NewOrderIncomeEvent(Order order)
        {
            try
            {
                if (_isDelete)
                {
                    return;
                }

                Order orderInJournal = MyJournal.IsMyOrder(order);

                if (orderInJournal == null)
                {
                    return;
                }
                MyJournal.SetNewOrder(order);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(),LogMessageType.Error);
            }
        }

        private void MyCopyServer_NewMyTradeEvent(MyTrade myTrade)
        {
            try
            {
                if (_isDelete)
                {
                    return;
                }
                if (MyJournal.SetNewMyTrade(myTrade) == false)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
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

            // 1 пробуем взять коннектор

            if(MyCopyServer == null)
            {
                TryGetServer();
                if (MyCopyServer == null)
                {
                    return;
                }
            }

            if(MyCopyServer.ServerStatus != ServerConnectStatus.Connect)
            {
                return;
            }

            if(MyCopyServer.LastStartServerTime.AddSeconds(MyCopyServer.WaitTimeToTradeAfterFirstStart) 
                > DateTime.Now)
            {
                return;
            }

            ProcessCopyRobots(masterRobots);
        }

        #endregion

        #region Robots copy logic

        private void ProcessCopyRobots(List<string> masterRobots)
        {
            // 1 обновляем список роботов

            TrySyncRobots(masterRobots);

            if (Robots == null
                || Robots.Count == 0)
            {
                TryCloseMyPositionsRobots();
                return;
            }

            // 2 берём позиции по мастер-роботам

            List<PositionToCopy> copyPositions = GetPositionsFromBots();

            // 3 берём позиции из журнала копировщика. Сортируем

            List<Position> positionsFromCopyTrader = MyJournal.OpenPositions;

            List<Position> positionsToClose = new List<Position>();

            for(int i = 0;i < positionsFromCopyTrader.Count;i++)
            {
                Position position = positionsFromCopyTrader[i];

                bool isInArray = false;

                for(int j = 0;j < copyPositions.Count;j++)
                {
                    if(position.SecurityName == copyPositions[j].SlaveSecurityName)
                    {
                        isInArray = true;
                        copyPositions[j].SetPositionCopyJournal(position);
                        break;
                    }

                }

                if (isInArray == false)
                {
                    positionsToClose.Add(position);
                }
            }

            // 4 закрываем позиции по инструментам которые были закрыты у робота

            for(int i = 0;i < positionsToClose.Count;i++)
            {
                ClosePosition(positionsToClose[i], positionsToClose[i].OpenVolume);
            }

            // 5 открываем новые позиции если есть расбалансировка

            for(int i = 0;i < copyPositions.Count;i++)
            {
                PositionToCopy positionToCopy = copyPositions[i];

                bool haveActiveOrders = false;

                for(int j = 0; j < positionToCopy.PositionsCopyJournal.Count;j++)
                {
                    if (positionToCopy.PositionsCopyJournal[j].OpenActive == true 
                        || positionToCopy.PositionsCopyJournal [j].CloseActive == true)
                    {
                        haveActiveOrders = true;
                        break;
                    }
                }

                if(haveActiveOrders == true)
                {
                    continue;
                }

                if(CopyType == CopyTraderCopyType.Absolute)
                {
                    TrySyncPositionToCopyRobotsAbsMode(copyPositions[i]);
                }
                else if(CopyType == CopyTraderCopyType.FullCopy)
                {
                    TrySyncPositionToCopyRobotsFullMode(copyPositions[i]);
                }
            }
        }

        private void TrySyncPositionToCopyRobotsAbsMode(PositionToCopy positionToCopy)
        {
            // 1 уже синхронизировано. Ничего не делаем
            if (positionToCopy.RobotVolumeAbs == positionToCopy.SlaveVolumeAbs)
            {
                return;
            }

            // 2 мастер в нулевом положении. Закрываем все позиции по копи-журналу

            else if(positionToCopy.RobotVolumeAbs == 0)
            {
                for(int i = 0;i < positionToCopy.PositionsCopyJournal.Count;i++)
                {
                    ClosePosition(positionToCopy.PositionsCopyJournal[i], positionToCopy.PositionsCopyJournal[i].OpenVolume);
                }
            }

            // 3 надо докупать либо закрывать шорты

            else if(positionToCopy.RobotVolumeAbs > positionToCopy.SlaveVolumeAbs)
            {
                decimal difference = positionToCopy.RobotVolumeAbs - positionToCopy.SlaveVolumeAbs;

                // 3.1. пытаемся закрыть шорты 

                for(int i = 0;i < positionToCopy.PositionsCopyJournal.Count; i++)
                {
                    Position curPosition = positionToCopy.PositionsCopyJournal[i];

                    if(curPosition.Direction == Side.Sell)
                    {
                        if(difference > curPosition.OpenVolume)
                        {
                            ClosePosition(curPosition, curPosition.OpenVolume);
                        }
                        else
                        {
                            ClosePosition(curPosition, difference);
                        }

                        return;
                    }
                }

                // 3.2. открываем лонги

                BuyAtMarket(difference, positionToCopy.SlaveSecurityName);
            }

            // 4 надо продавать либо закрывать лонги

            else if(positionToCopy.RobotVolumeAbs < positionToCopy.SlaveVolumeAbs)
            {
                decimal difference = positionToCopy.SlaveVolumeAbs - positionToCopy.RobotVolumeAbs;

                // 4.1. пытаемся закрыть лонги

                for (int i = 0; i < positionToCopy.PositionsCopyJournal.Count; i++)
                {
                    Position curPosition = positionToCopy.PositionsCopyJournal[i];

                    if (curPosition.Direction == Side.Buy)
                    {
                        if (difference > curPosition.OpenVolume)
                        {
                            ClosePosition(curPosition, curPosition.OpenVolume);
                        }
                        else
                        {
                            ClosePosition(curPosition, difference);
                        }

                        return;
                    }
                }

                // 4.2. открываем шорты

                SellAtMarket(difference, positionToCopy.SlaveSecurityName);
            }
        }

        private void TrySyncPositionToCopyRobotsFullMode(PositionToCopy positionToCopy)
        {
            // 1 уже синхронизировано. Ничего не делаем
           /* if (positionToCopy.RobotVolumeAbs == positionToCopy.SlaveVolumeAbs)
            {
                return;
            }

            // 2 мастер в нулевом положении. Закрываем все позиции по копи-журналу

            else if (positionToCopy.RobotVolumeAbs == 0)
            {
                for (int i = 0; i < positionToCopy.PositionsCopyJournal.Count; i++)
                {
                    ClosePositions(positionToCopy.PositionsCopyJournal[i]);
                }
            }

            // 3 надо докупать либо закрывать шорты

            else if (positionToCopy.RobotVolumeAbs > positionToCopy.SlaveVolumeAbs)
            {




            }

            // 4 надо продавать либо закрывать лонги

            else if (positionToCopy.RobotVolumeAbs < positionToCopy.SlaveVolumeAbs)
            {




            }*/
        }

        private List<PositionToCopy> GetPositionsFromBots()
        {
            List<Position> positionsInRobots = new List<Position>();

            for(int i = 0;i< Robots.Count; i++)
            {
                List<Position> openPositions = Robots[i].OpenPositions;

                if(openPositions != null 
                    && openPositions.Count > 0)
                {
                    positionsInRobots.AddRange(openPositions);
                }
            }

            List<PositionToCopy> resultPositions = new List<PositionToCopy>();

            for(int i = 0; i < positionsInRobots.Count; i++)
            {
                Position currenPos = positionsInRobots[i];

                bool isInArray = false;

                for(int j = 0;j < resultPositions.Count;j++)
                {
                    if (resultPositions[j].SecurityNameMaster == currenPos.SecurityName)
                    {
                        isInArray = true;
                        resultPositions[j].SetPositionRobot(currenPos);
                        break;
                    }
                }

                if(isInArray == false)
                {
                    PositionToCopy positionToCopy = new PositionToCopy();
                    positionToCopy.SecurityNameMaster = currenPos.SecurityName.Replace(" TestPaper","");
                    positionToCopy.SetPositionRobot(currenPos);
                    ModifySecurityPositionToCopy(positionToCopy);
                    resultPositions.Add(positionToCopy);
                }
            }

            return resultPositions;
        }

        public List<BotPanel> Robots = new List<BotPanel>();

        private void TrySyncRobots(List<string> robotsNames)
        {
            // 1 создаём не достающие

            List<BotPanel> allRobots = ServerMaster.GetAllBotsFromBotStation();

            for (int i = 0; i < robotsNames.Count; i++)
            {
                string currentBot = robotsNames[i];

                bool botInArray = false;

                for (int j = 0; j < Robots.Count; j++)
                {
                    if (Robots[j].NameStrategyUniq == currentBot)
                    {
                        botInArray = true;
                        break;
                    }
                }

                if (botInArray == false)
                {
                    BotPanel bot = allRobots.Find(b => b.NameStrategyUniq == currentBot);

                    if (bot != null)
                    {
                        Robots.Add(bot);
                    }
                    else
                    {
                        robotsNames.RemoveAt(i);
                        i--;
                    }
                }
            }

            // 2 убираем лишние

            for (int i = 0; i < Robots.Count; i++)
            {
                bool isInArray = false;

                for (int j = 0; j < robotsNames.Count; j++)
                {
                    if (robotsNames[j] == Robots[i].NameStrategyUniq)
                    {
                        isInArray = true;
                        break;
                    }
                }

                if (isInArray == false)
                {
                    Robots.RemoveAt(i);
                    i--;
                }
            }
        }

        private void TryCloseMyPositionsRobots()
        {
            List<Position> positionsFromCopyTrader = MyJournal.OpenPositions;

            for(int i =0;i < positionsFromCopyTrader.Count;i++)
            {
                if(MyCopyServer.ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }
                ClosePosition(positionsFromCopyTrader[i], positionsFromCopyTrader[i].OpenVolume);
            }
        }

        #endregion

        #region Trade operations

        public Position BuyAtMarket(decimal volume , string securityName)
        {
            try
            {
                Side direction = Side.Buy;
                OrderPriceType priceType = OrderPriceType.Market;

                Security security = GetSecurityByName(securityName);

                if(security == null)
                {
                    return null;
                }

                Portfolio portfolio = GetPortfolioByName(this.PortfolioName);

                if(portfolio == null)
                {
                    return null;
                }

                decimal price = 0;

                OrderTypeTime orderTypeTime = OrderTypeTime.GTC;

                TimeSpan timeLife = TimeSpan.FromSeconds(60);

                Position newDeal = _dealCreator.CreatePosition(this.NameUnique, direction, price, volume, priceType,
                    timeLife, security, portfolio, StartProgram.IsOsTrader, orderTypeTime);

                MyJournal.SetNewDeal(newDeal);

                MyCopyServer.ExecuteOrder(newDeal.OpenOrders[0]);

                return newDeal;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private Position SellAtMarket(decimal volume, string securityName)
        {
            try
            {
                Side direction = Side.Buy;
                OrderPriceType priceType = OrderPriceType.Market;

                Security security = GetSecurityByName(securityName);

                if (security == null)
                {
                    return null;
                }

                Portfolio portfolio = GetPortfolioByName(this.PortfolioName);

                if (portfolio == null)
                {
                    return null;
                }

                decimal price = 0;

                OrderTypeTime orderTypeTime = OrderTypeTime.GTC;

                TimeSpan timeLife = TimeSpan.FromSeconds(60);

                Position newDeal = _dealCreator.CreatePosition(this.NameUnique, direction, price, volume, priceType,
                    timeLife, security, portfolio, StartProgram.IsOsTrader, orderTypeTime);

                MyJournal.SetNewDeal(newDeal);

                MyCopyServer.ExecuteOrder(newDeal.OpenOrders[0]);

                return newDeal;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private void ClosePosition(Position position, decimal volume)
        {
            try
            {
                if (position == null)
                {
                    return;
                }

                Security security = GetSecurityByName(position.SecurityName);

                if (security == null)
                {
                    return;
                }

                if (position.State == PositionStateType.Done &&
                    position.OpenVolume == 0)
                {
                    return;
                }

                position.State = PositionStateType.Closing;

                OrderPriceType priceType = OrderPriceType.Market;

                TimeSpan lifeTime = TimeSpan.FromSeconds(60);

                OrderTypeTime orderTypeTime = OrderTypeTime.GTC;

                Order closeOrder = _dealCreator.CreateCloseOrderForDeal(security, position, 0,
                    priceType, lifeTime, StartProgram.IsOsTrader,
                    orderTypeTime, MyCopyServer.ServerNameUnique);

                if(closeOrder == null)
                {
                    return;
                }

                closeOrder.PortfolioNumber = position.PortfolioName;
                closeOrder.SecurityNameCode = security.Name;
                closeOrder.SecurityClassCode = security.NameClass;

                if (closeOrder == null)
                {
                    if (position.OpenVolume == 0)
                    {
                        position.State = PositionStateType.OpeningFail;
                    }

                    return;
                }

                position.AddNewCloseOrder(closeOrder);

                MyCopyServer.ExecuteOrder(closeOrder);
                
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private Security GetSecurityByName(string name)
        {
            List<Security> securityList = MyCopyServer.Securities;

            for(int i = 0;i < securityList.Count;i++)
            {
                if (securityList[i].Name == name)
                {
                    return securityList[i];
                }
            }

            return null;
        }

        private Portfolio GetPortfolioByName(string name)
        {
            List<Portfolio> portfoliosList = MyCopyServer.Portfolios;

            for (int i = 0; i < portfoliosList.Count; i++)
            {
                if (portfoliosList[i].Number == name)
                {
                    return portfoliosList[i];
                }
            }

            return null;
        }


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

    public enum CopyTraderCopyType
    {
        FullCopy,
        Absolute
    }
    
    public enum CopyTraderVolumeType
    {
        QtyMultiplicator,
        DepoProportional
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

    public class PositionToCopy
    {
        public void SetPositionRobot(Position pos)
        {
            PositionsRobots.Add(pos);

            if(pos.OpenVolume == 0)
            {
                return;
            }

            if(pos.Direction == Side.Buy)
            {
                RobotVolumeBuy += pos.OpenVolume;
                RobotVolumeAbs += pos.OpenVolume;
            }
            else
            {
                RobotVolumeSell -= pos.OpenVolume;
                RobotVolumeAbs -= pos.OpenVolume;
            }
        }

        public List<Position> PositionsRobots = new List<Position>();

        public void SetPositionCopyJournal(Position pos)
        {
            PositionsCopyJournal.Add(pos);

            if (pos.OpenVolume == 0)
            {
                return;
            }

            if (pos.Direction == Side.Buy)
            {
                SlaveVolumeBuy += pos.OpenVolume;
                SlaveVolumeAbs += pos.OpenVolume;
            }
            else
            {
                SlaveVolumeSell -= pos.OpenVolume;
                SlaveVolumeAbs -= pos.OpenVolume;
            }
        }

        public List<Position> PositionsCopyJournal = new List<Position>();

        public string SecurityNameMaster;

        public string SlaveSecurityName;

        public string SlaveSecurityClass;

        public decimal RobotVolumeBuy = 0;

        public decimal RobotVolumeSell = 0;

        public decimal RobotVolumeAbs = 0;

        public decimal SlaveVolumeBuy = 0;

        public decimal SlaveVolumeSell = 0;

        public decimal SlaveVolumeAbs = 0;
    }
}
