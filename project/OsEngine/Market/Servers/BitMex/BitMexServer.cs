/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.BitMex.BitMexEntity;
using OsEngine.Market.Servers.Entity;

namespace OsEngine.Market.Servers.BitMex
{
    /// <summary>
    /// Bitmex server
    /// сервер Bitmex
    /// </summary>
    public class BitMexServer : AServer
    {
        public BitMexServer()
        {
            BitMexServerRealization realization = new BitMexServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamId, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");
            CreateParameterBoolean("IsDemo", false);
        }
        
        public List<Candle> GetBitMexCandleHistory(string nameSec, TimeSpan tf)
        {
            return ((BitMexServerRealization)ServerRealization).GetBitMexCandleHistory(nameSec, tf);
        }
    }

    public class BitMexServerRealization : IServerRealization
    {
        public BitMexServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread ordersExecutor = new Thread(ExecutorOrdersThreadArea);
            ordersExecutor.CurrentCulture = new CultureInfo("ru-RU");
            ordersExecutor.IsBackground = true;
            ordersExecutor.Start();
        }

        /// <summary>
        /// server status
        /// статус сервера
        /// </summary>
        public ServerConnectStatus ServerStatus { get; set; }

        /// <summary>
        /// server type
        /// тип сервера
        /// </summary>
        public ServerType ServerType
        {
            get { return ServerType.BitMex; }
        }

        /// <summary>
        /// server parameters for server
        /// параметры подключения для сервера
        /// </summary>
        public List<IServerParameter> ServerParameters { get; set; }

        /// <summary>
        /// sever time
        /// время сервера
        /// </summary>
        public DateTime ServerTime { get; set; }

        /// <summary>
        /// BitMex client
        /// </summary>
        private BitMexClient _client;

        /// <summary>
        /// last start server time
        /// время последнего старта сервера
        /// </summary>
        private DateTime _lastStartServerTime;

        /// <summary>
        /// securities
        /// бумаги
        /// </summary>
        private List<Security> _securities;

        /// <summary>
        /// portfolios
        /// портфели
        /// </summary>
        private List<Portfolio> _portfolios;

        /// <summary>
        /// candles
        /// свечи
        /// </summary>
        private List<Candle> _candles;

        /// <summary>
        /// connection
        /// подключение
        /// </summary>
        public void Connect()
        {
            if (_client == null)
            {
                _client = new BitMexClient();
                _client.Connected += _client_Connected;
                _client.Disconnected += _client_Disconnected;
                _client.UpdatePortfolio += _client_UpdatePortfolio;
                _client.UpdatePosition += _client_UpdatePosition;
                _client.UpdateMarketDepth += _client_UpdateMarketDepth;
                _client.NewTradesEvent += _client_NewTrades;
                _client.MyTradeEvent += _client_NewMyTrades;
                _client.MyOrderEvent += _client_MyOrderEvent;
                _client.UpdateSecurity += _client_UpdateSecurity;
                _client.BitMexLogMessageEvent += _client_SendLogMessage;
                _client.ErrorEvent += _client_ErrorEvent;
            }

            _lastStartServerTime = DateTime.Now;

            if (((ServerParameterBool)ServerParameters[2]).Value)
            {
                _client.Domain = "https://testnet.bitmex.com";
                _client.ServerAdres = "wss://testnet.bitmex.com/realtime";
            }
            else
            {
                _client.Domain = "https://www.bitmex.com";
                _client.ServerAdres = "wss://www.bitmex.com/realtime";
            }
            _client.Id = ((ServerParameterString)ServerParameters[0]).Value;
            _client.SecKey = ((ServerParameterPassword)ServerParameters[1]).Value;

            _client.Connect();
        }

        /// <summary>
        /// dispose API
        /// осыободить апи
        /// </summary>
        public void Dispose()
        {
            if (_client != null)
            {
                _client.Disconnect();

                _client.Connected -= _client_Connected;
                _client.Disconnected -= _client_Disconnected;
                _client.UpdatePortfolio -= _client_UpdatePortfolio;
                _client.UpdateMarketDepth -= _client_UpdateMarketDepth;
                _client.NewTradesEvent -= _client_NewTrades;
                _client.MyTradeEvent -= _client_NewMyTrades;
                _client.MyOrderEvent -= _client_MyOrderEvent;
                _client.UpdateSecurity -= _client_UpdateSecurity;
                _client.BitMexLogMessageEvent -= _client_SendLogMessage;
                _client.ErrorEvent -= _client_ErrorEvent;
            }

            _client = null;
            ServerStatus = ServerConnectStatus.Disconnect;
            _subscribedSec = new List<string>();
            _portfolioStarted = false;
            DisconnectEvent?.Invoke();
        }

        /// <summary>
        /// take current state of orders
        /// взять текущие состояни ордеров
        /// </summary>
        public void GetOrdersState(List<Order> orders)
        {
            GetAllOrders(orders);
        }

