using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using Kraken.WebSockets;
using System.Threading.Tasks;


namespace OsEngine.Market.Servers.Kraken
{
    public class KrakenServer : AServer
    {
        public KrakenServer()
        {
            KrakenServerRealization realization = new KrakenServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");
            CreateParameterEnum("Leverage type", "None", new List<string> (){"None","Two","Three","Four","Five"});

        }

        /// <summary>
        /// instrument history query
        /// запрос истории по инструменту
        /// </summary>
        public List<Candle> GetCandleHistory(string nameSec, TimeSpan tf)
        {
            return ((KrakenServerRealization)ServerRealization).GetCandleHistory(nameSec, tf);
        }
    }

    public class KrakenServerRealization : IServerRealization
    {
        public KrakenServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        /// <summary>
        /// server type
        /// тип сервера
        /// </summary>
        public ServerType ServerType
        {
            get { return ServerType.Kraken; }
        }

        /// <summary>
        /// server parameters
        /// параметры сервера
        /// </summary>
        public List<IServerParameter> ServerParameters { get; set; }

        /// <summary>
        /// server time
        /// время сервера
        /// </summary>
        public DateTime ServerTime { get; set; }

        // requests
        // запросы

        /// <summary>
        /// binance client
        /// </summary>
        private KrakenClient _clientRest;

        private KrakenApi _clientSocketPublicData;

        private KrakenApi _clientSocketPrivateData;

        /// <summary>
        /// release API
        /// освободить апи
        /// </summary>
        public void Dispose()
        {
            if (_clientRest != null)
            {
                _clientRest.Dispose();

                _clientRest.Connected -= _client_Connected;
                _clientRest.UpdatePairs -= _client_UpdatePairs;
                _clientRest.Disconnected -= _client_Disconnected;
                _clientRest.NewPortfolioEvent -= _client_NewPortfolioSpot;
                _clientRest.UpdateMarketDepth -= _client_UpdateMarketDepth;
                _clientRest.NewTradesEvent -= _client_NewTradesEvent;
                _clientRest.MyTradeEvent -= _client_MyTradeEvent;
                _clientRest.MyOrderEvent -= _client_MyOrderEvent;
                _clientRest.LogMessageEvent -= SendLogMessage;
            }

            _clientRest = null;
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        /// <summary>
        /// connect to API
        /// подсоединиться к апи
        /// </summary>
        public async void Connect()
        {
            if (_clientRest == null)
            {
                _clientRest = new KrakenClient(
                    ((ServerParameterString)ServerParameters[0]).Value,
                    ((ServerParameterPassword)ServerParameters[1]).Value);
                _clientRest.Connected += _client_Connected;
                _clientRest.UpdatePairs += _client_UpdatePairs;
                _clientRest.Disconnected += _client_Disconnected;
                _clientRest.NewPortfolioEvent += _client_NewPortfolioSpot;

                _clientRest.UpdateMarketDepth += _client_UpdateMarketDepth;
                _clientRest.NewTradesEvent += _client_NewTradesEvent;
                _clientRest.MyTradeEvent += _client_MyTradeEvent;
                _clientRest.MyOrderEvent += _client_MyOrderEvent;
                _clientRest.LogMessageEvent += SendLogMessage;
            }

            KrakenApi.LogMessageEvent += SendLogMessage;
            KrakenApi.TradeUpdateEvent += KrakenApi_TradeUpdateEvent;
            KrakenApi.MarketDepthUpdateEvent += KrakenApi_MarketDepthUpdateEvent;
            KrakenApi.MyTradeUpdateEvent += KrakenApi_MyTradeUpdateEvent;
            KrakenApi.OrdersUpdateEvent += KrakenApi_OrdersUpdateEvent;

            _clientRest.Connect();

            _clientSocketPublicData = new KrakenApi();

            _clientSocketPublicData.ConfigureAuthentication(
         "https://api.kraken.com",
         ((ServerParameterString)ServerParameters[0]).Value,
         ((ServerParameterPassword)ServerParameters[1]).Value);

            //_clientSocket.ConfigureWebsocket(("wss://ws-auth.kraken.com"));
            _clientSocketPublicData.ConfigureWebsocket(("wss://ws.kraken.com"));

            AuthToken token = await _clientSocketPublicData.AuthenticationClient.GetWebsocketToken();

            var client = _clientSocketPublicData.BuildClient();

            await Task.Run(() => KrakenApi.RunKraken(client, token));

            _clientSocketPrivateData = new KrakenApi();
            _clientSocketPrivateData.ConfigureAuthentication(
              "https://api.kraken.com",
               ((ServerParameterString)ServerParameters[0]).Value,
               ((ServerParameterPassword)ServerParameters[1]).Value);

            _clientSocketPrivateData.ConfigureWebsocket(("wss://ws-auth.kraken.com"));

            token = await _clientSocketPrivateData.AuthenticationClient.GetWebsocketToken();

            client = _clientSocketPrivateData.BuildClient();

            await Task.Run(() => KrakenApi.RunKraken(client, token));

        }

        private void KrakenApi_OrdersUpdateEvent(Order order)
        {
            if (MyOrderEvent != null)
            {
                MyOrderEvent(order);
            }
        }

        private void KrakenApi_MyTradeUpdateEvent(MyTrade myTrade)
        {
            if(MyTradeEvent != null)
            {
                MyTradeEvent(myTrade);
            }
        }

        private void KrakenApi_MarketDepthUpdateEvent(MarketDepth md)
        {
            if(MarketDepthEvent != null)
            {
                MarketDepthEvent(md);
            }
        }

        private void KrakenApi_TradeUpdateEvent(OsEngine.Entity.Trade trade)
        {
            if(NewTradesEvent != null)
            {
                NewTradesEvent(trade);
            }
        }

        /// <summary>
        /// request securities
        /// запросить бумаги
        /// </summary>
        public void GetSecurities()
        {
            _clientRest.GetSecurities();
        }

        /// <summary>
        /// request portfolios
        /// запросить портфели
        /// </summary>
        public void GetPortfolios()
        {
            _clientRest.GetBalanceAndPortfolio();
        }

        /// <summary>
        /// send order
        /// исполнить ордер
        /// </summary>
        public void SendOrder(Order order)
        {
            KrakenApi.AddOrder(order);
        }

        /// <summary>
        /// cancel order
        /// отозвать ордер
        /// </summary>
        public void CancelOrder(Order order)
        {
            KrakenApi.CancelOrder(order);
        }

        /// <summary>
        /// subscribe
        /// подписаться 
        /// </summary>
        public void Subscrible(Security security)
        {
            _clientSocketPublicData.Subscrible(security.NameFull,security.Name);
        }

        /// <summary>
        /// take candle history for period
        /// взять историю свечек за период
        /// </summary>
        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder,
            DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            int curStartTime = (int)(startTime - new DateTime(1970, 1, 1, 0, 0, 0, 0)).TotalSeconds;

            List<Candle> candles = new List<Candle>();

            while (true)
            {
                List<Candle> newCandles = _clientRest.GetCandles(security.Name, timeFrameBuilder.TimeFrameTimeSpan, curStartTime);

                if(newCandles == null ||
                    newCandles.Count == 0)
                {
                    break;
                }

                Candle lastCandle = newCandles[newCandles.Count - 1];

                candles.Merge(newCandles);

                if(newCandles.Count == 1)
                {
                    break;
                }

                curStartTime = (int)(lastCandle.TimeStart - new DateTime(1970, 1, 1, 0, 0, 0, 0)).TotalSeconds;
            }

            return candles;
        }

