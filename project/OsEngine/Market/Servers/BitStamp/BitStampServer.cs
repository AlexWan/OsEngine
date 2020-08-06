using System;
using System.Collections.Generic;
using System.Globalization;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.BitStamp.BitStampEntity;
using OsEngine.Market.Servers.Entity;

namespace OsEngine.Market.Servers.BitStamp
{
    /// <summary>
    /// BitStamp server
    /// сервер BitStamp
    /// </summary>
    public class BitStampServer : AServer
    {
        public BitStampServer()
        {
            BitStampServerRealization realization = new BitStampServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamId, "");
            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");
        }
    }
    
    public class BitStampServerRealization : IServerRealization
    {
        public BitStampServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;
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
            get { return ServerType.BitStamp; }
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

// requests / запросы

        /// <summary>
        /// bitstamp client
        /// </summary>
        private BitstampClient _client;

        /// <summary>
        /// dispose API
        /// освободить апи
        /// </summary>
        public void Dispose()
        {
            if (_client != null)
            {
                _client.Dispose();

                _client.Connected -= ClientOnConnected;
                _client.UpdatePairs -= ClientOnUpdatePairs;
                _client.Disconnected -= ClientOnDisconnected;
                _client.UpdatePortfolio -= ClientOnUpdatePortfolio;
                _client.UpdateMarketDepth -= ClientOnUpdateMarketDepth;
                _client.NewTradesEvent -= ClientOnNewTradesEvent;
                _client.MyTradeEvent -= ClientOnMyTradeEvent;
                _client.MyOrderEvent -= ClientOnMyOrderEvent;
                _client.LogMessageEvent -= ClientOnLogMessageEvent;
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
                _client = new BitstampClient(((ServerParameterString) ServerParameters[1]).Value,
                    ((ServerParameterPassword) ServerParameters[2]).Value,
                    ((ServerParameterString) ServerParameters[0]).Value);
                _client.Connected += ClientOnConnected;
                _client.UpdatePairs += ClientOnUpdatePairs;
                _client.Disconnected += ClientOnDisconnected;
                _client.UpdatePortfolio += ClientOnUpdatePortfolio;
                _client.UpdateMarketDepth += ClientOnUpdateMarketDepth;
                _client.NewTradesEvent += ClientOnNewTradesEvent;
                _client.MyTradeEvent += ClientOnMyTradeEvent;
                _client.MyOrderEvent += ClientOnMyOrderEvent;
                _client.LogMessageEvent += ClientOnLogMessageEvent;
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
        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime,
            DateTime actualTime)
        {
            return null;
        }

        /// <summary>
        /// take ticks data for period
        /// взять тиковые данные по инструменту за период
        /// </summary>
        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        /// <summary>
        /// request order status
        /// запросить статус ордеров
        /// </summary>
        public void GetOrdersState(List<Order> orders)
        {
            return;
        }

        // incoming data processing
        // обработка входящих данных

        private void ClientOnLogMessageEvent(string message, LogMessageType type)
        {
            SendLogMessage(message, type);
        }

        private void ClientOnMyOrderEvent(Order order)
        {
            order.ServerType = ServerType.BitStamp;

            if (MyOrderEvent != null)
            {
                MyOrderEvent(order);
            }
        }

        private void ClientOnMyTradeEvent(MyTrade trade)
        {
            if (MyTradeEvent != null)
            {
                MyTradeEvent(trade);
            }
        }

        private void ClientOnNewTradesEvent(Trade trade)
        {
            if (NewTradesEvent != null)
            {
                NewTradesEvent(trade);
            }
        }

        private void ClientOnUpdateMarketDepth(MarketDepth marketDepth)
        {
            marketDepth.Time = ServerTime;

            if (marketDepth.Time == DateTime.MinValue)
            {
                marketDepth.Time = DateTime.Now;
            }

            if (MarketDepthEvent != null)
            {
                MarketDepthEvent(marketDepth);
            }
        }

        private List<Portfolio> _portfolios;

        private void ClientOnUpdatePortfolio(BalanceResponse portf)
        {
            try
            {
                if (portf == null ||
                    portf.eur_balance == null)
                {
                    return;
                }

                if (_portfolios == null)
                {
                    _portfolios = new List<Portfolio>();
                }

                Portfolio osPortEur = _portfolios.Find(p => p.Number == "eurPortfolio");

                if (osPortEur == null)
                {
                    osPortEur = new Portfolio();
                    osPortEur.Number = "eurPortfolio";
                    osPortEur.ValueBegin = portf.eur_balance.ToDecimal();
                    _portfolios.Add(osPortEur);
                }

                osPortEur.ValueCurrent = portf.eur_balance.ToDecimal();
                osPortEur.ValueBlocked = portf.eur_reserved.ToDecimal();

                Portfolio osPortUsd = _portfolios.Find(p => p.Number == "usdPortfolio");

                if (osPortUsd == null)
                {
                    osPortUsd = new Portfolio();
                    osPortUsd.Number = "usdPortfolio";
                    osPortUsd.ValueBegin = portf.usd_balance.ToDecimal();
                    _portfolios.Add(osPortUsd);
                }

                osPortUsd.ValueCurrent = portf.usd_balance.ToDecimal();
                osPortUsd.ValueBlocked = portf.usd_reserved.ToDecimal();


                Portfolio osPortBtc = _portfolios.Find(p => p.Number == "btcPortfolio");

                if (osPortBtc == null)
                {
                    osPortBtc = new Portfolio();
                    osPortBtc.Number = "btcPortfolio";
                    osPortBtc.ValueBegin = portf.btc_balance.ToDecimal();
                    _portfolios.Add(osPortBtc);
                }

                osPortBtc.ValueCurrent = portf.btc_balance.ToDecimal();
                osPortBtc.ValueBlocked = portf.btc_reserved.ToDecimal();

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

        private List<Security> _securities;

        private void ClientOnUpdatePairs(List<PairInfoResponse> pairs)
        {
            if (_securities == null)
            {
                _securities = new List<Security>();
            }
            for (int i = 0; i < pairs.Count; i++)
            {

                Security security = new Security();
                security.Name = pairs[i].url_symbol;
                security.NameFull = pairs[i].url_symbol;
                security.NameId = pairs[i].url_symbol;
                security.NameClass = "currency";
                security.SecurityType = SecurityType.CurrencyPair;

                security.Lot = 1;
                security.PriceStep = 0.01m;
                security.PriceStepCost = 0.01m;

                if (security.PriceStep < 1)
                {
                    security.Decimals = Convert.ToString(security.PriceStep).Split(',')[1].Length;
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

        private void ClientOnConnected()
        {
            ServerStatus = ServerConnectStatus.Connect;

            if (ConnectEvent != null)
            {
                ConnectEvent();
            }
        }

        private void ClientOnDisconnected()
        {
            ServerStatus = ServerConnectStatus.Disconnect;
            if (DisconnectEvent != null)
            {
                DisconnectEvent();
            }
        }

        // outgoing events
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
        /// appear new portfolios
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
