using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;

namespace OsEngine.Market.Servers.Oanda
{
    public class OandaServer: AServer
    {
        public OandaServer()
        {
            OandaServerRealization realization = new OandaServerRealization();
            ServerRealization = realization;

            CreateParameterString("Client ID", "");
            CreateParameterPassword("Token", "");
            CreateParameterBoolean("IsDemo", false);
        }
    }
    
    public class OandaServerRealization : IServerRealization
    {
        public OandaServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread watcherConnectThread = new Thread(ReconnectThread);
            watcherConnectThread.IsBackground = true;
            watcherConnectThread.Start();
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public ServerType ServerType => ServerType.Oanda;

        public List<IServerParameter> ServerParameters { get; set; }

        public DateTime ServerTime { get; set; }

        private DateTime _lastTimeIncomeDate = DateTime.MinValue;

        private void ReconnectThread()
        {
            while (true)
            {
                Thread.Sleep(5000);

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    continue;
                }

                if (DateTime.Now.DayOfWeek == DayOfWeek.Saturday
                    ||
                    DateTime.Now.DayOfWeek == DayOfWeek.Sunday)
                {
                    continue;
                }

                if (_lastTimeIncomeDate == DateTime.MinValue)
                {
                    continue;
                }

                if (_lastTimeIncomeDate.AddMinutes(5) < DateTime.Now)
                {
                    _lastTimeIncomeDate = DateTime.Now;
                    Dispose();
                }
            }
        }

        // запросы

        private OandaClient _client;

        public void Connect()
        {
            if (_client == null)
            {
                _client = new OandaClient();

                _client.ConnectionFail += ClientOnConnectionFail;
                _client.ConnectionSucsess += ClientOnConnectionSucsess;
                _client.LogMessageEvent += ClientOnLogMessageEvent;
                _client.NewMyTradeEvent += ClientOnNewMyTradeEvent;
                _client.NewOrderEvent += ClientOnNewOrderEvent;
                _client.NewTradeEvent += ClientOnNewTradeEvent;
                _client.PortfolioChangeEvent += ClientOnPortfolioChangeEvent;
                _client.NewSecurityEvent += ClientOnNewSecurityEvent;
                _client.MarketDepthChangeEvent += ClientOnMarketDepthChangeEvent;
            }
            _client.Connect(((ServerParameterString)ServerParameters[0]).Value, ((ServerParameterPassword)ServerParameters[1]).Value,
                ((ServerParameterBool)ServerParameters[2]).Value);
        }

        public void Dispose()
        {
            if (_client != null)
            {
                _client.ConnectionFail -= ClientOnConnectionFail;
                _client.ConnectionSucsess -= ClientOnConnectionSucsess;
                _client.LogMessageEvent -= ClientOnLogMessageEvent;
                _client.NewMyTradeEvent -= ClientOnNewMyTradeEvent;
                _client.NewOrderEvent -= ClientOnNewOrderEvent;
                _client.NewTradeEvent -= ClientOnNewTradeEvent;
                _client.PortfolioChangeEvent -= ClientOnPortfolioChangeEvent;
                _client.NewSecurityEvent -= ClientOnNewSecurityEvent;
                _client.MarketDepthChangeEvent -= ClientOnMarketDepthChangeEvent;
            }

            try
            {
                if (_client != null && ServerStatus == ServerConnectStatus.Connect)
                {
                    _client.Disconnect();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

            _client = null;
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        public void GetSecurities()
        {
            if (_portfolios == null)
            {
                GetPortfolios();
                Thread.Sleep(10000);
            }

            if (_portfolios != null)
            {
                _client.GetSecurities(_portfolios);
            }
        }

        public void GetPortfolios()
        {
            _client.GetPortfolios();
        }

        public void SendOrder(Order order)
        {
            _client.ExecuteOrder(order);
        }

        public void CanselOrder(Order order)
        {
            _client.CanselOrder(order);
        }

        public void Subscrible(Security security)
        {
            _client.StartStreamThreads();
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime,
            DateTime actualTime)
        {
            return null;
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        public void GetOrdersState(List<Order> orders)
        {
            
        }

        // разбор входящих данных

        private void ClientOnMarketDepthChangeEvent(MarketDepth marketDepth)
        {
            if (MarketDepthEvent != null)
            {
                MarketDepthEvent(marketDepth);
            }
        }

        private void ClientOnNewSecurityEvent(List<Security> securities)
        {
            if (SecurityEvent != null)
            {
                SecurityEvent(securities);
            }
        }

        private List<Portfolio> _portfolios;

        private void ClientOnPortfolioChangeEvent(Portfolio portfolio)
        {
            if (portfolio == null)
            {
                return;
            }

            if (_portfolios == null)
            {
                _portfolios = new List<Portfolio>();
            }

            if (_portfolios.Find(p => p.Number == portfolio.Number) == null)
            {
                _portfolios.Add(portfolio);
            }

            if (PortfolioEvent != null)
            {
                PortfolioEvent(_portfolios);
            }
        }

        private void ClientOnNewTradeEvent(Trade trade)
        {
            if (NewTradesEvent != null)
            {
                NewTradesEvent(trade);
            }
        }

        private void ClientOnNewOrderEvent(Order order)
        {
            if (MyOrderEvent != null)
            {
                MyOrderEvent(order);
            }
        }

        private void ClientOnNewMyTradeEvent(MyTrade myTrade)
        {
            if (MyTradeEvent != null)
            {
                MyTradeEvent(myTrade);
            }
        }

        private void ClientOnLogMessageEvent(string message, LogMessageType type)
        {
            SendLogMessage(message, type);
        }

        private void ClientOnConnectionSucsess()
        {
            if (ConnectEvent != null)
            {
                ConnectEvent();
            }
        }

        private void ClientOnConnectionFail()
        {
            if (DisconnectEvent != null)
            {
                DisconnectEvent();
            }
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
}
