using System;
using Kraken.WebSockets.Messages;
using System.Security;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OsEngine.Market.Servers.Kraken2.Sockets;
using Kraken.WebSockets.Sockets;
using System.Net.WebSockets;
using Kraken.WebSockets;
using OsEngine.Entity;
using Order = Kraken.WebSockets.Messages.Order;

namespace Kraken.WebSockets
{
    public class KrakenApi : IKrakenApi
    {
        private AuthenticationClient authenticationClient;
        private string websocketUri;

        private static readonly Lazy<IKrakenApiClientFactory> factory =
            new Lazy<IKrakenApiClientFactory>(() => new KrakenApiClientFactory(new KrakenMessageSerializer()));

        public IKrakenApi ConfigureAuthentication(string uri, string apiKey, string apiSecret, int version = 0)
        {
            authenticationClient = new AuthenticationClient(uri, apiKey.ToSecureString(), apiSecret.ToSecureString(), version);
            return this;
        }

        public IKrakenApi ConfigureWebsocket(string uri)
        {
            websocketUri = uri;
            return this;
        }

        public IAuthenticationClient AuthenticationClient =>
            authenticationClient ?? throw new InvalidOperationException("Please configure authentication.");

        public IKrakenApiClient BuildClient()
        {
            return factory.Value.Create(websocketUri ?? throw new InvalidOperationException("Please configure websocket"));
        }

        private static IKrakenApiClient clientOne;

        private static IKrakenApiClient clientTwo;

        private static AuthToken _token;

        public static async Task RunKraken(IKrakenApiClient c, AuthToken token)
        {
            if(clientOne == null)
            {
                clientOne = c;
                c.HeartbeatReceived += Client_HeartbeatReceived;
                c.SystemStatusChanged += Client_SystemStatusChanged;
                c.SubscriptionStatusChanged += Client_SubscriptionStatusChanged;
                c.TickerReceived += Client_TickerReceived;
                c.OhlcReceived += Client_OhlcReceived;
                c.TradeReceived += Client_TradeReceived;
                c.SpreadReceived += Client_SpreadReceived;
                c.BookSnapshotReceived += Client_BookSnapshotReceived;
                c.BookUpdateReceived += Client_BookUpdateReceived;
                c.OwnTradesReceived += Client_OwnTradesReceived;
                c.OpenOrdersReceived += Client_OpenOrdersReceived;

                await c.ConnectAsync();
            }
            else
            {
                clientTwo = c;
                _token = token;
                c.HeartbeatReceived += Client_HeartbeatReceived;
                c.SystemStatusChanged += Client_SystemStatusChanged;
                c.SubscriptionStatusChanged += Client_SubscriptionStatusChanged;
                c.TickerReceived += Client_TickerReceived;
                c.OhlcReceived += Client_OhlcReceived;
                c.TradeReceived += Client_TradeReceived;
                c.SpreadReceived += Client_SpreadReceived;
                c.BookSnapshotReceived += Client_BookSnapshotReceived;
                c.BookUpdateReceived += Client_BookUpdateReceived;
                c.OwnTradesReceived += Client_OwnTradesReceived;
                c.OpenOrdersReceived += Client_OpenOrdersReceived;

                await c.ConnectAsync();
                await clientTwo.SubscribeAsync(new Subscribe(null, new SubscribeOptions(SubscribeOptionNames.OwnTrades, token.Token)));
                await clientTwo.SubscribeAsync(new Subscribe(null, new SubscribeOptions(SubscribeOptionNames.OpenOrders, token.Token)));
            }
        }

        public static List<Sec> Securities = new List<Sec>();

        public void Subscrible(string socketName, string nameInRest)
        {
            Sec newSecurity = new Sec();
            newSecurity.NameInRest = nameInRest;
            newSecurity.NameInSocket = socketName;
            Securities.Add(newSecurity);

            clientOne.SubscribeAsync(new Subscribe(new[] { socketName }, new SubscribeOptions(SubscribeOptionNames.All)));
            //clientOne.SubscribeAsync(new Subscribe(new[] { securityName }, new SubscribeOptions(SubscribeOptionNames.All)));
        }

        private static List<MarketDepth> _marketDepths = new List<MarketDepth>();
        
