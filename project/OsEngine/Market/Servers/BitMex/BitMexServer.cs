using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Newtonsoft.Json;
using OsEngine.Entity;
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
        public BitMexServer(bool neadLoadTicks = false)
        {
            BitMexServerRealization realization = new BitMexServerRealization();
            ServerRealization = realization;

            CreateParameterString("Идентификатор: ", "");
            CreateParameterPassword("Секретный ключ", "");
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

        private DateTime _lastStartServerTime;

        private List<Security> _securities;

        private List<Portfolio> _portfolios;

        private List<Candle> _candles;

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
                _client.UpdateMarketDepth += _client_UpdateMarketDepth;
                _client.NewTradesEvent += _client_NewTrades;
                _client.MyTradeEvent += _client_NewMyTrades;
                _client.MyOrderEvent += _client_BitMexUpdateOrder;
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
                _client.MyOrderEvent -= _client_BitMexUpdateOrder;
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
            //
        }

        /// <summary>
        /// исполнить ордер
        /// </summary>
        public void SendOrder(Order order)
        {
            //
        }

        /// <summary>
        /// отозвать ордер
        /// </summary>
        public void CanselOrder(Order order)
        {
            //
        }

        /// <summary>
        /// подписаться на свечи, все сделки 
        /// </summary>
        public void Subscrible(Security security)
        {
            SubcribeDepthTradeOrder(security.Name);
        }

        private bool isPortfolio = false; // уже подписались на портфели

        /// <summary>
        /// запросить портфели
        /// </summary>
        public void GetPortfolios()
        {
            if (!isPortfolio)
            {
                string queryPortf = "{\"op\": \"subscribe\", \"args\": [\"margin\"]}";
                //string queryPos = "{\"op\": \"subscribe\", \"args\": [\"position\"]}";

                _client.SendQuery(queryPortf);
                //Thread.Sleep(500);
                //_client.SendQuery(queryPos);
                isPortfolio = true;
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

        // реализация событий
        void _client_Connected()
        {
            if (ConnectEvent != null)
            {
                ConnectEvent();
            }
            ServerStatus = ServerConnectStatus.Connect;
        }

        void _client_Disconnected()
        {
            if (DisconnectEvent != null)
            {
                DisconnectEvent();
            }
            ServerStatus = ServerConnectStatus.Disconnect;
        }

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
            }
            catch (Exception error)
            {
                _client_SendLogMessage(error.ToString(), LogMessageType.Error);
            }

            if (PortfolioEvent != null)
            {
                PortfolioEvent(_portfolios);
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

                    if (MarketDepthEvent != null)
                    {
                        MarketDepthEvent(_depth.GetCopy());
                    }
                }
            }
            catch (Exception error)
            {
                _client_SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private readonly object _newTradesLoker = new object();

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
                        trade.Price = trades.data[j].price;
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
        /// блокиратор доступа к ордерам
        /// </summary>
        private object _orderLocker = new object();

        /// <summary>
        /// входящий из системы ордер
        /// </summary>
        void _client_BitMexUpdateOrder(BitMexOrder myOrder)
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

                        //if (myOrder.action == "insert")
                        //{
                        Order order = new Order();
                        order.NumberUser = Convert.ToInt32(myOrder.data[i].clOrdID);
                        order.NumberMarket = myOrder.data[i].orderID;
                        order.SecurityNameCode = myOrder.data[i].symbol;

                        if (!string.IsNullOrEmpty(myOrder.data[i].price))
                        {
                            order.Price = Convert.ToDecimal(myOrder.data[i].price.Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                        }

                        order.State = OrderStateType.Pending;

                        if (myOrder.data[i].orderQty != null)
                        {
                            order.Volume = Convert.ToInt32(myOrder.data[i].orderQty);
                        }

                        order.Comment = myOrder.data[i].text;
                        order.TimeCallBack = Convert.ToDateTime(myOrder.data[0].transactTime);
                        order.PortfolioNumber = myOrder.data[i].account.ToString();
                        order.TypeOrder = myOrder.data[i].ordType == "Limit" ? OrderPriceType.Limit : OrderPriceType.Market;

                        if (myOrder.data[i].side == "Sell")
                        {
                            order.Side = Side.Sell;
                        }
                        else if (myOrder.data[i].side == "Buy")
                        {
                            order.Side = Side.Buy;
                        }
                        if (MyOrderEvent != null)
                        {
                            MyOrderEvent(order);
                        }
                        //}
                        //else if (myOrder.action == "update" || (myOrder.action == "partial" && (myOrder.data[i].ordStatus == "Canceled" || myOrder.data[i].ordStatus == "Rejected")))
                        //{

                        //}
                    }
                }
                catch (Exception error)
                {
                    _client_SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

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

            if (SecurityEvent != null)
            {
                SecurityEvent(_securities);
            }
        }

        private void _client_ErrorEvent(string error)
        {
            _client_SendLogMessage(error, LogMessageType.Error);

            if (error ==
             "{\"error\":{\"message\":\"The system is currently overloaded. Please try again later.\",\"name\":\"HTTPError\"}}")
            { // останавливаемся на минуту
                //LastSystemOverload = DateTime.Now;
            }
            if (error == "{\"error\":{\"message\":\"Executing at order price would lead to immediate liquidation\",\"name\":\"ValidationError\"}}")
            {
                _client_SendLogMessage("Цена ликвидации при таком объме выше чем текущая цена ордера. Уменьшите объём", LogMessageType.Error);
            }
            if (error == "{\"error\":{\"message\":\"This key is disabled.\",\"name\":\"HTTPError\"}}")
            {
                _client_SendLogMessage("Биржа заблокировала Ваши ключи.", LogMessageType.System);
                //_serverStatusNead = ServerConnectStatus.Disconnect;
                Thread.Sleep(2500);
            }
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
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
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

        //public int trades { get; set; }
        //public double? vwap { get; set; }
        //public int? lastSize { get; set; }
        //public object turnover { get; set; }
        //public double homeNotional { get; set; }
        //public int foreignNotional { get; set; }
    }
}
