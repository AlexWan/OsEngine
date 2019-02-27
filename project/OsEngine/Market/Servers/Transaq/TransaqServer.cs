using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.Transaq.TransaqEntity;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Serialization;
using OsEngine.Language;
using Candle = OsEngine.Entity.Candle;
using Order = OsEngine.Entity.Order;
using Security = OsEngine.Entity.Security;
using Trade = OsEngine.Entity.Trade;

namespace OsEngine.Market.Servers.Transaq
{
    public class TransaqServer : AServer
    {
        public TransaqServer()
        {
            WorkingTimeSettings = new ServerWorkingTimeSettings()
            {
                StartSessionTime = new TimeSpan(9, 55, 0),
                EndSessionTime = new TimeSpan(23, 50, 0),
                WorkingAtWeekend = false,
                ServerTimeZone = "Russian Standard Time",
            };

            ServerRealization = new TransaqServerRealization(WorkingTimeSettings);

            CreateParameterString(OsLocalization.Market.Message63, "");
            CreateParameterPassword(OsLocalization.Market.Message64, "");
            CreateParameterString(OsLocalization.Market.Label41, "213.247.141.133");
            CreateParameterString(OsLocalization.Market.Message90, "3900");
        }

        public ServerWorkingTimeSettings WorkingTimeSettings;

        /// <summary>
        /// override method that gives server state
        /// переопределяем метод отдающий состояние сервера
        /// </summary>
        public override bool IsTimeToServerWork
        {
            get { return ((TransaqServerRealization)ServerRealization).ServerInWork; }
        }

        /// <summary>
        /// request of history by instrument
        /// запрос истории по инструменту
        /// </summary>
        public void GetCandleHistory(CandleSeries series)
        {
            ((TransaqServerRealization)ServerRealization).GetCandleHistory(series);
        }
    }

    public class TransaqServerRealization : IServerRealization
    {
        private readonly string _logPath;

        public TransaqServerRealization(ServerWorkingTimeSettings workingTimeSettings)
        {
            _workingTimeSettings = workingTimeSettings;

            ServerStatus = ServerConnectStatus.Disconnect;

            _logPath = AppDomain.CurrentDomain.BaseDirectory + @"Engine\TransaqLog";

            DirectoryInfo dirInfo = new DirectoryInfo(_logPath);

            if (!dirInfo.Exists)
            {
                dirInfo.Create();
            }
        }

        /// <summary>
        /// server type
        /// тип сервера
        /// </summary>
        public ServerType ServerType
        {
            get { return ServerType.Transaq; }
        }

        public List<IServerParameter> ServerParameters { get; set; }

        /// <summary>
        /// server time
        /// время сервера
        /// </summary>
        public DateTime ServerTime { get; set; }

        public ServerConnectStatus ServerStatus { get; set; }

        // outgoing events
        // исходящие события

        /// <summary>
        /// called when the order has changed
        /// вызывается когда изменился ордер
        /// </summary>
        public event Action<Order> MyOrderEvent;

        /// <summary>
        /// called when my trade has changed
        /// вызывается когда изменился мой трейд
        /// </summary>
        public event Action<MyTrade> MyTradeEvent;

        /// <summary>
        /// appear new portfolios
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

        TransaqClient _client;
        
        #region requests / запросы

        private CancellationTokenSource _cancellationTokenSource;

        private CancellationToken _cancellationToken;



        /// <summary>
        /// connect to API
        /// подсоединиться к апи
        /// </summary>
        public void Connect()
        {
            _client = new TransaqClient(((ServerParameterString)ServerParameters[0]).Value,
                ((ServerParameterPassword)ServerParameters[1]).Value,
                ((ServerParameterString)ServerParameters[2]).Value,
                ((ServerParameterString)ServerParameters[3]).Value,
                _logPath);

            _client.Connected += _client_Connected;
            _client.Disconnected += _client_Disconnected;
            _client.LogMessageEvent += SendLogMessage;
            _client.UpdatePairs += ClientOnUpdatePairs;
            _client.ClientsInfo += ClientsInfoUpdate;
            _client.UpdatePortfolio += ClientOnUpdatePortfolio;
            _client.NewTradesEvent += ClientOnNewTradesEvent;
            _client.UpdateMarketDepth += ClientOnUpdateMarketDepth;
            _client.MyOrderEvent += ClientOnMyOrderEvent;
            _client.MyTradeEvent += ClientOnMyTradeEvent;
            _client.NewCandles += ClientOnNewCandles;
            _client.NeedChangePassword += NeedChangePassword;

            _client.Connect();

            _cancellationTokenSource = new CancellationTokenSource();

            _cancellationToken = _cancellationTokenSource.Token;

            Task.Run(() => SessionTimeHandler(), _cancellationToken);
        }

