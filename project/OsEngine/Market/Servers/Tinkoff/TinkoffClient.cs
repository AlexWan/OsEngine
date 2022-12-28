/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using System.IO;
using OsEngine.Market.Servers.Tinkoff.TinkoffJsonSchema;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;

namespace OsEngine.Market.Servers.Tinkoff
{
    public class TinkoffClient
    {
        public TinkoffClient(string token, bool IsGrpcConnection)
        {
            this.IsGrpcConnection = IsGrpcConnection;

            _token = token;

            Task worker = new Task(WorkerPlace);
            worker.Start();

            _rateGate = new RateGate(1, TimeSpan.FromMilliseconds(500));
            rateGateGrpcConnect = new RateGate(1, TimeSpan.FromMilliseconds(1000));
        }

        private bool IsGrpcConnection;

        RateGate _rateGate;

        RateGate rateGateGrpcConnect;

        public string _token;

        private string _url = "https://invest-public-api.tinkoff.ru/rest/tinkoff.public.invest.api.contract.v1.";

        public void Connect()
        {
            if (string.IsNullOrEmpty(_token))
            {
                return;
            }

            IsConnected = true;

            if (Connected != null)
            {
                Connected();
            }

        }

        public void Dispose()
        {
            IsConnected = false;

            if (Disconnected != null)
            {
                Disconnected();
            }

            _isDisposed = true;

            if (IsGrpcConnection == true)
            {
                KillRouter();
            }
        }

        private bool _isDisposed;

        #region реализация запроса к серверу

        public bool IsConnected;

        private object _queryLocker = new object();

        public string CreatePrivatePostQuery(string end_point, Dictionary<string, string> parameters)
        {
            lock (_queryLocker)
            {
                _rateGate.WaitToProceed();

                if (parameters == null)
                {
                    parameters = new Dictionary<string, string>();
                }

                StringBuilder sb = new StringBuilder();

                int i = 0;
                foreach (var param in parameters)
                {
                    if (param.Value.StartsWith("["))
                    {
                        sb.Append("\"" + param.Key + "\"" + ": " + param.Value);
                    }
                    else if (param.Value.StartsWith("{"))
                    {
                        sb.Append("\"" + param.Key + "\"" + ": " + param.Value);
                    }
                    else
                    {
                        sb.Append("\"" + param.Key + "\"" + ": \"" + param.Value + "\"");
                    }

                    i++;
                    if (i < parameters.Count)
                    {
                        sb.Append(",");
                    }
                }


                string url = end_point;

                string str_data = "{" + sb.ToString() + "}";

                byte[] data = Encoding.UTF8.GetBytes(str_data);

                Uri uri = new Uri(url);

                var web_request = (HttpWebRequest)WebRequest.Create(uri);

                web_request.Accept = "application/json";
                web_request.Method = "POST";
                web_request.ContentType = "application/json";
                web_request.ContentLength = data.Length;
                web_request.Headers.Add("Authorization", "Bearer " + _token);

                using (Stream req_tream = web_request.GetRequestStream())
                {
                    req_tream.Write(data, 0, data.Length);
                }

                var resp = web_request.GetResponse();

                HttpWebResponse httpWebResponse = (HttpWebResponse)resp;

                string response_msg;

                using (var stream = httpWebResponse.GetResponseStream())
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        response_msg = reader.ReadToEnd();
                    }
                }

                httpWebResponse.Close();