        private static void Client_BookUpdateReceived(object sender, Events.KrakenDataEventArgs<BookUpdateMessage> e)
        {
            string pair = e.Pair.ToString();

            Sec security = Securities.Find(sec => sec.NameInSocket == pair);

            MarketDepth depth = _marketDepths.Find(d => d.SecurityNameCode == security.NameInRest);

            if (depth == null)
            {
                depth = new MarketDepth();
                depth.SecurityNameCode = security.NameInRest;
                _marketDepths.Add(depth);
            }

            for (int i = 0; e.DataMessage.Asks != null && i < e.DataMessage.Asks.Length;i++)
            {
                OsEngine.Entity.MarketDepthLevel ask = new OsEngine.Entity.MarketDepthLevel();
                ask.Price = e.DataMessage.Asks[i].Price;
                ask.Ask = e.DataMessage.Asks[i].Volume;

                for(int i2 = 0;i2 < depth.Asks.Count;i2++)
                {
                    if(depth.Asks[i2].Price == ask.Price)
                    {
                        depth.Asks.RemoveAt(i2);
                        break;
                    }
                }

                if(ask.Ask != 0)
                {
                    depth.Asks.Add(ask);
                }
                
                depth.Time = new DateTime(1970, 1, 1).AddSeconds(Convert.ToDouble(e.DataMessage.Asks[i].Timestamp));
            }

            for (int i = 0; e.DataMessage.Bids != null && i < e.DataMessage.Bids.Length; i++)
            {
                OsEngine.Entity.MarketDepthLevel bid = new OsEngine.Entity.MarketDepthLevel();
                bid.Price = e.DataMessage.Bids[i].Price;
                bid.Bid = e.DataMessage.Bids[i].Volume;

                for (int i2 = 0; i2 < depth.Bids.Count; i2++)
                {
                    if (depth.Bids[i2].Price == bid.Price)
                    {
                        depth.Bids.RemoveAt(i2);
                        break;
                    }
                }

                if (bid.Bid != 0)
                {
                    depth.Bids.Add(bid);
                }

                depth.Time = new DateTime(1970, 1, 1).AddSeconds(Convert.ToDouble(e.DataMessage.Bids[i].Timestamp));
            }

            // 1 Теперь сортируем биды и аски

            for(int i = 0;i < depth.Asks.Count;i++)
            {
                for(int i2 = 0;i2< depth.Asks.Count-1;i2++)
                {
                    if(depth.Asks[i2].Price > depth.Asks[i2+1].Price)
                    {
                        MarketDepthLevel level = depth.Asks[i2];
                        depth.Asks[i2] = depth.Asks[i2+1];
                        depth.Asks[i2 + 1] = level;
                    }
                }
            }


            for (int i = 0; i < depth.Bids.Count; i++)
            {
                for (int i2 = 0; i2 < depth.Bids.Count - 1; i2++)
                {
                    if (depth.Bids[i2].Price < depth.Bids[i2 + 1].Price)
                    {
                        MarketDepthLevel level = depth.Bids[i2];
                        depth.Bids[i2] = depth.Bids[i2 + 1];
                        depth.Bids[i2 + 1] = level;
                    }
                }
            }

            // 2 Теперь удаляем перехлёсты


            while(depth.Bids.Count > 0 &&
                depth.Asks.Count > 0 &&
                depth.Bids[0].Price > depth.Asks[0].Price)
            {
                depth.Asks.RemoveAt(0);
            }

            // !!!!!!!!!!!!!!!!!!!!!!


            // высылаем копию

            if (MarketDepthUpdateEvent != null)
            {
                MarketDepthUpdateEvent(depth.GetCopy());
            }

        }

