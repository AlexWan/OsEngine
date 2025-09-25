using OpenFAST;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Xml;
using OpenFAST.Codec;
using OpenFAST.Template.Loader;
using OpenFAST.Template;
using OsEngine.Market.Servers.MoexFixFastTwimeFutures.Entity;


namespace OsEngine.Market.Servers.MoexFixFastTwimeFutures
{
    public class MoexFixFastTwimeFuturesServer : AServer
    {
        public MoexFixFastTwimeFuturesServer()
        {
            if (!Directory.Exists(@"Engine\Log\MoexFixFastTwimeConnectorLogs\"))
            {
                Directory.CreateDirectory(@"Engine\Log\MoexFixFastTwimeConnectorLogs\");
            }

            MoexFixFastTwimeFuturesServerRealization realization = new MoexFixFastTwimeFuturesServerRealization();
            ServerRealization = realization;

            CreateParameterEnum("Trading Protocol", "TWIME", new List<string>() { "TWIME", "FIX Gate" });
            CreateParameterString("Trade Account", "");
            CreateParameterString("TWIME Login", "");
            CreateParameterString("TWIME Trade Address", "");
            CreateParameterInt("TWIME Trade Port", 0);
            CreateParameterString("FIX SenderCompID", "");
            CreateParameterPassword("Password", "");
            CreateParameterString("FIX Trade Address", "");
            CreateParameterInt("FIX Trade Port", 0);
            CreateParameterString("FIX Trade TargetCompID", "");
            CreateParameterPath("Multicast Config Directory");
            CreateParameterInt("Limit of requests to the server (per second)", 30);
            CreateParameterBoolean("Use Options", false);
        }
    }
    public class MoexFixFastTwimeFuturesServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public MoexFixFastTwimeFuturesServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread thread1 = new Thread(InstrumentDefinitionsReader);
            thread1.Name = "GetterSecurity";
            thread1.Start();

            Thread thread2 = new Thread(GetFastMessagesByTrades);
            thread2.Name = "GetterTrades";
            thread2.Start();

            Thread thread3 = new Thread(TradeMessagesReader);
            thread3.Name = "TradesReaderFromQueue";
            thread3.Start();

            Thread thread4 = new Thread(GetFastMessagesByOrders);
            thread4.Name = "GetterOrders";
            thread4.Start();

            Thread thread5 = new Thread(OrderMessagesReader);
            thread5.Name = "OrdersReaderFromQueue";
            thread5.Start();

            Thread thread6 = new Thread(RecoveryData);
            thread6.Name = "HistoricalReplayMoexFast";
            thread6.Start();

            Thread thread7 = new Thread(ProcessTradingServerEvents);
            thread7.Name = "TradeServerProcessing";
            thread7.Start();
        }

        public void Connect(WebProxy proxy)
        {
            try
            {
                _securities.Clear();
                _myPortfolios.Clear();
                _subscribedSecurities.Clear();

                _tradingProtocol = ((ServerParameterEnum)ServerParameters[0]).Value;
                _TradeAccount = ((ServerParameterString)ServerParameters[1]).Value; // FZ00XOX
                _TWIMELogin = ((ServerParameterString)ServerParameters[2]).Value;
                _TWIMETradeAddress = ((ServerParameterString)ServerParameters[3]).Value;
                _TWIMETradePort = ((ServerParameterInt)ServerParameters[4]).Value;
                _senderCompID = ((ServerParameterString)ServerParameters[5]).Value;
                _password = ((ServerParameterPassword)ServerParameters[6]).Value;
                _FIXTradeAddress = ((ServerParameterString)ServerParameters[7]).Value;
                _FIXTradePort = ((ServerParameterInt)ServerParameters[8]).Value;
                _FIXTradeTargetCompID = ((ServerParameterString)ServerParameters[9]).Value;
                _configDir = ((ServerParameterPath)ServerParameters[10]).Value;

                int orderActionsLimit = ((ServerParameterInt)ServerParameters[11]).Value;

                _useOptions = ((ServerParameterBool)ServerParameters[12]).Value;

                _rateGateForOrders = new RateGate(orderActionsLimit, TimeSpan.FromSeconds(1));

                if (_tradingProtocol.Equals("FIX Gate") & (string.IsNullOrEmpty(_senderCompID) || string.IsNullOrEmpty(_password)))
                {
                    SendLogMessage("Can`t run connector with FIX protocol. No FIXCompId or password", LogMessageType.Error);
                    return;
                }

                if (string.IsNullOrEmpty(_TradeAccount))
                {
                    SendLogMessage("Connection terminated. You must specify Trade Account", LogMessageType.Error);
                    return;
                }

                if (_tradingProtocol.Equals("FIX Gate") & (string.IsNullOrEmpty(_FIXTradeAddress) || string.IsNullOrEmpty(_FIXTradeTargetCompID) || _FIXTradePort == 0))
                {
                    SendLogMessage("Can`t run connector. No FIX Trade parameters are specified", LogMessageType.Error);
                    return;
                }

                if (_tradingProtocol.Equals("TWIME") & (string.IsNullOrEmpty(_TWIMETradeAddress) || _TWIMETradePort == 0 || string.IsNullOrEmpty(_TWIMELogin)))
                {
                    SendLogMessage("Can`t run connector. No TWIME Trade parameters are specified", LogMessageType.Error);
                    return;
                }

                if (string.IsNullOrEmpty(_configDir) || !Directory.Exists(_configDir) || !File.Exists(_configDir + "\\configuration.xml") || !File.Exists(_configDir + "\\templates.xml"))
                {
                    SendLogMessage("Can`t run connector. No multicast directory are specified or the config and templates files don't exist", LogMessageType.Error);
                    return;
                }

                LoadFASTTemplates();

                _marketDataAddresses = GetAddressesForFastConnect();

                if (_tradingProtocol.Equals("TWIME"))
                {
                    CreateTWIMEConnection(_TWIMETradeAddress, _TWIMETradePort);
                }
                else
                {
                    CreateFIXTradeConnection(_FIXTradeAddress, _FIXTradePort);
                }

                if (_fixTradeConnectionSuccessful == true || _twimeConnectionSuccessful == true)
                {

                    CreateSocketConnections("Futures defintion", "Instrument Replay");

                    if ((_socketFuturesStreamA == null || _socketFuturesStreamB == null))
                    {
                        SendLogMessage("Connection FAST socket futures failed", LogMessageType.Error);

                        if (ServerStatus != ServerConnectStatus.Disconnect)
                        {
                            ServerStatus = ServerConnectStatus.Disconnect;
                            DisconnectEvent();
                            return;
                        }
                    }

                    if (_useOptions)
                    {
                        CreateSocketConnections("Options defintion", "Instrument Replay");

                        if ((_socketOptionsStreamA == null || _socketOptionsStreamB == null))
                        {
                            SendLogMessage("Connection FAST socket options failed", LogMessageType.Error);

                            if (ServerStatus != ServerConnectStatus.Disconnect)
                            {
                                ServerStatus = ServerConnectStatus.Disconnect;
                                DisconnectEvent();
                                return;
                            }
                        }
                    }

                    ServerStatus = ServerConnectStatus.Connect;
                    ConnectEvent();
                }
                else
                {
                    SendLogMessage("Error logging on to MOEX. No response from server.", LogMessageType.Error);
                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message.ToString(), LogMessageType.Error);
                SendLogMessage("Connection cannot be open to MOEX TWIME/FIX/FAST servers. Error request", LogMessageType.Error);

                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
        }

        public void Dispose()
        {
            _securities.Clear();
            _myPortfolios.Clear();
            DeleteWebSocketConnection();
            DataCleaning();

            SendLogMessage("Connection Closed by TwimeFast. WebSocket Data Closed Event", LogMessageType.System);

            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        private void DataCleaning()
        {
            _msgSeqNum = 1;
            _incomingFixMsgNum = 1;
            _allSecuritiesLoaded = false;

            _tradesIncremental.Clear();
            _tradeNumsForCheckMissed.Clear();

            _ordersIncremental.Clear();
            _ordersSnapshotsMsgs.Clear();
            _minRptSeqFromOrders.Clear();
            _ordersSnapshotsByName.Clear();
            _waitingDepthChanges.Clear();
            _depthChanges.Clear();
            _orderNumsForCheckMissed.Clear();
            _stockChanges.Clear();
            _controlDepths.Clear();

            _socketsInstruments.Clear();
            _socketsOrders.Clear();
            _socketsTrades.Clear();

            _secIdByName.Clear();
            _secNameById.Clear();
            _subscribedOptions.Clear();

            _timeLastDataReceipt = DateTime.Now.AddMinutes(30);
            _timeOfTheLastFIXMessage = DateTime.Now.AddMinutes(30);

            _tradeMessages = new ConcurrentQueue<OpenFAST.Message>();
            _orderMessages = new ConcurrentQueue<OpenFAST.Message>();

            _tryingConnectRecoveryOrdersCount = 1;
            _tryingConnectRecoveryTradesCount = 1;
        }

        private void DeleteWebSocketConnection()
        {
            try
            {
                lock (_socketLockerHistoricalReplay)
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
                }

                lock (_socketLockerInstruments)
                {
                    // закрываем сокеты получения данных по инструментам
                    if (_socketFuturesStreamA != null)
                    {
                        try
                        {
                            _socketFuturesStreamA.Close();
                        }
                        catch
                        {
                            // ignore
                        }

                        try
                        {
                            _socketFuturesStreamB.Close();
                        }
                        catch
                        {
                            // ignore
                        }

                        _socketFuturesStreamA = null;
                        _socketFuturesStreamB = null;
                    }

                    if (_socketOptionsStreamA != null)
                    {
                        try
                        {
                            _socketOptionsStreamA.Close();
                        }
                        catch
                        {
                            // ignore
                        }

                        try
                        {
                            _socketOptionsStreamB.Close();
                        }
                        catch
                        {
                            // ignore
                        }

                        _socketOptionsStreamA = null;
                        _socketOptionsStreamB = null;
                    }
                }

                // закрываем сокеты получения данных по сделкам
                if (_tradesIncrementalSocketA != null)
                {
                    lock (_socketLockerTradesIncremental)
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

                        _tradesIncrementalSocketA = null;
                        _tradesIncrementalSocketB = null;
                    }
                }

                if (_tradesSnapshotSocketA != null && _tradesSnapshotSocketB != null)
                {
                    lock (_socketLockerTradesSnapshots)
                    {
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

                        _tradesSnapshotSocketA = null;
                        _tradesSnapshotSocketB = null;
                    }
                }

                // закрываем сокеты получения данных по ордерам
                if (_ordersIncrementalSocketA != null)
                {
                    lock (_socketLockerOrdersIncremental)
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

                        _ordersIncrementalSocketA = null;
                        _ordersIncrementalSocketB = null;
                    }
                }

                if (_ordersSnapshotSocketA != null && _ordersSnapshotSocketB != null)
                {
                    lock (_socketLockerOrdersSnapshots)
                    {
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

                        _ordersSnapshotSocketA = null;
                        _ordersSnapshotSocketB = null;
                    }
                }

                lock (_socketLockerTrading)
                {
                    // отключаемся от сервера TWIME 
                    if (_TwimeTcpClient != null && _TwimeTradeStream != null)
                    {
                        try
                        {
                            byte[] termMsg = _twimeMessageConstructor.Terminate(out string msgLog);
                            SendLogMessage(msgLog, LogMessageType.System);

                            _TwimeTradeStream.Write(termMsg, 0, termMsg.Length);
                        }
                        catch
                        {
                            // ignore
                        }

                        while (_TwimeTcpClient != null || _TwimeTradeStream != null)
                        {
                            Thread.Sleep(1); // ждем когда сокет закроется
                        }
                    }
                }

                lock (_socketLockerTrading)
                {
                    // отключаемся от сервера FIXTrade 
                    if (_fixTradeTcpClient != null && _fixTradeStream != null)
                    {
                        try
                        {
                            string logout = _fixTradeMessages.LogoutMessage(_msgSeqNum);
                            SendFIXTradeMessage(logout);
                            _logoutInitiator = true;
                        }
                        catch
                        {
                            // ignore
                        }

                        while (_fixTradeTcpClient != null || _fixTradeStream != null)
                        {
                            Thread.Sleep(1); // ждем когда сокет закроется
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Error delete sockets:" + exception.ToString(), LogMessageType.Error);
            }
            finally
            {
            }
        }

        private void ProcessTradingServerEvents()
        {
            DateTime lastFIXTradeTestRequestTime = DateTime.MinValue;

            while (true)
            {
                try
                {
                    if (_tradingProtocol.Equals("TWIME"))
                    {
                        // проверка новых сообщений от TWIME
                        if (_TwimeTradeStream != null && _TwimeTradeStream.DataAvailable)
                        {
                            TwimeMessageReader();
                        }

                        // поддержка связи с сервером TWIME
                        if (_twimeConnectionSuccessful == true && _startKeepaliveIntervalForTwime.AddMilliseconds(29500) < DateTime.Now)
                        {
                            byte[] seqMsg = _twimeMessageConstructor.Sequence(out string msgLog);

                            WriteLogTradingServerEvent(msgLog);

                            try
                            {
                                _TwimeTradeStream.Write(seqMsg, 0, seqMsg.Length);
                            }
                            catch (IOException ex)
                            {
                                SendLogMessage($"Error write socket: {ex.Message}", LogMessageType.Error);

                                _TwimeTcpClient = null;
                                _TwimeTradeStream = null;

                                if (ServerStatus != ServerConnectStatus.Disconnect)
                                {
                                    ServerStatus = ServerConnectStatus.Disconnect;
                                    DisconnectEvent();
                                }
                            }

                            _startKeepaliveIntervalForTwime = DateTime.Now;
                        }
                    }
                    else
                    {
                        // контроль входящих сообщений
                        if (_fixTradeStream != null && _fixTradeStream.DataAvailable)
                        {
                            FixMessageReader();
                        }

                        // контроль соединения с FIX сервером

                        if (_fixTradeConnectionSuccessful == true && _startOfTheHeartbeatTimer.AddSeconds(30) < DateTime.Now)
                        {
                            string hrbtMsg = _fixTradeMessages.HeartbeatMessage(_msgSeqNum, false, null);

                            SendFIXTradeMessage(hrbtMsg);

                            _startOfTheHeartbeatTimer = DateTime.Now;
                        }

                        if (_isThereFirstHearbeat == true && _timeOfTheLastFIXMessage.AddMinutes(1) < DateTime.Now)
                        {
                            if (lastFIXTradeTestRequestTime > _timeOfTheLastFIXMessage)
                            {
                                if (ServerStatus != ServerConnectStatus.Disconnect)
                                {
                                    SendLogMessage("FIX Trade server connection lost. No response for too long", LogMessageType.Error);
                                    ServerStatus = ServerConnectStatus.Disconnect;
                                    DisconnectEvent();
                                }
                            }
                            else
                            {
                                // send TestRequest
                                SendLogMessage($"Not get Heartbeat on time. Need send test request.", LogMessageType.System);
                                _fixTradeMessages.TestRequestMessage(_msgSeqNum, DateTime.UtcNow.ToString("OsEngine"));
                                _isThereFirstHearbeat = false;
                                lastFIXTradeTestRequestTime = DateTime.Now;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        public DateTime ServerTime { get; set; }

        public ServerType ServerType => ServerType.MoexFixFastTwimeFutures;

        public ServerConnectStatus ServerStatus { get; set; } = ServerConnectStatus.Disconnect;

        public List<IServerParameter> ServerParameters { get; set; }

        public event Action ConnectEvent;
        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties

        private bool _useOptions;
        private string _tradingProtocol = string.Empty;
        private Dictionary<int, Order> _ordersForClearing = new Dictionary<int, Order>();

        // for TWIME
        private string _TWIMELogin;
        private string _TWIMETradeAddress;
        private int _TWIMETradePort;
        private bool _twimeConnectionSuccessful = false;
        private ulong _nextTwimeMsgNum;
        private DateTime _startKeepaliveIntervalForTwime = DateTime.Now.AddMinutes(30);

        // for FIX
        private string _senderCompID;
        private string _password;
        private string _TradeAccount; // торговый счет
        private string _FIXTradeAddress;
        private int _FIXTradePort;
        private string _FIXTradeTargetCompID;
        private long _msgSeqNum = 1;
        private long _incomingFixMsgNum = 1;
        private bool _logoutInitiator = false;
        private bool _fixTradeConnectionSuccessful = false;
        private bool _isThereFirstHearbeat = false;
        private DateTime _timeOfTheLastFIXMessage = DateTime.Now.AddMinutes(30);
        private DateTime _startOfTheHeartbeatTimer = DateTime.Now.AddMinutes(30);

        // for FAST
        private string _configDir;
        private MessageTemplate[] _templates;
        private List<MarketDataGroup> _marketDataAddresses;

        private Socket _socketFuturesStreamA;
        private Socket _socketFuturesStreamB;
        private Socket _socketOptionsStreamA;
        private Socket _socketOptionsStreamB;
        List<Socket> _socketsInstruments = new List<Socket>();
        private Socket _tradesIncrementalSocketA;
        private Socket _tradesIncrementalSocketB;
        private Socket _tradesSnapshotSocketA;
        private Socket _tradesSnapshotSocketB;
        List<Socket> _socketsTrades = new List<Socket>();
        private Socket _ordersIncrementalSocketA;
        private Socket _ordersIncrementalSocketB;
        private Socket _ordersSnapshotSocketA;
        private Socket _ordersSnapshotSocketB;
        List<Socket> _socketsOrders = new List<Socket>();
        private Socket _historicalReplaySocket;
        private IPEndPoint _historicalReplayEndPointForTrades;
        private IPEndPoint _historicalReplayEndPointForTradesReserve;
        private IPEndPoint _historicalReplayEndPointForOrders;
        private IPEndPoint _historicalReplayEndPointForOrdersReserve;
        private long _missingOrdersBeginSeqNo = -1;
        private long _missingOrdersEndSeqNo = 0;
        private long _missingTradesBeginSeqNo = -1;
        private long _missingTradesEndSeqNo = 0;
        private List<int> _missingOrdersRptSeqNums = new List<int>();
        private List<int> _missingTradesRptSeqNums = new List<int>();
        private int _ordRecoveryReqCount = 0;
        private int _trdRecoveryReqCount = 0;
        private bool _limitOrderReq = false;
        private bool _limitTradeReq = false;
        private bool _endOfRecoveryTrades = true;
        private bool _endOfRecoveryOrders = true;
        private DateTime _timeLastDataReceipt = DateTime.Now.AddMinutes(30);
        private int _tryingConnectRecoveryOrdersCount = 1;
        private bool _isTryingConnectToOrdersReserveIP = false;
        private int _tryingConnectRecoveryTradesCount = 1;
        private bool _isTryingConnectToTradesReserveIP = false;

        #endregion

        #region 3 Securities

        public void GetSecurities()
        {
            DateTime waitingTimeSecurities = DateTime.Now;

            int minutesToGetSecurities = 2;

            if (_useOptions)
                minutesToGetSecurities = 8;

            // даём время на загрузку бумаг
            while (DateTime.Now < waitingTimeSecurities.AddMinutes(minutesToGetSecurities))
            {
                if (_allSecuritiesLoaded)
                {
                    SecurityEvent(_securities);
                    return;
                }
                SendLogMessage("Securities not dowloaded. Wait, please", LogMessageType.System);
                Thread.Sleep(3000);
            }

            SendLogMessage("Securities could not be loaded. Check the connection.", LogMessageType.Error);
            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        List<Security> _securities = new List<Security>();

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        private List<Portfolio> _myPortfolios = new List<Portfolio>();

        public void GetPortfolios()
        {
            Portfolio newPortfolio = new Portfolio();
            newPortfolio.Number = _TradeAccount;
            newPortfolio.ValueCurrent = 1;
            _myPortfolios.Add(newPortfolio);

            if (_myPortfolios.Count != 0)
            {
                PortfolioEvent(_myPortfolios);
            }
        }

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            return null;
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        #endregion

        #region 6 Sockets creation

        private string _socketLockerHistoricalReplay = "socketLockerFastTwimeHistoricalReplay";
        private string _socketLockerInstruments = "socketLockerFastTwimeInstruments";
        private string _socketLockerTradesSnapshots = "socketLockerFastTwimeTradeSnapshots";
        private string _socketLockerTradesIncremental = "socketLockerFastTwimeTradesIncremental";
        private string _socketLockerOrdersSnapshots = "socketLockerFastTwimeOrdersSnapshots";
        private string _socketLockerOrdersIncremental = "socketLockerFastTwimeOrdersIncremental";
        private string _socketLockerTrading = "_socketLockerFIXTradeFastTwime";

        private TcpClient _fixTradeTcpClient;
        private NetworkStream _fixTradeStream;
        private FixMessageConstructor _fixTradeMessages;
        private TcpClient _TwimeTcpClient;
        private NetworkStream _TwimeTradeStream;
        private TwimeMessageConstructor _twimeMessageConstructor;

        private void CreateSocketConnections(string MDLabel, string typeConnection)
        {
            try
            {
                MarketDataGroup mdGroup = _marketDataAddresses.Find(p => p.Label == MDLabel);

                for (int i = 0; i < mdGroup.FastConnections.Count; i++)
                {
                    if (mdGroup.FastConnections[i].Type != typeConnection)
                        continue;

                    if (mdGroup.FeedType == "FO-TRADES" && mdGroup.FastConnections[i].Type == "Historical Replay")
                    {
                        IPAddress ipAddr = IPAddress.Parse(mdGroup.FastConnections[i].SrsIP);
                        IPAddress ipAddr2 = IPAddress.Parse(mdGroup.FastConnections[i].MulticastIP);

                        _historicalReplayEndPointForTrades = new IPEndPoint(ipAddr, mdGroup.FastConnections[i].Port);
                        _historicalReplayEndPointForTradesReserve = new IPEndPoint(ipAddr2, mdGroup.FastConnections[i].Port);

                        return;
                    }
                    else if (mdGroup.FeedType == "ORDERS-LOG" && mdGroup.FastConnections[i].Type == "Historical Replay")
                    {
                        IPAddress ipAddr1 = IPAddress.Parse(mdGroup.FastConnections[i].SrsIP);
                        IPAddress ipAddr2 = IPAddress.Parse(mdGroup.FastConnections[i].MulticastIP);

                        _historicalReplayEndPointForOrders = new IPEndPoint(ipAddr1, mdGroup.FastConnections[i].Port);
                        _historicalReplayEndPointForOrdersReserve = new IPEndPoint(ipAddr2, mdGroup.FastConnections[i].Port);

                        return;
                    }


                    // Create a UDP socket
                    Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    // Configure the socket options
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, 1 * 1024 * 1024); // Set receive buffer size

                    //// Join the multicast group
                    IPAddress multicastAddress = IPAddress.Parse(mdGroup.FastConnections[i].MulticastIP);
                    IPAddress sourceAddress = IPAddress.Parse(mdGroup.FastConnections[i].SrsIP);

                    //// Bind the socket to the port
                    //// Specify the local IP address and port to bind to.
                    IPAddress localAddress = IPAddress.Any; // Listen on all available interfaces
                    IPEndPoint localEndPoint = new IPEndPoint(localAddress, mdGroup.FastConnections[i].Port);

                    socket.Bind(localEndPoint);

                    byte[] membershipAddresses = new byte[12]; // 3 IPs * 4 bytes (IPv4)
                    Buffer.BlockCopy(multicastAddress.GetAddressBytes(), 0, membershipAddresses, 0, 4);
                    Buffer.BlockCopy(sourceAddress.GetAddressBytes(), 0, membershipAddresses, 4, 4);
                    Buffer.BlockCopy(localAddress.GetAddressBytes(), 0, membershipAddresses, 8, 4);
                    socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddSourceMembership, membershipAddresses);


                    if (mdGroup.FeedType == "FUT-INFO" && mdGroup.FastConnections[i].Type == "Instrument Replay")
                    {
                        if (mdGroup.FastConnections[i].Feed == "A")
                        {
                            _socketFuturesStreamA = socket;
                            _socketsInstruments.Add(_socketFuturesStreamA);
                        }

                        if (mdGroup.FastConnections[i].Feed == "B")
                        {
                            _socketFuturesStreamB = socket;
                            _socketsInstruments.Add(_socketFuturesStreamB);
                        }
                    }

                    if (mdGroup.FeedType == "OPT-INFO" && mdGroup.FastConnections[i].Type == "Instrument Replay")
                    {
                        if (mdGroup.FastConnections[i].Feed == "A")
                        {
                            _socketOptionsStreamA = socket;
                            _socketsInstruments.Add(_socketOptionsStreamA);
                        }

                        if (mdGroup.FastConnections[i].Feed == "B")
                        {
                            _socketOptionsStreamB = socket;
                            _socketsInstruments.Add(_socketOptionsStreamB);
                        }
                    }

                    if (mdGroup.FeedType == "FO-TRADES" && mdGroup.FastConnections[i].Type == "Incremental")
                    {
                        if (mdGroup.FastConnections[i].Feed == "A")
                        {
                            _tradesIncrementalSocketA = socket;
                            _socketsTrades.Add(_tradesIncrementalSocketA);
                        }

                        if (mdGroup.FastConnections[i].Feed == "B")
                        {
                            _tradesIncrementalSocketB = socket;
                            _socketsTrades.Add(_tradesIncrementalSocketB);
                        }
                    }

                    if (mdGroup.FeedType == "FO-TRADES" && mdGroup.FastConnections[i].Type == "Snapshot")
                    {
                        if (mdGroup.FastConnections[i].Feed == "A")
                        {
                            _tradesSnapshotSocketA = socket;
                            _socketsTrades.Add(_tradesSnapshotSocketA);
                        }

                        if (mdGroup.FastConnections[i].Feed == "B")
                        {
                            _tradesSnapshotSocketB = socket;
                            _socketsTrades.Add(_tradesSnapshotSocketB);
                        }
                    }

                    if (mdGroup.FeedType == "ORDERS-LOG" && mdGroup.FastConnections[i].Type == "Incremental")
                    {
                        if (mdGroup.FastConnections[i].Feed == "A")
                        {
                            _ordersIncrementalSocketA = socket;
                            _socketsOrders.Add(_ordersIncrementalSocketA);
                        }

                        if (mdGroup.FastConnections[i].Feed == "B")
                        {
                            _ordersIncrementalSocketB = socket;
                            _socketsOrders.Add(_ordersIncrementalSocketB);
                        }
                    }

                    if (mdGroup.FeedType == "ORDERS-LOG" && mdGroup.FastConnections[i].Type == "Snapshot")
                    {
                        if (mdGroup.FastConnections[i].Feed == "A")
                        {
                            _ordersSnapshotSocketA = socket;
                            _socketsOrders.Add(_ordersSnapshotSocketA);
                        }

                        if (mdGroup.FastConnections[i].Feed == "B")
                        {
                            _ordersSnapshotSocketB = socket;
                            _socketsOrders.Add(_ordersSnapshotSocketB);
                        }
                    }

                }

            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        public List<MarketDataGroup> GetAddressesForFastConnect()
        {
            // прочитать конфиг FIX/FAST соединения и вернуть список с адресами подключения

            List<MarketDataGroup> fastConnections = new List<MarketDataGroup>();

            XmlDocument xmlDoc = new XmlDocument();

            xmlDoc.Load(_configDir + "\\configuration.xml");

            XmlNodeList mdNodes = xmlDoc.SelectSingleNode("configuration").SelectNodes("MarketDataGroup");

            for (int i = 0; i < mdNodes.Count; i++)
            {
                string mdLabel = mdNodes[i].Attributes["label"].Value;


                MarketDataGroup mdGroup = new MarketDataGroup();

                mdGroup.FeedType = mdNodes[i].Attributes["feedType"].Value;
                mdGroup.MarketID = mdNodes[i].Attributes["marketID"].Value;
                mdGroup.Label = mdLabel;
                mdGroup.FastConnections = new List<FastConnection>();

                XmlNodeList mdConnections = mdNodes[i].SelectSingleNode("connections").SelectNodes("connection");

                for (int j = 0; j < mdConnections.Count; j++)
                {
                    FastConnection connection = new FastConnection();

                    connection.Type = mdConnections[j].SelectSingleNode("type").InnerText;

                    if (connection.Type == "Historical Replay")
                    {
                        XmlNodeList histIP = mdConnections[j].SelectNodes("ip");
                        connection.SrsIP = histIP[0].InnerText;
                        connection.MulticastIP = histIP[1].InnerText;
                        connection.Port = Convert.ToInt32(mdConnections[j].SelectSingleNode("port").InnerText);
                    }
                    else
                    {
                        connection.Feed = mdConnections[j].SelectSingleNode("feed").InnerText;
                        connection.SrsIP = mdConnections[j].SelectSingleNode("src-ip").InnerText;
                        connection.MulticastIP = mdConnections[j].SelectSingleNode("ip").InnerText;
                        connection.Port = Convert.ToInt32(mdConnections[j].SelectSingleNode("port").InnerText);
                    }

                    mdGroup.FastConnections.Add(connection);
                }

                fastConnections.Add(mdGroup);

            }
            return fastConnections;
        }

        private void CreateTWIMEConnection(string TWIMEServerAddress, int TWIMEServerPort)
        {
            try
            {
                _TwimeTcpClient = new TcpClient(TWIMEServerAddress, TWIMEServerPort);
                _TwimeTradeStream = _TwimeTcpClient.GetStream();

                if (_TwimeTcpClient.Connected)
                {
                    WriteLogTradingServerEvent("\n-------------- New TWIME Session -------------\n");

                    _twimeMessageConstructor = new TwimeMessageConstructor();

                    byte[] TwimeMsg = _twimeMessageConstructor.Establish(30000, _TWIMELogin, out string msgLog);

                    WriteLogTradingServerEvent(msgLog);

                    _TwimeTradeStream.Write(TwimeMsg, 0, TwimeMsg.Length);

                    DateTime waitingEstablishAckTime = DateTime.Now;

                    while (waitingEstablishAckTime.AddSeconds(20) > DateTime.Now)
                    {
                        if (_twimeConnectionSuccessful)
                            return;

                        Thread.Sleep(100);
                        SendLogMessage("Wait TWIME connection", LogMessageType.System);
                    }

                    SendLogMessage("There was no Logon from the server", LogMessageType.Error);
                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                    }

                }

            }
            catch (Exception ex)
            {
                SendLogMessage("Error connect TWIME server " + ex.ToString(), LogMessageType.Error);
                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
        }

        private void CreateFIXTradeConnection(string fixTradeAddress, int fixTradePort)
        {
            _fixTradeTcpClient = new TcpClient(fixTradeAddress, fixTradePort);
            _fixTradeStream = _fixTradeTcpClient.GetStream();

            if (_fixTradeTcpClient.Connected)
            {
                WriteLogTradingServerEvent("\n-------------- New FIX Session -------------\n");

                _fixTradeMessages = new FixMessageConstructor(_senderCompID, _FIXTradeTargetCompID);

                bool resetSeqNum = _msgSeqNum == 1 ? true : false;

                string logonMsg = _fixTradeMessages.LogonMessage(_msgSeqNum, 30, resetSeqNum);

                SendFIXTradeMessage(logonMsg);

                DateTime waitingLogonTime = DateTime.Now;

                while (waitingLogonTime.AddSeconds(20) > DateTime.Now)
                {
                    if (_fixTradeConnectionSuccessful)
                        return;

                    Thread.Sleep(1000);
                    SendLogMessage("Wait FIXtrade connection", LogMessageType.System);
                }

                SendLogMessage("There was no Logon from the server", LogMessageType.Error);
                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            else
            {
                SendLogMessage("TCP client for FIX not created", LogMessageType.Error);
                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
        }

        private void SendFIXTradeMessage(string message)
        {
            try
            {
                byte[] dataMes = Encoding.UTF8.GetBytes(message);

                _fixTradeStream.Write(dataMes, 0, dataMes.Length);
            }
            catch (Exception ex)
            {
                SendLogMessage("Error sending FIX Message " + ex.ToString(), LogMessageType.Error);

                _fixTradeStream = null;
                _fixTradeTcpClient = null;

                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            _msgSeqNum++;

            WriteLogTradingServerEvent("Sent: " + message);
        }

        #endregion

        #region  7 Security subscribe

        private Dictionary<string, string> _secNameById = new Dictionary<string, string>(); // для получения имени инструмента по коду
        private Dictionary<string, string> _secIdByName = new Dictionary<string, string>(); // для получения кода инструмента по имени
        private List<Security> _subscribedOptions = new List<Security>(); // для получения CFICode для торговых операций по опционам

        private List<string> _subscribedSecurities = new List<string>();

        public void Subscribe(Security security)
        {
            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                return;
            }

            try
            {
                for (int i = 0; i < _subscribedSecurities.Count; i++)
                {
                    if (_subscribedSecurities[i].Equals(security.NameId))
                    {
                        return;
                    }
                }

                if (_tradesIncrementalSocketA == null && _tradesIncrementalSocketB == null
                    && _ordersIncrementalSocketA == null && _ordersIncrementalSocketB == null)
                {
                    CreateSocketConnections("Derivative trades", "Incremental");
                    CreateSocketConnections("Full orders log", "Incremental");
                }

                if (_ordersSnapshotSocketA == null && _ordersSnapshotSocketB == null)
                {
                    CreateSocketConnections("Full orders log", "Snapshot");
                }

                _ordersSnapshotsByName.Add(security.NameId, new Snapshot());
                _waitingDepthChanges.Add(security.NameId, new List<OrderChange>());
                _stockChanges.Add(security.NameId, new List<OrderChange>());

                //добавляем в необходимые списки
                _depthChanges.Add(security.NameId, new List<OrderChange>());
                _tradeNumsForCheckMissed.Add(security.NameId, new List<NumbersMD>());
                _orderNumsForCheckMissed.Add(security.NameId, new List<NumbersMD>());
                _controlDepths.Add(security.NameId, new ControlFastDepth());

                _subscribedSecurities.Add(security.NameId);
                _secNameById.Add(security.NameId, security.Name);
                _secIdByName.Add(security.Name, security.NameId);

                if (security.NameClass != "Futures")
                {
                    _subscribedOptions.Add(security);
                }

                if (_subscribedSecurities.Count == 1) // запускаем проверку FAST соединения с момента подписки на инструмент
                {
                    _timeLastDataReceipt = DateTime.Now;
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent { add { } remove { } }

        #endregion

        #region 8 Sockets parsing messages

        private bool _allSecuritiesLoaded = false;

        private void InstrumentDefinitionsReader()
        {
            Thread.Sleep(1000);

            byte[] buffer = new byte[4096];

            List<long> futuresMsgIDs = new List<long>();
            List<Security> securities = new List<Security>();
            List<long> optionsMsgIDs = new List<long>();

            DateTime timeLastDataReceipt = DateTime.Now;
            Context context = null;

            long totNumReportsFut = 0;
            long totNumReportsOpt = 0;
            bool optionsLoaded = false;
            bool futuresLoaded = false;

            List<string> excessSecurities = new List<string>();

            while (true)
            {
                try
                {
                    if (_socketFuturesStreamA == null || _socketFuturesStreamB == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (_useOptions)
                    {
                        if (_socketOptionsStreamA == null || _socketOptionsStreamB == null)
                        {
                            Thread.Sleep(1);
                            continue;
                        }
                    }

                    if (_allSecuritiesLoaded)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (context == null)
                    {
                        context = CreateNewContext();
                    }

                    for (int s = 0; s < _socketsInstruments.Count; s++)
                    {
                        int length = 0;

                        try
                        {
                            lock (_socketLockerInstruments)
                            {

                                if (_socketFuturesStreamA == null || _socketFuturesStreamB == null)
                                    continue;

                                if (_useOptions)
                                {
                                    if (_socketOptionsStreamA == null || _socketOptionsStreamB == null)
                                        continue;
                                }

                                length = _socketsInstruments[s].Receive(buffer);
                            }
                        }
                        catch (SocketException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            SendLogMessage("Error receiving FAST Message " + ex.ToString(), LogMessageType.Error);
                            if (ServerStatus != ServerConnectStatus.Disconnect)
                            {
                                ServerStatus = ServerConnectStatus.Disconnect;
                                DisconnectEvent();
                            }
                        }

                        using (MemoryStream stream = new MemoryStream(buffer, 4, length))
                        {
                            FastDecoder decoder = new FastDecoder(context, stream);
                            OpenFAST.Message msg = decoder.ReadMessage();

                            if (msg == null)
                                continue;

                            string msgType = msg.GetString("MessageType");
                            long msgSeqNum = long.Parse(msg.GetString("MsgSeqNum"));

                            if (msgType == "d") /// security definition
                            {
                                timeLastDataReceipt = DateTime.Now;

                                if (_useOptions)
                                {
                                    if (optionsLoaded && futuresLoaded)
                                    {
                                        _allSecuritiesLoaded = true;
                                        _securities = securities;
                                    }
                                }
                                else
                                {
                                    if (futuresLoaded)
                                    {
                                        _allSecuritiesLoaded = true;
                                        _securities = securities;
                                    }
                                }

                                string symbol = msg.GetString("Symbol");
                                string securityID = msg.GetString("SecurityID");
                                string marketId = msg.GetString("MarketID");

                                if (marketId != "MOEX")
                                    continue;

                                bool securityAlreadyPresent = false;

                                for (int i = 0; i < securities.Count; i++)
                                {
                                    if (securities[i].NameId == securityID)
                                    {
                                        securityAlreadyPresent = true;
                                        break;
                                    }
                                }

                                if (securityAlreadyPresent) // если бумага в списке есть, дальше не обрабатываем
                                {
                                    continue;
                                }

                                // Обрабатываем новые бумаги

                                string secDecimals = "0";
                                string lot = "1";
                                string CFICode = msg.GetString("CFICode");

                                if (CFICode == "FMXXSX" || CFICode == "ESXXXX") // календарный спред
                                {
                                    if (excessSecurities.Find(s => s == securityID) == null)
                                    {
                                        excessSecurities.Add(securityID);
                                        continue;
                                    }
                                    else
                                    {
                                        continue;
                                    }

                                }

                                string nameFull = Encoding.UTF8.GetString(msg.GetBytes("SecurityDesc"));

                                Security newSecurity = new Security();
                                newSecurity.Name = symbol;
                                newSecurity.NameId = securityID;
                                newSecurity.NameFull = nameFull;
                                newSecurity.Exchange = marketId;

                                if (msg.IsDefined("EvntGrp"))
                                {
                                    SequenceValue secVal = msg.GetValue("EvntGrp") as SequenceValue;

                                    for (int i = 0; i < secVal.Length; i++)
                                    {
                                        GroupValue groupVal = secVal[i] as GroupValue;

                                        string dateTimeExp = groupVal.GetString("EventTime");

                                        DateTime experationDT = DateTime.ParseExact(dateTimeExp, "yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);

                                        newSecurity.Expiration = experationDT;
                                    }
                                }

                                if (CFICode.StartsWith("O"))
                                {
                                    if (optionsMsgIDs.FindIndex(nmb => nmb == msgSeqNum) != -1)
                                    {
                                        continue;
                                    }

                                    newSecurity.NameClass = "Option-" + CFICode;
                                    newSecurity.SecurityType = SecurityType.Option;
                                    newSecurity.Strike = msg.GetString("StrikePrice").ToDecimal();

                                    if (CFICode.StartsWith("OC"))
                                        newSecurity.OptionType = OptionType.Call;
                                    else
                                        newSecurity.OptionType = OptionType.Put;

                                    totNumReportsOpt = msg.GetLong("TotNumReports");

                                    optionsMsgIDs.Add(msgSeqNum);

                                    if (optionsMsgIDs.Count == totNumReportsOpt)
                                    {
                                        optionsLoaded = true;

                                        SendLogMessage($"Загружено {optionsMsgIDs.Count} опционов.", LogMessageType.System);
                                    }
                                }

                                if (CFICode.StartsWith("F"))
                                {
                                    if (futuresMsgIDs.FindIndex(nmb => nmb == msgSeqNum) != -1)
                                    {
                                        continue;
                                    }

                                    totNumReportsFut = msg.GetLong("TotNumReports");
                                    futuresMsgIDs.Add(msgSeqNum);

                                    if (futuresMsgIDs.Count == totNumReportsFut - excessSecurities.Count)
                                    {
                                        futuresLoaded = true;

                                        SendLogMessage($"Загружено {futuresMsgIDs.Count} фьючерсов.", LogMessageType.System);
                                    }

                                    newSecurity.NameClass = "Futures";
                                    newSecurity.SecurityType = SecurityType.Futures;
                                }

                                string SecurityTradingStatus = msg.GetString("SecurityTradingStatus");

                                if (SecurityTradingStatus != "18" || SecurityTradingStatus != "121")
                                {
                                    newSecurity.State = SecurityStateType.Activ;
                                }
                                else
                                {
                                    newSecurity.State = SecurityStateType.Close;
                                }

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

                                newSecurity.PriceStepCost = msg.GetString("MinPriceIncrementAmount").ToDecimal();
                                newSecurity.DecimalsVolume = 1;
                                newSecurity.Decimals = int.Parse(secDecimals);
                                newSecurity.Decimals = GetDecimals(newSecurity.PriceStep);
                                newSecurity.Go = msg.GetString("InitialMarginOnBuy").ToDecimal();
                                newSecurity.PriceLimitLow = msg.GetString("LowLimitPx").ToDecimal();
                                newSecurity.PriceLimitHigh = msg.GetString("HighLimitPx").ToDecimal();

                                securities.Add(newSecurity);

                                if (securities.Count % 100 == 0)
                                {
                                    SendLogMessage($"В списке {securities.Count} бумаг.", LogMessageType.System);
                                }
                            }
                        }
                    }
                }
                catch (OpenFAST.Error.DynErrorException ex)
                {
                    // TODO: внести в мастер
                    SendLogMessage(ex.ToString(), LogMessageType.Error);

                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                    }

                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        Dictionary<long, OpenFAST.Message> _tradesIncremental = new Dictionary<long, OpenFAST.Message>();

        private void GetFastMessagesByTrades()
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

                    // если долго не получаем данные, отключаемся
                    if (_timeLastDataReceipt.AddMinutes(5) < DateTime.Now)
                    {
                        SendLogMessage("There has been no data on trades for too long!", LogMessageType.Error);
                        if (ServerStatus != ServerConnectStatus.Disconnect)
                        {
                            ServerStatus = ServerConnectStatus.Disconnect;
                            DisconnectEvent();
                        }
                    }

                    if (context == null)
                    {
                        context = CreateNewContext();
                    }

                    // получаем постоянно инкрементальные обновления и при необходимости снэпшоты
                    for (int s = 0; s < _socketsTrades.Count; s++)
                    {
                        int length = 0;
                        try
                        {
                            lock (_socketLockerTradesIncremental)
                            {
                                if (_tradesIncrementalSocketA == null || _tradesIncrementalSocketB == null)
                                {
                                    continue;
                                }

                                length = _socketsTrades[s].Receive(buffer);
                            }
                        }
                        catch (SocketException)
                        {
                            // обычно возникает если мы прерываем блокирующую операцию
                            break;
                        }
                        catch (Exception ex)
                        {
                            SendLogMessage("Error receiving FAST Message " + ex.ToString(), LogMessageType.Error);
                            if (ServerStatus != ServerConnectStatus.Disconnect)
                            {
                                ServerStatus = ServerConnectStatus.Disconnect;
                                DisconnectEvent();
                            }
                        }

                        if (length == 0)
                            continue;

                        _timeLastDataReceipt = DateTime.Now;

                        using (MemoryStream stream = new MemoryStream(buffer, 4, length))
                        {
                            FastDecoder decoder = new FastDecoder(context, stream);
                            OpenFAST.Message msg = decoder.ReadMessage();

                            long msgSeqNum = msg.GetLong("MsgSeqNum");

                            if (msg.GetString("MessageType") == "X")
                            {
                                bool needAddMsg = IsMessageMissed(_tradesIncremental, msgSeqNum, msg);

                                if (needAddMsg) // если предыдущего сообщения с таким номером не было, т.е. обрабытываем только уникальные сообщения
                                {
                                    if (msg.IsDefined("MDEntries"))
                                    {
                                        SendMsgToQueue(msg, _tradeMessages);
                                    }
                                }
                            }

                            if (msg.GetString("MessageType") == "4") // сброс номеров
                            {
                                _tradesIncremental.Clear();
                                WriteLogTrades("Цикл изменений по трейдам закончился");
                            }

                            if (msg.GetString("MessageType") == "h" && msg.GetString("TradSesStatus") == "3") // Trading Session Status
                            {
                                SendLogMessage("Сессия завершена", LogMessageType.System);

                                // отменяем активные однодневные ордера т.к. на бирже их снимут
                                if (_ordersForClearing.Count > 0)
                                {
                                    Dictionary<int, Order>.Enumerator enumerator = _ordersForClearing.GetEnumerator();

                                    while (enumerator.MoveNext())
                                    {
                                        Order order = enumerator.Current.Value;
                                        order.State = OrderStateType.Cancel;
                                        order.TimeCancel = DateTime.UtcNow.AddHours(3);
                                        order.TimeCallBack = DateTime.UtcNow.AddHours(3);
                                        order.Comment = "This order was withdrawn by the exchange due to the expiration date";

                                        MyOrderEvent(order);
                                    }
                                    _ordersForClearing.Clear();
                                }

                                // чистим  стакан
                                Dictionary<string, List<OrderChange>>.Enumerator orderLists = _depthChanges.GetEnumerator();

                                while (orderLists.MoveNext())
                                {
                                    orderLists.Current.Value.Clear();
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

        private ConcurrentQueue<OpenFAST.Message> _tradeMessages = new ConcurrentQueue<OpenFAST.Message>();
        private Dictionary<string, List<NumbersMD>> _tradeNumsForCheckMissed = new Dictionary<string, List<NumbersMD>>(); // для проверки пропука данных по RptSeq

        private void TradeMessagesReader()
        {
            Thread.Sleep(1000);

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

                    // в этой очереди снэпшоты и трейды по подписанным инструментам
                    _tradeMessages.TryDequeue(out msg);

                    if (msg == null)
                    {
                        continue;
                    }

                    string msgType = msg.GetString("MessageType");
                    long msgSeqNum = msg.GetLong("MsgSeqNum");

                    if (msgType == "X")
                    {
                        if (msg.IsDefined("MDEntries"))
                        {
                            SequenceValue secVal = msg.GetValue("MDEntries") as SequenceValue;

                            for (int i = 0; i < secVal.Length; i++)
                            {
                                GroupValue groupVal = secVal[i] as GroupValue;

                                string MDEntryType = groupVal.GetString("MDEntryType");

                                string nameID = groupVal.GetString("SecurityID");

                                if (!IsSubscribedToThisSecurity(nameID)) // если не подписаны на этот инструмент, трейд не берем
                                    continue;

                                int RptSeqFromTrades = groupVal.GetInt("RptSeq");

                                // проверка пропуска даных по RptSeq и дублирования
                                if (_tradeNumsForCheckMissed[nameID].Find(n => n.RptSeq == RptSeqFromTrades) != null)
                                    continue;

                                _tradeNumsForCheckMissed[nameID].Add(new NumbersMD { MsgSeqNum = msgSeqNum, RptSeq = RptSeqFromTrades });

                                if (_endOfRecoveryOrders & _endOfRecoveryTrades)
                                {
                                    int missedRptSeqCount = 0;

                                    bool missingTradesData = IsDataMissed(_tradeNumsForCheckMissed[nameID], out _missingTradesBeginSeqNo, out _missingTradesEndSeqNo, out missedRptSeqCount, out _missingTradesRptSeqNums);

                                    if (!_limitTradeReq && missedRptSeqCount >= 2 && missingTradesData)
                                    {
                                        WriteLogTrades($"По инструменту {_secNameById[nameID]} пропуск данных о трейдах. Номера сообщений с {_missingTradesBeginSeqNo} по {_missingTradesEndSeqNo}. Количество пропущенных RptSeq: {missedRptSeqCount}\n");

                                        if (_historicalReplayEndPointForTrades == null)
                                            CreateSocketConnections("Derivative trades", "Historical Replay");

                                        _endOfRecoveryTrades = false;
                                    }
                                }

                                if (MDEntryType != "2")
                                    continue;

                                Trade trade = new Trade();
                                trade.SecurityNameCode = _secNameById[nameID];
                                trade.Price = groupVal.GetString("MDEntryPx").ToDecimal();

                                string timeTrade = groupVal.GetString("MDEntryTime");

                                if (timeTrade.Length == 14)
                                    timeTrade = "0" + timeTrade;

                                string timePart = timeTrade.Substring(0, 6); // "143249" - Время (ччммсс)
                                string nanosecondsPart = timeTrade.Substring(6); // "756700432" - Наносекунды

                                timeTrade = DateTime.UtcNow.ToString("ddMMyyyy") + timePart;
                                DateTime tradeDateTime = DateTime.ParseExact(timeTrade, "ddMMyyyyHHmmss", System.Globalization.CultureInfo.InvariantCulture);
                                long nanoseconds = long.Parse(nanosecondsPart);
                                tradeDateTime = tradeDateTime.AddTicks(nanoseconds / 100); // Переводим наносекунды в тики

                                trade.Time = tradeDateTime.AddHours(3);
                                trade.Id = groupVal.GetString("MDEntryID");
                                trade.Side = groupVal.GetString("OrderSide") == "1" ? Side.Buy : Side.Sell;
                                trade.Volume = groupVal.GetString("MDEntrySize").ToDecimal();

                                NewTradesEvent(trade);

                                WriteLogTrades($"Получен трейд в СИСТЕМУ: RptSeq: {RptSeqFromTrades}, Инструмент: {trade.SecurityNameCode}, {trade.Side}, Цена: {trade.Price}, Время: {trade.Time}, Объем: {trade.Volume}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        Dictionary<long, OpenFAST.Message> _ordersIncremental = new Dictionary<long, OpenFAST.Message>();
        Dictionary<long, OpenFAST.Message> _ordersSnapshotsMsgs = new Dictionary<long, OpenFAST.Message>(); // используется для проверки пропуска собщений

        private void GetFastMessagesByOrders()
        {
            byte[] buffer = new byte[4096];
            OpenFAST.Context context = null;

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

                    // если долго не получаем данные, отключаемся
                    if (_timeLastDataReceipt.AddMinutes(5) < DateTime.Now)
                    {
                        SendLogMessage("There has been no data on orders for too long!", LogMessageType.Error);
                        if (ServerStatus != ServerConnectStatus.Disconnect)
                        {
                            ServerStatus = ServerConnectStatus.Disconnect;
                            DisconnectEvent();
                        }
                    }

                    if (context == null)
                    {
                        context = CreateNewContext();
                    }

                    for (int s = 0; s < _socketsOrders.Count; s++)
                    {
                        int length = 0;
                        try
                        {
                            lock (_socketLockerOrdersIncremental)
                            {
                                if (_ordersIncrementalSocketA == null || _ordersIncrementalSocketB == null)
                                {
                                    continue;
                                }

                                length = _socketsOrders[s].Receive(buffer);
                            }
                        }
                        catch (SocketException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            SendLogMessage("Error receiving FAST Message " + ex.ToString(), LogMessageType.Error);
                            if (ServerStatus != ServerConnectStatus.Disconnect)
                            {
                                ServerStatus = ServerConnectStatus.Disconnect;
                                DisconnectEvent();
                            }
                        }

                        if (length == 0)
                            continue;

                        _timeLastDataReceipt = DateTime.Now;

                        using (MemoryStream stream = new MemoryStream(buffer, 4, length))
                        {
                            FastDecoder decoder = new FastDecoder(context, stream);
                            OpenFAST.Message msg = decoder.ReadMessage();

                            long msgSeqNum = msg.GetLong("MsgSeqNum");

                            if (msg.GetString("MessageType") == "W")
                            {
                                string nameID = msg.GetString("SecurityID");

                                bool needAddMsg = IsMessageMissed(_ordersSnapshotsMsgs, msgSeqNum, msg);

                                if (needAddMsg)
                                {
                                    if (IsSubscribedToThisSecurity(nameID))
                                        _orderMessages.Enqueue(msg);
                                }
                            }

                            if (msg.GetString("MessageType") == "X")
                            {
                                bool needAddMsg = IsMessageMissed(_ordersIncremental, msgSeqNum, msg);

                                if (needAddMsg) // если предыдущего сообщения с таким номером не было
                                {
                                    if (msg.IsDefined("MDEntries"))
                                    {
                                        SendMsgToQueue(msg, _orderMessages);
                                    }
                                }
                            }

                            if (msg.GetString("MessageType") == "4")
                            {
                                _ordersSnapshotsMsgs.Clear();
                                WriteLogOrders("Цикл снэпшотов ордеров закончился");
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

        private ConcurrentQueue<OpenFAST.Message> _orderMessages = new ConcurrentQueue<OpenFAST.Message>();
        private Dictionary<string, Snapshot> _ordersSnapshotsByName = new Dictionary<string, Snapshot>(); // для восстановления
        private Dictionary<string, List<OrderChange>> _depthChanges = new Dictionary<string, List<OrderChange>>(); // для обновления стакана
        private Dictionary<string, List<OrderChange>> _waitingDepthChanges = new Dictionary<string, List<OrderChange>>(); // для ожидания снэпшота
        private Dictionary<string, List<NumbersMD>> _orderNumsForCheckMissed = new Dictionary<string, List<NumbersMD>>(); // для проверки пропука данных по RptSeq
        private DateTime _lastMdTime = DateTime.MinValue;
        private Dictionary<string, int> _minRptSeqFromOrders = new Dictionary<string, int>();
        private Dictionary<string, List<OrderChange>> _stockChanges = new Dictionary<string, List<OrderChange>>(); // удаляющие и изменяющие заявки, не нашедшие своих добавляющих заявок
        Dictionary<string, ControlFastDepth> _controlDepths = new Dictionary<string, ControlFastDepth>();

        private void OrderMessagesReader()
        {
            Thread.Sleep(1000);

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

                    // в этой очереди снэпшоты и orders по подписанным инструментам
                    _orderMessages.TryDequeue(out msg);

                    if (msg == null)
                    {
                        continue;
                    }

                    string msgType = msg.GetString("MessageType");
                    long msgSeqNum = msg.GetLong("MsgSeqNum");

                    if (msgType == "X") // инкрементальные данные
                    {
                        if (msg.IsDefined("MDEntries"))
                        {
                            SequenceValue secVal = msg.GetValue("MDEntries") as SequenceValue;

                            for (int i = 0; i < secVal.Length; i++)
                            {
                                GroupValue groupVal = secVal[i] as GroupValue;

                                string nameID = groupVal.GetString("SecurityID");
                                string id = groupVal.GetString("MDEntryID");

                                if (!IsSubscribedToThisSecurity(nameID)) // если не подписаны на этот инструмент, ордер не берем
                                    continue;

                                int RptSeqFromOrder = groupVal.GetInt("RptSeq");

                                // проверка пропуска даных по RptSeq и дублирования сообщений
                                if (_orderNumsForCheckMissed[nameID].Find(n => n.RptSeq == RptSeqFromOrder) != null)
                                    continue;

                                _orderNumsForCheckMissed[nameID].Add(new NumbersMD { MsgSeqNum = msgSeqNum, RptSeq = RptSeqFromOrder });

                                OrderChange newOrderChange = GetNewOrderChange(groupVal, nameID);

                                // если новая заявка пришла из восстановленных, надо проверить, может её уже пытались удалить или изменить
                                if (_stockChanges.ContainsKey(newOrderChange.NameID) && _stockChanges[newOrderChange.NameID].Count > 0)
                                {
                                    //по востановленой заявке есть удаляющие или изменяющие ордера

                                    OrderChange storedOrder;

                                    do
                                    {
                                        storedOrder = _stockChanges[newOrderChange.NameID].Find(o => o.MDEntryID == newOrderChange.MDEntryID);

                                        if (storedOrder != null && storedOrder.Action == OrderAction.Change)
                                        {
                                            newOrderChange.Volume = storedOrder.Volume;

                                            _stockChanges[newOrderChange.NameID].Remove(storedOrder);
                                        }
                                        else if (storedOrder != null && storedOrder.Action == OrderAction.Delete)
                                        {
                                            newOrderChange.Volume = -1;

                                            _stockChanges[newOrderChange.NameID].Remove(storedOrder);

                                            break;
                                        }

                                    } while (storedOrder != null);

                                    if (newOrderChange.Volume == -1)
                                        continue;

                                }

                                if (_endOfRecoveryOrders & _endOfRecoveryTrades)
                                {
                                    int missedRptSeqCount = 0;

                                    bool missingOrdersData = IsDataMissed(_orderNumsForCheckMissed[nameID], out _missingOrdersBeginSeqNo, out _missingOrdersEndSeqNo, out missedRptSeqCount, out _missingOrdersRptSeqNums);

                                    if (!_limitOrderReq && missedRptSeqCount >= 2 && missingOrdersData)
                                    {
                                        WriteLogOrders($"Требуется восстановление заявок в стакане по {_secNameById[nameID]}. Номера сообщений с {_missingOrdersBeginSeqNo} по {_missingOrdersEndSeqNo}. Количество пропущенных RptSeq: {missedRptSeqCount}.");

                                        if (_historicalReplayEndPointForOrders == null)
                                            CreateSocketConnections("Full orders log", "Historical Replay");

                                        lock (_socketLockerHistoricalReplay)
                                        {
                                            _endOfRecoveryOrders = false;
                                        }
                                    }

                                }

                                // храним минимальный номер обновления по инструменту
                                if (_minRptSeqFromOrders.ContainsKey(nameID))
                                {
                                    if (_minRptSeqFromOrders[nameID] > RptSeqFromOrder)
                                    {
                                        _minRptSeqFromOrders[nameID] = RptSeqFromOrder;
                                    }
                                }
                                else
                                {
                                    _minRptSeqFromOrders.Add(nameID, RptSeqFromOrder);
                                }


                                if (_ordersSnapshotsByName.ContainsKey(nameID))
                                {
                                    if (!_ordersSnapshotsByName[nameID].SnapshotWasApplied)
                                    {
                                        // пока снэпшот не применен, изменения заявок складываем в ожидающие
                                        _waitingDepthChanges[nameID].Add(newOrderChange);

                                        WriteLogOrders($"Получен ордер в ОЖИДАЮЩИЕ: MsgSeqNum: {msgSeqNum}, " +
                                            $"RptSeq: {RptSeqFromOrder}, Инструмент: {_secNameById[nameID]}, " +
                                            $"ID: {newOrderChange.MDEntryID}, Действие: {newOrderChange.Action}, " +
                                            $"Type: {newOrderChange.OrderType}, Цена: {newOrderChange.Price}, Объем: {newOrderChange.Volume}");
                                    }
                                    else
                                    {
                                        if (newOrderChange.RptSeq <= _ordersSnapshotsByName[nameID].RptSeq)
                                            continue;

                                        // обработка и сразу в систему
                                        bool isUpdOrdersList = UpdateOrderData(nameID, newOrderChange);

                                        if (isUpdOrdersList)
                                        {
                                            UpdateDepth(_depthChanges[nameID], nameID);

                                            WriteLogOrders($"Получен ордер в СИСТЕМУ. СТАКАН ОБНОВЛЕН: MsgSeqNum: {msgSeqNum}, " +
                                                $"RptSeq: {RptSeqFromOrder}, Инструмент: {_secNameById[nameID]}, " +
                                                 $"ID: {newOrderChange.MDEntryID}, Действие: {newOrderChange.Action}, " +
                                            $"Type: {newOrderChange.OrderType}, Цена: {newOrderChange.Price}, Объем: {newOrderChange.Volume}");
                                        }
                                    }
                                }

                            }
                        }
                    }

                    if (msgType == "W") // снэпшот
                    {
                        string nameID = msg.GetString("SecurityID");

                        if (_ordersSnapshotsByName[nameID].SnapshotWasApplied == true)
                            continue;

                        int RptSeq = msg.GetInt("RptSeq");
                        string LastFragment = msg.GetString("LastFragment");
                        string RouteFirst = msg.GetString("RouteFirst");
                        long LastMsgSeqNumProcessed = msg.GetLong("LastMsgSeqNumProcessed");
                        long MsgSeqNum = msg.GetLong("MsgSeqNum");

                        SnapshotFragment fragment = new SnapshotFragment();
                        fragment.MsgSeqNum = MsgSeqNum;
                        fragment.RptSeq = RptSeq;
                        fragment.LastFragment = LastFragment == "1" ? true : false;
                        fragment.RouteFirst = RouteFirst == "1" ? true : false;
                        fragment.SymbolNameID = nameID;
                        fragment.mdLevel = new List<MarketDepthLevel>();

                        WriteLogOrders($"Получен фрагмент снэпшота по инструменту: {_secNameById[nameID]}, MsgSeqNum: {MsgSeqNum}, Первый: {fragment.RouteFirst}, Последний: {fragment.LastFragment}, RptSeq: {RptSeq}");

                        if (msg.IsDefined("MDEntries"))
                        {
                            SequenceValue secVal = msg.GetValue("MDEntries") as SequenceValue;

                            for (int i = 0; i < secVal.Length; i++)
                            {
                                GroupValue groupVal = secVal[i] as GroupValue;

                                OrderChange newOrderChange = GetNewOrderChange(groupVal, nameID);

                                MarketDepthLevel level = new MarketDepthLevel();

                                string MDEntryType = groupVal.GetString("MDEntryType");
                                double price = groupVal.GetString("MDEntryPx").ToDouble();
                                string Id = groupVal.GetString("MDEntryID");
                                double volume = groupVal.GetString("MDEntrySize").ToDouble();


                                switch (MDEntryType)// 0 - котировка на покупку, 1 - котровка на продажу
                                {
                                    case "0":
                                        level.Bid = volume;
                                        level.Ask = 0;
                                        break;
                                    case "1":
                                        level.Ask = volume;
                                        level.Bid = 0;
                                        break;
                                    case "J": // пустой снэпшот
                                        SendLogMessage($"The snapshot for depth {nameID} symbol is empty", LogMessageType.System);
                                        _ordersSnapshotsByName[nameID].SnapshotWasApplied = true;
                                        continue;
                                    default:
                                        SendLogMessage("Order type ERROR", LogMessageType.Error);
                                        break;
                                }

                                level.Price = price;

                                fragment.mdLevel.Add(level); // для первичного заполнения стакана

                                _depthChanges[nameID].Add(newOrderChange);

                                WriteLogOrders($"Фрагмент содержит запись {i}: Инструмент: {_secNameById[nameID]}, ID: {Id}, Тип: {newOrderChange.OrderType}, Цена: {price}, Объем: {volume}");
                            }
                        }

                        // добавляем сообщение в снэпшот и проверяем его готовность
                        if (_ordersSnapshotsByName[nameID].SnapshotFragments == null)
                        {
                            _ordersSnapshotsByName[nameID].SnapshotFragments = new List<SnapshotFragment>();
                            _ordersSnapshotsByName[nameID].SnapshotFragments.Add(fragment);
                            _ordersSnapshotsByName[nameID].RptSeq = fragment.RptSeq;
                            _ordersSnapshotsByName[nameID].SymbolNameID = fragment.SymbolNameID;
                        }
                        else
                        {
                            _ordersSnapshotsByName[nameID].SnapshotFragments.Add(fragment);

                            if (_ordersSnapshotsByName[nameID].SnapshotFragments[0].RptSeq != fragment.RptSeq)
                            {
                                _ordersSnapshotsByName[nameID].SnapshotFragments.Clear();

                                WriteLogOrders($"Пришел фрагмент по инструменту {_secNameById[nameID]} с другим RptSeq. Ждем новый цикл.");
                            }
                        }

                        // если снэпшот сформирован
                        if (fragment.LastFragment & _ordersSnapshotsByName[nameID].IsComletedSnapshot(_ordersSnapshotsByName[nameID].SnapshotFragments) == true)
                        {
                            WriteLogOrders($"Снэпшот {_secNameById[nameID]} сформирован.");

                            // формируем первый стакан из снэпшота
                            MarketDepth marketDepth = new MarketDepth();

                            marketDepth = MakeFirstDepth(nameID, _ordersSnapshotsByName);

                            marketDepth.SecurityNameCode = _secNameById[nameID];
                            marketDepth.Time = DateTime.UtcNow.AddHours(3);

                            if (_waitingDepthChanges[nameID].Count > 0 & _minRptSeqFromOrders.ContainsKey(nameID))
                            {
                                if ((_ordersSnapshotsByName[nameID].RptSeq >= _minRptSeqFromOrders[nameID]
                                     || _ordersSnapshotsByName[nameID].RptSeq == _minRptSeqFromOrders[nameID] - 1)
                                     & _ordersSnapshotsByName[nameID].RptSeq != 0)
                                {
                                    WriteLogOrders($"Пытаюсь применить ожидающие заявки по инструменту {_secNameById[nameID]} .");
                                    // меняем стакан, согласно ожидающих заявок
                                    for (int j = 0; j < _waitingDepthChanges[nameID].Count; j++)
                                    {
                                        if (_waitingDepthChanges[nameID][j].RptSeq <= _ordersSnapshotsByName[nameID].RptSeq)
                                            continue;

                                        UpdateOrderData(nameID, _waitingDepthChanges[nameID][j]);
                                    }

                                    // отправили первый стакан
                                    MarketDepthEvent(marketDepth);

                                    WriteLogOrders($"Первый стакан по инструменту {_secNameById[nameID]} отправлен.");

                                    _ordersSnapshotsByName[nameID].SnapshotWasApplied = true;

                                    // чистим фрагменты снэпшота и ожидающие трейды
                                    _ordersSnapshotsByName[nameID].SnapshotFragments.Clear();
                                    _waitingDepthChanges[nameID].Clear();
                                }
                                else
                                {
                                    SendLogMessage("The market depth levels are not yet correct.Wait for a new snapshot", LogMessageType.Error);

                                    _ordersSnapshotsByName[nameID].SnapshotFragments.Clear();
                                    _ordersSnapshotsByName[nameID].SnapshotFragments = null;
                                    continue;
                                }
                            }
                            else
                            {
                                // отправили первый стакан
                                MarketDepthEvent(marketDepth);

                                WriteLogOrders($"Первый стакан по инструменту {_secNameById[nameID]} отправлен.");


                                // если нет ожидающих заявок из инкрементов, считаем снэпшот примененым
                                _ordersSnapshotsByName[nameID].SnapshotWasApplied = true;
                                WriteLogOrders($"Ожидающих заявок нет.Снэпшот по инструменту {_secNameById[nameID]} применен");
                            }

                            WriteLogOrders($"Снэпшот по инструменту {_secNameById[nameID]} применен");

                            if (!IsNeedSnapshotSockets(_ordersSnapshotsByName) && _ordersSnapshotSocketA != null)
                            {
                                lock (_socketLockerOrdersSnapshots)
                                {
                                    _socketsOrders.Remove(_ordersSnapshotSocketA);
                                    _socketsOrders.Remove(_ordersSnapshotSocketB);

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

                                    _ordersSnapshotSocketA = null;
                                    _ordersSnapshotSocketB = null;
                                }
                                WriteLogOrders("Сокеты снэпшотов закрыты");
                            }
                        }
                        else if (fragment.LastFragment & _ordersSnapshotsByName[nameID].IsComletedSnapshot(_ordersSnapshotsByName[nameID].SnapshotFragments) == false)
                        {
                            WriteLogOrders($"\n-!--Пришел последний фрагмент, но снэпшот по инструменту: {_secNameById[nameID]} не сформирован. Ждем новый цикл \n");
                            _ordersSnapshotsByName[nameID].SnapshotFragments.Clear();
                        }
                        else
                        {
                            WriteLogOrders($"Снэпшот по инструменту: {_secNameById[nameID]} не сформирован. Ждем");
                        }
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private OrderChange GetNewOrderChange(GroupValue groupVal, string nameID)
        {
            OrderChange newOrderChange = new OrderChange();

            string MDEntryType = groupVal.GetString("MDEntryType");

            int RptSeqFromOrder = 0;

            if (groupVal.TryGetValue("RptSeq", out IFieldValue value))
                RptSeqFromOrder = (value as ScalarValue).ToInt();

            OrderType orderType;

            switch (MDEntryType)// 0 - котировка на покупку, 1 - котровка на продажу
            {
                case "0": orderType = OrderType.Bid; break;
                case "1": orderType = OrderType.Ask; break;
                default: orderType = OrderType.None; break;
            }

            OrderAction action;

            string MDUpdateAction = "5";

            if (groupVal.TryGetValue("MDUpdateAction", out IFieldValue field))
                MDUpdateAction = field.ToString();

            switch (MDUpdateAction)
            {
                case "0": action = OrderAction.Add; break;
                case "1": action = OrderAction.Change; break;
                case "2": action = OrderAction.Delete; break;
                default: action = OrderAction.None; break;
            }

            double price = groupVal.GetString("MDEntryPx").ToDouble();
            string id = groupVal.GetString("MDEntryID");
            double volume = groupVal.GetString("MDEntrySize").ToDouble();

            newOrderChange.NameID = nameID;
            newOrderChange.MDEntryID = id;
            newOrderChange.OrderType = orderType;
            newOrderChange.Price = price;
            newOrderChange.Action = action;
            newOrderChange.RptSeq = RptSeqFromOrder;
            newOrderChange.Volume = volume;

            return newOrderChange;
        }

        private void UpdateDepth(List<OrderChange> orderChanges, string NameID)
        {
            MarketDepth marketDepth = new MarketDepth();
            List<MarketDepthLevel> asks = new List<MarketDepthLevel>();
            List<MarketDepthLevel> bids = new List<MarketDepthLevel>();
            Dictionary<double, double> mdLevels = new Dictionary<double, double>();

            marketDepth.SecurityNameCode = _secNameById[NameID];

            List<OrderChange> asksByPrice = orderChanges.FindAll(p => p.OrderType == OrderType.Ask);

            asksByPrice.Sort((x, y) => x.Price.CompareTo(y.Price));

            for (int i = 0; i < asksByPrice.Count; i++)
            {
                if (!mdLevels.ContainsKey(asksByPrice[i].Price))
                {
                    mdLevels.Add(asksByPrice[i].Price, asksByPrice[i].Volume);
                }
                else
                {
                    mdLevels[asksByPrice[i].Price] += asksByPrice[i].Volume;
                }
            }

            Dictionary<double, double>.Enumerator levelAsk = mdLevels.GetEnumerator();

            while (levelAsk.MoveNext())
            {
                asks.Add(new MarketDepthLevel()
                {
                    Price = Convert.ToDouble(levelAsk.Current.Key),
                    Ask = Convert.ToDouble(levelAsk.Current.Value)
                });
            }

            List<OrderChange> bidsByPrice = orderChanges.FindAll(p => p.OrderType == OrderType.Bid);

            bidsByPrice.Sort((y, x) => x.Price.CompareTo(y.Price));

            mdLevels.Clear();

            for (int i = 0; i < bidsByPrice.Count; i++)
            {
                if (!mdLevels.ContainsKey(bidsByPrice[i].Price))
                {
                    mdLevels.Add(bidsByPrice[i].Price, bidsByPrice[i].Volume);
                }
                else
                {
                    mdLevels[bidsByPrice[i].Price] += bidsByPrice[i].Volume;
                }
            }

            Dictionary<double, double>.Enumerator levelBid = mdLevels.GetEnumerator();

            while (levelBid.MoveNext())
            {
                bids.Add(new MarketDepthLevel()
                {
                    Price = Convert.ToDouble(levelBid.Current.Key),
                    Bid = Convert.ToDouble(levelBid.Current.Value)
                });
            }

            // удаляяем уровни с нулевым объемом
            List<MarketDepthLevel> nullAsks = asks.FindAll(a => a.Ask == 0);

            if (nullAsks.Count > 0)
            {
                for (int i = 0; nullAsks.Count > 0; i++)
                {
                    asks.Remove(nullAsks[i]);
                }
            }

            List<MarketDepthLevel> nullBids = bids.FindAll(b => b.Bid == 0);

            if (nullBids.Count > 0)
            {
                for (int i = 0; nullBids.Count > 0; i++)
                {
                    bids.Remove(nullBids[i]);
                }
            }

            marketDepth.Asks = asks;
            marketDepth.Bids = bids;

            // удаляем лишние уровни
            const int maxCount = 25;

            if (marketDepth.Bids.Count > maxCount)
                marketDepth.Bids.RemoveRange(maxCount - 1, marketDepth.Bids.Count - maxCount);

            if (marketDepth.Asks.Count > maxCount)
                marketDepth.Asks.RemoveRange(maxCount - 1, marketDepth.Asks.Count - maxCount);

            marketDepth.Time = DateTime.UtcNow.AddHours(3);

            if (_lastMdTime != DateTime.MinValue && _lastMdTime >= marketDepth.Time)
            {
                marketDepth.Time = _lastMdTime.AddTicks(1);
            }

            _lastMdTime = marketDepth.Time;

            // Проверка активности уровней стакана

            if (_controlDepths[NameID].BidsF.Count == 0 && _controlDepths[NameID].AsksF.Count == 0)
            {
                for (int i = 0; i < 5; i++)
                {
                    _controlDepths[NameID].BidsF.Add(new ControlDepthLevel() { Bid = marketDepth.Bids[i].Bid, Price = marketDepth.Bids[i].Price });
                    _controlDepths[NameID].AsksF.Add(new ControlDepthLevel() { Ask = marketDepth.Asks[i].Ask, Price = marketDepth.Asks[i].Price });
                }
            }
            else
            {
                RemoveInactiveLevels(_controlDepths[NameID], marketDepth, NameID);
            }

            MarketDepthEvent(marketDepth);
        }

        private void RemoveInactiveLevels(ControlFastDepth controlDepth, MarketDepth sourceDepth, string NameID)
        {
            List<ControlDepthLevel> inactiveBids = new List<ControlDepthLevel>();
            List<ControlDepthLevel> inactiveAsks = new List<ControlDepthLevel>();

            for (int i = 0; i < controlDepth.BidsF.Count; i++) // проверяем 5 уровней бид, изменились ли они с прошлого обновления стакана
            {
                if (sourceDepth.Bids[i].Bid == controlDepth.BidsF[i].Bid)
                {
                    controlDepth.BidsF[i].ImmutabilityCount++;

                    if (controlDepth.BidsF[i].ImmutabilityCount == 500) // стакан обновился 500 раз, а уровень нет
                    {
                        inactiveBids.Add(controlDepth.BidsF[i]);

                        controlDepth.BidsF.RemoveAt(i);
                        sourceDepth.Bids.RemoveAt(i);

                        i--;
                    }
                }
                else
                {
                    controlDepth.BidsF[i].ImmutabilityCount = 0;

                    controlDepth.BidsF[i].Bid = sourceDepth.Bids[i].Bid;
                    controlDepth.BidsF[i].Price = sourceDepth.Bids[i].Price;
                }
            }

            if (controlDepth.BidsF.Count < 5)
            {
                for (int k = controlDepth.BidsF.Count; k < 5; k++)
                {
                    controlDepth.BidsF.Add(new ControlDepthLevel() { Bid = sourceDepth.Bids[k].Bid, Price = sourceDepth.Bids[k].Price });
                }
            }

            for (int i = 0; i < controlDepth.AsksF.Count; i++) // проверяем 5 уровней аск, изменились ли они с прошлого обновления стакана
            {
                if (sourceDepth.Asks[i].Ask == controlDepth.AsksF[i].Ask && sourceDepth.Asks[i].Price == controlDepth.AsksF[i].Price)
                {

                    controlDepth.AsksF[i].ImmutabilityCount++;

                    if (controlDepth.AsksF[i].ImmutabilityCount == 500)
                    {
                        inactiveAsks.Add(controlDepth.AsksF[i]);

                        // удаляем из стаканов
                        controlDepth.AsksF.RemoveAt(i);
                        sourceDepth.Asks.RemoveAt(i);

                        i--;
                    }
                }
                else
                {
                    controlDepth.AsksF[i].ImmutabilityCount = 0;

                    controlDepth.AsksF[i].Ask = sourceDepth.Asks[i].Ask;
                    controlDepth.AsksF[i].Price = sourceDepth.Asks[i].Price;
                }
            }

            if (controlDepth.AsksF.Count < 5)
            {
                for (int k = controlDepth.AsksF.Count; k < 5; k++)
                {
                    controlDepth.AsksF.Add(new ControlDepthLevel() { Ask = sourceDepth.Asks[k].Ask, Price = sourceDepth.Asks[k].Price });
                }
            }

            if (inactiveAsks.Count > 0)
            {
                for (int i = 0; i < inactiveAsks.Count; i++)
                {
                    int inactiveOrders = _depthChanges[NameID].RemoveAll(o => o.OrderType == OrderType.Ask && o.Price == inactiveAsks[i].Price);

                    if (inactiveOrders > 0)
                        WriteLogOrders($"Удалено {inactiveOrders} не активных заявок Ask по инструменту: {_secNameById[NameID]} по цене {inactiveAsks[i].Price}");
                }
            }

            if (inactiveBids.Count > 0)
            {
                for (int i = 0; i < inactiveBids.Count; i++)
                {
                    int inactiveOrders = _depthChanges[NameID].RemoveAll(o => o.OrderType == OrderType.Ask && o.Price == inactiveBids[i].Price);

                    if (inactiveOrders > 0)
                        WriteLogOrders($"Удалено {inactiveOrders} не активных заявок Bid по инструменту: {_secNameById[NameID]} по цене {inactiveBids[i].Price}");
                }
            }
        }

        // Внести изменения в данные по заявкам, находящимся в стакане
        private bool UpdateOrderData(string NameID, OrderChange orderChange)
        {
            OrderChange order = _depthChanges[NameID].Find(p => p.MDEntryID == orderChange.MDEntryID);

            if (order != null)
            {
                switch (orderChange.Action)
                {
                    case OrderAction.Add:

                        if (orderChange.Volume == 0)
                        {
                            return false;
                        }

                        order.Volume += orderChange.Volume;

                        return true;

                    case OrderAction.Change:

                        if (orderChange.Volume == 0)
                        {
                            SendLogMessage($"Change  order to ZERO!!!", LogMessageType.Error);

                            return false;
                        }

                        order.Volume = orderChange.Volume;

                        return true;

                    case OrderAction.Delete:

                        int index = _depthChanges[NameID].FindIndex(p => p.MDEntryID == orderChange.MDEntryID);

                        if (index != -1)
                        {
                            _depthChanges[NameID].RemoveAt(index);

                            return true;
                        }
                        else
                        {
                            SendLogMessage($"Change index for delete order not found", LogMessageType.Error);
                            return false;
                        }
                        ;

                    default:
                        SendLogMessage($"Action for change order not found", LogMessageType.Error);
                        return false;
                }
            }
            else
            {
                if (orderChange.Action == OrderAction.Delete || orderChange.Action == OrderAction.Change)
                {
                    WriteLogOrders($" Не найдена заявка для удаляющего или изменяющего ордера {_secNameById[NameID]} - {orderChange.OrderType} - N {orderChange.MDEntryID} - {orderChange.Action} - Price:{orderChange.Price} - Объем: {orderChange.Volume}");

                    _stockChanges[NameID].Add(orderChange);

                    return false;
                }

                _depthChanges[NameID].Add(orderChange);

                return true;
            }
        }

        // Собрать первый стакан из снэпшота
        private MarketDepth MakeFirstDepth(string uniqueName, Dictionary<string, Snapshot> ordersSnapshotsByName)
        {
            MarketDepth marketDepth = new MarketDepth();
            List<MarketDepthLevel> asks = new List<MarketDepthLevel>();
            List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

            for (int i = 0; i < ordersSnapshotsByName[uniqueName].SnapshotFragments.Count; i++)
            {
                List<MarketDepthLevel> mdLevels = ordersSnapshotsByName[uniqueName].SnapshotFragments[i].mdLevel;

                for (int k = 0; k < mdLevels.Count; k++)
                {
                    MarketDepthLevel level = new MarketDepthLevel();

                    if (mdLevels[k].Ask != 0)
                    {
                        MarketDepthLevel levelFromAsks = asks.Find(a => a.Price == mdLevels[k].Price);

                        if (levelFromAsks != null) //  если в асках уже есть уровень с этой ценой, прибавляем объем
                        {
                            levelFromAsks.Ask += mdLevels[k].Ask;
                        }
                        else // в асках еще нет уровня с такой ценой
                        {
                            level.Ask = mdLevels[k].Ask;
                            level.Price = mdLevels[k].Price;
                            asks.Add(level);
                        }
                    }
                    if (mdLevels[k].Bid != 0)
                    {
                        MarketDepthLevel levelFromBids = bids.Find(b => b.Price == mdLevels[k].Price);

                        if (levelFromBids != null) //  если в бидах уже есть уровень с этой ценой, прибавляем объем
                            levelFromBids.Bid += mdLevels[k].Bid;
                        else
                        {
                            level.Bid = mdLevels[k].Bid;
                            level.Price = mdLevels[k].Price;
                            bids.Add(level);
                        }
                    }
                }
            }

            asks.Sort((x, y) => x.Price.CompareTo(y.Price));
            bids.Sort((y, x) => x.Price.CompareTo(y.Price));

            marketDepth.Asks = asks;
            marketDepth.Bids = bids;

            // удаляем лишние уровни
            const int maxCount = 25;
            if (marketDepth.Bids.Count > maxCount)
                marketDepth.Bids.RemoveRange(maxCount - 1, marketDepth.Bids.Count - maxCount);

            if (marketDepth.Asks.Count > maxCount)
                marketDepth.Asks.RemoveRange(maxCount - 1, marketDepth.Asks.Count - maxCount);

            return marketDepth;
        }

        private ReplyTwimeMessage _replyTwimeMessage = new ReplyTwimeMessage();

        // Обработка сообщений TWIME протокола
        private void TwimeMessageReader()
        {
            byte[] bufferMsg = new byte[4096];

            try
            {
                int bytesRead = _TwimeTradeStream.Read(bufferMsg, 0, bufferMsg.Length);

                ushort messageId = BitConverter.ToUInt16(bufferMsg, 2);

                if (bytesRead > _replyTwimeMessage.MsgLength[messageId])
                {
                    WriteLogTradingServerEvent($"Пришло больше одного сообщения. Прочитано: {bytesRead} байт.");

                    List<byte[]> byteArrays = _replyTwimeMessage.GetMessagesArrays(bufferMsg);

                    for (int i = 0; i < byteArrays.Count; i++)
                    {
                        messageId = BitConverter.ToUInt16(byteArrays[i], 2);

                        ParseTwimeMessage(byteArrays[i], messageId);
                    }
                }
                else
                {
                    ParseTwimeMessage(bufferMsg, messageId);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
                Thread.Sleep(3000);
            }

        }

        //разбор одного сообщения 
        private void ParseTwimeMessage(byte[] bufferMsg, ushort messageId)
        {
            ulong ClOrdId;
            uint Count;

            switch (messageId)
            {
                case 5001: // EstablishmentAck

                    _startKeepaliveIntervalForTwime = DateTime.Now;

                    _nextTwimeMsgNum = BitConverter.ToUInt64(bufferMsg, 20); // запоминаем номер следующего сообщения прикладного уровня

                    SendLogMessage("TWIME connection open ", LogMessageType.System);
                    _twimeConnectionSuccessful = true;

                    break;

                case 5006:  // Sequence

                    ulong nextTwimeSeqNo = BitConverter.ToUInt64(bufferMsg, 8);
                    uint missedMsgCount = 0;

                    WriteLogTradingServerEvent($"Получен Sequence. Сдед.номер:{nextTwimeSeqNo}");

                    if (nextTwimeSeqNo > _nextTwimeMsgNum)
                    {
                        missedMsgCount = (uint)(nextTwimeSeqNo - _nextTwimeMsgNum);

                        SendLogMessage($"{missedMsgCount} messages were missed ", LogMessageType.System);

                        // запросить RetransmitRequest (message id=5004)
                        byte[] retransMsg = _twimeMessageConstructor.RetransmitRequest(_nextTwimeMsgNum, missedMsgCount, out string msgLog);

                        SendLogMessage(msgLog, LogMessageType.System);

                        _TwimeTradeStream.Write(retransMsg, 0, retransMsg.Length);
                    }

                    break;

                case 5003:  // Terminate

                    byte reason = bufferMsg[8];

                    if (reason == 0 || reason == 10)
                    {
                        SendLogMessage("Terminate received. The session ended correctly", LogMessageType.System);
                    }
                    else
                    {
                        SendLogMessage($"Error terminate received. Reason: {reason}", LogMessageType.Error);
                    }

                    _twimeConnectionSuccessful = false;
                    _TwimeTcpClient = null;
                    _TwimeTradeStream = null;

                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                    }

                    break;

                case 5002:  // EstablishmentReject

                    byte EstablishmentRejectCode = bufferMsg[16];
                    SendLogMessage($"Establishment Reject received. Reject reason: {EstablishmentRejectCode}", LogMessageType.Error);
                    break;

                case 5007:  // FloodReject

                    ClOrdId = BitConverter.ToUInt64(bufferMsg, 8);
                    uint QueueSize = BitConverter.ToUInt32(bufferMsg, 16);
                    uint PenaltyRemain = BitConverter.ToUInt32(bufferMsg, 20);

                    SendLogMessage($"Flood Reject received. ClOrdId {ClOrdId} was rejected, you sent {QueueSize} requests at the last second. The message reception will resume after {PenaltyRemain / 1000000} seconds", LogMessageType.Error);
                    break;

                case 5008: // SessionReject

                    ClOrdId = BitConverter.ToUInt64(bufferMsg, 8);
                    uint RefTagID = BitConverter.ToUInt32(bufferMsg, 16);
                    byte SessionRejectReason = bufferMsg[20];

                    SendLogMessage($"Session Reject received. ClOrdId {ClOrdId} was rejected, Tag {RefTagID} is incorrect. Session reject reason: {SessionRejectReason}", LogMessageType.Error);
                    break;

                case 5009: // BusinessMessageReject

                    ClOrdId = BitConverter.ToUInt64(bufferMsg, 8);
                    int OrdRejReason = BitConverter.ToInt32(bufferMsg, 24);

                    SendLogMessage($"Business Message Reject received. Order {ClOrdId} was rejected, Reject reason: {OrdRejReason}", LogMessageType.Error);

                    //отклонение заявки на размещение нового ордера
                    int index = _newOrdersTwime.FindIndex(p => p.NumberUser == (int)ClOrdId);
                    if (index != -1)
                    {
                        Order orderFail = _newOrdersTwime[index];
                        orderFail.State = OrderStateType.Fail;
                        orderFail.TimeCallBack = DateTime.UtcNow.AddHours(3);
                        orderFail.SecurityNameCode = _newOrdersTwime[index].SecurityNameCode;
                        orderFail.Side = _newOrdersTwime[index].Side;
                        MyOrderEvent(orderFail);

                        SendLogMessage($"Ордер {index} отклонен", LogMessageType.Error);

                        _newOrdersTwime.RemoveAt(index);
                    }

                    break;

                case 5004: // RetransmitRequest

                    ulong FromSeqNo = BitConverter.ToUInt64(bufferMsg, 16);
                    Count = BitConverter.ToUInt32(bufferMsg, 24);

                    SendLogMessage($"Retransmit Request received. Requests {Count} messages, starting from {FromSeqNo}", LogMessageType.System);
                    break;

                case 5005: // Retransmission

                    ulong NextSeqNo = BitConverter.ToUInt64(bufferMsg, 8);
                    Count = BitConverter.ToUInt32(bufferMsg, 24);

                    SendLogMessage($"Retransmission received. {Count} messages will be sent in response to the Retransmit Request. NextSeqNo: {NextSeqNo}", LogMessageType.System);
                    // после этого придут запрашиваемые сообщения
                    _nextTwimeMsgNum = NextSeqNo; // номер первого сообщения присваиваем ожидаемому

                    break;

                case 7010:  // EmptyBook

                    _nextTwimeMsgNum++;

                    SendLogMessage($"EmtyBook message received.", LogMessageType.System);
                    break;

                case 7014:  // SystemEvent

                    _nextTwimeMsgNum++;

                    byte TradSesEvent = bufferMsg[28];
                    SendLogMessage($"System Event message received. Event type: {TradSesEvent}", LogMessageType.System);

                    break;

                case 7015:  //  NewOrderSingleResponse

                    _nextTwimeMsgNum++;

                    SendLogMessage($"NewOrderSingleResponse message received.", LogMessageType.System);

                    Order order = new Order();

                    TwimeOrderReport ordReport = new TwimeOrderReport(messageId, bufferMsg);
                    order = ordReport.GetOrderReport();

                    if (order != null)
                    {
                        order.SecurityNameCode = _secNameById[order.SecurityNameCode];
                        order.PortfolioNumber = _TradeAccount;

                        if (_useOptions)
                        {
                            Security option = _subscribedOptions.Find(s => s.Name == order.SecurityNameCode);
                            order.SecurityClassCode = option != null ? option.NameClass : "Futures";
                        }

                        WriteLogTradingServerEvent(PrintOrderReport(order));

                        _ordersForClearing[order.NumberUser] = order;

                        MyOrderEvent(order);
                    }
                    else
                    {
                        SendLogMessage("The receipt of the order report failed with an error", LogMessageType.Error);
                    }

                    break;

                case 7017:  //  OrderCancelResponse

                    _nextTwimeMsgNum++;

                    SendLogMessage($"OrderCancelResponse message received.", LogMessageType.System);

                    Order canceledOrder = new Order();
                    TwimeOrderReport ordCancReport = new TwimeOrderReport(messageId, bufferMsg);
                    canceledOrder = ordCancReport.GetOrderReport();

                    if (canceledOrder != null)
                    {
                        index = _newOrdersTwime.FindIndex(p => p.NumberUser == canceledOrder.NumberUser);

                        if (index != -1)
                        {
                            canceledOrder.SecurityNameCode = _newOrdersTwime[index].SecurityNameCode;
                            canceledOrder.Side = _newOrdersTwime[index].Side;
                            canceledOrder.Price = _newOrdersTwime[index].Price;
                            _newOrdersTwime.RemoveAt(index);
                        }
                        else
                            SendLogMessage($"The index for the canseled order was not found", LogMessageType.Error);

                        canceledOrder.PortfolioNumber = _TradeAccount;

                        if (_useOptions)
                        {
                            Security option = _subscribedOptions.Find(s => s.Name == canceledOrder.SecurityNameCode);
                            canceledOrder.SecurityClassCode = option != null ? option.NameClass : "Futures";
                        }

                        WriteLogTradingServerEvent(PrintOrderReport(canceledOrder));

                        if (_ordersForClearing.ContainsKey(canceledOrder.NumberUser))
                        {
                            _ordersForClearing.Remove(canceledOrder.NumberUser);
                        }

                        MyOrderEvent(canceledOrder);
                    }
                    else
                    {
                        SendLogMessage("The receipt of the order report failed with an error", LogMessageType.Error);
                    }
                    break;

                case 7018:  //  OrderReplaceResponse

                    _nextTwimeMsgNum++;

                    SendLogMessage($"OrderReplaceResponse message received.", LogMessageType.System);

                    Order changedOrder = new Order();
                    TwimeOrderReport ordReplReport = new TwimeOrderReport(messageId, bufferMsg);
                    changedOrder = ordReplReport.GetOrderReport();

                    if (changedOrder != null)
                    {
                        index = _newOrdersTwime.FindIndex(p => p.NumberUser == changedOrder.NumberUser);

                        if (index != -1)
                        {
                            changedOrder.SecurityNameCode = _newOrdersTwime[index].SecurityNameCode;
                            changedOrder.Side = _newOrdersTwime[index].Side;
                            _newOrdersTwime.RemoveAt(index);
                        }
                        else
                            SendLogMessage($"The index for the changed order was not found", LogMessageType.Error);

                        changedOrder.PortfolioNumber = _TradeAccount;

                        if (_useOptions)
                        {
                            Security option = _subscribedOptions.Find(s => s.Name == changedOrder.SecurityNameCode);
                            changedOrder.SecurityClassCode = option != null ? option.NameClass : "Futures";
                        }

                        WriteLogTradingServerEvent(PrintOrderReport(changedOrder));

                        MyOrderEvent(changedOrder);
                    }
                    else
                    {
                        SendLogMessage("The receipt of the order report failed with an error", LogMessageType.Error);
                    }

                    break;

                case 7007:  //  OrderMassCancelResponse

                    _nextTwimeMsgNum++;

                    SendLogMessage("OrderMassCancelResponse received", LogMessageType.System);

                    break;

                case 7019:  //  ExecutionSingleReport

                    _nextTwimeMsgNum++;

                    SendLogMessage($"ExecutionSingleReport message received.", LogMessageType.System);

                    Order executionOrder = new Order();

                    TwimeOrderReport ordExecReport = new TwimeOrderReport(messageId, bufferMsg);

                    executionOrder = ordExecReport.GetOrderReport(out MyTrade trade);

                    if (executionOrder != null)
                    {

                        executionOrder.SecurityNameCode = _secNameById[executionOrder.SecurityNameCode];
                        executionOrder.PortfolioNumber = _TradeAccount;
                        trade.SecurityNameCode = executionOrder.SecurityNameCode;

                        if (_useOptions)
                        {
                            Security option = _subscribedOptions.Find(s => s.Name == executionOrder.SecurityNameCode);
                            executionOrder.SecurityClassCode = option != null ? option.NameClass : "Futures";
                        }

                        _newOrdersTwime.RemoveAll(o => o.NumberUser == executionOrder.NumberUser);

                        WriteLogTradingServerEvent(PrintOrderReport(executionOrder));

                        StringBuilder sbTrd = new StringBuilder();
                        sbTrd.AppendLine("New trade:");
                        sbTrd.AppendLine($"trade.NumberTrade: {trade.NumberTrade}");
                        sbTrd.AppendLine($"trade.Security: {trade.SecurityNameCode}");
                        sbTrd.AppendLine($"trade.Time: {trade.Time}");
                        sbTrd.AppendLine($"trade.Price: {trade.Price}");
                        sbTrd.AppendLine($"trade.Volume: {trade.Volume}");
                        sbTrd.AppendLine($"trade.Side: {trade.Side}");
                        sbTrd.AppendLine($"trade.NumberOrderParent: {trade.NumberOrderParent}");
                        sbTrd.AppendLine("--------------------");

                        string trdRep = sbTrd.ToString();

                        WriteLogTradingServerEvent($"Ордер содержит трейд: {trdRep}");

                        MyTradeEvent(trade);

                        if (_ordersForClearing.ContainsKey(executionOrder.NumberUser))
                        {
                            _ordersForClearing.Remove(executionOrder.NumberUser);
                        }

                        MyOrderEvent(executionOrder);
                    }
                    else
                    {
                        SendLogMessage("The receipt of the execution order report failed with an error", LogMessageType.Error);
                    }
                    break;

                default:
                    SendLogMessage("Message ID not found!", LogMessageType.Error);
                    break;
            }
        }



        // Обработка FIX сообщений
        private void FixMessageReader()
        {
            List<Dictionary<string, string>> fixMessages = new List<Dictionary<string, string>>();

            try
            {
                byte[] dataAns = new byte[4096];

                int bytesRead = _fixTradeStream.Read(dataAns, 0, dataAns.Length);

                string fixMessageFromMOEX = Encoding.UTF8.GetString(dataAns).Substring(0, bytesRead);

                WriteLogTradingServerEvent("Received: " + fixMessageFromMOEX);

                string[] tagsValues = fixMessageFromMOEX.Split(new char[] { '\u0001' });

                for (int i = 0; i < tagsValues.Length; i++)
                {
                    if (tagsValues[i].Contains("8=FIX.4.4"))
                    {
                        fixMessages.Add(new Dictionary<string, string>());

                        AddMsgToDict(fixMessages[fixMessages.Count - 1], tagsValues, i);
                    }
                }

                for (int j = 0; j < fixMessages.Count; j++)
                {
                    ParseFixMessage(fixMessages[j]);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                Thread.Sleep(3000);
            }
        }

        private void ParseFixMessage(Dictionary<string, string> fixMsgValues)
        {
            long newFixMsgNum = long.Parse(fixMsgValues["34"]);

            _timeOfTheLastFIXMessage = DateTime.Now;

            // проверка очередности входящих сообщений
            if (newFixMsgNum == _incomingFixMsgNum)
            {
                _incomingFixMsgNum++;
            }
            else if (newFixMsgNum > _incomingFixMsgNum)
            {
                string resendMsg = _fixTradeMessages.ResendMessage(_msgSeqNum, _incomingFixMsgNum, newFixMsgNum - 1);
                SendFIXTradeMessage(resendMsg);
                SendLogMessage($" Входящий номер больше чем ожидалось. Resend Request has been sent", LogMessageType.System);

                _incomingFixMsgNum = newFixMsgNum + 1;
            }

            string typeMsg = fixMsgValues["35"];

            switch (typeMsg)
            {
                case "A": // Logon

                    _startOfTheHeartbeatTimer = DateTime.Now;

                    if (newFixMsgNum > _incomingFixMsgNum)
                    {
                        string resendMsg = _fixTradeMessages.ResendMessage(_msgSeqNum, _incomingFixMsgNum, newFixMsgNum - 1);
                        SendFIXTradeMessage(resendMsg);
                        SendLogMessage($" Входящий номер больше чем ожидалось. Resend Request has been sent", LogMessageType.System);
                    }
                    else
                    {
                        _incomingFixMsgNum++;
                    }

                    SendLogMessage("FIX connection open", LogMessageType.System);

                    _fixTradeConnectionSuccessful = true;

                    break;

                case "0": // Hearbeat

                    WriteLogTradingServerEvent($"Получен Hearbeat: вх. {newFixMsgNum}, исх.{_msgSeqNum}");

                    _isThereFirstHearbeat = true;

                    break;

                case "1": // Test Request

                    string testReqId = string.Empty;

                    if (fixMsgValues.TryGetValue("112", out testReqId))
                    {
                        string hrbtMsgForTest = _fixTradeMessages.HeartbeatMessage(_msgSeqNum, true, testReqId);
                        SendFIXTradeMessage(hrbtMsgForTest);
                    }

                    break;

                case "5": // Logout

                    if (!_logoutInitiator) //если инициатор logout был сервер
                    {
                        string logout = _fixTradeMessages.LogoutMessage(_msgSeqNum);
                        SendFIXTradeMessage(logout);
                    }
                    SendLogMessage("Logout received", LogMessageType.System);
                    _fixTradeConnectionSuccessful = false;
                    _isThereFirstHearbeat = false;
                    _fixTradeTcpClient = null;
                    _fixTradeStream = null;

                    break;

                case "3":   //RejectMessage
                            // Указывает на неверно переданное или недопустимое сообщение сессионного уровня, пришедшее от противоположной стороны.
                            // RejectMessage rejectMessage = new RejectMessage();

                    string reason;

                    if (fixMsgValues.TryGetValue("371", out reason))
                    {
                        string RefSeqNum = fixMsgValues.ContainsKey("45") ? fixMsgValues["45"] : "not found";
                        string RefMsgType = fixMsgValues.ContainsKey("372") ? fixMsgValues["372"] : "not found";
                        SendLogMessage($"The message Reject has been received. RefTagID: {fixMsgValues["371"]}, RefMsgType: {RefMsgType}, Reson: {reason}. {fixMsgValues["58"]}", LogMessageType.Error);

                        if (RefMsgType.Equals("D"))
                        {
                            if (_newOrdersFix.ContainsKey(RefSeqNum))
                            {
                                Order orderFail = _newOrdersFix[RefSeqNum];
                                orderFail.State = OrderStateType.Fail;
                                orderFail.TimeCallBack = DateTime.UtcNow.AddHours(3);

                                MyOrderEvent(orderFail);

                                _newOrdersFix.Remove(RefSeqNum);
                            }
                            else
                                SendLogMessage($"The order's MsgSeqNum {RefSeqNum} was not found", LogMessageType.Error);
                        }
                    }
                    else
                    {
                        SendLogMessage("Unknown reason for rejecting the message, see the trading log", LogMessageType.System);
                    }
                    break;

                case "h":
                    SendLogMessage($"Trading Session Status: {fixMsgValues["340"]}. Text: {fixMsgValues["58"]}", LogMessageType.System);
                    break;

                case "8": //ExequtionReport
                          // Используется для получения информации по заявке

                    UpdateMyOrder(fixMsgValues);
                    break;

                case "9": // Order Cancel Reject (9)
                          //  формируется в случае получения некорректного запроса на снятие или изменение заявки

                    string OrigClOrdID = fixMsgValues["41"] ?? "N/A";

                    string errorReason = "no text";
                    fixMsgValues.TryGetValue("58", out errorReason);
                    fixMsgValues.TryGetValue("102", out errorReason);

                    SendLogMessage($"FIX sent order cancel reject: {errorReason}. Order user id: {OrigClOrdID}", LogMessageType.Error);
                    break;

                case "r":   //OrderMassCancelReport
                            // используется для подтверждения получения или отклонение некорректного массового запроса на снятие ранее размещенных заявок
                    string MassCancelRequestType = fixMsgValues["530"] == "1" ? "Cancel by security id" : "Cancel all orders";

                    string MassCancelResponse = fixMsgValues["531"];

                    string MassCancelRejectReason = MassCancelResponse == "0" ? fixMsgValues["532"] : "";

                    string textReport = fixMsgValues.ContainsKey("58") ? fixMsgValues["58"] : " no text";

                    if (MassCancelResponse == "0")
                    {
                        SendLogMessage($"FIX rejected order mass cancel with report: Text={textReport}. MassCancelRequestType: {MassCancelRequestType}, MassCancelResponse={MassCancelResponse}, {MassCancelRejectReason} ", LogMessageType.Error);
                    }
                    else if (MassCancelResponse == "8" || MassCancelResponse == "9")
                    {
                        SendLogMessage(MassCancelRequestType, LogMessageType.Error);
                    }
                    break;

                case "4":  //SequenceReset

                    string GapFillFlag = fixMsgValues["123"];

                    _incomingFixMsgNum = long.Parse(fixMsgValues["36"]) + 1;

                    SendLogMessage($"The message SequenceReset has been received. GapFillFlag={GapFillFlag}, NewSeqNo={fixMsgValues["36"]}", LogMessageType.System);
                    break;

                case "2":

                    SendLogMessage("The message ResendRequest has been received", LogMessageType.NoName);
                    break;

                default:
                    SendLogMessage($" Message type: {typeMsg}. Template selection error", LogMessageType.Error);
                    break;
            }

            fixMsgValues.Clear();
        }

        private void AddMsgToDict(Dictionary<string, string> fixMsgValues, string[] tagsValues, int index)
        {
            for (int i = index; i < tagsValues.Length; i++)
            {
                int equalIndex = tagsValues[i].IndexOf("=");

                if (equalIndex != -1)
                {
                    string tag = tagsValues[i].Substring(0, equalIndex);
                    string value = tagsValues[i].Substring(equalIndex + 1);

                    if (!fixMsgValues.ContainsKey(tag))
                    {
                        fixMsgValues.Add(tag, value);
                    }
                }
            }
        }

        private void UpdateMyOrder(Dictionary<string, string> fixValues)
        {
            string ExecType = fixValues["150"];

            if (ExecType == "6" || ExecType == "E")// рассматривается снятие заявки, рассматривается изменение, эти отчеты не берем
                return;

            string OrdStatus = fixValues["39"];

            if (ExecType == "I" & OrdStatus == "8")
            {
                string ClOrdId = fixValues["11"];
                SendLogMessage($"The request for the status of the order has been rejected. Reason: {fixValues["103"]}", LogMessageType.Error);
                _newOrdersFix.Remove(ClOrdId);
                return;
            }
            else if (ExecType == "I")
            {
                SendLogMessage("Пришел ответ на запрос статуса ордера", LogMessageType.System);
            }

            string OrdType = fixValues.ContainsKey("40") ? fixValues["40"] : "2";
            string Price = fixValues.ContainsKey("44") ? fixValues["44"] : "0";
            string OrderQty = fixValues["38"] ?? "0";

            string LastQty = fixValues.ContainsKey("32") ? fixValues["32"] : "0";
            string LastPx = fixValues.ContainsKey("31") ? fixValues["31"] : "0";

            DateTime TransactionTime = DateTime.UtcNow.AddHours(3);

            if (fixValues.TryGetValue("60", out string TransactTime))  // 20240820-18:53:09.994956107
            {
                string datetimePart = TransactTime.Substring(0, 17);
                string nanosecondsPart = TransactTime.Substring(18);

                TransactionTime = DateTime.ParseExact(datetimePart, "yyyyMMdd-HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
                long nanoseconds = long.Parse(nanosecondsPart);
                TransactionTime = TransactionTime.AddTicks(nanoseconds / 100).AddHours(3); // Переводим наносекунды в тики
            }

            string Text = fixValues.ContainsKey("58") ? fixValues["58"] : "";
            string Symbol = fixValues["55"];

            Order order = new Order();

            order.SecurityNameCode = Symbol;
            order.SecurityClassCode = fixValues["198"].StartsWith("F") ? "Futures" : "Options";
            order.PortfolioNumber = fixValues.ContainsKey("1") ? fixValues["1"] : _TradeAccount;
            order.NumberMarket = fixValues["37"];
            order.Comment = Text;

            if (ExecType == "F") // сделка
            {
                order.Volume = LastQty.ToDecimal();
                order.TimeDone = TransactionTime;
            }
            else
            {
                order.Volume = OrderQty.ToDecimal();
            }

            if (ExecType == "5") // изменено
            {
                string origClordId = fixValues["41"];// пользовательский номер ордера, который был изменен
                string newClOrdId = fixValues["37"];//биржевой номер
                string ClOrdId = fixValues["11"]; // новый пользовательский номер

                // ордер уже менялся и надо обновить соответствие ордеру в ОСЕ  
                if (_modifiedOrders.Exists(o => o.NewNumUser == fixValues["41"]))
                {
                    IDsModifiedFixOrders modifiedOrder = _modifiedOrders.Find(o => o.NewNumUser == fixValues["41"]);
                    modifiedOrder.NumMarket = newClOrdId;
                    modifiedOrder.NewNumUser = fixValues["11"];

                    order.NumberUser = Convert.ToInt32(modifiedOrder.NumUserInSystem);
                }
                else  // в списке измененных ордеров нет информации о новом измененном ордере
                {
                    _modifiedOrders.Add(new IDsModifiedFixOrders() { NumUserInSystem = origClordId, NumMarket = newClOrdId, NewNumUser = ClOrdId });

                    order.NumberUser = Convert.ToInt32(origClordId);
                }
            }

            if (int.TryParse(fixValues["11"], out order.NumberUser))
            {

            }
            else
            {
                // Преобразование не удалось, значит пришел длинный пользовательский номер, установленный биржей измененному или ордеру
                IDsModifiedFixOrders modifiedOrder = _modifiedOrders.Find(o => o.NumMarket == order.NumberMarket);

                if (modifiedOrder != null)
                {
                    order.NumberUser = Convert.ToInt32(modifiedOrder.NumUserInSystem);
                }
                else
                {
                    order.NumberUser = Convert.ToInt32(fixValues["41"]); // изменений цены не было, была отмена
                }
            }

            order.Side = fixValues["54"] == "1" ? Side.Buy : Side.Sell;

            if (OrdType == "2")
            {
                order.Price = Price.ToDecimal();
                order.TypeOrder = OrderPriceType.Limit;
            }
            else
            {
                order.TypeOrder = OrderPriceType.Market;
            }

            order.TimeCallBack = TransactionTime;

            if (OrdStatus == "0" || OrdStatus == "9")
            {
                order.State = OrderStateType.Active;
                order.TimeCreate = TransactionTime;
            }
            else if (OrdStatus == "1")
            {
                order.State = OrderStateType.Partial;
                order.Price = LastPx.ToDecimal();
            }
            else if (OrdStatus == "2")
            {
                order.State = OrderStateType.Done;
                order.Price = LastPx.ToDecimal();
            }
            else if (OrdStatus == "4") // отменена/снята
            {
                decimal volFilled = fixValues["14"].ToDecimal();

                if (volFilled > 0)
                {
                    order.State = OrderStateType.Done;
                }
                else
                {
                    order.State = OrderStateType.Cancel;
                    order.TimeCancel = TransactionTime;
                }
            }
            else if (OrdStatus == "8") //Отклонена
            {
                order.State = OrderStateType.Fail;

                SendLogMessage($"FIX sent order status 'fail' with comment: {order.Comment}", LogMessageType.Error);
            }

            if (ExecType == "I")
            {
                string key = order.NumberUser.ToString();
                order.Price = _newOrdersFix[key].Price;
                _newOrdersFix.Remove(key);
            }

            if (ExecType == "F")  // MyTrades
            {
                string tradeId = fixValues["527"];

                MyTrade trade = new MyTrade();

                trade.SecurityNameCode = Symbol;
                trade.Price = LastPx.ToDecimal();
                trade.Volume = LastQty.ToDecimal();
                trade.NumberOrderParent = order.NumberMarket;
                trade.NumberTrade = tradeId;
                trade.Time = TransactionTime;
                trade.Side = order.Side;

                MyTradeEvent(trade);
            }

            WriteLogTradingServerEvent(PrintOrderReport(order));

            if (order.State == OrderStateType.Active & ExecType != "5")
            {
                _ordersForClearing[order.NumberUser] = order; // все активные заявки храним, чтобы отменить по завершении сессии
            }
            else if (order.State == OrderStateType.Done || order.State == OrderStateType.Partial || order.State == OrderStateType.Cancel)
            {
                if (_ordersForClearing.ContainsKey(order.NumberUser))
                {
                    _ordersForClearing.Remove(order.NumberUser);
                }
            }

            MyOrderEvent(order);
        }

        /// Восстановление данных по TCP
        private void RecoveryData()
        {

            byte[] buffer = new byte[4096];
            byte[] sizeBuffer = new byte[4];

            string messageFromMOEX = string.Empty;

            Thread.Sleep(1000);

            while (true)
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    Thread.Sleep(1);
                    continue;
                }

                try
                {

                    lock (_socketLockerHistoricalReplay)
                    {
                        bool ordersRecoveryNeed = _historicalReplayEndPointForOrders != null && _missingOrdersBeginSeqNo > 0
                            && _missingOrdersRptSeqNums.Count >= 2 && _tryingConnectRecoveryOrdersCount != 10;

                        bool tradesRecoveryNeed = _historicalReplayEndPointForTrades != null && _missingTradesBeginSeqNo > 0
                            && _missingTradesRptSeqNums.Count >= 2 && _tryingConnectRecoveryTradesCount != 10;

                        // проверяем нужно ли восстанавливать какие-либо данные
                        if (ordersRecoveryNeed)
                        {
                            if (_ordRecoveryReqCount == 1000)
                            {
                                _limitOrderReq = true;
                                SendLogMessage("You have reached the request limit for the ORDERS-LOG stream", LogMessageType.Error);
                                continue;
                            }
                            // восстанавливаем данные из потока ордеров
                            WriteLogRecovery($"Попытка восстановить пропущенные сообщения по ордерам: {_missingOrdersBeginSeqNo}-{_missingOrdersEndSeqNo}");

                        }
                        else
                        if (tradesRecoveryNeed)
                        {
                            if (_trdRecoveryReqCount == 15000)
                            {
                                _limitTradeReq = true;
                                SendLogMessage("You have reached the request limit for the FO-TRADES stream", LogMessageType.Error);
                                continue;
                            }
                            // восстанавливаем данные из потока сделок
                            WriteLogRecovery($"Попытка восстановить пропущенные сообщения по трейдам: {_missingTradesBeginSeqNo}-{_missingTradesEndSeqNo}");
                        }
                        else
                        {
                            Thread.Sleep(500);
                            continue;
                        }

                        int msgCountRecieved = 0;

                        _historicalReplaySocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                        int msgSeqNum = 1;

                        try
                        {
                            if (ordersRecoveryNeed)
                            {
                                // было по 5 попыток подключения к серверам восстановления сообщений ORDERS-LOG
                                if (_tryingConnectRecoveryOrdersCount == 10)
                                {
                                    Thread.Sleep(100);
                                    continue;
                                }

                                if (_isTryingConnectToOrdersReserveIP)
                                {
                                    _historicalReplaySocket.Connect(_historicalReplayEndPointForOrdersReserve);
                                }
                                else
                                {
                                    _historicalReplaySocket.Connect(_historicalReplayEndPointForOrders);
                                }
                            }
                            else if (tradesRecoveryNeed)
                            {
                                // было по 5 попыток подключения к серверам восстановления сообщений FO-TRADES
                                if (_tryingConnectRecoveryTradesCount == 10)
                                {
                                    Thread.Sleep(100);
                                    continue;
                                }

                                if (_isTryingConnectToTradesReserveIP)
                                {
                                    _historicalReplaySocket.Connect(_historicalReplayEndPointForTradesReserve);
                                }
                                else
                                {
                                    _historicalReplaySocket.Connect(_historicalReplayEndPointForTrades);
                                }
                            }
                        }
                        catch (SocketException ex)
                        {
                            _historicalReplaySocket?.Dispose();
                            _historicalReplaySocket = null;

                            if (ordersRecoveryNeed)
                            {
                                SendLogMessage($"Попытка {_tryingConnectRecoveryOrdersCount} подключиться к серверу восстановления ордеров:\n" + ex.Message, LogMessageType.Error);

                                _tryingConnectRecoveryOrdersCount++;

                                if (_tryingConnectRecoveryOrdersCount == 5 && !_isTryingConnectToOrdersReserveIP)
                                {
                                    SendLogMessage($"Не удалось подключиться к серверу восстановления ордеров. " +
                                        $"Подключаюсь к резервному: {_historicalReplayEndPointForOrdersReserve}.\n" + ex.Message, LogMessageType.Error);


                                    _isTryingConnectToOrdersReserveIP = true;
                                }
                                else if (_tryingConnectRecoveryOrdersCount == 10 && _isTryingConnectToOrdersReserveIP)
                                {
                                    SendLogMessage($"Не удалось подключиться к резервному серверу восстановления ордеров. " +
                                        $"Проверьте подключение.\n" + ex.Message, LogMessageType.Error);
                                }

                                Thread.Sleep(3000);
                                continue;
                            }
                            else if (tradesRecoveryNeed)
                            {
                                SendLogMessage($"Попытка {_tryingConnectRecoveryTradesCount} подключиться к серверу восстановления трейдов:\n" + ex.Message, LogMessageType.Error);

                                _tryingConnectRecoveryTradesCount++;

                                if (_tryingConnectRecoveryTradesCount == 5 && !_isTryingConnectToOrdersReserveIP)
                                {
                                    SendLogMessage($"Не удалось подключиться к серверу восстановления трейдов. " +
                                        $"Подключаюсь к резервному: {_historicalReplayEndPointForTradesReserve}.\n" + ex.Message, LogMessageType.Error);


                                    _isTryingConnectToTradesReserveIP = true;
                                }
                                else if (_tryingConnectRecoveryTradesCount == 10 && _isTryingConnectToTradesReserveIP)
                                {
                                    SendLogMessage($"Не удалось подключиться к серверу восстановления трейдов. " +
                                        $"Проверьте подключение.\n" + ex.Message, LogMessageType.Error);
                                }

                                Thread.Sleep(3000);
                                continue;
                            }
                        }

                        FixMessageConstructor recoveryMessages = new FixMessageConstructor("OsEngine", "MOEX");
                        string logonMsg = recoveryMessages.LogonFASTMessage("user0", "pass0", msgSeqNum);
                        _historicalReplaySocket.Send(Encoding.UTF8.GetBytes(logonMsg));

                        msgSeqNum++;

                        sizeBuffer = new byte[4];
                        int length = 0;

                        try
                        {
                            length = _historicalReplaySocket.Receive(sizeBuffer, 4, SocketFlags.None);
                        }
                        catch (Exception ex)
                        {
                            SendLogMessage("Error receiving Data " + ex.ToString(), LogMessageType.Error);

                            if (ServerStatus != ServerConnectStatus.Disconnect)
                            {
                                ServerStatus = ServerConnectStatus.Disconnect;
                                DisconnectEvent();
                            }
                        }

                        while (length < 4)
                        {
                            try
                            {
                                length += _historicalReplaySocket.Receive(sizeBuffer, length, 4 - length, SocketFlags.None);
                            }
                            catch (Exception ex)
                            {
                                SendLogMessage("Error receiving Data Message " + ex.ToString(), LogMessageType.Error);
                                if (ServerStatus != ServerConnectStatus.Disconnect)
                                {
                                    ServerStatus = ServerConnectStatus.Disconnect;
                                    DisconnectEvent();
                                }
                            }
                        }

                        int msgSize = BitConverter.ToInt32(sizeBuffer, 0);

                        int totalBytesReceived = 0;
                        byte[] messageBuffer = new byte[msgSize];
                        while (totalBytesReceived < msgSize)
                        {
                            int bytesRead = 0;
                            try
                            {
                                bytesRead = _historicalReplaySocket.Receive(buffer, msgSize - totalBytesReceived, SocketFlags.None);
                            }
                            catch (Exception ex)
                            {
                                SendLogMessage("Error receiving Data Message " + ex.ToString(), LogMessageType.Error);
                                if (ServerStatus != ServerConnectStatus.Disconnect)
                                {
                                    ServerStatus = ServerConnectStatus.Disconnect;
                                    DisconnectEvent();
                                }
                            }

                            Array.Copy(buffer, 0, messageBuffer, totalBytesReceived, bytesRead);
                            totalBytesReceived += bytesRead;
                        }

                        Context context = CreateNewContext();

                        using (MemoryStream stream = new MemoryStream(messageBuffer, 0, msgSize))
                        {
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

                        long begSeqNum = 0;
                        long endSeqNum = 0;

                        if (ordersRecoveryNeed)
                        {
                            begSeqNum = _missingOrdersBeginSeqNo;
                            endSeqNum = _missingOrdersEndSeqNo - _missingOrdersBeginSeqNo >= 1000 ? begSeqNum + 1000 - 1 : _missingOrdersEndSeqNo;
                            WriteLogRecovery($"Подаю запрос на восстановление {endSeqNum - begSeqNum} сообщений ордеров с {begSeqNum} по {endSeqNum}");
                        }
                        else if (tradesRecoveryNeed)
                        {
                            begSeqNum = _missingTradesBeginSeqNo;
                            endSeqNum = _missingTradesEndSeqNo - _missingTradesBeginSeqNo >= 1000 ? begSeqNum + 1000 - 1 : _missingTradesEndSeqNo;
                            WriteLogRecovery($"Подаю запрос на восстановление {endSeqNum - begSeqNum} сообщений трейдов с {begSeqNum} по {endSeqNum}");
                        }

                        string MDReqId = "1";

                        string MDRequestMsg = recoveryMessages.MarketDataRequestMessage(msgSeqNum, MDReqId, begSeqNum, endSeqNum);

                        _historicalReplaySocket.Send(Encoding.UTF8.GetBytes(MDRequestMsg));

                        while (true) // начинаем цикл приема сообщений
                        {
                            sizeBuffer = new byte[4];
                            try
                            {
                                length = _historicalReplaySocket.Receive(sizeBuffer, 4, SocketFlags.None);
                            }
                            catch (Exception ex)
                            {
                                SendLogMessage("Error receiving Data Message " + ex.ToString(), LogMessageType.Error);
                                if (ServerStatus != ServerConnectStatus.Disconnect)
                                {
                                    ServerStatus = ServerConnectStatus.Disconnect;
                                    DisconnectEvent();
                                }
                            }

                            while (length < 4)
                            {
                                try
                                {
                                    length += _historicalReplaySocket.Receive(sizeBuffer, length, 4 - length, SocketFlags.None);
                                }
                                catch (Exception ex)
                                {
                                    SendLogMessage("Error receiving Data Message " + ex.ToString(), LogMessageType.Error);
                                    if (ServerStatus != ServerConnectStatus.Disconnect)
                                    {
                                        ServerStatus = ServerConnectStatus.Disconnect;
                                        DisconnectEvent();
                                    }
                                }
                            }

                            msgSize = BitConverter.ToInt32(sizeBuffer, 0);

                            totalBytesReceived = 0;
                            messageBuffer = new byte[msgSize];

                            while (totalBytesReceived < msgSize)
                            {
                                int bytesRead = 0;
                                try
                                {
                                    bytesRead = _historicalReplaySocket.Receive(buffer, msgSize - totalBytesReceived, SocketFlags.None);
                                }
                                catch (Exception ex)
                                {
                                    SendLogMessage("Error receiving Data Message " + ex.ToString(), LogMessageType.Error);
                                    if (ServerStatus != ServerConnectStatus.Disconnect)
                                    {
                                        ServerStatus = ServerConnectStatus.Disconnect;
                                        DisconnectEvent();
                                    }
                                }

                                Array.Copy(buffer, 0, messageBuffer, totalBytesReceived, bytesRead);
                                totalBytesReceived += bytesRead;
                            }

                            using (MemoryStream stream = new MemoryStream(messageBuffer, 0, msgSize))
                            {
                                OpenFAST.Message msg = null;

                                try
                                {
                                    FastDecoder decoder = new FastDecoder(context, stream);
                                    msg = decoder.ReadMessage();
                                }
                                catch (NullReferenceException)
                                {
                                    // в редких случаях исключение возникает в самой библиотеке OpenFast
                                }
                                catch (Exception)
                                {
                                    // Иногда просто что-то глючит, но он все равно читает сообщение
                                }

                                if (msg == null)
                                {
                                    SendLogMessage("Failed to read message from historical replay server", LogMessageType.Error);

                                    _historicalReplaySocket.Close();
                                    break;
                                }

                                string messageType = msg.GetString("MessageType");

                                if (messageType == "X")
                                {
                                    long MsgSeqNum = msg.GetLong("MsgSeqNum");

                                    if (msg.IsDefined("MDEntries"))
                                    {
                                        SequenceValue secVal = msg.GetValue("MDEntries") as SequenceValue;

                                        for (int i = 0; i < secVal.Length; i++)
                                        {
                                            GroupValue groupVal = secVal[i] as GroupValue;

                                            int RptSeq = groupVal.GetInt("RptSeq");
                                            string nameID = groupVal.GetString("SecurityID");

                                            msgCountRecieved++;

                                            if (ordersRecoveryNeed && _missingOrdersRptSeqNums.Contains(RptSeq))
                                            {
                                                _orderMessages.Enqueue(msg);
                                                _ordersIncremental[MsgSeqNum] = msg;

                                                WriteLogRecovery($"Обработано сообщение - ордер MsgSeqNum: {MsgSeqNum} RptSeq: {RptSeq}");
                                            }

                                            if (tradesRecoveryNeed && _missingTradesRptSeqNums.Contains(RptSeq))
                                            {
                                                _tradeMessages.Enqueue(msg);
                                                _tradesIncremental[MsgSeqNum] = msg;

                                                WriteLogRecovery($"Обработано сообщение - трейд MsgSeqNum: {MsgSeqNum} RptSeq:{RptSeq}");
                                            }
                                        }
                                    }
                                }
                                else if (messageType == "5") // Logout
                                {
                                    WriteLogRecovery($"TCP recovery received MessageType: {messageType}");
                                    WriteLogRecovery($"Historical Replay server Logout. Reason: {msg.GetString("Text")}");

                                    //отвечаем серверу Logout
                                    string MDLogoutMsg = recoveryMessages.LogoutFASTMessage(msgSeqNum);
                                    _historicalReplaySocket.Send(Encoding.UTF8.GetBytes(MDLogoutMsg));

                                    if (ordersRecoveryNeed)
                                    {
                                        _ordRecoveryReqCount++;
                                        _missingOrdersBeginSeqNo = 0;
                                        _missingOrdersEndSeqNo = 0;
                                        // _missingOrdersData = false;
                                        _missingOrdersRptSeqNums.Clear();

                                        WriteLogRecovery($"Прием сообщений orders закончен. Всего получено от сервера: {msgCountRecieved} сообщений.\n Уже сделано запросов: {_ordRecoveryReqCount}");

                                        _endOfRecoveryOrders = true;
                                    }
                                    else if (tradesRecoveryNeed)
                                    {
                                        _trdRecoveryReqCount++;
                                        _missingTradesBeginSeqNo = 0;
                                        _missingTradesEndSeqNo = 0;
                                        //  _missingTradesData = false;
                                        _missingTradesRptSeqNums.Clear();

                                        WriteLogRecovery($"Прием сообщений trades закончен. Всего получено от сервера: {msgCountRecieved} сообщений.\n Уже сделано запросов: {_trdRecoveryReqCount}");

                                        _endOfRecoveryTrades = true;
                                    }

                                    // закрываем сокет
                                    _historicalReplaySocket.Close();
                                    _historicalReplaySocket = null;
                                    _tryingConnectRecoveryOrdersCount = 0;
                                    _isTryingConnectToOrdersReserveIP = false;
                                    _tryingConnectRecoveryTradesCount = 0;
                                    _isTryingConnectToTradesReserveIP = false;

                                    break;
                                }
                                else if (messageType == "0") // heartbeat
                                {
                                    continue;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

        #endregion

        #region 9 Trade

        private RateGate _rateGateForOrders = new RateGate(30, TimeSpan.FromSeconds(1));

        // список измененых ордеров, т.к. на бирже номер ордера, назначенный OsEngine меняется       
        List<IDsModifiedFixOrders> _modifiedOrders = new List<IDsModifiedFixOrders>();
        //для хранения нового ордера, чтобы обработать сообщение об отклонении в TWIME
        private List<Order> _newOrdersTwime = new List<Order>();
        //для хранения нового ордера, чтобы обработать сообщение об отклонении в FIX
        private Dictionary<string, Order> _newOrdersFix = new Dictionary<string, Order>();

        public void SendOrder(Order order)
        {
            _rateGateForOrders.WaitToProceed();

            try
            {
                if (_tradingProtocol.Equals("TWIME"))
                {
                    byte timeInForce = 0;
                    ulong expireDate = 18446744073709551615;

                    _newOrdersTwime.Add(order);

                    ulong ClOrdID = (ulong)order.NumberUser;
                    int securityID = Convert.ToInt32(_secIdByName[order.SecurityNameCode]);
                    int volume = (int)order.Volume;

                    byte[] newOrder = _twimeMessageConstructor.NewOrderSingle
                                      (
                                      ClOrdID,
                                      expireDate,
                                      order.Price,
                                      securityID,
                                      order.NumberUser,
                                      volume,
                                      timeInForce,
                                      (byte)order.Side,
                                      _TradeAccount,
                                      out string msgLog
                                      );

                    WriteLogTradingServerEvent(msgLog);

                    _TwimeTradeStream.Write(newOrder, 0, newOrder.Length);
                }
                else
                {
                    string timeInForce = "0"; // 0 - котировочная на 1 день, 6 - для многодневных заявок
                    string ExpireDate = "";

                    string ordType = "2"; // Limit only
                    string price = order.TypeOrder == OrderPriceType.Market ? null : order.Price.ToString().Replace(",", ".");
                    string MarketSegmentID = order.SecurityClassCode.Equals("Futures") ? "F" : "O";
                    string symbol = order.SecurityNameCode;
                    string account = _TradeAccount.Substring(4); // 3-х символьный код клиента.
                    string CFICode = string.Empty;

                    if (!order.SecurityClassCode.Equals("Futures"))
                    {
                        Security option = _subscribedOptions.Find(o => o.Name == order.SecurityNameCode);

                        if (option != null)
                            CFICode = option.NameClass.Substring(7);
                    }
                    else
                    {
                        CFICode = null;
                    }

                    string newOrder = _fixTradeMessages.NewOrderMessage
                                (
                                    order.NumberUser.ToString(),
                                    ordType,
                                    symbol,
                                    CFICode,
                                    account,
                                    timeInForce,
                                    ((byte)order.Side).ToString(),
                                    false, null, null, // айсберг
                                    order.Volume.ToString().Replace(",", "."),
                                    price,
                                    new string[] { "1", _senderCompID, "C", "3" }, // группа Parties
                                    ExpireDate,
                                    MarketSegmentID,
                                    _msgSeqNum
                                );

                    _newOrdersFix[_msgSeqNum.ToString()] = order;

                    SendFIXTradeMessage(newOrder);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Order send error " + ex.ToString(), LogMessageType.Error);
            }
        }

        public bool CancelOrder(Order order)
        {
            _rateGateForOrders.WaitToProceed();

            try
            {
                if (_tradingProtocol.Equals("TWIME"))
                {
                    _newOrdersTwime.Add(order);

                    ulong ClOrdID = (ulong)DateTime.UtcNow.Ticks;
                    long OrderID = Convert.ToInt64(order.NumberMarket);
                    int securityID = Convert.ToInt32(_secIdByName[order.SecurityNameCode]);

                    byte[] ordDelMsg = _twimeMessageConstructor.OrderCancel(ClOrdID, OrderID, securityID, _TradeAccount, out string msgLog);

                    WriteLogTradingServerEvent(msgLog);

                    _TwimeTradeStream.Write(ordDelMsg, 0, ordDelMsg.Length);
                }
                else
                {
                    string origClOrdID = order.NumberUser.ToString();

                    // проверяем менялся ли этот ордер
                    if (_modifiedOrders.Exists(o => o.NumUserInSystem == order.NumberUser.ToString()))
                    {
                        origClOrdID = _modifiedOrders.Find(o => o.NumUserInSystem == order.NumberUser.ToString()).NewNumUser;
                    }

                    string orderID = order.NumberMarket.ToString();
                    string clOrdID = DateTime.UtcNow.Ticks.ToString();
                    string CFICode = string.Empty;

                    if (!order.SecurityClassCode.Equals("Futures"))
                    {
                        Security option = _subscribedOptions.Find(o => o.Name == order.SecurityNameCode);

                        if (option != null)
                            CFICode = option.NameClass.Substring(7);
                    }
                    else
                    {
                        CFICode = null;
                    }

                    string orderDelMsg = _fixTradeMessages.OrderCanselMessage
                        (
                             new string[] { "1", _senderCompID, "C", "3" }, // группа Parties
                            clOrdID,
                            orderID,
                            origClOrdID,
                            order.SecurityNameCode,
                            CFICode,
                            ((byte)order.Side).ToString(),
                            order.Volume.ToString().Replace(",", "."),
                            _msgSeqNum
                        );

                    SendFIXTradeMessage(orderDelMsg);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Order cancel request error " + exception.ToString(), LogMessageType.Error);
                return false;
            }
            return true;
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
            _rateGateForOrders.WaitToProceed();

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

                if (order.State != OrderStateType.Active)
                {
                    SendLogMessage("Can`t change price of non-active order", LogMessageType.Error);
                    return;
                }

                if (_tradingProtocol.Equals("TWIME"))
                {
                    _newOrdersTwime.Add(order);

                    ulong ClOrdID = (ulong)DateTime.UtcNow.Ticks;
                    long OrderID = Convert.ToInt64(order.NumberMarket);
                    int securityID = Convert.ToInt32(_secIdByName[order.SecurityNameCode]);
                    uint OrderQty = (uint)order.Volume;
                    int ClOrdLinkID = order.NumberUser;

                    byte[] changeOrderMsg = _twimeMessageConstructor.OrderReplace
                                      (
                                      ClOrdID,
                                      OrderID,
                                      newPrice,
                                      OrderQty,
                                      ClOrdLinkID,
                                      securityID,
                                      _TradeAccount
                                      , out string msgLog);

                    WriteLogTradingServerEvent(msgLog);

                    _TwimeTradeStream.Write(changeOrderMsg, 0, changeOrderMsg.Length);
                }
                else
                {
                    string origClOrdID;

                    // проверяем менялся ли этот ордер
                    if (_modifiedOrders.Exists(o => o.NumUserInSystem == order.NumberUser.ToString()))
                    {
                        origClOrdID = _modifiedOrders.Find(o => o.NumUserInSystem == order.NumberUser.ToString()).NewNumUser;
                    }
                    else
                    {
                        origClOrdID = order.NumberUser.ToString();
                    }

                    string clOrdID = DateTime.UtcNow.Ticks.ToString();   // идентификатор заявки на снятие/изменение
                    string orderID = order.NumberMarket;
                    string side = order.Side == Side.Buy ? "1" : "2";
                    string orderQty = order.Volume.ToString();
                    string price = order.TypeOrder == OrderPriceType.Limit ? newPrice.ToString().Replace(',', '.') : "0";
                    string CFICode = string.Empty;

                    if (!order.SecurityClassCode.Equals("Futures"))
                    {
                        Security option = _subscribedOptions.Find(o => o.Name == order.SecurityNameCode);

                        if (option != null)
                            CFICode = option.NameClass.Substring(7);
                    }
                    else
                    {
                        CFICode = null;
                    }

                    string changeOrderMsg = _fixTradeMessages.OrderReplaceMessage
                        (
                          new string[] { "1", _senderCompID, "C", "3" }, // группа Parties
                            clOrdID,
                            orderID,
                            origClOrdID,
                            orderQty,
                            price,
                            order.SecurityNameCode,
                            CFICode,
                            side,
                            _msgSeqNum
                        );

                    SendFIXTradeMessage(changeOrderMsg);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        // переменные для сертификации

        /*string _secType = string.Empty;
        byte F = 1;
        byte O = 2;*/

        public void CancelAllOrders()
        {
            SendLogMessage("Я в методе отмены всех ордеров", LogMessageType.System);

            _rateGateForOrders.WaitToProceed();

            try
            {
                if (_tradingProtocol.Equals("TWIME"))
                {
                    ulong ClOrdID = (ulong)DateTime.UtcNow.Ticks;
                    int ClOrdLinkID = 0;
                    int securityID = 0;
                    byte SecurityType = 1; //Отмена только фьючерсов  1-Futures, 2-Options 
                    byte Side = 89;
                    string SecurityGroup = "%";

                    byte[] orderMassCancel = _twimeMessageConstructor.OrderMassCancel(ClOrdID, ClOrdLinkID, securityID, SecurityType, Side, _TradeAccount, SecurityGroup, out string msgLog);

                    WriteLogTradingServerEvent(msgLog);

                    _TwimeTradeStream.Write(orderMassCancel, 0, orderMassCancel.Length);
                }
                else
                {
                    string clOrdID = DateTime.UtcNow.Ticks.ToString(); // идентификатор заявки на снятие
                    string account = _TradeAccount.Substring(4); // 3-х символьный код клиента.
                    string massCancelReqType = "8"; // "8" или "9" - Отмена всех заявок на конкретном сегменте рынка.
                    string marketSegmentID = "F";

                    string orderMassCancelMsg = _fixTradeMessages.OrderMassCanselMessage
                       (
                         new string[] { "1", _senderCompID, "C", "3" }, // группа Parties
                         clOrdID,
                         massCancelReqType,
                         marketSegmentID,
                          account,
                         null,
                         _msgSeqNum
                       );

                    SendFIXTradeMessage(orderMassCancelMsg);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Cancel all orders request error " + exception.ToString(), LogMessageType.Error);
            }
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            _rateGateForOrders.WaitToProceed();

            try
            {
                if (_tradingProtocol.Equals("TWIME"))
                {
                    ulong ClOrdID = (ulong)DateTime.UtcNow.Ticks;
                    int ClOrdLinkID = 0;
                    int securityID = Convert.ToInt32(security.NameId);
                    byte SecurityType = security.NameClass.Equals("Futures") ? (byte)0 : (byte)1; // 0-Futures, 1-Options 
                    byte Side = 89;
                    string SecurityGroup = "%";

                    byte[] orderMassCancel = _twimeMessageConstructor.OrderMassCancel(ClOrdID, ClOrdLinkID, securityID, SecurityType, Side, _TradeAccount, SecurityGroup, out string msgLog);

                    SendLogMessage(msgLog, LogMessageType.System);

                    _TwimeTradeStream.Write(orderMassCancel, 0, orderMassCancel.Length);

                }
                else
                {
                    string clOrdID = DateTime.UtcNow.Ticks.ToString(); // идентификатор заявки на снятие
                    string tradingSessionID = security.NameClass;
                    string account = _TradeAccount.Substring(4); // 3-х символьный код клиента.
                    string MarketSegmentID = security.NameClass.Equals("Futures") ? "F" : "O";
                    string massCancelReqType = "1"; // "1" - Отмена всех заявок по инструменту.

                    string orderMassCancelMsg = _fixTradeMessages.OrderMassCanselMessage
                        (
                          new string[] { "1", _senderCompID, "C", "3" }, // группа Parties
                          clOrdID,
                          massCancelReqType,
                          MarketSegmentID,
                          account,
                          security.NameId,
                          _msgSeqNum
                        );

                    SendFIXTradeMessage(orderMassCancelMsg);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Cancel all orders request error " + exception.ToString(), LogMessageType.Error);
            }
        }

        public void GetAllActivOrders()
        {

        }

        public OrderStateType GetOrderStatus(Order order)
        {
            _rateGateForOrders.WaitToProceed();

            try
            {
                if (_tradingProtocol.Equals("TWIME"))
                {
                    SendLogMessage("You can get the status of the order only in the FIX protocol", LogMessageType.Error);
                    return OrderStateType.None;
                }

                string ClOrdID = order.NumberUser.ToString();

                _newOrdersFix[ClOrdID] = order; // сохраняем, чтобы извлечь цену ордера для отчета

                // проверяем менялся ли этот ордер
                if (_modifiedOrders.Exists(o => o.NumUserInSystem == order.NumberUser.ToString()))
                {
                    ClOrdID = _modifiedOrders.Find(o => o.NumUserInSystem == order.NumberUser.ToString()).NewNumUser;
                }

                string orderID = order.NumberMarket.ToString();

                string orderStatMsg = _fixTradeMessages.OrderStatusRequest
                    (
                        ClOrdID,
                        orderID,
                        order.SecurityNameCode,
                        ((byte)order.Side).ToString(),
                        _msgSeqNum
                    );

                SendFIXTradeMessage(orderStatMsg);
            }
            catch (Exception exception)
            {
                SendLogMessage("Get order status request error " + exception.ToString(), LogMessageType.Error);
            }

            return OrderStateType.None;
        }

        public List<Order> GetActiveOrders(int startIndex, int count)
        {
            return null;
        }

        public List<Order> GetHistoricalOrders(int startIndex, int count)
        {
            return null;
        }

        #endregion

        #region 10 Helpers

        private void LoadFASTTemplates()
        {
            IMessageTemplateLoader loader = new XmlMessageTemplateLoader();

            using (FileStream stream = File.OpenRead(_configDir + "\\templates.xml"))
            {
                _templates = loader.Load(stream);
            }
        }

        private int GetDecimals(decimal x)
        {
            int precision = 0;
            while (x * (decimal)Math.Pow(10, precision) != Math.Round(x * (decimal)Math.Pow(10, precision)))
                precision++;
            return precision;
        }

        private void SendMsgToQueue(OpenFAST.Message msg, ConcurrentQueue<OpenFAST.Message> fastMessagesQueue)
        {
            SequenceValue secVal = msg.GetValue("MDEntries") as SequenceValue;

            string nameForCheck = string.Empty;

            for (int i = 0; i < secVal.Length; i++)
            {
                GroupValue groupVal = secVal[i] as GroupValue;

                string nameId = groupVal.GetString("SecurityID");


                if (IsSubscribedToThisSecurity(nameId))
                {
                    if (nameId != nameForCheck) // чтобы не отправлять повторно сообщение, содержащее более 1 трейда по одному инструменту
                    {
                        nameForCheck = nameId;

                        fastMessagesQueue.Enqueue(msg);
                    }
                }
            }
        }

        /// <summary>
        /// Прверка пропуска в потоке А, добавление из потока В, в случае отсутствия
        /// </summary>
        /// <param name="dictFastMsg"></param>
        /// <param name="msgSeqNum"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        private bool IsMessageMissed(Dictionary<long, OpenFAST.Message> dictFastMsg, long msgSeqNum, OpenFAST.Message msg)
        {
            if (dictFastMsg.ContainsKey(msgSeqNum))
            {
                if (dictFastMsg[msgSeqNum] == msg)
                {
                    return false;
                }
                else
                {
                    dictFastMsg[msgSeqNum] = msg;
                    return false;
                }
            }
            else
            {
                dictFastMsg.Add(msgSeqNum, msg);
                return true;
            }
        }

        private bool IsDataMissed(List<NumbersMD> nums, out long beginMsgSeqNum, out long endMsgSeqNum,
                                  out int rptSeqMissedCount, out List<int> missedNums)
        {
            // проверяем пропуски данных
            beginMsgSeqNum = 0; // начало пропущенных данных
            endMsgSeqNum = 0; // конец пропущенных данных
            rptSeqMissedCount = 0; // количество пропущенных данных по инструменту
            missedNums = new List<int>(); // какие номера пропущены

            if (nums.Count < 5)
                return false;

            nums.Sort((x, y) => x.RptSeq.CompareTo(y.RptSeq));

            for (int i = 1; i < nums.Count; i++)
            {
                if (nums[i].RptSeq != nums[i - 1].RptSeq + 1)
                {
                    if (beginMsgSeqNum == 0)
                    {
                        beginMsgSeqNum = nums[i - 1].MsgSeqNum + 1;
                    }

                    endMsgSeqNum = nums[i].MsgSeqNum - 1;
                    rptSeqMissedCount += nums[i].RptSeq - (nums[i - 1].RptSeq + 1);

                    int j = 0;

                    while (j < nums[i].RptSeq - (nums[i - 1].RptSeq + 1))
                    {
                        missedNums.Add(nums[i - 1].RptSeq + 1 + j);

                        j++;
                    }
                }
            }

            if (beginMsgSeqNum != 0)
                return true; // данные пропущены
            else
                return false;
        }

        private bool IsSubscribedToThisSecurity(string nameID)
        {
            for (int j = 0; j < _subscribedSecurities.Count; j++)
            {
                if (_subscribedSecurities[j] == nameID)
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsNeedSnapshotSockets(Dictionary<string, Snapshot> dictSnap)
        {
            bool needSocket = false;
            Dictionary<string, Snapshot>.ValueCollection.Enumerator enumerator = dictSnap.Values.GetEnumerator();

            while (enumerator.MoveNext())
            {
                // если в словаре есть хоть один непримененный снэпшот, продолжаем слушать сокет
                if (!enumerator.Current.SnapshotWasApplied)
                    needSocket = true;
            }

            return needSocket;
        }

        private Context CreateNewContext()
        {
            Context context = new Context();
            for (int t = 0; t < _templates.Length; t++)
            {
                MessageTemplate tmplt = _templates[t];
                context.RegisterTemplate(int.Parse(tmplt.Id), tmplt);
            }

            return context;
        }

        #endregion

        #region 11 Log

        private string _logLockTrade = "locker for trades stream ";
        private StreamWriter _logFileTrades = new StreamWriter($"Engine\\Log\\MoexFixFastTwimeConnectorLogs\\FAST_Trades_{DateTime.Now:dd-MM-yyyy}.txt");

        private string _logLockOrder = "locker for orders stream";
        private StreamWriter _logFileOrders = new StreamWriter($"Engine\\Log\\MoexFixFastTwimeConnectorLogs\\FAST_Orders_{DateTime.Now:dd-MM-yyyy}.txt");

        private string _logLockTrading = "locker for incoming trade messages";
        private StreamWriter _logTradingMsg = new StreamWriter($"Engine\\Log\\MoexFixFastTwimeConnectorLogs\\TradingServerLog_{DateTime.Now:dd-MM-yyyy}.txt", false, Encoding.UTF8);

        private string _logLockRecover = "locker for Recover";
        private StreamWriter _logFileRecover = new StreamWriter($"Engine\\Log\\MoexFixFastTwimeConnectorLogs\\DataRecoveryLog_{DateTime.Now:dd-MM-yyyy}.txt");

        private void WriteLogTrades(string message)
        {
            lock (_logLockTrade)
            {
                _logFileTrades.WriteLine($"{DateTime.Now}: {message}");
            }
        }

        private void WriteLogOrders(string message)
        {
            lock (_logLockOrder)
            {
                _logFileOrders.WriteLine($"{DateTime.Now}: {message}");
            }
        }

        private void WriteLogTradingServerEvent(string message)
        {
            lock (_logLockTrading)
            {
                _logTradingMsg.WriteLine($"{DateTime.Now}: {message}");
            }
        }

        private void WriteLogRecovery(string message)
        {
            lock (_logLockRecover)
            {
                _logFileRecover.WriteLine($"{DateTime.Now}: {message}");
            }
        }

        private string PrintOrderReport(Order order)
        {
            StringBuilder rep = new StringBuilder();
            rep.AppendLine("Получен отчет по ордеру: ");
            rep.AppendLine("----------------------------");
            rep.AppendLine($"SecurityNameCode: {order.SecurityNameCode}");
            rep.AppendLine($"OrderState: {order.State}");
            rep.AppendLine($"OrdType: {order.TypeOrder}");
            rep.AppendLine($"Side: {order.Side}");
            rep.AppendLine($"Price: {order.Price}");
            rep.AppendLine($"Volume: {order.Volume}");
            rep.AppendLine($"TransactTime: {order.TimeCallBack}");
            rep.AppendLine($"SecurityClassCode: {order.SecurityClassCode}");
            rep.AppendLine($"PortfolioNumber: {order.PortfolioNumber}");
            rep.AppendLine($"NumberUser: {order.NumberUser}");
            rep.AppendLine($"NumberMarket: {order.NumberMarket}");
            rep.AppendLine($"Comment: {order.Comment}");
            rep.AppendLine("----------------------------");

            return rep.ToString();
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent(message, messageType);
        }

        #endregion
    }
}