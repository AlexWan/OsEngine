using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OkonkwoOandaV20;
using OkonkwoOandaV20.TradeLibrary.DataTypes.Instrument;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;

namespace OsEngine.Market.Servers.Oanda
{
    public class OandaServer: AServer
    {
        public OandaServer()
        {
            OandaServerRealization realization = new OandaServerRealization();
            ServerRealization = realization;

            CreateParameterString("Client ID", "");
            CreateParameterPassword("Token", "");
            CreateParameterBoolean("IsDemo", false);
        }
    }
    
    public class OandaServerRealization : IServerRealization
    {
        public OandaServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread watcherConnectThread = new Thread(ReconnectThread);
            watcherConnectThread.IsBackground = true;
            watcherConnectThread.Start();

            Thread workerGetTicks = new Thread(CandleGeterThread);
            workerGetTicks.IsBackground = true;
            workerGetTicks.Start();
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public ServerType ServerType => ServerType.Oanda;

        public List<IServerParameter> ServerParameters { get; set; }

        public DateTime ServerTime { get; set; }

        private DateTime _lastTimeIncomeDate = DateTime.MinValue;

        private void ReconnectThread()
        {
            while (true)
            {
                Thread.Sleep(5000);

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    continue;
                }

                if (DateTime.Now.DayOfWeek == DayOfWeek.Saturday
                    ||
                    DateTime.Now.DayOfWeek == DayOfWeek.Sunday)
                {
                    continue;
                }

                if (_lastTimeIncomeDate == DateTime.MinValue)
                {
                    continue;
                }

                if (_lastTimeIncomeDate.AddMinutes(5) < DateTime.Now)
                {
                    _lastTimeIncomeDate = DateTime.Now;
                    Dispose();
                }
            }
        }

        // requests
        // запросы

        private OandaClient _client;

        public void Connect()
        {
            if (_client == null)
            {
                _client = new OandaClient();

                _client.ConnectionFail += ClientOnConnectionFail;
                _client.ConnectionSucsess += ClientOnConnectionSucsess;
                _client.LogMessageEvent += ClientOnLogMessageEvent;
                _client.NewMyTradeEvent += ClientOnNewMyTradeEvent;
                _client.NewOrderEvent += ClientOnNewOrderEvent;
                _client.NewTradeEvent += ClientOnNewTradeEvent;
                _client.PortfolioChangeEvent += ClientOnPortfolioChangeEvent;
                _client.NewSecurityEvent += ClientOnNewSecurityEvent;
                _client.MarketDepthChangeEvent += ClientOnMarketDepthChangeEvent;
            }
            _client.Connect(((ServerParameterString)ServerParameters[0]).Value, ((ServerParameterPassword)ServerParameters[1]).Value,
                ((ServerParameterBool)ServerParameters[2]).Value);

           
        }

