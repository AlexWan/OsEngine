/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.TelegramNews.TGAuthEntity;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TL;
using Message = TL.Message;
using RpcException = TL.RpcException;


namespace OsEngine.Market.Servers.TelegramNews
{
    class TelegramNewsServer : AServer
    {
        public TelegramNewsServer()
        {
            // конструктор без uniqueNumber: сервер одиночный (IsSupports_MultipleInstances = false),
            // ServerNum остаётся 0 и ServerNameUnique = "TelegramNews" - так не ломаются файлы настроек существующих пользователей

            TelegramNewsServerRealization realization = new TelegramNewsServerRealization();
            realization.AuthInputRequestEvent += Realization_AuthInputRequestEvent;
            ServerRealization = realization;

            ServerParameterString channels = CreateParameterString(OsLocalization.Market.ServerParamTelegramChannels, "");
            channels.Comment = OsLocalization.Market.ServerParamTelegramChannelsDescription;

            ServerParameterInt apiId = CreateParameterInt(OsLocalization.Market.ServerParamTelegramApiId, 0);
            apiId.Comment = OsLocalization.Market.ServerParamTelegramApiIdDescription;

            ServerParameterPassword apiHash = CreateParameterPassword(OsLocalization.Market.ServerParamTelegramApiHash, "");
            apiHash.Comment = OsLocalization.Market.ServerParamTelegramApiHashDescription;

            ServerParameterString phone = CreateParameterString(OsLocalization.Market.ServerParamTelegramPhone, "");
            phone.Comment = OsLocalization.Market.ServerParamTelegramPhoneDescription;

            ServerParameterInt maxAge = CreateParameterInt(OsLocalization.Market.ServerParamTelegramMaxMissedNewsAge, 24);
            maxAge.Comment = OsLocalization.Market.ServerParamTelegramMaxMissedNewsAgeDescription;
        }

        private void Realization_AuthInputRequestEvent(string what)
        {
            try
            {
                TelegramNewsServerRealization realization = (TelegramNewsServerRealization)ServerRealization;

                if (System.Windows.Application.Current == null)
                {
                    ServerMaster.SendNewLogMessage("TelegramNews. Authorization input requested, but UI is not available: " + what, LogMessageType.Error);
                    realization.ProvideAuthInput(null);
                    return;
                }

                string input = null;

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (what == "verification_code")
                    {
                        AuthTGCodeDialogUi dialog = new AuthTGCodeDialogUi();

                        if (dialog.ShowDialog() == true && string.IsNullOrEmpty(dialog.VerificationCode) == false)
                        {
                            input = dialog.VerificationCode;
                        }
                    }
                    else if (what == "password")
                    {
                        AuthTGPasswordDialogUi dialog = new AuthTGPasswordDialogUi();

                        if (dialog.ShowDialog() == true && string.IsNullOrEmpty(dialog.Password) == false)
                        {
                            input = dialog.Password;
                        }
                    }
                    else
                    {
                        ServerMaster.SendNewLogMessage("TelegramNews. Unsupported authorization request: " + what, LogMessageType.Error);
                    }
                });

