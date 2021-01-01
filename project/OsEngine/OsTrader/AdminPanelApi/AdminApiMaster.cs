using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.AdminPanelApi.Model;
using OsEngine.OsTrader.Panels;
using OsEngine.PrimeSettings;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OsEngine.OsTrader.AdminPanelApi
{
    public class AdminApiMaster : IDisposable
    {
        private readonly OsTraderMaster _traderMaster;

        private Counter _counter = new Counter();

        private TcpServer _apiServer;
        
        public AdminApiMaster(OsTraderMaster traderMaster)
        {
            _traderMaster = traderMaster;
            PrimeSettingsMaster.ApiState = ApiState.Off;
            ServerMaster.ServerCreateEvent += ServerMasterOnServerCreateEvent; 
            StartApi();
            _counter.Start();

            Task.Run(() =>
            {
                while (true)
                {
                    try
                    {
                        Thread.Sleep(5000);
                        SendProcessId();
                        SendCounter();
                        SendPortfolios();
                        SendServers();
                        
                        if (_traderMaster.PanelsArray != null)
                        {
                            SendBotList();
                            SendBot();
                            SendBotLog();
                            SendBotParams();
                            SendPositionsAndOrders();
                        }
                    }
                    catch (Exception e)
                    {
                        _traderMaster.SendNewLogMessage(e.ToString(), LogMessageType.Error);
                    }
                }
            });
        }

        private void SendProcessId()
        {
            JsonObject jId = new JsonObject();

            jId.Add("ProcessId", Process.GetCurrentProcess().Id);

            _apiServer.Send("processId_" + jId);

        }

        private void SendCounter()
        {
            var cpu = Math.Round(_counter.Cpu, 1);
            var ram = Math.Round(_counter.Ram, 1);

            JsonObject jCounter = new JsonObject();

            jCounter.Add("Cpu", cpu);
            jCounter.Add("Ram", ram);

            _apiServer.Send("osCounter_" + jCounter);
        }

        private void ServerMasterOnServerCreateEvent(IServer server)
        {
            ServerModel serverModel = new ServerModel(server, _apiServer);
        }

        private void StartApi()
        {
            if (PrimeSettingsMaster.ApiState == ApiState.Active)
            {
                return;
            }

            var permittedIp = PrimeSettingsMaster.Ip.Replace(" ", "").Split(',');
            var permittedToken = PrimeSettingsMaster.Token.Replace(" ", "");
            var exitPort = PrimeSettingsMaster.Port.Replace(" ", "");

            _apiServer = new TcpServer();
            _apiServer.ExitPort = Convert.ToInt32(exitPort);
            _apiServer.Started += ApiServerOnStarted;
            _apiServer.Connect(permittedIp, permittedToken);
        }

        private void ApiServerOnStarted()
        {
            PrimeSettingsMaster.ApiState = ApiState.Active;
        }

        private void SendPositionsAndOrders()
        {
            try
            {
                JsonArray jPositions = new JsonArray();
                JsonArray jOrders = new JsonArray();

                foreach (var panel in _traderMaster.PanelsArray)
                {
                    var allJournals = panel.GetJournals();

                    foreach (var journal in allJournals)
                    {
                        var allPos = journal.AllPosition;
                        foreach (var position in allPos)
                        {
                            jPositions.Add(GetJPosition(panel.NameStrategyUniq, position));

                            if (position.OpenOrders != null && position.OpenOrders.Count > 0)
                            {
                                foreach (var positionOpenOrder in position.OpenOrders)
                                {
                                    jOrders.Add(GetJOrder(position.NameBot, positionOpenOrder));
                                }
                            }
                            if (position.CloseOrders != null && position.CloseOrders.Count > 0)
                            {
                                foreach (var positionCloseOrder in position.CloseOrders)
                                {
                                    jOrders.Add(GetJOrder(position.NameBot, positionCloseOrder));
                                }
                            }
                        }
                    }
                }

                if (jPositions.Count != 0)
                {
                    _apiServer.Send("positionsSnapshot_" + jPositions.ToString());
                }

                if (jOrders.Count != 0)
                {
                    _apiServer.Send("ordersSnapshot_" + jOrders.ToString());
                }
            }
            catch (Exception e)
            {
                _traderMaster.SendNewLogMessage(e.ToString(), LogMessageType.Error);
            }
        }

        private void SendPortfolios()
        {
            JsonArray jPortfolios = new JsonArray();
            var servers = ServerMaster.GetServers();
            if (servers == null)
            {
                return;
            }

            foreach (var server in servers)
            {
                if (server.Portfolios == null)
                {
                    continue;
                }
                foreach (var portfolio in server.Portfolios)
                {
                    jPortfolios.Add(GetJPortfolio(portfolio));
                }
            }

            if (jPortfolios.Count != 0)
            {
                _apiServer.Send("portfoliosSnapshot_" + jPortfolios.ToString());
            }
        }

        private void SendServers()
        {
            var servers = ServerMaster.GetServers();
            if (servers == null)
            {
                return;
            }
            foreach (var server in servers)
            {
                string msg = "serverState_" + $"{{\"Server\":\"{server.ServerType.ToString()}\", \"State\":\"{server.ServerStatus}\"}}";
                _apiServer.Send(msg);
            }
        }

        private void SendBotList()
        {
            JsonArray jBots = new JsonArray();

            if (_traderMaster.PanelsArray == null)
            {
                return;
            }

            foreach (var botPanel in _traderMaster.PanelsArray)
            {
                JsonObject jBot = new JsonObject();
                jBot.Add(nameof(botPanel.NameStrategyUniq), botPanel.NameStrategyUniq);
                jBots.Add(jBot);
            }

            if (jBots.Count != 0)
            {
                _apiServer.Send("allBotsList_" + jBots.ToString());
            }
        }

        private void SendBotLog()
        {
            foreach (var botPanel in _traderMaster.PanelsArray)
            {
                var log = botPanel.GetLogMessages();
                if (log != null && log.Count > 0)
                {
                    var message = "botLog_" + GetJBotLog(botPanel.NameStrategyUniq, log).ToString();
                    _apiServer.Send(message);
                }
            }
        }

        private void SendBot()
        {
            foreach (var botPanel in _traderMaster.PanelsArray)
            {
                var message = "botState_" + GetJBot(botPanel).ToString();
                _apiServer.Send(message);
            }
        }

        private void SendBotParams()
        {
            foreach (var botPanel in _traderMaster.PanelsArray)
            {
                var parameters = botPanel.Parameters;

                if (parameters != null && parameters.Count > 0)
                {
                    var message = "botParams_" + GetJBotParams(botPanel.NameStrategyUniq, parameters).ToString();
                    _apiServer.Send(message);
                }
            }
        }

        private JsonObject GetJPosition(string botName, Position position)
        {
            JsonObject jPosition = new JsonObject();

            jPosition.Add(nameof(position.Number), position.Number);
            jPosition.Add(nameof(position.TimeOpen), position.TimeOpen.ToString(CultureInfo.CurrentCulture));
            jPosition.Add(nameof(position.TimeClose), position.TimeClose.ToString(CultureInfo.CurrentCulture));
            jPosition.Add(nameof(position.NameBot), botName);
            jPosition.Add(nameof(position.SecurityName), position.SecurityName);
            jPosition.Add(nameof(position.Direction), position.Direction.ToString());
            jPosition.Add(nameof(position.State), position.State.ToString());
            jPosition.Add(nameof(position.MaxVolume), position.MaxVolume);
            jPosition.Add(nameof(position.OpenVolume), position.OpenVolume);
            jPosition.Add(nameof(position.WaitVolume), position.WaitVolume);
            jPosition.Add(nameof(position.EntryPrice), position.EntryPrice);
            jPosition.Add(nameof(position.ClosePrice), position.ClosePrice);
            jPosition.Add(nameof(position.ProfitPortfolioPunkt), position.ProfitPortfolioPunkt);
            jPosition.Add(nameof(position.StopOrderRedLine), position.StopOrderRedLine);
            jPosition.Add(nameof(position.StopOrderPrice), position.StopOrderPrice);
            jPosition.Add(nameof(position.ProfitOrderRedLine), position.ProfitOrderRedLine);
            jPosition.Add(nameof(position.ProfitOrderPrice), position.ProfitOrderPrice);

            return jPosition;
        }

        private JsonObject GetJOrder(string roboName, Order order)
        {
            JsonObject jOrder = new JsonObject();

            jOrder.Add("RobotName", roboName);
            jOrder.Add(nameof(order.NumberUser), order.NumberUser);
            jOrder.Add(nameof(order.NumberMarket), order.NumberMarket);
            jOrder.Add(nameof(order.TimeCreate), order.TimeCreate.ToString(CultureInfo.CurrentCulture));
            jOrder.Add(nameof(order.SecurityNameCode), order.SecurityNameCode);
            jOrder.Add(nameof(order.PortfolioNumber), order.PortfolioNumber);
            jOrder.Add(nameof(order.Side), order.Side.ToString());
            jOrder.Add(nameof(order.State), order.State.ToString());
            jOrder.Add(nameof(order.Price), order.Price);
            jOrder.Add(nameof(order.PriceReal), order.PriceReal);
            jOrder.Add(nameof(order.Volume), order.Volume);
            jOrder.Add(nameof(order.TypeOrder), order.TypeOrder.ToString());
            jOrder.Add(nameof(order.TimeRoundTrip), order.TimeRoundTrip.ToString());

            return jOrder;
        }

        private JsonObject GetJPortfolio(Portfolio portfolio)
        {
            JsonObject jPortfolio = new JsonObject();

            jPortfolio.Add(nameof(portfolio.Number), portfolio.Number);
            jPortfolio.Add(nameof(portfolio.ValueBegin), portfolio.ValueBegin);
            jPortfolio.Add(nameof(portfolio.ValueCurrent), portfolio.ValueCurrent);
            jPortfolio.Add(nameof(portfolio.ValueBlocked), portfolio.ValueBlocked);

            var positionsOnBoard = portfolio.GetPositionOnBoard();

            if (positionsOnBoard != null)
            {
                JsonArray jPositions = new JsonArray();

                foreach (var positionOnBoard in positionsOnBoard)
                {
                    if (positionOnBoard.ValueBegin != 0 || positionOnBoard.ValueCurrent !=0 || positionOnBoard.ValueBlocked != 0)
                    {
                        JsonObject jPosition = new JsonObject();
                        jPosition.Add(nameof(positionOnBoard.PortfolioName), positionOnBoard.PortfolioName);
                        jPosition.Add(nameof(positionOnBoard.ValueBegin), positionOnBoard.ValueBegin);
                        jPosition.Add(nameof(positionOnBoard.ValueCurrent), positionOnBoard.ValueCurrent);
                        jPosition.Add(nameof(positionOnBoard.ValueBlocked), positionOnBoard.ValueBlocked);
                        jPosition.Add(nameof(positionOnBoard.SecurityNameCode), positionOnBoard.SecurityNameCode);
                        jPositions.Add(jPosition);
                    }
                }

                if (jPositions.Count != 0)
                {
                    jPortfolio.Add("PositionsOnBoard", jPositions);
                }
            }
            
            return jPortfolio;
        }

        private JsonObject GetJBotLog(string botName, List<LogMessage> logMessages)
        {
            JsonObject jLog = new JsonObject();

            jLog.Add("BotName", botName);

            JsonArray logList = new JsonArray();

            foreach (var logMessage in logMessages)
            {
                JsonObject log = new JsonObject();
                log.Add(nameof(logMessage.Time), logMessage.Time);
                log.Add(nameof(logMessage.Type), logMessage.Type);
                log.Add(nameof(logMessage.Message), logMessage.Message);
                logList.Add(log);
            }

            jLog.Add("Log", logList);

            return jLog;
        }

        private JsonObject GetJBotParams(string botName, List<IIStrategyParameter> parameters)
        {
            JsonObject jParams = new JsonObject();

            jParams.Add("BotName", botName);


            JsonArray paramsList = new JsonArray();

            foreach (var parameter in parameters)
            {
                JsonObject jParameter = new JsonObject();
                jParameter.Add(nameof(parameter.Name), parameter.Name);
                jParameter.Add("Value", parameter.ToString());

                paramsList.Add(jParameter);
            }

            jParams.Add("Params", paramsList);
            
            return jParams;
        }

        private JsonObject GetJBot(BotPanel botPanel)
        {
            JsonObject jLog = new JsonObject();

            jLog.Add("BotName", botPanel.NameStrategyUniq);

            var tabs = botPanel.GetTabs();

            DateTime lastTimeUpdate = DateTime.MinValue;

            foreach (var iBotTab in tabs)
            {
                lastTimeUpdate = iBotTab.LastTimeCandleUpdate;
            }

            jLog.Add("LastTimeUpdate", lastTimeUpdate.ToString(CultureInfo.InvariantCulture));

            var positions = GetOpenPositions(botPanel);

            jLog.Add("PositionsCount", positions.Count);

            var lots = positions.Sum(p => p.OpenVolume);

            jLog.Add("NettoCount", lots);

            jLog.Add("EventSendTime", DateTime.Now.ToString(CultureInfo.InvariantCulture));
            
            return jLog;
        }

        private List<Position> GetOpenPositions(BotPanel botPanel)
        {
            var journals = botPanel.GetJournals();
            List<Position> positions = new List<Position>();
            foreach (var journal in journals)
            {
                positions.AddRange(journal.OpenPositions);
            }

            return positions;
        }

        public void Dispose()
        {
            _apiServer.Disconnect();
        }
    }

    public enum ApiState
    {
        Active,
        NotAsk,
        Off
    }
}
