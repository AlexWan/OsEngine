/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.FixFastEquities.FIX;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using WebSocket4Net;

using OpenFAST;
using OpenFAST.Codec;
using OpenFAST.Template;
using OpenFAST.Template.Loader;
using System.Net.Sockets;
using System.Text;
using System.Xml;
using System.Net;
using LiteDB;
using System.Linq;
using Grpc.Core;
using OsEngine.Market.Servers.FixProtocolEntities;


namespace OsEngine.Market.Servers.FixFastEquities
{
    public class FixFastEquitiesServer : AServer
    {
        public FixFastEquitiesServer()
        {
            FixFastEquitiesServerRealization realization = new FixFastEquitiesServerRealization();
            ServerRealization = realization;

            // MFIX
            CreateParameterString("MFIX Trade Sever Address", "");
            CreateParameterString("MFIX Trade Sever Port", "");
            CreateParameterString("MFIX Trade Sever TargetCompId", "");
            CreateParameterString("MFIX Trade Capture Sever Address", "");
            CreateParameterString("MFIX Trade Capture Sever Port", "");
            CreateParameterString("MFIX Trade Capture Sever TargetCompId", "");
            CreateParameterString("MFIX Trade Server Login", "");
            CreateParameterPassword("MFIX Trade Server Password", "");
            CreateParameterString("MFIX Trade Capture Server Login", "");
            CreateParameterPassword("MFIX Trade Capture Server Password", "");
            CreateParameterString("MFIX Trade Account", "");
            CreateParameterString("MFIX Trade Client Code", "");

            // FAST
            CreateParameterPath("FIX/FAST Multicast Config Directory");
        }
    }

    public class FixFastEquitiesServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public FixFastEquitiesServerRealization()
        {
            Thread worker0 = new Thread(InstrumentDefinitionsReader);
            worker0.Name = "InstrumentsFixFastEquities";
            worker0.Start();

            Thread worker1 = new Thread(TradesReader);
            worker1.Name = "TradesFixFastEquities";
            worker1.Start();

            Thread worker2 = new Thread(OrdersReader);
            worker2.Name = "OrdersFixFastEquities";
            worker2.Start();

            Thread worker3 = new Thread(MFIXTradeServerConnection);
            worker3.Name = "MFIXTradeServerConnectionFixFastEquities";
            worker3.Start();

            Thread worker4 = new Thread(MFIXTradeCaptureServerConnection);
            worker4.Name = "MFIXTradeCaptureServerConnectionFixFastEquities";
            worker4.Start();

            //Thread worker = new Thread(ConnectionCheckThread);
            //worker.Name = "CheckAliveFixFastEquities";
            //worker.Start();

            //Thread worker2 = new Thread(DataMessageReader);
            //worker2.Name = "DataMessageReaderFixFastEquities";
            //worker2.Start();

            //Thread worker3 = new Thread(PortfolioMessageReader);
            //worker3.Name = "PortfolioMessageReaderFixFastEquities";
            //worker3.Start();
        }

