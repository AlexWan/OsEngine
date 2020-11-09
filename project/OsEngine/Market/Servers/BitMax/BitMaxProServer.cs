using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;

namespace OsEngine.Market.Servers.BitMax
{
    public class BitMaxProServer : AServer
    {
        public BitMaxProServer()
        {
            BitMaxProServerRealization realization = new BitMaxProServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");
        }

        /// <summary>
        /// instrument history query
        /// запрос истории по инструменту
        /// </summary>
        public List<Candle> GetCandleHistory(string nameSec, TimeSpan tf)
        {
            return ((BitMaxProServerRealization)ServerRealization).GetCandleHistory(nameSec, tf);
        }
    }

    public class BitMaxProServerRealization : IServerRealization
    {
        public BitMaxProServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        /// <summary>
        /// server type
        /// тип сервера
        /// </summary>
        public ServerType ServerType
        {
            get { return ServerType.BitMax; }
        }

        /// <summary>
        /// server status
        /// статус серверов
        /// </summary>
        public ServerConnectStatus ServerStatus { get; set; }

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

        /// <summary>
        /// bitMax Client
        /// </summary>
        private BitMaxProClient _client;

        #region requests / запросы

        /// <summary>
        /// connect to API
        /// подсоединиться к апи
        /// </summary>
        public void Connect()
        {
            if (_client == null)
            {
                _client = new BitMaxProClient(((ServerParameterString)ServerParameters[0]).Value,
                    ((ServerParameterPassword)ServerParameters[1]).Value);

                _client.Connected += Client_Connected;
                _client.UpdateSecurities += ClientReceivedSecurities;
                _client.Disconnected += Client_Disconnected;
                _client.NewPortfoliosEvent += ClientPortfoliosEvent;
                _client.NewSpotPortfoliosEvent += ClientOnNewSpotPortfoliosEvent;
                _client.UpdateMarketDepth += ClientUpdateMarketDepth;
                _client.NewTradesEvent += ClientNewTradesEvent;
                _client.MyOrderEvent += ClientMyOrderEvent;
                _client.LogMessageEvent += SendLogMessage;
            }

            _client.Connect();
        }

        

        /// <summary>
        /// release API
        /// освободить апи
        /// </summary>
        public void Dispose()
        {
            if (_client != null)
            {
                _client.Dispose();

                _client.Connected -= Client_Connected;
                _client.UpdateSecurities -= ClientReceivedSecurities;
                _client.Disconnected -= Client_Disconnected;
                _client.NewPortfoliosEvent -= ClientPortfoliosEvent;
                _client.NewSpotPortfoliosEvent -= ClientOnNewSpotPortfoliosEvent;
                _client.UpdateMarketDepth -= ClientUpdateMarketDepth;
                _client.NewTradesEvent -= ClientNewTradesEvent;
                _client.MyOrderEvent -= ClientMyOrderEvent;
                _client.LogMessageEvent -= SendLogMessage;
            }

            _client = null;
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        /// <summary>
        /// request securities
        /// запросить бумаги
        /// </summary>
        public void GetSecurities()
        {
            _client.GetSecurities();
        }

        private Task _portfoliosHandler;

        /// <summary>
        /// request portfolios
        /// запросить портфели
        /// </summary>
        public void GetPortfolios()
        {
            if (_portfoliosHandler != null)
            {
                return;
            }

            _portfoliosHandler = Task.Run(() =>
            {
                while (true)
                {
                    Thread.Sleep(1000);
                    if (_client == null)
                    {
                        return;
                    }
                    if (!_client.IsConnected)
                    {
                        continue;
                    }
                    _client.GetPortfolios();
                }
            });
        }

        public List<ReferencePrice> GetRefPrices()
        {
            var result = _client.GetRefPrices();

            return ConvertReferencePrices(result);
        }

        private List<ReferencePrice> ConvertReferencePrices(string values)
        {
            var referencePrices = new List<ReferencePrice>();

            var strings = values.Trim(new[] { '{', '}' }).Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var s in strings)
            {
                var data = s.Split(':');
                referencePrices.Add(new ReferencePrice()
                {
                    Asset = data[0].Trim('"'),
                    Price = data[1].Trim('"').ToDecimal(),
                });
            }

            return referencePrices;
        }

