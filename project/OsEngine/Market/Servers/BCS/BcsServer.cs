/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Entity.WebSocketOsEngine;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.BCS.Entity;
using OsEngine.Market.Servers.Entity;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.BCS
{
    public class BcsServer : AServer
    {
        public BcsServer(int uniqueNumber)
        {
            ServerNum = uniqueNumber;
            BcsServerRealization realization = new BcsServerRealization();
            ServerRealization = realization;

            ServerParameterPassword token = CreateParameterPassword(OsLocalization.Market.ServerParamToken, "");
            token.Comment = OsLocalization.Market.ServerParamBCSTokenDescription;

            ServerParameterBool useStock = CreateParameterBoolean(OsLocalization.Market.UseStock, true);
            ServerParameterBool useFutures = CreateParameterBoolean(OsLocalization.Market.UseFutures, false);
            ServerParameterBool useCurrency = CreateParameterBoolean(OsLocalization.Market.UseCurrency, false);
            ServerParameterBool useBonds = CreateParameterBoolean(OsLocalization.Market.UseBonds, false);
            ServerParameterBool useFunds = CreateParameterBoolean(OsLocalization.Market.UseFunds, false);
            ServerParameterBool useOptions = CreateParameterBoolean(OsLocalization.Market.UseOptions, false);
            ServerParameterBool useOther = CreateParameterBoolean(OsLocalization.Market.UseOther, false);
            useStock.Comment = OsLocalization.Market.UseStockDescription;
            useFutures.Comment = OsLocalization.Market.UseFuturesDescription;
            useCurrency.Comment = OsLocalization.Market.UseCurrencyDescription;
            useBonds.Comment = OsLocalization.Market.UseBondsDescription;
            useFunds.Comment = OsLocalization.Market.UseFundsDescription;
            useOptions.Comment = OsLocalization.Market.UseOptionsDescription.Split('.')[0];
            useOther.Comment = OsLocalization.Market.UseOtherDescription2;

            ServerParameterEnum depthLevels = CreateParameterEnum(OsLocalization.Market.ServerParam13, "10", new List<string> { "1", "10", "20" });
            depthLevels.Comment = OsLocalization.Market.SetDepthLevelsDescription;

            ServerParameterBool ignoreMorningAuction = CreateParameterBoolean(OsLocalization.Market.IgnoreMorningAuctionTrades, false);
            ignoreMorningAuction.Comment = OsLocalization.Market.IgnoreMorningAuctionTradesDescription;
        }
    }

    public class BcsServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public BcsServerRealization()
        {
            Thread worker = new Thread(CheckLifetimeToken);
            worker.Name = "BcsCheckToken";
            worker.IsBackground = true;
            worker.Start();

            Thread worker2 = new Thread(DataMessageReader);
            worker2.Name = "BcsDataMessageReader";
            worker2.IsBackground = true;
            worker2.Start();

            Thread worker3 = new Thread(PortfolioMessageReader);
            worker3.Name = "BcsPortfolioMessageReader";
            worker3.IsBackground = true;
            worker3.Start();

            Thread worker4 = new Thread(OrderStateMessageReader);
            worker4.Name = "BcsOrdersMessageReader";
            worker4.IsBackground = true;
            worker4.Start();
        }

        private WebProxy _myProxy;

        public void Connect(WebProxy proxy = null)
        {
            try
            {
                _myProxy = proxy;
                _myPortfolios.Clear();
                _subscribedSecurities.Clear();
                _accessTokenExpireTime = DateTime.MinValue;

                SendLogMessage("Start Bcs Connection", LogMessageType.System);

                _apiTokenRefresh = ((ServerParameterPassword)ServerParameters[0]).Value;
                _ignoreMorningAuctionTrades = ((ServerParameterBool)ServerParameters[9]).Value;

                if (string.IsNullOrEmpty(_apiTokenRefresh))
                {
                    SendLogMessage("Connection terminated. You must specify the api token. You can get it on the Bcs website",
                        LogMessageType.Error);
                    return;
                }

                if (string.IsNullOrEmpty(_apiAccessToken))
                {
                    if (GetAccess24HToken() == false)
                    {
                        SendLogMessage("Authorization Error. Probably an invalid token is specified. You can see it on the Bcs website.",
                        LogMessageType.Error);
                        return;
                    }
                }

                ConfigureHandler();

                _clientForSocket = new HttpClient(_handler);

                CreatePublicWebSocketConnect();
                CreatePrivateWebSocketConnect();
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message.ToString(), LogMessageType.Error);
            }
        }

        private void CreateHttpClient()
        {
            _httpClient = new HttpClient(_handler)
            {
                BaseAddress = new Uri(_baseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiAccessToken);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        private void ConfigureHandler()
        {
            _handler = new HttpClientHandler();

            if (_myProxy != null)
            {
                _handler.Proxy = _myProxy;
                _handler.UseProxy = true;
            }
            else
            {
                _handler.UseProxy = false;
            }

            _handler.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
            _handler.CheckCertificateRevocationList = false;
        }

        private void CheckLifetimeToken()
        {
            while (true)
            {
                Thread.Sleep(60000);

                if (ServerStatus != ServerConnectStatus.Connect || _accessTokenExpireTime == DateTime.MinValue)
                {
                    continue;
                }

                if (DateTime.Now > _accessTokenExpireTime.AddMinutes(-30)) // истекает срок токена
                {
                    if (GetAccess24HToken() == false) // перевыпускаем токен
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

        private bool GetAccess24HToken()
        {
            try
            {
                string endPoint = "/trade-api-keycloak/realms/tradeapi/protocol/openid-connect/token";
                RestRequest requestRest = new RestRequest(endPoint, Method.POST);
                RestClient client = new RestClient(_baseUrl);

                requestRest.AddHeader("Content-Type", "application/x-www-form-urlencoded");
                requestRest.AddHeader("Accept", "application/json");
                requestRest.AddParameter("grant_type", "refresh_token");
                requestRest.AddParameter("client_id", "trade-api-write");
                requestRest.AddParameter("refresh_token", _apiTokenRefresh);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    BcsAuthResponse tokenResp = JsonConvert.DeserializeAnonymousType(response.Content, new BcsAuthResponse());

                    if (tokenResp != null)
                    {
                        _apiAccessToken = tokenResp.access_token;
                        _accessTokenExpireTime = DateTime.Now.AddSeconds(Convert.ToInt32(tokenResp.expires_in));
                        _refreshTokenExpireTime = DateTime.Now.AddSeconds(Convert.ToInt32(tokenResp.refresh_expires_in));

                        if (DateTime.Now.AddDays(5) > _refreshTokenExpireTime) // Refresh-токен имеет срок жизни 90 суток
                        {
                            SendLogMessage("Attention! The token's lifetime is less than 5 days.", LogMessageType.Error);
                        }

                        return true;
                    }
                    else
                    {
                        SendLogMessage($"Token request error: {response.Content}", LogMessageType.Error);
                        return false;
                    }
                }
                else
                {
                    SendLogMessage($"Token request error: code: {response.StatusCode} > msg: {response.Content}", LogMessageType.Error);
                    return false;
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Token request error: " + exception.ToString(), LogMessageType.Error);
                return false;
            }
        }

        public void Dispose()
        {
            _myPortfolios.Clear();
            _securitiesLots.Clear();

            UnsubscribeAllSecurities();
            _subscribedSecurities.Clear();
            DeleteWebSocketConnection();

            _httpClient?.Dispose();
            _httpClient = null;

            _clientForSocket?.Dispose();
            _clientForSocket = null;

            _handler?.Dispose();
            _handler = null;

            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        public DateTime ServerTime { get; set; }

        public ServerType ServerType => ServerType.BCS;

        public ServerConnectStatus ServerStatus { get; set; } = ServerConnectStatus.Disconnect;

        public List<IServerParameter> ServerParameters { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

        public bool IsCompletelyDeleted { get; set; }

        #endregion

        #region 2 Properties

        private readonly string _baseUrl = "https://be.broker.ru";

        private HttpClient _httpClient;
        private HttpClient _clientForSocket;
        private HttpClientHandler _handler;

        private bool _useStock = false;
        private bool _useFutures = false;
        private bool _useOptions = false;
        private bool _useCurrency = false;
        private bool _useBonds = false;
        private bool _useFunds = false;
        private bool _useOther = false;
        private bool _ignoreMorningAuctionTrades = true; // ignore trades before 7:00 MSK


        private string _apiTokenRefresh;
        private string _apiAccessToken; // life time 24 h

        private DateTime _accessTokenExpireTime = DateTime.MinValue;
        private DateTime _refreshTokenExpireTime = DateTime.MinValue;
        TimeZoneInfo _moscowTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time");

        private List<WebSocket> _webSocketPublicList = new List<WebSocket>();
        private WebSocket _webSocketPortfolio;
        private WebSocket _webSocketOrders;
        private WebSocket _webSocketMyTrades;
        private bool _socketDataIsActive;
        private bool _socketPortfolioIsActive;
        private bool _socketOrdersIsActive;
        private bool _socketMyTradesIsActive;
        private string _activationLocker = "activationLocker";

        #endregion

        #region 3 Securities

        private List<Security> _securities = new List<Security>();

        private RateGate _rateGateSecurity = new RateGate(1, TimeSpan.FromMilliseconds(100));

        public void GetSecurities()
        {
            if (_securities.Count > 0)
                _securities.Clear();

            _useStock = ((ServerParameterBool)ServerParameters[1]).Value;
            _useFutures = ((ServerParameterBool)ServerParameters[2]).Value;
            _useCurrency = ((ServerParameterBool)ServerParameters[3]).Value;
            _useBonds = ((ServerParameterBool)ServerParameters[4]).Value;
            _useFunds = ((ServerParameterBool)ServerParameters[5]).Value;
            _useOptions = ((ServerParameterBool)ServerParameters[6]).Value;
            _useOther = ((ServerParameterBool)ServerParameters[7]).Value;

            if (_useStock)
            {
                List<BcsSecurity> securities = GetSecuritiesByType("STOCK");

                if (securities != null && securities.Count > 0)
                    UpdateSecuritiesFromServer(securities);
            }

            if (_useCurrency)
            {
                List<BcsSecurity> securities = GetSecuritiesByType("CURRENCY");

                if (securities != null && securities.Count > 0)
                    UpdateSecuritiesFromServer(securities);
            }

            if (_useFutures)
            {
                List<BcsSecurity> securities = GetSecuritiesByType("FUTURES");

                if (securities != null && securities.Count > 0)
                    UpdateSecuritiesFromServer(securities);
            }

            if (_useBonds)
            {
                List<BcsSecurity> securities = GetSecuritiesByType("BONDS");

                if (securities != null && securities.Count > 0)
                    UpdateSecuritiesFromServer(securities);
            }

            if (_useFunds)
            {
                List<BcsSecurity> securities = GetSecuritiesByType("MUTUAL_FUNDS");

                if (securities != null && securities.Count > 0)
                    UpdateSecuritiesFromServer(securities);
            }

            if (_useOptions)
            {
                string[] baseAssetsForOptions = new string[] {"VTBR", "SBER", "T", "GAZP", "LKOH", "NVTK", "AFLT", "PLZL", "GMKN", "YNDX", "PIKK", "SMLT", "SNGSP",
                    "CHMF", "NLMK", "SVCB", "ROSN", "AFKS", "VKCO", "MTLR", "TATN", "MGNT", "ALRS", "MAGN", "MOEX", "SBERP", "SNGS", "MTSS", "RTKM", "IRAO",
                    "FEES", "POSI", "TATNP", "GLDRUB_TOM", "CNY000SMALL", "EUR000SMALL", "USD000SMALL" };

                for (int i = 0; i < baseAssetsForOptions.Length; i++)
                {
                    List<BcsSecurity> securities = GetSecuritiesByType("OPTIONS", baseAssetsForOptions[i]);

                    if (securities != null && securities.Count > 0)
                        UpdateSecuritiesFromServer(securities);
                }
            }

            if (_useOther)
            {
                List<BcsSecurity> securities = GetSecuritiesByType("INDICES");

                if (securities != null && securities.Count > 0)
                    UpdateSecuritiesFromServer(securities);

                securities = GetSecuritiesByType("GOODS");

                if (securities != null && securities.Count > 0)
                    UpdateSecuritiesFromServer(securities);
            }

            if (_securities.Count > 0)
            {
                SendLogMessage("Securities loaded. Count: " + _securities.Count, LogMessageType.System);

                SecurityEvent?.Invoke(_securities);
            }
        }

        private List<BcsSecurity> GetSecuritiesByType(string secType, string baseAsset = null)
        {
            string apiEndpoint = "/trade-api-information-service/api/v1/instruments/by-type?";

            string type = $"type={secType}";

            string size = "size=100";

            int page = -1;

            List<BcsSecurity> securitiesResp = [];
            List<BcsSecurity> bcsSec = [];

            int tryCount = 1;

            do
            {
                page++;

                string path = baseAsset == null ? apiEndpoint + type + "&" + size + "&" + "page=" + page : apiEndpoint + type + "&" + size + "&" + "page=" + page + "&" + "baseAssetTicker=" + baseAsset;

                _rateGateSecurity.WaitToProceed();

                HttpResponseMessage response = CreateHttpRequestAsync(path, HttpMethod.Get, null).Result;

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    try
                    {
                        string responseMsg = response.Content.ReadAsStringAsync().Result;
                        bcsSec = JsonConvert.DeserializeAnonymousType(responseMsg, new List<BcsSecurity>());

                        if (bcsSec != null && bcsSec.Count > 0)
                        {
                            securitiesResp.AddRange(bcsSec);
                        }
                        else
                        {
                            return securitiesResp;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error parsing securities: {ex.Message}");
                        return securitiesResp;
                    }
                }
                else
                {
                    SendLogMessage($"Stock securities request error. Status: {response.StatusCode}-{response.ReasonPhrase}\n" +
                        $"Try: {tryCount} downloading securities {type}", LogMessageType.Error);

                    page--;

                    tryCount++;

                    continue;
                }

            } while (bcsSec.Count == 100 && tryCount < 5);

            if (tryCount == 5)
            {
                SendLogMessage($"There are too many attempts to download securities {type} type. ", LogMessageType.Error);
                return null;
            }

            return securitiesResp;
        }

        private void UpdateSecuritiesFromServer(List<BcsSecurity> securities)
        {
            try
            {
                for (int i = 0; i < securities.Count; i++)
                {
                    BcsSecurity item = securities[i];

                    SecurityType instrumentType = GetSecurityType(item.instrumentType);

                    if (instrumentType == SecurityType.None)
                    {
                        continue;
                    }

                    if (instrumentType == SecurityType.Stock && item.boards[0].classCode != "TQBR")
                    {
                        continue;
                    }

                    Security newSecurity = new Security();
                    newSecurity.SecurityType = instrumentType;
                    newSecurity.Exchange = item.boards[0].exchange;
                    newSecurity.DecimalsVolume = 0;
                    newSecurity.VolumeStep = 1;
                    newSecurity.Name = item.ticker;
                    newSecurity.NameFull = item.displayName;
                    newSecurity.Lot = instrumentType == SecurityType.Futures || instrumentType == SecurityType.Option ? 1.0m : item.lotSize.ToDecimal();
                    newSecurity.NameId = string.IsNullOrEmpty(item.isin) ? i + "-" + item.ticker + "-" + item.boards[0].exchange : item.isin;
                    newSecurity.Decimals = GetDecimals(item.minimumStep.ToDecimal());
                    newSecurity.PriceStep = item.minimumStep.ToDecimal();
                    newSecurity.PriceStepCost = newSecurity.PriceStep;
                    newSecurity.State = SecurityStateType.Activ;

                    string instrType = item.instrumentType.StartsWith("MUT") ? item.instrumentType.Split('_')[1] : item.instrumentType;
                    newSecurity.NameClass = instrType + "-" + item.boards[0].classCode;

                    if (_useBonds && newSecurity.SecurityType == SecurityType.Bond)
                    {
                        newSecurity.AciValue = item.accruedInt.ToDecimal();
                        newSecurity.NominalCurrent = item.faceValue.ToDecimal();
                        newSecurity.NominalInitial = item.faceValue.ToDecimal();

                        if (DateTime.TryParseExact(item.emissionDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result))
                        {
                            newSecurity.PlacementDate = result;
                        }
                    }

                    if (string.IsNullOrEmpty(item.maturityDate) == false && item.maturityDate != "0")
                    {
                        DateTime matDate = DateTime.MinValue;

                        int year = Convert.ToInt32(item.maturityDate.Substring(0, 4));
                        int month = Convert.ToInt32(item.maturityDate.Substring(4, 2));
                        int day = Convert.ToInt32(item.maturityDate.Substring(6, 2));

                        matDate = new DateTime(year, month, day);

                        if (newSecurity.SecurityType == SecurityType.Futures)
                        {
                            newSecurity.Expiration = matDate;
                            newSecurity.UsePriceStepCostToCalculateVolume = true;
                        }
                        else if (newSecurity.SecurityType == SecurityType.Bond)
                        {
                            newSecurity.MaturityDate = matDate;
                        }
                        else if (newSecurity.SecurityType == SecurityType.Option)
                        {
                            newSecurity.Expiration = matDate;
                            newSecurity.UsePriceStepCostToCalculateVolume = true;
                            newSecurity.Strike = item.strike.ToDecimal();
                            newSecurity.OptionType = item.type == "put" ? OptionType.Put : OptionType.Call;
                            newSecurity.UnderlyingAsset = item.baseAssetSecuritySecCode;
                        }
                    }
                    _securities.Add(newSecurity);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Security parsing error:\n {ex.Message} - {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private SecurityType GetSecurityType(string secType)
        {
            if (secType.Equals("FUTURES"))
            {
                return SecurityType.Futures;
            }
            else if (secType.Equals("OPTIONS"))
            {
                return SecurityType.Option;
            }
            else if (secType.Equals("STOCK"))
            {
                return SecurityType.Stock;
            }
            else if (secType.Equals("BONDS"))
            {
                return SecurityType.Bond;
            }
            else if (secType.Equals("MUTUAL_FUNDS"))
            {
                return SecurityType.Fund;
            }
            else if (secType.Equals("INDICES"))
            {
                return SecurityType.Index;
            }
            else if (secType.Equals("CURRENCY"))
            {
                return SecurityType.CurrencyPair;
            }
            else if (secType.Equals("GOODS"))
            {
                return SecurityType.Commodities;
            }

            return SecurityType.None;
        }

        private int GetDecimals(decimal x)
        {
            int precision = 0;
            while (x * (decimal)Math.Pow(10, precision) != Math.Round(x * (decimal)Math.Pow(10, precision)))
                precision++;
            return precision;
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        private List<Portfolio> _myPortfolios = new List<Portfolio>();
        private Dictionary<string, decimal> _securitiesLots = [];

        public void GetPortfolios()
        {
            try
            {
                string path = "/trade-api-bff-portfolio/api/v1/portfolio";

                HttpResponseMessage portfolioResponse = CreateHttpRequestAsync(path, HttpMethod.Get, null).Result;

                if (portfolioResponse.StatusCode == HttpStatusCode.OK)
                {
                    string responseMsg = portfolioResponse.Content.ReadAsStringAsync().Result;
                    List<BcsPortfolio> bcsPortfolios = JsonConvert.DeserializeAnonymousType(responseMsg, new List<BcsPortfolio>());

                    if (bcsPortfolios != null && bcsPortfolios.Count > 0)
                    {
                        UpdateMyPortfolio(bcsPortfolios);
                    }
                }
                else
                {
                    // получаем через вебсокет
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Portfolio request error: {ex.Message}", LogMessageType.Error);
            }
        }

        private void UpdateMyPortfolio(List<BcsPortfolio> bcsPortfolios)
        {
            if (_securities.Count == 0)
                return;

            List<string> accounts = GetAllAccounts(bcsPortfolios);

            for (int i = 0; i < accounts.Count; i++)
            {
                Portfolio portfolio = _myPortfolios.Find(p => p.Number == accounts[i]);

                if (portfolio == null)
                {
                    portfolio = new Portfolio();
                    portfolio.Number = accounts[i];

                    _myPortfolios.Add(portfolio);
                }

                List<BcsPortfolio> portfT365term = bcsPortfolios.FindAll(p => p.account == accounts[i] && p.term == "T365");

                if (portfT365term.Count > 0)
                {
                    decimal totalBalance = 0;
                    decimal totalBlocked = 0;
                    decimal totalUnrealizedPl = 0;

                    for (int j = 0; j < portfT365term.Count; j++)
                    {
                        decimal quantity = portfT365term[j].quantity.ToDecimal();
                        decimal currPrice = portfT365term[j].currentPrice.ToDecimal();
                        decimal currBlocked = portfT365term[j].locked.ToDecimal();
                        decimal currUnrealizedPl = quantity == 0 ? 0 : portfT365term[j].unrealizedPL.ToDecimal(); // после закрытия позиции фьючерса нереализованная прибыль отображается до клиринга

                        decimal posCurrBalance = quantity * currPrice;
                        totalBalance += posCurrBalance;
                        totalBlocked += currBlocked;
                        totalUnrealizedPl += currUnrealizedPl;

                        PositionOnBoard posPortf = null;

                        if (portfolio.PositionOnBoard != null)
                        {
                            if (portfolio.PositionOnBoard.Count > portfT365term.Count)
                            {
                                for (int k = 0; k < portfolio.PositionOnBoard.Count; k++)
                                {
                                    if (portfT365term.Find(p => p.ticker == portfolio.PositionOnBoard[k].SecurityNameCode) == null)
                                    {
                                        _securitiesLots.Remove(portfolio.PositionOnBoard[k].SecurityNameCode);
                                        portfolio.PositionOnBoard.Remove(portfolio.PositionOnBoard[k]);
                                        k--;
                                    }
                                }
                            }

                            posPortf = portfolio.PositionOnBoard.Find(p => p.SecurityNameCode == portfT365term[j].ticker);
                        }

                        if (posPortf == null)
                        {
                            posPortf = new PositionOnBoard();
                            posPortf.SecurityNameCode = portfT365term[j].ticker;

                            Security security = _securities.Find(s => s.Name == portfT365term[j].ticker);

                            if (security != null)
                            {
                                _securitiesLots[security.Name] = security.Lot;
                                posPortf.ValueCurrent = quantity / security.Lot;
                                posPortf.ValueBegin = quantity / security.Lot;
                            }
                            else
                            {
                                posPortf.ValueCurrent = quantity;
                                posPortf.ValueBegin = quantity;
                            }

                            posPortf.ValueBlocked = currBlocked;
                            posPortf.PortfolioName = portfT365term[j].account;
                            posPortf.UnrealizedPnl = currUnrealizedPl;


                            portfolio.SetNewPosition(posPortf);
                        }
                        else
                        {

                            if (_securitiesLots.TryGetValue(posPortf.SecurityNameCode, out decimal lot))
                            {
                                posPortf.ValueCurrent = quantity / lot;
                            }
                            else
                            {
                                posPortf.ValueCurrent = quantity;
                            }

                            posPortf.ValueBlocked = currBlocked;
                            posPortf.UnrealizedPnl = currUnrealizedPl;
                        }
                    }

                    if (portfolio.ValueBegin == 0)
                        portfolio.ValueBegin = totalBalance;

                    portfolio.ValueCurrent = totalBalance;
                    portfolio.ValueBlocked = totalBlocked;
                    portfolio.UnrealizedPnl = totalUnrealizedPl;

                }

                if (_myPortfolios.Count != 0)
                {
                    PortfolioEvent?.Invoke(_myPortfolios);
                }
            }
        }

        private List<string> GetAllAccounts(List<BcsPortfolio> bcsPortfolios)
        {
            List<string> accounts = [];

            for (int i = 0; i < bcsPortfolios.Count; i++)
            {
                if (accounts.Find(a => a == bcsPortfolios[i].account) == null)
                {
                    accounts.Add(bcsPortfolios[i].account);
                }
            }

            return accounts;
        }

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            if (candleCount <= 0)
            {
                return null;
            }

            DateTime endTime = DateTime.Now;
            DateTime startTime = endTime.AddTicks(-(timeFrameBuilder.TimeFrameTimeSpan.Ticks * candleCount));

            List<Candle> candles = GetHistoryCandles(security, timeFrameBuilder, startTime, endTime);

            if (candles == null)
            {
                return null;
            }

            while (candles.Count > candleCount)
            {
                candles.RemoveAt(0);
            }

            return candles;
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {

            if (!CheckTime(startTime, endTime, actualTime))
            {
                return null;
            }
            DateTime requestedStartTime = startTime;

            List<Candle> candles = GetHistoryCandles(security, timeFrameBuilder, requestedStartTime, endTime);

            if (candles == null)
            {
                return null;
            }

            return candles;
        }

        private readonly RateGate _rateGateCandles = new RateGate(1, TimeSpan.FromMilliseconds(500));

        private List<Candle> GetHistoryCandles(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime)
        {
            string timeFrame = GetCandleTimeFrame(timeFrameBuilder.TimeFrameTimeSpan);

            if (timeFrame == null)
            {
                return null;
            }

            if (timeFrame == "D" && endTime.Date > DateTime.Now.Date.AddDays(-1))
            {
                endTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0);
            }

            string classCode = GetClassCode(security);

            if (string.IsNullOrEmpty(classCode))
            {
                SendLogMessage($"BCS candles request error. Empty class code for {security.Name}", LogMessageType.Error);
                return null;
            }

            List<Candle> candles = new List<Candle>();
            TimeSpan requestTimeSpan = TimeSpan.FromTicks(timeFrameBuilder.TimeFrameTimeSpan.Ticks * 1440);
            DateTime requestStartTime = startTime;

            while (requestStartTime < endTime)
            {
                DateTime requestEndTime = requestStartTime.Add(requestTimeSpan);

                if (requestEndTime > endTime)
                {
                    requestEndTime = endTime;
                }

                List<Candle> newCandles = RequestHistoryCandles(security, classCode, timeFrame, requestStartTime, requestEndTime);

                if (newCandles == null)
                {
                    return candles.Count == 0 ? null : candles;
                }

                if (newCandles.Count > 0)
                {
                    if (candles.Count > 0 &&
                        candles[candles.Count - 1].TimeStart == newCandles[0].TimeStart)
                    {
                        newCandles.RemoveAt(0);
                    }

                    candles.AddRange(newCandles);
                }

                if (requestEndTime == endTime)
                {
                    break;
                }

                requestStartTime = requestEndTime;
            }

            for (int i = 0; i < candles.Count; i++)
            {
                if (candles[i].TimeStart < startTime ||
                    candles[i].TimeStart > endTime)
                {
                    candles.RemoveAt(i);
                    i--;
                }
            }

            return candles;
        }

        private List<Candle> RequestHistoryCandles(Security security, string classCode, string timeFrame, DateTime startTime, DateTime endTime)
        {
            try
            {
                _rateGateCandles.WaitToProceed();

                string path = "/trade-api-market-data-connector/api/v1/candles-chart?"
                    + "classCode=" + Uri.EscapeDataString(classCode) + "&ticker=" + Uri.EscapeDataString(security.Name)
                    + "&startDate=" + Uri.EscapeDataString(startTime.ToString("yyyy-MM-ddTHH:mm:00Z"))
                    + "&endDate=" + Uri.EscapeDataString(endTime.ToString("yyyy-MM-ddTHH:mm:00Z"))
                    + "&timeFrame=" + Uri.EscapeDataString(timeFrame);

                // "/trade-api-market-data-connector/api/v1/candles-chart?classCode=TQBR&ticker=ALRS&startDate=2026-07-01T00%3A00%3A00Z&endDate=2026-07-10T00%3A00%3A00Z&timeFrame=H1";

                HttpResponseMessage response = CreateHttpRequestAsync(path, HttpMethod.Get, null).Result;

                if (response == null)
                {
                    return null;
                }

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage($"BCS candles request error. Code: {response.StatusCode} || msg: {response.ReasonPhrase}", LogMessageType.Error);
                    return null;
                }

                string responseMsg = response.Content.ReadAsStringAsync().Result;
                BcsCandles candleResponse = JsonConvert.DeserializeObject<BcsCandles>(responseMsg);

                if (candleResponse == null ||
                    candleResponse.bars == null)
                {
                    return new List<Candle>();
                }

                return ConvertToOsEngineCandles(candleResponse.bars, security);
            }
            catch (Exception ex)
            {
                SendLogMessage($"BCS candles request error: {ex.Message}", LogMessageType.Error);
                return null;
            }
        }

        private List<Candle> ConvertToOsEngineCandles(Bar[] bars, Security security)
        {
            List<Candle> candles = new List<Candle>();

            for (int i = bars.Length - 1; i >= 0; i--)
            {
                Bar bar = bars[i];

                Candle candle = new Candle();

                DateTime startTimeUtc = DateTime.Parse(bar.time, null, DateTimeStyles.RoundtripKind);

                candle.TimeStart = TimeZoneInfo.ConvertTimeFromUtc(startTimeUtc, _moscowTimeZone);
                candle.Open = bar.open.ToDecimal();
                candle.High = Math.Round(bar.high.ToDecimal(), security.Decimals);
                candle.Low = Math.Round(bar.low.ToDecimal(), security.Decimals);
                candle.Close = Math.Round(bar.close.ToDecimal(), security.Decimals);
                candle.Volume = Math.Round(bar.volume.ToDecimal(), security.DecimalsVolume);
                candle.State = CandleState.Finished;
                candles.Add(candle);
            }

            return candles;
        }

        private bool CheckTime(DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if (startTime >= endTime ||
                startTime >= DateTime.UtcNow ||
                actualTime > endTime ||
                actualTime > DateTime.UtcNow)
            {
                return false;
            }

            return true;
        }

        private string GetClassCode(Security security)
        {
            if (security.NameClass == null)
            {
                return null;
            }

            string[] splitNameClass = security.NameClass.Split('-');

            if (splitNameClass.Length < 2)
            {
                return security.NameClass;
            }

            return splitNameClass[splitNameClass.Length - 1];
        }

        private string GetCandleTimeFrame(TimeSpan timeFrame)
        {
            if (timeFrame.TotalMinutes == 1)
            {
                return "M1";
            }
            else if (timeFrame.TotalMinutes == 5)
            {
                return "M5";
            }
            else if (timeFrame.TotalMinutes == 15)
            {
                return "M15";
            }
            else if (timeFrame.TotalMinutes == 30)
            {
                return "M30";
            }
            else if (timeFrame.TotalHours == 1)
            {
                return "H1";
            }
            else if (timeFrame.TotalHours == 4)
            {
                return "H4";
            }
            else if (timeFrame.TotalDays == 1)
            {
                return "D";
            }

            return null;
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        #endregion

        #region 6 WebSocket creation

        private readonly string _wsHostMarketData = "wss://ws.broker.ru/trade-api-market-data-connector/api/v1/market-data/ws";
        private readonly string _wsHostPortfolio = "wss://ws.broker.ru/trade-api-bff-portfolio/api/v1/portfolio/ws";
        private readonly string _wsHostOrders = "wss://ws.broker.ru/trade-api-bff-operations/api/v1/orders/execution/ws";
        private readonly string _wsHostMyTrades = "wss://ws.broker.ru/trade-api-bff-operations/api/v1/orders/transaction/ws";

        private string _socketLocker = "webSocketLockerBcs";

        private void CreatePublicWebSocketConnect()
        {
            try
            {
                if (WebSocketDataMessage == null)
                {
                    WebSocketDataMessage = new ConcurrentQueue<string>();
                }

                _socketDataIsActive = false;

                _webSocketPublicList.Add(CreateNewSocketMarketData());
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private WebSocket CreateNewSocketMarketData()
        {
            try
            {
                WebSocket webSocketMarketData = new WebSocket(_wsHostMarketData);
                webSocketMarketData.SetHeader("Authorization", "Bearer " + _apiAccessToken);
                webSocketMarketData.EmitOnPing = true;
                webSocketMarketData.OnOpen += WebSocketData_Opened;
                webSocketMarketData.OnClose += WebSocketData_Closed;
                webSocketMarketData.OnMessage += WebSocketData_MessageReceived;
                webSocketMarketData.OnError += WebSocketData_Error;

                if (_myProxy != null)
                {
                    webSocketMarketData.SetProxy(_myProxy);
                }

                webSocketMarketData.ConnectAsync(TimeSpan.FromSeconds(30), _clientForSocket);

                return webSocketMarketData;
            }
            catch (Exception ex)
            {
                SendLogMessage("Create socket market data error: " + ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private void CreatePrivateWebSocketConnect()
        {
            try
            {
                _socketPortfolioIsActive = false;

                lock (_socketLocker)
                {
                    WebSocketPortfolioMessage = new ConcurrentQueue<string>();
                    WebSocketMyOrdersAndTradesMessage = new ConcurrentQueue<string>();

                    _webSocketPortfolio = new WebSocket(_wsHostPortfolio);
                    _webSocketPortfolio.SetHeader("Authorization", "Bearer " + _apiAccessToken);
                    _webSocketPortfolio.EmitOnPing = true;
                    _webSocketPortfolio.OnOpen += WebSocketPortfolio_Opened;
                    _webSocketPortfolio.OnClose += WebSocketPortfolio_Closed;
                    _webSocketPortfolio.OnMessage += WebSocketPortfolio_MessageReceived;
                    _webSocketPortfolio.OnError += WebSocketPortfolio_Error;

                    if (_myProxy != null)
                    {
                        _webSocketPortfolio.SetProxy(_myProxy);
                    }

                    _webSocketPortfolio.ConnectAsync(TimeSpan.FromSeconds(30), _clientForSocket);

                    _webSocketOrders = new WebSocket(_wsHostOrders);
                    _webSocketOrders.SetHeader("Authorization", "Bearer " + _apiAccessToken);
                    _webSocketOrders.EmitOnPing = true;
                    _webSocketOrders.OnOpen += WebSocketOrders_Opened;
                    _webSocketOrders.OnClose += WebSocketOrders_Closed;
                    _webSocketOrders.OnMessage += WebSocketOrders_MessageReceived;
                    _webSocketOrders.OnError += WebSocketOrders_Error;

                    if (_myProxy != null)
                    {
                        _webSocketOrders.SetProxy(_myProxy);
                    }

                    _webSocketOrders.ConnectAsync(TimeSpan.FromSeconds(30), _clientForSocket);

                    _webSocketMyTrades = new WebSocket(_wsHostMyTrades);
                    _webSocketMyTrades.SetHeader("Authorization", "Bearer " + _apiAccessToken);
                    _webSocketMyTrades.EmitOnPing = true;
                    _webSocketMyTrades.OnOpen += WebSocketMyTrades_Opened;
                    _webSocketMyTrades.OnClose += WebSocketMyTrades_Closed;
                    _webSocketMyTrades.OnMessage += WebSocketMyTrades_MessageReceived;
                    _webSocketMyTrades.OnError += WebSocketMyTrades_Error;

                    if (_myProxy != null)
                    {
                        _webSocketMyTrades.SetProxy(_myProxy);
                    }

                    _webSocketMyTrades.ConnectAsync(TimeSpan.FromSeconds(30), _clientForSocket);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Create private sockets error" + ex.ToString(), LogMessageType.Error);
            }
        }
        private void DeleteWebSocketConnection()
        {
            try
            {
                lock (_socketLocker)
                {
                    if (_webSocketPublicList != null)
                    {
                        try
                        {
                            for (int i = 0; i < _webSocketPublicList.Count; i++)
                            {
                                WebSocket webSocketPublic = _webSocketPublicList[i];

                                webSocketPublic.OnOpen -= WebSocketData_Opened;
                                webSocketPublic.OnClose -= WebSocketData_Closed;
                                webSocketPublic.OnMessage -= WebSocketData_MessageReceived;
                                webSocketPublic.OnError -= WebSocketData_Error;

                                if (webSocketPublic.ReadyState == WebSocketState.Open)
                                {
                                    webSocketPublic.CloseAsync();
                                }

                                webSocketPublic = null;
                            }
                        }
                        catch
                        {
                            // ignore
                        }

                        _webSocketPublicList.Clear();
                    }

                    if (_webSocketPortfolio != null)
                    {
                        try
                        {
                            _webSocketPortfolio.OnOpen -= WebSocketPortfolio_Opened;
                            _webSocketPortfolio.OnClose -= WebSocketPortfolio_Closed;
                            _webSocketPortfolio.OnMessage -= WebSocketPortfolio_MessageReceived;
                            _webSocketPortfolio.OnError -= WebSocketPortfolio_Error;
                            _webSocketPortfolio.CloseAsync();
                        }
                        catch
                        {
                            // ignore
                        }
                    }

                    if (_webSocketOrders != null)
                    {
                        try
                        {
                            _webSocketOrders.OnOpen -= WebSocketOrders_Opened;
                            _webSocketOrders.OnClose -= WebSocketOrders_Closed;
                            _webSocketOrders.OnMessage -= WebSocketOrders_MessageReceived;
                            _webSocketOrders.OnError -= WebSocketOrders_Error;
                            _webSocketOrders.CloseAsync();
                        }
                        catch
                        {
                            // ignore
                        }
                    }

                    if (_webSocketMyTrades != null)
                    {
                        try
                        {
                            _webSocketMyTrades.OnOpen -= WebSocketMyTrades_Opened;
                            _webSocketMyTrades.OnClose -= WebSocketMyTrades_Closed;
                            _webSocketMyTrades.OnMessage -= WebSocketMyTrades_MessageReceived;
                            _webSocketMyTrades.OnError -= WebSocketMyTrades_Error;
                            _webSocketMyTrades.CloseAsync();
                        }
                        catch
                        {
                            // ignore
                        }
                    }
                }
            }
            catch
            {

            }
            finally
            {
                _webSocketPortfolio = null;
                _webSocketOrders = null;
                _webSocketMyTrades = null;
            }
        }

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

            if (_socketOrdersIsActive == false)
            {
                return;
            }

            if (_socketMyTradesIsActive == false)
            {
                return;
            }

            try
            {
                lock (_activationLocker)
                {
                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        if (_httpClient == null)
                            CreateHttpClient();

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

        #endregion

        #region 7 WebSocket events

        private void WebSocketData_Opened(object sender, EventArgs e)
        {
            SendLogMessage("Socket market data activated", LogMessageType.System);
            _socketDataIsActive = true;
            CheckActivationSockets();
        }

        private void WebSocketData_Closed(object sender, CloseEventArgs e)
        {
            try
            {
                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    string message = this.GetType().Name + OsLocalization.Market.Message101 + "\n";
                    message += OsLocalization.Market.Message102;

                    SendLogMessage(message, LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketData_Error(object sender, ErrorEventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                if (e.Exception != null)
                {
                    string message = e.Exception.ToString();

                    if (message.Contains("The remote party closed the WebSocket connection"))
                    {
                        // ignore
                    }
                    else
                    {
                        SendLogMessage(e.Exception.ToString(), LogMessageType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Data socket error" + ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketData_MessageReceived(object sender, MessageEventArgs e)
        {
            try
            {
                if (e == null)
                {
                    return;
                }

                if (string.IsNullOrEmpty(e.Data))
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

                WebSocketDataMessage.Enqueue(e.Data);
            }
            catch (Exception error)
            {
                SendLogMessage("Market data socket error. " + error.ToString(), LogMessageType.Error);
            }
        }

        // Portfolio socket
        private void WebSocketPortfolio_Opened(object sender, EventArgs e)
        {
            SendLogMessage("Socket Portfolio activated", LogMessageType.System);
            _socketPortfolioIsActive = true;
            CheckActivationSockets();
        }

        private void WebSocketPortfolio_Closed(object sender, CloseEventArgs e)
        {
            try
            {
                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    string message = this.GetType().Name + OsLocalization.Market.Message101 + "\n";
                    message += OsLocalization.Market.Message102;

                    SendLogMessage(message, LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPortfolio_Error(object sender, ErrorEventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                if (e.Exception != null)
                {
                    string message = e.Exception.ToString();

                    if (message.Contains("The remote party closed the WebSocket connection"))
                    {
                        // ignore
                    }
                    else
                    {
                        SendLogMessage(e.Exception.ToString(), LogMessageType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Portfolio socket error" + ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPortfolio_MessageReceived(object sender, MessageEventArgs e)
        {
            try
            {
                if (e == null)
                {
                    return;
                }

                if (string.IsNullOrEmpty(e.Data))
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

                WebSocketPortfolioMessage.Enqueue(e.Data);
            }
            catch (Exception error)
            {
                SendLogMessage("Portfolio socket error. " + error.ToString(), LogMessageType.Error);
            }
        }

        // Orders socket
        private void WebSocketOrders_Opened(object sender, EventArgs e)
        {
            SendLogMessage("Socket Orders activated", LogMessageType.System);
            _socketOrdersIsActive = true;
            CheckActivationSockets();
        }

        private void WebSocketOrders_Closed(object sender, CloseEventArgs e)
        {
            try
            {
                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    string message = this.GetType().Name + OsLocalization.Market.Message101 + "\n";
                    message += OsLocalization.Market.Message102;

                    SendLogMessage(message, LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketOrders_Error(object sender, ErrorEventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                if (e.Exception != null)
                {
                    string message = e.Exception.ToString();

                    if (message.Contains("The remote party closed the WebSocket connection"))
                    {
                        // ignore
                    }
                    else
                    {
                        SendLogMessage(e.Exception.ToString(), LogMessageType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("My orders socket error" + ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketOrders_MessageReceived(object sender, MessageEventArgs e)
        {
            try
            {
                if (e == null)
                {
                    return;
                }
                if (string.IsNullOrEmpty(e.Data))
                {
                    return;
                }

                if (WebSocketMyOrdersAndTradesMessage == null)
                {
                    return;
                }

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                WebSocketMyOrdersAndTradesMessage.Enqueue(e.Data);
            }
            catch (Exception error)
            {
                SendLogMessage("My orders socket error. " + error.ToString(), LogMessageType.Error);
            }
        }

        // My trades socket events
        private void WebSocketMyTrades_Opened(object sender, EventArgs e)
        {
            SendLogMessage("Socket My trades activated", LogMessageType.System);
            _socketMyTradesIsActive = true;
            CheckActivationSockets();
        }

        private void WebSocketMyTrades_Closed(object sender, CloseEventArgs e)
        {
            try
            {
                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    string message = this.GetType().Name + OsLocalization.Market.Message101 + "\n";
                    message += OsLocalization.Market.Message102;

                    SendLogMessage(message, LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketMyTrades_Error(object sender, ErrorEventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                if (e.Exception != null)
                {
                    string message = e.Exception.ToString();

                    if (message.Contains("The remote party closed the WebSocket connection"))
                    {
                        // ignore
                    }
                    else
                    {
                        SendLogMessage(e.Exception.ToString(), LogMessageType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("My trades socket error" + ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketMyTrades_MessageReceived(object sender, MessageEventArgs e)
        {
            try
            {
                if (e == null)
                {
                    return;
                }
                if (string.IsNullOrEmpty(e.Data))
                {
                    return;
                }

                if (WebSocketMyOrdersAndTradesMessage == null)
                {
                    return;
                }

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                WebSocketMyOrdersAndTradesMessage.Enqueue(e.Data);
            }
            catch (Exception error)
            {
                SendLogMessage("My trades socket error. " + error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region 8 WebSocket Security subscribe

        private RateGate _rateGateSubscribe = new RateGate(1, TimeSpan.FromMilliseconds(100));

        List<Security> _subscribedSecurities = new List<Security>();

        public void Subscribe(Security security)
        {
            try
            {
                if (_hasLimitReached)
                {
                    return;
                }

                for (int i = 0; i < _subscribedSecurities.Count; i++)
                {
                    if (_subscribedSecurities[i].Name == security.Name)
                    {
                        return;
                    }
                }

                _rateGateSubscribe.WaitToProceed();

                _subscribedSecurities.Add(security);

                if (_webSocketPublicList.Count == 0)
                {
                    return;
                }

                WebSocket webSocketPublic = _webSocketPublicList[_webSocketPublicList.Count - 1];

                if (webSocketPublic.ReadyState == WebSocketState.Open
                    && _subscribedSecurities.Count != 0
                    && _subscribedSecurities.Count % 30 == 0)
                {
                    WebSocket newSocket = CreateNewSocketMarketData();

                    DateTime timeEnd = DateTime.Now.AddSeconds(10);

                    while (newSocket.ReadyState != WebSocketState.Open)
                    {
                        Thread.Sleep(1000);

                        if (timeEnd < DateTime.Now)
                        {
                            break;
                        }
                    }

                    if (newSocket.ReadyState == WebSocketState.Open)
                    {
                        _webSocketPublicList.Add(newSocket);
                        webSocketPublic = newSocket;
                    }
                }

                if (webSocketPublic != null)
                {
                    string depth = string.Empty;

                    if (((ServerParameterBool)ServerParameters[17]).Value == false)
                    {
                        depth = "1";
                    }
                    else
                    {
                        depth = ((ServerParameterEnum)ServerParameters[8]).Value;
                    }

                    // trades subscription
                    webSocketPublic.SendAsync($"{{\"subscribeType\": 0,\"dataType\": 2,\"instruments\": [{{\"ticker\": \"{security.Name}\",\"classCode\": \"{GetClassCode(security)}\"}}]}}");

                    _rateGateSubscribe.WaitToProceed();
                    // market depth subscription
                    webSocketPublic.SendAsync($"{{\"subscribeType\": 0,\"dataType\": 0,\"depth\": \"{depth}\" ,\"instruments\": [{{\"ticker\": \"{security.Name}\",\"classCode\": \"{GetClassCode(security)}\"}}]}}");
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"Subscribe error {security.Name} " + exception.ToString(), LogMessageType.Error);
            }
        }

        private void UnsubscribeAllSecurities()
        {
            try
            {
                if (_webSocketPublicList != null
                  && _webSocketPublicList.Count != 0)
                {
                    string depth = string.Empty;

                    if (((ServerParameterBool)ServerParameters[17]).Value == false)
                    {
                        depth = "1";
                    }
                    else
                    {
                        depth = ((ServerParameterEnum)ServerParameters[8]).Value;
                    }

                    for (int i = 0; i < _webSocketPublicList.Count; i++)
                    {
                        _rateGateSubscribe.WaitToProceed();

                        WebSocket webSocketPublic = _webSocketPublicList[i];

                        if (webSocketPublic != null && webSocketPublic?.ReadyState == WebSocketState.Open)
                        {
                            if (_subscribedSecurities != null)
                            {
                                List<string> argsList = new List<string>();

                                for (int j = 0; j < _subscribedSecurities.Count; j++)
                                {
                                    Security security1 = _subscribedSecurities[j];

                                    argsList.Add($"{{\"ticker\": \"{security1.Name}\",\"classCode\": \"{GetClassCode(security1)}\"}}");
                                }

                                if (argsList.Count > 0)
                                {
                                    string unsubscrTradesMessage = $"{{\"subscribeType\": 1,\"dataType\": 2,\"instruments\":[{string.Join(",", argsList)}]}}";

                                    webSocketPublic.SendAsync(unsubscrTradesMessage);

                                    _rateGateSubscribe.WaitToProceed();

                                    string unsubscrDepthMessage = $"{{\"subscribeType\": 1,\"dataType\": 0,\"depth\": \"{depth}\",\"instruments\":[{string.Join(",", argsList)}]}}";

                                    webSocketPublic.SendAsync(unsubscrDepthMessage);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"Unsubscribe error: " + exception.ToString(), LogMessageType.Error);
            }
        }

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent { add { } remove { } }

        #endregion

        #region 9 WebSocket parsing the messages

        private ConcurrentQueue<string> WebSocketDataMessage = new ConcurrentQueue<string>();
        private ConcurrentQueue<string> WebSocketPortfolioMessage = new ConcurrentQueue<string>();
        private ConcurrentQueue<string> WebSocketMyOrdersAndTradesMessage = new ConcurrentQueue<string>();
        private string _subscribeLimitLocker = "limitLocker";
        private bool _hasLimitReached = false;

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

                    if (message.StartsWith("{\"dis"))
                    {
                        WarningSocketMessage warning = JsonConvert.DeserializeAnonymousType(message, new WarningSocketMessage());

                        if (warning != null)
                        {
                            if (warning.displayOptions.text.StartsWith("Превышен лимит"))
                            {
                                lock (_subscribeLimitLocker)
                                {
                                    if (!_hasLimitReached)
                                    {
                                        _hasLimitReached = true;
                                        SendLogMessage($"Внимание! {warning.displayOptions.text}.\n Подписано инструментов: {_subscribedSecurities.Count}", LogMessageType.Error);
                                    }
                                }
                            }

                            Thread.Sleep(500);
                            continue;
                        }
                    }

                    if (message.StartsWith("{\"err"))
                    {
                        SendLogMessage($"Ошибка чтения рыночных данных! {message}", LogMessageType.Error);
                        Thread.Sleep(2000);
                        continue;
                    }

                    PublicMarketDataResponse response = JsonConvert.DeserializeAnonymousType(message, new PublicMarketDataResponse());

                    if (response.ResponseType != null)
                    {
                        if (response.ResponseType.Equals("LastTrades"))
                        {
                            if (response.Errors != null && response.Errors.Count > 0)
                            {
                                for (int i = 0; i < response.Errors.Count; i++)
                                {
                                    Error error = response.Errors[i];
                                    SendLogMessage($"Ошибка: {error.Message} (Код: {error.Code})", LogMessageType.Error);
                                }
                            }
                            else
                            {
                                UpdateTrade(response);
                            }
                        }
                        if (response.ResponseType.Equals("OrderBook"))
                        {
                            if (response.Errors != null && response.Errors.Count > 0)
                            {
                                for (int j = 0; j < response.Errors.Count; j++)
                                {
                                    Error error = response.Errors[j];
                                    SendLogMessage($"Ошибка: {error.Message} (Код: {error.Code})", LogMessageType.Error);
                                }
                            }
                            else
                            {
                                UpdateMarketDepth(response);
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

        private void UpdateTrade(PublicMarketDataResponse tradeData)
        {
            Trade trade = new Trade();
            trade.SecurityNameCode = tradeData.Ticker;
            trade.Time = ConvertUtsStringToDateTimeRu(tradeData.DateTime);

            if (_ignoreMorningAuctionTrades && trade.Time.Hour < 7)
            {
                return;
            }

            trade.Price = tradeData.Price.ToDecimal();
            trade.Side = tradeData.Side == "BUY" ? Side.Buy : Side.Sell;
            trade.Volume = tradeData.Volume.ToDecimal();

            trade.Id = trade.Time.Ticks.ToString();

            NewTradesEvent?.Invoke(trade);
        }

        private void UpdateMarketDepth(PublicMarketDataResponse depthData)
        {
            if (depthData.Bids == null ||
                depthData.Asks == null)
            {
                return;
            }

            if (depthData.Bids.Count == 0 ||
                depthData.Asks.Count == 0)
            {
                return;
            }

            MarketDepth depth = new MarketDepth();
            depth.SecurityNameCode = depthData.Ticker;

            depth.Time = ConvertUtsStringToDateTimeRu(depthData.DateTime);

            for (int i = 0; i < depthData.Bids.Count; i++)
            {
                MarketDepthLevel newBid = new MarketDepthLevel();
                newBid.Price = depthData.Bids[i].Price.ToDouble();
                newBid.Bid = depthData.Bids[i].Quantity.ToDouble() / 10;
                depth.Bids.Add(newBid);
            }

            for (int i = 0; i < depthData.Asks.Count; i++)
            {
                MarketDepthLevel newAsk = new MarketDepthLevel();
                newAsk.Price = depthData.Asks[i].Price.ToDouble();
                newAsk.Ask = depthData.Asks[i].Quantity.ToDouble() / 10;
                depth.Asks.Add(newAsk);
            }

            if (_lastMdTime != DateTime.MinValue &&
                _lastMdTime >= depth.Time)
            {
                depth.Time = _lastMdTime.AddTicks(1);
            }

            _lastMdTime = depth.Time;

            MarketDepthEvent?.Invoke(depth);
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

                    List<BcsPortfolio> bcsPortfolios = JsonConvert.DeserializeAnonymousType(message, new List<BcsPortfolio>());

                    if (bcsPortfolios != null && bcsPortfolios.Count > 0)
                    {
                        UpdateMyPortfolio(bcsPortfolios);
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void OrderStateMessageReader()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (WebSocketMyOrdersAndTradesMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message;

                    WebSocketMyOrdersAndTradesMessage.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    BcsOrdersResponse orderResponse = JsonConvert.DeserializeAnonymousType(message, new BcsOrdersResponse());

                    if (orderResponse != null && orderResponse.Data != null)
                    {
                        if (orderResponse.Data.MessageType == "9")
                        {
                            SendLogMessage("Приостановка размещения ордера:\n" + orderResponse.Data.RejectReason, LogMessageType.Error);
                            continue;
                        }

                        if (orderResponse.Data.OrderStatus == "5") // изменение ордера
                        {
                            //на бирже номер изменился, фиксируем последовательно в список
                            string oldNumber = orderResponse.Data.OrderNumber;
                            string newNumber = orderResponse.Data.OrderId.Split('-')[2];

                            if (_changedOrderNumsMarket.Count > 0)
                            {
                                // Проверяем менялся ли уже этот ордер
                                bool hasOrderChanged = false;

                                for (int i = 0; i < _changedOrderNumsMarket.Count; i++)
                                {
                                    List<string> nums = _changedOrderNumsMarket[i];

                                    if (nums[^1] == oldNumber)
                                    {
                                        nums.Add(newNumber);
                                        hasOrderChanged = true;
                                        break;
                                    }
                                }

                                if (!hasOrderChanged)
                                {
                                    _changedOrderNumsMarket.Add([oldNumber, newNumber]);
                                }
                            }
                            else
                            {
                                _changedOrderNumsMarket.Add([oldNumber, newNumber]);
                            }

                            continue;
                        }

                        if (orderResponse.Data.OrderStatus == "6" || orderResponse.Data.OrderStatus == "9") //  в процессе отмены или замены
                        {
                            continue;
                        }

                        UpdateMyOrder(orderResponse);
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void UpdateMyOrder(BcsOrdersResponse orderEvent)
        {
            try
            {
                if (orderEvent.Data.OrderStatus == "6" || orderEvent.Data.OrderStatus == "9" || orderEvent.Data.OrderStatus == "5")
                {
                    return;
                }

                OrderStateType stateType = GetOrderState(orderEvent.Data.OrderStatus);

                if (stateType == OrderStateType.Fail)
                {
                    SendLogMessage("Ордер отклонён!\n" + orderEvent.Data.RejectReason, LogMessageType.Error);
                }

                if (stateType == OrderStateType.Active && orderEvent.Data.OrderType.Equals("1")) // игнор размещения маркет ордера
                {
                    return;
                }

                Order newOrder = new Order();

                Security security = GetSecurityByName(orderEvent.Data.Ticker, orderEvent.Data.ClassCode);

                if (security != null)
                {
                    newOrder.SecurityNameCode = security.Name;
                    newOrder.SecurityClassCode = security.NameClass;

                    if (stateType == OrderStateType.Done || stateType == OrderStateType.Partial)
                    {
                        newOrder.Volume = orderEvent.Data.LastQuantity.ToDecimal() / security.Lot;
                    }
                    else
                    {
                        newOrder.Volume = orderEvent.Data.OrderQuantity.ToDecimal() / security.Lot;
                    }
                }
                else
                {
                    newOrder.SecurityNameCode = orderEvent.Data.Ticker;
                    newOrder.SecurityClassCode = orderEvent.Data.ClassCode;

                    if (stateType == OrderStateType.Done || stateType == OrderStateType.Partial)
                    {
                        newOrder.Volume = orderEvent.Data.LastQuantity.ToDecimal();
                    }
                    else
                    {
                        newOrder.Volume = orderEvent.Data.OrderQuantity.ToDecimal();
                    }
                }

                newOrder.TimeCallBack = ConvertUtsStringToDateTimeRu(orderEvent.Data.TransactionTime);

                if (stateType == OrderStateType.Done)
                {
                    newOrder.TimeDone = ConvertUtsStringToDateTimeRu(orderEvent.Data.TransactionTime);
                }
                else if (stateType == OrderStateType.Active)
                {
                    newOrder.TimeCreate = ConvertUtsStringToDateTimeRu(orderEvent.Data.TransactionTime);
                }
                else if (stateType == OrderStateType.Cancel)
                {
                    newOrder.TimeCancel = ConvertUtsStringToDateTimeRu(orderEvent.Data.TransactionTime);
                }

                if (Guid.TryParse(orderEvent.ClientOrderId, out Guid clientOrderId))
                {
                    newOrder.NumberUser = GetOrderUserNumber(clientOrderId);

                    AddOrderIdAndUserNum(orderEvent.Data.OrderId, newOrder.NumberUser);
                }

                newOrder.NumberMarket = orderEvent.Data.OrderNumber;

                if (stateType == OrderStateType.Done || stateType == OrderStateType.Partial || stateType == OrderStateType.Cancel)
                {
                    // если ордер менялся отчет придет с новым номером ордера, а надо указать первоначальный NumberMarket чтобы в системе обновился статус позиции

                    if (_changedOrderNumsMarket.Count > 0)
                    {
                        for (int i = 0; i < _changedOrderNumsMarket.Count; i++)
                        {
                            List<string> nums = _changedOrderNumsMarket[i];

                            if (nums[^1] == orderEvent.Data.OrderNumber)
                            {
                                // нашли список с номерами ордера, которому меняли цену
                                newOrder.NumberMarket = nums[0];
                                _changedOrderNumsMarket.Remove(nums);

                                break;
                            }
                        }
                    }
                }

                newOrder.Side = orderEvent.Data.Side.Equals("1") ? Side.Buy : Side.Sell;
                newOrder.State = stateType;
                newOrder.TypeOrder = orderEvent.Data.OrderType.Equals("1") ? OrderPriceType.Market : OrderPriceType.Limit;
                newOrder.Price = newOrder.TypeOrder == OrderPriceType.Limit ? orderEvent.Data.Price.ToDecimal() : orderEvent.Data.AveragePrice.ToDecimal();
                newOrder.ServerType = ServerType.BCS;

                if (_myPortfolios.Count == 1)
                {
                    newOrder.PortfolioNumber = _myPortfolios[0].Number;
                }

                string orderString = JsonConvert.SerializeObject(newOrder);

                MyOrderEvent?.Invoke(newOrder);

                if (orderEvent.Data.ExecutionType == "11") // сделка
                {
                    UpdateMyTrade(orderEvent, security.Lot, newOrder.NumberMarket, newOrder.Price);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($" Update my order error: {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdateMyTrade(BcsOrdersResponse dealEvent, decimal lot, string orderNumber, decimal price)
        {
            try
            {
                MyTrade trade = new MyTrade();
                trade.SecurityNameCode = dealEvent.Data.Ticker;
                trade.Price = price;
                trade.Volume = dealEvent.Data.ExecutedQuantity.ToDecimal() / lot;
                trade.NumberOrderParent = orderNumber;
                trade.NumberTrade = dealEvent.Data.ExecutionId;
                trade.Time = ConvertUtsStringToDateTimeRu(dealEvent.Data.TransactionTime);
                trade.Side = dealEvent.Data.Side.Equals("1") ? Side.Buy : Side.Sell;

                MyTradeEvent?.Invoke(trade);
            }
            catch (Exception ex)
            {
                SendLogMessage($" Update my trade error: {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private OrderStateType GetOrderState(string status)
        {
            OrderStateType stateType;

            if (status == "0" || status == "5")
            {
                stateType = OrderStateType.Active;
            }
            else if (status == "8")
            {
                stateType = OrderStateType.Fail;
            }
            else if (status == "1")
            {
                stateType = OrderStateType.Partial;
            }
            else if (status == "2")
            {
                stateType = OrderStateType.Done;
            }
            else if (status == "4")
            {
                stateType = OrderStateType.Cancel;
            }
            else if (status == "10")
            {
                stateType = OrderStateType.Pending;
            }
            else
            {
                stateType = OrderStateType.None;
            }

            return stateType;
        }

        public event Action<MyTrade> MyTradeEvent;
        public event Action<Order> MyOrderEvent;

        #endregion

        #region 10 Trade

        private RateGate _rateGateOrdersOperations = new RateGate(1, TimeSpan.FromMilliseconds(100));
        private RateGate _rateGateOrderStatus = new RateGate(1, TimeSpan.FromMilliseconds(100));
        private RateGate _rateGateGetOrders = new RateGate(1, TimeSpan.FromMilliseconds(100));
        private string _orderNumbersLocker = "orderNumbersLocker";
        private Dictionary<int, Guid> _guidByNumberOrders = new Dictionary<int, Guid>();
        private Dictionary<Guid, int> _numberByGuidOrders = new Dictionary<Guid, int>();
        private Queue<int> _orderQueue = new Queue<int>();
        private List<List<string>> _changedOrderNumsMarket = [];
        private Dictionary<string, int> _userNumberByOrderId = new Dictionary<string, int>();

        public void SendOrder(Order order)
        {
            _rateGateOrdersOperations.WaitToProceed();

            try
            {
                string endPoint = "/trade-api-bff-operations/api/v1/orders";

                Dictionary<string, dynamic> jsonContent = new Dictionary<string, dynamic>();

                Guid orderId = Guid.NewGuid();
                string side = order.Side == Side.Buy ? "1" : "2";
                string type = order.TypeOrder == OrderPriceType.Market ? "1" : "2";

                decimal quantity = 0;
                Security security = _subscribedSecurities.Find(s => s.Name == order.SecurityNameCode && s.NameClass == order.SecurityClassCode);

                if (security != null)
                {
                    quantity = order.Volume * security.Lot;
                }

                jsonContent.Add("clientOrderId", orderId.ToString());
                jsonContent.Add("side", side);
                jsonContent.Add("orderType", type);
                jsonContent.Add("orderQuantity", quantity);
                jsonContent.Add("ticker", order.SecurityNameCode);
                jsonContent.Add("classCode", order.SecurityClassCode.Split('-')[1]);

                if (order.TypeOrder == OrderPriceType.Limit)
                {
                    jsonContent.Add("price", order.Price);
                }

                AddOrderIds(order.NumberUser, orderId);

                string jsonRequest = JsonConvert.SerializeObject(jsonContent);

                HttpResponseMessage sendOrderResponse = CreateHttpRequestAsync(endPoint, HttpMethod.Post, jsonRequest).Result;

                if (sendOrderResponse.StatusCode == HttpStatusCode.OK)
                {
                    string responseMsg = sendOrderResponse.Content.ReadAsStringAsync().Result;
                    BcsOrderResponse response = JsonConvert.DeserializeAnonymousType(responseMsg, new BcsOrderResponse());

                    if (response.status == "OK")
                    {
                        // ignore
                    }
                    else
                    {
                        CreateOrderFail(order);
                        SendLogMessage($"Order Id: {response.clientOrderId} fail. Status: {response.status}", LogMessageType.Error);
                    }
                }
                else if (sendOrderResponse.StatusCode == HttpStatusCode.Forbidden)
                {
                    string responseMsg = sendOrderResponse.Content.ReadAsStringAsync().Result;

                    CreateOrderFail(order);
                    SendLogMessage($"Order Fail. Status: {sendOrderResponse.StatusCode} || msg:{responseMsg}", LogMessageType.Error);
                }
                else
                {
                    CreateOrderFail(order);
                    SendLogMessage($"Order Fail. Status: {sendOrderResponse.StatusCode} || msg:{sendOrderResponse.ReasonPhrase}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                CreateOrderFail(order);
                SendLogMessage("Order send error " + exception.ToString(), LogMessageType.Error);
            }
        }

        private void CreateOrderFail(Order order)
        {
            order.State = OrderStateType.Fail;

            MyOrderEvent?.Invoke(order);
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
            _rateGateOrdersOperations.WaitToProceed();

            try
            {
                string endPoint = "trade-api-bff-operations/api/v1/orders/edit";

                Dictionary<string, dynamic> jsonContent = new Dictionary<string, dynamic>();

                Guid clientOrderId = Guid.NewGuid();
                string type = order.TypeOrder == OrderPriceType.Market ? "1" : "2";

                decimal quantity = 0;
                Security security = GetSecurityByName(order.SecurityNameCode, order.SecurityClassCode);

                if (security != null)
                {
                    quantity = (order.Volume - order.VolumeExecute) * security.Lot;
                }

                string orderId = "";

                try
                {
                    orderId = GetClientOrderId(order.NumberUser).ToString();
                }
                catch (KeyNotFoundException ex)
                {
                    SendLogMessage("Change order error: " + ex.Message, LogMessageType.Error);
                    return;
                }

                jsonContent.Add("orderIdType", 1);
                jsonContent.Add("orderId", orderId);
                jsonContent.Add("clientOrderId", clientOrderId.ToString());
                jsonContent.Add("orderType", type);

                if (order.TypeOrder == OrderPriceType.Limit)
                {
                    jsonContent.Add("price", newPrice);
                }

                jsonContent.Add("orderQuantity", quantity);

                string jsonRequest = JsonConvert.SerializeObject(jsonContent);

                HttpResponseMessage changeOrderResponse = CreateHttpRequestAsync(endPoint, HttpMethod.Post, jsonRequest).Result;

                if (changeOrderResponse.StatusCode == HttpStatusCode.OK)
                {
                    string responseMsg = changeOrderResponse.Content.ReadAsStringAsync().Result;
                    BcsOrderResponse response = JsonConvert.DeserializeAnonymousType(responseMsg, new BcsOrderResponse());

                    if (response.status == "OK")
                    {
                        AddOrderIds(order.NumberUser, clientOrderId);
                        order.Price = newPrice;
                        MyOrderEvent?.Invoke(order);
                    }
                    else
                    {
                        SendLogMessage($"Order Id: {response.clientOrderId} change price error. Status: {response.status}", LogMessageType.Error);
                    }
                }
                else if (changeOrderResponse.StatusCode == HttpStatusCode.Forbidden)
                {
                    string responseMsg = changeOrderResponse.Content.ReadAsStringAsync().Result;
                    SendLogMessage($"Change price order forbidden. Status: {changeOrderResponse.StatusCode} || msg:{responseMsg}", LogMessageType.Error);
                }
                else
                {
                    SendLogMessage($"Change price order error. Body: {jsonRequest}\n Status: {changeOrderResponse.StatusCode} || msg:{changeOrderResponse.ReasonPhrase}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Change price order request error: " + exception.ToString(), LogMessageType.Error);
            }
        }

        public bool CancelOrder(Order order)
        {
            _rateGateOrdersOperations.WaitToProceed();

            try
            {
                string endPoint = "/trade-api-bff-operations/api/v1/orders/cancel";

                Dictionary<string, dynamic> jsonContent = new Dictionary<string, dynamic>();

                Guid clientOrderId = Guid.NewGuid();

                string orderId = "";

                try
                {
                    orderId = GetClientOrderId(order.NumberUser).ToString();
                }
                catch (KeyNotFoundException ex)
                {
                    SendLogMessage("Order cancel error: " + ex.Message, LogMessageType.Error);
                    return false;
                }

                jsonContent.Add("orderIdType", 1);
                jsonContent.Add("orderId", orderId);
                jsonContent.Add("clientOrderId", clientOrderId.ToString());

                string jsonRequest = JsonConvert.SerializeObject(jsonContent);
                HttpResponseMessage cancelOrderResponse = CreateHttpRequestAsync(endPoint, HttpMethod.Post, jsonRequest).Result;

                if (cancelOrderResponse.StatusCode == HttpStatusCode.OK)
                {
                    string responseMsg = cancelOrderResponse.Content.ReadAsStringAsync().Result;
                    BcsOrderResponse response = JsonConvert.DeserializeAnonymousType(responseMsg, new BcsOrderResponse());

                    if (response.status == "OK")
                    {
                        AddOrderIds(order.NumberUser, clientOrderId);
                        return true;
                    }
                    else
                    {
                        SendLogMessage($"Order Id: {response.clientOrderId} cancel error. Status: {response.status}", LogMessageType.Error);
                    }
                }
                else if (cancelOrderResponse.StatusCode == HttpStatusCode.Forbidden)
                {
                    string responseMsg = cancelOrderResponse.Content.ReadAsStringAsync().Result;
                    SendLogMessage($"Cancel order forbidden. Status: {cancelOrderResponse.StatusCode} || msg:{responseMsg}", LogMessageType.Error);
                }
                else
                {
                    SendLogMessage($"Order cancel request error. Body: {jsonRequest}\n Status: {cancelOrderResponse.StatusCode} || msg:{cancelOrderResponse.ReasonPhrase}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Order cancel error: " + exception.ToString(), LogMessageType.Error);
            }
            return false;
        }

        public void CancelAllOrders()
        {
            List<Order> activeOrders = GetAllActiveOrdersFromExchange();

            if (activeOrders.Count > 0)
            {
                for (int i = 0; i < activeOrders.Count; i++)
                {
                    CancelOrder(activeOrders[i]);
                }
            }
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            List<Order> activeOrders = GetAllActiveOrdersFromExchange();

            if (activeOrders.Count > 0)
            {
                List<Order> ordersBySec = activeOrders.FindAll(o => o.SecurityNameCode == security.Name && o.SecurityClassCode == security.NameClass);

                if (ordersBySec.Count > 0)
                {
                    for (int i = 0; i < ordersBySec.Count; i++)
                    {
                        CancelOrder(ordersBySec[i]);
                    }
                }
            }
        }

        public OrderStateType GetOrderStatus(Order order)
        {
            _rateGateOrderStatus.WaitToProceed();

            try
            {
                string orderId = "";

                try
                {
                    orderId = GetClientOrderId(order.NumberUser).ToString();
                }
                catch (KeyNotFoundException ex)
                {
                    SendLogMessage("Order status request error: " + ex.Message, LogMessageType.Error);
                    return OrderStateType.None;
                }

                string endPoint = $"/trade-api-bff-operations/api/v1/orders?orderIdType=1&orderId={orderId}";

                HttpResponseMessage statusOrderResponse = CreateHttpRequestAsync(endPoint, HttpMethod.Get, null).Result;

                if (statusOrderResponse.StatusCode == HttpStatusCode.OK)
                {
                    string responseMsg = statusOrderResponse.Content.ReadAsStringAsync().Result;
                    BcsOrdersResponse statusResponse = JsonConvert.DeserializeAnonymousType(responseMsg, new BcsOrdersResponse());

                    OrderStateType stateType = GetOrderState(statusResponse.Data.OrderStatus);

                    if (stateType == OrderStateType.Fail)
                    {
                        SendLogMessage("Order fail! Reason: " + statusResponse.Data.RejectReason, LogMessageType.Error);
                    }

                    UpdateMyOrder(statusResponse);

                    return stateType;
                }
                else
                {
                    SendLogMessage($"Order status request error: {statusOrderResponse.StatusCode} || msg:{statusOrderResponse.ReasonPhrase}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Order status getting error: " + ex.ToString(), LogMessageType.Error);
            }

            return OrderStateType.None;
        }

        public void GetAllActivOrders()
        {
            List<Order> ordersResult = GetAllActiveOrdersFromExchange();

            if (ordersResult.Count > 0)
            {
                for (int i = 0; i < ordersResult.Count; i++)
                {
                    MyOrderEvent?.Invoke(ordersResult[i]);
                }
            }
        }

        public List<Order> GetActiveOrders(int startIndex, int count)
        {
            try
            {
                List<Order> ordersResult = GetAllActiveOrdersFromExchange();

                if (ordersResult.Count != 0 && startIndex < ordersResult.Count)
                {
                    if (startIndex + count < ordersResult.Count)
                    {
                        return ordersResult.GetRange(startIndex, count);
                    }
                    else
                    {
                        return ordersResult.GetRange(startIndex, ordersResult.Count - startIndex);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Active orders getting error: " + ex.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private List<Order> GetAllActiveOrdersFromExchange()
        {
            List<Order> ordersResult = [];
            try
            {
                List<Order> activeOrdersBuy = GetOrdersFromExchangeByStatus([3], 1);
                List<Order> activeOrdersSell = GetOrdersFromExchangeByStatus([3], 2);

                if (activeOrdersBuy != null && activeOrdersBuy.Count > 0)
                {
                    ordersResult.AddRange(activeOrdersBuy);
                }

                if (activeOrdersSell != null && activeOrdersSell.Count > 0)
                {
                    ordersResult.AddRange(activeOrdersSell);
                }

                if (ordersResult.Count > 0)
                {
                    ordersResult.Sort((a, b) => b.TimeCallBack.CompareTo(a.TimeCallBack));
                    return ordersResult;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Active orders getting error: " + ex.ToString(), LogMessageType.Error);
                return ordersResult;
            }

            return ordersResult;
        }

        public List<Order> GetHistoricalOrders(int startIndex, int count)
        {
            try
            {
                List<Order> ordersResult = [];

                List<Order> historyOrdersBuy = GetOrdersFromExchangeByStatus([1, 2], 1);
                List<Order> historyOrdersSell = GetOrdersFromExchangeByStatus([1, 2], 2);

                if (historyOrdersBuy != null && historyOrdersBuy.Count > 0)
                {
                    ordersResult.AddRange(historyOrdersBuy);
                }

                if (historyOrdersSell != null && historyOrdersSell.Count > 0)
                {
                    ordersResult.AddRange(historyOrdersSell);
                }

                if (ordersResult.Count != 0 && startIndex < ordersResult.Count)
                {
                    if (startIndex + count < ordersResult.Count)
                    {
                        ordersResult.Sort((a, b) => b.TimeCallBack.CompareTo(a.TimeCallBack));
                        return ordersResult.GetRange(startIndex, count);
                    }
                    else
                    {
                        ordersResult.Sort((a, b) => b.TimeCallBack.CompareTo(a.TimeCallBack));
                        return ordersResult.GetRange(startIndex, ordersResult.Count - startIndex);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("History orders getting error: " + ex.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private List<Order> GetOrdersFromExchangeByStatus(int[] orderStatuses, int side)
        {
            List<Order> orders = new List<Order>();
            int page = -1;
            int totalRecords = 0;

            try
            {
                do
                {
                    _rateGateGetOrders.WaitToProceed();

                    page++;

                    string endPoint = $"/trade-api-bff-order-details/api/v1/orders/search?page={page}&size=100&sort=orderDateTime,desc";

                    Dictionary<string, dynamic> jsonContent = new Dictionary<string, dynamic>
                {
                    { "startDateTime", DateTime.UtcNow.AddMonths(-3).ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                    { "endDateTime", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                    {"side",  side },
                    {"orderStatus", orderStatuses},
                    {"orderTypes", new int[]{1, 2, 3, 10} },
                    {"tickers",  Array.Empty<string>() },
                    {"classCode", Array.Empty<string>() }
                };

                    string jsonRequest = JsonConvert.SerializeObject(jsonContent);

                    HttpResponseMessage orderListResponse = CreateHttpRequestAsync(endPoint, HttpMethod.Post, jsonRequest).Result;

                    if (orderListResponse.StatusCode == HttpStatusCode.OK)
                    {
                        string responseMsg = orderListResponse.Content.ReadAsStringAsync().Result;
                        BcsOrdersListResponse ordersResponse = JsonConvert.DeserializeAnonymousType(responseMsg, new BcsOrdersListResponse());

                        if (ordersResponse != null && ordersResponse.records.Length > 0)
                        {
                            totalRecords = Convert.ToInt32(ordersResponse.totalRecords);

                            for (int i = 0; i < ordersResponse.records.Length; i++)
                            {
                                Order order = ConvertToOsEngineOrder(ordersResponse.records[i]);

                                if (order == null)
                                    continue;

                                orders.Add(order);
                            }
                        }
                        else
                        {
                            return null;
                        }
                    }
                    else
                    {
                        SendLogMessage($"Order list getting error: {orderListResponse.StatusCode} || msg:{orderListResponse.ReasonPhrase}", LogMessageType.Error);
                        return null;
                    }

                } while (totalRecords == 100);

                return orders;
            }
            catch (Exception ex)
            {
                SendLogMessage("Orders getting error: " + ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private Order ConvertToOsEngineOrder(Record record)
        {
            try
            {
                Order order = new Order();

                Security security = GetSecurityByName(record.ticker, record.classCode);

                if (security != null)
                {
                    order.SecurityNameCode = security.Name;
                    order.SecurityClassCode = security.NameClass;
                    order.Volume = record.orderQuantityLots.ToDecimal() / security.Lot;
                }
                else
                {
                    order.SecurityNameCode = record.ticker;
                    order.SecurityClassCode = record.classCode;
                    order.Volume = record.orderQuantityLots.ToDecimal();
                }

                if (_userNumberByOrderId.TryGetValue(record.orderId, out int userNumber))
                {
                    order.NumberUser = userNumber;
                }

                order.NumberMarket = record.orderNum;
                order.TimeCallBack = ConvertUtsStringToDateTimeRu(record.updateDateTime);
                order.Price = record.price.ToDecimal();
                order.Volume = record.orderQuantityLots.ToDecimal();
                order.Side = record.side == "1" ? Side.Buy : Side.Sell;

                if (_myPortfolios.Count == 1)
                {
                    order.PortfolioNumber = _myPortfolios[0].Number;
                }

                if (record.orderStatus == "1")
                {
                    order.State = OrderStateType.Cancel;
                    order.TimeCancel = ConvertUtsStringToDateTimeRu(record.updateDateTime);
                }
                else if (record.orderStatus == "2")
                {
                    order.State = OrderStateType.Done;
                    order.TimeDone = ConvertUtsStringToDateTimeRu(record.updateDateTime);
                }
                else
                {
                    order.State = OrderStateType.Active;
                    order.TimeCreate = ConvertUtsStringToDateTimeRu(record.updateDateTime);
                }

                if (record.orderType == " 1")
                    order.TypeOrder = OrderPriceType.Market;
                else if (record.orderType == "2")
                    order.TypeOrder = OrderPriceType.Limit;
                else order.TypeOrder = OrderPriceType.Iceberg;

                order.ServerType = ServerType.BCS;

                if (order.State == OrderStateType.Active && _changedOrderNumsMarket.Count > 0)
                {
                    for (int i = 0; i < _changedOrderNumsMarket.Count; i++)
                    {
                        List<string> nums = _changedOrderNumsMarket[i];

                        if (nums[^1] == record.orderNum)
                        {
                            order.NumberMarket = nums[0];
                            break;
                        }
                    }
                }

                return order;
            }
            catch (Exception ex)
            {
                SendLogMessage("Order convert error: " + ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        #endregion

        #region 11 Queries

        private async Task<HttpResponseMessage> CreateHttpRequestAsync(string path, HttpMethod method, string body)
        {
            try
            {
                int maxRetries = 3;

                for (int i = 0; i < maxRetries; i++)
                {
                    using HttpRequestMessage request = new HttpRequestMessage(method, path);

                    if (method == HttpMethod.Post && !string.IsNullOrEmpty(body))
                    {
                        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                    }

                    try
                    {
                        if (_httpClient != null)
                            return await _httpClient.SendAsync(request);
                        else
                        {
                            if (_handler == null)
                                ConfigureHandler();

                            CreateHttpClient();

                            await Task.Delay(1000);
                            i--;
                            continue;
                        }
                    }
                    catch (HttpRequestException ex) when (ex.InnerException is System.IO.IOException)
                    {
                        if (i == maxRetries - 1)
                            throw;

                        SendLogMessage($"Попытка {i + 1} запросить путь: <<{path}>> не удалась, повтор через {1000 * (i + 1)}мс", LogMessageType.System);
                        await Task.Delay(1000 * (i + 1));
                    }
                }

                throw new InvalidOperationException("Запрос не удался после всех попыток");
            }
            catch (Exception ex)
            {
                SendLogMessage($"Create http request error {ex.Message} {ex.StackTrace}", LogMessageType.Error);
                return null;
            }
        }

        #endregion

        #region 12 Helpers

        public DateTime ConvertUtsStringToDateTimeRu(string timeString)
        {
            DateTime startTimeUtc = DateTime.Parse(timeString, null, DateTimeStyles.RoundtripKind);
            return TimeZoneInfo.ConvertTimeFromUtc(startTimeUtc, _moscowTimeZone);
        }

        private Guid GetClientOrderId(int key)
        {
            lock (_orderNumbersLocker)
            {
                if (_guidByNumberOrders.TryGetValue(key, out Guid value))
                {
                    return value;
                }
            }

            throw new KeyNotFoundException($"Ключ {key} не найден в словаре guidByNumberOrders.");
        }

        private int GetOrderUserNumber(Guid key)
        {
            lock (_orderNumbersLocker)
            {
                if (_numberByGuidOrders.TryGetValue(key, out int value))
                {
                    return value;
                }
            }

            throw new KeyNotFoundException($"Ключ {key} не найден в словаре numberByGuidOrders.");
        }

        private void AddOrderIds(int userNumber, Guid clientOrderId)
        {
            lock (_orderNumbersLocker)
            {
                _guidByNumberOrders[userNumber] = clientOrderId;
                _numberByGuidOrders[clientOrderId] = userNumber;
                _orderQueue.Enqueue(userNumber);

                if (_guidByNumberOrders.Count >= 500)
                {
                    RemoveFirstElementsQueue(50);
                }
            }
        }

        private void AddOrderIdAndUserNum(string orderId, int userNumber)
        {
            lock (_orderNumbersLocker)
            {
                _userNumberByOrderId.TryAdd(orderId, userNumber);
            }
        }

        private void RemoveFirstElementsQueue(int count)
        {
            List<string> keys = [];

            for (int i = 0; i < count && _orderQueue.Count > 0; i++)
            {
                int key = _orderQueue.Dequeue();

                if (_guidByNumberOrders.TryGetValue(key, out Guid value))
                {
                    _numberByGuidOrders.Remove(value);
                    _guidByNumberOrders.Remove(key);

                    Dictionary<string, int>.Enumerator entor = _userNumberByOrderId.GetEnumerator();

                    while (entor.MoveNext())
                    {
                        if (entor.Current.Value == key)
                        {
                            keys.Add(entor.Current.Key);
                        }
                    }
                }
            }

            for (int j = 0; j < keys.Count; j++)
            {
                _userNumberByOrderId.Remove(keys[j]);
            }
        }

        private string _securitiesLocker = "securitiesLocker";

        private Security GetSecurityByName(string name, string className)
        {
            lock (_securitiesLocker)
            {
                return _subscribedSecurities.Find(s => s.Name == name && s.NameClass.Contains(className));
            }
        }

        public void SetLeverage(Security security, decimal leverage) { }

        #endregion

        #region 13 Log

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent?.Invoke(message, messageType);
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }
        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }
        public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }
    }
}
