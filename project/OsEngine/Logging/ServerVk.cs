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
using System.Windows;

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

        private readonly HttpClient _httpClient;

        private readonly Random _random = new Random();

        private readonly object _userIdsLocker = new object();

        /// <summary>
        /// Last message Update Id (используется для Long Poll)
        /// </summary>
        private long _lastUpdateId;

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
        private static bool _isReady;

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
                MessageBox.Show(ex.ToString());
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

                HttpResponseMessage response = _httpClient.PostAsync("https://api.vk.com/method/messages.send", content).Result;
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

                    HttpResponseMessage response = _httpClient.PostAsync("https://api.vk.com/method/messages.send", content).Result;
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
                    string getLongPollServerUrl = $"https://api.vk.com/method/messages.getLongPollServer?" +
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
                    string key = longPollServer.response.key;
                    long ts = longPollServer.response.ts;

                    if (!server.StartsWith("http://") && !server.StartsWith("https://"))
                    {
                        server = "https://" + server;
                    }

                    string longPollUrl = $"{server}?act=a_check&key={key}&ts={ts}&wait=25&mode=2&version=3";

                    HttpResponseMessage longPollResponse = _httpClient.GetAsync(longPollUrl).GetAwaiter().GetResult();
                    string longPollContent = longPollResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                    dynamic updates = JsonConvert.DeserializeObject(longPollContent);

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
                        _lastUpdateId = updates.ts;
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

    public class VkLongPollServerResponse
    {
        public VkLongPollServer response { get; set; }
    }

    public class VkLongPollServer
    {
        public string key { get; set; }
        public string server { get; set; }
        public long ts { get; set; }
    }

    public class VkLongPollUpdates
    {
        public long ts { get; set; }
        public List<VkLongPollUpdate> updates { get; set; }
    }

    public class VkLongPollUpdate
    {
        public int type { get; set; }
        public object @object { get; set; }  // Может быть разным в зависимости от type

        // Для type = 4 (новое сообщение)
        public VkLongPollMessage message => GetMessage();

        private VkLongPollMessage GetMessage()
        {
            if (type == 4 && @object != null)
            {
                var objDict = @object as Newtonsoft.Json.Linq.JObject;
                if (objDict != null)
                {
                    var msg = objDict["message"]?.ToObject<VkLongPollMessage>();
                    if (msg != null) return msg;

                    // Альтернативный формат: некоторые версии API возвращают message напрямую
                    return objDict.ToObject<VkLongPollMessage>();
                }
            }
            return null;
        }
    }

    public class VkLongPollMessage
    {
        public long id { get; set; }
        public long date { get; set; }
        public long from_id { get; set; }
        public long peer_id { get; set; }
        public string text { get; set; }
        public int @out { get; set; }
    }

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
