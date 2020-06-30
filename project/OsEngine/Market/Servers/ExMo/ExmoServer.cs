using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;

namespace OsEngine.Market.Servers.ExMo
{
    public class ExmoServer : AServer
    {
        public ExmoServer()
        {
            ExmoServerRealization realization = new ExmoServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");
        }
    }

    public class ExmoServerRealization : IServerRealization
    {
        public ExmoServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        /// <summary>
        /// server type
        /// тип сервера
        /// </summary>
        public ServerType ServerType
        {
            get { return ServerType.Exmo; }
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
        private ExmoClient _client;

        /// <summary>
        /// release API
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
        /// connect to API
        /// подсоединиться к апи
        /// </summary>
        public void Connect()
        {
            if (_client == null)
            {
                _client = new ExmoClient(((ServerParameterString)ServerParameters[0]).Value, ((ServerParameterPassword)ServerParameters[1]).Value);
                _client.Connected += _client_Connected;
                _client.UpdatePairs += _client_UpdatePairs;
                _client.Disconnected += _client_Disconnected;
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
        /// request securities
        /// запросить бумаги
        /// </summary>
        public void GetSecurities()
        {
            _client.GetSecurities();
        }

        /// <summary>
        /// request portfolios
        /// запросить портфели
        /// </summary>
        public void GetPortfolios()
        {
            _client.GetBalance();
        }

        /// <summary>
        /// send order
        /// исполнить ордер
        /// </summary>
        public void SendOrder(Order order)
        {
            _client.ExecuteOrder(order);
        }

        /// <summary>
        /// cancel order
        /// отозвать ордер
        /// </summary>
        public void CancelOrder(Order order)
        {
            _client.CancelOrder(order);
        }

        /// <summary>
        /// subscribe
        /// подписаться 
        /// </summary>
        public void Subscrible(Security security)
        {
            _client.SubscribleTradesAndDepths(security);
        }

        /// <summary>
        /// take candle history for period
        /// взять историю свечек за период
        /// </summary>
        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder,
            DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        /// <summary>
        /// take ticks data on instrument for period
        /// взять тиковые данные по инструменту за период
        /// </summary>
        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime lastDate)
        {
            return null;
        }

        /// <summary>
        /// request order state
        /// запросить статус ордеров
        /// </summary>
        public void GetOrdersState(List<Order> orders)
        {
            _client.GetAllOrders(orders);
        }

        /// <summary>
        /// server status
        /// статус серверов
        /// </summary>
        public ServerConnectStatus ServerStatus { get; set; }

        //parsing incoming data разбор входящих данных

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

        void _client_NewTradesEvent(List<Trade> trades)
        {
            for (int i = 0; i < trades.Count; i++)
            {
                if (NewTradesEvent != null)
                {
                    NewTradesEvent(trades[i]);
                }
            }
        }

        void _client_UpdateMarketDepth(MarketDepth myDepth)
        {
            myDepth.Time = ServerTime;
            if (MarketDepthEvent != null)
            {
                MarketDepthEvent(myDepth);
            }

        }

        private List<Portfolio> _portfolios = new List<Portfolio>();

        void _client_UpdatePortfolio(Portfolio portfs)
        {
            bool isInArray = false;
            for (int i = 0; i < _portfolios.Count; i++)
            {
                if (_portfolios[i].Number == portfs.Number)
                {
                    _portfolios[i] = portfs;
                    isInArray = true;
                    break;
                }
            }

            if (isInArray == false)
            {
                _portfolios.Add(portfs);
            }

            if (PortfolioEvent != null)
            {
                PortfolioEvent(_portfolios);
            }
        }

        void _client_Disconnected()
        {
            if (DisconnectEvent != null)
            {
                DisconnectEvent();
            }
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        private List<Security> _securities;

        void _client_UpdatePairs(List<Security> pairs)
        {
            _securities = pairs;

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

        // outgoing messages исходящие события

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
        public event Action<Trade> NewTradesEvent;

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
}
