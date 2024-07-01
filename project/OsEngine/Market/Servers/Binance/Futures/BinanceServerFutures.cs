/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Binance.Futures.Entity;
using OsEngine.Market.Servers.Entity;
using RestSharp;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebSocket4Net;
using TradeResponse = OsEngine.Market.Servers.Binance.Spot.BinanceSpotEntity.TradeResponse;
using System.Net;

namespace OsEngine.Market.Servers.Binance.Futures
{
    public enum FuturesType
    {
        USDT,
        COIN
    }

    public class BinanceServerFutures : AServer
    {
        public BinanceServerFutures()
        {
            BinanceServerFuturesRealization realization = new BinanceServerFuturesRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");
            CreateParameterEnum("Futures Type", "USDT-M", new List<string> { "USDT-M", "COIN-M" });
            CreateParameterBoolean("HedgeMode", false);
        }
    }

    public class BinanceServerFuturesRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public BinanceServerFuturesRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread worker1 = new Thread(PortfolioUpdater);
            worker1.Name = "BinanceFutThread_PortfolioUpdater";
            worker1.Start();

            Thread worker2 = new Thread(KeepaliveUserDataStream);
            worker2.IsBackground = true;
            worker2.Name = "BinanceFutThread_KeepaliveUserDataStream";
            worker2.Start();

            Thread worker3 = new Thread(ConverterPublicMessages);
            worker3.IsBackground = true;
            worker3.Name = "BinanceFutThread_ConverterPublicMessages";
            worker3.Start();

