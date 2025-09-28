/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace OsEngine.Market.Servers.Atp
{
    public class AtpServer : AServer
    {
        public AtpServer()
        {
            AtpServerRealization realization = new AtpServerRealization();
            ServerRealization = realization;

            CreateParameterString("Broker ID", "");
            CreateParameterString("User ID", "");
            CreateParameterPassword("User password", "");
            CreateParameterString("Data server url", "tcp://demo9.atplatform.cn:41213");
            CreateParameterString("Trade server url", "tcp://demo9.atplatform.cn:40905");
            CreateParameterButton("Securities");
            CreateParameterBoolean("Auth on/off", false);

            CreateParameterEnum("Activate routers",
                "trade+data", new List<string>() { "trade+data", "trade", "data" });

            ServerParameterButton secButton = ((ServerParameterButton)ServerParameters[5]);
            secButton.UserClickButton += SecButton_UserClickButton;
        }

        private void SecButton_UserClickButton()
        {
            SecuritiesAtpUi ui = new SecuritiesAtpUi(this);
            ui.ShowDialog();

            ((AtpServerRealization)ServerRealization)._securities = this.Securities;
            ((AtpServerRealization)ServerRealization).TrySaveSecuritiesInFile();
        }
    }

    public class AtpServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public AtpServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;
            Thread worker = new Thread(SecurityLoader);
            worker.Start();

            Thread worker2 = new Thread(WorkerPlaceMarketData);
            worker2.IsBackground = true;
            worker2.Start();

            Thread worker3 = new Thread(WorkerPlaceTradeRouter);
            worker3.IsBackground = true;
            worker3.Start();

            Thread worker4 = new Thread(CheckSocketThreadsStatus);
            worker4.Start();
        }

        public DateTime ServerTime { get; set; }

        private DateTime _lastConnectTime;

        public void Connect(WebProxy proxy = null)
        {
            BrokerId = ((ServerParameterString)ServerParameters[0]).Value;
            UserId = ((ServerParameterString)ServerParameters[1]).Value;
            UserPassword = ((ServerParameterPassword)ServerParameters[2]).Value;
            AppId = "wang_osengine_1.8.2";
            AufCode = "9QADGM87BU4APIUY";
            DataServerUrl = ((ServerParameterString)ServerParameters[3]).Value;
            TradeServerUrl = ((ServerParameterString)ServerParameters[4]).Value;
            IsReal = ((ServerParameterBool)ServerParameters[6]).Value;

            if (string.IsNullOrEmpty(BrokerId))
            {
                SendLogMessage("No BrokerId!!! No connection!!!", LogMessageType.Error);
                return;
            }

            if (string.IsNullOrEmpty(UserId))
            {
                SendLogMessage("No UserId!!! No connection!!!", LogMessageType.Error);
                return;
            }

            if (string.IsNullOrEmpty(UserPassword))
            {
                SendLogMessage("No UserPassword!!! No connection!!!", LogMessageType.Error);
                return;
            }

            if (DataServerUrl == TradeServerUrl)
            {
                SendLogMessage("Data and Trade servers url is equal!!! No connection!!!",
                LogMessageType.Error);
                return;
            }

            if (DataRouterIsActivate == true &&
                string.IsNullOrEmpty(DataServerUrl))
            {
                SendLogMessage("No Data server url!!! No connection!!!", LogMessageType.Error);
                return;
            }

            if (TradeRouterIsActivate == true &&
                string.IsNullOrEmpty(TradeServerUrl))
            {
                SendLogMessage("No Trade server url!!! No connection!!!", LogMessageType.Error);
                return;
            }

            _tradeSocketConnect = false;
            _marketSocketConnect = false;

            _subscribeSecurities = new List<Security>();

            CloseRouters();

            Thread.Sleep(5000);

            _lastConnectTime = DateTime.Now;

            LoadRouters();

            Thread.Sleep(5000);

            _messagesToSendMarketData = new ConcurrentQueue<string>();
            _messagesToSendTrade = new ConcurrentQueue<string>();

            string connectionStr = "C@";
            connectionStr += BrokerId + "@";
            connectionStr += UserId + "@";
            connectionStr += UserPassword + "@";
            connectionStr += AppId + "@";
            connectionStr += AufCode + "@";
            connectionStr += DataServerUrl + "@";
            connectionStr += TradeServerUrl + "@";
            connectionStr += IsReal + "@";

            _messagesToSendMarketData.Enqueue(connectionStr);
            _messagesToSendTrade.Enqueue(connectionStr);

            if (DataRouterIsActivate == true)
            {// Сокет для данных
                if (_socketMarketData == null)
                {
                    IPHostEntry ipHost = Dns.GetHostEntry("localhost");

                    IPAddress ipAddr = null;

                    for (int i = 0; i < ipHost.AddressList.Length; i++)
                    {
                        IPAddress ipAddrCurrent = ipHost.AddressList[i];

                        string adr = ipAddrCurrent.ToString();

                        if (adr == "127.0.0.1")
                        {
                            ipAddr = ipHost.AddressList[i];
                            break;
                        }
                    }

                    if (ipAddr == null)
                    {
                        SendLogMessage("No localhost address", LogMessageType.Error);
                        return;
                    }

                    IPEndPoint ipEndPoint = new IPEndPoint(ipAddr, 5555);

                    _socketMarketData = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                    try
                    {
                        _socketMarketData.Connect(ipEndPoint);
                    }
                    catch (Exception ex)
                    {
                        SendLogMessage("Atp market server is not responding" + ex.ToString(),

                            LogMessageType.Error);
                        return;
                    }
                }
            }

            if (TradeRouterIsActivate == true)
            {// Сокет для торговли
                if (_socketToTrade == null)
                {
                    IPHostEntry ipHost = Dns.GetHostEntry("localhost");

                    IPAddress ipAddr = null;

                    for (int i = 0; i < ipHost.AddressList.Length; i++)
                    {
                        IPAddress ipAddrCurrent = ipHost.AddressList[i];

                        string adr = ipAddrCurrent.ToString();

                        if (adr == "127.0.0.1")
                        {
                            ipAddr = ipHost.AddressList[i];
                            break;
                        }
                    }

                    IPEndPoint ipEndPoint = new IPEndPoint(ipAddr, 5556);

                    _socketToTrade = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                    try
                    {
                        _socketToTrade.Connect(ipEndPoint);
                    }
                    catch (Exception ex)
                    {
                        SendLogMessage("Atp trade server is not responding" + ex.ToString(),

                            LogMessageType.Error);
                        return;
                    }
                }
            }

            ClearFileSystem();

            Thread.Sleep(5000);

            _canSendMessagesMarketData = true;
            _canSendMessagesTradeRouter = true;
        }

        public void Dispose()
        {
            _canSendMessagesMarketData = false;
            _canSendMessagesTradeRouter = false;

            try
            {
                CloseRouters();
            }
            catch (Exception exeption)
            {
                HandlerException(exeption);
            }

            try
            {
                if (_socketMarketData != null)
                {
                    try
                    {
                        SendMessage("Disconnect", _socketMarketData, "MarketServer");
                        _socketMarketData.Shutdown(SocketShutdown.Send);
                    }
                    catch
                    {
                        // ignore
                    }

                    _socketMarketData.Close();
                    _socketMarketData.Dispose();
                    _socketMarketData = null;
                }
            }
            catch (Exception exeption)
            {
                HandlerException(exeption);
            }
            try
            {
                if (File.Exists("Atp_Router\\Files\\ConnectTrade.txt"))
                {
                    File.Delete("Atp_Router\\Files\\ConnectTrade.txt");
                }

                if (File.Exists("Atp_Router\\Files\\ConnectData.txt"))
                {
                    File.Delete("Atp_Router\\Files\\ConnectData.txt");
                }
            }
            catch (Exception exeption)
            {
                HandlerException(exeption);
            }

            try
            {
                if (_socketToTrade != null)
                {
                    try
                    {
                        SendMessage("Disconnect", _socketToTrade, "TradeServer");
                        _socketToTrade.Shutdown(SocketShutdown.Send);
                    }
                    catch
                    {
                        // ignore
                    }

                    _socketToTrade.Close();
                    _socketToTrade.Dispose();
                    _socketToTrade = null;
                }
            }
            catch (Exception exeption)
            {
                HandlerException(exeption);
            }


            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        public ServerType ServerType
        {
            get { return ServerType.Atp; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public bool TradeRouterIsActivate
        {
            get
            {
                ServerParameterEnum parameter = ((ServerParameterEnum)ServerParameters[7]);

                if (parameter.Value.Contains("trade"))
                {
                    return true;
                }

                return false;
            }
        }

        public bool DataRouterIsActivate
        {
            get
            {
                ServerParameterEnum parameter = ((ServerParameterEnum)ServerParameters[7]);

                if (parameter.Value.Contains("data"))
                {
                    return true;
                }

                return false;
            }
        }

        public void CloseRouters()
        {
            Process[] ps1 = System.Diagnostics.Process.GetProcesses();

            List<Process> process = new List<Process>();

            for (int i = 0; i < ps1.Length; i++)
            {
                Process p = ps1[i];

                try
                {
                    if (p.MainModule.FileName != ""
                        && p.Modules != null)
                    {
                        process.Add(p);
                    }
                }
                catch
                {

                }
            }

            for (int i = 0; i < process.Count; i++)
            {
                Process p = process[i];

                for (int j = 0; p.Modules != null && j < p.Modules.Count; j++)
                {
                    if (p.Modules[j].FileName == null)
                    {
                        continue;
                    }

                    if (p.Modules[j].FileName.EndsWith("marketdata.exe"))
                    {
                        p.Kill();
                        p.Dispose();
                        break;
                    }
                    else if (p.Modules[j].FileName.EndsWith("cmd.exe"))
                    {
                        p.Kill();
                        p.Dispose();
                        break;
                    }
                    else if (p.Modules[j].FileName.EndsWith("trader.exe"))
                    {
                        p.Kill();
                        p.Dispose();
                        break;
                    }

                }
            }
        }

        public void LoadRouters()
        {
            //\Atp_Router\api-samplecode\marketdata\x64\Debug\marketdata.exe
            //\Atp_Router\api-samplecode\trader\x64\Debug\trader.exe

            string curDir = Environment.CurrentDirectory;

            string dirMarketData = curDir + "\\Atp_Router\\api-samplecode\\marketdata\\x64\\Debug\\marketdata.exe";
            string dirTrader = curDir + "\\Atp_Router\\api-samplecode\\trader\\x64\\Debug\\trader.exe";

            try
            {
                if (TradeRouterIsActivate)
                {
                    Process.Start(dirTrader);
                }

                if (DataRouterIsActivate)
                {
                    Process.Start(dirMarketData);
                }

                Thread.Sleep(3000);
            }
            catch (Exception e)
            {
                SendLogMessage(e.ToString(), LogMessageType.Error);
            }
        }

        private void ClearFileSystem()
        {
            try
            {
                CheckFolders();

                ClearFolder("Atp_Router\\Files\\");

                string[] folders = Directory.GetDirectories("Atp_Router\\Files\\");

                for (int i = 0; i < folders.Length; i++)
                {
                    ClearFolder(folders[i]);
                }
            }
            catch (Exception e)
            {
                SendLogMessage(e.ToString(), LogMessageType.Error);
            }
        }

        private void CheckFolders()
        {
            string path = "Atp_Router\\Files\\";

            string[] folders = new string[] 
            {
                path, 
                path + "MyTrades\\", 
                path + "MyTrades2\\", 
                path + "OrderAction2\\", 
                path + "OrderActiv\\", 
                path + "OrderFail1\\", 
                path + "OrderFail2\\",
                path + "OrderFail3\\",
                path + "OrderFail4\\",
                path + "OrderFail5\\",
            };

            for (int i = 0; i < folders.Length; i++)
            {
                if (!Directory.Exists(folders[i]))
                {
                    Directory.CreateDirectory(folders[i]);
                }
            }            
        }

        private void ClearFolder(string folderPath)
        {
            string[] files = Directory.GetFiles(folderPath);

            for (int i = 0; i < files.Length; i++)
            {
                File.Delete(files[i]);
            }
        }

        #endregion

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        private string BrokerId;

        private string UserId;

        private string UserPassword;

        private string AppId;

        private string AufCode;

        private string DataServerUrl;

        private string TradeServerUrl;

        private bool IsReal;

        #endregion

        #region 3 Securities

        public void GetSecurities()
        {
            SecurityEvent(_securities);
        }

        public List<Security> _securities = new List<Security>();

        private void SecurityLoader()
        {
            Thread.Sleep(2000);
            TryLoadSecuritiesFromFile();
        }

        public void TryLoadSecuritiesFromFile()
        {
            if (!File.Exists(@"Engine\" + @"AtpSecurities.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + @"AtpSecurities.txt"))
                {
                    while (reader.EndOfStream == false)
                    {
                        string str = reader.ReadLine();

                        string[] array = str.Split('!');

                        Security newSec = new Security();

                        newSec.Name = array[0];
                        newSec.NameClass = array[1];
                        newSec.NameFull = array[2];
                        newSec.NameId = array[3];

                        if (string.IsNullOrEmpty(newSec.NameId))
                        {
                            newSec.NameId = newSec.NameFull;
                        }

                        newSec.NameFull = array[4];
                        Enum.TryParse(array[5], out newSec.State);
                        newSec.PriceStep = array[6].ToDecimal();
                        newSec.Lot = array[7].ToDecimal();
                        newSec.PriceStepCost = array[8].ToDecimal();
                        newSec.Go = array[9].ToDecimal();
                        Enum.TryParse(array[10], out newSec.SecurityType);
                        newSec.Decimals = Convert.ToInt32(array[11]);
                        newSec.PriceLimitLow = array[12].ToDecimal();
                        newSec.PriceLimitHigh = array[13].ToDecimal();
                        Enum.TryParse(array[14], out newSec.OptionType);
                        newSec.Strike = array[15].ToDecimal();
                        newSec.Expiration = Convert.ToDateTime(array[16]);

                        _securities.Add(newSec);
                    }

                    reader.Close();
                }
                SecurityEvent(_securities);
            }
            catch (Exception)
            {
                // ignore
            }
        }

        public void TrySaveSecuritiesInFile()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + @"AtpSecurities.txt", false))
                {
                    for (int i = 0; i < _securities.Count; i++)
                    {
                        _securities[i].NameClass = "atpSecurity";

                        string result = _securities[i].Name + "!";
                        result += _securities[i].NameClass + "!";
                        result += _securities[i].NameFull + "!";
                        result += _securities[i].NameId + "!";
                        result += _securities[i].NameFull + "!";
                        result += _securities[i].State + "!";
                        result += _securities[i].PriceStep + "!";
                        result += _securities[i].Lot + "!";
                        result += _securities[i].PriceStepCost + "!";
                        result += _securities[i].Go + "!";
                        result += _securities[i].SecurityType + "!";
                        result += _securities[i].Decimals + "!";
                        result += _securities[i].PriceLimitLow + "!";
                        result += _securities[i].PriceLimitHigh + "!";
                        result += _securities[i].OptionType + "!";
                        result += _securities[i].Strike + "!";
                        result += _securities[i].Expiration + "!";

                        writer.WriteLine(result);
                    }

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        List<Portfolio> _portfolios = new List<Portfolio>();

        public void GetPortfolios()
        {
            if (_portfolios.Count == 0)
            {
                Portfolio portfolio = new Portfolio();
                portfolio.Number = "Atp portfolio";
                portfolio.ValueCurrent = 1;
                _portfolios.Add(portfolio);
            }

            PortfolioEvent(_portfolios);
        }

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        public List<Candle> GetCandleHistory(string nameSec, TimeSpan tf, bool IsOsData, int CountToLoad, DateTime timeEnd)
        {
            return null;
        }

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            return null;
        }

        #endregion

        #region 6 Tcp router

        // data socket

        private bool _canSendMessagesMarketData;

        private Socket _socketMarketData;

        private ConcurrentQueue<string> _messagesToSendMarketData = new ConcurrentQueue<string>();

        private void WorkerPlaceMarketData()
        {
            while (true)
            {
                Thread.Sleep(1);
                try
                {

                    if (_socketMarketData == null)
                    {
                        _lastTimeSendMessageInSocketData = DateTime.Now;
                        continue;
                    }

                    if (_canSendMessagesMarketData == false)
                    {
                        _lastTimeSendMessageInSocketData = DateTime.Now;
                        continue;
                    }

                    if (ServerStatus == ServerConnectStatus.Disconnect &&
                        _marketSocketConnect == false)
                    {
                        if (File.Exists("Atp_Router\\Files\\ConnectData.txt"))
                        {
                            SendLogMessage("data router is connected", LogMessageType.System);
                            _marketSocketConnect = true;
                            CheckConnectStatus();
                        }
                    }

                    if (_messagesToSendMarketData.IsEmpty)
                    { // request any incoming data for us that are saving in server / запрос каких-либо входящих данных для нас, которые копятся в сервере
                        if (IncomeMessageFromDataRouter(SendMessage("Process", _socketMarketData, "MarketServer")))
                        {
                            Thread.Sleep(10);
                        }

                        _lastTimeSendMessageInSocketData = DateTime.Now;
                        continue;
                    }

                    string message = null;
                    _messagesToSendMarketData.TryDequeue(out message);

                    if (message == null)
                    {
                        _lastTimeSendMessageInSocketData = DateTime.Now;
                        continue;
                    }

                    _lastTimeSendMessageInSocketData = DateTime.Now;

                    IncomeMessageFromDataRouter(SendMessage(message, _socketMarketData, "MarketServer"));
                }
                catch (Exception error)
                {
                    _canSendMessagesMarketData = false;

                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        ServerStatus = ServerConnectStatus.Disconnect;
                        if (DisconnectEvent != null)
                        {
                            DisconnectEvent();
                        }
                    }

                    Thread.Sleep(10000);
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        // trade socket

        private bool _canSendMessagesTradeRouter;

        private Socket _socketToTrade;

        private ConcurrentQueue<string> _messagesToSendTrade = new ConcurrentQueue<string>();

        private DateTime _lastTimeSendPing;

        private void WorkerPlaceTradeRouter()
        {
            while (true)
            {
                Thread.Sleep(100);
                try
                {

                    if (_socketToTrade == null)
                    {
                        _lastTimeSendMessageInSocketTrade = DateTime.Now;
                        continue;
                    }

                    if (_canSendMessagesTradeRouter == false)
                    {
                        _lastTimeSendMessageInSocketTrade = DateTime.Now;
                        continue;
                    }

                    if (ServerStatus == ServerConnectStatus.Disconnect &&
                       _tradeSocketConnect == false)
                    {
                        if (File.Exists("Atp_Router\\Files\\ConnectTrade.txt"))
                        {
                            SendLogMessage("trade router is connected", LogMessageType.System);
                            _tradeSocketConnect = true;
                            CheckConnectStatus();

                        }
                    }

                    TryGetTradeDataFromFileSys();

                    if (_messagesToSendTrade.IsEmpty)
                    {
                        // request any incoming data for us that are saving in server
                        // запрос каких-либо входящих данных для нас, которые копятся в сервере

                        if (_lastTimeSendPing.AddSeconds(15) < DateTime.Now)
                        {
                            _lastTimeSendPing = DateTime.Now;
                            _lastTimeSendMessageInSocketTrade = DateTime.Now;
                            IncomeMessageFromTradeRouter(SendMessage("Process", _socketToTrade, "TradeServer"));
                            continue;
                        }
                    }

                    string message = null;
                    _messagesToSendTrade.TryDequeue(out message);

                    if (message == null)
                    {
                        _lastTimeSendMessageInSocketTrade = DateTime.Now;
                        continue;
                    }

                    _lastTimeSendMessageInSocketTrade = DateTime.Now;

                    IncomeMessageFromTradeRouter(SendMessage(message, _socketToTrade, "TradeServer"));
                }
                catch (Exception error)
                {
                    _canSendMessagesTradeRouter = false;

                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        ServerStatus = ServerConnectStatus.Disconnect;

                        if (DisconnectEvent != null)
                        {
                            DisconnectEvent();
                        }
                    }

                    Thread.Sleep(10000);
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        string placeLostDataSocket = "";

        string placeLostTradeSocket = "";

        string lastMessageToDataServer = "";

        string lastMessageToTradeServer = "";

        private string SendMessage(string message, Socket socket, string socketName)
        {
            if (socketName == "MarketServer")
            {
                if (message.StartsWith("Process"))
                {
                    // message += iteratorPDataServer;
                    //iteratorPDataServer++;
                }

                placeLostDataSocket = "Sending";
                lastMessageToDataServer = message;
            }
            else
            {
                if (message.StartsWith("Process"))
                {
                    // message += iteratorPTradeServer;
                    // iteratorPTradeServer++;
                }

                placeLostTradeSocket = "Sending";
                lastMessageToTradeServer = message;
            }

            // send data through socket

            byte[] msg = Encoding.UTF8.GetBytes(message);
            socket.Send(msg);

            if (message.StartsWith("Process") == false)
            {
                return message;
            }

            if (socketName == "MarketServer")
            {
                placeLostDataSocket = "Receive";
            }
            else
            {
                placeLostTradeSocket = "Receive";
            }

            // get response from the server

            byte[] bytes = new byte[1024];
            int bytesRec = socket.Receive(bytes);
            string request = Encoding.UTF8.GetString(bytes, 0, bytesRec);

            //clear socket / Освобождаем сокет
            //sender.Shutdown(SocketShutdown.Send);
            //sender.Close();

            for (int i = 0; i < request.Length; i++)
            {
                if (request[i] == '%')
                {
                    request = request.Substring(0, i);
                    break;
                }
            }

            return request;
        }

        // common connect

        private bool _tradeSocketConnect = false;

        private bool _marketSocketConnect = false;

        private void CheckConnectStatus()
        {
            if (TradeRouterIsActivate == true &&
                _tradeSocketConnect == false)
            {
                return;
            }
            if (DataRouterIsActivate == true
                && _marketSocketConnect == false)
            {
                return;
            }

            ServerStatus = ServerConnectStatus.Connect;

            if (ConnectEvent != null)
            {
                ConnectEvent();
            }

        }

        private DateTime _lastTimeSendMessageInSocketTrade;

        private DateTime _lastTimeSendMessageInSocketData;

        private void CheckSocketThreadsStatus()
        {
            while (true)
            {
                Thread.Sleep(5000);

                if (_socketToTrade != null &&
                    _lastTimeSendMessageInSocketTrade.AddSeconds(10) < DateTime.Now
                    && _lastTimeSendMessageInSocketTrade.AddSeconds(30) > DateTime.Now)
                {
                    SendLogMessage("Sockets thread is lost. Trade router. Reconnect", LogMessageType.Error);
                    SendLogMessage("Place lost trade socket thread: " + placeLostTradeSocket, LogMessageType.Error);
                    SendLogMessage("Last message to trade server: " + lastMessageToTradeServer, LogMessageType.Error);
                    CloseRouters();
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }

                if (_socketMarketData != null &&
                    _lastTimeSendMessageInSocketData.AddSeconds(10) < DateTime.Now
                     && _lastTimeSendMessageInSocketData.AddSeconds(30) > DateTime.Now)
                {
                    SendLogMessage("Sockets thread is lost. Data router. Reconnect", LogMessageType.Error);
                    SendLogMessage("Place lost data socket thread: " + placeLostDataSocket, LogMessageType.Error);
                    SendLogMessage("Last message to data server: " + lastMessageToDataServer, LogMessageType.Error);

                    CloseRouters();
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
        }

        #endregion

        #region 7 Check file system 

        private void TryGetTradeDataFromFileSys()
        {
            TryLoadMyTrade();
            TryLoadMyTrades2();
            TryLoadOrderAction2();
            TryLoadOrderFail1();
            TryLoadOrderFail3();
        }

        private string[] GetSortedFileNames(string[] files)
        {
            List<string> result = new List<string>();

            result.AddRange(files);

            for (int i = 0; i < result.Count; i++)
            {
                for (int i2 = 1; i2 < result.Count; i2++)
                {
                    string previousNum = (result[i2 - 1].Split('\\')[result[i2 - 1].Split('\\').Length - 1]).Replace(".txt", "");
                    string curNum = (result[i2].Split('\\')[result[i2].Split('\\').Length - 1]).Replace(".txt", "");

                    if (Convert.ToInt32(previousNum) > Convert.ToInt32(curNum))
                    {
                        string prevAdress = result[i2];
                        result[i2] = result[i2 - 1];
                        result[i2 - 1] = prevAdress;
                    }
                }
            }

            return result.ToArray();
        }

        private int _counterMyTrades = 0;
        private void TryLoadMyTrade()
        {
            if (Directory.Exists("Atp_Router\\Files\\MyTrades\\") == false)
            {
                return;
            }

            string[] files = Directory.GetFiles("Atp_Router\\Files\\MyTrades\\");

            if (_counterMyTrades >= files.Length)
            {
                return;
            }

            files = GetSortedFileNames(files);

            for (int i = _counterMyTrades; i < files.Length; i++)
            {
                try
                {
                    /*  DateTime timeCreate = File.GetCreationTime(files[i]);
                      if (timeCreate.AddSeconds(1) > DateTime.Now)
                      {
                          return;
                      }*/

                    using (StreamReader reader = new StreamReader(files[i]))
                    {
                        string myTrade = reader.ReadLine();

                        if (myTrade.EndsWith("%"))
                        {
                            myTrade = myTrade.Substring(0, myTrade.Length - 1);
                        }

                        LoadMyTrade(myTrade);
                    }
                }
                catch (Exception e)
                {
                    SendLogMessage(e.ToString(), LogMessageType.Error);
                }
            }

            _counterMyTrades = files.Length;
        }

        private int _counterMyTrades2 = 0;
        private void TryLoadMyTrades2()
        {
            if (Directory.Exists("Atp_Router\\Files\\MyTrades2\\") == false)
            {
                return;
            }

            string[] files = Directory.GetFiles("Atp_Router\\Files\\MyTrades2\\");

            if (_counterMyTrades2 >= files.Length)
            {
                return;
            }

            files = GetSortedFileNames(files);

            for (int i = _counterMyTrades2; i < files.Length; i++)
            {
                try
                {
                    /* DateTime timeCreate = File.GetCreationTime(files[i]);
                     if (timeCreate.AddSeconds(1) > DateTime.Now)
                     {
                         return;
                     }*/
                    using (StreamReader reader = new StreamReader(files[i]))
                    {
                        string myTrade = reader.ReadLine();

                        if (myTrade.EndsWith("%"))
                        {
                            myTrade = myTrade.Substring(0, myTrade.Length - 1);
                        }

                        LoadMyTrade(myTrade);
                    }
                }
                catch (Exception e)
                {
                    SendLogMessage(e.ToString(), LogMessageType.Error);
                }
            }

            _counterMyTrades2 = files.Length;
        }

        private int _counterOrderAction2 = 0;
        private void TryLoadOrderAction2()
        {
            if (Directory.Exists("Atp_Router\\Files\\OrderAction2\\") == false)
            {
                return;
            }

            string[] files = Directory.GetFiles("Atp_Router\\Files\\OrderAction2\\");

            if (_counterOrderAction2 >= files.Length)
            {
                return;
            }

            files = GetSortedFileNames(files);

            for (int i = _counterOrderAction2; i < files.Length; i++)
            {
                try
                {
                    /* DateTime timeCreate =  File.GetCreationTime(files[i]);
                     if(timeCreate.AddSeconds(1) > DateTime.Now)
                     {
                         return;
                     }*/

                    using (StreamReader reader = new StreamReader(files[i]))
                    {
                        string myTrade = reader.ReadLine();

                        if (myTrade.EndsWith("%"))
                        {
                            myTrade = myTrade.Substring(0, myTrade.Length - 1);
                        }

                        LoadMyOrder(myTrade);
                    }
                }
                catch (Exception e)
                {
                    SendLogMessage(e.ToString(), LogMessageType.Error);
                }
            }

            _counterOrderAction2 = files.Length;
        }

        private int _counterOrderFail1 = 0;
        private void TryLoadOrderFail1()
        {
            if (Directory.Exists("Atp_Router\\Files\\OrderFail1\\") == false)
            {
                return;
            }

            string[] files = Directory.GetFiles("Atp_Router\\Files\\OrderFail1\\");

            if (_counterOrderFail1 >= files.Length)
            {
                return;
            }

            files = GetSortedFileNames(files);

            for (int i = _counterOrderFail1; i < files.Length; i++)
            {
                try
                {
                    /* DateTime timeCreate = File.GetCreationTime(files[i]);
                     if (timeCreate.AddSeconds(1) > DateTime.Now)
                     {
                         return;
                     }*/
                    using (StreamReader reader = new StreamReader(files[i]))
                    {
                        string myTrade = reader.ReadLine();

                        if (myTrade.EndsWith("%"))
                        {
                            myTrade = myTrade.Substring(0, myTrade.Length - 1);
                        }

                        LoadMyFailOrder(myTrade);
                    }
                }
                catch (Exception e)
                {
                    SendLogMessage(e.ToString(), LogMessageType.Error);
                }
            }

            _counterOrderFail1 = files.Length;
        }

        private int _counterOrderFail3 = 0;
        private void TryLoadOrderFail3()
        {
            if (Directory.Exists("Atp_Router\\Files\\OrderFail3\\") == false)
            {
                return;
            }

            string[] files = Directory.GetFiles("Atp_Router\\Files\\OrderFail3\\");

            if (_counterOrderFail3 >= files.Length)
            {
                return;
            }

            files = GetSortedFileNames(files);

            for (int i = _counterOrderFail3; i < files.Length; i++)
            {
                try
                {
                    /*DateTime timeCreate = File.GetCreationTime(files[i]);
                    if (timeCreate.AddSeconds(1) > DateTime.Now)
                    {
                        return;
                    }*/
                    using (StreamReader reader = new StreamReader(files[i]))
                    {
                        string myTrade = reader.ReadLine();

                        if (myTrade.EndsWith("%"))
                        {
                            myTrade = myTrade.Substring(0, myTrade.Length - 1);
                        }

                        LoadMyFailOrder(myTrade);
                    }
                }
                catch (Exception e)
                {
                    SendLogMessage(e.ToString(), LogMessageType.Error);
                }
            }

            _counterOrderFail3 = files.Length;
        }

        #endregion

        #region 8 WebSocket security subscribe

        private RateGate rateGateSubscribe = new RateGate(1, TimeSpan.FromMilliseconds(300));

        private List<Security> _subscribeSecurities = new List<Security>();

        public void Subscribe(Security security)
        {
            try
            {
                rateGateSubscribe.WaitToProceed();

                for (int i = 0; i < _subscribeSecurities.Count; i++)
                {
                    if (_subscribeSecurities[i].Name == security.Name)
                    {
                        return;
                    }
                }

                _messagesToSendMarketData.Enqueue("S@" + security.Name + "@");
                _messagesToSendTrade.Enqueue("S@" + security.Name + "@");

                _subscribeSecurities.Add(security);
            }
            catch (Exception exeption)
            {
                HandlerException(exeption);
            }
        }

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent { add { } remove { } }

        #endregion

        #region 9 WebSocket parsing the messages

        private void IncomeMessageFromTradeRouter(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            if (message.StartsWith("Process"))
            {
                return;
            }

            if (message.StartsWith("\0\0"))
            {
                return;
            }

            SendLogMessage("Trade router message: " + message, LogMessageType.System);

            if (message.StartsWith("Connect") &&
                ServerStatus == ServerConnectStatus.Disconnect)
            {
                SendLogMessage("trade router is connected", LogMessageType.System);
                _tradeSocketConnect = true;
                CheckConnectStatus();
            }
            else if (message.StartsWith("Disconnect"))
            {
                ServerStatus = ServerConnectStatus.Disconnect;

                if (DisconnectEvent != null)
                {
                    DisconnectEvent();
                }
            }
            else if (message.StartsWith("MyTrade1"))
            {
                LoadMyTrade(message);
            }
            else if (message.StartsWith("OrderAction2"))
            {
                LoadMyOrder(message);
            }
            else if (message.StartsWith("OrderFail3"))
            {
                LoadMyFailOrder(message);
            }
            else if (message.StartsWith("OrderFail1"))
            {
                LoadMyFailOrder(message);
            }
        }

        private bool IncomeMessageFromDataRouter(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return true;
            }

            if (message.StartsWith("Process"))
            {
                return true;
            }

            //SendLogMessage("DateRouter: " + message, LogMessageType.System);

            if (message.StartsWith("Connect") &&
                ServerStatus != ServerConnectStatus.Connect)
            {
                SendLogMessage("data router is connected", LogMessageType.System);
                _marketSocketConnect = true;
                CheckConnectStatus();
                SendLogMessage(message, LogMessageType.System);
            }
            else if (message.StartsWith("Disconnect"))
            {
                ServerStatus = ServerConnectStatus.Disconnect;

                if (DisconnectEvent != null)
                {
                    DisconnectEvent();
                }
                SendLogMessage(message, LogMessageType.System);
            }
            else if (message.StartsWith("Md"))
            {
                LoadMd(message);
            }
            else
            {
                SendLogMessage(message, LogMessageType.System);
            }

            return false;
        }

        private void LoadMyOrder(string strMyOrder)
        {
            //OrderAction2      0
            //@177              1 UserOrderId OrderRef
            //@NI2312 - SH      2 InstrumentID
            //@0                3 Direction
            //@3                4 OrderStatus
            //@20231127         5 InsertDate
            //@06:17:17         6 InsertTime
            //@12344            7 LimitPrice
            //@1                8 VolumeTotalOriginal

            string[] ordArr = strMyOrder.Split('@');

            Order order = new Order();

            order.NumberMarket = ordArr[1];

            try
            {
                order.NumberUser = Convert.ToInt32(ordArr[0]);
            }
            catch
            {
                try
                {
                    order.NumberUser = Convert.ToInt32(ordArr[1]);
                }
                catch
                {
                    // ignore
                }
            }

            if (string.IsNullOrEmpty(order.NumberMarket) == true)
            {
                order.NumberMarket = ordArr[9];
            }

            order.SecurityNameCode = ordArr[2];

            order.Price = ordArr[7].ToDecimal();
            order.Volume = ordArr[8].ToDecimal();


            string date = ordArr[5];
            string time = ordArr[6];

            DateTime timeData = GetTimeFromStrings(date, time);
            order.TimeCallBack = timeData;

            if (ordArr[3] == "0")
            {
                order.Side = Side.Buy;
            }
            else
            {
                order.Side = Side.Sell;
            }

            string status = ordArr[4];

            if (status == "0")
            { // Done
                order.State = OrderStateType.Done;
            }
            else if (status == "5")
            { // Cancel
                order.State = OrderStateType.Cancel;
            }
            else if (status == "3")
            { // Cancel
                order.State = OrderStateType.Active;
            }
            else
            {
                return;
            }

            if (order.State == OrderStateType.Active
                && _lastConnectTime.AddMinutes(1) > DateTime.Now)
            {
                return;
            }

            if (MyOrderEvent != null)
            {
                MyOrderEvent(order);
            }
        }

        private void LoadMyFailOrder(string strMyOrder)
        {
            //OrderFail3@922@55@ @@@15@The order has been all traded or canceled.%

            string[] ordArr = strMyOrder.Split('@');

            Order order = new Order();

            order.NumberMarket = ordArr[1];

            try
            {
                order.NumberUser = Convert.ToInt32(ordArr[1]);
            }
            catch
            {
                // ignore
            }

            order.State = OrderStateType.Cancel;

            if (MyOrderEvent != null)
            {
                MyOrderEvent(order);
            }

            SendLogMessage(strMyOrder, LogMessageType.Error);
        }

        private void LoadMyTrade(string strMyTrade)
        {//MyTrade1      0
         //@177          1 UserOrderId
         //@MTwmwtbx     2
         //@20231127     3 Date
         //@06:17:17     4 Time
         //@NI2312 - SH  5 SecCode
         //@1            6 Volume
         //@0 %          7 Price
         //@0 %          8 Direction
         //@asdfag213    9 TradeId

            string[] mtArr = strMyTrade.Split('@');

            MyTrade trade = new MyTrade();
            trade.NumberOrderParent = mtArr[1];

            if (string.IsNullOrEmpty(trade.NumberOrderParent) == true)
            {
                trade.NumberOrderParent = mtArr[2];
            }

            trade.SecurityNameCode = mtArr[5].Replace(" ", "");

            string date = mtArr[3];
            string time = mtArr[4];

            DateTime timeData = GetTimeFromStrings(date, time);
            trade.Time = timeData;

            decimal volume = mtArr[6].ToDecimal();
            decimal price = mtArr[7].ToDecimal();

            trade.Volume = volume;
            trade.Price = price;

            if (mtArr[8] == "0")
            {
                trade.Side = Side.Buy;
            }
            else
            {
                trade.Side = Side.Sell;
            }

            trade.NumberTrade = mtArr[9];

            if (MyTradeEvent != null)
            {
                MyTradeEvent(trade);
            }

            /*
            /////////////////////////////////////////////////////////////////////////
            ///TFtdcDirectionType是一个买卖方向类型
            /////////////////////////////////////////////////////////////////////////
            ///买
            #define THOST_FTDC_D_Buy '0'
            ///卖
            #define THOST_FTDC_D_Sell '1'

            typedef char TThostFtdcDirectionType;*/
        }

        private void LoadMd(string md)
        {
            /*
             * 
            InstrumentID);
            TradingDay);
            UpdateTime);

            LastPrice);
            Volume);

            BidPrice1);
            BidVolume1);

            AskPrice1);
            AskVolume1);
            */

            string[] str = md.Split('@');

            MarketDepth newMd = new MarketDepth();

            string date = str[2];
            string time = str[3];

            DateTime timeData = GetTimeFromStrings(date, time);

            // формируем трейд

            Trade newTrade = new Trade();
            newTrade.SecurityNameCode = str[1];
            newTrade.Time = timeData;
            newTrade.Price = str[4].ToDecimal();
            newTrade.Volume = str[5].ToDecimal();

            if (NewTradesEvent != null)
            {
                bool isSameTimeInArray = false;
                bool isInArray = false;

                for (int i = 0; i < _lastTrades.Count; i++)
                {
                    if (_lastTrades[i].SecurityNameCode == newTrade.SecurityNameCode)
                    {
                        isInArray = true;
                        if (_lastTrades[i].Time == newTrade.Time)
                        {
                            isSameTimeInArray = true;
                            break;
                        }

                        _lastTrades[i] = newTrade;
                        break;
                    }
                }

                if (isInArray == false)
                {
                    _lastTrades.Add(newTrade);
                }

                if (isSameTimeInArray == false)
                {
                    NewTradesEvent(newTrade);
                }
            }

            // формируем стакан

            newMd.SecurityNameCode = str[1];

            MarketDepthLevel b1 = GetBid(str[6], str[7]);

            MarketDepthLevel a1 = GetAsk(str[8], str[9]);

            if (b1.Price != 0
                && b1.Bid != 0)
            {
                newMd.Bids.Add(b1);
            }

            if (a1.Price != 0
                && a1.Ask != 0)
            {
                newMd.Asks.Add(a1);
            }

            if (newMd.Asks.Count != 0 ||
                newMd.Bids.Count != 0)
            {
                if (MarketDepthEvent != null)
                {
                    MarketDepthEvent(newMd);
                }
            }
        }

        private DateTime GetTimeFromStrings(string date, string time)
        {
            int year = 0;
            int month = 0;
            int day = 0;

            try
            {
                year = Convert.ToInt32(date.Substring(0, 4));
                month = Convert.ToInt32(date.Substring(4, 2));
                day = Convert.ToInt32(date.Substring(6, 2));
            }
            catch
            {
                DateTime now = DateTime.Now;
                year = now.Year;
                month = now.Month;
                day = now.Day;
            }

            // Time 3 "15:00:00"

            int hour = 0;
            int minute = 0;
            int second = 0;

            try
            {
                hour = Convert.ToInt32(time.Substring(0, 2));
                minute = Convert.ToInt32(time.Substring(3, 2));
                second = Convert.ToInt32(time.Substring(6, 2));
            }
            catch
            {
                DateTime now = DateTime.Now;
                hour = now.Hour;
                minute = now.Minute;
                second = now.Second;
            }

            DateTime timeData = new DateTime(year, month, day, hour, minute, second);

            return timeData;
        }

        private MarketDepthLevel GetBid(string price, string vol)
        {
            MarketDepthLevel level = new MarketDepthLevel();

            level.Bid = vol.ToDouble();
            level.Price = price.ToDouble();

            return level;
        }

        private MarketDepthLevel GetAsk(string price, string vol)
        {
            MarketDepthLevel level = new MarketDepthLevel();

            level.Ask = vol.ToDouble();
            level.Price = price.ToDouble();

            return level;
        }

        private List<Trade> _lastTrades = new List<Trade>();

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

        #endregion

        #region 10 Trade

        private RateGate rateGateSendOrder = new RateGate(1, TimeSpan.FromMilliseconds(200));

        private RateGate rateGateCancelOrder = new RateGate(1, TimeSpan.FromMilliseconds(200));

        public void SendOrder(Order order)
        {
            rateGateSendOrder.WaitToProceed();

            order.NumberMarket = order.NumberUser.ToString();

            string orderToTcp = "O@";

            bool isBuy = true;

            if (order.Side == Side.Sell)
            {
                isBuy = false;
            }

            orderToTcp += order.SecurityNameCode + "@";
            orderToTcp += isBuy + "@";
            orderToTcp += order.Price.ToString().Replace(",", ".") + "@";
            orderToTcp += order.Volume.ToString().Replace(",", ".") + "@";
            orderToTcp += order.NumberUser + "@";

            _messagesToSendTrade.Enqueue(orderToTcp);
        }

        public bool CancelOrder(Order order)
        {
            if (order.NumberUser == 0)
            {
                SendLogMessage("NumberUser is 0. Can`t cancel order", LogMessageType.Error);
            }

            rateGateCancelOrder.WaitToProceed();
            string orderToTcp = "R@";
            orderToTcp += order.NumberUser + "@";

            _messagesToSendTrade.Enqueue(orderToTcp);
            return true;
        }

        public void GetOrdersState(List<Order> orders)
        {

        }

        public void CancelAllOrders()
        {

        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            rateGateCancelOrder.WaitToProceed();

        }

        public void ResearchTradesToOrders(List<Order> orders)
        {

        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {

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

        #region 11 Log

        public event Action<string, LogMessageType> LogMessageEvent;

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent(message, messageType);
        }

        private void HandlerException(Exception exception)
        {
            if (exception is AggregateException)
            {
                AggregateException httpError = (AggregateException)exception;

                foreach (var item in httpError.InnerExceptions)

                {
                    if (item is NullReferenceException == false)
                    {
                        SendLogMessage(item.InnerException.Message + $" {exception.StackTrace}", LogMessageType.Error);
                    }

                }
            }
            else
            {
                if (exception is NullReferenceException == false)
                {
                    SendLogMessage(exception.Message + $" {exception.StackTrace}", LogMessageType.Error);
                }
            }
        }

        #endregion
    }
}