        /// <summary>
        /// exchange requires to change the password
        /// биржа требует изменить пароль
        /// </summary>
        private void NeedChangePassword()
        {
            Application.Current.Dispatcher.Invoke((Action)delegate {
                string message = OsLocalization.Market.Message94;

                ChangeTransaqPassword changeTransaqPasswordWindow = new ChangeTransaqPassword(message, this);
                changeTransaqPasswordWindow.ShowDialog();
            });            
        }

        /// <summary>
        /// change password
        /// изменить пароль
        /// </summary>
        public void ChangePassword(string oldPassword, string newPassword)
        {
            string cmd = $"<command id=\"change_pass\" oldpass=\"{oldPassword}\" newpass=\"{newPassword}\"/>";

            // sending command / отправка команды
            string res = _client.ConnectorSendCommand(cmd);

            if (res == "<result success=\"true\"/>")
            {
                ((ServerParameterPassword)ServerParameters[1]).Value = newPassword;
            }

            Dispose();
        }

        private readonly ServerWorkingTimeSettings _workingTimeSettings;

        public bool ServerInWork = true;

        private void SessionTimeHandler()
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                var serverCurrentTime = TimeManager.GetExchangeTime(_workingTimeSettings.ServerTimeZone);

                if (serverCurrentTime.DayOfWeek == DayOfWeek.Saturday ||
                    serverCurrentTime.DayOfWeek == DayOfWeek.Sunday ||
                    serverCurrentTime.TimeOfDay < _workingTimeSettings.StartSessionTime ||
                    serverCurrentTime.TimeOfDay > _workingTimeSettings.EndSessionTime)
                {
                    ServerInWork = false;

                    if (_client.IsConnected)
                    {
                        _client.Dispose();
                    }
                }
                else
                {
                    ServerInWork = true;
                }

