/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.BitMex.BitMexEntity;
using OsEngine.Market.Servers.Entity;
using RestSharp;
using WebSocket4Net;


namespace OsEngine.Market.Servers.BitMex
{
    public class BitMexServer : AServer
    {
        public BitMexServer()
        {
            BitMexServerRealization realization = new BitMexServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamId, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");
        }
    }

    public class BitMexServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public BitMexServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread thread = new Thread(CheckAliveWebSocket);
            thread.IsBackground = true;
            thread.Name = "CheckAliveWebSocketBitMex";
            thread.Start();

            Thread converter = new Thread(MessageReader);
            converter.IsBackground = true;
            converter.Name = "MessageReaderBitMex";
            converter.Start();
        }

        public void Connect()
        {
            _id = ((ServerParameterString)ServerParameters[0]).Value;
            _secKey = ((ServerParameterPassword)ServerParameters[1]).Value;

            if (string.IsNullOrEmpty(_id) ||
                string.IsNullOrEmpty(_secKey))
            {
                SendLogMessage("Can`t run BitMex connector. No keys", LogMessageType.Error);
                return;
            }

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3
                    | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            try
            {
                RestRequest requestRest = new RestRequest("/api/v1", Method.GET);
                IRestResponse response = new RestClient(_domain).Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    _fifoListWebSocketMessage = new ConcurrentQueue<string>();

                    CreateWebSocketConnection();
                }
                else
                {
                    SendLogMessage("Connection can be open. BitMex. Error request", LogMessageType.Error);

                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Can`t run BitMex connector. No internet connection. {ex.ToString()}", LogMessageType.Error);
                return;
            }
        }

        public void Dispose()
        {
            try
            {
                _subscribedSec.Clear();
                _depths.Clear();
                DeleteWebsocketConnection();
            }
            catch (Exception exeption)
            {
                SendLogMessage(exeption.ToString(), LogMessageType.Error);
            }

            _fifoListWebSocketMessage = null;

            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;

                if (DisconnectEvent != null)
                {
                    DisconnectEvent();
                }
            }
        }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public ServerConnectStatus ServerStatus { get; set; }

        public ServerType ServerType
        {
            get { return ServerType.BitMex; }
        }

        #endregion

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        public DateTime ServerTime { get; set; }

        private string _id;

        private string _secKey;

        private string _domain = "https://www.bitmex.com";

        private string _serverAdress = "wss://ws.bitmex.com/realtime";

        #endregion

        #region 3 Securities

        public void GetSecurities()
        {
            _rateGate.WaitToProceed();

            try
            {
                RestRequest requestRest = new RestRequest("/api/v1/instrument/active", Method.GET);
                IRestResponse response = new RestClient(_domain).Execute(requestRest);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage($"Http State Code: {response.StatusCode} - {response.Content}", LogMessageType.Error);
                    return;
                }

                List<BitMexSecurity> responseSecutity = JsonConvert.DeserializeObject<List<BitMexSecurity>>(response.Content);

                List<Security> securities = new List<Security>();

                if (responseSecutity.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < responseSecutity.Count; i++)
                {
                    BitMexSecurity newSecurity = responseSecutity[i];

                    if (newSecurity.state != "Open")
                    {
                        continue;
                    }

                    Security security = new Security();
                    security.Exchange = ServerType.BitMex.ToString();
                    security.Name = newSecurity.symbol;
                    security.NameFull = newSecurity.symbol;
                    security.NameClass = newSecurity.typ;
                    security.NameId = newSecurity.symbol + newSecurity.listing;
                    security.SecurityType = SecurityType.CurrencyPair;
                    security.Lot = 1;
                    security.PriceStep = newSecurity.tickSize.ToDecimal();
                    security.PriceStepCost = security.PriceStep;

                    if (security.PriceStep < 1)
                    {
                        string prStep = security.PriceStep.ToString(CultureInfo.InvariantCulture);

                        security.Decimals = Convert.ToString(prStep).Split('.')[1].Length;
                    }
                    else
                    {
                        security.Decimals = 0;
                    }

                    if (newSecurity.lotSize != null)
                    {
                        decimal lotSize = newSecurity.lotSize.ToDecimal();
                        decimal mult = newSecurity.multiplier.ToDecimal();

                        if (newSecurity.quoteCurrency != "USD" && newSecurity.quoteCurrency != "USDC" && newSecurity.typ != "FFICSX")
                        {
                            decimal underlyingToPositionMultiplier = newSecurity.underlyingToPositionMultiplier.ToDecimal();
                            decimal underlyingToSettleMultiplier = newSecurity.underlyingToSettleMultiplier.ToDecimal();
                            decimal minimumTradeAmount = 0;

                            if (underlyingToPositionMultiplier != 0)
                            {
                                minimumTradeAmount = lotSize / underlyingToPositionMultiplier;
                            }
                            else if (underlyingToSettleMultiplier != 0)
                            {
                                minimumTradeAmount = mult * lotSize / underlyingToSettleMultiplier;
                            }
                            else
                            {
                                return;
                            }

                            string qtyInStr = minimumTradeAmount.ToStringWithNoEndZero().Replace(",", ".");
                            if (qtyInStr.Split('.').Length > 1)
                            {
                                security.DecimalsVolume = qtyInStr.Split('.')[1].Length;
                            }
                            else
                            {
                                security.DecimalsVolume = 0;
                            }
                        }
                        else
                        {
                            security.DecimalsVolume = 0;
                        }
                    }

                    security.State = SecurityStateType.Activ;
                    securities.Add(security);
                }

                if (SecurityEvent != null)
                {
                    SecurityEvent(securities);
                }
            }
            catch (Exception exception)
            {
                if (LogMessageEvent != null)
                {
                    LogMessageEvent(exception.ToString(), LogMessageType.Error);
                }
            }
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        public event Action<List<Portfolio>> PortfolioEvent;

        private bool _isUpdateValueBegin = false;
        public void GetPortfolios()
        {
            _rateGate.WaitToProceed();

            try
            {
                RestClient client = new RestClient(_domain);
                RestRequest requestRest = new RestRequest("/api/v1/user/margin?currency=all", Method.GET);

                string expires = GetExpires().ToString();
                string message = "GET" + "/api/v1/user/margin?currency=all" + expires;
                byte[] signatureBytes = hmacsha256(Encoding.UTF8.GetBytes(_secKey), Encoding.UTF8.GetBytes(message));
                string signatureString = ByteArrayToString(signatureBytes);

                requestRest.AddHeader("api-expires", expires);
                requestRest.AddHeader("api-key", _id);
                requestRest.AddHeader("api-signature", signatureString);

                IRestResponse json = client.Execute(requestRest);

                if (json.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    List<Datum> responsePortfolio = JsonConvert.DeserializeAnonymousType(json.Content, new List<Datum>());

                    Portfolio myPortfolio = new Portfolio();
                    myPortfolio.Number = "BitMex";
                    myPortfolio.ValueBegin = 1;
                    myPortfolio.ValueCurrent = 1;

                    for (int i = 0; i < responsePortfolio.Count; i++)
                    {
                        PositionOnBoard newPortfolio = new PositionOnBoard();
                        newPortfolio.SecurityNameCode = responsePortfolio[i].currency;
                        newPortfolio.ValueBegin = responsePortfolio[i].walletBalance.ToDecimal() / 1000000;

                        if (responsePortfolio[i].marginBalance.ToDecimal() == responsePortfolio[i].walletBalance.ToDecimal())
                        {
                            newPortfolio.ValueCurrent = responsePortfolio[i].availableMargin.ToDecimal() / 1000000;
                        }
                        else
                        {
                            newPortfolio.ValueCurrent = responsePortfolio[i].marginBalance.ToDecimal() / 1000000;
                        }

                        newPortfolio.ValueBlocked = newPortfolio.ValueBegin - responsePortfolio[i].availableMargin.ToDecimal() / 1000000;
                        newPortfolio.PortfolioName = "BitMex";
                        myPortfolio.SetNewPosition(newPortfolio);
                    }

                    if (PortfolioEvent != null)
                    {
                        _isUpdateValueBegin = true;
                        PortfolioEvent(new List<Portfolio> { myPortfolio });
                    }
                }
                else
                {
                    SendLogMessage($"Http State Code: {json.StatusCode}, {json.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region 5 Data

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            int countLoadCandle = GetCountCandlesToLoad();
            int countCandle = countLoadCandle > candleCount ? countLoadCandle : candleCount;

            DateTime timeStart = GetCountCandlesFromSliceTime(timeFrameBuilder, countCandle);
            DateTime timeEnd = DateTime.Now;

            List<Candle> candles = GetCandles(security.Name, timeFrameBuilder.TimeFrameTimeSpan, timeStart, timeEnd);

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

        private DateTime GetCountCandlesFromSliceTime(TimeFrameBuilder timeFrameBuilder, int countCandle)
        {
            DateTime timeStart = DateTime.Now;

            if (timeFrameBuilder.TimeFrameTimeSpan.Days != 0)
            {
                int totalDay = timeFrameBuilder.TimeFrameTimeSpan.Days;
                return timeStart = DateTime.Now - TimeSpan.FromDays(totalDay * countCandle);
            }
            else if (timeFrameBuilder.TimeFrameTimeSpan.Hours != 0)
            {
                int totalHour = Convert.ToInt32(timeFrameBuilder.TimeFrameTimeSpan.TotalHours);
                return timeStart = DateTime.Now - TimeSpan.FromHours(totalHour * countCandle);
            }
            else
            {
                int totalMinutes = Convert.ToInt32(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes);
                return timeStart = DateTime.Now - TimeSpan.FromMinutes(totalMinutes * countCandle);
            }
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder,
            DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if (endTime > DateTime.Now ||
                startTime >= endTime ||
                startTime >= DateTime.Now ||
                actualTime > endTime ||
                actualTime > DateTime.Now)
            {
                return null;
            }

            List<Candle> candles = new List<Candle>();

            List<Candle> newCandles = GetCandles(security.Name, timeFrameBuilder.TimeFrameTimeSpan,
                startTime, endTime);

            if (newCandles == null || newCandles.Count == 0)
            {
                return null;
            }

            candles.AddRange(newCandles);
            actualTime = candles[0].TimeStart;

            while (actualTime > startTime)
            {
                newCandles = GetCandles(security.Name, timeFrameBuilder.TimeFrameTimeSpan,
                    startTime, actualTime);

                if (newCandles != null && candles.Count != 0 && newCandles.Count != 0)
                {
                    for (int i = 0; i < newCandles.Count; i++)
                    {
                        if (candles[0].TimeStart <= newCandles[i].TimeStart)
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
                    Thread.Sleep(5000);
                    break;
                }

                candles.InsertRange(0, newCandles);

                actualTime = candles[0].TimeStart;

                Thread.Sleep(3000);
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

            return candles;
        }

        private List<Candle> GetCandles(string security, TimeSpan timeSpan, DateTime startTime, DateTime endTime)
        {
            try
            {
                if (timeSpan.TotalMinutes < 1)
                {
                    return null;
                }

                if (timeSpan.Minutes == 1)
                {
                    return GetCandlesTf(security, "1m", startTime, endTime);
                }
                else if (timeSpan.Minutes == 5)
                {
                    return GetCandlesTf(security, "5m", startTime, endTime);
                }
                else if (timeSpan.TotalMinutes == 60)
                {
                    return GetCandlesTf(security, "1h", startTime, endTime);
                }
                else if (timeSpan.TotalMinutes == 1440)
                {
                    return GetCandlesTf(security, "1d", startTime, endTime);
                }
                else
                {
                    return СandlesBuilder(security, (int)timeSpan.TotalMinutes, startTime, endTime);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private List<Candle> GetCandlesTf(string security, string tf, DateTime startTime, DateTime timeEnd, int a = 1)
        {
            _rateGate.WaitToProceed();
            try
            {
                List<BitMexCandle> allBitMexCandles = new List<BitMexCandle>();

                List<Candle> allCandles = new List<Candle>();

                string end = timeEnd.ToString("yyyy-MM-dd HH:mm:ss");
                string start = startTime.ToString("yyyy-MM-dd HH:mm:ss");

                //string parameters = $"binSize={tf}&partial={true.ToString()}&symbol={security}" +
                //    $"&count={10000.ToString()}&reverse={true.ToString()}&startTime={start}&endTime={end}";

                Dictionary<string, string> param = new Dictionary<string, string>();
                param.Add("binSize", tf);
                param.Add("partial", true.ToString());
                param.Add("symbol", security);
                param.Add("count", 10000.ToString());
                param.Add("reverse", true.ToString());
                param.Add("startTime", start);
                param.Add("endTime", end);

                try
                {
                    string responseQuery = Query("GET", "/trade/bucketed", param);
                    if (responseQuery == "[]")
                    {
                        return null;
                    }

                    //RestClient client = new RestClient(_domain);
                    //RestRequest request = new RestRequest("/api/v1/trade/bucketed", Method.GET);

                    //string expires = GetExpires().ToString();
                    //string message = /*"GET" + */"/api/v1/trade/bucketed?" /*+ expires*/ + parameters;
                    //byte[] signatureBytes = hmacsha256(Encoding.UTF8.GetBytes(_secKey), Encoding.UTF8.GetBytes(message));
                    //string signatureString = ByteArrayToString(signatureBytes);

                    //request.AddHeader("api-expires", expires);
                    //request.AddHeader("api-key", _id);
                    //request.AddHeader("api-signature", signatureString);
                    //request.AddParameter("application/x-www-form-urlencoded", parameters, ParameterType.RequestBody);

                    //IRestResponse json = client.Execute(request);

                    //if (json.StatusCode != HttpStatusCode.OK)
                    //{
                    //    SendLogMessage($"Http State Code: {json.StatusCode} - {json.Content}", LogMessageType.Error);
                    //    return null;
                    //}

                    List<BitMexCandle> responseCandles =
                       JsonConvert.DeserializeAnonymousType(responseQuery, new List<BitMexCandle>());

                    allBitMexCandles.AddRange(responseCandles);
                }
                catch
                {
                    // ignored
                }

                for (int i = 0; i < allBitMexCandles.Count; i++)
                {
                    Candle newCandle = new Candle();

                    if (allBitMexCandles[i].open < allBitMexCandles[i].high)
                    {
                        newCandle.Open = allBitMexCandles[i].open;
                        newCandle.High = allBitMexCandles[i].high;
                    }
                    else
                    {
                        newCandle.Open = allBitMexCandles[i].high;
                        newCandle.High = allBitMexCandles[i].open;
                    }

                    if (allBitMexCandles[i].open > allBitMexCandles[i].low)
                    {
                        newCandle.Open = allBitMexCandles[i].open;
                        newCandle.Low = allBitMexCandles[i].low;
                    }
                    else
                    {
                        newCandle.Open = allBitMexCandles[i].low;
                        newCandle.Low = allBitMexCandles[i].open;
                    }

                    newCandle.Close = allBitMexCandles[i].close;
                    newCandle.TimeStart = Convert.ToDateTime(allBitMexCandles[i].timestamp);
                    newCandle.Volume = allBitMexCandles[i].volume;

                    allCandles.Add(newCandle);
                }

                allCandles.Reverse();
                return allCandles;
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private int GetCountCandlesToLoad()
        {
            AServer server = null;

            for (int i = 0; i < ServerMaster.GetServers().Count; i++)
            {
                if (ServerMaster.GetServers()[i].ServerType == ServerType.BitMex)
                {
                    server = (AServer)ServerMaster.GetServers()[i];
                    break;
                }
            }

            for (int i = 0; i < server.ServerParameters.Count; i++)
            {
                if (server.ServerParameters[i].Name.Equals(OsLocalization.Market.ServerParam6))
                {
                    ServerParameterInt Param = (ServerParameterInt)server.ServerParameters[i];
                    return Param.Value;
                }
            }

            return 300;
        }

        private List<Candle> СandlesBuilder(string security, int tf, DateTime startTime, DateTime endTime)
        {
            List<Candle> oldCandles;
            int a;
            if (tf > 60)
            {
                a = tf / 60;
                oldCandles = GetCandlesTf(security, "1h", startTime, endTime);
            }
            else if (tf >= 10)
            {
                a = tf / 5;
                oldCandles = GetCandlesTf(security, "5m", startTime, endTime);
            }
            else
            {
                a = tf / 1;
                oldCandles = GetCandlesTf(security, "1m", startTime, endTime);
            }

            if (oldCandles == null || oldCandles.Count == 0)
            {
                return null;
            }

            int index = 0;

            if (tf > 60)
            {
                for (int i = 0; i < oldCandles.Count; i++)
                {
                    if (oldCandles[i].TimeStart.Hour % a == 0)
                    {
                        index = i;
                        break;
                    }
                }
            }
            else
            {
                for (int i = 0; i < oldCandles.Count; i++)
                {
                    if (oldCandles[i].TimeStart.Minute % tf == 0)
                    {
                        index = i;
                        break;
                    }
                }
            }

            List<Candle> candlesTimeFrame = new List<Candle>();

            int count = 0;

            Candle newCandle = new Candle();

            for (int i = index; i < oldCandles.Count; i++)
            {
                count++;
                if (count == 1)
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

                if (i == oldCandles.Count - 1 && count != a)
                {
                    newCandle.Close = oldCandles[i].Close;
                    newCandle.State = CandleState.None;
                    candlesTimeFrame.Add(newCandle);
                }

                if (count == a)
                {
                    newCandle.Close = oldCandles[i].Close;
                    newCandle.State = CandleState.Finished;
                    candlesTimeFrame.Add(newCandle);
                    count = 0;
                }
            }

            for (int i = 1; candlesTimeFrame != null && i < candlesTimeFrame.Count; i++)
            {
                if (candlesTimeFrame[i - 1].TimeStart == candlesTimeFrame[i].TimeStart)
                {
                    candlesTimeFrame.RemoveAt(i);
                    i--;
                }
            }

            return candlesTimeFrame;
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if (endTime > DateTime.Now ||
                startTime >= endTime ||
                startTime >= DateTime.Now ||
                actualTime > endTime ||
                actualTime > DateTime.Now)
            {
                return null;
            }


            List<Trade> trades = new List<Trade>();

            List<Trade> newTrades = GetTickHistoryToSecurity(security.Name, startTime, endTime);

            if (newTrades == null ||
                    newTrades.Count == 0)
            {
                return null;
            }

            trades.AddRange(newTrades);
            actualTime = trades[0].Time.AddMilliseconds(1);

            while (actualTime > startTime)
            {
                newTrades = GetTickHistoryToSecurity(security.Name, startTime, actualTime);

                if (newTrades != null && trades.Count != 0 && newTrades.Count != 0)
                {
                    for (int j = 0; j < trades.Count; j++)
                    {
                        for (int i = 0; i < newTrades.Count; i++)
                        {
                            if (trades[j].Time.AddMilliseconds(1) <= newTrades[i].Time.AddMilliseconds(1)
                                && trades[j].Id == newTrades[i].Id)
                            {
                                newTrades.RemoveAt(i);
                                i--;
                            }
                        }
                    }

                }

                if (newTrades.Count == 0)
                {
                    Thread.Sleep(5000);
                    break;
                }

                trades.InsertRange(0, newTrades);
                actualTime = trades[0].Time.AddMilliseconds(1);
                Thread.Sleep(3000);
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

        public List<Trade> GetTickHistoryToSecurity(string security, DateTime startTime, DateTime endTime)
        {
            _rateGate.WaitToProceed();
            try
            {
                List<Trade> trades = new List<Trade>();

                string start = startTime.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string end = endTime.ToString("yyyy-MM-dd HH:mm:ss.fff");

                //string parameters = $"symbol={security}&start={0.ToString()}&count={1000.ToString()}&reverse={true.ToString()}&startTime={start}&endTime={end}";

                Dictionary<string, string> param = new Dictionary<string, string>();
                param.Add("symbol", security);
                param.Add("start", 0.ToString());
                param.Add("reverse", true.ToString());
                param.Add("startTime", start);
                param.Add("endTime", end);
                param.Add("count", "1000");

                string responseQuery = Query("GET", "/trade", param);

                if (responseQuery == "")
                {
                    return null;
                }

                //RestClient client = new RestClient(_domain);
                //RestRequest request = new RestRequest("/api/v1/trade", Method.GET);

                //string expires = GetExpires().ToString();
                //string message = "GET" + "/api/v1/trade" + expires + parameters;
                //byte[] signatureBytes = hmacsha256(Encoding.UTF8.GetBytes(_secKey), Encoding.UTF8.GetBytes(message));
                //string signatureString = ByteArrayToString(signatureBytes);

                //request.AddHeader("api-expires", expires);
                //request.AddHeader("api-key", _id);
                //request.AddHeader("api-signature", signatureString);
                //request.AddParameter("application/x-www-form-urlencoded", parameters, ParameterType.RequestBody);

                //IRestResponse json = client.Execute(request);

                //if (json.StatusCode != HttpStatusCode.OK)
                //{
                //    SendLogMessage($"Http State Code: {json.StatusCode} - {json.Content}", LogMessageType.Error);
                //    return null;
                //}

                List<DatumTrades> tradeHistoryResponse = JsonConvert.DeserializeAnonymousType(responseQuery, new List<DatumTrades>());

                for (int i = 0; i < tradeHistoryResponse.Count; i++)
                {
                    if (string.IsNullOrEmpty(tradeHistoryResponse[i].price))
                    {
                        continue;
                    }

                    Trade trade = new Trade();
                    trade.SecurityNameCode = tradeHistoryResponse[i].symbol;
                    trade.Id = tradeHistoryResponse[i].trdMatchID;
                    trade.Time = Convert.ToDateTime(tradeHistoryResponse[i].timestamp).AddMilliseconds(1);
                    trade.Price = tradeHistoryResponse[i].price.ToDecimal();
                    trade.Volume = tradeHistoryResponse[i].size.ToDecimal();
                    trade.Side = tradeHistoryResponse[i].side == "Sell" ? Side.Sell : Side.Buy;
                    trades.Add(trade);
                }

                trades.Reverse();
                return trades;
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                return null;
            }
        }

        #endregion

        #region 6 WebSocket creation

        private WebSocket _webSocket;

        private void CreateWebSocketConnection()
        {
            if (_webSocket != null)
            {
                return;
            }
            _webSocket = new WebSocket(_serverAdress);
            _webSocket.EnableAutoSendPing = true;
            _webSocket.AutoSendPingInterval = 10;

            _webSocket.Opened += _webSocket_Opened;
            _webSocket.Closed += _webSocket_Closed;
            _webSocket.MessageReceived += _webSocket_MessageReceived;
            _webSocket.Error += _webSocket_Error;

            _webSocket.Open();
        }

        private void DeleteWebsocketConnection()
        {
            if (_webSocket != null)
            {
                try
                {
                    _webSocket.Close();
                }
                catch
                {
                    // ignore
                }

                _webSocket.Opened -= _webSocket_Opened;
                _webSocket.Closed -= _webSocket_Closed;
                _webSocket.MessageReceived -= _webSocket_MessageReceived;
                _webSocket.Error -= _webSocket_Error;
                _webSocket = null;
            }
        }

        private void Auth()
        {
            string nonce = GetExpires().ToString();
            byte[] signatureBytes = hmacsha256(Encoding.UTF8.GetBytes(_secKey), Encoding.UTF8.GetBytes("GET/realtime" + nonce));
            string signatureString = ByteArrayToString(signatureBytes);
            string que = "{\"op\": \"authKeyExpires\", \"args\": [\"" + _id + "\"," + nonce + ",\"" + signatureString + "\"]}";
            _webSocket.Send(que);
        }

        #endregion

        #region 7 WebSocket events

        private void _webSocket_Error(object sender, SuperSocket.ClientEngine.ErrorEventArgs e)
        {
            var error = (SuperSocket.ClientEngine.ErrorEventArgs)e;

            if (error.Exception != null)
            {
                SendLogMessage(error.Exception.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocket_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            try
            {
                if (e == null)
                {
                    return;
                }
                if (string.IsNullOrEmpty(e.Message))
                {
                    return;
                }
                if (_fifoListWebSocketMessage == null)
                {
                    return;
                }

                _fifoListWebSocketMessage.Enqueue(e.Message);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocket_Closed(object sender, EventArgs e)
        {
            if (DisconnectEvent != null && ServerStatus != ServerConnectStatus.Disconnect)
            {
                SendLogMessage("Connection Closed by BitMex. WebSocket Closed Event", LogMessageType.Connect);
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        private void _webSocket_Opened(object sender, EventArgs e)
        {
            Auth();
            SendLogMessage("Connection Open", LogMessageType.Connect);

            if (ConnectEvent != null && ServerStatus != ServerConnectStatus.Connect)
            {
                ServerStatus = ServerConnectStatus.Connect;
                ConnectEvent();
            }

            _webSocket.Send("{\"op\": \"subscribe\", \"args\": [\"margin\"]}");  // Portfolio
            _webSocket.Send("{\"op\": \"subscribe\", \"args\": [\"position\"]}"); // Position
        }

        #endregion

        #region 8 WebSocket check alive

        private DateTime _timeLastSendPing = DateTime.Now;

        private void CheckAliveWebSocket()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(3000);

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        _timeLastSendPing = DateTime.Now;
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (_webSocket != null && _webSocket.State == WebSocketState.Open ||
                        _webSocket.State == WebSocketState.Connecting)
                    {
                        if (_timeLastSendPing.AddSeconds(25) < DateTime.Now)
                        {
                            _webSocket.Send("ping");
                            _timeLastSendPing = DateTime.Now;
                        }
                    }
                    else
                    {
                        if (ServerStatus != ServerConnectStatus.Disconnect)
                        {
                            ServerStatus = ServerConnectStatus.Disconnect;
                            DisconnectEvent();
                        }
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        #endregion

        #region 9 Security subscrible

        private RateGate _rateGateSubscrible = new RateGate(1, TimeSpan.FromMilliseconds(350));

        public void Subscrible(Security security)
        {
            try
            {
                _rateGateSubscrible.WaitToProceed();

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                for (int i = 0; i < _subscribedSec.Count; i++)
                {
                    if (_subscribedSec[i].Equals(security.Name))
                    {
                        return;
                    }
                }

                _subscribedSec.Add(security.Name);

                _webSocket.Send("{\"op\": \"subscribe\", \"args\": [\"orderBookL2_25:" + security.Name + "\"]}"); // MarketDepth
                _webSocket.Send("{\"op\": \"subscribe\", \"args\": [\"trade:" + security.Name + "\"]}");  // Trade
                _webSocket.Send("{\"op\": \"subscribe\", \"args\": [\"execution:" + security.Name + "\"]}"); // MyTrade
                _webSocket.Send("{\"op\": \"subscribe\", \"args\": [\"order:" + security.Name + "\"]}");   // Order

            }
            catch (Exception exeption)
            {
                Thread.Sleep(5000);
                SendLogMessage(exeption.ToString(), LogMessageType.Error);
            }
        }

        private List<string> _subscribedSec = new List<string>();

        #endregion

        #region 10 WebSocket parsing the messages

        private ConcurrentQueue<string> _fifoListWebSocketMessage = new ConcurrentQueue<string>();

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        private void MessageReader()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (_fifoListWebSocketMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message;

                    _fifoListWebSocketMessage.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    if (message.Equals("pong"))
                    {
                        continue;
                    }

                    if (message.StartsWith("{\"table\"" + ":" + "\"execution\""))
                    {
                        UpdateMyTrade(message);
                        continue;
                    }

                    if (message.StartsWith("{\"table\"" + ":" + "\"order\""))
                    {
                        UpdateOrder(message);
                        continue;
                    }

                    if (message.StartsWith("{\"table\"" + ":" + "\"margin\""))
                    {
                        UpdatePortfolio(message);
                        continue;
                    }

                    if (message.StartsWith("{\"table\"" + ":" + "\"position\""))
                    {
                        UpdatePosition(message);
                        continue;
                    }

                    if (message.StartsWith("{\"table\"" + ":" + "\"orderBookL2_25\""))
                    {
                        UpdateMarketDepth(message);
                        continue;
                    }

                    if (message.StartsWith("{\"table\"" + ":" + "\"trade\""))
                    {
                        UpdateTrade(message);
                        continue;
                    }

                    if (message.Contains("error"))
                    {
                        SendLogMessage(message.ToString(), LogMessageType.Error);
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void UpdateOrder(string message)
        {
            try
            {
                BitMexOrder responceOrder = JsonConvert.DeserializeAnonymousType(message, new BitMexOrder());

                if (responceOrder.data == null ||
                    responceOrder.data.Count == 0)
                {
                    return;
                }

                List<Order> newOrders = new List<Order>();

                for (int i = 0; i < responceOrder.data.Count; i++)
                {
                    decimal multiplierForSecurity = GetMultiplierForSecurity(responceOrder.data[i].symbol);

                    DatumOrder item = responceOrder.data[i];

                    if (string.IsNullOrEmpty(item.orderID))
                    {
                        continue;
                    }

                    OrderStateType stateType = GetOrderState(item.ordStatus);

                    Order newOrder = new Order();
                    newOrder.SecurityNameCode = item.symbol;
                    newOrder.TimeCallBack = Convert.ToDateTime(item.transactTime);
                    newOrder.TimeCreate = Convert.ToDateTime(item.timestamp);
                    newOrder.NumberUser = Convert.ToInt32(item.clOrdID);
                    newOrder.NumberMarket = item.orderID.ToString();
                    newOrder.Side = item.side.Equals("Buy") ? Side.Buy : Side.Sell;
                    newOrder.State = stateType;
                    newOrder.Volume = item.orderQty.ToDecimal() / multiplierForSecurity;
                    newOrder.Price = item.price.ToDecimal();
                    newOrder.ServerType = ServerType.BitMex;
                    newOrder.PortfolioNumber = "BitMex";
                    newOrder.SecurityClassCode = item.symbol;
                    newOrder.TypeOrder = item.ordType == "Limit"
                            ? OrderPriceType.Limit
                            : OrderPriceType.Market;

                    MyOrderEvent(newOrder);

                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private OrderStateType GetOrderState(string orderStateResponse)
        {
            OrderStateType stateType;

            switch (orderStateResponse)
            {
                case ("New"):
                    stateType = OrderStateType.Active;
                    break;
                case ("PartiallyFilled"):
                    stateType = OrderStateType.Partial;
                    break;
                case ("Filled"):
                    stateType = OrderStateType.Done;
                    break;
                case ("Canceled"):
                    stateType = OrderStateType.Cancel;
                    break;
                case ("Expired"):
                    stateType = OrderStateType.Cancel;
                    break;
                case ("Rejected"):
                    stateType = OrderStateType.Fail;
                    break;
                default:
                    stateType = OrderStateType.None;
                    break;
            }

            return stateType;
        }

        private void UpdatePortfolio(string message)
        {
            try
            {
                BitMexPortfolio responcePortfolio = JsonConvert.DeserializeAnonymousType(message, new BitMexPortfolio());

                if (responcePortfolio.data == null ||
                    responcePortfolio.data.Count == 0
                    || responcePortfolio.action != "update")
                {
                    return;
                }

                Portfolio portfolio = new Portfolio();
                portfolio.Number = "BitMex";
                portfolio.ValueBegin = 1;
                portfolio.ValueCurrent = 1;

                PositionOnBoard pos = new PositionOnBoard();

                pos.SecurityNameCode = responcePortfolio.data[0].currency;
                pos.ValueBegin = responcePortfolio.data[0].availableMargin.ToDecimal() / 1000000;

                if (responcePortfolio.data[0].marginBalance.ToDecimal() != 0)
                {
                    pos.ValueCurrent = responcePortfolio.data[0].marginBalance.ToDecimal() / 1000000;
                }
                else
                {
                    pos.ValueCurrent = responcePortfolio.data[0].availableMargin.ToDecimal() / 1000000;
                }

                pos.ValueBlocked = responcePortfolio.data[0].initMargin.ToDecimal() / 1000000;

                portfolio.SetNewPosition(pos);

                if (PortfolioEvent != null)
                {
                    PortfolioEvent(new List<Portfolio> { portfolio });
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private List<MarketDepth> _depths = new List<MarketDepth>();

        private void UpdateMarketDepth(string message)
        {
            try
            {
                BitMexQuotes responceDepths = JsonConvert.DeserializeAnonymousType(message, new BitMexQuotes());

                if (responceDepths.data == null ||
                   responceDepths.data.Count == 0)
                {
                    return;
                }

                MarketDepth depth = null;

                decimal volumeMultiplier = 1;

                for (int i = 0; i < _depths.Count; i++)
                {
                    if (_depths[i].SecurityNameCode == responceDepths.data[0].symbol)
                    {
                        depth = _depths[i];
                        break;
                    }
                }

                if (responceDepths.action == "partial")
                {
                    if (depth == null)
                    {
                        depth = new MarketDepth();
                        _depths.Add(depth);
                    }
                    else
                    {
                        depth.Asks.Clear();
                        depth.Bids.Clear();
                    }
                    depth.SecurityNameCode = responceDepths.data[0].symbol;
                    List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
                    List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                    for (int i = 0; i < responceDepths.data.Count; i++)
                    {
                        if (responceDepths.data[i].price == null ||
                            responceDepths.data[i].price.ToDecimal() == 0)
                        {
                            continue;
                        }

                        if (responceDepths.data[i].symbol.Contains("USDT"))
                        {
                            volumeMultiplier = 1000000;
                        }
                        if (responceDepths.data[i].side == "Sell")
                        {
                            ascs.Add(new MarketDepthLevel()
                            {
                                Ask = responceDepths.data[i].size.ToDecimal() / volumeMultiplier,
                                Price = responceDepths.data[i].price.ToDecimal(),
                                Id = responceDepths.data[i].id
                            });

                            if (depth.Bids != null && depth.Bids.Count > 2 &&
                                responceDepths.data[i].price.ToDecimal() < depth.Bids[0].Price)
                            {
                                depth.Bids.RemoveAt(0);
                            }
                        }
                        else
                        {
                            bids.Add(new MarketDepthLevel()
                            {
                                Bid = responceDepths.data[i].size.ToDecimal() / volumeMultiplier,
                                Price = responceDepths.data[i].price.ToDecimal(),
                                Id = responceDepths.data[i].id
                            });

                            if (depth.Asks != null && depth.Asks.Count > 2 &&
                                responceDepths.data[i].price.ToDecimal() > depth.Asks[0].Price)
                            {
                                depth.Asks.RemoveAt(0);
                            }
                        }
                    }

                    ascs.Reverse();
                    depth.Asks = ascs;
                    depth.Bids = bids;
                }

                if (responceDepths.action == "update"
                    || responceDepths.action == "insert")
                {
                    if (depth == null)
                    {
                        return;
                    }

                    for (int i = 0; i < responceDepths.data.Count; i++)
                    {
                        if (responceDepths.data[i].price == null ||
                            responceDepths.data[i].price == "0")
                        {
                            continue;
                        }
                        if (responceDepths.data[i].symbol.Contains("USDT"))
                        {
                            volumeMultiplier = 1000000;
                        }

                        if (responceDepths.data[i].side == "Sell")
                        {
                            for (int j = 0; j < depth.Asks.Count; j++)
                            {
                                if (depth.Asks[j].Id == responceDepths.data[i].id
                                    && responceDepths.action == "update")
                                {
                                    depth.Asks[j].Ask = responceDepths.data[i].size.ToDecimal() / volumeMultiplier;
                                }
                                else
                                {
                                    decimal priceLevel = responceDepths.data[i].price.ToDecimal();

                                    if (j == 0 && priceLevel < depth.Asks[j].Price)
                                    {
                                        depth.Asks.Insert(j, new MarketDepthLevel()
                                        {
                                            Ask = responceDepths.data[i].size.ToDecimal() / volumeMultiplier,
                                            Price = responceDepths.data[i].price.ToDecimal(),
                                            Id = responceDepths.data[i].id
                                        });
                                    }
                                    else if (j != depth.Asks.Count - 1 && priceLevel > depth.Asks[j].Price && priceLevel < depth.Asks[j + 1].Price)
                                    {
                                        depth.Asks.Insert(j + 1, new MarketDepthLevel()
                                        {
                                            Ask = responceDepths.data[i].size.ToDecimal() / volumeMultiplier,
                                            Price = responceDepths.data[i].price.ToDecimal(),
                                            Id = responceDepths.data[i].id
                                        });
                                    }
                                    else if (j == depth.Asks.Count - 1 && priceLevel > depth.Asks[j].Price)
                                    {
                                        depth.Asks.Add(new MarketDepthLevel()
                                        {
                                            Ask = responceDepths.data[i].size.ToDecimal() / volumeMultiplier,
                                            Price = responceDepths.data[i].price.ToDecimal(),
                                            Id = responceDepths.data[i].id
                                        });
                                    }

                                    if (depth.Bids != null && depth.Bids.Count > 2 &&
                                        responceDepths.data[i].price.ToDecimal() < depth.Bids[0].Price)
                                    {
                                        depth.Bids.RemoveAt(0);
                                    }
                                }
                            }
                        }
                        else if (responceDepths.data[i].side == "Buy")
                        {
                            for (int j = 0; j < depth.Bids.Count; j++)
                            {
                                if (depth.Bids[j].Id == responceDepths.data[i].id
                                    && responceDepths.action == "update")
                                {
                                    depth.Bids[j].Bid = responceDepths.data[i].size.ToDecimal() / volumeMultiplier;
                                }
                                else
                                {
                                    decimal priceLevel = responceDepths.data[i].price.ToDecimal();

                                    if (j == 0 && priceLevel > depth.Bids[j].Price)
                                    {
                                        depth.Bids.Insert(j, new MarketDepthLevel()
                                        {
                                            Bid = responceDepths.data[i].size.ToDecimal() / volumeMultiplier,
                                            Price = responceDepths.data[i].price.ToDecimal(),
                                            Id = responceDepths.data[i].id
                                        });
                                    }
                                    else if (j != depth.Bids.Count - 1 && priceLevel < depth.Bids[j].Price && priceLevel > depth.Bids[j + 1].Price)
                                    {
                                        depth.Bids.Insert(j + 1, new MarketDepthLevel()
                                        {
                                            Bid = responceDepths.data[i].size.ToDecimal() / volumeMultiplier,
                                            Price = responceDepths.data[i].price.ToDecimal(),
                                            Id = responceDepths.data[i].id
                                        });
                                    }
                                    else if (j == depth.Bids.Count - 1 && priceLevel < depth.Bids[j].Price)
                                    {
                                        depth.Bids.Add(new MarketDepthLevel()
                                        {
                                            Bid = responceDepths.data[i].size.ToDecimal() / volumeMultiplier,
                                            Price = responceDepths.data[i].price.ToDecimal(),
                                            Id = responceDepths.data[i].id
                                        });
                                    }

                                    if (depth.Asks != null && depth.Asks.Count > 2 &&
                                        responceDepths.data[i].price.ToDecimal() > depth.Asks[0].Price)
                                    {
                                        depth.Asks.RemoveAt(0);
                                    }
                                }
                            }
                        }
                    }
                }

                if (responceDepths.action == "delete")
                {
                    if (depth == null)
                        return;

                    for (int i = 0; i < responceDepths.data.Count; i++)
                    {
                        if (responceDepths.data[i].side == "Sell")
                        {
                            for (int j = 0; j < depth.Asks.Count; j++)
                            {
                                if (depth.Asks[j].Id == responceDepths.data[i].id)
                                {
                                    depth.Asks.RemoveAt(j);
                                }
                            }
                        }
                        else
                        {
                            for (int j = 0; j < depth.Bids.Count; j++)
                            {
                                if (depth.Bids[j].Id == responceDepths.data[i].id)
                                {
                                    depth.Bids.RemoveAt(j);
                                }
                            }
                        }
                    }
                }

                depth.Time = Convert.ToDateTime(responceDepths.data[0].timestamp);

                if (depth.Time == DateTime.MinValue)
                {
                    return;
                }

                if (depth.Time == _lastTimeMd)
                {
                    _lastTimeMd = _lastTimeMd.AddMilliseconds(1);
                    depth.Time = _lastTimeMd;
                }

                _lastTimeMd = depth.Time;

                if (MarketDepthEvent != null)
                {
                    MarketDepthEvent(depth.GetCopy());
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private DateTime _lastTimeMd = DateTime.MinValue;

        private void UpdateTrade(string message)
        {
            try
            {
                BitMexTrades responceTrades = JsonConvert.DeserializeAnonymousType(message, new BitMexTrades());

                if (responceTrades.data.Count == 0
                    || responceTrades.data == null)
                {
                    return;
                }

                for (int i = 0; i < responceTrades.data.Count; i++)
                {
                    Trade trade = new Trade();
                    trade.SecurityNameCode = responceTrades.data[i].symbol;
                    trade.Price = responceTrades.data[i].price.ToDecimal();
                    trade.Id = responceTrades.data[i].trdMatchID;
                    trade.Time = Convert.ToDateTime(responceTrades.data[i].timestamp).AddMilliseconds(1);
                    trade.Volume = responceTrades.data[i].size.ToDecimal(); ;
                    trade.Side = responceTrades.data[i].side == "Buy" ? Side.Buy : Side.Sell;

                    if (NewTradesEvent != null)
                    {
                        NewTradesEvent(trade);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void UpdatePosition(string message)
        {
            try
            {
                BitMexPosition responcePositions = JsonConvert.DeserializeAnonymousType(message, new BitMexPosition());

                if (responcePositions.data.Count == 0
                    || responcePositions.data == null 
                    || _isUpdateValueBegin == false)
                {
                    return;
                }

                Portfolio portfolio = new Portfolio();
                portfolio.Number = "BitMex";
                portfolio.ValueBegin = 1;
                portfolio.ValueCurrent = 1;

                for (int i = 0; i < responcePositions.data.Count; i++)
                {
                    decimal multiplierForSecurity = GetMultiplierForSecurity(responcePositions.data[i].symbol);
                    PositionOnBoard newPos = new PositionOnBoard();

                    newPos.PortfolioName = "BitMex";
                    newPos.SecurityNameCode = responcePositions.data[i].symbol;
                    newPos.ValueBlocked = 0;
                    newPos.ValueCurrent = responcePositions.data[i].currentQty.ToDecimal() / multiplierForSecurity;

                    portfolio.SetNewPosition(newPos);
                }

                if (PortfolioEvent != null)
                {
                    PortfolioEvent(new List<Portfolio> { portfolio });
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateMyTrade(string message)
        {
            try
            {
                BitMexMyOrders responceMyTrade = JsonConvert.DeserializeAnonymousType(message, new BitMexMyOrders());

                if (responceMyTrade.data == null
                    || responceMyTrade.data.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < responceMyTrade.data.Count; i++)
                {
                    if (responceMyTrade.data[i].lastQty == null ||
                        responceMyTrade.data[i].lastQty.ToDecimal() == 0)
                    {
                        continue;
                    }

                    decimal multiplierForSecurity = GetMultiplierForSecurity(responceMyTrade.data[i].symbol);

                    MyTrade myTrade = new MyTrade();
                    myTrade.Time = Convert.ToDateTime(responceMyTrade.data[i].transactTime);
                    myTrade.NumberOrderParent = responceMyTrade.data[i].orderID;
                    myTrade.NumberTrade = responceMyTrade.data[i].clOrdID;
                    myTrade.Price = responceMyTrade.data[i].avgPx.ToDecimal();
                    myTrade.SecurityNameCode = responceMyTrade.data[i].symbol;
                    myTrade.Side = responceMyTrade.data[i].side == "Buy" ? Side.Buy : Side.Sell;
                    myTrade.Volume = responceMyTrade.data[i].lastQty.ToDecimal() / multiplierForSecurity;

                    if (MyTradeEvent != null)
                    {
                        MyTradeEvent(myTrade);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region 11 Trade

        private RateGate _rateGateOrder = new RateGate(1, TimeSpan.FromMilliseconds(350));

        public void SendOrder(Order order)
        {
            _rateGateOrder.WaitToProceed();

            try
            {
                decimal multiplierForSecurity = GetMultiplierForSecurity(order.SecurityNameCode);

                RestClient client = new RestClient(_domain);
                RestRequest request = new RestRequest("/api/v1/order", Method.POST);

                string secName = order.SecurityNameCode;
                string side = order.Side == Side.Buy ? "Buy" : "Sell";
                string volumeOrder = (order.Volume * multiplierForSecurity).ToString().Replace(",", ".");
                string typeOrder = "";
                string parameters = "";
                string price = "";

                if (order.TypeOrder == OrderPriceType.Market)
                {
                    typeOrder = "Market";
                    parameters = $"symbol={secName}&side={side}&orderQty={volumeOrder}&clOrdID={order.NumberUser}&ordType={typeOrder}";
                }
                else
                {
                    typeOrder = "Limit";
                    price = order.Price.ToString().Replace(",", ".");
                    parameters = $"symbol={secName}&side={side}&orderQty={volumeOrder}&clOrdID={order.NumberUser}&ordType={typeOrder}&timeInForce=GoodTillCancel&price={price}";
                }

                string expires = GetExpires().ToString();
                string message = "POST" + "/api/v1/order" + expires + parameters;
                byte[] signatureBytes = hmacsha256(Encoding.UTF8.GetBytes(_secKey), Encoding.UTF8.GetBytes(message));
                string signatureString = ByteArrayToString(signatureBytes);

                request.AddHeader("api-expires", expires);
                request.AddHeader("api-key", _id);
                request.AddHeader("api-signature", signatureString);
                request.AddParameter("application/x-www-form-urlencoded", parameters, ParameterType.RequestBody);

                IRestResponse json = client.Execute(request);

                if (json.StatusCode != HttpStatusCode.OK)
                {
                    CreateOrderFail(order);
                    SendLogMessage($"Http State Code: {json.StatusCode} - {json.Content}", LogMessageType.Error);
                }
                else
                {
                    DatumOrder responceOrder = JsonConvert.DeserializeAnonymousType(json.Content, new DatumOrder());
                    order.NumberMarket = responceOrder.orderID;
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {

        }

        public void CancelOrder(Order order)
        {
            _rateGateOrder.WaitToProceed();

            try
            {
                RestClient client = new RestClient(_domain);
                RestRequest request = new RestRequest("/api/v1/order", Method.DELETE);

                string secName = order.SecurityNameCode;
                string orderId = order.NumberMarket.ToString();
                string parameters = $"symbol={secName}&orderID={orderId}"; ;

                string expires = GetExpires().ToString();
                string message = "DELETE" + "/api/v1/order" + expires + parameters;
                byte[] signatureBytes = hmacsha256(Encoding.UTF8.GetBytes(_secKey), Encoding.UTF8.GetBytes(message));
                string signatureString = ByteArrayToString(signatureBytes);

                request.AddHeader("api-expires", expires);
                request.AddHeader("api-key", _id);
                request.AddHeader("api-signature", signatureString);
                request.AddParameter("application/x-www-form-urlencoded", parameters, ParameterType.RequestBody);

                IRestResponse json = client.Execute(request);

                if (json.StatusCode != HttpStatusCode.OK)
                {
                    CreateOrderFail(order);
                    SendLogMessage($"Http State Code: {json.StatusCode} - {json.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exeption)
            {
                SendLogMessage(exeption.ToString(), LogMessageType.Error);
            }
        }

        public void GetOrdersState(List<Order> orders)
        {

        }

        public void CancelAllOrders()
        {

        }

        public void GetAllActivOrders()
        {

        }

        public void GetOrderStatus(Order order)
        {
            
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            _rateGateOrder.WaitToProceed();

            try
            {
                RestClient client = new RestClient(_domain);
                RestRequest request = new RestRequest("/api/v1/order", Method.DELETE);

                string secName = security.Name;
                string parameters = $"symbol={secName}"; ;

                string expires = GetExpires().ToString();
                string message = "DELETE" + "/api/v1/order" + expires + parameters;
                byte[] signatureBytes = hmacsha256(Encoding.UTF8.GetBytes(_secKey), Encoding.UTF8.GetBytes(message));
                string signatureString = ByteArrayToString(signatureBytes);

                request.AddHeader("api-expires", expires);
                request.AddHeader("api-key", _id);
                request.AddHeader("api-signature", signatureString);
                request.AddParameter("application/x-www-form-urlencoded", parameters, ParameterType.RequestBody);

                IRestResponse json = client.Execute(request);

                if (json.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage($"Http State Code: {json.StatusCode} - {json.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exeption)
            {
                SendLogMessage(exeption.ToString(), LogMessageType.Error);
            }
        }

        private void CreateOrderFail(Order order)
        {
            order.State = OrderStateType.Fail;

            if (MyOrderEvent != null)
            {
                MyOrderEvent(order);
            }
        }

        private decimal GetMultiplierForSecurity(string security)
        {
            RestRequest requestRest = new RestRequest("/api/v1/instrument/active", Method.GET);
            IRestResponse response = new RestClient(_domain).Execute(requestRest);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                SendLogMessage($"Http State Code: {response.StatusCode} - {response.Content}", LogMessageType.Error);
                return 1;
            }

            List<BitMexSecurity> responseSecurity = JsonConvert.DeserializeObject<List<BitMexSecurity>>(response.Content);

            decimal multiplierForSecurity = 1;
            decimal underlyingToPositionMultiplier = 1;
            decimal underlyingToSettleMultiplier = 1;

            for (int i = 0; i < responseSecurity.Count; i++)
            {
                if (security == responseSecurity[i].symbol)
                {
                    underlyingToPositionMultiplier = responseSecurity[i].underlyingToPositionMultiplier.ToDecimal();
                    underlyingToSettleMultiplier = responseSecurity[i].underlyingToSettleMultiplier.ToDecimal();
                    break;
                }
            }

            if (underlyingToPositionMultiplier != 0)
            {
                multiplierForSecurity = underlyingToPositionMultiplier;
            }
            else if (underlyingToSettleMultiplier != 0)
            {
                multiplierForSecurity = underlyingToSettleMultiplier;
            }

            return multiplierForSecurity;
        }

        #endregion

        #region 12 Queries

        RateGate _rateGate = new RateGate(1, TimeSpan.FromMilliseconds(350));

        private object _queryHttpLocker = new object();

        private string Query(string method, string function, Dictionary<string, string> param = null, bool auth = false, bool json = false)
        {
            _rateGate.WaitToProceed();

            lock (_queryHttpLocker)
            {

                string paramData = json ? BuildJSON(param) : BuildQueryData(param);
                string url = "/api/v1" + function + ((method == "GET" && paramData != "") ? "?" + paramData : "");
                string postData = (method != "GET") ? paramData : "";

                HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(_domain + url);
                webRequest.Method = method;

                if (auth)
                {
                    string expires = GetExpires().ToString();
                    string message = method + url + expires + postData;
                    byte[] signatureBytes = hmacsha256(Encoding.UTF8.GetBytes(_secKey), Encoding.UTF8.GetBytes(message));
                    string signatureString = ByteArrayToString(signatureBytes);

                    webRequest.Headers.Add("api-expires", expires);
                    webRequest.Headers.Add("api-key", _id);
                    webRequest.Headers.Add("api-signature", signatureString);
                }

                try
                {
                    if (postData != "")
                    {
                        webRequest.ContentType = json ? "application/json" : "application/x-www-form-urlencoded";
                        byte[] data = Encoding.UTF8.GetBytes(postData);
                        using (Stream stream = webRequest.GetRequestStream())
                        {
                            stream.Write(data, 0, data.Length);
                        }
                    }

                    using (WebResponse webResponse = webRequest.GetResponse())
                    using (Stream str = webResponse.GetResponseStream())
                    using (StreamReader sr = new StreamReader(str))
                    {
                        return sr.ReadToEnd();
                    }
                }
                catch (WebException wex)
                {
                    using (HttpWebResponse response = (HttpWebResponse)wex.Response)
                    {
                        if (response == null)
                            throw;

                        using (Stream str = response.GetResponseStream())
                        {
                            using (StreamReader sr = new StreamReader(str))
                            {
                                return sr.ReadToEnd();
                            }
                        }
                    }
                }
            }
        }

        private string BuildQueryData(Dictionary<string, string> param)
        {
            if (param == null)
                return "";

            StringBuilder b = new StringBuilder();
            foreach (KeyValuePair<string, string> item in param)
                b.Append(string.Format("&{0}={1}", item.Key, WebUtility.UrlEncode(item.Value)));

            try { return b.ToString().Substring(1); }
            catch (Exception) { return ""; }
        }

        private string BuildJSON(Dictionary<string, string> param)
        {
            if (param == null)
                return "";

            List<string> entries = new List<string>();
            foreach (KeyValuePair<string, string> item in param)
                entries.Add(string.Format("\"{0}\":\"{1}\"", item.Key, item.Value));

            return "{" + string.Join(",", entries) + "}";
        }

        public static string ByteArrayToString(byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }

        private long GetExpires()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600; // set expires one hour in the future
        }

        private byte[] hmacsha256(byte[] keyByte, byte[] messageBytes)
        {
            using (HMACSHA256 hash = new HMACSHA256(keyByte))
            {
                return hash.ComputeHash(messageBytes);
            }
        }

        #endregion

        #region 13 Log

        public event Action<string, LogMessageType> LogMessageEvent;

        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        #endregion

    }
}
