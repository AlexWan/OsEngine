using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kraken.WebSockets.Events;
using Kraken.WebSockets.Extensions;
using Kraken.WebSockets.Messages;

namespace Kraken.WebSockets
{
    /// <summary>
    /// Kraken API client.
    /// </summary>
    internal sealed class KrakenApiClient : IKrakenApiClient
    {
        private readonly IKrakenSocket socket;
        private readonly IKrakenMessageSerializer serializer;

        private bool disposedValue = false;

        /// <summary>
        /// Gets the system status.
        /// </summary>
        /// <value>The system status.</value>
        public SystemStatus SystemStatus { get; private set; }

        /// <summary>
        /// Gets the subscriptions.
        /// </summary>
        /// <value>The subscriptions.</value>
        public IDictionary<int, SubscriptionStatus> Subscriptions { get; } = new Dictionary<int, SubscriptionStatus>();


        /// <summary>
        /// Occurs when system status changed.
        /// </summary>
        public event EventHandler<KrakenMessageEventArgs<Heartbeat>> HeartbeatReceived;

        /// <summary>
        /// Occurs when system status changed.
        /// </summary>
        public event EventHandler<KrakenMessageEventArgs<SystemStatus>> SystemStatusChanged;

        /// <summary>
        /// Occurs when subscription status changed.
        /// </summary>
        public event EventHandler<KrakenMessageEventArgs<SubscriptionStatus>> SubscriptionStatusChanged;

        /// <summary>
        /// Occurs when add order status received.
        /// </summary>
        public event EventHandler<KrakenMessageEventArgs<AddOrderStatusEvent>> AddOrderStatusReceived;

        /// <summary>
        /// The cancel order status received
        /// </summary>
        public EventHandler<KrakenMessageEventArgs<CancelOrderStatusEvent>> CancelOrderStatusReceived;

        /// <summary>
        /// Occurs when a new ticker information was received.
        /// </summary>
        public event EventHandler<KrakenDataEventArgs<TickerMessage>> TickerReceived;

        /// <summary>
        /// Occurs when new ohlc information was received.
        /// </summary>
        public event EventHandler<KrakenDataEventArgs<OhlcMessage>> OhlcReceived;

        /// <summary>
        /// Occurs when new trade information was received.
        /// </summary>
        public event EventHandler<KrakenDataEventArgs<TradeMessage>> TradeReceived;

        /// <summary>
        /// Occurs when new spread information was received.
        /// </summary>
        public event EventHandler<KrakenDataEventArgs<SpreadMessage>> SpreadReceived;

        /// <summary>
        /// Occurs when new book snapshot information was received.
        /// </summary>
        public event EventHandler<KrakenDataEventArgs<BookSnapshotMessage>> BookSnapshotReceived;

        /// <summary>
        /// Occurs when new book update information was received.
        /// </summary>
        public event EventHandler<KrakenDataEventArgs<BookUpdateMessage>> BookUpdateReceived;

        /// <summary>
        /// Occurs when own trades information was received.
        /// </summary>
        public event EventHandler<KrakenPrivateEventArgs<OwnTradesMessage>> OwnTradesReceived;

        /// <summary>
        /// Occurs when open orders information was received.
        /// </summary>
        public event EventHandler<KrakenPrivateEventArgs<OpenOrdersMessage>> OpenOrdersReceived;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Kraken.WebSockets.KrakenApiClient" /> class.
        /// </summary>
        /// <param name="socket">Socket.</param>
        /// <param name="serializer">Serializer.</param>
        /// <exception cref="ArgumentNullException">
        /// socket
        /// or
        /// serializer
        /// </exception>
        internal KrakenApiClient(IKrakenSocket socket, IKrakenMessageSerializer serializer)
        {
            this.socket = socket ?? throw new ArgumentNullException(nameof(socket));
            this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));


