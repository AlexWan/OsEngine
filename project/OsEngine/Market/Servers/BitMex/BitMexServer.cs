﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.BitMex.BitMexEntity;
using OsEngine.Market.Servers.Entity;

namespace OsEngine.Market.Servers.BitMex
{
    /// <summary>
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
        /// статус сервера
        /// </summary>
        public ServerConnectStatus ServerStatus { get; set; }

        /// <summary>
        /// тип сервера
        /// </summary>
        public ServerType ServerType
        {
            get { return ServerType.BitMex; }
        }

        /// <summary>
        /// параметры подключения для сервера
        /// </summary>
        public List<IServerParameter> ServerParameters { get; set; }

        /// <summary>
        /// время сервера
        /// </summary>
        public DateTime ServerTime { get; set; }

        /// <summary>
        /// BitMex client
        /// </summary>
        private BitMexClient _client;

        /// <summary>
        /// время последнего старта сервера
        /// </summary>
        private DateTime _lastStartServerTime;

        /// <summary>
        /// бумаги
        /// </summary>
        private List<Security> _securities;

        /// <summary>
        /// портфели
        /// </summary>
        private List<Portfolio> _portfolios;

        /// <summary>
        /// свечи
        /// </summary>
        private List<Candle> _candles;

        /// <summary>
        /// стакан
        /// </summary>
        private MarketDepth _depth;

        /// <summary>
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
        }

        /// <summary>
        /// взять текущие состояни ордеров
        /// </summary>
        public void GetOrdersState(List<Order> orders)
        {
            GetAllOrders(orders);
        }

        /// <summary>
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
                Dictionary<string, string> param = new Dictionary<string, string>
                {
                    ["symbol"] = namesSec[i],
                    //param["filter"] = "{\"open\":true}";
                    //param["columns"] = "";
                    ["count"] = 30.ToString(),
                    //param["start"] = 0.ToString();
                    ["reverse"] = true.ToString()
                };
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

                    MyTradeEvent?.Invoke(trade);
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

                    MyOrderEvent?.Invoke(newOrder);
                }
            }

            return true;
        }

        /// <summary>
        /// подписаться на свечи, все сделки 
        /// </summary>
        public void Subscrible(Security security)
        {
            SubcribeDepthTradeOrder(security.Name);
        }

        /// <summary>
        /// взять историю свечек за период
        /// </summary>
        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder,
            DateTime startTime, DateTime endTime, DateTime actualTime)
        {
           return GetBitMexCandleHistory(security.Name, timeFrameBuilder.TimeFrameTimeSpan);
        }

        /// <summary>
        /// взять тиковые данные по инструменту за определённый период
        /// </summary>
        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime lastDate)
        {
            List<Trade> lastTrades = new List<Trade>();

            while (lastDate < endTime)
            {
                lastDate = TimeZoneInfo.ConvertTimeToUtc(lastDate);

                List<Trade> trades = GetTickHistoryToSecurity(security, startTime, endTime, lastDate);

                if (trades == null ||
                    trades.Count == 0)
                {
                    lastDate = lastDate.AddSeconds(1);
                    Thread.Sleep(2000);
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
                    lastDate = lastDate.AddSeconds(1);
                    continue;
                }

                DateTime uniTime = trades[trades.Count - 1].Time.ToUniversalTime();

                if (trades.Count != 0 && lastDate < uniTime)
                {
                    lastDate = trades[trades.Count - 1].Time;
                }
                else
                {
                    lastDate = lastDate.AddSeconds(1);
                }

                lastTrades.AddRange(trades);

                Thread.Sleep(2000);
            }

            return lastTrades;
        }

        private bool _portfolioStarted = false; // уже подписались на портфели

        /// <summary>
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
        /// получить инструменты
        /// </summary>
        public void GetSecurities()
        {
            _client.GetSecurities();
        }

