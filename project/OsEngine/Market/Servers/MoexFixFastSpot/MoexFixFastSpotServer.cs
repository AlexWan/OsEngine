/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.MoexFixFastSpot.FIX;
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

namespace OsEngine.Market.Servers.MoexFixFastSpot
{
    public class MoexFixFastSpotServer : AServer
    {
        public MoexFixFastSpotServer()
        {
            MoexFixFastSpotServerRealization realization = new MoexFixFastSpotServerRealization();
            ServerRealization = realization;

            // MFIX
            CreateParameterString("MFIX Trade Server Address", "");
            CreateParameterString("MFIX Trade Server Port", "");
            CreateParameterString("MFIX Trade Server TargetCompId", "");
            CreateParameterString("MFIX Trade Server Login", "");
            CreateParameterPassword("MFIX Trade Server Password", "");
            CreateParameterString("MFIX Trade Account", "");
            CreateParameterString("MFIX Trade Client Code", "");
            
            // FAST
            CreateParameterPath("FIX/FAST Multicast Config Directory");

            CreateParameterInt("Order actions limit for login (per second)", 30);

            // new passwords
            CreateParameterPassword("NEW MFIX Trade Server Password", "");
            CreateParameterString("FIX Tag 11 separator", "//");
            CreateParameterBoolean("FIX Tag 11 contains order IDs", false);
        }
    }

    public class MoexFixFastSpotServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public MoexFixFastSpotServerRealization()
        {            
            Thread worker0 = new Thread(ConnectionCheckThread);
            worker0.Name = "ConnectionCheckerMoexFixFastSpot";
            worker0.Start();

            Thread worker1 = new Thread(InstrumentDefinitionsReader);
            worker1.Name = "InstrumentsMoexFixFastSpot";
            worker1.Start();

            Thread worker2 = new Thread(TradesIncrementalReader);
            worker2.Name = "TradesIncremenalMoexFixFastSpot";
            worker2.Start();

            Thread worker3 = new Thread(TradesSnapshotsReader);
            worker3.Name = "TradesSnapshotsMoexFixFastSpot";
            worker3.Start();

            Thread worker4 = new Thread(TradeMessagesReader);
            worker4.Name = "TradeMessagesReaderMoexFixFastSpot";
            worker4.Start();

            Thread worker5 = new Thread(OrderMessagesReader);
            worker5.Name = "OrderMessagesReaderMoexFixFastSpot";
            worker5.Start();

            Thread worker6 = new Thread(OrdersIncrementalReaderA);
            worker6.Name = "OrdersIncremenalAMoexFixFastSpot";
            worker6.Start();

            Thread worker7 = new Thread(OrdersIncrementalReaderB);
            worker7.Name = "OrdersIncremenalBMoexFixFastSpot";
            worker7.Start();

            Thread worker8 = new Thread(OrderSnapshotsReader);
            worker8.Name = "OrdersSnapshotsMoexFixFastSpot";
            worker8.Start();

            Thread worker9 = new Thread(MFIXTradeServerConnection);
            worker9.Name = "MFIXTradeServerConnectionMoexFixFastSpot";
            worker9.Start();                       

            Thread worker10 = new Thread(HistoricalReplayThread);
            worker10.Name = "HistoricalReplayMoexFixFastSpot";
            worker10.Start();            
        }

        public void Connect(WebProxy proxy)
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
                
                _MFIXTradeServerLogin = ((ServerParameterString)ServerParameters[3]).Value;
                _MFIXTradeServerPassword = ((ServerParameterPassword)ServerParameters[4]).Value;
                                
                _MFIXTradeAccount = ((ServerParameterString)ServerParameters[5]).Value;         
                _MFIXTradeClientCode = ((ServerParameterString)ServerParameters[6]).Value;

                _ConfigDir = ((ServerParameterPath)ServerParameters[7]).Value;
                              
                int orderActionsLimit = ((ServerParameterInt)ServerParameters[8]).Value;

                _rateGateForOrders = new RateGate(orderActionsLimit, TimeSpan.FromSeconds(1));

                _MFIXTradeServerNewPassword = ((ServerParameterPassword)ServerParameters[9]).Value;
                _MFIXTag11Separator = ((ServerParameterString)ServerParameters[10]).Value;
                _MFIXTag11ContainsOrderIDs = ((ServerParameterBool)ServerParameters[11]).Value;

                if (_MFIXTradeServerNewPassword == _MFIXTradeServerPassword) // if already changed password
                {
                    ((ServerParameterPassword)ServerParameters[9]).Value = "";
                    _MFIXTradeServerNewPassword = "";
                }                               

                if (string.IsNullOrEmpty(_MFIXTradeServerAddress) || string.IsNullOrEmpty(_MFIXTradeServerPort) || string.IsNullOrEmpty(_MFIXTradeServerTargetCompId))
                {
                    SendLogMessage("Connection terminated. You must specify FIX Trade Server Credentials", LogMessageType.Error);
                    return;
                }
                
                if (string.IsNullOrEmpty(_MFIXTradeServerLogin) || string.IsNullOrEmpty(_MFIXTradeServerPassword))
                {
                    SendLogMessage("Connection terminated. You must specify FIX Trade server login/password", LogMessageType.Error);
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
                SendLogMessage("Connection cannot be open to MOEX FIX/FAST servers. Error request", LogMessageType.Error);
                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
        }

        private void ConnectionCheckThread()
        {
            DateTime lastMFIXTradeTestRequestTime = DateTime.MinValue;            

            while (true)
            {
                Thread.Sleep(10000);

                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    continue;
                }

                // MFIX servers
                if (_lastMFIXTradeTime.AddMinutes(1) < DateTime.Now)
                {
                    if (lastMFIXTradeTestRequestTime > _lastMFIXTradeTime) // already send test request
                    {
                        if (ServerStatus != ServerConnectStatus.Disconnect)
                        {
                            SendLogMessage("MFIX Trade server connection lost. No response for too long", LogMessageType.Error);
                            ServerStatus = ServerConnectStatus.Disconnect;
                            DisconnectEvent();
                        }
                    } else
                    {  // 1. send Test Request (1)
                        Header header = new Header();
                        header.MsgType = "1"; // Test Request
                        header.SenderCompID = _MFIXTradeServerLogin;
                        header.TargetCompID = _MFIXTradeServerTargetCompId;
                        header.MsgSeqNum = _MFIXTradeMsgSeqNum++;
                                                
                        TestRequestMessage msg = new TestRequestMessage();
                        msg.TestReqID = "OsEngine";

                        string message = SendFIXMessage(_MFIXTradeSocket, header, msg);
                        WriteLogOutgoingFIXMessage(message);

                        lastMFIXTradeTestRequestTime = DateTime.Now;
                    }
                }               

                // FIX/FAST Multicast server
                if (_lastFASTMulticastTime.AddMinutes(2) < DateTime.Now)
                {
                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        SendLogMessage("MOEX FIX/FAST Multicast server connection lost. No data over UDP for too long.", LogMessageType.Error);
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                    }
                }
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
        
