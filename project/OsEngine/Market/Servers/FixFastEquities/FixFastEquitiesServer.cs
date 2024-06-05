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
using System.Windows.Interop;
using System.Collections;
using OsEngine.Charts.CandleChart.Indicators;
using System.Diagnostics;


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

            Thread worker1 = new Thread(TradesIncrementalReader);
            worker1.Name = "TradesIncremenalFixFastEquities";
            worker1.Start();

            Thread worker2 = new Thread(TradesSnapshotsReader);
            worker2.Name = "TradesSnapshotsFixFastEquities";
            worker2.Start();

            Thread worker3 = new Thread(TradeMessagesReader);
            worker3.Name = "TradeMessagesReaderFixFastEquities";
            worker3.Start();

            Thread worker4 = new Thread(OrderMessagesReader);
            worker4.Name = "OrderMessagesReaderFixFastEquities";
            worker4.Start();

            Thread worker5_1 = new Thread(OrdersIncrementalReaderA);
            worker5_1.Name = "OrdersIncremenalAFixFastEquities";
            worker5_1.Start();
            Thread worker5_2 = new Thread(OrdersIncrementalReaderB);
            worker5_2.Name = "OrdersIncremenalBFixFastEquities";
            worker5_2.Start();

            Thread worker6 = new Thread(OrderSnapshotsReader);
            worker6.Name = "OrdersSnapshotsFixFastEquities";
            worker6.Start();


            Thread worker7 = new Thread(MFIXTradeServerConnection);
            worker7.Name = "MFIXTradeServerConnectionFixFastEquities";
            worker7.Start();

            Thread worker8 = new Thread(MFIXTradeCaptureServerConnection);
            worker8.Name = "MFIXTradeCaptureServerConnectionFixFastEquities";
            worker8.Start();

            Thread worker9 = new Thread(HistoricalReplayThread);
            worker9.Name = "HistoricalReplayFixFastEquities";
            worker9.Start();

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
            
            using (FileStream stream = File.OpenRead(_ConfigDir + "\\template.xml"))
            {
                _templates = loader.Load(stream);
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
            DeleteWebSocketConnection();

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
                
        private MessageTemplate[] _templates;

        private string _logLock = "locker for stream writer";
        private StreamWriter _logFile = new StreamWriter("FIXFAST_Multicast_UDP-log.txt");
        private StreamWriter _logFileXOrders = new StreamWriter("FIXFAST_Multicast_UDP-log-XOrders.txt");

        private Socket _instrumentSocketA;
        private Socket _instrumentSocketB;

        private Socket _tradesIncrementalSocketA;
        private Socket _tradesIncrementalSocketB;
        private Socket _tradesSnapshotSocketA;
        private Socket _tradesSnapshotSocketB;

        private Socket _ordersIncrementalSocketA;
        private Socket _ordersIncrementalSocketB;
        private Socket _ordersSnapshotSocketA;
        private Socket _ordersSnapshotSocketB;

        private Socket _historicalReplaySocket;
        private IPEndPoint _historicalReplayEndPoint;
        private long _missingOLRBeginSeqNo = 0;
        private long _missingOLREndSeqNo = 0;
        private bool _missingOLRData = false;
        private long _missingTLRBeginSeqNo = 0;
        private long _missingTLREndSeqNo = 0;
        private bool _missingTLRData = false;
        private bool _restoreMissingDataViaTCPOnStart = true; // включить восстановление данных по TCP при запуске в случае если следующего снэпшота ждать еще 8-15 минут

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
            return null;
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;           
        }

        

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

                    if (feedType == "Historical Replay")
                    {                        
                        string ipAddressString = connectionNode.SelectNodes("ip")[0].InnerText; // берем второй адрес
                        string portString = connectionNode.SelectSingleNode("port").InnerText;
                        
                        
                        IPAddress ipAddr = IPAddress.Parse(ipAddressString);
                        
                        _historicalReplayEndPoint = new IPEndPoint(ipAddr, int.Parse(portString));
                                              

                        continue;
                    }

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
                        // Configure the socket options
                        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, 1 * 1024 * 1024); // Set receive buffer size

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

                        if (feedType == "Orders Snapshot")
                        {
                            if (feedId == "A")
                            {
                                _ordersSnapshotSocketA = socket;
                            }

                            if (feedId == "B")
                            {
                                _ordersSnapshotSocketB = socket;
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

        private void CloseMFIXTradeConnection()
        {
            // Создаем и отправляем два запроса на отключение (Logout)
            
            //Создаем заголовк
            Header tradeServerLogoutHeader = new Header
            {
                BeginString = "FIX.4.4", //Версия FIX "FIX .4 .4»,
                MsgType = "5", //Тип сообщения Logout
                SenderCompID = _MFIXTradeServerLogin,
                TargetCompID = _MFIXTradeServerTargetCompId,
                MsgSeqNum = _MFIXTradeMsgSeqNum++
            };

            Header tradeCaptureServerLogoutHeader = new Header
            {
                BeginString = "FIX.4.4", //Версия FIX "FIX .4 .4»,
                MsgType = "5", //Тип сообщения Logout
                SenderCompID = _MFIXTradeCaptureServerLogin,
                TargetCompID = _MFIXTradeCaptureServerTargetCompId,
                MsgSeqNum = _MFIXTradeCaptureMsgSeqNum++
            };

            //Создаем сообщение Logout
            LogoutMessage logoutTServerMessageBody = new LogoutMessage();            
            LogoutMessage logoutTCServerMessageBody = new LogoutMessage();
            
            //Вычисляем длину сообщения
            tradeServerLogoutHeader.BodyLength = tradeServerLogoutHeader.GetHeaderSize() + logoutTServerMessageBody.GetMessageSize();
            tradeCaptureServerLogoutHeader.BodyLength = tradeCaptureServerLogoutHeader.GetHeaderSize() + logoutTCServerMessageBody.GetMessageSize();

            //Создаем концовку сообщения
            Trailer tradeServerTrailer = new Trailer(tradeServerLogoutHeader.ToString() + logoutTServerMessageBody.ToString());
            Trailer tradeCaptureServerTrailer = new Trailer(tradeCaptureServerLogoutHeader.ToString() + logoutTCServerMessageBody.ToString());

            //Формируем полное готовое сообщение
            string tradeServerLogoutMessage = tradeServerLogoutHeader.ToString() + logoutTServerMessageBody.ToString() + tradeServerTrailer.ToString();
            string tradeCaptureServerLogoutMessage = tradeCaptureServerLogoutHeader.ToString() + logoutTCServerMessageBody.ToString() + tradeCaptureServerTrailer.ToString();
            
            //Отправляем сообщение
            int bytesSent = _MFIXTradeSocket.Send(Encoding.UTF8.GetBytes(tradeServerLogoutMessage));
            bytesSent = _MFIXTradeCaptureSocket.Send(Encoding.UTF8.GetBytes(tradeCaptureServerLogoutMessage));            
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
                        //newSecurity.Decimals = curSec.Decimals;
                        newSecurity.Decimals = GetDecimals(newSecurity.PriceStep);
                        newSecurity.DecimalsVolume = curSec.DecimalsVolume;
                        newSecurity.State = SecurityStateType.Activ;
                        newSecurity.Exchange = "MOEX";
                        newSecurity.SecurityType = SecurityType.Stock;


                        //if (curSec.NameClass.Contains("Stock"))
                        //    newSecurity.SecurityType = SecurityType.Stock;

                        //if (curSec.NameClass.Contains("Bond"))
                        //    newSecurity.SecurityType = SecurityType.Bond;

                        //if (curSec.NameClass.Contains("Index"))
                        //    newSecurity.SecurityType = SecurityType.Index;

                        //if (curSec.NameClass.Contains("Fund"))
                        //    newSecurity.SecurityType = SecurityType.Fund;

                        switch (curSec.NameClass)
                        {
                            case "TQBR":
                                newSecurity.SecurityType = SecurityType.Stock;
                                break;

                                case "TQCB": // корп
                                case "TQOB": // офз
                                newSecurity.SecurityType = SecurityType.Bond;
                                break;

                            case "TQIF": // фонд
                            case "TQTF":
                                newSecurity.SecurityType = SecurityType.Fund;
                                break;
                        }
                        
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
                    // закрываем сокет восстановления по TCP
                    if (_historicalReplaySocket != null)
                    {
                        try
                        {
                            _historicalReplaySocket.Close();
                        }
                        catch
                        {
                            // ignore
                        }

                        _historicalReplaySocket = null;                        
                    }


                    // закрываем сокеты получения данных по инструментам
                    if (_instrumentSocketA != null)
                    {
                        try
                        {
                            _instrumentSocketA.Close();
                        }
                        catch
                        {
                            // ignore
                        }

                        try
                        {
                            _instrumentSocketB.Close();
                        }
                        catch
                        {
                            // ignore
                        }

                        _instrumentSocketA = null;
                        _instrumentSocketB = null;
                    }

                    // закрываем сокеты получения данных по сделкам
                    if (_tradesIncrementalSocketA != null)
                    {
                        try
                        {
                            _tradesIncrementalSocketA.Close();
                        }
                        catch
                        {
                            // ignore
                        }

                        try
                        {
                            _tradesIncrementalSocketB.Close();
                        }
                        catch
                        {
                            // ignore
                        }

                        try
                        {
                            _tradesSnapshotSocketA.Close();
                        }
                        catch
                        {
                            // ignore
                        }

                        try
                        {
                            _tradesSnapshotSocketB.Close();
                        }
                        catch
                        {
                            // ignore
                        }

                        _tradesIncrementalSocketA = null;
                        _tradesIncrementalSocketB = null;
                        _tradesSnapshotSocketA = null;
                        _tradesSnapshotSocketB = null;
                    }

                    // закрываем сокеты получения данных по инструментам
                    if (_instrumentSocketA != null)
                    {
                        try
                        {
                            _instrumentSocketA.Close();
                        }
                        catch
                        {
                            // ignore
                        }

                        try
                        {
                            _instrumentSocketB.Close();
                        }
                        catch
                        {
                            // ignore
                        }

                        _instrumentSocketA = null;
                        _instrumentSocketB = null;
                    }

                    // закрываем сокеты получения данных по ордерам
                    if (_ordersIncrementalSocketA != null)
                    {
                        try
                        {
                            _ordersIncrementalSocketA.Close();
                        }
                        catch
                        {
                            // ignore
                        }

                        try
                        {
                            _ordersIncrementalSocketB.Close();
                        }
                        catch
                        {
                            // ignore
                        }

                        try
                        {
                            _ordersSnapshotSocketA.Close();
                        }
                        catch
                        {
                            // ignore
                        }

                        try
                        {
                            _ordersSnapshotSocketB.Close();
                        }
                        catch
                        {
                            // ignore
                        }

                        _ordersIncrementalSocketA = null;
                        _ordersIncrementalSocketB = null;
                        _ordersSnapshotSocketA = null;
                        _ordersSnapshotSocketB = null;
                    }

                    CloseMFIXTradeConnection();
                }
            }
            catch (Exception exeption)
            {

            }
            finally
            {
                //_webSocketData = null;
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
        }

        #endregion
               

        #region 8 Security subscription
                
        List<Security> _subscribedSecurities = new List<Security>();
        Dictionary<string, List<OrdersUpdate>> _marketDepths = new Dictionary<string, List<OrdersUpdate>>();
        List<string> _TradingSessionIDs = new List<string>() { 
            "TQBR", // акции и ДР 
            "TQCB", // корп облигации
            "TQOB", // гос облигации
            "TQTF", // etf
            "TQIF", // паи
        };

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
                _marketDepths.Add(security.Name, new List<OrdersUpdate>());
            }
        }

        #endregion

        #region 9 WebSocket parsing the messages
                
        private DateTime _lastInstrumentDefinitionsTime = DateTime.MinValue;
        private bool _allSecuritiesLoaded = false;
        private long _totNumReports = 0;

        // очереди сообщений, которые прилетают из FIX/FAST Multicast UPD соединений
        private ConcurrentQueue<OpenFAST.Message> _tradeMessages = new ConcurrentQueue<OpenFAST.Message>();
        private ConcurrentQueue<OpenFAST.Message> _orderMessages = new ConcurrentQueue<OpenFAST.Message>();

        private void InstrumentDefinitionsReader()
        {
            byte[] buffer = new byte[4096];
            
            List<long> snapshotIds = new List<long>();
            List<Security> securities = new List<Security>();

            Thread.Sleep(1000);

            OpenFAST.Context context = null;

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

                    if (context == null)
                    {
                        context = new OpenFAST.Context();
                        foreach (MessageTemplate tmplt in _templates)
                        {
                            context.RegisterTemplate(int.Parse(tmplt.Id), tmplt);
                        }
                    }

                    // читаем из потоков А и B
                    // либо сразу обрабатываем либо перемещаем в очередь для разбора
                    for (int s = 0; s < 2; s++)
                    { 
                        int length = s == 0 ? _instrumentSocketA.Receive(buffer) : _instrumentSocketB.Receive(buffer);

                        using (MemoryStream stream = new MemoryStream(buffer, 4, length))
                        {
                            FastDecoder decoder = new FastDecoder(context, stream);
                            OpenFAST.Message msg = decoder.ReadMessage();

                            string msgType = msg.GetString("MessageType");
                            long msgSeqNum = int.Parse(msg.GetString("MsgSeqNum"));
                            
                            if (msgType == "d") /// security definition
                            {
                                _lastInstrumentDefinitionsTime = DateTime.UtcNow;
                                _totNumReports = msg.GetLong("TotNumReports"); // общее число "бумаг" (возможны дубли)

                                if (snapshotIds.FindIndex(nmb => nmb == msgSeqNum) != -1)
                                {
                                    if (snapshotIds.Count == _totNumReports)
                                    {
                                        _securities = securities;
                                        _allSecuritiesLoaded = true;
                                    }

                                    continue;
                                }
                                
                                snapshotIds.Add(msgSeqNum);
                                if (snapshotIds.Count % 1000 == 0)
                                {
                                    SendLogMessage($"Loading securities data: {snapshotIds.Count}/{_totNumReports}", LogMessageType.System);
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

                                string TradingSessionID = "TQBR"; // по-умолчанию акции
                                bool isInKnownTradingSession = false;
                                if (msg.IsDefined("MarketSegmentGrp"))
                                {
                                    SequenceValue secVal = msg.GetValue("MarketSegmentGrp") as SequenceValue;

                                    for (int i = 0; i < secVal.Length; i++)
                                    {
                                        GroupValue groupVal = secVal[i] as GroupValue;

                                        if (groupVal.IsDefined("RoundLot"))
                                        {
                                            lot = groupVal.GetString("RoundLot");
                                        }
                                        
                                        if (groupVal.IsDefined("TradingSessionRulesGrp"))
                                        {
                                            SequenceValue secVal2 = groupVal.GetValue("TradingSessionRulesGrp") as SequenceValue;

                                            for (int j = 0; j < secVal2.Length; j++)
                                            {
                                                GroupValue trdSessionGrp = secVal2[j] as GroupValue;                                                                                             

                                                TradingSessionID = trdSessionGrp.GetString("TradingSessionID");

                                                if (_TradingSessionIDs.Contains(TradingSessionID))
                                                {
                                                    isInKnownTradingSession = true;
                                                    break;
                                                }
                                            }                                                                                    
                                        }                                      
                                    }
                                }

                                if (!isInKnownTradingSession) // бумага не торгуется ни в одном из "известных" режимов торгов (board)
                                    continue;

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
                                    case "10":
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

                                    default:
                                        newSecurity.SecurityType = SecurityType.None;
                                        break;
                                }

                                newSecurity.NameClass = TradingSessionID; // newSecurity.SecurityType.ToString() + " " + currency;
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
                                newSecurity.Decimals = GetDecimals(newSecurity.PriceStep);
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

        private void TradesIncrementalReader()
        {
            byte[] buffer = new byte[4096];

            OpenFAST.Context context = null;                               

            Thread.Sleep(1000);


            while (true)
            {
                try
                {
                    if (_tradesIncrementalSocketA == null || _tradesIncrementalSocketB == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (context == null)
                    {
                        context = new OpenFAST.Context();
                        foreach (MessageTemplate tmplt in _templates)
                        {
                            context.RegisterTemplate(int.Parse(tmplt.Id), tmplt);
                        }
                    }

                    // читаем из потоков А и B
                    // либо сразу обрабатываем либо перемещаем в очередь для разбора
                    for (int s = 0; s < 2; s++)
                    {
                        int length = s == 0 ? _tradesIncrementalSocketA.Receive(buffer) : _tradesIncrementalSocketB.Receive(buffer);

                        using (MemoryStream stream = new MemoryStream(buffer, 4, length))
                        {
                            FastDecoder decoder = new FastDecoder(context, stream);
                            OpenFAST.Message msg = decoder.ReadMessage();
                            
                            long msgSeqNum = msg.GetLong("MsgSeqNum");

                            // проверяем нет ли сообщения с таким номером
                            if (_tradesIncremental.ContainsKey(msgSeqNum))
                            {
                                if (_tradesIncremental[msgSeqNum] == msg)                                   
                                    continue; // такое сообщение уже есть
                                else 
                                    _tradesIncremental[msgSeqNum] = msg;
                            } else
                            {
                                _tradesIncremental.Add(msgSeqNum, msg);

                                if (_tradesIncremental.Count % 100 == 0)
                                {
                                    //SendLogMessage($"TradesIncremental + msgSeqNum = {msgSeqNum}. Total: " + tradesIncremental.Count, LogMessageType.System);
                                }
                            }

                            _tradeMessages.Enqueue(msg);
                        }
                    }

                    // проверяем пропуски данных
                    List<long> keys = _tradesIncremental.Keys.ToList();
                    
                    if (keys.Count < 2)
                        continue;

                    keys.Sort();
                    long beginMsgSeqNum = 0; // начало пропущенных данных
                    long endMsgSeqNum = 0; // конец пропущенных данных

                    for (int i = 1; i < keys.Count - 1; i++)
                    {
                        if (keys[i] != keys[i - 1] + 1)
                        {
                            if (beginMsgSeqNum == 0)
                                beginMsgSeqNum = keys[i - 1] + 1;

                            endMsgSeqNum = keys[i] - 1;
                        }
                    }

                    _missingTLREndSeqNo = endMsgSeqNum;
                    _missingTLRBeginSeqNo = beginMsgSeqNum;                  
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void WriteLog(string message, string source)
        {
            lock (_logLock)
            {
                _logFile.WriteLine($"{DateTime.Now} {source}: {message}");
            }
        }

        private void TradesSnapshotsReader()
        {
            byte[] buffer = new byte[4096];

          
            // накапливаем инкрементальные обновления по всем инструментам, чтобы не принимать лишние
            Dictionary<long, OpenFAST.Message> tradesSnapshot = new Dictionary<long, OpenFAST.Message>();


            OpenFAST.Context context = null;

            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (_tradesSnapshotSocketA == null || _tradesSnapshotSocketB == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (context == null)
                    {
                        context = new OpenFAST.Context();
                        foreach (MessageTemplate tmplt in _templates)
                        {
                            context.RegisterTemplate(int.Parse(tmplt.Id), tmplt);
                        }
                    }

                    // читаем из потоков А и B
                    // либо сразу обрабатываем либо перемещаем в очередь для разбора

                    for (int s = 0; s < 2; s++)
                    {
                        int length = s == 0 ? _tradesSnapshotSocketA.Receive(buffer) : _tradesSnapshotSocketB.Receive(buffer);

                        using (MemoryStream stream = new MemoryStream(buffer, 4, length))
                        {
                            FastDecoder decoder = new FastDecoder(context, stream);
                            OpenFAST.Message msg = decoder.ReadMessage();

                            long msgSeqNum = msg.GetLong("MsgSeqNum");

                            if (msgSeqNum == 1 && tradesSnapshot.Count >= _totNumReports)
                            {
                                WriteLog($"MsgSeqNum = {msgSeqNum}. Total: {tradesSnapshot.Count}/{_totNumReports}. TradesSnapshot cleared", "TradesSnapshotsReader");
                                tradesSnapshot.Clear();
                            }

                            // проверяем нет ли сообщения с таким номером
                            if (tradesSnapshot.ContainsKey(msgSeqNum))
                            {
                                if (tradesSnapshot[msgSeqNum] == msg)
                                    continue; // такое сообщение уже есть
                                else
                                    tradesSnapshot[msgSeqNum] = msg;
                            }
                            else
                            {
                                tradesSnapshot.Add(msgSeqNum, msg);

                                if (tradesSnapshot.Count % 1000 == 0)
                                {
                                    WriteLog($"TradesSnapshot +1 msgSeqNum={msgSeqNum}. Total: " + tradesSnapshot.Count, "TradesSnapshotsReader");
                                }
                            }

                            _tradeMessages.Enqueue(msg);
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

        public class UpdateComparer : IComparer<GroupValue>
        {
            public int Compare(GroupValue x, GroupValue y)
            {
                return x.GetLong("RptSeq").CompareTo(y.GetLong("RptSeq"));
            }
        }

        public class TradeComparer : IComparer<Trade>
        {
            public int Compare(Trade x, Trade y)
            {
                return long.Parse(x.Id).CompareTo(long.Parse(y.Id));
            }
        }

        class SecuritySnapshot
        {
            public bool IsComplete = false;
            public int RptSeq = 0;
            public long LastMsgSeqNumProcessed = 0;
            public bool RouteFirstReceived = false;
            public bool LastFragmentReceived = false;
            public List<Trade> Trades = new List<Trade>();

            public void AddTrade(Trade trade)
            {
                // check if trade with such id already exists
                if (Trades.Any(t => t.Id == trade.Id))
                {
                    return;
                }

                // Find the index where the new item should be inserted
                int index = Trades.BinarySearch(trade, new TradeComparer());

                // If the item is not found (index is negative)
                if (index < 0)
                {
                    index = ~index; // Convert negative index to positive
                }
                
                // Insert the new item
                Trades.Insert(index, trade);                
            }
        };
               

        class OrdersSnapshot
        {
            public bool IsComplete = false;
            public int RptSeq = 0;  // Номер обновления инструмента. Соответствует RptSeq(83) в последнем сообщении Market Data - Incremental Refresh(X), которое было опубликовано кмоменту формирования снепшота по инструменту.
            public long LastMsgSeqNumProcessed = 0; // Номер последнего обработанного сообщения.
            public bool RouteFirstReceived = false;
            public bool LastFragmentReceived = false;
            public List<OrdersUpdate> Data = new List<OrdersUpdate>();

            public void AddData(OrdersUpdate update)
            {
                // check if trade with such id already exists
                if (Data.Any(t => t.Id == update.Id))
                {
                    return;
                }

                // Find the index where the new item should be inserted
                int index = Data.BinarySearch(update, new OrderBookComparer());

                // If the item is not found (index is negative)
                if (index < 0)
                {
                    index = ~index; // Convert negative index to positive
                }

                // Insert the new item
                Data.Insert(index, update);
            }
        };

        private string _ordersIncrementalLocker = "OrdersIncrementalLocker";
        private Dictionary<long, OpenFAST.Message> _ordersIncremental = new Dictionary<long, OpenFAST.Message>();
        // накапливаем все сообщения из снэпшотов 
        private Dictionary<long, OpenFAST.Message> _tradesIncremental = new Dictionary<long, OpenFAST.Message>();

        private void OrdersIncrementalReaderA()
        {
            byte[] buffer = new byte[4096];

            OpenFAST.Context context = null;

            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (_ordersIncrementalSocketA == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (context == null)
                    {
                        context = new OpenFAST.Context();
                        foreach (MessageTemplate tmplt in _templates)
                        {
                            context.RegisterTemplate(int.Parse(tmplt.Id), tmplt);
                        }
                    }

                    // читаем из потоков А и B
                    // либо сразу обрабатываем либо перемещаем в очередь для разбора

                    int length = _ordersIncrementalSocketA.Receive(buffer);

                    using (MemoryStream stream = new MemoryStream(buffer, 4, length))
                    {
                        FastDecoder decoder = new FastDecoder(context, stream);
                        OpenFAST.Message msg = decoder.ReadMessage();

                        long msgSeqNum = msg.GetLong("MsgSeqNum");
                        bool needToEnqueue = false;

                        lock (_ordersIncrementalLocker)
                        {
                            // проверяем нет ли сообщения с таким номером
                            if (_ordersIncremental.ContainsKey(msgSeqNum))
                            {
                                continue; // такое сообщение уже есть
                            }
                            else
                            {
                                _ordersIncremental.Add(msgSeqNum, msg);
                            
                                if (_ordersIncremental.Count % 100 == 0)
                                {
                                    //SendLogMessage($"OrdersIncremental + msgSeqNum = {msgSeqNum}. Total: " + ordersIncremental.Count, LogMessageType.System);
                                }
                            }
                        }
                                                
                        _orderMessages.Enqueue(msg);                        
                    }

                    // проверяем пропуски данных
                    List<long> keys = _ordersIncremental.Keys.ToList();
                    

                    if (keys.Count < 2)
                        continue;

                    keys.Sort();
                    long beginMsgSeqNum = 0; // начало пропущенных данных
                    long endMsgSeqNum = 0; // конец пропущенных данных

                    for (int i = 1; i < keys.Count - 1; i++)
                    {
                        if (keys[i] != keys[i - 1] + 1)
                        {
                            if (beginMsgSeqNum == 0)
                                beginMsgSeqNum = keys[i - 1] + 1;

                            endMsgSeqNum = keys[i] - 1;
                        }
                    }

                    _missingOLREndSeqNo = endMsgSeqNum;
                    _missingOLRBeginSeqNo = beginMsgSeqNum;
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void OrdersIncrementalReaderB()
        {
            byte[] buffer = new byte[4096];

            OpenFAST.Context context = null;

            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (_ordersIncrementalSocketB == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (context == null)
                    {
                        context = new OpenFAST.Context();
                        foreach (MessageTemplate tmplt in _templates)
                        {
                            context.RegisterTemplate(int.Parse(tmplt.Id), tmplt);
                        }
                    }

                    // читаем из потоков А и B
                    // либо сразу обрабатываем либо перемещаем в очередь для разбора

                    int length = _ordersIncrementalSocketB.Receive(buffer);

                    using (MemoryStream stream = new MemoryStream(buffer, 4, length))
                    {
                        FastDecoder decoder = new FastDecoder(context, stream);
                        OpenFAST.Message msg = decoder.ReadMessage();

                        long msgSeqNum = msg.GetLong("MsgSeqNum");
                        bool needToEnqueue = false;

                        lock (_ordersIncrementalLocker)
                        {
                            // проверяем нет ли сообщения с таким номером
                            if (_ordersIncremental.ContainsKey(msgSeqNum))
                            {
                               
                                    continue; // такое сообщение уже есть
                               
                            }
                            else
                            {
                                _ordersIncremental.Add(msgSeqNum, msg);
                                if (_ordersIncremental.Count % 100 == 0)
                                {
                                    //SendLogMessage($"OrdersIncremental + msgSeqNum = {msgSeqNum}. Total: " + ordersIncremental.Count, LogMessageType.System);
                                }
                            }
                        }

                        
                        _orderMessages.Enqueue(msg);
                    }

                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void OrderSnapshotsReader()
        {
            byte[] buffer = new byte[4096];

            // накапливаем инкрементальные обновления по всем инструментам, чтобы не принимать лишние
            Dictionary<long, OpenFAST.Message> orderSnapshots = new Dictionary<long, OpenFAST.Message>();

            OpenFAST.Context context = null;

            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (_ordersSnapshotSocketA == null || _ordersSnapshotSocketB == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (context == null)
                    {
                        context = new OpenFAST.Context();
                        foreach (MessageTemplate tmplt in _templates)
                        {
                            context.RegisterTemplate(int.Parse(tmplt.Id), tmplt);
                        }
                    }

                    // читаем из потоков А и B
                    // либо сразу обрабатываем либо перемещаем в очередь для разбора

                    for (int s = 0; s < 2; s++)
                    {
                        int length = s == 0 ? _ordersSnapshotSocketA.Receive(buffer) : _ordersSnapshotSocketB.Receive(buffer);

                        using (MemoryStream stream = new MemoryStream(buffer, 4, length))
                        {
                            FastDecoder decoder = new FastDecoder(context, stream);
                            OpenFAST.Message msg = decoder.ReadMessage();

                            long msgSeqNum = msg.GetLong("MsgSeqNum");
                            
                            if (msgSeqNum == 1)
                            {
                                WriteLog($"MsgSeqNum = {msgSeqNum}. Total: {orderSnapshots.Count}/{_totNumReports}. Order Snapshots cleared", "OrderSnapshotsReader");
                                SendLogMessage($"MsgSeqNum={msgSeqNum}/{_totNumReports}. Total: {orderSnapshots.Count}. Order Snapshots cleared - collecting new cycle", LogMessageType.System);
                                orderSnapshots.Clear();
                            }

                            // проверяем нет ли сообщения с таким номером
                            if (orderSnapshots.ContainsKey(msgSeqNum))
                            {                                                                    
                                 continue; // такое сообщение уже есть                               
                            }
                            else
                            {
                                orderSnapshots.Add(msgSeqNum, msg);

                                if (orderSnapshots.Count % 1000 == 0)
                                {
                                    SendLogMessage($"OrderSnapshots MsgSeqNum={msgSeqNum}/{_totNumReports}. Total: {orderSnapshots.Count}.", LogMessageType.System);
                                    WriteLog($"OrderSnapshots +1000 msgSeqNum={msgSeqNum}. Total: {orderSnapshots.Count}/{_totNumReports}", "OrderSnapshotsReader");
                                }
                            }

                            _orderMessages.Enqueue(msg);
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

        private void HistoricalReplayThread()
        {
            byte[] buffer = new byte[4096];

            string currentFeed = "OLR";

            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (_historicalReplayEndPoint == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    // проверяем нужно ли восстанавливать какие-либо данные
                    if (_missingOLRBeginSeqNo != 0 && _missingOLRData && !_ordersIncremental.ContainsKey(_missingOLRBeginSeqNo))
                    {
                        currentFeed = "OLR"; // восстанавливаем данные из потока ордеров

                        SendLogMessage($"Trying to recover missing OLR SeqNo: {_missingOLRBeginSeqNo}-{_missingOLREndSeqNo}", LogMessageType.System);
                    } else
                    if (_missingTLRBeginSeqNo != 0 && _missingTLRData && !_tradesIncremental.ContainsKey(_missingTLRBeginSeqNo))
                    {
                        currentFeed = "TLR"; // восстанавливаем данные из потока сделок
                        SendLogMessage($"Trying to recover missing TLR SeqNo: {_missingTLRBeginSeqNo}-{_missingTLREndSeqNo}", LogMessageType.System);
                    } else
                    {
                        // отдыхаем полсекунды, ничего не надо восстанавливать
                        Thread.Sleep(500);
                        continue;
                    }

                    // init historical replay socket
                    _historicalReplaySocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                    // начинаем общение с 1
                    int msgSeqNum = 1;

                    // 1. Соединяемся с потоком восстановления по TCP
                    _historicalReplaySocket.Connect(_historicalReplayEndPoint);

                    // 2. Отправляем запрос на подключение к потоку восстановления
                    FASTHeader header = new FASTHeader
                    {
                        BeginString = "FIXT.1.1",
                        MsgType = "A", //Тип сообщения на установку сессии
                        SenderCompID = "OsEngine",
                        TargetCompID = "MOEX", // TODO: id фирмы брокера (?)
                        MsgSeqNum = msgSeqNum++
                    };
                                        
                    FASTLogonMessage logonMessageBody = new FASTLogonMessage
                    {
                        Username = "user0",
                        Password = "pass0",
                    };

                    //Вычисляем длину сообщения
                    header.BodyLength = header.GetHeaderSize() + logonMessageBody.GetMessageSize();
                    
                    //Создаем концовку сообщения
                    Trailer logonTrailer = new Trailer(header.ToString() + logonMessageBody.ToString());
                    
                    //Формируем полное готовое сообщение
                    string logonMessage = header.ToString() + logonMessageBody.ToString() + logonTrailer.ToString();

                    //Отправляем сообщение
                    int bytesSent = _historicalReplaySocket.Send(Encoding.UTF8.GetBytes(logonMessage));

                    // получаем ответ - должен быть Logon
                    int length = _historicalReplaySocket.Receive(buffer, 4, SocketFlags.None);
                    ArraySegment<byte> segment = new ArraySegment<byte>(buffer, 0, 4);
                    // Convert the segment to an integer
                    length = BitConverter.ToInt32(segment.Array, segment.Offset);

                    length = _historicalReplaySocket.Receive(buffer, length, SocketFlags.None);

                    using (MemoryStream stream = new MemoryStream(buffer, 0, length))
                    {

                        OpenFAST.Context context = new OpenFAST.Context();
                        foreach (MessageTemplate tmplt in _templates)
                        {
                            context.RegisterTemplate(int.Parse(tmplt.Id), tmplt);
                        }

                        FastDecoder decoder = new FastDecoder(context, stream);
                        OpenFAST.Message msg = decoder.ReadMessage();

                        string messageType = msg.GetString("MessageType");

                        if (messageType == "5")
                        {
                            string Text = msg.GetString("Text");
                            SendLogMessage($"Bad authorization: {Text}", LogMessageType.Error);
                            return;
                        }
                        

                        if (messageType != "A")
                        {
                            SendLogMessage($"Bad message type: {messageType}", LogMessageType.Error);
                            continue;
                        }
                    }

                    // 3. Отправляем MarketDataRequest (V)
                    header = new FASTHeader
                    {
                        BeginString = "FIXT.1.1",
                        MsgType = "V", 
                        SenderCompID = "OsEngine",
                        TargetCompID = "MOEX", // 
                        MsgSeqNum = msgSeqNum++
                    };

                    MarketDataRequestMessage marketDataRequest = new MarketDataRequestMessage
                    {
                        ApplID = currentFeed,
                        ApplBegSeqNum = currentFeed == "OLR" ? _missingOLRBeginSeqNo.ToString() : _missingTLRBeginSeqNo.ToString(),
                        ApplEndSeqNum = currentFeed == "OLR" ? _missingOLREndSeqNo.ToString() : _missingTLREndSeqNo.ToString(),
                    };

                    //Вычисляем длину сообщения
                    header.BodyLength = header.GetHeaderSize() + marketDataRequest.GetMessageSize();

                    //Создаем концовку сообщения
                    Trailer mdRequestTrailer = new Trailer(header.ToString() + marketDataRequest.ToString());

                    //Формируем полное готовое сообщение
                    string marketDataRequestMessage = header.ToString() + marketDataRequest.ToString() + mdRequestTrailer.ToString();

                    //Отправляем сообщение
                    bytesSent = _historicalReplaySocket.Send(Encoding.UTF8.GetBytes(marketDataRequestMessage));
                                      

                    while (true) // начинаем цикл приема сообщений
                    {                       

                        length = _historicalReplaySocket.Receive(buffer, 4, SocketFlags.None);
                        segment = new ArraySegment<byte>(buffer, 0, 4);
                        // Convert the segment to an integer
                        length = BitConverter.ToInt32(segment.Array, segment.Offset);
                        length = _historicalReplaySocket.Receive(buffer, length, SocketFlags.None);

                        using (MemoryStream stream = new MemoryStream(buffer, 0, length))
                        {

                            OpenFAST.Context context = new OpenFAST.Context();
                            foreach (MessageTemplate tmplt in _templates)
                            {
                                context.RegisterTemplate(int.Parse(tmplt.Id), tmplt);
                            }

                            FastDecoder decoder = new FastDecoder(context, stream);
                            OpenFAST.Message msg = null;
                            try
                            {
                                msg = decoder.ReadMessage();
                            }
                            catch (Exception ex)
                            {
                                // Иногда просто что-то глючит, но он все равно читает сообщение
                            }

                            if (msg == null)
                            {
                                SendLogMessage("Failed to read message from historical replay server", LogMessageType.Error);
                                
                                // закрываем сокет, а то нас сервак забанит
                                _historicalReplaySocket.Close();
                                break;
                            }

                            string messageType = msg.GetString("MessageType");

                            SendLogMessage($"TCP recovery received MessageType: {messageType} for {currentFeed}", LogMessageType.System);

                            if (messageType == "X")
                            {
                                long MsgSeqNum = msg.GetLong("MsgSeqNum");

                                if (currentFeed == "OLR")
                                {
                                    _orderMessages.Enqueue(msg);
                                    _ordersIncremental[MsgSeqNum] = msg;
                                }
                                else
                                {
                                    _tradeMessages.Enqueue(msg);
                                    _tradesIncremental[MsgSeqNum] = msg;
                                }
                            } 
                            else if (messageType == "5") // Logout
                            {
                                SendLogMessage($"Historical Replay server Logout. {msg.GetString("Text")}", LogMessageType.System);

                                //отвечаем серверу Logout
                                header = new FASTHeader
                                {
                                    BeginString = "FIXT.1.1",
                                    MsgType = "5", // logout
                                    SenderCompID = "OsEngine",
                                    TargetCompID = "MOEX", // 
                                    MsgSeqNum = msgSeqNum++
                                };

                                LogoutMessage logoutMessageBody = new LogoutMessage
                                {
                                    Text = "Logging out"
                                };

                                //Вычисляем длину сообщения
                                header.BodyLength = header.GetHeaderSize() + logoutMessageBody.GetMessageSize();

                                //Создаем концовку сообщения
                                Trailer logoutTrailer = new Trailer(header.ToString() + logoutMessageBody.ToString());

                                //Формируем полное готовое сообщение
                                string logoutMessage = header.ToString() + logoutMessageBody.ToString() + logoutTrailer.ToString();

                                //Отправляем сообщение
                                bytesSent = _historicalReplaySocket.Send(Encoding.UTF8.GetBytes(logoutMessage));

                                if (currentFeed == "OLR")
                                {
                                    _missingOLRBeginSeqNo = 0;
                                    _missingOLREndSeqNo = 0;
                                    _missingOLRData = false;
                                }
                                else
                                {
                                    _missingTLRBeginSeqNo = 0;
                                    _missingTLREndSeqNo = 0;
                                    _missingTLRData = false;
                                }
                                
                                // закрываем сокет
                                _historicalReplaySocket.Close();

                                break;
                            } else if (messageType == "0") // heartbeat
                            {
                                continue;
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


        private string TradeToString(GroupValue groupVal)
        {            
            string TradingSessionID = groupVal.GetString("TradingSessionID") ?? "N/A";                       
            string TradingSessionSubID = groupVal.GetString("TradingSessionSubID") ?? "N";
            string MDEntryType = groupVal.GetString("MDEntryType");
            int RptSeq = groupVal.IsDefined("RptSeq") ? groupVal.GetInt("RptSeq") : -1;
            string symbol = groupVal.GetString("Symbol") ?? "N/A";
            string side = groupVal.GetString("OrderSide") ?? "_";
            string price = groupVal.GetString("MDEntryPx") ?? "_";
            string size = groupVal.GetString("MDEntrySize") ?? "_";
            string time = groupVal.GetString("MDEntryTime") ?? "00:00:00";


            return $"[RptSeq={RptSeq}] {symbol}, {side}, {price} x {size} @ t={time} TradingSessionID{TradingSessionID} TradingSessionSubID={TradingSessionSubID} MDEntryType={MDEntryType}";
        }

        private void TradeMessagesReader()
        {            
            Thread.Sleep(1000);

            // сначала накапливаем трейды из снэпшотов
            // как только получили все трейды из снэпшотов - отправляем их в обработчик
            Dictionary<string, SecuritySnapshot> tradeSnapshots = new Dictionary<string, SecuritySnapshot>();
            
            // после обработки всех трейдов из снэпшотов обрабатываем трейды из инкрементальных обновлений
            Dictionary<string, List<GroupValue>> tradesFromIncremental = new Dictionary<string, List<GroupValue>>();

            // последнее обработанное сообщение из инкрементальных обновлений
            Dictionary<string, int> lastRptSeqProcessed = new Dictionary<string, int>();


            //bool faketradesnotloaded = true;

            while (true)
            {            
                try
                {
                    if (_tradeMessages.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    OpenFAST.Message msg;

                    _tradeMessages.TryDequeue(out msg);

                    if (msg == null)
                    {
                        continue;
                    }

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

                                string TradingSessionID = groupVal.GetString("TradingSessionID");
                                
                                WriteLog($"msgType=X " + TradeToString(groupVal), "TradeMessagesReader");

                                if (!_TradingSessionIDs.Contains(TradingSessionID))
                                    continue;

                                string TradingSessionSubID = groupVal.GetString("TradingSessionSubID") ?? "N";
                                if (TradingSessionSubID != "N")
                                {
                                    continue;
                                }

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

                                string MDEntryType = groupVal.GetString("MDEntryType");
                                int RptSeq = groupVal.GetInt("RptSeq");

                                //if (MDEntryType == "z")
                                //{                                   
                                    if (!tradesFromIncremental.ContainsKey(name))
                                    {
                                        tradesFromIncremental.Add(name, new List<GroupValue>());
                                    }
                                                                        
                                    // check if trade with such id already exists
                                    if (tradesFromIncremental[name].Any(t => t.GetInt("RptSeq") == RptSeq))
                                    {
                                        continue;
                                    }

                                    // Find the index where the new item should be inserted
                                    int index = tradesFromIncremental[name].BinarySearch(groupVal, new UpdateComparer());

                                    // If the item is not found (index is negative)
                                    if (index < 0)
                                    {
                                        index = ~index; // Convert negative index to positive
                                    }

                                    // Insert the new item
                                    tradesFromIncremental[name].Insert(index, groupVal);                                                                        
                                //}

                                //if (MDEntryType == "J") // Empty Book
                                //{
                                //    tradeSnapshots[name].Trades.Clear();
                                //    tradeSnapshots[name].IsComplete = true;
                                //    tradeSnapshots[name].RptSeq = 0;
                                //    tradeSnapshots[name].LastFragmentReceived = true;
                                //    tradeSnapshots[name].RouteFirstReceived = true;
                                //    lastRptSeqProcessed[name] = RptSeq;
                                //    tradesFromIncremental[name].Clear();
                                //}
                            }
                        }
                    }

                    // Обрабатываем снэпшот
                    if (msgType == "W") /// Market Data - Snapshot/Full Refresh (W)
                    {
                        //_lastInstrumentDefinitionsTime = DateTime.UtcNow;                                                          
                        string name = msg.GetString("Symbol");
                        string TradingSessionID = msg.GetString("TradingSessionID");
                        long LastMsgSeqNumProcessed = msg.GetLong("LastMsgSeqNumProcessed");
                                              
                        if (!_TradingSessionIDs.Contains(TradingSessionID))
                            continue;

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

                        int RptSeq = msg.GetInt("RptSeq");

                        if (!tradeSnapshots.ContainsKey(name))
                        {
                            tradeSnapshots.Add(name, new SecuritySnapshot());
                            // сохраняем, чтобы знать, какие данные есть в снэпшоте
                            tradeSnapshots[name].RptSeq = RptSeq;
                            tradeSnapshots[name].LastMsgSeqNumProcessed = LastMsgSeqNumProcessed;
                        }
                        else 
                        if (tradeSnapshots[name].RptSeq != RptSeq)
                        {
                            SendLogMessage($"Trade Snapshot update for {name} is not complete. RptSeq={RptSeq} TradeSnapshots[" + name + "].RptSeq = " + tradeSnapshots[name].RptSeq, LogMessageType.System);
                            tradeSnapshots[name].RptSeq = RptSeq;
                            tradeSnapshots[name].LastMsgSeqNumProcessed = LastMsgSeqNumProcessed;
                            tradeSnapshots[name].IsComplete = false;
                            tradeSnapshots[name].LastFragmentReceived = false;
                            tradeSnapshots[name].RouteFirstReceived = false;
                        }

                        string LastFragment = msg.GetString("LastFragment"); // 1 - сообщение последнее, снэпшот сформирован
                        string RouteFirst = msg.GetString("RouteFirst"); // 1 - сообщение первое, формирующее снэпшот по инструменту


                        if (msg.IsDefined("GroupMDEntries"))
                        {
                            SequenceValue secVal = msg.GetValue("GroupMDEntries") as SequenceValue;
                            WriteLog($"W-Trade {name} (RptSeq={RptSeq}): with {secVal.Length} entries. Total trade entries: {tradeSnapshots[name].Trades.Count}", "TradeMessagesReader");

                            for (int i = 0; i < secVal.Length; i++)
                            {
                                GroupValue groupVal = secVal[i] as GroupValue;

                                string MDEntryType = groupVal.GetString("MDEntryType");

                                string TradingSessionSubID = groupVal.GetString("TradingSessionSubID") ?? "N";
                                //if (TradingSessionSubID != "N")
                                //{
                                //    continue;
                                //}

                                if (MDEntryType == "z")
                                {
                                    Trade trade = new Trade();
                                    trade.SecurityNameCode = name;
                                    trade.Price = groupVal.GetString("MDEntryPx").ToDecimal();
                                                                    
                                    if (name == "SBER" && trade.Price < 200)
                                    {
                                        int xxxxxx = 0;
                                    }

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
                                                                        
                                    //NewTradesEvent(trade);

                                    tradeSnapshots[name].AddTrade(trade);
                                }

                                if (MDEntryType == "J") // Empty Book
                                {
                                    tradeSnapshots[name].Trades.Clear();
                                    tradeSnapshots[name].IsComplete = true;
                                    tradeSnapshots[name].RptSeq = 0;
                                    tradeSnapshots[name].LastFragmentReceived = true;
                                    tradeSnapshots[name].RouteFirstReceived = true;
                                    lastRptSeqProcessed[name] = RptSeq;
                                    tradesFromIncremental[name].Clear();
                                }
                            }
                        }

                        bool snapshotAlreadyComplete = tradeSnapshots[name].IsComplete;
                                               
                        if (!snapshotAlreadyComplete)
                        {
                            if (LastFragment == "1")
                            {
                                SendLogMessage($"{name} received trades snapshot with LastFragment=Y - {tradeSnapshots[name].Trades.Count} entries", LogMessageType.System);
                                tradeSnapshots[name].LastFragmentReceived = true;

                                if (tradeSnapshots[name].RouteFirstReceived)
                                {
                                    tradeSnapshots[name].IsComplete = true;
                                    SendLogMessage($"{name} trades snapshot is complete - {tradeSnapshots[name].Trades.Count} entries", LogMessageType.System);
                                }
                            }

                            if (RouteFirst == "1")
                            {
                                //if (tradeSnapshots[name].RouteFirstReceived)
                                //{
                                //    tradeSnapshots[name].IsComplete = true;
                                //    SendLogMessage($"{name} trades snapshot is complete - {tradeSnapshots[name].Trades.Count} entries", LogMessageType.System);
                                //}
                                //else
                                //{
                                    tradeSnapshots[name].RouteFirstReceived = true;
                                    SendLogMessage($"{name} received trades snapshot with RouteFirst=Y - {tradeSnapshots[name].Trades.Count} entries", LogMessageType.System);
                                //}
                            }

                            // если снэпшот сформирован
                            if (tradeSnapshots[name].IsComplete)
                            {
                                if (!lastRptSeqProcessed.ContainsKey(name))
                                {
                                    lastRptSeqProcessed.Add(name, 0);
                                }

                                // отправляем все трейды из снэпшота
                                for (int i = 0; i < tradeSnapshots[name].Trades.Count; i++)
                                {
                                    NewTradesEvent(tradeSnapshots[name].Trades[i]);                                    
                                }
                                lastRptSeqProcessed[name] = tradeSnapshots[name].RptSeq;
                            }
                        }
                    }                                     

                    // обабатываем накопленные трейды
                    for (int i = 0; i < tradesFromIncremental.Count; i++)
                    {
                        KeyValuePair<string, List<GroupValue>> item = tradesFromIncremental.ElementAt(i);
                        string name = item.Key;
                        List<GroupValue> tradeDefs = item.Value;

                        if (!tradeSnapshots.ContainsKey(name))
                        {
                            continue;
                        }                                               

                        // отправляем все трейды из инкрементальных обновлений так как снэпшот к этому времени уже отправлен
                        for (int j = 0; j < tradeDefs.Count; j++)
                        {
                            GroupValue tradeDef = tradeDefs[j];
                        
                            int RptSeq = tradeDef.GetInt("RptSeq");

                            if (!lastRptSeqProcessed.ContainsKey(name))
                            {
                                lastRptSeqProcessed.Add(name, 0);
                            }

                            // пропускаем уже обработанные трейды
                            if (RptSeq <= lastRptSeqProcessed[name])
                                continue;
                            
                            //здесь контроль накопления трейдов
                            if (RptSeq != lastRptSeqProcessed[name] + 1) // пропущенное обновление!
                            {
                                //SendLogMessage($"{name} received {RptSeq} but last processed {lastRptSeqProcessed[name]}.", LogMessageType.System);
                                tradesFromIncremental[name].RemoveRange(0, j);

                                if (tradesFromIncremental[name].Count > 5 && !_missingTLRData)
                                {
                                    _missingTLRData = true; // надо восстанавливать по TCP
                                    
                                    if (_missingTLRBeginSeqNo == 0) // если при этом потоки данных не зафиксировали пропуска данных
                                    {
                                        // такое бывает когда мы подключились посреди торговой сессии и пропуск возник из-за того, что 
                                        // последние данные в снэпшоте оказались слишком старыми и 
                                        // надо восстанавливать принудительно или ждать следующего цикла снэпшотов

                                        // при этом надо среди всех снэпшотов найти наименьшее значение обработанных сообщений
                                        long minSeq = 0;
                                        foreach (SecuritySnapshot snapshot in tradeSnapshots.Values)
                                        {
                                            if (minSeq == 0 || snapshot.LastMsgSeqNumProcessed < minSeq)
                                            {
                                                minSeq = snapshot.LastMsgSeqNumProcessed;
                                            }
                                        }

                                        _missingTLRBeginSeqNo = minSeq + 1;

                                        // подобная ситуация возникает только при первом подключении, так что проверять надо MsgSeqNum самых первых принятых обновлений
                                        List<long> keys = _tradesIncremental.Keys.ToList();
                                        keys.Sort();
                                        if (keys[0] > _missingTLRBeginSeqNo)
                                            _missingTLREndSeqNo = keys[0] - 1;
                                    }
                                }

                                if (tradesFromIncremental[name].Count % 100 == 0)
                                    SendLogMessage($"{name} X-trade update with rptseq={RptSeq} but last rptseq={lastRptSeqProcessed[name]}. Total trades in queue: {tradesFromIncremental[name].Count}. Snapshot rptseq={tradeSnapshots[name].RptSeq}...", LogMessageType.System);
                                break;
                            }

                            string MDEntryType = tradeDef.GetString("MDEntryType");
                            if (MDEntryType == "z")
                            {
                                Trade trade = new Trade();

                                trade.SecurityNameCode = name;
                                trade.Price = tradeDef.GetString("MDEntryPx").ToDecimal();

                                string time = tradeDef.GetString("MDEntryTime");
                                if (time.Length == 8)
                                {
                                    time = "0" + time;
                                }

                                time = DateTime.UtcNow.ToString("ddMMyyyy") + time;

                                DateTime tradeDateTime = DateTime.ParseExact(time, "ddMMyyyyHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);

                                trade.Time = tradeDateTime;


                                trade.Id = tradeDef.GetString("MDEntryID");
                                trade.Side = tradeDef.GetString("OrderSide") == "B" ? Side.Buy : Side.Sell;
                                trade.Volume = tradeDef.GetString("MDEntrySize").ToDecimal();

                                NewTradesEvent(trade);
                            }

                            lastRptSeqProcessed[name] = RptSeq;
                        }

                        if (tradesFromIncremental[name].Count > 0)
                        {
                            if (lastRptSeqProcessed[name] == tradesFromIncremental[name][tradesFromIncremental[name].Count - 1].GetInt("RptSeq"))
                            {
                                // очищаем накопленные обновления если все их обработали
                                tradesFromIncremental[name].Clear();
                            }
                        }

                        if (tradesFromIncremental[name].Count > 10 && tradeSnapshots[name].RptSeq >= tradesFromIncremental[name][0].GetInt("RptSeq") && tradeSnapshots[name].IsComplete) // если необработанных сообщений накопилось много, то пора восстанавливать данные из снэпшота
                        {
                            SendLogMessage($"{name} Total trades in queue: {tradesFromIncremental[name].Count}. Restoring from snapshot...", LogMessageType.System);

                            // отправляем все трейды из снэпшота
                            for (int k = 0; k < tradeSnapshots[name].Trades.Count; k++)
                            {
                                NewTradesEvent(tradeSnapshots[name].Trades[k]);                                
                            }
                            lastRptSeqProcessed[name] = tradeSnapshots[name].RptSeq;
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

        private void OrderMessagesReader()
        {
            Thread.Sleep(1000);

            // сначала накапливаем стаканы из снэпшотов
            // как только получили все данные из снэпшотов - отправляем их в обработчик
            Dictionary<string, OrdersSnapshot> orderSnapshots = new Dictionary<string, OrdersSnapshot>();

            // после обработки всех ордеров (стаканов) из снэпшотов обрабатываем стаканы из инкрементальных обновлений
            Dictionary<string, List<GroupValue>> ordersFromIncremental = new Dictionary<string, List<GroupValue>>();

            // последнее обработанное сообщение из инкрементальных обновлений
            Dictionary<string, int> lastRptSeqProcessed = new Dictionary<string, int>();

           
            //bool faketradesnotloaded = true;

            while (true)
            {
                try
                {
                    if (_orderMessages.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    OpenFAST.Message msg;

                    _orderMessages.TryDequeue(out msg);

                    if (msg == null)
                    {
                        continue;
                    }

                    string msgType = msg.GetString("MessageType");
                    string MsgSeqNum = msg.GetString("MsgSeqNum");

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
                                string TradingSessionID = groupVal.GetString("TradingSessionID");                                                              

                                if (!_TradingSessionIDs.Contains(TradingSessionID))
                                    continue;

                                string TradingSessionSubID = groupVal.GetString("TradingSessionSubID") ?? "N";
                                //if (TradingSessionSubID != "N")
                                //{
                                //    continue;                                    
                                //}

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

                                string MDEntryType = groupVal.GetString("MDEntryType");
                                int RptSeq = groupVal.GetInt("RptSeq");

                                _logFileXOrders.WriteLine($"{DateTime.Now} [{MsgSeqNum}] {msgType}: {name} rptseq={RptSeq}, MDEntryType={MDEntryType} ");

                                if (!ordersFromIncremental.ContainsKey(name))
                                {
                                    ordersFromIncremental.Add(name, new List<GroupValue>());
                                }

                                // check if trade with such id already exists
                                if (ordersFromIncremental[name].Any(t => t.GetInt("RptSeq") == RptSeq))
                                {
                                    continue;
                                }

                                // Find the index where the new item should be inserted
                                int index = ordersFromIncremental[name].BinarySearch(groupVal, new UpdateComparer());

                                // If the item is not found (index is negative)
                                if (index < 0)
                                {
                                    index = ~index; // Convert negative index to positive
                                }

                                // Insert the new item
                                ordersFromIncremental[name].Insert(index, groupVal);



                                if (MDEntryType == "J") // Empty Book
                                {
                                    SendLogMessage($"{name} X-Orders Empty Book received. Clearing snapshot?", LogMessageType.System);

                                    //if (orderSnapshots.ContainsKey(name))
                                    //{
                                    //    orderSnapshots[name].Data.Clear();
                                    //    orderSnapshots[name].IsComplete = true;
                                    //    orderSnapshots[name].RptSeq = 0;
                                    //    orderSnapshots[name].LastFragmentReceived = true;
                                    //    orderSnapshots[name].RouteFirstReceived = true;

                                    //    _marketDepths[name].Clear();


                                    //}
                                    //if (ordersFromIncremental.ContainsKey(name))
                                    //{
                                    //    lastRptSeqProcessed[name] = RptSeq;
                                    //    ordersFromIncremental[name].Clear();
                                    //}

                                    // Отправляем очищенный стакан 
                                    BuildMDFromOrdersUpdates(name);
                                }
                            }
                        }
                    }

                    // Обрабатываем снэпшот
                    if (msgType == "W") /// Market Data - Snapshot/Full Refresh (W)
                    {
                        //_lastInstrumentDefinitionsTime = DateTime.UtcNow;                                                          
                        string name = msg.GetString("Symbol") ?? "N/A";
                        string TradingSessionID = msg.GetString("TradingSessionID") ?? "_";
                        long LastMsgSeqNumProcessed = msg.GetLong("LastMsgSeqNumProcessed");                      

                        if (!_TradingSessionIDs.Contains(TradingSessionID))
                            continue;

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

                        if (!lastRptSeqProcessed.ContainsKey(name))
                        {
                            lastRptSeqProcessed.Add(name, 0);
                        }

                        int RptSeq = msg.GetInt("RptSeq");
                        if (!orderSnapshots.ContainsKey(name))
                        {
                            orderSnapshots.Add(name, new OrdersSnapshot());
                            // сохраняем, чтобы знать, какие данные есть в снэпшоте
                            orderSnapshots[name].RptSeq = RptSeq;
                            orderSnapshots[name].LastMsgSeqNumProcessed = LastMsgSeqNumProcessed;
                        }
                        else                                          
                        if (orderSnapshots[name].RptSeq != RptSeq) 
                        {
                            SendLogMessage("Orders Snapshot update for " + name + " is not complete. RptSeq = " + RptSeq + " OrderSnapshots[" + name + "].RptSeq = " + orderSnapshots[name].RptSeq, LogMessageType.System);
                            orderSnapshots[name].RptSeq = RptSeq;
                            orderSnapshots[name].LastMsgSeqNumProcessed = LastMsgSeqNumProcessed;
                            orderSnapshots[name].IsComplete = false;
                            orderSnapshots[name].LastFragmentReceived = false;
                            orderSnapshots[name].RouteFirstReceived = false;
                            orderSnapshots[name].Data.Clear();
                        }

                        string LastFragment = msg.GetString("LastFragment"); // 1 - сообщение последнее, снэпшот сформирован
                        string RouteFirst = msg.GetString("RouteFirst"); // 1 - сообщение первое, формирующее снэпшот по инструменту

                        if (msg.IsDefined("GroupMDEntries"))
                        {
                            SequenceValue secVal = msg.GetValue("GroupMDEntries") as SequenceValue;
                            SendLogMessage($"W-Orders {name} (RptSeq={RptSeq}): with {secVal.Length} entries. Total entries: {orderSnapshots[name].Data.Count}", LogMessageType.System);

                            for (int i = 0; i < secVal.Length; i++)
                            {
                                GroupValue groupVal = secVal[i] as GroupValue;

                                string MDEntryType = groupVal.GetString("MDEntryType");
                                string TradingSessionSubID = groupVal.GetString("TradingSessionSubID") ?? "N";
                                //if (TradingSessionSubID != "N")
                                //{
                                //    continue;
                                //}

                                OrdersUpdate update = new OrdersUpdate();
                                update.Id = groupVal.GetString("MDEntryID") ?? "0";
                                update.Type = MDEntryType;
                                update.Price = groupVal.GetString("MDEntryPx");
                                update.Size = groupVal.GetString("MDEntrySize");
                                update.Action = "Add";
                                update.Side = MDEntryType == "0" ? "Bids" : "Asks";
                                update.Time = groupVal.GetString("MDEntryTime");

                                orderSnapshots[name].AddData(update);

                                if (MDEntryType == "J") // Empty Book
                                {
                                    SendLogMessage($"{name} W-Orders Empty Book received. Clearing snapshot...", LogMessageType.System);
                                    if (orderSnapshots.ContainsKey(name))
                                    {
                                        orderSnapshots[name].Data.Clear();
                                        orderSnapshots[name].IsComplete = true;
                                        orderSnapshots[name].RptSeq = RptSeq;
                                        orderSnapshots[name].LastFragmentReceived = true;
                                        orderSnapshots[name].RouteFirstReceived = true;

                                        _marketDepths[name].Clear();
                                    }
                                    if (ordersFromIncremental.ContainsKey(name))
                                    {
                                        lastRptSeqProcessed[name] = RptSeq;
                                        ordersFromIncremental[name].Clear();
                                    }

                                    // Отправляем очищенный стакан 
                                    BuildMDFromOrdersUpdates(name);
                                }
                            }
                        }

                        bool snapshotAlreadyComplete = orderSnapshots[name].IsComplete;

                      
                        if (!snapshotAlreadyComplete)
                        {
                            if (LastFragment == "1")
                            {
                                SendLogMessage($"{name} received orders snapshot with LastFragment=Y - {orderSnapshots[name].Data.Count} entries", LogMessageType.System);
                                orderSnapshots[name].LastFragmentReceived = true;

                                if (orderSnapshots[name].RouteFirstReceived)
                                {
                                    orderSnapshots[name].IsComplete = true;
                                    SendLogMessage($"{name} orders snapshot is complete - {orderSnapshots[name].Data.Count} entries", LogMessageType.System);
                                }
                            }

                            if (RouteFirst == "1")
                            {
                                //if (orderSnapshots[name].RouteFirstReceived)
                                //{
                                //    orderSnapshots[name].IsComplete = true;
                                //    SendLogMessage($"{name} orders snapshot is complete - {orderSnapshots[name].Data.Count} entries", LogMessageType.System);
                                //}
                                //else
                                //{
                                    orderSnapshots[name].RouteFirstReceived = true;
                                    SendLogMessage($"{name} received orders snapshot with RouteFirst=Y - {orderSnapshots[name].Data.Count} entries", LogMessageType.System);
                                //}
                            }

                            // если снэпшот сформирован
                            if (orderSnapshots[name].IsComplete && lastRptSeqProcessed[name] < orderSnapshots[name].RptSeq)
                            {
                                _marketDepths[name].Clear();

                                SendLogMessage($"Orders snapshot for {name} just completed - applying snapshot", LogMessageType.System);

                                if (!lastRptSeqProcessed.ContainsKey(name))
                                {
                                    lastRptSeqProcessed.Add(name, 0);
                                }

                                // отправляем все обновления стакана из снэпшота
                                for (int i = 0; i < orderSnapshots[name].Data.Count; i++)
                                {
                                    string mdEntryType = orderSnapshots[name].Data[i].Type;

                                    if (mdEntryType == "0" || mdEntryType == "1")
                                    {
                                        _marketDepths[name].Add(orderSnapshots[name].Data[i]);
                                    }                                    
                                }
                                lastRptSeqProcessed[name] = orderSnapshots[name].RptSeq;

                                // проверяем приходили ли обновления по ордерам?
                                if (!ordersFromIncremental.ContainsKey(name))
                                {
                                    ordersFromIncremental.Add(name, new List<GroupValue>());
                                } 
                                
                                if (ordersFromIncremental[name].Count == 0)
                                {
                                    // Отправляем сформированный стакан 
                                    BuildMDFromOrdersUpdates(name);
                                }
                            }
                        }
                    }

                    // обабатываем накопленные обновления стакана
                    for (int i = 0; i < ordersFromIncremental.Count; i++)
                    {
                        KeyValuePair<string, List<GroupValue>> item = ordersFromIncremental.ElementAt(i);
                        string name = item.Key;
                        List<GroupValue> updateDefs = item.Value;

                        if (!orderSnapshots.ContainsKey(name))
                        {
                            continue;
                        }                                             


                        bool needToSendMarketDepth = false;
                        // применяем все обновления стаканов из инкрементальных обновлений так как снэпшот к этому времени уже отправлен
                        for (int j = 0; j < updateDefs.Count; j++)
                        {
                            GroupValue updateDef = updateDefs[j];

                            int RptSeq = updateDef.GetInt("RptSeq");

                            if (!lastRptSeqProcessed.ContainsKey(name))
                            {
                                lastRptSeqProcessed.Add(name, 0);
                            }

                            if (!orderSnapshots.ContainsKey(name))
                            {
                                orderSnapshots.Add(name, new OrdersSnapshot());
                            }

                            // пропускаем уже обработанные трейды
                            if (RptSeq <= lastRptSeqProcessed[name])
                                continue;

                            //здесь контроль накопления трейдов
                            if (RptSeq != lastRptSeqProcessed[name] + 1) // пропущенное обновление!
                            {
                                ordersFromIncremental[name].RemoveRange(0, j);                                                              

                                if (ordersFromIncremental[name].Count > 10 && !_missingOLRData)
                                {
                                    _missingOLRData = true; // надо восстанавливать по TCP                                 
                
                                    if (_missingOLRBeginSeqNo == 0) // если при этом потоки данных не зафиксировали пропуска данных
                                    {
                                        // такое бывает когда мы подключились посреди торговой сессии и пропуск возник из-за того, что 
                                        // последние данные в снэпшоте оказались слишком старыми и 
                                        // надо восстанавливать принудительно или ждать следующего цикла снэпшотов

                                        // при этом надо среди всех снэпшотов найти наименьшее значение обработанных сообщений
                                        long minSeq = 0;
                                        foreach (OrdersSnapshot snapshot in orderSnapshots.Values)
                                        {
                                            if (minSeq == 0 || snapshot.LastMsgSeqNumProcessed < minSeq)
                                            {
                                                minSeq = snapshot.LastMsgSeqNumProcessed;
                                            }
                                        }

                                        _missingOLRBeginSeqNo = minSeq + 1;

                                        // подобная ситуация возникает только при первом подключении, так что проверять надо MsgSeqNum самых первых принятых обновлений
                                        List<long> keys = _ordersIncremental.Keys.ToList();                                                                                
                                        keys.Sort();
                                        if (keys[0] > _missingOLRBeginSeqNo)
                                            _missingOLREndSeqNo = keys[0] - 1;
                                    }
                                }

                                //if (ordersFromIncremental[name].Count % 100 == 0)
                                SendLogMessage($"{name} X-order update with rptseq={RptSeq} but last rptseq={lastRptSeqProcessed[name]}. Total updates in queue: {ordersFromIncremental[name].Count}. Snapshot rptseq={orderSnapshots[name].RptSeq}...", LogMessageType.System);
                                break;
                            }

                            // применить инкрементальные обновления
                            string mdUpdateAction = updateDef.GetString("MDUpdateAction");
                            string mdEntryType = updateDef.GetString("MDEntryType");
                            string mdEntryTime = updateDef.GetString("MDEntryTime");
                            string mdEntryPx = updateDef.GetString("MDEntryPx");
                            string mdEntrySize = updateDef.GetString("MDEntrySize");
                            string mdEntryId = updateDef.GetString("MDEntryID") ?? "0";

                            lastRptSeqProcessed[name] = RptSeq;

                            if (mdEntryType == "0" || mdEntryType == "1") // bid or ask
                            {
                                if (mdUpdateAction == "0") // add
                                {
                                    OrdersUpdate update = new OrdersUpdate();
                                    update.Type = mdEntryType;
                                    update.Id = mdEntryId;
                                    update.Action = "Add";
                                    update.Side = mdEntryType == "0" ? "Bids" : "Asks";
                                    update.Price = mdEntryPx;
                                    update.Size = mdEntrySize;
                                    update.Time = mdEntryTime;

                                    if (mdEntryType == "0" || mdEntryType == "1")
                                    {
                                        _marketDepths[name].Add(update);
                                    }
                                }

                                if (mdUpdateAction == "1") // Update
                                {
                                    int index = _marketDepths[name].FindIndex(x => x.Id == mdEntryId);
                                    if (index == -1)
                                    {
                                        SendLogMessage($"{name} received orders UPDATE rptseq={RptSeq} with mdentryid={mdEntryId} but entry not found. Num of market depths={_marketDepths[name].Count}", LogMessageType.System);
                                        continue;
                                    }

                                    _marketDepths[name][index].Price = mdEntryPx;
                                    _marketDepths[name][index].Size = mdEntrySize;
                                }

                                if (mdUpdateAction == "2") // Delete
                                {
                                    int index = _marketDepths[name].FindIndex(x => x.Id == mdEntryId);
                                    if (index == -1)
                                    {
                                        SendLogMessage($"{name} received orders DELETE rptseq={RptSeq} with mdentryid={mdEntryId} but entry not found. Num of market depths={_marketDepths[name].Count}", LogMessageType.System);
                                        continue;
                                    }

                                    _marketDepths[name].RemoveAt(index);
                                }

                                needToSendMarketDepth = true;
                            }                            
                        }

                        if (needToSendMarketDepth)
                        {
                            // Отправляем сформированный стакан 
                            BuildMDFromOrdersUpdates(name);

                            if (lastRptSeqProcessed[name] == ordersFromIncremental[name][ordersFromIncremental[name].Count - 1].GetInt("RptSeq"))
                            {
                                // очищаем накопленные обновления если все их обработали
                                //ordersFromIncremental[name].Clear();
                            }
                        }

                        if (orderSnapshots[name].RptSeq > lastRptSeqProcessed[name] && orderSnapshots[name].IsComplete) // если необработанных сообщений накопилось много, то пора восстанавливать данные из снэпшота
                        {
                            SendLogMessage($"{name} Total order updates in queue: {ordersFromIncremental[name].Count}. Restoring from snapshot...", LogMessageType.System);

                            // очищаем накопленное, чтобы заново восстановить из снэпшота
                            _marketDepths[name].Clear();                            

                            // отправляем все обновления из снэпшота                                                                                    
                            for (int k = 0; k < orderSnapshots[name].Data.Count; k++)
                            {
                                string mdEntryType = orderSnapshots[name].Data[k].Type;

                                if (mdEntryType == "0" || mdEntryType == "1")
                                {
                                    _marketDepths[name].Add(orderSnapshots[name].Data[k]);
                                }
                            }
                            lastRptSeqProcessed[name] = orderSnapshots[name].RptSeq;
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

        // конструируем MarketDepth по сохраненным обновлениям стакана OrdersUpdates
        private void BuildMDFromOrdersUpdates(string name)
        {
            List<OrdersUpdate> updates = _marketDepths[name];

            MarketDepth md = new MarketDepth();
            md.SecurityNameCode = name;

            for (int i = 0; i < updates.Count; i++)
            {
                OrdersUpdate update = updates[i];
                decimal price = update.Price.ToDecimal();
                decimal size = update.Size.ToDecimal();

                md.Time = DateTime.UtcNow;
                                
                if (update.Side == "Bids")
                {
                    int index = md.Bids.FindIndex(x => x.Price == price);

                    if (index == -1)
                    {
                        MarketDepthLevel mdLevel = new MarketDepthLevel();
                        mdLevel.Price = price;
                        mdLevel.Bid = size;
                        md.Bids.Add(mdLevel);
                    }
                    else
                    {
                        md.Bids[index].Bid += size;
                    }
                }
                else
                {
                    int index = md.Asks.FindIndex(x => x.Price == price);
                    if (index == -1)
                    {
                        MarketDepthLevel mdLevel = new MarketDepthLevel();
                        mdLevel.Price = price;
                        mdLevel.Ask = size;
                        md.Asks.Add(mdLevel);
                    }
                    else
                    {
                        md.Asks[index].Ask += size;                                                
                    }
                }

                // sort bids/asks by price
                md.Bids.Sort((x, y) => y.Price.CompareTo(x.Price));
                md.Asks.Sort((x, y) => x.Price.CompareTo(y.Price));
                
                // обрезаем лишние значения
                const int maxCount = 25;
                if (md.Bids.Count > maxCount)
                    md.Bids.RemoveRange(maxCount - 1, md.Bids.Count - maxCount);

                if (md.Asks.Count > maxCount)
                    md.Asks.RemoveRange(maxCount - 1, md.Asks.Count - maxCount);

                // вычищаем ошибочные данные тестового сервера например когда аск меньше бид
                
            }

            // отправляем на сервер
            MarketDepthEvent(md);
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
                        string OrderQty = fixMessage.Fields["OrderQty"] ?? "0";

                        string LastQty = fixMessage.Fields.ContainsKey("LastQty") ? fixMessage.Fields["LastQty"] : "0";
                        string LastPx = fixMessage.Fields.ContainsKey("LastPx") ? fixMessage.Fields["LastPx"] : "0";

                        string TransactTime = fixMessage.Fields["TransactTime"];
                        string Text = fixMessage.Fields.ContainsKey("Text") ? fixMessage.Fields["Text"] : "";

                        Order order = new Order();

                        order.SecurityNameCode = fixMessage.Fields["Symbol"];
                        order.PortfolioNumber = fixMessage.Fields["Account"];
                        order.NumberMarket = fixMessage.Fields["OrderID"];
                        order.Comment = Text;                                               
                                                
                        if (ExecType == "F") // сделка
                        {
                            order.Volume = LastQty.ToDecimal();
                        }

                        try
                        {
                            order.NumberUser = Convert.ToInt32(fixMessage.Fields["ClOrdID"]);
                        }
                        catch
                        {
                            // ignore
                        }

                        if (ExecType == "5") // Изменено
                        {
                            try
                            {
                                order.NumberUser = Convert.ToInt32(fixMessage.Fields["OrigClOrdID"]);
                            }
                            catch
                            {
                                // ignore
                            }

                            order.Volume = OrderQty.ToDecimal(); // сделки не было значит вот это
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
                        else if (OrdStatus == "4") // отменена/снята
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

                    // 3. Обрабатываем OrderCancelReject
                    if (fixMessage.MessageType == "OrderCancelReject")
                    {                        
                        string OrigClOrdID = fixMessage.Fields["OrigClOrdID"] ?? "N/A";

                        string Text = fixMessage.Fields["Text"];

                        SendLogMessage($"MFIX sent order cancel reject: {Text}. Order user id: {OrigClOrdID}", LogMessageType.Error);
                    }

                    // 4. Обрабатываем OrderMassCancelReport
                    if (fixMessage.MessageType == "OrderMassCancelReport")
                    {
                        string MassCancelRequestType = fixMessage.Fields["MassCancelRequestType"] == "1" ? "Cancel by security id" : "Cancel all orders";
                        string MassCancelResponse = fixMessage.Fields["MassCancelResponse"];
                        string MassCancelRejectReason = MassCancelResponse == "0" ? fixMessage.Fields["MassCancelRejectReason"] : "";

                        string Text = fixMessage.Fields["Text"] ?? "";

                        if (MassCancelResponse == "0")
                        {
                            SendLogMessage($"MFIX rejected order mass cancel with report: Text={Text}. {MassCancelRequestType}, MassCancelResponse={MassCancelResponse}, {MassCancelRejectReason} ", LogMessageType.Error);
                        }
                    }

                    // 5. Обрабатываем Logout
                    if (fixMessage.MessageType == "Logout")
                    {
                        _MFIXTradeSocket.Close();
                        _MFIXTradeSocket = null;
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

                    // 3. Обрабатываем Logout
                    if (fixMessage.MessageType == "Logout")
                    {
                        _MFIXTradeCaptureSocket.Close();
                        _MFIXTradeCaptureSocket = null;
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }
        
        private DateTime _lastMdTime = DateTime.MinValue;

        public event Action<Trade> NewTradesEvent;

        public event Action<MarketDepth> MarketDepthEvent;

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

                NewOrderSingleMessage msg = new NewOrderSingleMessage()
                {
                    ClOrdID = order.NumberUser.ToString(),
                    NoPartyID = "1",
                    PartyID = _MFIXTradeClientCode,
                    Account = _MFIXTradeAccount,
                    NoTradingSessions = "1",
                    TradingSessionID = order.SecurityClassCode,
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
                if (order.TypeOrder == OrderPriceType.Market)
                {
                    SendLogMessage("Can`t change price of market order", LogMessageType.Error);
                    return;
                }

                if (order.TypeOrder == OrderPriceType.Iceberg)
                {
                    SendLogMessage("Can`t change price of iceberg order", LogMessageType.Error);
                    return;
                }

                if (order.State != OrderStateType.Activ)
                {
                    SendLogMessage("Can`t change price of non-active order", LogMessageType.Error);
                    return;
                }

                Header header = new Header
                {
                    BeginString = "FIX.4.4", //Версия FIX "FIX .4 .4»,
                    MsgType = "G", // Order cancel replace request
                    SenderCompID = _MFIXTradeServerLogin,
                    TargetCompID = _MFIXTradeServerTargetCompId,
                    MsgSeqNum = _MFIXTradeMsgSeqNum++
                };

                OrderCancelReplaceRequestMessage msg = new OrderCancelReplaceRequestMessage()
                {
                    ClOrdID = DateTime.UtcNow.Ticks.ToString(), // идентификатор заявки на снятие/изменение
                    OrigClOrdID = order.NumberUser.ToString(),
                    OrderID = order.NumberMarket,
                    PartyID = _MFIXTradeClientCode,
                    Account = _MFIXTradeAccount,                    
                    TradingSessionID = order.SecurityClassCode,
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

        public void CancelAllOrders()
        {
            try
            {
                Header header = new Header
                {
                    BeginString = "FIX.4.4", //Версия FIX "FIX .4 .4»,
                    MsgType = "q", // order mass cancel request
                    SenderCompID = _MFIXTradeServerLogin,
                    TargetCompID = _MFIXTradeServerTargetCompId,
                    MsgSeqNum = _MFIXTradeMsgSeqNum++
                };

                OrderMassCancelRequestMessage msg = new OrderMassCancelRequestMessage()
                {
                    ClOrdID = DateTime.UtcNow.Ticks.ToString(), // идентификатор заявки на снятие
                    TransactTime = DateTime.UtcNow.ToString("yyyyMMdd-HH:mm:ss.fff"),                    
                    Account = _MFIXTradeAccount,
                    PartyID = _MFIXTradeClientCode,
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
                SendLogMessage("Cancel all orders request error " + exception.ToString(), LogMessageType.Error);
            }
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            try
            {
                Header header = new Header
                {
                    BeginString = "FIX.4.4", //Версия FIX "FIX .4 .4»,
                    MsgType = "q", // order mass cancel request
                    SenderCompID = _MFIXTradeServerLogin,
                    TargetCompID = _MFIXTradeServerTargetCompId,
                    MsgSeqNum = _MFIXTradeMsgSeqNum++
                };

                OrderMassCancelRequestMessage msg = new OrderMassCancelRequestMessage()
                {
                    ClOrdID = DateTime.UtcNow.Ticks.ToString(), // идентификатор заявки на снятие
                    MassCancelRequestType = "7",
                    TradingSessionID = security.NameClass,
                    Symbol = security.NameId,
                    TransactTime = DateTime.UtcNow.ToString("yyyyMMdd-HH:mm:ss.fff"),
                    Account = _MFIXTradeAccount,
                    PartyID = _MFIXTradeClientCode,
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
                SendLogMessage("Cancel all orders request error " + exception.ToString(), LogMessageType.Error);
            }
        }

        public void GetAllActivOrders()
        {
            // Торговый сервер не дает возможности запросить все активные ордера
        }

        public void GetOrderStatus(Order order)
        {
            // Order Status Request (H) - запрос не реализован на серверах MFIX Trade и MFIX Trade Capture (в документации зачеркнуто)
        }

        #endregion

        #region 11 Helpers

        private int GetDecimals(decimal x)
        {
            var precision = 0;
            while (x * (decimal)Math.Pow(10, precision) != Math.Round(x * (decimal)Math.Pow(10, precision)))
                precision++;
            return precision;
        }

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

    public class OrdersUpdate
    {
        public string Type;
        public string Id;
        public string Action = "Add"; // Add, Update, Delete
        public string Side = "Bids"; // Bids, Asks
        public string Price;
        public string Size;
        public string Time;
    }

    public class OrderBookComparer : IComparer<OrdersUpdate>
    {
        public int Compare(OrdersUpdate x, OrdersUpdate y)
        {
            return long.Parse(x.Id).CompareTo(long.Parse(y.Id));
        }
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