        private static void Client_BookSnapshotReceived(object sender, Events.KrakenDataEventArgs<BookSnapshotMessage> e)
        {
            string pair = e.Pair.ToString();

            Sec security = Securities.Find(sec => sec.NameInSocket == pair);

            MarketDepth depth = _marketDepths.Find(d => d.SecurityNameCode == security.NameInRest);

            if (depth == null)
            {
                depth = new MarketDepth();
                depth.SecurityNameCode = security.NameInRest;
                _marketDepths.Add(depth);
            }

            for (int i = 0; e.DataMessage.Asks != null && i < e.DataMessage.Asks.Length; i++)
            {
                OsEngine.Entity.MarketDepthLevel ask = new OsEngine.Entity.MarketDepthLevel();
                ask.Price = e.DataMessage.Asks[i].Price;
                ask.Ask = e.DataMessage.Asks[i].Volume;

                for (int i2 = 0; i2 < depth.Asks.Count; i2++)
                {
                    if (depth.Asks[i2].Price == ask.Price)
                    {
                        depth.Asks.RemoveAt(i2);
                        break;
                    }
                }

                if (ask.Ask != 0)
                {
                    depth.Asks.Add(ask);
                }

                depth.Time = new DateTime(1970, 1, 1).AddSeconds(Convert.ToDouble(e.DataMessage.Asks[i].Timestamp));
            }

            for (int i = 0; e.DataMessage.Bids != null && i < e.DataMessage.Bids.Length; i++)
            {
                OsEngine.Entity.MarketDepthLevel bid = new OsEngine.Entity.MarketDepthLevel();
                bid.Price = e.DataMessage.Bids[i].Price;
                bid.Bid = e.DataMessage.Bids[i].Volume;

                for (int i2 = 0; i2 < depth.Bids.Count; i2++)
                {
                    if (depth.Bids[i2].Price == bid.Price)
                    {
                        depth.Bids.RemoveAt(i2);
                        break;
                    }
                }

                if (bid.Bid != 0)
                {
                    depth.Bids.Add(bid);
                }

                depth.Time = new DateTime(1970, 1, 1).AddSeconds(Convert.ToDouble(e.DataMessage.Bids[i].Timestamp));
            }

            // 1 Теперь сортируем биды и аски

            for (int i = 0; i < depth.Asks.Count; i++)
            {
                for (int i2 = 0; i2 < depth.Asks.Count - 1; i2++)
                {
                    if (depth.Asks[i2].Price > depth.Asks[i2 + 1].Price)
                    {
                        MarketDepthLevel level = depth.Asks[i2];
                        depth.Asks[i2] = depth.Asks[i2 + 1];
                        depth.Asks[i2 + 1] = level;
                    }
                }
            }


            for (int i = 0; i < depth.Bids.Count; i++)
            {
                for (int i2 = 0; i2 < depth.Bids.Count - 1; i2++)
                {
                    if (depth.Bids[i2].Price < depth.Bids[i2 + 1].Price)
                    {
                        MarketDepthLevel level = depth.Bids[i2];
                        depth.Bids[i2] = depth.Bids[i2 + 1];
                        depth.Bids[i2 + 1] = level;
                    }
                }
            }

            // 2 Теперь удаляем перехлёсты


            while (depth.Bids.Count > 0 &&
                depth.Asks.Count > 0 &&
                depth.Bids[0].Price > depth.Asks[0].Price)
            {
                depth.Asks.RemoveAt(0);
            }

            // !!!!!!!!!!!!!!!!!!!!!!


            // высылаем копию

            if (MarketDepthUpdateEvent != null)
            {
                MarketDepthUpdateEvent(depth.GetCopy());
            }
        }

        private static void Client_SpreadReceived(object sender, Events.KrakenDataEventArgs<SpreadMessage> e)
        {
            // += (sender, e) => Console.WriteLine($"Spread received");
            return;

            string pair = e.Pair.ToString();

            Sec security = Securities.Find(sec => sec.NameInSocket == pair);
            DateTime time = new DateTime(1970, 1, 1).AddSeconds(Convert.ToDouble(e.DataMessage.Time));

            OsEngine.Entity.MarketDepth depth = new OsEngine.Entity.MarketDepth();
            depth.Time = time;
            depth.SecurityNameCode = security.NameInRest;

            OsEngine.Entity.MarketDepthLevel ask = new OsEngine.Entity.MarketDepthLevel();
            ask.Price = e.DataMessage.Ask;
            ask.Ask = 1;

            OsEngine.Entity.MarketDepthLevel bid = new OsEngine.Entity.MarketDepthLevel();
            bid.Price = e.DataMessage.Bid;
            bid.Bid = 1;

            depth.Asks.Add(ask);
            depth.Bids.Add(bid);

            if(MarketDepthUpdateEvent != null)
            {
                MarketDepthUpdateEvent(depth);
            }

        }

