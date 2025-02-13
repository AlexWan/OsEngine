﻿/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.Transaq.TransaqEntity;
using RestSharp;
using RestSharp.Deserializers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Xml;
using System.Xml.Serialization;
using Candle = OsEngine.Entity.Candle;
using Order = OsEngine.Entity.Order;
using Security = OsEngine.Entity.Security;
using Trade = OsEngine.Entity.Trade;

namespace OsEngine.Market.Servers.Transaq
{
    public class TransaqServer : AServer
    {
        public TransaqServer()
        {
            ServerRealization = new TransaqServerRealization();

            CreateParameterString(OsLocalization.Market.Message63, "");
            CreateParameterPassword(OsLocalization.Market.Message64, "");
            CreateParameterString(OsLocalization.Market.Label41, "tr1.finam.ru");
            CreateParameterString(OsLocalization.Market.Message90, "3900");
            CreateParameterString(OsLocalization.Market.Message100, "6:50/23:50");
            CreateParameterBoolean(OsLocalization.Market.UseStock, true);
            CreateParameterBoolean(OsLocalization.Market.UseFutures, true);
            CreateParameterBoolean(OsLocalization.Market.UseCurrency, true);
            CreateParameterBoolean(OsLocalization.Market.UseOptions, false);
            CreateParameterBoolean(OsLocalization.Market.UseOther, false);
            CreateParameterButton(OsLocalization.Market.ButtonNameChangePassword);

            ServerParameters[4].Comment = OsLocalization.Market.Label160;
            ServerParameters[5].Comment = OsLocalization.Market.Label107;
            ServerParameters[6].Comment = OsLocalization.Market.Label107;
            ServerParameters[7].Comment = OsLocalization.Market.Label107;
            ServerParameters[8].Comment = OsLocalization.Market.Label107;
            ServerParameters[9].Comment = OsLocalization.Market.Label107;
            ServerParameters[10].Comment = OsLocalization.Market.Label105;
        }
    }

    public class TransaqServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public TransaqServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            _deserializer = new XmlDeserializer();
            _logPath = AppDomain.CurrentDomain.BaseDirectory + @"Engine\TransaqLog";

            DirectoryInfo dirInfo = new DirectoryInfo(_logPath);

            _myCallbackDelegate = new CallBackDelegate(CallBackDataHandler);
            SetCallback(_myCallbackDelegate);

            if (!dirInfo.Exists)
            {
                dirInfo.Create();
            }

            Thread worker = new Thread(CycleGettingPortfolios);
            worker.Name = "ThreadTransaqGetPortfolio";
            worker.Start();

            Thread worker2 = new Thread(ThreadPrivateDataParsingWorkPlace);
            worker2.Name = "ThreadTransaqDataParsing";
            worker2.Start();

            Thread worker3 = new Thread(ThreadTradesParsingWorkPlace);
            worker3.Name = "TransaqThreadTradesParsing";
            worker3.Start();

            Thread worker4 = new Thread(ThreadMarketDepthsParsingWorkPlace);
            worker4.Name = "TransaqThreadDepthsParsing";
            worker4.Start();

            Thread worker5 = new Thread(Converter);
            worker5.Name = "TransaqThreadConverter";
            worker5.Start();

            Thread worker6 = new Thread(ThreadUpdateAndSubscribeSecurity);
            worker6.Name = "TransaqThreadUpdateSecurity";
            worker6.Start();

            Thread worker7 = new Thread(ThreadHistoricalDataParsingWorkPlace);
            worker7.Name = "TransaqThreadUpdateHistoricalData";
            worker7.Start();

