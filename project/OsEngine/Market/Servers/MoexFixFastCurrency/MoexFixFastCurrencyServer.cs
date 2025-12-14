using OpenFAST.Codec;
using OpenFAST.Template.Loader;
using OpenFAST.Template;
using OpenFAST;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.MoexFixFastCurrency.Entity;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Xml;


namespace OsEngine.Market.Servers.MoexFixFastCurrency
{
    public class MoexFixFastCurrencyServer : AServer
    {
        public MoexFixFastCurrencyServer()
        {
            MoexFixFastCurrencyServerRealization realization = new MoexFixFastCurrencyServerRealization();
            ServerRealization = realization;

            CreateParameterString("SenderCompID", "");
            CreateParameterPassword("Password", "");
            CreateParameterString("MFIX Trade Account", "");
            CreateParameterString("MFIX Trade Client Code", "");
            CreateParameterString("FX MFIX Trade Address", "");
            CreateParameterInt("FX MFIX Trade Port", 0);
            CreateParameterString("FX MFIX Trade TargetCompID", "");
            CreateParameterString("Trading start time(MSK)", "09:50");
            CreateParameterPath("Multicast Config Directory");
            CreateParameterPassword("NEW MFIX Trade Server Password", "");
            CreateParameterInt("Limit of requests to the server (per second)", 30);
        }
    }
    public class MoexFixFastCurrencyServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public MoexFixFastCurrencyServerRealization()
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
            thread6.Name = "HistoricalReplayMoexFixFastCurrency";
            thread6.Start();

