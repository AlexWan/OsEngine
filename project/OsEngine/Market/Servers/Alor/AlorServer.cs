using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;

namespace OsEngine.Market.Servers.Alor
{
    public class AlorServer : AServer
    {
        public AlorServer()
        {
            AlorServerRealization realization = new AlorServerRealization();
            ServerRealization = realization;

            CreateParameterPassword(OsLocalization.Market.ServerParamToken, "");
        }
    }

    public class AlorServerRealization : IServerRealization
    {
        #region Properties

        public ServerType ServerType => ServerType.Alor;
        public ServerConnectStatus ServerStatus { get; set; } = ServerConnectStatus.Disconnect;
        public List<IServerParameter> ServerParameters { get; set; }
        public DateTime ServerTime { get; set; }

        #endregion
       
        
        #region Methods
        public void Connect()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void GetSecurities()
        {
            throw new NotImplementedException();
        }

        public void GetPortfolios()
        {
            throw new NotImplementedException();
        }

        public void SendOrder(Order order)
        {
            throw new NotImplementedException();
        }

        public void CancelOrder(Order order)
        {
            throw new NotImplementedException();
        }

        public void CancelAllOrders()
        {
            throw new NotImplementedException();
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            throw new NotImplementedException();
        }

        public void Subscrible(Security security)
        {
            throw new NotImplementedException();
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime,
            DateTime actualTime)
        {
            throw new NotImplementedException();
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            throw new NotImplementedException();
        }

        public void GetOrdersState(List<Order> orders)
        {
            throw new NotImplementedException();
        }

        public void ResearchTradesToOrders(List<Order> orders)
        {
            throw new NotImplementedException();
        }
        
        #endregion
        
        #region Delegates and events
        
        public event Action<Order> MyOrderEvent;
        public event Action<MyTrade> MyTradeEvent;
        public event Action<List<Portfolio>> PortfolioEvent;
        public event Action<List<Security>> SecurityEvent;
        public event Action<MarketDepth> MarketDepthEvent;
        public event Action<Trade> NewTradesEvent;
        public event Action ConnectEvent;
        public event Action DisconnectEvent;
        public event Action<string, LogMessageType> LogMessageEvent;
        
        #endregion
    }
}