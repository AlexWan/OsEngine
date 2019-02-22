﻿using System;
using System.Collections.Generic;
using System.Globalization;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;

namespace OsEngine.Market.Servers.Binance
{
    /// <summary>
    /// сервер Binance
    /// </summary>
    public class BinanceServer:AServer
    {
        public BinanceServer()
        {
            BinanceServerRealization realization = new BinanceServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey,"");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");
        }
        
        /// <summary>
        /// запрос истории по инструменту
        /// </summary>
        public List<Candle> GetCandleHistory(string nameSec, TimeSpan tf)
        {
            return ((BinanceServerRealization)ServerRealization).GetCandleHistory(nameSec, tf);
        }
    }

    public class BinanceServerRealization : IServerRealization
    {
        public BinanceServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        /// <summary>
        /// тип сервера
        /// </summary>
        public ServerType ServerType
        {
            get { return ServerType.Binance; }
        }

        /// <summary>
        /// параметры сервера
        /// </summary>
        public List<IServerParameter> ServerParameters { get; set; }

        /// <summary>
        /// время сервера
        /// </summary>
        public DateTime ServerTime { get; set; }

// запросы

        /// <summary>
        /// binance client
        /// </summary>
        private BinanceClient _client;

        /// <summary>
        /// освободить апи
        /// </summary>
        public void Dispose()
        {
            if (_client != null)
            {
                _client.Dispose();

                _client.Connected -= _client_Connected;
                _client.UpdatePairs -= _client_UpdatePairs;
                _client.Disconnected -= _client_Disconnected;
                _client.NewPortfolio -= _client_NewPortfolio;
                _client.UpdatePortfolio -= _client_UpdatePortfolio;
                _client.UpdateMarketDepth -= _client_UpdateMarketDepth;
                _client.NewTradesEvent -= _client_NewTradesEvent;
                _client.MyTradeEvent -= _client_MyTradeEvent;
                _client.MyOrderEvent -= _client_MyOrderEvent;
                _client.LogMessageEvent -= SendLogMessage;
            }

            _client = null;
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        /// <summary>
        /// подсоединиться к апи
        /// </summary>
        public void Connect()
        {
            if (_client == null)
            {
                _client = new BinanceClient(((ServerParameterString)ServerParameters[0]).Value, ((ServerParameterPassword)ServerParameters[1]).Value);
                _client.Connected += _client_Connected;
                _client.UpdatePairs += _client_UpdatePairs;
                _client.Disconnected += _client_Disconnected;
                _client.NewPortfolio += _client_NewPortfolio;
                _client.UpdatePortfolio += _client_UpdatePortfolio;
                _client.UpdateMarketDepth += _client_UpdateMarketDepth;
                _client.NewTradesEvent += _client_NewTradesEvent;
                _client.MyTradeEvent += _client_MyTradeEvent;
                _client.MyOrderEvent += _client_MyOrderEvent;
                _client.LogMessageEvent += SendLogMessage;
            }

            _client.Connect();
        }

        /// <summary>
        /// запросить бумаги
        /// </summary>
        public void GetSecurities()
        {
            _client.GetSecurities();
        }

        /// <summary>
        /// запросить портфели
        /// </summary>
        public void GetPortfolios()
        {
            _client.GetBalance();
        }

        /// <summary>
        /// исполнить ордер
        /// </summary>
        public void SendOrder(Order order)
        {
            _client.ExecuteOrder(order);
        }

        /// <summary>
        /// отозвать ордер
        /// </summary>
        public void CanselOrder(Order order)
        {
            _client.CanselOrder(order);
        }

        /// <summary>
        /// подписаться 
        /// </summary>
        public void Subscrible(Security security)
        {
            _client.SubscribleTradesAndDepths(security);
        }

        /// <summary>
        /// взять историю свечек за период
        /// </summary>
        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder,
            DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            List<Candle> candles = new List<Candle>();

            actualTime = startTime;

            while (actualTime < endTime)
            {
                List<Candle> newCandles = _client.GetCandlesForTimes(security.Name, 
                    timeFrameBuilder.TimeFrameTimeSpan,
                    actualTime, endTime);

                if (candles.Count != 0 && newCandles.Count != 0)
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

                if (newCandles.Count == 0)
                {
                    return candles;
                }

                candles.AddRange(newCandles);

                actualTime = candles[candles.Count - 1].TimeStart;
            }

            if (candles.Count == 0)
            {
                return null;
            }

            return candles;
        }

        /// <summary>
        /// взять тиковые данные по инструменту за период
        /// </summary>
        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        /// <summary>
        /// запросить статус ордеров
        /// </summary>
        public void GetOrdersState(List<Order> orders)
        {
            _client.GetAllOrders(orders);
        }

        /// <summary>
        /// статус серверов
        /// </summary>
        public ServerConnectStatus ServerStatus { get; set; }

        /// <summary>
        /// запрос истории по инструменту
        /// </summary>
        public List<Candle> GetCandleHistory(string nameSec, TimeSpan tf)
        {
            return _client.GetCandles(nameSec, tf);
        }

// разбор входящих данных

        void _client_MyOrderEvent(Order order)
        {
            MyOrderEvent?.Invoke(order);
        }

        void _client_MyTradeEvent(MyTrade myTrade)
        {
            MyTradeEvent?.Invoke(myTrade);
        }

        /// <summary>
        /// блокиратор многопоточного доступа к тикам
        /// </summary>
        private readonly object _newTradesLoker = new object();

        void _client_NewTradesEvent(BinanceEntity.TradeResponse trades)
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
                    Convert.ToDecimal(
                        trades.data.p.Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                        CultureInfo.InvariantCulture);
                trade.Id = trades.data.t.ToString();
                trade.Time = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(trades.data.T));
                trade.Volume =
                    Convert.ToDecimal(
                        trades.data.q.Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                        CultureInfo.InvariantCulture);
                trade.Side = trades.data.m == true ? Side.Sell : Side.Buy;

                NewTradesEvent?.Invoke(trade);
            }
        }

        /// <summary>
        /// все стаканы
        /// </summary>
        private List<MarketDepth> _depths;

        private readonly object _depthLocker = new object();

        void _client_UpdateMarketDepth(BinanceEntity.DepthResponse myDepth)
        {
            try
            {
                lock (_depthLocker)
                {
                    if (_depths == null)
                    {
                        _depths = new List<MarketDepth>();
                    }

                    var needDepth = _depths.Find(depth =>
                        depth.SecurityNameCode == myDepth.stream.Split('@')[0].ToUpper());

                    if (needDepth == null)
                    {
                        needDepth = new MarketDepth();
                        needDepth.SecurityNameCode = myDepth.stream.Split('@')[0].ToUpper();
                        _depths.Add(needDepth);
                    }

                    List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
                    List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                    for (int i = 0; i < myDepth.data.asks.Count; i++)
                    {
                        ascs.Add(new MarketDepthLevel()
                        {
                            Ask = Convert.ToDecimal(
                                myDepth.data.asks[i][1].ToString().Replace(".",
                                    CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                                CultureInfo.InvariantCulture),
                            Price = Convert.ToDecimal(
                                myDepth.data.asks[i][0].ToString().Replace(".",
                                    CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                                CultureInfo.InvariantCulture)
                        });

                        bids.Add(new MarketDepthLevel()
                        {
                            Bid = Convert.ToDecimal(
                                myDepth.data.bids[i][1].ToString().Replace(".",
                                    CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                                CultureInfo.InvariantCulture),
                            Price = Convert.ToDecimal(
                                myDepth.data.bids[i][0].ToString().Replace(".",
                                    CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                                CultureInfo.InvariantCulture),
                        });
                    }

                    needDepth.Asks = ascs;
                    needDepth.Bids = bids;
                    needDepth.Time = ServerTime;

                    if (needDepth.Time == DateTime.MinValue)
                    {
                        return;
                    }

                    MarketDepthEvent?.Invoke(needDepth.GetCopy());
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private List<Portfolio> _portfolios;

        void _client_UpdatePortfolio(BinanceEntity.OutboundAccountInfo portfs)
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
                foreach (var onePortf in portfs.B)
                {
                    if (onePortf == null ||
                        onePortf.f == null ||
                        onePortf.l == null)
                    {
                        continue;
                    }
                    Portfolio neeedPortf = _portfolios.Find(p => p.Number == onePortf.a);

                    if (neeedPortf == null)
                    {
                        continue;
                    }

                    neeedPortf.ValueCurrent = Convert.ToDecimal(onePortf.f.Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                    neeedPortf.ValueBlocked = Convert.ToDecimal(onePortf.l.Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                }

                PortfolioEvent?.Invoke(_portfolios);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        void _client_NewPortfolio(BinanceEntity.AccountResponse portfs)
        {
            try
            {
                if (portfs == null)
                {
                    return;
                }

                if (_portfolios == null)
                {
                    _portfolios = new List<Portfolio>();
                }

                if (portfs.balances == null)
                {
                    return;
                }

                foreach (var onePortf in portfs.balances)
                {
                    Portfolio newPortf = new Portfolio();
                    newPortf.Number = onePortf.asset;
                    newPortf.ValueCurrent = Convert.ToDecimal(onePortf.free.Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                    newPortf.ValueBlocked = Convert.ToDecimal(onePortf.locked.Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);

                    _portfolios.Add(newPortf);
                }

                PortfolioEvent?.Invoke(_portfolios);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        void _client_Disconnected()
        {
            DisconnectEvent?.Invoke();
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        private List<Security> _securities;

        void _client_UpdatePairs(BinanceEntity.SecurityResponce pairs)
        {
            if (_securities == null)
            {
                _securities = new List<Security>();
            }

            foreach (var sec in pairs.symbols)
            {
                Security security = new Security();
                security.Name = sec.symbol;
                security.NameFull = sec.symbol;
                security.NameClass = sec.quoteAsset;
                security.NameId = sec.symbol + sec.quoteAsset;
                security.SecurityType = SecurityType.CurrencyPair;
                // sec.filters[1] - минимальный объем равный цена * объем
                security.Lot = Convert.ToDecimal(sec.filters[2].stepSize.Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                security.PriceStep = Convert.ToDecimal(sec.filters[0].tickSize.Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                security.PriceStepCost = security.PriceStep;
               
                security.PriceLimitLow = Convert.ToDecimal(sec.filters[0].minPrice.Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                security.PriceLimitHigh = Convert.ToDecimal(sec.filters[0].maxPrice.Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);

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

        void _client_Connected()
        {
            ConnectEvent?.Invoke();
            ServerStatus = ServerConnectStatus.Connect;
        }

// исходящие события

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

        /// <summary>
        /// соединение с API установлено
        /// </summary>
        public event Action ConnectEvent;

        /// <summary>
        /// соединение с API разорвано
        /// </summary>
        public event Action DisconnectEvent;

// сообщения для лога

        /// <summary>
        /// добавить в лог новое сообщение
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
}