            // Add watch for incoming messages 
            this.socket.DataReceived += HandleIncomingMessage;
        }

        /// <summary>
        /// Connects to the websocket endpoint.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task ConnectAsync()
        {
            //logger.LogDebug("Connect to the websocket");
            await socket.ConnectAsync();
        }

        /// <summary>
        /// Creates a subscription.
        /// </summary>
        /// <param name="subscribe">The subscription.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">subscribe</exception>
        public Task SubscribeAsync(Subscribe subscribe)
        {
            if (subscribe == null)
            {
               // logger.LogError("No subscribe options passed to method");
                throw new ArgumentNullException(nameof(subscribe));
            }

            return SubscribeAsyncInternal(subscribe);
        }

        private async Task SubscribeAsyncInternal(Subscribe subscribe)
        {
           // logger.LogTrace("Adding subscription: {subscribe}", subscribe);
            await socket.SendAsync(subscribe);
        }

        /// <summary>
        /// Unsubscribe from a specific subscription.
        /// </summary>
        /// <param name="channelId">The channel identifier.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">subscription</exception>
        public Task UnsubscribeAsync(int channelId)
        {
            if (channelId == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(channelId));
            }

            return UnsubscribeAsyncInternal(channelId);
        }

        private async Task UnsubscribeAsyncInternal(int channelId)
        {
            //logger.LogTrace("Unsubscribe from subscription with channelId '{channelId}'", channelId);
            await socket.SendAsync(new Unsubscribe(channelId));
        }

        /// <summary>
        /// Adds the order.
        /// </summary>
        /// <param name="addOrderCommand">The add order message.</param>
        /// <exception cref="ArgumentNullException">addOrderMessage</exception>
        public Task AddOrder(AddOrderCommand addOrderCommand)
        {
            if (addOrderCommand == null)
            {
                //logger.LogError("No add order command provided");
                throw new ArgumentNullException(nameof(addOrderCommand));
            }

            return AddOrderInternal(addOrderCommand);
        }

        private async Task AddOrderInternal(AddOrderCommand addOrderCommand)
        {
            //logger.LogTrace("Adding new order:{@addOrderCommand}", addOrderCommand);
            await socket.SendAsync(addOrderCommand);
        }

        /// <summary>
        /// Cancels the order.
        /// </summary>
        /// <param name="cancelOrder">The cancel order.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">cancelOrder</exception>
        public Task CancelOrder(CancelOrderCommand cancelOrder)
        {
            if (cancelOrder == null)
            {
                //logger.LogError("No cancelOrder command provided");
                throw new ArgumentNullException(nameof(cancelOrder));
            }

            return CancelOrderInternal(cancelOrder);
        }

        private async Task CancelOrderInternal(CancelOrderCommand cancelOrder)
        {
            //logger.LogTrace("Cancelling existing order: {@cancelOrder}", cancelOrder);
            await socket.SendAsync(cancelOrder);
        }

        #region IDisposable Support

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (Subscriptions.Any())
                    {
                        foreach (var subscription in Subscriptions.Keys)
                        {
                            UnsubscribeAsync(subscription).GetAwaiter().GetResult();
                        }
                    }

                    socket.CloseAsync().GetAwaiter().GetResult();
                }

                disposedValue = true;
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, 
        /// releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }
        #endregion

        #region Private Helper

        private void HandleIncomingMessage(object sender, KrakenMessageEventArgs eventArgs)
        {
            //logger.LogDebug("Handling incoming '{@event}' message", eventArgs.Event);

            switch (eventArgs.Event)
            {
                case Heartbeat.EventName:
                    HandleEvent(eventArgs, HeartbeatReceived);
                    break;

                case SystemStatus.EventName:
                    SystemStatus = HandleEvent(eventArgs, SystemStatusChanged);
                    break;

                case SubscriptionStatus.EventName:
                    SynchronizeSubscriptions(HandleEvent(eventArgs, SubscriptionStatusChanged));
                    break;

                case AddOrderStatusEvent.EventName:
                    HandleEvent(eventArgs, AddOrderStatusReceived);
                    break;

                case CancelOrderStatusEvent.EventName:
                    HandleEvent(eventArgs, CancelOrderStatusReceived);
                    break;

                case "private":
                    HandlePrivateContent(eventArgs);
                    break;

                case "data":
                    var subscription = Subscriptions.ContainsKey(eventArgs.ChannelId.Value) ? Subscriptions[eventArgs.ChannelId.Value] : null;
                    if (subscription == null)
                    {
                       // logger.LogWarning("Didn't find a subscription for channelId {channelId}", eventArgs.ChannelId);
                        break;
                    }

                    HandleData(eventArgs, subscription);
                    break;

                default:
                    //logger.LogWarning("Could not handle incoming message: {message}", eventArgs.RawContent);
                    break;
            }
        }

        private void HandleData(KrakenMessageEventArgs eventArgs, SubscriptionStatus subscription)
        {
            var dataType = subscription.Subscription.Name;

            if (dataType == SubscribeOptionNames.Ticker)
            {
                var tickerMessage = TickerMessage.CreateFromString(eventArgs.RawContent, subscription);
                TickerReceived.InvokeAll(this, new TickerEventArgs(subscription.ChannelId.Value, subscription.Pair, tickerMessage));
            }
            if (dataType == SubscribeOptionNames.OHLC)
            {
                var ohlcMessage = OhlcMessage.CreateFromString(eventArgs.RawContent);
                OhlcReceived.InvokeAll(this, new OhlcEventArgs(subscription.ChannelId.Value, subscription.Pair, ohlcMessage));
            }
            if (dataType == SubscribeOptionNames.Trade)
            {
                var tradeMessage = TradeMessage.CreateFromString(eventArgs.RawContent);
                TradeReceived.InvokeAll(this, new TradeEventArgs(subscription.ChannelId.Value, subscription.Pair, tradeMessage));
            }
            if (dataType == SubscribeOptionNames.Spread)
            {
                var spreadMessage = SpreadMessage.CreateFromString(eventArgs.RawContent);
                SpreadReceived.InvokeAll(this, new SpreadEventArgs(subscription.ChannelId.Value, subscription.Pair, spreadMessage));
            }
            if (dataType == SubscribeOptionNames.Book)
            {
                if (eventArgs.RawContent.Contains(@"""as"":") && eventArgs.RawContent.Contains(@"""bs"":"))
                {
                    var bookSnapshotMessage = BookSnapshotMessage.CreateFromString(eventArgs.RawContent);
                    BookSnapshotReceived.InvokeAll(this, new KrakenDataEventArgs<BookSnapshotMessage>(eventArgs.ChannelId.Value, subscription.Pair, bookSnapshotMessage));
                }
                if (eventArgs.RawContent.Contains(@"""a"":") || eventArgs.RawContent.Contains(@"""b"":"))
                {
                    var bookUpdateMessage = BookUpdateMessage.CreateFromString(eventArgs.RawContent);
                    BookUpdateReceived.InvokeAll(this, new KrakenDataEventArgs<BookUpdateMessage>(eventArgs.ChannelId.Value, subscription.Pair, bookUpdateMessage));
                }
            }
        }

        private void HandlePrivateContent(KrakenMessageEventArgs eventArgs)
        {
            // handle private content
            if (eventArgs.RawContent.Contains(@"""ownTrades"""))
            {
                var ownTrades = OwnTradesMessage.CreateFromString(eventArgs.RawContent);
                OwnTradesReceived.InvokeAll(this, new KrakenPrivateEventArgs<OwnTradesMessage>(ownTrades));
            }

            if (eventArgs.RawContent.Contains(@"""openOrders"""))
            {
                var openOrders = OpenOrdersMessage.CreateFromString(eventArgs.RawContent);
                OpenOrdersReceived.InvokeAll(this, new KrakenPrivateEventArgs<OpenOrdersMessage>(openOrders));
            }
        }

        private TEvent HandleEvent<TEvent>(KrakenMessageEventArgs eventArgs, EventHandler<KrakenMessageEventArgs<TEvent>> eventHandler)
            where TEvent : KrakenMessage, new()
        {
            try
            {
                var eventObject = serializer.Deserialize<TEvent>(eventArgs.RawContent);
                //logger.LogTrace("Event '{eventType}' received: {@event}", eventObject.GetType().Name, eventObject);

                eventHandler.InvokeAll(this, eventObject);
                return eventObject;
            }
            catch (Exception ex)
            {
                //logger.LogError(ex, "Failed to deserialize addOrderStatus: {message}", eventArgs.RawContent);
                return null;
            }
        }

        private void SynchronizeSubscriptions(SubscriptionStatus currentStatus)
        {
            if (currentStatus.ChannelId == null || !currentStatus.ChannelId.HasValue)
            {
                //logger.LogWarning("SubscriptionStatus has no channelID");
                // no channelID --> error?
                return;
            }

            // handle unsubscribe
            var channelIdValue = currentStatus.ChannelId.Value;
            if (currentStatus.Status == "unsubscribed")
            {
                if (!Subscriptions.ContainsKey(channelIdValue)) return;

                Subscriptions.Remove(channelIdValue);
                //logger.LogDebug("Subscription for {channelID} successfully removed", channelIdValue);
                return;
            }

            // handle subscription
            var value = channelIdValue;
            if (Subscriptions.ContainsKey(value))
            {
                Subscriptions[value] = currentStatus;
            }
            else
            {
                Subscriptions.Add(value, currentStatus);
            }
        }

        #endregion
    }
}