        public void Dispose()
        {
            if (_client != null)
            {
                _client.ConnectionFail -= ClientOnConnectionFail;
                _client.ConnectionSucsess -= ClientOnConnectionSucsess;
                _client.LogMessageEvent -= ClientOnLogMessageEvent;
                _client.NewMyTradeEvent -= ClientOnNewMyTradeEvent;
                _client.NewOrderEvent -= ClientOnNewOrderEvent;
                _client.NewTradeEvent -= ClientOnNewTradeEvent;
                _client.PortfolioChangeEvent -= ClientOnPortfolioChangeEvent;
                _client.NewSecurityEvent -= ClientOnNewSecurityEvent;
                _client.MarketDepthChangeEvent -= ClientOnMarketDepthChangeEvent;
            }

            try
            {
                if (_client != null)
                {
                    _client.Dispose();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

            _client = null;
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        public void GetSecurities()
        {
            if (_portfolios == null)
            {
                GetPortfolios();
                Thread.Sleep(15000);
            }

            if (_portfolios != null)
            {
              _client.GetSecurities(_portfolios);
              Thread.Sleep(15000);
            }
        }

        public void GetPortfolios()
        {
            _client.GetPortfolios();
        }

        public void SendOrder(Order order)
        {
            _client.ExecuteOrder(order);
        }

        public void CancelOrder(Order order)
        {
            _client.CancelOrder(order);
        }

        public void Subscrible(Security security)
        {
            _client.StartStreamThreads(security);

            if (_namesSecuritiesToGetTicks.Find(name => name.Name == security.Name) == null)
            {
                _namesSecuritiesToGetTicks.Add(security);
            }
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime,
            DateTime actualTime)
        {
            return null;
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        public void GetOrdersState(List<Order> orders)
        {
            
        }

        // parsing incoming data
        // разбор входящих данных

        private void ClientOnMarketDepthChangeEvent(MarketDepth marketDepth)
        {
            if (MarketDepthEvent != null)
            {
                MarketDepthEvent(marketDepth);
            }

            _depthsDontGo = false;
        }

        private void ClientOnNewSecurityEvent(List<Security> securities)
        {
            if (SecurityEvent != null)
            {
                SecurityEvent(securities);
            }
        }

        private List<Portfolio> _portfolios;

        private void ClientOnPortfolioChangeEvent(Portfolio portfolio)
        {
            if (portfolio == null)
            {
                return;
            }

            if (_portfolios == null)
            {
                _portfolios = new List<Portfolio>();
            }

            if (_portfolios.Find(p => p.Number == portfolio.Number) == null)
            {
                _portfolios.Add(portfolio);
            }

            if (PortfolioEvent != null)
            {
                PortfolioEvent(_portfolios);
            }
        }

        private void ClientOnNewTradeEvent(Trade trade)
        {
            if (NewTradesEvent != null)
            {
                NewTradesEvent(trade);
            }
        }

        private void ClientOnNewOrderEvent(Order order)
        {
            if (MyOrderEvent != null)
            {
                MyOrderEvent(order);
            }
        }

        private void ClientOnNewMyTradeEvent(MyTrade myTrade)
        {
            if (MyTradeEvent != null)
            {
                MyTradeEvent(myTrade);
            }
        }

        private void ClientOnLogMessageEvent(string message, LogMessageType type)
        {
            SendLogMessage(message, type);
        }

        private void ClientOnConnectionSucsess()
        {
            ServerStatus = ServerConnectStatus.Connect;
            if (ConnectEvent != null)
            {
                ConnectEvent();
            }
        }

        private void ClientOnConnectionFail()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            if (DisconnectEvent != null)
            {
                DisconnectEvent();
            }
        }

        // outgoing events исходящие события

        private List<Security> _namesSecuritiesToGetTicks = new List<Security>();

        private bool _depthsDontGo = true;

        /// <summary>
        /// взять таймфрейм в формате Oanda
        /// </summary>
        private string GetTimeFrameInOandaFormat(TimeFrame frame)
        {
            if (frame == TimeFrame.Sec5)
            {
                return CandleStickGranularity.Seconds05;
            }
            else if (frame == TimeFrame.Sec10)
            {
                return CandleStickGranularity.Seconds10;
            }
            else if (frame == TimeFrame.Sec15)
            {
                return CandleStickGranularity.Seconds15;
            }
            else if (frame == TimeFrame.Sec30)
            {
                return CandleStickGranularity.Seconds30;
            }
            else if (frame == TimeFrame.Min1)
            {
                return CandleStickGranularity.Minutes01;
            }
            else if (frame == TimeFrame.Min5)
            {
                return CandleStickGranularity.Minutes05;
            }
            else if (frame == TimeFrame.Min10)
            {
                return CandleStickGranularity.Minutes10;
            }
            else if (frame == TimeFrame.Min15)
            {
                return CandleStickGranularity.Minutes10;
            }
            else if (frame == TimeFrame.Min30)
            {
                return CandleStickGranularity.Minutes10;
            }
            else if (frame == TimeFrame.Hour1)
            {
                return CandleStickGranularity.Hours01;
            }
            else if (frame == TimeFrame.Hour2)
            {
                return CandleStickGranularity.Hours02;
            }
            return null;
        }

        private void CandleGeterThread()
        {
            while (true)
            {
                Thread.Sleep(1000);

                if (_depthsDontGo == false)
                {
                    return;
                }

                try
                {
                    for (int i = 0; i < _namesSecuritiesToGetTicks.Count; i++)
                    {
                        short count = 1;
                        string price = "MBA";
                        string instrument = _namesSecuritiesToGetTicks[i].Name;
                        string granularity = GetTimeFrameInOandaFormat(TimeFrame.Min1).ToString();

                        var parameters = new Dictionary<string, string>();
                        parameters.Add("price", price);
                        parameters.Add("granularity", granularity);
                        parameters.Add("count", count.ToString());

                        Task<List<CandlestickPlus>> result = Rest20.GetCandlesAsync(instrument, parameters);

                        while (!result.IsCanceled &&
                               !result.IsCompleted &&
                               !result.IsFaulted)
                        {
                            Thread.Sleep(10);
                        }

                        List<CandlestickPlus> candleOanda = result.Result;

                        Trade newCandle = new Trade();

                        newCandle.Price = Convert.ToDecimal(candleOanda[0].bid.c);
                        newCandle.Time = DateTime.Parse(candleOanda[0].time);
                        newCandle.SecurityNameCode = _namesSecuritiesToGetTicks[i].Name;
                        newCandle.Volume = candleOanda[0].volume;
                        newCandle.Side = Side.Buy;

                        if (NewTradesEvent != null)
                        {
                            NewTradesEvent(newCandle);
                        }

                        MarketDepth depth = new MarketDepth();
                        depth.SecurityNameCode = newCandle.SecurityNameCode;
                        depth.Time = newCandle.Time;

                        depth.Asks = new List<MarketDepthLevel>()
                        {
                            new MarketDepthLevel()
                            {
                                Ask = 1,Price = newCandle.Price + _namesSecuritiesToGetTicks[i].PriceStep
                            }
                        };

                        depth.Bids = new List<MarketDepthLevel>()
                        {
                            new MarketDepthLevel()
                            {
                                Bid= 1,Price = newCandle.Price - _namesSecuritiesToGetTicks[i].PriceStep
                            }
                        };

                        if (MarketDepthEvent != null)
                        {
                            MarketDepthEvent(depth);
                        }
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                    Thread.Sleep(1000);
                }


            }
        }

        /// <summary>
        /// called when order has changed
        /// вызывается когда изменился ордер
        /// </summary>
        public event Action<Order> MyOrderEvent;

        /// <summary>
        /// called when my trade has changed
        /// вызывается когда изменился мой трейд
        /// </summary>
        public event Action<MyTrade> MyTradeEvent;

        /// <summary>
        /// appeared new portfolios
        /// появились новые портфели
        /// </summary>
        public event Action<List<Portfolio>> PortfolioEvent;

        /// <summary>
        /// new securities
        /// новые бумаги
        /// </summary>
        public event Action<List<Security>> SecurityEvent;

        /// <summary>
        /// new depth
        /// новый стакан
        /// </summary>
        public event Action<MarketDepth> MarketDepthEvent;

        /// <summary>
        /// new trade
        /// новый трейд
        /// </summary>
        public event Action<Trade> NewTradesEvent;

        /// <summary>
        /// API connection established
        /// соединение с API установлено
        /// </summary>
        public event Action ConnectEvent;

        /// <summary>
        /// API connection lost
        /// соединение с API разорвано
        /// </summary>
        public event Action DisconnectEvent;

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
        /// outgoing log message
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }
}
