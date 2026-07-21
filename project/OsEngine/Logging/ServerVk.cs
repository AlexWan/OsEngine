/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json;
using OsEngine.Market;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading;

namespace OsEngine.Logging
{
    public class ServerVk
    {
        #region Static members

        private static ServerVk _server;

        public static ServerVk GetServer()
        {
            if (_server == null)
            {
                _server = new ServerVk();
            }
            return _server;
        }

        #endregion

        #region Fields

        private HttpClient _httpClient;

        /// <summary>
        /// Base URL VK API (api.vk.ru — актуальный домен, api.vk.com в некоторых сетях не резолвится)
        /// </summary>
        private string _apiBaseUrl = "https://api.vk.ru/method/";

        private Random _random = new Random();

        private string _userIdsLocker = "UserIdsLocker";

        /// <summary>
        /// Long Poll server URL (переиспользуется при реконнекте)
        /// </summary>
        private string _longPollServerUrl;

        /// <summary>
        /// Long Poll server key (переиспользуется при реконнекте)
        /// </summary>
        private string _longPollKey;

        /// <summary>
        /// Last Long Poll timestamp (сохраняется между реконнектами,
        /// чтобы не обрабатывать уже полученные апдейты повторно)
        /// </summary>
        private long _lastUpdateTs;

        /// <summary>
        /// Access Token for VK API
        /// </summary>
        public string AccessToken;

        /// <summary>
        /// User IDs for private messages
        /// </summary>
        public List<long> UserIds = new List<long>();

        /// <summary>
        /// Processing Command from VK
        /// </summary>
        public bool ProcessingCommand;

        /// <summary>
        /// Shows whether the server is ready to work
        /// </summary>
        private bool _isReady;

        /// <summary>
        /// Queue of messages
        /// </summary>
        private ConcurrentQueue<(string, LogMessage)> _messagesQueue = new ConcurrentQueue<(string, LogMessage)>();

        /// <summary>
        /// First user ID for backward compatibility
        /// </summary>
        public long UserId
        {
            get
            {
                lock (_userIdsLocker)
                {
                    return UserIds.Count > 0 ? UserIds[0] : 0;
                }
            }
            set
            {
                lock (_userIdsLocker)
                {
                    if (value != 0 && !UserIds.Contains(value))
                    {
                        UserIds.Insert(0, value);
                    }
                }
            }
        }

        #endregion

        #region Constructors

        private ServerVk()
        {
            Load();

            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(60);

            Thread worker1 = new Thread(PullMessages);
            worker1.CurrentCulture = new CultureInfo("ru-RU");
            worker1.IsBackground = true;
            worker1.Start();

            Thread worker2 = new Thread(PollAndHandleUpdatesAsync);
            worker2.CurrentCulture = new CultureInfo("ru-RU");
            worker2.IsBackground = true;
            worker2.Start();
        }

        #endregion

        #region Public methods

        /// <summary>
        /// Send message to VK server
        /// </summary>
        public void Send(LogMessage message, string nameBot)
        {
            if (!_isReady)
            {
                return;
            }

            _messagesQueue.Enqueue((nameBot, message));
        }

