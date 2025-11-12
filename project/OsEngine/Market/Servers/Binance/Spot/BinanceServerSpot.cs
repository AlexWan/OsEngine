/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Binance.Spot.BinanceSpotEntity;
using OsEngine.Market.Servers.Entity;
using RestSharp;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using OsEngine.Entity.WebSocketOsEngine;
using TradeResponse = OsEngine.Market.Servers.Binance.Spot.BinanceSpotEntity.TradeResponse;

namespace OsEngine.Market.Servers.Binance.Spot
{
    public class BinanceServerSpot : AServer
    {
        public BinanceServerSpot(int uniqueNumber)
        {
            ServerNum = uniqueNumber;
            BinanceServerRealization realization = new BinanceServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterSecretKey, "");
            CreateParameterBoolean("Extended Data", false);

            ServerParameters[0].Comment = OsLocalization.Market.Label246;
            ServerParameters[1].Comment = OsLocalization.Market.Label247;
            ServerParameters[2].Comment = OsLocalization.Market.Label269;
        }
    }

    public class BinanceServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public BinanceServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread worker1 = new Thread(KeepaliveUserDataStream);
            worker1.Name = "BinanceSpotThread_KeepaliveUserDataStream";
            worker1.Start();

            Thread worker2 = new Thread(ConverterPublicData);
            worker2.Name = "BinanceSpotThread_ConverterPublicData";
            worker2.Start();

            Thread worker3 = new Thread(ConverterUserData);
            worker3.Name = "BinanceSpotThread_ConverterUserData";
            worker3.Start();
        }

        private WebProxy _myProxy;

        public void Connect(WebProxy proxy = null)
        {
            _myProxy = proxy;

            ApiKey = ((ServerParameterString)ServerParameters[0]).Value;
            SecretKey = ((ServerParameterPassword)ServerParameters[1]).Value;

            if (string.IsNullOrEmpty(ApiKey) ||
                string.IsNullOrEmpty(SecretKey))
            {
                SendLogMessage("Can`t run Binance Spot connector. No keys", LogMessageType.Error);
                return;
            }

            // check server availability for HTTP communication with it 
            Uri uri = new Uri(_baseUrl + "/v1/time");
            try
            {
                RestRequest requestRest = new RestRequest("/v1/time", Method.GET);
                RestClient client = new RestClient(_baseUrl);
                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage("Can`t run Binance Spot connector. No internet connection", LogMessageType.Error);

                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                        return;
                    }
                }
            }
            catch
            {
                SendLogMessage("Can`t run Binance Spot connector. No internet connection", LogMessageType.Error);
                return;
            }

            if (((ServerParameterBool)ServerParameters[2]).Value == true)
            {
                _extendedMarketData = true;
            }
            else
            {
                _extendedMarketData = false;
            }

            CreateDataStreams();
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

            _subscribedSecurities.Clear();
            _securities = new List<Security>();
            _depths.Clear();
            _newMessagePrivate = new ConcurrentQueue<BinanceUserMessage>();
            _newMessagePublic = new ConcurrentQueue<string>();
        }

        public ServerType ServerType
        {
            get { return ServerType.Binance; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public DateTime ServerTime { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

        #endregion

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        public string ApiKey;

        public string SecretKey;

        private string _baseUrl = "https://api.binance.com/api";

        private string _spotListenKey = "";

        private string _marginListenKey = "";

        private bool _notMargineAccount;

        private bool _extendedMarketData;

        #endregion

        #region 3 Securities

        public void GetSecurities()
        {

            try
            {
                var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, "api/v1/exchangeInfo", null, false);
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

        private List<Security> _securities;

        private void UpdatePairs(SecurityResponce pairs)
        {
            if (_securities == null)
            {
                _securities = new List<Security>();
            }

            foreach (var sec in pairs.symbols)
            {
                if (sec.status != "TRADING")
                {
                    continue;
                }

                Security security = new Security();
                security.Name = sec.symbol;
                security.NameFull = sec.symbol;
                security.NameClass = sec.quoteAsset;
                security.NameId = sec.symbol + sec.quoteAsset;
                security.SecurityType = SecurityType.CurrencyPair;
                security.Exchange = ServerType.Binance.ToString();
                // sec.filters[1] - minimum volume equal to price * volume

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
                   sec.filters[1] != null)
                {
                    if (sec.filters[1].minQty != null)
                    {
                        decimal minQty = sec.filters[1].minQty.ToDecimal();

                        security.Lot = 1;
                        string qtyInStr = minQty.ToStringWithNoEndZero().Replace(",", ".");

                        if (qtyInStr.Split('.').Length > 1)
                        {
                            security.DecimalsVolume = qtyInStr.Split('.')[1].Length;
                        }
                    }

                    if (sec.filters[1].stepSize != null)
                    {
                        security.VolumeStep = sec.filters[1].stepSize.ToDecimal();
                    }
                }

                if (sec.filters.Count > 1 &&
                    sec.filters[6] != null &&
                    sec.filters[6].minNotional != null)
                {
                    security.MinTradeAmount = sec.filters[6].minNotional.ToDecimal();
                }

                security.MinTradeAmountType = MinTradeAmountType.C_Currency;

                security.State = SecurityStateType.Activ;
                _securities.Add(security);
            }

            if (SecurityEvent != null)
            {
                SecurityEvent(_securities);
            }
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        private List<Portfolio> _portfolios = new List<Portfolio>();

        public void GetPortfolios()
        {
            GetBalanceSpot();
            GetBalanceMargin();
        }

        private void GetBalanceSpot()
        {
            try
            {
                var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, "api/v3/account", null, true);

                if (res == null)
                {
                    return;
                }

                AccountResponse resp = JsonConvert.DeserializeAnonymousType(res, new AccountResponse());

                NewPortfolioSpot(resp);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void GetBalanceMargin()
        {
            try
            {
                var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, "/sapi/v1/margin/account", null, true);

                if (res == null)
                {
                    return;
                }

                AccountResponseMargin resp = JsonConvert.DeserializeAnonymousType(res, new AccountResponseMargin());

                NewPortfolioMargin(resp);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void NewPortfolioSpot(AccountResponse portfs)
        {
            try
            {
                Portfolio myPortfolio = _portfolios.Find(p => p.Number == "BinanceSpot");

                if (myPortfolio == null)
                {
                    Portfolio newPortf = new Portfolio();
                    newPortf.Number = "BinanceSpot";
                    newPortf.ValueBegin = 1;
                    newPortf.ValueCurrent = 1;
                    _portfolios.Add(newPortf);
                    myPortfolio = newPortf;
                }

                if (portfs.balances == null)
                {
                    return;
                }

                foreach (var onePortf in portfs.balances)
                {
                    PositionOnBoard newPortf = new PositionOnBoard();
                    newPortf.SecurityNameCode = onePortf.asset;

                    newPortf.ValueBegin =
                        onePortf.free.ToDecimal();
                    newPortf.ValueCurrent =
                        onePortf.free.ToDecimal();
                    newPortf.ValueBlocked =
                        onePortf.locked.ToDecimal();

                    newPortf.PortfolioName = "BinanceSpot";

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

        private void NewPortfolioMargin(AccountResponseMargin portfs)
        {
            try
            {
                if (portfs == null)
                {
                    return;
                }

                if (portfs.userAssets == null)
                {
                    return;
                }

                Portfolio myPortfolio = _portfolios.Find(p => p.Number == "BinanceMargin");

                if (myPortfolio == null)
                {
                    Portfolio newPortf = new Portfolio();
                    newPortf.Number = "BinanceMargin";
                    newPortf.ValueBegin = 1;
                    newPortf.ValueCurrent = 1;

                    _portfolios.Add(newPortf);
                    myPortfolio = newPortf;
                }


                foreach (var onePortf in portfs.userAssets)
                {
                    PositionOnBoard newPortf = new PositionOnBoard();
                    newPortf.SecurityNameCode = onePortf.asset;
                    newPortf.ValueBegin =
                        onePortf.free.ToDecimal();
                    newPortf.ValueCurrent =
                        onePortf.free.ToDecimal();
                    newPortf.ValueBlocked =
                        onePortf.locked.ToDecimal();

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

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            List<Candle> candles = GetCandles(security.Name, timeFrameBuilder.TimeFrameTimeSpan);

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
            List<Candle> candles = new List<Candle>();

            if (actualTime > endTime)
            {
                return null;
            }

            actualTime = startTime;

            while (actualTime < endTime)
            {
                List<Candle> newCandles = GetCandlesForTimes(security.Name,
                    timeFrameBuilder.TimeFrameTimeSpan,
                    actualTime, endTime);

                if (newCandles != null && candles.Count != 0 && newCandles.Count != 0)
                {
                    for (int i = 0; i < newCandles.Count; i++)
                    {
                        if (candles[candles.Count - 1].TimeStart >= newCandles[i].TimeStart)
                        {
                            newCandles.RemoveAt(i);
                            i--;
                        }
                    }
                }

                if (newCandles == null)
                {
                    actualTime = actualTime.AddDays(5);
                    continue;
                }

                if (newCandles.Count == 0)
                {
                    break;
                }

                candles.AddRange(newCandles);

                actualTime = candles[candles.Count - 1].TimeStart;

                Thread.Sleep(60);
            }

            if (candles.Count == 0)
            {
                return null;
            }

            for (int i = candles.Count - 1; i >= 0; i--)
            {
                if (candles[i].TimeStart <= endTime)
                {
                    break;
                }
                if (candles[i].TimeStart > endTime)
                {
                    candles.RemoveAt(i);
                }
            }

            for (int i = 1; i < candles.Count; i++)
            {
                Candle candleNow = candles[i];
                Candle candleLast = candles[i - 1];

                if (candleLast.TimeStart == candleNow.TimeStart)
                {
                    candles.RemoveAt(i);
                    i--;
                    continue;
                }
            }

            for (int i = 0; i < candles.Count; i++)
            {
                Candle candleNow = candles[i];

                if (candleNow.Volume == 0)
                {
                    candles.RemoveAt(i);
                    i--;
                    continue;
                }
            }

            return candles;
        }

        private List<Candle> _deserializeCandles(string jsonCandles)
        {
            try
            {
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
                        newCandle.TimeStart = new DateTime(1970, 1, 1).AddMilliseconds(param[0].ToDouble());
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
                        newCandle.TimeStart = new DateTime(1970, 1, 1).AddMilliseconds(param[0].ToDouble());
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
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
                return null;
            }
        }

        public List<Candle> GetCandlesForTimes(string nameSec, TimeSpan tf, DateTime timeStart, DateTime timeEnd)
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
                case 240:
                    needTf = "4h";
                    break;
                case 1440:
                    needTf = "1d";
                    break;
            }

            if (needTf == "")
            {
                return null;
            }

            string endPoint = "api/v1/klines";

            if (needTf != "2m" && needTf != "10m" && needTf != "20m" && needTf != "45m")
            {
                var param = new Dictionary<string, string>();
                param.Add("symbol=" + nameSec.ToUpper(), "&interval=" + needTf + "&startTime=" + startTime + "&endTime=" + endTime);

                var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, endPoint, param, false);
                if (res == "")
                {
                    return null;
                }

                var candles = _deserializeCandles(res);
                return candles;
            }
            else
            {
                if (needTf == "2m")
                {
                    var param = new Dictionary<string, string>();
                    param.Add("symbol=" + nameSec.ToUpper(), "&interval=1m" + "&startTime=" + startTime + "&endTime=" + endTime);
                    var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);

                    var newCandles = BuildCandles(candles, 2, 1);
                    return newCandles;
                }
                else if (needTf == "10m")
                {
                    var param = new Dictionary<string, string>();
                    param.Add("symbol=" + nameSec.ToUpper(), "&interval=5m" + "&startTime=" + startTime + "&endTime=" + endTime);
                    var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);
                    var newCandles = BuildCandles(candles, 10, 5);
                    return newCandles;
                }
                else if (needTf == "20m")
                {
                    var param = new Dictionary<string, string>();
                    param.Add("symbol=" + nameSec.ToUpper(), "&interval=5m" + "&startTime=" + startTime + "&endTime=" + endTime);
                    var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);
                    var newCandles = BuildCandles(candles, 20, 5);
                    return newCandles;
                }
                else if (needTf == "45m")
                {
                    var param = new Dictionary<string, string>();
                    param.Add("symbol=" + nameSec.ToUpper(), "&interval=15m" + "&startTime=" + startTime + "&endTime=" + endTime);
                    var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);
                    var newCandles = BuildCandles(candles, 45, 15);
                    return newCandles;
                }
            }

            return null;
        }

        public List<Candle> GetCandles(string nameSec, TimeSpan tf)
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

            string endPoint = "api/v1/klines";

            if (needTf != "2m" && needTf != "10m" && needTf != "20m" && needTf != "45m")
            {
                var param = new Dictionary<string, string>();
                param.Add("symbol=" + nameSec.ToUpper(), "&interval=" + needTf);

                var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, endPoint, param, false);

                var candles = _deserializeCandles(res);
                return candles;
            }
            else
            {
                if (needTf == "2m")
                {
                    var param = new Dictionary<string, string>();
                    param.Add("symbol=" + nameSec.ToUpper(), "&interval=1m");
                    var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);

                    var newCandles = BuildCandles(candles, 2, 1);
                    return newCandles;
                }
                else if (needTf == "10m")
                {
                    var param = new Dictionary<string, string>();
                    param.Add("symbol=" + nameSec.ToUpper(), "&interval=5m");
                    var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);
                    var newCandles = BuildCandles(candles, 10, 5);
                    return newCandles;
                }
                else if (needTf == "20m")
                {
                    var param = new Dictionary<string, string>();
                    param.Add("symbol=" + nameSec.ToUpper(), "&interval=5m");
                    var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);
                    var newCandles = BuildCandles(candles, 20, 5);
                    return newCandles;
                }
                else if (needTf == "45m")
                {
                    var param = new Dictionary<string, string>();
                    param.Add("symbol=" + nameSec.ToUpper(), "&interval=15m");
                    var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);
                    var newCandles = BuildCandles(candles, 45, 15);
                    return newCandles;
                }
            }

            return null;
        }

        private List<Candle> BuildCandles(List<Candle> oldCandles, int needTf, int oldTf)
        {
            List<Candle> newCandles = new List<Candle>();

            if (oldCandles == null)
            {
                return null;
            }

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
            if (lastDate > endTime)
            {
                return null;
            }

            string markerDateTime = "";

            List<Trade> trades = new List<Trade>();

            DateTime startOver = startTime;

            long lastId = 0;

            while (true)
            {
                if (startOver >= endTime)
                {
                    break;
                }

                List<Trade> newTrades = new List<Trade>();

                if (lastId == 0)
                {
                    List<Trade> firstTrades = new List<Trade>();

                    int countMinutesAddToFindFirstTrade = 0;
                    int countDaysAddToFindFirstTrade = 0;

                    do
                    {
                        firstTrades = GetTickHistoryToSecurity(security.Name, startOver, startOver.AddSeconds(60), 0);
                        startOver = startOver.AddSeconds(60);

                        if ((firstTrades == null || firstTrades.Count == 0) &&
                            countMinutesAddToFindFirstTrade < 10)
                        {
                            countMinutesAddToFindFirstTrade++;
                            startOver = startOver.AddMinutes(60);
                        }
                        else if ((firstTrades == null || firstTrades.Count == 0) &&
                            countDaysAddToFindFirstTrade < 10)
                        {
                            countDaysAddToFindFirstTrade++;
                            startOver = startOver.AddDays(1);
                        }
                        else if (firstTrades == null || firstTrades.Count == 0)
                        {
                            startOver = startOver.AddDays(30);
                        }

                        if (startOver >= endTime)
                        {
                            return null;
                        }
                    }
                    while (firstTrades == null || firstTrades.Count == 0);


                    Trade firstTrade = firstTrades.First();

                    lastId = Convert.ToInt64(firstTrade.Id);

                    newTrades.Add(firstTrade);
                }
                else
                {
                    newTrades = GetTickHistoryToSecurity(security.Name, new DateTime(), new DateTime(), lastId + 1);

                    try
                    {
                        lastId = Convert.ToInt64(newTrades[newTrades.Count - 1].Id);
                    }
                    catch { } // If the date for which we download the candles is greater than today: Ignore
                }

                if (newTrades != null && newTrades.Count != 0)
                    trades.AddRange(newTrades);
                else
                    break;

                startOver = trades[trades.Count - 1].Time.AddMilliseconds(1);

                if (markerDateTime != startOver.ToShortDateString())
                {
                    if (startOver >= endTime)
                    {
                        break;
                    }
                    markerDateTime = startOver.ToShortDateString();
                    SendLogMessage(security.Name + " Binance Spot loading trades: " + markerDateTime, LogMessageType.System);
                }
            }

            if (trades.Count == 0)
            {
                return null;
            }

            for (int i = trades.Count - 1; i >= 0; i--)
            {
                if (trades[i].Time <= endTime)
                {
                    break;
                }
                if (trades[i].Time > endTime)
                {
                    trades.RemoveAt(i);
                }
            }

            for (int i = 1; i < trades.Count; i++)
            {
                Trade tradeNow = trades[i];
                Trade tradeLast = trades[i - 1];

                if (tradeLast.Time == tradeNow.Time)
                {
                    trades.RemoveAt(i);
                    i--;
                    continue;
                }
            }

            return trades;
        }

        public List<Trade> GetTickHistoryToSecurity(string security, DateTime startTime, DateTime endTime, long fromId)
        {
            try
            {
                Thread.Sleep(1000); // do not remove! RateGate does not help in CreateQuery

                string timeStamp = TimeManager.GetUnixTimeStampMilliseconds().ToString();
                Dictionary<string, string> param = new Dictionary<string, string>();

                if (startTime != new DateTime() && endTime != new DateTime())
                {
                    long from = TimeManager.GetTimeStampMilliSecondsToDateTime(startTime);
                    long to = TimeManager.GetTimeStampMilliSecondsToDateTime(endTime);

                    param.Add("symbol=", security);
                    param.Add("&startTime=", from.ToString());
                    param.Add("&endTime=", to.ToString());
                    param.Add("&limit=", "1000");
                }
                else if (fromId != 0)
                {
                    param.Add("symbol=", security);
                    param.Add("&fromId=", fromId.ToString());
                    param.Add("&limit=", "1000");
                }

                string endPoint = "api/v3/aggTrades";

                var res2 = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, endPoint, param, false);

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

        private List<Trade> CreateTradesFromJson(string secName, AgregatedHistoryTrade[] binTrades)
        {
            List<Trade> trades = new List<Trade>();

            foreach (var jtTrade in binTrades)
            {
                var trade = new Trade();

                trade.Time = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(jtTrade.T));
                trade.Price = jtTrade.p.ToDecimal();
                trade.MicroSeconds = 0;
                trade.Id = jtTrade.a.ToString();
                trade.Volume = Math.Abs(jtTrade.q.ToDecimal());
                trade.SecurityNameCode = secName;

                if (jtTrade.m[0] == 'T'
                    || jtTrade.m[0] == 't')
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

        private WebSocket _spotSocketClient;

        private WebSocket _marginSocketClient;

        private Dictionary<string, WebSocket> _wsStreamsSecurityData = new Dictionary<string, WebSocket>();

        private RateGate _rateGateCreateDisposeSockets = new RateGate(1, TimeSpan.FromSeconds(2));

        private void CreateDataStreams()
        {
            _rateGateCreateDisposeSockets.WaitToProceed();

            if (_spotSocketClient == null)
            {
                _spotSocketClient = CreateUserDataStream("api/v3/userDataStream", BinanceExchangeType.SpotExchange);

                if (_spotSocketClient == null)
                {
                    return;
                }

                _spotSocketClient.OnMessage += _spotSocketClient_MessageReceived;
            }

            try
            {
                if (_marginSocketClient == null)
                {
                    _marginSocketClient = CreateUserDataStream("/sapi/v1/userDataStream", BinanceExchangeType.MarginExchange);

                    if (_marginSocketClient != null)
                    {
                        _marginSocketClient.OnMessage += _marginSocketClient_MessageReceived;
                    }
                    else
                    {
                        _notMargineAccount = true;
                    }
                }
            }
            catch
            {
                _notMargineAccount = true;
            }
        }

        private void DisposeSockets()
        {
            try
            {
                _rateGateCreateDisposeSockets.WaitToProceed();

                if (_spotSocketClient != null)
                {
                    _spotSocketClient.OnOpen -= Client_Opened;
                    _spotSocketClient.OnClose -= Client_Closed;
                    _spotSocketClient.OnError -= Client_Error;
                    _spotSocketClient.OnMessage -= _spotSocketClient_MessageReceived;
                    _spotSocketClient.CloseAsync();
                }
            }
            catch
            {
                // ignore
            }

            _spotSocketClient = null;

            try
            {
                if (_marginSocketClient != null)
                {
                    _marginSocketClient.OnOpen -= Client_Opened;
                    _marginSocketClient.OnClose -= Client_Closed;
                    _marginSocketClient.OnError -= Client_Error;
                    _marginSocketClient.OnMessage -= _spotSocketClient_MessageReceived;
                    _marginSocketClient.CloseAsync();
                }
            }
            catch
            {
                // ignore
            }

            _marginSocketClient = null;

            try
            {
                if (_wsStreamsSecurityData != null)
                {
                    foreach (var ws in _wsStreamsSecurityData)
                    {
                        ws.Value.OnMessage -= _publicSocketClient_RessageReceived;
                        ws.Value.OnError -= Client_Error;
                        ws.Value.OnClose -= Client_Closed;
                    }
                }
            }
            catch
            {
                // ignore
            }

            try
            {
                if (_wsStreamsSecurityData != null)
                {
                    foreach (var ws in _wsStreamsSecurityData)
                    {
                        try
                        {
                            ws.Value.CloseAsync();
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
                if (_wsStreamsSecurityData != null)
                {
                    _wsStreamsSecurityData.Clear();
                }
            }
            catch
            {
                // ignore
            }
        }

        private WebSocket CreateUserDataStream(string url, BinanceExchangeType exType)
        {
            try
            {
                var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.POST, url, null, false);

                string urlStr = "";

                if (string.IsNullOrEmpty(res))
                {
                    SendLogMessage("Socket don`t open. Internet error", LogMessageType.Connect);
                    return null;
                }

                if (exType == BinanceExchangeType.SpotExchange)
                {
                    _spotListenKey = JsonConvert.DeserializeAnonymousType(res, new ListenKey()).listenKey;
                    urlStr = "wss://stream.binance.com:9443/ws/" + _spotListenKey;
                }
                else if (exType == BinanceExchangeType.MarginExchange)
                {
                    _marginListenKey = JsonConvert.DeserializeAnonymousType(res, new ListenKey()).listenKey;
                    urlStr = "wss://stream.binance.com:9443/ws/" + _marginListenKey;
                }

                WebSocket client = new WebSocket(urlStr); //create a web socket / создаем вебсокет

                if (_myProxy != null)
                {
                    client.SetProxy(_myProxy);
                }

                client.OnOpen += Client_Opened;
                client.OnError += Client_Error;
                client.OnClose += Client_Closed;
                client.ConnectAsync();

                return client;
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.Message, LogMessageType.Connect);
                return null;
            }
        }

        private void Client_Opened(object sender, EventArgs e)
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

        private void Client_Closed(object sender, CloseEventArgs e)
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

        private void Client_Error(object sender, ErrorEventArgs e)
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

        #endregion

        #region 7 WebSocket events

        private ConcurrentQueue<BinanceUserMessage> _newMessagePrivate = new ConcurrentQueue<BinanceUserMessage>();

        private ConcurrentQueue<string> _newMessagePublic = new ConcurrentQueue<string>();

        private void _marginSocketClient_MessageReceived(object sender, MessageEventArgs e)
        {
            UserDataMessageHandler(sender, e, BinanceExchangeType.MarginExchange);
        }

        private void _spotSocketClient_MessageReceived(object sender, MessageEventArgs e)
        {
            UserDataMessageHandler(sender, e, BinanceExchangeType.SpotExchange);
        }

        private void UserDataMessageHandler(object sender, MessageEventArgs e, BinanceExchangeType type)
        {
            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                return;
            }

            BinanceUserMessage message = new BinanceUserMessage();
            message.MessageStr = e.Data;
            message.ExchangeType = type;

            _newMessagePrivate.Enqueue(message);
        }

        private void _publicSocketClient_RessageReceived(object sender, MessageEventArgs e)
        {
            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                return;
            }
            _newMessagePublic.Enqueue(e.Data);
        }

        #endregion

        #region 8 WebSocket check alive

        private void KeepaliveUserDataStream()
        {
            DateTime _timeStart = DateTime.Now;

            while (true)
            {
                try
                {
                    Thread.Sleep(30000);

                    if (_spotListenKey == "" &&
                        _marginListenKey == "")
                    {
                        continue;
                    }

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        _timeStart = DateTime.Now;
                        continue;
                    }

                    if (_timeStart.AddMinutes(5) < DateTime.Now)
                    {
                        _timeStart = DateTime.Now;

                        CreateQuery(BinanceExchangeType.SpotExchange, Method.PUT,
                            "api/v1/userDataStream", new Dictionary<string, string>()
                                { { "listenKey=", _spotListenKey } }, false);

                        if (_notMargineAccount == false)
                        {
                            CreateQuery(BinanceExchangeType.MarginExchange, Method.PUT,
                                "sapi/v1/userDataStream", new Dictionary<string, string>()
                                    { { "listenKey=", _marginListenKey } }, false);
                        }
                    }
                }
                catch (Exception e)
                {
                    SendLogMessage(e.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        #endregion

        #region 9 Security subscribe

        private List<Security> _subscribedSecurities = new List<Security>();

        public void Subscribe(Security security)
        {
            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                return;
            }

            for (int i = 0; i < _subscribedSecurities.Count; i++)
            {
                if (_subscribedSecurities[i].NameClass == security.NameClass
                    && _subscribedSecurities[i].Name == security.Name)
                {
                    return;
                }
            }

            _subscribedSecurities.Add(security);

            string urlStr = null;

            if (((ServerParameterBool)ServerParameters[10]).Value == false)
            {
                urlStr = "wss://stream.binance.com:9443/stream?streams="
                                            + security.Name.ToLower()
                                            + "@depth5/"
                                            + security.Name.ToLower() + "@trade";
            }
            else
            {
                urlStr = "wss://stream.binance.com:9443/stream?streams="
                                            + security.Name.ToLower()
                                            + "@depth20/"
                                            + security.Name.ToLower() + "@trade";
            }

            if (_extendedMarketData)
            {
                urlStr += "/" + security.Name.ToLower() + "@miniTicker";
            }

            WebSocket _wsClient = new WebSocket(urlStr); // create web-socket 

            if (_myProxy != null)
            {
                _wsClient.SetProxy(_myProxy);
            }

            _wsClient.EmitOnPing = true;

            _wsClient.OnMessage += _publicSocketClient_RessageReceived;
            _wsClient.OnError += Client_Error;
            _wsClient.OnClose += Client_Closed;
            _wsClient.ConnectAsync();

            _wsStreamsSecurityData.Add(security.Name, _wsClient);
        }

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent { add { } remove { } }

        #endregion

        #region 10 WebSocket parsing the messages

        public void ConverterPublicData()
        {
            while (true)
            {
                try
                {
                    if (_newMessagePublic.IsEmpty == true)
                    {
                        Thread.Sleep(1);
                    }
                    else
                    {
                        string mes;

                        if (_newMessagePublic.TryDequeue(out mes))
                        {
                            if (mes.Contains("\"lastUpdateId\""))
                            {
                                var quotes = JsonConvert.DeserializeAnonymousType(mes, new DepthResponse());
                                UpdateMarketDepth(quotes);
                            }
                            else if (mes.Contains("\"e\"" + ":" + "\"trade\""))
                            {
                                var quotes = JsonConvert.DeserializeAnonymousType(mes, new TradeResponse());
                                UpdateTrades(quotes);
                            }
                            else if (mes.Contains("\"e\":\"24hrMiniTicker\"")) // 24hr rolling window mini-ticker statistics
                            {
                                var quotes = JsonConvert.DeserializeAnonymousType(mes, new MiniTickerResponse());
                                UpdateVolume24h(quotes);
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
                    if (_newMessagePrivate.IsEmpty == true)
                    {
                        Thread.Sleep(1);
                    }
                    else
                    {
                        BinanceUserMessage messsage;

                        if (_newMessagePrivate.TryDequeue(out messsage))
                        {
                            string mes = messsage.MessageStr;

                            if (mes.Contains("code"))
                            {
                                SendLogMessage(JsonConvert.DeserializeAnonymousType(mes, new ErrorMessage()).msg, LogMessageType.Error);
                            }

                            else if (mes.Contains("\"e\"" + ":" + "\"executionReport\""))
                            {
                                UpdateOrderAndMyTrade(mes);
                            }
                            else if (mes.Contains("\"e\"" + ":" + "\"outboundAccountPosition\""))
                            {
                                var portfolios = JsonConvert.DeserializeAnonymousType(mes, new OutboundAccountInfo());

                                if (messsage.ExchangeType == BinanceExchangeType.SpotExchange)
                                {
                                    UpdatePortfolio(portfolios, BinanceExchangeType.SpotExchange);
                                }
                                if (messsage.ExchangeType == BinanceExchangeType.MarginExchange)
                                {
                                    UpdatePortfolio(portfolios, BinanceExchangeType.MarginExchange);
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

        private void UpdatePortfolio(OutboundAccountInfo portfs, BinanceExchangeType source)
        {
            try
            {
                if (portfs == null)
                {
                    return;
                }

                if (_portfolios == null)
                {
                    return;
                }

                Portfolio portfolio = null;

                if (source == BinanceExchangeType.SpotExchange)
                {
                    portfolio = _portfolios.Find(p => p.Number == "BinanceSpot");
                }
                else if (source == BinanceExchangeType.MarginExchange)
                {
                    portfolio = _portfolios.Find(p => p.Number == "BinanceMargin");
                }

                if (portfolio == null)
                {
                    return;
                }

                foreach (var onePortf in portfs.B)
                {
                    if (onePortf == null ||
                        onePortf.f == null ||
                        onePortf.l == null)
                    {
                        continue;
                    }

                    PositionOnBoard neeedPortf =
                        portfolio.GetPositionOnBoard().Find(p => p.SecurityNameCode == onePortf.a);

                    if (neeedPortf == null)
                    {
                        PositionOnBoard newPos = new PositionOnBoard();
                        newPos.PortfolioName = portfolio.Number;
                        newPos.SecurityNameCode = onePortf.a;
                        newPos.ValueBegin = onePortf.f.ToDecimal();
                        newPos.ValueCurrent = onePortf.f.ToDecimal();
                        portfolio.SetNewPosition(newPos);

                        continue;
                    }

                    neeedPortf.ValueCurrent =
                        onePortf.f.ToDecimal();
                    neeedPortf.ValueBlocked =
                        onePortf.l.ToDecimal();
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

        private void UpdateOrderAndMyTrade(string mes)
        {
            var order = JsonConvert.DeserializeAnonymousType(mes, new ExecutionReport());

            string orderNumUserInString = order.C.Replace("x-RKXTQ2AK", "");

            if (string.IsNullOrEmpty(orderNumUserInString) ||
                orderNumUserInString == "null")
            {
                orderNumUserInString = order.c.Replace("x-RKXTQ2AK", "");
            }

            int orderNumUser = 0;

            try
            {
                orderNumUser = Convert.ToInt32(orderNumUserInString);
            }
            catch
            {
                // ignore
            }

            if (order.x == "NEW")
            {
                Order newOrder = new Order();
                newOrder.SecurityNameCode = order.s;
                newOrder.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(order.E.ToDouble());
                newOrder.NumberUser = orderNumUser;

                newOrder.NumberMarket = order.i.ToString();
                //newOrder.PortfolioNumber = order.PortfolioNumber; add to server
                newOrder.Side = order.S == "BUY" ? Side.Buy : Side.Sell;
                newOrder.State = OrderStateType.Active;
                newOrder.Volume = order.q.ToDecimal();
                newOrder.Price = order.p.ToDecimal();
                newOrder.ServerType = ServerType.Binance;
                newOrder.PortfolioNumber = "Binance";

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
                SetOrderOnBoard(newOrder);
            }
            else if (order.x == "CANCELED")
            {
                Order newOrder = new Order();
                newOrder.SecurityNameCode = order.s;
                newOrder.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(order.E.ToDouble());
                newOrder.TimeCancel = newOrder.TimeCallBack;
                newOrder.NumberUser = orderNumUser;
                newOrder.NumberMarket = order.i.ToString();
                newOrder.Side = order.S == "BUY" ? Side.Buy : Side.Sell;
                newOrder.State = OrderStateType.Cancel;
                newOrder.Volume = order.q.ToDecimal();
                newOrder.Price = order.p.ToDecimal();
                newOrder.ServerType = ServerType.Binance;
                newOrder.PortfolioNumber = "Binance";

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
                newOrder.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(order.E.ToDouble());
                newOrder.NumberUser = orderNumUser;
                newOrder.NumberMarket = order.i.ToString();
                newOrder.Side = order.S == "BUY" ? Side.Buy : Side.Sell;
                newOrder.State = OrderStateType.Fail;
                newOrder.Volume = order.q.ToDecimal();
                newOrder.Price = order.p.ToDecimal();
                newOrder.ServerType = ServerType.Binance;
                newOrder.PortfolioNumber = "Binance";

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

                SendLogMessage("Binance spot order fail. Order num: " + newOrder.NumberUser +
                    " \n Reasons in order request: " + order.r, LogMessageType.Error);
            }
            else if (order.x == "TRADE")
            {
                Order oldOrder = GetOrderFromBoard(order.i.ToLower());

                if (oldOrder != null &&
                    order.z != null &&
                    oldOrder.Volume == order.z.ToString().ToDecimal())
                {// Order Done
                    Order newOrder = new Order();
                    newOrder.SecurityNameCode = order.s;
                    newOrder.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(order.E.ToDouble());
                    newOrder.NumberUser = orderNumUser;

                    newOrder.NumberMarket = order.i.ToString();
                    //newOrder.PortfolioNumber = order.PortfolioNumber; 
                    newOrder.Side = order.S == "BUY" ? Side.Buy : Side.Sell;
                    newOrder.State = OrderStateType.Done;
                    newOrder.Volume = order.q.ToDecimal();
                    newOrder.Price = order.p.ToDecimal();
                    newOrder.ServerType = ServerType.Binance;
                    newOrder.PortfolioNumber = "Binance";

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

                MyTrade trade = new MyTrade();
                trade.Time = new DateTime(1970, 1, 1).AddMilliseconds(order.T.ToDouble());
                trade.NumberOrderParent = order.i.ToString();
                trade.NumberTrade = order.t.ToString();
                trade.Price = order.L.ToDecimal();
                trade.SecurityNameCode = order.s;
                trade.Side = order.S == "BUY" ? Side.Buy : Side.Sell;

                if (string.IsNullOrEmpty(order.n)
                    || order.n.ToDecimal() == 0)
                {// there is no commission. just put it in trade
                    trade.Volume = order.l.ToDecimal();
                }
                else
                {
                    if (order.N != null &&
                        string.IsNullOrEmpty(order.N.ToString()) == false)
                    {// the commission is taken in some coin
                        string commissionSecName = order.N.ToString();

                        if (trade.SecurityNameCode.StartsWith("BNB")
                            || trade.SecurityNameCode.StartsWith(commissionSecName))
                        {
                            trade.Volume = order.l.ToDecimal() - order.n.ToDecimal();

                            int decimalVolum = GetDecimalsVolume(trade.SecurityNameCode);
                            if (decimalVolum > 0)
                            {
                                trade.Volume = Math.Floor(trade.Volume * (decimal)Math.Pow(10, decimalVolum)) / (decimal)Math.Pow(10, decimalVolum);
                            }
                        }
                        else
                        {
                            trade.Volume = order.l.ToDecimal();
                        }
                    }
                    else
                    {// unknown coin commission. We take the entire volume
                        trade.Volume = order.l.ToDecimal();
                    }
                }

                if (MyTradeEvent != null)
                {
                    MyTradeEvent(trade);
                }
            }
            else if (order.x == "EXPIRED")
            {
                Order newOrder = new Order();
                newOrder.SecurityNameCode = order.s;
                newOrder.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(order.E.ToDouble());
                newOrder.TimeCancel = newOrder.TimeCallBack;
                newOrder.NumberUser = orderNumUser;
                newOrder.NumberMarket = order.i.ToString();
                newOrder.Side = order.S == "BUY" ? Side.Buy : Side.Sell;
                newOrder.State = OrderStateType.Cancel;
                newOrder.Volume = order.q.ToDecimal();
                newOrder.Price = order.p.ToDecimal();
                newOrder.ServerType = ServerType.Binance;
                newOrder.PortfolioNumber = "Binance";

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
            else if (order.x == "FILLED")
            {
                Order newOrder = new Order();
                newOrder.SecurityNameCode = order.s;
                newOrder.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(order.E.ToDouble());
                newOrder.NumberUser = orderNumUser;
                newOrder.NumberMarket = order.i.ToString();
                newOrder.Side = order.S == "BUY" ? Side.Buy : Side.Sell;
                newOrder.State = OrderStateType.Done;
                newOrder.Volume = order.q.ToDecimal();
                newOrder.Price = order.p.ToDecimal();
                newOrder.ServerType = ServerType.Binance;
                newOrder.PortfolioNumber = "Binance";

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

        private int GetDecimalsVolume(string security)
        {
            for (int i = 0; i < _securities.Count; i++)
            {
                if (security == _securities[i].Name)
                {
                    return _securities[i].DecimalsVolume;
                }
            }

            return 0;
        }

        private List<Order> _ordersOnBoard = new List<Order>();

        private void SetOrderOnBoard(Order order)
        {
            _ordersOnBoard.Add(order);

            while (_ordersOnBoard.Count > 200)
            {
                _ordersOnBoard.RemoveAt(0);
            }
        }

        private Order GetOrderFromBoard(string orderNum)
        {
            for (int i = 0; i < _ordersOnBoard.Count; i++)
            {
                if (_ordersOnBoard[i].NumberMarket == orderNum)
                {
                    return _ordersOnBoard[i];
                }
            }
            return null;
        }

        private readonly object _newTradesLoker = new object();

        private void UpdateTrades(TradeResponse trades)
        {
            lock (_newTradesLoker)
            {
                if (trades.data == null)
                {
                    return;
                }
                Trade trade = new Trade();
                trade.SecurityNameCode = trades.data.s;
                trade.Price =
                        trades.data.p.ToDecimal();
                trade.Id = trades.data.t.ToString();
                trade.Time = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(trades.data.T));
                trade.Volume =
                        trades.data.q.ToDecimal();
                trade.Side = trades.data.m == true ? Side.Sell : Side.Buy;

                NewTradesEvent?.Invoke(trade);
            }
        }

        private List<MarketDepth> _depths = new List<MarketDepth>();

        private void UpdateMarketDepth(DepthResponse myDepth)
        {
            try
            {

                if (myDepth.data.asks == null || myDepth.data.asks.Count == 0 ||
                    myDepth.data.bids == null || myDepth.data.bids.Count == 0)
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

                for (int i = 0; i < myDepth.data.asks.Count; i++)
                {
                    ascs.Add(new MarketDepthLevel()
                    {
                        Ask =
                            myDepth.data.asks[i][1].ToString().ToDouble()
                        ,
                        Price =
                            myDepth.data.asks[i][0].ToString().ToDouble()

                    });
                }

                for (int i = 0; i < myDepth.data.bids.Count; i++)
                {
                    bids.Add(new MarketDepthLevel()
                    {
                        Bid =
                            myDepth.data.bids[i][1].ToString().ToDouble()
                        ,
                        Price =
                            myDepth.data.bids[i][0].ToString().ToDouble()
                    });
                }

                needDepth.Asks = ascs;
                needDepth.Bids = bids;
                needDepth.Time = ServerTime;

                if (needDepth.Time == DateTime.MinValue)
                {
                    return;
                }

                if (needDepth.Time == _lastTimeMd)
                {
                    _lastTimeMd = _lastTimeMd.AddMilliseconds(1);
                    needDepth.Time = _lastTimeMd;
                }

                _lastTimeMd = needDepth.Time;

                if (MarketDepthEvent != null)
                {
                    if (_newMessagePublic.Count < 1000)
                    {
                        MarketDepthEvent(needDepth.GetCopy());
                    }
                    else
                    {
                        MarketDepthEvent(needDepth);
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateVolume24h(MiniTickerResponse ticker)
        {
            try
            {
                if (ticker == null)
                {
                    return;
                }

                SecurityVolumes volume = new SecurityVolumes();

                volume.SecurityNameCode = ticker.data.s;
                volume.Volume24h = ticker.data.v.ToDecimal();
                volume.Volume24hUSDT = ticker.data.q.ToDecimal();
                volume.TimeUpdate = TimeManager.GetDateTimeFromTimeStamp((long)ticker.data.E.ToDecimal());

                Volume24hUpdateEvent?.Invoke(volume);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private DateTime _lastTimeMd = DateTime.MinValue;

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent;

        #endregion

        #region 11 Trade

        public void SendOrder(Order order)
        {
            if (order.PortfolioNumber == "BinanceSpot")
            {
                ExecuteOrderOnSpotExchange(order);
            }
            else if (order.PortfolioNumber == "BinanceMargin")
            {
                ExecuteOrderOnMarginExchange(order);
            }
        }

        private string _lockOrder = "lockOrder";

        private void ExecuteOrderOnMarginExchange(Order order)
        {
            lock (_lockOrder)
            {
                try
                {
                    string TypeOrder = order.TypeOrder == OrderPriceType.Market ? "MARKET" : "LIMIT";

                    Dictionary<string, string> param = new Dictionary<string, string>();

                    param.Add("symbol=", order.SecurityNameCode.ToUpper());
                    param.Add("&side=", order.Side == Side.Buy ? "BUY" : "SELL");
                    param.Add("&type=", TypeOrder);
                    if (TypeOrder.Equals("LIMIT"))
                    {
                        param.Add("&timeInForce=", "GTC");
                    }
                    param.Add("&newClientOrderId=", "x-RKXTQ2AK" + order.NumberUser.ToString());

                    if (order.PositionConditionType == OrderPositionConditionType.Open)
                    {
                        param.Add("&sideEffectType=", "MARGIN_BUY");
                    }
                    else
                    {
                        param.Add("&sideEffectType=", "AUTO_REPAY");
                    }

                    param.Add("&quantity=",
                        order.Volume.ToString(CultureInfo.InvariantCulture)
                            .Replace(CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator, "."));
                    if (TypeOrder.Equals("LIMIT"))
                    {
                        param.Add("&price=",
                        order.Price.ToString(CultureInfo.InvariantCulture)
                            .Replace(CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator, "."));
                    }

                    var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.POST, "/sapi/v1/margin/order", param, true);

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
        }

        private void ExecuteOrderOnSpotExchange(Order order)
        {
            lock (_lockOrder)
            {
                try
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();

                    string TypeOrder = order.TypeOrder == OrderPriceType.Market ? "MARKET" : "LIMIT";

                    param.Add("symbol=", order.SecurityNameCode.ToUpper());
                    param.Add("&side=", order.Side == Side.Buy ? "BUY" : "SELL");
                    param.Add("&type=", TypeOrder);
                    if (TypeOrder.Equals("LIMIT"))
                    {
                        param.Add("&timeInForce=", "GTC");
                    }
                    param.Add("&newClientOrderId=", "x-RKXTQ2AK" + order.NumberUser.ToString());
                    param.Add("&quantity=",
                        order.Volume.ToString(CultureInfo.InvariantCulture)
                            .Replace(CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator, "."));
                    if (TypeOrder.Equals("LIMIT"))
                    {
                        param.Add("&price=",
                      order.Price.ToString(CultureInfo.InvariantCulture)
                          .Replace(CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator, "."));
                    }

                    var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.POST, "api/v3/order", param, true);

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
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {

        }

        public bool CancelOrder(Order order)
        {
            lock (_lockOrder)
            {
                try
                {
                    if (string.IsNullOrEmpty(order.NumberMarket))
                    {
                        Order onBoard = GetOrderState(order);

                        if (onBoard == null)
                        {
                            order.State = OrderStateType.Fail;
                            SendLogMessage("When revoking an order, we didn't find it on the exchange. We think it's already been revoked.",
                                LogMessageType.Error);
                            if (MyOrderEvent != null)
                            {
                                MyOrderEvent(order);
                            }
                            return true;
                        }
                        else if (onBoard.State == OrderStateType.Cancel)
                        {
                            order.TimeCancel = onBoard.TimeCallBack;
                            order.State = OrderStateType.Cancel;
                            if (MyOrderEvent != null)
                            {
                                MyOrderEvent(order);
                            }
                            return true;
                        }
                        else if (onBoard.State == OrderStateType.Fail)
                        {
                            order.State = OrderStateType.Fail;

                            if (MyOrderEvent != null)
                            {
                                MyOrderEvent(order);
                            }
                            return true;
                        }

                        order.NumberMarket = onBoard.NumberMarket;
                        order = onBoard;
                    }

                    Dictionary<string, string> param = new Dictionary<string, string>();

                    param.Add("symbol=", order.SecurityNameCode.ToUpper());
                    param.Add("&orderId=", order.NumberMarket);

                    if (order.PortfolioNumber == "BinanceSpot")
                    {
                        CreateQuery(BinanceExchangeType.SpotExchange, Method.DELETE, "api/v3/order", param, true);
                    }
                    else if (order.PortfolioNumber == "BinanceMargin")
                    {
                        CreateQuery(BinanceExchangeType.SpotExchange, Method.DELETE, "/sapi/v1/margin/order", param, true);
                    }
                    else
                    {
                        CreateQuery(BinanceExchangeType.SpotExchange, Method.DELETE, "api/v3/order", param, true);
                        CreateQuery(BinanceExchangeType.SpotExchange, Method.DELETE, "/sapi/v1/margin/order", param, true);
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                }
                return true;
            }
        }

        private Order GetOrderState(Order oldOrder)
        {
            List<string> namesSec = new List<string>();
            namesSec.Add(oldOrder.SecurityNameCode);

            string endPoint = "/api/v3/allOrders";

            List<HistoryOrderReport> allOrders = new List<HistoryOrderReport>();

            try
            {
                for (int i = 0; i < namesSec.Count; i++)
                {
                    var param = new Dictionary<string, string>();
                    param.Add("symbol=", namesSec[i].ToUpper());
                    //param.Add("&recvWindow=" , "100");
                    //param.Add("&limit=", GetNonce());
                    param.Add("&limit=", "500");
                    //"symbol={symbol.ToUpper()}&recvWindow={recvWindow}"

                    var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, endPoint, param, true);

                    if (res == null)
                    {
                        continue;
                    }

                    HistoryOrderReport[] orders = JsonConvert.DeserializeObject<HistoryOrderReport[]>(res);

                    if (orders != null && orders.Length != 0)
                    {
                        allOrders.AddRange(orders);
                    }
                }

                HistoryOrderReport orderOnBoard =
                    allOrders.Find(ord => ord.clientOrderId.Replace("x-RKXTQ2AK", "") == oldOrder.NumberUser.ToString());

                if (orderOnBoard == null)
                {
                    return null;
                }

                Order newOrder = new Order();
                newOrder.NumberMarket = orderOnBoard.orderId;
                newOrder.NumberUser = oldOrder.NumberUser;
                newOrder.SecurityNameCode = oldOrder.SecurityNameCode;
                //newOrder.State = OrderStateType.Cancel;

                newOrder.Volume = oldOrder.Volume;
                newOrder.VolumeExecute = oldOrder.VolumeExecute;
                newOrder.Price = oldOrder.Price;
                newOrder.TypeOrder = oldOrder.TypeOrder;
                newOrder.TimeCallBack = oldOrder.TimeCallBack;
                newOrder.TimeCancel = newOrder.TimeCallBack;
                newOrder.ServerType = ServerType.Binance;
                newOrder.PortfolioNumber = oldOrder.PortfolioNumber;

                if (orderOnBoard.status == "NEW" ||
                    orderOnBoard.status == "PARTIALLY_FILLED")
                { // order is active. Do nothing
                    newOrder.State = OrderStateType.Active;
                }
                else if (orderOnBoard.status == "FILLED")
                {
                    newOrder.State = OrderStateType.Done;
                }
                else
                {
                    newOrder.State = OrderStateType.Cancel;
                }

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(newOrder);
                }

                return newOrder;
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.Message, LogMessageType.Error);
                return null;
            }
        }

        public void CancelAllOrders()
        {
            List<Order> openOrders = GetAllActivOrdersArray(100);

            if (openOrders == null)
            {
                return;
            }

            for (int i = 0; i < openOrders.Count; i++)
            {
                CancelOrder(openOrders[i]);
            }
        }

        public void GetAllActivOrders()
        {
            List<Order> openOrders = GetAllActivOrdersArray(100);

            if (openOrders == null)
            {
                return;
            }

            for (int i = 0; i < openOrders.Count; i++)
            {
                if (MyOrderEvent != null)
                {
                    MyOrderEvent(openOrders[i]);
                }
            }
        }

        private List<Order> GetAllActivOrdersArray(int maxCountByCategory)
        {
            List<Order> ordersOpenAll = new List<Order>();

            List<Order> orders = new List<Order>();

            GetAllOpenOrders(orders, 100);

            if (orders != null
                && orders.Count > 0)
            {
                ordersOpenAll.AddRange(orders);
            }

            return ordersOpenAll;
        }

        public OrderStateType GetOrderStatus(Order order)
        {
            List<Order> allSecurityOrders = GetAllOrdersToSecurity(order.SecurityNameCode);

            if (allSecurityOrders == null)
            {
                return OrderStateType.None;
            }

            Order myOrderActualOnBoard = null;

            for (int i = 0; i < allSecurityOrders.Count; i++)
            {
                Order curOrder = allSecurityOrders[i];

                if (curOrder.NumberUser != 0 &&
                    order.NumberUser != 0 &&
                    curOrder.NumberUser == order.NumberUser)
                {
                    myOrderActualOnBoard = curOrder;
                    break;
                }

                if (string.IsNullOrEmpty(curOrder.NumberMarket) == false &&
                    string.IsNullOrEmpty(order.NumberMarket) == false
                    && curOrder.NumberMarket == order.NumberMarket)
                {
                    myOrderActualOnBoard = curOrder;
                    break;
                }
            }

            if (myOrderActualOnBoard == null)
            {
                return OrderStateType.None;
            }

            if (MyOrderEvent != null)
            {
                MyOrderEvent(myOrderActualOnBoard);
            }

            if (myOrderActualOnBoard.State == OrderStateType.Done ||
                myOrderActualOnBoard.State == OrderStateType.Partial)
            { // запрашиваем MyTrades, если по ордеру были исполнения

                List<MyTrade> tradesSpot = GetAllMyTradesToOrder(myOrderActualOnBoard);

                if (tradesSpot != null)
                {
                    for (int i = 0; i < tradesSpot.Count; i++)
                    {
                        if (MyTradeEvent != null)
                        {
                            MyTradeEvent(tradesSpot[i]);
                        }
                    }
                }
            }

            return myOrderActualOnBoard.State;
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            try
            {
                Dictionary<string, string> param = new Dictionary<string, string>();

                param.Add("symbol=", security.Name.ToUpper());

                CreateQuery(BinanceExchangeType.SpotExchange, Method.DELETE, "api/v3/openOrders", param, true);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        public List<MyTrade> GetAllMyTradesToOrder(Order order)
        {
            try
            {
                string endPoint = "/api/v3/myTrades";
                var param = new Dictionary<string, string>();
                param.Add("symbol=", order.SecurityNameCode.ToUpper());
                //param.Add("orderId=", order.NumberMarket);
                param.Add("&limit=", "500");
                var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, endPoint, param, true);

                if (res == null)
                {
                    return null;
                }

                HistoryMyTradeReport[] myTrades = JsonConvert.DeserializeObject<HistoryMyTradeReport[]>(res);

                List<MyTrade> trades = new List<MyTrade>();

                for (int i = 0; i < myTrades.Length; i++)
                {
                    if (myTrades[i].orderId != order.NumberMarket)
                    {
                        continue;
                    }

                    MyTrade newTrade = new MyTrade();
                    newTrade.SecurityNameCode = myTrades[i].symbol;
                    newTrade.NumberTrade = myTrades[i].id;
                    newTrade.NumberOrderParent = myTrades[i].orderId;
                    newTrade.Volume = myTrades[i].qty.ToDecimal();
                    newTrade.Price = myTrades[i].price.ToDecimal();
                    newTrade.Time = new DateTime(1970, 1, 1).AddMilliseconds(myTrades[i].time.ToDouble());
                    newTrade.Side = order.Side;
                    trades.Add(newTrade);
                }

                return trades;
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                return null;
            }
        }

        public List<Order> GetAllOrdersToSecurity(string securityName)
        {
            try
            {
                string endPoint = "/api/v3/allOrders";
                var param = new Dictionary<string, string>();
                param.Add("symbol=", securityName.ToUpper());
                param.Add("&limit=", "500");
                var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, endPoint, param, true);

                if (res == null)
                {
                    return null;
                }

                HistoryOrderReport[] orders = JsonConvert.DeserializeObject<HistoryOrderReport[]>(res);

                if (orders == null)
                {
                    return null;
                }

                List<Order> result = new List<Order>();

                for (int i = 0; i < orders.Length; i++)
                {
                    HistoryOrderReport myOrder = orders[i];

                    Order newOrder = new Order();
                    newOrder.NumberMarket = orders[i].orderId;

                    if (orders[i].clientOrderId != null)
                    {
                        string id = orders[i].clientOrderId.Replace("x-RKXTQ2AK", "");
                        try
                        {
                            newOrder.NumberUser = Convert.ToInt32(id);
                        }
                        catch
                        {
                            // ignore
                        }
                    }

                    newOrder.SecurityNameCode = orders[i].symbol;
                    newOrder.Price = orders[i].price.ToDecimal();
                    newOrder.Volume = orders[i].origQty.ToDecimal();
                    newOrder.ServerType = ServerType.Binance;
                    newOrder.PortfolioNumber = "BinanceSpot";

                    if (orders[i].side == "BUY")
                    {
                        newOrder.Side = Side.Buy;
                    }
                    else
                    {
                        newOrder.Side = Side.Sell;
                    }

                    if (orders[i].type == "MARKET")
                    {
                        newOrder.TypeOrder = OrderPriceType.Market;
                    }
                    else
                    {
                        newOrder.TypeOrder = OrderPriceType.Limit;
                    }

                    newOrder.TimeCreate = new DateTime(1970, 1, 1).AddMilliseconds(orders[i].time.ToDouble());
                    newOrder.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(orders[i].updateTime.ToDouble());

                    if (myOrder.status == "NEW")
                    {
                        newOrder.State = OrderStateType.Active;
                    }
                    else if (myOrder.status == "FILLED")
                    {
                        newOrder.State = OrderStateType.Done;
                        newOrder.TimeDone = newOrder.TimeCallBack;
                    }
                    else if (myOrder.status == "PARTIALLY_FILLED")
                    {
                        newOrder.State = OrderStateType.Partial;
                    }
                    else if (myOrder.status == "CANCEL"
                        || myOrder.status == "CANCELED"
                        || myOrder.status == "EXPIRED")
                    {
                        newOrder.State = OrderStateType.Cancel;
                        newOrder.TimeCancel = newOrder.TimeCallBack;
                    }
                    else if (myOrder.status == "REJECTED")
                    {
                        newOrder.State = OrderStateType.Fail;
                    }
                    else
                    {

                    }

                    result.Add(newOrder);
                }

                return result;
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private void GetAllOpenOrders(List<Order> array, int maxCount)
        {
            try
            {
                List<Order> openOrders = new List<Order>();

                string endPoint = "/api/v3/openOrders";

                var param = new Dictionary<string, string>();

                var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, endPoint, param, true);

                if (res == null)
                {
                    return;
                }

                HistoryOrderReport[] orders = JsonConvert.DeserializeObject<HistoryOrderReport[]>(res);

                if (orders == null)
                {
                    return;
                }

                for (int i = 0; i < orders.Length; i++)
                {
                    Order newOrder = new Order();
                    newOrder.NumberMarket = orders[i].orderId;

                    if (orders[i].clientOrderId != null)
                    {
                        string id = orders[i].clientOrderId.Replace("x-RKXTQ2AK", "");
                        try
                        {
                            newOrder.NumberUser = Convert.ToInt32(id);
                        }
                        catch
                        {
                            // ignore
                        }
                    }

                    newOrder.SecurityNameCode = orders[i].symbol;
                    newOrder.State = OrderStateType.Active;
                    newOrder.Price = orders[i].price.ToDecimal();
                    newOrder.Volume = orders[i].origQty.ToDecimal();
                    newOrder.ServerType = ServerType.Binance;
                    newOrder.PortfolioNumber = "BinanceSpot";

                    if (orders[i].side == "BUY")
                    {
                        newOrder.Side = Side.Buy;
                    }
                    else
                    {
                        newOrder.Side = Side.Sell;
                    }

                    newOrder.TimeCreate = new DateTime(1970, 1, 1).AddMilliseconds(orders[i].time.ToDouble());
                    newOrder.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(orders[i].updateTime.ToDouble());

                    try
                    {
                        newOrder.Volume = orders[i].origQty.ToDecimal();
                    }
                    catch
                    {
                        // ignore
                    }

                    openOrders.Add(newOrder);
                }

                if (openOrders.Count > 0)
                {
                    array.AddRange(openOrders);

                    if (array.Count > maxCount)
                    {
                        while (array.Count > maxCount)
                        {
                            array.RemoveAt(array.Count - 1);
                        }
                        return;
                    }
                    else if (array.Count < 80)
                    {
                        return;
                    }
                }
                else
                {
                    return;
                }

                return;
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                return;
            }
        }

        public List<Order> GetActiveOrders(int startIndex, int count)
        {
            int countToMethod = startIndex + count;

            List<Order> result = GetAllActivOrdersArray(countToMethod);

            List<Order> resultExit = new List<Order>();

            if (result != null
                && startIndex < result.Count)
            {
                if (startIndex + count < result.Count)
                {
                    resultExit = result.GetRange(startIndex, count);
                }
                else
                {
                    resultExit = result.GetRange(startIndex, result.Count - startIndex);
                }
            }

            return resultExit;
        }

        public List<Order> GetHistoricalOrders(int startIndex, int count)
        {
            return null;
        }

        #endregion

        #region 12 Queries

        private object _queryHttpLocker = new object();

        private RateGate _rateGate = new RateGate(1, TimeSpan.FromMilliseconds(100));

        public string CreateQuery(BinanceExchangeType startUri, Method method, string endpoint, Dictionary<string, string> param = null, bool auth = false)
        {
            try
            {
                lock (_queryHttpLocker)
                {
                    _rateGate.WaitToProceed();
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

                    string baseUrl = "";

                    if (startUri == BinanceExchangeType.SpotExchange)
                    {
                        baseUrl = "https://api.binance.com";
                    }
                    else if (startUri == BinanceExchangeType.MarginExchange)
                    {
                        baseUrl = "https://api.binance.com";
                    }

                    RestClient client = new RestClient(baseUrl);

                    if (_myProxy != null)
                    {
                        client.Proxy = _myProxy;
                    }

                    var response = client.Execute(request).Content;

                    if (response.Contains("code"))
                    {
                        var error = JsonConvert.DeserializeAnonymousType(response, new ErrorMessage());
                        throw new Exception(error.msg);
                    }

                    return response;
                }
            }
            catch (Exception ex)
            {
                if (ex.ToString().Contains("This listenKey does not exist"))
                {

                }
                if (ex.ToString().Contains("Unknown order sent"))
                {
                    //SendLogMessage(ex.ToString(), LogMessageType.System);
                    return null;
                }

                SendLogMessage(ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private string GetNonce()
        {
            var resTime = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, "api/v1/time", null, false);
            var result = JsonConvert.DeserializeAnonymousType(resTime, new BinanceTime());

            if (result != null)
            {
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

    public enum BinanceExchangeType
    {
        SpotExchange,
        MarginExchange
    }

    public class BinanceUserMessage
    {
        public string MessageStr;

        public BinanceExchangeType ExchangeType;
    }
}