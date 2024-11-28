/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
            catch (Exception exeption)
            {
                SendLogMessage($"Can`t run BitMex connector. No internet connection. {exeption.ToString()}", LogMessageType.Error);
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

        public ServerConnectStatus ServerStatus { get; set; }

        public ServerType ServerType
        {
            get { return ServerType.BitMex; }
        }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

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

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    List<BitMexSecurity> responseSecutity = JsonConvert.DeserializeObject<List<BitMexSecurity>>(response.Content);

                    if (responseSecutity != null
                        && responseSecutity.Count != 0)
                    {
                        List<Security> securities = new List<Security>();

                        for (int i = 0; i < responseSecutity.Count; i++)
                        {
                            BitMexSecurity newSecurity = responseSecutity[i];

                            if (newSecurity.typ == null)
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
                            security.Decimals = newSecurity.tickSize.DecimalsCount();

                            decimal lotSize = newSecurity.lotSize.ToDecimal();
                            decimal multSecurity = newSecurity.multiplier.ToDecimal();
                            decimal underlyingToPositionMultiplier = newSecurity.underlyingToPositionMultiplier.ToDecimal();
                            decimal underlyingToSettleMultiplier = newSecurity.underlyingToSettleMultiplier.ToDecimal();
                            decimal minimumTradeAmount = 0;

                            if (underlyingToPositionMultiplier != 0)
                            {
                                minimumTradeAmount = lotSize / underlyingToPositionMultiplier;
                            }
                            else if (underlyingToSettleMultiplier != 0)
                            {
                                minimumTradeAmount = multSecurity * lotSize / underlyingToSettleMultiplier;
                            }

                            string qtyInStr = minimumTradeAmount.ToStringWithNoEndZero();

                            security.DecimalsVolume = qtyInStr.DecimalsCount();

                            security.State = SecurityStateType.Activ;
                            securities.Add(security);
                        }

                        if (SecurityEvent != null)
                        {
                            SecurityEvent(securities);
                        }
                    }
                    else
                    {
                        SendLogMessage($"Securities request error: {response.Content}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Securities request error. Status: {response.StatusCode} - {response.Content}", LogMessageType.Error);
                    return;
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

        private bool firstPortfolio = false;

        public void GetPortfolios()
        {
            _rateGate.WaitToProceed();

            try
            {
                RestClient client = new RestClient(_domain);
                RestRequest request = new RestRequest("/api/v1/user/margin", Method.GET);

                request.AddParameter("currency", "all");
                string expires = GetExpires().ToString();
                string message = "GET" + "/api/v1/user/margin?currency=all" + expires;
                byte[] signatureBytes = hmacsha256(Encoding.UTF8.GetBytes(_secKey), Encoding.UTF8.GetBytes(message));
                string signatureString = ByteArrayToString(signatureBytes);

                request.AddHeader("api-expires", expires);
                request.AddHeader("api-key", _id);
                request.AddHeader("api-signature", signatureString);

                IRestResponse json = client.Execute(request);

                if (json.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    List<Datum> responsePortfolio = JsonConvert.DeserializeAnonymousType(json.Content, new List<Datum>());

                    if (responsePortfolio != null
                        && responsePortfolio.Count != 0)
                    {
                        Portfolio myPortfolio = new Portfolio();
                        myPortfolio.Number = "BitMexPortfolio";
                        myPortfolio.ValueBegin = 1;
                        myPortfolio.ValueCurrent = 1;

                        for (int i = 0; i < responsePortfolio.Count; i++)
                        {
                            decimal securityMultiplierForPortfolio = GetSecurityMultiplierForPortfolio(responsePortfolio[i].currency);

                            PositionOnBoard newPortfolio = new PositionOnBoard();

                            newPortfolio.SecurityNameCode = responsePortfolio[i].currency;
                            newPortfolio.PortfolioName = "BitMexPortfolio";
                            newPortfolio.ValueBegin = responsePortfolio[i].walletBalance.ToDecimal() / securityMultiplierForPortfolio;

                            if (responsePortfolio[i].marginBalance.ToDecimal() == responsePortfolio[i].walletBalance.ToDecimal())
                            {
                                newPortfolio.ValueCurrent = responsePortfolio[i].availableMargin.ToDecimal() / securityMultiplierForPortfolio;
                            }
                            else
                            {
                                newPortfolio.ValueCurrent = responsePortfolio[i].marginBalance.ToDecimal() / securityMultiplierForPortfolio;
                            }

                            newPortfolio.ValueBlocked = responsePortfolio[i].initMargin.ToDecimal() / securityMultiplierForPortfolio;

                            myPortfolio.SetNewPosition(newPortfolio);
                        }

                        PortfolioEvent(new List<Portfolio> { myPortfolio });

                        firstPortfolio = true;
                    }
                    else
                    {
                        SendLogMessage($"Portfolio request error: {json.Content}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Portfolio request error. Status: {json.StatusCode}, {json.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exeption)
            {
                SendLogMessage(exeption.ToString(), LogMessageType.Error);
            }
        }

        private decimal GetSecurityMultiplierForPortfolio(string security)
        {
            _rateGate.WaitToProceed();

            try
            {
                RestRequest requestRest = new RestRequest("/api/v1/wallet/assets", Method.GET);
                IRestResponse response = new RestClient(_domain).Execute(requestRest);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage($"Http State Code: {response.StatusCode} - {response.Content}", LogMessageType.Error);
                    return 1;
                }

                decimal securityMultiplierForPortfolio = 1;
                int scale = 0;

                List<BitMexAsset> responseAsset = JsonConvert.DeserializeAnonymousType(response.Content, new List<BitMexAsset>());

                for (int i = 0; i < responseAsset.Count; i++)
                {
                    if (responseAsset[i].currency == security)
                    {
                        scale = Convert.ToInt32(responseAsset[i].scale);
                    }
                }

                securityMultiplierForPortfolio = 1 * (int)Math.Pow(10, scale);

                return securityMultiplierForPortfolio;
            }
            catch (Exception exeption)
            {
                SendLogMessage(exeption.ToString(), LogMessageType.Error);
                return 1;
            }
        }

        public event Action<List<Portfolio>> PortfolioEvent;

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

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder,
           DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if (startTime >= endTime ||
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

        private List<Candle> GetCandlesTf(string security, string tf, DateTime startTime, DateTime timeEnd)
        {
            _rateGate.WaitToProceed();

            try
            {
                List<BitMexCandle> allBitMexCandles = new List<BitMexCandle>();

                List<Candle> allCandles = new List<Candle>();

                string end = timeEnd.ToString("yyyy-MM-dd HH:mm:ss");
                string start = startTime.ToString("yyyy-MM-dd HH:mm:ss");

                RestClient client = new RestClient(_domain);
                RestRequest request = new RestRequest("/api/v1/trade/bucketed", Method.GET);

                request.AddParameter("binSize", tf);
                request.AddParameter("partial", true.ToString());
                request.AddParameter("symbol", security);
                request.AddParameter("count", 10000.ToString());
                request.AddParameter("reverse", true.ToString());
                request.AddParameter("startTime", start);
                request.AddParameter("endTime", end);

                IRestResponse json = client.Execute(request);

                if (json.StatusCode == HttpStatusCode.OK)
                {
                    List<BitMexCandle> responseCandles =
                 JsonConvert.DeserializeAnonymousType(json.Content, new List<BitMexCandle>());

                    if (responseCandles != null)
                    {
                        allBitMexCandles.AddRange(responseCandles);

                        for (int i = 0; i < allBitMexCandles.Count; i++)
                        {
                            Candle newCandle = new Candle();

                            if (allBitMexCandles[i].open.ToDecimal() < allBitMexCandles[i].high.ToDecimal())
                            {
                                newCandle.Open = allBitMexCandles[i].open.ToDecimal();
                                newCandle.High = allBitMexCandles[i].high.ToDecimal();
                            }
                            else
                            {
                                newCandle.Open = allBitMexCandles[i].high.ToDecimal();
                                newCandle.High = allBitMexCandles[i].open.ToDecimal();
                            }

                            if (allBitMexCandles[i].open.ToDecimal() > allBitMexCandles[i].low.ToDecimal())
                            {
                                newCandle.Open = allBitMexCandles[i].open.ToDecimal();
                                newCandle.Low = allBitMexCandles[i].low.ToDecimal();
                            }
                            else
                            {
                                newCandle.Open = allBitMexCandles[i].low.ToDecimal();
                                newCandle.Low = allBitMexCandles[i].open.ToDecimal();
                            }

                            newCandle.Close = allBitMexCandles[i].close.ToDecimal();
                            newCandle.TimeStart = Convert.ToDateTime(allBitMexCandles[i].timestamp);
                            newCandle.Volume = allBitMexCandles[i].volume.ToDecimal();

                            allCandles.Add(newCandle);
                        }

                        allCandles.Reverse();
                        return allCandles;
                    }
                    else
                    {
                        SendLogMessage("Empty Candles request error. Status: " + json.StatusCode, LogMessageType.Error);
                        return null;
                    }
                }
                else
                {
                    SendLogMessage($"Candles request error: {json.StatusCode} - {json.Content}", LogMessageType.Error);
                    return null;
                }
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
            if (startTime >= endTime ||
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

                RestClient client = new RestClient(_domain);
                RestRequest request = new RestRequest("/api/v1/trade", Method.GET);

                request.AddParameter("symbol", security);
                request.AddParameter("start", 0.ToString());
                request.AddParameter("reverse", true.ToString());
                request.AddParameter("startTime", start);
                request.AddParameter("endTime", end);
                request.AddParameter("count", 1000.ToString());

                IRestResponse json = client.Execute(request);

                if (json.StatusCode == HttpStatusCode.OK)
                {
                    List<DatumTrades> tradeHistoryResponse = JsonConvert.DeserializeAnonymousType(json.Content, new List<DatumTrades>());

                    if (tradeHistoryResponse != null)
                    {
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
                    else
                    {
                        SendLogMessage("Empty Trades request error. Status: " + json.StatusCode, LogMessageType.Error);
                        return null;
                    }
                }
                else
                {
                    SendLogMessage($"Trades request error: {json.StatusCode} - {json.Content}", LogMessageType.Error);
                    return null;
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Trades request error:" + exception.ToString(), LogMessageType.Error);
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

        private void PassAuthenticationWebSocket()
        {
            string nonce = GetExpires().ToString();
            byte[] signatureBytes = hmacsha256(Encoding.UTF8.GetBytes(_secKey), Encoding.UTF8.GetBytes("GET/realtime" + nonce));
            string signatureString = ByteArrayToString(signatureBytes);

            _webSocket.Send("{\"op\": \"authKeyExpires\", \"args\": [\"" + _id + "\"," + nonce + ",\"" + signatureString + "\"]}");
        }

        #endregion

        #region 7 WebSocket events

        private void _webSocket_Error(object sender, SuperSocket.ClientEngine.ErrorEventArgs e)
        {
            SuperSocket.ClientEngine.ErrorEventArgs error = (SuperSocket.ClientEngine.ErrorEventArgs)e;

            if (error.Exception != null)
            {
                SendLogMessage(error.Exception.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocket_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

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
            catch (Exception exeption)
            {
                SendLogMessage(exeption.ToString(), LogMessageType.Error);
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
            PassAuthenticationWebSocket();

            SendLogMessage("Connection Open", LogMessageType.Connect);

            if (ServerStatus != ServerConnectStatus.Connect
                && _webSocket != null
                && _webSocket.State == WebSocketState.Open)
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
                catch (Exception exeption)
                {
                    SendLogMessage(exeption.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        #endregion

        #region 9 Security subscrible

        private List<string> _subscribedSec = new List<string>();

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

        #endregion

        #region 10 WebSocket parsing the messages

        private ConcurrentQueue<string> _fifoListWebSocketMessage = new ConcurrentQueue<string>();

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
                catch (Exception exeption)
                {
                    SendLogMessage(exeption.ToString(), LogMessageType.Error);
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
                    responceOrder.data.Count == 0
                    || responceOrder.action == "partial")
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
                    newOrder.TimeCallBack = Convert.ToDateTime(item.timestamp);
                    newOrder.TimeCreate = Convert.ToDateTime(item.timestamp);
                    newOrder.NumberUser = Convert.ToInt32(item.clOrdID);
                    newOrder.NumberMarket = item.orderID.ToString();
                    newOrder.Side = item.side.Equals("Buy") ? Side.Buy : Side.Sell;
                    newOrder.State = stateType;
                    newOrder.Volume = item.orderQty.ToDecimal() / multiplierForSecurity;
                    newOrder.Price = item.price.ToDecimal();
                    newOrder.ServerType = ServerType.BitMex;
                    newOrder.PortfolioNumber = "BitMexPortfolio";
                    newOrder.SecurityClassCode = item.symbol;
                    newOrder.TypeOrder = item.ordType == "Limit"
                            ? OrderPriceType.Limit
                            : OrderPriceType.Market;

                    MyOrderEvent(newOrder);

                }
            }
            catch (Exception exeption)
            {
                SendLogMessage(exeption.ToString(), LogMessageType.Error);
            }
        }

        private void UpdatePortfolio(string message)
        {
            try
            {
                BitMexPortfolio responcePortfolio = JsonConvert.DeserializeAnonymousType(message, new BitMexPortfolio());

                if (responcePortfolio.data == null ||
                    responcePortfolio.data.Count == 0
                    || responcePortfolio.action == "partial"
                    || !firstPortfolio)
                {
                    return;
                }

                Portfolio portfolio = new Portfolio();
                portfolio.Number = "BitMexPortfolio";
                portfolio.ValueBegin = 1;
                portfolio.ValueCurrent = 1;

                for (int i = 0; i < responcePortfolio.data.Count; i++)
                {
                    PositionOnBoard pos = new PositionOnBoard();

                    pos.SecurityNameCode = responcePortfolio.data[i].currency;

                    decimal securityMultiplierForPortfolio = GetSecurityMultiplierForPortfolio(pos.SecurityNameCode);

                    pos.ValueBegin = responcePortfolio.data[i].availableMargin.ToDecimal() / securityMultiplierForPortfolio;

                    if (responcePortfolio.data[i].marginBalance.ToDecimal() != 0)
                    {
                        pos.ValueCurrent = responcePortfolio.data[i].marginBalance.ToDecimal() / securityMultiplierForPortfolio;
                    }
                    else
                    {
                        pos.ValueCurrent = responcePortfolio.data[i].availableMargin.ToDecimal() / securityMultiplierForPortfolio;
                    }

                    pos.ValueBlocked = responcePortfolio.data[i].initMargin.ToDecimal() / securityMultiplierForPortfolio;

                    portfolio.SetNewPosition(pos);
                }

                PortfolioEvent(new List<Portfolio> { portfolio });
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

                        if (responceDepths.data[i].side == "Sell")
                        {
                            ascs.Add(new MarketDepthLevel()
                            {
                                Ask = responceDepths.data[i].size.ToDecimal(),
                                Price = responceDepths.data[i].price.ToDecimal(),
                                Id = Convert.ToInt64(responceDepths.data[i].id)
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
                                Bid = responceDepths.data[i].size.ToDecimal(),
                                Price = responceDepths.data[i].price.ToDecimal(),
                                Id = Convert.ToInt64(responceDepths.data[i].id)
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

                        if (responceDepths.data[i].side == "Sell")
                        {
                            for (int j = 0; j < depth.Asks.Count; j++)
                            {
                                if (depth.Asks[j].Id == Convert.ToInt64(responceDepths.data[i].id)
                                    && responceDepths.action == "update")
                                {
                                    depth.Asks[j].Ask = responceDepths.data[i].size.ToDecimal();
                                }
                                else
                                {
                                    decimal priceLevel = responceDepths.data[i].price.ToDecimal();

                                    if (j == 0 && priceLevel < depth.Asks[j].Price)
                                    {
                                        depth.Asks.Insert(j, new MarketDepthLevel()
                                        {
                                            Ask = responceDepths.data[i].size.ToDecimal(),
                                            Price = responceDepths.data[i].price.ToDecimal(),
                                            Id = Convert.ToInt64(responceDepths.data[i].id)
                                        });
                                    }
                                    else if (j != depth.Asks.Count - 1 && priceLevel > depth.Asks[j].Price && priceLevel < depth.Asks[j + 1].Price)
                                    {
                                        depth.Asks.Insert(j + 1, new MarketDepthLevel()
                                        {
                                            Ask = responceDepths.data[i].size.ToDecimal(),
                                            Price = responceDepths.data[i].price.ToDecimal(),
                                            Id = Convert.ToInt64(responceDepths.data[i].id)
                                        });
                                    }
                                    else if (j == depth.Asks.Count - 1 && priceLevel > depth.Asks[j].Price)
                                    {
                                        depth.Asks.Add(new MarketDepthLevel()
                                        {
                                            Ask = responceDepths.data[i].size.ToDecimal(),
                                            Price = responceDepths.data[i].price.ToDecimal(),
                                            Id = Convert.ToInt64(responceDepths.data[i].id)
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
                                if (depth.Bids[j].Id == Convert.ToInt64(responceDepths.data[i].id)
                                    && responceDepths.action == "update")
                                {
                                    depth.Bids[j].Bid = responceDepths.data[i].size.ToDecimal();
                                }
                                else
                                {
                                    decimal priceLevel = responceDepths.data[i].price.ToDecimal();

                                    if (j == 0 && priceLevel > depth.Bids[j].Price)
                                    {
                                        depth.Bids.Insert(j, new MarketDepthLevel()
                                        {
                                            Bid = responceDepths.data[i].size.ToDecimal(),
                                            Price = responceDepths.data[i].price.ToDecimal(),
                                            Id = Convert.ToInt64(responceDepths.data[i].id)
                                        });
                                    }
                                    else if (j != depth.Bids.Count - 1 && priceLevel < depth.Bids[j].Price && priceLevel > depth.Bids[j + 1].Price)
                                    {
                                        depth.Bids.Insert(j + 1, new MarketDepthLevel()
                                        {
                                            Bid = responceDepths.data[i].size.ToDecimal(),
                                            Price = responceDepths.data[i].price.ToDecimal(),
                                            Id = Convert.ToInt64(responceDepths.data[i].id)
                                        });
                                    }
                                    else if (j == depth.Bids.Count - 1 && priceLevel < depth.Bids[j].Price)
                                    {
                                        depth.Bids.Add(new MarketDepthLevel()
                                        {
                                            Bid = responceDepths.data[i].size.ToDecimal(),
                                            Price = responceDepths.data[i].price.ToDecimal(),
                                            Id = Convert.ToInt64(responceDepths.data[i].id)
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
                                if (depth.Asks[j].Id == Convert.ToInt64(responceDepths.data[i].id))
                                {
                                    depth.Asks.RemoveAt(j);
                                }
                            }
                        }
                        else
                        {
                            for (int j = 0; j < depth.Bids.Count; j++)
                            {
                                if (depth.Bids[j].Id == Convert.ToInt64(responceDepths.data[i].id))
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

                    NewTradesEvent(trade);
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
                    || responcePositions.action == "partial"
                    || !firstPortfolio)
                {
                    return;
                }

                Portfolio portfolio = new Portfolio();
                portfolio.Number = "BitMexPortfolio";
                portfolio.ValueBegin = 1;
                portfolio.ValueCurrent = 1;

                for (int i = 0; i < responcePositions.data.Count; i++)
                {
                    decimal multiplierForSecurity = GetMultiplierForSecurity(responcePositions.data[i].symbol);

                    PositionOnBoard newPos = new PositionOnBoard();

                    newPos.PortfolioName = "BitMexPortfolio";
                    newPos.SecurityNameCode = responcePositions.data[i].symbol;
                    newPos.ValueBlocked = 0;
                    newPos.ValueCurrent = responcePositions.data[i].currentQty.ToDecimal() / multiplierForSecurity;

                    portfolio.SetNewPosition(newPos);
                }

                PortfolioEvent(new List<Portfolio> { portfolio });
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
                BitMexMyTrade responceMyTrade = JsonConvert.DeserializeAnonymousType(message, new BitMexMyTrade());

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
                    myTrade.NumberTrade = responceMyTrade.data[i].execID;
                    myTrade.Price = responceMyTrade.data[i].avgPx.ToDecimal();
                    myTrade.SecurityNameCode = responceMyTrade.data[i].symbol;
                    myTrade.Side = responceMyTrade.data[i].side == "Buy" ? Side.Buy : Side.Sell;
                    myTrade.Volume = responceMyTrade.data[i].lastQty.ToDecimal() / multiplierForSecurity;

                    MyTradeEvent(myTrade);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        #endregion

        #region 11 Trade

        private RateGate _rateGateOrder = new RateGate(1, TimeSpan.FromMilliseconds(350));

        private RateGate _rateGateCancelOrder = new RateGate(1, TimeSpan.FromMilliseconds(350));

        private RateGate _rateGateGetOrder = new RateGate(1, TimeSpan.FromMilliseconds(350));

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
                    SendLogMessage($"Order created, but answer is wrong: {json.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exeption)
            {
                SendLogMessage(exeption.ToString(), LogMessageType.Error);
            }
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {

        }

        public void CancelOrder(Order order)
        {
            _rateGateCancelOrder.WaitToProceed();

            try
            {
                RestClient client = new RestClient(_domain);
                RestRequest request = new RestRequest("/api/v1/order", Method.DELETE);

                string secName = order.SecurityNameCode;
                string orderId = order.NumberMarket.ToString();
                string parameters = $"symbol={secName}&orderID={orderId}";

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
                    SendLogMessage($"Cancel order failed. Status: {json.StatusCode} - {json.Content}", LogMessageType.Error);
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
            List<Order> orders = GetAllOpenOrders();

            if (orders == null)
            {
                return;
            }

            for (int i = 0; i < orders.Count; i++)
            {
                Order order = orders[i];

                if (order.State == OrderStateType.Active)
                {
                    CancelOrder(order);
                }
            }
        }

        public void GetAllActivOrders()
        {
            List<Order> orders = GetAllOpenOrders();

            if (orders == null)
            {
                return;
            }

            for (int i = 0; i < orders.Count; i++)
            {
                if (MyOrderEvent != null)
                {
                    MyOrderEvent(orders[i]);
                }
            }
        }

        public void GetOrderStatus(Order order)
        {
            Order orderFromExchange = GetOrderFromExchange(order.NumberUser.ToString());

            if (orderFromExchange == null)
            {
                return;
            }

            if (MyOrderEvent != null)
            {
                MyOrderEvent(orderFromExchange);
            }

            if (orderFromExchange.State == OrderStateType.Done
                || orderFromExchange.State == OrderStateType.Partial)
            {
                FindMyTradesToOrder(orderFromExchange);
            }
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            _rateGateCancelOrder.WaitToProceed();

            try
            {
                RestClient client = new RestClient(_domain);
                RestRequest request = new RestRequest("/api/v1/order/all", Method.DELETE);

                string secName = security.Name;
                string parameters = $"symbol={secName}";

                string expires = GetExpires().ToString();
                string message = "DELETE" + "/api/v1/order/all" + expires + parameters;
                byte[] signatureBytes = hmacsha256(Encoding.UTF8.GetBytes(_secKey), Encoding.UTF8.GetBytes(message));
                string signatureString = ByteArrayToString(signatureBytes);

                request.AddHeader("api-expires", expires);
                request.AddHeader("api-key", _id);
                request.AddHeader("api-signature", signatureString);
                request.AddParameter("application/x-www-form-urlencoded", parameters, ParameterType.RequestBody);

                IRestResponse json = client.Execute(request);

                if (json.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage($"Cancel all Orders to security failed. Status: {json.StatusCode} - {json.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exeption)
            {
                SendLogMessage(exeption.ToString(), LogMessageType.Error);
            }
        }

        private void FindMyTradesToOrder(Order order)
        {
            _rateGateGetOrder.WaitToProceed();

            try
            {
                Dictionary<string, string> param = new Dictionary<string, string>();
                param.Add("reverse", true.ToString());
                param.Add("count", 50.ToString());

                RestClient client = new RestClient(_domain);
                RestRequest request = new RestRequest("/api/v1/execution", Method.GET);

                string paramData = BuildQueryData(param);

                request.AddParameter("reverse", true.ToString());
                request.AddParameter("count", 50.ToString());

                string expires = GetExpires().ToString();
                string message = "GET" + "/api/v1/execution?" + paramData + expires;
                byte[] signatureBytes = hmacsha256(Encoding.UTF8.GetBytes(_secKey), Encoding.UTF8.GetBytes(message));
                string signatureString = ByteArrayToString(signatureBytes);

                request.AddHeader("api-expires", expires);
                request.AddHeader("api-key", _id);
                request.AddHeader("api-signature", signatureString);

                IRestResponse json = client.Execute(request);

                if (json.StatusCode == HttpStatusCode.OK)
                {
                    List<DatumMyTrade> responceMyTrade = JsonConvert.DeserializeAnonymousType(json.Content, new List<DatumMyTrade>());

                    if (responceMyTrade == null
                        || responceMyTrade.Count == 0)
                    {
                        return;
                    }

                    for (int i = 0; i < responceMyTrade.Count; i++)
                    {
                        if (responceMyTrade[i].clOrdID == order.NumberUser.ToString())
                        {
                            if (responceMyTrade[i].lastQty == null ||
                        responceMyTrade[i].lastQty.ToDecimal() == 0)
                            {
                                continue;
                            }

                            decimal multiplierForSecurity = GetMultiplierForSecurity(responceMyTrade[i].symbol);

                            MyTrade myTrade = new MyTrade();
                            myTrade.Time = Convert.ToDateTime(responceMyTrade[i].transactTime);
                            myTrade.NumberOrderParent = responceMyTrade[i].orderID;
                            myTrade.NumberTrade = responceMyTrade[i].execID;
                            myTrade.Price = responceMyTrade[i].avgPx.ToDecimal();
                            myTrade.SecurityNameCode = responceMyTrade[i].symbol;
                            myTrade.Side = responceMyTrade[i].side == "Buy" ? Side.Buy : Side.Sell;
                            myTrade.Volume = responceMyTrade[i].lastQty.ToDecimal() / multiplierForSecurity;

                            MyTradeEvent(myTrade);
                            break;
                        }
                    }
                }
                else
                {
                    SendLogMessage($"Get all orders myTrade error: {json.StatusCode} - {json.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exeption)
            {
                SendLogMessage(exeption.Message, LogMessageType.Error);
            }
        }

        private List<Order> GetAllOpenOrders()
        {
            _rateGateGetOrder.WaitToProceed();

            try
            {
                Dictionary<string, string> param = new Dictionary<string, string>();
                param.Add("reverse", true.ToString());
                param.Add("count", 50.ToString());
                param.Add("filter", "{\"open\":true}");

                RestClient client = new RestClient(_domain);
                RestRequest request = new RestRequest("/api/v1/order", Method.GET);

                string paramData = BuildQueryData(param);

                request.AddParameter("reverse", true.ToString());
                request.AddParameter("count", 50.ToString());
                request.AddParameter("filter", "{\"open\":true}");

                string expires = GetExpires().ToString();
                string message = "GET" + "/api/v1/order?" + paramData + expires;
                byte[] signatureBytes = hmacsha256(Encoding.UTF8.GetBytes(_secKey), Encoding.UTF8.GetBytes(message));
                string signatureString = ByteArrayToString(signatureBytes);

                request.AddHeader("api-expires", expires);
                request.AddHeader("api-key", _id);
                request.AddHeader("api-signature", signatureString);

                IRestResponse json = client.Execute(request);

                if (json.StatusCode == HttpStatusCode.OK)
                {
                    List<DatumOrder> responceOrder = JsonConvert.DeserializeAnonymousType(json.Content, new List<DatumOrder>());

                    if (responceOrder != null &&
                        responceOrder.Count != 0)
                    {
                        List<Order> orders = new List<Order>();

                        List<Order> newOrders = new List<Order>();

                        for (int i = 0; i < responceOrder.Count; i++)
                        {
                            DatumOrder item = responceOrder[i];

                            if (item.ordStatus != "New")
                            {
                                continue;
                            }

                            if (string.IsNullOrEmpty(item.orderID))
                            {
                                continue;
                            }

                            decimal multiplierForSecurity = GetMultiplierForSecurity(responceOrder[i].symbol);

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
                            newOrder.PortfolioNumber = "BitMexPortfolio";
                            newOrder.SecurityClassCode = item.symbol;
                            newOrder.TypeOrder = item.ordType == "Limit"
                                ? OrderPriceType.Limit
                                : OrderPriceType.Market;

                            orders.Add(newOrder);
                        }

                        return orders;
                    }
                }
                else
                {
                    SendLogMessage($"Get all orders request error: {json.StatusCode} - {json.Content}", LogMessageType.Error);
                }
                return null;
            }
            catch (Exception exeption)
            {
                SendLogMessage(exeption.Message, LogMessageType.Error);
                return null;
            }
        }

        private Order GetOrderFromExchange(string numberUser)
        {
            _rateGateGetOrder.WaitToProceed();

            if (string.IsNullOrEmpty(numberUser))
            {
                SendLogMessage("Order ID is empty", LogMessageType.Error);
                return null;
            }

            try
            {
                Dictionary<string, string> param = new Dictionary<string, string>();
                param.Add("reverse", true.ToString());
                param.Add("count", 50.ToString());

                RestClient client = new RestClient(_domain);
                RestRequest request = new RestRequest("/api/v1/order", Method.GET);

                string paramData = BuildQueryData(param);

                request.AddParameter("reverse", true.ToString());
                request.AddParameter("count", 50.ToString());

                string expires = GetExpires().ToString();
                string message = "GET" + "/api/v1/order?" + paramData + expires;
                byte[] signatureBytes = hmacsha256(Encoding.UTF8.GetBytes(_secKey), Encoding.UTF8.GetBytes(message));
                string signatureString = ByteArrayToString(signatureBytes);

                request.AddHeader("api-expires", expires);
                request.AddHeader("api-key", _id);
                request.AddHeader("api-signature", signatureString);

                IRestResponse json = client.Execute(request);

                if (json.StatusCode == HttpStatusCode.OK)
                {
                    List<DatumOrder> responceOrder = JsonConvert.DeserializeAnonymousType(json.Content, new List<DatumOrder>());

                    if (responceOrder != null &&
                        responceOrder.Count != 0)
                    {

                        Order newOrder = new Order();

                        for (int i = 0; i < responceOrder.Count; i++)
                        {
                            DatumOrder item = responceOrder[i];

                            if (string.IsNullOrEmpty(item.orderID))
                            {
                                continue;
                            }

                            if (responceOrder[i].clOrdID == numberUser)
                            {
                                decimal multiplierForSecurity = GetMultiplierForSecurity(responceOrder[i].symbol);

                                OrderStateType stateType = GetOrderState(item.ordStatus);

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
                                newOrder.PortfolioNumber = "BitMexPortfolio";
                                newOrder.SecurityClassCode = item.symbol;
                                newOrder.TypeOrder = item.ordType == "Limit"
                                    ? OrderPriceType.Limit
                                    : OrderPriceType.Market;
                            }
                        }

                        return newOrder;
                    }
                }
                else
                {
                    SendLogMessage($"Get status order request error: {json.StatusCode} - {json.Content}", LogMessageType.Error);
                }
                return null;
            }
            catch (Exception exeption)
            {
                SendLogMessage(exeption.Message, LogMessageType.Error);
                return null;
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

        private decimal GetMultiplierForSecurity(string security)
        {
            _rateGate.WaitToProceed();

            try
            {
                RestRequest requestRest = new RestRequest("/api/v1/instrument/active", Method.GET);
                IRestResponse response = new RestClient(_domain).Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
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
                else
                {
                    SendLogMessage($"Http State Code: {response.StatusCode} - {response.Content}", LogMessageType.Error);
                    return 1;
                }

            }
            catch (Exception exeption)
            {
                SendLogMessage(exeption.Message, LogMessageType.Error);
                return 1;
            }
        }

        #endregion

        #region 12 Helpers

        RateGate _rateGate = new RateGate(1, TimeSpan.FromMilliseconds(350));

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