            Thread worker8 = new Thread(ThreadSecurityInfoParsingWorkPlace);
            worker8.Name = "TransaqThreadUpdateSecurityInfo";
            worker8.Start();

            
        }

        public ServerType ServerType
        {
            get { return ServerType.Transaq; }
        }

        public List<IServerParameter> ServerParameters { get; set; }

        public DateTime ServerTime { get; set; }

        public ServerConnectStatus ServerStatus { get; set; }

        private readonly string _logPath;

        delegate bool CallBackDelegate(IntPtr pData);

        private CallBackDelegate _myCallbackDelegate;

        public void Connect()
        {
            string time = ((ServerParameterString)ServerParameters[4]).Value;
            if (CheckConnectionTime(time) == false)
            {
                return;
            }

            string login = ((ServerParameterString)ServerParameters[0]).Value;
            string password = ((ServerParameterPassword)ServerParameters[1]).Value;
            string serverIp = ((ServerParameterString)ServerParameters[2]).Value;
            string serverPort = ((ServerParameterString)ServerParameters[3]).Value;

            _useStock = ((ServerParameterBool)ServerParameters[5]).Value;
            _useFutures = ((ServerParameterBool)ServerParameters[6]).Value;
            _useCurrency = ((ServerParameterBool)ServerParameters[7]).Value;
            _useOptions = ((ServerParameterBool)ServerParameters[8]).Value;
            _useOther = ((ServerParameterBool)ServerParameters[9]).Value;
            ServerParameterButton btn = ((ServerParameterButton)ServerParameters[10]);

            btn.UserClickButton += () => { ButtonClickChangePasswordWindowShow(); };

            try
            {
                _isLibraryInitialized = ConnectorInitialize();

                // formation of the command text / формирование текста команды
                string cmd = "<command id=\"connect\">";
                cmd = cmd + "<login>" + login + "</login>";
                cmd = cmd + "<password>" + password + "</password>";
                cmd = cmd + "<host>" + serverIp + "</host>";
                cmd = cmd + "<port>" + serverPort + "</port>";
                cmd = cmd + "<push_pos_equity>" + 3 + "</push_pos_equity>";
                cmd = cmd + "<rqdelay>100</rqdelay>";
                cmd = cmd + "</command>";

                // sending the command / отправка команды
                ConnectorSendCommand(cmd);
            }
            catch (Exception e)
            {
                SendLogMessage(e.ToString(), LogMessageType.Error);
            }
        }

        private bool CheckConnectionTime(string time)
        {
            string[] parts = time.Split('/');

            string[] from = parts[0].Split(':');
            string[] to = parts[1].Split(':');

            int hoursFrom, minutesFrom, hoursTo, minutesTo;

            if (!int.TryParse(from[0], out hoursFrom) || !int.TryParse(from[1], out minutesFrom)
                || !int.TryParse(to[0], out hoursTo) || !int.TryParse(to[1], out minutesTo))
            {
                SendLogMessage($"Данные содержат не числовое значение: {time}", LogMessageType.Error);
                return false;
            }

            if (hoursFrom < 0 || hoursFrom > 23 || hoursTo < 0 || hoursTo > 23 ||
                minutesFrom < 0 || minutesFrom > 59 || minutesTo < 0 || minutesTo > 59)
            {
                SendLogMessage($"Время подключения указано некорректно: {time}", LogMessageType.Error);
                return false;
            }

            TimeSpan connectionTimeFrom = new TimeSpan(hoursFrom, minutesFrom, 0);
            TimeSpan connectionTimeTo = new TimeSpan(hoursTo, minutesTo, 0);

            DateTime nowInMoscow = DateTime.Now.ToUniversalTime().AddHours(3);
            TimeSpan nowInMoscowTimeSpan = new TimeSpan(nowInMoscow.Hour, nowInMoscow.Minute, 0);

            if (connectionTimeTo < nowInMoscowTimeSpan)
            {
                bool isTimeWork = nowInMoscowTimeSpan >= connectionTimeTo || nowInMoscowTimeSpan < connectionTimeTo;
                return isTimeWork;
            }
            else
            {
                bool isTimeWork = nowInMoscowTimeSpan >= connectionTimeFrom && nowInMoscowTimeSpan < connectionTimeTo;
                return isTimeWork;
            }
        }

        /// <summary>
        /// Initializes the library: starts the callback queue processing thread
        /// Выполняет инициализацию библиотеки: запускает поток обработки очереди обратных вызовов
        /// </summary>
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        public bool ConnectorInitialize()
        {
            try
            {
                IntPtr pResult = Initialize(MarshalUtf8.StringToHGlobalUtf8(_logPath), 1);

                if (!pResult.Equals(IntPtr.Zero))
                {
                    MarshalUtf8.PtrToStringUtf8(pResult);

                    FreeMemory(pResult);

                    return false;
                }
                else
                {
                    FreeMemory(pResult);
                    return true;
                }
            }
            catch (Exception e)
            {
                SendLogMessage(e.ToString(), LogMessageType.Error);
                return false;
            }
        }

        /// <summary>
        /// disconnect to exchange
        /// разорвать соединение с биржей 
        /// </summary>
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        public void Disconnect()
        {
            try
            {
                // formation of the command text / формирование текста команды
                string cmd = "<command id=\"disconnect\">";
                cmd = cmd + "</command>";

                // sending the command / отправка команды
                ConnectorSendCommand(cmd);
            }
            catch (Exception e)
            {
                SendLogMessage(e.ToString(), LogMessageType.Error);
            }
        }

        private bool _isLibraryInitialized = false;

        public void Dispose()
        {
            try
            {
                Disconnected();

                if (_isLibraryInitialized == true)
                {
                    Disconnect();
                    Thread.Sleep(2000);
                    bool res = ConnectorUnInitialize();

                    _isLibraryInitialized = !res;
                }
            }
            catch (Exception e)
            {
                SendLogMessage(e.ToString(), LogMessageType.Error);
            }
            finally
            {
                _newsIsSubscribed = false;

                _depths?.Clear();

                _depths = null;

                _allCandleSeries?.Clear();

                _allTicks?.Clear();

                _newMessage = new ConcurrentQueue<string>();

                _transaqSecuritiesInString = new ConcurrentQueue<string>();

                _securities = new List<Security>();

                _secsSpecification = new List<SecurityInfo>();

                _subscribeSecurities = new List<Security>();

                _unsignedSecurities = new List<Security>();

                _mdQueue = new ConcurrentQueue<string>();

                _myTradesQueue = new ConcurrentQueue<string>();

                _tradesQueue = new ConcurrentQueue<string>();

                _ordersQueue = new ConcurrentQueue<string>();

                _portfoliosQueue = new ConcurrentQueue<string>();

                _positionsQueue = new ConcurrentQueue<string>();

                _clientLimitsQueue = new ConcurrentQueue<string>();

                _clientInfoQueue = new ConcurrentQueue<string>();

                _candlesQueue = new ConcurrentQueue<string>();

                _historicalTradesQueue = new ConcurrentQueue<string>();
            }
        }

        /// <summary>
        /// Shuts down the internal threads of the library, including completing thread queue callbacks
        /// Выполняет остановку внутренних потоков библиотеки, в том числе завершает поток обработки очереди обратных вызовов
        /// </summary>
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        public bool ConnectorUnInitialize()
        {
            try
            {
                IntPtr pResult = UnInitialize();

                if (!pResult.Equals(IntPtr.Zero))
                {
                    MarshalUtf8.PtrToStringUtf8(pResult);
                    FreeMemory(pResult);
                    return false;
                }
                else
                {
                    FreeMemory(pResult);
                    return true;
                }
            }
            catch (Exception e)
            {
                SendLogMessage(e.ToString(), LogMessageType.Error);
                return false;
            }
        }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties

        private bool _useStock = false;

        private bool _useFutures = false;

        private bool _useOptions = false;

        private bool _useCurrency = false;

        private bool _useOther = false;

        #endregion

        #region 3 Client to Transaq

        private void Connected()
        {
            SendLogMessage("Transaq client activated ", LogMessageType.System);

            if (ServerStatus != ServerConnectStatus.Connect)
            {
                ServerStatus = ServerConnectStatus.Connect;
                ConnectEvent?.Invoke();
            }
        }

        private void Disconnected()
        {
            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                SendLogMessage("Transaq client disconnected ", LogMessageType.System);

                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent?.Invoke();
            }
        }

        private void NeedChangePassword()
        {
            Application.Current.Dispatcher.Invoke((Action)delegate
            {
                string message = OsLocalization.Market.Message94;

                ChangeTransaqPassword changeTransaqPasswordWindow = new ChangeTransaqPassword(message, this);
                changeTransaqPasswordWindow.ShowDialog();
            });
        }

        private void ButtonClickChangePasswordWindowShow()
        {
            ChangeTransaqPassword changeTransaqPassword = new ChangeTransaqPassword(this);
            changeTransaqPassword.ShowDialog();
        }

        public void ChangePassword(string oldPassword, string newPassword, ChangeTransaqPassword window)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    window.TextInfo.Text = OsLocalization.Market.Label102;
                    return;
                }

                string cmd = $"<command id=\"change_pass\" oldpass=\"{oldPassword}\" newpass=\"{newPassword}\"/>";

                // sending command / отправка команды
                string res = ConnectorSendCommand(cmd);

                if (res == "<result success=\"true\"/>")
                {
                    ((ServerParameterPassword)ServerParameters[1]).Value = newPassword;
                    window.TextInfo.Text = OsLocalization.Market.Label103;
                }
                else
                {
                    window.TextInfo.Text = res;
                }

                Dispose();
            }
            catch (Exception ex)
            {
                window.TextInfo.Text = ex.ToString();
            }
        }

        #endregion

        #region 4 Securities

        public void GetSecurities() { }

        private List<Security> _securities = new List<Security>();

        private ConcurrentQueue<string> _transaqSecuritiesInString = new ConcurrentQueue<string>();

        private List<SecurityInfo> _secsSpecification = new List<SecurityInfo>();

        private void ThreadUpdateAndSubscribeSecurity()
        {
            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (!_transaqSecuritiesInString.IsEmpty)
                    {
                        while (true)
                        {
                            Thread.Sleep(500);
                            if (_lastUpdateSecurityArrayTime == DateTime.MinValue)
                            {
                                continue;
                            }
                            if (_lastUpdateSecurityArrayTime.AddSeconds(3) > DateTime.Now)
                            {
                                continue;
                            }

                            break;
                        }

                        bool isChangedSecurity = false;
                        while (_transaqSecuritiesInString.IsEmpty == false)
                        {
                            string curArray = null;

                            if (_transaqSecuritiesInString.TryDequeue(out curArray))
                            {
                                if (IsCreateSecurities(curArray))
                                {
                                    isChangedSecurity = true;
                                }
                            }
                        }

                        if (!isChangedSecurity)
                        {
                            continue;
                        }

                        _securities.RemoveAll(s => s == null);

                        if (_securities.Count == 0)
                        {
                            continue;
                        }

                        UpDateAllSpecifications();

                        SecurityEvent?.Invoke(_securities);

                        SendLogMessage("Securities count: " + _securities.Count, LogMessageType.System);
                    }
                    else if (_unsignedSecurities.Count != 0)
                    {
                        for (int i = 0; i < _unsignedSecurities.Count; i++)
                        {
                            Security security = _unsignedSecurities[i];

                            _unsignedSecurities.Remove(security);

                            Subscrible(security);
                        }
                    }

                    Thread.Sleep(10000);
                }
                catch(Exception ex)
                {
                    SendLogMessage("ThreadUpdateAndSubscribeSecurity error: " + ex.ToString(),LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void UpdateSecurity(SecurityInfo secs)
        {
            _lastUpdateSecurityArrayTime = DateTime.Now;

            bool isInArray = false;

            for (int i = 0; i < _secsSpecification.Count; i++)
            {
                if (_secsSpecification[i].Secid == secs.Secid)
                {
                    _secsSpecification[i] = secs;
                    isInArray = true;
                    break;
                }
            }

            if (isInArray == false)
            {
                _secsSpecification.Add(secs);
            }

            if (_securities == null ||
                _securities.Count == 0)
            {
                return;
            }

            UpDateAllSpecifications();
        }

        private void UpDateAllSpecifications()
        {
            if (_securities == null ||
                _securities.Count == 0)
            {
                return;
            }

            if (_secsSpecification == null
                || _secsSpecification.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _secsSpecification.Count; i++)
            {
                SecurityInfo secInfo = _secsSpecification[i];

                for (int j = 0; j < _securities.Count; j++)
                {
                    Security secCur = _securities[j];

                    if (secCur.NameId == secInfo.Secid)
                    {
                        if (string.IsNullOrEmpty(secInfo.Maxprice) == false)
                        {
                            secCur.PriceLimitHigh = secInfo.Maxprice.ToDecimal();
                        }

                        if (string.IsNullOrEmpty(secInfo.Minprice) == false)
                        {
                            secCur.PriceLimitLow = secInfo.Minprice.ToDecimal();
                        }

                        if (string.IsNullOrEmpty(secInfo.Buy_deposit) == false)
                        {
                            secCur.Go = secInfo.Buy_deposit.ToDecimal();
                        }

                        break;
                    }
                }
            }
        }

        private DateTime _lastUpdateSecurityArrayTime;

        private object _lockerCreateSecurities = new object();

        private bool IsCreateSecurities(string data)
        {
            lock (_lockerCreateSecurities)
            {
                List<TransaqEntity.Security> transaqSecurities = _deserializer.Deserialize<List<TransaqEntity.Security>>(new RestResponse() { Content = data }); ;

                bool isChangedSecurity = false;

                for (int i = 0; i < transaqSecurities.Count; i++)
                {
                    TransaqEntity.Security securityData = transaqSecurities[i];

                    try
                    {
                        if (!CheckFilter(securityData))
                        {
                            continue;
                        }

                        Security security = new Security();

                        security.Name = securityData.Seccode;
                        security.NameFull = securityData.Shortname;
                        security.NameClass = securityData.Board;
                        security.NameId = securityData.Secid;
                        security.Decimals = Convert.ToInt32(securityData.Decimals);
                        security.Exchange = securityData.Board;

                        if(securityData.Sectype == "FUT")
                        {
                            int countD = 0;

                            for(int i2 = 0;i2 < security.NameFull.Length;i2++)
                            {
                                if (security.NameFull[i2] == '-')
                                {
                                    countD++;
                                }
                            }

                            if(countD >= 2)
                            {
                                security.NameClass = "FUTSPREAD";
                            }
                        }

                        if (securityData.Sectype == "FUT")
                        {
                            security.SecurityType = SecurityType.Futures;
                        }
                        else if (securityData.Sectype == "SHARE")
                        {
                            security.SecurityType = SecurityType.Stock;
                        }
                        else if (securityData.Sectype == "OPT")
                        {
                            security.SecurityType = SecurityType.Option;
                        }
                        else if (securityData.Sectype == "BOND")
                        {
                            security.SecurityType = SecurityType.Bond;
                        }
                        else if (securityData.Sectype == "CURRENCY"
                            || securityData.Sectype == "CETS"
                            || security.NameClass == "CETS")
                        {
                            security.SecurityType = SecurityType.CurrencyPair;
                        }
                        else if (securityData.Sectype == "FUND")
                        {
                            security.SecurityType = SecurityType.Fund;
                        }
                        else if (security.NameClass == "MCT"
                           && (security.NameFull.Contains("call") || security.NameFull.Contains("put")))
                        {
                            security.NameClass = "MCT_put_call";
                            security.SecurityType = SecurityType.Option;
                        }
                        else if (security.NameClass == "MCT")
                        {
                            security.SecurityType = SecurityType.Futures;
                        }
                        else if (security.NameClass == "QUOTES")
                        {
                            // ignore
                        }
                        else if (security.NameClass == "DVP")
                        {
                            // ignore
                        }
                        else if (security.NameClass == "INDEXM"
                            || security.NameClass == "INDEXE"
                            || security.NameClass == "INDEXR")
                        {
                            security.SecurityType = SecurityType.Index;
                        }

                        if (security.SecurityType == SecurityType.None)
                        {
                            security.SecurityType = SecurityType.Bond;
                        }

                        security.Lot = securityData.Lotsize.ToDecimal();

                        if (security.Lot == 0)
                        {
                            security.Lot = 1;
                        }

                        security.PriceStep = securityData.Minstep.ToDecimal();

                        if (security.PriceStep == 0)
                        {
                            continue;
                        }

                        decimal pointCost;

                        try
                        {
                            pointCost = securityData.Point_cost.ToDecimal();
                        }
                        catch (Exception e)
                        {
                            decimal.TryParse(securityData.Point_cost, NumberStyles.Float, CultureInfo.InvariantCulture, out pointCost);
                        }

                        if (security.SecurityType == SecurityType.Futures
                        || security.SecurityType == SecurityType.Option)
                        {
                            if (security.PriceStep > 1)
                            {
                                security.PriceStepCost = security.PriceStep * pointCost / 100;
                            }
                            else
                            {
                                security.PriceStepCost = pointCost / 100;
                            }
                        }
                        else
                        {
                            security.PriceStepCost = security.PriceStep;
                        }

                        security.State = securityData.Active == "true" ? SecurityStateType.Activ : SecurityStateType.Close;

                        if (security.State != SecurityStateType.Activ)
                        {
                            continue;
                        }

                        if (_securities.Contains(security))
                        {
                            continue;
                        }

                        isChangedSecurity = true;
                        _securities.Add(security);

                    }
                    catch (Exception e)
                    {
                        SendLogMessage(e.Message, LogMessageType.Error);
                    }
                }

                if (isChangedSecurity)
                    return true;
                else
                    return false;
            }
        }

        private object _filterLocker = new object();

        private bool CheckFilter(TransaqEntity.Security security)
        {
            lock (_filterLocker)
            {
                if (security.Sectype == "SHARE")
                {
                    if (_useStock)
                    {
                        return true;
                    }
                    return false;
                }

                if (security.Sectype == "FUT")
                {
                    if (_useFutures)
                    {
                        return true;
                    }
                    return false;
                }

                if (security.Sectype == "OPT")
                {
                    if (_useOptions)
                    {
                        return true;
                    }
                    return false;
                }

                if (security.Sectype == "CETS" || security.Sectype == "CURRENCY")
                {
                    if (_useCurrency)
                    {
                        return true;
                    }
                    return false;
                }

                if (_useOther)
                {
                    return true;
                }

                return false;
            }
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 5 Portfolios

        public void GetPortfolios()
        {

        }

        private List<Portfolio> _portfolios;

        private void CycleGettingPortfolios()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(3000);

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        continue;
                    }

                    if (_clients == null || _clients.Count == 0)
                    {
                        continue;
                    }

                    for (int i = 0; i < _clients.Count; i++)
                    {
                        if (ServerStatus == ServerConnectStatus.Disconnect)
                        {
                            break;
                        }

                        Client client = _clients[i];

                        string command;

                        if (client.Type == "mct")
                        {
                            command = $"<command id=\"get_portfolio_mct\" client=\"{client.Id}\"/>";

                            string res = ConnectorSendCommand(command);

                            if (res != "<result success=\"true\"/>")
                            {
                                Thread.Sleep(5000);
                            }
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(client.Union))
                            {
                                command = $"<command id=\"get_mc_portfolio\" union=\"{client.Union}\" />";
                            }
                            else
                            {
                                command = $"<command id=\"get_mc_portfolio\" client=\"{client.Id}\" />";
                            }

                            string res = ConnectorSendCommand(command);

                            if (res != "<result success=\"true\"/>")
                            {
                                Thread.Sleep(5000);
                            }
                        }

                        if (string.IsNullOrEmpty(client.Union) && !string.IsNullOrEmpty(client.Forts_acc))
                        {
                            command = $"<command id=\"get_client_limits\" client=\"{client.Id}\"/>";
                            string res = ConnectorSendCommand(command);

                            if (res != "<result success=\"true\"/>")
                            {
                                Thread.Sleep(5000);
                            }
                        }
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage("CycleGettingPortfolios error: " + error.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private List<Client> _clients;

        private void ClientsInfoUpdate(Client clientInfo)
        {
            try
            {
                if (_clients == null)
                {
                    _clients = new List<Client>();
                }

                if (!string.IsNullOrEmpty(clientInfo.Union))
                {
                    Client needClient = _clients.Find(c => string.IsNullOrEmpty(clientInfo.Union));

                    if (needClient == null)
                    {
                        _clients.Add(clientInfo);
                    }
                }
                else
                {
                    _clients.Add(clientInfo);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateClientPortfolio(string portfolio)
        {
            try
            {
                if (_portfolios == null)
                {
                    _portfolios = new List<Portfolio>();
                }

                Portfolio unitedPortfolio = ParsePortfolio(portfolio);

                Portfolio needPortfolio = _portfolios.Find(p => p.Number == unitedPortfolio.Number);

                if (needPortfolio != null)
                {
                    _portfolios.Remove(needPortfolio);
                }
                _portfolios.Add(unitedPortfolio);

                PortfolioEvent?.Invoke(_portfolios);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private Portfolio ParsePortfolio(string data)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(data);

            XmlElement root = doc.DocumentElement;

            if (root == null)
            {
                return null;
            }

            string union = root.GetAttribute("union");
            string client = root.GetAttribute("client");

            XmlNode openEquity = root.SelectSingleNode("open_equity");
            XmlNode equity = root.SelectSingleNode("equity");
            XmlNode block = root.SelectSingleNode("go");
            XmlNode cover = root.SelectSingleNode("cover");
            XmlNode pnl = root.SelectSingleNode("unrealized_pnl");

            XmlNodeList allSecurity = root.GetElementsByTagName("security");

            Portfolio portfolio = new Portfolio();

            if (openEquity != null)
            {
                portfolio.ValueBegin = openEquity.InnerText.ToDecimal();
            }
            if (equity != null)
            {
                portfolio.ValueCurrent = equity.InnerText.ToDecimal();
            }
            if (equity != null
                && cover != null
                && block != null)
            {
                portfolio.ValueBlocked =
                    (equity.InnerText.ToDecimal()
                    - cover.InnerText.ToDecimal())
                    + block.InnerText.ToDecimal();
            }

            if(pnl != null)
            {
                portfolio.UnrealizedPnl = pnl.InnerText.ToDecimal();
            }

            if (!string.IsNullOrEmpty(union))
            {
                portfolio.Number = "United_" + union;
            }
            else
            {
                portfolio.Number = client;
            }

            for (int i = 0; i < allSecurity.Count; i++)
            {
                XmlNode node = (XmlNode)allSecurity[i];

                PositionOnBoard pos = new PositionOnBoard();

                pos.SecurityNameCode = node.SelectSingleNode("seccode")?.InnerText;
                pos.PortfolioName = portfolio.Number;

                XmlNode beginNode = node.SelectSingleNode("open_balance");
                XmlNode buyNode = node.SelectSingleNode("bought");
                XmlNode sellNode = node.SelectSingleNode("sold");
                XmlNode pnlPos = node.SelectSingleNode("unrealized_pnl");

                decimal lot = 1;

                for (int j = 0; j < _securities.Count; j++)
                {
                    if (pos.SecurityNameCode == _securities[j].Name)
                    {
                        lot = _securities[j].Lot;
                    }
                }

                if (beginNode != null)
                {
                    pos.ValueBegin = beginNode.InnerText.ToDecimal() / lot;
                }

                if(pnlPos != null)
                {
                    pos.UnrealizedPnl = pnlPos.InnerText.ToDecimal();
                }

                if (buyNode != null &&
                    sellNode != null)
                {
                    pos.ValueCurrent = pos.ValueBegin + buyNode.InnerText.ToDecimal() / lot - sellNode.InnerText.ToDecimal() / lot;
                }

                portfolio.SetNewPosition(pos);
            }

            return portfolio;
        }

        private void UpdateClientLimits(ClientLimits clientLimits)
        {
            try
            {
                if (_portfolios == null)
                {
                    return;
                }

                Portfolio needPortfolio = _portfolios.Find(p => p.Number == clientLimits.Client);

                if (needPortfolio != null)
                {
                    InitPortfolio(needPortfolio, clientLimits);
                }

                PortfolioEvent?.Invoke(_portfolios);

            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private Portfolio InitPortfolio(Portfolio portfolio, ClientLimits clientLimits)
        {
            portfolio.ValueBegin = clientLimits.MoneyCurrent.ToDecimal();
            portfolio.ValueCurrent = clientLimits.MoneyFree.ToDecimal();
            portfolio.ValueBlocked = clientLimits.MoneyReserve.ToDecimal();
            portfolio.UnrealizedPnl = clientLimits.Profit.ToDecimal();

            return portfolio;
        }

        private void UpdateClientPositions(TransaqPositions transaqPositions)
        {
            if (_portfolios == null)
            {
                _portfolios = new List<Portfolio>();
            }

            if (transaqPositions.Forts_position.Count == 0)
            {
                for (int i = 0; i < _portfolios.Count; i++)
                {
                    Portfolio portfolio = _portfolios[i];
                    portfolio.ClearPositionOnBoard();
                }
            }
            else
            {
                for (int i = 0; i < transaqPositions.Forts_position.Count; i++)
                {
                    Forts_position fortsPosition = transaqPositions.Forts_position[i];
                    Portfolio needPortfolio = _portfolios.Find(p => p.Number == fortsPosition.Client);

                    if (needPortfolio != null)
                    {
                        PositionOnBoard pos = new PositionOnBoard()
                        {
                            SecurityNameCode = fortsPosition.Seccode,
                            ValueBegin = fortsPosition.Startnet.ToDecimal(),
                            ValueCurrent = fortsPosition.Totalnet.ToDecimal(),
                            ValueBlocked = fortsPosition.Openbuys.ToDecimal() +
                                           fortsPosition.Opensells.ToDecimal(),
                            PortfolioName = needPortfolio.Number,

                        };
                        needPortfolio.SetNewPosition(pos);
                    }
                }
            }

            PortfolioEvent?.Invoke(_portfolios);
        }

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 6 Data

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            List<Trade> trades = new List<Trade>();

            string cmd = "<command id=\"subscribe_ticks\">";
            cmd += "<security>";

            string board = security.NameClass;

            if (board == "FUTSPREAD")
            {
                board = "FUT";
            }

            cmd += "<board>" + board + "</board>";
            cmd += "<seccode>" + security.Name + "</seccode>";
            cmd += "<tradeno>1</tradeno>";
            cmd += "</security>";
            cmd += "<filter>true</filter>";
            cmd += "</command>";

            string res = ConnectorSendCommand(cmd);

            if (res != "<result success=\"true\"/>")
            {
                SendLogMessage("GetTickDataToSecurity method error " + res, LogMessageType.Error);
                return null;
            }

            DateTime lastTickTime = DateTime.MinValue;
            List<Tick> ticks = new List<Tick>();

            while (true)
            {
                try
                {
                    ticks = _allTicks.Where(x => x.Seccode == security.Name).ToList();
                    if (ticks.Count > 0)
                    {
                        lastTickTime = DateTime.Parse(ticks.Last().Tradetime);
                    }
                }
                catch
                {

                }

                if (lastTickTime >= actualTime)
                {
                    for (int i = 0; i < ticks.Count; i++)
                    {
                        Tick tick = ticks[i];
                        trades.Add(new Trade()
                        {
                            SecurityNameCode = tick.Seccode,
                            Id = tick.Tradeno,
                            Price = tick.Price.ToDecimal(),
                            Side = tick.Buysell == "B" ? Side.Buy : Side.Sell,
                            Volume = tick.Quantity.ToDecimal(),
                            Time = DateTime.Parse(tick.Tradetime),
                        });
                    }

                    string cmd_uns = "<command id=\"subscribe_ticks\">";
                    cmd_uns += "<filter>true</filter>";
                    cmd_uns += "</command>";

                    string res2 = ConnectorSendCommand(cmd_uns);

                    _allTicks.RemoveAll(x => x.Seccode == security.Name);

                    if (res2 != "<result success=\"true\"/>")
                    {
                        SendLogMessage("GetTickDataToSecurity method error 2 " + res2, LogMessageType.Error);
                    }

                    break;
                }

                Thread.Sleep(300);
            }

            return trades;
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime,
            DateTime actualTime)
        {
            return null;
        }

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            _rateGateCandle.WaitToProceed();

            try
            {
                int newTf;
                int oldTf;
                string needPeriodId = GetNeedIdPeriod(timeFrameBuilder.TimeFrame, out newTf, out oldTf);

                string cmd = "<command id=\"gethistorydata\">";
                cmd += "<security>";

                string board = security.NameClass;

                if (board == "FUTSPREAD")
                {
                    board = "FUT";
                }

                cmd += "<board>" + board + "</board>";
                cmd += "<seccode>" + security.Name + "</seccode>";
                cmd += "</security>";
                cmd += "<period>" + needPeriodId + "</period>";
                cmd += "<count>" + candleCount + "</count>";
                cmd += "<reset>" + "true" + "</reset>";
                cmd += "</command>";

                // sending command / отправка команды
                string res = ConnectorSendCommand(cmd);

                if (res != "<result success=\"true\"/>")
                {
                    SendLogMessage(OsLocalization.Market.Message95 + "  " + res, LogMessageType.Error);
                }

                DateTime startLoadingTime = DateTime.Now;

                while (startLoadingTime.AddSeconds(20) > DateTime.Now)
                {
                    TransaqEntity.Candles candles = null;

                    for (int i = 0; i < _allCandleSeries.Count; i++)
                    {
                        TransaqEntity.Candles curSeries = _allCandleSeries[i];

                        if (curSeries.Seccode == security.Name && curSeries.Period == needPeriodId)
                        {
                            candles = curSeries;
                            break;
                        }
                    }

                    if (candles == null)
                    {
                        Thread.Sleep(200);
                        continue;
                    }

                    List<Candle> donorCandles = ParseCandles(candles);
                    List<Candle> newCandle = new List<Candle>();

                    if ((timeFrameBuilder.TimeFrame == TimeFrame.Min1 && needPeriodId == "1") ||
                        (timeFrameBuilder.TimeFrame == TimeFrame.Min5 && needPeriodId == "2") ||
                        (timeFrameBuilder.TimeFrame == TimeFrame.Min15 && needPeriodId == "3") ||
                        (timeFrameBuilder.TimeFrame == TimeFrame.Hour1 && needPeriodId == "4"))
                    {
                        newCandle = donorCandles;
                    }
                    else
                    {
                        newCandle = BuildCandles(donorCandles, newTf, oldTf);
                    }

                    for (int i = 0; newCandle != null && i < newCandle.Count; i++)
                    {
                        if (newCandle[i] == null)
                        {
                            newCandle.RemoveAt(i);
                            i--;
                        }
                    }

                    for (int i = 1; newCandle != null && i < newCandle.Count; i++)
                    {
                        if (newCandle[i - 1].TimeStart == newCandle[i].TimeStart)
                        {
                            newCandle.RemoveAt(i);
                            i--;
                        }
                    }

                    for (int i = 0; newCandle != null && i < newCandle.Count; i++)
                    {
                        if (newCandle[i].Open == 0
                        || newCandle[i].High == 0
                        || newCandle[i].Low == 0
                            || newCandle[i].Close == 0)
                        {
                            newCandle.RemoveAt(i);
                            i--;
                        }
                    }

                    return newCandle;

                }

                SendLogMessage($"No candle data was received for the security {security.Name}", LogMessageType.Error);

                return null;
            }
            catch (Exception ex)
            {
                SendLogMessage("Error GetCandles  " + ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private RateGate _rateGateCandle = new RateGate(1, TimeSpan.FromMilliseconds(300));

        private List<Candle> ParseCandles(TransaqEntity.Candles candles)
        {
            try
            {
                List<Candle> osCandles = new List<Candle>();

                for (int i = 0; i < candles.Candle.Count; i++)
                {
                    Candle osCandle = new Candle();
                    osCandle.Open = candles.Candle[i].Open.ToDecimal();
                    osCandle.High = candles.Candle[i].High.ToDecimal();
                    osCandle.Low = candles.Candle[i].Low.ToDecimal();
                    osCandle.Close = candles.Candle[i].Close.ToDecimal();
                    osCandle.Volume = candles.Candle[i].Volume.ToDecimal();
                    osCandle.TimeStart = DateTime.Parse(candles.Candle[i].Date);

                    osCandles.Add(osCandle);
                }

                return osCandles;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        private List<Candle> BuildCandles(List<Candle> oldCandles, int needTf, int oldTf)
        {
            List<Candle> newCandles = new List<Candle>();

            if (oldCandles == null ||
                oldCandles.Count == 0)
            {
                return newCandles;
            }

            int index;

            if (needTf == 120)
            {
                index = oldCandles.FindIndex(can => can.TimeStart.Hour % 2 == 0);
            }
            else if (needTf == 1440)
            {
                index = oldCandles.FindIndex(can => can.TimeStart.Hour == 10 &&
                                                    can.TimeStart.Minute == 0 &&
                                                    can.TimeStart.Second == 0);

                for (int i = index; i < oldCandles.Count; i++)
                {
                    if (i == 497)
                    {

                    }

                    if (oldCandles[i].TimeStart.Hour == 10 &&
                        oldCandles[i].TimeStart.Minute == 0 &&
                        oldCandles[i].TimeStart.Second == 0)
                    {
                        if (newCandles.Count != 0)
                        {
                            newCandles[newCandles.Count - 1].State = CandleState.Finished;
                        }
                        newCandles.Add(new Candle());
                        newCandles[newCandles.Count - 1].State = CandleState.None;
                        newCandles[newCandles.Count - 1].Open = oldCandles[i].Open;
                        newCandles[newCandles.Count - 1].TimeStart = oldCandles[i].TimeStart;
                        newCandles[newCandles.Count - 1].Low = Decimal.MaxValue;
                    }

                    if (newCandles.Count == 0)
                    {
                        continue;
                    }

                    newCandles[newCandles.Count - 1].High = oldCandles[i].High > newCandles[newCandles.Count - 1].High
                        ? oldCandles[i].High
                        : newCandles[newCandles.Count - 1].High;

                    newCandles[newCandles.Count - 1].Low = oldCandles[i].Low < newCandles[newCandles.Count - 1].Low
                        ? oldCandles[i].Low
                        : newCandles[newCandles.Count - 1].Low;

                    newCandles[newCandles.Count - 1].Close = oldCandles[i].Close;

                    newCandles[newCandles.Count - 1].Volume += oldCandles[i].Volume;

                }

                return newCandles;
            }
            else
            {
                index = oldCandles.FindIndex(can => can.TimeStart.Minute % needTf == 0);
            }

            if (index < 0)
            {
                index = 0;
            }

            int count = needTf / oldTf;

            int counter = 0;

            Candle newCandle = new Candle();

            for (int i = index; i < oldCandles.Count; i++)
            {
                if (i == 497)
                {

                }

                counter++;

                if (counter == 1)
                {
                    newCandle = new Candle();
                    newCandle.Open = oldCandles[i].Open;
                    newCandle.TimeStart = oldCandles[i].TimeStart;

                    if (needTf <= 60 && newCandle.TimeStart.Minute % needTf != 0)  //AVP, если свечка пришла в некратное ТФ время, например, был пропуск свечи, то ТФ правим на кратное. на MOEX  в пропущенные на клиринге свечках, на 10 минутках давало сбой - сдвиг свечек на 5 минут.
                    {
                        newCandle.TimeStart = newCandle.TimeStart.AddMinutes((newCandle.TimeStart.Minute % needTf) * -1);
                    }
                    newCandle.Low = Decimal.MaxValue;
                }

                newCandle.High = oldCandles[i].High > newCandle.High
                    ? oldCandles[i].High
                    : newCandle.High;

                newCandle.Low = oldCandles[i].Low < newCandle.Low
                    ? oldCandles[i].Low
                    : newCandle.Low;

                newCandle.Volume += oldCandles[i].Volume;

                if (counter == count || (needTf <= 60 && i < oldCandles.Count - 2 && oldCandles[i + 1].TimeStart.Minute % needTf == 0))    // AVP добавил проверку "или", что следующая свечка в мелком ТФ, должна войти в следующую свечу более крупного ТФ
                {
                    newCandle.Close = oldCandles[i].Close;
                    newCandle.State = CandleState.Finished;
                    newCandles.Add(newCandle);
                    counter = 0;
                }

                if (i == oldCandles.Count - 1 && counter != count)
                {
                    newCandle.Close = oldCandles[i].Close;
                    newCandle.State = CandleState.Started;
                    newCandles.Add(newCandle);
                }
            }

            return newCandles;
        }

        private string GetNeedIdPeriod(TimeFrame tf, out int newTf, out int oldTf)
        {
            switch (tf)
            {
                case TimeFrame.Min1:
                    newTf = 1;
                    oldTf = 1;
                    return "1";
                case TimeFrame.Min2:
                    newTf = 2;
                    oldTf = 1;
                    return "1";
                case TimeFrame.Min3:
                    newTf = 3;
                    oldTf = 1;
                    return "1";
                case TimeFrame.Min5:
                    newTf = 5;
                    oldTf = 5;
                    return "2";
                case TimeFrame.Min10:
                    newTf = 10;
                    oldTf = 5;
                    return "2";
                case TimeFrame.Min20:
                    newTf = 20;
                    oldTf = 5;
                    return "2";
                case TimeFrame.Min15:
                    newTf = 15;
                    oldTf = 15;
                    return "3";
                case TimeFrame.Min30:
                    newTf = 30;
                    oldTf = 15;
                    return "3";
                case TimeFrame.Min45:
                    newTf = 45;
                    oldTf = 15;
                    return "3";
                case TimeFrame.Hour1:
                    newTf = 60;
                    oldTf = 60;
                    return "4";
                case TimeFrame.Hour2:
                    newTf = 120;
                    oldTf = 60;
                    return "4";
                case TimeFrame.Day:
                    newTf = 1440;
                    oldTf = 60;
                    return "4";
                case TimeFrame.Hour4:
                    newTf = 240;
                    oldTf = 60;
                    return "4";
                default:
                    newTf = 0;
                    oldTf = 0;
                    return "5";
            }
        }

        private List<TransaqEntity.Candles> _allCandleSeries = new List<TransaqEntity.Candles>();

        private List<Tick> _allTicks = new List<Tick>();

        #endregion

        #region 7 Security subscrible

        private RateGate _rateGateSubscribe = new RateGate(1, TimeSpan.FromMilliseconds(300));

        private List<Security> _subscribeSecurities = new List<Security>();

        private List<Security> _unsignedSecurities = new List<Security>();

        public void Subscrible(Security security)
        {
            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                return;
            }

            for (int i = 0; i < _subscribeSecurities.Count; i++)
            {
                if (_subscribeSecurities[i].Name == security.Name
                    && _subscribeSecurities[i].NameId == security.NameId
                     && _subscribeSecurities[i].NameFull == security.NameFull
                    && _subscribeSecurities[i].NameClass == security.NameClass)
                {
                    return;
                }
            }

            if (CheckSecurityAvailability(security))
            {
                SubscribeRecursion(security, 1);
                return;
            }

            SendLogMessage($"Subscription to security {security.Name} not implemented. Repeat attempt.", LogMessageType.Error);

            _unsignedSecurities.Add(security);
        }

        private void SubscribeRecursion(Security security, int counter)
        {
            _rateGateSubscribe.WaitToProceed();

            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                return;
            }

            string board = security.NameClass;

            if(board == "FUTSPREAD")
            {
                board = "FUT";
            }

            string cmd = "<command id=\"subscribe\">";
            cmd += "<alltrades>";
            cmd += "<security>";
            cmd += "<board>" + board + "</board>";
            cmd += "<seccode>" + security.Name + "</seccode>";
            cmd += "</security>";
            cmd += "</alltrades>";
            cmd += "<quotes>";
            cmd += "<security>";
            cmd += "<board>" + board + "</board>";
            cmd += "<seccode>" + security.Name + "</seccode>";
            cmd += "</security>";
            cmd += "</quotes>";
            cmd += "</command>";

            // sending command / отправка команды
            string res = ConnectorSendCommand(cmd);

            if (res != "<result success=\"true\"/>")
            {
                if (counter >= 3)
                {
                    SendLogMessage("Subscrible security error " + security.Name + "   " + res, LogMessageType.Error);
                    return;
                }
                else
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        return;
                    }

                    counter++;
                    SubscribeRecursion(security, counter);
                }
            }

            _subscribeSecurities.Add(security);
        }

        public bool SubscribeNews()
        {
            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                return false;
            }

            _newsIsSubscribed = true;

            return true;
        }

        private bool _newsIsSubscribed = false;

        public event Action<News> NewsEvent;

        #endregion

        #region 8 Trade

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        public void SendOrder(Order order)
        {
            try
            {
                string side = order.Side == Side.Buy ? "B" : "S";

                Security needSec = _securities.Find(
                    s => s.Name == order.SecurityNameCode &&
                    s.NameClass == order.SecurityClassCode);


                if (needSec == null)
                {
                    needSec = _securities.Find(
                    s => s.Name == order.SecurityNameCode);
                }

                string cmd = "<command id=\"neworder\">";
                cmd += "<security>";

                string board = needSec.NameClass;

                if (board == "FUTSPREAD")
                {
                    board = "FUT";
                }

                cmd += "<board>" + board + "</board>";
                cmd += "<seccode>" + needSec.Name + "</seccode>";
                cmd += "</security>";

                if (order.PortfolioNumber.StartsWith("United_"))
                {
                    string union = order.PortfolioNumber.Split('_')[1];
                    cmd += "<union>" + union + "</union>";
                }
                else
                {
                    cmd += "<client>" + order.PortfolioNumber + "</client>";
                }
                if (order.TypeOrder == OrderPriceType.Limit)
                {
                    cmd += "<price>" + order.Price.ToString().Replace(',', '.') + "</price>";
                }
                else if (order.TypeOrder == OrderPriceType.Market)
                {
                    cmd += "<bymarket/>";
                }

                string volume = order.Volume.ToString();

                volume = volume.Replace(",0", "");
                volume = volume.Replace(".0", "");

                cmd += "<quantity>" + volume + "</quantity>";
                cmd += "<buysell>" + side + "</buysell>";
                cmd += "<brokerref>" + order.NumberUser + "</brokerref>";
                cmd += "<unfilled> PutInQueue </unfilled>";

                order.Comment = order.NumberUser.ToString();

                if (needSec.NameClass == "TQBR")
                {
                    cmd += "<usecredit> true </usecredit>";
                }

                if (needSec.NameClass == "FUT")
                {
                    cmd += "<brokerref>" + order.NumberUser + "</brokerref>";
                }

                cmd += "</command>";

                lock (_sendOrdersLocker)
                {
                    _sendOrders.Add(order);
                    if (_sendOrders.Count > 500)
                    {
                        _sendOrders.RemoveAt(0);
                    }
                }

                // sending command / отправка команды
                string res = ConnectorSendCommand(cmd);

                if (res == null)
                {
                    order.State = OrderStateType.Fail;
                    SendLogMessage("SendOrderFall. Order num: " + order.NumberUser, LogMessageType.Error);
                }

                Result result = Deserialize<Result>(res);

                if (!result.Success)
                {
                    order.State = OrderStateType.Fail;
                    SendLogMessage("SendOrderFall" + result.Message, LogMessageType.Error);

                    if (MyOrderEvent != null)
                    {
                        MyOrderEvent(order);
                    }
                }
                else
                {
                    order.NumberUser = result.TransactionId;
                }

                order.TimeCallBack = ServerTime;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private List<Order> _sendOrders = new List<Order>();

        private string _sendOrdersLocker = "sendOrdersLocker";

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        public void CancelOrder(Order order)
        {
            try
            {
                string cmd = "<command id=\"cancelorder\">";
                cmd += "<transactionid>" + order.NumberUser + "</transactionid>";
                cmd += "</command>";

                // отправка команды
                string res = ConnectorSendCommand(cmd);

                if (!res.StartsWith("<result success=\"true\""))
                {
                    SendLogMessage("CancelOrder method error " + res, LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private RateGate _rateGateChangePriceOrder = new RateGate(1, TimeSpan.FromMilliseconds(200));

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
            try
            {
                _rateGateChangePriceOrder.WaitToProceed();

                string cmd = "<command id=\"moveorder\">";
                cmd += "<transactionid>" + order.NumberUser + "</transactionid>";
                cmd += "<price>" + newPrice.ToString().Replace(',', '.') + "</price>";
                cmd += "<moveflag>" + 0 + "</moveflag>";
                cmd += "</command>";

                // sending command / отправка команды
                string res = ConnectorSendCommand(cmd);

                if (res == null)
                {
                    order.State = OrderStateType.Fail;
                    SendLogMessage("SendOrderFall. Order num: " + order.NumberUser, LogMessageType.Error);
                }

                Result result = Deserialize<Result>(res);

                if (!result.Success)
                {
                    order.State = OrderStateType.Fail;
                    SendLogMessage("ChangeOrderFall" + result.Message, LogMessageType.Error);

                    if (MyOrderEvent != null)
                    {
                        MyOrderEvent(order);
                    }
                }
                else
                {
                    order.NumberUser = result.TransactionId;
                }

                order.TimeCallBack = ServerTime;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        public void CancelAllOrders()
        {

        }

        public void GetOrdersState(List<Order> orders)
        {

        }

        public void ResearchTradesToOrders(List<Order> orders)
        {

        }

        public void CancelAllOrdersToSecurity(Security security)
        {

        }

        public void GetAllActivOrders()
        {

        }

        public void GetOrderStatus(Order order)
        {

        }

        #endregion

        #region 9 Parsing incomig data

        private ConcurrentQueue<string> _newMessage = new ConcurrentQueue<string>();

        /// <summary>
        /// processor of data from callbacks 
        /// обработчик данных пришедших через каллбек
        /// </summary>
        /// <param name="pData">data from Transaq / данные, поступившие от транзака</param>
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        private bool CallBackDataHandler(IntPtr pData)
        {
            try
            {

                string data = MarshalUtf8.PtrToStringUtf8(pData);
                _newMessage.Enqueue(data);
                FreeMemory(pData);

                return true;
            }
            catch (Exception e)
            {
                SendLogMessage(e.ToString(), LogMessageType.Error);
                return false;
            }
        }

        /// <summary>
        /// takes messages from the shared queue, converts them to C# classes, and sends them to up
        /// берет сообщения из общей очереди, конвертирует их в классы C# и отправляет на верх
        /// </summary>
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        private void Converter()
        {
            while (true)
            {
                try
                {
                    if (!_newMessage.IsEmpty)
                    {
                        string data;

                        if (_newMessage.TryDequeue(out data))
                        {
                            // тяжёлые данные переносим в другую очередь разбирающуюся другим потоком
                            if (data.StartsWith("<quotes>"))
                            {
                                _mdQueue.Enqueue(data);
                            }
                            else if (data.StartsWith("<alltrades>"))
                            {
                                _tradesQueue.Enqueue(data);
                            }
                            else if (data.StartsWith("<ticks>"))
                            {
                                _historicalTradesQueue.Enqueue(data);
                            }
                            else if (data.StartsWith("<pits>"))
                            {
                                continue;
                            }
                            else if (data.StartsWith("<orders>"))
                            {
                                _ordersQueue.Enqueue(data);
                            }
                            else if (data.StartsWith("<mc_portfolio"))
                            {
                                _portfoliosQueue.Enqueue(data);
                            }
                            else if (data.StartsWith("<positions"))
                            {
                                _positionsQueue.Enqueue(data);
                            }
                            else if (data.StartsWith("<trades>"))
                            {
                                _myTradesQueue.Enqueue(data);
                            }
                            else if (data.StartsWith("<sec_info_upd>"))
                            {
                                _securityInfoQueue.Enqueue(data);
                            }
                            else if (data.StartsWith("<securities>"))
                            {
                                _transaqSecuritiesInString.Enqueue(data);
                                _lastUpdateSecurityArrayTime = DateTime.Now;

                            }
                            else if (data.StartsWith("<clientlimits"))
                            {
                                _clientLimitsQueue.Enqueue(data);
                            }
                            else if (data.StartsWith("<client"))
                            {
                                _clientInfoQueue.Enqueue(data);
                            }
                            else if (data.StartsWith("<candles"))
                            {
                                _candlesQueue.Enqueue(data);
                            }
                            else if (data.StartsWith("<candlekinds>"))
                            {

                            }
                            else if (data.StartsWith("<messages>"))
                            {
                                if (data.Contains("Время действия Вашего пароля истекло"))
                                {
                                    NeedChangePassword();

                                    Disconnected();
                                }
                            }
                            else if (data.StartsWith("<server_status"))
                            {
                                ServerStatus status = Deserialize<ServerStatus>(data);

                                if (status.Connected == "true")
                                {
                                    if (ServerStatus == ServerConnectStatus.Disconnect
                                        && data.Contains("recover=\"true\"") == false)
                                    {
                                        Connected();
                                    }
                                    else
                                    {
                                        if (data.Contains("recover=\"true\""))
                                        {
                                            SendLogMessage("Transaq client status error: <Reconnect>", LogMessageType.Error);

                                            Disconnected();
                                        }
                                    }
                                }
                                else if (status.Connected == "false")
                                {
                                    Disconnected();
                                }
                                else if (status.Connected == "error")
                                {
                                    SendLogMessage("Transaq client status error: " + status.Text, LogMessageType.Error);
                                    Disconnected();
                                }
                            }
                            else if (data.StartsWith("<error>"))
                            {
                                SendLogMessage(data, LogMessageType.Error);
                            }
                            else if (data.StartsWith("<news_header>"))
                            {
                                if(_newsIsSubscribed)
                                {
                                    _newsIdQueue.Enqueue(data);
                                }
                            }
                            else if (data.StartsWith("<news_body>"))
                            {
                                if (_newsIsSubscribed)
                                {
                                    _newsBodyQueue.Enqueue(data);
                                }
                            }
                            else if (data.StartsWith("<markets>")
                                || data.StartsWith("<boards>")
                                || data.StartsWith("<portfolio_mct client")
                                || data.StartsWith("<overnight status=")
                                 || data.StartsWith("<union id=")
                                || data.StartsWith("<candlekinds>"))
                            {
                                // do nothin
                            }
                            else
                            {
                                SendLogMessage(data, LogMessageType.Error);
                            }
                        }
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private ConcurrentQueue<string> _ordersQueue = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _myTradesQueue = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _tradesQueue = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _mdQueue = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _portfoliosQueue = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _positionsQueue = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _clientLimitsQueue = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _clientInfoQueue = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _candlesQueue = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _historicalTradesQueue = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _securityInfoQueue = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _newsIdQueue = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _newsBodyQueue = new ConcurrentQueue<string>();

        private List<TransaqNews> _news = new List<TransaqNews>();

        private void ThreadHistoricalDataParsingWorkPlace()
        {
            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (_candlesQueue.IsEmpty == false)
                    {
                        string data = null;

                        if (_candlesQueue.TryDequeue(out data))
                        {
                            TransaqEntity.Candles newCandles = Deserialize<TransaqEntity.Candles>(data);

                            _allCandleSeries.Add(newCandles);
                        }
                    }
                    else if(_historicalTradesQueue.IsEmpty == false)
                    {
                        string data = null;

                        if (_historicalTradesQueue.TryDequeue(out data))
                        {
                            List<Tick> newTicks = _deserializer.Deserialize<List<Tick>>(new RestResponse() { Content = data });

                            _allTicks.AddRange(newTicks);
                        }
                    }
                    else if (_newsIdQueue.IsEmpty == false)
                    {
                        string data = null;

                        if (_newsIdQueue.TryDequeue(out data))
                        {
                            TransaqNews newsTransaq = _deserializer.Deserialize<TransaqNews>(new RestResponse() { Content = data });

                            _news.Add(newsTransaq);

                            if(_news.Count > 100)
                            {
                                _news.RemoveAt(0);
                            }

                            string cmd =
                                $"<command id=\"get_news_body\" news_id=\"{newsTransaq.Id}\"/>";
                            
                            // sending command / отправка команды
                            string res = ConnectorSendCommand(cmd);

                        }
                    }
                    else if (_newsBodyQueue.IsEmpty == false)
                    {
                        string data = null;

                        if (_newsBodyQueue.TryDequeue(out data))
                        {
                            TransaqNewsBody newsTransaq = _deserializer.Deserialize<TransaqNewsBody>(new RestResponse() { Content = data });

                            TransaqNews myNews = null;

                            for(int i = 0;i < _news.Count;i++)
                            {
                                if (_news[i].Id == newsTransaq.Id)
                                {
                                    myNews = _news[i];
                                    myNews.NewsBody = newsTransaq.Text;
                                    break;
                                }
                            }

                            if(myNews == null)
                            {
                                continue;
                            }

                            News news = new News();

                            news.TimeMessage = DateTime.Parse(myNews.Timestamp);
                            news.Source = this.ServerType + " " + myNews.Source;
                            news.Value = myNews.NewsBody;

                            if (news != null)
                            {
                                NewsEvent(news);
                            }
                        }
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
                catch (Exception e)
                {
                    SendLogMessage(e.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void ThreadSecurityInfoParsingWorkPlace()
        {
            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (_securityInfoQueue.IsEmpty == false)
                    {
                        string data = null;

                        if (_securityInfoQueue.TryDequeue(out data))
                        {
                            SecurityInfo newInfo = 
                                _deserializer.Deserialize<SecurityInfo>(new RestResponse() { Content = data }); ;
                            UpdateSecurity(newInfo);
                        }
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
                catch (Exception e)
                {
                    SendLogMessage(e.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void ThreadPrivateDataParsingWorkPlace()
        {
            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (_ordersQueue.IsEmpty == false)
                    {
                        string data = null;

                        if (_ordersQueue.TryDequeue(out data))
                        {
                            List<TransaqEntity.Order> orders = 
                                _deserializer.Deserialize<List<TransaqEntity.Order>>(new RestResponse() { Content = data });

                            UpdateMyOrders(orders);
                        }
                    }
                    else if (_myTradesQueue.IsEmpty == false)
                    {
                        string data = null;

                        if (_myTradesQueue.TryDequeue(out data))
                        {
                            List<TransaqEntity.Trade> trades = 
                                _deserializer.Deserialize<List<TransaqEntity.Trade>>(new RestResponse() { Content = data });

                            UpdateMyTrades(trades);
                        }
                    }
                    else if(_portfoliosQueue.IsEmpty == false)
                    {
                        string data = null;

                        if (_portfoliosQueue.TryDequeue(out data))
                        {
                            UpdateClientPortfolio(data);
                        }
                    }
                    else if (_positionsQueue.IsEmpty == false)
                    {
                        string data = null;

                        if (_positionsQueue.TryDequeue(out data))
                        {
                            TransaqPositions positions = Deserialize<TransaqPositions>(data);

                            UpdateClientPositions(positions);
                        }
                    }
                    else if (_clientLimitsQueue.IsEmpty == false)
                    {
                        string data = null;

                        if (_clientLimitsQueue.TryDequeue(out data))
                        {
                            ClientLimits limits = Deserialize<ClientLimits>(data);

                            UpdateClientLimits(limits);
                        }
                    }
                    else if (_clientInfoQueue.IsEmpty == false)
                    {
                        string data = null;

                        if (_clientInfoQueue.TryDequeue(out data))
                        {
                            Client clientInfo = _deserializer.Deserialize<Client>(new RestResponse() { Content = data });

                            ClientsInfoUpdate(clientInfo);
                        }
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
                catch (Exception e)
                {
                    SendLogMessage(e.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void ThreadTradesParsingWorkPlace()
        {
            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (_tradesQueue.IsEmpty == false)
                    {
                       string data  = null;

                        if (_tradesQueue.TryDequeue(out data))
                        {
                            List<TransaqEntity.Trade> trades = 
                                _deserializer.Deserialize<List<TransaqEntity.Trade>>(new RestResponse() { Content = data });

                            UpdateTrades(trades);
                        }
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
                catch (Exception e)
                {
                    SendLogMessage(e.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void ThreadMarketDepthsParsingWorkPlace()
        {
            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (_mdQueue.IsEmpty == false)
                    {
                        string data = null;

                        if (_mdQueue.TryDequeue(out data))
                        {
                            List<Quote> quotes = _deserializer.Deserialize<List<Quote>>(new RestResponse() { Content = data });

                            UpdateMarketDepths(quotes);
                        }
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
                catch (Exception e)
                {
                    SendLogMessage(e.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void UpdateMyTrades(List<TransaqEntity.Trade> trades)
        {
            for (int i = 0; i < trades.Count; i++)
            {
                TransaqEntity.Trade trade = trades[i];

                MyTrade myTrade = new MyTrade();
                myTrade.Time = DateTime.Parse(trade.Time);
                myTrade.NumberOrderParent = trade.Orderno;
                myTrade.NumberTrade = trade.Tradeno;
                myTrade.Volume = trade.Quantity.ToDecimal();
                myTrade.Price = trade.Price.ToDecimal();
                myTrade.SecurityNameCode = trade.Seccode;
                myTrade.Side = trade.Buysell == "B" ? Side.Buy : Side.Sell;

                MyTradeEvent?.Invoke(myTrade);
            }
        }

        private void UpdateMyOrders(List<TransaqEntity.Order> orders)
        {
            for (int i = 0; i < orders.Count; i++)
            {
                TransaqEntity.Order order = orders[i];

                if (order.Orderno == "0")
                {
                    //continue;
                }

                Order newOrder = new Order();
                newOrder.SecurityNameCode = order.Seccode;
                newOrder.NumberUser = Convert.ToInt32(order.Transactionid);
                newOrder.SecurityClassCode = order.Board;
                newOrder.NumberMarket = order.Orderno;
                newOrder.TimeCallBack = order.Time != null ? DateTime.Parse(order.Time) : ServerTime;
                newOrder.Side = order.Buysell == "B" ? Side.Buy : Side.Sell;
                newOrder.Volume = order.Quantity.ToDecimal();
                newOrder.Price = order.Price.ToDecimal();
                newOrder.ServerType = ServerType.Transaq;

                if(string.IsNullOrEmpty(order.Union) == false)
                {
                    newOrder.PortfolioNumber = "United_" + order.Union;
                }
                else
                {
                    newOrder.PortfolioNumber = order.Client;
                }

                lock (_sendOrdersLocker)
                {
                    if (string.IsNullOrEmpty(newOrder.NumberMarket) == false
                        && newOrder.NumberUser != 0
                        && newOrder.NumberMarket != "0")
                    {
                        for (int i2 = _sendOrders.Count - 1; i2 > -1; i2--)
                        {
                            if (_sendOrders[i2].NumberUser == newOrder.NumberUser)
                            {
                                newOrder.TypeOrder = _sendOrders[i2].TypeOrder;
                                break;
                            }
                        }
                    }
                }

                if (order.Status == "active")
                {
                    if (order.Result.Contains("Цена сделки вне лимита"))
                    {
                        SendLogMessage(order.Result, LogMessageType.Error);
                    }

                    newOrder.State = OrderStateType.Active;
                }
                else if (order.Status == "cancelled" ||
                         order.Status == "expired" ||
                         order.Status == "disabled" ||
                         order.Status == "removed")
                {
                    if (order.Status == "removed"
                        && string.IsNullOrEmpty(order.Result) == false)
                    {
                        SendLogMessage(order.Result, LogMessageType.Error);
                    }

                    newOrder.State = OrderStateType.Cancel;
                }
                else if (order.Status == "matched")
                {
                    newOrder.State = OrderStateType.Done;
                }
                else if (order.Status == "denied" ||
                         order.Status == "rejected" ||
                         order.Status == "failed" ||
                         order.Status == "refused")
                {
                    if (string.IsNullOrEmpty(order.Result) == false)
                    {
                        SendLogMessage(order.Result, LogMessageType.Error);
                    }
                    newOrder.State = OrderStateType.Fail;
                }
                else if (order.Status == "forwarding" ||
                         order.Status == "wait" ||
                         order.Status == "watching")
                {
                    newOrder.State = OrderStateType.Pending;
                }
                else
                {
                    newOrder.State = OrderStateType.None;
                }

                MyOrderEvent?.Invoke(newOrder);
            }
        }

        private List<MarketDepth> _depths;

        DateTime _lastMdTime;

        private void UpdateMarketDepths(List<Quote> quotes)
        {
            if (quotes == null || quotes.Count == 0)
            {
                return;
            }

            if (_depths == null)
            {
                _depths = new List<MarketDepth>();
            }

            Dictionary<string, List<Quote>> sortedQuotes = new Dictionary<string, List<Quote>>();


            for (int i = 0; i < quotes.Count; i++)
            {
                Quote quote = quotes[i];
                if (!sortedQuotes.ContainsKey(quote.Seccode))
                {
                    sortedQuotes.Add(quote.Seccode, new List<Quote>());
                }

                sortedQuotes[quote.Seccode].Add(quote);
            }

            List<string> keys = new List<string>(sortedQuotes.Keys);
            for (int index = 0; index < keys.Count; index++)
            {
                string key = keys[index];
                List<Quote> sortedQuote = sortedQuotes[key];

                MarketDepth needDepth = _depths.Find(depth => depth.SecurityNameCode == sortedQuote[0].Seccode);

                if (needDepth == null)
                {
                    needDepth = new MarketDepth();
                    needDepth.SecurityNameCode = sortedQuote[0].Seccode;
                    _depths.Add(needDepth);
                }

                for (int i = 0; i < sortedQuote.Count; i++)
                {
                    if (sortedQuote[i].Buy > 0)
                    {
                        MarketDepthLevel needLevel = needDepth.Bids.Find(level => level.Price == sortedQuote[i].Price);
                        if (needLevel != null)
                        {
                            needLevel.Bid = sortedQuote[i].Buy;
                        }
                        else
                        {
                            if (sortedQuote[i].Price == 0)
                            {
                                continue;
                            }

                            needDepth.Bids.Add(new MarketDepthLevel()
                            {
                                Price = sortedQuote[i].Price,
                                Bid = sortedQuote[i].Buy,
                            });
                            needDepth.Bids.Sort((a, b) =>
                            {
                                if (a.Price > b.Price)
                                {
                                    return -1;
                                }
                                else if (a.Price < b.Price)
                                {
                                    return 1;
                                }
                                else
                                {
                                    return 0;
                                }
                            });

                            while (needDepth.Asks.Count > 0 &&
                             needDepth.Bids[0].Price >= needDepth.Asks[0].Price)
                            {
                                needDepth.Asks.RemoveAt(0);
                            }
                        }
                    }

                    if (sortedQuote[i].Sell > 0)
                    {
                        MarketDepthLevel needLevel = needDepth.Asks.Find(level => level.Price == sortedQuote[i].Price);
                        if (needLevel != null)
                        {
                            needLevel.Ask = sortedQuote[i].Sell;
                        }
                        else
                        {
                            if (sortedQuote[i].Price == 0)
                            {
                                continue;
                            }

                            needDepth.Asks.Add(new MarketDepthLevel()
                            {
                                Price = sortedQuote[i].Price,
                                Ask = sortedQuote[i].Sell,
                            });
                            needDepth.Asks.Sort((a, b) =>
                            {
                                if (a.Price > b.Price)
                                {
                                    return 1;
                                }
                                else if (a.Price < b.Price)
                                {
                                    return -1;
                                }
                                else
                                {
                                    return 0;
                                }
                            });

                            while (needDepth.Bids.Count > 0 &&
                             needDepth.Bids[0].Price >= needDepth.Asks[0].Price)
                            {
                                needDepth.Bids.RemoveAt(0);
                            }
                        }
                    }

                    if (sortedQuote[i].Buy == -1)
                    {
                        int deleteLevelIndex = needDepth.Bids.FindIndex(level => level.Price == sortedQuote[i].Price);
                        if (deleteLevelIndex != -1)
                        {
                            needDepth.Bids.RemoveAt(deleteLevelIndex);
                        }
                    }

                    if (sortedQuote[i].Sell == -1)
                    {
                        int deleteLevelIndex = needDepth.Asks.FindIndex(level => level.Price == sortedQuote[i].Price);
                        if (deleteLevelIndex != -1)
                        {
                            needDepth.Asks.RemoveAt(deleteLevelIndex);
                        }
                    }
                }

                needDepth.Time = ServerTime == DateTime.MinValue ? TimeManager.GetExchangeTime("Russian Standard Time") : ServerTime;

                if (needDepth.Time <= _lastMdTime)
                {
                    needDepth.Time = _lastMdTime.AddTicks(1);
                }

                _lastMdTime = needDepth.Time;

                if (needDepth.Asks == null ||
                    needDepth.Asks.Count == 0 ||
                    needDepth.Bids == null ||
                    needDepth.Bids.Count == 0)
                {
                    return;
                }

                while (needDepth.Bids.Count > 0 &&
                    needDepth.Bids[0].Price >= needDepth.Asks[0].Price)
                {
                    needDepth.Bids.RemoveAt(0);
                }

                if (needDepth.Bids.Count == 0)
                {
                    return;
                }

                while (needDepth.Bids.Count > 25)
                {
                    needDepth.Bids.RemoveAt(needDepth.Bids.Count - 1);
                }

                while (needDepth.Asks.Count > 25)
                {
                    needDepth.Asks.RemoveAt(needDepth.Asks.Count - 1);
                }

                if (MarketDepthEvent != null)
                {
                    MarketDepthEvent(needDepth.GetCopy());
                }
            }
        }

        private void UpdateTrades(List<TransaqEntity.Trade> trades)
        {
            for (int i = 0; i < trades.Count; i++)
            {
                TransaqEntity.Trade t = trades[i];

                if (string.IsNullOrEmpty(t.Price)
                    || string.IsNullOrEmpty(t.Quantity))
                {
                    continue;
                }

                Trade trade = new Trade();
                trade.SecurityNameCode = t.Seccode;
                trade.Id = t.Tradeno;
                trade.Price = t.Price.ToDecimal();
                trade.Side = t.Buysell == "B" ? Side.Buy : Side.Sell;
                trade.Volume = t.Quantity.ToDecimal();
                trade.Time = DateTime.Parse(t.Time);

                NewTradesEvent?.Invoke(trade);
            }
        }

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        #endregion

        #region 10 Log messages

        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion

        #region 11 Queries

        private string _commandLocker = "commandSendLocker";

        /// <summary>
        /// sent the command
        /// отправить команду
        /// </summary>
        /// <param name="command">command as a XML document / команда в виде XML документа</param>
        /// <returns>result of sending command/результат отправки команды</returns>
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        public string ConnectorSendCommand(string command)
        {
            try
            {
                lock (_commandLocker)
                {
                    IntPtr pData = MarshalUtf8.StringToHGlobalUtf8(command);
                    IntPtr pResult = SendCommand(pData);

                    string result = MarshalUtf8.PtrToStringUtf8(pResult);

                    Marshal.FreeHGlobal(pData);
                    FreeMemory(pResult);

                    return result;
                }
            }
            catch (AccessViolationException e)
            {
                // no message
                return null;
            }
            catch (Exception e)
            {
                SendLogMessage(e.ToString(), LogMessageType.Error);
                return null;
            }
        }

        #endregion

        #region 12 Helpers

        private object _securityAvabilityLocker = new object();

        private bool CheckSecurityAvailability(Security security)
        {
            lock (_securityAvabilityLocker)
            {
                // проверка на случай, если запрос происходит раньше, чем данная бумага пришла с биржи.
                if (_securities != null && _securities.Count != 0)
                {
                    for (int i = 0; i < _securities.Count; i++)
                    {
                        Security currentSec = _securities[i];

                        if (currentSec.Name == security.Name
                            && currentSec.NameFull == security.NameFull
                            && currentSec.NameClass == security.NameClass)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
        }

        private readonly XmlDeserializer _deserializer;

        /// <summary>
        /// converts a string of data to needed format
        /// преобразует строку с данными в нужный формат
        /// </summary>
        /// <typeparam name="T">type for converting / тип, в который нужно преобразовать данные</typeparam>
        /// <param name="data">data string / строка с данными</param>
        /// <returns>nessesary object / объект нужного типа</returns>
        public T Deserialize<T>(string data)
        {
            T newData;
            XmlSerializer formatter = new XmlSerializer(typeof(T));
            using (StringReader fs = new StringReader(data))
            {
                newData = (T)formatter.Deserialize(fs);
            }
            return newData;
        }

        //--------------------------------------------------------------------------------
        // file of library TXmlConnector.the dll must be in the same folder as the program
        // файл библиотеки TXmlConnector.dll должен находиться в одной папке с программой

        [DllImport("txmlconnector64.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern bool SetCallback(CallBackDelegate pCallback);

        [DllImport("txmlconnector64.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr SendCommand(IntPtr pData);

        [DllImport("txmlconnector64.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern bool FreeMemory(IntPtr pData);

        [DllImport("TXmlConnector64.dll", CallingConvention = CallingConvention.Winapi)]
        private static extern IntPtr Initialize(IntPtr pPath, Int32 logLevel);

        [DllImport("TXmlConnector64.dll", CallingConvention = CallingConvention.Winapi)]
        private static extern IntPtr UnInitialize();

        [DllImport("TXmlConnector64.dll", CallingConvention = CallingConvention.Winapi)]
        private static extern IntPtr SetLogLevel(Int32 logLevel);
        //--------------------------------------------------------------------------------

        #endregion
    }
}