        /// <summary>
        /// send order
        /// исполнить ордер
        /// </summary>
        public void SendOrder(Order order)
        {
            var guid = Guid.NewGuid().ToString().Replace('-', '0');

            string needId = guid;

            while (needId.Length > 32)
            {
                needId = needId.Remove(needId.Length - 1);
            }

            var result = _client.SendOrder(order, needId);

            if (result == null || result.Code != 0)
            {
                order.State = OrderStateType.Fail;

                MyOrderEvent?.Invoke(order);

                SendLogMessage($"Order placement error № {result?.Code}.", LogMessageType.Error);
            }
            else if (result.Data.Status == "Ack")
            {
                var newCoupler = new OrderCoupler()
                {
                    OsOrderNumberUser = order.NumberUser,
                    OrderNumberMarket = result.Data.Info.OrderId,
                };

                _couplers.Add(newCoupler);
                order.State = OrderStateType.Activ;
                order.NumberMarket = result.Data.Info.OrderId;
                MyOrderEvent?.Invoke(order);
            }
            else if (result.Data.Status == "DONE")
            {
                var newCoupler = new OrderCoupler()
                {
                    OsOrderNumberUser = order.NumberUser,
                    OrderNumberMarket = result.Data.Info.OrderId,
                };

                _couplers.Add(newCoupler);
                order.State = OrderStateType.Done;
                order.NumberMarket = result.Data.Info.OrderId;
                MyOrderEvent?.Invoke(order);
            }
        }

        /// <summary>
        /// cancel order
        /// отозвать ордер
        /// </summary>
        public void CancelOrder(Order order)
        {
            var guid = Guid.NewGuid().ToString().Replace('-', '0');

            string needId = guid;

            while (needId.Length > 32)
            {
                needId = needId.Remove(needId.Length - 1);
            }

            var needCoupler = _couplers.Find(c => c.OrderNumberMarket == order.NumberMarket);
            if (needCoupler != null)
            {
                needCoupler.OrderCancelId = needId;
            }
            
            _client.CancelOrder(order, needId);
        }

        /// <summary>
        /// subscribe
        /// подписаться 
        /// </summary>
        public void Subscrible(Security security)
        {
            _client.SubscribeTradesAndDepths(security.Name);
        }

        private string GetNeedTimeFrameForServer(int tameFrame)
        {
            string needTf = "";

            switch (tameFrame)
            {
                case 1:
                    needTf = "1";
                    break;
                case 2:
                    needTf = "1";
                    break;
                case 3:
                    needTf = "1";
                    break;
                case 5:
                    needTf = "5";
                    break;
                case 10:
                    needTf = "5";
                    break;
                case 15:
                    needTf = "5";
                    break;
                case 20:
                    needTf = "5";
                    break;
                case 30:
                    needTf = "30";
                    break;
                case 45:
                    needTf = "5";
                    break;
                case 60:
                    needTf = "60";
                    break;
                case 120:
                    needTf = "60";
                    break;
                case 1440:
                    needTf = "1d";
                    break;
            }

            return needTf;
        }