            Thread worker4 = new Thread(ConverterUserData);
            worker4.IsBackground = true;
            worker4.Name = "BinanceFutThread_ConverterUserData";
            worker4.Start();
        }

        public void Connect()
        {
            ApiKey = ((ServerParameterString)ServerParameters[0]).Value;
            SecretKey = ((ServerParameterPassword)ServerParameters[1]).Value;
            HedgeMode = ((ServerParameterBool)ServerParameters[3]).Value;

            if (string.IsNullOrEmpty(ApiKey) ||
                string.IsNullOrEmpty(SecretKey))
            {
                SendLogMessage("Can`t run Binance Futures connector. No keys", LogMessageType.Error);
                return;
            }

            // check server availability for HTTP communication with it / проверяем доступность сервера для HTTP общения с ним
            Uri uri = new Uri(_baseUrl + "/" + type_str_selector + "/v1/time");
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                var httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);
                HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            }
            catch
            {
                SendLogMessage("Can`t run Binance Futures connector. No internet connection", LogMessageType.Error);
                return;
            }

            if (((ServerParameterEnum)ServerParameters[2]).Value == "USDT-M")
            {
                _baseUrl = "https://fapi.binance.com";
                wss_point = "wss://fstream.binance.com";
                type_str_selector = "fapi";
            }
            else if (((ServerParameterEnum)ServerParameters[2]).Value == "COIN-M")
            {
                _baseUrl = "https://dapi.binance.com";
                wss_point = "wss://dstream.binance.com";
                type_str_selector = "dapi";
            }

            ActivateSockets();
        }

        public void Dispose()
        {
            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;

                if (DisconnectEvent != null)
                {
                    DisconnectEvent();
                }
            }

            DisposeSockets();

            _subscribledSecurities.Clear();
            _securities = new List<Security>();
            _depths.Clear();
            _queuePrivateMessages = new ConcurrentQueue<BinanceUserMessage>();
            _queuePublicMessages = new ConcurrentQueue<string>();
        }

        public ServerType ServerType
        {
            get { return ServerType.BinanceFutures; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public DateTime ServerTime { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        public string _baseUrl = "https://fapi.binance.com";

        public string wss_point = "wss://fstream.binance.com";

        public string type_str_selector = "fapi";

        public string ApiKey;

        public string SecretKey;

        public bool HedgeMode
        {
            get { return _hedgeMode; }
            set
            {
                if (value == _hedgeMode)
                {
                    return;
                }
                _hedgeMode = value;
                SetPositionMode();
            }
        }

        private bool _hedgeMode;

        public void SetPositionMode()
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }
                var rs = CreateQuery(Method.GET, "/" + type_str_selector + "/v1/positionSide/dual", new Dictionary<string, string>(), true);
                if (rs != null)
                {
                    var modeNow = JsonConvert.DeserializeAnonymousType(rs, new HedgeModeResponse());
                    if (modeNow.dualSidePosition != HedgeMode)
                    {
                        var param = new Dictionary<string, string>();
                        param.Add("dualSidePosition=", HedgeMode.ToString().ToLower());
                        CreateQuery(Method.POST, "/" + type_str_selector + "/v1/positionSide/dual", param, true);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);

            }

        }

        #endregion

        #region 3 Securities

        public void GetSecurities()
        {

            try
            {
                //Get All Margin Pairs (MARKET_DATA)
                //GET /sapi/v1/margin/allPairs

                string res = CreateQuery(Method.GET, "/" + type_str_selector + "/v1/exchangeInfo", null, false);
                SecurityResponce secResp = JsonConvert.DeserializeAnonymousType(res, new SecurityResponce());
                UpdatePairs(secResp);
            }
            catch (Exception ex)
            {
                if (LogMessageEvent != null)
                {
                    LogMessageEvent(ex.ToString(), LogMessageType.Error);
                }
            }
        }

        private List<Security> _securities = new List<Security>();

        private void UpdatePairs(SecurityResponce pairs)
        {
            foreach (var sec in pairs.symbols)
            {
                Security security = new Security();
                security.Name = sec.symbol;
                security.NameFull = sec.symbol;
                security.NameClass = sec.quoteAsset;
                security.NameId = sec.symbol + sec.quoteAsset;
                security.SecurityType = SecurityType.Futures;
                security.Exchange = ServerType.BinanceFutures.ToString();
                security.Lot = 1;
                security.PriceStep = sec.filters[0].tickSize.ToDecimal();
                security.PriceStepCost = security.PriceStep;

                security.PriceLimitLow = sec.filters[0].minPrice.ToDecimal();
                security.PriceLimitHigh = sec.filters[0].maxPrice.ToDecimal();

                if (security.PriceStep < 1)
                {
                    string prStep = security.PriceStep.ToString(CultureInfo.InvariantCulture);
                    security.Decimals = Convert.ToString(prStep).Split('.')[1].Split('1')[0].Length + 1;
                }
                else
                {
                    security.Decimals = 0;
                }

                if (sec.filters.Count > 1 &&
                    sec.filters[1] != null &&
                    sec.filters[1].minQty != null)
                {
                    decimal minQty = sec.filters[1].minQty.ToDecimal();
                    security.MinTradeAmount = minQty;
                    string qtyInStr = minQty.ToStringWithNoEndZero().Replace(",", ".");
                    if (qtyInStr.Replace(",", ".").Split('.').Length > 1)
                    {
                        security.DecimalsVolume = qtyInStr.Replace(",", ".").Split('.')[1].Length;
                    }
                }

                security.State = SecurityStateType.Activ;
                _securities.Add(security);
            }

            List<Security> secNonPerp = new List<Security>();

            for (int i = 0; i < _securities.Count; i++)
            {
                string[] str = _securities[i].Name.Split('_');

                if (str.Length > 1 &&
                    str[1] != "PERP")
                {
                    secNonPerp.Add(_securities[i]);
                }

            }

            List<Security> securitiesHistorical = CreateHistoricalSecurities(secNonPerp);

            _securities.AddRange(securitiesHistorical);

            if (SecurityEvent != null)
            {
                SecurityEvent(_securities);
            }
        }

        private List<Security> CreateHistoricalSecurities(List<Security> securities)
        {
            List<Security> secHistorical = new List<Security>();

            for (int i = 0; i < securities.Count; i++)
            {
                if (secHistorical.Find(s => s.Name.Split('_')[0] == securities[i].Name.Split('_')[0]) != null)
                {
                    continue;
                }

                secHistorical.AddRange(GetHistoricalSecBySec(securities[i]));
            }

            return secHistorical;
        }

        private List<Security> GetHistoricalSecBySec(Security sec)
        {
            List<Security> secHistorical = new List<Security>();

            string name = sec.Name.Split('_')[0];

            secHistorical.Add(GetHistoryOneSecurity(name + "_201225", sec));
            secHistorical.Add(GetHistoryOneSecurity(name + "_210326", sec));
            secHistorical.Add(GetHistoryOneSecurity(name + "_210625", sec));
            secHistorical.Add(GetHistoryOneSecurity(name + "_210924", sec));
            secHistorical.Add(GetHistoryOneSecurity(name + "_211231", sec));
            secHistorical.Add(GetHistoryOneSecurity(name + "_220325", sec));
            secHistorical.Add(GetHistoryOneSecurity(name + "_220624", sec));

            return secHistorical;
        }

        private Security GetHistoryOneSecurity(string secName, Security sec)
        {
            Security security = new Security();
            security.Name = secName;
            security.NameFull = secName;
            security.NameClass = "FutHistory";
            security.Exchange = ServerType.BinanceFutures.ToString();
            security.NameId = secName;
            security.SecurityType = SecurityType.Futures;
            security.Lot = sec.Lot;
            security.PriceStep = sec.PriceStep;
            security.PriceStepCost = sec.PriceStepCost;

            security.PriceLimitLow = sec.PriceLimitLow;
            security.PriceLimitHigh = sec.PriceLimitHigh;

            security.Decimals = sec.Decimals;
            security.DecimalsVolume = sec.DecimalsVolume;

            security.State = SecurityStateType.Activ;

            return security;
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        public void GetPortfolios()
        {
            AccountResponseFutures responce = GetAccountInfo();

            if(responce != null)
            {
                UpdatePortfolio(responce);
            }
        }

        private void PortfolioUpdater()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(30000);

                    if (this.ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        continue;
                    }

                    AccountResponseFutures resp = GetAccountInfo();

                    if (resp != null)
                    {
                        UpdatePortfolio(resp);
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                }
            }
        }

        private object _getAccountLocker = "getAccountLocker";

        public AccountResponseFutures GetAccountInfo()
        {
            lock (_getAccountLocker)
            {
                try
                {
                    string res = null;

                    if (type_str_selector == "dapi")
                    {
                        res = CreateQuery(Method.GET, "/" + type_str_selector + "/v1/account", null, true);
                    }
                    else if (type_str_selector == "fapi")
                    {
                        res = CreateQuery(Method.GET, "/" + type_str_selector + "/v2/account", null, true);
                    }

                    if (res == null)
                    {
                        return null;
                    }

                    AccountResponseFutures resp = JsonConvert.DeserializeAnonymousType(res, new AccountResponseFutures());
                    return resp;
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                    return null;
                }
            }
        }

        private List<Portfolio> _portfolios = new List<Portfolio>();

        private void UpdatePortfolio(AccountResponseFutures portfs)
        {
            try
            {
                Portfolio myPortfolio = _portfolios.Find(p => p.Number == "BinanceFutures");

                if (myPortfolio == null)
                {
                    Portfolio newPortf = new Portfolio();
                    newPortf.Number = "BinanceFutures";
                    newPortf.ValueBegin = 1;
                    newPortf.ValueCurrent = 1;
                    _portfolios.Add(newPortf);
                    myPortfolio = newPortf;
                }

                if (portfs.assets == null)
                {
                    return;
                }

                foreach (var onePortf in portfs.assets)
                {
                    PositionOnBoard newPortf = new PositionOnBoard();
                    newPortf.SecurityNameCode = onePortf.asset;
                    newPortf.ValueBegin =
                        onePortf.marginBalance.ToDecimal();
                    newPortf.ValueCurrent =
                        onePortf.marginBalance.ToDecimal();


                    decimal lockedBalanceUSDT = 0m;
                    if (onePortf.asset.Equals("USDT"))
                    {

                        foreach (var position in portfs.positions)
                        {
                            if (position.symbol == "USDTUSDT") continue;

                            lockedBalanceUSDT += (position.initialMargin.ToDecimal() + position.maintMargin.ToDecimal());
                        }
                    }

                    newPortf.ValueBlocked = lockedBalanceUSDT;

                    myPortfolio.SetNewPosition(newPortf);
                }

                foreach (var onePortf in portfs.positions)
                {
                    if (string.IsNullOrEmpty(onePortf.positionAmt))
                    {
                        continue;
                    }

                    PositionOnBoard newPortf = new PositionOnBoard();

                    string name = onePortf.symbol + "_" + onePortf.positionSide;

                    newPortf.SecurityNameCode = name;
                    newPortf.ValueBegin =
                        onePortf.positionAmt.ToDecimal();

                    newPortf.ValueCurrent =
                        onePortf.positionAmt.ToDecimal();

                    myPortfolio.SetNewPosition(newPortf);
                }

                if (PortfolioEvent != null)
                {
                    PortfolioEvent(_portfolios);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

        public PremiumIndex GetPremiumIndex(string symbol)
        {
            try
            {
                var res = CreateQuery(
                    Method.GET,
                    "/" + type_str_selector + "/v1/premiumIndex",
                    new Dictionary<string, string>() { { "symbol=", symbol } },
                    true);

                PremiumIndex resp = JsonConvert.DeserializeAnonymousType(res, new PremiumIndex());
                return resp;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            List<Candle> candles = GetCandles(security.Name, timeFrameBuilder.TimeFrameTimeSpan);

            for (int i  = 1; candles != null && i < candles.Count;i++)
            {
                if (candles[i-1].TimeStart == candles[i].TimeStart)
                {
                    candles.RemoveAt(i);
                    i--;
                }
            }

            if (candles != null && candles.Count != 0)
            {
                for (int i = 0; i < candles.Count; i++)
                {
                    candles[i].State = CandleState.Finished;
                }
                candles[candles.Count - 1].State = CandleState.Started;
            }

            return candles;
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, 
            DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if(timeFrameBuilder.TimeFrame == TimeFrame.Hour2
                || timeFrameBuilder.TimeFrame == TimeFrame.Hour4)
            {
                return null;
            }

            if(actualTime > endTime)
            {
                return null;
            }

            if (endTime > DateTime.Now - new TimeSpan(0, 0, 1, 0))
                endTime = DateTime.Now - new TimeSpan(0, 0, 1, 0);

            int interval = 500 * (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;

            List<Candle> candles = new List<Candle>();

            var startTimeStep = startTime;
            var endTimeStep = startTime;

            while (endTime > endTimeStep)
            {
                endTimeStep = endTimeStep + new TimeSpan(0, 0, interval, 0);

                DateTime realEndTime = endTimeStep;

                if (realEndTime > DateTime.Now - new TimeSpan(0, 0, (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes, 0))
                    realEndTime = DateTime.Now - new TimeSpan(0, 0, (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes, 0);

                List<Candle> stepCandles = GetCandlesForTimes(security.Name, timeFrameBuilder.TimeFrameTimeSpan, startTimeStep, realEndTime);

                if (stepCandles != null)
                {
                    if(stepCandles.Count == 1 &&
                        candles.Count > 1 &&
                        stepCandles[0].TimeStart == candles[candles.Count-1].TimeStart)
                    {
                        break;
                    }

                    candles.AddRange(stepCandles);
                    endTimeStep = stepCandles[stepCandles.Count - 1].TimeStart;
                }
                   
                startTimeStep = endTimeStep;

                if (endTime < endTimeStep)
                {
                    break;
                }

                if(startTimeStep > endTime)
                {
                    break;
                }

                Thread.Sleep(300);
            }

            if (candles.Count == 0)
            {
                return null;
            }

            for(int i = 0;i < candles.Count;i++)
            {
                if (candles[i].TimeStart > endTime)
                {
                    candles.RemoveAt(i);
                    i--;
                }
            }

            for (int i = 1; i < candles.Count; i++)
            {
                if (candles[i - 1].TimeStart == candles[i].TimeStart)
                {
                    candles.RemoveAt(i);
                    i--;
                }
            }

            return candles;
        }

        private List<Candle> GetCandlesForTimes(string nameSec, TimeSpan tf, DateTime timeStart, DateTime timeEnd)
        {
            DateTime yearBegin = new DateTime(1970, 1, 1);

            var timeStampStart = timeStart - yearBegin;
            var r = timeStampStart.TotalMilliseconds;
            string startTime = Convert.ToInt64(r).ToString();

            var timeStampEnd = timeEnd - yearBegin;
            var rEnd = timeStampEnd.TotalMilliseconds;
            string endTime = Convert.ToInt64(rEnd).ToString();


            string needTf = "";

            switch ((int)tf.TotalMinutes)
            {
                case 1:
                    needTf = "1m";
                    break;
                case 2:
                    needTf = "2m";
                    break;
                case 3:
                    needTf = "3m";
                    break;
                case 5:
                    needTf = "5m";
                    break;
                case 10:
                    needTf = "10m";
                    break;
                case 15:
                    needTf = "15m";
                    break;
                case 20:
                    needTf = "20m";
                    break;
                case 30:
                    needTf = "30m";
                    break;
                case 45:
                    needTf = "45m";
                    break;
                case 60:
                    needTf = "1h";
                    break;
                case 120:
                    needTf = "2h";
                    break;
            }

            string endPoint = "" + type_str_selector + "/v1/klines";

            if (needTf != "2m" && needTf != "10m" && needTf != "20m" && needTf != "45m")
            {
                var param = new Dictionary<string, string>();
                param.Add("symbol=" + nameSec.ToUpper(), "&interval=" + needTf + "&startTime=" + startTime + "&endTime=" + endTime);

                var res = CreateQuery(Method.GET, endPoint, param, false);

                var candles = _deserializeCandles(res);
                return candles;

            }
            else
            {
                if (needTf == "2m")
                {
                    var param = new Dictionary<string, string>();
                    param.Add("symbol=" + nameSec.ToUpper(), "&interval=1m" + "&startTime=" + startTime + "&endTime=" + endTime);
                    var res = CreateQuery(Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);

                    var newCandles = BuildCandles(candles, 2, 1);
                    return newCandles;
                }
                else if (needTf == "10m")
                {
                    var param = new Dictionary<string, string>();
                    param.Add("symbol=" + nameSec.ToUpper(), "&interval=5m" + "&startTime=" + startTime + "&endTime=" + endTime);
                    var res = CreateQuery(Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);
                    var newCandles = BuildCandles(candles, 10, 5);
                    return newCandles;
                }
                else if (needTf == "20m")
                {
                    var param = new Dictionary<string, string>();
                    param.Add("symbol=" + nameSec.ToUpper(), "&interval=5m" + "&startTime=" + startTime + "&endTime=" + endTime);
                    var res = CreateQuery(Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);
                    var newCandles = BuildCandles(candles, 20, 5);
                    return newCandles;
                }
                else if (needTf == "45m")
                {
                    var param = new Dictionary<string, string>();
                    param.Add("symbol=" + nameSec.ToUpper(), "&interval=15m" + "&startTime=" + startTime + "&endTime=" + endTime);
                    var res = CreateQuery(Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);
                    var newCandles = BuildCandles(candles, 45, 15);
                    return newCandles;
                }
            }

            return null;
        }

        private string _candleLocker = "candleLocker";

        private List<Candle> GetCandles(string nameSec, TimeSpan tf)
        {
            string needTf = "";

            switch ((int)tf.TotalMinutes)
            {
                case 1:
                    needTf = "1m";
                    break;
                case 2:
                    needTf = "2m";
                    break;
                case 3:
                    needTf = "3m";
                    break;
                case 5:
                    needTf = "5m";
                    break;
                case 10:
                    needTf = "10m";
                    break;
                case 15:
                    needTf = "15m";
                    break;
                case 20:
                    needTf = "20m";
                    break;
                case 30:
                    needTf = "30m";
                    break;
                case 45:
                    needTf = "45m";
                    break;
                case 60:
                    needTf = "1h";
                    break;
                case 120:
                    needTf = "2h";
                    break;
                case 240:
                    needTf = "4h";
                    break;
                case 1440:
                    needTf = "1d";
                    break;
            }

            string endPoint = "/" + type_str_selector + "/v1/klines";

            if (needTf != "2m" && needTf != "10m" && needTf != "20m" && needTf != "45m")
            {
                var param = new Dictionary<string, string>();
                param.Add("symbol=" + nameSec.ToUpper(), "&interval=" + needTf);

                var res = CreateQuery(Method.GET, endPoint, param, false);

                var candles = _deserializeCandles(res);
                return candles;

            }
            else
            {
                if (needTf == "2m")
                {
                    var param = new Dictionary<string, string>();
                    param.Add("symbol=" + nameSec.ToUpper(), "&interval=1m");
                    var res = CreateQuery(Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);

                    var newCandles = BuildCandles(candles, 2, 1);
                    return newCandles;
                }
                else if (needTf == "10m")
                {
                    var param = new Dictionary<string, string>();
                    param.Add("symbol=" + nameSec.ToUpper(), "&interval=5m");
                    var res = CreateQuery(Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);
                    var newCandles = BuildCandles(candles, 10, 5);
                    return newCandles;
                }
                else if (needTf == "20m")
                {
                    var param = new Dictionary<string, string>();
                    param.Add("symbol=" + nameSec.ToUpper(), "&interval=5m");
                    var res = CreateQuery(Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);
                    var newCandles = BuildCandles(candles, 20, 5);
                    return newCandles;
                }
                else if (needTf == "45m")
                {
                    var param = new Dictionary<string, string>();
                    param.Add("symbol=" + nameSec.ToUpper(), "&interval=15m");
                    var res = CreateQuery(Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);
                    var newCandles = BuildCandles(candles, 45, 15);
                    return newCandles;
                }
            }

            return null;
        }

        private List<Candle> _deserializeCandles(string jsonCandles)
        {
            try
            {
                lock (_candleLocker)
                {
                    if (jsonCandles == null ||
                        jsonCandles == "[]")
                        return null;

                    string res = jsonCandles.Trim(new char[] { '[', ']' });

                    if (string.IsNullOrEmpty(res) == true)
                    {
                        return null;
                    }

                    var res2 = res.Split(new char[] { ']' });

                    List<Candle> _candles = new List<Candle>();

                    Candle newCandle;

                    for (int i = 0; i < res2.Length; i++)
                    {
                        if (i != 0)
                        {
                            string upd = res2[i].Substring(2);
                            upd = upd.Replace("\"", "");
                            string[] param = upd.Split(',');

                            newCandle = new Candle();
                            newCandle.TimeStart = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(param[0]));
                            newCandle.Low = param[3].ToDecimal();
                            newCandle.High = param[2].ToDecimal();
                            newCandle.Open = param[1].ToDecimal();
                            newCandle.Close = param[4].ToDecimal();
                            newCandle.Volume = param[5].ToDecimal();
                            _candles.Add(newCandle);
                        }
                        else
                        {
                            string[] param = res2[i].Replace("\"", "").Split(',');

                            newCandle = new Candle();
                            newCandle.TimeStart = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(param[0]));
                            newCandle.Low = param[3].ToDecimal();
                            newCandle.High = param[2].ToDecimal();
                            newCandle.Open = param[1].ToDecimal();
                            newCandle.Close = param[4].ToDecimal();
                            newCandle.Volume = param[5].ToDecimal();

                            _candles.Add(newCandle);
                        }
                    }

                    return _candles;
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private List<Candle> BuildCandles(List<Candle> oldCandles, int needTf, int oldTf)
        {
            if (oldCandles == null)
            {
                return null;
            }

            List<Candle> newCandles = new List<Candle>();

            int index = oldCandles.FindIndex(can => can.TimeStart.Minute % needTf == 0);

            int count = needTf / oldTf;

            int counter = 0;

            Candle newCandle = new Candle();

            for (int i = index; i < oldCandles.Count; i++)
            {
                counter++;

                if (counter == 1)
                {
                    newCandle = new Candle();
                    newCandle.Open = oldCandles[i].Open;
                    newCandle.TimeStart = oldCandles[i].TimeStart;
                    newCandle.Low = Decimal.MaxValue;
                }

                newCandle.High = oldCandles[i].High > newCandle.High
                    ? oldCandles[i].High
                    : newCandle.High;

                newCandle.Low = oldCandles[i].Low < newCandle.Low
                    ? oldCandles[i].Low
                    : newCandle.Low;

                newCandle.Volume += oldCandles[i].Volume;

                if (counter == count)
                {
                    newCandle.Close = oldCandles[i].Close;
                    newCandle.State = CandleState.Finished;
                    newCandles.Add(newCandle);
                    counter = 0;
                }

                if (i == oldCandles.Count - 1 && counter != count)
                {
                    newCandle.Close = oldCandles[i].Close;
                    newCandle.State = CandleState.None;
                    newCandles.Add(newCandle);
                }
            }

            for (int i = 1; newCandles != null && i < newCandles.Count; i++)
            {
                if (newCandles[i - 1].TimeStart == newCandles[i].TimeStart)
                {
                    newCandles.RemoveAt(i);
                    i--;
                }
            }

            return newCandles;
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime lastDate)
        {
            if(lastDate > startTime ||
                startTime > endTime)
            {
                return null;
            }

            //endTime = endTime.AddDays(1);
            string markerDateTime = "";
            List<Trade> trades = new List<Trade>();
            DateTime startOver = startTime;

            if (endTime > DateTime.Now - new TimeSpan(0, 0, 1, 0))
            {
                endTime = DateTime.Now - new TimeSpan(0, 0, 1, 0);
            }

            while (true)
            {
                if (startOver >= endTime)
                {
                    break;
                }

                List<Trade> newTrades = GetTickHistoryToSecurity(security.Name, startOver);

                if (newTrades != null && newTrades.Count != 0)
                {
                    trades.AddRange(newTrades);
                }
                else
                {
                    startOver.AddDays(1);
                    break;
                }

                startOver = trades[trades.Count - 1].Time.AddMilliseconds(1);

                if (markerDateTime != startOver.ToShortDateString())
                {
                    if (startOver >= endTime)
                    {
                        break;
                    }
                    markerDateTime = startOver.ToShortDateString();
                    SendLogMessage(security.Name + " Binance Futures trades start loading: " + markerDateTime, LogMessageType.System);
                }
            }

            if (trades.Count == 0)
            {
                return null;
            }

            for(int i = 0;i < trades.Count;i++)
            {
                if (trades[i].Time >= endTime)
                {
                    trades.RemoveAt(i);
                    i--;
                }
            }

            for (int i = 1; i < trades.Count; i++)
            {
                if (trades[i-1].Time == trades[i].Time)
                {
                    trades.RemoveAt(i);
                    i--;
                }
            }

            if(trades.Count == 0)
            {
                return null;
            }

            return trades;
        }

        private string _lockerTrades = "lockerTrades";

        private List<Trade> GetTickHistoryToSecurity(string security, DateTime startTime)
        {
            lock (_lockerTrades)
            {
                try
                {
                    Thread.Sleep(1000); // не убирать RateGate не помогает в CreateQuery

                    long from = TimeManager.GetTimeStampMilliSecondsToDateTime(startTime);

                    string timeStamp = TimeManager.GetUnixTimeStampMilliseconds().ToString();
                    Dictionary<string, string> param = new Dictionary<string, string>();

                    param.Add("symbol=" + security, "&limit=1000" + "&startTime=" + from);

                    string endPoint = "" + type_str_selector + "/v1/aggTrades";

                    var res2 = CreateQuery(Method.GET, endPoint, param, false);

                    AgregatedHistoryTrade[] tradeHistory = JsonConvert.DeserializeObject<AgregatedHistoryTrade[]>(res2);

                    var oldTrades = CreateTradesFromJson(security, tradeHistory);

                    return oldTrades;
                }
                catch
                {
                    SendLogMessage(OsLocalization.Market.Message95 + security, LogMessageType.Error);

                    return null;
                }
            }
        }

        private List<Trade> CreateTradesFromJson(string secName, AgregatedHistoryTrade[] binTrades)
        {
            List<Trade> trades = new List<Trade>();

            foreach (var jtTrade in binTrades)
            {
                var trade = new Trade();

                trade.Time = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(jtTrade.T));
                trade.Price = jtTrade.P.ToDecimal();
                trade.MicroSeconds = 0;
                trade.Id = jtTrade.A.ToString();
                trade.Volume = Math.Abs(jtTrade.Q.ToDecimal());
                trade.SecurityNameCode = secName;

                if (!jtTrade.m)
                {
                    trade.Side = Side.Buy;
                    trade.Ask = 0;
                    trade.AsksVolume = 0;
                    trade.Bid = trade.Price;
                    trade.BidsVolume = trade.Volume;
                }
                else
                {
                    trade.Side = Side.Sell;
                    trade.Ask = trade.Price;
                    trade.AsksVolume = trade.Volume;
                    trade.Bid = 0;
                    trade.BidsVolume = 0;
                }

                trades.Add(trade);
            }

            return trades;
        }

        #endregion

        #region 6 WebSocket creation

        private string _listenKey = "";

        private WebSocket _socketPrivateData;

        private Dictionary<string, WebSocket> _socketsArray = new Dictionary<string, WebSocket>();

        public void RenewListenKey()
        {
            try
            {
                _listenKey = CreateListenKey();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.Message, LogMessageType.Connect);
            }
        }

        private string CreateListenKey()
        {
            string createListenKeyUrl = String.Format("/{0}/v1/listenKey", type_str_selector);
            var createListenKeyResult = CreateQueryNoLock(Method.POST, createListenKeyUrl, null, false);
            return JsonConvert.DeserializeAnonymousType(createListenKeyResult, new ListenKey()).listenKey;
        }

        private void ActivateSockets()
        {
            try
            {
                _listenKey = CreateListenKey();
                string urlStr = wss_point + "/ws/" + _listenKey;

                _socketPrivateData = new WebSocket(urlStr);
                _socketPrivateData.Opened += _socketClient_Opened;
                _socketPrivateData.Closed += _socketClient_Closed;
                _socketPrivateData.Error += _socketClient_Error;
                _socketPrivateData.MessageReceived += _socketClient_PrivateMessage;
                _socketPrivateData.Open();

                _socketsArray.Add("userDataStream", _socketPrivateData);

            }
            catch (Exception exception)
            {
                SendLogMessage(exception.Message, LogMessageType.Connect);
            }
        }

        private void DisposeSockets()
        {
            try
            {
                if (_socketPrivateData != null)
                {
                    _socketPrivateData.Opened -= _socketClient_Opened;
                    _socketPrivateData.Closed -= _socketClient_Closed;
                    _socketPrivateData.Error -= _socketClient_Error;
                    _socketPrivateData.MessageReceived -= _socketClient_PrivateMessage;
                }
            }
            catch
            {
                // ignore
            }

            _socketPrivateData = null;

            try
            {
                if(_socketsArray != null)
                {
                    foreach (var ws in _socketsArray)
                    {
                        ws.Value.Closed -= new EventHandler(_socketClient_Closed);
                        ws.Value.Error -= new EventHandler<SuperSocket.ClientEngine.ErrorEventArgs>(_socketClient_Error);
                        ws.Value.MessageReceived -= new EventHandler<MessageReceivedEventArgs>(_socket_PublicMessage);
                    }
                }
            }
            catch
            {
                // ignore
            }

            try
            {
                if (_socketsArray != null)
                {
                    foreach (var ws in _socketsArray)
                    {
                        try
                        {
                            ws.Value.Close();
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
                // ignore
            }

            try
            {
                if (_socketsArray != null)
                {
                    foreach (var ws in _socketsArray)
                    {
                        try
                        {
                            ws.Value.Dispose();
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
                // ignore
            }

            try
            {
                if (_socketsArray != null)
                {
                    _socketsArray.Clear();
                }
            }
            catch
            {
                // ignore
            }
        }

        #endregion

        #region 7 WebSocket events

        private ConcurrentQueue<BinanceUserMessage> _queuePrivateMessages = new ConcurrentQueue<BinanceUserMessage>();

        private ConcurrentQueue<string> _queuePublicMessages = new ConcurrentQueue<string>();

        private void _socketClient_Opened(object sender, EventArgs e)
        {
            if (ServerStatus != ServerConnectStatus.Connect)
            {
                SendLogMessage("Websockets activate. Connection status", LogMessageType.System);

                ServerStatus = ServerConnectStatus.Connect;

                if (ConnectEvent != null)
                {
                    ConnectEvent();
                }
            }
        }

        private void _socketClient_Closed(object sender, EventArgs e)
        {
            if (ServerStatus == ServerConnectStatus.Connect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;

                SendLogMessage("Websocket lost connection: " + e.ToString(), LogMessageType.Error);

                if (DisconnectEvent != null)
                {
                    DisconnectEvent();
                }
            }
        }

        private void _socketClient_Error(object sender, SuperSocket.ClientEngine.ErrorEventArgs e)
        {
            SendLogMessage("Error websocket :" + e.ToString(), LogMessageType.Error);
        }

        private void _socketClient_PrivateMessage(object sender, MessageReceivedEventArgs e)
        {
            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                return;
            }
            BinanceUserMessage message = new BinanceUserMessage();
            message.MessageStr = e.Message;
            _queuePrivateMessages.Enqueue(message);
        }

        private void _socket_PublicMessage(object sender, MessageReceivedEventArgs e)
        {
            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                return;
            }

            _queuePublicMessages.Enqueue(e.Message);
        }

        #endregion

        #region 8 WebSocket check alive

        private void KeepaliveUserDataStream()
        {
            _timeStart = DateTime.Now;

            while (true)
            {
                try
                {
                    Thread.Sleep(10000);

                    if (_listenKey == "")
                    {
                        continue;
                    }

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        _timeStart = DateTime.Now;
                        continue;
                    }

                    if (_timeStart.AddMinutes(25) < DateTime.Now)
                    {
                        _timeStart = DateTime.Now;

                        CreateQueryNoLock(Method.PUT,
                            "/" + type_str_selector + "/v1/listenKey", new Dictionary<string, string>()
                                { { "listenKey=", _listenKey } }, false);

                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private DateTime _timeStart;

        #endregion

        #region 9 Security subscrible

        private List<Security> _subscribledSecurities = new List<Security>();

        public void Subscrible(Security security)
        {
            if(ServerStatus == ServerConnectStatus.Disconnect)
            {
                return;
            }

            for (int i = 0; i < _subscribledSecurities.Count; i++)
            {
                if (_subscribledSecurities[i].NameClass == security.NameClass
                    && _subscribledSecurities[i].Name == security.Name)
                {
                    return;
                }
            }

            _subscribledSecurities.Add(security);

            string urlStrDepth = wss_point + "/stream?streams="
                             + security.Name.ToLower() + "@depth20";

            WebSocket wsClientDepth = new WebSocket(urlStrDepth);

            wsClientDepth.Closed += new EventHandler(_socketClient_Closed);
            wsClientDepth.Error += new EventHandler<SuperSocket.ClientEngine.ErrorEventArgs>(_socketClient_Error);
            wsClientDepth.MessageReceived += new EventHandler<MessageReceivedEventArgs>(_socket_PublicMessage);
            wsClientDepth.Open();

            _socketsArray.Add(security.Name + "_depth20", wsClientDepth);

            string urlStrTrades = wss_point + "/stream?streams="
                 + security.Name.ToLower() + "@trade";

            WebSocket wsClientTrades = new WebSocket(urlStrTrades);

            wsClientTrades.Closed += new EventHandler(_socketClient_Closed);
            wsClientTrades.Error += new EventHandler<SuperSocket.ClientEngine.ErrorEventArgs>(_socketClient_Error);
            wsClientTrades.MessageReceived += new EventHandler<MessageReceivedEventArgs>(_socket_PublicMessage);
            wsClientTrades.Open();

            _socketsArray.Add(security.Name + "_trades", wsClientTrades);
        }

        #endregion

        #region 10 WebSocket parsing the messages

        public void ConverterPublicMessages()
        {
            while (true)
            {
                try
                {
                    if(_queuePublicMessages.IsEmpty)
                    {
                        Thread.Sleep(1);
                    }
                    else
                    {
                        string mes;

                        if (_queuePublicMessages.TryDequeue(out mes))
                        {
                            if (mes.Contains("\"depthUpdate\""))
                            {
                                var quotes = JsonConvert.DeserializeAnonymousType(mes, new DepthResponseFutures());
                                UpdateMarketDepth(quotes);
                            }
                            else if (mes.Contains("\"e\":\"trade\""))
                            {
                                var quotes = JsonConvert.DeserializeAnonymousType(mes, new TradeResponse());

                                if (quotes.data.X.ToString() != "MARKET")
                                {//INSURANCE_FUND
                                    continue;
                                }

                                UpdateTrades(quotes);
                            }
                            else if (mes.Contains("error"))
                            {
                                SendLogMessage(mes, LogMessageType.Error);
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

        public void ConverterUserData()
        {
            while (true)
            {
                try
                {
                    if(_queuePrivateMessages.IsEmpty == true)
                    {
                        Thread.Sleep(1);
                    }
                    else
                    {
                        BinanceUserMessage messsage;

                        if (_queuePrivateMessages.TryDequeue(out messsage))
                        {
                            string mes = messsage.MessageStr;

                            if (mes.Contains("code"))
                            {
                                // если есть code ошибки, то пытаемся распарсить
                                ErrorMessage _err = new ErrorMessage();

                                try
                                {
                                    _err = JsonConvert.DeserializeAnonymousType(mes, new ErrorMessage());
                                }
                                catch (Exception e)
                                {
                                    // если не смогли распарсить, то просто покажем что пришло
                                    _err.code = 9999;
                                    _err.msg = mes;
                                }
                                SendLogMessage("ConverterUserData ERORR. Code: " + _err.code.ToString() + ", msg: " + _err.msg, LogMessageType.Error);
                            }

                            else if (mes.Contains("\"e\"" + ":" + "\"ORDER_TRADE_UPDATE\""))
                            {
                                UpdateMyOrderAndMyOrders(mes);
                            }
                            else if (mes.Contains("\"e\"" + ":" + "\"ACCOUNT_UPDATE\""))
                            {
                                var portfolios = JsonConvert.DeserializeAnonymousType(mes, new AccountResponseFuturesFromWebSocket());
                                UpdatePortfolio(portfolios);
                            }
                            else if (IsListenKeyExpiredEvent(mes))
                            {
                                RenewListenKey();
                            }
                            else
                            {

                            }

                            //ORDER_TRADE_UPDATE
                            // "{\"e\":\"ORDER_TRADE_UPDATE\",\"T\":1579688850841,\"E\":1579688850846,\"o\":{\"s\":\"BTCUSDT\",\"c\":\"1998\",\"S\":\"BUY\",\"o\":\"LIMIT\",\"f\":\"GTC\",\"q\":\"0.001\",\"p\":\"8671.86\",\"ap\":\"0.00000\",\"sp\":\"0.00\",\"x\":\"NEW\",\"X\":\"NEW\",\"i\":760799835,\"l\":\"0.000\",\"z\":\"0.000\",\"L\":\"0.00\",\"T\":1579688850841,\"t\":0,\"b\":\"0.00000\",\"a\":\"0.00000\",\"m\":false,\"R\":false,\"wt\":\"CONTRACT_PRICE\",\"ot\":\"LIMIT\"}}"

                            //ACCOUNT_UPDATE
                            //"{\"e\":\"ACCOUNT_UPDATE\",\"T\":1579688850841,\"E\":1579688850846,\"a\":{\"B\":[{\"a\":\"USDT\",\"wb\":\"29.88018817\",\"cw\":\"29.88018817\"},{\"a\":\"BNB\",\"wb\":\"0.00000000\",\"cw\":\"0.00000000\"}],\"P\":[{\"s\":\"BTCUSDT\",\"pa\":\"0.000\",\"ep\":\"0.00000\",\"cr\":\"-0.05040000\",\"up\":\"0.00000000\",\"mt\":\"cross\",\"iw\":\"0.00000000\"},{\"s\":\"ETHUSDT\",\"pa\":\"0.000\",\"ep\":\"0.00000\",\"cr\":\"0.00000000\",\"up\":\"0.00000000\",\"mt\":\"cross\",\"iw\":\"0.00000000\"},{\"s\":\"BCHUSDT\",\"pa\":\"0.000\",\"ep\":\"0.00000\",\"cr\":\"0.00000000\",\"up\":\"0.00000000\",\"mt\":\"cross\",\"iw\":\"0.00000000\"},{\"s\":\"XRPUSDT\",\"pa\":\"0.0\",\"ep\":\"0.00000\",\"cr\":\"0.00000000\",\"up\":\"0.000000\",\"mt\":\"cross\",\"iw\":\"0.00000000\"},{\"s\":\"EOSUSDT\",\"pa\":\"0.0\",\"ep\":\"0.0000\",\"cr\":\"0.00000000\",\"up\":\"0.00000\",\"mt\":\"cross\",\"iw\":\"0.00000000\"},{\"s\":\"LTCUSDT\",\"pa\":\"0.000\",\"ep\":\"0.00000\",\"cr\":\"0.00000000\",\"up\":\"0.00000000\",\"mt\":\"cross\",\"iw\":\"0.00000000\"},{\"s\":\"TRXUSDT\",\"pa\":\"0\",\"ep\":\"0.00000\",\"cr\":\"0.00000000\",\"up\":\"0.00000\",\"mt\":\"cross\",\"iw\":\"0.00000000\"},{\"s\":\"ETCUSDT\",\"pa\":\"0.00\",\"ep\":\"0.00000\",\"cr\":\"0.00000000\",\"up\":\"0.0000000\",\"mt\":\"cross\",\"iw\":\"0.00000000\"},{\"s\":\"LINKUSDT\",\"pa\":\"0.00\",\"ep\":\"0.00000\",\"cr\":\"0.00000000\",\"up\":\"0.0000000\",\"mt\":\"cross\",\"iw\":\"0.00000000\"},{\"s\":\"XLMUSDT\",\"pa\":\"0\",\"ep\":\"0.00000\",\"cr\":\"0.00000000\",\"up\":\"0.00000\",\"mt\":\"cross\",\"iw\":\"0.00000000\"}]}}"

                            //LISTEN_KEY_EXPIRED
                            //"{\"e\": \"listenKeyExpired\", \"E\": 1653994245400}
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

        private static bool IsListenKeyExpiredEvent(string userDataMsg)
        {
            const string EVENT_NAME_KEY = "e";
            const string LISTEN_KEY_EXPIRED_EVENT_NAME = "listenKeyExpired";
            JObject userDataMsgJSON = JObject.Parse(userDataMsg);
            if (userDataMsgJSON != null && userDataMsgJSON.Property(EVENT_NAME_KEY) != null)
            {
                string eventName = userDataMsgJSON.Value<string>(EVENT_NAME_KEY);
                return String.Equals(eventName, LISTEN_KEY_EXPIRED_EVENT_NAME, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        private void UpdatePortfolio(AccountResponseFuturesFromWebSocket portfs)
        {
            try
            {
                return;

                if (portfs == null)
                {
                    return;
                }

                if (_portfolios == null)
                {
                    return;
                }

                Portfolio portfolio = null;

                portfolio = _portfolios.Find(p => p.Number == "BinanceFutures");


                if (portfolio == null)
                {
                    return;
                }

                foreach (var onePortf in portfs.a.B)
                {
                    if (onePortf == null ||
                        onePortf.a == null ||
                        onePortf.wb == null)
                    {
                        continue;
                    }

                    PositionOnBoard neeedPortf =
                        portfolio.GetPositionOnBoard().Find(p => p.SecurityNameCode == onePortf.a);

                    if (neeedPortf == null)
                    {
                        continue;
                    }

                    neeedPortf.ValueCurrent =
                        onePortf.wb.ToDecimal();
                }

                bool allPosesIsNull = true;

                foreach (var onePortf in portfs.a.P)
                {
                    if (onePortf == null ||
                        onePortf.s == null ||
                        onePortf.pa == null)
                    {
                        continue;
                    }

                    if (onePortf.ep.ToDecimal() == 0)
                    {
                        continue;
                    }

                    allPosesIsNull = false;

                    string name = onePortf.s;

                    if (onePortf.pa.ToDecimal() > 0)
                    {
                        name += "_LONG";
                    }
                    else
                    {
                        name += "_SHORT";
                    }

                    PositionOnBoard neeedPortf =
                        portfolio.GetPositionOnBoard().Find(p => p.SecurityNameCode == name);

                    if (neeedPortf == null)
                    {
                        PositionOnBoard newPositionOnBoard = new PositionOnBoard();
                        newPositionOnBoard.SecurityNameCode = name;
                        newPositionOnBoard.PortfolioName = portfolio.Number;
                        newPositionOnBoard.ValueBegin =
                            onePortf.pa.ToDecimal();
                        portfolio.SetNewPosition(newPositionOnBoard);
                        neeedPortf = newPositionOnBoard;
                    }

                    neeedPortf.ValueCurrent =
                        onePortf.pa.ToDecimal();
                }

                if (allPosesIsNull == true)
                {
                    foreach (var onePortf in portfs.a.P)
                    {
                        if (onePortf == null ||
                            onePortf.s == null ||
                            onePortf.pa == null)
                        {
                            continue;
                        }

                        PositionOnBoard neeedPortf =
                            portfolio.GetPositionOnBoard().Find(p => p.SecurityNameCode == onePortf.s);

                        if (neeedPortf == null)
                        {
                            PositionOnBoard newPositionOnBoard = new PositionOnBoard();
                            newPositionOnBoard.SecurityNameCode = onePortf.s;
                            newPositionOnBoard.PortfolioName = portfolio.Number;
                            newPositionOnBoard.ValueBegin = 0;
                            portfolio.SetNewPosition(newPositionOnBoard);
                            neeedPortf = newPositionOnBoard;
                        }

                        neeedPortf.ValueCurrent = 0;
                        break;
                    }
                }

                if (PortfolioEvent != null)
                {
                    PortfolioEvent(_portfolios);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateMyOrderAndMyOrders(string mes)
        {
            // если ошибки в ответе ордера
            OrderUpdResponse ord = new OrderUpdResponse();
            try
            {
                ord = JsonConvert.DeserializeAnonymousType(mes, new OrderUpdResponse());
            }
            catch (Exception)
            {
                SendLogMessage("error in order update:" + mes, LogMessageType.Error);
                return;
            }

            var order = ord.o;

            Int32 orderNumUser;

            try
            {
                orderNumUser = Convert.ToInt32(order.c.ToString().Replace("x-gnrPHWyE", ""));
            }
            catch (Exception)
            {
                orderNumUser = Convert.ToInt32(order.c.GetHashCode());
            }

            if (order.x == "NEW")
            {
                Order newOrder = new Order();
                newOrder.SecurityNameCode = order.s;
                newOrder.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(ord.T));
                newOrder.NumberUser = orderNumUser;

                newOrder.NumberMarket = order.i.ToString();
                //newOrder.PortfolioNumber = order.PortfolioNumber; добавить в сервере
                newOrder.Side = order.S == "BUY" ? Side.Buy : Side.Sell;
                newOrder.State = OrderStateType.Activ;
                newOrder.Volume = order.q.ToDecimal();
                newOrder.Price = order.p.ToDecimal();
                newOrder.ServerType = ServerType.BinanceFutures;
                newOrder.PortfolioNumber = "BinanceFutures";

                if(order.o == "MARKET")
                {
                    newOrder.TypeOrder = OrderPriceType.Market;
                }
                else
                {
                    newOrder.TypeOrder = OrderPriceType.Limit;
                }

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(newOrder);
                }
            }
            else if (order.x == "CANCELED")
            {
                Order newOrder = new Order();
                newOrder.SecurityNameCode = order.s;
                newOrder.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(ord.T));
                newOrder.TimeCancel = newOrder.TimeCallBack;
                newOrder.NumberUser = orderNumUser;
                newOrder.NumberMarket = order.i.ToString();
                newOrder.Side = order.S == "BUY" ? Side.Buy : Side.Sell;
                newOrder.State = OrderStateType.Cancel;
                newOrder.Volume = order.q.ToDecimal();
                newOrder.Price = order.p.ToDecimal();
                newOrder.ServerType = ServerType.BinanceFutures;
                newOrder.PortfolioNumber = "BinanceFutures";

                if (order.o == "MARKET")
                {
                    newOrder.TypeOrder = OrderPriceType.Market;
                }
                else
                {
                    newOrder.TypeOrder = OrderPriceType.Limit;
                }

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(newOrder);
                }
            }
            else if (order.x == "REJECTED")
            {
                Order newOrder = new Order();
                newOrder.SecurityNameCode = order.s;
                newOrder.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(ord.T));
                newOrder.NumberUser = orderNumUser;
                newOrder.NumberMarket = order.i.ToString();
                newOrder.Side = order.S == "BUY" ? Side.Buy : Side.Sell;
                newOrder.State = OrderStateType.Fail;
                newOrder.Volume = order.q.ToDecimal();
                newOrder.Price = order.p.ToDecimal();
                newOrder.ServerType = ServerType.BinanceFutures;
                newOrder.PortfolioNumber = "BinanceFutures";

                if (order.o == "MARKET")
                {
                    newOrder.TypeOrder = OrderPriceType.Market;
                }
                else
                {
                    newOrder.TypeOrder = OrderPriceType.Limit;
                }

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(newOrder);
                }
            }
            else if (order.x == "TRADE")
            {

                MyTrade trade = new MyTrade();
                trade.Time = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(order.T));
                trade.NumberOrderParent = order.i;
                trade.NumberTrade = order.t;
                trade.Volume = order.l.ToDecimal();
                trade.Price = order.L.ToDecimal();
                trade.SecurityNameCode = order.s;
                trade.Side = order.S == "BUY" ? Side.Buy : Side.Sell;

                if (MyTradeEvent != null)
                {
                    MyTradeEvent(trade);
                }

                if(order.X == "FILLED")
                {
                    Order newOrder = new Order();
                    newOrder.SecurityNameCode = order.s;
                    newOrder.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(ord.T));
                    newOrder.NumberUser = orderNumUser;
                    newOrder.NumberMarket = order.i.ToString();
                    newOrder.Side = order.S == "BUY" ? Side.Buy : Side.Sell;
                    newOrder.State = OrderStateType.Done;
                    newOrder.Volume = order.q.ToDecimal();
                    newOrder.Price = trade.Price;
                    newOrder.ServerType = ServerType.BinanceFutures;
                    newOrder.PortfolioNumber = "BinanceFutures";

                    if (order.o == "MARKET")
                    {
                        newOrder.TypeOrder = OrderPriceType.Market;
                    }
                    else
                    {
                        newOrder.TypeOrder = OrderPriceType.Limit;
                    }

                    if (MyOrderEvent != null)
                    {
                        MyOrderEvent(newOrder);
                    }
                }
            }
            else if (order.x == "EXPIRED")
            {
                Order newOrder = new Order();
                newOrder.SecurityNameCode = order.s;
                newOrder.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(ord.T));
                newOrder.TimeCancel = newOrder.TimeCallBack;
                newOrder.NumberUser = orderNumUser;
                newOrder.NumberMarket = order.i.ToString();
                newOrder.Side = order.S == "BUY" ? Side.Buy : Side.Sell;
                newOrder.State = OrderStateType.Cancel;
                newOrder.Volume = order.q.ToDecimal();
                newOrder.Price = order.p.ToDecimal();
                newOrder.ServerType = ServerType.BinanceFutures;
                newOrder.PortfolioNumber = "BinanceFutures";

                if (order.o == "MARKET")
                {
                    newOrder.TypeOrder = OrderPriceType.Market;
                }
                else
                {
                    newOrder.TypeOrder = OrderPriceType.Limit;
                }

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(newOrder);
                }
            }
            else
            {
                SendLogMessage(order.x, LogMessageType.Error);
            }
        }

        private void UpdateTrades(TradeResponse trades)
        {
            if (trades.data == null)
            {
                return;
            }
            Trade trade = new Trade();
            trade.SecurityNameCode = trades.stream.ToString().ToUpper().Split('@')[0];

            if (trade.SecurityNameCode != trades.data.s)
            {
                return;
            }

            trade.Price =
                    trades.data.p.ToDecimal();
            trade.Id = trades.data.t.ToString();
            trade.Time = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(trades.data.T));
            trade.Volume =
                    trades.data.q.ToDecimal();
            trade.Side = trades.data.m == true ? Side.Sell : Side.Buy;

            NewTradesEvent?.Invoke(trade);
        }

        private List<MarketDepth> _depths = new List<MarketDepth>();

        private readonly object _depthLocker = new object();

        private void UpdateMarketDepth(DepthResponseFutures myDepth)
        {
            try
            {
                lock (_depthLocker)
                {
                    if (myDepth.data.a == null || myDepth.data.a.Count == 0 ||
                        myDepth.data.b == null || myDepth.data.b.Count == 0)
                    {
                        return;
                    }

                    string secName = myDepth.stream.Split('@')[0].ToUpper();

                    MarketDepth needDepth = null;

                    for (int i = 0; i < _depths.Count; i++)
                    {
                        if (_depths[i].SecurityNameCode == secName)
                        {
                            needDepth = _depths[i];
                            break;
                        }
                    }

                    if (needDepth == null)
                    {
                        needDepth = new MarketDepth();
                        needDepth.SecurityNameCode = secName;
                        _depths.Add(needDepth);
                    }

                    List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
                    List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                    for (int i = 0; i < myDepth.data.a.Count; i++)
                    {
                        ascs.Add(new MarketDepthLevel()
                        {
                            Ask =
                                myDepth.data.a[i][1].ToString().ToDecimal()
                            ,
                            Price =
                                myDepth.data.a[i][0].ToString().ToDecimal()

                        });
                    }

                    for (int i = 0; i < myDepth.data.b.Count; i++)
                    {
                        bids.Add(new MarketDepthLevel()
                        {
                            Bid =
                                myDepth.data.b[i][1].ToString().ToDecimal()
                            ,
                            Price =
                                myDepth.data.b[i][0].ToString().ToDecimal()
                        });
                    }

                    needDepth.Asks = ascs;
                    needDepth.Bids = bids;

                    needDepth.Time = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(myDepth.data.T));

                    if (needDepth.Time == DateTime.MinValue)
                    {
                        return;
                    }

                    if (MarketDepthEvent != null)
                    {
                        MarketDepthEvent(needDepth.GetCopy());
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        #endregion

        #region 11 Trade

        private object _lockOrder = new object();

        public void SendOrder(Order order)
        {

            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                Dictionary<string, string> param = new Dictionary<string, string>();

                param.Add("symbol=", order.SecurityNameCode.ToUpper());
                param.Add("&side=", order.Side == Side.Buy ? "BUY" : "SELL");
                if (HedgeMode)
                {
                    if (order.PositionConditionType == OrderPositionConditionType.Close)
                    {
                        param.Add("&positionSide=", order.Side == Side.Buy ? "SHORT" : "LONG");
                    }
                    else
                    {
                        param.Add("&positionSide=", order.Side == Side.Buy ? "LONG" : "SHORT");
                    }
                }
                param.Add("&type=", order.TypeOrder == OrderPriceType.Limit ? "LIMIT" : "MARKET");
                //param.Add("&timeInForce=", "GTC");
                param.Add("&newClientOrderId=", "x-gnrPHWyE" + order.NumberUser.ToString());
                param.Add("&quantity=",
                    order.Volume.ToString(CultureInfo.InvariantCulture)
                        .Replace(CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator, "."));

                if (!HedgeMode && order.PositionConditionType == OrderPositionConditionType.Close)
                {
                    param.Add("&reduceOnly=", "true");
                }

                if (order.TypeOrder == OrderPriceType.Limit)
                {
                    param.Add("&timeInForce=", "GTC");
                    param.Add("&price=",
                        order.Price.ToString(CultureInfo.InvariantCulture)
                            .Replace(CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator, "."));

                }

                var res = CreateQuery(Method.POST, "/" + type_str_selector + "/v1/order", param, true);

                if (res != null && res.Contains("clientOrderId"))
                {
                    SendLogMessage(res, LogMessageType.Trade);
                }
                else
                {
                    order.State = OrderStateType.Fail;

                    if (MyOrderEvent != null)
                    {
                        MyOrderEvent(order);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        public void CancelOrder(Order order)
        {
            lock (_lockOrder)
            {
                try
                {
                    if (string.IsNullOrEmpty(order.NumberMarket))
                    {
                        Order onBoard = GetActualOrderQuery(order);

                        if (onBoard == null)
                        {
                            order.State = OrderStateType.Cancel;
                            SendLogMessage("When revoking an order, we didn't find it on the exchange. we think it's already been revoked.",
                                LogMessageType.Error);
                            if (MyOrderEvent != null)
                            {
                                MyOrderEvent(order);
                            }
                            return;
                        }
                        else if(onBoard.State == OrderStateType.Cancel)
                        {
                            order.TimeCancel = onBoard.TimeCallBack;
                            order.State = OrderStateType.Cancel;
                            if (MyOrderEvent != null)
                            {
                                MyOrderEvent(order);
                            }
                            return;
                        }
                        else if (onBoard.State == OrderStateType.Fail)
                        {
                            order.State = OrderStateType.Fail;

                            if (MyOrderEvent != null)
                            {
                                MyOrderEvent(order);
                            }
                            return;
                        }

                        order.NumberMarket = onBoard.NumberMarket;
                        order = onBoard;
                    }

                    Dictionary<string, string> param = new Dictionary<string, string>();

                    param.Add("symbol=", order.SecurityNameCode.ToUpper());
                    param.Add("&orderId=", order.NumberMarket);

                    CreateQuery(Method.DELETE, "/" + type_str_selector + "/v1/order", param, true);

                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                }
            }
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
            return;
            if(string.IsNullOrEmpty(order.NumberMarket))
            {
                SendLogMessage("Can`t change order price. Market Num order is null. " 
                    + " SecName: " + order.SecurityNameCode
                    + " NumberUser: " + order.NumberUser
                    ,LogMessageType.Error);
                return;
            }

            var param = new Dictionary<string, string>();
            param.Add("orderId=", order.NumberMarket);
            param.Add("origClientOrderId=", order.NumberUser.ToString());
            param.Add("symbol=", order.SecurityNameCode.ToUpper());
            param.Add("side=", order.Side.ToString().ToUpper());
            param.Add("quantity=", order.Volume.ToString());
            param.Add("price=", newPrice.ToString());

            var res = CreateQuery(
                       Method.PUT,
                       "/" + type_str_selector + "/v1/order",
                       param,
                       true);

            if (res == null)
            {
                return;
            }
        }

        public void CancelAllOrders()
        {
            try
            {
                List<Order> ordersOnBoard = GetAllActivOrdersQuery();

                if (ordersOnBoard == null)
                {
                    return;
                }

                for (int i = 0; i < ordersOnBoard.Count; i++)
                {
                    CancelOrder(ordersOnBoard[i]);
                }
            }
            catch(Exception e)
            {
                SendLogMessage(e.ToString(),LogMessageType.Error);
            }
        }

        public void GetAllActivOrders()
        {
            List<Order> ordersOnBoard = GetAllActivOrdersQuery();

            if(ordersOnBoard == null)
            {
                return;
            }

            for(int i = 0;i < ordersOnBoard.Count;i++)
            {
                if (MyOrderEvent != null)
                {
                    MyOrderEvent(ordersOnBoard[i]);
                }
            }
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            try
            {
                Dictionary<string, string> param = new Dictionary<string, string>();

                param.Add("symbol=", security.Name.ToUpper());

                CreateQuery(Method.DELETE, "/" + type_str_selector + "/v1/allOpenOrders", param, true);
            }
            catch (Exception exeption)
            {
                SendLogMessage(exeption.Message, LogMessageType.Error);
            }
        }

        public void GetOrderStatus(Order order)
        {
            Order myOrder = GetActualOrderQuery(order);

            if(myOrder == null)
            {
                return;
            }

            if (MyOrderEvent != null)
            {
                MyOrderEvent(myOrder);
            }

            if(myOrder.State == OrderStateType.Done ||
                myOrder.State == OrderStateType.Patrial)
            {
                List<MyTrade> myTrades = GetMyTradesByOrderQuery(myOrder);

                for(int i = 0;myTrades != null && i < myTrades.Count;i++)
                {
                    if(MyTradeEvent != null)
                    {
                        MyTradeEvent(myTrades[i]);
                    }
                }
            }
        }

        public List<MyTrade> GetMyTradesByOrderQuery(Order order)
        {
            try
            {
                var param = new Dictionary<string, string>();
                param.Add("symbol=", order.SecurityNameCode.ToUpper());

                var res = CreateQuery(
                           Method.GET,
                           "/" + type_str_selector + "/v1/userTrades",
                           param,
                           true);

                if(res == null)
                {
                    return null;
                }

                List<TradesResponseReserches> responceTrades =
                    JsonConvert.DeserializeAnonymousType(res, new List<TradesResponseReserches>());

                List < MyTrade > trades = new List<MyTrade >();

                for (int j = 0; j < responceTrades.Count; j++)
                {
                    if (order.NumberMarket == Convert.ToString(responceTrades[j].orderid))
                    {
                        TradesResponseReserches responcetrade = responceTrades[j];

                        MyTrade trade = new MyTrade();
                        trade.Time = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(responcetrade.time));
                        trade.NumberOrderParent = responcetrade.orderid.ToString();
                        trade.NumberTrade = responcetrade.id.ToString();
                        trade.Volume = responcetrade.qty.ToDecimal();
                        trade.Price = responcetrade.price.ToDecimal();
                        trade.SecurityNameCode = responcetrade.symbol;
                        trade.Side = responcetrade.side == "BUY" ? Side.Buy : Side.Sell;

                        trades.Add(trade);
                    }
                }

                return trades;
            }
            catch (Exception)
            {
                //ignore
            }
            return null;
        }

        private List<Order> GetAllActivOrdersQuery()
        {
            try
            {
                string res = CreateQuery(Method.GET, "/" + type_str_selector + "/v1/openOrders", null, true);

                if (res == null ||
                    res == "[]")
                {
                    return null;
                }

                List<OrderOpenRestRespFut> respOrders = JsonConvert.DeserializeAnonymousType(res, new List<OrderOpenRestRespFut>());

                List<Order> orderOnBoard = new List<Order>();

                for (int i = 0; i < respOrders.Count; i++)
                {
                    OrderOpenRestRespFut orderOnBoardResp = respOrders[i];

                    Order newOrder = new Order();

                    newOrder.PortfolioNumber = "BinanceFutures";
                    newOrder.NumberMarket = orderOnBoardResp.orderId;

                    if (string.IsNullOrEmpty(orderOnBoardResp.clientOrderId) == false)
                    {
                        try
                        {
                            newOrder.NumberUser =
                                Convert.ToInt32(orderOnBoardResp.clientOrderId.Replace("x-gnrPHWyE", ""));
                        }
                        catch
                        {
                            // ignore
                        }
                    }

                    newOrder.Price = orderOnBoardResp.price.ToDecimal();
                    newOrder.Volume = orderOnBoardResp.origQty.ToDecimal();
                    newOrder.TypeOrder = OrderPriceType.Limit;
                    newOrder.State = OrderStateType.Activ;

                    if (string.IsNullOrEmpty(orderOnBoardResp.executedQty) == false &&
                        orderOnBoardResp.executedQty != "0")
                    {
                        newOrder.VolumeExecute = orderOnBoardResp.executedQty.ToDecimal();
                        newOrder.State = OrderStateType.Patrial;
                    }
                    newOrder.ServerType = ServerType.BinanceFutures;
                    newOrder.SecurityNameCode = orderOnBoardResp.symbol;
                    newOrder.TimeCreate = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(orderOnBoardResp.time));
                    newOrder.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(orderOnBoardResp.updateTime));

                    if (orderOnBoardResp.side == "BUY")
                    {
                        newOrder.Side = Side.Buy;
                    }
                    else
                    {
                        newOrder.Side = Side.Sell;
                    }

                    orderOnBoard.Add(newOrder);
                }

                return orderOnBoard;
            }
            catch (Exception exeption)
            {
                SendLogMessage(exeption.Message, LogMessageType.Error);
            }
            return null;
        }

        private Order GetActualOrderQuery(Order oldOrder)
        {
            string endPoint = "/" + type_str_selector + "/v1/allOrders";

            var param = new Dictionary<string, string>();
            param.Add("symbol=", oldOrder.SecurityNameCode.ToUpper());
            //param.Add("&recvWindow=" , "100");
            //param.Add("&limit=", GetNonce());
            param.Add("&limit=", "500");
            //"symbol={symbol.ToUpper()}&recvWindow={recvWindow}"

            var res = CreateQuery(Method.GET, endPoint, param, true);

            if (res == null)
            {
                return null;
            }

            List<HistoryOrderReport> allOrders = 
                JsonConvert.DeserializeAnonymousType(res, new List<HistoryOrderReport>());

            HistoryOrderReport orderOnBoard = null;

            for (int i = 0;i < allOrders.Count;i++)
            {
                if(string.IsNullOrEmpty(allOrders[i].clientOrderId))
                {
                    continue;
                }

                if (allOrders[i].clientOrderId.Replace("x-gnrPHWyE", "") == oldOrder.NumberUser.ToString())
                {
                    orderOnBoard = allOrders[i];
                    break;
                }
            }

            if (orderOnBoard == null)
            {
                return null;
            }

            Order newOrder = new Order();
            newOrder.NumberMarket = orderOnBoard.orderId;
            newOrder.NumberUser = oldOrder.NumberUser;
            newOrder.SecurityNameCode = oldOrder.SecurityNameCode;
            newOrder.State = OrderStateType.Cancel;

            newOrder.Volume = oldOrder.Volume;
            newOrder.VolumeExecute = oldOrder.VolumeExecute;
            newOrder.Price = oldOrder.Price;
            newOrder.TypeOrder = oldOrder.TypeOrder;

            newOrder.TimeCreate = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(orderOnBoard.time));
            newOrder.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(orderOnBoard.updateTime));

            newOrder.ServerType = ServerType.BinanceFutures;
            newOrder.PortfolioNumber = oldOrder.PortfolioNumber;

            newOrder.Side = oldOrder.Side;

            if (orderOnBoard.status == "NEW" ||
                orderOnBoard.status == "PARTIALLY_FILLED")
            { // order is active. Do nothing / ордер активен. Ничего не делаем
                newOrder.State = OrderStateType.Activ;
            }
            else if (orderOnBoard.status == "FILLED")
            {
                newOrder.State = OrderStateType.Done;
            }
            else
            {
                newOrder.State = OrderStateType.Cancel;
                newOrder.TimeCancel = newOrder.TimeCallBack;
            }

            return newOrder;
        }

        #endregion

        #region 12 Queries

        private string _queryHttpLocker = "queryHttpLocker";

        RateGate _rateGate = new RateGate(1, TimeSpan.FromMilliseconds(100));

        public string CreateQuery(Method method, string endpoint, Dictionary<string, string> param = null, bool auth = false)
        {
            try
            {
                lock (_queryHttpLocker)
                {
                    _rateGate.WaitToProceed();
                    return PerformHttpRequest(method, endpoint, param, auth);
                }
            }
            catch (Exception ex)
            {
                return HandleHttpRequestException(ex);
            }
        }

        public string CreateQueryNoLock(Method method, string endpoint, Dictionary<string, string> param = null, bool auth = false)
        {
            try
            {
                return PerformHttpRequest(method, endpoint, param, auth);
            }
            catch (Exception ex)
            {
                return HandleHttpRequestException(ex);
            }
        }

        private string PerformHttpRequest(Method method, string endpoint, Dictionary<string, string> param = null, bool auth = false)
        {
            string fullUrl = "";

            if (param != null)
            {
                fullUrl += "?";

                foreach (var onePar in param)
                {
                    fullUrl += onePar.Key + onePar.Value;
                }
            }

            if (auth)
            {
                string message = "";

                string timeStamp = GetNonce();

                message += "timestamp=" + timeStamp;

                if (fullUrl == "")
                {
                    fullUrl = "?timestamp=" + timeStamp + "&signature=" + CreateSignature(message);
                }
                else
                {
                    message = fullUrl + "&timestamp=" + timeStamp;
                    fullUrl += "&timestamp=" + timeStamp + "&signature=" + CreateSignature(message.Trim('?'));
                }
            }

            var request = new RestRequest(endpoint + fullUrl, method);
            request.AddHeader("X-MBX-APIKEY", ApiKey);

            string baseUrl = _baseUrl;

            var response = new RestClient(baseUrl).Execute(request).Content;

            if (response.StartsWith("<!DOCTYPE"))
            {
                throw new Exception(response);
            }
            else if (response.Contains("code") && !response.Contains("code\": 200"))
            {
                var error = JsonConvert.DeserializeAnonymousType(response, new ErrorMessage());
                throw new Exception(error.msg);
            }

            return response;
        }

        private string HandleHttpRequestException(Exception ex)
        {
            if (ex.ToString().Contains("This listenKey does not exist"))
            {
                RenewListenKey();
                return null;
            }
            if (ex.ToString().Contains("Unknown order sent"))
            {
                //SendLogMessage(ex.ToString(), LogMessageType.System);
                return null;
            }

            SendLogMessage(ex.ToString(), LogMessageType.Error);
            return null;
        }

        private string GetNonce()
        {
            var resTime = CreateQuery(Method.GET, "/" + type_str_selector + "/v1/time", null, false);

            if (!string.IsNullOrEmpty(resTime))
            {
                var result = JsonConvert.DeserializeAnonymousType(resTime, new BinanceTime());
                return (result.serverTime + 500).ToString();
            }
            else
            {
                DateTime yearBegin = new DateTime(1970, 1, 1);
                var timeStamp = DateTime.UtcNow - yearBegin;
                var r = timeStamp.TotalMilliseconds;
                var re = Convert.ToInt64(r);

                return re.ToString();
            }
        }

        private string CreateSignature(string message)
        {
            var messageBytes = Encoding.UTF8.GetBytes(message);
            var keyBytes = Encoding.UTF8.GetBytes(SecretKey);
            var hash = new HMACSHA256(keyBytes);
            var computedHash = hash.ComputeHash(messageBytes);
            return BitConverter.ToString(computedHash).Replace("-", "").ToLower();
        }

        private byte[] Hmacsha256(byte[] keyByte, byte[] messageBytes)
        {
            using (var hash = new HMACSHA256(keyByte))
            {
                return hash.ComputeHash(messageBytes);
            }
        }

        #endregion

        #region 13 Log

        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }


}