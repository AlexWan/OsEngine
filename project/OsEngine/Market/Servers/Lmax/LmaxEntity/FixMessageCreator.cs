using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OsEngine.Market.Servers.FixProtocolEntities;

namespace OsEngine.Market.Servers.Lmax.LmaxEntity
{
    /// <summary>
	/// create messages for exchange 
    /// создает сообщения для биржи
    /// </summary>
    public class FixMessageCreator
    {
        private StandartHeaderSettings _settings;

        public FixMessageCreator(StandartHeaderSettings settings)
        {
            _settings = settings;
        }

        private long _msgSeqNum;

        /// <summary>
		/// heartbeat message
        /// сообщение пулься
        /// </summary>
        public string HeartbeatMsg(bool isTrading)
        {
            var newMessage = new FixMessage(++_msgSeqNum, "0", isTrading, _settings);

            return newMessage.ToString();
        }

        /// <summary>
		/// message for entering
        /// сообщение для входа
        /// </summary>
        /// <param name="isTrading">true - if the entrance to the trading session, false for market data / true - если вход в торговую сессию, false для market data</param>
        /// <param name="heartbeatInterval"></param>
        /// <returns>сообщение в формате fix</returns>
        public string LogOnMsg(bool isTrading, int heartbeatInterval)
        {
            var newMessage = new FixMessage(++_msgSeqNum, "A", isTrading, _settings);

            var logOnFields = new List<Field>
            {
                new Field((int)Tags.EncryptMethod, "0"),
                new Field((int)Tags.HeartBtInt, heartbeatInterval.ToString()),
                new Field((int)Tags.ResetSeqNumFlag, "Y"),
                new Field((int)Tags.Username, _settings.Username),
                new Field((int)Tags.Password, _settings.Password)
            };

            newMessage.AddBody(logOnFields);

            var res = newMessage.ToString();

            return res;
        }

        /// <summary>
		/// message for exit
        /// сообщение для выхода
        /// </summary>
        /// <param name="isTrading">true - if the exit from the trading session, false for market data / true - если выход из торговой сессии, false для market data</param>
        /// <returns>message in fix format / сообщение в формате fix</returns>
        public string LogOutMsg(bool isTrading)
        {
            var newMessage = new FixMessage(++_msgSeqNum, "5", isTrading, _settings);

            var res = newMessage.ToString();

            return res;
        }

        /// <summary>
		/// create message for test request
        /// создать сообщение для тестового запроса
        /// </summary>
        /// <param name="reqId"></param>
        /// <param name="isTrading"></param>
        /// <returns></returns>
        public string TestRequestMsg(string reqId, bool isTrading)
        {
            var newMessage = new FixMessage(++_msgSeqNum, "1", isTrading, _settings);

            var logOnFields = new List<Field> { new Field((int)Tags.TestReqID, reqId) };

            newMessage.AddBody(logOnFields);

            var res = newMessage.ToString();

            return res;
        }

        /// <summary>
		/// create message for new order
        /// создать сообщение для нового ордера
        /// </summary>
        public string NewOrderSingleMsg(string clOrdId, string securityId, string side,
                                              string orderQty, string ordType, string price)
        {
            var newMessage = new FixMessage(++_msgSeqNum, "D", true, _settings);

            var newOrder = new List<Field>
            {
                new Field((int) Tags.ClOrdID, clOrdId),
                new Field((int) Tags.SecurityID, securityId),
                new Field((int) Tags.SecurityIDSource, "8"),
                new Field((int) Tags.Side, side),
                new Field((int) Tags.ExecInst, "H"),
                new Field((int) Tags.TransactTime, DateTime.UtcNow.ToString("yyyyMMdd-HH:mm:ss")),
                new Field((int) Tags.OrderQty, orderQty),
                new Field((int) Tags.OrdType, ordType)
            };

            if (ordType == "2")// if limit order / если лимитный
            {
                newOrder.Add(new Field((int)Tags.Price, price));
                newOrder.Add(new Field((int)Tags.TimeInForce, "1"));
            }

            newMessage.AddBody(newOrder);

            var res = newMessage.ToString();

            return res;
        }

        /// <summary>
		/// create message for cancelling order
        /// создать сообщение для отмены ордера
        /// </summary>
        public string OrderCancelRequestMsg(string origClOrdId, string clOrdId, string securityId)
        {
            var newMessage = new FixMessage(++_msgSeqNum, "F", true, _settings);

            var newOrder = new List<Field>
            {
                new Field((int)Tags.OrigClOrdID, clOrdId),
                new Field((int)Tags.ClOrdID, origClOrdId),
                new Field((int)Tags.SecurityID, securityId),
                new Field((int)Tags.SecurityIDSource, "8"),
                new Field((int)Tags.TransactTime, DateTime.UtcNow.ToString("yyyyMMdd-HH:mm:ss"))
            };

            newMessage.AddBody(newOrder);

            var res = newMessage.ToString();

            return res;
        }


        /// <summary>
		/// create a message for request status
        /// создать сообщение для запроса статуса ордера
        /// </summary>
        /// <param name="clOrdId">order number specified by the user when creating/номер ордера заданный пользователем при создании</param>
        /// <param name="side">order side/направление ордера</param>
        /// <param name="securityId">instrument id/id инструмента</param>
        public string OrderStatusRequestMsg(string clOrdId, string securityId, string side)
        {
            var newMessage = new FixMessage(++_msgSeqNum, "H", true, _settings);

            var newOrder = new List<Field>
            {
                new Field((int)Tags.ClOrdID, clOrdId),
                new Field((int)Tags.SecurityID, securityId),
                new Field((int)Tags.SecurityIDSource, "8"),
                new Field((int)Tags.Side, side)
            };

            newMessage.AddBody(newOrder);

            var res = newMessage.ToString();

            return res;
        }


        public string TradeCaptureReportRequestMsg(string tradeRequestId, string tradeRequestType, string subscriptionRequestType, DateTime starTime, DateTime endTime)
        {
            var newMessage = new FixMessage(++_msgSeqNum, "AD", true, _settings);

            var newOrder = new List<Field>
            {
                new Field((int) Tags.TradeRequestID, tradeRequestId),
                new Field((int) Tags.TradeRequestType, tradeRequestType),
                new Field((int) Tags.SubscriptionRequestType, subscriptionRequestType),
                new Field((int) Tags.NoDates, "2"),
                new Field((int) Tags.TransactTime, starTime.ToString("yyyyMMdd-HH:mm:ss")),
                new Field((int) Tags.TransactTime, endTime.ToString("yyyyMMdd-HH:mm:ss"))
            };


            newMessage.AddBody(newOrder);

            var res = newMessage.ToString();

            return res;
        }
    }
}