        public void Dispose()
        {
            _securities.Clear();
            _myPortfolios.Clear();      
            DeleteWebSocketConnection();

            SendLogMessage("Connection Closed by MoexFixFastSpot. Socket Data Closed Event", LogMessageType.System);

            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        public DateTime ServerTime { get; set; }

        public ServerType ServerType => ServerType.MoexFixFastSpot;

        public ServerConnectStatus ServerStatus { get; set; } = ServerConnectStatus.Disconnect;

        public List<IServerParameter> ServerParameters { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

        #endregion

        #region 2 Properties

        private string _MFIXTradeServerAddress;
        private string _MFIXTradeServerPort;
        private string _MFIXTradeServerTargetCompId;
        private string _MFIXTradeServerLogin;
        private string _MFIXTradeServerPassword;
        private string _MFIXTradeAccount; // trade account
        private string _MFIXTradeClientCode; // client code

        private string _ConfigDir;

        private string _MFIXTradeServerNewPassword;
        private string _MFIXTag11Separator;
        private bool _MFIXTag11ContainsOrderIDs;

        private MessageTemplate[] _templates;
        
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
        private long _missingOLRBeginSeqNo = -1;
        private long _missingOLREndSeqNo = 0;
        private bool _missingOLRData = false;
        private long _missingTLRBeginSeqNo = -1;
        private long _missingTLREndSeqNo = 0;
        private bool _missingTLRData = false;
       
        private Socket _MFIXTradeSocket;
        private long _MFIXTradeMsgSeqNum = 1;
        private long _MFIXTradeMsgSeqNumIncoming = 1;
        
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
            Portfolio newPortfolio = new Portfolio(); // we've got only one portfolio by default
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

        #region 6 Socket creation

        private string _socketLockerHistoricalReplay = "socketLockerMoexFixFastSpotHistoricalReplay";
        private string _socketLockerInstruments = "socketLockerMoexFixFastSpotInstruments";
        private string _socketLockerTradesSnapshots = "socketLockerMoexFixFastSpotTradeSnapshots";
        private string _socketLockerTradesIncremental = "socketLockerMoexFixFastSpotTradesIncremental";
        private string _socketLockerOrdersSnapshots = "socketLockerMoexFixFastSpotOrdersSnapshots";
        private string _socketLockerOrdersIncremental = "socketLockerMoexFixFastSpotOrdersIncremental";
        
        private void CreateSocketConnections()
        {
            _lastFASTMulticastTime = DateTime.Now;

            try
            {                
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
                for (int i = 0; i < connectionNodes.Count; i++)
                {
                    XmlNode connectionNode = connectionNodes[i];
                    string connectionId = connectionNode.Attributes["id"].Value;

                    XmlNode typeNode = connectionNode.SelectSingleNode("type");
                    string feedType = typeNode.Attributes["feed-type"].Value;

                    if (feedType == "Statistics Incremental" || feedType == "Statistics Snapshot" || feedType == "Instrument Status")
                    {
                        // (пока) не используем потоки статистики и статуса инструментов
                        continue;
                    }

                    if (feedType == "Historical Replay")
                    {                        
                        string ipAddressString = connectionNode.SelectNodes("ip")[0].InnerText; // берем второй адрес
                        string portString = connectionNode.SelectSingleNode("port").InnerText;
                        
                        
                        IPAddress ipAddr = IPAddress.Parse(ipAddressString);
                        
                        _historicalReplayEndPoint = new IPEndPoint(ipAddr, int.Parse(portString));
                                              

                        continue;
                    }

                    XmlNodeList feedNodes = connectionNode.SelectNodes("feed");

                    for (int j = 0; j < feedNodes.Count; j++)
                    {
                        XmlNode feedNode = feedNodes[j];

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
                if (EstablishMFIXTradeConnection() == false)
                {
                    SendLogMessage("Failed to establish connection. Check settings and/or network status", LogMessageType.Error);
                    return;
                }

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
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private string SendFIXMessage(Socket socket, AFIXHeader header, AFIXMessageBody messageBody)
        {
            header.BodyLength = header.GetHeaderSize() + messageBody.GetMessageSize();
            
            //Создаем концовку сообщения
            Trailer trailer = new Trailer(header.ToString() + messageBody.ToString());
            
            //Формируем полное готовое сообщение
            string fullFIXMessage = header.ToString() + messageBody.ToString() + trailer.ToString();

            try
            {
                socket.Send(Encoding.UTF8.GetBytes(fullFIXMessage));
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

            return fullFIXMessage;
        }

        private bool EstablishMFIXTradeConnection()
        {
            // 1. Создаем и отправляем запрос на подключение (Logon)

            //Создаем заголовк
            Header tradeServerLogonHeader = new Header();
            tradeServerLogonHeader.MsgType = "A"; //Тип сообщения на установку сессии
            tradeServerLogonHeader.SenderCompID = _MFIXTradeServerLogin;
            tradeServerLogonHeader.TargetCompID = _MFIXTradeServerTargetCompId;
            tradeServerLogonHeader.MsgSeqNum = _MFIXTradeMsgSeqNum++;
                       
            //Создаем сообщение на подключение onLogon
            LogonMessage logonTServerMessageBody = new LogonMessage();
            logonTServerMessageBody.EncryptMethod = 0;
            logonTServerMessageBody.HeartBtInt = 30;
            logonTServerMessageBody.ResetSeqNumFlag = tradeServerLogonHeader.MsgSeqNum == 1;
            logonTServerMessageBody.Password = _MFIXTradeServerPassword;
            logonTServerMessageBody.NewPassword = _MFIXTradeServerNewPassword;           

            // 2. Создаем сокет и подключаемся
            // MFIX Trade Server
            IPAddress ipAddr = IPAddress.Parse(_MFIXTradeServerAddress);
            IPEndPoint ipEndPoint = new IPEndPoint(ipAddr, int.Parse(_MFIXTradeServerPort));

            //Создаем сокет для подключения
            _MFIXTradeSocket = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            //Подключаемся
            _MFIXTradeSocket.Connect(ipEndPoint);
                                 
            //Отправляем сообщение
            string tServerLogonMessage = SendFIXMessage(_MFIXTradeSocket, tradeServerLogonHeader, logonTServerMessageBody);
            WriteLogOutgoingFIXMessage(tServerLogonMessage);
            SendLogMessage($"Connecting to MFIX Trade server with MsgSeqNum={tradeServerLogonHeader.MsgSeqNum}", LogMessageType.System);

            bool tradeServerConnected = false;

            //Получаем ответ от сервера
            byte[] bytes = new byte[4096];
            int bytesRec = 0;

            DateTime startTime = DateTime.Now;

            while (tradeServerConnected == false)
            {
                if (startTime.AddMinutes(1) < DateTime.Now)
                {
                    SendLogMessage("Error logging on to MOEX MFIX. No response from server.", LogMessageType.Error);
                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                    }

                    return false;
                }

                bytesRec = 0;
                if (tradeServerConnected == false)
                {
                    try
                    {
                        bytesRec = _MFIXTradeSocket.Receive(bytes);
                    } catch (Exception ex)
                    {
                        SendLogMessage("Error receiving FIX Message " + ex.ToString(), LogMessageType.Error);
                        if (ServerStatus != ServerConnectStatus.Disconnect)
                        {
                            ServerStatus = ServerConnectStatus.Disconnect;
                            DisconnectEvent();
                        }
                    }

                    if (bytesRec > 0)
                    {
                        string serverMessage = Encoding.UTF8.GetString(bytes, 0, bytesRec);
                        WriteLogIncomingFIXMessage(serverMessage);

                        FIXMessage fixMessage = FIXMessage.ParseFIXMessage(serverMessage);

                        string Text = "";
                        if (fixMessage.Fields.ContainsKey("Text"))
                        {
                            Text = fixMessage.Fields["Text"];
                        }

                        if (fixMessage.MessageType == "Logon")
                        {
                            if (fixMessage.MsgSeqNum > _MFIXTradeMsgSeqNumIncoming)
                            {
                                // need to make Resend Request
                                Header resendRequestHeader = new Header();
                                resendRequestHeader.MsgType = "2"; //Resend Request
                                resendRequestHeader.SenderCompID = _MFIXTradeServerLogin;
                                resendRequestHeader.TargetCompID = _MFIXTradeServerTargetCompId;
                                resendRequestHeader.MsgSeqNum = _MFIXTradeMsgSeqNum++;

                                ResendRequestMessage resendRequestMessageBody = new ResendRequestMessage();
                                resendRequestMessageBody.BeginSeqNo = _MFIXTradeMsgSeqNumIncoming;
                                resendRequestMessageBody.EndSeqNo = fixMessage.MsgSeqNum - 1;

                                string resendRequestMessage = SendFIXMessage(_MFIXTradeSocket, resendRequestHeader, resendRequestMessageBody);
                                WriteLogOutgoingFIXMessage(resendRequestMessage);
                                SendLogMessage($"Making Resend Request to MFIX Trade server", LogMessageType.System);
                            }
                            else
                            {
                                _MFIXTradeMsgSeqNumIncoming++;
                            }

                            tradeServerConnected = true;
                            SendLogMessage("MFIX Trade server connected", LogMessageType.System);

                            if (fixMessage.Fields.ContainsKey("SessionStatus"))
                            {
                                int SessionStatus = int.Parse(fixMessage.Fields["SessionStatus"]);

                                if (SessionStatus == 0) // set new password
                                {
                                    SendLogMessage($"New password was set successfully for MFIX Trade server for login {_MFIXTradeServerLogin}. {Text}", LogMessageType.System);
                                }
                                else if (SessionStatus == 3) // new password not set
                                {
                                    SendLogMessage($"Failed to set new password for MFIX Trade server for login {_MFIXTradeServerLogin}. {Text}", LogMessageType.Error);
                                }
                            }
                        }

                        if (fixMessage.MessageType == "Logout")
                        {
                            // Подключение поддержки Windows-1251 (если не сделано глобально)
                            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                            Encoding russianEncoding = Encoding.GetEncoding(1251);
                            string serverMessageRussian = russianEncoding.GetString(bytes, 0, bytesRec);

                            SendLogMessage($"Failed to Logon to MFIX Trade server {_MFIXTradeServerLogin}. {Text}. Russian version: {serverMessageRussian}", LogMessageType.Error);
                        }
                    }
                }                                

                Thread.Sleep(100);
            }

            return true;
        }

        private void CloseMFIXTradeConnection()
        {
            if (_MFIXTradeSocket == null)
            {
                return; // сокет еще не создан
            }

            // Создаем и отправляем запрос на отключение (Logout)

            //Создаем заголовк
            Header tradeServerLogoutHeader = new Header();
            tradeServerLogoutHeader.MsgType = "5"; //Тип сообщения Logout
            tradeServerLogoutHeader.SenderCompID = _MFIXTradeServerLogin;
            tradeServerLogoutHeader.TargetCompID = _MFIXTradeServerTargetCompId;
            tradeServerLogoutHeader.MsgSeqNum = _MFIXTradeMsgSeqNum++;
                       
            //Создаем сообщение Logout
            LogoutMessage logoutTServerMessageBody = new LogoutMessage();            
                       
            //Отправляем сообщение
            string logonMessageTServer = SendFIXMessage(_MFIXTradeSocket, tradeServerLogoutHeader, logoutTServerMessageBody);
            WriteLogOutgoingFIXMessage(logonMessageTServer);
            
            while (_MFIXTradeSocket != null)
            {
                Thread.Sleep(1); // ждем когда сокет закроется
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

                dir += "MoexFixFastSpotSecurities.db";

                if (File.Exists(dir) == false)
                {
                    SendLogMessage("No saved securities in file", LogMessageType.System);
                    return;
                }

                using (LiteDatabase db = new LiteDatabase(dir))
                {
                    ILiteCollection<SecurityToSave> collection = db.GetCollection<SecurityToSave>("securities");

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
                        newSecurity.Decimals = GetDecimals(newSecurity.PriceStep);
                        newSecurity.DecimalsVolume = curSec.DecimalsVolume;
                        newSecurity.State = SecurityStateType.Activ;
                        newSecurity.Exchange = "MOEX";
                        newSecurity.SecurityType = SecurityType.Stock;

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

                dir += "MoexFixFastSpotSecurities.db";

                using (LiteDatabase db = new LiteDatabase(dir))
                {
                    ILiteCollection<SecurityToSave> collection = db.GetCollection<SecurityToSave>("securities");
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

            SendLogMessage("Waiting for securities to load from IDF stream", LogMessageType.System);

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

                CloseMFIXTradeConnection();                
            }
            catch
            {

            }
            finally
            {
            }
        }                
        
        #endregion             

        #region 7 Security subscription
                
        List<Security> _subscribedSecurities = new List<Security>();
        Dictionary<string, List<OrdersUpdate>> _marketDepths = new Dictionary<string, List<OrdersUpdate>>();
        List<string> _TradingSessionIDs = new List<string>() { 
            "TQBR", // акции и ДР 
            "TQCB", // корп облигации
            "TQOB", // гос облигации
            "TQTF", // etf
            "TQIF", // паи
        };

        public void Subscribe(Security security)
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

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent { add { } remove { } }

        #endregion

        #region 8 Sockets receiving and parsing the messages

        private DateTime _lastInstrumentDefinitionsTime = DateTime.MinValue;
        private DateTime _lastMFIXTradeTime = DateTime.MinValue;
        private DateTime _lastFASTMulticastTime = DateTime.MinValue;
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

                    //if (_allSecuritiesLoaded)
                    //{
                    //    Thread.Sleep(1);
                    //    continue;
                    //}

                    if (context == null)
                    {
                        context = CreateNewContext();
                    }

                    // читаем из потоков А и B
                    // либо сразу обрабатываем либо перемещаем в очередь для разбора
                    for (int s = 0; s < 2; s++)
                    {
                        int length = 0;

                        try
                        {
                            lock (_socketLockerInstruments)
                            {
                                if (_instrumentSocketA == null || _instrumentSocketB == null)
                                {
                                    continue;
                                }

                                length = s == 0 ? _instrumentSocketA.Receive(buffer) : _instrumentSocketB.Receive(buffer);
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

                        _lastFASTMulticastTime = DateTime.Now;

                        using (MemoryStream stream = new MemoryStream(buffer, 4, length - 4))
                        {
                            FastDecoder decoder = new FastDecoder(context, stream);
                            OpenFAST.Message msg = decoder.ReadMessage();

                            string msgType = msg.GetString("MessageType");
                            long msgSeqNum = long.Parse(msg.GetString("MsgSeqNum"));

                            if (msgType == "d") /// security definition
                            {
                                _lastInstrumentDefinitionsTime = DateTime.UtcNow;
                                long newTotNumReports =  msg.GetLong("TotNumReports"); // общее число "бумаг" (возможны дубли)
                                if (newTotNumReports != _totNumReports)
                                {
                                    _totNumReports = newTotNumReports;
                                    WriteLog($"Setting TotNumReports={_totNumReports}.", "InstrumentDefinitionsReader");
                                    SendLogMessage($"Setting TotNumReports={_totNumReports}", LogMessageType.System);
                                }

                                if (_allSecuritiesLoaded)
                                {
                                   Thread.Sleep(1);
                                   continue;
                                }

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
                        context = CreateNewContext();
                    }

                    // читаем из потоков А и B
                    // либо сразу обрабатываем либо перемещаем в очередь для разбора
                    for (int s = 0; s < 2; s++)
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

                                length = s == 0 ? _tradesIncrementalSocketA.Receive(buffer) : _tradesIncrementalSocketB.Receive(buffer);
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

                        _lastFASTMulticastTime = DateTime.Now;

                        using (MemoryStream stream = new MemoryStream(buffer, 4, length - 4))
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
                        context = CreateNewContext();
                    }

                    // читаем из потоков А и B
                    // либо сразу обрабатываем либо перемещаем в очередь для разбора

                    for (int s = 0; s < 2; s++)
                    {
                        int length = 0;                        

                        try
                        {
                            lock (_socketLockerTradesSnapshots)
                            {
                                if (_tradesSnapshotSocketA == null || _tradesSnapshotSocketB == null)
                                {
                                    continue;
                                }

                                length = s == 0 ? _tradesSnapshotSocketA.Receive(buffer) : _tradesSnapshotSocketB.Receive(buffer);
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

                        _lastFASTMulticastTime = DateTime.Now;

                        using (MemoryStream stream = new MemoryStream(buffer, 4, length - 4))
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
                                    //WriteLog($"TradesSnapshot +1 msgSeqNum={msgSeqNum}. Total: " + tradesSnapshot.Count, "TradesSnapshotsReader");
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
                        context = CreateNewContext();
                    }

                    // читаем из потоков А и B
                    // либо сразу обрабатываем либо перемещаем в очередь для разбора

                    int length = 0;
                    try
                    {
                        lock (_socketLockerOrdersIncremental)
                        {
                            if (_ordersIncrementalSocketA == null)
                            {
                                continue;
                            }

                            length = _ordersIncrementalSocketA.Receive(buffer);
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
                    
                    _lastFASTMulticastTime = DateTime.Now;

                    using (MemoryStream stream = new MemoryStream(buffer, 4, length - 4))
                    {
                        FastDecoder decoder = new FastDecoder(context, stream);
                        OpenFAST.Message msg = decoder.ReadMessage();

                        long msgSeqNum = msg.GetLong("MsgSeqNum");                        

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
                    List<long> keys = null;
                    lock (_ordersIncrementalLocker)
                    {
                        keys =_ordersIncremental.Keys.ToList();
                    }

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
                        context = CreateNewContext();
                    }

                    // читаем из потоков А и B
                    // либо сразу обрабатываем либо перемещаем в очередь для разбора

                    int length = 0;
                    try 
                    {
                        lock (_socketLockerOrdersIncremental)
                        {
                            if (_ordersIncrementalSocketB == null)
                            {                            
                                continue;
                            }

                            length = _ordersIncrementalSocketB.Receive(buffer);
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

                    _lastFASTMulticastTime = DateTime.Now;

                    using (MemoryStream stream = new MemoryStream(buffer, 4, length - 4))
                    {
                        FastDecoder decoder = new FastDecoder(context, stream);
                        OpenFAST.Message msg = decoder.ReadMessage();

                        long msgSeqNum = msg.GetLong("MsgSeqNum");                        

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
                        context = CreateNewContext();
                    }

                    // читаем из потоков А и B
                    // либо сразу обрабатываем либо перемещаем в очередь для разбора

                    for (int s = 0; s < 2; s++)
                    {
                        int length = 0;                                                

                        try
                        {
                            lock (_socketLockerOrdersSnapshots)
                            {
                                if (_ordersSnapshotSocketA == null || _ordersSnapshotSocketB == null)
                                {
                                    continue;
                                }

                                length = s == 0 ? _ordersSnapshotSocketA.Receive(buffer) : _ordersSnapshotSocketB.Receive(buffer);
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

                        _lastFASTMulticastTime = DateTime.Now;

                        using (MemoryStream stream = new MemoryStream(buffer, 4, length - 4))
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

                                if (orderSnapshots.Count % 5000 == 0)
                                {
                                    SendLogMessage($"OrderSnapshots MsgSeqNum={msgSeqNum}/{_totNumReports}. Total: {orderSnapshots.Count}.", LogMessageType.System);
                                    //WriteLog($"OrderSnapshots +1000 msgSeqNum={msgSeqNum}. Total: {orderSnapshots.Count}/{_totNumReports}", "OrderSnapshotsReader");
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
            byte[] sizeBuffer = new byte[4];

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
                    if (_missingOLRBeginSeqNo > 0 && _missingOLRData && !_ordersIncremental.ContainsKey(_missingOLRBeginSeqNo))
                    {
                        currentFeed = "OLR"; // восстанавливаем данные из потока ордеров

                        SendLogMessage($"Trying to recover missing OLR SeqNo: {_missingOLRBeginSeqNo}-{_missingOLREndSeqNo}", LogMessageType.System);
                    }
                    else
                    if (_missingTLRBeginSeqNo > 0 && _missingTLRData && !_tradesIncremental.ContainsKey(_missingTLRBeginSeqNo))
                    {
                        currentFeed = "TLR"; // восстанавливаем данные из потока сделок
                        SendLogMessage($"Trying to recover missing TLR SeqNo: {_missingTLRBeginSeqNo}-{_missingTLREndSeqNo}", LogMessageType.System);
                    }
                    else
                    {
                        // отдыхаем полсекунды, ничего не надо восстанавливать
                        Thread.Sleep(500);
                        continue;
                    }

                    lock (_socketLockerHistoricalReplay)
                    {
                        // init historical replay socket
                        _historicalReplaySocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                        // начинаем общение с 1
                        int msgSeqNum = 1;

                        // 1. Соединяемся с потоком восстановления по TCP
                        _historicalReplaySocket.Connect(_historicalReplayEndPoint);

                        // 2. Отправляем запрос на подключение к потоку восстановления
                        FASTHeader header = new FASTHeader();
                        header.MsgType = "A"; //Тип сообщения на установку сессии
                        header.SenderCompID = "OsEngine";
                        header.TargetCompID = "MOEX"; // TODO: id фирмы брокера (?)
                        header.MsgSeqNum = msgSeqNum++;

                        FASTLogonMessage logonMessageBody = new FASTLogonMessage();
                        logonMessageBody.Username = "user0";
                        logonMessageBody.Password = "pass0";                        
                                                
                        //Отправляем сообщение
                        SendFIXMessage(_historicalReplaySocket, header, logonMessageBody);                            

                        // получаем ответ - должен быть Logon
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

                        OpenFAST.Context context = CreateNewContext();

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

                        // 3. Отправляем MarketDataRequest (V)
                        header = new FASTHeader();
                        header.MsgType = "V";
                        header.SenderCompID = "OsEngine";
                        header.TargetCompID = "MOEX";
                        header.MsgSeqNum = msgSeqNum++;
                        
                        long ApplBegSeqNum = currentFeed == "OLR" ? _missingOLRBeginSeqNo : _missingTLRBeginSeqNo;
                        long ApplEndSeqNum = currentFeed == "OLR" ? _missingOLREndSeqNo : _missingTLREndSeqNo;

                        const long TCPReplayLimitNumberOfMessages = 500;
                        if (ApplEndSeqNum - ApplBegSeqNum >= TCPReplayLimitNumberOfMessages) // tcp replay limit
                        {
                            ApplEndSeqNum = ApplBegSeqNum + TCPReplayLimitNumberOfMessages - 1;
                        }

                        MarketDataRequestMessage marketDataRequest = new MarketDataRequestMessage();
                        marketDataRequest.ApplID = currentFeed;
                        marketDataRequest.ApplBegSeqNum = ApplBegSeqNum.ToString();
                        marketDataRequest.ApplEndSeqNum = ApplEndSeqNum.ToString();
                        
                        //Отправляем сообщение
                        SendFIXMessage(_historicalReplaySocket, header, marketDataRequest);

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

                                    // закрываем сокет, а то нас сервак забанит
                                    _historicalReplaySocket.Close();
                                    break;
                                }

                                string messageType = msg.GetString("MessageType");
                                                                
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

                                    SendLogMessage($"TCP recovery received MessageType: {messageType} for {currentFeed} with MsgSeqNum={MsgSeqNum}", LogMessageType.System);
                                }
                                else if (messageType == "5") // Logout
                                {
                                    SendLogMessage($"TCP recovery received MessageType: {messageType} for {currentFeed}", LogMessageType.System);
                                    SendLogMessage($"Historical Replay server Logout. {msg.GetString("Text")}", LogMessageType.System);

                                    //отвечаем серверу Logout
                                    header = new FASTHeader();
                                    header.MsgType = "5"; // logout
                                    header.SenderCompID = "OsEngine";
                                    header.TargetCompID = "MOEX";
                                    header.MsgSeqNum = msgSeqNum++;

                                    LogoutMessage logoutMessageBody = new LogoutMessage();
                                    logoutMessageBody.Text = "Logging out";                                                                      

                                    //Отправляем сообщение
                                    SendFIXMessage(_historicalReplaySocket, header, logoutMessageBody);

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
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
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
                                
                                //WriteLog($"msgType=X " + TradeToString(groupVal), "TradeMessagesReader");

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
                            //WriteLog($"W-Trade {name} (RptSeq={RptSeq}): with {secVal.Length} entries. Total trade entries: {tradeSnapshots[name].Trades.Count}", "TradeMessagesReader");

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
                                    
                                    if (_missingTLRBeginSeqNo == -1) // если при этом потоки данных не зафиксировали пропуска данных
                                    {
                                        // такое бывает когда мы подключились посреди торговой сессии и пропуск возник из-за того, что 
                                        // последние данные в снэпшоте оказались слишком старыми и 
                                        // надо восстанавливать принудительно или ждать следующего цикла снэпшотов

                                        // при этом надо среди всех снэпшотов найти наименьшее значение обработанных сообщений
                                        long minSeq = 0;
                                        for (int snapshotIndex = 0; i < tradeSnapshots.Values.Count; snapshotIndex++)
                                        {
                                            SecuritySnapshot snapshot = tradeSnapshots.Values.ElementAt(snapshotIndex);
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
                                    SendLogMessage($"{name} X-trade update with rptseq={RptSeq} but last rptseq={lastRptSeqProcessed[name]}. Total trades in queue: {tradesFromIncremental[name].Count}. Snapshot rptseq={tradeSnapshots[name].RptSeq}|lastmsg={tradeSnapshots[name].LastMsgSeqNumProcessed}", LogMessageType.System);
                                
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

                        if (tradesFromIncremental[name].Count > 5 && tradeSnapshots[name].RptSeq >= tradesFromIncremental[name][0].GetInt("RptSeq") && tradeSnapshots[name].IsComplete) // если необработанных сообщений накопилось много, то пора восстанавливать данные из снэпшота
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

                                //_logFileXOrders.WriteLine($"{DateTime.Now} [{MsgSeqNum}] {msgType}: {name} rptseq={RptSeq}, MDEntryType={MDEntryType} ");

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
                            //SendLogMessage($"W-Orders {name} (RptSeq={RptSeq}): with {secVal.Length} entries. Total entries: {orderSnapshots[name].Data.Count}", LogMessageType.System);

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

                                if (ordersFromIncremental[name].Count > 5 && !_missingOLRData)
                                {
                                    _missingOLRData = true; // надо восстанавливать по TCP                                 
                
                                    if (_missingOLRBeginSeqNo == -1) // если при этом потоки данных не зафиксировали пропуска данных
                                    {
                                        // такое бывает когда мы подключились посреди торговой сессии и пропуск возник из-за того, что 
                                        // последние данные в снэпшоте оказались слишком старыми и 
                                        // надо восстанавливать принудительно или ждать следующего цикла снэпшотов

                                        // при этом надо среди всех снэпшотов найти наименьшее значение обработанных сообщений
                                        long minSeq = 0;
                                        for (int snapshotIndex = 0; snapshotIndex < orderSnapshots.Values.Count; snapshotIndex++)
                                        {
                                            OrdersSnapshot snapshot = orderSnapshots.Values.ElementAt(snapshotIndex);
                                            if (minSeq == 0 || snapshot.LastMsgSeqNumProcessed < minSeq)
                                            {
                                                minSeq = snapshot.LastMsgSeqNumProcessed;
                                            }
                                        }

                                        _missingOLRBeginSeqNo = minSeq + 1;

                                        // подобная ситуация возникает только при первом подключении, так что проверять надо MsgSeqNum самых первых принятых обновлений
                                        List<long> keys = null;

                                        lock (_ordersIncrementalLocker)
                                        {
                                            keys = _ordersIncremental.Keys.ToList();
                                        }
                                        keys.Sort();
                                        if (keys[0] > _missingOLRBeginSeqNo)
                                            _missingOLREndSeqNo = keys[0] - 1;
                                    }
                                }

                                if (ordersFromIncremental[name].Count % 10 == 0)
                                    SendLogMessage($"{name} X-order update with rptseq={RptSeq} but last rptseq={lastRptSeqProcessed[name]}. Total updates in queue: {ordersFromIncremental[name].Count}. Snapshot rptseq={orderSnapshots[name].RptSeq}|lastmsg={orderSnapshots[name].LastMsgSeqNumProcessed}", LogMessageType.System);
                                
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
                double price = update.Price.ToDouble();
                double size = update.Size.ToDouble();

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

                    try
                    {
                        bytesRec = _MFIXTradeSocket.Receive(bytes);
                    }
                    catch (Exception ex)
                    {
                        SendLogMessage("Error receiving FIX Message " + ex.ToString(), LogMessageType.Error);
                        if (ServerStatus != ServerConnectStatus.Disconnect)
                        {
                            ServerStatus = ServerConnectStatus.Disconnect;
                            DisconnectEvent();
                        }
                    }

                    _lastMFIXTradeTime = DateTime.Now;

                    string serverMessage = Encoding.UTF8.GetString(bytes, 0, bytesRec);
                    WriteLogIncomingFIXMessage(serverMessage);

                    FIXMessage fixMessage = FIXMessage.ParseFIXMessage(serverMessage);
                    
                    if (fixMessage.MsgSeqNum == _MFIXTradeMsgSeqNumIncoming)
                    {
                        _MFIXTradeMsgSeqNumIncoming++;
                    }

                    // 0. Обрабатываем TestRequest
                    if (fixMessage.MessageType == "TestRequest")
                    {
                        Header header = new Header();
                        header.MsgType = "0"; //Тип сообщения на установку сессии
                        header.SenderCompID = _MFIXTradeServerLogin;
                        header.TargetCompID = _MFIXTradeServerTargetCompId;
                        header.MsgSeqNum = _MFIXTradeMsgSeqNum++;
                        
                        string TestReqID = fixMessage.Fields["TestReqID"];
                        HeartbeatMessage hbMsg = new HeartbeatMessage();
                        hbMsg.TestReqID = TestReqID;
                                                
                        string heartbeatMessage = SendFIXMessage(_MFIXTradeSocket, header, hbMsg);
                        WriteLogOutgoingFIXMessage(heartbeatMessage);
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
                        DateTime TransactionTime = DateTime.ParseExact(TransactTime, "yyyyMMdd-HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);

                        string Text = fixMessage.Fields.ContainsKey("Text") ? fixMessage.Fields["Text"] : "";

                        string Symbol = fixMessage.Fields["Symbol"];

                        Order order = new Order();

                        order.SecurityNameCode = Symbol;
                        order.SecurityClassCode = fixMessage.Fields["TradingSessionID"];
                        order.PortfolioNumber = fixMessage.Fields["Account"];
                        order.NumberMarket = fixMessage.Fields["OrderID"];
                        order.Comment = Text;

                        if (ExecType == "F") // сделка
                        {
                            order.Volume = LastQty.ToDecimal();
                        }
                        else
                        {
                            order.Volume = OrderQty.ToDecimal(); // сделки не было значит вот это
                        }

                        try
                        {
                            order.NumberUser = int.Parse(fixMessage.Fields["SecondaryClOrdID"]);
                        }
                        catch
                        {
                            // ignore
                        }

                        if (order.NumberUser == 0) // ищем номер пользователя по биржевому номеру
                        {
                            int NumberUser = 0;

                            List<int> keysList = new List<int>(_changedOrderIds.Keys);
                            for (int key = 0; key < keysList.Count; key++)
                            {
                                if (_changedOrderIds[key] == order.NumberMarket)
                                    NumberUser = key;
                            }

                            order.NumberUser = NumberUser;
                        }

                        if (_changedOrderIds.ContainsKey(order.NumberUser))
                        {
                            // для измененного ордера установлен новый идентификатор на бирже
                            _changedOrderIds[order.NumberUser] = order.NumberMarket;
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

                            SendLogMessage($"MFIX sent order status 'fail' with comment: {order.Comment}", LogMessageType.Error);
                        }

                        if (ExecType == "F")  // MyTrades
                        {                               
                            string tradeId = fixMessage.Fields["ExecID"].Split('|')[0];

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

                        SendLogMessage("MFIX Trade server disconnected", LogMessageType.System);
                    }

                    // 6. Sequence Reset
                    if (fixMessage.MessageType == "SequenceReset")
                    {
                        string GapFillFlag = fixMessage.Fields["GapFillFlag"];                                                
                        _MFIXTradeMsgSeqNumIncoming = long.Parse(fixMessage.Fields["NewSeqNo"]) + 1; // new sequence number with loss of data
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }
                
        public event Action<Trade> NewTradesEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

        #endregion

        #region 9 Trade

        private RateGate _rateGateForOrders = new RateGate(30, TimeSpan.FromSeconds(1));
               
        private List<Order> _sendOrders = new List<Order>();

        public void SendOrder(Order order)
        {
            _rateGateForOrders.WaitToProceed();

            try
            {
                Header header = new Header();
                header.MsgType = "D"; // new single order
                header.SenderCompID = _MFIXTradeServerLogin;
                header.TargetCompID = _MFIXTradeServerTargetCompId;
                header.MsgSeqNum = _MFIXTradeMsgSeqNum++;

                NewOrderSingleMessage msg = new NewOrderSingleMessage();
                msg.ClOrdID = _MFIXTradeClientCode + _MFIXTag11Separator + (_MFIXTag11ContainsOrderIDs ? order.NumberUser.ToString() : _MFIXTradeClientCode);
                msg.SecondaryClOrdID = order.NumberUser.ToString();
                msg.NoPartyID = "1";
                msg.PartyID = _MFIXTradeClientCode;
                msg.Account = _MFIXTradeAccount;
                msg.NoTradingSessions = "1";
                msg.TradingSessionID = order.SecurityClassCode;
                msg.Symbol = order.SecurityNameCode;
                msg.Side = order.Side == Side.Buy ? "1" : "2";
                msg.TransactTime = DateTime.UtcNow.ToString("yyyyMMdd-HH:mm:ss.fff");
                msg.OrdType = order.TypeOrder == OrderPriceType.Market ? "1" : "2"; // 1 - Market, 2 - Limit
                msg.OrderQty = order.Volume.ToString();
                msg.Price = order.TypeOrder == OrderPriceType.Limit ? order.Price.ToString().Replace(',', '.') : "0";
                
                string newSingleOrderMessage = SendFIXMessage(_MFIXTradeSocket, header, msg);
                WriteLogOutgoingFIXMessage(newSingleOrderMessage);
            }
            catch (Exception exception)
            {
                SendLogMessage("Order send error " + exception.ToString(), LogMessageType.Error);
            }
        }

        // в этот словарь помещаем соответствия номеров рыночных ордеров пользовательским номерам ордеров        
        Dictionary<int, string> _changedOrderIds = new Dictionary<int, string>();

        /// <summary>
        /// Order price change
        /// </summary>
        /// <param name="order">An order that will have a new price</param>
        /// <param name="newPrice">New price</param>
        public void ChangeOrderPrice(Order order, decimal newPrice)
        {          
            _changedOrderIds[order.NumberUser] = ""; // делаем соответствие номера

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

                Header header = new Header();
                header.MsgType = "G"; // Order cancel replace request
                header.SenderCompID = _MFIXTradeServerLogin;
                header.TargetCompID = _MFIXTradeServerTargetCompId;
                header.MsgSeqNum = _MFIXTradeMsgSeqNum++;

                OrderCancelReplaceRequestMessage msg = new OrderCancelReplaceRequestMessage();
                msg.ClOrdID = _MFIXTradeClientCode + _MFIXTag11Separator + (_MFIXTag11ContainsOrderIDs ? DateTime.UtcNow.Ticks.ToString() : _MFIXTradeClientCode);
                msg.OrigClOrdID = _MFIXTradeClientCode + _MFIXTag11Separator + (_MFIXTag11ContainsOrderIDs ?  order.NumberUser.ToString() : _MFIXTradeClientCode);
                msg.SecondaryClOrdID = order.NumberUser.ToString();
                msg.OrderID = order.NumberMarket;
                msg.PartyID = _MFIXTradeClientCode;
                msg.Account = _MFIXTradeAccount;
                msg.TradingSessionID = order.SecurityClassCode;
                msg.Symbol = order.SecurityNameCode;
                msg.Side = order.Side == Side.Buy ? "1" : "2";
                msg.TransactTime = DateTime.UtcNow.ToString("yyyyMMdd-HH:mm:ss.fff");
                msg.OrdType = order.TypeOrder == OrderPriceType.Market ? "1" : "2"; // 1 - Market, 2 - Limit
                msg.OrderQty = order.Volume.ToString();
                msg.Price = order.TypeOrder == OrderPriceType.Limit ? newPrice.ToString().Replace(',', '.') : "0";
                               
                string orderMessage = SendFIXMessage(_MFIXTradeSocket, header, msg);
                WriteLogOutgoingFIXMessage(orderMessage);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public bool CancelOrder(Order order)
        {
            _rateGateForOrders.WaitToProceed();

            try
            {
                Header header = new Header();
                header.MsgType = "F"; // order cancel request
                header.SenderCompID = _MFIXTradeServerLogin;
                header.TargetCompID = _MFIXTradeServerTargetCompId;
                header.MsgSeqNum = _MFIXTradeMsgSeqNum++;

                OrderCancelRequestMessage msg = new OrderCancelRequestMessage();

                msg.ClOrdID = _MFIXTradeClientCode + _MFIXTag11Separator + (_MFIXTag11ContainsOrderIDs ? DateTime.UtcNow.Ticks.ToString() : _MFIXTradeClientCode);
                msg.OrigClOrdID = _MFIXTradeClientCode + _MFIXTag11Separator + (_MFIXTag11ContainsOrderIDs ?  order.NumberUser.ToString() : _MFIXTradeClientCode);

                msg.OrderID = order.NumberMarket.ToString();

                msg.Side = order.Side == Side.Buy ? "1" : "2";
                msg.TransactTime = DateTime.UtcNow.ToString("yyyyMMdd-HH:mm:ss.fff");

                string orderMessage = SendFIXMessage(_MFIXTradeSocket, header, msg);
                WriteLogOutgoingFIXMessage(orderMessage);
            }
            catch (Exception exception)
            {
                SendLogMessage("Order cancel request error " + exception.ToString(), LogMessageType.Error);
                return false;
            }

            return true;
        }              

        public void CancelAllOrders()
        {
            _rateGateForOrders.WaitToProceed();

            try
            {
                Header header = new Header();
                header.MsgType = "q"; // order mass cancel request
                header.SenderCompID = _MFIXTradeServerLogin;
                header.TargetCompID = _MFIXTradeServerTargetCompId;
                header.MsgSeqNum = _MFIXTradeMsgSeqNum++;                

                OrderMassCancelRequestMessage msg = new OrderMassCancelRequestMessage();
                msg.ClOrdID = _MFIXTradeClientCode + _MFIXTag11Separator + (_MFIXTag11ContainsOrderIDs ? DateTime.UtcNow.Ticks.ToString() : _MFIXTradeClientCode);
                msg.TransactTime = DateTime.UtcNow.ToString("yyyyMMdd-HH:mm:ss.fff");
                msg.Account = _MFIXTradeAccount;
                msg.PartyID = _MFIXTradeClientCode;

                string orderMessage = SendFIXMessage(_MFIXTradeSocket, header, msg);
                WriteLogOutgoingFIXMessage(orderMessage);
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
                Header header = new Header();
                header.MsgType = "q"; // order mass cancel request
                header.SenderCompID = _MFIXTradeServerLogin;
                header.TargetCompID = _MFIXTradeServerTargetCompId;
                header.MsgSeqNum = _MFIXTradeMsgSeqNum++;

                OrderMassCancelRequestMessage msg = new OrderMassCancelRequestMessage();
                msg.ClOrdID = _MFIXTradeClientCode + _MFIXTag11Separator + (_MFIXTag11ContainsOrderIDs ? DateTime.UtcNow.Ticks.ToString() : _MFIXTradeClientCode);
                msg.MassCancelRequestType = "1";
                msg.TradingSessionID = security.NameClass;
                msg.Symbol = security.NameId;
                msg.TransactTime = DateTime.UtcNow.ToString("yyyyMMdd-HH:mm:ss.fff");
                msg.Account = _MFIXTradeAccount;
                msg.PartyID = _MFIXTradeClientCode;

                string orderMessage = SendFIXMessage(_MFIXTradeSocket, header, msg);
                WriteLogOutgoingFIXMessage(orderMessage);
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

        public OrderStateType GetOrderStatus(Order order)
        {
            return OrderStateType.None;
            // Order Status Request (H) - запрос не реализован на серверах MFIX Trade и MFIX Trade Capture (в документации зачеркнуто)
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

        private int GetDecimals(decimal x)
        {
            int precision = 0;
            while (x * (decimal)Math.Pow(10, precision) != Math.Round(x * (decimal)Math.Pow(10, precision)))
                precision++;
            return precision;
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

        private OpenFAST.Context CreateNewContext()
        {
            OpenFAST.Context context = new OpenFAST.Context();
            for (int t = 0; t < _templates.Count(); t++)
            {
                MessageTemplate tmplt = _templates[t];
                context.RegisterTemplate(int.Parse(tmplt.Id), tmplt);
            }

            return context;
        }

        public void SetLeverage(Security security, decimal leverage) { }

        #endregion

        #region 11 Log

        private string _logLock = "locker for stream writer";
        private StreamWriter _logFile = new StreamWriter("Engine\\Log\\FIXFAST_Multicast_UDP-log.txt");
        private StreamWriter _logFileXOrders = new StreamWriter("Engine\\Log\\FIXFAST_Multicast_UDP-log-XOrders.txt");

        private StreamWriter _logFileMFIX = new StreamWriter($"Engine\\Log\\MFIX_{DateTime.Now.ToString("yyyy-MM-dd")}.txt");              

        private void WriteLogOutgoingFIXMessage(string message)
        {
            _logFileMFIX.WriteLine($"{DateTime.UtcNow} >>> [MFIX Trade]: {message}");
            _logFileMFIX.Flush();
        }

        private void WriteLogIncomingFIXMessage(string message)
        {
            _logFileMFIX.WriteLine($"{DateTime.UtcNow} <<< [MFIX Trade]: {message}");
            _logFileMFIX.Flush();
        }

        private void WriteLog(string message, string source)
        {
            lock (_logLock)
            {
                _logFile.WriteLine($"{DateTime.Now} {source}: {message}");
            }
        }

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent?.Invoke(message, messageType);
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }

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