        /// <summary>
        /// Read initial data
        /// </summary>
        public void Load()
        {
            try
            {
                if (File.Exists(@"Engine\vkSet.txt"))
                {
                    using StreamReader reader = new StreamReader(@"Engine\vkSet.txt");
                    AccessToken = reader.ReadLine();
                    string userIdsLine = reader.ReadLine();
                    string isProcessingCommand = reader.ReadLine();

                    List<long> loadedIds = ParseUserIds(userIdsLine);

                    lock (_userIdsLocker)
                    {
                        UserIds = loadedIds;
                    }

                    if (isProcessingCommand == "True" || isProcessingCommand == "true")
                    {
                        ProcessingCommand = true;
                    }
                    else
                    {
                        ProcessingCommand = false;
                    }

                    _isReady = UserIds.Count > 0 && !string.IsNullOrEmpty(AccessToken);
                }
                else
                {
                    AccessToken = string.Empty;

                    lock (_userIdsLocker)
                    {
                        UserIds.Clear();
                    }

                    ProcessingCommand = false;
                    _isReady = false;
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Save initial data
        /// </summary>
        public void Save()
        {
            try
            {
                string userIdsLine;

                lock (_userIdsLocker)
                {
                    userIdsLine = string.Join(",", UserIds);
                }

                using StreamWriter writer = new StreamWriter(@"Engine\vkSet.txt");
                writer.WriteLine(AccessToken);
                writer.WriteLine(userIdsLine);
                writer.WriteLine(ProcessingCommand);

                _isReady = !string.IsNullOrEmpty(userIdsLine) && !string.IsNullOrEmpty(AccessToken);
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Show settings window
        /// </summary>
        public void ShowDialog()
        {
            ServerVkUi ui = new ServerVkUi();
            ui.ShowDialog();
        }

        /// <summary>
        /// Disconnect VK server (до повторного включения через Save в окне настроек)
        /// </summary>
        public void Disconnect()
        {
            _isReady = false;

            _messagesQueue.Clear();

            ServerMaster.SendNewLogMessage("VK server disconnected by user", LogMessageType.System);
        }

        /// <summary>
        /// Parse user IDs from comma/space/semicolon separated string
        /// </summary>
        public List<long> ParseUserIds(string line)
        {
            List<long> result = new List<long>();

            if (string.IsNullOrWhiteSpace(line))
            {
                return result;
            }

            string[] parts = line.Split(new char[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();

                if (long.TryParse(part, out long id) && !result.Contains(id))
                {
                    result.Add(id);
                }
            }

            return result;
        }

        /// <summary>
        /// Parse user IDs and screen names (короткие адреса вида durov или vk.com/durov),
        /// screen names конвертируются в числовые ID через users.get
        /// </summary>
        public List<long> ParseAndResolveUserIds(string line, out List<string> unresolvedNames)
        {
            List<long> result = new List<long>();
            List<string> screenNames = new List<string>();
            unresolvedNames = new List<string>();

            if (string.IsNullOrWhiteSpace(line))
            {
                return result;
            }

            string[] parts = line.Split(new char[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();

                if (long.TryParse(part, out long id))
                {
                    if (!result.Contains(id))
                    {
                        result.Add(id);
                    }
                }
                else
                {
                    string screenName = CleanScreenName(part);

                    if (!string.IsNullOrEmpty(screenName)
                        && !screenNames.Contains(screenName))
                    {
                        screenNames.Add(screenName);
                    }
                }
            }

            List<long> resolved = ResolveScreenNames(screenNames, out unresolvedNames);

            for (int i = 0; i < resolved.Count; i++)
            {
                if (!result.Contains(resolved[i]))
                {
                    result.Add(resolved[i]);
                }
            }

            return result;
        }

        /// <summary>
        /// Validate Access Token and user IDs via users.get
        /// </summary>
        public bool ValidateTokenAndUserIds(List<long> userIds, out string errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrEmpty(AccessToken))
            {
                errorMessage = "Access Token is empty";
                ServerMaster.SendNewLogMessage($"VK validation failed: {errorMessage}", LogMessageType.Error);
                return false;
            }

            if (userIds == null
                || userIds.Count == 0)
            {
                errorMessage = "User IDs list is empty";
                ServerMaster.SendNewLogMessage($"VK validation failed: {errorMessage}", LogMessageType.Error);
                return false;
            }

            try
            {
                string ids = string.Join(",", userIds);

                string url = _apiBaseUrl + "users.get?" +
                             $"user_ids={ids}" +
                             $"&access_token={AccessToken}&v=5.131";

                HttpResponseMessage response = _httpClient.GetAsync(url).GetAwaiter().GetResult();
                string responseContent = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                dynamic parsed = JsonConvert.DeserializeObject(responseContent);

                if (parsed?.error != null)
                {
                    errorMessage = $"VK API error {parsed.error.error_code}: {parsed.error.error_msg}";
                    ServerMaster.SendNewLogMessage($"VK validation failed: {errorMessage}", LogMessageType.Error);
                    return false;
                }

                if (parsed?.response == null)
                {
                    errorMessage = $"VK API unexpected response: {responseContent}";
                    ServerMaster.SendNewLogMessage($"VK validation failed: {errorMessage}", LogMessageType.Error);
                    return false;
                }

                List<long> found = new List<long>();

                foreach (var user in parsed.response)
                {
                    found.Add((long)user.id);
                }

                List<long> missing = new List<long>();

                for (int i = 0; i < userIds.Count; i++)
                {
                    if (!found.Contains(userIds[i]))
                    {
                        missing.Add(userIds[i]);
                    }
                }

                if (missing.Count > 0)
                {
                    errorMessage = $"User IDs not found: {string.Join(", ", missing)}";
                    ServerMaster.SendNewLogMessage($"VK validation failed: {errorMessage}", LogMessageType.Error);
                    return false;
                }

                return true;
            }
            catch (Exception error)
            {
                errorMessage = error.Message;
                ServerMaster.SendNewLogMessage($"VK validation failed: {errorMessage}", LogMessageType.Error);
                return false;
            }
        }

        /// <summary>
        /// Remove prefixes @, vk.com/, https://vk.com/ from screen name
        /// </summary>
        private string CleanScreenName(string name)
        {
            name = name.Trim();

            if (name.StartsWith("@"))
            {
                name = name.Substring(1);
            }

            int vkComIndex = name.IndexOf("vk.com/", StringComparison.OrdinalIgnoreCase);

            if (vkComIndex >= 0)
            {
                name = name.Substring(vkComIndex + "vk.com/".Length);
            }

            name = name.TrimEnd('/');

            return name;
        }

        /// <summary>
        /// Convert screen names to numeric user IDs via users.get
        /// </summary>
        private List<long> ResolveScreenNames(List<string> screenNames, out List<string> unresolvedNames)
        {
            List<long> result = new List<long>();
            unresolvedNames = new List<string>();

            if (screenNames == null
                || screenNames.Count == 0)
            {
                return result;
            }

            if (string.IsNullOrEmpty(AccessToken))
            {
                unresolvedNames.AddRange(screenNames);

                ServerMaster.SendNewLogMessage(
                    "VK: can't resolve screen names, Access Token is empty",
                    LogMessageType.Error);
                return result;
            }

            try
            {
                string names = string.Join(",", screenNames);

                string url = _apiBaseUrl + "users.get?" +
                             $"user_ids={Uri.EscapeDataString(names)}" +
                             $"&fields=screen_name" +
                             $"&access_token={AccessToken}&v=5.131";

                HttpResponseMessage response = _httpClient.GetAsync(url).GetAwaiter().GetResult();
                string responseContent = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                dynamic parsed = JsonConvert.DeserializeObject(responseContent);

                if (parsed?.response == null)
                {
                    unresolvedNames.AddRange(screenNames);

                    ServerMaster.SendNewLogMessage(
                        $"VK users.get failed for '{names}'. Response: {responseContent}",
                        LogMessageType.Error);
                    return result;
                }

                List<string> foundNames = new List<string>();

                foreach (var user in parsed.response)
                {
                    long id = (long)user.id;

                    if (!result.Contains(id))
                    {
                        result.Add(id);
                    }

                    string screenName = user.screen_name?.ToString();

                    if (!string.IsNullOrEmpty(screenName))
                    {
                        foundNames.Add(screenName.ToLowerInvariant());
                    }
                }

                for (int i = 0; i < screenNames.Count; i++)
                {
                    if (!foundNames.Contains(screenNames[i].ToLowerInvariant()))
                    {
                        unresolvedNames.Add(screenNames[i]);
                    }
                }

                if (unresolvedNames.Count > 0)
                {
                    ServerMaster.SendNewLogMessage(
                        $"VK: screen names not found: {string.Join(", ", unresolvedNames)}",
                        LogMessageType.Error);
                }
            }
            catch (Exception error)
            {
                unresolvedNames.AddRange(screenNames);
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }

            return result;
        }

        /// <summary>
        /// Create command event
        /// </summary>
        public void ExecuteCommand(string botName, CommandVk cmd)
        {
            try
            {
                VkCommandEvent?.Invoke(botName, cmd);
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        public event Action<string, CommandVk> VkCommandEvent;

        #endregion

        #region Private methods

        /// <summary>
        /// Send message to VK (по документации VK API)
        /// Используем peer_ids для нескольких получателей.
        /// Если VK вернул ошибку — fallback на отправку по одному user_id.
        /// </summary>
        private void SendMessageAsync(string messageText)
        {
            try
            {
                if (string.IsNullOrEmpty(AccessToken))
                {
                    return;
                }

                List<long> userIdsCopy;

                lock (_userIdsLocker)
                {
                    userIdsCopy = new List<long>(UserIds);
                }

                if (userIdsCopy.Count == 0)
                {
                    return;
                }

                messageText = CheckString(messageText);

                string peerIds = string.Join(",", userIdsCopy);

                string postData = BuildPostData(peerIds, messageText, true);

                StringContent content = new StringContent(postData, System.Text.Encoding.UTF8, "application/x-www-form-urlencoded");

                HttpResponseMessage response = _httpClient.PostAsync(_apiBaseUrl + "messages.send", content).Result;
                string responseContent = response.Content.ReadAsStringAsync().Result;

                if (responseContent.Contains("\"error\""))
                {
                    ServerMaster.SendNewLogMessage(
                        $"VK peer_ids send failed. Fallback to individual user_id sends. Response: {responseContent}",
                        LogMessageType.Error);

                    SendToEachUser(messageText, userIdsCopy);
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Fallback: отправка сообщения каждому получателю отдельным запросом
        /// </summary>
        private void SendToEachUser(string messageText, List<long> userIds)
        {
            for (int i = 0; i < userIds.Count; i++)
            {
                try
                {
                    long userId = userIds[i];

                    string postData = BuildPostData(userId.ToString(), messageText, false);

                    StringContent content = new StringContent(postData, System.Text.Encoding.UTF8, "application/x-www-form-urlencoded");

                    HttpResponseMessage response = _httpClient.PostAsync(_apiBaseUrl + "messages.send", content).Result;
                    string responseContent = response.Content.ReadAsStringAsync().Result;

                    if (responseContent.Contains("\"error\""))
                    {
                        ServerMaster.SendNewLogMessage(
                            $"VK user_id send failed for {userId}. Response: {responseContent}",
                            LogMessageType.Error);
                    }
                }
                catch (Exception error)
                {
                    ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        /// <summary>
        /// Build POST data for messages.send
        /// </summary>
        private string BuildPostData(string destination, string messageText, bool isPeerIds)
        {
            string paramName = isPeerIds ? "peer_ids" : "user_id";

            string postData = $"{paramName}={destination}" +
                              $"&random_id={_random.Next()}" +
                              $"&message={Uri.EscapeDataString(messageText)}" +
                              $"&access_token={AccessToken}&v=5.131";

            if (ProcessingCommand)
            {
                string keyboardJson = @"
                    {
                         ""one_time"": false,
                              ""buttons"": [[{""action"": {""type"": ""text"", ""label"": ""StopAllBots""}, ""color"": ""negative""},
                              {""action"": {""type"": ""text"", ""label"": ""StartAllBots""}, ""color"": ""positive""}],
                              [{""action"": {""type"": ""text"", ""label"": ""CancelAllActiveOrders""}, ""color"": ""secondary""},
                              {""action"": {""type"": ""text"", ""label"": ""GetStatus""}, ""color"": ""primary""}]]
                    }";
                postData += $"&keyboard={Uri.EscapeDataString(keyboardJson)}";
            }

            return postData;
        }

        /// <summary>
        /// Poll and handle updates (commands) - используем Long Poll API
        /// </summary>
        private void PollAndHandleUpdatesAsync()
        {
            while (true)
            {
                if (MainWindow.ProccesIsWorked == false)
                {
                    return;
                }

                if (!_isReady)
                {
                    Thread.Sleep(5000);
                    continue;
                }

                try
                {
                    if (string.IsNullOrEmpty(_longPollKey))
                    {
                        string getLongPollServerUrl = _apiBaseUrl + "messages.getLongPollServer?" +
                                                      $"access_token={AccessToken}" +
                                                      $"&v=5.131";

                        HttpResponseMessage response = _httpClient.GetAsync(getLongPollServerUrl).GetAwaiter().GetResult();
                        string responseContent = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                        dynamic longPollServer = JsonConvert.DeserializeObject(responseContent);

                        if (longPollServer?.response == null)
                        {
                            Thread.Sleep(5000);
                            continue;
                        }

                        string server = longPollServer.response.server;
                        _longPollKey = longPollServer.response.key;
                        _lastUpdateTs = longPollServer.response.ts;
                        _longPollServerUrl = server;
                    }

                    string serverUrl = _longPollServerUrl;

                    if (!serverUrl.StartsWith("http://") && !serverUrl.StartsWith("https://"))
                    {
                        serverUrl = "https://" + serverUrl;
                    }

                    string longPollUrl = $"{serverUrl}?act=a_check&key={_longPollKey}&ts={_lastUpdateTs}&wait=25&mode=2&version=3";

                    HttpResponseMessage longPollResponse = _httpClient.GetAsync(longPollUrl).GetAwaiter().GetResult();
                    string longPollContent = longPollResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                    dynamic updates = JsonConvert.DeserializeObject(longPollContent);

                    // Обработка ошибок Long Poll (https://dev.vk.com/ru/api/user-long-poll/getting-started)
                    if (updates?.failed != null)
                    {
                        int failed = (int)updates.failed;

                        if (failed == 1)
                        {
                            // История устарела — обновляем ts и продолжаем
                            _lastUpdateTs = updates.ts;
                            continue;
                        }

                        if (failed == 4)
                        {
                            // Недопустимый номер версии — перезапрос сервера не поможет
                            ServerMaster.SendNewLogMessage(
                                $"VK Long Poll: invalid version. Response: {longPollContent}",
                                LogMessageType.Error);
                            Thread.Sleep(60000);
                            continue;
                        }

                        // failed == 2 (key expired) или 3 — перезапрашиваем сервер
                        _longPollKey = null;
                        continue;
                    }

                    if (updates?.updates != null)
                    {
                        foreach (var update in updates.updates)
                        {
                            if (update is Newtonsoft.Json.Linq.JArray updateArray && updateArray.Count > 0)
                            {
                                int type = (int)updateArray[0];

                                // type 4 = новое сообщение
                                if (type == 4 && updateArray.Count >= 6)
                                {
                                    // Структура: [4, message_id, flags, from_id, timestamp, text, ...]
                                    string messageText = updateArray[5]?.ToString();
                                    long fromId = (long)updateArray[3];

                                    lock (_userIdsLocker)
                                    {
                                        if (UserIds.Count > 0 && !UserIds.Contains(fromId))
                                        {
                                            continue;
                                        }
                                    }

                                    if (!string.IsNullOrEmpty(messageText))
                                    {
                                        HandleMessage(messageText);
                                    }
                                }
                            }
                        }
                    }

                    if (updates?.ts != null)
                    {
                        _lastUpdateTs = updates.ts;
                    }

                    Thread.Sleep(500);
                }
                catch (OperationCanceledException)
                {
                    // Long Poll connection was broken or request timed out.
                    // This is a normal situation for a persistent connection — just reconnect.
                    Thread.Sleep(5000);
                }
                catch (HttpRequestException)
                {
                    // VK closed the connection or network dropped — reconnect quietly.
                    Thread.Sleep(5000);
                }
                catch (IOException)
                {
                    // Transport connection was forcibly closed by remote host — reconnect quietly.
                    Thread.Sleep(5000);
                }
                catch (Exception error)
                {
                    ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        /// <summary>
        /// command handler
        /// </summary>
        private void HandleMessage(string messageText)
        {
            if (string.IsNullOrEmpty(messageText)
                || !ProcessingCommand)
            {
                return;
            }

            switch (messageText)
            {
                case "StopAllBots":
                    ExecuteCommand(null, CommandVk.StopAllBots);
                    break;
                case "StartAllBots":
                    ExecuteCommand(null, CommandVk.StartAllBots);
                    break;
                case "CancelAllActiveOrders":
                    ExecuteCommand(null, CommandVk.CancelAllActiveOrders);
                    break;
                case "GetStatus":
                    ExecuteCommand(null, CommandVk.GetStatus);
                    break;
            }
        }

        /// <summary>
        /// message queue handling
        /// </summary>
        private void PullMessages()
        {
            Thread.Sleep(500);

            while (true)
            {
                try
                {
                    if (MainWindow.ProccesIsWorked == false)
                    {
                        return;
                    }

                    if (_messagesQueue == null
                        || _messagesQueue.Count == 0)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (!_isReady)
                    {
                        Thread.Sleep(2000);
                        continue;
                    }

                    if (!_messagesQueue.TryDequeue(out (string, LogMessage) msg))
                    {
                        continue;
                    }

                    string finalMessage = msg.Item2.Type == LogMessageType.Error
                        ? $"__{msg.Item2.Message}__" : msg.Item2.Message;

                    SendMessageAsync($"{msg.Item1} | {finalMessage}");
                }
                catch (Exception error)
                {
                    ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
                    Thread.Sleep(2000);
                }
            }
        }

        /// <summary>
        /// Checking the message string for valid characters
        /// </summary>
        private string CheckString(string str)
        {
            // VK ограничение 4096 символов
            if (str.Length > 4000)
            {
                str = str.Substring(0, 4000) + "...";
            }
            return str;
        }

        #endregion
    }

    #region Helper

    /// <summary>
    /// Commands sent to the bot
    /// - Посылаемые боту команды
    /// </summary>
    public enum CommandVk
    {
        None,
        StopBot,
        StartBot,
        StopAllBots,
        StartAllBots,
        CancelActiveOrdersForBot,
        CancelAllActiveOrders,
        GetStatus
    }

    #endregion
}