        /// <summary>
        /// check order state
        /// проверить ордера на состояние
        /// </summary>
        /// <param name="oldOpenOrders"></param>
        /// <returns></returns>
        public bool GetAllOrders(List<Order> oldOpenOrders)
        {
            List<string> namesSec = new List<string>();

            for (int i = 0; i < oldOpenOrders.Count; i++)
            {
                if (namesSec.Find(name => name.Contains(oldOpenOrders[i].SecurityNameCode)) == null)
                {
                    namesSec.Add(oldOpenOrders[i].SecurityNameCode);
                }
            }

            string endPoint = "/order";
            List<DatumOrder> allOrders = new List<DatumOrder>();

            for (int i = 0; i < namesSec.Count; i++)
            {
                Dictionary<string, string> param = new Dictionary<string, string>();
                param["symbol"] = namesSec[i];
                //param["filter"] = "{\"open\":true}";
                //param["columns"] = "";
                param["count"] = 30.ToString();
                //param["start"] = 0.ToString();
                param["reverse"] = true.ToString();
                //param["startTime"] = "";
                //param["endTime"] = "";

                var res = _client.CreateQuery("GET", endPoint, param, true);

                List<DatumOrder> orders = JsonConvert.DeserializeAnonymousType(res, new List<DatumOrder>());

                if (orders != null && orders.Count != 0)
                {
                    allOrders.AddRange(orders);
                }
            }

            for (int i = 0; i < oldOpenOrders.Count; i++)
            {
                DatumOrder myOrder = allOrders.Find(ord => ord.orderID == oldOpenOrders[i].NumberMarket);

                if (myOrder == null)
                {
                    continue;
                }

                if (String.IsNullOrEmpty(myOrder.clOrdID))
                {
                    continue;
                }

                if (myOrder.ordStatus == "New")
                {
                    continue;
                }
                else if (myOrder.ordStatus == "Filled")
                {// order is executed
                    MyTrade trade = new MyTrade();
                    trade.NumberOrderParent = oldOpenOrders[i].NumberMarket;
                    trade.NumberTrade = NumberGen.GetNumberOrder(StartProgram.IsOsTrader).ToString();
                    trade.SecurityNameCode = oldOpenOrders[i].SecurityNameCode;
                    trade.Time = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(myOrder.timestamp));
                    trade.Side = oldOpenOrders[i].Side;

                    if (MyTradeEvent != null)
                    {
                        MyTradeEvent(trade);
                    }
                }
                else //if (myOrder.ordStatus == "Canceled")
                {
                    Order newOrder = new Order();
                    newOrder.NumberMarket = oldOpenOrders[i].NumberMarket;
                    newOrder.NumberUser = oldOpenOrders[i].NumberUser;
                    newOrder.SecurityNameCode = oldOpenOrders[i].SecurityNameCode;
                    newOrder.State = OrderStateType.Cancel;
                    newOrder.Volume = oldOpenOrders[i].Volume;
                    newOrder.VolumeExecute = oldOpenOrders[i].VolumeExecute;
                    newOrder.Price = oldOpenOrders[i].Price;
                    newOrder.TypeOrder = oldOpenOrders[i].TypeOrder;
                    newOrder.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(myOrder.timestamp));
                    newOrder.TimeCancel = newOrder.TimeCallBack;
                    newOrder.ServerType = ServerType.BitMex;
                    newOrder.PortfolioNumber = oldOpenOrders[i].PortfolioNumber;

                    if (MyOrderEvent != null)
                    {
                        MyOrderEvent(newOrder);
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// subscribe to candles, all trades
        /// подписаться на свечи, все сделки 
        /// </summary>
        public void Subscrible(Security security)
        {
            SubcribeDepthTradeOrder(security.Name);
        }

        /// <summary>
        /// take candle history for period
        /// взять историю свечек за период
        /// </summary>
        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder,
            DateTime startTime, DateTime endTime, DateTime actualTime)
        {
           return GetCandles(security.Name, timeFrameBuilder,startTime);
        }

        /// <summary>
        /// take candle history
        /// взять историю свечек
        /// </summary>
        /// <param name="security"></param>
        /// <param name="tf"></param>
        /// <returns></returns>
        private List<Candle> GetCandles(string security, TimeFrameBuilder timeFrameBuilder, DateTime timeStart)
        {
            try
            {
                lock (_getCandles)
                {
                    string tf = "1m";

                    if (timeFrameBuilder.TimeFrame == TimeFrame.Min5)
                    {
                        tf = "5m";
                    }
                    else if (timeFrameBuilder.TimeFrame == TimeFrame.Hour1)
                    {
                        tf = "1h";
                    }

                    int shift = Convert.ToInt32(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes);

                    // 1m,5m,1h

                    List<BitMexCandle> allbmcandles = new List<BitMexCandle>();

                    DateTime actualTime = DateTime.Now.AddDays(1);

                    _candles = null;

                    while  (actualTime > timeStart)
                    {
                        
                        actualTime = TimeZoneInfo.ConvertTimeToUtc(actualTime);

                        string end = actualTime.ToString("yyyy-MM-dd HH:mm");

                        var param = new Dictionary<string, string>();
                        param["symbol"] = security;
                        param["count"] = 500.ToString();
                        param["binSize"] = tf;
                        param["reverse"] = true.ToString();
                        //param["startTime"] = start;
                        param["endTime"] = end;
                        param["partial"] = true.ToString();

                        try
                        {
                            var res = _client.CreateQuery("GET", "/trade/bucketed", param);

                            List<BitMexCandle> bmcandles =
                                JsonConvert.DeserializeAnonymousType(res, new List<BitMexCandle>());
                            bmcandles.Reverse();
                            allbmcandles.InsertRange(0,bmcandles);

                            actualTime = Convert.ToDateTime(bmcandles[0].timestamp)
                                .Subtract(TimeSpan.FromMinutes(shift));
                        }
                        catch
                        {
                            // ignored
                        }
                        Thread.Sleep(2000);
                    }

                    if (_candles == null)
                    {
                        _candles = new List<Candle>();
                    }

                    foreach (var bitMexCandle in allbmcandles)
                    {
                        Candle newCandle = new Candle();

                        newCandle.Open = bitMexCandle.open;
                        newCandle.High = bitMexCandle.high;
                        newCandle.Low = bitMexCandle.low;
                        newCandle.Close = bitMexCandle.close;
                        newCandle.TimeStart = Convert.ToDateTime(bitMexCandle.timestamp).Subtract(TimeSpan.FromMinutes(shift));
                        newCandle.Volume = bitMexCandle.volume;

                        _candles.Add(newCandle);
                    }
                   // _candles.Reverse();
                    return _candles;
                }
            }
            catch (Exception error)
            {
                _client_SendLogMessage(error.ToString(), LogMessageType.Error);
                return null;
            }
        }

        /// <summary>
        /// take tick data on instrument for period
        /// взять тиковые данные по инструменту за определённый период
        /// </summary>
        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime lastDate)
        {
            List<Trade> lastTrades = new List<Trade>();

            lastDate = endTime;
           

            while (lastDate > startTime)
            {
                lastDate = TimeZoneInfo.ConvertTimeToUtc(lastDate);
                List<Trade> trades = GetTickHistoryToSecurity(security, lastDate);

                if (trades == null ||
                    trades.Count == 0)
                {
                    Thread.Sleep(5000);
                    continue;
                }

                for (int i2 = 0; i2 < trades.Count; i2++)
                {
                    Trade ft = lastTrades.Find(x => x.Id == trades[i2].Id);

                    if (ft != null)
                    {
                        trades.RemoveAt(i2);
                    }
                }

                if (trades.Count == 0)
                {
                    Thread.Sleep(5000);
                    continue;
                }

                lastDate = trades[0].Time;

                lastTrades.InsertRange(0,trades);

                Thread.Sleep(3000);
            }

            return lastTrades;
        }