        private static void Client_TradeReceived(object sender, Events.KrakenDataEventArgs<TradeMessage> e)
        {
            string pair = e.Pair.ToString();

            Sec security = Securities.Find(sec => sec.NameInSocket == pair);

            for(int i = 0;i < e.DataMessage.Trades.Length;i++)
            {
                OsEngine.Entity.Trade trade = new OsEngine.Entity.Trade();
                trade.SecurityNameCode = security.NameInRest;
                trade.Price = e.DataMessage.Trades[i].Price;
                trade.Volume = e.DataMessage.Trades[i].Volume;
                trade.Time = new DateTime(1970, 1, 1).AddSeconds(Convert.ToDouble(e.DataMessage.Trades[i].Time));

                if (e.DataMessage.Trades[i].Side.ToLower() == "s")
                {
                    trade.Side = OsEngine.Entity.Side.Sell;
                }

                if(TradeUpdateEvent != null)
                {
                    TradeUpdateEvent(trade);
                }

            }
            //= (sender, e) => Console.WriteLine($"Trade received");
        }

        private static void Client_OhlcReceived(object sender, Events.KrakenDataEventArgs<OhlcMessage> e)
        {
            //(sender, e) => Console.WriteLine($"Ohlc received");
        }

        private static void Client_TickerReceived(object sender, Events.KrakenDataEventArgs<TickerMessage> e)
        {
            //= (sender, e) => Console.WriteLine($"Ticker received");
        }

        private static void Client_SubscriptionStatusChanged(object sender, Events.KrakenMessageEventArgs<SubscriptionStatus> e)
        {
            string message = e.Message.Status.ToString() + e.Message.Subscription.Name.ToString();
            if(e.Message.Pair != null)
            {
                message += e.Message.Pair.ToString();
            }
            if (e.Message.ErrorMessage != null)
            {
                message += e.Message.ErrorMessage.ToString();
            }

            SendLogMessage(message, 
                OsEngine.Logging.LogMessageType.Connect);

            // = (sender, e) => 
            // Console.WriteLine($"Subscription status changed: status={e.Message.Status}, pair={e.Message.Pair}, channelId={e.Message.ChannelId}, error={e.Message.ErrorMessage}, subscription.name={e.Message.Subscription.Name}");
        }

        private static void Client_SystemStatusChanged(object sender, Events.KrakenMessageEventArgs<SystemStatus> e)
        {
            SendLogMessage(e.Message.Status.ToString(), OsEngine.Logging.LogMessageType.Connect);
            //(sender, e) => Console.WriteLine($"System status changed: status={e.Message.Status}");
        }

        private static void Client_HeartbeatReceived(object sender, Events.KrakenMessageEventArgs<Heartbeat> e)
        {
  
        }

        public event Action<OsEngine.Entity.Order> NewOrderEvent;

        public event Action<OsEngine.Entity.MyTrade> MyTradeEvent;

        public event Action Connect;

        public event Action Disconnect;

        public static event Action<OsEngine.Entity.MarketDepth> MarketDepthUpdateEvent;

        public static event Action<OsEngine.Entity.Trade> TradeUpdateEvent;

        public static event Action<OsEngine.Entity.Order> OrdersUpdateEvent;

        public static event Action<OsEngine.Entity.MyTrade> MyTradeUpdateEvent;

        /// <summary>
        /// Adds the order.
        /// </summary>
        /// <param name="addOrderCommand">The add order message.</param>
        /// <returns></returns>
        public static void AddOrder(OsEngine.Entity.Order order)
        {
            Side type = Side.Buy;

            if(order.Side == OsEngine.Entity.Side.Buy)
            {
                type = Side.Buy;
            }
            if (order.Side == OsEngine.Entity.Side.Sell)
            {
                type = Side.Sell;
            }

            decimal volume = order.Volume;

            Sec security = Securities.Find(s => s.NameInRest == order.SecurityNameCode);

            AddOrderCommand newCommand = new AddOrderCommand(_token.Token,OrderType.Limit, type, security.NameInSocket, volume);

            newCommand.Price = order.Price;
            newCommand.Userref = order.NumberUser.ToString();

            clientTwo.AddOrder(newCommand);
        }

