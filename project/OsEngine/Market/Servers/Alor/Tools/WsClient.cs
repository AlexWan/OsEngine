using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OsEngine.Market.Servers.Alor.Dto;
using OsEngine.Market.Servers.Alor.Tools.Events;

namespace OsEngine.Market.Servers.Alor.Tools
{
    public class WsClient
    {
        private readonly WsConnectionClient _connection;

        private readonly TokenProvider _tokenProvider;

        private readonly List<Subscription> _subscriptions;

        public WsClient(string name, Uri host, TokenProvider tokenProvider)
        {
            _subscriptions = new List<Subscription>();
            _connection = new WsConnectionClient(name, host);
            _tokenProvider = tokenProvider;
            
            _connection.MessageEvent += OnMessage;
        }

        public void SubscribePortfolioChanges(string exchangeCode, string portfolioId, bool isForts)
        {
            var request = new
            {
                portfolio = portfolioId,
                exchange = exchangeCode,
                format = "Simple",
                frequency = 0
            };
            if (isForts)
            {
                Subscribe("SpectraRisksGetAndSubscribe", request);
            }
            else
            {
                Subscribe("RisksGetAndSubscribe", request);
            }
        }

        private void Subscribe(string operationName, dynamic request)
        {
            var reqGuid = Guid.NewGuid();
            var token = _tokenProvider.GetAccessTokenAsync().Result;

            request.guid = reqGuid.ToString();
            request.token = token;
            request.opcode = operationName;

            var message = JsonConvert.SerializeObject(request);
            
            _subscriptions.Add(new Subscription()
            {
                guid = reqGuid.ToString(),
                opcode = operationName,
                state = SubscriptionStatusEnum.Pending
            });
            _connection.SendTextMessage(message);
        }
        
        private void OnMessage(object sender, MessageEventArgs e)
        {
            try
            {
                string data = Encoding.UTF8.GetString(e.Buffer);
                MessageData message = ProcessWebsocketMessage(data);
                if (message.messageScheme == MessageSchemeEnum.AcknowledgementMsg)
                {
                    UpdateSubscription(message);
                }
                RaiseMessageEvent(DateTime.Now, message);
            }
            catch (Exception ex)
            {
                var msg = $"Error ProcessWebsocketMessage() {ex.Message}\n{ex.StackTrace}";
                RaiseErrorEvent(DateTime.Now, msg);
            }
        }
        
        public MessageData ProcessWebsocketMessage(string data)
        {
            MessageData message = new MessageData();
            data = data.TrimEnd('\0');
            data = data.Replace("\n", string.Empty);
            data = data.Replace(" ", string.Empty);
            dynamic msg;
            if (data.Contains("requestGuid"))
            {
                msg = JObject.Parse(data);

                message.guid = msg.requestGuid;
                message.message = msg.message;
                message.msgType = MessageTypeEnum.Ack;
                message.messageScheme = MessageSchemeEnum.AcknowledgementMsg;
                return message;
            }
            
            msg = JObject.Parse(data);
            message.data = msg.data;
            message.guid = msg.guid;
            message.messageScheme = GetMessageSchemeFromData(data);
            message.msgType = MessageTypeEnum.Refresh;

            return message;
        }

        private MessageSchemeEnum GetMessageSchemeFromData(string data)
        {
            MessageSchemeEnum msgScheme = MessageSchemeEnum.Unknown;
            if (data.Contains("bids"))
            {
                msgScheme = MessageSchemeEnum.OrderbookSnapshotMsg;
            } else if (data.Contains("orderno"))
            {
                msgScheme = MessageSchemeEnum.TradeMsg;
            } else if (data.Contains("last_price_time"))
            {
                msgScheme = MessageSchemeEnum.SecurityMsg;
            }
            return msgScheme;
        }

        private void UpdateSubscription(MessageData message)
        {
            var subscriptionIdx = _subscriptions.FindIndex(s => s.guid == message.guid);
            if (subscriptionIdx >= 0)
            {
                _subscriptions[subscriptionIdx].state = SubscriptionStatusEnum.Active;
            }
        }
        
        private void RaiseMessageEvent(DateTime timestamp,MessageData data)
        {
            var messageCallback = new SocketMessageEventArgs() {Data = data, TimeStamp = timestamp};
            OnSocketMessage(messageCallback);
        }

        private void RaiseErrorEvent(DateTime timestamp, string errorMsg)
        {
            var errorCallback = new SocketErrorEventArgs() {TimeStamp = timestamp, ErrorMessage = errorMsg};
            
            OnSocketError(errorCallback);
        }
        
        private void OnSocketMessage(SocketMessageEventArgs e)
        {
            var handler = SocketMessageEvent;
            handler?.Invoke(this, e);
        }
        
        private void OnSocketError(SocketErrorEventArgs e)
        {
            var handler = SocketErrorEvent;
            handler?.Invoke(this, e);
        }
        
        public event EventHandler<SocketMessageEventArgs> SocketMessageEvent;
        public event EventHandler<SocketErrorEventArgs> SocketErrorEvent;
    }
}