        public List<Trade> GetTickHistoryToSecurity(Security security, DateTime endTime)
        {
            try
            {
                lock (_lockerStarter)
                {
                    _client_SendLogMessage("LOAD BLOCK: "+endTime.ToString(), LogMessageType.System);
                    List<Trade> trades = new List<Trade>();

                    Dictionary<string, string> param = new Dictionary<string, string>();

                    //string start = startTime.ToString("yyyy-MM-dd HH:mm:ss");
                    string end = endTime.ToString("yyyy-MM-dd HH:mm:ss");

                    param["symbol"] = security.Name;
                    param["count"] = 500.ToString();
                    param["start"] = 0.ToString();
                    param["reverse"] = true.ToString();
                    // param["startTime"] = start;
                    param["endTime"] = end;

                    var res = _client.CreateQuery("GET", "/trade", param);

                    List<TradeBitMex> tradeHistory = JsonConvert.DeserializeAnonymousType(res, new List<TradeBitMex>());

                    tradeHistory.Reverse();

                    foreach (var oneTrade in tradeHistory)
                    {
                        if (string.IsNullOrEmpty(oneTrade.price))
                        {
                            continue;
                        }

                        Trade trade = new Trade();
                        trade.SecurityNameCode = oneTrade.symbol;
                        trade.Id = oneTrade.trdMatchID;
                        trade.Time = Convert.ToDateTime(oneTrade.timestamp);
                        trade.Price = oneTrade.price.ToDecimal();
                        trade.Volume = oneTrade.size.ToDecimal();
                        trade.Side = oneTrade.side == "Sell" ? Side.Sell : Side.Buy;
                        trades.Add(trade);
                    }

                    return trades;
                }
            }
            catch (Exception error)
            {
                _client_SendLogMessage(error.ToString(), LogMessageType.Error);
                return null;
            }
        }



        private bool _portfolioStarted = false; // already subscribed to portfolios / уже подписались на портфели

        /// <summary>
        /// request portfolios
        /// запросить портфели
        /// </summary>
        public void GetPortfolios()
        {
            if (!_portfolioStarted)
            {
                string queryPortf = "{\"op\": \"subscribe\", \"args\": [\"margin\"]}";
                string queryPos = "{\"op\": \"subscribe\", \"args\": [\"position\"]}";

                _client.SendQuery(queryPortf);
                Thread.Sleep(500);
                _client.SendQuery(queryPos);
                _portfolioStarted = true;
            }
        }

        /// <summary>
        /// get instruments
        /// получить инструменты
        /// </summary>
        public void GetSecurities()
        {
            _client.GetSecurities();
        }

// data subscription
// Подпись на данные

        /// <summary>
        /// downloading candle master
        /// мастер загрузки свечек
        /// </summary>
        private CandleManager _candleManager;

        /// <summary>
        /// securities already subscribed to data updates
        /// бумаги уже подписанные на обновления данных
        /// </summary>
        private List<string> _subscribedSec = new List<string>();

        /// <summary>
        /// multi-threaded access locker in StartThisSecurity
        /// объект блокирующий многопоточный доступ в StartThisSecurity
        /// </summary>
        private object _lockerStarter = new object();

