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

            Uri uri = new Uri(_domain + "/api/v1");
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12
                    | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

                var httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);
                HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();

            }
            catch (Exception ex)
            {
                SendLogMessage($"Can`t run BitMex connector. No internet connection. {ex.ToString()}", LogMessageType.Error);
                return;
            }

            CreateWebSocketConnection();
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

            DeleteWebsocketConnection();
            _subscribedSec.Clear();
            _securities = new List<Security>();
            _depths.Clear();
            _fifoListWebSocketMessage = new ConcurrentQueue<string>();
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

        private List<Security> _securities;

        public void GetSecurities()
        {
            try
            {
                string res11 = Query("GET", "/instrument/active");
                List<BitMexSecurity> listSec = JsonConvert.DeserializeObject<List<BitMexSecurity>>(res11);

                if (_securities == null)
                {
                    _securities = new List<Security>();
                }

                for (int i = 0; i < listSec.Count; i++)
                {
                    BitMexSecurity sec = listSec[i];

                    if (sec.state != "Open")
                    {
                        continue;
                    }

                    Security security = new Security();
                    security.Exchange = ServerType.BitMex.ToString();
                    security.Name = sec.symbol;
                    security.NameFull = sec.symbol;
                    security.NameClass = sec.typ;
                    security.NameId = sec.symbol + sec.listing;
                    security.SecurityType = SecurityType.CurrencyPair;
                    security.Lot = 1;
                    security.PriceStep = sec.tickSize.ToDecimal();
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

                    if (sec.lotSize != null)
                    {
                        decimal lotSize = sec.lotSize.ToDecimal();
                        decimal mult = sec.multiplier.ToDecimal();

                        if (sec.quoteCurrency != "USD" && sec.quoteCurrency != "USDC" && sec.typ != "FFICSX")
                        {
                            decimal underlyingToPositionMultiplier = sec.underlyingToPositionMultiplier.ToDecimal();
                            decimal underlyingToSettleMultiplier = sec.underlyingToSettleMultiplier.ToDecimal();
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
                    _securities.Add(security);
                }

                if (SecurityEvent != null)
                {
                    SecurityEvent(_securities);
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

        public void GetPortfolios()
        {
            try
            {
                string res = Query("GET", "/user/margin?currency=all", null, true);

                if (res == null)
                {
                    return;
                }

                List<Datum> resp = JsonConvert.DeserializeAnonymousType(res, new List<Datum>());

                Portfolio myPortfolio = new Portfolio();
                myPortfolio.Number = "BitMex";
                myPortfolio.ValueBegin = 1;
                myPortfolio.ValueCurrent = 1;

                for (int i = 0; i < resp.Count; i++)
                {
                    PositionOnBoard newPortf = new PositionOnBoard();
                    newPortf.SecurityNameCode = resp[i].currency;
                    newPortf.ValueBegin = resp[i].walletBalance.ToDecimal() / 1000000;

                    if (resp[i].marginBalance.ToDecimal() == resp[i].walletBalance.ToDecimal())
                    {
                        newPortf.ValueCurrent = resp[i].availableMargin.ToDecimal() / 1000000;
                    }
                    else
                    {
                        newPortf.ValueCurrent = resp[i].marginBalance.ToDecimal() / 1000000;
                    }

                    newPortf.ValueBlocked = newPortf.ValueBegin - resp[i].availableMargin.ToDecimal() / 1000000;
                    newPortf.PortfolioName = "BitMex";
                    myPortfolio.SetNewPosition(newPortf);
                }

                if (PortfolioEvent != null)
                {
                    PortfolioEvent(new List<Portfolio> { myPortfolio });
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
            int countLoad = GetCountCandlesToLoad();
            int countCandle = countLoad > candleCount ? countLoad : candleCount;

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
            List<Candle> candles = new List<Candle>();

            if (actualTime > endTime ||
                startTime >= endTime ||
                startTime >= DateTime.UtcNow ||
                actualTime > endTime ||
                actualTime > DateTime.UtcNow)
            {
                return null;
            }

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
                    continue;
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

        private List<Candle> _candles;

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
            try
            {
                List<BitMexCandle> allbmcandles = new List<BitMexCandle>();

                _candles = null;

                string end = timeEnd.ToString("yyyy-MM-dd HH:mm");
                string start = startTime.ToString("yyyy-MM-dd HH:mm");

                var param = new Dictionary<string, string>();
                param.Add("binSize", tf);
                param.Add("partial", true.ToString());
                param.Add("symbol", security);
                param.Add("count", 10000.ToString());
                param.Add("reverse", true.ToString());
                param.Add("startTime", start);
                param.Add("endTime", end);

                try
                {
                    var res = Query("GET", "/trade/bucketed", param);

                    if (res == "[]")
                    {
                        return null;
                    }

                    List<BitMexCandle> bmcandles =
                        JsonConvert.DeserializeAnonymousType(res, new List<BitMexCandle>());

                    allbmcandles.AddRange(bmcandles);
                }
                catch
                {
                    // ignored
                }

                if (_candles == null)
                {
                    _candles = new List<Candle>();
                }

                for (int i = 0; i < allbmcandles.Count; i++)
                {
                    Candle newCandle = new Candle();

                    if (allbmcandles[i].open < allbmcandles[i].high)
                    {
                        newCandle.Open = allbmcandles[i].open;
                        newCandle.High = allbmcandles[i].high;
                    }
                    else
                    {
                        newCandle.Open = allbmcandles[i].high;
                        newCandle.High = allbmcandles[i].open;
                    }

                    if (allbmcandles[i].open > allbmcandles[i].low)
                    {
                        newCandle.Open = allbmcandles[i].open;
                        newCandle.Low = allbmcandles[i].low;
                    }
                    else
                    {
                        newCandle.Open = allbmcandles[i].low;
                        newCandle.Low = allbmcandles[i].open;
                    }

                    newCandle.Close = allbmcandles[i].close;
                    newCandle.TimeStart = Convert.ToDateTime(allbmcandles[i].timestamp);
                    newCandle.Volume = allbmcandles[i].volume;

                    _candles.Add(newCandle);
                }

                _candles.Reverse();
                return _candles;
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

            List<Candle> candlestf = new List<Candle>();

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
                    candlestf.Add(newCandle);
                }

                if (count == a)
                {
                    newCandle.Close = oldCandles[i].Close;
                    newCandle.State = CandleState.Finished;
                    candlestf.Add(newCandle);
                    count = 0;
                }
            }

            for (int i = 1; candlestf != null && i < candlestf.Count; i++)
            {
                if (candlestf[i - 1].TimeStart == candlestf[i].TimeStart)
                {
                    candlestf.RemoveAt(i);
                    i--;
                }
            }

            return candlestf;
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime lastDate)
        {
            if (lastDate > endTime ||
                startTime >= endTime ||
                startTime >= DateTime.UtcNow ||
                lastDate > endTime ||
                lastDate > DateTime.UtcNow)
            {
                return null;
            }

            List<Trade> allTrades = new List<Trade>();

            List<Trade> trades = GetTickHistoryToSecurity(security.Name, startTime, endTime);

            if (trades == null ||
                    trades.Count == 0)
            {
                return null;
            }

            allTrades.AddRange(trades);
            Trade lastTrade = trades[trades.Count - 1];
            //lastTrade.Time = TimeZoneInfo.ConvertTimeToUtc(lastTrade.Time);

            while (lastTrade.Time > startTime)
            {
                // lastDate = TimeZoneInfo.ConvertTimeToUtc(lastDate);
                trades = GetTickHistoryToSecurity(security.Name, startTime, lastTrade.Time);

                if (trades.Count == 0)
                {
                    break;
                }

                lastTrade = trades[trades.Count - 1];

                if (trades != null && allTrades.Count != 0 && trades.Count != 0)
                {
                    for (int i = 0; i < trades.Count; i++)
                    {
                        if (allTrades[0].Time <= trades[i].Time
                            || allTrades[0].Id == trades[i].Id)
                        {
                            trades.RemoveAt(i);
                            i--;
                        }
                    }
                }

                if (trades.Count == 0)
                {
                    Thread.Sleep(5000);
                    continue;
                }

                allTrades.InsertRange(allTrades.Count, trades);
                Thread.Sleep(3000);
            }

            allTrades.Reverse();

            return allTrades;
        }

        public List<Trade> GetTickHistoryToSecurity(string security, DateTime startTime, DateTime endTime)
        {
            try
            {
                List<Trade> trades = new List<Trade>();

                string start = startTime.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string end = endTime.ToString("yyyy-MM-dd HH:mm:ss.fff");

                Dictionary<string, string> param = new Dictionary<string, string>();
                param.Add("symbol", security);
                param.Add("start", 0.ToString());
                param.Add("reverse", true.ToString());
                param.Add("startTime", start);
                param.Add("endTime", end);
                param.Add("count", "1000");

                var res = Query("GET", "/trade", param);

                if (res == "")
                {
                    return null;
                }

                List<DatumTrades> tradeHistory = JsonConvert.DeserializeAnonymousType(res, new List<DatumTrades>());

                for (int i = 0; i < tradeHistory.Count; i++)
                {
                    if (string.IsNullOrEmpty(tradeHistory[i].price))
                    {
                        continue;
                    }

                    Trade trade = new Trade();
                    trade.SecurityNameCode = tradeHistory[i].symbol;
                    trade.Id = tradeHistory[i].trdMatchID;
                    trade.Time = Convert.ToDateTime(tradeHistory[i].timestamp);

                    //long r = TimeManager.GetTimeStampMilliSecondsToDateTime(Convert.ToDateTime(tradeHistory[i].timestamp));
                    //trade.Time = TimeManager.GetDateTimeFromTimeStamp(r);

                    trade.Price = tradeHistory[i].price.ToDecimal();
                    trade.Volume = tradeHistory[i].size.ToDecimal();
                    trade.Side = tradeHistory[i].side == "Sell" ? Side.Sell : Side.Buy;
                    trades.Add(trade);
                }

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
                        BitMexMyOrders myOrder = JsonConvert.DeserializeAnonymousType(message, new BitMexMyOrders());

                        if (myOrder.data.Count != 0 &&
                            (myOrder.data[0].execType == "Trade"
                            || myOrder.data[0].execType == "New"
                            || myOrder.data[0].execType == "Filled"))
                        {
                            UpdateMyTrade(myOrder);
                        }
                        continue;
                    }

                    if (message.StartsWith("{\"table\"" + ":" + "\"order\""))
                    {
                        BitMexOrder order = JsonConvert.DeserializeAnonymousType(message, new BitMexOrder());

                        if (order != null && order.data.Count != 0)
                        {
                            UpdateOrder(order);
                        }
                        continue;
                    }

                    if (message.StartsWith("{\"table\"" + ":" + "\"margin\""))
                    {
                        BitMexPortfolio portf = JsonConvert.DeserializeAnonymousType(message, new BitMexPortfolio());

                        if (portf != null)
                        {
                            UpdatePortfolio(portf);
                        }
                        continue;
                    }

                    if (message.StartsWith("{\"table\"" + ":" + "\"position\""))
                    {
                        BitMexPosition pos = JsonConvert.DeserializeAnonymousType(message, new BitMexPosition());

                        if (pos != null && pos.data.Count != 0)
                        {
                            UpdatePosition(pos);
                        }
                        continue;
                    }

                    if (message.StartsWith("{\"table\"" + ":" + "\"orderBookL2_25\""))
                    {
                        BitMexQuotes quotes = JsonConvert.DeserializeAnonymousType(message, new BitMexQuotes());

                        if (quotes.data.Count != 0 && quotes.data != null)
                        {
                            UpdateMarketDepth(quotes);
                        }
                        continue;
                    }

                    if (message.StartsWith("{\"table\"" + ":" + "\"trade\""))
                    {
                        BitMexTrades trade = JsonConvert.DeserializeAnonymousType(message, new BitMexTrades());

                        if (NewTradesEvent != null && trade != null)
                        {
                            UpdateTrade(trade);
                        }
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

        private List<Order> _newOrders = new List<Order>();

        private void UpdateOrder(BitMexOrder myOrder)
        {
            try
            {
                for (int i = 0; i < myOrder.data.Count; i++)
                {
                    decimal multiplier = GetMultiplierForSecurity(myOrder.data[i].symbol);

                    if (string.IsNullOrEmpty(myOrder.data[i].clOrdID))
                    {
                        continue;
                    }

                    if (myOrder.action == "insert")
                    {
                        Order order = new Order();
                        order.SecurityNameCode = myOrder.data[i].symbol;
                        order.TimeCallBack = Convert.ToDateTime(myOrder.data[i].transactTime);
                        order.NumberUser = Convert.ToInt32(myOrder.data[i].clOrdID);
                        order.NumberMarket = myOrder.data[i].orderID;
                        order.Side = myOrder.data[i].side == "Buy" ? Side.Buy : Side.Sell;

                        order.State = OrderStateType.Pending;

                        if (myOrder.data[i].orderQty != null)
                        {
                            order.Volume = myOrder.data[i].orderQty.ToDecimal() / multiplier;
                        }

                        if (!string.IsNullOrEmpty(myOrder.data[i].price))
                        {
                            order.Price = myOrder.data[i].price.ToDecimal();
                        }

                        order.ServerType = ServerType.BitMex;
                        order.PortfolioNumber = "BitMex";

                        order.Comment = myOrder.data[i].text;


                        order.TypeOrder = myOrder.data[i].ordType == "Limit"
                            ? OrderPriceType.Limit
                            : OrderPriceType.Market;

                        if (MyOrderEvent != null)
                        {
                            MyOrderEvent(order);
                        }

                        _newOrders.Add(order);
                    }
                    else if (myOrder.action == "update" ||
                       (myOrder.action == "partial" &&
                        (myOrder.data[i].ordStatus == "Canceled" || myOrder.data[i].ordStatus == "Rejected")
                        ))
                    {
                        Order needOrder = null;
                        for (int j = 0; j < _newOrders.Count; j++)
                        {
                            if (_newOrders[j].NumberUser == Convert.ToInt32(myOrder.data[i].clOrdID))
                            {
                                needOrder = _newOrders[j];
                            }
                        }

                        if (needOrder == null)
                        {
                            needOrder = new Order();

                            needOrder.NumberUser = Convert.ToInt32(myOrder.data[i].clOrdID);
                            needOrder.NumberMarket = myOrder.data[i].orderID;
                            needOrder.SecurityNameCode = myOrder.data[i].symbol;

                            if (!string.IsNullOrEmpty(myOrder.data[i].price))
                            {
                                needOrder.Price = Convert.ToDecimal(myOrder.data[i].price);
                            }

                            if (!string.IsNullOrEmpty(myOrder.data[i].text))
                            {
                                needOrder.Comment = myOrder.data[i].text;
                            }

                            if (!string.IsNullOrEmpty(myOrder.data[0].transactTime))
                            {
                                needOrder.TimeCallBack = Convert.ToDateTime(myOrder.data[0].transactTime);
                            }

                            needOrder.PortfolioNumber = "BitMex";

                            if (!string.IsNullOrEmpty(myOrder.data[i].ordType))
                            {
                                needOrder.TypeOrder = myOrder.data[i].ordType == "Limit"
                                     ? OrderPriceType.Limit
                                     : OrderPriceType.Market;
                            }

                            if (!string.IsNullOrEmpty(myOrder.data[i].side))
                            {
                                if (myOrder.data[i].side == "Sell")
                                {
                                    needOrder.Side = Side.Sell;
                                }
                                else if (myOrder.data[i].side == "Buy")
                                {
                                    needOrder.Side = Side.Buy;
                                }
                            }

                            if (MyOrderEvent != null)
                            {
                                MyOrderEvent(needOrder);
                            }
                            _newOrders.Add(needOrder);
                        }

                        if (needOrder != null)
                        {
                            if (Convert.ToBoolean(myOrder.data[i].workingIndicator))
                            {
                                needOrder.State = OrderStateType.Active;
                            }

                            if (myOrder.data[i].ordStatus == "Canceled")
                            {
                                needOrder.State = OrderStateType.Cancel;
                            }

                            if (myOrder.data[i].ordStatus == "Rejected")
                            {
                                needOrder.State = OrderStateType.Fail;
                                needOrder.VolumeExecute = 0;
                            }

                            if (myOrder.data[i].ordStatus == "PartiallyFilled")
                            {
                                needOrder.State = OrderStateType.Partial;
                                if (myOrder.data[i].cumQty != null)
                                {
                                    needOrder.VolumeExecute = myOrder.data[i].cumQty.ToDecimal();
                                }
                            }

                            if (myOrder.data[i].ordStatus == "Filled")
                            {
                                needOrder.State = OrderStateType.Done;
                                if (myOrder.data[i].cumQty != null)
                                {
                                    needOrder.VolumeExecute = myOrder.data[i].cumQty.ToDecimal();
                                }
                            }

                            if (MyOrderEvent != null)
                            {
                                MyOrderEvent(needOrder);
                            }
                        }
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void UpdatePortfolio(BitMexPortfolio portf)
        {
            try
            {
                Portfolio osPortf = new Portfolio();

                Portfolio portfolio = new Portfolio();
                portfolio.Number = "BitMex";
                portfolio.ValueBegin = 1;
                portfolio.ValueCurrent = 1;

                PositionOnBoard pos = new PositionOnBoard();

                if (portf.action == "update")
                {
                    pos.SecurityNameCode = portf.data[0].currency;

                    if (portf.data[0].marginBalance.ToDecimal() != 0)
                    {
                        pos.ValueCurrent = portf.data[0].marginBalance.ToDecimal() / 1000000;
                    }
                    else
                    {
                        pos.ValueCurrent = portf.data[0].availableMargin.ToDecimal() / 1000000;
                    }

                    pos.ValueBlocked = portf.data[0].initMargin.ToDecimal() / 1000000;
                }
                else
                {
                    return;
                }

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

        private void UpdateMarketDepth(BitMexQuotes quotes)
        {
            try
            {
                MarketDepth depth = null;

                decimal vol = 1;

                for (int i = 0; i < _depths.Count; i++)
                {
                    if (_depths[i].SecurityNameCode == quotes.data[0].symbol)
                    {
                        depth = _depths[i];
                        break;
                    }
                }

                if (quotes.action == "partial")
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
                    depth.SecurityNameCode = quotes.data[0].symbol;
                    List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
                    List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                    for (int i = 0; i < quotes.data.Count; i++)
                    {
                        if (quotes.data[i].price == null ||
                            quotes.data[i].price.ToDecimal() == 0)
                        {
                            continue;
                        }

                        if (quotes.data[i].symbol.Contains("USDT"))
                        {
                            vol = 1000000;
                        }
                        if (quotes.data[i].side == "Sell")
                        {
                            ascs.Add(new MarketDepthLevel()
                            {
                                Ask = quotes.data[i].size.ToDecimal() / vol,
                                Price = quotes.data[i].price.ToDecimal(),
                                Id = quotes.data[i].id
                            });

                            if (depth.Bids != null && depth.Bids.Count > 2 &&
                                quotes.data[i].price.ToDecimal() < depth.Bids[0].Price)
                            {
                                depth.Bids.RemoveAt(0);
                            }
                        }
                        else
                        {
                            bids.Add(new MarketDepthLevel()
                            {
                                Bid = quotes.data[i].size.ToDecimal() / vol,
                                Price = quotes.data[i].price.ToDecimal(),
                                Id = quotes.data[i].id
                            });

                            if (depth.Asks != null && depth.Asks.Count > 2 &&
                                quotes.data[i].price.ToDecimal() > depth.Asks[0].Price)
                            {
                                depth.Asks.RemoveAt(0);
                            }
                        }
                    }

                    ascs.Reverse();
                    depth.Asks = ascs;
                    depth.Bids = bids;
                }

                if (quotes.action == "update")
                {
                    if (depth == null)
                        return;

                    for (int i = 0; i < quotes.data.Count; i++)
                    {
                        if (quotes.data[i].symbol.Contains("USDT"))
                        {
                            vol = 1000000;
                        }

                        if (quotes.data[i].side == "Sell")
                        {
                            for (int j = 0; j < depth.Asks.Count; j++)
                            {
                                if (depth.Asks[j].Id == quotes.data[i].id)
                                {
                                    depth.Asks[j].Ask = quotes.data[i].size.ToDecimal() / vol;
                                }
                                else
                                {
                                    if (quotes.data[i].price == null ||
                                   quotes.data[i].price == "0")
                                    {
                                        continue;
                                    }

                                    decimal price = quotes.data[i].price.ToDecimal();

                                    if (j == 0 && price < depth.Asks[j].Price)
                                    {
                                        depth.Asks.Insert(j, new MarketDepthLevel()
                                        {
                                            Ask = quotes.data[i].size.ToDecimal() / vol,
                                            Price = quotes.data[i].price.ToDecimal(),
                                            Id = quotes.data[i].id
                                        });
                                    }
                                    else if (j != depth.Asks.Count - 1 && price > depth.Asks[j].Price && price < depth.Asks[j + 1].Price)
                                    {
                                        depth.Asks.Insert(j + 1, new MarketDepthLevel()
                                        {
                                            Ask = quotes.data[i].size.ToDecimal() / vol,
                                            Price = quotes.data[i].price.ToDecimal(),
                                            Id = quotes.data[i].id
                                        });
                                    }
                                    else if (j == depth.Asks.Count - 1 && price > depth.Asks[j].Price)
                                    {
                                        depth.Asks.Add(new MarketDepthLevel()
                                        {
                                            Ask = quotes.data[i].size.ToDecimal() / vol,
                                            Price = quotes.data[i].price.ToDecimal(),
                                            Id = quotes.data[i].id
                                        });
                                    }

                                    if (depth.Bids != null && depth.Bids.Count > 2 &&
                                        quotes.data[i].price.ToDecimal() < depth.Bids[0].Price)
                                    {
                                        depth.Bids.RemoveAt(0);
                                    }
                                }
                            }
                        }
                        else if (quotes.data[i].side == "Buy")
                        {
                            for (int j = 0; j < depth.Bids.Count; j++)
                            {
                                if (depth.Bids[j].Id == quotes.data[i].id)
                                {
                                    depth.Bids[j].Bid = quotes.data[i].size.ToDecimal() / vol;
                                }
                                else
                                {
                                    if (quotes.data[i].price == null ||
                                        quotes.data[i].price == "0")
                                    {
                                        continue;
                                    }

                                    decimal price = quotes.data[i].price.ToDecimal();

                                    if (j == 0 && price > depth.Bids[j].Price)
                                    {
                                        depth.Bids.Insert(j, new MarketDepthLevel()
                                        {
                                            Bid = quotes.data[i].size.ToDecimal() / vol,
                                            Price = quotes.data[i].price.ToDecimal(),
                                            Id = quotes.data[i].id
                                        });
                                    }
                                    else if (j != depth.Bids.Count - 1 && price < depth.Bids[j].Price && price > depth.Bids[j + 1].Price)
                                    {
                                        depth.Bids.Insert(j + 1, new MarketDepthLevel()
                                        {
                                            Bid = quotes.data[i].size.ToDecimal() / vol,
                                            Price = quotes.data[i].price.ToDecimal(),
                                            Id = quotes.data[i].id
                                        });
                                    }
                                    else if (j == depth.Bids.Count - 1 && price < depth.Bids[j].Price)
                                    {
                                        depth.Bids.Add(new MarketDepthLevel()
                                        {
                                            Bid = quotes.data[i].size.ToDecimal() / vol,
                                            Price = quotes.data[i].price.ToDecimal(),
                                            Id = quotes.data[i].id
                                        });
                                    }

                                    if (depth.Asks != null && depth.Asks.Count > 2 &&
                                        quotes.data[i].price.ToDecimal() > depth.Asks[0].Price)
                                    {
                                        depth.Asks.RemoveAt(0);
                                    }
                                }
                            }
                        }
                    }
                }

                if (quotes.action == "delete")
                {
                    if (depth == null)
                        return;

                    for (int i = 0; i < quotes.data.Count; i++)
                    {
                        if (quotes.data[i].side == "Sell")
                        {
                            for (int j = 0; j < depth.Asks.Count; j++)
                            {
                                if (depth.Asks[j].Id == quotes.data[i].id)
                                {
                                    depth.Asks.RemoveAt(j);
                                }
                            }
                        }
                        else
                        {
                            for (int j = 0; j < depth.Bids.Count; j++)
                            {
                                if (depth.Bids[j].Id == quotes.data[i].id)
                                {
                                    depth.Bids.RemoveAt(j);
                                }
                            }
                        }
                    }
                }

                if (quotes.action == "insert")
                {
                    if (depth == null)
                    {
                        return;
                    }

                    for (int i = 0; i < quotes.data.Count; i++)
                    {
                        if (quotes.data[i].price == null ||
                            quotes.data[i].price == "0")
                        {
                            continue;
                        }

                        if (quotes.data[i].symbol.Contains("USDT"))
                        {
                            vol = 1000000;
                        }

                        if (quotes.data[i].side == "Sell")
                        {
                            decimal price = quotes.data[i].price.ToDecimal();

                            for (int j = 0; j < depth.Asks.Count; j++)
                            {
                                if (j == 0 && price < depth.Asks[j].Price)
                                {
                                    depth.Asks.Insert(j, new MarketDepthLevel()
                                    {
                                        Ask = quotes.data[i].size.ToDecimal() / vol,
                                        Price = quotes.data[i].price.ToDecimal(),
                                        Id = quotes.data[i].id
                                    });
                                }
                                else if (j != depth.Asks.Count - 1 && price > depth.Asks[j].Price && price < depth.Asks[j + 1].Price)
                                {
                                    depth.Asks.Insert(j + 1, new MarketDepthLevel()
                                    {
                                        Ask = quotes.data[i].size.ToDecimal() / vol,
                                        Price = quotes.data[i].price.ToDecimal(),
                                        Id = quotes.data[i].id
                                    });
                                }
                                else if (j == depth.Asks.Count - 1 && price > depth.Asks[j].Price)
                                {
                                    depth.Asks.Add(new MarketDepthLevel()
                                    {
                                        Ask = quotes.data[i].size.ToDecimal() / vol,
                                        Price = quotes.data[i].price.ToDecimal(),
                                        Id = quotes.data[i].id
                                    });
                                }

                                if (depth.Bids != null && depth.Bids.Count > 2 &&
                                    quotes.data[i].price.ToDecimal() < depth.Bids[0].Price)
                                {
                                    depth.Bids.RemoveAt(0);
                                }
                            }
                        }
                        else // quotes.data[i].side == "Buy"
                        {
                            decimal price = quotes.data[i].price.ToDecimal();

                            for (int j = 0; j < depth.Bids.Count; j++)
                            {
                                if (j == 0 && price > depth.Bids[j].Price)
                                {
                                    depth.Bids.Insert(j, new MarketDepthLevel()
                                    {
                                        Bid = quotes.data[i].size.ToDecimal() / vol,
                                        Price = quotes.data[i].price.ToDecimal(),
                                        Id = quotes.data[i].id
                                    });
                                }
                                else if (j != depth.Bids.Count - 1 && price < depth.Bids[j].Price && price > depth.Bids[j + 1].Price)
                                {
                                    depth.Bids.Insert(j + 1, new MarketDepthLevel()
                                    {
                                        Bid = quotes.data[i].size.ToDecimal() / vol,
                                        Price = quotes.data[i].price.ToDecimal(),
                                        Id = quotes.data[i].id
                                    });
                                }
                                else if (j == depth.Bids.Count - 1 && price < depth.Bids[j].Price)
                                {
                                    depth.Bids.Add(new MarketDepthLevel()
                                    {
                                        Bid = quotes.data[i].size.ToDecimal() / vol,
                                        Price = quotes.data[i].price.ToDecimal(),
                                        Id = quotes.data[i].id
                                    });
                                }

                                if (depth.Asks != null && depth.Asks.Count > 2 &&
                                    quotes.data[i].price.ToDecimal() > depth.Asks[0].Price)
                                {
                                    depth.Asks.RemoveAt(0);
                                }
                            }
                        }
                    }
                }

                depth.Time = Convert.ToDateTime(quotes.data[0].timestamp);

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

        private void UpdateTrade(BitMexTrades trades)
        {
            try
            {
                for (int i = 0; i < trades.data.Count; i++)
                {
                    Trade trade = new Trade();
                    trade.SecurityNameCode = trades.data[i].symbol;
                    trade.Price = trades.data[i].price.ToDecimal();
                    trade.Id = trades.data[i].trdMatchID;

                    //trade.Time = DateTime.ParseExact(trades.data[i].timestamp, "yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

                    //long timeMs = TimeManager.GetTimeStampMilliSecondsToDateTime(Convert.ToDateTime(trades.data[i].timestamp));
                    //trade.Time = TimeManager.GetDateTimeFromTimeStamp(timeMs);

                    trade.Time = Convert.ToDateTime(trades.data[i].timestamp).AddMilliseconds(1);
                    trade.Volume = trades.data[i].size.ToDecimal(); ;
                    trade.Side = trades.data[i].side == "Buy" ? Side.Buy : Side.Sell;

                    ServerTime = trade.Time;

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

        private void UpdatePosition(BitMexPosition pos)
        {
            Portfolio portfolio = new Portfolio();
            portfolio.Number = "BitMex";
            portfolio.ValueBegin = 1;
            portfolio.ValueCurrent = 1;

            for (int i = 0; i < pos.data.Count; i++)
            {
                decimal multiplier = GetMultiplierForSecurity(pos.data[i].symbol);
                PositionOnBoard newPos = new PositionOnBoard();

                if (pos.action == "partial")
                {
                    newPos.PortfolioName = "BitMex";
                    newPos.SecurityNameCode = pos.data[i].symbol;
                    newPos.ValueBegin = pos.data[i].currentQty.ToDecimal() / multiplier;
                    //newPos.ValueCurrent = pos.data[i].currentQty.ToDecimal() / multiplier;
                }
                else if (pos.action == "update")
                {
                    newPos.PortfolioName = "BitMex";
                    newPos.SecurityNameCode = pos.data[i].symbol;
                    //newPos.ValueBlocked = pos.data[i].posMargin.ToDecimal();
                    newPos.ValueCurrent = pos.data[i].currentQty.ToDecimal() / multiplier;
                }
                else
                {
                    newPos.PortfolioName = "BitMex";
                    newPos.SecurityNameCode = pos.data[i].symbol;
                    //newPos.ValueBlocked = pos.data[i].posMargin.ToDecimal();
                    newPos.ValueCurrent = pos.data[i].currentQty.ToDecimal() / multiplier;
                }

                portfolio.SetNewPosition(newPos);
            }

            if (PortfolioEvent != null)
            {
                PortfolioEvent(new List<Portfolio> { portfolio });
            }
        }

        private void UpdateMyTrade(BitMexMyOrders myOrder)
        {
            try
            {
                for (int i = 0; i < myOrder.data.Count; i++)
                {
                    if (myOrder.data[i].lastQty == null ||
                        myOrder.data[i].lastQty.ToDecimal() == 0)
                    {
                        continue;
                    }

                    decimal multiplier = GetMultiplierForSecurity(myOrder.data[i].symbol);

                    MyTrade myTrade = new MyTrade();
                    myTrade.Time = Convert.ToDateTime(myOrder.data[i].transactTime);
                    myTrade.NumberOrderParent = myOrder.data[i].orderID;
                    myTrade.NumberTrade = myOrder.data[i].clOrdID;
                    myTrade.Price = myOrder.data[i].avgPx.ToDecimal();
                    myTrade.SecurityNameCode = myOrder.data[i].symbol;
                    myTrade.Side = myOrder.data[i].side == "Buy" ? Side.Buy : Side.Sell;

                    if (myOrder.data[i].lastQty != null)
                    {
                        myTrade.Volume = myOrder.data[i].lastQty.ToDecimal() / multiplier;
                    }

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

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        #endregion

        #region 11 Trade

        private RateGate _rateGateSendOrder = new RateGate(1, TimeSpan.FromMilliseconds(350));

        private RateGate _rateGateCancelOrder = new RateGate(1, TimeSpan.FromMilliseconds(350));

        public void SendOrder(Order order)
        {
            try
            {
                _rateGateSendOrder.WaitToProceed();

                decimal multiplier = GetMultiplierForSecurity(order.SecurityNameCode);

                Dictionary<string, string> param = new Dictionary<string, string>();
                param.Add("symbol", order.SecurityNameCode);
                param.Add("side", order.Side == Side.Buy ? "Buy" : "Sell");
                param.Add("orderQty", (order.Volume * multiplier).ToString().Replace(",", "."));
                param.Add("clOrdID", order.NumberUser.ToString());
                param.Add("origClOrdID", order.NumberUser.ToString());

                if (order.TypeOrder == OrderPriceType.Market)
                {
                    param.Add("ordType", "Market");
                }
                else
                {
                    param.Add("ordType", "Limit");
                    param.Add("timeInForce", "GoodTillCancel");
                    param.Add("price", order.Price.ToString().Replace(",", "."));
                }

                var res = Query("POST", "/order", param, true);

                if (res != null && res.Contains("clOrdID"))
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
            try
            {
                _rateGateCancelOrder.WaitToProceed();

                if (string.IsNullOrEmpty(order.NumberMarket))
                {
                    Order onBoard = GetOrdersState(order);

                    if (onBoard == null)
                    {
                        order.State = OrderStateType.Fail;
                        SendLogMessage("When revoking an orderOnBoard, we didn't find it on the exchange. We think it's already been revoked.",
                            LogMessageType.Error);
                        if (MyOrderEvent != null)
                        {
                            MyOrderEvent(order);
                        }
                        return;
                    }
                    else if (onBoard.State == OrderStateType.Cancel)
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
                param.Add("symbol", order.SecurityNameCode);
                param.Add("orderID", order.NumberMarket);

                Query("DELETE", "/order", param, true);

            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private Order GetOrdersState(Order oldOrder)
        {
            List<string> namesSec = new List<string>();
            namesSec.Add(oldOrder.SecurityNameCode);

            List<DatumOrder> allOrders = new List<DatumOrder>();

            for (int i = 0; i < namesSec.Count; i++)
            {
                Dictionary<string, string> param = new Dictionary<string, string>();
                param.Add("symbol", namesSec[i]);
                param.Add("count", 500.ToString());
                param.Add("reverse", true.ToString());

                var res = Query("GET", "/order", param, true);

                if (res == null || res == "[]")
                {
                    continue;
                }

                List<DatumOrder> orders = JsonConvert.DeserializeAnonymousType(res, new List<DatumOrder>());

                if (orders != null && orders.Count != 0)
                {
                    allOrders.AddRange(orders);
                }
            }

            DatumOrder orderOnBoard = new DatumOrder();
            for (int i = 0; i < allOrders.Count; i++)
            {
                if (allOrders[i].clOrdID == oldOrder.NumberUser.ToString())
                {
                    orderOnBoard = allOrders[i];
                }
            }

            if (orderOnBoard == null)
            {
                return null;
            }

            if (String.IsNullOrEmpty(orderOnBoard.clOrdID))
            {
                return null;
            }

            Order newOrder = new Order();
            newOrder.NumberMarket = orderOnBoard.orderID;
            newOrder.NumberUser = oldOrder.NumberUser;
            newOrder.SecurityNameCode = oldOrder.SecurityNameCode;
            newOrder.State = OrderStateType.Cancel;
            newOrder.Volume = oldOrder.Volume;
            newOrder.VolumeExecute = oldOrder.VolumeExecute;
            newOrder.Price = oldOrder.Price;
            newOrder.TypeOrder = oldOrder.TypeOrder;
            newOrder.TimeCallBack = oldOrder.TimeCallBack;
            newOrder.TimeCancel = newOrder.TimeCallBack;
            newOrder.ServerType = ServerType.BitMex;
            newOrder.PortfolioNumber = oldOrder.PortfolioNumber;

            if (orderOnBoard.ordStatus == "New" ||
                orderOnBoard.ordStatus == "PartiallyFilled")
            {
                newOrder.State = OrderStateType.Active;
            }
            else if (orderOnBoard.ordStatus == "Filled")
            {
                newOrder.State = OrderStateType.Done;
                //MyTrade trade = new MyTrade();
                //trade.NumberOrderParent = oldOrder.NumberMarket;
                //trade.NumberTrade = NumberGen.GetNumberOrder(StartProgram.IsOsTrader).ToString();
                //trade.SecurityNameCode = oldOrder.SecurityNameCode;
                //trade.Time = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(orderOnBoard.timestamp));
                //trade.Side = oldOrder.Side;

                //if (MyTradeEvent != null)
                //{
                //    MyTradeEvent(trade);
                //}
            }
            else //if (orderOnBoard.ordStatus == "Canceled")
            {
                newOrder.State = OrderStateType.Cancel;
            }

            if (MyOrderEvent != null)
            {
                MyOrderEvent(newOrder);
            }

            return newOrder;
        }

        public void CancelAllOrders()
        {
            List<Order> openOrders = GetAllOpenOrders();

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
            List<Order> openOrders = GetAllOpenOrders();

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

        public void GetOrderStatus(Order order)
        {
            List<Order> allSecurityOrders = GetAllOrdersToSecurity(order.SecurityNameCode);

            if (allSecurityOrders == null)
            {
                return;
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
                return;
            }

            if (MyOrderEvent != null)
            {
                MyOrderEvent(myOrderActualOnBoard);
            }

            if (myOrderActualOnBoard.State == OrderStateType.Done ||
                myOrderActualOnBoard.State == OrderStateType.Partial)
            { // запрашиваем MyTrades, если по ордеру были исполнения

                List<MyTrade> trades = GetAllMyTradesToOrder(myOrderActualOnBoard);

                if (trades != null)
                {
                    for (int i = 0; i < trades.Count; i++)
                    {
                        if (MyTradeEvent != null)
                        {
                            MyTradeEvent(trades[i]);
                        }
                    }
                }
            }
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            try
            {
                _rateGateCancelOrder.WaitToProceed();

                Dictionary<string, string> param = new Dictionary<string, string>();
                param.Add("symbol", security.Name);

                Query("DELETE", "/order/all", param, true);
            }
            catch (Exception exeption)
            {
                SendLogMessage(exeption.ToString(), LogMessageType.Error);
            }
        }

        public List<MyTrade> GetAllMyTradesToOrder(Order order)
        {
            var param = new Dictionary<string, string>();
            param.Add("symbol", order.SecurityNameCode);
            param.Add("count", 500.ToString());

            var res = Query("GET", "/execution", param, true);

            if (res == null || res == "[]")
            {
                return null;
            }

            List<DatumMyOrder> myTrades = JsonConvert.DeserializeAnonymousType(res, new List<DatumMyOrder>());

            decimal multiplier = GetMultiplierForSecurity(order.SecurityNameCode);

            List<MyTrade> trades = new List<MyTrade>();

            for (int i = 0; i < myTrades.Count; i++)
            {
                if (myTrades[i].orderID != order.NumberMarket)
                {
                    continue;
                }

                MyTrade newTrade = new MyTrade();
                newTrade.SecurityNameCode = myTrades[i].symbol;
                newTrade.NumberTrade = myTrades[i].clOrdID;
                newTrade.NumberOrderParent = myTrades[i].orderID;
                newTrade.Volume = myTrades[i].lastQty.ToDecimal() / multiplier;
                newTrade.Price = myTrades[i].price.ToDecimal();
                newTrade.Time = Convert.ToDateTime(myTrades[i].transactTime);
                newTrade.Side = order.Side;
                trades.Add(newTrade);
            }

            return trades;
        }

        public List<Order> GetAllOrdersToSecurity(string securityName)
        {
            var param = new Dictionary<string, string>();
            param.Add("symbol", securityName);
            param.Add("count", 500.ToString());
            param.Add("reverse", true.ToString());

            var res = Query("GET", "/order", param, true);

            if (res == null || res == "[]")
            {
                return null;
            }

            List<DatumOrder> orders = JsonConvert.DeserializeAnonymousType(res, new List<DatumOrder>());

            if (orders == null)
            {
                return null;
            }

            decimal multiplier = GetMultiplierForSecurity(securityName);

            List<Order> result = new List<Order>();

            for (int i = 0; i < orders.Count; i++)
            {
                DatumOrder myOrder = orders[i];

                Order newOrder = new Order();
                newOrder.NumberMarket = orders[i].orderID;

                if (orders[i].clOrdID != null)
                {
                    string id = orders[i].clOrdID;
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
                newOrder.Volume = orders[i].orderQty.ToDecimal() / multiplier;
                newOrder.ServerType = ServerType.BitMex;
                newOrder.PortfolioNumber = "BitMex";

                if (orders[i].side == "Buy")
                {
                    newOrder.Side = Side.Buy;
                }
                else
                {
                    newOrder.Side = Side.Sell;
                }

                if (orders[i].ordType == "Market")
                {
                    newOrder.TypeOrder = OrderPriceType.Market;
                }
                else
                {
                    newOrder.TypeOrder = OrderPriceType.Limit;
                }
                newOrder.TimeCallBack = Convert.ToDateTime(orders[i].transactTime);
                newOrder.TimeCreate = Convert.ToDateTime(orders[i].timestamp);

                if (myOrder.ordStatus == "New")
                {
                    newOrder.State = OrderStateType.Active;
                }
                else if (myOrder.ordStatus == "Filled")
                {
                    newOrder.State = OrderStateType.Done;
                    newOrder.TimeDone = newOrder.TimeCallBack;
                }
                else if (myOrder.ordStatus == "PartiallyFilled")
                {
                    newOrder.State = OrderStateType.Partial;
                }
                else if (myOrder.ordStatus == "Canceled"
                || myOrder.ordStatus == "Expired")
                {
                    newOrder.State = OrderStateType.Cancel;
                    newOrder.TimeCancel = newOrder.TimeCallBack;
                }
                else if (myOrder.ordStatus == "Rejected")
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

        private List<Order> GetAllOpenOrders()
        {
            List<Order> openOrders = new List<Order>();

            var param = new Dictionary<string, string>();
            //param.Add("filter", "{\"open\":true}");
            param.Add("reverse", true.ToString());

            var res = Query("GET", "/order", param, true);

            if (res == null || res == "[]")
            {
                return openOrders;
            }

            List<DatumOrder> orders = JsonConvert.DeserializeAnonymousType(res, new List<DatumOrder>());

            if (orders == null)
            {
                return null;
            }

            for (int i = 0; i < orders.Count; i++)
            {
                if (orders[i] == null)
                {
                    return null;
                }

                Order newOrder = new Order();
                newOrder.NumberMarket = orders[i].orderID;

                if (orders[i].clOrdID != null)
                {
                    string id = orders[i].clOrdID;
                    try
                    {
                        newOrder.NumberUser = Convert.ToInt32(id);
                    }
                    catch
                    {
                        // ignore
                    }
                }

                decimal multiplier = GetMultiplierForSecurity(orders[i].symbol);

                newOrder.SecurityNameCode = orders[i].symbol;
                newOrder.State = OrderStateType.Active;
                newOrder.Price = orders[i].price.ToDecimal();
                newOrder.Volume = orders[i].orderQty.ToDecimal() / multiplier;
                newOrder.ServerType = ServerType.BitMex;
                newOrder.PortfolioNumber = "BitMex";

                if (orders[i].side == "Buy")
                {
                    newOrder.Side = Side.Buy;
                }
                else
                {
                    newOrder.Side = Side.Sell;
                }

                newOrder.TimeCreate = Convert.ToDateTime(orders[i].transactTime);
                newOrder.TimeCallBack = newOrder.TimeCreate;

                try
                {
                    newOrder.Volume = orders[i].orderQty.ToDecimal() / multiplier;
                }
                catch
                {
                    // ignore
                }

                openOrders.Add(newOrder);
            }

            return openOrders;
        }

        private decimal GetMultiplierForSecurity(string security)
        {
            string res11 = Query("GET", "/instrument/active");
            List<BitMexSecurity> listSec = JsonConvert.DeserializeObject<List<BitMexSecurity>>(res11);

            decimal multiplier = 1;
            decimal underlyingToPositionMultiplier = 1;
            decimal underlyingToSettleMultiplier = 1;

            for (int i = 0; i < listSec.Count; i++)
            {
                if (security == listSec[i].symbol)
                {
                    underlyingToPositionMultiplier = listSec[i].underlyingToPositionMultiplier.ToDecimal();
                    underlyingToSettleMultiplier = listSec[i].underlyingToSettleMultiplier.ToDecimal();
                    break;
                }
            }

            if (underlyingToPositionMultiplier != 0)
            {
                multiplier = underlyingToPositionMultiplier;
            }
            else if (underlyingToSettleMultiplier != 0)
            {
                multiplier = underlyingToSettleMultiplier;
            }

            return multiplier;
        }

        #endregion

        #region 12 Queries

        RateGate _rateGate = new RateGate(1, TimeSpan.FromMilliseconds(500));

        private object _queryHttpLocker = new object();

        private string Query(string method, string function, Dictionary<string, string> param = null, bool auth = false, bool json = false)
        {
            lock (_queryHttpLocker)
            {
                _rateGate.WaitToProceed();
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
                        var data = Encoding.UTF8.GetBytes(postData);
                        using (var stream = webRequest.GetRequestStream())
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
            foreach (var item in param)
                b.Append(string.Format("&{0}={1}", item.Key, WebUtility.UrlEncode(item.Value)));

            try { return b.ToString().Substring(1); }
            catch (Exception) { return ""; }
        }

        private string BuildJSON(Dictionary<string, string> param)
        {
            if (param == null)
                return "";

            var entries = new List<string>();
            foreach (var item in param)
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
            using (var hash = new HMACSHA256(keyByte))
            {
                return hash.ComputeHash(messageBytes);
            }
        }

        //private long GetNonce()
        //{
        //    DateTime yearBegin = new DateTime(1970, 1, 1);
        //    var timeStamp = DateTime.UtcNow - yearBegin;
        //    var r = timeStamp.TotalMilliseconds;
        //    var re = Convert.ToInt64(r);

        //    return re;
        //}

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
