using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Alor.Dto;
using OsEngine.Market.Servers.Alor.Tools;
using OsEngine.Market.Servers.Entity;

namespace OsEngine.Market.Servers.Alor
{
    public class AlorServer : AServer
    {
        public AlorServer()
        {
            AlorServerRealization realization = new AlorServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamToken, "");
            CreateParameterString(OsLocalization.Market.Exchange, "");
            CreateParameterString(OsLocalization.Market.Label3, "");
            CreateParameterBoolean(OsLocalization.Market.UseStock, true);
            CreateParameterBoolean(OsLocalization.Market.UseFutures, true);
            CreateParameterBoolean(OsLocalization.Market.UseCurrency, true);
            CreateParameterBoolean(OsLocalization.Market.UseOptions, false);
            CreateParameterBoolean(OsLocalization.Market.UseOther, false);
        }
    }

    public class AlorServerRealization : IServerRealization
    {
        #region Fields
        
        /// <summary> Синхронизатор работы с статусом коннектора </summary>
        private readonly object _syncStatus = new object();

        private readonly string _restApiHost = "https://api.alor.ru";
        private readonly string _oauthApiHost = "https://oauth.alor.ru";

        private readonly string _wsHost = "wss://api.alor.ru/ws";
        private readonly string _orderWsHost = "";
        
        /// <summary> Хешсет идентификаторов операции для отслеживания результата постановки заявок, по которым еще не было ответа </summary>
        private readonly HashSet<int> _awaitedPlaceOrderUserIdHashSet;

        /// <summary> Хешсет идентификаторов операции для отслеживания результата снятия заявок, по которым еще не было ответа </summary>
        private readonly HashSet<int> _awaitedCancelOrderUserIdHashSet;

        /// <summary> Хешсет идентификаторов операции для отслеживания результата снятия всех заявок, по которым еще не было ответа </summary>
        private readonly HashSet<int> _awaitedCancelAllOrdersUserIdHashSet;
        
        /// <summary>
        ///  Временное хранилице ордеров, чтобы дважды не обработать коллбэки (а приходят они иногда одни и те же)
        /// </summary>
        private readonly Dictionary<long, Order> _userOrderDataDictionary;

        private ApiClient _apiClient;

        private WsClient _wsClient;

        private bool _useStock = false;
        private bool _useFutures = false;
        private bool _useOptions = false;
        private bool _useCurrency = false;
        private bool _useOther = false;

        private string _portfolioId;
        private string _exchangeCode;
        
        #endregion
        
        #region Properties

        public ServerType ServerType => ServerType.Alor;
        public ServerConnectStatus ServerStatus { get; set; } = ServerConnectStatus.Disconnect;
        public List<IServerParameter> ServerParameters { get; set; }
        public DateTime ServerTime { get; set; }


        #endregion
        
        #region Constructors

        public AlorServerRealization()
        {
            _awaitedPlaceOrderUserIdHashSet = new HashSet<int>();
            _awaitedCancelOrderUserIdHashSet = new HashSet<int>();
            _awaitedCancelAllOrdersUserIdHashSet = new HashSet<int>();
            _userOrderDataDictionary = new Dictionary<long, Order>();
        }
        
        #endregion
        
        #region Methods

        private void InitPortfolio()
        {
            var apiEndpoint = $"{_restApiHost}/md/v2/{_exchangeCode}/{_portfolioId}/summary";
            var portfolioSummary = _apiClient.GetAsync<AlorPortfolioSummary>(apiEndpoint);
            _wsClient.SubscribePortfolioChanges(_exchangeCode, _portfolioId, (_useFutures || _useOptions) && _exchangeCode == "MOEX");
            // @todo update info in the system (what? how?)
        }

        private void InitSecurities()
        {
            //securities?sector=FOND&limit=1000
            _useStock =((ServerParameterBool)ServerParameters[3]).Value;
            _useFutures =((ServerParameterBool)ServerParameters[4]).Value;
            _useCurrency =((ServerParameterBool)ServerParameters[5]).Value;
            _useOptions =((ServerParameterBool)ServerParameters[6]).Value;
            _useOther =((ServerParameterBool)ServerParameters[7]).Value;

            string apiEndpoint;
            if (_useStock || _useOther)
            {
                apiEndpoint = $"{_restApiHost}/securities?sector=FOND&limit=10000";
                UpdateSecuritiesFromServer(apiEndpoint);
            }

            if (_useFutures)
            {
                apiEndpoint = $"{_restApiHost}/securities?sector=FORTS&cfiCode=F&limit=1000";
                UpdateSecuritiesFromServer(apiEndpoint);
            }
            
            if (_useOptions)
            {
                apiEndpoint = $"{_restApiHost}/securities?sector=FORTS&cfiCode=O&limit=10000";
                UpdateSecuritiesFromServer(apiEndpoint);
            }

            if (_useCurrency)
            {
                apiEndpoint = $"{_restApiHost}/securities?sector=CURR&limit=10000";
                UpdateSecuritiesFromServer(apiEndpoint);
            }
        }

        private void UpdateSecuritiesFromServer(string apiEndpoint)
        {
            try
            {
                var stocks = _apiClient.GetAsync<List<AlorSecurity>>(apiEndpoint);
                if (stocks.Result.Count == 0) return;

                var securities = new List<Security>();
                foreach (var item in stocks.Result)
                {
                    var instrumentType = GetSecurityType(item);
                    if (!CheckNeedSecurity(instrumentType))
                    {
                        continue;
                    }
                    Security newSecurity = new Security();
                    
                    newSecurity.Exchange = item.exchange;
                    newSecurity.DecimalsVolume = 1;
                    newSecurity.Lot = item.lotsize;
                    newSecurity.Name = item.symbol;
                    newSecurity.NameFull = item.description;
                    newSecurity.NameClass = item.type;
                    newSecurity.NameId = item.shortname;
                    newSecurity.SecurityType = instrumentType;
                    newSecurity.Decimals = GetDecimals(item.minstep);
                    newSecurity.PriceStep = item.minstep;
                    newSecurity.PriceStepCost = item.pricestep;
                    newSecurity.State = SecurityStateType.Activ;
                    securities.Add(newSecurity);
                }
                if (securities.Count > 0)
                    SecurityEvent?.Invoke(securities);
            }
            catch (Exception e)
            {
                SendLogMessage($"Error loading stocks: {e.Message}", LogMessageType.Error);
            }
        }

        private SecurityType GetSecurityType(AlorSecurity security)
        {
            var cfiCode = security.cfiCode;
            if (cfiCode.StartsWith("F")) return SecurityType.Futures;
            if (cfiCode.StartsWith("O")) return SecurityType.Option;
            if (cfiCode.StartsWith("ES") || cfiCode.StartsWith("EP")) return SecurityType.Stock;
            var board = security.board;
            if (board == "CETS") return SecurityType.CurrencyPair;
            
            return SecurityType.None;
        }

        private bool CheckNeedSecurity(SecurityType instrumentType)
        {
            switch (instrumentType)
            {
                case SecurityType.Stock when _useStock:
                case SecurityType.Futures when _useFutures:
                case SecurityType.Option when _useOptions:
                case SecurityType.CurrencyPair when _useCurrency:
                case SecurityType.None when _useOther:
                    return true;
                case SecurityType.Bond:
                case SecurityType.Index:
                default:
                    return false;
            }
        }

        private int GetDecimals(decimal x)
        {
            var precision = 0;
            while (x * (decimal)Math.Pow(10, precision) != Math.Round(x * (decimal)Math.Pow(10, precision))) 
                precision++;
            return precision;
        }
        
        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent?.Invoke(message, messageType);
        }
        
        public void Connect()
        {
            SendLogMessage("Start Alor Connection", LogMessageType.System);
            _exchangeCode = ((ServerParameterString)ServerParameters[1]).Value;
            _portfolioId = ((ServerParameterString)ServerParameters[2]).Value;
            
            var apiToken = ((ServerParameterString)ServerParameters[0]).Value;
            var tokenProvider = new TokenProvider($"{_oauthApiHost}/refresh?token={apiToken}");
            _apiClient = new ApiClient(tokenProvider);
            InitSecurities();
            
            _wsClient = new WsClient("AlorWs", new Uri(_wsHost), tokenProvider);
            InitPortfolio();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void GetSecurities()
        {
        }

        public void GetPortfolios()
        {
            throw new NotImplementedException();
        }

        public void SendOrder(Order order)
        {
            throw new NotImplementedException();
        }

        public void CancelOrder(Order order)
        {
            throw new NotImplementedException();
        }

        public void CancelAllOrders()
        {
            throw new NotImplementedException();
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            throw new NotImplementedException();
        }

        public void Subscrible(Security security)
        {
            throw new NotImplementedException();
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime,
            DateTime actualTime)
        {
            throw new NotImplementedException();
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            throw new NotImplementedException();
        }

        public void GetOrdersState(List<Order> orders)
        {
            throw new NotImplementedException();
        }

        public void ResearchTradesToOrders(List<Order> orders)
        {
            throw new NotImplementedException();
        }
        
        #endregion
        
        #region Delegates and events
        
        public event Action<Order> MyOrderEvent;
        public event Action<MyTrade> MyTradeEvent;
        public event Action<List<Portfolio>> PortfolioEvent;
        public event Action<List<Security>> SecurityEvent;
        public event Action<MarketDepth> MarketDepthEvent;
        public event Action<Trade> NewTradesEvent;
        public event Action ConnectEvent;
        public event Action DisconnectEvent;
        public event Action<string, LogMessageType> LogMessageEvent;
        
        #endregion
    }
}