using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OsEngine.Entity;
using OsEngine.Logging;

namespace OsEngine.Market.Servers
{
    public class AServerOrdersHub
    {
        public AServerOrdersHub(AServer server) 
        {
            _server = server;

            IServerPermission permission = ServerMaster.GetServerPermission(server.ServerType);

            if(permission == null)
            {
                return;
            }

            if(permission.CanQueryOrdersAfterReconnect)
            {
                _canQueryOrdersAfterReconnect = true;
            }

            if(permission.CanQueryOrderStatus)
            {
                _canQueryOrderStatus = true;
            }

            if(_canQueryOrdersAfterReconnect == false 
                && _canQueryOrderStatus == false)
            {
                return;
            }



        }

        AServer _server;

        bool _canQueryOrdersAfterReconnect;

        bool _canQueryOrderStatus;

        #region Set orders

        public void SetOrderFromOsEngine(Order order)
        {
            if (_canQueryOrderStatus == false)
            {
                return;
            }

            _ordersFromOsEngine.Enqueue(order);
        }

        public void SetOrderFromApi(Order order)
        {
            if (_canQueryOrderStatus == false)
            {
                return;
            }

            _orderFromApi.Enqueue(order);
        }

        ConcurrentQueue<Order> _ordersFromOsEngine = new ConcurrentQueue<Order>();

        ConcurrentQueue<Order> _orderFromApi = new ConcurrentQueue<Order>();

        #endregion

        #region Orders Hub

        public void ClearAllButActive()
        {
               
        }

        public List<Order> OrdersByOsEngine = new List<Order>();

        public List<Order> OrdersUnknown = new List<Order>();

        #endregion

        #region Query order status

        public void SetBidAsk(decimal bid, decimal ask, Security security)
        {
            // это мы должны проверять тут лимитки от OsEngine

        }

        public event Action<Order> ActivStateOrderCheckStatusEvent;

        public event Action<Order> LostOrderEvent;

        #endregion

        #region Query orders after reconnect



        public event Action<Order> GetAllActivOrdersOnReconnectEvent;

        #endregion

        #region Log

        /// <summary>
        /// add a new message in the log
        /// </summary>
        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        /// <summary>
        /// outgoing messages for the log event
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }
}