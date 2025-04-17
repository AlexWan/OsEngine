using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using System;
using System.Collections.Generic;
using System.Threading;
using Td = Telegram.Td;
using TdApi = Telegram.Td.Api;
using Telegram.Td.Api;
using WebSocketSharp;
using System.Collections.Concurrent;
using OsEngine.Market.Servers.TelegramNews.TGAuthEntity;

namespace OsEngine.Market.Servers.TelegramNews
{
    class TelegramNewsServer : AServer
    {
        public TelegramNewsServer()
        {
            TelegramNewsServerRealization realization = new TelegramNewsServerRealization();
            ServerRealization = realization;

            CreateParameterString("Telegram channel IDs", "");
            CreateParameterInt("API ID", 0);
            CreateParameterString("API Hash", "");
            CreateParameterString("Phone number", "");
        }
    }

    public class TelegramNewsServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public TelegramNewsServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            // disable TDLib log
            Td.Client.Execute(new TdApi.SetLogVerbosityLevel(0));
            if (Td.Client.Execute(new TdApi.SetLogStream(new TdApi.LogStreamFile(@"Engine\Log\" + "tdlib.log", 1 << 27, false))) is TdApi.Error)
            {
                SendLogMessage("Write access to the current directory is required", LogMessageType.Error);
            }

            Thread threadForTDLibWork = new Thread(Td.Client.Run);
            threadForTDLibWork.IsBackground = true;
            threadForTDLibWork.Start();

            Thread threadForTGMessageReading = new Thread(TGChannelsReader);
            threadForTGMessageReading.IsBackground = true;
            threadForTGMessageReading.Name = "TGReader";
            threadForTGMessageReading.Start();
        }

        public DateTime ServerTime { get; set; }

        public void Connect()
        {
            _channelsIDsString = ((ServerParameterString)ServerParameters[0]).Value;
            _apiID = ((ServerParameterInt)ServerParameters[1]).Value;
            _apiHash = ((ServerParameterString)ServerParameters[2]).Value;
            _phoneNumber = ((ServerParameterString)ServerParameters[3]).Value;

            if (string.IsNullOrEmpty(_channelsIDsString))
            {
                SendLogMessage("Can`t run connector. ID Telegram channel not are specified", LogMessageType.Error);
                return;
            }

            if (_apiID <= 0)
            {
                SendLogMessage("Can`t run connector. Enter API ID.", LogMessageType.Error);
                return;
            }

            if (string.IsNullOrEmpty(_apiHash))
            {
                SendLogMessage("Can`t run connector. API Hash not are specified", LogMessageType.Error);
                return;
            }

            if (string.IsNullOrEmpty(_phoneNumber))
            {
                SendLogMessage("Can`t run connector. Phone number not are specified", LogMessageType.Error);
                return;
            }

            string[] tgChannelsIDs = _channelsIDsString.Split(',');

            for (int i = 0; i < tgChannelsIDs.Length; i++)
            {
                try
                {
                    long id = Convert.ToInt64(tgChannelsIDs[i]);
                    _trackedChats.Add(id, "");
                }
                catch (Exception ex)
                {
                    SendLogMessage("Incorrect format for the channel ID " + tgChannelsIDs[i], LogMessageType.Error);
                    SendLogMessage(ex.Message.ToString(), LogMessageType.Error);
                }
            }

            if (_trackedChats.Count == 0)
            {
                SendLogMessage("There is not a single channel to download", LogMessageType.Error);
                return;
            }

            _client = Td.Client.Create(new UpdateHandler());

            // процесс авторизации
            while (ServerStatus == ServerConnectStatus.Disconnect)
            {
                // await authorization
                _gotAuthorization.Reset();
                _gotAuthorization.WaitOne();

                if (_haveAuthorization == true)
                {
                    ServerStatus = ServerConnectStatus.Connect;
                    ConnectEvent();

                    break;
                }
            }
        }

        public void Dispose()
        {
            _trackedChats.Clear();
            _telegramMessages = new ConcurrentQueue<TdApi.Message>();
            _telegramStartMsg = new ConcurrentQueue<TdApi.Message>();

            _haveAuthorization = false;

            if (_client != null && _defaultHandler != null)
            {
                _client.Send(new TdApi.Close(), _defaultHandler);
            }

            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        public static void OnAuthorizationStateUpdated(TdApi.AuthorizationState authorizationState)
        {
            if (authorizationState != null)
            {
                _authorizationState = authorizationState;
            }
            if (_authorizationState is TdApi.AuthorizationStateWaitTdlibParameters)
            {
                TdApi.SetTdlibParameters request = new TdApi.SetTdlibParameters();
                request.DatabaseDirectory = @"Engine\Log\tdlib";
                request.UseMessageDatabase = false;
                request.UseSecretChats = false;
                request.ApiId = _apiID;
                request.ApiHash = _apiHash;
                request.SystemLanguageCode = "en";
                request.DeviceModel = "Desktop";
                request.ApplicationVersion = "1.0";

                _client.Send(request, new AuthorizationRequestHandler());
            }
            else if (_authorizationState is TdApi.AuthorizationStateWaitPhoneNumber)
            {
                _client.Send(new TdApi.SetAuthenticationPhoneNumber(_phoneNumber, null), new AuthorizationRequestHandler());
            }
            else if (_authorizationState is TdApi.AuthorizationStateWaitOtherDeviceConfirmation state)
            {
                ServerMaster.SendNewLogMessage("Please confirm this login link on another device: " + state.Link, LogMessageType.Error);
            }
            else if (_authorizationState is TdApi.AuthorizationStateWaitCode)
            {
                ServerMaster.SendNewLogMessage("Authorization state is wait code", LogMessageType.Connect);

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    AuthTGCodeDialogUi dialog = new AuthTGCodeDialogUi();

                    if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.VerificationCode))
                    {
                        _client.Send(new TdApi.CheckAuthenticationCode(dialog.VerificationCode), new AuthorizationRequestHandler());
                    }
                    else
                    {
                        // Отмена авторизации
                        _haveAuthorization = false;

                        if (_client != null && _defaultHandler != null)
                            _client.Send(new TdApi.Close(), _defaultHandler);
                    }
                });
            }
            else if (_authorizationState is TdApi.AuthorizationStateWaitPassword)
            {
                ServerMaster.SendNewLogMessage("Authorization state is wait password", LogMessageType.Connect);

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    AuthTGPasswordDialogUi dialog = new AuthTGPasswordDialogUi();

                    if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.Password))
                    {
                        _client.Send(new TdApi.CheckAuthenticationPassword(dialog.Password), new AuthorizationRequestHandler());
                    }
                    else
                    {
                        // Отмена авторизации
                        _haveAuthorization = false;

                        if (_client != null && _defaultHandler != null)
                            _client.Send(new TdApi.Close(), _defaultHandler);
                    }
                });
            }
            else if (_authorizationState is TdApi.AuthorizationStateReady)
            {
                ServerMaster.SendNewLogMessage("Authorization state is ready", LogMessageType.Connect);

                _haveAuthorization = true;
                _gotAuthorization.Set();

            }
            else if (_authorizationState is TdApi.AuthorizationStateLoggingOut)
            {
                _haveAuthorization = false;

                ServerMaster.SendNewLogMessage("Authorization logging out! Re-authentication is required!", LogMessageType.Error);
            }
            else if (_authorizationState is TdApi.AuthorizationStateClosing)
            {
                _haveAuthorization = false;

                ServerMaster.SendNewLogMessage("Authorization Closing...", LogMessageType.Connect);
            }
            else if (_authorizationState is TdApi.AuthorizationStateClosed)
            {
                ServerMaster.SendNewLogMessage("Authorization closed", LogMessageType.Connect);
            }
            else
            {
                ServerMaster.SendNewLogMessage("Unsupported authorization state!", LogMessageType.Error);
            }
        }

        public ServerType ServerType
        {
            get { return ServerType.TelegramNews; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public event Action ConnectEvent;
        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        private string _channelsIDsString;
        private static int _apiID;
        private static string _apiHash;
        private static string _phoneNumber;

        private bool _newsIsSubscribed;
        private static bool _allChatTitlesDownloaded;

        private static Td.Client _client = null;
        private readonly static Td.ClientResultHandler _defaultHandler = new DefaultHandler();
        private static TdApi.AuthorizationState _authorizationState = null;
        private static volatile bool _haveAuthorization = false;
        private static volatile AutoResetEvent _gotAuthorization = new AutoResetEvent(false);

        private static Dictionary<long, string> _trackedChats = new Dictionary<long, string>();
        private static ConcurrentQueue<TdApi.Message> _telegramMessages = new ConcurrentQueue<TdApi.Message>();
        private static ConcurrentQueue<TdApi.Message> _telegramStartMsg = new ConcurrentQueue<TdApi.Message>();

        #endregion

        #region 3 News subscrible

        public bool SubscribeNews()
        {
            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                return false;
            }

            _newsIsSubscribed = true;

            return true;
        }

        #endregion

        #region 4 Messages parsing

        private void TGChannelsReader()
        {
            while (true)
            {
                try
                {
                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        Thread.Sleep(3000);
                        continue;
                    }

                    // если слетела авторизация после коннекта
                    if (!_haveAuthorization)
                    {
                        if (ServerStatus != ServerConnectStatus.Disconnect)
                        {
                            ServerStatus = ServerConnectStatus.Disconnect;
                            DisconnectEvent();

                            Thread.Sleep(100);
                            continue;
                        }
                    }

                    if (!_newsIsSubscribed)
                    {
                        Thread.Sleep(3000);
                        continue;
                    }

                    if (!_allChatTitlesDownloaded)
                    {
                        TdApi.Message msgS;

                        _telegramStartMsg.TryDequeue(out msgS);

                        if (msgS == null)
                            continue;

                        CheckChannelTitles(msgS);
                        Thread.Sleep(100);
                    }

                    TdApi.Message msg;

                    _telegramMessages.TryDequeue(out msg);

                    if (msg == null)
                    {
                        continue;
                    }

                    ProcessIncomingMessage(msg);

                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.Message.ToString(), LogMessageType.Error);
                    SendLogMessage("Error receiving Telegram messages", LogMessageType.Error);

                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                    }
                }
            }
        }

        private void CheckChannelTitles(TdApi.Message message)
        {
            long chatId = message.ChatId;

            if (_trackedChats.ContainsKey(chatId) && _trackedChats[chatId] == "")
            {
                _client.Send(new GetChat(chatId), _defaultHandler);
            }

            var enumtor = _trackedChats.GetEnumerator();

            int fillValuesCount = _trackedChats.Count;

            while (enumtor.MoveNext())
            {
                if (enumtor.Current.Value == "")
                {
                    fillValuesCount--;
                }
            }

            if (fillValuesCount == _trackedChats.Count)
            {
                _allChatTitlesDownloaded = true;
            }
        }

        // Обрабатываем входящее сообщение
        private void ProcessIncomingMessage(TdApi.Message message)
        {
            long chatId = message.ChatId;

            if (_trackedChats.TryGetValue(chatId, out string chatTitle))
            {
                string messageText = ExtractMessageText(message.Content);
                string source = chatTitle.IsNullOrEmpty() ? chatId.ToString() : chatTitle;

                News news = new News();

                news.Source = source;
                news.Value = messageText;
                news.TimeMessage = DateTimeOffset.FromUnixTimeSeconds(message.Date).ToLocalTime().DateTime;

                NewsEvent(news);
            }
        }

        // Извлекаем текст из разных типов сообщений
        private string ExtractMessageText(MessageContent content)
        {
            if (content is TdApi.MessageText messageText)
            {
                return messageText.Text.Text;
            }
            else if (content is TdApi.MessagePhoto photo)
            {
                if (photo.Caption != null)
                    return photo.Caption.Text.IsNullOrEmpty() ? "[фото без подписи]" : photo.Caption.Text;
                else
                    return "[фото без подписи]";
            }
            else if (content is TdApi.MessageDocument doc)
            {
                if (doc.Caption != null)
                    return doc.Caption.Text.IsNullOrEmpty() ? "[документ без описания]" : doc.Caption.Text;
                else
                    return "[документ без описания]";
            }
            else if (content is TdApi.MessageVideo video)
            {
                if (video.Caption != null)
                    return video.Caption.Text.IsNullOrEmpty() ? "[видео без подписи]" : video.Caption.Text;
                else
                    return "[видео без подписи]";
            }
            else
            {
                return $"[сообщение типа {content.GetType().Name}]";
            }
        }

        public event Action<News> NewsEvent;

        #endregion

        #region 5 Log

        public event Action<string, LogMessageType> LogMessageEvent;

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent(message, messageType);
        }

        #endregion

        #region 6 Not used functions

        public void CancelAllOrders()
        {
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
        }

        public void CancelOrder(Order order)
        {
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
        }

        public void Subscrible(Security security)
        {
        }

        public void GetAllActivOrders()
        {
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            return null;
        }

        public void GetOrderStatus(Order order)
        {
        }

        public void GetPortfolios()
        {
            Portfolio portfolio = new Portfolio();
            portfolio.ValueCurrent = 1;
            portfolio.Number = "TelegramNews fake portfolio";

            if (PortfolioEvent != null)
            {
                PortfolioEvent(new List<Portfolio>() { portfolio });
            }
        }

        public void GetSecurities()
        {
            List<Security> securities = new List<Security>();

            Security fakeSec = new Security();
            fakeSec.Name = "Noname";
            fakeSec.NameId = "NonameId";
            fakeSec.NameClass = "NoClass";
            fakeSec.NameFull = "Nonamefull";

            securities.Add(fakeSec);

            SecurityEvent(securities);
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        public void SendOrder(Order order)
        {
        }

        public event Action<List<Security>> SecurityEvent;
        public event Action<List<Portfolio>> PortfolioEvent;
        public event Action<MarketDepth> MarketDepthEvent;
        public event Action<Trade> NewTradesEvent;
        public event Action<Order> MyOrderEvent;
        public event Action<MyTrade> MyTradeEvent;
        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent;

        #endregion

        #region 7 Handlers

        public class DefaultHandler : Td.ClientResultHandler
        {
            void Td.ClientResultHandler.OnResult(TdApi.BaseObject @object)
            {
                if (@object is Td.Api.Chat chat)
                {
                    _trackedChats[chat.Id] = chat.Title;
                }
                else if (@object is Error error)
                {
                    ServerMaster.SendNewLogMessage("Error receiving the chat:\n" + error.Message, LogMessageType.NoName);
                }
            }
        }

        public class UpdateHandler : Td.ClientResultHandler
        {
            private string _queueLockerTGUpdates = "_queueLockerTGUpdates";

            void Td.ClientResultHandler.OnResult(TdApi.BaseObject @object)
            {
                if (@object is TdApi.UpdateAuthorizationState)
                {
                    OnAuthorizationStateUpdated((@object as TdApi.UpdateAuthorizationState).AuthorizationState);
                }
                else if (@object is TdApi.UpdateNewMessage newMessage)
                {
                    lock (_queueLockerTGUpdates)
                    {
                        if (!_allChatTitlesDownloaded)
                            _telegramStartMsg.Enqueue(newMessage.Message);

                        _telegramMessages.Enqueue(newMessage.Message);
                    }
                }
            }
        }

        public class AuthorizationRequestHandler : Td.ClientResultHandler
        {
            void Td.ClientResultHandler.OnResult(TdApi.BaseObject @object)
            {
                if (@object is TdApi.Error error)
                {
                    ServerMaster.SendNewLogMessage("AuthorizationRequestHandler: Receive an error:" + error.Message, LogMessageType.Error);

                    OnAuthorizationStateUpdated(null); // repeat last action
                }
                else
                {
                    // result is already received through UpdateAuthorizationState, nothing to do
                }
            }
        }
        #endregion
    }
}