        /// <summary>
        /// start downloading instrument data
        /// начать выкачивать данный иснтрументн
        /// </summary>
        /// <param name="namePaper"> instrument name / название инструмента</param>
        /// <returns> in the case of a successful launch returns CandleSeries, the object generating candles / в случае успешного запуска возвращает CandleSeries, объект генерирующий свечи</returns>
        public void SubcribeDepthTradeOrder(string namePaper)
        {
            try
            {
                if (_lastStartServerTime.AddSeconds(15) > DateTime.Now)
                {
                    return;
                }

                lock (_lockerStarter)
                {
                    if (namePaper == "")
                    {
                        return;
                    }
                    // need to start the server if it is still disabled / надо запустить сервер если он ещё отключен
                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        //MessageBox.Show("Сервер не запущен. Скачивание данных прервано. Инструмент: " + namePaper);
                        return;
                    }

                    if (_securities == null || _portfolios == null)
                    {
                        Thread.Sleep(5000);
                        return;
                    }

                    Security security = null;

                    for (int i = 0; _securities != null && i < _securities.Count; i++)
                    {
                        if (_securities[i].Name == namePaper)
                        {
                            security = _securities[i];
                            break;
                        }
                    }

                    if (security == null)
                    {
                        return;
                    }

                    _candles = null;

                    if (_subscribedSec.Find(s => s == namePaper) == null)
                    {


                        string queryQuotes = "{\"op\": \"subscribe\", \"args\": [\"orderBookL2:" + security.Name + "\"]}";

                        _client.SendQuery(queryQuotes);



                        string queryTrades = "{\"op\": \"subscribe\", \"args\": [\"trade:" + security.Name + "\"]}";

                        _client.SendQuery(queryTrades);



                        string queryMyTrades = "{\"op\": \"subscribe\", \"args\": [\"execution:" + security.Name + "\"]}";

                        _client.SendQuery(queryMyTrades);



                        string queryorders = "{\"op\": \"subscribe\", \"args\": [\"order:" + security.Name + "\"]}";

                        _client.SendQuery(queryorders);
                        _subscribedSec.Add(namePaper);

                    }
                    Thread.Sleep(300);
                }
            }
            catch (Exception error)
            {
                _client_SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private object _getCandles = new object();

        /// <summary>
        /// take candle history
        /// взять историю свечек
        /// </summary>
        /// <param name="security"></param>
        /// <param name="tf"></param>
        /// <param name="shift"></param>
        /// <returns></returns>
        private List<Candle> GetCandlesTf(string security, string tf, int shift)
        {
            try
            {
                lock (_getCandles)
                {
                    List<BitMexCandle> allbmcandles = new List<BitMexCandle>();

                    DateTime endTime;
                    DateTime startTime = DateTime.MinValue;

                    _candles = null;

                    for (int i = 0; i < 10; i++)
                    {
                        Thread.Sleep(500);
                        if (i == 0)
                        {
                            endTime = DateTime.UtcNow.Add(TimeSpan.FromMinutes(shift));
                            startTime = endTime.Subtract(TimeSpan.FromMinutes(480));
                        }
                        else
                        {
                            endTime = startTime.Subtract(TimeSpan.FromMinutes(1));
                            startTime = startTime.Subtract(TimeSpan.FromMinutes(480));
                        }

                        string end = endTime.ToString("yyyy-MM-dd HH:mm");
                        string start = startTime.ToString("yyyy-MM-dd HH:mm");

                        var param = new Dictionary<string, string>();
                        param["symbol"] = security;
                        param["count"] = 500.ToString();
                        param["binSize"] = tf;
                        param["reverse"] = true.ToString();
                        param["startTime"] = start;
                        param["endTime"] = end;
                        param["partial"] = true.ToString();

                        try
                        {
                            var res = _client.CreateQuery("GET", "/trade/bucketed", param);

                            List<BitMexCandle> bmcandles =
                                JsonConvert.DeserializeAnonymousType(res, new List<BitMexCandle>());

                            allbmcandles.AddRange(bmcandles);
                        }
                        catch
                        {
                            // ignored
                        }
                    }

                    if (_candles == null)
                    {
                        _candles = new List<Candle>();
                    }

                    foreach (var bitMexCandle in allbmcandles)
                    {
                        Candle newCandle = new Candle();

                        newCandle.Open = bitMexCandle.open;
                        newCandle.High = bitMexCandle.high;
                        newCandle.Low = bitMexCandle.low;
                        newCandle.Close = bitMexCandle.close;
                        newCandle.TimeStart = Convert.ToDateTime(bitMexCandle.timestamp).Subtract(TimeSpan.FromMinutes(shift));
                        newCandle.Volume = bitMexCandle.volume;

                        _candles.Add(newCandle);
                    }
                    _candles.Reverse();
                    return _candles;
                }
            }
            catch (Exception error)
            {
                _client_SendLogMessage(error.ToString(), LogMessageType.Error);
                return null;
            }
        }

        /// <summary>
        /// multi-threaded access locker to GetBitMexCandleHistory
        /// блокиратор многопоточного доступа к GetBitMexCandleHistory
        /// </summary>
        private readonly object _getCandlesLocker = new object();

        /// <summary>
        /// take instrument candle
        /// взять свечи по инструменту
        /// </summary>
        /// <param name="security"> short security name / короткое название бумаги</param>
        /// <param name="timeSpan"> timeframe / таймФрейм</param>
        /// <returns>failure will return null / в случае неудачи вернётся null</returns>
        public List<Candle> GetBitMexCandleHistory(string security, TimeSpan timeSpan)
        {
            try
            {
                lock (_getCandlesLocker)
                {
                    if (timeSpan.TotalMinutes > 60 ||
                        timeSpan.TotalMinutes < 1)
                    {
                        return null;
                    }

                    if (timeSpan.Minutes == 1)
                    {
                        return GetCandlesTf(security, "1m", 1);
                    }
                    if (timeSpan.Minutes == 5)
                    {
                        return GetCandlesTf(security, "5m", 5);
                    }
                    if (timeSpan.Minutes == 00)
                    {
                        return GetCandlesTf(security, "1h", 60);
                    }
                    else
                    {
                        return СandlesBuilder(security, timeSpan.Minutes);
                    }
                }
            }
            catch (Exception error)
            {
                _client_SendLogMessage(error.ToString(), LogMessageType.Error);
                return null;
            }
        }

        /// <summary>
        /// method returns candles of a larger timeframe made from smaller
        /// метод возврящает свечи большего таймфрейма, сделанные из меньшего
        /// </summary>
        /// <param name="security"></param>
        /// <param name="tf"></param>
        /// <returns></returns>
        private List<Candle> СandlesBuilder(string security, int tf)
        {
            List<Candle> candles1M;
            int a;
            if (tf >= 10)
            {
                candles1M = GetCandlesTf(security, "5m", 5);
                a = tf / 5;
            }
            else
            {
                candles1M = GetCandlesTf(security, "1m", 1);
                a = tf / 1;
            }

            int index = candles1M.FindIndex(can => can.TimeStart.Minute % tf == 0);

            List<Candle> candlestf = new List<Candle>();

            int count = 0;

            Candle newCandle = new Candle();

            for (int i = index; i < candles1M.Count; i++)
            {
                count++;
                if (count == 1)
                {
                    newCandle = new Candle();
                    newCandle.Open = candles1M[i].Open;
                    newCandle.TimeStart = candles1M[i].TimeStart;
                    newCandle.Low = Decimal.MaxValue;
                }

                newCandle.High = candles1M[i].High > newCandle.High
                    ? candles1M[i].High
                    : newCandle.High;

                newCandle.Low = candles1M[i].Low < newCandle.Low
                    ? candles1M[i].Low
                    : newCandle.Low;

                newCandle.Volume += candles1M[i].Volume;

                if (i == candles1M.Count - 1 && count != a)
                {
                    newCandle.Close = candles1M[i].Close;
                    newCandle.State = CandleState.None;
                    candlestf.Add(newCandle);
                }

                if (count == a)
                {
                    newCandle.Close = candles1M[i].Close;
                    newCandle.State = CandleState.Finished;
                    candlestf.Add(newCandle);
                    count = 0;
                }
            }

            return candlestf;
        }

        // event implementation
        // реализация событий

        /// <summary>
        /// client connected
        /// клиент подключился
        /// </summary>
        void _client_Connected()
        {
            if (ConnectEvent != null)
            {
                ConnectEvent();
            }
            ServerStatus = ServerConnectStatus.Connect;
        }

        /// <summary>
        /// client disconnected
        /// клиент отключился
        /// </summary>
        void _client_Disconnected()
        {
            if (DisconnectEvent != null)
            {
                DisconnectEvent();
            }
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        /// <summary>
        /// portfolios updated
        /// обновились портфели
        /// </summary>
        void _client_UpdatePortfolio(BitMexPortfolio portf)
        {
            try
            {
                if (_portfolios == null)
                {
                    _portfolios = new List<Portfolio>();
                }
                Portfolio osPortf = _portfolios.Find(p => p.Number == portf.data[0].account.ToString());

                if (osPortf == null)
                {
                    osPortf = new Portfolio();
                    osPortf.Number = portf.data[0].account.ToString();
                    osPortf.ValueBegin = Convert.ToDecimal(portf.data[0].walletBalance) / 100000000m;
                    osPortf.ValueCurrent = Convert.ToDecimal(portf.data[0].walletBalance) / 100000000m;
                    _portfolios.Add(osPortf);
                }

                if (portf.data[0].walletBalance == 0)
                {
                    return;
                }

                if (portf.action == "update")
                {
                    osPortf.ValueCurrent = Convert.ToDecimal(portf.data[0].walletBalance) / 100000000m;
                    osPortf.Profit = portf.data[0].unrealisedPnl;

                }
                else
                {
                    osPortf.ValueCurrent = Convert.ToDecimal(portf.data[0].walletBalance) / 100000000m;
                    osPortf.Profit = portf.data[0].unrealisedPnl;
                }

                if (PortfolioEvent != null)
                {
                    PortfolioEvent(_portfolios);
                }
            }
            catch (Exception error)
            {
                _client_SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// positions updated
        /// обновились позиции
        /// </summary>
        private void _client_UpdatePosition(BitMexPosition pos)
        {
            if (_portfolios != null)
            {
                for (int i = 0; i < pos.data.Count; i++)
                {
                    Portfolio needPortfolio = _portfolios.Find(p => p.Number == pos.data[i].account.ToString());

                    PositionOnBoard newPos = new PositionOnBoard();

                    newPos.PortfolioName = pos.data[i].account.ToString();
                    newPos.SecurityNameCode = pos.data[i].symbol;

                    if (string.IsNullOrEmpty(pos.data[i].currentQty) == false)
                    {
                        newPos.ValueCurrent =
                                pos.data[i].currentQty.ToDecimal();
                    }
                    

                    needPortfolio.SetNewPosition(newPos);
                }

                if (PortfolioEvent != null)
                {
                    PortfolioEvent(_portfolios);
                }
            }
        }

        private object _quoteLock = new object();

        /// <summary>
        /// depth
        /// стакан
        /// </summary>
        private List<MarketDepth> _depths = new List<MarketDepth>();

        /// <summary>
        /// updated depth came
        /// пришел обновленный стакан
        /// </summary>
        void _client_UpdateMarketDepth(BitMexQuotes quotes)
        {
            try
            {
                MarketDepth depth = _depths.Find(d => d.SecurityNameCode == quotes.data[0].symbol);

                lock (_quoteLock)
                {
                    

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
                            if (quotes.data[i].side == "Sell")
                            {
                                ascs.Add(new MarketDepthLevel()
                                {
                                    Ask = quotes.data[i].size,
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
                                    Bid = quotes.data[i].size,
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
                            if (quotes.data[i].side == "Sell")
                            {
                                if (depth.Asks.Find(asc => asc.Id == quotes.data[i].id) != null)
                                {
                                    depth.Asks.Find(asc => asc.Id == quotes.data[i].id).Ask = quotes.data[i].size;
                                }
                                else
                                {
                                    if (quotes.data[i].price == null || 
                                        quotes.data[i].price == "0")
                                    {
                                        continue;
                                    }

                                    long id = quotes.data[i].id;

                                    for (int j = 0; j < depth.Asks.Count; j++)
                                    {
                                        if (j == 0 && id > depth.Asks[j].Id)
                                        {
                                            depth.Asks.Insert(j, new MarketDepthLevel()
                                            {
                                                Ask = quotes.data[i].size,
                                                Price = quotes.data[i].price.ToDecimal(),
                                                Id = quotes.data[i].id
                                            });
                                        }
                                        else if (j != depth.Asks.Count - 1 && id < depth.Asks[j].Id && id > depth.Asks[j + 1].Id)
                                        {
                                            depth.Asks.Insert(j + 1, new MarketDepthLevel()
                                            {
                                                Ask = quotes.data[i].size,
                                                Price = quotes.data[i].price.ToDecimal(),
                                                Id = quotes.data[i].id
                                            });
                                        }
                                        else if (j == depth.Asks.Count - 1 && id < depth.Asks[j].Id)
                                        {
                                            depth.Asks.Add(new MarketDepthLevel()
                                            {
                                                Ask = quotes.data[i].size,
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
                            else // (quotes.data[i].side == "Buy")
                            {
                                if (quotes.data[i].price == null || 
                                    quotes.data[i].price == "0")
                                {
                                    continue;
                                }

                                long id = quotes.data[i].id;

                                for (int j = 0; j < depth.Bids.Count; j++)
                                {
                                    if (j == 0 && id < depth.Bids[i].Id)
                                    {
                                        depth.Bids.Insert(j, new MarketDepthLevel()
                                        {
                                            Bid = quotes.data[i].size,
                                            Price = quotes.data[i].price.ToDecimal(),
                                            Id = quotes.data[i].id
                                        });
                                    }
                                    else if (j != depth.Bids.Count - 1 && id > depth.Bids[i].Id && id < depth.Bids[j + 1].Id)
                                    {
                                        depth.Bids.Insert(j + 1, new MarketDepthLevel()
                                        {
                                            Bid = quotes.data[i].size,
                                            Price = quotes.data[i].price.ToDecimal(),
                                            Id = quotes.data[i].id
                                        });
                                    }
                                    else if (j == depth.Bids.Count - 1 && id > depth.Bids[j].Id)
                                    {
                                        depth.Bids.Add(new MarketDepthLevel()
                                        {
                                            Bid = quotes.data[i].size,
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

                        depth.Time = ServerTime;
                    }

                    if (quotes.action == "delete")
                    {
                        if (depth == null)
                            return;

                        for (int i = 0; i < quotes.data.Count; i++)
                        {
                            if (quotes.data[i].side == "Sell")
                            {
                                depth.Asks.Remove(depth.Asks.Find(asc => asc.Id == quotes.data[i].id));
                            }
                            else
                            {
                                depth.Bids.Remove(depth.Bids.Find(bid => bid.Id == quotes.data[i].id));
                            }
                        }

                        depth.Time = ServerTime;
                    }
                }

                if (quotes.action == "insert")
                {
                    if (depth == null)
                        return;

                    for (int i = 0; i < quotes.data.Count; i++)
                    {
                        if (quotes.data[i].price == null || 
                            quotes.data[i].price == "0")
                        {
                            continue;
                        }
                        if (quotes.data[i].side == "Sell")
                        {
                            long id = quotes.data[i].id;

                            for (int j = 0; j < depth.Asks.Count; j++)
                            {
                                if (j == 0 && id > depth.Asks[j].Id)
                                {
                                    depth.Asks.Insert(j, new MarketDepthLevel()
                                    {
                                        Ask = quotes.data[i].size,
                                        Price = quotes.data[i].price.ToDecimal(),
                                        Id = quotes.data[i].id
                                    });
                                }
                                else if (j != depth.Asks.Count - 1 && id < depth.Asks[j].Id && id > depth.Asks[j + 1].Id)
                                {
                                    depth.Asks.Insert(j + 1, new MarketDepthLevel()
                                    {
                                        Ask = quotes.data[i].size,
                                        Price = quotes.data[i].price.ToDecimal(),
                                        Id = quotes.data[i].id
                                    });
                                }
                                else if (j == depth.Asks.Count - 1 && id < depth.Asks[j].Id)
                                {
                                    depth.Asks.Add(new MarketDepthLevel()
                                    {
                                        Ask = quotes.data[i].size,
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
                            long id = quotes.data[i].id;

                            for (int j = 0; j < depth.Bids.Count; j++)
                            {
                                if (j == 0 && id < depth.Bids[j].Id)
                                {
                                    depth.Bids.Insert(j, new MarketDepthLevel()
                                    {
                                        Bid = quotes.data[i].size,
                                        Price = quotes.data[i].price.ToDecimal(),
                                        Id = quotes.data[i].id
                                    });
                                }
                                else if (j != depth.Bids.Count - 1 && id > depth.Bids[j].Id && id < depth.Bids[j + 1].Id)
                                {
                                    depth.Bids.Insert(j + 1, new MarketDepthLevel()
                                    {
                                        Bid = quotes.data[i].size,
                                        Price = quotes.data[i].price.ToDecimal(),
                                        Id = quotes.data[i].id
                                    });
                                }
                                else if (j == depth.Bids.Count - 1 && id > depth.Bids[j].Id)
                                {
                                    depth.Bids.Add(new MarketDepthLevel()
                                    {
                                        Bid = quotes.data[i].size,
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

                    while (depth.Asks != null && depth.Asks.Count > 200)
                    {
                        depth.Asks.RemoveAt(200);
                    }

                    while (depth.Bids != null && depth.Bids.Count > 200)
                    {
                        depth.Bids.RemoveAt(200);
                    }

                    depth.Time = ServerTime;

                    if (MarketDepthEvent != null)
                    {
                        MarketDepthEvent(depth.GetCopy());
                    }
                }
            }
            catch (Exception error)
            {
                _client_SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private readonly object _newTradesLoker = new object();

        /// <summary>
        /// trades updated
        /// обновились трейды 
        /// </summary>
        void _client_NewTrades(BitMexTrades trades)
        {
            try
            {
                lock (_newTradesLoker)
                {
                    for (int j = 0; j < trades.data.Count; j++)
                    {
                        Trade trade = new Trade();
                        trade.SecurityNameCode = trades.data[j].symbol;
                        trade.Price = trades.data[j].price.ToDecimal();
                        trade.Id = trades.data[j].trdMatchID;
                        trade.Time = Convert.ToDateTime(trades.data[j].timestamp);
                        trade.Volume = trades.data[j].size;
                        trade.Side = trades.data[j].side == "Buy" ? Side.Buy : Side.Sell;

                        ServerTime = trade.Time;

                        if (NewTradesEvent != null)
                        {
                            NewTradesEvent(trade);
                        }
                    }
                }
            }
            catch (Exception error)
            {
                _client_SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private object _myTradeLocker = new object();

        private List<MyTrade> _myTrades;

        /// <summary>
        /// my trades updated
        /// обновились мои трейды
        /// </summary>
        void _client_NewMyTrades(BitMexMyOrders order)
        {
            try
            {
                lock (_myTradeLocker)
                {
                    for (int i = 0; i < order.data.Count; i++)
                    {
                        if(order.data[i].lastQty == null ||
                            order.data[i].lastQty == 0)
                        {
                            continue;
                        }

                        MyTrade trade = new MyTrade();
                        trade.NumberTrade = order.data[i].execID;
                        trade.NumberOrderParent = order.data[i].orderID;
                        trade.SecurityNameCode = order.data[i].symbol;
                        trade.Price = order.data[i].avgPx.ToDecimal();
                        trade.Time = Convert.ToDateTime(order.data[i].transactTime);
                        trade.Side = order.data[i].side == "Buy" ? Side.Buy : Side.Sell;

                        if (order.data[i].lastQty != null)
                        {
                            trade.Volume = (int)order.data[i].lastQty;
                        }

                        if (_myTrades == null)
                        {
                            _myTrades = new List<MyTrade>();
                        }
                        _myTrades.Add(trade);

                        if (MyTradeEvent != null)
                        {
                            MyTradeEvent(trade);
                        }
                    }
                }
            }
            catch (Exception error)
            {
                _client_SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// securities updated
        /// обновились бумаги
        /// </summary>
        void _client_UpdateSecurity(List<BitMexSecurity> bitmexSecurities)
        {
            if (_securities == null)
            {
                _securities = new List<Security>();
            }

            foreach (var sec in bitmexSecurities)
            {
                Security security = new Security();
                security.Name = sec.symbol;
                security.NameFull = sec.symbol;
                security.NameClass = sec.typ;
                security.NameId = sec.symbol + sec.expiry;
                security.SecurityType = SecurityType.CurrencyPair;
                security.Lot = 1;
                security.PriceStep = sec.tickSize;
                security.PriceStepCost = sec.tickSize;

                if (security.PriceStep < 1)
                {
                    string prStep = security.PriceStep.ToString(CultureInfo.InvariantCulture);
                    security.Decimals = Convert.ToString(prStep).Split('.')[1].Split('1')[0].Length + 1;
                }
                else
                {
                    security.Decimals = 0;
                }

                security.State = SecurityStateType.Activ;
                _securities.Add(security);
            }

            if (SecurityEvent != null)
            {
                SecurityEvent(_securities);
            }
        }

        /// <summary>
        /// client error
        /// ошибка в клиенте
        /// </summary>
        private void _client_ErrorEvent(string error)
        {
            _client_SendLogMessage(error, LogMessageType.Error);

            if (error ==
             "{\"error\":{\"message\":\"The system is currently overloaded. Please try again later.\",\"name\":\"HTTPError\"}}")
            { // stop for a minute / останавливаемся на минуту
                //LastSystemOverload = DateTime.Now;
            }


            if (error == "{\"error\":{\"message\":\"This key is disabled.\",\"name\":\"HTTPError\"}}")
            {
                //_serverStatusNead = ServerConnectStatus.Disconnect;
                Thread.Sleep(2500);
            }
        }

        // work with orders
        // работа с ордерами

        /// <summary>
        /// place of work thread on the queues of order execution and cancellation
        /// место работы потока на очередях исполнения заявок и их отмены
        /// </summary>
        private void ExecutorOrdersThreadArea()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(400);

                    if (_client == null ||
                        LastSystemOverload.AddSeconds(30) > DateTime.Now)
                    {
                        continue;
                    }

                    if (_ordersToExecute != null && _ordersToExecute.Count != 0)
                    {
                        Order order;
                        if (_ordersToExecute.TryDequeue(out order))
                        {
                            Dictionary<string, string> param = new Dictionary<string, string>();

                            param["symbol"] = order.SecurityNameCode;
                            param["price"] = order.Price.ToString().Replace(",", ".");
                            param["side"] = order.Side == Side.Buy ? "Buy" : "Sell";
                            //param["orderIDs"] = order.NumberUser.ToString();
                            param["orderQty"] = order.Volume.ToString().Replace(",", ".");
                            param["clOrdID"] = order.NumberUser.ToString();
                            param["origClOrdID"] = order.NumberUser.ToString();

                            param["ordType"] = order.TypeOrder == OrderPriceType.Limit ? "Limit" : "Market";

                            var res = _client.CreateQuery("POST", "/order", param, true);

                            if (res == "")
                            {
                                //order.State = OrderStateType.Cancel;
                                //_ordersToCheck.Remove(order);
                                //_ordersToSend.Enqueue(order);
                            }
                        }
                    }
                    else if (_ordersToCansel != null && _ordersToCansel.Count != 0)
                    {
                        Order order;
                        if (_ordersToCansel.TryDequeue(out order))
                        {
                            Dictionary<string, string> param = new Dictionary<string, string>();
                            //param["clOrdID"] = order.NumberUser.ToString();
                            param["orderID"] = order.NumberMarket;

                            var res = _client.CreateQuery("DELETE", "/order", param, true);

                            order.State = OrderStateType.Cancel;
                            _ordersToCheck.Remove(order);
                            if (MyOrderEvent != null)
                            {
                                MyOrderEvent(order);
                            }
                        }
                    }

                    if (_lastOrderCheck.AddSeconds(30) < DateTime.Now &&
                        _ordersToCheck.Count != 0)
                    {
                        CheckOrders(_ordersToCheck[0].SecurityNameCode);
                        _lastOrderCheck = DateTime.Now;
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        public static DateTime LastSystemOverload;

        private DateTime _lastOrderCheck;
        public void CheckOrders(string security)
        {
            lock (_orderLocker)
            {
                if (_ordersToCheck.Count == 0)
                {
                    return;
                }

                var param = new Dictionary<string, string>();
                param["symbol"] = security;
                //param["filter"] = "{\"open\":true}";
                //param["columns"] = "";
                param["count"] = 30.ToString();
                //param["start"] = 0.ToString();
                param["reverse"] = true.ToString();
                //param["startTime"] = "";
                //param["endTime"] = "";

                var res = _client.CreateQuery("GET", "/order", param, true);

                List<DatumOrder> myOrders =
                    JsonConvert.DeserializeAnonymousType(res, new List<DatumOrder>());

                List<Order> osaOrders = new List<Order>();

                for (int i = 0; i < myOrders.Count; i++)
                {
                    if (String.IsNullOrEmpty(myOrders[i].clOrdID))
                    {
                        continue;
                    }

                    Order order = new Order();
                    try
                    {
                        order.NumberUser = Convert.ToInt32(myOrders[i].clOrdID);
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    order.NumberMarket = myOrders[i].orderID;
                    order.SecurityNameCode = myOrders[i].symbol;

                    if (!string.IsNullOrEmpty(myOrders[i].price))
                    {
                        order.Price = myOrders[i].price.ToDecimal();
                    }

                    if (myOrders[i].ordStatus == "Filled")
                    {
                        order.State = OrderStateType.Done;
                    }
                    else if (myOrders[i].ordStatus == "Canceled")
                    {
                        order.State = OrderStateType.Cancel;
                    }
                    else if (myOrders[i].ordStatus == "New")
                    {
                        order.State = OrderStateType.Activ;
                    }

                    if (myOrders[i].orderQty != null)
                    {
                        order.Volume = myOrders[i].orderQty.ToDecimal();
                    }

                    order.Comment = myOrders[i].text;
                    order.TimeCallBack = Convert.ToDateTime(myOrders[0].transactTime);
                    order.PortfolioNumber = myOrders[i].account.ToString();
                    order.TypeOrder = myOrders[i].ordType == "Limit"
                        ? OrderPriceType.Limit
                        : OrderPriceType.Market;

                    if (myOrders[i].side == "Sell")
                    {
                        order.Side = Side.Sell;
                    }
                    else if (myOrders[i].side == "Buy")
                    {
                        order.Side = Side.Buy;
                    }
                    osaOrders.Add(order);
                }

                for (int i = 0; i < _ordersToCheck.Count; i++)
                {
                    Order osOrder = osaOrders.Find(o => o.NumberUser == _ordersToCheck[i].NumberUser);

                    if (osOrder == null && string.IsNullOrEmpty(_ordersToCheck[i].NumberMarket))
                    {
                        if (_ordersToCheck[i].TimeCreate.AddMinutes(1) < ServerTime)
                        {
                            _ordersToCheck[i].State = OrderStateType.Cancel;

                            if (MyOrderEvent != null)
                            {
                                MyOrderEvent(_ordersToCheck[i]);
                            }

                            if (MyOrderEvent != null)
                            {
                                MyOrderEvent(_ordersToCheck[i]);
                            }
                            CancelOrder(_ordersToCheck[i]);
                            _ordersToCheck.RemoveAt(i);

                            i--;
                        }
                    }
                    else if (osOrder != null)
                    {
                        if (!string.IsNullOrEmpty(osOrder.NumberMarket))
                        {
                            _ordersToCheck[i].NumberMarket = osOrder.NumberMarket;
                        }

                        if (osOrder.State == OrderStateType.Cancel)
                        {

                            _ordersToCheck[i].State = OrderStateType.Cancel;
                            if (MyOrderEvent != null)
                            {
                                MyOrderEvent(_ordersToCheck[i]);
                            }
                            _ordersToCheck.RemoveAt(i);
                            i--;
                        }
                        else if (osOrder.State == OrderStateType.Done)
                        {
                            _ordersToCheck[i].State = OrderStateType.Done;
                            _ordersToCheck[i].TimeCallBack = ServerTime;
                            _ordersToCheck[i].VolumeExecute = _ordersToCheck[i].Volume;

                            if (MyOrderEvent != null)
                            {
                                MyOrderEvent(_ordersToCheck[i]);
                            }

                            if (_myTrades != null &&
                                _myTrades.Count != 0)
                            {
                                List<MyTrade> myTrade =
                                    _myTrades.FindAll(trade => trade.NumberOrderParent == _ordersToCheck[i].NumberMarket);

                                for (int tradeNum = 0; tradeNum < myTrade.Count; tradeNum++)
                                {
                                    if (MyTradeEvent != null)
                                    {
                                        MyTradeEvent(myTrade[tradeNum]);
                                    }
                                }
                            }

                            _ordersToCheck.RemoveAt(i);
                            i--;
                        }
                        else if (osOrder.State == OrderStateType.Activ)
                        {
                            _ordersToCheck[i].State = OrderStateType.Activ;
                            if (MyOrderEvent != null)
                            {
                                MyOrderEvent(_ordersToCheck[i]);
                            }

                            if (_ordersCanseled.Find(o => o.NumberUser == osOrder.NumberUser) != null)
                            {
                                _ordersToCansel.Enqueue(osOrder);
                            }
                            // report about the order came. Delete order from orders needed to be checked / отчёт об ордере пришёл. Удаляем ордер из ордеров нужных к проверке
                            _ordersToCheck.RemoveAt(i);
                            i--;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// queue of orders for placing in the system
        /// очередь ордеров для выставления в систему
        /// </summary>
        private ConcurrentQueue<Order> _ordersToExecute = new ConcurrentQueue<Order>();

        /// <summary>
        /// queue of orders for canceling in the system
        /// очередь ордеров для отмены в системе
        /// </summary>
        private ConcurrentQueue<Order> _ordersToCansel = new ConcurrentQueue<Order>();

        /// <summary>
        /// needed check orders
        /// ордера которые нужно проверить
        /// </summary>
        private List<Order> _ordersToCheck = new List<Order>();

        /// <summary>
        /// waiting for registration orders
        /// ордера, ожидающие регистрации
        /// </summary>
        private List<Order> _newOrders = new List<Order>();

        private List<Order> _ordersCanseled = new List<Order>();

        /// <summary>
        /// блокиратор доступа к ордерам
        /// </summary>
        private object _orderLocker = new object();

        /// <summary>
        /// incoming from system orders
        /// входящий из системы ордер
        /// </summary>
        private void _client_MyOrderEvent(BitMexOrder myOrder)
        {
            lock (_orderLocker)
            {
                try
                {
                    if (_lastStartServerTime.AddSeconds(15) > DateTime.Now)
                    {
                        return;
                    }

                    for (int i = 0; i < myOrder.data.Count; i++)
                    {
                        if (string.IsNullOrEmpty(myOrder.data[i].clOrdID))
                        {
                            continue;
                        }

                        try
                        {
                            Convert.ToInt32(myOrder.data[i].clOrdID);
                        }
                        catch
                        {
                            continue;
                        }

                        if (myOrder.action == "insert")
                        {
                            Order order = new Order();
                            order.NumberUser = Convert.ToInt32(myOrder.data[i].clOrdID);
                            order.NumberMarket = myOrder.data[i].orderID;
                            order.SecurityNameCode = myOrder.data[i].symbol;
                            order.ServerType = ServerType.BitMex;

                            if (!string.IsNullOrEmpty(myOrder.data[i].price))
                            {
                                order.Price = myOrder.data[i].price.ToDecimal();
                            }

                            order.State = OrderStateType.Pending;

                            if (myOrder.data[i].orderQty != null)
                            {
                                order.Volume = myOrder.data[i].orderQty.ToDecimal();
                            }

                            order.Comment = myOrder.data[i].text;
                            order.TimeCallBack = Convert.ToDateTime(myOrder.data[0].transactTime);
                            order.PortfolioNumber = myOrder.data[i].account.ToString();
                            order.TypeOrder = myOrder.data[i].ordType == "Limit"
                                ? OrderPriceType.Limit
                                : OrderPriceType.Market;

                            if (myOrder.data[i].side == "Sell")
                            {
                                order.Side = Side.Sell;
                            }
                            else if (myOrder.data[i].side == "Buy")
                            {
                                order.Side = Side.Buy;
                            }

                            _newOrders.Add(order);

                            if (_ordersToCheck != null && _ordersToCheck.Count != 0)
                            {
                                Order or = _ordersToCheck.Find(o => o.NumberUser == order.NumberUser);

                                if (or != null && !string.IsNullOrEmpty(order.NumberMarket))
                                {
                                    or.NumberMarket = order.NumberMarket;
                                }
                            }
                        }

                        else if (myOrder.action == "update" ||
                           (myOrder.action == "partial" &&
                            (myOrder.data[i].ordStatus == "Canceled" || myOrder.data[i].ordStatus == "Rejected")
                            ))
                        {
                            var needOrder = _newOrders.Find(order => order.NumberUser == Convert.ToInt32(myOrder.data[i].clOrdID));

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

                                needOrder.PortfolioNumber = myOrder.data[i].account.ToString();

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

                                _newOrders.Add(needOrder);
                            }

                            if (_ordersToCheck != null && _ordersToCheck.Count != 0)
                            {
                                Order or = _ordersToCheck.Find(o => o.NumberUser == needOrder.NumberUser);

                                if (or != null && !string.IsNullOrEmpty(needOrder.NumberMarket))
                                {
                                    or.NumberMarket = needOrder.NumberMarket;
                                }
                            }

                            if (needOrder != null)
                            {
                                if (Convert.ToBoolean(myOrder.data[i].workingIndicator))
                                {
                                    needOrder.State = OrderStateType.Activ;
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
                                    needOrder.State = OrderStateType.Patrial;
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
                                if (_myTrades != null &&
                                    _myTrades.Count != 0)
                                {
                                    List<MyTrade> myTrade =
                                        _myTrades.FindAll(trade => trade.NumberOrderParent == needOrder.NumberMarket);

                                    for (int tradeNum = 0; tradeNum < myTrade.Count; tradeNum++)
                                    {
                                        if (MyTradeEvent != null)
                                        {
                                            MyTradeEvent(myTrade[tradeNum]);
                                        }
                                    }
                                }

                                if (needOrder.State == OrderStateType.Done ||
                                    needOrder.State == OrderStateType.Cancel ||
                                    needOrder.State == OrderStateType.Fail)
                                {
                                    for (int i2 = 0; i2 < _ordersToCheck.Count; i2++)
                                    {
                                        if (_ordersToCheck[i2].NumberUser == needOrder.NumberUser)
                                        {
                                            _ordersToCheck.RemoveAt(i2);
                                            break;
                                        }
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
        }

        /// <summary>
        /// sent order to execution in the trading systme
        /// выслать ордер на исполнение в торговую систему
        /// </summary>
        /// <param name="order">order / ордер</param>
        public void SendOrder(Order order)
        {
            order.TimeCreate = ServerTime;
            _ordersToExecute.Enqueue(order);

            _ordersToCheck.Add(order);
        }

        /// <summary>
        /// cancel orders from the trading system
        /// отозвать ордер из торговой системы
        /// </summary>
        /// <param name="order">order/ордер</param>
        public void CancelOrder(Order order)
        {
            _ordersToCansel.Enqueue(order);
            _ordersCanseled.Add(order);
        }

        // outgoing events
        // исходящие события

        /// <summary>
        /// API connection established
        /// соединение с API установлено
        /// </summary>
        public event Action ConnectEvent;

        /// <summary>
        /// API connection lost
        /// соединение с API разорвано
        /// </summary>
        public event Action DisconnectEvent;

        /// <summary>
        /// called when order changed
        /// вызывается когда изменился ордер
        /// </summary>
        public event Action<Order> MyOrderEvent;

        /// <summary>
        /// called when my trade changed
        /// вызывается когда изменился мой трейд
        /// </summary>
        public event Action<MyTrade> MyTradeEvent;

        /// <summary>
        /// appear a new portfolio
        /// появились новые портфели
        /// </summary>
        public event Action<List<Portfolio>> PortfolioEvent;

        /// <summary>
        /// new securities
        /// новые бумаги
        /// </summary>
        public event Action<List<Security>> SecurityEvent;

        /// <summary>
        /// new depth
        /// новый стакан
        /// </summary>
        public event Action<MarketDepth> MarketDepthEvent;

        /// <summary>
        /// new trade
        /// новый трейд
        /// </summary>
        public event Action<Trade> NewTradesEvent;

        private void _client_SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        // work with log
        // работа с логом

        /// <summary>
        /// send a new message to up
        /// выслать новое сообщение на верх
        /// </summary>
        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        /// <summary>
        /// outgoing log message
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }

    /// <summary>
    /// one BitMex tick
    /// один тик BitMex
    /// </summary>
    public class TradeBitMex
    {
        public string timestamp { get; set; }
        public string symbol { get; set; }
        public string side { get; set; }
        public string size { get; set; }
        public string price { get; set; }
        public string tickDirection { get; set; }
        public string trdMatchID { get; set; }
        public object grossValue { get; set; }
        public double homeNotional { get; set; }
        public string foreignNotional { get; set; }
    }

    /// <summary>
    /// BitMex candle
    /// свеча BitMex
    /// </summary>
    public class BitMexCandle
    {
        public string timestamp { get; set; }
        public string symbol { get; set; }
        public decimal open { get; set; }
        public decimal high { get; set; }
        public decimal low { get; set; }
        public decimal close { get; set; }
        public int volume { get; set; }
    }
}
