/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json;
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
        private static ServerVk _server;

        private readonly HttpClient _httpClient;

        /// <summary>
        /// Last message Update Id (используется для Long Poll)
        /// </summary>
        private long _lastUpdateId;

        /// <summary>
        /// Access Token for VK API
        /// </summary>
        public string AccessToken;

        /// <summary>
        /// User ID for private messages
        /// </summary>
        public long UserId;

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

        public static ServerVk GetServer()
        {
            if (_server == null)
            {
                _server = new ServerVk();
            }
            return _server;
        }

        private ServerVk()
        {
            Load();

            _httpClient = new HttpClient();

            Thread worker1 = new Thread(PullMessages);
            worker1.CurrentCulture = new CultureInfo("ru-RU");
            worker1.IsBackground = true;
            worker1.Start();

            Thread worker2 = new Thread(PollAndHandleUpdatesAsync);
            worker2.CurrentCulture = new CultureInfo("ru-RU");
            worker2.IsBackground = true;
            worker2.Start();
        }

        /// <summary>
        /// Send message to VK (по документации VK API)
        /// </summary>
        private void SendMessageAsync(string messageText)
        {
            try
            {
                if (string.IsNullOrEmpty(AccessToken))
                {
                    return;
                }

                if (UserId == 0)
                {
                    return;
                }

                messageText = CheckString(messageText);

                string postData = $"user_id={UserId}&random_id={new Random().Next()}&message={Uri.EscapeDataString(messageText)}&access_token={AccessToken}&v=5.131";

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



                var content = new StringContent(postData, System.Text.Encoding.UTF8, "application/x-www-form-urlencoded");

                HttpResponseMessage response = _httpClient.PostAsync("https://api.vk.com/method/messages.send", content).Result;
                string responseContent = response.Content.ReadAsStringAsync().Result;
            }
            catch
            {
                
            }
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

                    HttpResponseMessage response = _httpClient.GetAsync(getLongPollServerUrl).Result;
                    string responseContent = response.Content.ReadAsStringAsync().Result;

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

                    HttpResponseMessage longPollResponse = _httpClient.GetAsync(longPollUrl).Result;
                    string longPollContent = longPollResponse.Content.ReadAsStringAsync().Result;

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
                catch
                {
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
                        continue;

                    string finalMessage = msg.Item2.Type == LogMessageType.Error
                        ? $"__{msg.Item2.Message}__" : msg.Item2.Message;

                    SendMessageAsync($"{msg.Item1} | {finalMessage}");
                }
                catch 
                {
                    Thread.Sleep(2000);
                }
            }
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
                    UserId = Convert.ToInt64(reader.ReadLine());
                    string isProcessingCommand = reader.ReadLine();

                    if (isProcessingCommand == "True" || isProcessingCommand == "true")
                    {
                        ProcessingCommand = true;
                    }
                    else
                    {
                        ProcessingCommand = false;
                    }

                    _isReady = true;
                }
                else
                {
                    AccessToken = string.Empty;
                    UserId = 0;
                    ProcessingCommand = false;
                    _isReady = false;
                }
            }
            catch 
            {
                // ignore
            }
        }

        /// <summary>
        /// Save initial data
        /// </summary>
        public void Save()
        {
            try
            {
                using StreamWriter writer = new StreamWriter(@"Engine\vkSet.txt");
                writer.WriteLine(AccessToken);
                writer.WriteLine(UserId);
                writer.WriteLine(ProcessingCommand);

                if (!string.IsNullOrEmpty(AccessToken)
                    && UserId != 0)
                {
                    _isReady = true;
                }
            }
            catch
            {
                // ignore
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