﻿using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;

namespace OsEngine.Market.Servers.NinjaTrader
{
    /// <summary>
    /// сервер Ninja
    /// </summary>
    public class NinjaTraderServer: AServer
    {
        public NinjaTraderServer()
        {
            ServerRealization = new NinjaTraderServerRealization();

            CreateParameterString("ServerAddress", "localhost");
            CreateParameterPassword("Port", "11000");
        }
    }
    
    public class NinjaTraderServerRealization : IServerRealization
    {
        public NinjaTraderServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public ServerType ServerType => ServerType.NinjaTrader;

        public List<IServerParameter> ServerParameters { get; set; }

        public DateTime ServerTime { get; set; }

 // запросы

        private NinjaTraderClient _client;

        public void Connect()
        {
            if (_client == null)
            {
                _client = new NinjaTraderClient(((ServerParameterString)ServerParameters[1]).Value, ((ServerParameterPassword)ServerParameters[0]).Value);
                _client.Connected += ClientOnConnected;
                _client.UpdateSecuritiesEvent += ClientOnUpdateSecuritiesEvent;
                _client.Disconnected += ClientOnDisconnected;
                _client.UpdatePortfolio += ClientOnUpdatePortfolio;
                _client.UpdateMarketDepth += ClientOnUpdateMarketDepth;
                _client.NewTradesEvent += ClientOnNewTradesEvent;
                _client.MyTradeEvent += ClientOnMyTradeEvent;
                _client.MyOrderEvent += ClientOnMyOrderEvent;
                _client.LogMessageEvent += ClientOnLogMessageEvent;
            }
        }

        public void Dispose()
        {
            if (_client != null)
            {
                _client.Dispose();

                _client.Connected -= ClientOnConnected;
                _client.UpdateSecuritiesEvent -= ClientOnUpdateSecuritiesEvent;
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

        public void GetSecurities()
        {
            _client.GetSecurities();
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
            _client.SubscribleTradesAndDepths(security);
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

        private void ClientOnLogMessageEvent(string message, LogMessageType type)
        {
            SendLogMessage(message, type);
        }

        private void ClientOnMyOrderEvent(Order order)
        {
            MyOrderEvent?.Invoke(order);
        }

        private void ClientOnMyTradeEvent(MyTrade myTrade)
        {
            MyTradeEvent?.Invoke(myTrade);
        }

        private void ClientOnNewTradesEvent(Trade trade)
        {
            NewTradesEvent?.Invoke(trade);
        }

        private void ClientOnUpdateMarketDepth(MarketDepth marketDepth)
        {
            MarketDepthEvent?.Invoke(marketDepth);
        }

        private void ClientOnUpdatePortfolio(List<Portfolio> portfolios)
        {
            PortfolioEvent?.Invoke(portfolios);
        }

        private void ClientOnDisconnected()
        {
            DisconnectEvent?.Invoke();
        }

        private void ClientOnUpdateSecuritiesEvent(List<Security> securities)
        {
            SecurityEvent?.Invoke(securities);
        }

        private void ClientOnConnected()
        {
            ConnectEvent?.Invoke();
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