                realization.ProvideAuthInput(input);
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }
    }

    public class TelegramNewsServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public TelegramNewsServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Directory.CreateDirectory(_logDir);

            try
            {
                lock (_wtelegramFileLocker)
                {
                    FileInfo logFile = new FileInfo(Path.Combine(_logDir, "wteleg.log"));

                    if (logFile.Exists && logFile.Length > 1024 * 1024)
                    {
                        File.WriteAllText(logFile.FullName, "");
                    }
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }

            // хендлер статический: библиотечное свойство глобально на процесс, экземпляр захватывать нельзя
            WTelegram.Helpers.Log = WTelegramLogHandler;
            _activeInstance = this;

            // поток-ридер очереди сообщений: обработка рекламных (спонсорских) сообщений
            Thread reader = new Thread(MessageReaderThread);
            reader.Name = "TelegramNewsMessageReader";
            reader.IsBackground = true;
            reader.Start();
        }

        public DateTime ServerTime { get; set; }

        public void Connect(WebProxy proxy)
        {
            _channelsIDsString = ((ServerParameterString)ServerParameters[0]).Value;
            _apiID = ((ServerParameterInt)ServerParameters[1]).Value;
            _apiHash = ((ServerParameterPassword)ServerParameters[2]).Value;
            _phoneNumber = ((ServerParameterString)ServerParameters[3]).Value;
            _maxMissedNewsAgeHours = ((ServerParameterInt)ServerParameters[4]).Value;

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
                string rawId = tgChannelsIDs[i].Trim();
                string id = rawId;

                if (id.StartsWith(BotApiChannelPrefix))
                {
                    id = id.Substring(BotApiChannelPrefix.Length);
                }
                else if (id.StartsWith("-"))
                {
                    id = id.Substring(1);
                }

                long numericId;

                if (id.Length == 0 || long.TryParse(id, out numericId) == false)
                {
                    SendLogMessage("Wrong Telegram channel ID skipped: '" + rawId + "'", LogMessageType.Error);
                    continue;
                }

                if (_trackedChats.TryAdd(id, (null, "", false)) == false)
                {
                    SendLogMessage("Duplicate Telegram channel ID skipped: " + id, LogMessageType.System);
                }
            }

            if (_trackedChats.Count == 0)
            {
                SendLogMessage("There is not a single channel to download", LogMessageType.Error);
                return;
            }

            try
            {
                _client = new WTelegram.Client(_apiID, _apiHash, @"Engine\Log\TelegramLogs\WTelegram.session");

                if (proxy != null)
                {
                    // MTProto идёт по raw TCP, поэтому WebProxy применяем через HTTP CONNECT туннель в TcpHandler
                    _proxy = proxy;
                    _client.TcpHandler = TcpHandlerThroughProxy;
                    SendLogMessage("Proxy is enabled. Address: " + proxy.Address.Host, LogMessageType.System);
                }

                // логин идёт в фоне: ConnectEvent выстрелит по факту успешной авторизации
                Thread loginThread = new Thread(LoginWorker);
                loginThread.Name = "TelegramNewsLogin";
                loginThread.IsBackground = true;
                loginThread.Start();
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
                return;
            }
        }

        private void LoginWorker()
        {
            try
            {
                while (true) // цикл перезахода: после удаления сессии логин начинается заново
                {
                    string loginInfo = _phoneNumber;

                    try
                    {
                        while (_client.User == null)
                        {
                            string needed;

                            try
                            {
                                needed = _client.Login(loginInfo).GetAwaiter().GetResult();
                            }
                            catch (RpcException ex) when (ex.Code == 420) // FLOOD_WAIT: ждём и повторяем, а не роняем коннектор
                            {
                                int waitSeconds = ExtractWaitTime(ex.Message);
                                SendLogMessage("Flood wait on login. Waiting " + waitSeconds + " sec", LogMessageType.System);
                                Thread.Sleep(TimeSpan.FromSeconds(waitSeconds));
                                continue;
                            }

                            if (needed == null)
                            {
                                break;
                            }

                            loginInfo = WaitAuthInput(needed);

                            if (loginInfo == null)
                            {
                                // пользователь отменил ввод кода/пароля
                                SendLogMessage("Authorization canceled by user", LogMessageType.System);
                                DisposeClient();
                                return;
                            }
                        }
                    }
                    catch (RpcException ex) when (ex.Code == 401) // AUTH_KEY_UNREGISTERED: сессия отозвана
                    {
                        SendLogMessage("Telegram session is no longer valid", LogMessageType.Error);

                        if (AskSessionReset("Telegram session is no longer valid",
                                "Сессия Telegram больше недействительна"))
                        {
                            ResetSessionAndClient();
                            continue;
                        }

                        DisposeClient();
                        return;
                    }

                    User user = _client.User;

                    if (PhonesMatch(user.phone) == false)
                    {
                        // сессия привязана к другому номеру: пользователь сменил номер в параметрах, а вошли в старый аккаунт
                        SendLogMessage($"Logged in as +{user.phone} instead of {_phoneNumber}", LogMessageType.Error);

                        if (AskSessionReset(
                                "Logged in to Telegram account +" + user.phone + " instead of " + _phoneNumber,
                                "Выполнен вход в аккаунт Telegram +" + user.phone + " вместо " + _phoneNumber))
                        {
                            ResetSessionAndClient();
                            continue;
                        }

                        DisposeClient();
                        return;
                    }

                    SendLogMessage($"Successful login! ID: {user.id}, Name: {user.first_name}", LogMessageType.Connect);

                    FinalizeConnect();
                    return;
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
                DisposeClient();
            }
        }

        private bool PhonesMatch(string sessionPhone)
        {
            // сравниваем по цифрам: в параметрах номер может быть с плюсом, пробелами, через восьмёрку
            string paramDigits = new string(_phoneNumber.Where(char.IsDigit).ToArray());
            string sessionDigits = sessionPhone == null ? "" : new string(sessionPhone.Where(char.IsDigit).ToArray());

            if (paramDigits.Length == 0 || sessionDigits.Length == 0)
            {
                // не с чем сравнивать - не мешаем входу
                return true;
            }

            if (paramDigits == sessionDigits)
            {
                return true;
            }

            if (paramDigits.Length >= 10 && sessionDigits.Length >= 10)
            {
                // 10 последних цифр совпадают даже при разном коде страны (7/8 для РФ)
                return paramDigits.Substring(paramDigits.Length - 10) == sessionDigits.Substring(sessionDigits.Length - 10);
            }

            return false;
        }

        private bool AskSessionReset(string reasonEng, string reasonRu)
        {
            // удаление сессии необратимо, поэтому спрашиваем подтверждение
            bool result = false;

            try
            {
                if (System.Windows.Application.Current == null)
                {
                    return false;
                }

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.ConvertToLocString(
                        "Eng:" + reasonEng + ". Delete the Telegram session file and log in again_" +
                        "Ru:" + reasonRu + ". Удалить файл сессии Telegram и войти заново_"));
                    ui.ShowDialog();

                    result = ui.UserAcceptAction;
                });
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }

            return result;
        }

        private void ResetSessionAndClient()
        {
            DisposeClient();

            try
            {
                string sessionPath = Path.Combine(_logDir, "WTelegram.session");

                if (File.Exists(sessionPath))
                {
                    File.Delete(sessionPath);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

            _client = new WTelegram.Client(_apiID, _apiHash, @"Engine\Log\TelegramLogs\WTelegram.session");

            if (_proxy != null)
            {
                _client.TcpHandler = TcpHandlerThroughProxy;
            }

            SendLogMessage("Telegram session file deleted. Starting login again", LogMessageType.System);
        }

        private string WaitAuthInput(string what)
        {
            if (AuthInputRequestEvent == null)
            {
                SendLogMessage("Authorization input requested, but there is no subscriber: " + what, LogMessageType.Error);
                return null;
            }

            _authInputTcs = new TaskCompletionSource<string>();

            AuthInputRequestEvent(what);

            string input = _authInputTcs.Task.GetAwaiter().GetResult();
            _authInputTcs = null;

            return input;
        }

        public void ProvideAuthInput(string input)
        {
            TaskCompletionSource<string> tcs = _authInputTcs;

            if (tcs != null)
            {
                tcs.TrySetResult(input);
            }
        }

        public event Action<string> AuthInputRequestEvent;

        private void FinalizeConnect()
        {
            _client.OnOther += Client_OnOther;

            ServerStatus = ServerConnectStatus.Connect;
            ConnectEvent();

            // чаты и менеджер поднимаем в фоне: движок уже работает, а новости пойдут с именами каналов
            Thread initThread = new Thread(ChatsAndUpdatesInitWorker);
            initThread.Name = "TelegramNewsChatsInit";
            initThread.IsBackground = true;
            initThread.Start();
        }

        private void ChatsAndUpdatesInitWorker()
        {
            try
            {
                WTelegram.Client client = _client;

                if (client == null)
                {
                    return;
                }

                try
                {
                    // заголовки каналов нужны для подписи источника и спонсорки, но без них новости всё равно пойдут
                    Messages_Chats chats = client.Messages_GetAllChats().GetAwaiter().GetResult();

                    Dictionary<long, ChatBase>.Enumerator en = chats.chats.GetEnumerator();

                    while (en.MoveNext())
                    {
                        string chatKey = en.Current.Key.ToString();

                        if (_trackedChats.ContainsKey(chatKey))
                        {
                            string title = en.Current.Value.Title;
                            bool isPublic = false;

                            if (en.Current.Value is Channel channel)
                            {
                                // Is public channel
                                if (channel.username != null)
                                {
                                    isPublic = true;

                                    lock (_sponsorMsgLocker)
                                    {
                                        _timeChannelsSponsorMsg[en.Current.Key] = DateTime.Now.AddMinutes(-6);
                                    }
                                }
                            }

                            _trackedChats[chatKey] = (en.Current.Value, title, isPublic);
                        }
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }

                // state привязываем к аккаунту: при смене номера телефона чужое состояние не подхватится
                _updatesStatePath = Path.Combine(_logDir, "Updates_" + client.User.id + ".state");

                Dictionary<long, WTelegram.UpdateManager.MBoxState> state = WTelegram.UpdateManager.LoadState(_updatesStatePath);

                _manager = client.WithUpdateManager(UpdatesMessages, state);

                if (state == null)
                {
                    try
                    {
                        // первый запуск: выставляем pts по диалогам, иначе догонка вывалила бы всю историю каналов
                        Messages_Dialogs dialogs = client.Messages_GetAllDialogs().GetAwaiter().GetResult();
                        _manager.LoadDialogs(dialogs).GetAwaiter().GetResult();
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error.ToString(), LogMessageType.Error);
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void DisposeClient()
        {
            lock (_clientLocker)
            {
                if (_client != null)
                {
                    _client.OnOther -= Client_OnOther;
                    _client.Dispose();
                    _client = null;
                }
            }
        }

        private Task Client_OnOther(IObject arg)
        {
            if (arg is ReactorError err)
            {
                // typically: network connection was totally lost
                SendLogMessage($"Fatal reactor error: {err.Exception.ToString()}", LogMessageType.Error);

                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }

            }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            // если логин ждёт ввода кода/пароля — отпускаем воркер с отменой
            ProvideAuthInput(null);

            if (_activeInstance == this)
            {
                _activeInstance = null;
            }

            _trackedChats.Clear();
            _messagesToRead.Clear();

            lock (_sponsorMsgLocker)
            {
                _timeChannelsSponsorMsg.Clear();
            }

            _proxy = null;

            SaveUpdatesState();

            FlushWTelegramLog();

            DisposeClient();

            _manager = null;
            _updatesStatePath = null;

            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        public ServerType ServerType
        {
            get { return ServerType.TelegramNews; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

        public bool IsCompletelyDeleted { get; set; }

        #endregion

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        private string _channelsIDsString;
        private int _apiID;
        private string _apiHash;
        private string _phoneNumber;
        private int _maxMissedNewsAgeHours;
        private string _updatesStatePath;
        private DateTime _lastStateSave = DateTime.MinValue;

        private const string BotApiChannelPrefix = "-100";

        private WTelegram.Client _client;
        private WTelegram.UpdateManager _manager;
        private ConcurrentDictionary<string, (ChatBase, string, bool)> _trackedChats = new ConcurrentDictionary<string, (ChatBase, string, bool)>();
        private Dictionary<long, DateTime> _timeChannelsSponsorMsg = new Dictionary<long, DateTime>();
        private ConcurrentQueue<Message> _messagesToRead = new ConcurrentQueue<Message>();
        private string _sponsorMsgLocker = "_sponsorMsgLocker";
        private string _clientLocker = "_clientLocker";
        private TaskCompletionSource<string> _authInputTcs;

        #endregion

        #region Proxy

        private WebProxy _proxy;

        private Task<TcpClient> TcpHandlerThroughProxy(string host, int port)
        {
            // делегат WTelegram требует Task, сам туннель строим синхронно
            return Task.FromResult(CreateTcpClientThroughHttpProxy(host, port, _proxy));
        }

        private TcpClient CreateTcpClientThroughHttpProxy(string host, int port, WebProxy proxy)
        {
            TcpClient tcpClient = new TcpClient();

            try
            {
                IAsyncResult connectResult = tcpClient.BeginConnect(proxy.Address.Host, proxy.Address.Port, null, null);

                if (connectResult.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(10)) == false)
                {
                    tcpClient.Close();
                    throw new TimeoutException("Proxy connection timeout: " + proxy.Address.Host);
                }

                tcpClient.EndConnect(connectResult);

                NetworkStream stream = tcpClient.GetStream();
                stream.ReadTimeout = 10000;
                stream.WriteTimeout = 10000;

                StringBuilder request = new StringBuilder();
                request.Append("CONNECT " + host + ":" + port + " HTTP/1.1\r\n");
                request.Append("Host: " + host + ":" + port + "\r\n");

                NetworkCredential credentials = proxy.Credentials as NetworkCredential;

                if (credentials != null
                    && string.IsNullOrEmpty(credentials.UserName) == false)
                {
                    string authString = credentials.UserName + ":" + credentials.Password;
                    string authBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
                    request.Append("Proxy-Authorization: Basic " + authBase64 + "\r\n");
                }

                request.Append("\r\n");

                byte[] requestBytes = Encoding.ASCII.GetBytes(request.ToString());
                stream.Write(requestBytes, 0, requestBytes.Length);

                string response = ReadHttpHeaders(stream);

                if (response.StartsWith("HTTP/1.1 200") == false
                    && response.StartsWith("HTTP/1.0 200") == false)
                {
                    tcpClient.Close();
                    throw new Exception("Proxy CONNECT failed: " + response.Split('\r')[0]);
                }

                // дальше по туннелю идёт постоянное MTProto-соединение, таймауты чтения снимаем
                stream.ReadTimeout = -1;
                stream.WriteTimeout = -1;

                return tcpClient;
            }
            catch (Exception error)
            {
                tcpClient.Close();
                SendLogMessage(error.ToString(), LogMessageType.Error);
                throw;
            }
        }

        private string ReadHttpHeaders(NetworkStream stream)
        {
            StringBuilder headers = new StringBuilder();
            byte[] buffer = new byte[1];
            int tailMatch = 0;

            // читаем по байту до конца заголовков "\r\n\r\n"
            while (headers.Length < 8192)
            {
                int read = stream.Read(buffer, 0, 1);

                if (read == 0)
                {
                    throw new Exception("Proxy closed connection during CONNECT");
                }

                char c = (char)buffer[0];
                headers.Append(c);

                if (c == "\r\n\r\n"[tailMatch])
                {
                    tailMatch++;

                    if (tailMatch == 4)
                    {
                        return headers.ToString();
                    }
                }
                else
                {
                    tailMatch = c == '\r' ? 1 : 0;
                }
            }

            throw new Exception("Proxy CONNECT response is too long");
        }

        #endregion

        #region 3 News subscribe

        public bool SubscribeNews()
        {
            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                return false;
            }

            return true;
        }

        #endregion

        #region 4 Messages parsing

        private Task UpdatesMessages(Update update)
        {
            if (update is UpdateNewMessage { message: Message message })
            {
                try
                {
                    DispatchNews(message, false);
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
            else if (update is UpdateEditMessage { message: Message editedMessage })
            {
                try
                {
                    DispatchNews(editedMessage, true);
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }

            return Task.CompletedTask;
        }

        private void DispatchNews(Message message, bool isEdited)
        {
            string chatId = message.Peer.ID.ToString();

            if (_trackedChats.TryGetValue(chatId, out (ChatBase, string, bool) chatElements) == false)
            {
                return;
            }

            string text = message.message;

            if (string.IsNullOrEmpty(text))
            {
                // пост без текста: отдаём роботам маркер типа медиа, чтобы факт публикации не терялся
                text = GetMediaMarker(message.media);

                if (text == null)
                {
                    return;
                }
            }

            if (_maxMissedNewsAgeHours > 0)
            {
                if (isEdited == false)
                {
                    if (message.Date < DateTime.UtcNow.AddHours(-_maxMissedNewsAgeHours))
                    {
                        // слишком старое сообщение из догонки: роботам не отдаём, но обработку спонсорки делаем
                        _messagesToRead.Enqueue(message);
                        return;
                    }
                }
                else if (message.edit_date != default
                    && message.edit_date < DateTime.UtcNow.AddHours(-_maxMissedNewsAgeHours))
                {
                    // у правки Date — дата оригинального поста, поэтому возраст считаем от времени правки
                    return;
                }
            }

            TelegramNewsMessage news = new TelegramNewsMessage();

            news.Source = string.IsNullOrEmpty(chatElements.Item2) ? chatId : chatElements.Item2;
            news.Value = text;
            news.TimeMessage = message.Date.ToLocalTime();
            news.IsEdited = isEdited;

            NewsEvent?.Invoke(news);

            if (isEdited == false)
            {
                // медленную бухгалтерию (рекламные сообщения) делает поток-ридер
                _messagesToRead.Enqueue(message);
            }
        }

        private string GetMediaMarker(MessageMedia media)
        {
            if (media is MessageMediaPhoto)
            {
                return "[photo]";
            }

            if (media is MessageMediaDocument mediaDoc
                && mediaDoc.document is Document doc)
            {
                for (int i = 0; i < doc.attributes.Length; i++)
                {
                    if (doc.attributes[i] is DocumentAttributeVideo)
                    {
                        return "[video]";
                    }

                    if (doc.attributes[i] is DocumentAttributeAudio audio)
                    {
                        return audio.flags.HasFlag(DocumentAttributeAudio.Flags.voice) ? "[voice]" : "[audio]";
                    }

                    if (doc.attributes[i] is DocumentAttributeSticker)
                    {
                        return "[sticker]";
                    }

                    if (doc.attributes[i] is DocumentAttributeAnimated)
                    {
                        return "[gif]";
                    }

                    if (doc.attributes[i] is DocumentAttributeFilename fileName)
                    {
                        return "[document: " + fileName.file_name + "]";
                    }
                }

                return "[document]";
            }

            if (media is MessageMediaWebPage)
            {
                return "[web page]";
            }

            if (media is MessageMediaGeo
                || media is MessageMediaGeoLive
                || media is MessageMediaVenue)
            {
                return "[location]";
            }

            if (media is MessageMediaContact)
            {
                return "[contact]";
            }

            if (media is MessageMediaPoll)
            {
                return "[poll]";
            }

            return null;
        }

        private void MessageReaderThread()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (MainWindow.ProccesIsWorked == false)
                    {
                        return;
                    }

                    ServerTime = DateTime.Now;

                    FlushWTelegramLog();

                    if (DateTime.Now > _lastStateSave.AddMinutes(5))
                    {
                        // периодический снапшот state: при крэше приложения догонка начнётся с него, а не с последнего Dispose
                        _lastStateSave = DateTime.Now;
                        SaveUpdatesState();
                    }

                    if (_messagesToRead.IsEmpty)
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    Message message;

                    if (_messagesToRead.TryDequeue(out message) == false)
                    {
                        continue;
                    }

                    if (ServerStatus != ServerConnectStatus.Connect
                        || _client == null)
                    {
                        continue;
                    }

                    HandleMessageBookkeeping(message);
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void HandleMessageBookkeeping(Message message)
        {
            string chatId = message.Peer.ID.ToString();

            if (_trackedChats.TryGetValue(chatId, out (ChatBase, string, bool) chatElements) == false
                || chatElements.Item1 == null)
            {
                return;
            }

            if (chatElements.Item3) // public channel
            {
                // We have already read the sponsorship message from this channel and it has been more than 5 minutes.
                bool needReadSponsored = false;

                lock (_sponsorMsgLocker)
                {
                    if (_timeChannelsSponsorMsg.TryGetValue(chatElements.Item1.ID, out DateTime timeOfReadingMessage)
                        && timeOfReadingMessage.AddMinutes(5) < DateTime.Now)
                    {
                        needReadSponsored = true;
                    }
                }

                if (needReadSponsored)
                {
                    HandleSponsoredMessage(chatElements.Item1);
                }
            }
        }

        private void HandleSponsoredMessage(InputPeer inputPeer)
        {
            Thread.Sleep(1000);

            Messages_SponsoredMessages sponsMsgs = null;

            try
            {
                if (_client == null)
                {
                    return;
                }

                // Get a list of sponsored messages
                sponsMsgs = _client.Messages_GetSponsoredMessages(inputPeer).GetAwaiter().GetResult();
            }
            catch (RpcException ex) when (ex.Code == 420)
            {
                int waitTime = ExtractWaitTime(ex.Message);
                Thread.Sleep(TimeSpan.FromSeconds(waitTime));
            }
            catch (Exception ex)
            {
                SendLogMessage($"Failed to get sponsored messages: {ex.ToString()}", LogMessageType.Error);
            }

            if (sponsMsgs == null)
            {
                return;
            }

            for (int i = 0; i < sponsMsgs.messages.Length; i++)
            {
                Thread.Sleep(500);

                try
                {
                    if (_client == null)
                    {
                        return;
                    }

                    // mark as read
                    bool hasBeenRaed = _client.Messages_ViewSponsoredMessage(sponsMsgs.messages[i].random_id).GetAwaiter().GetResult();

                    if (hasBeenRaed)
                    {
                        // The result must be cached for 5 minutes.
                        lock (_sponsorMsgLocker)
                        {
                            _timeChannelsSponsorMsg[inputPeer.ID] = DateTime.Now;
                        }
                    }
                }
                catch (RpcException ex) when (ex.Code == 420)
                {
                    int waitTime = ExtractWaitTime(ex.Message);
                    Thread.Sleep(TimeSpan.FromSeconds(waitTime));
                }
                catch (Exception ex)
                {
                    SendLogMessage($"Failed to view sponsored message: {ex.ToString()}", LogMessageType.Error);
                }
            }
        }

        private int ExtractWaitTime(string errorMessage)
        {
            // Пример ошибки: "FLOOD_WAIT_3" → вернет 3
            Match match = Regex.Match(errorMessage, @"FLOOD_WAIT_(\d+)");
            return match.Success ? int.Parse(match.Groups[1].Value) : 10; // Дефолт: 10 сек
        }

        private void SaveUpdatesState()
        {
            try
            {
                WTelegram.UpdateManager manager = _manager;
                string path = _updatesStatePath;

                if (manager == null
                    || string.IsNullOrEmpty(path))
                {
                    return;
                }

                manager.SaveState(path);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public event Action<News> NewsEvent;

        #endregion

        #region 5 Log

        private static string _logDir = @"Engine\Log\TelegramLogs";
        private static readonly object _wtelegramFileLocker = new object();
        private static TelegramNewsServerRealization _activeInstance;
        private static bool _logFileErrorReported;
        private static ConcurrentQueue<string> _wtelegramLogQueue = new ConcurrentQueue<string>();

        private static void WTelegramLogHandler(int level, string message)
        {
            try
            {
                TelegramNewsServerRealization instance = _activeInstance;

                if (level >= 4 // Error и Critical
                    && instance != null)
                {
                    instance.SendLogMessage(message, LogMessageType.Error);
                }

                // в файл пишем пачкой из потока-ридера: UpdateManager логирует каждый апдейт,
                // и AppendAllText на каждую строку дергал бы диск сотни раз в минуту
                _wtelegramLogQueue.Enqueue($"[{DateTime.Now:HH:mm:ss}] LogEvent : {message}\n");
            }
            catch (Exception error)
            {
                // логгер не имеет права ронять потоки библиотеки, поэтому об ошибке записи сообщаем один раз
                if (_logFileErrorReported == false)
                {
                    _logFileErrorReported = true;
                    ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        private void FlushWTelegramLog()
        {
            if (_wtelegramLogQueue.IsEmpty)
            {
                return;
            }

            try
            {
                lock (_wtelegramFileLocker)
                {
                    StringBuilder batch = new StringBuilder();
                    string line;

                    while (_wtelegramLogQueue.TryDequeue(out line))
                    {
                        batch.Append(line);
                    }

                    string logPath = Path.Combine(_logDir, "wteleg.log");
                    File.AppendAllText(logPath, batch.ToString());

                    FileInfo logFile = new FileInfo(logPath);

                    if (logFile.Length > 1024 * 1024)
                    {
                        File.WriteAllText(logPath, "");
                    }
                }
            }
            catch (Exception error)
            {
                if (_logFileErrorReported == false)
                {
                    _logFileErrorReported = true;
                    ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, messageType);
            }
            else if (messageType == LogMessageType.Error)
            {
                ServerMaster.SendNewLogMessage(message, messageType);
            }
        }

        #endregion

        #region 6 Not used functions

        public void CancelAllOrders()
        {
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
        }

        public bool CancelOrder(Order order)
        {
            return true;
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
        }

        public void Subscribe(Security security)
        {
        }

        public void GetAllActivOrders()
        {
        }

        public List<Order> GetActiveOrders(int startIndex, int count)
        {
            return null;
        }

        public List<Order> GetHistoricalOrders(int startIndex, int count)
        {
            return null;
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            return null;
        }

        public OrderStateType GetOrderStatus(Order order)
        {
            return OrderStateType.None;
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
        public event Action<MarketDepth> MarketDepthEvent { add { } remove { } }
        public event Action<Trade> NewTradesEvent { add { } remove { } }
        public event Action<Order> MyOrderEvent { add { } remove { } }
        public event Action<MyTrade> MyTradeEvent { add { } remove { } }
        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }

        public void SetLeverage(Security security, decimal leverage) { }

        #endregion
    }
}