        /// <summary>
        /// Cancels the order.
        /// </summary>
        /// <param name="cancelOrder">The cancel order.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">cancelOrder</exception>
        public static void CancelOrder(OsEngine.Entity.Order order)
        {
            List<string> transactions = new List<string>();
            transactions.Add(order.NumberMarket);

            CancelOrderCommand canselOrder = new CancelOrderCommand(_token.Token, transactions);

            clientTwo.CancelOrder(canselOrder);
        }

        private static List<OrdersRef> _ordersRefs = new List<OrdersRef>();

        private static void Client_OpenOrdersReceived(object sender, Events.KrakenPrivateEventArgs<OpenOrdersMessage> e)
        {
            //OrdersUpdateEvent

            List<Order> orders = e.PrivateMessage.Orders.ToList();

            for (int i = 0; i < orders.Count; i++)
            {
                Order o = orders[i];

                int numberUser = 0;

                if (o.UserRef == 0)
                {
                    OrdersRef myRef = _ordersRefs.Find(ord => ord.MarketRef == o.OrderId);
                    if(myRef == null)
                    {
                        continue;
                    }
                    else
                    {
                        numberUser = myRef.UserRef;
                    }
                }
                else if(_ordersRefs.Find(ord => ord.UserRef == o.UserRef) == null)
                {
                    OrdersRef newRef = new OrdersRef();
                    newRef.MarketRef = o.OrderId;
                    newRef.UserRef = Convert.ToInt32(o.UserRef);
                    _ordersRefs.Add(newRef);

                    numberUser = Convert.ToInt32(o.UserRef);
                }

                OsEngine.Entity.Order orderOsEngine = new OsEngine.Entity.Order();

                orderOsEngine.NumberMarket = o.OrderId;
                orderOsEngine.NumberUser = numberUser;

                DateTime time = new DateTime(1970, 1, 1).AddSeconds(Convert.ToDouble(o.OpenTimestamp));

                orderOsEngine.TimeCallBack = time;

                if (o.Status == "canceled" ||
                    o.Status == "expired")
                {
                    orderOsEngine.State = OsEngine.Entity.OrderStateType.Cancel;
                }

                else if (o.Status == "closed" ||
                    o.Status == "canceled" ||
                    o.Status == "expired")
                {
                    orderOsEngine.State = OsEngine.Entity.OrderStateType.Done;
                }

                else
                {
                    orderOsEngine.State = OsEngine.Entity.OrderStateType.Activ;
                }

                if (OrdersUpdateEvent != null)
                {
                    OrdersUpdateEvent(orderOsEngine);
                }
            }


            // = (sender, e) => Console.WriteLine($"OpenOrders received");
        }

        private static void Client_OwnTradesReceived(object sender, Events.KrakenPrivateEventArgs<OwnTradesMessage> e)
        {

            List<TradeObject> trades = e.PrivateMessage.Trades;

            for (int i = 0; i < trades.Count; i++)
            {
                TradeObject t = trades[i];

                OsEngine.Entity.MyTrade trade = new OsEngine.Entity.MyTrade();
                trade.NumberOrderParent = t.OrderTxId;
                trade.Price = t.Price;
                trade.NumberTrade = t.TradeId;
                string pair = t.Pair.ToString();

                Sec security = Securities.Find(sec => sec.NameInSocket == pair);

                if (security == null)
                {
                    return;
                }

                trade.SecurityNameCode = security.NameInRest;
                trade.Volume = t.Volume;
                DateTime time = new DateTime(1970, 1, 1).AddSeconds(Convert.ToDouble(t.Time));
                trade.Time = time;

                if (t.Type == "sell")
                {
                    trade.Side = OsEngine.Entity.Side.Sell;
                }
                else
                {
                    trade.Side = OsEngine.Entity.Side.Sell;
                }

                if (MyTradeUpdateEvent != null)
                {
                    MyTradeUpdateEvent(trade);
                }
            }


            //
            // = (sender, e) => Console.WriteLine($"OwnTrades received");
        }