// Подпись на данные

        /// <summary>
        /// мастер загрузки свечек
        /// </summary>
        private CandleManager _candleManager;

        /// <summary>
        /// бумаги уже подписанные на обновления данных
        /// </summary>
        private List<string> _subscribedSec = new List<string>();

        /// <summary>
        /// объект блокирующий многопоточный доступ в StartThisSecurity
        /// </summary>
        private object _lockerStarter = new object();

        /// <summary>
        /// начать выкачивать данный иснтрументн
        /// </summary>
        /// <param name="namePaper"> название инструмента</param>
        /// <returns>в случае успешного запуска возвращает CandleSeries, объект генерирующий свечи</returns>
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
                    // надо запустить сервер если он ещё отключен
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
                    if (_lastStartServerTime != DateTime.MinValue &&
                        _lastStartServerTime.AddSeconds(15) > DateTime.Now)
                    {
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

                        var param = new Dictionary<string, string>
                        {
                            ["symbol"] = security,
                            ["count"] = 500.ToString(),
                            ["binSize"] = tf,
                            ["reverse"] = true.ToString(),
                            ["startTime"] = start,
                            ["endTime"] = end,
                            ["partial"] = true.ToString()
                        };

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
        /// блокиратор многопоточного доступа к GetBitMexCandleHistory
        /// </summary>
        private readonly object _getCandlesLocker = new object();

        /// <summary>
        /// взять свечи по инструменту
        /// </summary>
        /// <param name="security"> короткое название бумаги</param>
        /// <param name="timeSpan">таймФрейм</param>
        /// <returns>в случае неудачи вернётся null</returns>
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

        public List<Trade> GetTickHistoryToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            try
            {
                lock (_lockerStarter)
                {
                    List<Trade> trades = new List<Trade>();
                    if (startTime < endTime)
                    {
                        if (startTime < actualTime)
                        {
                            startTime = actualTime;
                        }

                        Dictionary<string, string> param = new Dictionary<string, string>();

                        string start = startTime.ToString("yyyy-MM-dd HH:mm:ss");
                        string end = startTime.AddMinutes(1).ToString("yyyy-MM-dd HH:mm:ss");

                        param["symbol"] = security.Name;
                        param["count"] = 500.ToString();
                        param["start"] = 0.ToString();
                        param["reverse"] = true.ToString();
                        param["startTime"] = start;
                        param["endTime"] = end;

                        var res = _client.CreateQuery("GET", "/trade", param);

                        List<TradeBitMex> tradeHistory = JsonConvert.DeserializeAnonymousType(res, new List<TradeBitMex>());

                        tradeHistory.Reverse();

                        foreach (var oneTrade in tradeHistory)
                        {
                            Trade trade = new Trade();
                            trade.SecurityNameCode = oneTrade.symbol;
                            trade.Id = oneTrade.trdMatchID;
                            trade.Time = Convert.ToDateTime(oneTrade.timestamp);
                            trade.Price = oneTrade.price;
                            trade.Volume = oneTrade.size;
                            trade.Side = oneTrade.side == "Sell" ? Side.Sell : Side.Buy;
                            trades.Add(trade);
                        }
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

        // реализация событий

        /// <summary>
        /// клиент подключился
        /// </summary>
        void _client_Connected()
        {
            ConnectEvent?.Invoke();
            ServerStatus = ServerConnectStatus.Connect;
        }

        /// <summary>
        /// клиент отключился
        /// </summary>
        void _client_Disconnected()
        {
            DisconnectEvent?.Invoke();
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        /// <summary>
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

                PortfolioEvent?.Invoke(_portfolios);
            }
            catch (Exception error)
            {
                _client_SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
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
                    newPos.ValueCurrent = pos.data[i].currentQty;

                    needPortfolio.SetNewPosition(newPos);
                }

                PortfolioEvent?.Invoke(_portfolios);
            }
        }

        private object _quoteLock = new object();

        /// <summary>
        /// пришел обновленный стакан
        /// </summary>
        void _client_UpdateMarketDepth(BitMexQuotes quotes)
        {
            try
            {
                lock (_quoteLock)
                {
                    if (quotes.action == "partial")
                    {
                        if (_depth == null)
                        {
                            _depth = new MarketDepth();
                        }
                        else
                        {
                            _depth.Asks.Clear();
                            _depth.Bids.Clear();
                        }
                        _depth.SecurityNameCode = quotes.data[0].symbol;
                        List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
                        List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                        for (int i = 0; i < quotes.data.Count; i++)
                        {
                            if (quotes.data[i].price == 0)
                            {
                                continue;
                            }
                            if (quotes.data[i].side == "Sell")
                            {
                                ascs.Add(new MarketDepthLevel()
                                {
                                    Ask = quotes.data[i].size,
                                    Price = quotes.data[i].price,
                                    Id = quotes.data[i].id
                                });

                                if (_depth.Bids != null && _depth.Bids.Count > 2 &&
                                    quotes.data[i].price < _depth.Bids[0].Price)
                                {
                                    _depth.Bids.RemoveAt(0);
                                }
                            }
                            else
                            {
                                bids.Add(new MarketDepthLevel()
                                {
                                    Bid = quotes.data[i].size,
                                    Price = quotes.data[i].price,
                                    Id = quotes.data[i].id
                                });

                                if (_depth.Asks != null && _depth.Asks.Count > 2 &&
                                    quotes.data[i].price > _depth.Asks[0].Price)
                                {
                                    _depth.Asks.RemoveAt(0);
                                }
                            }
                        }

                        ascs.Reverse();
                        _depth.Asks = ascs;
                        _depth.Bids = bids;
                    }

                    if (quotes.action == "update")
                    {
                        if (_depth == null)
                            return;

                        for (int i = 0; i < quotes.data.Count; i++)
                        {
                            if (quotes.data[i].side == "Sell")
                            {
                                if (_depth.Asks.Find(asc => asc.Id == quotes.data[i].id) != null)
                                {
                                    _depth.Asks.Find(asc => asc.Id == quotes.data[i].id).Ask = quotes.data[i].size;
                                }
                                else
                                {
                                    if (quotes.data[i].price == 0)
                                    {
                                        continue;
                                    }

                                    long id = quotes.data[i].id;

                                    for (int j = 0; j < _depth.Asks.Count; j++)
                                    {
                                        if (j == 0 && id > _depth.Asks[j].Id)
                                        {
                                            _depth.Asks.Insert(j, new MarketDepthLevel()
                                            {
                                                Ask = quotes.data[i].size,
                                                Price = quotes.data[i].price,
                                                Id = quotes.data[i].id
                                            });
                                        }
                                        else if (j != _depth.Asks.Count - 1 && id < _depth.Asks[j].Id && id > _depth.Asks[j + 1].Id)
                                        {
                                            _depth.Asks.Insert(j + 1, new MarketDepthLevel()
                                            {
                                                Ask = quotes.data[i].size,
                                                Price = quotes.data[i].price,
                                                Id = quotes.data[i].id
                                            });
                                        }
                                        else if (j == _depth.Asks.Count - 1 && id < _depth.Asks[j].Id)
                                        {
                                            _depth.Asks.Add(new MarketDepthLevel()
                                            {
                                                Ask = quotes.data[i].size,
                                                Price = quotes.data[i].price,
                                                Id = quotes.data[i].id
                                            });
                                        }

                                        if (_depth.Bids != null && _depth.Bids.Count > 2 &&
                                            quotes.data[i].price < _depth.Bids[0].Price)
                                        {
                                            _depth.Bids.RemoveAt(0);
                                        }
                                    }
                                }
                            }
                            else // (quotes.data[i].side == "Buy")
                            {
                                if (quotes.data[i].price == 0)
                                {
                                    continue;
                                }

                                long id = quotes.data[i].id;

                                for (int j = 0; j < _depth.Bids.Count; j++)
                                {
                                    if (j == 0 && id < _depth.Bids[i].Id)
                                    {
                                        _depth.Bids.Insert(j, new MarketDepthLevel()
                                        {
                                            Bid = quotes.data[i].size,
                                            Price = quotes.data[i].price,
                                            Id = quotes.data[i].id
                                        });
                                    }
                                    else if (j != _depth.Bids.Count - 1 && id > _depth.Bids[i].Id && id < _depth.Bids[j + 1].Id)
                                    {
                                        _depth.Bids.Insert(j + 1, new MarketDepthLevel()
                                        {
                                            Bid = quotes.data[i].size,
                                            Price = quotes.data[i].price,
                                            Id = quotes.data[i].id
                                        });
                                    }
                                    else if (j == _depth.Bids.Count - 1 && id > _depth.Bids[j].Id)
                                    {
                                        _depth.Bids.Add(new MarketDepthLevel()
                                        {
                                            Bid = quotes.data[i].size,
                                            Price = quotes.data[i].price,
                                            Id = quotes.data[i].id
                                        });
                                    }

                                    if (_depth.Asks != null && _depth.Asks.Count > 2 &&
                                        quotes.data[i].price > _depth.Asks[0].Price)
                                    {
                                        _depth.Asks.RemoveAt(0);
                                    }
                                }
                            }
                        }

                        _depth.Time = ServerTime;
                    }

                    if (quotes.action == "delete")
                    {
                        if (_depth == null)
                            return;

                        for (int i = 0; i < quotes.data.Count; i++)
                        {
                            if (quotes.data[i].side == "Sell")
                            {
                                _depth.Asks.Remove(_depth.Asks.Find(asc => asc.Id == quotes.data[i].id));
                            }
                            else
                            {
                                _depth.Bids.Remove(_depth.Bids.Find(bid => bid.Id == quotes.data[i].id));
                            }
                        }

                        _depth.Time = ServerTime;
                    }
                }

                if (quotes.action == "insert")
                {
                    if (_depth == null)
                        return;

                    for (int i = 0; i < quotes.data.Count; i++)
                    {
                        if (quotes.data[0].price == 0)
                        {
                            continue;
                        }
                        if (quotes.data[i].side == "Sell")
                        {
                            long id = quotes.data[i].id;

                            for (int j = 0; j < _depth.Asks.Count; j++)
                            {
                                if (j == 0 && id > _depth.Asks[j].Id)
                                {
                                    _depth.Asks.Insert(j, new MarketDepthLevel()
                                    {
                                        Ask = quotes.data[i].size,
                                        Price = quotes.data[i].price,
                                        Id = quotes.data[i].id
                                    });
                                }
                                else if (j != _depth.Asks.Count - 1 && id < _depth.Asks[j].Id && id > _depth.Asks[j + 1].Id)
                                {
                                    _depth.Asks.Insert(j + 1, new MarketDepthLevel()
                                    {
                                        Ask = quotes.data[i].size,
                                        Price = quotes.data[i].price,
                                        Id = quotes.data[i].id
                                    });
                                }
                                else if (j == _depth.Asks.Count - 1 && id < _depth.Asks[j].Id)
                                {
                                    _depth.Asks.Add(new MarketDepthLevel()
                                    {
                                        Ask = quotes.data[i].size,
                                        Price = quotes.data[i].price,
                                        Id = quotes.data[i].id
                                    });
                                }

                                if (_depth.Bids != null && _depth.Bids.Count > 2 &&
                                    quotes.data[i].price < _depth.Bids[0].Price)
                                {
                                    _depth.Bids.RemoveAt(0);
                                }
                            }
                        }
                        else // quotes.data[i].side == "Buy"
                        {
                            long id = quotes.data[i].id;

                            for (int j = 0; j < _depth.Bids.Count; j++)
                            {
                                if (j == 0 && id < _depth.Bids[j].Id)
                                {
                                    _depth.Bids.Insert(j, new MarketDepthLevel()
                                    {
                                        Bid = quotes.data[i].size,
                                        Price = quotes.data[i].price,
                                        Id = quotes.data[i].id
                                    });
                                }
                                else if (j != _depth.Bids.Count - 1 && id > _depth.Bids[j].Id && id < _depth.Bids[j + 1].Id)
                                {
                                    _depth.Bids.Insert(j + 1, new MarketDepthLevel()
                                    {
                                        Bid = quotes.data[i].size,
                                        Price = quotes.data[i].price,
                                        Id = quotes.data[i].id
                                    });
                                }
                                else if (j == _depth.Bids.Count - 1 && id > _depth.Bids[j].Id)
                                {
                                    _depth.Bids.Add(new MarketDepthLevel()
                                    {
                                        Bid = quotes.data[i].size,
                                        Price = quotes.data[i].price,
                                        Id = quotes.data[i].id
                                    });
                                }

                                if (_depth.Asks != null && _depth.Asks.Count > 2 &&
                                    quotes.data[i].price > _depth.Asks[0].Price)
                                {
                                    _depth.Asks.RemoveAt(0);
                                }
                            }
                        }
                    }

                    while (_depth.Asks != null && _depth.Asks.Count > 200)
                    {
                        _depth.Asks.RemoveAt(200);
                    }

                    while (_depth.Bids != null && _depth.Bids.Count > 200)
                    {
                        _depth.Bids.RemoveAt(200);
                    }

                    _depth.Time = ServerTime;

                    MarketDepthEvent?.Invoke(_depth.GetCopy());
                }
            }
            catch (Exception error)
            {
                _client_SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private readonly object _newTradesLoker = new object();

        /// <summary>
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
                        trade.Price = Convert.ToDecimal(trades.data[j].price.Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberGroupSeparator), CultureInfo.InvariantCulture);
                        trade.Id = trades.data[j].trdMatchID;
                        trade.Time = Convert.ToDateTime(trades.data[j].timestamp);
                        trade.Volume = trades.data[j].size;
                        trade.Side = trades.data[j].side == "Buy" ? Side.Buy : Side.Sell;

                        ServerTime = trade.Time;

                        NewTradesEvent?.Invoke(trade);
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
                        MyTrade trade = new MyTrade();
                        trade.NumberTrade = order.data[i].execID;
                        trade.NumberOrderParent = order.data[i].orderID;
                        trade.SecurityNameCode = order.data[i].symbol;
                        trade.Price = Convert.ToDecimal(order.data[i].avgPx.Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberGroupSeparator), CultureInfo.InvariantCulture);
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

                        MyTradeEvent?.Invoke(trade);
                    }
                }
            }
            catch (Exception error)
            {
                _client_SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
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
                security.Lot = Convert.ToDecimal(sec.lotSize.ToString().Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
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

            SecurityEvent?.Invoke(_securities);
        }

        /// <summary>
        /// ошибка в клиенте
        /// </summary>
        private void _client_ErrorEvent(string error)
        {
            _client_SendLogMessage(error, LogMessageType.Error);

            if (error ==
             "{\"error\":{\"message\":\"The system is currently overloaded. Please try again later.\",\"name\":\"HTTPError\"}}")
            { // останавливаемся на минуту
                //LastSystemOverload = DateTime.Now;
            }


            if (error == "{\"error\":{\"message\":\"This key is disabled.\",\"name\":\"HTTPError\"}}")
            {
                //_serverStatusNead = ServerConnectStatus.Disconnect;
                Thread.Sleep(2500);
            }
        }

// работа с ордерами

        /// <summary>
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
                            Dictionary<string, string> param = new Dictionary<string, string>
                            {
                                ["symbol"] = order.SecurityNameCode,
                                ["price"] = order.Price.ToString().Replace(",", "."),
                                ["side"] = order.Side == Side.Buy ? "Buy" : "Sell",
                                //param["orderIDs"] = order.NumberUser.ToString();
                                ["orderQty"] = order.Volume.ToString(),
                                ["clOrdID"] = order.NumberUser.ToString(),

                                ["ordType"] = order.TypeOrder == OrderPriceType.Limit ? "Limit" : "Market"
                            };

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
                            Dictionary<string, string> param = new Dictionary<string, string>
                            {
                                //param["clOrdID"] = order.NumberUser.ToString();
                                ["orderID"] = order.NumberMarket
                            };

                            var res = _client.CreateQuery("DELETE", "/order", param, true);

                            order.State = OrderStateType.Cancel;
                            _ordersToCheck.Remove(order);
                            MyOrderEvent?.Invoke(order);
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

                var param = new Dictionary<string, string>
                {
                    ["symbol"] = security,
                    //param["filter"] = "{\"open\":true}";
                    //param["columns"] = "";
                    ["count"] = 30.ToString(),
                    //param["start"] = 0.ToString();
                    ["reverse"] = true.ToString()
                };
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
                        order.Price = Convert.ToDecimal(myOrders[i].price.Replace(",",
                            CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                            CultureInfo.InvariantCulture);
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
                        order.Volume = Convert.ToDecimal(myOrders[i].orderQty.Replace(",",
                                CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                            CultureInfo.InvariantCulture);
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
                        if (_ordersToCheck[i].TimeCreate.AddMinutes(5) < ServerTime)
                        {
                            _ordersToCheck[i].State = OrderStateType.Cancel;

                            MyOrderEvent?.Invoke(_ordersToCheck[i]);

                            MyOrderEvent?.Invoke(_ordersToCheck[i]);
                            CanselOrder(_ordersToCheck[i]);
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
                            MyOrderEvent?.Invoke(_ordersToCheck[i]);
                            _ordersToCheck.RemoveAt(i);
                            i--;
                        }
                        else if (osOrder.State == OrderStateType.Done)
                        {
                            _ordersToCheck[i].State = OrderStateType.Done;
                            _ordersToCheck[i].TimeCallBack = ServerTime;
                            _ordersToCheck[i].VolumeExecute = _ordersToCheck[i].Volume;

                            MyOrderEvent?.Invoke(_ordersToCheck[i]);

                            if (_myTrades != null &&
                                _myTrades.Count != 0)
                            {
                                List<MyTrade> myTrade =
                                    _myTrades.FindAll(trade => trade.NumberOrderParent == _ordersToCheck[i].NumberMarket);

                                for (int tradeNum = 0; tradeNum < myTrade.Count; tradeNum++)
                                {
                                    MyTradeEvent?.Invoke(myTrade[tradeNum]);
                                }
                            }

                            _ordersToCheck.RemoveAt(i);
                            i--;
                        }
                        else if (osOrder.State == OrderStateType.Activ)
                        {
                            _ordersToCheck[i].State = OrderStateType.Activ;
                            MyOrderEvent?.Invoke(_ordersToCheck[i]);

                            if (_ordersCanseled.Find(o => o.NumberUser == osOrder.NumberUser) != null)
                            {
                                _ordersToCansel.Enqueue(osOrder);
                            }
                            // отчёт об ордере пришёл. Удаляем ордер из ордеров нужных к проверке
                            _ordersToCheck.RemoveAt(i);
                            i--;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// очередь ордеров для выставления в систему
        /// </summary>
        private ConcurrentQueue<Order> _ordersToExecute = new ConcurrentQueue<Order>();

        /// <summary>
        /// очередь ордеров для отмены в системе
        /// </summary>
        private ConcurrentQueue<Order> _ordersToCansel = new ConcurrentQueue<Order>();

        /// <summary>
        /// ордера которые нужно проверить
        /// </summary>
        private List<Order> _ordersToCheck = new List<Order>();

        /// <summary>
        /// ордера, ожидающие регистрации
        /// </summary>
        private List<Order> _newOrders = new List<Order>();

        private List<Order> _ordersCanseled = new List<Order>();

        /// <summary>
        /// блокиратор доступа к ордерам
        /// </summary>
        private object _orderLocker = new object();

        /// <summary>
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

                        if (myOrder.action == "insert")
                        {
                            Order order = new Order();
                            order.NumberUser = Convert.ToInt32(myOrder.data[i].clOrdID);
                            order.NumberMarket = myOrder.data[i].orderID;
                            order.SecurityNameCode = myOrder.data[i].symbol;
                            order.ServerType = ServerType.BitMex;

                            if (!string.IsNullOrEmpty(myOrder.data[i].price))
                            {
                                order.Price = Convert.ToDecimal(myOrder.data[i].price.Replace(",",
                                CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                                CultureInfo.InvariantCulture);
                            }

                            order.State = OrderStateType.Pending;

                            if (myOrder.data[i].orderQty != null)
                            {
                                order.Volume = Convert.ToDecimal(myOrder.data[i].orderQty.Replace(",",
                                        CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                                    CultureInfo.InvariantCulture);
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
                                        needOrder.VolumeExecute = Convert.ToDecimal(myOrder.data[i].cumQty.Replace(",",
                                                CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                                            CultureInfo.InvariantCulture);
                                    }

                                 }

                                if (myOrder.data[i].ordStatus == "Filled")
                                {
                                    needOrder.State = OrderStateType.Done;
                                    if (myOrder.data[i].cumQty != null)
                                    {
                                        needOrder.VolumeExecute = Convert.ToDecimal(myOrder.data[i].cumQty.Replace(",",
                                                CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                                            CultureInfo.InvariantCulture);
                                    }
                                }
                                if (_myTrades != null &&
                                    _myTrades.Count != 0)
                                {
                                    List<MyTrade> myTrade =
                                        _myTrades.FindAll(trade => trade.NumberOrderParent == needOrder.NumberMarket);

                                    for (int tradeNum = 0; tradeNum < myTrade.Count; tradeNum++)
                                    {
                                        MyTradeEvent?.Invoke(myTrade[tradeNum]);
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

                                MyOrderEvent?.Invoke(needOrder);
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
        /// выслать ордер на исполнение в торговую систему
        /// </summary>
        /// <param name="order">ордер</param>
        public void SendOrder(Order order)
        {
            order.TimeCreate = ServerTime;
            _ordersToExecute.Enqueue(order);

            _ordersToCheck.Add(order);
        }

        /// <summary>
        /// отозвать ордер из торговой системы
        /// </summary>
        /// <param name="order">ордер</param>
        public void CanselOrder(Order order)
        {
            _ordersToCansel.Enqueue(order);
            _ordersCanseled.Add(order);
        }

        // исходящие события

        /// <summary>
        /// соединение с API установлено
        /// </summary>
        public event Action ConnectEvent;

        /// <summary>
        /// соединение с API разорвано
        /// </summary>
        public event Action DisconnectEvent;

        /// <summary>
        /// вызывается когда изменился ордер
        /// </summary>
        public event Action<Order> MyOrderEvent;

        /// <summary>
        /// вызывается когда изменился мой трейд
        /// </summary>
        public event Action<MyTrade> MyTradeEvent;

        /// <summary>
        /// появились новые портфели
        /// </summary>
        public event Action<List<Portfolio>> PortfolioEvent;

        /// <summary>
        /// новые бумаги
        /// </summary>
        public event Action<List<Security>> SecurityEvent;

        /// <summary>
        /// новый стакан
        /// </summary>
        public event Action<MarketDepth> MarketDepthEvent;

        /// <summary>
        /// новый трейд
        /// </summary>
        public event Action<Trade> NewTradesEvent;

        private void _client_SendLogMessage(string message, LogMessageType type)
        {
            LogMessageEvent?.Invoke(message, type);
        }

        // работа с логом

        /// <summary>
        /// выслать новое сообщение на верх
        /// </summary>
        private void SendLogMessage(string message, LogMessageType type)
        {
            LogMessageEvent?.Invoke(message, type);
        }

        /// <summary>
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }

    /// <summary>
    /// один тик BitMex
    /// </summary>
    public class TradeBitMex
    {
        public string timestamp { get; set; }
        public string symbol { get; set; }
        public string side { get; set; }
        public int size { get; set; }
        public decimal price { get; set; }
        public string tickDirection { get; set; }
        public string trdMatchID { get; set; }
        public object grossValue { get; set; }
        public double homeNotional { get; set; }
        public int foreignNotional { get; set; }
    }

    /// <summary>
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
