/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json;
using NSec.Cryptography;
using OsEngine.Entity;
using OsEngine.Entity.WebSocketOsEngine;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Binance.Spot.BinanceSpotEntity;
using OsEngine.Market.Servers.Entity;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
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

            Thread worker4 = new Thread(ConverterPublicDataMarketDepth);
            worker4.Name = "BinanceSpotThread_ConverterUserDataMarketDepth";
            worker4.Start();
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

            try
            {
                RestRequest requestRest = new RestRequest("/v1/time", Method.GET);
                RestClient client = new RestClient(_baseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage("Can`t run Binance Spot connector. No internet connection", LogMessageType.Error);
                    Disconnect();
                    return;
                }
            }
            catch
            {
                SendLogMessage("Can`t run Binance Spot connector. No internet connection", LogMessageType.Error);
                Disconnect();
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
            try
            {
                DisposeSockets();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            _subscribedSecurities.Clear();
            _newMessagePrivate = new ConcurrentQueue<BinanceUserMessage>();
            _newMessagePublic = new ConcurrentQueue<string>();
            _newMessagePublicMarketDepth = new ConcurrentQueue<string>();

            Disconnect();
        }

        public void Disconnect()
        {
            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
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

        public bool IsCompletelyDeleted { get; set; }

        #endregion

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        public string ApiKey;

        public string SecretKey;

        private string _baseUrl = "https://api.binance.com/api";

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

        private Dictionary<string, Security> _securitiesDict = new Dictionary<string, Security>();

        private void UpdatePairs(SecurityResponce pairs)
        {
            if (_securitiesDict == null)
            {
                _securitiesDict = new Dictionary<string, Security>();
            }

            List<Security> securities = new List<Security>();

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
                securities.Add(security);
            }

            if (securities.Count > 0)
            {
                securities = securities.OrderBy(s => s.Name).ToList();
            }

            foreach (Security sec in securities)
            {
                _securitiesDict[sec.Name] = sec;
            }

            if (SecurityEvent != null)
            {
                SecurityEvent(securities);
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
            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;
            DateTime endTime = DateTime.UtcNow;
            DateTime startTime = endTime.AddMinutes(-tfTotalMinutes * candleCount);

            return GetCandleDataToSecurity(security, timeFrameBuilder, startTime, endTime, startTime);
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
                param.Add("symbol=" + nameSec.ToUpper(), "&interval=" + needTf + "&startTime=" + startTime + "&endTime=" + endTime + "&limit=1000");

                var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, endPoint, param, false);
                if (string.IsNullOrEmpty(res))
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

                    if (string.IsNullOrEmpty(res))
                    {
                        return null;
                    }

                    var candles = _deserializeCandles(res);

                    var newCandles = BuildCandles(candles, 2, 1);
                    return newCandles;
                }
                else if (needTf == "10m")
                {
                    var param = new Dictionary<string, string>();
                    param.Add("symbol=" + nameSec.ToUpper(), "&interval=5m" + "&startTime=" + startTime + "&endTime=" + endTime);
                    var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, endPoint, param, false);

                    if (string.IsNullOrEmpty(res))
                    {
                        return null;
                    }

                    var candles = _deserializeCandles(res);
                    var newCandles = BuildCandles(candles, 10, 5);
                    return newCandles;
                }
                else if (needTf == "20m")
                {
                    var param = new Dictionary<string, string>();
                    param.Add("symbol=" + nameSec.ToUpper(), "&interval=5m" + "&startTime=" + startTime + "&endTime=" + endTime);
                    var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, endPoint, param, false);

                    if (string.IsNullOrEmpty(res))
                    {
                        return null;
                    }

                    var candles = _deserializeCandles(res);
                    var newCandles = BuildCandles(candles, 20, 5);
                    return newCandles;
                }
                else if (needTf == "45m")
                {
                    var param = new Dictionary<string, string>();
                    param.Add("symbol=" + nameSec.ToUpper(), "&interval=15m" + "&startTime=" + startTime + "&endTime=" + endTime);
                    var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, endPoint, param, false);

                    if (string.IsNullOrEmpty(res))
                    {
                        return null;
                    }

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

        private string _webSocketUrlPrivate = "wss://ws-api.binance.com:443/ws-api/v3";

        private RateGate _rateGateCreateDisposeSockets = new RateGate(1, TimeSpan.FromSeconds(2));

        private void CreateDataStreams()
        {
            try
            {
                if (_spotSocketClient != null)
                {
                    return;
                }

                _spotSocketClient = new WebSocket(_webSocketUrlPrivate);

                if (_myProxy != null)
                {
                    _spotSocketClient.SetProxy(_myProxy);
                }

                _spotSocketClient.EmitOnPing = true;
                _spotSocketClient.OnOpen += _spotSocketClient_OnOpen;
                _spotSocketClient.OnError += _spotSocketClient_OnError;
                _spotSocketClient.OnMessage += _spotSocketClient_MessageReceived;
                _spotSocketClient.OnClose += _spotSocketClient_OnClose;
                _spotSocketClient.ConnectAsync();

                if (_marginSocketClient != null)
                {
                    return;
                }

                _marginSocketClient = new WebSocket(_webSocketUrlPrivate);

                if (_myProxy != null)
                {
                    _marginSocketClient.SetProxy(_myProxy);
                }

                _marginSocketClient.EmitOnPing = true;
                _marginSocketClient.OnOpen += _marginSocketClient_OnOpen;
                _marginSocketClient.OnError += _marginSocketClient_OnError;
                _marginSocketClient.OnMessage += _marginSocketClient_MessageReceived;
                _marginSocketClient.OnClose += _marginSocketClient_OnClose;
                _marginSocketClient.ConnectAsync();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void DisposeSockets()
        {
            try
            {
                _rateGateCreateDisposeSockets.WaitToProceed();

                if (_spotSocketClient != null)
                {
                    _spotSocketClient.OnOpen -= _spotSocketClient_OnOpen;
                    _spotSocketClient.OnError -= _spotSocketClient_OnError;
                    _spotSocketClient.OnMessage -= _spotSocketClient_MessageReceived;
                    _spotSocketClient.OnClose -= _spotSocketClient_OnClose;
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
                    _marginSocketClient.OnOpen -= _marginSocketClient_OnOpen;
                    _marginSocketClient.OnError -= _marginSocketClient_OnError;
                    _marginSocketClient.OnMessage -= _marginSocketClient_MessageReceived;
                    _marginSocketClient.OnClose -= _marginSocketClient_OnClose;
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
                        ws.Value.OnMessage -= _publicSocketClient_MessageReceived;
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

        private string _socketActivateLocker = "socketActivateLocker";

        private void CheckSocketsActivate()
        {
            lock (_socketActivateLocker)
            {

                if (_spotSocketClient == null
                    || _spotSocketClient.ReadyState != WebSocketState.Open)
                {
                    Disconnect();
                    return;
                }

                if (_marginSocketClient == null
                    || _marginSocketClient.ReadyState != WebSocketState.Open)
                {
                    Disconnect();
                    return;
                }

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Connect;

                    if (ConnectEvent != null)
                    {
                        ConnectEvent();
                    }
                }
            }
        }

        private void CreateAuthMessageWebSocektSpot()
        {
            _rateGateCreateDisposeSockets.WaitToProceed();

            try
            {
                long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                string payload = $"apiKey={ApiKey}&timestamp={timestamp}";
                string signature = SignEd25519(payload);

                _spotSocketClient.SendAsync($"{{\"id\": \"1\",\"method\": \"session.logon\",\"params\": {{\"apiKey\": \"{ApiKey}\",\"signature\": \"{signature}\",\"timestamp\": {timestamp}}}}}");
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private void CreateAuthMessageWebSocektMargin()
        {
            _rateGateCreateDisposeSockets.WaitToProceed();

            try
            {
                var res = CreateQuery(BinanceExchangeType.MarginExchange, Method.POST, "/sapi/v1/userListenToken", null, false);

                if (string.IsNullOrEmpty(res))
                {
                    SendLogMessage("Margin Socket don`t ListenKey", LogMessageType.Connect);
                    _notMargineAccount = true;
                    return;
                }

                string marginKey = JsonConvert.DeserializeAnonymousType(res, new ListenKey()).token;

                if (string.IsNullOrEmpty(marginKey))
                {
                    SendLogMessage("Margin Socket don`t ListenKey", LogMessageType.Connect);
                    _notMargineAccount = true;
                    return;
                }

                _marginSocketClient.SendAsync($"{{\"id\": \"2\",\"method\": \"userDataStream.subscribe.listenToken\",\"params\": {{\"listenToken\": \"{marginKey}\" }}}}");
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        #endregion

        #region 7 WebSocket events

        private ConcurrentQueue<BinanceUserMessage> _newMessagePrivate = new ConcurrentQueue<BinanceUserMessage>();

        private ConcurrentQueue<string> _newMessagePublic = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _newMessagePublicMarketDepth = new ConcurrentQueue<string>();

        private void _spotSocketClient_OnClose(object sender, CloseEventArgs e)
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

        private void _spotSocketClient_OnError(object sender, ErrorEventArgs e)
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

                    if (message.Contains("The remote party closed the spot WebSocket connection"))
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
                SendLogMessage("Data spot socket error" + ex.ToString(), LogMessageType.Error);
            }
        }

        private void _spotSocketClient_OnOpen(object sender, EventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    CreateAuthMessageWebSocektSpot();
                    SendLogMessage("Binance Spot WebSocket private connection open", LogMessageType.System);
                    CheckSocketsActivate();

                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _marginSocketClient_OnClose(object sender, CloseEventArgs e)
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

        private void _marginSocketClient_OnError(object sender, ErrorEventArgs e)
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

                    if (message.Contains("The remote party closed the margin WebSocket connection"))
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
                SendLogMessage("Data margin socket error" + ex.ToString(), LogMessageType.Error);
            }
        }

        private void _marginSocketClient_OnOpen(object sender, EventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    SendLogMessage("Binance Margin WebSocket private connection open", LogMessageType.System);
                    CheckSocketsActivate();
                    CreateAuthMessageWebSocektMargin();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

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

        private void Client_Opened(object sender, EventArgs e)
        {
            if (ServerStatus != ServerConnectStatus.Connect)
            {
                SendLogMessage("Websockets activate. Connection status", LogMessageType.System);
            }
        }

        private void _publicSocketClient_MessageReceived(object sender, MessageEventArgs e)
        {
            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                return;
            }

            if (e.Data.Contains("\"lastUpdateId\""))
            {
                _newMessagePublicMarketDepth.Enqueue(e.Data);
            }
            else
            {
                _newMessagePublic.Enqueue(e.Data);
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

        #region 8 WebSocket check alive

        private void KeepaliveUserDataStream()
        {
            DateTime _timeStart = DateTime.Now;

            while (true)
            {
                try
                {
                    Thread.Sleep(30000);

                    if (IsCompletelyDeleted == true)
                    {
                        return;
                    }

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        _timeStart = DateTime.Now;
                        continue;
                    }

                    if (_timeStart.AddMinutes(5) < DateTime.Now)
                    {
                        _timeStart = DateTime.Now;

                        CreateAuthMessageWebSocektSpot();

                        if (_notMargineAccount == false)
                        {
                            CreateAuthMessageWebSocektMargin();
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

        private RateGate _rateGateSubscribe = new RateGate(1, TimeSpan.FromMilliseconds(150));

        public void Subscribe(Security security)
        {
            _rateGateSubscribe.WaitToProceed();

            try
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

                WebSocket _wsClient = new WebSocket(urlStr);

                if (_myProxy != null)
                {
                    _wsClient.SetProxy(_myProxy);
                }

                _wsClient.EmitOnPing = true;
                _wsClient.OnOpen += Client_Opened;
                _wsClient.OnMessage += _publicSocketClient_MessageReceived;
                _wsClient.OnError += Client_Error;
                _wsClient.OnClose += Client_Closed;
                _wsClient.ConnectAsync();

                _wsStreamsSecurityData.Add(security.Name, _wsClient);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
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
                        if (IsCompletelyDeleted == true)
                        {
                            return;
                        }

                        Thread.Sleep(1);
                    }
                    else
                    {
                        string mes;

                        if (_newMessagePublic.TryDequeue(out mes))
                        {
                            if (mes.Contains("\"e\"" + ":" + "\"trade\""))
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

        public void ConverterPublicDataMarketDepth()
        {
            while (true)
            {
                try
                {
                    if (_newMessagePublicMarketDepth.IsEmpty == true)
                    {
                        if (IsCompletelyDeleted == true)
                        {
                            return;
                        }

                        Thread.Sleep(1);
                    }
                    else
                    {
                        string mes;

                        if (_newMessagePublicMarketDepth.TryDequeue(out mes))
                        {
                            var quotes = JsonConvert.DeserializeAnonymousType(mes, new DepthResponse());
                            UpdateMarketDepth(quotes);
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
                        if (IsCompletelyDeleted == true)
                        {
                            return;
                        }
                        Thread.Sleep(1);
                    }
                    else
                    {
                        BinanceUserMessage messsage;

                        if (_newMessagePrivate.TryDequeue(out messsage))
                        {
                            string mes = messsage.MessageStr;

                            if (mes.Contains("\"id\":\"1"))
                            {
                                SubscribeToTheUserDataStream(mes);
                            }
                            else if (mes.Contains("code"))
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

        private void SubscribeToTheUserDataStream(string messsage)
        {
            try
            {
                AuthenticationResponse auth = JsonConvert.DeserializeAnonymousType(messsage, new AuthenticationResponse());

                if (auth.status == "200")
                {
                    _spotSocketClient.SendAsync($"{{\"id\": \"3\",\"method\": \"userDataStream.subscribe\"}}");
                }
                else
                {

                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
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

                foreach (var onePortf in portfs.@event.B)
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
            try
            {
                ExecutionReportEvent orders = JsonConvert.DeserializeAnonymousType(mes, new ExecutionReportEvent());

                ExecutionReport order = orders.@event;

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
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private int GetDecimalsVolume(string security)
        {
            if (_securitiesDict.TryGetValue(security, out Security sec))
            {
                return sec.DecimalsVolume;
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

        private void UpdateTrades(TradeResponse trades)
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

                MarketDepth needDepth = new MarketDepth();
                needDepth.SecurityNameCode = secName;

                List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
                List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                for (int i = 0; i < myDepth.data.asks.Count; i++)
                {
                    ascs.Add(new MarketDepthLevel()
                    {
                        Ask = myDepth.data.asks[i][1].ToString().ToDouble(),
                        Price = myDepth.data.asks[i][0].ToString().ToDouble()
                    });
                }

                for (int i = 0; i < myDepth.data.bids.Count; i++)
                {
                    bids.Add(new MarketDepthLevel()
                    {
                        Bid = myDepth.data.bids[i][1].ToString().ToDouble(),
                        Price = myDepth.data.bids[i][0].ToString().ToDouble()
                    });
                }

                needDepth.Asks = ascs;
                needDepth.Bids = bids;
                needDepth.Time = DateTime.UtcNow;// ServerTime;

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
                    MarketDepthEvent(needDepth);
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

                    var res = CreateQuery(BinanceExchangeType.MarginExchange, Method.POST, "/sapi/v1/margin/order", param, true);

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
                        CreateQuery(BinanceExchangeType.MarginExchange, Method.DELETE, "/sapi/v1/margin/order", param, true);
                    }
                    else
                    {
                        CreateQuery(BinanceExchangeType.SpotExchange, Method.DELETE, "api/v3/order", param, true);
                        CreateQuery(BinanceExchangeType.MarginExchange, Method.DELETE, "/sapi/v1/margin/order", param, true);
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

                    string res = null;

                    if (oldOrder.PortfolioNumber == "BinanceSpot")
                    {
                        res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, "/api/v3/allOrders", param, true);
                    }
                    else if (oldOrder.PortfolioNumber == "BinanceMargin")
                    {
                        res = CreateQuery(BinanceExchangeType.MarginExchange, Method.GET, "/sapi/v1/margin/allOrders", param, true);
                    }

                    //res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, endPoint, param, true);

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
            {
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
                var param = new Dictionary<string, string>();
                param.Add("symbol=", order.SecurityNameCode.ToUpper());
                //param.Add("orderId=", order.NumberMarket);
                param.Add("&limit=", "500");

                string res = null;

                if (order.PortfolioNumber == "BinanceSpot")
                {
                    res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, "/api/v3/myTrades", param, true);
                }
                else if (order.PortfolioNumber == "BinanceMargin")
                {
                    res = CreateQuery(BinanceExchangeType.MarginExchange, Method.GET, "/sapi/v1/margin/myTrades", param, true);
                }

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
                var param = new Dictionary<string, string>();
                param.Add("symbol=", securityName.ToUpper());
                param.Add("&limit=", "500");

                List<Order> result = new List<Order>();
                List<Order> allOrders = new List<Order>();

                string res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, "/api/v3/allOrders", param, true);

                if (!string.IsNullOrEmpty(res))
                {
                    HistoryOrderReport[] orders = JsonConvert.DeserializeObject<HistoryOrderReport[]>(res);

                    if (orders != null)
                    {
                        result = ConvertOrders(orders, "BinanceSpot");
                        allOrders.AddRange(result);
                    }
                }

                string res2 = CreateQuery(BinanceExchangeType.MarginExchange, Method.GET, "/sapi/v1/margin/allOrders", param, true);

                if (!string.IsNullOrEmpty(res2))
                {
                    HistoryOrderReport[] orders = JsonConvert.DeserializeObject<HistoryOrderReport[]>(res2);

                    if (orders != null)
                    {
                        result = ConvertOrders(orders, "BinanceMargin");
                        allOrders.AddRange(result);
                    }
                }

                if (allOrders.Count == 0)
                {
                    return null;
                }

                return allOrders;

            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private List<Order> ConvertOrders(HistoryOrderReport[] historyOrder, string portfolioName)
        {
            List<Order> result = new List<Order>();

            for (int i = 0; i < historyOrder.Length; i++)
            {
                HistoryOrderReport myOrder = historyOrder[i];

                Order newOrder = new Order();
                newOrder.NumberMarket = myOrder.orderId;

                if (myOrder.clientOrderId != null)
                {
                    string id = myOrder.clientOrderId.Replace("x-RKXTQ2AK", "");
                    try
                    {
                        newOrder.NumberUser = Convert.ToInt32(id);
                    }
                    catch
                    {
                        // ignore
                    }
                }

                newOrder.SecurityNameCode = myOrder.symbol;
                newOrder.Price = myOrder.price.ToDecimal();
                newOrder.Volume = myOrder.origQty.ToDecimal();
                newOrder.ServerType = ServerType.Binance;
                newOrder.PortfolioNumber = portfolioName;

                if (myOrder.side == "BUY")
                {
                    newOrder.Side = Side.Buy;
                }
                else
                {
                    newOrder.Side = Side.Sell;
                }

                if (myOrder.type == "MARKET")
                {
                    newOrder.TypeOrder = OrderPriceType.Market;
                }
                else
                {
                    newOrder.TypeOrder = OrderPriceType.Limit;
                }

                newOrder.TimeCreate = new DateTime(1970, 1, 1).AddMilliseconds(myOrder.time.ToDouble());
                newOrder.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(myOrder.updateTime.ToDouble());

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

        private void GetAllOpenOrders(List<Order> array, int maxCount)
        {
            try
            {
                var param = new Dictionary<string, string>();

                List<Order> result = new List<Order>();
                List<Order> allOrders = new List<Order>();

                string res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, "/api/v3/openOrders", param, true);

                if (!string.IsNullOrEmpty(res))
                {
                    HistoryOrderReport[] orders = JsonConvert.DeserializeObject<HistoryOrderReport[]>(res);

                    if (orders != null)
                    {
                        result = ConvertOrders(orders, "BinanceSpot");
                        allOrders.AddRange(result);
                    }
                }

                string res2 = CreateQuery(BinanceExchangeType.MarginExchange, Method.GET, "/sapi/v1/margin/openOrders", param, true);

                if (!string.IsNullOrEmpty(res2))
                {
                    HistoryOrderReport[] orders = JsonConvert.DeserializeObject<HistoryOrderReport[]>(res2);

                    if (orders != null)
                    {
                        result = ConvertOrders(orders, "BinanceMargin");
                        allOrders.AddRange(result);
                    }
                }

                if (allOrders.Count > 0)
                {
                    array.AddRange(allOrders);

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
                            fullUrl = "?timestamp=" + timeStamp + "&signature=" + SignEd25519(message);
                        }
                        else
                        {
                            message = fullUrl + "&timestamp=" + timeStamp;
                            fullUrl += "&timestamp=" + timeStamp + "&signature=" + SignEd25519(message.Trim('?'));
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
                        throw new Exception($"{startUri}. {error.msg}");
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

        public string SignEd25519(string payload)
        {
            try
            {
                byte[] privateKeyBytes = Convert.FromBase64String(SecretKey);
                byte[] payloadBytes = Encoding.ASCII.GetBytes(payload);
                var algorithm = SignatureAlgorithm.Ed25519;

                byte[] rawPrivateKey = ExtractRawPrivateKeyFromPKCS8(privateKeyBytes);

                using (var key = Key.Import(algorithm, rawPrivateKey, KeyBlobFormat.RawPrivateKey))
                {
                    byte[] signatureBytes = algorithm.Sign(key, payloadBytes);
                    return Convert.ToBase64String(signatureBytes);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private static byte[] ExtractRawPrivateKeyFromPKCS8(byte[] pkcs8Bytes)
        {
            if (pkcs8Bytes.Length >= 32)
            {
                byte[] rawKey = new byte[32];
                Array.Copy(pkcs8Bytes, pkcs8Bytes.Length - 32, rawKey, 0, 32);
                return rawKey;
            }

            throw new Exception("Не удалось извлечь raw ключ из PKCS#8");
        }

        public void SetLeverage(Security security, decimal leverage) { }

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