                return response_msg;
            }
        }

        #endregion 

        #region Запросы

        public void GetSecurities()
        {
            List<Security> securities = new List<Security>();

            List<Security> currencies = GetSecurities(_url + "InstrumentsService/Currencies", SecurityType.CurrencyPair);
            securities.AddRange(currencies);

            List<Security> stocks = GetSecurities(_url + "InstrumentsService/Shares", SecurityType.Stock);
            securities.AddRange(stocks);

            List<Security> futures = GetSecurities(_url + "InstrumentsService/Futures", SecurityType.Futures);
            securities.AddRange(stocks);

            List<Security> etfs = GetSecurities(_url + "InstrumentsService/Etfs", SecurityType.Stock);
            securities.AddRange(etfs);

            List<Security> bonds = GetSecurities(_url + "InstrumentsService/Bonds", SecurityType.Bond);
            securities.AddRange(bonds);

            _allSecurities = securities;

            if (UpdatePairs != null)
            {
                UpdatePairs(securities);
            }
        }

        private Security GetSecurityByFigi(string figi)
        {
            Security mySec = _allSecurities.Find(s => s.NameId == figi);
            return mySec;
        }

        private List<Security> _allSecurities = new List<Security>();

        private List<Security> GetSecurities(string url, SecurityType type)
        {
            string secClass = type.ToString();

            Dictionary<string, string> param = new Dictionary<string, string>();

            param.Add("instrumentStatus", "INSTRUMENT_STATUS_UNSPECIFIED");

            var res = CreatePrivatePostQuery(url, param);

            if (res == null)
            {
                return null;
            }

            InstrumentsResponse securitiesResp = JsonConvert.DeserializeAnonymousType(res, new InstrumentsResponse());

            List<Security> securities = new List<Security>();

            for (int i = 0; i < securitiesResp.instruments.Count; i++)
            {
                Instrument jtSecurity = securitiesResp.instruments[i];

                Security newSecurity = new Security();

                newSecurity.Name = jtSecurity.ticker;
                newSecurity.NameId = jtSecurity.figi;
                newSecurity.NameFull = jtSecurity.name;

                if (jtSecurity.minPriceIncrement != null)
                {
                    newSecurity.PriceStep = jtSecurity.minPriceIncrement.GetValue();
                }
                else
                {
                    newSecurity.PriceStep = 1;
                }

                if (newSecurity.PriceStep == 0)
                {
                    newSecurity.PriceStep = 1;
                }

                newSecurity.PriceStepCost = newSecurity.PriceStep;

                if (secClass == "Stock" &&
                    jtSecurity.currency == "rub")
                {
                    newSecurity.NameClass = secClass + " Ru";
                }
                else if (secClass == "Stock" &&
                         (jtSecurity.currency == "EUR" ||
                          jtSecurity.currency == "USD"))
                {
                    newSecurity.NameClass = secClass + " US";
                }
                else
                {
                    newSecurity.NameClass = secClass;
                }

                newSecurity.SecurityType = type;
                newSecurity.Lot = jtSecurity.lot.ToDecimal();

                securities.Add(newSecurity);
            }

            return securities;
        }

        public List<Candle> GetCandleHistory(string nameId, TimeFrame tf, DateTime from, DateTime to)
        {
            List<Candle> candles = new List<Candle>();

            while (from.Hour > 1)
            {
                from = from.AddHours(-1);
            }

            while (from.Minute > 1)
            {
                from = from.AddMinutes(-1);
            }

            while (from.Second > 1)
            {
                from = from.AddSeconds(-1);
            }

            while (from <= to)
            {
                candles.AddRange(GetCandleHistoryFromDay(from, nameId, tf));

                from = from.AddDays(1);
            }

            return candles;
        }

        private List<Candle> GetCandleHistoryFromDay(DateTime time, string nameSec, TimeFrame tf)
        {
            /* {
                 "figi": "string",
                 "from": "2022-05-25T12:51:29.091Z",
                 "to": "2022-05-25T12:51:29.091Z",
                 "interval": "CANDLE_INTERVAL_UNSPECIFIED"
               }*/
            string portUrl = _url + "MarketDataService/GetCandles";

            Dictionary<string, string> param = new Dictionary<string, string>();

            param.Add("figi", nameSec);

            string dateFrom = ToIso8601(time);
            param.Add("from", dateFrom);

            string dateTo = ToIso8601(time.AddDays(1));
            param.Add("to", dateTo);

            string tfStr = CreateTimeFrameString(tf);
            param.Add("interval", tfStr);

            var resPort = CreatePrivatePostQuery(portUrl, param);

            CandlesResponse candlesResp = JsonConvert.DeserializeAnonymousType(resPort, new CandlesResponse());

            List<Candle> candles = ConvertToOsEngineCandles(candlesResp);

            List<Candle> candlesCollapsed = CollapseCandles(candles, CountCandleCollapse(tf));

            return candlesCollapsed;
        }

        private List<Candle> ConvertToOsEngineCandles(CandlesResponse responce)
        {
            List<Candle> candles = new List<Candle>();

            for (int i = 0; i < responce.candles.Count; i++)
            {
                CandleTinkoff canTin = responce.candles[i];

                Candle candle = new Candle();
                candle.Open = canTin.open.GetValue().ToStringWithNoEndZero().ToDecimal();
                candle.Close = canTin.close.GetValue().ToStringWithNoEndZero().ToDecimal();
                candle.High = canTin.high.GetValue().ToStringWithNoEndZero().ToDecimal();
                candle.Low = canTin.low.GetValue().ToStringWithNoEndZero().ToDecimal();

                candle.TimeStart = FromIso8601(canTin.time);

                candles.Add(candle);
            }

            return candles;
        }

        private List<Candle> CollapseCandles(List<Candle> candles, int collapseCount)
        {
            if (collapseCount == 1)
            {
                return candles;
            }

            List<Candle> newCandles = new List<Candle>();

            int curNumCollapse = 0;

            for (int i = 0; i < candles.Count; i++)
            {
                if (curNumCollapse == 0
                    || candles[i].TimeStart.DayOfYear
                    != newCandles[newCandles.Count - 1].TimeStart.DayOfYear)
                {
                    Candle newCandle = new Candle();
                    newCandle.TimeStart = candles[i].TimeStart;
                    newCandle.Open = candles[i].Open;
                    newCandle.Close = candles[i].Close;
                    newCandle.Low = candles[i].Low;
                    newCandle.High = candles[i].High;
                    newCandle.Volume = candles[i].Volume;
                    newCandles.Add(newCandle);

                    curNumCollapse++;
                }
                else
                {
                    curNumCollapse++;
                    newCandles[newCandles.Count - 1] = newCandles[newCandles.Count - 1].Merge(candles[i]);
                }

                if (curNumCollapse == collapseCount)
                {
                    curNumCollapse = 0;
                }
            }


            return newCandles;
        }

        private int CountCandleCollapse(TimeFrame tf)
        {
            int result = 1;
            //CANDLE_INTERVAL_1_MIN, CANDLE_INTERVAL_5_MIN, CANDLE_INTERVAL_15_MIN, CANDLE_INTERVAL_HOUR, CANDLE_INTERVAL_DAY
            if (tf == TimeFrame.Min1)
            {

            }
            if (tf == TimeFrame.Min2)
            {
                result = 2;
            }
            if (tf == TimeFrame.Min3)
            {
                result = 3;
            }
            else if (tf == TimeFrame.Min5)
            {

            }
            else if (tf == TimeFrame.Min10)
            {
                result = 2;
            }
            else if (tf == TimeFrame.Min15)
            {

            }
            else if (tf == TimeFrame.Min30)
            {
                result = 2;
            }
            else if (tf == TimeFrame.Min45)
            {
                result = 3;
            }
            else if (tf == TimeFrame.Hour1)
            {

            }
            else if (tf == TimeFrame.Hour2)
            {
                result = 2;
            }
            else if (tf == TimeFrame.Hour4)
            {
                result = 3;
            }
            else if (tf == TimeFrame.Day)
            {

            }

            return result;
        }

        private string CreateTimeFrameString(TimeFrame tf)
        {
            string result = "";
            //CANDLE_INTERVAL_1_MIN, CANDLE_INTERVAL_5_MIN, CANDLE_INTERVAL_15_MIN, CANDLE_INTERVAL_HOUR, CANDLE_INTERVAL_DAY
            if (tf == TimeFrame.Min1)
            {
                result += "CANDLE_INTERVAL_1_MIN";
            }
            if (tf == TimeFrame.Min2)
            {
                result += "CANDLE_INTERVAL_1_MIN";
            }
            if (tf == TimeFrame.Min3)
            {
                result += "CANDLE_INTERVAL_1_MIN";
            }
            else if (tf == TimeFrame.Min5)
            {
                result += "CANDLE_INTERVAL_5_MIN";
            }
            else if (tf == TimeFrame.Min10)
            {
                result += "CANDLE_INTERVAL_5_MIN";
            }
            else if (tf == TimeFrame.Min15)
            {
                result += "CANDLE_INTERVAL_15_MIN";
            }
            else if (tf == TimeFrame.Min30)
            {
                result += "CANDLE_INTERVAL_15_MIN";
            }
            else if (tf == TimeFrame.Min45)
            {
                result += "CANDLE_INTERVAL_15_MIN";
            }
            else if (tf == TimeFrame.Hour1)
            {
                result += "CANDLE_INTERVAL_HOUR";
            }
            else if (tf == TimeFrame.Hour2)
            {
                result += "CANDLE_INTERVAL_HOUR";
            }
            else if (tf == TimeFrame.Hour4)
            {
                result += "CANDLE_INTERVAL_HOUR";
            }
            else if (tf == TimeFrame.Day)
            {
                result += "CANDLE_INTERVAL_DAY";
            }

            return result;
        }

        private string ToIso8601(DateTime time)
        {
            // "2022-05-25T12:51:29.091Z",

            string result = "";

            result += time.Year + "-";

            if (time.Month < 10)
            {
                result += "0" + time.Month + "-";
            }
            else
            {
                result += time.Month + "-";
            }

            if (time.Day < 10)
            {
                result += "0" + time.Day + "T";
            }
            else
            {
                result += time.Day + "T";
            }
            // "2022-05-25T12:51:29.091Z",

            if (time.Hour < 10)
            {
                result += "0" + time.Hour + ":";
            }
            else
            {
                result += time.Hour + ":";
            }

            if (time.Minute < 10)
            {
                result += "0" + time.Minute + ":";
            }
            else
            {
                result += time.Minute + ":";
            }
            // "2022-05-25T12:51:29.091Z",
            if (time.Second < 10)
            {
                result += "0" + time.Second + ".091Z";
            }
            else
            {
                result += time.Second + ".091Z";
            }

            return result;
        }

        private DateTime FromIso8601(string str)
        {
            string timeInStr = str;

            int year = Convert.ToInt32(timeInStr.Substring(0, 4));
            int month = Convert.ToInt32(timeInStr.Substring(5, 2));
            int day = Convert.ToInt32(timeInStr.Substring(8, 2));
            int hour = Convert.ToInt32(timeInStr.Substring(11, 2));
            int minute = Convert.ToInt32(timeInStr.Substring(14, 2));
            DateTime time = new DateTime(year, month, day, hour, minute, 0);

            return time;
        }

        #endregion

        #region Имитация потоковых сервисов

        private async void WorkerPlace()
        {
            await Task.Delay(10000);

            while (true)
            {
                try
                {
                    await Task.Delay(20);

                    if (_isDisposed)
                    {
                        return;
                    }

                    if (IsConnected == false)
                    {
                        continue;
                    }

                    if (_portfolios != null &&
                        _lastBalanceUpdTime.AddSeconds(10) < DateTime.Now)
                    {
                        UpdBalance();
                        UpdateMyTradesAndOrders();
                        _lastBalanceUpdTime = DateTime.Now;
                    }

                    if (IsGrpcConnection == false)
                    {
                        UpDateLastPrices();

                        for (int i = 0; i < _securitiesSubscrible.Count; i++)
                        {
                            GetMarketDepth(_securitiesSubscrible[i]);
                        }
                    }
                }
                catch (Exception e)
                {
                    SendLogMessage(e.ToString(), LogMessageType.Error);
                    await Task.Delay(5000);
                }
            }
        }

        private DateTime _lastBalanceUpdTime;

        // обезличенные сделки и стаканы

        public void SubscribleDepthsAndTrades(Security security)
        {
            if (_securitiesSubscrible.Find(s => s.Name == security.Name) != null)
            {
                return;
            }
            _securitiesSubscrible.Add(security);

            // new logic router

            if (IsGrpcConnection == true)
            {
                rateGateGrpcConnect.WaitToProceed();

                CreateProcessRouter();

                Thread thread = new Thread(() => {
                    SubscribleTradesAndDepthsGrcConnetction(security);
                });
                thread.IsBackground = true;
                thread.Name = "router";
                thread.Start();
            }
        }

        // new Logic router

        private Process RouterAplication;

        private void KillRouter()
        {
            if (RouterAplication != null)
            {
                RouterAplication.Kill();
            }
        }

        private void CreateProcessRouter()
        {
            if (RouterAplication == null)
            {
                RouterAplication = Process.Start(Directory.GetCurrentDirectory() + @"\Tinkoff_Router\Tinkoff_Router.exe", _token);
            }
        }

        private void SubscribleTradesAndDepthsGrcConnetction(Security security)
        {
            try
            {
                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPAddress iP = IPAddress.Parse("127.0.0.1");
                socket.Connect(iP, Convert.ToInt32(7980));

                string Request = security.NameId;

                byte[] array = Encoding.UTF8.GetBytes(Request);

                socket.Send(array);

                WorkingStreamConnectionInRouter(socket, security);

            }
            catch (Exception error)
            {

                _isDisposed = true;
                SendLogMessage(error.Message + error.StackTrace, LogMessageType.Error);
            }
        }

        private void WorkingStreamConnectionInRouter(Socket socket, Security security)
        {
            byte[] recv = new byte[8192];

            while (true)
            {
                socket.Receive(recv, SocketFlags.None);

                string response = Encoding.ASCII.GetString(recv);

                var q = response.Contains(", \"instrumentUid\":");
                response = response.Replace(", \"instrumentUid\":", "|");
                response = response.Split('|')[0] + "}";

                if (response.Contains("direction"))
                {
                    UpdateTicks(response, security);
                }
                else if (response.Contains("depth"))
                {
                    UpdateDepths(response, security);
                }
                else
                {
                    ThrowErrorResponse(response);
                }
                socket.Send(new byte[1024]);
            }
        }

        private void ThrowErrorResponse(string response)
        {
            if (response.Contains("Stream limit exceeded 80001."))
            {
                SendLogMessage("Лимит стримов превышен код ошибки 80001. Обратитесь в техподдержку Тинькофф. Перевидите \"Подключение через роутер\" в false в настройках подключения", LogMessageType.Error);
                _isDisposed = true;
            }
            else
            {
                SendLogMessage(response, LogMessageType.Error);
            }
        }

        private void UpdateDepths(string response, Security security)
        {
            try
            {
                MarketDepthTinkoffResponse depths = JsonConvert.DeserializeAnonymousType(response, new MarketDepthTinkoffResponse());


                List<MarketDepthLevel> bids = new List<MarketDepthLevel>();


                for (int i = 0; i < depths.bids.Count; i++)
                {
                    MarketDepthLevel newBid = new MarketDepthLevel();
                    newBid.Bid = depths.bids[i].quantity.ToDecimal();
                    newBid.Price = depths.bids[i].price.GetValue().ToStringWithNoEndZero().ToDecimal();
                    bids.Add(newBid);
                }

                MarketDepth depth = new MarketDepth();

                depth.SecurityNameCode = security.Name;
                depth.Time = MaxTimeOnServer;
                depth.Bids = bids;

                List<MarketDepthLevel> asks = new List<MarketDepthLevel>();


                for (int i = 0; i < depths.asks.Count; i++)
                {
                    MarketDepthLevel newAsk = new MarketDepthLevel();
                    newAsk.Ask = depths.asks[i].quantity.ToDecimal();
                    newAsk.Price = depths.asks[i].price.GetValue().ToStringWithNoEndZero().ToDecimal();
                    asks.Add(newAsk);
                }

                depth.Asks = asks;

                if (depth.Asks == null ||
                    depth.Asks.Count == 0 ||
                    depth.Bids == null ||
                    depth.Bids.Count == 0)
                {
                    return;
                }

                if (UpdateMarketDepth != null)
                {
                    UpdateMarketDepth(depth);
                }


            }
            catch (Exception er)
            {
                SendLogMessage(er.Message, LogMessageType.Error);
            }
        }

        private void UpdateTicks(string response, Security security)
        {
            try
            {
                TinkoffTrades trades = JsonConvert.DeserializeAnonymousType(response, new TinkoffTrades());
                NewTradesEvent(
                new List<Trade>() {
                                        new Trade()
                                        {
                                            Side = trades.direction.Equals("TRADE_DIRECTION_BUY") ? Side.Buy : Side.Sell,
                                            SecurityNameCode = security.Name,
                                            Time = FromIso8601(trades.time),
                                            Price = trades.price.GetValue().ToStringWithNoEndZero().ToDecimal(),
                                            Volume = trades.quantity.ToDecimal(),
                                            Id = FromIso8601(trades.time).Ticks.ToString()
                                        }
                                   }
                );

            }
            catch (Exception er)
            {
                SendLogMessage(er.Message, LogMessageType.Error);
            }
        }
        // end new logic router

        List<Security> _securitiesSubscrible = new List<Security>();

        public void UpDateLastPrices()
        {
            if (_securitiesSubscrible.Count == 0)
            {
                return;
            }

            string url = _url + "MarketDataService/GetLastPrices";

            Dictionary<string, string> param = new Dictionary<string, string>();

            string figiResponse = "[";

            for (int i = 0; i < _securitiesSubscrible.Count; i++)
            {
                figiResponse
                    += "\""
                    + _securitiesSubscrible[i].NameId
                    + "\"";

                if (i + 1 != _securitiesSubscrible.Count)
                {
                    figiResponse += ",";
                }
            }
            figiResponse += "]";

            //["BBG00ZHCX1X2"]

            param.Add("figi", figiResponse);

            var res = CreatePrivatePostQuery(url, param);

            if (res == null)
            {
                return;
            }

            LastPricesResponse priceResp = JsonConvert.DeserializeAnonymousType(res, new LastPricesResponse());

            List<Trade> newFakeTrades = new List<Trade>();

            for (int i = 0; i < priceResp.lastPrices.Count; i++)
            {
                LastPrice price = priceResp.lastPrices[i];
                Security mySec = GetSecurityByFigi(price.figi);

                if (mySec == null)
                {
                    continue;
                }

                Trade newTrade = new Trade();

                newTrade.SecurityNameCode = mySec.Name;
                newTrade.Time = FromIso8601(price.time);
                newTrade.Price = price.price.GetValue().ToStringWithNoEndZero().ToDecimal();
                newTrade.Volume = 1;
                newTrade.Id = newTrade.Time.Ticks.ToString();

                newFakeTrades.Add(newTrade);
            }

            if (NewTradesEvent != null)
            {
                NewTradesEvent(newFakeTrades);
            }

            if (_securitiesSubscrible.Count >= 3)
            {
                CreateFakeMd(newFakeTrades);
            }
        }

        private void CreateFakeMd(List<Trade> trades)
        {
            for (int i = 0; i < trades.Count; i++)
            {
                CreateFakeMdByTrade(trades[i]);
            }
        }

        private void CreateFakeMdByTrade(Trade trade)
        {
            List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

            MarketDepthLevel newBid = new MarketDepthLevel();
            newBid.Bid = trade.Volume;
            newBid.Price = trade.Price;
            bids.Add(newBid);


            MarketDepth depth = new MarketDepth();

            depth.SecurityNameCode = trade.SecurityNameCode;
            depth.Time = MaxTimeOnServer;
            depth.Bids = bids;

            List<MarketDepthLevel> asks = new List<MarketDepthLevel>();

            MarketDepthLevel newAsk = new MarketDepthLevel();
            newAsk.Ask = trade.Volume;
            newAsk.Price = trade.Price;
            asks.Add(newAsk);

            depth.Asks = asks;

            if (depth.Asks == null ||
                depth.Asks.Count == 0 ||
                depth.Bids == null ||
                depth.Bids.Count == 0)
            {
                return;
            }

            if (UpdateMarketDepth != null)
            {
                UpdateMarketDepth(depth);
            }
        }

        private DateTime MaxTimeOnServer
        {
            set
            {
                if (value < _timeOnServer)
                {
                    return;
                }

                _timeOnServer = value;
            }
            get
            {
                return _timeOnServer;
            }
        }

        private DateTime _timeOnServer;

        public void GetMarketDepth(Security security)
        {
            if (_securitiesSubscrible.Count == 0 ||
                _securitiesSubscrible.Count >= 3)
            {
                return;
            }

            string url = _url + "MarketDataService/GetOrderBook";

            Dictionary<string, string> param = new Dictionary<string, string>();

            param.Add("figi", security.NameId);
            param.Add("depth", "5");

            var res = CreatePrivatePostQuery(url, param);

            if (res == null)
            {
                return;
            }

            MarketDepthTinkoffResponse mdResp = JsonConvert.DeserializeAnonymousType(res, new MarketDepthTinkoffResponse());

            List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

            for (int i = 0; i < mdResp.bids.Count; i++)
            {
                MarketDepthLevel newBid = new MarketDepthLevel();
                newBid.Bid = Convert.ToDecimal(mdResp.bids[i].quantity);
                newBid.Price = mdResp.bids[i].price.GetValue().ToStringWithNoEndZero().ToDecimal();
                bids.Add(newBid);
            }

            MarketDepth depth = new MarketDepth();
            depth.SecurityNameCode = security.Name;
            depth.Time = MaxTimeOnServer;
            depth.Bids = bids;

            List<MarketDepthLevel> asks = new List<MarketDepthLevel>();

            for (int i = 0; i < mdResp.asks.Count; i++)
            {
                MarketDepthLevel newAsk = new MarketDepthLevel();
                newAsk.Ask = Convert.ToDecimal(mdResp.asks[i].quantity);
                newAsk.Price = mdResp.asks[i].price.GetValue().ToStringWithNoEndZero().ToDecimal();
                asks.Add(newAsk);
            }

            depth.Asks = asks;

            if (depth.Asks == null ||
                depth.Asks.Count == 0 ||
                depth.Bids == null ||
                depth.Bids.Count == 0)
            {
                return;
            }

            if (UpdateMarketDepth != null)
            {
                UpdateMarketDepth(depth);
            }
        }

        // портфели и позиции

        private List<Portfolio> _portfolios = new List<Portfolio>();

        private AccountsResponse _accountsResponse;

        public void UpdBalance()
        {
            try
            {
                if (_accountsResponse == null)
                {
                    string url = _url + "UsersService/GetAccounts";

                    var res = CreatePrivatePostQuery(url, new Dictionary<string, string>());

                    if (res == null)
                    {
                        return;
                    }

                    _accountsResponse = JsonConvert.DeserializeAnonymousType(res, new AccountsResponse());
                }

                for (int i = 0; i < _accountsResponse.accounts.Count; i++)
                {
                    Account account = _accountsResponse.accounts[i];

                    if (string.IsNullOrEmpty(account.id))
                    {
                        continue;
                    }

                    if (_accountsResponse.accounts[i].accessLevel.Equals("ACCOUNT_ACCESS_LEVEL_FULL_ACCESS") == false)
                    {
                        continue;
                    }

                    string portUrl = _url + "OperationsService/GetPortfolio";

                    Dictionary<string, string> param = new Dictionary<string, string>();

                    param.Add("accountId", account.id);

                    var resPort = CreatePrivatePostQuery(portUrl, param);

                    if (resPort == null)
                    {
                        continue;
                    }

                    PortfoliosResponse portfoliosResponse = JsonConvert.DeserializeAnonymousType(resPort, new PortfoliosResponse());

                    List<PositionOnBoard> byPower = GetPortfolioByPower(portfoliosResponse, account.id);

                    UpdatePositionsInPortfolio(portfoliosResponse, portfoliosResponse.positions, account.id, byPower);
                }

                if (UpdatePortfolio != null)
                {
                    for (int i = 0; i < _portfolios.Count; i++)
                    {
                        UpdatePortfolio(_portfolios[i]);
                    }
                }
            }
            catch (Exception e)
            {
                SendLogMessage(e.ToString(), LogMessageType.Error);
            }
        }

        public List<PositionOnBoard> GetPortfolioByPower(PortfoliosResponse portfolio, string porfolioId)
        {
            Portfolio myPortfolio = _portfolios.Find(p => p.Number == porfolioId);

            if (myPortfolio == null)
            {
                myPortfolio = new Portfolio();
                myPortfolio.Number = porfolioId;
                myPortfolio.ValueCurrent = 1;
                _portfolios.Add(myPortfolio);
            }

            List<PositionOnBoard> sectionByPower = new List<PositionOnBoard>();

            PositionOnBoard totalAmountBonds = ConverToPosOnBoard(portfolio.totalAmountBonds, porfolioId, "BondsBuyPower");
            sectionByPower.Add(totalAmountBonds);

            PositionOnBoard totalAmountFutures = ConverToPosOnBoard(portfolio.totalAmountFutures, porfolioId, "FutBuyPower");
            sectionByPower.Add(totalAmountFutures);

            PositionOnBoard totalAmountCurrencies = ConverToPosOnBoard(portfolio.totalAmountCurrencies, porfolioId, "CurBuyPower");
            sectionByPower.Add(totalAmountCurrencies);

            PositionOnBoard totalAmountShares = ConverToPosOnBoard(portfolio.totalAmountShares, porfolioId, "SharesBuyPower");
            sectionByPower.Add(totalAmountShares);

            PositionOnBoard totalAmountEtf = ConverToPosOnBoard(portfolio.totalAmountEtf, porfolioId, "EtfBuyPower");
            sectionByPower.Add(totalAmountEtf);

            return sectionByPower;
        }

        private PositionOnBoard ConverToPosOnBoard(CurrencyQuotation currency, string portfolioId, string sectionName)
        {
            PositionOnBoard totalAmount = new PositionOnBoard();
            totalAmount.PortfolioName = portfolioId;
            totalAmount.SecurityNameCode = sectionName + "_" + currency.currency;
            totalAmount.ValueBegin = currency.GetValue();
            totalAmount.ValueCurrent = totalAmount.ValueBegin;

            return totalAmount;
        }

        public void UpdatePositionsInPortfolio(PortfoliosResponse portfolio, List<TinkoffApiPosition> positions, string porfolioName, List<PositionOnBoard> byPower)
        {
            if (positions == null
                || _allSecurities == null
                || _allSecurities.Count == 0)
            {
                return;
            }

            Portfolio myPortfolio = _portfolios.Find(p => p.Number == porfolioName);

            if (myPortfolio == null)
            {
                return;
            }

            List<PositionOnBoard> sectionPoses = new List<PositionOnBoard>();

            for (int i = 0; i < positions.Count; i++)
            {
                Security mySec = GetSecurityByFigi(positions[i].figi);

                if (mySec == null)
                {
                    continue;
                }

                PositionOnBoard pos = new PositionOnBoard();
                pos.PortfolioName = porfolioName;
                pos.ValueCurrent = positions[i].quantity.GetValue();
                pos.ValueBegin = positions[i].quantity.GetValue();
                pos.SecurityNameCode = mySec.Name;

                sectionPoses.Add(pos);
            }

            for (int i = 0; i < sectionPoses.Count; i++)
            {
                myPortfolio.SetNewPosition(sectionPoses[i]);
            }

            for (int i = 0; i < byPower.Count; i++)
            {
                myPortfolio.SetNewPosition(byPower[i]);
            }

            // теперь обновляем очищенные позиции

            List<PositionOnBoard> allPoses = myPortfolio.GetPositionOnBoard();

            for (int i = 0; i < allPoses.Count; i++)
            {
                string name = allPoses[i].SecurityNameCode;

                if (sectionPoses.Find(s => s.SecurityNameCode == name) != null)
                {
                    continue;
                }

                if (byPower.Find(s => s.SecurityNameCode == name) != null)
                {
                    continue;
                }

                // позиция по бумаге обнулена. Правим
                allPoses[i].ValueCurrent = 0;
            }

        }

        #endregion

        #region работа с ордерами

        /// <summary>
        /// execute order
        /// исполнить ордер
        /// </summary>
        /// <param name="order"></param>
        public void ExecuteOrder(Order order)
        {
            lock (_lockOrder)
            {
                try
                {
                    if (IsConnected == false)
                    {
                        order.State = OrderStateType.Fail;

                        if (MyOrderEvent != null)
                        {
                            MyOrderEvent(order);
                        }

                        return;
                    }

                    string url = _url + "OrdersService/PostOrder";

                    Security security = _allSecurities.Find(sec => sec.Name == order.SecurityNameCode);

                    if (security == null)
                    {
                        return;
                    }

                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param.Add("figi", security.NameId);
                    param.Add("quantity", order.Volume.ToString());
                    param.Add("price", ToQuotation(order.Price));


                    string side = "ORDER_DIRECTION_BUY";
                    if (order.Side == Side.Sell)
                    {
                        side = "ORDER_DIRECTION_SELL";
                    }
                    param.Add("direction", side);

                    param.Add("accountId", order.PortfolioNumber);

                    string type = "ORDER_TYPE_LIMIT";
                    if (order.TypeOrder == OrderPriceType.Market)
                    {
                        type = "ORDER_TYPE_MARKET";
                    }
                    param.Add("orderType", type);

                    param.Add("orderId", order.NumberUser.ToString());
                    string json = null;
                    try
                    {
                        json = CreatePrivatePostQuery(url, param);
                    }
                    catch (Exception error)
                    {
                        if (error.GetType().Name == "WebException")
                        {
                            WebException wExp = (WebException)error;

                            SendLogMessage("Error on order Execution \n" + wExp.Message.ToString(), LogMessageType.Error);

                            SendLogMessage(wExp.Response.ToString() + "\n"
                                + wExp.Response.Headers.ToString() + "\n"
                                , LogMessageType.Error);
                        }
                        else
                        {
                            SendLogMessage("Error on order Execution \n" + error.Message.ToString(), LogMessageType.Error);
                        }

                        order.State = OrderStateType.Fail;
                        if (MyOrderEvent != null)
                        {
                            MyOrderEvent(order);
                        }

                        return;
                    }


                    if (json == null)
                    {
                        order.State = OrderStateType.Fail;

                        if (MyOrderEvent != null)
                        {
                            MyOrderEvent(order);
                        }

                        return;
                    }


                    OrderTinkoff orderResponse = JsonConvert.DeserializeAnonymousType(json, new OrderTinkoff());

                    //EXECUTION_REPORT_STATUS_UNSPECIFIED, 
                    //EXECUTION_REPORT_STATUS_FILL, 
                    //EXECUTION_REPORT_STATUS_REJECTED, 
                    //EXECUTION_REPORT_STATUS_CANCELLED, 
                    //EXECUTION_REPORT_STATUS_NEW, 
                    //EXECUTION_REPORT_STATUS_PARTIALLYFILL

                    if (orderResponse.executionReportStatus == "EXECUTION_REPORT_STATUS_REJECTED")
                    {
                        order.State = OrderStateType.Fail;
                    }
                    else
                    {
                        order.State = OrderStateType.Activ;
                        order.NumberMarket = orderResponse.orderId;
                        lock (_openOrdersArrayLocker)
                        {
                            _openedOrders.Add(order);
                        }
                    }

                    if (MyOrderEvent != null)
                    {
                        MyOrderEvent(order);
                    }

                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                }
            }
        }

        private string ToQuotation(decimal value)
        {
            string valueInStr = value.ToStringWithNoEndZero().Replace(",", ".");

            string units = valueInStr.Split('.')[0];
            string nano = "0";

            if (valueInStr.Split('.').Length > 1)
            {
                nano = valueInStr.Split('.')[1];
            }
            //650000000
            while (nano.Length < 9)
            {
                nano += "0";
            }

            // 23.11 -> {"units":"23","nano":110000000}"
            // 23.01 -> {"units":"23","nano":10000000}"

            //{"nano": 6,"units": "units"}
            //{"nano": 113,"units": "89"}

            string res = "{\"nano\": ";

            res += nano + ",\"units\": ";

            res += "\"" + units + "\"" + "}";

            return res;
        }

        private object _lockOrder = new object();

        /// <summary>
        /// cancel order
        /// отменить ордер
        /// </summary>
        public void CancelOrder(Order order)
        {
            lock (_lockOrder)
            {

                string url = _url + "OrdersService/CancelOrder";

                Security security = _allSecurities.Find(sec => sec.Name == order.SecurityNameCode);

                if (security == null)
                {
                    return;
                }

                Dictionary<string, string> param = new Dictionary<string, string>();

                param.Add("accountId", order.PortfolioNumber);

                param.Add("orderId", order.NumberMarket.ToString());

                string json = null;
                try
                {
                    json = CreatePrivatePostQuery(url, param);
                }
                catch (Exception error)
                {
                    if (error.GetType().Name == "WebException")
                    {
                        WebException wExp = (WebException)error;

                        SendLogMessage("Error on order Cansel \n" + wExp.Message.ToString(), LogMessageType.Error);

                        SendLogMessage(wExp.Response.ToString() + "\n"
                            + wExp.Response.Headers.ToString() + "\n"
                            , LogMessageType.Error);
                    }
                    else
                    {
                        SendLogMessage("Error on order Cansel \n" + error.Message.ToString(), LogMessageType.Error);
                    }

                    return;
                }

                if (json == null)
                {
                    return;
                }


                OrderCancelResponce orderResponse = JsonConvert.DeserializeAnonymousType(json, new OrderCancelResponce());

                Order canceledOrder = new Order();
                canceledOrder.NumberUser = order.NumberUser;
                canceledOrder.NumberMarket = order.NumberMarket;
                canceledOrder.SecurityNameCode = order.SecurityNameCode;
                canceledOrder.PortfolioNumber = order.PortfolioNumber;
                canceledOrder.Side = order.Side;
                canceledOrder.Price = order.Price;
                canceledOrder.TypeOrder = order.TypeOrder;
                canceledOrder.Volume = order.Volume;
                canceledOrder.IsStopOrProfit = order.IsStopOrProfit;
                canceledOrder.LifeTime = order.LifeTime;
                canceledOrder.SecurityClassCode = order.SecurityClassCode;
                canceledOrder.State = OrderStateType.Cancel;
                canceledOrder.TimeCancel = _timeOnServer;

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(canceledOrder);
                }
            }
        }

        /// <summary>
        /// chack order state
        /// проверить ордера на состояние
        /// </summary>
        public bool GetAllOrders(List<Order> oldOpenOrders)
        {
            for (int i = 0; i < oldOpenOrders.Count; i++)
            {
                if (oldOpenOrders[i].ServerType != ServerType.Tinkoff)
                {
                    continue;
                }
                _openedOrders.Add(oldOpenOrders[i]);
            }

            return false;
        }

        private List<Order> _openedOrders = new List<Order>();

        private string _openOrdersArrayLocker = "ordersLocker";

        public void UpdateMyTradesAndOrders()
        {
            try
            {
                lock (_openOrdersArrayLocker)
                {
                    for (int i = 0; i < _openedOrders.Count; i++)
                    {
                        Order orderFromArray = _openedOrders[i];

                        if (orderFromArray.State == OrderStateType.Done ||
                            orderFromArray.State == OrderStateType.Fail ||
                            orderFromArray.State == OrderStateType.Cancel)
                        {
                            _openedOrders.RemoveAt(i);
                            i--;
                            continue;
                        }

                        if (string.IsNullOrEmpty(orderFromArray.NumberMarket))
                        {
                            continue;
                        }

                        Order curOrder = GetOldOrderState(orderFromArray.PortfolioNumber, orderFromArray.NumberMarket, _openedOrders[i]);

                        if (curOrder == null)
                        {
                            continue;
                        }

                        if (curOrder.State == OrderStateType.Done ||
                            curOrder.State == OrderStateType.Fail ||
                            curOrder.State == OrderStateType.Cancel)
                        {
                            _openedOrders.RemoveAt(i);
                            i--;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                SendLogMessage(e.ToString(), LogMessageType.Error);
            }
        }

        private Order GetOldOrderState(string portfolioId, string orderId, Order oldOrder)
        {
            if (IsConnected == false)
            {
                return null;
            }

            OrderStateResponce orderResp = GetHistorycalOrderResponce(portfolioId, orderId);

            if (orderResp == null)
            {
                return null;
            }

            Order order = GenerateOrder(orderResp, oldOrder);

            if (orderResp.stages != null &&
                orderResp.stages.Count != 0)
            {
                List<MyTrade> myTrades = GenerateMyTrades(orderResp, oldOrder);

                if (MyTradeEvent != null)
                {
                    for (int i = 0; i < myTrades.Count; i++)
                    {
                        MyTradeEvent(myTrades[i]);
                    }
                }
            }

            if (MyOrderEvent != null)
            {
                MyOrderEvent(order);
            }

            return order;
        }

        private Order GenerateOrder(OrderStateResponce orderResp, Order oldOrder)
        {
            Order order = new Order();

            order.NumberUser = oldOrder.NumberUser;
            order.NumberMarket = oldOrder.NumberMarket;
            order.SecurityNameCode = oldOrder.SecurityNameCode;
            order.PortfolioNumber = oldOrder.PortfolioNumber;
            order.Side = oldOrder.Side;
            order.TypeOrder = oldOrder.TypeOrder;
            order.Volume = oldOrder.Volume;
            order.IsStopOrProfit = oldOrder.IsStopOrProfit;
            order.LifeTime = oldOrder.LifeTime;
            order.SecurityClassCode = oldOrder.SecurityClassCode;

            order.Price = oldOrder.Price;
            order.TimeCreate = oldOrder.TimeCreate;
            order.TimeCallBack = FromIso8601(orderResp.orderDate);

            /// <summary>
            /// EXECUTION_REPORT_STATUS_UNSPECIFIED, 
            /// EXECUTION_REPORT_STATUS_FILL, 
            /// EXECUTION_REPORT_STATUS_REJECTED, 
            /// EXECUTION_REPORT_STATUS_CANCELLED, 
            /// EXECUTION_REPORT_STATUS_NEW, 
            /// EXECUTION_REPORT_STATUS_PARTIALLYFILL
            /// </summary>

            if (orderResp.executionReportStatus == "EXECUTION_REPORT_STATUS_UNSPECIFIED")
            {
                order.State = OrderStateType.None;
            }
            else if (orderResp.executionReportStatus == "EXECUTION_REPORT_STATUS_FILL")
            {
                order.State = OrderStateType.Done;
            }
            else if (orderResp.executionReportStatus == "EXECUTION_REPORT_STATUS_REJECTED")
            {
                order.State = OrderStateType.Fail;
            }
            else if (orderResp.executionReportStatus == "EXECUTION_REPORT_STATUS_CANCELLED")
            {
                order.State = OrderStateType.Cancel;
            }
            else if (orderResp.executionReportStatus == "EXECUTION_REPORT_STATUS_NEW")
            {
                order.State = OrderStateType.Activ;
            }
            else if (orderResp.executionReportStatus == "EXECUTION_REPORT_STATUS_PARTIALLYFILL")
            {
                order.State = OrderStateType.Patrial;
            }

            return order;
        }

        private List<MyTrade> GenerateMyTrades(OrderStateResponce orderResp, Order oldOrder)
        {
            List<MyTrade> trades = new List<MyTrade>();

            for (int i = 0; i < orderResp.stages.Count; i++)
            {
                MyTrade trade = new MyTrade();
                trade.SecurityNameCode = oldOrder.SecurityNameCode;
                trade.Side = oldOrder.Side;
                trade.NumberOrderParent = orderResp.orderId;

                trade.Volume = orderResp.stages[i].quantity.ToDecimal();
                trade.Price = orderResp.stages[i].price.GetValue().ToStringWithNoEndZero().ToDecimal();
                trade.NumberTrade = orderResp.stages[i].tradeId;
                trade.Time = FromIso8601(orderResp.orderDate);
                trades.Add(trade);
            }

            return trades;
        }

        private OrderStateResponce GetHistorycalOrderResponce(string portfolioId, string orderId)
        {
            string url = _url + "OrdersService/GetOrderState";

            Dictionary<string, string> param = new Dictionary<string, string>();

            param.Add("accountId", portfolioId);

            param.Add("orderId", orderId);

            string json = null;
            try
            {
                json = CreatePrivatePostQuery(url, param);
            }
            catch (Exception error)
            {
                if (error.GetType().Name == "WebException")
                {
                    WebException wExp = (WebException)error;

                    SendLogMessage("Get order State Exception \n" + wExp.Message.ToString(), LogMessageType.Error);

                    SendLogMessage(wExp.Response.ToString() + "\n"
                        + wExp.Response.Headers.ToString() + "\n"
                        , LogMessageType.Error);
                }
                else
                {
                    SendLogMessage("Get order State Exception \n" + error.Message.ToString(), LogMessageType.Error);
                }

                return null;
            }

            if (json == null)
            {
                return null;
            }


            OrderStateResponce orderResponse = JsonConvert.DeserializeAnonymousType(json, new OrderStateResponce());

            return orderResponse;
        }

        #endregion

        #region outgoing events / исходящие события

        /// <summary>
        /// my new orders
        /// новые мои ордера
        /// </summary>
        public event Action<Order> MyOrderEvent;

        /// <summary>
        /// my new trades
        /// новые мои сделки
        /// </summary>
        public event Action<MyTrade> MyTradeEvent;

        /// <summary>
        /// portfolios updated
        /// обновились портфели
        /// </summary>
        public event Action<Portfolio> UpdatePortfolio;

        /// <summary>
        /// new security in the system
        /// новые бумаги в системе
        /// </summary>
        public event Action<List<Security>> UpdatePairs;

        /// <summary>
        /// depth updated
        /// обновился стакан
        /// </summary>
        public event Action<MarketDepth> UpdateMarketDepth;

        /// <summary>
        /// ticks updated
        /// обновились тики
        /// </summary>
        public event Action<List<Trade>> NewTradesEvent;

        /// <summary>
        /// API connection established
        /// соединение с API установлено
        /// </summary>
        public event Action Connected;

        /// <summary>
        /// API connection lost
        /// соединение с API разорвано
        /// </summary>
        public event Action Disconnected;

        #endregion

        #region log messages / сообщения для лога

        /// <summary>
        /// add a new log message
        /// добавить в лог новое сообщение
        /// </summary>
        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        /// <summary>
        /// send exeptions
        /// отправляет исключения
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }
}
