using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Bybit.Entities;
using OsEngine.Market.Servers.Bybit.EntityCreators;
using OsEngine.Market.Servers.Bybit.Utilities;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.Bybit
{
    public class BybitServer : AServer
    {
        public BybitServer()
        {
            BybitServerRealization realization = new BybitServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");
            CreateParameterEnum("Futures Type", "USDT Perpetual", new List<string> { "USDT Perpetual", "Inverse Perpetual" });
            CreateParameterEnum("Net Type", "MainNet", new List<string> { "MainNet", "TestNet" });
        }

        /// <summary>
        /// instrument history query
        /// запрос истории по инструменту
        /// </summary>
        public List<Candle> GetCandleHistory(string nameSec, TimeSpan tf)
        {
            return ((BybitServerRealization)ServerRealization).GetCandleHistory(nameSec, tf);
        }
    }

    public class BybitServerRealization : AServerRealization
    {
        #region Properties
        public override ServerType ServerType => ServerType.Bybit;
        #endregion

        #region Fields
        private string public_key;
        private string secret_key;

        private Client client;
        private WsSource ws_source_public;
        private WsSource ws_source_private;

        private DateTime last_time_update_socket;

        private string futures_type = "USDT Perpetual";
        private string net_type = "MainNet";

        private readonly Dictionary<int, string> supported_intervals;
        public List<Portfolio> Portfolios;
        private CancellationTokenSource cancel_token_source;
        private readonly ConcurrentQueue<string> queue_messages_received_from_fxchange;
        private readonly Dictionary<string, Action<JToken>> response_handlers;
        private object locker = new object();
        private object locker_candles = new object();
        private BybitMarketDepthCreator market_mepth_creator;
        #endregion

        #region Constructor
        public BybitServerRealization() : base()
        {
            queue_messages_received_from_fxchange = new ConcurrentQueue<string>();

            supported_intervals = CreateIntervalDictionary();
            ServerStatus = ServerConnectStatus.Disconnect;

            response_handlers = new Dictionary<string, Action<JToken>>();
            response_handlers.Add("auth", HandleAuthMessage);
            response_handlers.Add("ping", HandlePingMessage);
            response_handlers.Add("subscribe", HandleSubscribeMessage);
            response_handlers.Add("orderBookL2_25", HandleorderBookL2_25Message);
            response_handlers.Add("trade", HandleTradesMessage);
            response_handlers.Add("execution", HandleMyTradeMessage);
            response_handlers.Add("order", HandleOrderMessage);
        }
        #endregion

        #region Service
        private Dictionary<int, string> CreateIntervalDictionary()
        {
            var dictionary = new Dictionary<int, string>();

            dictionary.Add(1, "1");
            dictionary.Add(3, "3");
            dictionary.Add(5, "5");
            dictionary.Add(15, "15");
            dictionary.Add(30, "30");
            dictionary.Add(60, "60");
            dictionary.Add(120, "120");
            dictionary.Add(240, "240");
            dictionary.Add(360, "360");
            dictionary.Add(720, "720");
            dictionary.Add(1440, "D");

            return dictionary;
        }
        #endregion

        public override void Connect()
        {
            public_key = ((ServerParameterString)ServerParameters[0]).Value;
            secret_key = ((ServerParameterPassword)ServerParameters[1]).Value;

            futures_type = ((ServerParameterEnum)ServerParameters[2]).Value;
            net_type = ((ServerParameterEnum)ServerParameters[3]).Value;

            if (futures_type != "Inverse Perpetual" && net_type != "TestNet")
            {
                client = new Client(public_key, secret_key, false, true);
            }

            else if (futures_type != "Inverse Perpetual" && net_type == "TestNet")
            {
                client = new Client(public_key, secret_key, false, false);
            }

            else if (futures_type == "Inverse Perpetual" && net_type != "TestNet")
            {
                client = new Client(public_key, secret_key, true, true);
            }

            else if (futures_type == "Inverse Perpetual" && net_type == "TestNet")
            {
                client = new Client(public_key, secret_key, true, false);
            }

            
            cancel_token_source = new CancellationTokenSource();
            market_mepth_creator = new BybitMarketDepthCreator();

            StartMessageReader();

            ws_source_private = new WsSource(client.WsPrivateUrl);
            ws_source_private.MessageEvent += WsSourceOnMessageEvent;
            ws_source_private.Start();

            ws_source_public = new WsSource(client.WsPublicUrl);
            ws_source_public.MessageEvent += WsSourceOnMessageEvent;
            ws_source_public.Start();
        }

        private void WsSourceOnMessageEvent(WsMessageType message_type, string message)
        {
            switch (message_type)
            {
                case WsMessageType.Opened:
                    SendLoginMessage();
                    OnConnectEvent();
                    StartPortfolioRequester();
                    break;
                case WsMessageType.Closed:
                    OnDisconnectEvent();
                    break;
                case WsMessageType.StringData:
                    queue_messages_received_from_fxchange.Enqueue(message);
                    break;
                case WsMessageType.Error:
                    SendLogMessage(message, LogMessageType.Error);
                    break;
                default:
                    throw new NotSupportedException(message);
            }
        }

        private void StartMessageReader()
        {
            Task.Run(() => MessageReader(cancel_token_source.Token), cancel_token_source.Token);
            Task.Run(() => SourceAliveCheckerThread(cancel_token_source.Token), cancel_token_source.Token);
        }

        private void StartPortfolioRequester()
        {
            Task.Run(() => PortfolioRequester(cancel_token_source.Token), cancel_token_source.Token);
        }

        private async void MessageReader(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!queue_messages_received_from_fxchange.IsEmpty && queue_messages_received_from_fxchange.TryDequeue(out string mes))
                    {
                        JToken response = JToken.Parse(mes);

                        if (response.First.Path == "success")
                        {

                            bool is_success = response.SelectToken("success").Value<bool>();

                            if (is_success)
                            {
                                string type = JToken.Parse(response.SelectToken("request").ToString()).SelectToken("op").Value<string>();

                                if (response_handlers.ContainsKey(type))
                                {
                                    response_handlers[type].Invoke(response);
                                }
                                else
                                {
                                    SendLogMessage(mes, LogMessageType.System);
                                }
                            }
                            else if (!is_success)
                            {
                                string type = JToken.Parse(response.SelectToken("request").ToString()).SelectToken("op").Value<string>();
                                string error_mssage = response.SelectToken("ret_msg").Value<string>();

                                if (type == "subscribe" && error_mssage.Contains("already"))
                                    continue;


                                SendLogMessage("Broken response success marker " + type,  LogMessageType.Error);
                            }
                        }

                        else if (response.First.Path == "topic") //orderBookL2_25.BTCUSD
                        {
                            string type = response.SelectToken("topic").Value<string>().Split('.')[0];

                            if (response_handlers.ContainsKey(type))
                            {
                                response_handlers[type].Invoke(response);
                            }
                            else
                            {
                                SendLogMessage(mes, LogMessageType.System);
                            }
                        }

                        else
                        {
                            SendLogMessage("Broken response topic marker " + response.First.Path,  LogMessageType.Error);
                        }
                    }
                    else
                    {
                        await Task.Delay(20);
                    }
                }
                catch (TaskCanceledException)
                {
                    return;
                }
                catch (Exception exception)
                {
                    SendLogMessage("MessageReader error: " + exception, LogMessageType.Error);
                }
            }
        }

        private async void SourceAliveCheckerThread(CancellationToken token)
        {
            var ping_message = BybitWsRequestBuilder.GetPingRequest();

            while (!token.IsCancellationRequested)
            {
                await Task.Delay(15000);

                ws_source_public?.SendMessage(ping_message);

                if (last_time_update_socket == DateTime.MinValue)
                {
                    continue;
                }
                if (last_time_update_socket.AddSeconds(60) < DateTime.Now)
                {
                    SendLogMessage("The websocket is disabled. Restart", LogMessageType.Error);
                    OnDisconnectEvent();
                    return;
                }
            }
        }


        #region Запросы Rest

        private async void PortfolioRequester(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(1000, token);

                    GetPortfolios();
                }
                catch (TaskCanceledException)
                {
                    return;
                }
                catch (Exception exception)
                {
                    SendLogMessage("Portfolio creator error: " + exception, LogMessageType.Error);
                }
            }
        }

        public override void GetPortfolios()
        {
            List<Portfolio> portfolios = new List<Portfolio>();

            Dictionary<string, string> parameters = new Dictionary<string, string>();

            parameters.Add("api_key", client.ApiKey);

            JToken account_response = BybitRestRequestBuilder.CreatePrivateGetQuery(client, "/v2/private/wallet/balance", parameters );

            string isSuccessfull = account_response.SelectToken("ret_msg").Value<string>();

            if (isSuccessfull == "OK")
            {
                portfolios.Add(BybitPortfolioCreator.Create(account_response.SelectToken("result"), "BybitPortfolio"));
            }
            else
            {
                SendLogMessage($"Can not get portfolios info.", LogMessageType.Error);
                portfolios.Add(BybitPortfolioCreator.Create("undefined"));
            }

            OnPortfolioEvent(portfolios);
        } // both futures

        public override void GetSecurities() // both futures
        {
            JToken account_response = BybitRestRequestBuilder.CreatePublicGetQuery(client, "/v2/public/symbols");

            string isSuccessfull = account_response.SelectToken("ret_msg").Value<string>();

            if (isSuccessfull == "OK")
            {
                OnSecurityEvent(BybitSecurityCreator.Create(account_response.SelectToken("result"), futures_type));
            }
            else
            {
                SendLogMessage($"Can not get securities.", LogMessageType.Error);
            }
        }

        public override void SendOrder(Order order)
        {
            if (order.TypeOrder == OrderPriceType.Iceberg)
            {
                SendLogMessage("Bybit does't support iceberg orders", LogMessageType.Error);
                return;
            }

            string side = "Buy";
            if (order.Side == Side.Sell)
                side = "Sell";

            string type = "Limit";
            if (order.TypeOrder == OrderPriceType.Market)
                type = "Market";

            string reduce = "false";
            if (order.PositionConditionType == OrderPositionConditionType.Close)
            {
                reduce = "true";
            }

            Dictionary<string, string> parameters = new Dictionary<string, string>();

            parameters.Add("api_key", client.ApiKey);
            parameters.Add("side", side);
            parameters.Add("order_type", type);
            parameters.Add("qty", order.Volume.ToString().Replace(",", "."));
            parameters.Add("time_in_force", "GoodTillCancel");
            parameters.Add("order_link_id", order.NumberUser.ToString());
            parameters.Add("symbol", order.SecurityNameCode);
            parameters.Add("price", order.Price.ToString().Replace(",", "."));
            parameters.Add("reduce_only", reduce);
            parameters.Add("close_on_trigger", "false");

            JToken place_order_response;

            if (client.FuturesMode == "Inverse")
                place_order_response =  BybitRestRequestBuilder.CreatePrivatePostQuery(client, "/v2/private/order/create", parameters);
            else 
                place_order_response = BybitRestRequestBuilder.CreatePrivatePostQuery(client, "/private/linear/order/create", parameters);

            var isSuccessful = place_order_response.SelectToken("ret_msg").Value<string>();

            if (isSuccessful == "OK")
            {
                SendLogMessage($"Order num {order.NumberUser} on exchange.", LogMessageType.Trade);
                order.State = OrderStateType.Activ;

                OnOrderEvent(order);
            }
            else
            {
                SendLogMessage($"Order exchange error num {order.NumberUser}" + isSuccessful, LogMessageType.Error);
                order.State = OrderStateType.Fail;

                OnOrderEvent(order);
            }
        } // both futures

        public override void CancelOrder(Order order)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();

            parameters.Add("api_key", client.ApiKey);
            parameters.Add("symbol", order.SecurityNameCode);
            parameters.Add("order_id", order.NumberMarket);

            JToken cancel_order_response;
            if (futures_type == "Inverse Perpetual")
                cancel_order_response = BybitRestRequestBuilder.CreatePrivatePostQuery(client, "/v2/private/order/cancel", parameters); ///private/linear/order/cancel
            else
                cancel_order_response = BybitRestRequestBuilder.CreatePrivatePostQuery(client, "/private/linear/order/cancel", parameters); 
            
            var isSuccessful = cancel_order_response.SelectToken("ret_msg").Value<string>();

            if (isSuccessful == "OK")
            {

            }
            else
            {
                SendLogMessage($"Error on order cancel num {order.NumberUser}", LogMessageType.Error);
            }
        } // both

        public override void GetOrdersState(List<Order> orders)
        {
            foreach (Order order in orders)
            {
                Dictionary<string, string> parameters = new Dictionary<string, string>();

                parameters.Add("api_key", client.ApiKey);
                parameters.Add("symbol", order.SecurityNameCode);
                parameters.Add("order_id", order.NumberMarket);

                JToken account_response;
                if(futures_type == "Inverse Perpetual")
                    account_response = BybitRestRequestBuilder.CreatePrivateGetQuery(client, "/v2/private/order", parameters); ///private/linear/order/search
                else
                    account_response = BybitRestRequestBuilder.CreatePrivateGetQuery(client, "/private/linear/order/search", parameters); 

                string isSuccessfull = account_response.SelectToken("ret_msg").Value<string>();

                if (isSuccessfull == "OK")
                {
                    string state = account_response.SelectToken("result").SelectToken("order_status").Value<string>();

                    switch (state)
                    {
                        case "Created":
                            order.State = OrderStateType.Activ;
                            break;
                        case "Rejected":
                            order.State = OrderStateType.Fail;
                            break;
                        case "New":
                            order.State = OrderStateType.Activ;
                            break;
                        case "PartiallyFilled":
                            order.State = OrderStateType.Patrial;
                            break;
                        case "Filled":
                            order.State = OrderStateType.Done;
                            break;
                        case "Cancelled":
                            order.State = OrderStateType.Cancel;
                            break;
                        case "PendingCancel":
                            order.State = OrderStateType.Cancel;
                            break;
                        default:
                            order.State = OrderStateType.None;
                            break;
                    }
                }
            }
        } // both

        #endregion


        #region Подписка на данные

        private void SendLoginMessage()
        {
            var login_message = BybitWsRequestBuilder.GetAuthRequest(client);
            ws_source_private.SendMessage(login_message);
        }

        public override void Subscrible(Security security)
        {
            SubscribeMarketDepth(security.Name);
            SubscribeTrades(security.Name);
            SubscribeOrders(security.Name);
            SubscribeMyTrades(security.Name);
        }

        private void SubscribeMarketDepth(string security)
        {
            string request = BybitWsRequestBuilder.GetSubscribeRequest("orderBookL2_25." + security);

            ws_source_public?.SendMessage(request);
        }

        private void SubscribeTrades(string security)
        {
            string request;

            request = BybitWsRequestBuilder.GetSubscribeRequest("trade." + security);

            ws_source_public?.SendMessage(request);
        }

        private void SubscribeOrders(string security)
        {
            string request = BybitWsRequestBuilder.GetSubscribeRequest("order");

            ws_source_private?.SendMessage(request);
        }

        private void SubscribeMyTrades(string security)
        {
            string request = BybitWsRequestBuilder.GetSubscribeRequest("execution");

            ws_source_private?.SendMessage(request);
        }

        #endregion

        #region message handlers
        private void HandleAuthMessage(JToken response)
        {
            SendLogMessage("Bybit: Successful authorization", LogMessageType.System);
        }

        private void HandlePingMessage(JToken response)
        {
            last_time_update_socket = DateTime.Now;
        }

        private void HandleSubscribeMessage(JToken response)
        {
            string subscribe = response.SelectToken("request").SelectToken("args").First().Value<string>();

            SendLogMessage("Bybit: Successful subscribed to " + subscribe, LogMessageType.System);
        }

        private void HandleorderBookL2_25Message(JToken response)
        {
            List<MarketDepth> new_md_list = new List<MarketDepth>();

            if (response.SelectToken("type").Value<string>() == "snapshot")
            {
                new_md_list = market_mepth_creator.CreateNew(response.SelectToken("data"), client);
            }

            if (response.SelectToken("type").Value<string>() == "delta")
            {
                new_md_list = market_mepth_creator.Update(response.SelectToken("data"));
            }

            foreach (var depth in new_md_list)
            {
                OnMarketDepthEvent(depth);
            }
        }

        private void HandleTradesMessage(JToken response)
        {
            List<Trade> new_trades = BybitTradesCreator.CreateFromWS(response.SelectToken("data"));

            foreach (var trade in new_trades)
            {
                OnTradeEvent(trade);
            }
        }

        private void HandleOrderMessage(JToken response)
        {
            List<Order> new_my_orders = BybitOrderCreator.Create(response.SelectToken("data"));

            foreach (var order in new_my_orders)
            {
                OnOrderEvent(order);
            }
        }

        private void HandleMyTradeMessage(JToken data)
        {
            List<MyTrade> new_my_trades = BybitTradesCreator.CreateMyTrades(data.SelectToken("data"));

            foreach (var trade in new_my_trades)
            {
                OnMyTradeEvent(trade);
            }
        }
        #endregion

        #region Работа со свечами

        public List<Candle> GetCandleHistory(string nameSec, TimeSpan tf)
        {
            var diff = new TimeSpan(0, (int)(tf.TotalMinutes * 200), 0);

            return GetCandles((int)tf.TotalMinutes, nameSec, DateTime.UtcNow - diff, DateTime.UtcNow);
        }

        public override List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder time_frame_builder, DateTime start_time, DateTime end_time, DateTime actual_time)
        {
            List<Candle> candles = new List<Candle>();

            int old_interval = Convert.ToInt32(time_frame_builder.TimeFrameTimeSpan.TotalMinutes);

            candles = GetCandles(old_interval, security.Name, start_time, end_time);

            if (candles.Count == 0)
            {
                return null;
            }

            return candles;
        }

        private List<Candle> GetCandles(int old_interval, string security, DateTime start_time, DateTime end_time)
        {
            lock (locker_candles)
            {
                List<Candle> tmp_candles = new List<Candle>();
                DateTime end_over = end_time;

                string need_interval_for_query = CandlesCreator.DetermineAppropriateIntervalForRequest(old_interval, supported_intervals, out var need_interval);

                while (true)
                {
                    var from = TimeManager.GetTimeStampSecondsToDateTime(start_time);
                
                    if (end_over <= start_time)
                    {
                        break;
                    }

                    List<Candle> new_candles = BybitCandlesCreator.GetCandleCollection(client, security, need_interval_for_query, from);

                    if (new_candles != null && new_candles.Count != 0)
                        tmp_candles.AddRange(new_candles);
                    else
                        break;

                    start_time = tmp_candles[tmp_candles.Count - 1].TimeStart.AddMinutes(old_interval);

                    Thread.Sleep(20);
                }

                for (int i = tmp_candles.Count - 1; i > 0; i--)
                {
                    if (tmp_candles[i].TimeStart > end_time)
                        tmp_candles.Remove(tmp_candles[i]);
                }

                if (old_interval == need_interval)
                {
                    return tmp_candles;
                }

                List<Candle> result_candles = CandlesCreator.CreateCandlesRequiredInterval(need_interval, old_interval, tmp_candles);

                return result_candles;
            }
        }
        #endregion

        #region Работа с тиками
        public override List<Trade> GetTickDataToSecurity(Security security, DateTime start_time, DateTime end_time, DateTime actual_time)
        {
            List<Trade> trades = new List<Trade>();

            trades = GetTrades(security.Name, start_time, end_time);

            if (trades.Count == 0)
            {
                return null;
            }

            return trades;
        }

        private List<Trade> GetTrades(string security, DateTime start_time, DateTime end_time)
        {
            lock (locker_candles)
            {
                List<Trade> result_trades = new List<Trade>();
                DateTime end_over = end_time;

                List<Trade> point_trades = BybitTradesCreator.GetTradesCollection(client, security, 1, -1);
                int last_trade_id = Convert.ToInt32(point_trades.Last().Id);

                while (true)
                {
                    List<Trade> new_trades = BybitTradesCreator.GetTradesCollection(client, security, 1000, last_trade_id - 1000);

                    if (new_trades != null && new_trades.Count != 0)
                    {
                        last_trade_id = Convert.ToInt32(new_trades.First().Id);

                        new_trades.AddRange(result_trades);
                        result_trades = new_trades;

                        if (result_trades.First().Time <= start_time)
                            break;
                    }
                    else
                        break;

                    Thread.Sleep(20);
                }

                for (int i = result_trades.Count - 1; i > 0; i--)
                {
                    if (result_trades[i].Time > end_time)
                        result_trades.Remove(result_trades[i]);
                }

                result_trades.Reverse();

                for (int i = result_trades.Count - 1; i > 0; i--)
                {
                    if (result_trades[i].Time < start_time)
                        result_trades.Remove(result_trades[i]);
                }

                result_trades.Reverse();

                return result_trades;
            }
        }

        #endregion


        public override void Dispose()
        {
            try
            {
                if (ws_source_private != null)
                {
                    ws_source_private.Dispose();
                    ws_source_private.MessageEvent -= WsSourceOnMessageEvent;
                    ws_source_private = null;          
                }

                if (ws_source_public != null)
                {
                    ws_source_public.Dispose();
                    ws_source_public.MessageEvent -= WsSourceOnMessageEvent;
                    ws_source_public = null;
                }

                if (cancel_token_source != null && !cancel_token_source.IsCancellationRequested)
                {
                    cancel_token_source.Cancel();
                }

                market_mepth_creator = new BybitMarketDepthCreator();
                cancel_token_source = new CancellationTokenSource();

                client = null;
            }
            catch (Exception e)
            {
                SendLogMessage("Bybit dispose error: " + e, LogMessageType.Error);
            }
        }
    }
}