            Thread thread7 = new Thread(MFIXTradeServerProcessing);
            thread7.Name = "MFIXTradeServerProcessing";
            thread7.Start();
        }

        public void Connect(WebProxy proxy)
        {
            try
            {
                _securities.Clear();
                _myPortfolios.Clear();
                _subscribedSecurities.Clear();

                _senderCompID = ((ServerParameterString)ServerParameters[0]).Value;
                _password = ((ServerParameterPassword)ServerParameters[1]).Value;
                _MFIXTradeAccount = ((ServerParameterString)ServerParameters[2]).Value;
                _MFIXTradeClientCode = ((ServerParameterString)ServerParameters[3]).Value;
                _FXMFIXTradeAddress = ((ServerParameterString)ServerParameters[4]).Value;
                _FXMFIXTradePort = ((ServerParameterInt)ServerParameters[5]).Value;
                _FXMFIXTradeTargetCompID = ((ServerParameterString)ServerParameters[6]).Value;
                _startMOEXTime = DateTime.ParseExact(((ServerParameterString)ServerParameters[7]).Value, "HH:mm", System.Globalization.CultureInfo.InvariantCulture);
                _configDir = ((ServerParameterPath)ServerParameters[8]).Value;
                _MFIXTradeServerNewPassword = ((ServerParameterPassword)ServerParameters[9]).Value;

                if (_MFIXTradeServerNewPassword == _password) // if already changed password
                {
                    ((ServerParameterPassword)ServerParameters[9]).Value = "";
                    _MFIXTradeServerNewPassword = "";
                }

                int orderActionsLimit = ((ServerParameterInt)ServerParameters[10]).Value;

                _rateGateForOrders = new RateGate(orderActionsLimit, TimeSpan.FromSeconds(1));

                if (string.IsNullOrEmpty(_senderCompID) || string.IsNullOrEmpty(_password))
                {
                    SendLogMessage("Can`t run connector. No CompId or password", LogMessageType.Error);
                    return;
                }

                if (string.IsNullOrEmpty(_MFIXTradeAccount) || string.IsNullOrEmpty(_MFIXTradeClientCode))
                {
                    SendLogMessage("Connection terminated. You must specify FIX Trade Account and Client Code", LogMessageType.Error);
                    return;
                }

                if (string.IsNullOrEmpty(_FXMFIXTradeAddress) || string.IsNullOrEmpty(_FXMFIXTradeTargetCompID) || _FXMFIXTradePort == 0)
                {
                    SendLogMessage("Can`t run connector. No MFIX Trade parameters are specified", LogMessageType.Error);
                    return;
                }

                if (string.IsNullOrEmpty(_FXMFIXTradeAddress) || string.IsNullOrEmpty(_FXMFIXTradeTargetCompID) || _FXMFIXTradePort == 0)
                {
                    SendLogMessage("Can`t run connector. No MFIX Trade parameters are specified", LogMessageType.Error);
                    return;
                }

                if (string.IsNullOrEmpty(_configDir) || !Directory.Exists(_configDir) || !File.Exists(_configDir + "\\config.xml") || !File.Exists(_configDir + "\\template.xml"))
                {
                    SendLogMessage("Can`t run connector. No multicast directory are specified or the config and templates files don't exist", LogMessageType.Error);
                    return;
                }

                LoadFASTTemplates();

                CreateFXMFIXTradeConnection(_FXMFIXTradeAddress, _FXMFIXTradePort);

                if (_fxMFIXTradeConnectionSuccessful == true)
                {
                    List<FastConnection> _addressesInstruments = GetAddressesForFastConnect("Instrument Replay");
                    CreateSocketConnections(_addressesInstruments);
                }
                else
                {
                    SendLogMessage("Error logging on to MOEX MFIX. No response from server.", LogMessageType.Error);
                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                    }
                }

                if ((_socketSecurityStreamA != null || _socketSecurityStreamB != null))
                {
                    ServerStatus = ServerConnectStatus.Connect;
                    ConnectEvent();
                }
                else
                {
                    SendLogMessage("Connection FAST socket securities failed", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message.ToString(), LogMessageType.Error);
                SendLogMessage("Connection cannot be open to MOEX FIX/FAST servers. Error request", LogMessageType.Error);
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

            SendLogMessage("Connection Closed by FixFastCurrency. WebSocket Data Closed Event", LogMessageType.System);

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
            _totNumReports = 0;

            _tradesIncremental.Clear();
            _tradesSnapshotsMsgs.Clear();
            _tradesSnapshotsByName.Clear();
            _minRptSeqFromTrades.Clear();
            _waitingTrades.Clear();
            _tradeNumsForCheckMissed.Clear();

            _ordersIncremental.Clear();
            _ordersSnapshotsMsgs.Clear();
            _minRptSeqFromOrders.Clear();
            _ordersSnapshotsByName.Clear();
            _waitingDepthChanges.Clear();
            _depthChanges.Clear();
            _orderNumsForCheckMissed.Clear();

            _socketsOrders.Clear();
            _socketsTrades.Clear();

            _timeLastDataReceipt = DateTime.Now.AddMinutes(30);
            _timeOfTheLastMFIXMessage = DateTime.Now.AddMinutes(30);

            _tradeMessages = new ConcurrentQueue<OpenFAST.Message>();
            _orderMessages = new ConcurrentQueue<OpenFAST.Message>();

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
                    if (_socketSecurityStreamA != null)
                    {
                        try
                        {
                            _socketSecurityStreamA.Close();
                        }
                        catch
                        {
                            // ignore
                        }

                        try
                        {
                            _socketSecurityStreamB.Close();
                        }
                        catch
                        {
                            // ignore
                        }

                        _socketSecurityStreamA = null;
                        _socketSecurityStreamB = null;
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

                lock (_socketLockerFXMFIXTrade)
                {
                    // отключаемся от сервера FXMFIXTrade 
                    if (_fxMFIXTradeTcpClient != null && _fxMFIXTradeStream != null)
                    {
                        try
                        {
                            string logout = _fxMFIXTradeMessages.LogoutMessage(_msgSeqNum);
                            SendFXMFIXTradeMessage(logout);
                            _logoutInitiator = true;
                        }
                        catch
                        {
                            // ignore
                        }

                        while (_fxMFIXTradeTcpClient != null || _fxMFIXTradeStream != null)
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

        private void MFIXTradeServerProcessing()
        {
            DateTime lastMFIXTradeTestRequestTime = DateTime.MinValue;

            while (true)
            {
                // контроль входящих сообщений
                if (_fxMFIXTradeStream != null && _fxMFIXTradeStream.DataAvailable)
                {
                    FixMessageReader();
                }

                // контроль соединения с MFIX сервером
                if (_isThereFirstHearbeat == true && _timeOfTheLastMFIXMessage.AddMinutes(1) < DateTime.Now)
                {
                    if (lastMFIXTradeTestRequestTime > _timeOfTheLastMFIXMessage)
                    {
                        if (ServerStatus != ServerConnectStatus.Disconnect)
                        {
                            SendLogMessage("MFIX Trade server connection lost. No response for too long", LogMessageType.Error);
                            ServerStatus = ServerConnectStatus.Disconnect;
                            DisconnectEvent();
                        }
                    }
                    else
                    {
                        // send TestRequest
                        SendLogMessage($"Not get Heartbeat on time. Need send test request.", LogMessageType.System);
                        _fxMFIXTradeMessages.TestRequestMessage(_msgSeqNum, DateTime.UtcNow.ToString("OsEngine"));
                        _isThereFirstHearbeat = false;
                        lastMFIXTradeTestRequestTime = DateTime.Now;
                    }
                }
            }
        }

        public DateTime ServerTime { get; set; }

        public ServerType ServerType => ServerType.MoexFixFastCurrency;

        public ServerConnectStatus ServerStatus { get; set; } = ServerConnectStatus.Disconnect;

        public List<IServerParameter> ServerParameters { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

        #endregion

        #region 2 Properties

        private DateTime _startMOEXTime;

        // for FIX
        private string _senderCompID;
        private string _password;
        private string _MFIXTradeAccount; // торговый счет
        private string _MFIXTradeClientCode; // код клиента
        private string _FXMFIXTradeAddress;
        private int _FXMFIXTradePort;
        private string _FXMFIXTradeTargetCompID;
        private string _MFIXTradeServerNewPassword;
        private long _msgSeqNum = 1;
        private long _incomingFixMsgNum = 1;
        private bool _logoutInitiator = false;
        private bool _fxMFIXTradeConnectionSuccessful = false;
        private bool _isThereFirstHearbeat = false;
        private DateTime _timeOfTheLastMFIXMessage = DateTime.Now.AddMinutes(30);

        // for FAST

        private bool _afterStartTrading = true;
        private string _configDir;
        private Context _contextFAST;
        private MessageTemplate[] _templates;

        private Socket _socketSecurityStreamA;
        private Socket _socketSecurityStreamB;
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
        private IPEndPoint _historicalReplayEndPoint;
        private long _missingOLRBeginSeqNo = -1;
        private long _missingOLREndSeqNo = 0;
        private bool _missingOLRData = false;
        private long _missingTLRBeginSeqNo = -1;
        private long _missingTLREndSeqNo = 0;
        private bool _missingTLRData = false;
        private List<int> _missingOLRRptSeqNums = new List<int>();
        private List<int> _missingTLRRptSeqNums = new List<int>();
        private DateTime _timeLastDataReceipt = DateTime.Now.AddMinutes(30);

        #endregion

        #region 3 Securities

        public void GetSecurities()
        {
            DateTime waitingTimeSecurities = DateTime.Now;

            // даём время на загрузку бумаг
            while (DateTime.Now < waitingTimeSecurities.AddSeconds(15))
            {
                if (_allSecuritiesLoaded)
                {
                    SecurityEvent(_securities);
                    return;
                }
                SendLogMessage("Securities not dowloaded. Wait, please", LogMessageType.System);
                Thread.Sleep(1000);
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
            newPortfolio.Number = _MFIXTradeAccount;
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

        private string _socketLockerHistoricalReplay = "socketLockerMoexFixFastCurrencyHistoricalReplay";
        private string _socketLockerInstruments = "socketLockerMoexFixFastCurrencyInstruments";
        private string _socketLockerTradesSnapshots = "socketLockerMoexFixFastCurrencyTradeSnapshots";
        private string _socketLockerTradesIncremental = "socketLockerMoexFixFastCurrencyTradesIncremental";
        private string _socketLockerOrdersSnapshots = "socketLockerMoexFixFastCurrencyOrdersSnapshots";
        private string _socketLockerOrdersIncremental = "socketLockerMoexFixFastCurrencyOrdersIncremental";
        private string _socketLockerFXMFIXTrade = "_socketLockerFXMFIXTradeCurrency";
        private TcpClient _fxMFIXTradeTcpClient;
        private NetworkStream _fxMFIXTradeStream;
        private MessageConstructor _fxMFIXTradeMessages;

        private void CreateSocketConnections(List<FastConnection> connectParams)
        {
            try
            {
                for (int i = 0; i < connectParams.Count; i++)
                {
                    if (connectParams[i].FeedType == "Historical Replay")
                    {
                        IPAddress ipAddr = IPAddress.Parse(connectParams[i].SrsIP);

                        _historicalReplayEndPoint = new IPEndPoint(ipAddr, connectParams[i].Port);

                        return;
                    }

                    // Create a UDP socket
                    Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    // Configure the socket options
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, 1 * 1024 * 1024); // Set receive buffer size

                    //// Join the multicast group
                    IPAddress multicastAddress = IPAddress.Parse(connectParams[i].MulticastIP);
                    IPAddress sourceAddress = IPAddress.Parse(connectParams[i].SrsIP);

                    //// Bind the socket to the port
                    //// Specify the local IP address and port to bind to.
                    IPAddress localAddress = IPAddress.Any; // Listen on all available interfaces
                    IPEndPoint localEndPoint = new IPEndPoint(localAddress, connectParams[i].Port);

                    socket.Bind(localEndPoint);

                    byte[] membershipAddresses = new byte[12]; // 3 IPs * 4 bytes (IPv4)
                    Buffer.BlockCopy(multicastAddress.GetAddressBytes(), 0, membershipAddresses, 0, 4);
                    Buffer.BlockCopy(sourceAddress.GetAddressBytes(), 0, membershipAddresses, 4, 4);
                    Buffer.BlockCopy(localAddress.GetAddressBytes(), 0, membershipAddresses, 8, 4);
                    socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddSourceMembership, membershipAddresses);

                    if (connectParams[i].FeedType == "Instrument Replay")
                    {
                        if (connectParams[i].FeedID == "A")
                        {
                            _socketSecurityStreamA = socket;
                        }

                        if (connectParams[i].FeedID == "B")
                        {
                            _socketSecurityStreamB = socket;
                        }
                    }

                    if (connectParams[i].FeedType == "Trades Incremental")
                    {
                        if (connectParams[i].FeedID == "A")
                        {
                            _tradesIncrementalSocketA = socket;
                            _socketsTrades.Add(_tradesIncrementalSocketA);
                        }

                        if (connectParams[i].FeedID == "B")
                        {
                            _tradesIncrementalSocketB = socket;
                            _socketsTrades.Add(_tradesIncrementalSocketB);
                        }
                    }

                    if (connectParams[i].FeedType == "Trades Snapshot")
                    {
                        if (connectParams[i].FeedID == "A")
                        {
                            _tradesSnapshotSocketA = socket;
                            _socketsTrades.Add(_tradesSnapshotSocketA);
                        }

                        if (connectParams[i].FeedID == "B")
                        {
                            _tradesSnapshotSocketB = socket;
                            _socketsTrades.Add(_tradesSnapshotSocketB);
                        }
                    }

                    if (connectParams[i].FeedType == "Orders Incremental")
                    {
                        if (connectParams[i].FeedID == "A")
                        {
                            _ordersIncrementalSocketA = socket;
                            _socketsOrders.Add(_ordersIncrementalSocketA);
                        }

                        if (connectParams[i].FeedID == "B")
                        {
                            _ordersIncrementalSocketB = socket;
                            _socketsOrders.Add(_ordersIncrementalSocketB);
                        }
                    }

                    if (connectParams[i].FeedType == "Orders Snapshot")
                    {
                        if (connectParams[i].FeedID == "A")
                        {
                            _ordersSnapshotSocketA = socket;
                            _socketsOrders.Add(_ordersSnapshotSocketA);
                        }

                        if (connectParams[i].FeedID == "B")
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

        public List<FastConnection> GetAddressesForFastConnect(string connectionType)
        {
            // прочитать конфиг FIX/FAST соединения и вернуть список с адресами подключения

            List<FastConnection> fastConnections = new List<FastConnection>();
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(_configDir + "\\config.xml");

            XmlNode chanelNode = xmlDoc.SelectSingleNode("/configuration/channel");

            XmlNodeList connectNodes = chanelNode.SelectSingleNode("connections").SelectNodes("connection");

            for (int i = 0; i < connectNodes.Count; i++)
            {
                string feedType = connectNodes[i].SelectSingleNode("type").Attributes["feed-type"].Value;

                if (!feedType.Equals(connectionType))
                    continue;

                if (feedType.Equals("Historical Replay"))
                {
                    FastConnection connection = new FastConnection();

                    XmlNode recoveryPoint = connectNodes[i] as XmlNode;

                    if (recoveryPoint != null)
                    {
                        connection.FeedType = feedType;
                        connection.SrsIP = recoveryPoint.SelectSingleNode("ip").InnerText;
                        connection.Port = Convert.ToInt32(recoveryPoint.SelectSingleNode("port").InnerText);
                    }
                    else { SendLogMessage("Couldn't get a TCP recovery address", LogMessageType.Error); }

                    fastConnections.Add(connection);
                    return fastConnections;
                }

                XmlNodeList feed = connectNodes[i].SelectNodes("feed");

                for (int j = 0; j < feed.Count; j++)
                {
                    FastConnection connection = new FastConnection();

                    connection.FeedType = feedType;
                    connection.FeedID = feed[j].Attributes["id"].Value;
                    connection.SrsIP = feed[j].SelectSingleNode("src-ip").InnerText;
                    connection.MulticastIP = feed[j].SelectSingleNode("ip").InnerText;
                    connection.Port = Convert.ToInt32(feed[j].SelectSingleNode("port").InnerText);

                    fastConnections.Add(connection);
                }
            }
            return fastConnections;
        }

        private void CreateFXMFIXTradeConnection(string fxMFIXTradeAddress, int fxMFIXTradePort)
        {
            _fxMFIXTradeTcpClient = new TcpClient(fxMFIXTradeAddress, fxMFIXTradePort);
            _fxMFIXTradeStream = _fxMFIXTradeTcpClient.GetStream();

            if (_fxMFIXTradeTcpClient.Connected)
            {
                WriteLogFXMFIXTrade("\n-------------- New Session -------------\n");

                _fxMFIXTradeMessages = new MessageConstructor(_senderCompID, _FXMFIXTradeTargetCompID);

                bool resetSeqNum = _msgSeqNum == 1 ? true : false;

                string logonMsg = _fxMFIXTradeMessages.LogonMessage(_password, _msgSeqNum, 30, resetSeqNum, _MFIXTradeServerNewPassword);

                SendFXMFIXTradeMessage(logonMsg);

                DateTime waitingLogonTime = DateTime.Now;

                while (waitingLogonTime.AddSeconds(20) > DateTime.Now)
                {
                    if (_fxMFIXTradeConnectionSuccessful)
                        return;

                    Thread.Sleep(1000);
                    SendLogMessage("Wait FXMFIXtrade connection", LogMessageType.System);
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

        private void SendFXMFIXTradeMessage(string message)
        {
            try
            {
                byte[] dataMes = Encoding.UTF8.GetBytes(message);

                _fxMFIXTradeStream.Write(dataMes, 0, dataMes.Length);
            }
            catch (Exception ex)
            {
                SendLogMessage("Error sending FIX Message " + ex.ToString(), LogMessageType.Error);
                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            _msgSeqNum++;

            WriteLogFXMFIXTrade("Sent: " + message);
        }

        #endregion

        #region  7 Security subscribe

        private List<string> _subscribedSecurities = new List<string>();

        public void Subscribe(Security security)
        {
            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                return;
            }

            try
            {
                string uniqueName = security.Name + security.NameClass;  // название бумаги может дублироваться в разных режимах, поэтому создаем уникальное имя

                for (int i = 0; i < _subscribedSecurities.Count; i++)
                {
                    if (_subscribedSecurities[i].Equals(uniqueName))
                    {
                        return;
                    }
                }

                _afterStartTrading = _startMOEXTime > DateTime.UtcNow.AddHours(3) ? false : true;

                if (_tradesIncrementalSocketA == null && _tradesIncrementalSocketB == null
                    && _ordersIncrementalSocketA == null && _ordersIncrementalSocketB == null)
                {
                    CreateSocketConnections(GetAddressesForFastConnect("Trades Incremental"));
                    CreateSocketConnections(GetAddressesForFastConnect("Orders Incremental"));
                }

                if (_afterStartTrading) // если берем инструмент после начала торгов
                {
                    if (_ordersSnapshotSocketA == null && _ordersSnapshotSocketB == null
                        && _tradesSnapshotSocketA == null && _tradesSnapshotSocketB == null)
                    {
                        CreateSocketConnections(GetAddressesForFastConnect("Trades Snapshot"));
                        CreateSocketConnections(GetAddressesForFastConnect("Orders Snapshot"));
                    }

                    _tradesSnapshotsByName.Add(uniqueName, new Snapshot());
                    _ordersSnapshotsByName.Add(uniqueName, new Snapshot());
                    _waitingDepthChanges.Add(uniqueName, new List<OrderChange>());
                    _waitingTrades.Add(uniqueName, new List<WaitingTrade>());

                }

                //добавляем в необходимые списки
                _depthChanges.Add(uniqueName, new List<OrderChange>());
                _tradeNumsForCheckMissed.Add(uniqueName, new List<NumbersData>());
                _orderNumsForCheckMissed.Add(uniqueName, new List<NumbersData>());

                _subscribedSecurities.Add(uniqueName);

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
        private long _totNumReports = 0;

        private void InstrumentDefinitionsReader()
        {
            byte[] buffer = new byte[4096];

            List<long> snapshotIds = new List<long>();
            List<Security> securities = new List<Security>();

            Thread.Sleep(1000);
            DateTime timeLastDataReceipt = DateTime.Now;
            Context context = null;

            while (true)
            {
                try
                {
                    if (_socketSecurityStreamA == null || _socketSecurityStreamB == null)
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
                        context = CreateNewContext();
                    }

                    // читаем из потоков А и B
                    for (int s = 0; s < 2; s++)
                    {
                        int length = s == 0 ? _socketSecurityStreamA.Receive(buffer) : _socketSecurityStreamB.Receive(buffer);

                        using (MemoryStream stream = new MemoryStream(buffer, 4, length))
                        {
                            FastDecoder decoder = new FastDecoder(context, stream);
                            OpenFAST.Message msg = decoder.ReadMessage();

                            string msgType = msg.GetString("MessageType");
                            long msgSeqNum = long.Parse(msg.GetString("MsgSeqNum"));

                            if (msgType == "d") /// security definition
                            {
                                timeLastDataReceipt = DateTime.Now;
                                _totNumReports = msg.GetLong("TotNumReports"); // общее число "бумаг" 

                                if (snapshotIds.FindIndex(nmb => nmb == msgSeqNum) != -1)
                                {
                                    if (snapshotIds.Count == _totNumReports)
                                    {
                                        _securities = securities;
                                        _allSecuritiesLoaded = true;

                                        SendLogMessage($"Загружено {_securities.Count} бумаг.", LogMessageType.System);
                                    }

                                    continue;
                                }

                                snapshotIds.Add(msgSeqNum);

                                string symbol = msg.GetString("Symbol");
                                string securityID = msg.IsDefined("SecurityID") ? msg.GetString("SecurityID") : msg.GetString("CFICode");
                                string currency = msg.GetString("SettlCurrency");
                                string marketCode = msg.GetString("MarketCode");

                                if (marketCode != "CURR")
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

                                if (securityAlreadyPresent) // если бумага в списке есть, дальше не обрабатываем
                                {
                                    continue;
                                }

                                // Обрабатываем новые бумаги

                                string secDecimals = "0";
                                string lot = "1";

                                string name = Encoding.UTF8.GetString(msg.GetBytes("EncodedSecurityDesc"));

                                string TradingSessionID = "CETS"; // по-умолчанию системные сделки
                                string TradingSessionSubID = "N"; // по-умолчанию нормальный период торгов

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
                                                TradingSessionSubID = trdSessionGrp.GetString("TradingSessionSubID");
                                            }
                                        }
                                    }
                                }

                                if (TradingSessionID == "CNGD") // не берем режим CNGD т.к. не торгуется в тестах
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

                                if (TradingSessionSubID != "NA")
                                {
                                    newSecurity.State = SecurityStateType.Activ;
                                }
                                else
                                {
                                    newSecurity.State = SecurityStateType.Close;
                                }

                                string productType = msg.IsDefined("Product") ? msg.GetString("Product") : "не определено";

                                switch (productType)
                                {
                                    case "4":
                                        newSecurity.SecurityType = SecurityType.CurrencyPair;
                                        break;
                                    case "7":
                                        newSecurity.SecurityType = SecurityType.Index;
                                        break;
                                    default:
                                        newSecurity.SecurityType = SecurityType.None;
                                        break;
                                }

                                newSecurity.NameClass = TradingSessionID;
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

        Dictionary<long, OpenFAST.Message> _tradesIncremental = new Dictionary<long, OpenFAST.Message>();
        Dictionary<long, OpenFAST.Message> _tradesSnapshotsMsgs = new Dictionary<long, OpenFAST.Message>();

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

                            if (msg.GetString("MessageType") == "W")
                            {
                                string name = msg.GetString("Symbol");
                                string TradingSessionID = msg.GetString("TradingSessionID");
                                string uniqueName = name + TradingSessionID;

                                bool needAddMsg = IsMessageMissed(_tradesSnapshotsMsgs, msgSeqNum, msg);

                                if (needAddMsg)
                                {
                                    if (IsSubscribedToThisSecurity(uniqueName))
                                        _tradeMessages.Enqueue(msg);
                                }
                            }

                            if (msg.GetString("MessageType") == "X")
                            {
                                bool needAddMsg = IsMessageMissed(_tradesIncremental, msgSeqNum, msg);

                                if (needAddMsg) // если предыдущего сообщения с таким номером не было, т.е. обрабытываем только уникальные сообщения
                                {
                                    if (msg.IsDefined("GroupMDEntries"))
                                    {
                                        SendMsgToQueue(msg, _tradeMessages);
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

        private ConcurrentQueue<OpenFAST.Message> _tradeMessages = new ConcurrentQueue<OpenFAST.Message>();
        private Dictionary<string, Snapshot> _tradesSnapshotsByName = new Dictionary<string, Snapshot>(); // для восстановления
        private Dictionary<string, List<NumbersData>> _tradeNumsForCheckMissed = new Dictionary<string, List<NumbersData>>(); // для проверки пропука данных по RptSeq
        private Dictionary<string, List<WaitingTrade>> _waitingTrades = new Dictionary<string, List<WaitingTrade>>(); // для ожидания снэпшота
        Dictionary<string, int> _minRptSeqFromTrades = new Dictionary<string, int>(); // минимальные значения полей RptSeq(83)

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
                        if (msg.IsDefined("GroupMDEntries"))
                        {
                            SequenceValue secVal = msg.GetValue("GroupMDEntries") as SequenceValue;

                            for (int i = 0; i < secVal.Length; i++)
                            {
                                GroupValue groupVal = secVal[i] as GroupValue;

                                string name = groupVal.GetString("Symbol");
                                string TradingSessionID = groupVal.GetString("TradingSessionID");
                                string uniqueName = name + TradingSessionID;

                                if (!IsSubscribedToThisSecurity(uniqueName)) // если не подписаны на этот инструмент, трейд не берем
                                    continue;

                                string MDEntryType = groupVal.GetString("MDEntryType");
                                int RptSeqFromTrades = groupVal.GetInt("RptSeq");

                                // проверка пропуска даных по RptSeq и дублирования
                                if (_tradeNumsForCheckMissed[uniqueName].Find(n => n.RptSeq == RptSeqFromTrades) != null)
                                    continue;

                                _tradeNumsForCheckMissed[uniqueName].Add(new NumbersData { MsgSeqNum = msgSeqNum, RptSeq = RptSeqFromTrades });

                                if (!_afterStartTrading && _tradeNumsForCheckMissed[uniqueName].Find(n => n.RptSeq == 1) == null)
                                {
                                    SendLogMessage("При подключении до старта торгов, отсутствует первое сообщение.\n Нужно указать верное время начала поступления данных и переподключиться", LogMessageType.Error);
                                    if (ServerStatus != ServerConnectStatus.Disconnect)
                                    {
                                        ServerStatus = ServerConnectStatus.Disconnect;
                                        DisconnectEvent();
                                    }
                                }

                                int missedRptSeqCount = 0;

                                _missingTLRData = IsDataMissed(_tradeNumsForCheckMissed[uniqueName], out _missingTLRBeginSeqNo, out _missingTLREndSeqNo, out missedRptSeqCount, out _missingTLRRptSeqNums);

                                if (_missingTLRData && missedRptSeqCount >= 5)
                                {
                                    WriteLogTrades($"Требуется восстановление заявок в трейдах. Номера сообщений с {_missingTLRBeginSeqNo} по {_missingTLREndSeqNo}. Количество пропущенных RptSeq: {missedRptSeqCount}\n");

                                    if (_historicalReplayEndPoint == null)
                                        CreateSocketConnections(GetAddressesForFastConnect("Historical Replay"));
                                }

                                // храним минимальный номер обновления по инструменту
                                if (_minRptSeqFromTrades.ContainsKey(uniqueName))
                                {
                                    if (_minRptSeqFromTrades[uniqueName] > RptSeqFromTrades)
                                    {
                                        _minRptSeqFromTrades[uniqueName] = RptSeqFromTrades;
                                    }
                                }
                                else
                                {
                                    _minRptSeqFromTrades.Add(uniqueName, RptSeqFromTrades);
                                }

                                if (MDEntryType == "z")
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

                                    //(если по этой бумаге снэпшот применен, трейд обновляем сразу)
                                    if (_afterStartTrading && _tradesSnapshotsByName.ContainsKey(uniqueName))
                                    {
                                        if (!_tradesSnapshotsByName[uniqueName].SnapshotWasApplied)
                                        {
                                            WaitingTrade waitingTrade = new WaitingTrade();
                                            waitingTrade.Trade = trade;
                                            waitingTrade.UniqueName = uniqueName;
                                            waitingTrade.RptSeq = RptSeqFromTrades;

                                            _waitingTrades[uniqueName].Add(waitingTrade);

                                            WriteLogTrades($"Получен трейд в ОЖИДАЮЩИЕ: RptSeq: {RptSeqFromTrades}, Инструмент: {trade.SecurityNameCode}, {trade.Side}, Цена: {trade.Price}, Время: {trade.Time}, Объем: {trade.Volume}");
                                        }
                                        else
                                        {
                                            NewTradesEvent(trade);
                                            WriteLogTrades($"Получен трейд в СИСТЕМУ: RptSeq: {RptSeqFromTrades}, Инструмент: {trade.SecurityNameCode}, {trade.Side}, Цена: {trade.Price}, Время: {trade.Time}, Объем: {trade.Volume}");
                                        }
                                    }
                                    else
                                    {
                                        NewTradesEvent(trade);
                                        WriteLogTrades($"Получен трейд в СИСТЕМУ: RptSeq: {RptSeqFromTrades}, Инструмент: {trade.SecurityNameCode}, {trade.Side}, Цена: {trade.Price}, Время: {trade.Time}, Объем: {trade.Volume}");
                                    }
                                }
                            }
                        }
                    }

                    if (msgType == "W") // снэпшот
                    {
                        int RptSeq = msg.GetInt("RptSeq");
                        string LastFragment = msg.GetString("LastFragment");
                        string RouteFirst = msg.GetString("RouteFirst");

                        // когда торги по инструменту не идут приходят такие фрагменты снэпшотов
                        if (RptSeq == 0 && LastFragment == "1" && RouteFirst == "1")
                            continue;

                        string name = msg.GetString("Symbol");
                        string TradingSessionID = msg.GetString("TradingSessionID");
                        long LastMsgSeqNumProcessed = msg.GetLong("LastMsgSeqNumProcessed");
                        long MsgSeqNum = msg.GetLong("MsgSeqNum");

                        string uniqueName = name + TradingSessionID;

                        SnapshotFragment fragment = new SnapshotFragment();
                        fragment.MsgSeqNum = MsgSeqNum;
                        fragment.RptSeq = RptSeq;
                        fragment.LastFragment = LastFragment == "1" ? true : false;
                        fragment.RouteFirst = RouteFirst == "1" ? true : false;
                        fragment.Symbol = name;
                        fragment.TradingSessionID = TradingSessionID;

                        WriteLogTrades($"Получен фрагмент снэпшота по инструменту: {uniqueName}, MsgSeqNum: {MsgSeqNum}, Первый: {fragment.RouteFirst}, Последний: {fragment.LastFragment}, RptSeq: {RptSeq}");

                        if (msg.IsDefined("GroupMDEntries"))
                        {
                            SequenceValue secVal = msg.GetValue("GroupMDEntries") as SequenceValue;

                            for (int i = 0; i < secVal.Length; i++)
                            {
                                GroupValue groupVal = secVal[i] as GroupValue;

                                string MDEntryType = groupVal.GetString("MDEntryType");

                                if (MDEntryType == "z")
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

                                    if (fragment.trades == null)
                                    {
                                        fragment.trades = new List<Trade>();
                                        fragment.trades.Add(trade);
                                    }
                                    else
                                    {
                                        fragment.trades.Add(trade);
                                    }
                                }

                                if (MDEntryType == "J")
                                {
                                    WriteLogTrades($"Пустой снэпшот");
                                    continue;
                                }
                            }
                        }

                        // добавляем сообщение в снэпшот и проверяем его готовность
                        if (_tradesSnapshotsByName[uniqueName].SnapshotFragments == null)
                        {
                            _tradesSnapshotsByName[uniqueName].SnapshotFragments = new List<SnapshotFragment>();
                            _tradesSnapshotsByName[uniqueName].SnapshotFragments.Add(fragment);
                            _tradesSnapshotsByName[uniqueName].RptSeq = fragment.RptSeq;
                            _tradesSnapshotsByName[uniqueName].Symbol = fragment.Symbol;
                            _tradesSnapshotsByName[uniqueName].TradingSessionID = fragment.TradingSessionID;
                        }
                        else
                        {
                            if (_tradesSnapshotsByName[uniqueName].SnapshotFragments[0].RptSeq != fragment.RptSeq)
                            {
                                _tradesSnapshotsByName[uniqueName].SnapshotFragments.Clear();
                                WriteLogTrades($"Пришел фрагмент по инструменту {uniqueName} с другим RptSeq. Ждем новый цикл");
                            }

                            _tradesSnapshotsByName[uniqueName].SnapshotFragments.Add(fragment);
                        }

                        // если снэпшот сформирован
                        if (_tradesSnapshotsByName[uniqueName].IsComletedSnapshot(_tradesSnapshotsByName[uniqueName].SnapshotFragments) == true)
                        {

                            // и его RptSeq больше минимального RptSeq из инкримента, то применяем обновление
                            if (_minRptSeqFromTrades.ContainsKey(uniqueName) && (_tradesSnapshotsByName[uniqueName].RptSeq >= _minRptSeqFromTrades[uniqueName]
                                || _tradesSnapshotsByName[uniqueName].RptSeq == _minRptSeqFromTrades[uniqueName] - 1
                                || _tradesSnapshotsByName[uniqueName].RptSeq != 0))
                            {
                                WriteLogTrades($"Снэпшот по инструменту {uniqueName} сформирован");

                                // отправляем трейды из снэпшота в систему
                                for (int i = 0; i < _tradesSnapshotsByName[uniqueName].SnapshotFragments.Count; i++)
                                {
                                    List<Trade> _trades = _tradesSnapshotsByName[uniqueName].SnapshotFragments[i].trades;

                                    for (int k = 0; k < _trades.Count; k++)
                                    {
                                        NewTradesEvent(_trades[k]);
                                    }
                                }

                                int _RptSeqFromSnapshot = _tradesSnapshotsByName[uniqueName].RptSeq;

                                // отправляем трейды из ожидающих в систему
                                for (int j = 0; j < _waitingTrades[uniqueName].Count; j++)
                                {
                                    if (_waitingTrades[uniqueName][j].RptSeq <= _RptSeqFromSnapshot)
                                        continue;

                                    NewTradesEvent(_waitingTrades[uniqueName][j].Trade);
                                }

                                _tradesSnapshotsByName[uniqueName].SnapshotWasApplied = true;

                                // чистим фрагменты снэпшота и ожидающие трейды
                                _tradesSnapshotsByName[uniqueName].SnapshotFragments.Clear();
                                _waitingTrades[uniqueName].Clear();

                                WriteLogTrades($"Снэпшот применен");

                                if (!IsNeedSnapshotSockets(_tradesSnapshotsByName) && _tradesSnapshotSocketA != null)
                                {
                                    lock (_socketLockerTradesSnapshots)
                                    {
                                        // удаляем из списка читаемых сокетов и закрываем
                                        _socketsTrades.Remove(_tradesSnapshotSocketA);
                                        _socketsTrades.Remove(_tradesSnapshotSocketB);

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
                                    WriteLogTrades("Сокеты снэпшотов закрыты");
                                }
                            }
                            else
                            {
                                WriteLogTrades($"Разница между RptSeq снэпшота и минимальным RptSeq трейда слишком большая. Снэпшот не применен");
                                _tradesSnapshotsByName[uniqueName].SnapshotFragments.Clear();
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

                            if (msg.GetString("MessageType") == "W")
                            {
                                string name = msg.GetString("Symbol");
                                string TradingSessionID = msg.GetString("TradingSessionID");
                                string uniqueName = name + TradingSessionID;

                                bool needAddMsg = IsMessageMissed(_ordersSnapshotsMsgs, msgSeqNum, msg);

                                if (needAddMsg)
                                {
                                    if (IsSubscribedToThisSecurity(uniqueName))
                                        _orderMessages.Enqueue(msg);
                                }
                            }

                            if (msg.GetString("MessageType") == "X")
                            {
                                bool needAddMsg = IsMessageMissed(_ordersIncremental, msgSeqNum, msg);

                                if (needAddMsg) // если предыдущего сообщения с таким номером не было
                                {
                                    if (msg.IsDefined("GroupMDEntries"))
                                    {
                                        SendMsgToQueue(msg, _orderMessages);
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

        private ConcurrentQueue<OpenFAST.Message> _orderMessages = new ConcurrentQueue<OpenFAST.Message>();
        private Dictionary<string, Snapshot> _ordersSnapshotsByName = new Dictionary<string, Snapshot>(); // для восстановления
        private Dictionary<string, List<OrderChange>> _depthChanges = new Dictionary<string, List<OrderChange>>(); // для обновления стакана
        private Dictionary<string, List<OrderChange>> _waitingDepthChanges = new Dictionary<string, List<OrderChange>>(); // для ожидания снэпшота
        private Dictionary<string, List<NumbersData>> _orderNumsForCheckMissed = new Dictionary<string, List<NumbersData>>(); // для проверки пропука данных по RptSeq
        private DateTime _lastMdTime = DateTime.MinValue;
        Dictionary<string, int> _minRptSeqFromOrders = new Dictionary<string, int>();

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
                        if (msg.IsDefined("GroupMDEntries"))
                        {
                            SequenceValue secVal = msg.GetValue("GroupMDEntries") as SequenceValue;

                            for (int i = 0; i < secVal.Length; i++)
                            {
                                GroupValue groupVal = secVal[i] as GroupValue;

                                string name = groupVal.GetString("Symbol");
                                string TradingSessionID = groupVal.GetString("TradingSessionID");
                                string uniqueName = name + TradingSessionID;

                                if (!IsSubscribedToThisSecurity(uniqueName)) // если не подписаны на этот инструмент, ордер не берем
                                    continue;

                                string MDEntryType = groupVal.GetString("MDEntryType");
                                int RptSeqFromOrder = groupVal.GetInt("RptSeq");

                                // проверка пропуска даных по RptSeq и дублирования сообщений
                                if (_orderNumsForCheckMissed[uniqueName].Find(n => n.RptSeq == RptSeqFromOrder) != null)
                                    continue;

                                _orderNumsForCheckMissed[uniqueName].Add(new NumbersData { MsgSeqNum = msgSeqNum, RptSeq = RptSeqFromOrder });

                                if (!_afterStartTrading && _orderNumsForCheckMissed[uniqueName].Find(n => n.RptSeq == 1) == null)
                                {
                                    SendLogMessage("При подключении до старта торгов, отсутствует первое сообщение.\n Нужно указать верное время начала поступления данных и переподключиться", LogMessageType.Error);
                                    if (ServerStatus != ServerConnectStatus.Disconnect)
                                    {
                                        ServerStatus = ServerConnectStatus.Disconnect;
                                        DisconnectEvent();
                                    }
                                }

                                int missedRptSeqCount = 0;

                                _missingOLRData = IsDataMissed(_orderNumsForCheckMissed[uniqueName], out _missingOLRBeginSeqNo, out _missingOLREndSeqNo, out missedRptSeqCount, out _missingOLRRptSeqNums);

                                if (_missingOLRData && missedRptSeqCount >= 5)
                                {
                                    WriteLogOrders($"Требуется восстановление заявок в стакане. Номера сообщений с {_missingOLRBeginSeqNo} по {_missingOLREndSeqNo}. Количество пропущенных RptSeq: {missedRptSeqCount}.");

                                    if (_historicalReplayEndPoint == null)
                                        CreateSocketConnections(GetAddressesForFastConnect("Historical Replay"));
                                }

                                // храним минимальный номер обновления по инструменту
                                if (_minRptSeqFromOrders.ContainsKey(uniqueName))
                                {
                                    if (_minRptSeqFromOrders[uniqueName] > RptSeqFromOrder)
                                    {
                                        _minRptSeqFromOrders[uniqueName] = RptSeqFromOrder;
                                    }
                                }
                                else
                                {
                                    _minRptSeqFromOrders.Add(uniqueName, RptSeqFromOrder);
                                }

                                OrderChange newOrderChange = new OrderChange();

                                string orderType;

                                switch (MDEntryType)// 0 - котировка на покупку, 1 - котровка на продажу
                                {
                                    case "0": orderType = "bid"; break;
                                    case "1": orderType = "ask"; break;
                                    default: orderType = "Order type ERROR"; break;
                                }

                                string action;

                                switch (groupVal.GetString("MDUpdateAction"))
                                {
                                    case "0": action = "add"; break;
                                    case "1": action = "change"; break;
                                    case "2": action = "delete"; break;
                                    default: action = "Action ERROR"; break;
                                }

                                decimal price = groupVal.GetString("MDEntryPx").ToDecimal();

                                string time = groupVal.GetString("MDEntryTime");
                                if (time.Length == 8)
                                {
                                    time = "0" + time;
                                }

                                time = DateTime.UtcNow.ToString("ddMMyyyy") + time;
                                DateTime orderDateTime = DateTime.ParseExact(time, "ddMMyyyyHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);

                                string id = groupVal.GetString("MDEntryID");
                                decimal volume = groupVal.GetString("MDEntrySize").ToDecimal();

                                newOrderChange.UniqueName = uniqueName;
                                newOrderChange.MDEntryID = id;
                                newOrderChange.OrderType = orderType;
                                newOrderChange.Price = price;
                                newOrderChange.Action = action;
                                newOrderChange.RptSeq = RptSeqFromOrder;
                                newOrderChange.Volume = volume;

                                if (_afterStartTrading && _ordersSnapshotsByName.ContainsKey(uniqueName))
                                {
                                    if (!_ordersSnapshotsByName[uniqueName].SnapshotWasApplied)
                                    {
                                        // пока снэпшот не применен, изменения заявок складываем в ожидающие
                                        _waitingDepthChanges[uniqueName].Add(newOrderChange);

                                        WriteLogOrders($"Получен ордер в ОЖИДАЮЩИЕ: RptSeq: {RptSeqFromOrder}, Инструмент: {uniqueName}, ID: {id}, Действие: {action}, Цена: {price}, Объем: {volume}");
                                    }
                                    else
                                    {
                                        // обработка и сразу в систему
                                        UpdateOrderData(uniqueName, newOrderChange);

                                        UpdateDepth(_depthChanges[uniqueName], name);

                                        WriteLogOrders($"Получен ордер в СИСТЕМУ. СТАКАН ОБНОВЛЕН: RptSeq: {RptSeqFromOrder}, Интсрумент: {uniqueName}, ID: {id}, Действие: {action}, Цена: {price}, Объем: {volume}");
                                    }
                                }
                                else
                                {
                                    // обработка и сразу в систему
                                    UpdateOrderData(uniqueName, newOrderChange);

                                    UpdateDepth(_depthChanges[uniqueName], name);

                                    WriteLogOrders($"Получен ордер в СИСТЕМУ. СТАКАН ОБНОВЛЕН: RptSeq: {RptSeqFromOrder}, Интсрумент: {uniqueName}, ID: {id}, Действие: {action}, Цена: {price}, Объем: {volume}");
                                }
                            }
                        }
                    }

                    if (msgType == "W") // снэпшот
                    {
                        int RptSeq = msg.GetInt("RptSeq");
                        string LastFragment = msg.GetString("LastFragment");
                        string RouteFirst = msg.GetString("RouteFirst");

                        // когда торги по инструменту не идут приходят такие фрагменты снэпшотов
                        if (RptSeq == 0 && LastFragment == "1" && RouteFirst == "1")
                        {
                            continue;
                        }

                        string name = msg.GetString("Symbol");
                        string TradingSessionID = msg.GetString("TradingSessionID");
                        long LastMsgSeqNumProcessed = msg.GetLong("LastMsgSeqNumProcessed");
                        long MsgSeqNum = msg.GetLong("MsgSeqNum");

                        string uniqueName = name + TradingSessionID;

                        SnapshotFragment fragment = new SnapshotFragment();
                        fragment.MsgSeqNum = MsgSeqNum;
                        fragment.RptSeq = RptSeq;
                        fragment.LastFragment = LastFragment == "1" ? true : false;
                        fragment.RouteFirst = RouteFirst == "1" ? true : false;
                        fragment.Symbol = name;
                        fragment.TradingSessionID = TradingSessionID;
                        fragment.mdLevel = new List<MarketDepthLevel>();

                        WriteLogOrders($"Получен фрагмент снэпшота по инструменту: {uniqueName}, MsgSeqNum: {MsgSeqNum}, Первый: {fragment.RouteFirst}, Последний: {fragment.LastFragment}, RptSeq: {RptSeq}");

                        if (msg.IsDefined("GroupMDEntries"))
                        {
                            SequenceValue secVal = msg.GetValue("GroupMDEntries") as SequenceValue;

                            for (int i = 0; i < secVal.Length; i++)
                            {
                                GroupValue groupVal = secVal[i] as GroupValue;

                                OrderChange newOrderChange = new OrderChange();
                                MarketDepthLevel level = new MarketDepthLevel();

                                string MDEntryType = groupVal.GetString("MDEntryType");
                                double price = groupVal.GetString("MDEntryPx").ToDouble();
                                string Id = groupVal.GetString("MDEntryID");
                                double volume = groupVal.GetString("MDEntrySize").ToDouble();

                                string orderType;

                                switch (MDEntryType)// 0 - котировка на покупку, 1 - котровка на продажу
                                {
                                    case "0":
                                        orderType = "bid";
                                        level.Bid = volume;
                                        level.Ask = 0;
                                        break;
                                    case "1":
                                        orderType = "ask";
                                        level.Ask = volume;
                                        level.Bid = 0;
                                        break;
                                    case "J": // пустой снэпшот
                                        continue;
                                    default: orderType = "Order type ERROR"; break;
                                }

                                string time = groupVal.GetString("MDEntryTime");
                                if (time.Length == 8)
                                {
                                    time = "0" + time;
                                }

                                time = DateTime.UtcNow.ToString("ddMMyyyy") + time;
                                DateTime orderDateTime = DateTime.ParseExact(time, "ddMMyyyyHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);

                                level.Price = price;

                                fragment.mdLevel.Add(level); // для первичного заполнения стакана

                                newOrderChange.UniqueName = uniqueName;
                                newOrderChange.MDEntryID = Id;
                                newOrderChange.Price = price.ToDecimal();
                                newOrderChange.Volume = volume.ToDecimal();
                                newOrderChange.OrderType = orderType;
                                newOrderChange.Action = string.Empty;

                                _depthChanges[uniqueName].Add(newOrderChange);
                            }
                        }

                        // добавляем сообщение в снэпшот и проверяем его готовность
                        if (_ordersSnapshotsByName[uniqueName].SnapshotFragments == null)
                        {
                            _ordersSnapshotsByName[uniqueName].SnapshotFragments = new List<SnapshotFragment>();
                            _ordersSnapshotsByName[uniqueName].SnapshotFragments.Add(fragment);
                            _ordersSnapshotsByName[uniqueName].RptSeq = fragment.RptSeq;
                            _ordersSnapshotsByName[uniqueName].Symbol = fragment.Symbol;
                            _ordersSnapshotsByName[uniqueName].TradingSessionID = fragment.TradingSessionID;
                        }
                        else
                        {
                            if (_ordersSnapshotsByName[uniqueName].SnapshotFragments[0].RptSeq != fragment.RptSeq)
                            {
                                _ordersSnapshotsByName[uniqueName].SnapshotFragments.Clear();

                                WriteLogOrders($"Пришел фрагмент по инструменту {uniqueName} с другим RptSeq. Ждем новый цикл.");
                            }
                            _ordersSnapshotsByName[uniqueName].SnapshotFragments.Add(fragment);
                        }

                        // если снэпшот сформирован
                        if (_ordersSnapshotsByName[uniqueName].IsComletedSnapshot(_ordersSnapshotsByName[uniqueName].SnapshotFragments) == true)
                        {
                            WriteLogOrders($"Снэпшот {uniqueName} сформирован.");

                            // и его RptSeq больше минимального RptSeq из инкримента, то применяем обновление
                            if (_minRptSeqFromOrders.ContainsKey(uniqueName) && (_ordersSnapshotsByName[uniqueName].RptSeq >= _minRptSeqFromOrders[uniqueName]
                                || _ordersSnapshotsByName[uniqueName].RptSeq == _minRptSeqFromOrders[uniqueName] - 1 || _ordersSnapshotsByName[uniqueName].RptSeq != 0))
                            {
                                // формируем первый стакан из снэпшота
                                MarketDepth marketDepth = new MarketDepth();

                                marketDepth = MakeFirstDepth(uniqueName, _ordersSnapshotsByName);

                                marketDepth.SecurityNameCode = name;
                                marketDepth.Time = DateTime.UtcNow;
                                MarketDepthEvent(marketDepth);

                                WriteLogOrders($"Первый стакан по инструменту {name} отправлен.");

                                int rptSeqFromSnapshot = _ordersSnapshotsByName[uniqueName].RptSeq;

                                // меняем стакан, согласно ожидающих заявок
                                for (int j = 0; j < _waitingDepthChanges[uniqueName].Count; j++)
                                {
                                    if (_waitingDepthChanges[uniqueName][j].RptSeq <= rptSeqFromSnapshot)
                                        continue;

                                    UpdateOrderData(uniqueName, _waitingDepthChanges[uniqueName][j]);
                                }

                                _ordersSnapshotsByName[uniqueName].SnapshotWasApplied = true;

                                // чистим фрагменты снэпшота и ожидающие трейды
                                _ordersSnapshotsByName[uniqueName].SnapshotFragments.Clear();
                                _waitingDepthChanges[uniqueName].Clear();

                                WriteLogOrders($"Снэпшот по инструменту {uniqueName} применен");

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
                            else
                            {
                                WriteLogOrders($"Разница между RptSeq снэпшота и минимальным RptSeq ордера слишком большая. Снэпшот не применен");
                                _ordersSnapshotsByName[uniqueName].SnapshotFragments.Clear();
                            }
                        }
                        else
                        {
                            WriteLogOrders($"Снэпшот по инструменту: {uniqueName} не сформирован. Ждем новый цикл\n");
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

        private void UpdateDepth(List<OrderChange> orderChanges, string securityName)
        {
            MarketDepth marketDepth = new MarketDepth();
            List<MarketDepthLevel> asks = new List<MarketDepthLevel>();
            List<MarketDepthLevel> bids = new List<MarketDepthLevel>();
            Dictionary<decimal, decimal> mdLevels = new Dictionary<decimal, decimal>();

            marketDepth.SecurityNameCode = securityName;

            List<OrderChange> asksByPrice = orderChanges.FindAll(p => p.OrderType == "ask");

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

            Dictionary<decimal, decimal>.Enumerator levelAsk = mdLevels.GetEnumerator();

            while (levelAsk.MoveNext())
            {
                asks.Add(new MarketDepthLevel()
                {
                    Price = Convert.ToDouble(levelAsk.Current.Key),
                    Ask = Convert.ToDouble(levelAsk.Current.Value)
                });
            }

            List<OrderChange> bidsByPrice = orderChanges.FindAll(p => p.OrderType == "bid");

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

            Dictionary<decimal, decimal>.Enumerator levelBid = mdLevels.GetEnumerator();

            while (levelBid.MoveNext())
            {
                bids.Add(new MarketDepthLevel()
                {
                    Price = Convert.ToDouble(levelBid.Current.Key),
                    Bid = Convert.ToDouble(levelBid.Current.Value)
                });
            }

            marketDepth.Asks = asks;
            marketDepth.Bids = bids;

            marketDepth.Time = DateTime.UtcNow;

            if (_lastMdTime != DateTime.MinValue && _lastMdTime >= marketDepth.Time)
            {
                marketDepth.Time = _lastMdTime.AddTicks(1);
            }

            _lastMdTime = marketDepth.Time;

            MarketDepthEvent(marketDepth);
        }


        // Внести изменения в данные по заявкам, находящимся в стакане
        private void UpdateOrderData(string uniqueName, OrderChange orderChange)
        {
            OrderChange order = _depthChanges[uniqueName].Find(p => p.MDEntryID == orderChange.MDEntryID);

            if (order != null)
            {
                switch (orderChange.Action)
                {
                    case "add":
                        order.Volume += orderChange.Volume;
                        break;

                    case "change":
                        order.Volume = orderChange.Volume;
                        break;

                    case "delete":
                        int index = _depthChanges[uniqueName].FindIndex(p => p.MDEntryID == orderChange.MDEntryID);
                        if (index != -1)
                            _depthChanges[uniqueName].RemoveAt(index);
                        else
                            SendLogMessage($"Change index for delete order not found", LogMessageType.Error);
                        break;

                    default:
                        SendLogMessage($"Action for change order not found", LogMessageType.Error);
                        break;
                }
            }
            else
            {
                _depthChanges[uniqueName].Add(orderChange);
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

            return marketDepth;
        }

        // Обработка FIX сообщений
        private void FixMessageReader()
        {
            Dictionary<string, string> fixMsgValues = new Dictionary<string, string>();

            try
            {
                byte[] dataAns = new byte[4096];

                int bytesRead = _fxMFIXTradeStream.Read(dataAns, 0, dataAns.Length);

                string fixMessageFromMOEX = Encoding.UTF8.GetString(dataAns);

                WriteLogFXMFIXTrade("Received: " + fixMessageFromMOEX);

                string[] tagsValues = fixMessageFromMOEX.Split(new char[] { '\u0001' });

                for (int i = 0; i < tagsValues.Length; i++)
                {
                    int index = tagsValues[i].IndexOf("=");
                    if (index != -1)
                    {
                        string tag = tagsValues[i].Substring(0, index);
                        string value = tagsValues[i].Substring(index + 1);

                        if (!fixMsgValues.ContainsKey(tag))
                        {
                            fixMsgValues.Add(tag, value);
                        }
                    }
                }

                long newFixMsgNum = long.Parse(fixMsgValues["34"]);

                _timeOfTheLastMFIXMessage = DateTime.Now;

                // проверка очередности входящих сообщений
                if (newFixMsgNum == _incomingFixMsgNum)
                {
                    _incomingFixMsgNum++;
                }

                string typeMsg = fixMsgValues["35"];

                switch (typeMsg)
                {
                    case "A": // Logon

                        if (newFixMsgNum > _incomingFixMsgNum)
                        {
                            string resendMsg = _fxMFIXTradeMessages.ResendMessage(_msgSeqNum, _incomingFixMsgNum, newFixMsgNum - 1);
                            SendFXMFIXTradeMessage(resendMsg);
                            SendLogMessage($" Входящий номер больше чем ожидалось. Resend Request has been sent", LogMessageType.System);
                        }
                        else
                        {
                            _incomingFixMsgNum++;
                        }

                        string textLogon = string.Empty;

                        if (fixMsgValues.TryGetValue("58", out textLogon))
                        {

                        }

                        SendLogMessage("FIX connection open " + textLogon, LogMessageType.System);
                        _fxMFIXTradeConnectionSuccessful = true;

                        string sessionStatus = string.Empty;

                        if (fixMsgValues.TryGetValue("1409", out sessionStatus))
                        {
                            string statusMsg = sessionStatus == "3" ? "New password is not secure" : "Session is active";
                            SendLogMessage($"{statusMsg} {fixMsgValues["58"]}", LogMessageType.System);
                        }
                        break;

                    case "0": // Hearbeat

                        SendLogMessage($"Получен Hearbeat: вх. {newFixMsgNum}, исх.{_msgSeqNum}", LogMessageType.System);

                        _isThereFirstHearbeat = true;

                        string hrbtMsg = _fxMFIXTradeMessages.HeartbeatMessage(_msgSeqNum, false, null);

                        SendFXMFIXTradeMessage(hrbtMsg);


                        break;

                    case "1": // Test Request

                        string testReqId = string.Empty;

                        if (fixMsgValues.TryGetValue("112", out testReqId))
                        {
                            string hrbtMsgForTest = _fxMFIXTradeMessages.HeartbeatMessage(_msgSeqNum, true, testReqId);
                            SendFXMFIXTradeMessage(hrbtMsgForTest);
                        }

                        break;

                    case "5": // Logout

                        if (!_logoutInitiator) //если инициатор logout был сервер
                        {
                            string logout = _fxMFIXTradeMessages.LogoutMessage(_msgSeqNum);
                            SendFXMFIXTradeMessage(logout);
                        }
                        SendLogMessage("Logout received", LogMessageType.System);
                        _fxMFIXTradeConnectionSuccessful = false;
                        _isThereFirstHearbeat = false;
                        _fxMFIXTradeTcpClient = null;
                        _fxMFIXTradeStream = null;

                        break;

                    case "3":   //RejectMessage
                                // Указывает на неверно переданное или недопустимое сообщение сессионного уровня, пришедшее от противоположной стороны.
                        RejectMessage rejectMessage = new RejectMessage();

                        string reason;

                        if (rejectMessage.sessionRejectReason.TryGetValue(fixMsgValues["371"], out reason))
                        {
                            SendLogMessage($"The message Reject has been received. RefTagID: {fixMsgValues["371"]}, RefMsgType: {fixMsgValues["372"]}, Reson: {reason}. {fixMsgValues["58"]}", LogMessageType.Error);
                        }
                        else
                        {
                            SendLogMessage(fixMessageFromMOEX, LogMessageType.System);
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

                        string Text = fixMsgValues["58"];

                        SendLogMessage($"MFIX sent order cancel reject: {Text}. Order user id: {OrigClOrdID}", LogMessageType.Error);
                        break;

                    case "r":   //OrderMassCancelReport
                                // используется для подтверждения получения или отклонение некорректного массового запроса на снятие ранее размещенных заявок
                        string MassCancelRequestType = fixMsgValues["530"] == "1" ? "Cancel by security id" : "Cancel all orders";

                        string MassCancelResponse = fixMsgValues["531"];

                        string MassCancelRejectReason = MassCancelResponse == "0" ? fixMsgValues["532"] : "";

                        string text = fixMsgValues["58"] ?? "";

                        if (MassCancelResponse == "0")
                        {
                            SendLogMessage($"MFIX rejected order mass cancel with report: Text={text}. {MassCancelRequestType}, MassCancelResponse={MassCancelResponse}, {MassCancelRejectReason} ", LogMessageType.Error);
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
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                Thread.Sleep(3000);
            }
        }

        private void UpdateMyOrder(Dictionary<string, string> fixValues)
        {
            string ExecType = fixValues["150"];
            string OrdStatus = fixValues["39"];

            string OrdType = fixValues["40"];
            string Price = fixValues.ContainsKey("44") ? fixValues["44"] : "0";
            string OrderQty = fixValues["38"] ?? "0";

            string LastQty = fixValues.ContainsKey("32") ? fixValues["32"] : "0";
            string LastPx = fixValues.ContainsKey("31") ? fixValues["31"] : "0";

            string TransactTime = fixValues["60"];
            DateTime TransactionTime = DateTime.ParseExact(TransactTime, "yyyyMMdd-HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            string Text = fixValues.ContainsKey("58") ? fixValues["58"] : "";
            string Symbol = fixValues["55"];

            Order order = new Order();

            order.SecurityNameCode = Symbol;
            order.SecurityClassCode = fixValues["336"];
            order.PortfolioNumber = fixValues["1"];
            order.NumberMarket = fixValues["37"];
            order.Comment = Text;

            if (ExecType == "F") // сделка
            {
                order.Volume = LastQty.ToDecimal();
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
                    IDsModifiedOrder modifiedOrder = _modifiedOrders.Find(o => o.NewNumUser == fixValues["41"]);
                    modifiedOrder.NumMarket = newClOrdId;
                    modifiedOrder.NewNumUser = fixValues["11"];

                    order.NumberUser = Convert.ToInt32(modifiedOrder.NumUserInSystem);
                }
                else  // в списке измененных ордеров нет информации о новом измененном ордере
                {
                    _modifiedOrders.Add(new IDsModifiedOrder() { NumUserInSystem = origClordId, NumMarket = newClOrdId, NewNumUser = ClOrdId });

                    order.NumberUser = Convert.ToInt32(origClordId);
                }
            }

            if (int.TryParse(fixValues["11"], out order.NumberUser))
            {

            }
            else
            {
                // Преобразование не удалось, значит пришел длинный пользовательский номер, установленный биржей измененному или ордеру
                IDsModifiedOrder modifiedOrder = _modifiedOrders.Find(o => o.NumMarket == order.NumberMarket);
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

            if (OrdStatus == "0" || OrdStatus == "9" || OrdStatus == "E")
            {
                order.State = OrderStateType.Active;
            }
            else if (OrdStatus == "1")
            {
                order.State = OrderStateType.Partial;
            }
            else if (OrdStatus == "2")
            {
                order.State = OrderStateType.Done;
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
                }
            }
            else if (OrdStatus == "8") //Отклонена
            {
                order.State = OrderStateType.Fail;

                SendLogMessage($"MFIX sent order status 'fail' with comment: {order.Comment}", LogMessageType.Error);
            }

            if (ExecType == "F")  // MyTrades
            {
                string tradeId = fixValues["17"].Split('|')[0];

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

            string ordReport = $"\n----------------------------\nExecType: {ExecType}\n" +
                $"SecurityNameCode: {order.SecurityNameCode}\n" +
                $"OrdStatus: {OrdStatus}\n" +
                  $"OrderState: {order.State}\n" +
                $"OrdType: {OrdType}\n" +
                $"Side: {order.Side}\n" +
                $"Price: {Price}\n" +
                 $"Volume: {order.Volume}\n" +
                $"OrderQty: {OrderQty}\n" +
                $"LastQty: {LastQty}\n" +
                $"LastPx: {LastPx}\n" +
                $"TransactTime: {TransactTime}\n" +
                $"Text: {Text}\n" +
                $"SecurityClassCode: {order.SecurityClassCode}\n" +
                $"PortfolioNumber: {order.PortfolioNumber}\n" +
                $"NumberUser: {order.NumberUser}\n" +
                $"NumberMarket: {order.NumberMarket}\n" +
                $"Comment: {order.Comment}\n----------------------------";

            WriteLogFXMFIXTrade("Получен отчет по ордеру: " + ordReport);

            MyOrderEvent(order);
        }

        /// Восстановление данных по TCP
        private void RecoveryData()
        {
            byte[] buffer = new byte[4096];
            byte[] sizeBuffer = new byte[4];

            string currentFeed = "OLR";
            string messageFromMOEX = string.Empty;

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
                    if (_missingOLRBeginSeqNo > 0 && _missingOLRData && _missingOLRRptSeqNums.Count > 4)
                    {
                        currentFeed = "OLR"; // восстанавливаем данные из потока ордеров
                        SendLogMessage($"Trying to recover missing OLR SeqNo: {_missingOLRBeginSeqNo}-{_missingOLREndSeqNo}", LogMessageType.System);

                    }
                    else
                    if (_missingTLRBeginSeqNo > 0 && _missingTLRData && _missingTLRRptSeqNums.Count > 4)
                    {
                        currentFeed = "TLR"; // восстанавливаем данные из потока сделок
                        SendLogMessage($"Trying to recover missing TLR SeqNo: {_missingTLRBeginSeqNo}-{_missingTLREndSeqNo}", LogMessageType.System);
                    }
                    else
                    {
                        Thread.Sleep(500);
                        continue;
                    }

                    lock (_socketLockerHistoricalReplay)
                    {
                        _historicalReplaySocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                        int msgSeqNum = 1;

                        _historicalReplaySocket.Connect(_historicalReplayEndPoint);

                        MessageConstructor recoveryMessages = new MessageConstructor("OsEngineCurr", "MOEX");
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

                        long ApplBegSeqNum = currentFeed == "OLR" ? _missingOLRBeginSeqNo : _missingTLRBeginSeqNo;
                        long ApplEndSeqNum = currentFeed == "OLR" ? _missingOLREndSeqNo : _missingTLREndSeqNo;

                        if (ApplEndSeqNum - ApplBegSeqNum >= 2000)
                        {
                            ApplEndSeqNum = ApplBegSeqNum + 2000 - 1;
                        }

                        string MDRequestMsg = recoveryMessages.MarketDataRequestMessage(msgSeqNum, currentFeed, ApplBegSeqNum, ApplEndSeqNum);
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

                                    if (msg.IsDefined("GroupMDEntries"))
                                    {
                                        SequenceValue secVal = msg.GetValue("GroupMDEntries") as SequenceValue;

                                        for (int i = 0; i < secVal.Length; i++)
                                        {
                                            GroupValue groupVal = secVal[i] as GroupValue;

                                            int RptSeq = groupVal.GetInt("RptSeq");
                                            string name = groupVal.GetString("Symbol");

                                            if (currentFeed == "OLR" && _missingOLRRptSeqNums.Contains(RptSeq))
                                            {
                                                _orderMessages.Enqueue(msg);
                                                _ordersIncremental[MsgSeqNum] = msg;

                                                WriteLogRecovery($"Обработано по {name} сообщение OLR: {RptSeq}");
                                            }

                                            if (currentFeed == "TLR" && _missingTLRRptSeqNums.Contains(RptSeq))
                                            {
                                                _tradeMessages.Enqueue(msg);
                                                _tradesIncremental[MsgSeqNum] = msg;

                                                WriteLogRecovery($"Обработано по {name} сообщение TLR: {RptSeq}");
                                            }
                                        }
                                    }
                                }
                                else if (messageType == "5") // Logout
                                {
                                    SendLogMessage($"TCP recovery received MessageType: {messageType} for {currentFeed}", LogMessageType.System);
                                    SendLogMessage($"Historical Replay server Logout. {msg.GetString("Text")}", LogMessageType.System);

                                    //отвечаем серверу Logout
                                    string MDLogoutMsg = recoveryMessages.LogoutFASTMessage(msgSeqNum);
                                    _historicalReplaySocket.Send(Encoding.UTF8.GetBytes(MDLogoutMsg));

                                    if (currentFeed == "OLR")
                                    {
                                        _missingOLRBeginSeqNo = 0;
                                        _missingOLREndSeqNo = 0;
                                        _missingOLRData = false;

                                        WriteLogRecovery($"Прием сообщений OLR закончен.");

                                    }
                                    else
                                    {
                                        _missingTLRBeginSeqNo = 0;
                                        _missingTLREndSeqNo = 0;
                                        _missingTLRData = false;

                                        WriteLogRecovery("Прием сообщений TLR закончен.");
                                    }

                                    // закрываем сокет
                                    _historicalReplaySocket.Close();
                                    _historicalReplaySocket = null;

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
        List<IDsModifiedOrder> _modifiedOrders = new List<IDsModifiedOrder>();

        public void SendOrder(Order order)
        {
            _rateGateForOrders.WaitToProceed();

            try
            {
                string ordType = order.TypeOrder == OrderPriceType.Market ? "1" : "2";
                string price = order.TypeOrder == OrderPriceType.Market ? null : order.Price.ToString().Replace(",", ".");

                string newOrder = _fxMFIXTradeMessages.NewOrderMessage
                            (
                                order.NumberUser.ToString(),
                                new string[] { "1", _MFIXTradeClientCode, "D", "1" },   // группа Parties
                                order.SecurityNameCode,
                                order.Volume.ToString().Replace(",", "."),
                                order.PortfolioNumber,
                                null,
                                false,
                                order.SecurityClassCode,
                                ((byte)order.Side).ToString(),
                                ordType,
                                price,
                                _msgSeqNum
                            );

                SendFXMFIXTradeMessage(newOrder);
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
                string origClOrdID = order.NumberUser.ToString();
                // проверяем менялся ли этот ордер
                if (_modifiedOrders.Exists(o => o.NumUserInSystem == order.NumberUser.ToString()))
                {
                    origClOrdID = _modifiedOrders.Find(o => o.NumUserInSystem == order.NumberUser.ToString()).NewNumUser;
                }

                string orderID = order.NumberMarket.ToString();
                string clOrdID = DateTime.UtcNow.Ticks.ToString();
                string side = order.Side == Side.Buy ? "1" : "2";

                string orderDelMsg = _fxMFIXTradeMessages.OrderCanselMessage(origClOrdID, orderID, clOrdID, side, _msgSeqNum);
                SendFXMFIXTradeMessage(orderDelMsg);
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
                string tradingSessionID = order.SecurityClassCode;
                string side = order.Side == Side.Buy ? "1" : "2";
                string ordType = order.TypeOrder == OrderPriceType.Market ? "1" : "2"; // 1 - Market, 2 - Limit
                string orderQty = order.Volume.ToString();
                string price = order.TypeOrder == OrderPriceType.Limit ? newPrice.ToString().Replace(',', '.') : "0";

                string changeOrderMsg = _fxMFIXTradeMessages.OrderReplaceMessage
                    (
                        clOrdID,
                        origClOrdID,
                        orderID,
                        _MFIXTradeAccount,
                         new string[] { "1", _MFIXTradeClientCode, "D", "1" },   // группа Parties
                        order.SecurityNameCode,
                        price,
                        orderQty,
                        side,
                        tradingSessionID,
                        ordType,
                        _msgSeqNum
                    );

                SendFXMFIXTradeMessage(changeOrderMsg);

            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

        }

        public void CancelAllOrders()
        {
            _rateGateForOrders.WaitToProceed();

            try
            {
                string clOrdID = DateTime.UtcNow.Ticks.ToString(); // идентификатор заявки на снятие
                string account = _MFIXTradeAccount;
                string orderMassCancelMsg = _fxMFIXTradeMessages.OrderMassCanselMessage(clOrdID, "7", null, null, account, _msgSeqNum);

                SendFXMFIXTradeMessage(orderMassCancelMsg);
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
                string clOrdID = DateTime.UtcNow.Ticks.ToString(); // идентификатор заявки на снятие
                string tradingSessionID = security.NameClass;
                string symbol = security.NameId;
                string account = _MFIXTradeAccount;
                string orderMassCancelMsg = _fxMFIXTradeMessages.OrderMassCanselMessage(clOrdID, "1", tradingSessionID, symbol, account, _msgSeqNum);

                SendFXMFIXTradeMessage(orderMassCancelMsg);
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

            using (FileStream stream = File.OpenRead(_configDir + "\\template.xml"))
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
            SequenceValue secVal = msg.GetValue("GroupMDEntries") as SequenceValue;

            string nameForCheck = string.Empty;

            for (int i = 0; i < secVal.Length; i++)
            {
                GroupValue groupVal = secVal[i] as GroupValue;

                string name = groupVal.GetString("Symbol");
                string TradingSessionID = groupVal.GetString("TradingSessionID");
                string uniqueName = name + TradingSessionID;

                if (IsSubscribedToThisSecurity(uniqueName))
                {
                    if (name != nameForCheck) // чтобы не отправлять повторно сообщение, содержащее более 1 трейда по одному инструменту
                    {
                        nameForCheck = name;

                        fastMessagesQueue.Enqueue(msg);
                    }
                }
            }
        }

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

        private bool IsDataMissed(List<NumbersData> nums, out long beginMsgSeqNum, out long endMsgSeqNum,
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

        private bool IsSubscribedToThisSecurity(string uniqueName)
        {
            for (int j = 0; j < _subscribedSecurities.Count; j++)
            {
                if (_subscribedSecurities[j] == uniqueName)
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

        public void SetLeverage(Security security, decimal leverage) { }

        #endregion

        #region 11 Log

        private string _logLockTrade = "locker for trades stream ";
        private StreamWriter _logFileTrades = new StreamWriter($"Engine\\Log\\FAST_Trades_{DateTime.Now:dd-MM-yyyy}.txt");

        private string _logLockOrder = "locker for orders stream";
        private StreamWriter _logFileOrders = new StreamWriter($"Engine\\Log\\FAST_Orders_{DateTime.Now:dd-MM-yyyy}.txt");

        private string _logLockMFIX = "locker for incoming FIX";
        private StreamWriter _logFXMFIXMsg = new StreamWriter($"Engine\\Log\\IncomingMFIX_{DateTime.Now:dd-MM-yyyy}.txt", true);

        private string _logLockRecover = "locker for Recover";
        private StreamWriter _logFileRecover = new StreamWriter($"Engine\\Log\\DataRecoveryLog_{DateTime.Now:dd-MM-yyyy}.txt");

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

        private void WriteLogFXMFIXTrade(string fixMessage)
        {
            lock (_logLockMFIX)
            {
                _logFXMFIXMsg.WriteLine($"{DateTime.Now}: {fixMessage}");
            }
        }

        private void WriteLogRecovery(string message)
        {
            lock (_logLockRecover)
            {
                _logFileRecover.WriteLine($"{DateTime.Now}: {message}");
            }
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