/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Candles.Series;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Connectors;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.MoexAlgopack.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Robots;
using System;
using System.Collections.Generic;
using System.IO;

namespace OsEngine.OsTrader.ClientManagement
{
    public class TradeClient : ILogItem
    {
        public TradeClient()
        {
          
        }

        public Log Log;

        public int Number
        {
            get
            {
                return _number;
            }
            set
            {
                _number = value;

                if(Log == null)
                {
                    Log = new Log("TradeClient" + Number, Entity.StartProgram.IsOsTrader);
                }
            }
        }
        private int _number;

        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if(_name == value)
                {
                    return;
                }

                _name = value.Replace("#","").Replace("&", "");

                if(NameChangeEvent != null)
                {
                    NameChangeEvent();
                }
            }
        }
        private string _name;

        public TradeClientRegime Regime;

        public string ClientUid;

        public void Save()
        {
            try
            {
                string dir = Directory.GetCurrentDirectory();
                dir += "\\Engine\\ClientManagement\\";

                if (Directory.Exists(dir) == false)
                {
                    Directory.CreateDirectory(dir);
                }

                using (StreamWriter writer = new StreamWriter(@"Engine\ClientManagement\" + Number + @"TradeClient.txt", false))
                {
                    writer.WriteLine(Number);
                    writer.WriteLine(Name);
                    writer.WriteLine(Regime.ToString());
                    writer.WriteLine(ClientUid);
                    writer.WriteLine("");
                    writer.WriteLine("");
                    writer.WriteLine(GetSaveStringConnectors());
                    writer.WriteLine(GetSaveStringRobots());


                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }

        }

        public void LoadFromFile(string fileAddress)
        {
            if (!File.Exists(fileAddress))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(fileAddress))
                {
                    Number = Convert.ToInt32(reader.ReadLine());
                    Name = reader.ReadLine();
                    Enum.TryParse(reader.ReadLine(), out Regime);
                    ClientUid = reader.ReadLine();
                    reader.ReadLine();
                    reader.ReadLine();

                    LoadConnectorsFromString(reader.ReadLine());
                    LoadRobotsFromString(reader.ReadLine());

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }

        }

        public void Delete()
        {
            try
            {
                if(RobotsSettings.Count > 0)
                {
                    TradeClientRobot[] robots = RobotsSettings.ToArray();

                    for (int i = 0; i < robots.Length; i++)
                    {
                        RemoveRobotAtNumber(robots[i].Number);
                    }
                }

                if(ConnectorsSettings.Count > 0)
                {
                    TradeClientConnector[] connectors = ConnectorsSettings.ToArray();

                    for (int i = 0; i < connectors.Length; i++)
                    {
                        RemoveConnectorAtNumber(connectors[i].Number);
                    }
                }

                if (File.Exists(@"Engine\ClientManagement\" + Number + @"TradeClient.txt") == true)
                {
                    File.Delete(@"Engine\ClientManagement\" + Number + @"TradeClient.txt");
                }
            }
            catch
            {
                // ignore
            }
        }

        public event Action NameChangeEvent;

        #region Connectors

        public List<TradeClientConnector> ConnectorsSettings = new List<TradeClientConnector>();

        private string GetSaveStringConnectors()
        {
            string saveStr = "";

            for(int i = 0;i < ConnectorsSettings.Count;i++)
            {
                saveStr += ConnectorsSettings[i].GetSaveString();

                if(i + 1 != ConnectorsSettings.Count)
                {
                    saveStr += "#";
                }
            }

            return saveStr;
        }

        private void LoadConnectorsFromString(string saveStr)
        {
            string[] connectors = saveStr.Split('#');

            for(int i = 0;i < connectors.Length;i++)
            {
                string currentSaveStr = connectors[i];

                if(string.IsNullOrEmpty(currentSaveStr) == true)
                {
                    continue;
                }

                TradeClientConnector connector = new TradeClientConnector();
                connector.LogMessageEvent += SendNewLogMessage;
                connector.LoadFromString(currentSaveStr);
                ConnectorsSettings.Add(connector);
            }
        }

        public TradeClientConnector AddNewConnector()
        {
            int newClientNumber = 0;

            for (int i = 0; i < ConnectorsSettings.Count; i++)
            {
                if (ConnectorsSettings[i].Number >= newClientNumber)
                {
                    newClientNumber = ConnectorsSettings[i].Number + 1;
                }
            }

            TradeClientConnector newConnector = new TradeClientConnector();
            newConnector.LogMessageEvent += SendNewLogMessage;
            newConnector.Number = newClientNumber;
            ConnectorsSettings.Add(newConnector);

            if (NewConnectorEvent != null)
            {
                NewConnectorEvent(newConnector);
            }

            Save();

            return newConnector;
        }

        public void RemoveConnectorAtNumber(int number)
        {
            TradeClientConnector connectorToRemove = null;

            for (int i = 0; i < ConnectorsSettings.Count; i++)
            {
                if (ConnectorsSettings[i].Number == number)
                {
                    connectorToRemove = ConnectorsSettings[i];
                    connectorToRemove.LogMessageEvent -= SendNewLogMessage;
                    ConnectorsSettings.RemoveAt(i);
                    break;
                }
            }

            if (connectorToRemove != null)
            {
                if (DeleteConnectorEvent != null)
                {
                    DeleteConnectorEvent(connectorToRemove);
                }
            }

            Save();
        }

        public event Action<TradeClientConnector> NewConnectorEvent;

        public event Action<TradeClientConnector> DeleteConnectorEvent;

        #endregion

        #region Robots

        public List<TradeClientRobot> RobotsSettings = new List<TradeClientRobot>();

        private string GetSaveStringRobots()
        {
            string saveStr = "";

            for (int i = 0; i < RobotsSettings.Count; i++)
            {
                saveStr += RobotsSettings[i].GetSaveString();

                if (i + 1 != RobotsSettings.Count)
                {
                    saveStr += "*";
                }
            }

            return saveStr;
        }

        private void LoadRobotsFromString(string saveStr)
        {
            if(saveStr == null)
            {
                return;
            }

            string[] robots = saveStr.Split('*');

            for (int i = 0; i < robots.Length; i++)
            {
                string currentSaveStr = robots[i];

                if (string.IsNullOrEmpty(currentSaveStr) == true)
                {
                    continue;
                }

                TradeClientRobot robot = new TradeClientRobot(this.ClientUid);
                robot.LogMessageEvent += SendNewLogMessage;
                robot.LoadFromString(currentSaveStr);
                RobotsSettings.Add(robot);
            }
        }

        public TradeClientRobot AddNewRobot()
        {
            int newClientNumber = 0;

            for (int i = 0; i < RobotsSettings.Count; i++)
            {
                if (RobotsSettings[i].Number >= newClientNumber)
                {
                    newClientNumber = RobotsSettings[i].Number + 1;
                }
            }

            TradeClientRobot newRobot = new TradeClientRobot(this.ClientUid);
            newRobot.LogMessageEvent += SendNewLogMessage;
            newRobot.Number = newClientNumber;
            RobotsSettings.Add(newRobot);

            if (NewRobotEvent != null)
            {
                NewRobotEvent(newRobot);
            }

            Save();

            return newRobot;
        }

        public void RemoveRobotAtNumber(int number)
        {
            TradeClientRobot connectorToRemove = null;

            for (int i = 0; i < RobotsSettings.Count; i++)
            {
                if (RobotsSettings[i].Number == number)
                {
                    connectorToRemove = RobotsSettings[i];
                    connectorToRemove.LogMessageEvent -= SendNewLogMessage;
                    RobotsSettings.RemoveAt(i);
                    break;
                }
            }

            if (connectorToRemove != null)
            {
                if (DeleteRobotEvent != null)
                {
                    DeleteRobotEvent(connectorToRemove);
                }
            }

            Save();
        }

        public void UpdateInfo()
        {
            if(RobotsSettings.Count == 0)
            {
                return;
            }
            if(NewRobotEvent != null)
            {
                NewRobotEvent(RobotsSettings[0]);
            }
        }

        public event Action<TradeClientRobot> NewRobotEvent;

        public event Action<TradeClientRobot> DeleteRobotEvent;

        #endregion

        #region Client server controls

        public AServer DeployServer(int connectorNumber)
        {
            if (connectorNumber >= ConnectorsSettings.Count)
            {
                return null;
            }

            TradeClientConnector connector = ConnectorsSettings[connectorNumber];

            connector.Deploy();

            return connector.MyServer;
        }

        public void CollapseSever(int connectorNumber)
        {
            if (connectorNumber >= ConnectorsSettings.Count)
            {
                return;
            }

            TradeClientConnector connector = ConnectorsSettings[connectorNumber];

            connector.Collapse();

        }

        public void ShowGuiServer(int connectorNumber)
        {
            if (connectorNumber >= ConnectorsSettings.Count)
            {
                return;
            }

            TradeClientConnector connector = ConnectorsSettings[connectorNumber];

            connector.ShowGui();

        }

        public void ConnectServer(int connectorNumber)
        {
            if (connectorNumber >= ConnectorsSettings.Count)
            {
                return;
            }

            TradeClientConnector connector = ConnectorsSettings[connectorNumber];

            connector.Connect();

        }

        public void DisconnectServer(int connectorNumber)
        {
            if (connectorNumber >= ConnectorsSettings.Count)
            {
                return;
            }

            TradeClientConnector connector = ConnectorsSettings[connectorNumber];

            connector.Disconnect();

        }

        #endregion

        #region Deploy robots and servers

        public void DeployOrUpdateRobot(int robotNumber, out string error)
        {

            // 1 берём настройки робота.

            TradeClientRobot botClient = null;

            for (int i = 0; i < RobotsSettings.Count; i++)
            {
                if (RobotsSettings[i].Number == robotNumber)
                {
                    botClient = RobotsSettings[i];
                    break;
                }
            }

            if (botClient == null)
            {
                error = "No number robot in array. Number: " + robotNumber;
                return;
            }

            // 1.1. Костыль на доступные данных по названию робота в слепке робота

            if(botClient.BotClassName == "None"
                || string.IsNullOrEmpty(botClient.BotClassName))
            {
                error = "No bot class name. Class: " + botClient.BotClassName;
                return;
            }

            // 1.2. Костыль на доступность настроек коннектора

            if(this.ConnectorsSettings == null
                || this.ConnectorsSettings.Count == 0)
            {
                error = "No connectors settings to client";
                return;
            }

            for(int i = 0;i < ConnectorsSettings.Count;i++)
            {
                if (ConnectorsSettings[i].ServerParameters.Count == 0)
                {
                    error = "Connectors client error. ConnectorsSettings[i].ServerParameters.Count == 0";
                    return;
                }

                if (ConnectorsSettings[i].ServerType == Market.ServerType.None)
                {
                    error = "Connectors client error. ConnectorsSettings[i].ServerType == Market.ServerType.None";
                    return;
                }

            }

            string botName = botClient.UniqueNameFull;

            // 2 сначала ищем робота в общих массивах ботов

            BotPanel bot = null;

            List<BotPanel> robotsInArray = OsTraderMaster.Master.PanelsArray;

            for(int i = 0;i < robotsInArray.Count;i++)
            {
                if (robotsInArray[i].NameStrategyUniq == botName)
                {
                    bot = robotsInArray[i];
                    break;
                }
            }
         
            // 3 создаём нового робота, если старого не нашли

            if(bot == null)
            {
                try
                {
                    botClient.CheckBotPosition(botClient.BotClassName);

                    bot = BotFactory.GetStrategyForName(botClient.BotClassName, botName, Entity.StartProgram.IsOsTrader, botClient.IsScript);

                    if (bot == null)
                    {
                        error = "Can`t create robot. Class: " + botClient.BotClassName;
                        return;
                    }

                    OsTraderMaster.Master.CreateNewBot(bot);

                }
                catch (Exception ex)
                {
                    error = "Can`t create robot. Error: " + ex.ToString();
                    return;
                }
            }

            // 4 устанавливаем параметры роботу. 

            try
            {
                for (int i = 0; i < botClient.Parameters.Count; i++)
                {
                    IIStrategyParameter parameterToSet = botClient.Parameters[i];

                    for (int i2 = 0; i2 < bot.Parameters.Count; i2++)
                    {
                        if (bot.Parameters[i2].Name == parameterToSet.Name
                            && bot.Parameters[i2].Type == parameterToSet.Type)
                        {
                            bot.Parameters[i2].LoadParamFromString(parameterToSet.GetStringToSave().Split('#'));
                            break;
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                error = "Can`t set parameters to bot. Error: " + ex.ToString();
                return;
            }

            // 5 разворачиваем коннекторы для робота

            List<AServer> servers = new List<AServer>();

            for(int i = 0;i < ConnectorsSettings.Count;i++)
            {
                AServer server = DeployServer(ConnectorsSettings[i].Number);

                if(server == null)
                {
                    error = "Can`t create server by settings. ServerNum: " + ConnectorsSettings[i].Number;
                    return;
                }

                server.StartServer();

                servers.Add(server);
            }

            // 6 ожидаем режима Connect 1 минуту.

            DateTime _endAwait = DateTime.Now.AddMinutes(1);

            while(true)
            {
                if(_endAwait < DateTime.Now)
                {
                    error = "No connection to server.";
                    return;
                }

                bool allConnected = true;

                for (int i = 0; i < servers.Count; i++)
                {
                    if (servers[i].ServerStatus != ServerConnectStatus.Connect)
                    {
                        allConnected = false;
                    }
                }

                if(allConnected == true)
                {
                    break;
                }
            }

            // 7 устанавливаем настройки источников по роботу

            List<IIBotTab> tabsInRobot = bot.GetTabs();

            if(botClient.SourceSettings.Count != tabsInRobot.Count)
            {
                error = "botClient.SourceSettings.Count != tabsInRobot.Count";
                return;
            }

            for(int i = 0;i < botClient.SourceSettings.Count;i++)
            {
                IIBotTab tabInRobot = tabsInRobot[i];
                TradeClientSourceSettings tabInClient = botClient.SourceSettings[i];

                if(tabInClient.BotTabType != tabInRobot.TabType)
                {
                    error = "tabInClient.BotTabType != tabInRobot.TabType";
                    return;
                }

                AServer myServer = DeployServer(tabInClient.ClientServerNum);

                SetSettingsInSource(tabInRobot, tabInClient, myServer);
            }

            error = "Success";
        }

        public void CollapseRobot(int robotNumber, out string error)
        {
            // 1 берём настройки робота.

            TradeClientRobot botClient = null;

            for (int i = 0; i < RobotsSettings.Count; i++)
            {
                if (RobotsSettings[i].Number == robotNumber)
                {
                    botClient = RobotsSettings[i];
                    break;
                }
            }

            if (botClient == null)
            {
                error = "No number robot in array. Number: " + robotNumber;
                return;
            }

            // 1.1. Костыль на доступные данных по названию робота в слепке робота

            if (botClient.BotClassName == "None"
                || string.IsNullOrEmpty(botClient.BotClassName))
            {
                error = "No bot class name. Class: " + botClient.BotClassName;
                return;
            }

            string botName = botClient.UniqueNameFull;

            // 2 ищем робота в общих массивах ботов

            BotPanel bot = null;

            List<BotPanel> robotsInArray = OsTraderMaster.Master.PanelsArray;

            for (int i = 0; i < robotsInArray.Count; i++)
            {
                if (robotsInArray[i].NameStrategyUniq == botName)
                {
                    bot = robotsInArray[i];
                    break;
                }
            }

            if(bot != null)
            {
                OsTraderMaster.Master.DeleteRobotByInstance(bot);
            }

            error = "Success";
        }

        public void ShowRobotsChartDialog(int robotNumber)
        {



        }

        private void SetSettingsInSource(IIBotTab tabInRobot, TradeClientSourceSettings tabInClient, AServer myServer)
        {
            if(tabInRobot.TabType == BotTabType.Simple)
            {
                BotTabSimple simple = (BotTabSimple)tabInRobot;

                simple.Connector.PortfolioName = tabInClient.PortfolioName;
                simple.Connector.TimeFrame = tabInClient.TimeFrame;
                simple.Connector.SecurityClass = tabInClient.Securities[0].Class;
                simple.Connector.SecurityName = tabInClient.Securities[0].Name;
                simple.Connector.SaveTradesInCandles = tabInClient.SaveTradesInCandle;

                simple.Connector.ServerType = myServer.ServerType;
                simple.Connector.ServerFullName = myServer.ServerNameUnique;

                simple.CommissionType = tabInClient.CommissionType;
                simple.CommissionValue = tabInClient.CommissionValue;
            }
            else if (tabInRobot.TabType == BotTabType.Index)
            {
                BotTabIndex index = (BotTabIndex)tabInRobot;

                index.UserFormula = tabInClient.UserFormula;
                index.CalculationDepth = tabInClient.CalculationDepth;
                index.PercentNormalization = tabInClient.PercentNormalization;

                index.AutoFormulaBuilder.Regime = tabInClient.RegimeIndexBuilder;
                index.AutoFormulaBuilder.DayOfWeekToRebuildIndex = tabInClient.DayOfWeekToRebuildIndex;
                index.AutoFormulaBuilder.HourInDayToRebuildIndex = tabInClient.HourInDayToRebuildIndex;
                index.AutoFormulaBuilder.IndexSortType = tabInClient.IndexSortType;
                index.AutoFormulaBuilder.IndexSecCount = tabInClient.IndexSecCount;
                index.AutoFormulaBuilder.IndexMultType = tabInClient.IndexMultType;
                index.AutoFormulaBuilder.DaysLookBackInBuilding = tabInClient.DaysLookBackInBuilding;

                List<ActivatedSecurity> securitiesList = new List<ActivatedSecurity>();

                for (int i = 0; i < tabInClient.Securities.Count; i++)
                {
                    if (string.IsNullOrEmpty(tabInClient.Securities[i].Name)
                        || string.IsNullOrEmpty(tabInClient.Securities[i].Class))
                    {
                        continue;
                    }

                    ActivatedSecurity sec = new ActivatedSecurity();
                    sec.IsOn = true;

                    sec.SecurityName = tabInClient.Securities[i].Name;
                    sec.SecurityClass = tabInClient.Securities[i].Class;

                    if (sec == null)
                    {
                        continue;
                    }

                    securitiesList.Add(sec);
                }

                index.SetNewSecuritiesList(securitiesList);
            }
            else if (tabInRobot.TabType == BotTabType.Screener)
            {
                BotTabScreener screener = (BotTabScreener)tabInRobot;

                screener.CandleMarketDataType = tabInClient.CandleMarketDataType;
                screener.TimeFrame = tabInClient.TimeFrame;
                screener.CommissionType = tabInClient.CommissionType;
                screener.CommissionValue = tabInClient.CommissionValue;
                screener.SecuritiesClass = tabInClient.Securities[0].Class;

                screener.ServerName = myServer.ServerNameUnique;
                screener.ServerType = myServer.ServerType;

                List<ActivatedSecurity> securitiesList = new List<ActivatedSecurity>();

                for (int i = 0; i < tabInClient.Securities.Count; i++)
                {
                    if (string.IsNullOrEmpty(tabInClient.Securities[i].Name)
                       || string.IsNullOrEmpty(tabInClient.Securities[i].Class))
                    {
                        continue;
                    }

                    ActivatedSecurity sec = new ActivatedSecurity();
                    sec.IsOn = true;
                    sec.SecurityName = tabInClient.Securities[i].Name;
                    sec.SecurityClass = tabInClient.Securities[i].Class;

                    if (sec == null)
                    {
                        continue;
                    }

                    securitiesList.Add(sec);
                }

                screener.SecuritiesNames = securitiesList;
                screener.SaveSettings();
                screener.NeedToReloadTabs = true;
            }
        }

        #endregion
    }

    public enum TradeClientRegime
    {
        Manual,
        Auto
    }

}