        public void Connect()
        {
            try
            {                
                _securities.Clear();
                _myPortfolios.Clear();
                _subscribedSecurities.Clear();
                

                SendLogMessage("Start FIX/FAST Equities Connection", LogMessageType.System);

                _MFIXTradeServerAddress = ((ServerParameterString)ServerParameters[0]).Value;
                _MFIXTradeServerPort = ((ServerParameterString)ServerParameters[1]).Value;
                _MFIXTradeServerTargetCompId = ((ServerParameterString)ServerParameters[2]).Value;
                _MFIXTradeCaptureServerAddress = ((ServerParameterString)ServerParameters[3]).Value;
                _MFIXTradeCaptureServerPort = ((ServerParameterString)ServerParameters[4]).Value;
                _MFIXTradeCaptureServerTargetCompId = ((ServerParameterString)ServerParameters[5]).Value;
                
                _MFIXTradeServerLogin = ((ServerParameterString)ServerParameters[6]).Value;
                _MFIXTradeServerPassword = ((ServerParameterPassword)ServerParameters[7]).Value;
                
                _MFIXTradeCaptureServerLogin = ((ServerParameterString)ServerParameters[8]).Value;
                _MFIXTradeCaptureServerPassword = ((ServerParameterPassword)ServerParameters[9]).Value;

                _MFIXTradeAccount = ((ServerParameterString)ServerParameters[10]).Value;         
                _MFIXTradeClientCode = ((ServerParameterString)ServerParameters[11]).Value;

                _ConfigDir = ((ServerParameterPath)ServerParameters[12]).Value;

                if (string.IsNullOrEmpty(_MFIXTradeServerAddress) || string.IsNullOrEmpty(_MFIXTradeServerPort) || string.IsNullOrEmpty(_MFIXTradeServerTargetCompId))
                {
                    SendLogMessage("Connection terminated. You must specify FIX Trade Server Credentials", LogMessageType.Error);
                    return;
                }

                if (string.IsNullOrEmpty(_MFIXTradeCaptureServerAddress) || string.IsNullOrEmpty(_MFIXTradeCaptureServerPort) || string.IsNullOrEmpty(_MFIXTradeCaptureServerTargetCompId))
                {
                    SendLogMessage("Connection terminated. You must specify FIX Trade Capture Server Credentials", LogMessageType.Error);
                    return;
                }

                if (string.IsNullOrEmpty(_MFIXTradeServerLogin) || string.IsNullOrEmpty(_MFIXTradeServerPassword) || string.IsNullOrEmpty(_MFIXTradeCaptureServerLogin) || string.IsNullOrEmpty(_MFIXTradeCaptureServerPassword))
                {
                    SendLogMessage("Connection terminated. You must specify FIX Trade server and Trade Capture server login/password", LogMessageType.Error);
                    return;
                }

                if (string.IsNullOrEmpty(_MFIXTradeAccount) || string.IsNullOrEmpty(_MFIXTradeClientCode))
                {
                    SendLogMessage("Connection terminated. You must specify FIX Trade Account and Client Code", LogMessageType.Error);
                    return;
                }

                if (string.IsNullOrEmpty(_ConfigDir) || !System.IO.Directory.Exists(_ConfigDir) || !System.IO.File.Exists(_ConfigDir + "\\config.xml") || !System.IO.File.Exists(_ConfigDir + "\\template.xml"))
                {
                    SendLogMessage("Connection terminated. You must specify FIX/FAST Multicast Config Directory and it must contain config.xml and template.xml", LogMessageType.Error);
                    return;
                }

                               
                LoadFASTTemplates();
                              
                CreateSocketConnections();
                
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message.ToString(), LogMessageType.Error);
            }
        }

        private void LoadFASTTemplates()
        {
            IMessageTemplateLoader loader = new XmlMessageTemplateLoader();
            MessageTemplate[] templates;
            using (FileStream stream = File.OpenRead(_ConfigDir + "\\template.xml"))
            {
                templates = loader.Load(stream);
            }

            _context = new OpenFAST.Context();
            foreach (MessageTemplate tmplt in templates)
            {
                _context.RegisterTemplate(int.Parse(tmplt.Id), tmplt);
            }
        }

        private void ConnectionCheckThread()
        {
            while (true)
            {
                Thread.Sleep(10000);

                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    continue;
                }

                if (_lastGetLiveTimeToketTime.AddMinutes(20) < DateTime.Now)
                {
                    //if (GetCurSessionToken() == false)
                    {
                        if (ServerStatus == ServerConnectStatus.Connect)
                        {
                            ServerStatus = ServerConnectStatus.Disconnect;
                            DisconnectEvent();
                        }
                    }
                }
            }
        }

        DateTime _lastGetLiveTimeToketTime = DateTime.MinValue;
        

        public void Dispose()
        {
            _securities.Clear();
            _myPortfolios.Clear();
            _lastGetLiveTimeToketTime = DateTime.MinValue;

           // DeleteWebSocketConnection();

            SendLogMessage("Connection Closed by FixFastEquities. WebSocket Data Closed Event", LogMessageType.System);

            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        public DateTime ServerTime { get; set; }

        public ServerType ServerType => ServerType.FixFastEquities;

        public ServerConnectStatus ServerStatus { get; set; } = ServerConnectStatus.Disconnect;

        public List<IServerParameter> ServerParameters { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties
                
        private string _MFIXTradeServerAddress;
        private string _MFIXTradeServerPort;
        private string _MFIXTradeServerTargetCompId;
        private string _MFIXTradeCaptureServerAddress;
        private string _MFIXTradeCaptureServerPort;
        private string _MFIXTradeCaptureServerTargetCompId;
        private string _MFIXTradeServerLogin;
        private string _MFIXTradeServerPassword;
        private string _MFIXTradeCaptureServerLogin;
        private string _MFIXTradeCaptureServerPassword;
        private string _MFIXTradeAccount; // торговый счет
        private string _MFIXTradeClientCode; // код клиента

        private string _ConfigDir;

        private OpenFAST.Context _context;
        
        private Socket _instrumentSocketA;
        private Socket _instrumentSocketB;

        private Socket _tradesIncrementalSocketA;
        private Socket _tradesIncrementalSocketB;
        private Socket _tradesSnapshotSocketA;
        private Socket _tradesSnapshotSocketB;

        private Socket _ordersIncrementalSocketA;
        private Socket _ordersIncrementalSocketB;

        private Socket _MFIXTradeSocket;
        private int _MFIXTradeMsgSeqNum;
        private Socket _MFIXTradeCaptureSocket;
        private int _MFIXTradeCaptureMsgSeqNum;

        #endregion

        #region 3 Securities

        public void GetSecurities()
        {                        
        }

        private List<Security> _securities = new List<Security>();

        public event Action<List<Security>> SecurityEvent;
        
        #endregion

        #region 4 Portfolios

        private List<Portfolio> _myPortfolios = new List<Portfolio>();

        public void GetPortfolios()
        {            
            Portfolio newPortfolio = new Portfolio();
            newPortfolio.Number = _MFIXTradeAccount;
            newPortfolio.ValueCurrent = 1;
            _myPortfolios.Add(newPortfolio);

            if (_myPortfolios.Count != 0)
            {
                PortfolioEvent(_myPortfolios);                
            }

            //ActivatePortfolioSocket();
        }

        private void GetCurrentPortfolio(string portfoliId, string namePrefix)
        {
            try
            {
                //string endPoint = $"/md/v2/clients/MOEX/{portfoliId}/summary?format=Simple";
                //RestRequest requestRest = new RestRequest(endPoint, Method.GET);
                ////requestRest.AddHeader("Authorization", "Bearer " + _apiTokenReal);
                //requestRest.AddHeader("accept", "application/json");

                //RestClient client = new RestClient(_restApiHost);

                //IRestResponse response = client.Execute(requestRest);

                //if (response.StatusCode == HttpStatusCode.OK)
                //{
                //    //string content = response.Content;
                //    //FixFastEquitiesPortfolioRest portfolio = JsonConvert.DeserializeAnonymousType(content, new FixFastEquitiesPortfolioRest());

                //    //ConvertToPortfolio(portfolio, portfoliId, namePrefix);
                //}
                //else
                //{
                //    SendLogMessage("Portfolio request error. Status: " 
                //        + response.StatusCode + "  " + namePrefix, LogMessageType.Error);
                //}
            }
            catch (Exception exception)
            {
                SendLogMessage("Portfolio request error " + exception.ToString(), LogMessageType.Error);
            }
        }

        //private void ConvertToPortfolio(FixFastEquitiesPortfolioRest portfolio, string name, string prefix)
        //{
        //    Portfolio newPortfolio = new Portfolio();
        //    newPortfolio.Number = name + "_" + prefix;
        //    newPortfolio.ValueCurrent = portfolio.buyingPower.ToDecimal();
        //    _myPortfolios.Add(newPortfolio);
        //}

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            DateTime endTime = DateTime.Now.ToUniversalTime();

            while(endTime.Hour != 23)
            {
                endTime = endTime.AddHours(1);
            }

            int candlesInDay = 0;

            if(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes >= 1)
            {
                candlesInDay = 900 / Convert.ToInt32(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes);
            }
            else
            {
                candlesInDay = 54000/ Convert.ToInt32(timeFrameBuilder.TimeFrameTimeSpan.TotalSeconds);
            }

            if(candlesInDay == 0)
            {
                candlesInDay = 1;
            }

            int daysCount = candleCount / candlesInDay;

            if(daysCount == 0)
            {
                daysCount = 1;
            }

            daysCount++;

            if(daysCount > 5)
            { // добавляем выходные
                daysCount = daysCount + (daysCount / 5) * 2;
            }

            DateTime startTime = endTime.AddDays(-daysCount);

            List<Candle> candles = GetCandleDataToSecurity(security, timeFrameBuilder, startTime, endTime, startTime);
        
            while(candles.Count > candleCount)
            {
                candles.RemoveAt(0);
            }

            return candles;
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime,
            DateTime actualTime)
        { 
            if(startTime != actualTime)
            {
                startTime = actualTime;
            }

            List<Candle> candles = new List<Candle>();

            //TimeSpan additionTime = TimeSpan.FromMinutes(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes * 2500);

            //DateTime endTimeReal = startTime.Add(additionTime);

            //while (startTime < endTime)
            //{
            //    //CandlesHistoryFixFastEquities history = GetHistoryCandle(security, timeFrameBuilder, startTime, endTimeReal);

            //    List<Candle> newCandles = ConvertToOsEngineCandles(history);

            //    if(newCandles != null &&
            //        newCandles.Count > 0)
            //    {
            //        candles.AddRange(newCandles);
            //    }

            //    if(string.IsNullOrEmpty(history.prev) 
            //        && string.IsNullOrEmpty(history.next))
            //    {// на случай если указаны очень старые данные, и их там нет
            //        startTime = startTime.Add(additionTime);
            //        endTimeReal = startTime.Add(additionTime);
            //        continue;
            //    }

            //    if (string.IsNullOrEmpty(history.next))
            //    {
            //        break;
            //    }

            //    DateTime realStart = ConvertToDateTimeFromUnixFromSeconds(history.next);

            //    startTime = realStart;
            //    endTimeReal = realStart.Add(additionTime);
            //}

            //while (candles != null &&
            //    candles.Count != 0 && 
            //    candles[candles.Count - 1].TimeStart > endTime)
            //{
            //    candles.RemoveAt(candles.Count - 1);
            //}

            return candles;
        }

        //private CandlesHistoryFixFastEquities GetHistoryCandle(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime)
        //{
        //    // curl -X GET "https://api.FixFastEquities.ru/md/v2/history?symbol=SBER&exchange=MOEX&tf=60&from=1549000661&to=1550060661&format=Simple" -H "accept: application/json"

        //    string endPoint = "md/v2/history?symbol=" + security.Name;
        //    endPoint += "&exchange=MOEX";

        //    //Начало отрезка времени (UTC) в формате Unix Time Seconds

        //    endPoint += "&tf=" + GetFixFastEquitiesTf(timeFrameBuilder);
        //    endPoint += "&from=" + ConvertToUnixTimestamp(startTime);
        //    endPoint += "&to=" + ConvertToUnixTimestamp(endTime);
        //    endPoint += "&format=Simple";

        //    try
        //    {
        //        RestRequest requestRest = new RestRequest(endPoint, Method.GET);
        //        requestRest.AddHeader("accept", "application/json");
        //        requestRest.AddHeader("Authorization", "Bearer " + _apiTokenReal);
        //        RestClient client = new RestClient(_restApiHost);
        //        IRestResponse response = client.Execute(requestRest);

        //        if (response.StatusCode == HttpStatusCode.OK)
        //        {
        //            string content = response.Content;
        //            CandlesHistoryFixFastEquities candles = JsonConvert.DeserializeAnonymousType(content, new CandlesHistoryFixFastEquities());
        //            return candles;
        //        }
        //        else
        //        {
        //            SendLogMessage("Candles request error. Status: " + response.StatusCode, LogMessageType.Error);
        //        }
        //    }
        //    catch (Exception exception)
        //    {
        //        SendLogMessage("Candles request error" + exception.ToString(), LogMessageType.Error);
        //    }
        //    return null;
        //}

        //private List<Candle> ConvertToOsEngineCandles(CandlesHistoryFixFastEquities candles)
        //{
        //    List<Candle> result = new List<Candle>();

        //    for(int i = 0;i < candles.history.Count;i++)
        //    {
        //        FixFastEquitiesCandle curCandle = candles.history[i];

        //        Candle newCandle = new Candle();
        //        newCandle.Open = curCandle.open.ToDecimal();
        //        newCandle.High = curCandle.high.ToDecimal();
        //        newCandle.Low = curCandle.low.ToDecimal();
        //        newCandle.Close = curCandle.close.ToDecimal();
        //        newCandle.Volume = curCandle.volume.ToDecimal();
        //        newCandle.TimeStart = ConvertToDateTimeFromUnixFromSeconds(curCandle.time);

        //        result.Add(newCandle);
        //    }

        //    return result;
        //}

        private string GetFixFastEquitiesTf(TimeFrameBuilder timeFrameBuilder)
        {
            //Длительность таймфрейма в секундах или код ("D" - дни, "W" - недели, "M" - месяцы, "Y" - годы)
            // 15
            // 60
            // 300
            // 3600
            // D - Day
            // W - Week
            // M - Month
            // Y - Year

            string result = "";

            if(timeFrameBuilder.TimeFrame == TimeFrame.Day)
            {
                result = "D";
            }
            else
            {
                result = timeFrameBuilder.TimeFrameTimeSpan.TotalSeconds.ToString();
            }

            return result;
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;

            //// blocked

            //List<Trade> trades = new List<Trade>();

            //TimeSpan additionTime = TimeSpan.FromMinutes(1440);

            //DateTime endTimeReal = startTime.Add(additionTime);

            //while (startTime < endTime)
            //{
            //    TradesHistoryFixFastEquities history = GetHistoryTrades(security, startTime, endTimeReal);

            //    List<Trade> newTrades = ConvertToOsEngineTrades(history);

            //    if (newTrades != null &&
            //        newTrades.Count > 0)
            //    {
            //        trades.AddRange(newTrades);
            //        DateTime realStart = newTrades[newTrades.Count - 1].Time;
            //        startTime = realStart;
            //        endTimeReal = realStart.Add(additionTime);
            //    }
            //    else
            //    {
            //        startTime = startTime.Add(additionTime);
            //        endTimeReal = startTime.Add(additionTime);
            //    }
            //}

            //return trades;
        }

        //private TradesHistoryFixFastEquities GetHistoryTrades(Security security, DateTime startTime, DateTime endTime)
        //{
        //    // curl -X GET "https://api.FixFastEquities.ru/md/v2/Securities/MOEX/LKOH/alltrades/history?from=1593430060&to=1593430560&limit=100&offset=10&format=Simple" -H "accept: application/json"

        //    string endPoint = "/md/v2/Securities/MOEX/" + security.Name;
        //    endPoint += "/alltrades/history?";

        //    endPoint += "from=" + ConvertToUnixTimestamp(startTime);
        //    endPoint += "&to=" + ConvertToUnixTimestamp(endTime);
        //    endPoint += "&limit=50000";
        //    endPoint += "&format=Simple";

        //    try
        //    {
        //        RestRequest requestRest = new RestRequest(endPoint, Method.GET);
        //        requestRest.AddHeader("accept", "application/json");
        //        requestRest.AddHeader("Authorization", "Bearer " + _apiTokenReal);
        //        RestClient client = new RestClient(_restApiHost);
        //        IRestResponse response = client.Execute(requestRest);

        //        if (response.StatusCode == HttpStatusCode.OK)
        //        {
        //            string content = response.Content;
        //            TradesHistoryFixFastEquities trades = JsonConvert.DeserializeAnonymousType(content, new TradesHistoryFixFastEquities());
        //            return trades;
        //        }
        //        else
        //        {
        //            SendLogMessage("Trades request error. Status: " + response.StatusCode, LogMessageType.Error);
        //        }
        //    }
        //    catch (Exception exception)
        //    {
        //        SendLogMessage("Trades request error" + exception.ToString(), LogMessageType.Error);
        //    }
        //    return null;
        //}

        //private List<Trade> ConvertToOsEngineTrades(TradesHistoryFixFastEquities trades)
        //{
        //    List<Trade> result = new List<Trade>();

        //    if(trades.list == null)
        //    {
        //        return result;
        //    }

        //    for (int i = 0; i < trades.list.Count; i++)
        //    {
        //        FixFastEquitiesTrade curTrade = trades.list[i];

        //        Trade newTrade = new Trade();
        //        newTrade.Volume = curTrade.qty.ToDecimal();
        //        newTrade.Time = ConvertToDateTimeFromTimeFixFastEquitiesData(curTrade.time);
        //        newTrade.Price = curTrade.price.ToDecimal();
        //        newTrade.Id = curTrade.id;
        //        newTrade.SecurityNameCode = curTrade.symbol;

        //        if(curTrade.side == "buy")
        //        {
        //            newTrade.Side = Side.Buy;
        //        }
        //        else
        //        {
        //            newTrade.Side = Side.Sell;
        //        }

        //        result.Add(newTrade);
        //    }

        //    return result;
        //}

        #endregion

        #region 6 WebSocket creation

        private readonly string _wsHost = "wss://api.FixFastEquities.ru/ws";

        private string _socketLocker = "webSocketLockerFixFastEquities";

        private string GetGuid()
        {
            lock (_guidLocker)
            {
                iterator++;
                return iterator.ToString();
            }
        }

        int iterator = 0;

        string _guidLocker = "guidLocker";

        private void CreateSocketConnections()
        {
            try
            {
                //_subscriptionsData.Clear();
                //_subscriptionsPortfolio.Clear();

                // прочитать конфиг FIX/FAST соединения и создать сокеты

                // Load the XML document
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(_ConfigDir + "\\config.xml");

                // Find the 'channel' element
                XmlNode channelNode = xmlDoc.SelectSingleNode("/configuration/channel[@id='FOND']");

                // Extract 'id' and 'label' from the 'channel' element
                string channelId = channelNode.Attributes["id"].Value;
                string channelLabel = channelNode.Attributes["label"].Value;


                XmlNodeList connectionNodes = channelNode.SelectSingleNode("connections").SelectNodes("connection");
                foreach (XmlNode connectionNode in connectionNodes)
                {
                    string connectionId = connectionNode.Attributes["id"].Value;

                    XmlNode typeNode = connectionNode.SelectSingleNode("type");
                    string feedType = typeNode.Attributes["feed-type"].Value;

                    XmlNodeList feedNodes = connectionNode.SelectNodes("feed");

                    foreach (XmlNode feedNode in feedNodes)
                    {
                        // Extract 'id', 'src-ip', 'ip', and 'port' from each 'feed' element
                        string feedId = feedNode.Attributes["id"].Value; // A / B
                        string sourceAddressString = feedNode.SelectSingleNode("src-ip").InnerText;
                        string multicastAddressString = feedNode.SelectSingleNode("ip").InnerText;
                        string port = feedNode.SelectSingleNode("port").InnerText;

                        // Create a UDP socket
                        Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                        //// Join the multicast group
                        IPAddress multicastAddress = IPAddress.Parse(multicastAddressString);
                        IPAddress sourceAddress = IPAddress.Parse(sourceAddressString);

                        //// Bind the socket to the port
                        //// Specify the local IP address and port to bind to.
                        IPAddress localAddress = IPAddress.Any; // Listen on all available interfaces
                        IPEndPoint localEndPoint = new IPEndPoint(localAddress, int.Parse(port));


                        socket.Bind(localEndPoint);

                        byte[] membershipAddresses = new byte[12]; // 3 IPs * 4 bytes (IPv4)
                        Buffer.BlockCopy(multicastAddress.GetAddressBytes(), 0, membershipAddresses, 0, 4);
                        Buffer.BlockCopy(sourceAddress.GetAddressBytes(), 0, membershipAddresses, 4, 4);
                        Buffer.BlockCopy(localAddress.GetAddressBytes(), 0, membershipAddresses, 8, 4);
                        socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddSourceMembership, membershipAddresses);


                        if (feedType == "Instrument Replay")
                        {
                            if (feedId == "A")
                            {
                                _instrumentSocketA = socket;
                            }

                            if (feedId == "B")
                            {
                                _instrumentSocketB = socket;
                            }
                        }

                        if (feedType == "Trades Incremental")
                        {
                            if (feedId == "A")
                            {
                                _tradesIncrementalSocketA = socket;
                            }

                            if (feedId == "B")
                            {
                                _tradesIncrementalSocketB = socket;
                            }
                        }

                        if (feedType == "Trades Snapshot")
                        {
                            if (feedId == "A")
                            {
                                _tradesSnapshotSocketA = socket;
                            }

                            if (feedId == "B")
                            {
                                _tradesSnapshotSocketB = socket;
                            }
                        }

                        if (feedType == "Orders Incremental")
                        {
                            if (feedId == "A")
                            {
                                _ordersIncrementalSocketA = socket;
                            }

                            if (feedId == "B")
                            {
                                _ordersIncrementalSocketB = socket;
                            }
                        }
                    }
                }

                // Подгружаем бумаги или восстанавливаем из файла
                CreateSecurities();

                // Establish MFIX Trade Connection
                EstablishMFIXTradeConnection(); 

                try
                {
                    SendLogMessage("All streams activated. Connect State", LogMessageType.System);
                    ServerStatus = ServerConnectStatus.Connect;
                    ConnectEvent();
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                }



                //if (_webSocketData != null)
                //{
                //    return;
                //}

                //_socketDataIsActive = false;
                //_socketPortfolioIsActive = false;

                //lock (_socketLocker)
                //{
                //    WebSocketDataMessage = new ConcurrentQueue<string>();
                //    WebSocketPortfolioMessage = new ConcurrentQueue<string>();

                //    _webSocketData = new WebSocket(_wsHost);
                //    _webSocketData.EnableAutoSendPing = true;
                //    _webSocketData.AutoSendPingInterval = 10;
                //    _webSocketData.Opened += WebSocketData_Opened;
                //    _webSocketData.Closed += WebSocketData_Closed;
                //    _webSocketData.MessageReceived += WebSocketData_MessageReceived;
                //    _webSocketData.Error += WebSocketData_Error;
                //    _webSocketData.Open();


                //    _webSocketPortfolio = new WebSocket(_wsHost);
                //    _webSocketPortfolio.EnableAutoSendPing = true;
                //    _webSocketPortfolio.AutoSendPingInterval = 10;
                //    _webSocketPortfolio.Opened += _webSocketPortfolio_Opened;
                //    _webSocketPortfolio.Closed += _webSocketPortfolio_Closed;
                //    _webSocketPortfolio.MessageReceived += _webSocketPortfolio_MessageReceived;
                //    _webSocketPortfolio.Error += _webSocketPortfolio_Error;
                //    _webSocketPortfolio.Open();

                //}

            }
            catch (Exception exeption)
            {
                SendLogMessage(exeption.ToString(), LogMessageType.Error);
            }
        }

        private void EstablishMFIXTradeConnection()
        {
            // 1. Создаем и отправляем два запроса на подключение (Logon)
            _MFIXTradeMsgSeqNum = 1;
            _MFIXTradeCaptureMsgSeqNum = 1;

            //Создаем заголовк
            Header tradeServerLogonHeader = new Header
            {
                BeginString = "FIX.4.4", //Версия FIX "FIX .4 .4»,
                MsgType = "A", //Тип сообщения на установку сессии
                SenderCompID = _MFIXTradeServerLogin,
                TargetCompID = _MFIXTradeServerTargetCompId,
                MsgSeqNum = _MFIXTradeMsgSeqNum++
            };

            Header tradeCaptureServerLogonHeader = new Header
            {
                BeginString = "FIX.4.4", //Версия FIX "FIX .4 .4»,
                MsgType = "A", //Тип сообщения на установку сессии
                SenderCompID = _MFIXTradeCaptureServerLogin,
                TargetCompID = _MFIXTradeCaptureServerTargetCompId,
                MsgSeqNum = _MFIXTradeCaptureMsgSeqNum++
            };

            //Создаем сообщение на подключение onLogon
            LogonMessage logonTServerMessageBody = new LogonMessage
            {
                EncryptMethod = 0,
                HeartBtInt = 30,
                ResetSeqNumFlag = true,
                Password = _MFIXTradeServerPassword,
            };

            LogonMessage logonTCServerMessageBody = new LogonMessage
            {
                EncryptMethod = 0,
                HeartBtInt = 30,
                ResetSeqNumFlag = true,
                Password = _MFIXTradeCaptureServerPassword,
            };

            //Вычисляем длину сообщения
            tradeServerLogonHeader.BodyLength = tradeServerLogonHeader.GetHeaderSize() + logonTServerMessageBody.GetMessageSize();
            tradeCaptureServerLogonHeader.BodyLength = tradeCaptureServerLogonHeader.GetHeaderSize() + logonTCServerMessageBody.GetMessageSize();

            //Создаем концовку сообщения
            Trailer tradeServerTrailer = new Trailer(tradeServerLogonHeader.ToString() + logonTServerMessageBody.ToString());
            Trailer tradeCaptureServerTrailer = new Trailer(tradeCaptureServerLogonHeader.ToString() + logonTCServerMessageBody.ToString());

            //Формируем полное готовое сообщение
            string tradeServerLogonMessage = tradeServerLogonHeader.ToString() + logonTServerMessageBody.ToString() + tradeServerTrailer.ToString();
            string tradeCaptureServerLogonMessage = tradeCaptureServerLogonHeader.ToString() + logonTCServerMessageBody.ToString() + tradeCaptureServerTrailer.ToString();

            // 2. Создаем два сокета и подключаемся к ним
            // MFIX Trade Server
            IPAddress ipAddr = IPAddress.Parse(_MFIXTradeServerAddress);
            IPEndPoint ipEndPoint = new IPEndPoint(ipAddr, int.Parse(_MFIXTradeServerPort));

            //Создаем сокет для подключения
            _MFIXTradeSocket = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            //_MFIXTradeSocket.Blocking = false;
            //Подключаемся
            _MFIXTradeSocket.Connect(ipEndPoint);

            // MFIX Trade Capture Server
            ipAddr = IPAddress.Parse(_MFIXTradeCaptureServerAddress);
            ipEndPoint = new IPEndPoint(ipAddr, int.Parse(_MFIXTradeCaptureServerPort));

            //Создаем сокет для подключения
            _MFIXTradeCaptureSocket = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            //_MFIXTradeCaptureSocket.Blocking = false;
            //Подключаемся
            _MFIXTradeCaptureSocket.Connect(ipEndPoint);
                                   
            //Отправляем сообщение
            int bytesSent = _MFIXTradeSocket.Send(Encoding.UTF8.GetBytes(tradeServerLogonMessage));
            bytesSent = _MFIXTradeCaptureSocket.Send(Encoding.UTF8.GetBytes(tradeCaptureServerLogonMessage));

            bool tradeServerConnected = false;
            bool tradeCaptureServerConnected = false;

            //Получаем ответ от сервера
            byte[] bytes = new byte[4096];
            int bytesRec = 0;

            while (tradeServerConnected == false || tradeCaptureServerConnected == false)
            {
                bytesRec = 0;
                if (tradeCaptureServerConnected == false)
                {
                    bytesRec = _MFIXTradeCaptureSocket.Receive(bytes);

                    if (bytesRec > 0)
                    {
                        string serverMessage = Encoding.UTF8.GetString(bytes, 0, bytesRec);
                        FIXMessage fixMessage = FIXMessage.ParseFIXMessage(serverMessage);

                        if (fixMessage.MessageType == "Logon")
                        {
                            tradeCaptureServerConnected = true;
                            SendLogMessage("MFIX Trade capture server connected", LogMessageType.System);
                        }
                    }
                }

                bytesRec = 0;
                if (tradeServerConnected == false)
                {
                    bytesRec = _MFIXTradeSocket.Receive(bytes);

                    if (bytesRec > 0)
                    {
                        string serverMessage = Encoding.UTF8.GetString(bytes, 0, bytesRec);
                        FIXMessage fixMessage = FIXMessage.ParseFIXMessage(serverMessage);

                        if (fixMessage.MessageType == "Logon")
                        {
                            tradeServerConnected = true;
                            SendLogMessage("MFIX Trade server connected", LogMessageType.System);
                        }
                    }
                }                                

                Thread.Sleep(100);
            }
        }

        private void LoadSecuritiesFromFile()
        {
            try
            {
                string dir = Directory.GetCurrentDirectory();
                dir += "\\Engine\\DataBases\\";

                if (Directory.Exists(dir) == false)
                {
                    Directory.CreateDirectory(dir);
                }

                dir += "FixFastEquitiesSecurities.db";

                if (File.Exists(dir) == false)
                {
                    SendLogMessage("No saved securities in file", LogMessageType.System);
                    return;
                }

                using (LiteDatabase db = new LiteDatabase(dir))
                {
                    var collection = db.GetCollection<SecurityToSave>("securities");

                    List<SecurityToSave> col = collection.FindAll().ToList();

                    for (int i = 0; i < col.Count; i++)
                    {
                        SecurityToSave curSec = col[i];

                        Security newSecurity = new Security();
                        newSecurity.Name = curSec.Name;
                        newSecurity.NameId = curSec.NameId;
                        newSecurity.NameFull = curSec.NameFull;
                        newSecurity.NameClass = curSec.NameClass;
                        newSecurity.Lot = curSec.Lot;
                        newSecurity.PriceStep = curSec.PriceStep;
                        newSecurity.PriceStepCost = curSec.PriceStep;
                        newSecurity.Decimals = curSec.Decimals;
                        newSecurity.DecimalsVolume = curSec.DecimalsVolume;
                        newSecurity.Exchange = "MOEX";

                        if (curSec.NameClass.Contains("Stock"))
                            newSecurity.SecurityType = SecurityType.Stock;

                        if (curSec.NameClass.Contains("Bond"))
                            newSecurity.SecurityType = SecurityType.Bond;

                        if (curSec.NameClass.Contains("Index"))
                            newSecurity.SecurityType = SecurityType.Index;

                        if (curSec.NameClass.Contains("Fund"))
                            newSecurity.SecurityType = SecurityType.Fund;
                        
                        _securities.Add(newSecurity);
                    }
                }
            }
            catch (Exception e)
            {
                SendLogMessage(e.ToString(), LogMessageType.Error);
            }
        }

        private void SaveSecuritiesToFile()
        {
            try
            {
                string dir = Directory.GetCurrentDirectory();
                dir += "\\Engine\\DataBases\\";

                if (Directory.Exists(dir) == false)
                {
                    Directory.CreateDirectory(dir);
                }

                dir += "FixFastEquitiesSecurities.db";

                using (LiteDatabase db = new LiteDatabase(dir))
                {
                    var collection = db.GetCollection<SecurityToSave>("securities");
                    collection.DeleteAll();

                    for (int i = 0; i < _securities.Count; i++)
                    {
                        SecurityToSave secToSave = new SecurityToSave();
                        secToSave.Name = _securities[i].Name;
                        secToSave.NameId = _securities[i].NameId;
                        secToSave.NameFull = _securities[i].NameFull;
                        secToSave.NameClass = _securities[i].NameClass;
                        secToSave.Lot = _securities[i].Lot;
                        secToSave.PriceStep = _securities[i].PriceStep;
                        secToSave.Decimals = _securities[i].Decimals;
                        secToSave.DecimalsVolume = _securities[i].DecimalsVolume;
                        
                        collection.Insert(secToSave);
                    }
                    
                    db.Commit();
                }
            }
            catch (Exception e)
            {
                SendLogMessage(e.ToString(), LogMessageType.Error);
            }
        }

        private void CreateSecurities()
        {
            // 1. Проверяем есть ли уже бумаги в базе данных
            // Если есть, то восстанавливаем их из базы данных
            LoadSecuritiesFromFile();
            if (_securities.Count > 0)
            {
                _allSecuritiesLoaded = true;

                SendLogMessage("Securities count: " + _securities.Count, LogMessageType.System);
                SecurityEvent(_securities);
                return;
            }
            
            // 2. Если нет, то подгружаем их с сервера и сохраняем в базу данных
            while (true)
            {
                Thread.Sleep(500);
                if (!_allSecuritiesLoaded)
                {
                    continue;
                }

                break;
            }
            
            SaveSecuritiesToFile();

            SecurityEvent(_securities);
            
            SendLogMessage("Securities count: " + _securities.Count, LogMessageType.System);
        }

        private void DeleteWebSocketConnection()
        {
            try
            {
                lock (_socketLocker)
                {
                    if (_webSocketData != null)
                    {
                        try
                        {
                            _webSocketData.Close();
                        }
                        catch
                        {
                            // ignore
                        }

                        try
                        {
                            _webSocketPortfolio.Close();
                        }
                        catch
                        {
                            // ignore
                        }

                        _webSocketData.Opened -= WebSocketData_Opened;
                        _webSocketData.Closed -= WebSocketData_Closed;
                        _webSocketData.MessageReceived -= WebSocketData_MessageReceived;
                        _webSocketData.Error -= WebSocketData_Error;
                        _webSocketData = null;

                        _webSocketPortfolio.Opened -= _webSocketPortfolio_Opened;
                        _webSocketPortfolio.Closed -= _webSocketPortfolio_Closed;
                        _webSocketPortfolio.MessageReceived -= _webSocketPortfolio_MessageReceived;
                        _webSocketPortfolio.Error -= _webSocketPortfolio_Error;
                        _webSocketPortfolio = null;
                    }
                }
            }
            catch (Exception exeption)
            {

            }
            finally
            {
                _webSocketData = null;
            }
        }

        private bool _socketDataIsActive;

        private bool _socketPortfolioIsActive;

        private string _activationLocker = "activationLocker";

        private void CheckActivationSockets()
        {
            if (_socketDataIsActive == false)
            {
                return;
            }

            if (_socketPortfolioIsActive == false)
            {
                return;
            }

            try
            {
                lock(_activationLocker)
                {
                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        SendLogMessage("All sockets activated. Connect State", LogMessageType.System);
                        ServerStatus = ServerConnectStatus.Connect;
                        ConnectEvent();
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }

        }

        private WebSocket _webSocketData;

        private WebSocket _webSocketPortfolio;

        private void ActivatePortfolioSocket()
        {
            if (string.IsNullOrEmpty(_MFIXTradeServerPort) == false)
            {
                ActivateCurrentPortfolioListening(_MFIXTradeServerPort);
            }
            if (string.IsNullOrEmpty(_MFIXTradeCaptureServerAddress) == false)
            {
                ActivateCurrentPortfolioListening(_MFIXTradeCaptureServerAddress);
            }
            if (string.IsNullOrEmpty(_MFIXTradeCaptureServerPort) == false)
            {
                ActivateCurrentPortfolioListening(_MFIXTradeCaptureServerPort);
            }
            if (string.IsNullOrEmpty(_MFIXTradeAccount) == false)
            {
                ActivateCurrentPortfolioListening(_MFIXTradeAccount);
            }
        }

        private void ActivateCurrentPortfolioListening(string portfolioName)
        {
            // myTrades subscription

            //RequestSocketSubscribleMyTrades subObjTrades = new RequestSocketSubscribleMyTrades();
            //subObjTrades.guid = GetGuid();
            //subObjTrades.token = _apiTokenReal;
            //subObjTrades.portfolio = portfolioName;

            //string messageTradeSub = JsonConvert.SerializeObject(subObjTrades);

            //FixFastEquitiesSocketSubscription myTradesSub = new FixFastEquitiesSocketSubscription();
            //myTradesSub.SubType = FixFastEquitiesSubType.MyTrades;
            //myTradesSub.Guid = subObjTrades.guid;

            //_subscriptionsPortfolio.Add(myTradesSub);
            //_webSocketPortfolio.Send(messageTradeSub);

            //Thread.Sleep(1000);

            //// orders subscription

            //RequestSocketSubscribleOrders subObjOrders = new RequestSocketSubscribleOrders();
            //subObjOrders.guid = GetGuid();
            //subObjOrders.token = _apiTokenReal;
            //subObjOrders.portfolio = portfolioName;

            //string messageOrderSub = JsonConvert.SerializeObject(subObjOrders);

            //FixFastEquitiesSocketSubscription ordersSub = new FixFastEquitiesSocketSubscription();
            //ordersSub.SubType = FixFastEquitiesSubType.Orders;
            //ordersSub.Guid = subObjOrders.guid;
            //ordersSub.ServiceInfo = portfolioName;

            //_subscriptionsPortfolio.Add(ordersSub);
            //_webSocketPortfolio.Send(messageOrderSub);

            //Thread.Sleep(1000);

            //// portfolio subscription

            //RequestSocketSubscriblePoftfolio subObjPortf = new RequestSocketSubscriblePoftfolio();
            //subObjPortf.guid = GetGuid();
            //subObjPortf.token = _apiTokenReal;
            //subObjPortf.portfolio = portfolioName;

            //string messagePortfolioSub = JsonConvert.SerializeObject(subObjPortf);

            //FixFastEquitiesSocketSubscription portfSub = new FixFastEquitiesSocketSubscription();
            //portfSub.SubType = FixFastEquitiesSubType.Porfolio;
            //portfSub.ServiceInfo = portfolioName;
            //portfSub.Guid = subObjPortf.guid;

            //_subscriptionsPortfolio.Add(portfSub);
            //_webSocketPortfolio.Send(messagePortfolioSub);

            //Thread.Sleep(1000);

            //// positions subscription

            //RequestSocketSubscriblePositions subObjPositions = new RequestSocketSubscriblePositions();
            //subObjPositions.guid = GetGuid();
            //subObjPositions.token = _apiTokenReal;
            //subObjPositions.portfolio = portfolioName;

            //string messagePositionsSub = JsonConvert.SerializeObject(subObjPositions);

            //FixFastEquitiesSocketSubscription positionsSub = new FixFastEquitiesSocketSubscription();
            //positionsSub.SubType = FixFastEquitiesSubType.Positions;
            //positionsSub.ServiceInfo = portfolioName;
            //positionsSub.Guid = subObjPositions.guid;

            //_subscriptionsPortfolio.Add(positionsSub);
            //_webSocketPortfolio.Send(messagePositionsSub);
        }

        #endregion

        #region 7 WebSocket events

        private void WebSocketData_Opened(object sender, EventArgs e)
        {
            SendLogMessage("Socket Data activated", LogMessageType.System);
            _socketDataIsActive = true;
            CheckActivationSockets();
        }

        private void WebSocketData_Closed(object sender, EventArgs e)
        {
            try
            {
                SendLogMessage("Connection Closed by FixFastEquities. WebSocket Data Closed Event", LogMessageType.Error);

                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketData_Error(object sender, SuperSocket.ClientEngine.ErrorEventArgs e)
        {
            try
            {
                var error = e;

                if (error.Exception != null)
                {
                    SendLogMessage(error.Exception.ToString(), LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Data socket error" + ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketData_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            try
            {
                if (e == null)
                {
                    return;
                }
                if (string.IsNullOrEmpty(e.Message))
                {
                    return;
                }
                if (e.Message.Length == 4)
                { // pong message
                    return;
                }

                if (e.Message.StartsWith("{\"requestGuid"))
                {
                    return;
                }

                if (WebSocketDataMessage == null)
                {
                    return;
                }

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                WebSocketDataMessage.Enqueue(e.Message);
            }
            catch (Exception error)
            {
                SendLogMessage("Trade socket error. " + error.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPortfolio_Opened(object sender, EventArgs e)
        {
            SendLogMessage("Socket Portfolio activated", LogMessageType.System);
            _socketPortfolioIsActive = true;
            CheckActivationSockets();
        }

        private void _webSocketPortfolio_Closed(object sender, EventArgs e)
        {
            try
            {
                SendLogMessage("Connection Closed by FixFastEquities. WebSocket Portfolio Closed Event", LogMessageType.Error);

                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPortfolio_Error(object sender, SuperSocket.ClientEngine.ErrorEventArgs e)
        {
            try
            {
                var error = e;

                if (error.Exception != null)
                {
                    SendLogMessage(error.Exception.ToString(), LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Portfolio socket error" + ex.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPortfolio_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            try
            {
                if (e == null)
                {
                    return;
                }
                if (string.IsNullOrEmpty(e.Message))
                {
                    return;
                }
                if (e.Message.Length == 4)
                { // pong message
                    return;
                }

                if (e.Message.StartsWith("{\"requestGuid"))
                {
                    return;
                }

                if (WebSocketPortfolioMessage == null)
                {
                    return;
                }

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                WebSocketPortfolioMessage.Enqueue(e.Message);
            }
            catch (Exception error)
            {
                SendLogMessage("Portfolio socket error. " + error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region 8 WebSocket Security subscrible
                
        List<Security> _subscribedSecurities = new List<Security>();
        Dictionary<string, MarketDepth> _marketDepths = new Dictionary<string, MarketDepth>();

        public void Subscrible(Security security)
        {
            for (int i = 0; i < _subscribedSecurities.Count; i++)
            {
                if (_subscribedSecurities[i].Name == security.Name)
                {
                    return;
                }
            }

            _subscribedSecurities.Add(security);
            
            if (!_marketDepths.ContainsKey(security.Name))
            {
                MarketDepth marketDepth = new MarketDepth();
                marketDepth.SecurityNameCode = security.Name;
                marketDepth.Time = DateTime.UtcNow;

                _marketDepths.Add(security.Name, marketDepth);
            }
        }

        #endregion

        #region 9 WebSocket parsing the messages

        private List<FixFastEquitiesSocketSubscription> _subscriptionsData = new List<FixFastEquitiesSocketSubscription>();

        private List<FixFastEquitiesSocketSubscription> _subscriptionsPortfolio = new List<FixFastEquitiesSocketSubscription>();

        private ConcurrentQueue<string> WebSocketDataMessage = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> WebSocketPortfolioMessage = new ConcurrentQueue<string>();

        private DateTime _lastInstrumentDefinitionsTime = DateTime.MinValue;
        private bool _allSecuritiesLoaded = false;

        private void InstrumentDefinitionsReader()
        {
            byte[] buffer = new byte[4096];
            
            List<int> snapshotIds = new List<int>();
            List<Security> securities = new List<Security>();

            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (_instrumentSocketA == null || _instrumentSocketB == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (_allSecuritiesLoaded)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    // читаем из потоков А и B
                    // либо сразу обрабатываем либо перемещаем в очередь для разбора
                    for (int s = 0; s < 2; s++)
                    { 
                        int length = s == 0 ? _instrumentSocketA.Receive(buffer) : _instrumentSocketB.Receive(buffer);

                        using (MemoryStream stream = new MemoryStream(buffer, 4, length))
                        {
                            FastDecoder decoder = new FastDecoder(_context, stream);
                            OpenFAST.Message msg = decoder.ReadMessage();

                            string msgType = msg.GetString("MessageType");
                            int msgSeqNum = int.Parse(msg.GetString("MsgSeqNum"));
                            
                            if (msgType == "d") /// security definition
                            {
                                _lastInstrumentDefinitionsTime = DateTime.UtcNow;
                                int totNumReports = int.Parse(msg.GetString("TotNumReports")); // общее число "бумаг" (возможны дубли)

                                if (snapshotIds.FindIndex(nmb => nmb == msgSeqNum) != -1)
                                {
                                    if (snapshotIds.Count == totNumReports)
                                    {
                                        _securities = securities;
                                        _allSecuritiesLoaded = true;
                                    }

                                    continue;
                                }
                                
                                snapshotIds.Add(msgSeqNum);
                                if (snapshotIds.Count % 1000 == 0)
                                {
                                    SendLogMessage($"Loading securities data: {snapshotIds.Count}/{totNumReports}", LogMessageType.System);
                                }

                                string symbol = msg.GetString("Symbol");
                                string securityID = msg.IsDefined("SecurityID") ? msg.GetString("SecurityID") : msg.GetString("CFICode");
                                string currency = msg.GetString("SettlCurrency");
                                string marketCode = msg.GetString("MarketCode");

                                if (marketCode != "FNDT")
                                    continue;

                                bool securityAlreadyPresent = false;
                                for (int i = 0; i < securities.Count; i++)
                                {
                                    if (securities[i].Name == symbol && securities[i].NameId == securityID)
                                    {
                                        securityAlreadyPresent = true;
                                        break;
                                    }
                                }

                                if (securityAlreadyPresent)
                                {
                                    continue;
                                }
                                
                                // Обрабатываем новые бумаги                            
                                string eveningSession = msg.IsDefined("EveningSession") ? msg.GetString("EveningSession") : "неизвестно";
                                string secDecimals = "0";// msg.IsDefined("GroupInstrAttrib") ? msg.GetGroup("GroupInstrAttrib").ToString() : "неизвестно";
                                string lot = "1";

                                string name = Encoding.UTF8.GetString(msg.GetBytes("EncodedSecurityDesc"));
                                //Типбумаги ={ msg.GetString("SecurityType")}

                                if (msg.IsDefined("MarketSegmentGrp"))
                                {
                                    SequenceValue secVal = msg.GetValue("MarketSegmentGrp") as SequenceValue;

                                    for (int i = 0; i < secVal.Length; i++)
                                    {
                                        GroupValue groupVal = secVal[i] as GroupValue;

                                        if (groupVal.IsDefined("RoundLot"))
                                        {
                                            lot = groupVal.GetValue("RoundLot").ToString();
                                        }
                                    }
                                }

                                if (msg.IsDefined("GroupInstrAttrib"))
                                {
                                    SequenceValue secVal = msg.GetValue("GroupInstrAttrib") as SequenceValue;

                                    for (int i = 0; i < secVal.Length; i++)
                                    {
                                        GroupValue groupVal = secVal[i] as GroupValue;

                                        if (groupVal.IsDefined("InstrAttribType"))
                                        {
                                            if (groupVal.GetValue("InstrAttribType").ToString() == "27")
                                            {
                                                secDecimals = groupVal.GetValue("InstrAttribValue").ToString();
                                            }
                                        }
                                    }
                                }

                                Security newSecurity = new Security();
                                newSecurity.Name = symbol;
                                newSecurity.NameId = securityID;
                                newSecurity.NameFull = name;
                                newSecurity.Exchange = "MOEX";
                                newSecurity.State = SecurityStateType.Activ;

                                string productType = msg.IsDefined("Product") ? msg.GetString("Product") : "не определено";
                                switch (productType)
                                {
                                    case "5":
                                        newSecurity.SecurityType = SecurityType.Stock;
                                        break;
                                    case "3":
                                    case "6":
                                    case "11":
                                        newSecurity.SecurityType = SecurityType.Bond;
                                        break;

                                    case "7":
                                        newSecurity.SecurityType = SecurityType.Index;
                                        break;
                                    case "12":
                                        newSecurity.SecurityType = SecurityType.Fund;
                                        break;
                                }

                                newSecurity.NameClass = newSecurity.SecurityType.ToString() + " " + currency;
                                newSecurity.Lot = lot.ToDecimal();

                                if (msg.IsDefined("MinPriceIncrement"))
                                {
                                    newSecurity.PriceStep = msg.GetString("MinPriceIncrement").ToDecimal();
                                }
                                else
                                {
                                    newSecurity.PriceStep = 1;
                                }

                                if (newSecurity.PriceStep == 0)
                                {
                                    newSecurity.PriceStep = 1;
                                }

                                newSecurity.PriceStepCost = newSecurity.PriceStep;
                                newSecurity.DecimalsVolume = 1;
                                newSecurity.Decimals = int.Parse(secDecimals);

                                //if (msg.IsDefined("MinPriceIncrement"))
                                //newSecurity.PriceLimitHigh = item.priceMax.ToDecimal();
                                //      if (string.IsNullOrEmpty(item.priceMin) == false)
                                //            {
                                //                newSecurity.PriceLimitLow = item.priceMin.ToDecimal();
                                //            }

                                securities.Add(newSecurity);
                            }
                        }
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void TradesReader()
        {
            byte[] buffer = new byte[4096];
                        

            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (_tradesIncrementalSocketA == null || _tradesIncrementalSocketB == null || _tradesSnapshotSocketA == null || _tradesSnapshotSocketB == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    // читаем из потоков А и B
                    // либо сразу обрабатываем либо перемещаем в очередь для разбора
                    for (int s = 0; s < 2; s++)
                    {
                        int length = s == 0 ? _tradesIncrementalSocketA.Receive(buffer) : _tradesIncrementalSocketB.Receive(buffer);

                        using (MemoryStream stream = new MemoryStream(buffer, 4, length))
                        {
                            FastDecoder decoder = new FastDecoder(_context, stream);
                            OpenFAST.Message msg = decoder.ReadMessage();

                            string msgType = msg.GetString("MessageType");

                            if (msgType == "X") /// Market Data - Incremental Refresh (X)
                            {
                                //_lastInstrumentDefinitionsTime = DateTime.UtcNow;                                                          

                                if (msg.IsDefined("GroupMDEntries"))
                                {
                                    SequenceValue secVal = msg.GetValue("GroupMDEntries") as SequenceValue;

                                    for (int i = 0; i < secVal.Length; i++)
                                    {
                                        GroupValue groupVal = secVal[i] as GroupValue;
                                                                                                                  

                                        string name = groupVal.GetString("Symbol");

                                        bool subscribed = false;
                                        for (int k = 0; k < _subscribedSecurities.Count; k++)
                                        {
                                            if (_subscribedSecurities[k].Name == name)
                                            {
                                                subscribed = true;
                                                break;
                                            }
                                        }

                                        if (!subscribed)
                                            continue;

                                        if (groupVal.IsDefined("MDEntryType") && groupVal.GetString("MDEntryType") == "z")
                                        {
                                            Trade trade = new Trade();
                                            trade.SecurityNameCode = name;
                                            trade.Price = groupVal.GetString("MDEntryPx").ToDecimal();
                                            
                                            string time = groupVal.GetString("MDEntryTime");
                                            if (time.Length == 8)
                                            {
                                                time = "0" + time;
                                            }

                                            time = DateTime.UtcNow.ToString("ddMMyyyy") + time;

                                            DateTime tradeDateTime = DateTime.ParseExact(time, "ddMMyyyyHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);

                                            trade.Time = tradeDateTime;


                                            trade.Id = groupVal.GetString("MDEntryID");
                                            trade.Side = groupVal.GetString("OrderSide") == "B" ? Side.Buy : Side.Sell;
                                            trade.Volume = groupVal.GetString("MDEntrySize").ToDecimal();

                                            if (NewTradesEvent != null)
                                            {
                                                NewTradesEvent(trade);
                                            }
                                        }
                                        
                                    }
                                }
                            }
                        }
                    }
                    for (int s = 0; s < 2; s++)
                    {
                        int length = s == 0 ? _tradesSnapshotSocketA.Receive(buffer) : _tradesSnapshotSocketB.Receive(buffer);

                        using (MemoryStream stream = new MemoryStream(buffer, 4, length))
                        {
                            FastDecoder decoder = new FastDecoder(_context, stream);
                            OpenFAST.Message msg = decoder.ReadMessage();

                            string msgType = msg.GetString("MessageType");

                            if (msgType == "W") /// Market Data - Snapshot/Full Refresh (W)
                            {
                                //_lastInstrumentDefinitionsTime = DateTime.UtcNow;                                                          
                                string name = msg.GetString("Symbol");
                                bool subscribed = false;
                                for (int k = 0; k < _subscribedSecurities.Count; k++)
                                {
                                    if (_subscribedSecurities[k].Name == name)
                                    {
                                        subscribed = true;
                                        break;
                                    }
                                }

                                if (!subscribed)
                                    continue;

                                if (msg.IsDefined("GroupMDEntries"))
                                {
                                    SequenceValue secVal = msg.GetValue("GroupMDEntries") as SequenceValue;

                                    for (int i = 0; i < secVal.Length; i++)
                                    {
                                        GroupValue groupVal = secVal[i] as GroupValue;
                                        
                                        if (groupVal.IsDefined("MDEntryType") && groupVal.GetString("MDEntryType") == "z")
                                        {
                                            Trade trade = new Trade();
                                            trade.SecurityNameCode = name;
                                            trade.Price = groupVal.GetString("MDEntryPx").ToDecimal();

                                            string time = groupVal.GetString("MDEntryTime");
                                            if (time.Length == 8)
                                            {
                                                time = "0" + time;
                                            }
                                            
                                            time = DateTime.UtcNow.ToString("ddMMyyyy") + time;

                                            DateTime tradeDateTime = DateTime.ParseExact(time, "ddMMyyyyHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
                                            

                                            trade.Time = tradeDateTime;
                                           

                                            trade.Id = groupVal.GetString("MDEntryID");
                                            trade.Side = groupVal.GetString("OrderSide") == "B" ? Side.Buy : Side.Sell;
                                            trade.Volume = groupVal.GetString("MDEntrySize").ToDecimal();

                                            if (NewTradesEvent != null)
                                            {
                                                NewTradesEvent(trade);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void OrdersReader()
        {
            byte[] buffer = new byte[4096];
            List<int> mdEntryIdsList = new List<int>();

            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (_ordersIncrementalSocketA == null || _ordersIncrementalSocketB == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    // читаем из потоков А и B
                    // либо сразу обрабатываем либо перемещаем в очередь для разбора
                    for (int s = 0; s < 2; s++)
                    {
                        int length = s == 0 ? _ordersIncrementalSocketA.Receive(buffer) : _ordersIncrementalSocketB.Receive(buffer);

                        using (MemoryStream stream = new MemoryStream(buffer, 4, length))
                        {
                            FastDecoder decoder = new FastDecoder(_context, stream);
                            OpenFAST.Message msg = decoder.ReadMessage();

                            string msgType = msg.GetString("MessageType");

                            if (msgType == "X") /// Market Data - Incremental Refresh (X)
                            {
                                //_lastInstrumentDefinitionsTime = DateTime.UtcNow;                                                          

                                if (msg.IsDefined("GroupMDEntries"))
                                {
                                    SequenceValue secVal = msg.GetValue("GroupMDEntries") as SequenceValue;

                                    for (int i = 0; i < secVal.Length; i++)
                                    {
                                        GroupValue groupVal = secVal[i] as GroupValue;


                                        string name = groupVal.GetString("Symbol");
                                        
                                        bool subscribed = false;
                                        for (int k = 0; k < _subscribedSecurities.Count; k++)
                                        {
                                            if (_subscribedSecurities[k].Name == name)
                                            {
                                                subscribed = true;
                                                break;
                                            }
                                        }

                                        if (!subscribed)
                                            continue;

                                        if (!groupVal.IsDefined("MDEntryType"))
                                            continue;

                                        int mdEntryId = groupVal.GetInt("MDEntryID");

                                        if (mdEntryIdsList.Contains(mdEntryId))
                                            continue;

                                        mdEntryIdsList.Add(mdEntryId);
                                        
                                        string mdEntryType = groupVal.GetString("MDEntryType");
                                        string mdUpdateAction = groupVal.GetString("MDUpdateAction");

                                        if (mdEntryType == "0" || mdEntryType == "1") // order book
                                        {
                                            decimal price = groupVal.GetString("MDEntryPx").ToDecimal();
                                            if (price <  0)
                                                continue;

                                            decimal volume = groupVal.GetString("MDEntrySize")?.ToDecimal() ?? 0;

                                            MarketDepth depth = _marketDepths[name];
                                            depth.Time = DateTime.UtcNow;

                                            mdEntryType= mdEntryType == "0" ? "Bid" : "Ask";

                                            mdUpdateAction = mdUpdateAction == "0" ? "Add" : mdUpdateAction == "1" ? "Update" : "Delete";
                                            
                                            if(mdEntryType == "Bid")
                                            {
                                                int index = depth.Bids.FindIndex(x => x.Price == price);

                                                if (index == -1 && mdUpdateAction != "Delete")
                                                {
                                                    MarketDepthLevel mdLevel = new MarketDepthLevel();
                                                    mdLevel.Price = price;
                                                    mdLevel.Bid = volume;
                                                    depth.Bids.Add(mdLevel);
                                                }
                                                else
                                                {
                                                    if (mdUpdateAction == "Add")
                                                        depth.Bids[index].Bid += volume;

                                                    if (mdUpdateAction == "Delete")
                                                    {
                                                        if (index == -1)
                                                            continue;

                                                        depth.Bids[index].Bid -= volume;
                                                        if (depth.Bids[index].Bid == 0)
                                                            depth.Bids.RemoveAt(index);
                                                    }

                                                    // TODO: Update
                                                }
                                            }
                                            else
                                            {
                                                int index = depth.Asks.FindIndex(x => x.Price == price);
                                                if (index == -1 && mdUpdateAction != "Delete")
                                                {
                                                    MarketDepthLevel mdLevel = new MarketDepthLevel();
                                                    mdLevel.Price = price;
                                                    mdLevel.Ask = volume;
                                                    depth.Asks.Add(mdLevel);
                                                }
                                                else
                                                {
                                                    if (mdUpdateAction == "Add")
                                                        depth.Asks[index].Ask += volume;

                                                    if (mdUpdateAction == "Delete")
                                                    {
                                                        if (index == -1)
                                                            continue;

                                                        depth.Asks[index].Ask -= volume;
                                                        if (depth.Asks[index].Ask == 0)
                                                            depth.Asks.RemoveAt(index);
                                                    }

                                                    // TODO: Update
                                                }
                                            }
                                            
                                            // sort bids/asks by price
                                            depth.Bids.Sort((x, y) => y.Price.CompareTo(x.Price));
                                            depth.Asks.Sort((x, y) => x.Price.CompareTo(y.Price));

                                            MarketDepthEvent(depth);
                                            _marketDepths[name] = depth;
                                        }

                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void MFIXTradeServerConnection()
        {
            byte[] bytes = new byte[4096];
            int bytesRec = 0;
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (_MFIXTradeSocket == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    bytesRec = _MFIXTradeSocket.Receive(bytes);
                                       
                    string serverMessage = Encoding.UTF8.GetString(bytes, 0, bytesRec);
                    FIXMessage fixMessage = FIXMessage.ParseFIXMessage(serverMessage);

                    // 0. Обрабатываем TestRequest
                    if (fixMessage.MessageType == "TestRequest")
                    {
                        Header header = new Header
                        {
                            BeginString = "FIX.4.4", //Версия FIX "FIX .4 .4»,
                            MsgType = "0", //Тип сообщения на установку сессии
                            SenderCompID = _MFIXTradeServerLogin,
                            TargetCompID = _MFIXTradeServerTargetCompId,
                            MsgSeqNum = _MFIXTradeMsgSeqNum++
                        };

                        string TestReqID = fixMessage.Fields["TestReqID"];
                        HeartbeatMessage hbMsg = new HeartbeatMessage()
                        {
                            TestReqID = TestReqID,
                        };

                        //Вычисляем длину сообщения
                        header.BodyLength = header.GetHeaderSize() + hbMsg.GetMessageSize();
                        //Создаем концовку сообщения
                        Trailer hbTrailer = new Trailer(header.ToString() + hbMsg.ToString());

                        //Формируем полное готовое сообщение
                        string fullMessage = header.ToString() + hbMsg.ToString() + hbTrailer.ToString();

                        //Отправляем сообщение
                        _MFIXTradeSocket.Send(Encoding.UTF8.GetBytes(fullMessage));
                    }

                    // 1. Обрабатываем ExecutionReport
                    if (fixMessage.MessageType == "ExecutionReport")
                    {                        
                        string ExecType = fixMessage.Fields["ExecType"];
                        string OrdStatus = fixMessage.Fields["OrdStatus"];
                        
                        string OrdType = fixMessage.Fields["OrdType"];
                        string Price = fixMessage.Fields.ContainsKey("Price") ? fixMessage.Fields["Price"] : "0";
                        string OrderQty = fixMessage.Fields["OrderQty"];

                        string LastQty = fixMessage.Fields.ContainsKey("LastQty") ? fixMessage.Fields["LastQty"] : "0";
                        string LastPx = fixMessage.Fields.ContainsKey("LastPx") ? fixMessage.Fields["LastPx"] : "0";

                        string TransactTime = fixMessage.Fields["TransactTime"];
                        string Text = fixMessage.Fields.ContainsKey("Text") ? fixMessage.Fields["Text"] : "";

                        Order order = new Order();

                        order.SecurityNameCode = fixMessage.Fields["Symbol"];
                        order.PortfolioNumber = fixMessage.Fields["Account"];
                        order.NumberMarket = fixMessage.Fields["OrderID"];
                        order.Comment = Text;

                        try
                        {
                            order.NumberUser = Convert.ToInt32(fixMessage.Fields["ClOrdID"]);
                        }
                        catch
                        {
                            // ignore
                        }

                        order.Side = fixMessage.Fields["Side"] == "1" ? Side.Buy : Side.Sell;

                        if (OrdType == "2")
                        {
                            order.Price = Price.ToDecimal();
                            order.TypeOrder = OrderPriceType.Limit;
                        }
                        else
                        {
                            order.TypeOrder = OrderPriceType.Market;
                        }

                        if (ExecType == "F") // сделка
                        {
                            order.Volume = LastQty.ToDecimal();
                        }

                        order.TimeCallBack = DateTime.ParseExact(TransactTime, "yyyyMMdd-HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
                                                                      
                        if (OrdStatus == "0" || OrdStatus == "9" || OrdStatus == "E")
                        {                            
                            order.State = OrderStateType.Activ;
                        }
                        else if (OrdStatus == "1")
                        {
                            order.State = OrderStateType.Patrial;
                        }
                        else if (OrdStatus == "2")
                        {
                            order.State = OrderStateType.Done;
                        }
                        else if (OrdStatus == "4")
                        {
                            decimal volFilled = fixMessage.Fields["CumQty"].ToDecimal();

                            if (volFilled > 0)
                            {
                                order.State = OrderStateType.Done;
                            }
                            else
                            {
                                order.State = OrderStateType.Cancel;
                            }                             
                        }
                        else if (OrdStatus == "8")
                        {
                            order.State = OrderStateType.Fail;
                        }

                        MyOrderEvent(order);
                    }

                    // 2. Обрабатываем Reject
                    if (fixMessage.MessageType == "Reject")
                    {                        
                        string RefTagID = fixMessage.Fields["RefTagID"];
                        string RefMsgType = fixMessage.Fields["RefMsgType"];
                        string SessionRejectReason = fixMessage.Fields["SessionRejectReason"];
                        
                        string Text = fixMessage.Fields["Text"];

                        SendLogMessage($"MFIX sent reject: {Text}. RefTagID: {RefTagID}, RefMsgType: {RefMsgType}, SessionRejectReason: {SessionRejectReason}", LogMessageType.Error);
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void MFIXTradeCaptureServerConnection()
        {
            byte[] bytes = new byte[4096];
            int bytesRec = 0;
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (_MFIXTradeCaptureSocket == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1);
                        continue;
                    }
                                       
                    bytesRec = _MFIXTradeCaptureSocket.Receive(bytes);

                   
                    string serverMessage = Encoding.UTF8.GetString(bytes, 0, bytesRec);
                    FIXMessage fixMessage = FIXMessage.ParseFIXMessage(serverMessage);


                    // 0. Обрабатываем TestRequest
                    if (fixMessage.MessageType == "TestRequest")
                    {
                        Header header = new Header
                        {
                            BeginString = "FIX.4.4", //Версия FIX "FIX .4 .4»,
                            MsgType = "0", //Тип сообщения на установку сессии
                            SenderCompID = _MFIXTradeCaptureServerLogin,
                            TargetCompID = _MFIXTradeCaptureServerTargetCompId,
                            MsgSeqNum = _MFIXTradeCaptureMsgSeqNum++
                        };

                        string TestReqID = fixMessage.Fields["TestReqID"];
                        HeartbeatMessage hbMsg = new HeartbeatMessage()
                        {
                            TestReqID = TestReqID,
                        };

                        //Вычисляем длину сообщения
                        header.BodyLength = header.GetHeaderSize() + hbMsg.GetMessageSize();
                        //Создаем концовку сообщения
                        Trailer hbTrailer = new Trailer(header.ToString() + hbMsg.ToString());

                        //Формируем полное готовое сообщение
                        string fullMessage = header.ToString() + hbMsg.ToString() + hbTrailer.ToString();

                        //Отправляем сообщение
                        _MFIXTradeCaptureSocket.Send(Encoding.UTF8.GetBytes(fullMessage));
                    }

                    // 1. Обрабатываем TradingSessionStatus
                    if (fixMessage.MessageType == "TradingSessionStatus")
                    {
                        string TradingSessionID = fixMessage.Fields["TradingSessionID"];
                        string Text = fixMessage.Fields["Text"];
                        SendLogMessage($"MFIX TC Server => {TradingSessionID}: {Text}", LogMessageType.System);
                    }

                    // 2. Обрабатываем TradeCaptureReport
                    if (fixMessage.MessageType == "TradeCaptureReport")
                    {
                        string ExecType = fixMessage.Fields["ExecType"];

                        if (ExecType != "F")
                            continue;

                        string Symbol = fixMessage.Fields["Symbol"];                                               
                        string price = fixMessage.Fields["LastPx"];
                        string qty = fixMessage.Fields["LastQty"];
                        string transactionTime = fixMessage.Fields["TransactTime"];
                        string tradeId = fixMessage.Fields["ExecID"].Split('|')[0];
                                                
                        MyTrade trade = new MyTrade();

                        trade.SecurityNameCode = Symbol;
                        trade.Price = price.ToDecimal();
                        trade.Volume = qty.ToDecimal();
                        trade.NumberOrderParent = fixMessage.Fields["OrderID"];
                        trade.NumberTrade = tradeId;
                        trade.Time = DateTime.ParseExact(transactionTime, "yyyyMMdd-HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture); // 	transactionTime	"20240511-13:09:35"	string
                        trade.Side = fixMessage.Fields["Side"] == "1" ? Side.Buy : Side.Sell;
                        
                        MyTradeEvent(trade);                       
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void DataMessageReader()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (WebSocketDataMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message;

                    WebSocketDataMessage.TryDequeue(out message);
                    
                    if (message == null)
                    {
                        continue;
                    }

                    if (message.Equals("pong"))
                    {
                        continue;
                    }

                    //SoketMessageBase baseMessage = 
                    //    JsonConvert.DeserializeAnonymousType(message, new SoketMessageBase());

                    //if(baseMessage == null 
                    //    || string.IsNullOrEmpty(baseMessage.guid))
                    //{
                    //    continue;
                    //}

                    //for(int i = 0;i < _subscriptionsData.Count;i++)
                    //{
                    //    if (_subscriptionsData[i].Guid != baseMessage.guid)
                    //    {
                    //        continue;
                    //    }

                    //    if (_subscriptionsData[i].SubType == FixFastEquitiesSubType.Trades)
                    //    {
                    //        UpDateTrade(baseMessage.data.ToString(), _subscriptionsData[i].ServiceInfo);
                    //        break;
                    //    }
                    //    else if (_subscriptionsData[i].SubType == FixFastEquitiesSubType.MarketDepth)
                    //    {
                    //        UpDateMarketDepth(message, _subscriptionsData[i].ServiceInfo);
                    //        break;
                    //    }
                    //}
                }
                catch (Exception exeption)
                {
                    SendLogMessage(exeption.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }
        
        private void UpDateMarketDepth(string data, string secName)
        {
            //MarketDepthFullMessage baseMessage =
            //JsonConvert.DeserializeAnonymousType(data, new MarketDepthFullMessage());

            //if (baseMessage.data.bids == null ||
            //    baseMessage.data.asks == null)
            //{
            //    return;
            //}

            //if (baseMessage.data.bids.Count == 0 ||
            //    baseMessage.data.asks.Count == 0)
            //{
            //    return;
            //}

            //MarketDepth depth = new MarketDepth();
            //depth.SecurityNameCode = secName;
            //depth.Time = ConvertToDateTimeFromUnixFromMilliseconds(baseMessage.data.ms_timestamp);

            //for (int i = 0; i < baseMessage.data.bids.Count; i++)
            //{
            //    MarketDepthLevel newBid = new MarketDepthLevel();
            //    newBid.Price = baseMessage.data.bids[i].price.ToDecimal();
            //    newBid.Bid = baseMessage.data.bids[i].volume.ToDecimal();
            //    depth.Bids.Add(newBid);
            //}

            //for (int i = 0; i < baseMessage.data.asks.Count; i++)
            //{
            //    MarketDepthLevel newAsk = new MarketDepthLevel();
            //    newAsk.Price = baseMessage.data.asks[i].price.ToDecimal();
            //    newAsk.Ask = baseMessage.data.asks[i].volume.ToDecimal();
            //    depth.Asks.Add(newAsk);
            //}

            //if(_lastMdTime != DateTime.MinValue &&
            //    _lastMdTime >= depth.Time)
            //{
            //    depth.Time = _lastMdTime.AddTicks(1);
            //}

            //_lastMdTime = depth.Time;

            //if (MarketDepthEvent != null)
            //{
            //    MarketDepthEvent(depth);
            //}
        }

        private DateTime _lastMdTime = DateTime.MinValue;

        public event Action<Trade> NewTradesEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        private void PortfolioMessageReader()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (WebSocketPortfolioMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message;

                    WebSocketPortfolioMessage.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    if (message.Equals("pong"))
                    {
                        continue;
                    }

                    //SoketMessageBase baseMessage =
                    //    JsonConvert.DeserializeAnonymousType(message, new SoketMessageBase());

                    //if (baseMessage == null
                    //    || string.IsNullOrEmpty(baseMessage.guid))
                    //{
                    //    continue;
                    //}

                    //for (int i = 0; i < _subscriptionsPortfolio.Count; i++)
                    //{
                    //    if (_subscriptionsPortfolio[i].Guid != baseMessage.guid)
                    //    {
                    //        continue;
                    //    }

                    //    if (_subscriptionsPortfolio[i].SubType == FixFastEquitiesSubType.Porfolio)
                    //    {
                    //        UpDateMyPortfolio(baseMessage.data.ToString(), _subscriptionsPortfolio[i].ServiceInfo);
                    //        break;
                    //    }
                    //    else if (_subscriptionsPortfolio[i].SubType == FixFastEquitiesSubType.Positions)
                    //    {
                    //        UpDatePositionOnBoard(baseMessage.data.ToString(), _subscriptionsPortfolio[i].ServiceInfo);
                    //        break;
                    //    }
                    //    else if (_subscriptionsPortfolio[i].SubType == FixFastEquitiesSubType.MyTrades)
                    //    {
                    //        UpDateMyTrade(baseMessage.data.ToString());
                    //        break;
                    //    }
                    //    else if (_subscriptionsPortfolio[i].SubType == FixFastEquitiesSubType.Orders)
                    //    {
                    //        UpDateMyOrder(baseMessage.data.ToString(), _subscriptionsPortfolio[i].ServiceInfo);
                    //        break;
                    //    }
                    //}
                }
                catch (Exception exeption)
                {
                    SendLogMessage(exeption.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void UpDateMyTrade(string data)
        {
            //MyTradeFixFastEquities baseMessage =
            //JsonConvert.DeserializeAnonymousType(data, new MyTradeFixFastEquities());

            //MyTrade trade = new MyTrade();

            //trade.SecurityNameCode = baseMessage.symbol;
            //trade.Price = baseMessage.price.ToDecimal();
            //trade.Volume = baseMessage.qty.ToDecimal();
            //trade.NumberOrderParent = baseMessage.orderno;
            //trade.NumberTrade = baseMessage.id;
            //trade.Time = ConvertToDateTimeFromTimeFixFastEquitiesData(baseMessage.date);
           
            //if(baseMessage.side == "buy")
            //{
            //    trade.Side = Side.Buy;
            //}
            //else
            //{
            //    trade.Side = Side.Sell;
            //}

            //if (MyTradeEvent != null)
            //{
            //    MyTradeEvent(trade);
            //}
        }

        private void UpDatePositionOnBoard(string data, string portfolioName)
        {
            //PositionOnBoardFixFastEquities baseMessage =
            //           JsonConvert.DeserializeAnonymousType(data, new PositionOnBoardFixFastEquities());

            Portfolio portf = null;

            for (int i = 0; i < _myPortfolios.Count; i++)
            {
                string realPortfName = _myPortfolios[i].Number.Split('_')[0];
                if (realPortfName == portfolioName)
                {
                    portf = _myPortfolios[i];
                    break;
                }
            }

            if (portf == null)
            {
                return;
            }

            PositionOnBoard newPos = new PositionOnBoard();
            newPos.PortfolioName = portf.Number;
            //newPos.ValueCurrent = baseMessage.qty.ToDecimal();
            //newPos.SecurityNameCode = baseMessage.symbol;
            //portf.SetNewPosition(newPos);

            if (PortfolioEvent != null)
            {
                PortfolioEvent(_myPortfolios);
            }
        }

        private void UpDateMyOrder(string data, string portfolioName)
        {
            //OrderFixFastEquities baseMessage =
            //JsonConvert.DeserializeAnonymousType(data, new OrderFixFastEquities());

            //Order order = ConvertToOsEngineOrder(baseMessage, portfolioName);

            //if(order == null)
            //{
            //    return;
            //}

            //lock (_sendOrdersArrayLocker)
            //{
            //    for (int i = 0; i < _sendOrders.Count; i++)
            //    {
            //        if (_sendOrders[i] == null)
            //        {
            //            continue;
            //        }

            //        if (_sendOrders[i].NumberUser == order.NumberUser)
            //        {
            //            order.TypeOrder = _sendOrders[i].TypeOrder;
            //            break;
            //        }
            //    }
            //}

            //if (MyOrderEvent != null)
            //{
            //    MyOrderEvent(order);
            //}
        }

        //private Order ConvertToOsEngineOrder(OrderFixFastEquities baseMessage, string portfolioName)
        //{
        //    Order order = new Order();

        //    order.SecurityNameCode = baseMessage.symbol;

        //    if(string.IsNullOrEmpty(baseMessage.filled) == false 
        //        && baseMessage.filled != "0")
        //    {
        //        order.Volume = baseMessage.filled.ToDecimal();
        //    }
        //    else
        //    {
        //        order.Volume = baseMessage.qty.ToDecimal();
        //    }

        //    order.PortfolioNumber = portfolioName;
            
        //    if (baseMessage.type == "limit")
        //    {
        //        order.Price = baseMessage.price.ToDecimal();
        //        order.TypeOrder = OrderPriceType.Limit;
        //    }
        //    else if (baseMessage.type == "market")
        //    {
        //        order.TypeOrder = OrderPriceType.Market;
        //    }

        //    try
        //    {
        //        order.NumberUser = Convert.ToInt32(baseMessage.comment);
        //    }
        //    catch
        //    {
        //        // ignore
        //    }

        //    order.NumberMarket = baseMessage.id;

        //    order.TimeCallBack = ConvertToDateTimeFromTimeFixFastEquitiesData(baseMessage.transTime);

        //    if (baseMessage.side == "buy")
        //    {
        //        order.Side = Side.Buy;
        //    }
        //    else
        //    {
        //        order.Side = Side.Sell;
        //    }

        //    //working - На исполнении
        //    //filled - Исполнена
        //    //canceled - Отменена
        //    //rejected - Отклонена

        //    if (baseMessage.status == "working")
        //    {
        //        order.State = OrderStateType.Activ;
        //    }
        //    else if (baseMessage.status == "filled")
        //    {
        //        order.State = OrderStateType.Done;
        //    }
        //    else if (baseMessage.status == "canceled")
        //    {
        //        lock (_changePriceOrdersArrayLocker)
        //        {
        //            DateTime now = DateTime.Now;
        //            for (int i = 0; i < _changePriceOrders.Count; i++)
        //            {
        //                if (_changePriceOrders[i].TimeChangePriceOrder.AddSeconds(2) < now)
        //                {
        //                    _changePriceOrders.RemoveAt(i);
        //                    i--;
        //                    continue;
        //                }

        //                if (_changePriceOrders[i].MarketId == order.NumberMarket)
        //                {
        //                    return null;
        //                }
        //            }
        //        }

        //        if (string.IsNullOrEmpty(baseMessage.filledQtyUnits))
        //        {
        //            order.State = OrderStateType.Cancel;
        //        }
        //        else if (baseMessage.filledQtyUnits == "0")
        //        {
        //            order.State = OrderStateType.Cancel;
        //        }
        //        else
        //        {
        //            try
        //            {
        //                decimal volFilled = baseMessage.filledQtyUnits.ToDecimal();

        //                if (volFilled > 0)
        //                {
        //                    order.State = OrderStateType.Done;
        //                }
        //                else
        //                {
        //                    order.State = OrderStateType.Cancel;
        //                }
        //            }
        //            catch
        //            {
        //                order.State = OrderStateType.Cancel;
        //            }
        //        }
        //    }
        //    else if (baseMessage.status == "rejected")
        //    {
        //        order.State = OrderStateType.Fail;
        //    }

        //    return order;
        //}

        private void UpDateMyPortfolio(string data, string portfolioName)
        {
            //FixFastEquitiesPortfolioSocket baseMessage =
            //JsonConvert.DeserializeAnonymousType(data, new FixFastEquitiesPortfolioSocket());

            //Portfolio portf = null;

            //for(int i = 0;i < _myPortfolios.Count;i++)
            //{
            //    string realPortfName = _myPortfolios[i].Number.Split('_')[0];
            //    if (realPortfName == portfolioName)
            //    {
            //        portf = _myPortfolios[i];
            //        break;
            //    }
            //}

            //if(portf == null)
            //{
            //    return;
            //}

            //portf.ValueBegin = baseMessage.portfolioLiquidationValue.ToDecimal();
            //portf.ValueCurrent = baseMessage.portfolioLiquidationValue.ToDecimal();
            //portf.Profit = baseMessage.profit.ToDecimal();
            

            //if (PortfolioEvent != null)
            //{
            //    PortfolioEvent(_myPortfolios);
            //}
        }

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        #endregion

        #region 10 Trade

        private RateGate rateGateSendOrder = new RateGate(1, TimeSpan.FromMilliseconds(350));

        private RateGate rateGateCancelOrder = new RateGate(1, TimeSpan.FromMilliseconds(350));

        private RateGate rateGateChangePriceOrder = new RateGate(1, TimeSpan.FromMilliseconds(350));

        private List<FixFastEquitiesSecuritiesAndPortfolious> _securitiesAndPortfolious = new List<FixFastEquitiesSecuritiesAndPortfolious>();

        private List<Order> _sendOrders = new List<Order>();

        private string _sendOrdersArrayLocker = "FixFastEquitiesSendOrdersArrayLocker";

        public void SendOrder(Order order)
        {
            //rateGateSendOrder.WaitToProceed();

            try
            {
                Header header = new Header
                {
                    BeginString = "FIX.4.4", //Версия FIX "FIX .4 .4»,
                    MsgType = "D", // new single order
                    SenderCompID = _MFIXTradeServerLogin,
                    TargetCompID = _MFIXTradeServerTargetCompId,
                    MsgSeqNum = _MFIXTradeMsgSeqNum++
                };


                string TradingSessionID = "TQBR"; // по-умолчанию акции
                if (order.SecurityClassCode.Contains("Bond")) TradingSessionID = "TQCB";
                if (order.SecurityClassCode.Contains("Etf")) TradingSessionID = "TQTF";

                NewOrderSingleMessage msg = new NewOrderSingleMessage()
                {
                    ClOrdID = order.NumberUser.ToString(),
                    NoPartyID = "1",
                    PartyID = _MFIXTradeClientCode,
                    Account = _MFIXTradeAccount,
                    NoTradingSessions = "1",
                    TradingSessionID = TradingSessionID,
                    Symbol = order.SecurityNameCode,
                    Side = order.Side == Side.Buy ? "1" : "2", 
                    TransactTime = DateTime.UtcNow.ToString("yyyyMMdd-HH:mm:ss.fff"),
                    OrdType = order.TypeOrder == OrderPriceType.Market ? "1" : "2", // 1 - Market, 2 - Limit
                    OrderQty = order.Volume.ToString(),                    
                    Price = order.TypeOrder == OrderPriceType.Limit ? order.Price.ToString() : "0",
                };

                //Вычисляем длину сообщения
                header.BodyLength = header.GetHeaderSize() + msg.GetMessageSize();
                //Создаем концовку сообщения
                Trailer trailer = new Trailer(header.ToString() + msg.ToString());

                //Формируем полное готовое сообщение
                string fullMessage = header.ToString() + msg.ToString() + trailer.ToString();

                _MFIXTradeSocket.Send(Encoding.UTF8.GetBytes(fullMessage));
            }
            catch (Exception exception)
            {
                SendLogMessage("Order send error " + exception.ToString(), LogMessageType.Error);
            }
        }

        //private LimitOrderFixFastEquitiesRequest GetLimitRequestObj(Order order)
        //{
        //    LimitOrderFixFastEquitiesRequest requestObj = new LimitOrderFixFastEquitiesRequest();

        //    if(order.Side == Side.Buy)
        //    {
        //        requestObj.side = "buy";
        //    }
        //    else
        //    {
        //        requestObj.side = "sell";

        //    }
        //    requestObj.type = "limit";
        //    requestObj.quantity = Convert.ToInt32(order.Volume);
        //    requestObj.price = order.Price;
        //    requestObj.comment = order.NumberUser.ToString();
        //    requestObj.instrument = new instrumentFixFastEquities();
        //    requestObj.instrument.symbol = order.SecurityNameCode;
        //    requestObj.user = new User();
        //    requestObj.user.portfolio = order.PortfolioNumber.Split('_')[0];

        //    return requestObj;
        //}

        //private MarketOrderFixFastEquitiesRequest GetMarketRequestObj(Order order)
        //{
        //    MarketOrderFixFastEquitiesRequest requestObj = new MarketOrderFixFastEquitiesRequest();

        //    if (order.Side == Side.Buy)
        //    {
        //        requestObj.side = "buy";
        //    }
        //    else
        //    {
        //        requestObj.side = "sell";
        //    }
        //    requestObj.type = "market";
        //    requestObj.quantity = Convert.ToInt32(order.Volume);
        //    requestObj.comment = order.NumberUser.ToString();
        //    requestObj.instrument = new instrumentFixFastEquities();
        //    requestObj.instrument.symbol = order.SecurityNameCode;
        //    requestObj.user = new User();
        //    requestObj.user.portfolio = order.PortfolioNumber.Split('_')[0];

        //    return requestObj;
        //}

        List<FixFastEquitiesChangePriceOrder> _changePriceOrders = new List<FixFastEquitiesChangePriceOrder>();

        private string _changePriceOrdersArrayLocker = "cangePriceArrayLocker";

        /// <summary>
        /// Order price change
        /// </summary>
        /// <param name="order">An order that will have a new price</param>
        /// <param name="newPrice">New price</param>
        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
            try
            {
                rateGateChangePriceOrder.WaitToProceed();

                if (order.TypeOrder == OrderPriceType.Market)
                {
                    SendLogMessage("Can`t change price to market order", LogMessageType.Error);
                    return;
                }
                
                string endPoint = "/commandapi/warptrans/TRADE/v2/client/orders/actions/limit/";

                endPoint += order.NumberMarket;

                RestRequest requestRest = new RestRequest(endPoint, Method.PUT);
                //requestRest.AddHeader("Authorization", "Bearer " + _apiTokenReal);
                requestRest.AddHeader("X-FixFastEquities-REQID", order.NumberUser.ToString() + GetGuid()); ;
                requestRest.AddHeader("accept", "application/json");

                //LimitOrderFixFastEquitiesRequest body = GetLimitRequestObj(order);
                //body.price = newPrice;

                int qty = Convert.ToInt32(order.Volume - order.VolumeExecute);

                if(qty <= 0 ||
                    order.State != OrderStateType.Activ)
                {
                    SendLogMessage("Can`t change price to order. It`s don`t in Activ state", LogMessageType.Error);
                    return;
                }

                //requestRest.AddJsonBody(body);
                
                //RestClient client = new RestClient(_restApiHost);

                FixFastEquitiesChangePriceOrder FixFastEquitiesChangePriceOrder = new FixFastEquitiesChangePriceOrder();
                FixFastEquitiesChangePriceOrder.MarketId = order.NumberMarket.ToString();
                FixFastEquitiesChangePriceOrder.TimeChangePriceOrder = DateTime.Now;

                lock(_changePriceOrdersArrayLocker)
                {
                    _changePriceOrders.Add(FixFastEquitiesChangePriceOrder);
                }

                //IRestResponse response = client.Execute(requestRest);

                //if (response.StatusCode == HttpStatusCode.OK)
                //{
                //    SendLogMessage("Order change price. New price: " + newPrice
                //        + "  " + order.SecurityNameCode, LogMessageType.System);

                //    order.Price = newPrice;
                //    if (MyOrderEvent != null)
                //    {
                //        MyOrderEvent(order);
                //    }

                //    //return;
                //}
                //else
                //{
                //    SendLogMessage("Change price order Fail. Status: "
                //        + response.StatusCode + "  " + order.SecurityNameCode, LogMessageType.Error);

                //    if (response.Content != null)
                //    {
                //        SendLogMessage("Fail reasons: "
                //      + response.Content, LogMessageType.Error);
                //    }
                //}

            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }               

        public void CancelOrder(Order order)
        {
            // rateGateCancelOrder.WaitToProceed();
                       
            try
            {
                Header header = new Header
                {
                    BeginString = "FIX.4.4", //Версия FIX "FIX .4 .4»,
                    MsgType = "F", // order cancel request
                    SenderCompID = _MFIXTradeServerLogin,
                    TargetCompID = _MFIXTradeServerTargetCompId,
                    MsgSeqNum = _MFIXTradeMsgSeqNum++
                };

                OrderCancelRequestMessage msg = new OrderCancelRequestMessage()
                {
                    OrigClOrdID = order.NumberUser.ToString(), 
                    OrderID = order.NumberMarket.ToString(),
                    ClOrdID = DateTime.UtcNow.Ticks.ToString(), // идентификатор заявки на снятие
                    Side = order.Side == Side.Buy ? "1" : "2",
                    TransactTime = DateTime.UtcNow.ToString("yyyyMMdd-HH:mm:ss.fff"),                    
                };

                //Вычисляем длину сообщения
                header.BodyLength = header.GetHeaderSize() + msg.GetMessageSize();
                //Создаем концовку сообщения
                Trailer trailer = new Trailer(header.ToString() + msg.ToString());

                //Формируем полное готовое сообщение
                string fullMessage = header.ToString() + msg.ToString() + trailer.ToString();

                _MFIXTradeSocket.Send(Encoding.UTF8.GetBytes(fullMessage));
            }
            catch (Exception exception)
            {
                SendLogMessage("Order cancel request error " + exception.ToString(), LogMessageType.Error);
            }
        }

        public void GetOrdersState(List<Order> orders)
        {

        }

        public void ResearchTradesToOrders(List<Order> orders)
        {

        }

        public void CancelAllOrders()
        {
            List<Order> orders = GetAllOrdersFromExchange();

            for (int i = 0; i < orders.Count;i++)
            {
                Order order = orders[i];

                if(order.State == OrderStateType.Activ)
                {
                    CancelOrder(order);
                }
            }
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            List<Order> orders = GetAllOrdersFromExchange();

            for (int i = 0; i < orders.Count; i++)
            {
                Order order = orders[i];

                if (order.State == OrderStateType.Activ
                    && order.SecurityNameCode == security.Name)
                {
                    CancelOrder(order);
                }
            }
        }

        public void GetAllActivOrders()
        {
            List<Order> orders = GetAllOrdersFromExchange();

            for(int i = 0; orders != null && i < orders.Count; i++)
            {
                if(orders[i] == null)
                {
                    continue;
                }

                if (orders[i].State != OrderStateType.Activ
                    && orders[i].State != OrderStateType.Patrial
                    && orders[i].State != OrderStateType.Pending)
                {
                    continue;
                }

                orders[i].TimeCreate = orders[i].TimeCallBack;

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(orders[i]);
                }
            }
        }

        public void GetOrderStatus(Order order)
        {
            List<Order> orders = GetAllOrdersFromExchange();

            if(orders == null ||
                orders.Count == 0)
            {
                return;
            }

            Order orderOnMarket = null;

            for(int i = 0;i < orders.Count;i++)
            {
                Order curOder = orders[i];

                if (order.NumberUser != 0
                    && curOder.NumberUser != 0
                    && curOder.NumberUser == order.NumberUser)
                {
                    orderOnMarket = curOder;
                    break;
                }

                if(string.IsNullOrEmpty(order.NumberMarket) == false 
                    && order.NumberMarket == curOder.NumberMarket)
                {
                    orderOnMarket = curOder;
                    break;
                }
            }

            if(orderOnMarket == null)
            {
                return;
            }

            if (orderOnMarket != null && 
                MyOrderEvent != null)
            {
                MyOrderEvent(orderOnMarket);
            }

            if(orderOnMarket.State == OrderStateType.Done 
                || orderOnMarket.State == OrderStateType.Patrial)
            {
                List<MyTrade> tradesBySecurity 
                    = GetMyTradesBySecurity(order.SecurityNameCode, order.PortfolioNumber.Split('_')[0]);

                if(tradesBySecurity == null)
                {
                    return;
                }

                List<MyTrade> tradesByMyOrder = new List<MyTrade>();

                for(int i = 0;i < tradesBySecurity.Count;i++)
                {
                    if (tradesBySecurity[i].NumberOrderParent == orderOnMarket.NumberMarket)
                    {
                        tradesByMyOrder.Add(tradesBySecurity[i]);
                    }
                }

                for(int i = 0;i < tradesByMyOrder.Count;i++)
                {
                    if(MyTradeEvent != null)
                    {
                        MyTradeEvent(tradesByMyOrder[i]);
                    }
                }
            }
        }

        private List<Order> GetAllOrdersFromExchange()
        {
            List<Order> orders = new List<Order>();

            if (string.IsNullOrEmpty(_MFIXTradeServerPort) == false)
            {
                List<Order> newOrders = GetAllOrdersFromExchangeByPortfolio(_MFIXTradeServerPort);

                if (newOrders != null &&
                    newOrders.Count > 0)
                {
                    orders.AddRange(newOrders);
                }
            }

            if (string.IsNullOrEmpty(_MFIXTradeCaptureServerAddress) == false)
            {
                List<Order> newOrders = GetAllOrdersFromExchangeByPortfolio(_MFIXTradeCaptureServerAddress);

                if (newOrders != null &&
                    newOrders.Count > 0)
                {
                    orders.AddRange(newOrders);
                }
            }

            if (string.IsNullOrEmpty(_MFIXTradeCaptureServerPort) == false)
            {
                List<Order> newOrders = GetAllOrdersFromExchangeByPortfolio(_MFIXTradeCaptureServerPort);

                if (newOrders != null &&
                    newOrders.Count > 0)
                {
                    orders.AddRange(newOrders);
                }
            }

            if (string.IsNullOrEmpty(_MFIXTradeAccount) == false)
            {
                List<Order> newOrders = GetAllOrdersFromExchangeByPortfolio(_MFIXTradeAccount);

                if (newOrders != null &&
                    newOrders.Count > 0)
                {
                    orders.AddRange(newOrders);
                }
            }

            return orders;
        }

        private List<Order> GetAllOrdersFromExchangeByPortfolio(string portfolio)
        {
            rateGateSendOrder.WaitToProceed();

            try
            {
                string endPoint = "/md/v2/clients/MOEX/" + portfolio + "/orders?format=Simple";

                //RestRequest requestRest = new RestRequest(endPoint, Method.GET);
                //requestRest.AddHeader("Authorization", "Bearer " + _apiTokenReal);
                //requestRest.AddHeader("accept", "application/json");

                //RestClient client = new RestClient(_restApiHost);

                //IRestResponse response = client.Execute(requestRest);


                //if (response.StatusCode == HttpStatusCode.OK)
                //{
                //    string respString = response.Content;

                //    if(respString == "[]")
                //    {
                //        return null;
                //    }
                //    else
                //    {

                //        //List<OrderFixFastEquities> orders = JsonConvert.DeserializeAnonymousType(respString, new List<OrderFixFastEquities>());

                //        //List<Order> osEngineOrders = new List<Order>();

                //        //for(int i = 0;i < orders.Count;i++)
                //        //{
                //        //    Order newOrd = ConvertToOsEngineOrder(orders[i], portfolio);

                //        //    if(newOrd == null)
                //        //    {
                //        //        continue;
                //        //    }

                //        //    osEngineOrders.Add(newOrd);
                //        //}

                //        //return osEngineOrders;
                        
                //    }
                //}
                //else if(response.StatusCode == HttpStatusCode.NotFound)
                //{
                //    return null;
                //}
                //else
                //{
                //    SendLogMessage("Get all orders request error. ", LogMessageType.Error);

                //    if (response.Content != null)
                //    {
                //        SendLogMessage("Fail reasons: "
                //      + response.Content, LogMessageType.Error);
                //    }
                //}
            }
            catch (Exception exception)
            {
                SendLogMessage("Get all orders request error." + exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private List<MyTrade> GetMyTradesBySecurity(string security, string portfolio)
        {
            try
            {
                // /md/v2/Clients/MOEX/D39004/LKOH/trades?format=Simple

                string endPoint = "/md/v2/clients/MOEX/" + portfolio + "/" + security + "/trades?format=Simple";

                //RestRequest requestRest = new RestRequest(endPoint, Method.GET);
                //requestRest.AddHeader("Authorization", "Bearer " + _apiTokenReal);
                //requestRest.AddHeader("accept", "application/json");

                //RestClient client = new RestClient(_restApiHost);

                //IRestResponse response = client.Execute(requestRest);


                //if (response.StatusCode == HttpStatusCode.OK)
                //{
                //    string respString = response.Content;

                //    if (respString == "[]")
                //    {
                //        return null;
                //    }
                //    else
                //    {
                //        //List<MyTradeFixFastEquitiesRest> allTradesJson 
                //        //    = JsonConvert.DeserializeAnonymousType(respString, new List<MyTradeFixFastEquitiesRest>());

                //        //List<MyTrade> osEngineOrders = new List<MyTrade>();

                //        //for (int i = 0; i < allTradesJson.Count; i++)
                //        //{
                //        //    MyTradeFixFastEquitiesRest tradeRest = allTradesJson[i];

                //        //    MyTrade newTrade = new MyTrade();
                //        //    newTrade.SecurityNameCode = security;
                //        //    newTrade.NumberTrade = tradeRest.id;
                //        //    newTrade.NumberOrderParent = tradeRest.orderno;
                //        //    newTrade.Volume = tradeRest.qty.ToDecimal();
                //        //    newTrade.Price = tradeRest.price.ToDecimal();
                //        //    newTrade.Time =  ConvertToDateTimeFromTimeFixFastEquitiesData(tradeRest.date);

                //        //    if (tradeRest.side == "buy")
                //        //    {
                //        //        newTrade.Side = Side.Buy;
                //        //    }
                //        //    else
                //        //    {
                //        //        newTrade.Side = Side.Sell;
                //        //    }

                //        //    osEngineOrders.Add(newTrade);
                //        //}

                //        //return osEngineOrders;

                //    }
                //}
                //else if (response.StatusCode == HttpStatusCode.NotFound)
                //{
                //    return null;
                //}
                //else
                //{
                //    SendLogMessage("Get all orders request error. ", LogMessageType.Error);

                //    if (response.Content != null)
                //    {
                //        SendLogMessage("Fail reasons: "
                //      + response.Content, LogMessageType.Error);
                //    }
                //}
            }
            catch (Exception exception)
            {
                SendLogMessage("Get all orders request error." + exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        #endregion

        #region 11 Helpers

        public long ConvertToUnixTimestamp(DateTime date)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            TimeSpan diff = date.ToUniversalTime() - origin;
            return Convert.ToInt64(diff.TotalSeconds);
        }

        private DateTime ConvertToDateTimeFromUnixFromSeconds(string seconds)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            DateTime result = origin.AddSeconds(seconds.ToDouble()).ToLocalTime();

            return result;
        }

        private DateTime ConvertToDateTimeFromUnixFromMilliseconds(string seconds)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            DateTime result = origin.AddMilliseconds(seconds.ToDouble());

            return result.ToLocalTime();
        }

        private DateTime ConvertToDateTimeFromTimeFixFastEquitiesData(string FixFastEquitiesTime)
        {
            //"time": "2018-08-07T08:40:03.445Z",

            string date = FixFastEquitiesTime.Split('T')[0];

            int year = Convert.ToInt32(date.Substring(0,4));
            int month = Convert.ToInt32(date.Substring(5, 2));
            int day = Convert.ToInt32(date.Substring(8, 2));

            string time = FixFastEquitiesTime.Split('T')[1];

            int hour = Convert.ToInt32(time.Substring(0, 2));

            if (FixFastEquitiesTime.EndsWith("+00:00"))
            {
                hour += 3;
            }

            if (FixFastEquitiesTime.EndsWith("+01:00"))
            {
                hour += 2;
            }

            if (FixFastEquitiesTime.EndsWith("+02:00"))
            {
                hour += 1;
            }
            int minute = Convert.ToInt32(time.Substring(3, 2));
            int second = Convert.ToInt32(time.Substring(6, 2));
            int ms = Convert.ToInt32(time.Substring(10, 3));

            DateTime dateTime = new DateTime(year, month, day, hour, minute, second, ms);

            return dateTime;
        }

        #endregion

        #region 12 Log

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent?.Invoke(message, messageType);
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }

    public enum FixFastEquitiesAvailableExchanges
    {
        MOEX,
        SPBX
    }

    public class FixFastEquitiesSocketSubscription
    {
        public string Guid;

        public FixFastEquitiesSubType SubType;

        public string ServiceInfo;
    }

    public class FixFastEquitiesChangePriceOrder
    {
        public string MarketId;

        public DateTime TimeChangePriceOrder;
    }

    public class FixFastEquitiesSecuritiesAndPortfolious
    {
       public string Security;

       public string Portfolio;
    }

    public enum FixFastEquitiesSubType
    {
        Trades,
        MarketDepth,
        Porfolio,
        Positions,
        Orders,
        MyTrades
    }

    public class SecurityToSave
    {
        public string Name { get; set; }
        public string NameId { get; set; }
        public string NameFull { get; set; }
        public string NameClass { get; set; }
        public decimal Lot { get; set; }
        public decimal PriceStep { get; set; }
        public int DecimalsVolume { get; set; }
        public int Decimals { get; set; }
    }
}