        /// <summary>
        /// take ticks data on instrument for period
        /// взять тиковые данные по инструменту за период
        /// </summary>
        public List<OsEngine.Entity.Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime lastDate)
        {
            return null;
        }

        /// <summary>
        /// request order state
        /// запросить статус ордеров
        /// </summary>
        public void GetOrdersState(List<Order> orders)
        {
            _clientRest.GetAllOrders(orders);
        }

        /// <summary>
        /// server status
        /// статус серверов
        /// </summary>
        public ServerConnectStatus ServerStatus { get; set; }

        /// <summary>
        /// request instrument history
        /// запрос истории по инструменту
        /// </summary>
        public List<Candle> GetCandleHistory(string nameSec, TimeSpan tf)
        {
            return _clientRest.GetCandles(nameSec, tf);
        }

        //parsing incoming data
        // разбор входящих данных

        void _client_MyOrderEvent(Order order)
        {
            if (MyOrderEvent != null)
            {
                MyOrderEvent(order);
            }
        }

        void _client_MyTradeEvent(MyTrade myTrade)
        {
            if (MyTradeEvent != null)
            {
                MyTradeEvent(myTrade);
            }
        }

        /// <summary>
        /// multi-threaded access locker to ticks
        /// блокиратор многопоточного доступа к тикам
        /// </summary>
        private readonly object _newTradesLoker = new object();

        void _client_NewTradesEvent(OsEngine.Entity.Trade trade)
        {
            lock (_newTradesLoker)
            {
                if (NewTradesEvent != null)
                {
                    NewTradesEvent(trade);
                }
            }
        }

        /// <summary>
        /// all depths
        /// все стаканы
        /// </summary>
        private List<MarketDepth> _depths;

        private readonly object _depthLocker = new object();

        void _client_UpdateMarketDepth(MarketDepth myDepth)
        {
            try
            {
                lock (_depthLocker)
                {
                    if (MarketDepthEvent != null)
                    {
                        MarketDepthEvent(myDepth.GetCopy());
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #region Портфели

        private List<Portfolio> _portfolios = new List<Portfolio>();

        void _client_NewPortfolioSpot(Portfolio portfs)
        {
            try
            {
                Portfolio myPortfolio = _portfolios.Find(p => p.Number == "KrakenTradePortfolio");

                if (myPortfolio == null)
                {
                    Portfolio newPortf = new Portfolio();
                    newPortf.Number = "KrakenTradePortfolio";
                    newPortf.ValueBegin = 1;
                    newPortf.ValueCurrent = 1;
                    _portfolios.Add(newPortf);
                    myPortfolio = newPortf;
                }

                List<PositionOnBoard> poses = portfs.GetPositionOnBoard();

                if(poses != null)
                {
                    foreach (var one in poses)
                    {
                        myPortfolio.SetNewPosition(one);
                    }
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

        #endregion

        void _client_Disconnected()
        {
            if (DisconnectEvent != null)
            {
                DisconnectEvent();
            }
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        private List<Security> _securities;

        void _client_UpdatePairs(Security pair)
        {
            if (_securities == null)
            {
                _securities = new List<Security>();
            }

            _securities.Add(pair);
            
            if (SecurityEvent != null)
            {
                SecurityEvent(_securities);
            }
        }

        void _client_Connected()
        {
            if (ConnectEvent != null)
            {
                ConnectEvent();
            }
            ServerStatus = ServerConnectStatus.Connect;
        }

        // outgoing messages
        // исходящие события

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
        /// new portfolios appeared
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
        public event Action<OsEngine.Entity.Trade> NewTradesEvent;

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

        // log messages
        // сообщения для лога

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
        /// outgoing log message
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        
    }

    public enum KrakenLeverageType
    {
        None,
        Two,
        Three,
        Four,
        Five
    }
}