        /// <summary>
        /// request instrument history
        /// запрос истории по инструменту
        /// </summary>
        public List<Candle> GetCandleHistory(string nameSec, TimeSpan tf)
        {
            string tfServer = GetNeedTimeFrameForServer((int)tf.TotalMinutes);

            int intervalTf;

            if (!int.TryParse(tfServer, out intervalTf))
            {
                intervalTf = 1440;
            }

            var interval = intervalTf * 1000;

            long start = TimeManager.GetUnixTimeStampMilliseconds() - Convert.ToInt64(TimeSpan.FromMinutes(interval).TotalMilliseconds);

            long end = TimeManager.GetUnixTimeStampMilliseconds();

            var bitMaxCandles = _client.GetCandles(nameSec, tfServer, start, end);

            List<Candle> newCandles = new List<Candle>();

            foreach (var bitMaxCandle in bitMaxCandles.Candles)
            {
                newCandles.Add(new Candle()
                {
                    Open = bitMaxCandle.Candle.O.ToDecimal(),
                    High = bitMaxCandle.Candle.H.ToDecimal(),
                    Low = bitMaxCandle.Candle.L.ToDecimal(),
                    Close = bitMaxCandle.Candle.C.ToDecimal(),
                    Volume = bitMaxCandle.Candle.V.ToDecimal(),
                    TimeStart = TimeManager.GetDateTimeFromTimeStamp(
                        Convert.ToInt64(bitMaxCandle.Candle.Ts.ToString().Replace(",",
                            CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                        CultureInfo.InvariantCulture)),
                    State = CandleState.None,
                });
            }

            if (intervalTf == (int)tf.TotalMinutes)
            {
                return newCandles;
            }

            var newTfCandles = BuildCandles(newCandles, (int)tf.TotalMinutes, intervalTf);

            return newTfCandles;
        }

        /// <summary>
        /// converts candles of one timeframe to a larger
        /// преобразует свечи одного таймфрейма в больший
        /// </summary>
        /// <param name="oldCandles"></param>
        /// <param name="needTf"></param>
        /// <param name="oldTf"></param>
        /// <returns></returns>
        private List<Candle> BuildCandles(List<Candle> oldCandles, int needTf, int oldTf)
        {
            List<Candle> newCandles = new List<Candle>();

            int index = oldCandles.FindIndex(can => can.TimeStart.Minute % needTf == 0);

            int count = needTf / oldTf;

            int counter = 0;

            Candle newCandle = new Candle();

            for (int i = index; i < oldCandles.Count; i++)
            {
                counter++;

                if (counter == 1)
                {
                    newCandle = new Candle();
                    newCandle.Open = oldCandles[i].Open;
                    newCandle.TimeStart = oldCandles[i].TimeStart;
                    newCandle.Low = Decimal.MaxValue;
                }

                newCandle.High = oldCandles[i].High > newCandle.High
                    ? oldCandles[i].High
                    : newCandle.High;

                newCandle.Low = oldCandles[i].Low < newCandle.Low
                    ? oldCandles[i].Low
                    : newCandle.Low;

                newCandle.Volume += oldCandles[i].Volume;

                if (counter == count)
                {
                    newCandle.Close = oldCandles[i].Close;
                    newCandle.State = CandleState.Finished;
                    newCandles.Add(newCandle);
                    counter = 0;
                }

                if (i == oldCandles.Count - 1 && counter != count)
                {
                    newCandle.Close = oldCandles[i].Close;
                    newCandle.State = CandleState.None;
                    newCandles.Add(newCandle);
                }
            }

            return newCandles;
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
        /// take ticks data on instrument for period
        /// взять тиковые данные по инструменту за период
        /// </summary>
        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        /// <summary>
        /// request order state
        /// запросить статус ордеров
        /// </summary>
        public void GetOrdersState(List<Order> orders)
        {
            foreach (var order in orders)
            {
                _client.GetOrderState(order.NumberMarket);
            }
        }

        #endregion

        #region parsing incoming data / разбор входящих данных

        void Client_Connected()
        {
            if (ConnectEvent != null)
            {
                ConnectEvent();
            }
            ServerStatus = ServerConnectStatus.Connect;
        }

        void Client_Disconnected()
        {
            if (DisconnectEvent != null)
            {
                DisconnectEvent();
            }

            ServerStatus = ServerConnectStatus.Disconnect;
        }

        private List<Portfolio> _portfolios = new List<Portfolio>();

        /// <summary>
        /// updated portfolio information
        /// обновилась информация о портфелях
        /// </summary>
        private void ClientPortfoliosEvent(Wallets wallets)
        {
            try
            {
                Portfolio myPortfolio = _portfolios.Find(p => p.Number == "BitMaxMargin");

                if (myPortfolio == null)
                {
                    Portfolio newPortf = new Portfolio();
                    newPortf.Number = "BitMaxMargin";
                    newPortf.ValueBegin = 1;
                    newPortf.ValueCurrent = 1;
                    _portfolios.Add(newPortf);
                    myPortfolio = newPortf;
                }

                if (wallets.Data == null)
                {
                    return;
                }

                UpdatePortfolio(myPortfolio, wallets);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ClientOnNewSpotPortfoliosEvent(Wallets wallets)
        {
            try
            {
                Portfolio myPortfolio = _portfolios.Find(p => p.Number == "BitMaxSpot");

                if (myPortfolio == null)
                {
                    Portfolio newPortf = new Portfolio();
                    newPortf.Number = "BitMaxSpot";
                    newPortf.ValueBegin = 1;
                    newPortf.ValueCurrent = 1;
                    _portfolios.Add(newPortf);
                    myPortfolio = newPortf;
                }

                if (wallets.Data == null)
                {
                    return;
                }

                UpdatePortfolio(myPortfolio, wallets);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void UpdatePortfolio(Portfolio myPortfolio, Wallets wallets)
        {
            myPortfolio.ClearPositionOnBoard();

            foreach (var wallet in wallets.Data)
            {
                PositionOnBoard newPortf = new PositionOnBoard();

                var valueCurrent = wallet.AvailableBalance.ToDecimal();

                newPortf.SecurityNameCode = wallet.Asset;

                newPortf.ValueBegin = wallet.TotalBalance.ToDecimal();
                newPortf.ValueCurrent = wallet.AvailableBalance.ToDecimal();
                newPortf.ValueBlocked = wallet.TotalBalance.ToDecimal() - valueCurrent;

                myPortfolio.SetNewPosition(newPortf);
            }

            PortfolioEvent?.Invoke(_portfolios);
        }

        /// <summary>
        /// получены инструменты с сервера
        /// </summary>
        /// <param name="products"></param>
        private void ClientReceivedSecurities(RootProducts products)
        {
            List<Security> securities = new List<Security>();

            foreach (var product in products.Data)
            {
                var newSec = new Security();

                newSec.Name = product.Symbol;
                newSec.NameClass = product.QuoteAsset;
                newSec.NameFull = product.Symbol;
                newSec.NameId = product.Symbol;
                newSec.Decimals = product.TickSize.DecimalsCount();
                newSec.SecurityType = SecurityType.CurrencyPair;
                newSec.State = product.Status == "Normal" ? SecurityStateType.Activ : SecurityStateType.Close;
                newSec.Lot = 1;
                newSec.PriceStep = product.TickSize.ToDecimal();
                newSec.PriceStepCost = newSec.PriceStep;
                newSec.Go = product.MinNotional.ToDecimal();

                securities.Add(newSec);
            }

            SecurityEvent?.Invoke(securities);
        }

        /// <summary>
        /// new trades event
        /// новые сделки на бирже
        /// </summary>
        /// <param name="trades"></param>
        private void ClientNewTradesEvent(TradeInfo trades)
        {
            if (trades.Data == null)
            {
                return;
            }
            foreach (var trade in trades.Data)
            {
                Trade newTrade = new Trade();
                newTrade.SecurityNameCode = trades.Symbol;
                newTrade.Price = trade.P.ToDecimal();
                newTrade.Id = trade.Seqnum.ToString();
                newTrade.Time = TimeManager.GetDateTimeFromTimeStamp(trade.Ts);
                newTrade.Volume = trade.Q.ToDecimal();
                newTrade.Side = trade.Bm == true ? Side.Sell : Side.Buy;

                ServerTime = newTrade.Time;

                if (NewTradesEvent != null)
                {
                    NewTradesEvent(newTrade);
                }
            }
        }

        /// <summary>
        /// updated market depth
        /// обновился стакан котировок
        /// </summary>
        private void ClientUpdateMarketDepth(MarketDepth marketDepth)
        {
            try
            {
                if (MarketDepthEvent != null)
                {
                    MarketDepthEvent(marketDepth);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private readonly List<OrderCoupler> _couplers = new List<OrderCoupler>();

        /// <summary>
        /// order and trade came
        /// пришел ордер и трейд
        /// </summary>
        /// <param name="bitMaxOrder"></param>
        private void ClientMyOrderEvent(OrderState bitMaxOrder)
        {
            var data = bitMaxOrder.Data;

            OrderCoupler needCoupler = _couplers.Find(c => c.OrderNumberMarket == data.OrderId);

            if (needCoupler == null)
            {
                return;
            }

            Order order = new Order();
            order.NumberUser = needCoupler.OsOrderNumberUser;
            order.NumberMarket = data.OrderId;
            order.PortfolioNumber = data.S.Split('/')[1];
            order.Price = data.P.ToDecimal();
            order.Volume = data.Q.ToDecimal();
            order.Side = data.Sd == "Buy" ? Side.Buy : Side.Sell;
            order.SecurityNameCode = data.S;
            order.ServerType = ServerType;
            order.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(data.T));
            order.TypeOrder = OrderPriceType.Limit;

            if (data.St == "New")
            {
                order.State = OrderStateType.Activ;
            }
            else if (data.St == "Canceled")
            {
                order.State = OrderStateType.Cancel;
                _couplers.Remove(needCoupler);
            }
            else if (data.St == "PartiallyFilled")
            {
                order.State = OrderStateType.Patrial;
            }
            else if (data.St == "Filled")
            {
                order.State = OrderStateType.Done;
                _couplers.Remove(needCoupler);
            }
            else if (data.St == "Rejected")
            {
                order.State = OrderStateType.Fail;
            }

            if (bitMaxOrder.Data.St == "PartiallyFilled" || bitMaxOrder.Data.St == "Filled")
            {
                var cumVolume = data.Cfq.ToDecimal();

                var tradeVolume = cumVolume - needCoupler.CurrentVolume;
                needCoupler.CurrentVolume += tradeVolume;

                MyTrade myTrade = new MyTrade
                {
                    NumberOrderParent = data.OrderId,
                    Side = data.Sd == "Buy" ? Side.Buy : Side.Sell,
                    SecurityNameCode = data.S,
                    Price = data.Ap.ToDecimal(),
                    Volume = tradeVolume,
                    NumberTrade = data.Sn.ToString(),
                    Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(data.T)),
                };

                MyTradeEvent?.Invoke(myTrade);
            }

            MyOrderEvent?.Invoke(order);
        }

        #endregion

        #region events

        public event Action<Order> MyOrderEvent;
        public event Action<MyTrade> MyTradeEvent;
        public event Action<List<Portfolio>> PortfolioEvent;

        /// <summary>
        /// new securities
        /// новые бумаги
        /// </summary>
        public event Action<List<Security>> SecurityEvent;

        public event Action<MarketDepth> MarketDepthEvent;
        public event Action<Trade> NewTradesEvent;
        public event Action ConnectEvent;
        public event Action DisconnectEvent;

        #endregion

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
        /// outgoing lom message
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        /// <summary>
        /// an auxiliary object that combines the order number in the osEngine, the order id on the exchange and the request id to cancel the order,
        /// It also stores the current open volume for the correct calculation of the volume of trades.
        /// вспомогательный объект, который сводит в себе номер ордера в осе, id ордера на бирже и id запроса на отмену ордера,
        /// а так же хранит текущий открытый объем для правильного расчета объема трейдов
        /// </summary>
        internal class OrderCoupler
        {
            public int OsOrderNumberUser;
            public string OrderNumberMarket;
            public string OrderCancelId;
            public decimal CurrentVolume = 0;
        }
    }
}
