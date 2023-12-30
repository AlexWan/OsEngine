﻿using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.Transaq.TransaqEntity;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;
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
                StartSessionTime = new TimeSpan(6, 55, 0),
                EndSessionTime = new TimeSpan(23, 50, 0),
                WorkingAtWeekend = false,
                ServerTimeZone = "Russian Standard Time",
            };

            ServerRealization = new TransaqServerRealization(WorkingTimeSettings);

            CreateParameterString(OsLocalization.Market.Message63, "");
            CreateParameterPassword(OsLocalization.Market.Message64, "");
            CreateParameterString(OsLocalization.Market.Label41, "tr1.finam.ru");
            CreateParameterString(OsLocalization.Market.Message90, "3900");
            CreateParameterBoolean(OsLocalization.Market.UseStock, true);
            CreateParameterBoolean(OsLocalization.Market.UseFutures, true);
            CreateParameterBoolean(OsLocalization.Market.UseCurrency, true);
            CreateParameterBoolean(OsLocalization.Market.UseOptions, false);
            CreateParameterBoolean(OsLocalization.Market.UseOther, false);
            CreateParameterBoolean(OsLocalization.Market.UseSecInfoUpdates, false);
            CreateParameterButton(OsLocalization.Market.ButtonNameChangePassword);

            ServerParameters[4].Comment = OsLocalization.Market.Label107;
            ServerParameters[5].Comment = OsLocalization.Market.Label107;
            ServerParameters[6].Comment = OsLocalization.Market.Label107;
            ServerParameters[7].Comment = OsLocalization.Market.Label107;
            ServerParameters[8].Comment = OsLocalization.Market.Label107;
            ServerParameters[9].Comment = OsLocalization.Market.Label108;
            ServerParameters[10].Comment = OsLocalization.Market.Label105;

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

        private bool _useStock = false;
        private bool _useFutures = false;
        private bool _useOptions = false;
        private bool _useCurrency = false;
        private bool _useOther = false;


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

            _useStock = ((ServerParameterBool)ServerParameters[4]).Value;
            _useFutures = ((ServerParameterBool)ServerParameters[5]).Value;
            _useCurrency = ((ServerParameterBool)ServerParameters[6]).Value;
            _useOptions = ((ServerParameterBool)ServerParameters[7]).Value;
            _useOther = ((ServerParameterBool)ServerParameters[8]).Value;
            var useSecUpdates = ((ServerParameterBool)ServerParameters[9]).Value;
            var btn = ((ServerParameterButton)ServerParameters[10]);
            btn.UserClickButton += () => { ButtonClickChangePasswordWindowShow(); };

            _client.Connected += _client_Connected;
            _client.Disconnected += _client_Disconnected;
            _client.LogMessageEvent += SendLogMessage;
            _client.UpdatePairs += ClientOnUpdatePairs;
            _client.ClientsInfo += ClientsInfoUpdate;
            _client.UpdatePortfolio += ClientOnUpdatePortfolio;
            _client.UpdatePositions += ClientOnUpdatePositions;
            _client.UpdateClientLimits += ClientOnUpdateLimits;
            _client.NewTradesEvent += ClientOnNewTradesEvent;
            _client.UpdateMarketDepth += ClientOnUpdateMarketDepth;
            _client.MyOrderEvent += ClientOnMyOrderEvent;
            _client.MyTradeEvent += ClientOnMyTradeEvent;
            _client.NewCandles += ClientOnNewCandles;
            _client.NeedChangePassword += NeedChangePassword;
            _client.NewTicks += _client_NewTicks;

            _client.Connect(useSecUpdates);

            _cancellationTokenSource = new CancellationTokenSource();

            _cancellationToken = _cancellationTokenSource.Token;

            Task.Run(new Action(SessionTimeHandler), _cancellationToken);
        }

        private void ButtonClickChangePasswordWindowShow()
        {
            ChangeTransaqPassword changeTransaqPassword = new ChangeTransaqPassword(this);
            changeTransaqPassword.ShowDialog();
        }

        private void _client_NewTicks(List<Tick> ticks)
        {
            _allTicks.AddRange(ticks);
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

        public void ChangePassword(string oldPassword, string newPassword, ChangeTransaqPassword window)
        {
            try
            {
                if (_client == null)
                {
                    window.TextInfo.Text = OsLocalization.Market.Label102;
                    return;
                }

                string cmd = $"<command id=\"change_pass\" oldpass=\"{oldPassword}\" newpass=\"{newPassword}\"/>";

                // sending command / отправка команды
                string res = _client.ConnectorSendCommand(cmd);

                if (res == "<result success=\"true\"/>")
                {
                    ((ServerParameterPassword)ServerParameters[1]).Value = newPassword;
                    window.TextInfo.Text = OsLocalization.Market.Label103;
                }
                else
                {
                    window.TextInfo.Text = res;
                }

                Dispose();
            }
            catch (Exception ex)
            {
                window.TextInfo.Text = ex.ToString();
            }
        }

        private readonly ServerWorkingTimeSettings _workingTimeSettings;

        public bool ServerInWork = true;

        private void SessionTimeHandler()
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                var serverCurrentTime = TimeManager.GetExchangeTime(_workingTimeSettings.ServerTimeZone);

                if ((!_workingTimeSettings.WorkingAtWeekend && serverCurrentTime.DayOfWeek == (DayOfWeek.Saturday | DayOfWeek.Sunday)) ||
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
                _client.UpdatePositions -= ClientOnUpdatePositions;
                _client.UpdateClientLimits -= ClientOnUpdateLimits;
                _client.NewTradesEvent -= ClientOnNewTradesEvent;
                _client.UpdateMarketDepth -= ClientOnUpdateMarketDepth;
                _client.MyOrderEvent -= ClientOnMyOrderEvent;
                _client.MyTradeEvent -= ClientOnMyTradeEvent;
                _client.NewCandles -= ClientOnNewCandles;
                _client.NeedChangePassword -= NeedChangePassword;
            }

            _depths?.Clear();

            _depths = null;

            _allCandleSeries?.Clear();

            _cancellationTokenSource?.Cancel();

            _transaqSecurities = new ConcurrentQueue<string>();

            _portfoliosHandlerTask = null;

            _securities = new List<Security>();

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
            if (_clients == null || _clients.Count == 0)
            {
                return;
            }

            if (_portfoliosHandlerTask != null)
            {
                return;
            }

            _portfoliosHandlerTask = Task.Run(new Action(CycleGettingPortfolios), _cancellationToken);

        }

        private void CycleGettingPortfolios()
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                Thread.Sleep(3000);
                if (ServerInWork == false)
                {
                    continue;
                }

                if (_client == null)
                {
                    continue;
                }

                if (!_client.IsConnected)
                {
                    continue;
                }

                if (_clients == null || _clients.Count == 0)
                {
                    continue;
                }

                foreach (var client in _clients)
                {
                    string command;
                    if (client.Type == "mct")
                    {
                        command = $"<command id=\"get_portfolio_mct\" client=\"{client.Id}\"/>";

                        string res = _client.ConnectorSendCommand(command);

                        if (res != "<result success=\"true\"/>")
                        {
                            SendLogMessage(res, LogMessageType.Error);
                            Thread.Sleep(5000);
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(client.Union))
                        {
                            command = $"<command id=\"get_mc_portfolio\" union=\"{client.Union}\" />";
                        }
                        else
                        {
                            command = $"<command id=\"get_mc_portfolio\" client=\"{client.Id}\" />";
                        }

                        string res = _client.ConnectorSendCommand(command);

                        if (res != "<result success=\"true\"/>")
                        {
                            SendLogMessage(res, LogMessageType.Error);
                            Thread.Sleep(5000);
                        }
                    }

                    if(string.IsNullOrEmpty(client.Union) && !string.IsNullOrEmpty(client.Forts_acc))
                    {
                        command = $"<command id=\"get_client_limits\" client=\"{client.Id}\"/>";
                        string res = _client.ConnectorSendCommand(command);

                        if (res != "<result success=\"true\"/>")
                        {
                            SendLogMessage(res, LogMessageType.Error);
                            Thread.Sleep(5000);
                        }
                    }

                    Thread.Sleep(500);
                }
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

            Security needSec = _securities.Find(
                s => s.Name == order.SecurityNameCode &&
                s.NameClass == order.SecurityClassCode);

            if(needSec == null)
            {
                needSec = _securities.Find(
                s => s.Name == order.SecurityNameCode);
            }

            string cmd = "<command id=\"neworder\">";
            cmd += "<security>";
            cmd += "<board>" + needSec.NameClass + "</board>";
            cmd += "<seccode>" + needSec.Name + "</seccode>";
            cmd += "</security>";

            if (order.PortfolioNumber.StartsWith("United_"))
            {
                var union = order.PortfolioNumber.Split('_')[1];
                cmd += "<union>" + union + "</union>";
            }
            else
            {
                cmd += "<client>" + order.PortfolioNumber + "</client>";
            }

            cmd += "<price>" + order.Price.ToString().Replace(',', '.') + "</price>";
            cmd += "<quantity>" + order.Volume + "</quantity>";
            cmd += "<buysell>" + side + "</buysell>";
            cmd += "<brokerref>" + order.NumberUser + "</brokerref>";
            cmd += "<unfilled> PutInQueue </unfilled>";

            if (needSec.NameClass == "TQBR")
            {
                cmd += "<usecredit> true </usecredit>";
            }
            
            cmd += "</command>";

            // sending command / отправка команды
            string res = _client.ConnectorSendCommand(cmd);

            var result = _client.Deserialize<Result>(res);

            if (!result.Success)
            {
                order.State = OrderStateType.Fail;
                SendLogMessage("SendOrderFall" + result.Message, LogMessageType.Error);
            }
            else
            {
                order.NumberUser = result.TransactionId;
                order.State = OrderStateType.Activ;
            }

            order.TimeCallBack = ServerTime;

            MyOrderEvent?.Invoke(order);
        }

        /// <summary>
        /// Order price change
        /// </summary>
        /// <param name="order">An order that will have a new price</param>
        /// <param name="newPrice">New price</param>
        public void ChangeOrderPrice(Order order, decimal newPrice)
        {

        }

        /// <summary>
        /// cancel order
        /// отменить ордер
        /// </summary>
        public void CancelOrder(Order order)
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
        /// cancel all orders from trading system
        /// отозвать все ордера из торговой системы
        /// </summary>
        public void CancelAllOrders()
        {

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
            List<Trade> trades = new List<Trade>();

            string cmd = "<command id=\"subscribe_ticks\">";
            cmd += "<security>";
            cmd += "<board>" + security.NameClass + "</board>";
            cmd += "<seccode>" + security.Name + "</seccode>";
            cmd += "<tradeno>1</tradeno>";
            cmd += "</security>";
            cmd += "<filter>true</filter>";
            cmd += "</command>";

            // sending command / Ð¾Ñ‚Ð¿Ñ€Ð°Ð²ÐºÐ° ÐºÐ¾Ð¼Ð°Ð½Ð´Ñ‹
            string res = _client.ConnectorSendCommand(cmd);

            if (res != "<result success=\"true\"/>")
            {
                SendLogMessage(res, LogMessageType.Error);
                return null;
            }

            DateTime lastTickTime = DateTime.MinValue;
            List<Tick> ticks = new List<Tick>();

            while (true)
            {

                try
                {
                    ticks = _allTicks.Where(x => x.Seccode == security.Name).ToList();
                    if (ticks.Count > 0)
                    {
                        lastTickTime = DateTime.Parse(ticks.Last().Tradetime);
                    }
                }
                catch
                {

                }

                if (lastTickTime >= actualTime)
                {
                    foreach (var tick in ticks)
                    {
                        trades.Add(new Trade()
                        {
                            SecurityNameCode = tick.Seccode,
                            Id = tick.Tradeno,
                            Price = Convert.ToDecimal(tick.Price.Replace(".", ",")),
                            Side = tick.Buysell == "B" ? Side.Buy : Side.Sell,
                            Volume = Convert.ToDecimal(tick.Quantity.Replace(".", ",")),
                            Time = DateTime.Parse(tick.Tradetime),
                        });
                    }

                    //Ð¾Ñ‚Ð¿Ð¸ÑÑ‹Ð²Ð°ÐµÐ¼ÑÑ
                    string cmd_uns = "<command id=\"subscribe_ticks\">";
                    cmd_uns += "<filter>true</filter>";
                    cmd_uns += "</command>";

                    string res2 = _client.ConnectorSendCommand(cmd_uns);

                    //Ð—Ð°Ñ‡Ð¸ÑÑ‚Ð¸Ð¼
                    _allTicks.RemoveAll(x => x.Seccode == security.Name);

                    if (res2 != "<result success=\"true\"/>")
                    {
                        SendLogMessage(res2, LogMessageType.Error);
                    }

                    break;
                }

                Thread.Sleep(300);
            }

            return trades;

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
            Task.Run(() => GetCandles(series), _cancellationToken);
        }

        private void GetCandles(CandleSeries series)
        {
            Security security = series.Security;
            TimeFrame tf = series.TimeFrame;

            int newTf;
            int oldTf;
            string needPeriodId = GetNeedIdPeriod(tf, out newTf, out oldTf);

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

                Thread.Sleep(500);
            }

            SendLogMessage(OsLocalization.Market.Message95 + security.Name, LogMessageType.Error);
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
                    if (needTf <=60 && newCandle.TimeStart.Minute % needTf !=0)  //AVP, если свечка пришла в некратное ТФ время, например, был пропуск свечи, то ТФ правим на кратное. на MOEX  в пропущенные на клиринге свечках, на 10 минутках давало сбой - сдвиг свечек на 5 минут.
                    {
                        newCandle.TimeStart = newCandle.TimeStart.AddMinutes((newCandle.TimeStart.Minute % needTf) * -1);        
                    }
                    newCandle.Low = Decimal.MaxValue;
                }

                newCandle.High = oldCandles[i].High > newCandle.High
                    ? oldCandles[i].High
                    : newCandle.High;

                newCandle.Low = oldCandles[i].Low < newCandle.Low
                    ? oldCandles[i].Low
                    : newCandle.Low;

                newCandle.Volume += oldCandles[i].Volume;

               

                if (counter == count || (needTf <= 60 && i < oldCandles.Count-2 && oldCandles[i+1].TimeStart.Minute % needTf == 0) )    // AVP добавил проверку "или", что следующая свечка в мелком ТФ, должна войти в следующую свечу более крупного ТФ
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
                case TimeFrame.Hour4:
                    newTf = 240;
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
            CreateSecurities();
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

            if (!string.IsNullOrEmpty(clientInfo.Union))
            {
                var needClient = _clients.Find(c => string.IsNullOrEmpty(clientInfo.Union));

                if (needClient == null)
                {
                    _clients.Add(clientInfo);
                }
            }
            else
            {
                _clients.Add(clientInfo);
            }
        }

        /// <summary>
        /// got portfolio data
        /// пришли данные по портфелям
        /// </summary>
        /// <param name="portfolio">portfolio / портфель </param>
        private void ClientOnUpdatePortfolio(string portfolio)
        {
            if (_portfolios == null)
            {
                _portfolios = new List<Portfolio>();
            }
            
            var unitedPortfolio = ParsePortfolio(portfolio);

            var needPortfolio = _portfolios.Find(p => p.Number == unitedPortfolio.Number);

            if (needPortfolio != null)
            {
                _portfolios.Remove(needPortfolio);
            }
            _portfolios.Add(unitedPortfolio);
            
            PortfolioEvent?.Invoke(_portfolios);
        }

        private Portfolio ParsePortfolio(string data)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(data);

            XmlElement root = doc.DocumentElement;

            if (root == null)
            {
                return null;
            }

            var union = root.GetAttribute("union");
            var client = root.GetAttribute("client");

            var openEquity = root.SelectSingleNode("open_equity");
            var equity = root.SelectSingleNode("equity");
            var block = root.SelectSingleNode("maint_req");

            var allSecurity = root.GetElementsByTagName("security");

            var portfolio = new Portfolio();

            if (openEquity != null) portfolio.ValueBegin = openEquity.InnerText.ToDecimal();
            if (equity != null) portfolio.ValueCurrent = equity.InnerText.ToDecimal();
            if (block != null) portfolio.ValueBlocked = block.InnerText.ToDecimal();

            if (!string.IsNullOrEmpty(union))
            {
                portfolio.Number = "United_" + union;
            }
            else
            {
                portfolio.Number = client;
            }

            foreach (var security in allSecurity)
            {
                var node = (XmlNode) security;

                var pos = new PositionOnBoard();

                pos.SecurityNameCode = node.SelectSingleNode("seccode")?.InnerText;
                pos.PortfolioName = portfolio.Number;

                var begin = node.SelectSingleNode("open_balance")?.InnerText.ToDecimal();
                var buy = node.SelectSingleNode("bought")?.InnerText.ToDecimal();
                var sell = node.SelectSingleNode("sold")?.InnerText.ToDecimal();

                pos.ValueBegin = Convert.ToDecimal(begin);
                pos.ValueCurrent = pos.ValueBegin + Convert.ToDecimal(buy - sell);

                portfolio.SetNewPosition(pos);
            }

            return portfolio;
        }

        /// <summary>
        /// got portfolio data
        /// пришли данные по портфелям
        /// </summary>
        private void ClientOnUpdateLimits(ClientLimits clientLimits)
        {
            if (_portfolios == null)
            {
                return;
            }

            var needPortfolio = _portfolios.Find(p => p.Number == clientLimits.Client);

            if (needPortfolio != null)
            {
                InitPortfolio(needPortfolio, clientLimits);
            }

            PortfolioEvent?.Invoke(_portfolios);
        }

        private Portfolio InitPortfolio(Portfolio portfolio, ClientLimits clientLimits)
        {
            portfolio.ValueBegin = clientLimits.MoneyCurrent.ToDecimal();
            portfolio.ValueCurrent = clientLimits.MoneyFree.ToDecimal();
            portfolio.ValueBlocked = clientLimits.MoneyReserve.ToDecimal();
            portfolio.Profit = clientLimits.Profit.ToDecimal();

            return portfolio;
        }

        private decimal _blocked = 0;

        /// <summary>
        /// updated position
        /// обновились позиции
        /// </summary>
        private void ClientOnUpdatePositions(TransaqPositions transaqPositions)
        {
            if (_portfolios == null)
            {
                _portfolios = new List<Portfolio>();
            }

            if (transaqPositions.Forts_money != null)
            {
                _blocked = Convert.ToDecimal(transaqPositions.Forts_money.Blocked.Replace(".", ","));
            }

            if (transaqPositions.Forts_position.Count == 0)
            {
                foreach (var portfolio in _portfolios)
                {
                    portfolio.ClearPositionOnBoard();
                }
            }
            else
            {
                foreach (var fortsPosition in transaqPositions.Forts_position)
                {
                    var needPortfolio = _portfolios.Find(p => p.Number == fortsPosition.Client);

                    if (needPortfolio != null)
                    {
                        PositionOnBoard pos = new PositionOnBoard()
                        {
                            SecurityNameCode = fortsPosition.Seccode,
                            ValueBegin = Convert.ToDecimal(fortsPosition.Startnet.Replace(".", ",")),
                            ValueCurrent = Convert.ToDecimal(fortsPosition.Totalnet.Replace(".", ",")),
                            ValueBlocked = Convert.ToDecimal(fortsPosition.Openbuys.Replace(".", ",")) +
                                           Convert.ToDecimal(fortsPosition.Opensells.Replace(".", ",")),
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

        private ConcurrentQueue<string> _transaqSecurities = new ConcurrentQueue<string>();

        /// <summary>
        /// got instruments
        /// пришли инструменты
        /// </summary>
        /// <param name="securities">list of instrument in transaq format / список инструментов в формате transaq</param>
        private void ClientOnUpdatePairs(string securities)
        {
            _transaqSecurities.Enqueue(securities);
            _lastUpdateSecurityArrayTime = DateTime.Now;
        }

        private DateTime _lastUpdateSecurityArrayTime;

        private void CreateSecurities()
        {
            while(true)
            {
                Thread.Sleep(500);
                if (_lastUpdateSecurityArrayTime ==  DateTime.MinValue)
                {
                    continue;
                }
                if(_lastUpdateSecurityArrayTime.AddSeconds(5) >  DateTime.Now)
                {
                    continue;
                }

                break;
            }

            DateTime timeStart = DateTime.Now;          

            while(_transaqSecurities.IsEmpty == false)
            {
                string curArray = null;
                
                if(_transaqSecurities.TryDequeue(out curArray))
                {
                    CreateSecurities(curArray);
                }
            }

            _securities.RemoveAll(s => s == null);
            SecurityEvent?.Invoke(_securities);

            TimeSpan timeOnWork =  DateTime.Now - timeStart;

            SendLogMessage("Time securities add: " + timeOnWork.ToString(), LogMessageType.System);
            SendLogMessage("Securities count: " + _securities.Count, LogMessageType.System);
        }

        private void CreateSecurities(string data)
        {
            List<TransaqEntity.Security> transaqSecurities = _client.DeserializeSecurities(data);

            foreach (TransaqEntity.Security securityData in transaqSecurities)
            {
                try
                {
                    if (!CheckFilter(securityData))
                    {
                        continue;
                    }

                    Security security = new Security();

                    security.Name = securityData.Seccode;
                    security.NameFull = securityData.Shortname;
                    security.NameClass = securityData.Board;
                    security.NameId = securityData.Secid;
                    security.Decimals = Convert.ToInt32(securityData.Decimals);

                    security.SecurityType = securityData.Sectype == "FUT" ? SecurityType.Futures
                        : securityData.Sectype == "SHARE" ? SecurityType.Stock
                        : securityData.Sectype == "OPT" ? SecurityType.Option
                        : securityData.Sectype == "BOND" ? SecurityType.Bond
                        : securityData.Sectype == "CURRENCY" || securityData.Sectype == "CETS" ? SecurityType.CurrencyPair
                        : SecurityType.None;

                    if (security.NameClass == "MCT"
                        && security.SecurityType == SecurityType.None
                        && (security.NameFull.Contains("call") || security.NameFull.Contains("put")))
                    {
                        security.NameClass = "MCT_put_call";
                    }

                    security.Lot = securityData.Lotsize.ToDecimal();

                    security.PriceStep = securityData.Minstep.ToDecimal();

                    decimal pointCost;
                    try
                    {
                        pointCost = securityData.Point_cost.ToDecimal();
                    }
                    catch (Exception e)
                    {
                        decimal.TryParse(securityData.Point_cost, NumberStyles.Float, CultureInfo.InvariantCulture, out pointCost);
                    }

                    if (security.PriceStep > 1)
                    {
                        security.PriceStepCost = security.PriceStep * pointCost / 100;
                    }
                    else
                    {
                        security.PriceStepCost = pointCost / 100;
                    }

                    security.State = securityData.Active == "true" ? SecurityStateType.Activ : SecurityStateType.Close;

                    if (_securities.Contains(security))
                    {
                        continue;
                    }

                    _securities.Add(security);
                }
                catch (Exception e)
                {
                    SendLogMessage(e.Message, LogMessageType.Error);
                }
            }
        }

        private readonly object _locker = new object();

        private bool CheckFilter(TransaqEntity.Security security)
        {
            lock (_locker)
            {
                if (security.Sectype == "SHARE")
                {
                    if (_useStock)
                    {
                        return true;
                    }
                    return false;
                }
                if (security.Sectype == "FUT")
                {
                    if (_useFutures)
                    {
                        return true;
                    }
                    return false;
                }
                if (security.Sectype == "OPT")
                {
                    if (_useOptions)
                    {
                        return true;
                    }
                    return false;
                }
                if (security.Sectype == "CETS" || security.Sectype == "CURRENCY")
                {
                    if (_useCurrency)
                    {
                        return true;
                    }
                    return false;
                }
                if (_useOther)
                {
                    return true;
                }

                return false;
            }
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
                        Id = t.Tradeno,
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
                        //continue;
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
                    newOrder.PortfolioNumber = string.IsNullOrEmpty(order.Union) ? order.Client : order.Union;

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

                    MyOrderEvent?.Invoke(newOrder);
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
                myTrade.Volume = trade.Quantity.ToDecimal();
                myTrade.Price = trade.Price.ToDecimal();
                myTrade.SecurityNameCode = trade.Seccode;
                myTrade.Side = trade.Buysell == "B" ? Side.Buy : Side.Sell;

                MyTradeEvent?.Invoke(myTrade);
            }
        }

        private List<Candles> _allCandleSeries = new List<Candles>();

        private List<Tick> _allTicks = new List<Tick>();

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

        public void ResearchTradesToOrders(List<Order> orders)
        {

        }

        public void CancelAllOrdersToSecurity(Security security)
        {

        }

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// outgoing lom message
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }
}
