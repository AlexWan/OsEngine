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
using System.Threading;

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

            SendLogMessage("CopyTrader Activate. Name: " + Number + " " + Name, LogMessageType.System);

            LoadPortfolios(save[5]);

            Task.Run(WorkThreadArea);
        }

        public CopyTrader(int number)
        {
            Number = number;

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
            _objectIsDelete = true;

            if (DeleteEvent != null)
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

                    if(MainWindow.ProccesIsWorked == false)
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

            LogCopyTrader = new Log("CopyPortfolio" + name, Entity.StartProgram.IsOsTrader);
            LogCopyTrader.Listen(this);

            Load();

            MyJournal = new Journal.Journal(name, StartProgram.IsOsTrader);
            MyJournal.LogMessageEvent += SendLogMessage;
            MyJournal.CanShowToolStripMenu = false;

            TradePeriodsSettings = new NonTradePeriods(name);

            SendLogMessage("Copy Portfolio Activate.", LogMessageType.System);
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
                    writer.WriteLine(OrderType);
                    writer.WriteLine(IcebergCount);
                    writer.WriteLine(GetSecuritiesSaveString());
                    writer.WriteLine(PanelsPosition);
                    writer.WriteLine(MinCurrencyQty);
                    writer.WriteLine(FailOpenOrdersReactionIsOn);
                    writer.WriteLine(FailOpenOrdersCountToReaction);
                    writer.WriteLine(IcebergMillisecondsDelay);
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
                    
                    Enum.TryParse(reader.ReadLine(), out OrderType);
                    IcebergCount = Convert.ToInt32(reader.ReadLine());
                    LoadSecuritiesFromString(reader.ReadLine());
                    PanelsPosition = reader.ReadLine();
                    MinCurrencyQty = reader.ReadLine().ToDecimal();

                    FailOpenOrdersReactionIsOn = Convert.ToBoolean(reader.ReadLine());
                    FailOpenOrdersCountToReaction = Convert.ToInt32(reader.ReadLine());
                    IcebergMillisecondsDelay = Convert.ToInt32(reader.ReadLine());

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
                IsOn = false;
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

        public CopyTraderVolumeType VolumeType;

        public decimal VolumeMult = 1;

        public string MasterAsset = "Prime";

        public string SlaveAsset = "Prime";

        public CopyTraderOrdersType OrderType = CopyTraderOrdersType.Market;

        public int IcebergCount = 2;

        public int IcebergMillisecondsDelay = 2000;

        public string PanelsPosition = "1,1,1";

        public decimal MinCurrencyQty = 25;

        public bool FailOpenOrdersReactionIsOn = true;

        public int FailOpenOrdersCountToReaction = 10;

        public int FailOpenOrdersCountFact;

        public NonTradePeriods TradePeriodsSettings;

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

            if(positionToCopy.SlaveSecurityName == null)
            {
                positionToCopy.SlaveSecurityName = positionToCopy.SecurityNameMaster;
            }
         
            

        }

        #endregion

        #region Server and Journal 

        public Journal.Journal MyJournal;

        public AServer MyCopyServer;

        public decimal MasterAssetValue;

        public decimal SlaveAssetValue;

        private void TryGetAssets()
        {
            // 1 ищем мастер счёт

            decimal masterAsset = 0;

            Portfolio portfolioMaster = null;

            for(int i = 0;i < Robots.Count;i++)
            {
                Portfolio firstPortfolio = Robots[i].GetFirstPortfolio();
                
                if(firstPortfolio != null)
                {
                    portfolioMaster = firstPortfolio;
                    break;
                }
            }

            if(portfolioMaster != null)
            {
                if(MasterAsset == "Prime")
                {
                    masterAsset = portfolioMaster.ValueCurrent;
                }
                else
                {
                    List<PositionOnBoard> assets = portfolioMaster.GetPositionOnBoard();

                    for (int i = 0;i < assets.Count;i++)
                    {
                        if(assets[i].SecurityNameCode == MasterAsset)
                        {
                            masterAsset = assets[i].ValueCurrent;
                            break;
                        }
                    }
                }
            }

            MasterAssetValue = masterAsset;

            // копи-счёт

            decimal slaveAsset = 0;

            Portfolio portfolioSlave = null;

            for (int i = 0; MyCopyServer.Portfolios != null && i < MyCopyServer.Portfolios.Count; i++)
            {
                Portfolio currentPortfolio = MyCopyServer.Portfolios[i];

                if(currentPortfolio.Number == PortfolioName)
                {
                    portfolioSlave = currentPortfolio;
                    break;
                }

            }

            if (portfolioSlave != null)
            {
                if (SlaveAsset == "Prime")
                {
                    slaveAsset = portfolioSlave.ValueCurrent;
                }
                else
                {
                    List<PositionOnBoard> assets = portfolioSlave.GetPositionOnBoard();

                    for (int i = 0; i < assets.Count; i++)
                    {
                        if (assets[i].SecurityNameCode == SlaveAsset)
                        {
                            slaveAsset = assets[i].ValueCurrent;
                            break;
                        }
                    }
                }
            }

            SlaveAssetValue = slaveAsset;
        }

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
                    
                    SendLogMessage("Server connected.", LogMessageType.System);

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

                if(order.State == OrderStateType.Fail)
                {
                    SendLogMessage("Order fail state!!! \n" 
                        + "Number: " + order.NumberUser + "\n"
                        + "Sec name: " + order.SecurityNameCode, LogMessageType.Error);

                    FailOpenOrdersCountFact++;

                    if (IsOn == true 
                        && FailOpenOrdersReactionIsOn == true
                        && FailOpenOrdersCountFact >= FailOpenOrdersCountToReaction)
                    {
                        SendLogMessage("STOP WORK MODULE!!! To much orders error \n", LogMessageType.Error);
                        IsOn = false;
                        Save();
                    }
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
            if(_isDelete == true)
            {
                return;
            }

            if(IsOn == false)
            {
                if (FailOpenOrdersCountFact != 0)
                {
                    FailOpenOrdersCountFact = 0;
                }
                return;
            }

            if(masterRobots == null ||
                masterRobots.Count == 0)
            {
                return;
            }

            if(_timeNoTrade > DateTime.Now)
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

            DateTime copyServerTime = MyCopyServer.ServerTime;

            if(copyServerTime == DateTime.MinValue)
            {
                copyServerTime = DateTime.Now;
            }

            if (TradePeriodsSettings.CanTradeThisTime(copyServerTime) == false)
            {
                return;
            }

            // 2 обновляем список роботов

            TrySyncRobots(masterRobots);

            if (Robots == null
                || Robots.Count == 0)
            {
                TryCloseMyPositionsRobots();
                return;
            }

            // 3 обновляем состояние портфелей

            TryGetAssets();

            if (VolumeType == CopyTraderVolumeType.DepoProportional)
            {
                if(MasterAssetValue == 0
                    || SlaveAssetValue == 0)
                {
                    return;
                }
            }

            // 4 идём в торговую логику

            ProcessCopyRobots();
        }

        private DateTime _timeNoTrade;

        private void ProcessCopyRobots()
        {

            // 1 берём позиции по мастер-роботам

            List<PositionToCopy> copyPositions = GetPositionsFromBots();

            // 2 берём позиции из журнала копировщика. 

            List<Position> positionsFromCopyTrader = MyJournal.OpenPositions;

            // 3 Если в рынке есть ордера - ждём пока исполнятся

            for (int i = 0; i < positionsFromCopyTrader.Count; i++)
            {
                Position position = positionsFromCopyTrader[i];

                if(position.OpenActive == true 
                    || position.CloseActive == true)
                {
                    return;
                }
            }

            // 4 ищем позиции копировщика, которые уже закрылись в роботах

            bool haveLostPositions = false;

            for (int i = 0; i < positionsFromCopyTrader.Count; i++)
            {
                Position position = positionsFromCopyTrader[i];

                bool isInArray = false;

                for (int j = 0; j < copyPositions.Count; j++)
                {
                    if (position.SecurityName == copyPositions[j].SlaveSecurityName)
                    {
                        isInArray = true;
                        copyPositions[j].SetPositionCopyJournal(position);
                        break;
                    }
                }

                if (isInArray == false)
                {
                    // 5 закрываем позиции по инструментам которые были закрыты у робота

                    if (OrderType == CopyTraderOrdersType.Market || IcebergCount <= 1)
                    {
                        CloseAtMarket(position, position.OpenVolume);
                    }
                    else
                    {
                        CloseAtMarketIceberg(position, position.OpenVolume, IcebergCount, null);
                    }
                    _timeNoTrade = DateTime.Now.AddSeconds(5);
                    haveLostPositions = true;
                }
            }

            if(haveLostPositions == true)
            {
                return;
            }

            // 6 открываем новые позиции если есть расбалансировка

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

                TrySyncPositionToCopyRobotsAbsMode(copyPositions[i]);
            }
        }

        private void TrySyncPositionToCopyRobotsAbsMode(PositionToCopy positionToCopy)
        {
            // 1 уже синхронизировано. Ничего не делаем
            if (positionToCopy.RobotVolumeAbs == positionToCopy.SlaveVolumeAbs)
            {
                return;
            }

            Security security = GetSecurityByName(positionToCopy.SlaveSecurityName);

            if (security == null)
            {
                return;
            }

            // 2 мастер в нулевом положении. Закрываем все позиции по копи-журналу

            else if(positionToCopy.RobotVolumeAbs == 0)
            {
                for(int i = 0;i < positionToCopy.PositionsCopyJournal.Count;i++)
                {
                    if (OrderType == CopyTraderOrdersType.Market || IcebergCount <= 1)
                    {
                        CloseAtMarket(positionToCopy.PositionsCopyJournal[i], 
                            positionToCopy.PositionsCopyJournal[i].OpenVolume);
                    }
                    else
                    {
                        CloseAtMarketIceberg(positionToCopy.PositionsCopyJournal[i], 
                            positionToCopy.PositionsCopyJournal[i].OpenVolume, IcebergCount, positionToCopy);
                    }
                }
            }

            // 3 надо докупать либо закрывать шорты

            else if(positionToCopy.RobotVolumeAbs > positionToCopy.SlaveVolumeAbs)
            {
                decimal difference = positionToCopy.RobotVolumeAbs - positionToCopy.SlaveVolumeAbs;

                difference = Math.Round(difference, security.DecimalsVolume);

                if (difference <= 0)
                {
                    return;
                }

                if(MinCurrencyQty != 0 &&
                    CheckMinCurrencyQty(difference,positionToCopy) == false)
                {
                    return;
                }

                // 3.1. пытаемся закрыть шорты 

                for (int i = 0;i < positionToCopy.PositionsCopyJournal.Count; i++)
                {
                    Position curPosition = positionToCopy.PositionsCopyJournal[i];

                    if(curPosition.Direction == Side.Sell)
                    {
                        if(difference > curPosition.OpenVolume)
                        {
                            if (OrderType == CopyTraderOrdersType.Market || IcebergCount <= 1)
                            {
                                CloseAtMarket(curPosition, curPosition.OpenVolume);
                            }
                            else
                            {
                                CloseAtMarketIceberg(curPosition, curPosition.OpenVolume, IcebergCount, positionToCopy);
                            }
                        }
                        else
                        {
                            if (OrderType == CopyTraderOrdersType.Market || IcebergCount <= 1)
                            {
                                CloseAtMarket(curPosition, difference);
                            }
                            else
                            {
                                CloseAtMarketIceberg(curPosition, difference, IcebergCount, positionToCopy);
                            }
                        }
                        _timeNoTrade = DateTime.Now.AddSeconds(5);
                        return;
                    }
                }

                // 3.2. открываем лонги

                if(OrderType == CopyTraderOrdersType.Market || IcebergCount <= 1)
                {
                    BuyAtMarket(difference, security, null);
                }
                else
                {
                    BuyAtMarketIceberg(difference, security, IcebergCount, positionToCopy);
                }

                _timeNoTrade = DateTime.Now.AddSeconds(5);
            }

            // 4 надо продавать либо закрывать лонги

            else if(positionToCopy.RobotVolumeAbs < positionToCopy.SlaveVolumeAbs)
            {
                decimal difference = positionToCopy.SlaveVolumeAbs - positionToCopy.RobotVolumeAbs;

                difference = Math.Round(difference, security.DecimalsVolume);

                if(difference <= 0)
                {
                    return;
                }

                if (MinCurrencyQty != 0 && 
                    CheckMinCurrencyQty(difference, positionToCopy) == false)
                {
                    return;
                }

                // 4.1. пытаемся закрыть лонги

                for (int i = 0; i < positionToCopy.PositionsCopyJournal.Count; i++)
                {
                    Position curPosition = positionToCopy.PositionsCopyJournal[i];

                    if (curPosition.Direction == Side.Buy)
                    {
                        if (difference > curPosition.OpenVolume)
                        {
                            if (OrderType == CopyTraderOrdersType.Market || IcebergCount <= 1)
                            {
                                CloseAtMarket(curPosition, curPosition.OpenVolume);
                            }
                            else
                            {
                                CloseAtMarketIceberg(curPosition, curPosition.OpenVolume, IcebergCount, positionToCopy);
                            }
                        }
                        else
                        {
                            if (OrderType == CopyTraderOrdersType.Market || IcebergCount <= 1)
                            {
                                CloseAtMarket(curPosition, difference);
                            }
                            else
                            {
                                CloseAtMarketIceberg(curPosition, difference, IcebergCount, positionToCopy);
                            }
                        }
                        _timeNoTrade = DateTime.Now.AddSeconds(5);
                        return;
                    }
                }

                // 4.2. открываем шорты

                if (OrderType == CopyTraderOrdersType.Market || IcebergCount <= 1)
                {
                    SellAtMarket(difference, security, null);
                }
                else
                {
                    SellAtMarketIceberg(difference, security, IcebergCount, positionToCopy);
                }

                _timeNoTrade = DateTime.Now.AddSeconds(5);
            }
        }

        private bool CheckMinCurrencyQty(decimal qty, PositionToCopy positionToCopy)
        {
            // false - если нельзя открывать по такому объёму позицию

            decimal price = 0;

            for(int i = 0;i < positionToCopy.PositionsRobots.Count;i++)
            {
                Position pos = positionToCopy.PositionsRobots[i];

                if(pos.EntryPrice != 0)
                {
                    price = pos.EntryPrice;
                }
            }

            if (price == 0)
            {
                for (int i = 0; i < positionToCopy.PositionsCopyJournal.Count; i++)
                {
                    Position pos = positionToCopy.PositionsCopyJournal[i];

                    if (pos.EntryPrice != 0)
                    {
                        price = pos.EntryPrice;
                    }
                }
            }

            if(price != 0)
            {
                decimal currencyOnQty = qty * price;

                if(currencyOnQty < MinCurrencyQty * 1.15m)
                {
                    return false;
                }
            }

            return true;
        }

        private decimal GetPriceByPositionToCopy(PositionToCopy positionToCopy)
        {
            decimal price = 0;

            for (int i = 0; i < positionToCopy.PositionsRobots.Count; i++)
            {
                Position pos = positionToCopy.PositionsRobots[i];

                if (pos.EntryPrice != 0)
                {
                    price = pos.EntryPrice;
                }
            }

            if (price == 0)
            {
                for (int i = 0; i < positionToCopy.PositionsCopyJournal.Count; i++)
                {
                    Position pos = positionToCopy.PositionsCopyJournal[i];

                    if (pos.EntryPrice != 0)
                    {
                        price = pos.EntryPrice;
                    }
                }
            }

            return price;
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

                string sec = currenPos.SecurityName.Replace(" TestPaper", "");

                for (int j = 0;j < resultPositions.Count;j++)
                {
                    if (resultPositions[j].SecurityNameMaster == sec)
                    {
                        isInArray = true;
                        resultPositions[j].SetPositionRobot(
                            currenPos, this.VolumeType, VolumeMult, MasterAssetValue,SlaveAssetValue);
                        break;
                    }
                }

                if(isInArray == false)
                {
                    PositionToCopy positionToCopy = new PositionToCopy();
                    positionToCopy.SecurityNameMaster = sec;
                    positionToCopy.SetPositionRobot(
                        currenPos, this.VolumeType, VolumeMult, MasterAssetValue, SlaveAssetValue);
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

                if (botInArray == false
                    && allRobots != null)
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

        public void TryCloseMyPositionsRobots()
        {
            try
            {
                Position[] positionsFromCopyTrader = MyJournal.OpenPositions.ToArray();

                for (int i = 0; i < positionsFromCopyTrader.Length; i++)
                {
                    if (MyCopyServer.ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        return;
                    }

                    if (OrderType == CopyTraderOrdersType.Market || IcebergCount <= 1)
                    {
                        CloseAtMarket(positionsFromCopyTrader[i], positionsFromCopyTrader[i].OpenVolume);
                    }
                    else
                    {
                        CloseAtMarketIceberg(positionsFromCopyTrader[i], positionsFromCopyTrader[i].OpenVolume, IcebergCount, null);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Trade operations

        private Position BuyAtMarketIceberg(decimal volume, Security security, int ordersCount, PositionToCopy positionToCopy)
        {
            List<decimal> volumes = GetVolumesArray(volume, ordersCount, security, positionToCopy, null);

            Position myPos = null;

            for(int i = 0;i < volumes.Count;i++)
            {
                if(myPos == null)
                {
                    myPos = BuyAtMarket(volumes[i], security, null);
                }
                else
                {
                    BuyAtMarket(volumes[i], security, myPos);
                }
                Thread.Sleep(IcebergMillisecondsDelay);
            }

            return myPos;
        }

        private Position BuyAtMarket(decimal volume , Security security, Position deal)
        {
            try
            {
                if (volume <= 0)
                {
                    SendLogMessage("Buy at market Error. \n"
                    + "Volume: " + volume + "\n"
                    + "Security: " + security.Name, LogMessageType.Error);
                    return null;
                }

                SendLogMessage("Buy at market. \n" 
                    + "Volume: " + volume + "\n"
                    + "Security: " + security.Name , LogMessageType.Trade);

                Side direction = Side.Buy;
                OrderPriceType priceType = OrderPriceType.Market;

                Portfolio portfolio = GetPortfolioByName(this.PortfolioName);

                if(portfolio == null)
                {
                    return null;
                }

                decimal price = 0;

                OrderTypeTime orderTypeTime = OrderTypeTime.GTC;

                TimeSpan timeLife = TimeSpan.FromSeconds(60);

                if (deal == null)
                {
                    deal = _dealCreator.CreatePosition(
                        this.NameUnique, direction, price, volume, 
                        priceType, timeLife, security, portfolio, 
                        StartProgram.IsOsTrader, orderTypeTime,
                        false);

                    MyJournal.SetNewDeal(deal);
                }
                else
                {
                    Order newOrder = _dealCreator.CreateOrder(
                        security, direction, price, volume, priceType, 
                        timeLife, StartProgram.IsOsTrader, OrderPositionConditionType.Open, 
                        orderTypeTime, MyCopyServer.ServerNameUnique,
                        false, deal.Number);

                    newOrder.PortfolioNumber = this.PortfolioName;
                    deal.AddNewOpenOrder(newOrder);
                }

                MyCopyServer.ExecuteOrder(deal.OpenOrders[^1]);

                return deal;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private Position SellAtMarketIceberg(decimal volume, Security security, int ordersCount, PositionToCopy positionToCopy)
        {
            List<decimal> volumes = GetVolumesArray(volume, ordersCount, security, positionToCopy, null);

            Position myPos = null;

            for (int i = 0; i < volumes.Count; i++)
            {
                if (myPos == null)
                {
                    myPos = SellAtMarket(volumes[i], security, null);
                }
                else
                {
                    SellAtMarket(volumes[i], security, myPos);
                }
                Thread.Sleep(IcebergMillisecondsDelay);
            }

            return myPos;
        }

        private Position SellAtMarket(decimal volume, Security security, Position deal)
        {
            try
            {
                if (volume <= 0)
                {
                    SendLogMessage("Buy at market Error. \n"
                    + "Volume: " + volume + "\n"
                    + "Security: " + security.Name, LogMessageType.Error);
                    return null;
                }

                SendLogMessage("Buy at market. \n"
                + "Volume: " + volume + "\n"
                + "Security: " + security.Name, LogMessageType.Trade);

                Side direction = Side.Sell;
                OrderPriceType priceType = OrderPriceType.Market;

                Portfolio portfolio = GetPortfolioByName(this.PortfolioName);

                if (portfolio == null)
                {
                    return null;
                }

                decimal price = 0;

                OrderTypeTime orderTypeTime = OrderTypeTime.GTC;

                TimeSpan timeLife = TimeSpan.FromSeconds(60);

                if (deal == null)
                {
                    deal = _dealCreator.CreatePosition(
                        this.NameUnique, direction, price, volume, priceType,
                        timeLife, security, portfolio, StartProgram.IsOsTrader, 
                        orderTypeTime, false);
                    MyJournal.SetNewDeal(deal);
                }
                else
                {
                    Order newOrder = _dealCreator.CreateOrder(
                        security, direction, price, volume, priceType, 
                        timeLife, StartProgram.IsOsTrader,
                        OrderPositionConditionType.Open, orderTypeTime, 
                        deal.ServerName, false, deal.Number);

                    newOrder.PortfolioNumber = this.PortfolioName;
                    deal.AddNewOpenOrder(newOrder);
                }

                MyCopyServer.ExecuteOrder(deal.OpenOrders[^1]);

                return deal;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private void CloseAtMarketIceberg(Position position, decimal volume, int ordersCount, PositionToCopy positionToCopy)
        {
            Security security = GetSecurityByName(position.SecurityName);

            if (security == null)
            {
                SendLogMessage("Close positions number: " + position.Number
                + "No security error. Sec in position: " + position.SecurityName, LogMessageType.Error);
                return;
            }

            List<decimal> volumes = GetVolumesArray(volume, ordersCount, security, positionToCopy, position);

            for (int i = 0; i < volumes.Count; i++)
            {
                CloseAtMarket(position, volumes[i]);
                Thread.Sleep(IcebergMillisecondsDelay);
            } 

        }

        private void CloseAtMarket(Position position, decimal volume)
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
                    SendLogMessage("Close positions number: " + position.Number
                    + "No security error. Sec in position: " + position.SecurityName, LogMessageType.Error);
                    return;
                }

                if (position.State == PositionStateType.Done &&
                    position.OpenVolume == 0)
                {
                    return;
                }

                if (volume <= 0)
                {
                    SendLogMessage("Close positions number: " + position.Number
                    + "No volume error. Volume: " + volume, LogMessageType.Error);
                    return;
                }

                SendLogMessage("Close positions number: " + position.Number + "\n"
                + "Volume: " + volume + "\n"
                + "Security: " + security.Name, LogMessageType.Trade);

                position.State = PositionStateType.Closing;

                OrderPriceType priceType = OrderPriceType.Market;

                TimeSpan lifeTime = TimeSpan.FromSeconds(60);

                OrderTypeTime orderTypeTime = OrderTypeTime.GTC;

                Order closeOrder = _dealCreator.CreateCloseOrderForDeal(
                    security, position, 0,
                    priceType, lifeTime, StartProgram.IsOsTrader,
                    orderTypeTime, MyCopyServer.ServerNameUnique, 
                    false);
                closeOrder.Volume = volume;

                if (closeOrder == null)
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

        private List<decimal> GetVolumesArray(decimal volume, decimal ordersCount, Security security, PositionToCopy positionToCopy, Position position)
        {
            // 1 бьём объём на равные части
            List<decimal> volumes = new List<decimal>();
            decimal allVolumeInArray = 0;

            for (int i = 0; i < ordersCount; i++)
            {
                decimal curVolume = volume / ordersCount;
                curVolume = Math.Round(curVolume, security.DecimalsVolume);

                if (curVolume > 0)
                {
                    allVolumeInArray += curVolume;
                    volumes.Add(curVolume);
                }
            }

            // 2 если после разделения на части и обрезаний итоговый объём изменился, добавляем его в первую ячейку

            if (allVolumeInArray != volume)
            {
                decimal residue = volume - allVolumeInArray;

                if (volumes.Count == 0)
                {
                    volumes.Add(0);
                }

                volumes[0] = Math.Round(volumes[0] + residue, security.DecimalsVolume);
            }

            // 3 проверяем чтобы объёмы были выше минимальных 

            for (int i = 0; i < volumes.Count; i++)
            {
                if(volumes.Count == 1)
                {
                    break;
                }

                if (i + 1 == volumes.Count)
                {
                    if (positionToCopy != null)
                    {
                        if (CanTradeThisVolume(volumes[i], security, GetPriceByPositionToCopy(positionToCopy)) == false)
                        {
                            volumes[i-1] += volumes[i];
                            volumes.RemoveAt(i);
                            break;
                        }
                    }
                    else if (position != null)
                    {
                        if (CanTradeThisVolume(volumes[i], security, position.EntryPrice) == false)
                        {
                            volumes[i-1] += volumes[i];
                            volumes.RemoveAt(i);
                            break;
                        }
                    }

                    break;
                }

                if(positionToCopy != null)
                {
                    if (CanTradeThisVolume(volumes[i], security, GetPriceByPositionToCopy(positionToCopy)) == false)
                    {
                        volumes[i + 1] += volumes[i];
                        volumes.RemoveAt(i);
                        i--;
                    }
                }
                else if(position != null)
                {
                    if (CanTradeThisVolume(volumes[i], security, position.EntryPrice) == false)
                    {
                        volumes[i + 1] += volumes[i];
                        volumes.RemoveAt(i);
                        i--;
                    }
                }
            }

            return volumes;
        }

        public bool CanTradeThisVolume(decimal volume, Security sec, decimal price)
        {
            if (volume <= 0)
            {
                return false;
            }

            if (sec == null)
            {
                return false;
            }

            if (sec.VolumeStep != 0)
            {
                if (volume < sec.VolumeStep)
                {
                    return false;
                }
            }

            if (sec.MinTradeAmount != 0)
            {
                if (sec.MinTradeAmountType == MinTradeAmountType.Contract)
                { // внутри бумаги минимальный объём одного ордера указан в контрактах

                    if (sec.MinTradeAmount > volume)
                    {
                        return false;
                    }
                }
                else if (sec.MinTradeAmountType == MinTradeAmountType.C_Currency)
                { // внутри бумаги минимальный объём для одного ордера указан в валюте контракта

                    // 1 пытаемся взять текущую цену из стакана
                    decimal lastPrice = price;

                    if (lastPrice != 0)
                    {
                        decimal qtyInContractCurrency = volume * lastPrice;

                        if (qtyInContractCurrency < sec.MinTradeAmount)
                        {
                            return false;
                        }

                    }
                }
            }

            return true;
        }

        #endregion

        #region Log

        public Log LogCopyTrader;

        public event Action<string, LogMessageType> LogMessageEvent;

        public void SendLogMessage(string message, LogMessageType messageType)
        {
            message = "Copy portfolio. Name: " + NameUnique + "\n" + message;
            LogMessageEvent?.Invoke(message, messageType);
        }

        #endregion
    }
    
    public enum CopyTraderVolumeType
    {
        Simple,
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
        public void SetPositionRobot(Position pos, 
            CopyTraderVolumeType copyType, decimal mult, decimal masterAsset, decimal slaveAsset)
        {
            PositionsRobots.Add(pos);

            if(pos.OpenVolume == 0)
            {
                return;
            }

            decimal volume = 0;

            if(copyType == CopyTraderVolumeType.Simple)
            {
                volume = pos.OpenVolume * mult;
            }
            else if(copyType == CopyTraderVolumeType.DepoProportional)
            {
                decimal proportion = masterAsset / slaveAsset;

                volume = (pos.OpenVolume / proportion) * mult;
            }

            if (pos.Direction == Side.Buy)
            {
                RobotVolumeBuy += volume;
                RobotVolumeAbs += volume;
            }
            else
            {
                RobotVolumeSell -= volume;
                RobotVolumeAbs -= volume;
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