                Thread.Sleep(15000);
            }
        }


        /// <summary>
        /// dispose API
        /// освободить апи
        /// </summary>
        public void Dispose()
        {
            if (_client != null)
            {
                _client.Dispose();

                _client.Connected -= _client_Connected;
                _client.Disconnected -= _client_Disconnected;
                _client.LogMessageEvent -= SendLogMessage;
                _client.UpdatePairs -= ClientOnUpdatePairs;
                _client.ClientsInfo -= ClientsInfoUpdate;
                _client.UpdatePortfolio -= ClientOnUpdatePortfolio;
                _client.NewTradesEvent -= ClientOnNewTradesEvent;
                _client.UpdateMarketDepth -= ClientOnUpdateMarketDepth;
                _client.MyOrderEvent -= ClientOnMyOrderEvent;
                _client.MyTradeEvent -= ClientOnMyTradeEvent;
                _client.NewCandles -= ClientOnNewCandles;
            }
            _depths?.Clear();
            _depths = null;
            _allCandleSeries?.Clear();
            _cancellationTokenSource?.Cancel();

            _client = null;
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        public void GetOrdersState(List<Order> orders)
        {

        }

        private List<Portfolio> _portfolios;

        private Task _portfoliosHandlerTask;

        /// <summary>
        /// request portfolios
        /// запросить портфели
        /// </summary>
        public void GetPortfolios()
        {

            // <command id="get_united_portfolio" client="код клиента" union="код юниона" />
            if (_clients == null || _clients.Count == 0)
            {
                return;
            }

            if (_portfoliosHandlerTask != null)
            {
                return;
            }

            string cmd = $"<command id=\"get_united_portfolio\" union=\"{_clients[0].Union}\"/>";

            _portfoliosHandlerTask = Task.Run(() => CycleGettingPortfolios(cmd));

        }

        private void CycleGettingPortfolios(string cmd)
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                if (ServerInWork == false)
                {
                    continue;
                }

                if (!_client.IsConnected)
                {
                    continue;
                }
                // sending command / отправка команды
                string res = _client.ConnectorSendCommand(cmd);

                if (res != "<result success=\"true\"/>")
                {
                    SendLogMessage(res, LogMessageType.Error);
                }
                Thread.Sleep(10000);
            }
        }


        public void GetSecurities()
        {

        }

        /// <summary>
        /// send order to exchange
        /// выслать ордер на биржу
        /// </summary>
        public void SendOrder(Order order)
        {
            string side = order.Side == Side.Buy ? "B" : "S";

            var needSec = _securities.Find(s => s.Name == order.SecurityNameCode);
                
            string cmd = "<command id=\"neworder\">";
            cmd += "<security>";
            cmd += "<board>" + needSec.NameClass + "</board>";
            cmd += "<seccode>" + needSec.Name + "</seccode>";
            cmd += "</security>";
            cmd += "<union>" + _clients[0].Union + "</union>";
            cmd += "<price>" + order.Price + "</price>";
            cmd += "<quantity>" + order.Volume + "</quantity>";
            cmd += "<buysell>" + side + "</buysell>";
            cmd += "<brokerref>" + order.NumberUser + "</brokerref>";
            cmd += "<unfilled> PutInQueue </unfilled>";

            cmd += "</command>";

            // sending command / отправка команды
            string res = _client.ConnectorSendCommand(cmd);

            Result data;
            var formatter = new XmlSerializer(typeof(Result));
            using (StringReader fs = new StringReader(res))
            {
                data = (Result)formatter.Deserialize(fs);
            }
            
            //<result success="true" transactionid="12445626"/>
            if (data.Success != "true")
            {
                order.State = OrderStateType.Fail;                
            }
            else
            {                            
                order.NumberUser = int.Parse(data.Transactionid);
                order.State = OrderStateType.Pending;                
            }
            order.TimeCallBack = ServerTime;

            MyOrderEvent?.Invoke(order);
        }

        /// <summary>
        /// cancel order
        /// отменить ордер
        /// </summary>
        public void CanselOrder(Order order)
        {
            string cmd = "<command id=\"cancelorder\">";
            cmd += "<transactionid>" + order.NumberUser + "</transactionid>";
            cmd += "</command>";

            // отправка команды
            string res = _client.ConnectorSendCommand(cmd);

            if (!res.StartsWith("<result success=\"true\""))
            {
                SendLogMessage(res, LogMessageType.Error);
            }
        }

        /// <summary>
        /// subscribe to get ticks and depth by instrument
        /// подписаться на получение тиков и стаканов по инструменту
        /// </summary>
        /// <param name="security">subscribed instrument / инструмент на который подписываемся</param>
        public void Subscrible(Security security)
        {
            string cmd = "<command id=\"subscribe\">";
            cmd += "<alltrades>";
            cmd += "<security>";
            cmd += "<board>" + security.NameClass + "</board>";
            cmd += "<seccode>" + security.Name + "</seccode>";
            cmd += "</security>";
            cmd += "</alltrades>";
            cmd += "<quotes>";
            cmd += "<security>";
            cmd += "<board>" + security.NameClass + "</board>";
            cmd += "<seccode>" + security.Name + "</seccode>";
            cmd += "</security>";
            cmd += "</quotes>";
            cmd += "</command>";

            // sending command / отправка команды
            string res = _client.ConnectorSendCommand(cmd);

            if (res != "<result success=\"true\"/>")
            {
                SendLogMessage(res, LogMessageType.Error);
            }
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime,
            DateTime actualTime)
        {
            return null;
        }

        /// <summary>
        /// request candle history
        /// запросить историю свечей
        /// </summary>
        public void GetCandleHistory(CandleSeries series)
        {
            Task.Run(() => GetCandles(series));
        }

        private void GetCandles(CandleSeries series)
        {
            Security security = series.Security;
            TimeFrame tf = series.TimeFrame;

            string needPeriodId = GetNeedIdPeriod(tf, out var newTf, out var oldTf);

            string cmd = "<command id=\"gethistorydata\">";
            cmd += "<security>";
            cmd += "<board>" + security.NameClass + "</board>";
            cmd += "<seccode>" + security.Name + "</seccode>";
            cmd += "</security>";
            cmd += "<period>" + needPeriodId + "</period>";
            cmd += "<count>" + 1000 + "</count>";
            cmd += "<reset>" + "true" + "</reset>";
            cmd += "</command>";

            // sending command / отправка команды
            string res = _client.ConnectorSendCommand(cmd);

            if (res != "<result success=\"true\"/>")
            {
                SendLogMessage(res, LogMessageType.Error);
                return;
            }

            var startLoadingTime = DateTime.Now;

            while (startLoadingTime.AddSeconds(10) > DateTime.Now)
            {
                var candles = _allCandleSeries.Find(s => s.Seccode == security.Name && s.Period == needPeriodId);

                if (candles != null)
                {
                    //series.CandlesAll?.Clear();
                    var donorCandles = ParseCandles(candles);

                    if ((tf == TimeFrame.Min1 && needPeriodId == "1") ||
                        (tf == TimeFrame.Min5 && needPeriodId == "2") ||
                        (tf == TimeFrame.Min15 && needPeriodId == "3") ||
                        (tf == TimeFrame.Hour1 && needPeriodId == "4"))
                    {
                        series.CandlesAll = donorCandles;
                    }
                    else
                    {
                        series.CandlesAll = BuildCandles(donorCandles, newTf, oldTf);
                    }

                    series.UpdateAllCandles();
                    series.IsStarted = true;
                    return;
                }

                Thread.Sleep(1000);
            }

            SendLogMessage( OsLocalization.Market.Message95 + security.Name, LogMessageType.Error);
        }

        private List<Candle> ParseCandles(Candles candles)
        {
            try
            {
                List<Candle> osCandles = new List<Candle>();

                foreach (var candle in candles.Candle)
                {
                    osCandles.Add(new Candle()
                    {
                        Open = Convert.ToDecimal(candle.Open.Replace(".", ",")),
                        High = Convert.ToDecimal(candle.High.Replace(".", ",")),
                        Low = Convert.ToDecimal(candle.Low.Replace(".", ",")),
                        Close = Convert.ToDecimal(candle.Close.Replace(".", ",")),
                        Volume = Convert.ToDecimal(candle.Volume.Replace(".", ",")),
                        TimeStart = DateTime.Parse(candle.Date),
                    });
                }

                return osCandles;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        /// <summary>
        /// build candles
        /// собрать свечи
        /// </summary>
        /// <param name="oldCandles"></param>
        /// <param name="needTf"></param>
        /// <param name="oldTf"></param>
        /// <returns></returns>
        private List<Candle> BuildCandles(List<Candle> oldCandles, int needTf, int oldTf)
        {
            List<Candle> newCandles = new List<Candle>();

            int index;

            if (needTf == 120)
            {
                index = oldCandles.FindIndex(can => can.TimeStart.Hour % 2 == 0);
            }
            else if (needTf == 1440)
            {
                index = oldCandles.FindIndex(can => can.TimeStart.Hour == 10 &&
                                                    can.TimeStart.Minute == 0 &&
                                                    can.TimeStart.Second == 0);

                for (int i = index; i < oldCandles.Count; i++)
                {
                    if (oldCandles[i].TimeStart.Hour == 10 &&
                        oldCandles[i].TimeStart.Minute == 0 &&
                        oldCandles[i].TimeStart.Second == 0)
                    {
                        if (newCandles.Count != 0)
                        {
                            newCandles[newCandles.Count - 1].State = CandleState.Finished;
                        }
                        newCandles.Add(new Candle());
                        newCandles[newCandles.Count - 1].State = CandleState.None;
                        newCandles[newCandles.Count - 1].Open = oldCandles[i].Open;
                        newCandles[newCandles.Count - 1].TimeStart = oldCandles[i].TimeStart;
                        newCandles[newCandles.Count - 1].Low = Decimal.MaxValue;
                    }

                    if (newCandles.Count == 0)
                    {
                        continue;
                    }

                    newCandles[newCandles.Count - 1].High = oldCandles[i].High > newCandles[newCandles.Count - 1].High
                        ? oldCandles[i].High
                        : newCandles[newCandles.Count - 1].High;

                    newCandles[newCandles.Count - 1].Low = oldCandles[i].Low < newCandles[newCandles.Count - 1].Low
                        ? oldCandles[i].Low
                        : newCandles[newCandles.Count - 1].Low;

                    newCandles[newCandles.Count - 1].Close = oldCandles[i].Close;

                    newCandles[newCandles.Count - 1].Volume += oldCandles[i].Volume;

                }

                return newCandles;
            }
            else
            {
                index = oldCandles.FindIndex(can => can.TimeStart.Minute % needTf == 0);
            }

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
                    newCandle.State = CandleState.Started;
                    newCandles.Add(newCandle);
                }
            }

            return newCandles;
        }

        /// <summary>
        /// get needed id
        /// получить нужный Id
        /// </summary>
        private string GetNeedIdPeriod(TimeFrame tf, out int newTf, out int oldTf)
        {
            switch (tf)
            {
                case TimeFrame.Min1:
                    newTf = 1;
                    oldTf = 1;
                    return "1";
                case TimeFrame.Min2:
                    newTf = 2;
                    oldTf = 1;
                    return "1";
                case TimeFrame.Min3:
                    newTf = 3;
                    oldTf = 1;
                    return "1";
                case TimeFrame.Min5:
                    newTf = 5;
                    oldTf = 5;
                    return "2";
                case TimeFrame.Min10:
                    newTf = 10;
                    oldTf = 5;
                    return "2";
                case TimeFrame.Min20:
                    newTf = 20;
                    oldTf = 5;
                    return "2";
                case TimeFrame.Min15:
                    newTf = 15;
                    oldTf = 15;
                    return "3";
                case TimeFrame.Min30:
                    newTf = 30;
                    oldTf = 15;
                    return "3";
                case TimeFrame.Min45:
                    newTf = 45;
                    oldTf = 15;
                    return "3";
                case TimeFrame.Hour1:
                    newTf = 60;
                    oldTf = 60;
                    return "4";
                case TimeFrame.Hour2:
                    newTf = 120;
                    oldTf = 60;
                    return "4";
                case TimeFrame.Day:
                    newTf = 1440;
                    oldTf = 60;
                    return "4";
                default:
                    newTf = 0;
                    oldTf = 0;
                    return "5";
            }
        }

        #endregion

        #region parsing incoming data / разбор входящих данных

        void _client_Connected()
        {
            ConnectEvent?.Invoke();
            ServerStatus = ServerConnectStatus.Connect;
        }

        void _client_Disconnected()
        {
            DisconnectEvent?.Invoke();
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        private List<Client> _clients;

        /// <summary>
        /// updated client data
        /// обновились данные о клиенте
        /// </summary>
        private void ClientsInfoUpdate(Client clientInfo)
        {
            if (_clients == null)
            {
                _clients = new List<Client>();
            }
            _clients.Add(clientInfo);
        }

        /// <summary>
        /// got portfolio data
        /// пришли данные по портфелям
        /// </summary>
        /// <param name="unitedPortfolio">portfolio in Transaq format / портфель в формате Transaq</param>
        private void ClientOnUpdatePortfolio(UnitedPortfolio unitedPortfolio)
        {
            if (_portfolios == null)
            {
                _portfolios = new List<Portfolio>();
            }

            var needPortfolio = _portfolios.Find(p => p.Number == unitedPortfolio.Union);

            if (needPortfolio == null)
            {
                needPortfolio = new Portfolio()
                {
                    Number = unitedPortfolio.Union,
                    ValueBegin = Convert.ToDecimal(unitedPortfolio.Open_equity.Replace(".", ",")),
                    ValueCurrent = Convert.ToDecimal(unitedPortfolio.Equity.Replace(".", ",")),
                };
                _portfolios.Add(needPortfolio);
            }
            else
            {
                needPortfolio.ValueBegin = Convert.ToDecimal(unitedPortfolio.Open_equity.Replace(".", ","));
                needPortfolio.ValueCurrent = Convert.ToDecimal(unitedPortfolio.Equity.Replace(".", ","));
            }

            needPortfolio.ClearPositionOnBoard();

            if (unitedPortfolio.Asset != null && unitedPortfolio.Asset.Count != 0)
            {
                foreach (var asset in unitedPortfolio.Asset)
                {
                    for (int i = 0; i < asset.Security.Count; i++)
                    {
                        PositionOnBoard pos = new PositionOnBoard()
                        {
                            SecurityNameCode = asset.Security[i].Seccode,
                            ValueBegin = Convert.ToDecimal(asset.Security[i].Open_balance.Replace(".", ",")),
                            ValueCurrent = Convert.ToDecimal(asset.Security[i].Balance.Replace(".", ",")),
                            ValueBlocked = asset.Security[i].Buying == "0" ?
                                Convert.ToDecimal(asset.Security[i].Selling.Replace(".", ",")) :
                                Convert.ToDecimal(asset.Security[i].Buying.Replace(".", ",")),
                            PortfolioName = needPortfolio.Number,

                        };
                        needPortfolio.SetNewPosition(pos);
                    }
                }
            }

            PortfolioEvent?.Invoke(_portfolios);
        }

        /// <summary>
        /// all instruments of connector
        /// все инструменты коннектора
        /// </summary>
        private List<Security> _securities;

        /// <summary>
        /// got instruments
        /// пришли инструменты
        /// </summary>
        /// <param name="securities">list of instrument in transaq format / список инструментов в формате transaq</param>
        private void ClientOnUpdatePairs(List<TransaqEntity.Security> securities)
        {
            Task.Run(() => HandleSecurities(securities));
        }

        private void HandleSecurities(List<TransaqEntity.Security> securities)
        {
            if (_securities == null)
            {
                _securities = new List<Security>();
            }
            foreach (var securityData in securities)
            {
                try
                {
                    if (securityData.Sectype != "FUT" &&
                        securityData.Sectype != "SHARE" &&
                        securityData.Sectype != "CURRENCY")
                    {
                        continue;
                    }
                    Security security = new Security();

                    security.Name = securityData.Seccode;
                    security.NameFull = securityData.Shortname;
                    security.NameClass = securityData.Board;
                    security.NameId = securityData.Secid; // + "-" + securityData.Board;
                    security.Decimals = Convert.ToInt32(securityData.Decimals);

                    security.SecurityType = securityData.Sectype == "FUT" ? SecurityType.Futures
                        : securityData.Sectype == "SHARE" ? SecurityType.Stock
                        : securityData.Sectype == "OPT" ? SecurityType.Option
                        : securityData.Sectype == "CURRENCY" ? SecurityType.CurrencyPair
                        : SecurityType.None;


                    security.Lot = Convert.ToDecimal(securityData.Lotsize.Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                        CultureInfo.InvariantCulture);

                    security.PriceStep = Convert.ToDecimal(securityData.Minstep.Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                        CultureInfo.InvariantCulture);

                    decimal pointCost;
                    try
                    {
                        pointCost = Convert.ToDecimal(securityData.Point_cost.Replace(
                                ".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                            CultureInfo.InvariantCulture);
                    }
                    catch (Exception e)
                    {
                        decimal.TryParse(securityData.Point_cost, NumberStyles.Float, CultureInfo.InvariantCulture, out pointCost);
                    }

                    security.PriceStepCost = security.PriceStep * pointCost / 100;

                    security.State = securityData.Active == "true" ? SecurityStateType.Activ : SecurityStateType.Close;

                    _securities.Add(security);

                }
                catch (Exception e)
                {
                    SendLogMessage(e.Message, LogMessageType.Error);
                }
            }
            SecurityEvent?.Invoke(_securities);
        }

        /// <summary>
        /// got new ticks from server
        /// с сервера пришли новые тики
        /// </summary>
        private void ClientOnNewTradesEvent(List<TransaqEntity.Trade> trades)
        {
            foreach (var t in trades)
            {
                try
                {
                    Trade trade = new Trade()
                    {
                        SecurityNameCode = t.Seccode,
                        Id = t.Secid,
                        Price = Convert.ToDecimal(t.Price.Replace(".", ",")),
                        Side = t.Buysell == "B" ? Side.Buy : Side.Sell,
                        Volume = Convert.ToDecimal(t.Quantity.Replace(".", ",")),
                        Time = DateTime.Parse(t.Time),
                    };

                    NewTradesEvent?.Invoke(trade);
                }
                catch (Exception e)
                {
                    SendLogMessage("" + e, LogMessageType.Error);
                }
            }

        }

        /// <summary>
        /// all depths
        /// все стаканы
        /// </summary>
        private List<MarketDepth> _depths;

        private readonly object _depthLocker = new object();

        /// <summary>
        /// updated market depth
        /// обновился стакан котировок
        /// </summary>
        private void ClientOnUpdateMarketDepth(List<Quote> quotes)
        {
            try
            {
                lock (_depthLocker)
                {
                    if (quotes == null || quotes.Count == 0)
                    {
                        return;
                    }
                    if (_depths == null)
                    {
                        _depths = new List<MarketDepth>();
                    }

                    Dictionary<string, List<Quote>> sortedQuotes = new Dictionary<string, List<Quote>>();

                    foreach (var quote in quotes)
                    {
                        if (!sortedQuotes.ContainsKey(quote.Seccode))
                        {
                            sortedQuotes.Add(quote.Seccode, new List<Quote>());
                        }

                        sortedQuotes[quote.Seccode].Add(quote);
                    }

                    foreach (var sortedQuote in sortedQuotes)
                    {
                        var needDepth = _depths.Find(depth => depth.SecurityNameCode == sortedQuote.Value[0].Seccode);

                        if (needDepth == null)
                        {
                            needDepth = new MarketDepth();
                            needDepth.SecurityNameCode = sortedQuote.Value[0].Seccode;
                            _depths.Add(needDepth);
                        }

                        for (int i = 0; i < sortedQuote.Value.Count; i++)
                        {
                            if (sortedQuote.Value[i].Buy == -1 && sortedQuote.Value[i].Sell == -1)
                            {

                            }
                            if (sortedQuote.Value[i].Buy > 0)
                            {
                                var needLevel = needDepth.Bids.Find(level => level.Price == sortedQuote.Value[i].Price);
                                if (needLevel != null)
                                {
                                    needLevel.Bid = sortedQuote.Value[i].Buy;
                                }
                                else
                                {
                                    needDepth.Bids.Add(new MarketDepthLevel()
                                    {
                                        Price = sortedQuote.Value[i].Price,
                                        Bid = sortedQuote.Value[i].Buy,
                                    });
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
                                }

                            }
                            if (sortedQuote.Value[i].Sell > 0)
                            {
                                var needLevel = needDepth.Asks.Find(level => level.Price == sortedQuote.Value[i].Price);
                                if (needLevel != null)
                                {
                                    needLevel.Ask = sortedQuote.Value[i].Sell;
                                }
                                else
                                {
                                    needDepth.Asks.Add(new MarketDepthLevel()
                                    {
                                        Price = sortedQuote.Value[i].Price,
                                        Ask = sortedQuote.Value[i].Sell,
                                    });
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
                                }
                            }
                            if (sortedQuote.Value[i].Buy == -1)
                            {
                                var deleteLevelIndex = needDepth.Bids.FindIndex(level => level.Price == sortedQuote.Value[i].Price);
                                if (deleteLevelIndex != -1)
                                {
                                    needDepth.Bids.RemoveAt(deleteLevelIndex);
                                }
                            }
                            if (sortedQuote.Value[i].Sell == -1)
                            {
                                var deleteLevelIndex = needDepth.Asks.FindIndex(level => level.Price == sortedQuote.Value[i].Price);
                                if (deleteLevelIndex != -1)
                                {
                                    needDepth.Asks.RemoveAt(deleteLevelIndex);
                                }
                            }
                        }

                        needDepth.Time = ServerTime == DateTime.MinValue ? TimeManager.GetExchangeTime("Russian Standard Time") : ServerTime;

                        if (MarketDepthEvent != null)
                        {
                            MarketDepthEvent(needDepth.GetCopy());
                        }
                    }
                }
            }
            catch (Exception e)
            {
                SendLogMessage("UpdateMarketDepth" + e, LogMessageType.Error);
            }
        }

        /// <summary>
        /// got order information
        /// пришла информация об ордерах
        /// </summary>
        private void ClientOnMyOrderEvent(List<TransaqEntity.Order> orders)
        {
            foreach (var order in orders)
            {
                try
                {
                    if (order.Orderno == "0")
                    {
                        continue;
                    }
                
                    Order newOrder = new Order();
                    newOrder.SecurityNameCode = order.Seccode;
                    newOrder.NumberUser = Convert.ToInt32(order.Transactionid);
                    newOrder.NumberMarket = order.Orderno;
                    newOrder.TimeCallBack = order.Time != null ? DateTime.Parse(order.Time) : ServerTime;
                    newOrder.Side = order.Buysell == "B" ? Side.Buy : Side.Sell;
                    newOrder.Volume = Convert.ToDecimal(order.Quantity);
                    newOrder.Price = Convert.ToDecimal(order.Price.Replace(".", ","));
                    newOrder.ServerType = ServerType.Transaq;
                    newOrder.PortfolioNumber = order.Union;

                    if (order.Status == "active")
                    {
                        newOrder.State = OrderStateType.Activ;                       
                    }
                    else if (order.Status == "cancelled" ||
                             order.Status == "expired" ||
                             order.Status == "disabled" ||
                             order.Status == "removed")
                    {
                        newOrder.State = OrderStateType.Cancel;
                    }
                    else if (order.Status == "matched")
                    {
                        newOrder.State = OrderStateType.Done;
                    }
                    else if (order.Status == "denied" ||
                             order.Status == "rejected" ||
                             order.Status == "failed" ||
                             order.Status == "refused")
                    {
                        newOrder.State = OrderStateType.Fail;
                    }
                    else if (order.Status == "forwarding" ||
                             order.Status == "wait" ||
                             order.Status == "watching")
                    {
                        newOrder.State = OrderStateType.Pending;
                    }
                    else
                    {
                        newOrder.State = OrderStateType.None;
                    }

                    if (MyOrderEvent != null)
                    {
                        MyOrderEvent(newOrder);
                    }
                }
                catch (Exception e)
                {
                    SendLogMessage(e.ToString(), LogMessageType.Error);
                }
            }

        }

        /// <summary>
        /// got my trades
        /// пришли мои трейды
        /// </summary>
        private void ClientOnMyTradeEvent(List<TransaqEntity.Trade> trades)
        {
            foreach (var trade in trades)
            {
                MyTrade myTrade = new MyTrade();
                myTrade.Time = DateTime.Parse(trade.Time);
                myTrade.NumberOrderParent = trade.Orderno;
                myTrade.NumberTrade = trade.Tradeno;
                myTrade.Volume = Convert.ToDecimal(trade.Quantity.Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                myTrade.Price = Convert.ToDecimal(trade.Price.Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                myTrade.SecurityNameCode = trade.Seccode;
                myTrade.Side = trade.Buysell == "B" ? Side.Buy : Side.Sell;

                if (MyTradeEvent != null)
                {
                    MyTradeEvent(myTrade);
                }
            }
        }

        private List<Candles> _allCandleSeries = new List<Candles>();

        private void ClientOnNewCandles(Candles candles)
        {
            _allCandleSeries.Add(candles);
        }

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
    }
}