        /// <summary>
        /// add a new log message
        /// добавить в лог новое сообщение
        /// </summary>
        private static void SendLogMessage(string message, OsEngine.Logging.LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        public static event Action<string, OsEngine.Logging.LogMessageType> LogMessageEvent;

        public class Sec
        {
            public string NameInRest;

            public string NameInSocket;

        }
    }

    public sealed class AuthenticationClient : IAuthenticationClient
    {
        private readonly string uri;
        private readonly SecureString apiKey;
        private readonly SecureString apiSecret;
        private readonly int version;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticationClient" /> class.
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <param name="apiKey">The API key.</param>
        /// <param name="apiSecret">The API secret.</param>
        /// <param name="version">The version. Default Value = 0</param>
        public AuthenticationClient(string uri, SecureString apiKey, SecureString apiSecret, int version = 0)
        {
            this.uri = uri;
            this.apiKey = apiKey;
            this.apiSecret = apiSecret;
            this.version = version;
        }

        /// <summary>
        /// Requests the websocket token from the configured REST API endpoint
        /// </summary>
        /// <returns></returns>
        public async Task<AuthToken> GetWebsocketToken()
        {
            var formContent = new Dictionary<string, string>();

            // generate a 64 bit nonce using a timestamp at tick resolution
            var nonce = DateTime.Now.Ticks;
            formContent.Add("nonce", nonce.ToString());

            var path = $"/{version}/private/GetWebSocketsToken";
            var address = $"{uri}{path}";

            var content = new FormUrlEncodedContent(formContent);
            var request = new HttpRequestMessage(HttpMethod.Post, address)
            {
                Content = content,
                Headers =
                {
                    {"API-Key", apiKey.ToPlainString()},
                    {"API-Sign", Convert.ToBase64String(CalculateSignature(await content.ReadAsByteArrayAsync(), nonce, path)) }
                }
            };

            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.SendAsync(request);
                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    using (var jsonReader = new JsonTextReader(new StreamReader(stream)))
                    {
                        var jObject = new JsonSerializer().Deserialize<JObject>(jsonReader);
                        return jObject.Property("result").Value.ToObject<AuthToken>();
                    }
                }
            }
        }

        private byte[] CalculateSignature(byte[] props, long nonce, string path)
        {
            var decodedSecret = Convert.FromBase64String(apiSecret.ToPlainString());

            var np = Encoding.UTF8.GetBytes((nonce + Convert.ToChar(0)).ToString()).Concat(props).ToArray();
            var hash256Bytes = SHA256Hash(np);

            var pathBytes = Encoding.UTF8.GetBytes(path);

            var z = pathBytes.Concat(hash256Bytes).ToArray();

            var signature = getHash(decodedSecret, z);

            return signature;
        }

        private byte[] SHA256Hash(byte[] value)
        {
            using (var hash = SHA256.Create())
            {
                return hash.ComputeHash(value);
            }
        }

        private byte[] getHash(byte[] keyByte, byte[] messageBytes)
        {
            using (var hmacsha512 = new HMACSHA512(keyByte))
            {
                var result = hmacsha512.ComputeHash(messageBytes);
                return result;
            }
        }
    }

    public interface IKrakenApiClientFactory
    {
        /// <summary>
        /// Creates a new <see cref="IKrakenApiClient"/> instance connected to the specified URI.
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <returns></returns>
        IKrakenApiClient Create(string uri);
    }

    internal class KrakenApiClientFactory : IKrakenApiClientFactory
    {
        private readonly IKrakenMessageSerializer serializer;

        /// <summary>
        /// Initializes a new instance of the <see cref="KrakenApiClientFactory"/> class.
        /// </summary>
        /// <param name="serializer">The serializer.</param>
        public KrakenApiClientFactory(IKrakenMessageSerializer serializer)
        {
            this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        /// <summary>
        /// Creates a new <see cref="IKrakenApiClient" /> instance connected to the specified URI.
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException">uri</exception>
        public IKrakenApiClient Create(string uri)
        {
            if (uri == string.Empty)
            {
                throw new ArgumentOutOfRangeException(nameof(uri));
            }

            var socket = new KrakenSocket(uri, serializer, new DefaultWebSocket(new ClientWebSocket()));
            return new KrakenApiClient(socket, serializer);
        }
    }

    public class OrdersRef
    {
        public int UserRef;

        public string MarketRef;
    }
}