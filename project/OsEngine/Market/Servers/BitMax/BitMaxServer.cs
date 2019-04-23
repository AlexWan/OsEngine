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
    public class BitMaxServer : AServer
    {
        public BitMaxServer()
        {
            BitMaxServerRealization realization = new BitMaxServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");
            CreateParameterBoolean(OsLocalization.Market.ServerParam4, false);
        }

        /// <summary>
        /// instrument history query
        /// запрос истории по инструменту
        /// </summary>
        public List<Candle> GetCandleHistory(string nameSec, TimeSpan tf)
        {
            return ((BitMaxServerRealization)ServerRealization).GetCandleHistory(nameSec, tf);
        }
    }

    public class BitMaxServerRealization : IServerRealization
    {
        public BitMaxServerRealization()
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
        private BitMaxClient _client;

        #region requests / запросы

        /// <summary>
        /// connect to API
        /// подсоединиться к апи
        /// </summary>
        public void Connect()
        {
            if (_client == null)
            {
                _client = new BitMaxClient(((ServerParameterString)ServerParameters[0]).Value,
                    ((ServerParameterPassword)ServerParameters[1]).Value,
                    ((ServerParameterBool)ServerParameters[2]).Value);

                _client.Connected += Client_Connected;
                _client.UpdateSecurities += ClientReceivedSecurities;
                _client.Disconnected += Client_Disconnected;
                _client.NewPortfoliosEvent += ClientPortfoliosEvent;
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
                    Thread.Sleep(10000);
                    if (!_client.IsConnected)
                    {
                        continue;
                    }
                    _client.GetPortfolios();
                }
            });
        }

        /// <summary>
        /// send order
        /// исполнить ордер
        /// </summary>
        public void SendOrder(Order order)
        {
            var guid = Guid.NewGuid().ToString().Replace('-', '0');

            var needId = guid.Remove(0, guid.Length - 32);

            var result = _client.SendOrder(order, needId);

            if (result.code == 0)
            {
                _couplers.Add(new OrderCoupler()
                {
                    OsOrderNumberUser = order.NumberUser,
                    OrderNumberMarket = needId,
                });
            }
            else
            {
                order.State = OrderStateType.Fail;

                MyOrderEvent?.Invoke(order);

                SendLogMessage($"Order placement error № {result.code} : {result.message}", LogMessageType.Error);
            }
        }

        /// <summary>
        /// cancel order
        /// отозвать ордер
        /// </summary>
        public void CanselOrder(Order order)
        {
            var guid = Guid.NewGuid().ToString().Replace('-', '0');

            var needId = guid.Remove(0, guid.Length - 32);

            var result = _client.CancelOrder(order, needId);

            if (result.code == 0)
            {
                var needCoupler = _couplers.Find(c => c.OrderNumberMarket == order.NumberMarket);
                if (needCoupler != null)
                {
                    needCoupler.OrderCancelId = needId;
                }
                else
                {
                    SendLogMessage($"Order cancellation  error № {result.code} : {result.message}", LogMessageType.Error);
                }
            }
            else
            {
                SendLogMessage($"Order cancellation  error № {result.code} : {result.message}", LogMessageType.Error);

                order.State = OrderStateType.Cancel;

                MyOrderEvent?.Invoke(order);
            }
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

            foreach (var bitMaxCandle in bitMaxCandles)
            {
                newCandles.Add(new Candle()
                {
                    Open = Convert.ToDecimal(
                        bitMaxCandle.o.Replace(",",
                            CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                        CultureInfo.InvariantCulture),
                    High = Convert.ToDecimal(
                        bitMaxCandle.h.Replace(",",
                            CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                        CultureInfo.InvariantCulture),
                    Low = Convert.ToDecimal(
                        bitMaxCandle.l.Replace(",",
                            CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                        CultureInfo.InvariantCulture),
                    Close = Convert.ToDecimal(
                        bitMaxCandle.c.Replace(",",
                            CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                        CultureInfo.InvariantCulture),
                    Volume = Convert.ToDecimal(
                        bitMaxCandle.v.Replace(",",
                            CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                        CultureInfo.InvariantCulture),
                    TimeStart = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(bitMaxCandle.t.ToString().Replace(",",
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
            _depths.Clear();
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        private List<Portfolio> _portfolios = new List<Portfolio>();

        /// <summary>
        /// updated portfolio information
        /// обновилась информация о портфелях
        /// </summary>
        /// <param name="accaunt"></param>
        private void ClientPortfoliosEvent(Accaunt accaunt)
        {
            foreach (var wallet in accaunt.data)
            {
                var needPortfolio = _portfolios.Find(p => p.Number == wallet.assetCode);
                if (needPortfolio != null)
                {
                    needPortfolio.ValueCurrent = Convert.ToDecimal(
                        wallet.totalAmount.Replace(".",
                            CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                        CultureInfo.InvariantCulture);
                    needPortfolio.ValueBlocked = Convert.ToDecimal(
                        wallet.inOrderAmount.Replace(".",
                            CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                        CultureInfo.InvariantCulture);
                }
                else
                {
                    var valueCurrent = Convert.ToDecimal(
                        wallet.totalAmount.Replace(".",
                            CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                        CultureInfo.InvariantCulture);

                    var valueBlocked = Convert.ToDecimal(
                        wallet.inOrderAmount.Replace(".",
                            CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                        CultureInfo.InvariantCulture);
                    if (valueCurrent != 0 || valueBlocked != 0)
                    {
                        _portfolios.Add(new Portfolio
                        {
                            Number = wallet.assetCode,
                            ValueCurrent = valueCurrent,
                            ValueBlocked = valueBlocked,
                        });
                    }
                }
            }
            PortfolioEvent?.Invoke(_portfolios);
        }

        /// <summary>
        /// получены инструменты с сервера
        /// </summary>
        /// <param name="products"></param>
        private void ClientReceivedSecurities(List<Product> products)
        {
            List<Security> securities = new List<Security>();

            foreach (var product in products)
            {
                var newSec = new Security();

                newSec.Name = product.baseAsset + "-" + product.quoteAsset;
                newSec.NameClass = product.quoteAsset;
                newSec.NameFull = product.symbol;
                newSec.NameId = product.symbol;
                newSec.Decimals = product.priceScale;
                newSec.SecurityType = SecurityType.CurrencyPair;
                newSec.State = product.status == "Normal" ? SecurityStateType.Activ : SecurityStateType.Close;
                newSec.Lot = GetValueByDecimals(product.qtyScale);
                newSec.PriceStep = GetValueByDecimals(product.priceScale);
                newSec.PriceStepCost = newSec.PriceStep;

                securities.Add(newSec);
            }

            SecurityEvent?.Invoke(securities);
        }

        /// <summary>
        /// получить точность шкалы на основании количества знаков после запятой
        /// </summary>
        /// <param name="decimals">количество знаков после запятой</param>
        private decimal GetValueByDecimals(int decimals)
        {
            switch (decimals)
            {
                case 0:
                    return 1;
                case 1:
                    return 0.1m;
                case 2:
                    return 0.01m;
                case 3:
                    return 0.001m;
                case 4:
                    return 0.0001m;
                case 5:
                    return 0.00001m;
                case 6:
                    return 0.000001m;
                case 7:
                    return 0.0000001m;
                case 8:
                    return 0.00000001m;
                case 9:
                    return 0.000000001m;
                case 10:
                    return 0.0000000001m;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// multi-threaded access locker to ticks
        /// блокиратор многопоточного доступа к тикам
        /// </summary>
        private readonly object _newTradesLoker = new object();

        /// <summary>
        /// new trades event
        /// новые сделки на бирже
        /// </summary>
        /// <param name="trades"></param>
        private void ClientNewTradesEvent(Trades trades)
        {
            lock (_newTradesLoker)
            {
                if (trades.trades == null)
                {
                    return;
                }
                foreach (var trade in trades.trades)
                {
                    Trade newTrade = new Trade();
                    newTrade.SecurityNameCode = trades.s.Replace('/', '-');
                    newTrade.Price =
                        Convert.ToDecimal(
                            trade.p.Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                            CultureInfo.InvariantCulture);
                    newTrade.Id = trade.t.ToString();
                    newTrade.Time = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(trade.t));
                    newTrade.Volume =
                        Convert.ToDecimal(
                            trade.q.Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                            CultureInfo.InvariantCulture);
                    newTrade.Side = trade.bm == true ? Side.Sell : Side.Buy;

                    ServerTime = newTrade.Time;

                    if (NewTradesEvent != null)
                    {
                        NewTradesEvent(newTrade);
                    }
                }
            }
        }

        /// <summary>
        /// all depths
        /// все стаканы
        /// </summary>
        private List<MarketDepth> _depths;

        private bool _needSortAsks = false;
        private bool _needSortBids = false;

        private readonly object _depthLocker = new object();

        /// <summary>
        /// updated market depth
        /// обновился стакан котировок
        /// </summary>
        /// <param name="bitMaxDepth"></param>
        private void ClientUpdateMarketDepth(Depth bitMaxDepth)
        {
            try
            {
                lock (_depthLocker)
                {
                    if (_depths == null)
                    {
                        _depths = new List<MarketDepth>();
                    }

                    if (bitMaxDepth.asks == null ||
                        bitMaxDepth.bids == null)
                    {
                        return;
                    }

                    var needDepth = _depths.Find(depth =>
                        depth.SecurityNameCode == bitMaxDepth.s.Replace('/', '-'));

                    if (needDepth == null)
                    {
                        needDepth = new MarketDepth();
                        needDepth.SecurityNameCode = bitMaxDepth.s.Replace('/', '-');
                        _depths.Add(needDepth);
                    }

                    for (int i = 0; i < bitMaxDepth.asks.Count; i++)
                    {
                        var needPrice = Convert.ToDecimal(
                            bitMaxDepth.asks[i][0].Replace(",",
                                CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                            CultureInfo.InvariantCulture);

                        var needLevel = needDepth.Asks.Find(l => l.Price == needPrice);

                        var qty = Convert.ToDecimal(
                            bitMaxDepth.asks[i][1].Replace(",",
                                CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                            CultureInfo.InvariantCulture);

                        if (needLevel != null)
                        {
                            if (qty == 0)
                            {
                                needDepth.Asks.Remove(needLevel);
                                needLevel = needDepth.Bids.Find(l => l.Price == needPrice);

                                if (needLevel != null)
                                {
                                    needDepth.Bids.Remove(needLevel);
                                }
                            }
                            else
                            {
                                needLevel.Ask = qty;
                            }
                        }
                        else
                        {
                            if (qty == 0)
                            {
                                continue;
                            }
                            needDepth.Asks.Add(new MarketDepthLevel()
                            {
                                Ask = qty,
                                Price = needPrice,
                            });
                            _needSortAsks = true;
                        }
                    }

                    for (int i = 0; i < bitMaxDepth.bids.Count; i++)
                    {
                        var needPrice = Convert.ToDecimal(
                            bitMaxDepth.bids[i][0].Replace(",",
                                CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                            CultureInfo.InvariantCulture);

                        var needLevel = needDepth.Bids.Find(l => l.Price == needPrice);

                        var qty = Convert.ToDecimal(
                            bitMaxDepth.bids[i][1].Replace(",",
                                CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                            CultureInfo.InvariantCulture);

                        if (needLevel != null)
                        {
                            if (qty == 0)
                            {
                                needDepth.Bids.Remove(needLevel);
                                needLevel = needDepth.Asks.Find(l => l.Price == needPrice);

                                if (needLevel != null)
                                {
                                    needDepth.Asks.Remove(needLevel);
                                }
                            }
                            else
                            {
                                needLevel.Bid = qty;
                            }
                        }
                        else
                        {
                            if (qty == 0)
                            {
                                continue;
                            }

                            needDepth.Bids.Add(new MarketDepthLevel()
                            {
                                Bid = qty,
                                Price = needPrice,
                            });
                            _needSortBids = true;
                        }
                    }

                    if (_needSortAsks)
                    {
                        needDepth.Asks.Sort((a, b) =>
                        {
                            if (a.Price > b.Price)
                            {
                                return 1;
                            }
                            else if (a.Price < b.Price)
                            {
                                return -1;
                            }
                            else
                            {
                                return 0;
                            }
                        });
                        _needSortAsks = false;
                    }

                    if (_needSortBids)
                    {
                        needDepth.Bids.Sort((a, b) =>
                        {
                            if (a.Price > b.Price)
                            {
                                return -1;
                            }
                            else if (a.Price < b.Price)
                            {
                                return 1;
                            }
                            else
                            {
                                return 0;
                            }
                        });
                        _needSortBids = false;
                    }
                    if (needDepth.Asks.Count > 20)
                    {
                        needDepth.Asks.RemoveRange(20, needDepth.Asks.Count - 20);
                    }
                    if (needDepth.Bids.Count > 20)
                    {
                        needDepth.Bids.RemoveRange(20, needDepth.Bids.Count - 20);
                    }

                    needDepth.Time = ServerTime;

                    if (needDepth.Time == DateTime.MinValue)
                    {
                        return;
                    }

                    if (MarketDepthEvent != null)
                    {
                        MarketDepthEvent(needDepth.GetCopy());
                    }
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
        private void ClientMyOrderEvent(BitMaxOrder bitMaxOrder)
        {
            OrderCoupler needCoupler;

            if (bitMaxOrder.status == "Canceled")
            {
                needCoupler = _couplers.Find(c => c.OrderCancelId == bitMaxOrder.coid);
            }
            else
            {
                needCoupler = _couplers.Find(c => c.OrderNumberMarket == bitMaxOrder.coid);
            }

            if (needCoupler == null)
            {
                return;
            }

            if (bitMaxOrder.status == "PartiallyFilled" || bitMaxOrder.status == "Filled")
            {
                var partialVolume = Convert.ToDecimal(
                    bitMaxOrder.f.Replace(",",
                        CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                    CultureInfo.InvariantCulture);

                var tradeVolume = partialVolume - needCoupler.CurrentVolume;
                needCoupler.CurrentVolume += tradeVolume;

                MyTrade myTrade = new MyTrade()
                {
                    NumberOrderParent = bitMaxOrder.coid,
                    Side = bitMaxOrder.side == "Sell" ? Side.Sell : Side.Buy,
                    NumberPosition = bitMaxOrder.coid,
                    SecurityNameCode = bitMaxOrder.s.Replace('/', '-'),
                    Price = Convert.ToDecimal(
                        bitMaxOrder.p.Replace(",",
                            CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                        CultureInfo.InvariantCulture),
                    Volume = tradeVolume,
                    NumberTrade = Guid.NewGuid().ToString(),
                    Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(bitMaxOrder.t)),
                };

                MyTradeEvent?.Invoke(myTrade);
            }

            Order order = new Order();
            order.NumberUser = needCoupler.OsOrderNumberUser;
            order.NumberMarket = bitMaxOrder.coid;
            order.PortfolioNumber = bitMaxOrder.s.Split('/')[1];
            order.Price = Convert.ToDecimal(
                bitMaxOrder.p.Replace(",",
                    CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                CultureInfo.InvariantCulture);
            order.Volume = Convert.ToDecimal(
                bitMaxOrder.q.Replace(",",
                    CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                CultureInfo.InvariantCulture);
            order.Side = bitMaxOrder.side == "Sell" ? Side.Sell : Side.Buy;
            order.SecurityNameCode = bitMaxOrder.s.Replace('/', '-');
            order.ServerType = ServerType;
            order.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(bitMaxOrder.t));
            order.TypeOrder = OrderPriceType.Limit;

            if (bitMaxOrder.status == "New")
            {
                order.State = OrderStateType.Activ;
            }
            else if (bitMaxOrder.status == "PartiallyFilled")
            {
                order.State = OrderStateType.Patrial;
            }
            else if (bitMaxOrder.status == "Filled")
            {
                order.State = OrderStateType.Done;
                _couplers.Remove(needCoupler);
            }
            else if (bitMaxOrder.status == "Canceled")
            {
                order.State = OrderStateType.Cancel;
                _couplers.Remove(needCoupler);
            }
            else if (bitMaxOrder.status == "Rejected")
            {
                order.State = OrderStateType.Fail;
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
