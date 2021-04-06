using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kraken.WebSockets.Messages;
using System.Diagnostics.Contracts;
using Kraken.WebSockets.Events;


namespace Kraken.WebSockets
{
    public interface IKrakenApi
    {
        IAuthenticationClient AuthenticationClient { get; }

        IKrakenApi ConfigureAuthentication(string uri, string apiKey, string apiSecret, int version = 0);

        IKrakenApi ConfigureWebsocket(string uri);

        IKrakenApiClient BuildClient();
    }

    public interface IAuthenticationClient
    {
        Task<AuthToken> GetWebsocketToken();
    }

    public class AuthToken
    {
        public string Token { get; set; }

        public decimal? Expires { get; set; }
    }

    /// <summary>
    /// This interface describes the client for the Kraken WebSocket API
    /// </summary>
    public interface IKrakenApiClient : IDisposable
    {
        /// <summary>
        /// Gets the system status.
        /// </summary>
        /// <value>The system status.</value>
        SystemStatus SystemStatus { get; }

        /// <summary>
        /// Gets the subscriptions.
        /// </summary>
        /// <value>The subscriptions.</value>
        IDictionary<int, SubscriptionStatus> Subscriptions { get; }

        /// <summary>
        /// Occurs when system status changed.
        /// </summary>
        event EventHandler<KrakenMessageEventArgs<Heartbeat>> HeartbeatReceived;

        /// <summary>
        /// Occurs when system status changed.
        /// </summary>
        event EventHandler<KrakenMessageEventArgs<SystemStatus>> SystemStatusChanged;

        /// <summary>
        /// Occurs when subscription status changed.
        /// </summary>
        event EventHandler<KrakenMessageEventArgs<SubscriptionStatus>> SubscriptionStatusChanged;

        /// <summary>
        /// Occurs when a new ticker information was received.
        /// </summary>
        event EventHandler<KrakenDataEventArgs<TickerMessage>> TickerReceived;

        /// <summary>
        /// Occurs when new ohlc information was received.
        /// </summary>
        event EventHandler<KrakenDataEventArgs<OhlcMessage>> OhlcReceived;

        /// <summary>
        /// Occurs when new trade information was received.
        /// </summary>
        event EventHandler<KrakenDataEventArgs<TradeMessage>> TradeReceived;

        /// <summary>
        /// Occurs when new spread information was received.
        /// </summary>
        event EventHandler<KrakenDataEventArgs<SpreadMessage>> SpreadReceived;

        /// <summary>
        /// Occurs when new book snapshot information was received.
        /// </summary>
        event EventHandler<KrakenDataEventArgs<BookSnapshotMessage>> BookSnapshotReceived;

        /// <summary>
        /// Occurs when new book update information was received.
        /// </summary>
        event EventHandler<KrakenDataEventArgs<BookUpdateMessage>> BookUpdateReceived;

        /// <summary>
        /// Occurs when own trades information was received.
        /// </summary>
        event EventHandler<KrakenPrivateEventArgs<OwnTradesMessage>> OwnTradesReceived;

        /// <summary>
        /// Occurs when open orders information was received.
        /// </summary>
        event EventHandler<KrakenPrivateEventArgs<OpenOrdersMessage>> OpenOrdersReceived;

        /// <summary>
        /// Connects to the websocket endpoint.
        /// </summary>
        /// <returns></returns>
        Task ConnectAsync();

        /// <summary>
        /// Creates a subscription.
        /// </summary>
        /// <param name="subscribe">The subscription.</param>
        /// <returns></returns>
        Task SubscribeAsync(Subscribe subscribe);

        /// <summary>
        /// Unsubscribe from a specific subscription.
        /// </summary>
        /// <param name="channelId">The channel identifier.</param>
        /// <returns></returns>
        Task UnsubscribeAsync(int channelId);

        /// <summary>
        /// Adds the order.
        /// </summary>
        /// <param name="addOrderCommand">The add order message.</param>
        /// <returns></returns>
        Task AddOrder(AddOrderCommand addOrderCommand);

        /// <summary>
        /// Cancels the order.
        /// </summary>
        /// <param name="cancelOrder">The cancel order.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">cancelOrder</exception>
        Task CancelOrder(CancelOrderCommand cancelOrder);
    }
}