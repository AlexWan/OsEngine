/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Threading;
using Newtonsoft.Json;
using OsEngine.OsTrader.Gui;
using RestSharp;
using Application = System.Windows.Application;
using Color = System.Drawing.Color;

namespace OsEngine.Logging
{
    /// <summary>
    /// webhook messaging server
    /// сервер рассылки сообщений через вебхуки
    /// </summary>
    public class ServerWebhook
    {
// singleton
// синглетон
        private static ServerWebhook _server; // webhook messaging server / сервер рассылки сообщений через вебхуки

        /// <summary>
        /// get access to server
        /// получить доступ к серверу 
        /// </summary>
        /// <returns></returns>
        public static ServerWebhook GetServer()
        {
            if (_server == null)
            {
                _server = new ServerWebhook();
            }
            return _server;
        }

        private ServerWebhook() // constructor / конструктор
        {
            Load();
        }

        /// <summary>
        /// webhooks to send outК
        /// вебхуки, на которые будет происходить рассылка
        /// </summary>
        public string[] Webhooks;

        /// <summary>
        /// Slack bot token for screenshots
        /// Токен слак бота для отправки скриншотов
        /// </summary>
        public string SlackBotToken;

        /// <summary>
        /// Will send chart screenshots (it supports slack and discord)
        /// Отправлять скриншоты графика (поддерживаются slack и discord)
        /// </summary>
        public bool MustSendChartScreenshots;

        /// <summary>
        /// shows whether the server is ready to work
        /// готов ли сервер к работе
        /// </summary>
        public static bool IsReady;

        /// <summary>
        /// locker of multithreading access to server
        /// локер многопоточного доступа к серверу
        /// </summary>
        public object LokerMessanger = new object();

        /// <summary>
        /// upload
        /// загрузить
        /// </summary>
        public void Load()
        {
            if (File.Exists(@"Engine\webhookSet.txt"))
            {
                StreamReader reader = new StreamReader(@"Engine\webhookSet.txt");

                SlackBotToken = reader.ReadLine();
                MustSendChartScreenshots = Convert.ToBoolean(reader.ReadLine());

                IsReady = false;
                for (int i = 0; !reader.EndOfStream; i++)
                {
                    if (Webhooks == null || Webhooks[0] == null)
                    {
                        Webhooks = new string[1];
                        Webhooks[0] = reader.ReadLine();
                        IsReady = true;
                    }
                    else
                    {
                        string[] newWebhooks = new string[Webhooks.Length + 1];

                        for (int ii = 0; ii < Webhooks.Length; ii++)
                        {
                            newWebhooks[ii] = Webhooks[ii];
                        }

                        newWebhooks[newWebhooks.Length - 1] = reader.ReadLine();
                        Webhooks = newWebhooks;
                        IsReady = true;
                    }

                }

                reader.Close();

            }
            else
            {
                SlackBotToken = string.Empty;
                MustSendChartScreenshots = false;
                IsReady = false;
            }
        }

        /// <summary>
        /// save
        /// сохранить
        /// </summary>
        public void Save()
        {
            StreamWriter writer = new StreamWriter(@"Engine\webhookSet.txt");
            writer.WriteLine(SlackBotToken);
            writer.WriteLine(MustSendChartScreenshots);
            IsReady = false;
            if (Webhooks != null && Webhooks[0] != null)
            {
                for (int i = 0; i < Webhooks.Length; i++)
                {
                    IsReady = true;
                    writer.WriteLine(Webhooks[i]);
                }
            }
            writer.Close();
        }

        /// <summary>
        /// show settings window
        /// показать окно настроек
        /// </summary>
        public void ShowDialog()
        {
            ServerWebhookDeliveryUi ui = new ServerWebhookDeliveryUi();
            ui.ShowDialog();
        }

        /// <summary>
        /// Send message. If the distribution server is configured, the message will be sent
        /// Отправить сообщение. Если сервер рассылки настроен, сообщение будет отправлено
        /// </summary>
        /// <param name="message"> message / сообщение </param>
        /// <param name="nameBot"> name of bot that sent the message / имя робота, отправившего сообщение </param>
        public void Send(LogMessage message, string nameBot)
        {
            if (!IsReady)
            {
                return;
            }

            WebhookThreadSaveSender sender = new WebhookThreadSaveSender();
            sender.Message = message;
            sender.NameBot = nameBot;
            Thread worker = new Thread(sender.Send);
            worker.CurrentCulture = new CultureInfo("ru-RU");
            worker.IsBackground = true;
            worker.Start();
        }
    }

    /// <summary>
    /// message sender
    /// отправщик сообщений
    /// </summary>
    public class WebhookThreadSaveSender
    {
        /// <summary>
        /// message
        /// сообщение
        /// </summary>
        public LogMessage Message;

        /// <summary>
        /// bot
        /// бот
        /// </summary>
        public string NameBot;

        /// <summary>
        /// send
        /// отправить
        /// </summary>
        public void Send()
        {
            byte[] screenshot = null;

            lock (ServerWebhook.GetServer().LokerMessanger)
            {
                for (int i = 0; i < ServerWebhook.GetServer().Webhooks.Length; i++)
                {
                    Send(ServerWebhook.GetServer().Webhooks[i], Message, NameBot, screenshot);
                }
            }
        }

        /// <summary>
        /// must send screenshot for the message?
        /// должны отправить скриншот для сообщения?
        /// </summary>
        private bool MustSendScreenshotFor(LogMessage message)
        {
            if (!ServerWebhook.GetServer().MustSendChartScreenshots)
            {
                return false;
            }

            switch (message.Type)
            {
                case LogMessageType.Trade:
                case LogMessageType.Signal:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// send
        /// отправить 
        /// </summary>
        /// <param name="message"> message / сообщение </param>
        /// <param name="botName"> bot name / имя бота</param>
        /// <param name="webhook"> webhook / вебхук </param>
        public void Send(string webhook, LogMessage message, string botName, byte[] file = null)
        {
            try
            {
                if (webhook.StartsWith("https://discordapp.com/api/webhooks/"))
                {
                    SendDiscordMessage(webhook, message, botName, file);
                }
                else if (webhook.StartsWith("https://hooks.slack.com/services/"))
                {
                    SendSlackMessage(webhook, message, botName, file);
                }
                else if (webhook.StartsWith("https://api.telegram.org/bot"))
                {
                    SendTelegramMessage(webhook, message, botName);
                }
                else
                {
                    SendRawMessage(webhook, message, botName);
                }
            }
            catch
            {
                // ingored
            }
        }

        #region Discord messaging

        // HOWTO
        //
        // 1 Go to "Server settings" - "Webhooks"
        // 2 Press "Create webhook"
        //
        // 1 Откройте "Настройки сервера" - "Вебхуки"
        // 2 Нажмите кнопку "Создать вебхук"

        /// <summary>
        /// send message to Discord
        /// отправить сообщение в Discord
        /// </summary>
        /// <param name="webhook"> webhook / вебхук </param>
        /// <param name="message"> log message / сообщение из лога </param>
        /// <param name="botName"> bot name / имя бота </param>
        /// <param name="file"> file (chart screenshot) / скриншот чарта </param>
        private void SendDiscordMessage(string webhook, LogMessage message, string botName, byte[] file = null)
        {
            string payload = SerializedString(DiscordPayload(message, botName));

            if (file != null)
            { // send file and message together / отправить файл и сообщение вместе

                var request = new RestRequest(Method.POST);
                request.AddParameter("payload_json", payload);
                request.AddFile("chart_pic", file, $"chart_{WebUtcTime(message.Time)}.png");
                request.AddHeader("Content-Type", "multipart/form-data");
                var response = new RestClient(webhook).Execute(request);
            }
            else
            { // send message only / отправить только сообщение

                PostJson(webhook, payload);
            }
        }

        /// <summary>
        /// serialize message to Discord
        /// сериализовать сообщение в Discord
        /// </summary>
        /// <param name="message"> log message / сообщение из лога </param>
        /// <param name="botName"> bot name / имя бота </param>
        private DiscordMessage DiscordPayload(LogMessage message, string botName)
        {
            return new DiscordMessage()
            {
                Embeds = new DiscordMessageEmbed[]
                {
                    new DiscordMessageEmbed()
                    {
                        Title = FormattedBotName(botName),
                        Timestamp = WebUtcTime(message.Time),
                        Color = MessageColor(message, "decimal"),
                        Fields = new DiscordMessageEmbedField[]
                        {
                            new DiscordMessageEmbedField()
                            {
                                Name = FormattedMessage(message),
                                Value = FormattedMessageType(message),
                                Inline = false
                            },
                        }
                    }
                }
            };
        }

        public sealed class DiscordMessage
        {
            [JsonProperty("content")] public string Content { get; set; }

            [JsonProperty("embeds")] public DiscordMessageEmbed[] Embeds { get; set; }
        }

        public sealed class DiscordMessageEmbed
        {
            [JsonProperty("title")] public string Title { get; set; }

            [JsonProperty("desctiption")] public string Description { get; set; }

            [JsonProperty("timestamp")] public string Timestamp { get; set; }

            [JsonProperty("color")] public string Color { get; set; }

            [JsonProperty("fields")] public DiscordMessageEmbedField[] Fields { get; set; }
        }

        public sealed class DiscordMessageEmbedField
        {
            [JsonProperty("name")] public string Name { get; set; }

            [JsonProperty("value")] public string Value { get; set; }

            [JsonProperty("inline")] public bool Inline { get; set; }
        }
        #endregion

        #region Slack messaging

        // HOWTO
        //
        // 1 Create Slack app
        // 2 Set slack app scopes: https://api.slack.com/apps/<app_id>/oauth
        //   "OAuth & Permissions" - "Scopes" - "Upload and modify files as user"
        //   (it is only necessary for screenshots)
        // 3 Switch on webhooks: https://api.slack.com/apps/<app_id>/incoming-webhooks "Incoming Webhooks" - "On"
        // 4 Add new webhook: https://api.slack.com/apps/<app_id>/incoming-webhooks "Add New Webhook to Workspace"
        //
        // 1 Создайте приложение Slack
        // 2 Установите scopes для приложения: https://api.slack.com/apps/<app_id>/oauth
        //   "OAuth & Permissions" - "Scopes" - "Upload and modify files as user"
        //   (это нужно только для отправки скриншотов)
        // 3 Включите вебхуки: https://api.slack.com/apps/<app_id>/incoming-webhooks "Incoming Webhooks" - "On"
        // 4 Добавьте новый вебхук: https://api.slack.com/apps/<app_id>/incoming-webhooks "Add New Webhook to Workspace"

        /// <summary>
        /// send message to Slack
        /// отправить сообщение в Slack
        /// </summary>
        /// <param name="webhook"> webhook / вебхук </param>
        /// <param name="message"> log message / сообщение из лога </param>
        /// <param name="botName"> bot name / имя бота </param>
        /// <param name="file"> file (chart screenshot) / скриншот чарта </param>
        private void SendSlackMessage(string webhook, LogMessage message, string botName, byte[] file = null)
        {
            SlackMessage payload = SlackPayload(message, botName);
            string uploadToken = ServerWebhook.GetServer().SlackBotToken;

            if (!String.IsNullOrEmpty(uploadToken) && file != null)
            { // upload file to Slack / загрузить файл в Slack

                string uploadUrl = "https://slack.com/api/files.upload";

                var uploadRequest = new RestRequest(Method.POST);
                uploadRequest.AddParameter("token", uploadToken);
                uploadRequest.AddFile("file", file, $"chart_{WebUtcTime(message.Time)}.png");
                uploadRequest.AddHeader("Content-Type", "multipart/form-data");
                var uploadResult = new RestClient(uploadUrl).Execute(uploadRequest);

                if (uploadResult.StatusCode == HttpStatusCode.OK)
                { // make file as public (must be public to send it to chat)
                  // сделать файл публичным (должен быть публичным для отправки его в чат)

                    string uploadResultJson = uploadResult.Content;
                    string imageId = JsonConvert.DeserializeObject<SlackUploadResult>(uploadResultJson).File.Id;
                    string shareImageUrl = "https://slack.com/api/files.sharedPublicURL";

                    var shareImageRequest = new RestRequest(Method.POST);
                    shareImageRequest.AddParameter("token", uploadToken);
                    shareImageRequest.AddParameter("file", imageId);
                    shareImageRequest.AddHeader("Content-Type", "application/json");
                    var shareImageResult = new RestClient(shareImageUrl).Execute(shareImageRequest);

                    if (shareImageResult.StatusCode == HttpStatusCode.OK)
                    { // will serialize message with link to the file / будем сериализовать сообщение со ссылкой на файл

                        string shareImageResultJson = shareImageResult.Content;
                        string imageUrl = JsonConvert.DeserializeObject<SlackUploadResult>(shareImageResultJson).File.Permalink_public;
                        payload.Attachments[0].Image_url = imageUrl;
                    }
                }
            }

            PostJson(webhook, SerializedString(payload));
        }

        /// <summary>
        /// serialize message to Slack
        /// сериализовать сообщение в Slack
        /// </summary>
        /// <param name="message"> log message / сообщение из лога </param>
        /// <param name="botName"> bot name / имя бота </param>
        private SlackMessage SlackPayload(LogMessage message, string botName)
        {
            return new SlackMessage()
            {
                Attachments = new SlackMessageAttachment[]
                {
                    new SlackMessageAttachment()
                    {
                        Title = FormattedBotName(botName),
                        Footer =FormattedMessageType(message),
                        Ts = UnixEpochTimestamp(message.Time),
                        Color = MessageColor(message),
                        Fields = new SlackMessageAttachmentField[]
                        {
                            new SlackMessageAttachmentField()
                            {
                                Title = FormattedMessage(message),
                                Short = false
                            },
                        }
                    }
                }
            };
        }

        public sealed class SlackMessage
        {
            [JsonProperty("text")] public string Text { get; set; }

            [JsonProperty("mrkdwn")] public bool Mrkdwn { get; set; }

            [JsonProperty("attachments")] public SlackMessageAttachment[] Attachments { get; set; }
        }

        public sealed class SlackMessageAttachment
        {
            [JsonProperty("title")] public string Title { get; set; }

            [JsonProperty("text")] public string Text { get; set; }

            [JsonProperty("color")] public string Color { get; set; }

            [JsonProperty("image_url")] public string Image_url { get; set; }

            [JsonProperty("fields")] public SlackMessageAttachmentField[] Fields { get; set; }

            [JsonProperty("footer")] public string Footer { get; set; }

            [JsonProperty("ts")] public string Ts { get; set; }
        }

        public sealed class SlackMessageAttachmentField
        {
            [JsonProperty("title")] public string Title { get; set; }

            [JsonProperty("value")] public string Value { get; set; }

            [JsonProperty("short")] public bool Short { get; set; }
        }

        public sealed class SlackUploadResult
        {
            [JsonProperty("file")] public SlackUploadResultFile File { get; set; }
        }

        public sealed class SlackUploadResultFile
        {
            [JsonProperty("id")] public string Id { get; set; }

            [JsonProperty("permalink_public")] public string Permalink_public { get; set; }

            [JsonProperty("thumb_360")] public string Thumb_360 { get; set; }
        }
        #endregion

        #region Telegram messaging

        // HOWTO
        //
        // !!! IT WONT WORK WITH RUSSIAN IPS !!!
        // 1 Create Telegram bot
        // 2 Add bot and type /start command
        // 3 Open in browser https://api.telegram.org/bot<bot-token>/getUpdates
        // 4 Get message.chat.id from the response
        // 5 Set a webhook https://api.telegram.org/bot<bot-token>/sendMessage?chat_id=<id>
        //
        // !!! НЕ БУДЕТ РАБОТАТЬ В РОССИИ !!!
        // 1 Создать Telegram бота
        // 2 Добавить бота и набрать команду /start
        // 3 Открыть в браузере https://api.telegram.org/bot<bot-token>/getUpdates
        // 4 Взять message.chat.id из полученного ответа
        // 5 Сформировать вебхук https://api.telegram.org/bot<bot-token>/sendMessage?chat_id=<id>

        /// <summary>
        /// send message to Telegram
        /// отправить сообщение в Telegram
        /// </summary>
        /// <param name="webhook"> webhook / вебхук </param>
        /// <param name="message"> log message / сообщение из лога </param>
        /// <param name="botName"> bot name / имя бота </param>
        private void SendTelegramMessage(string webhook, LogMessage message, string botName)
        {
            PostJson(webhook, SerializedString(TelegramPayload(message, botName)));
        }

        /// <summary>
        /// serialize message to Telegram
        /// сериализовать сообщение в Telegram
        /// </summary>
        /// <param name="message"> log message / сообщение из лога </param>
        /// <param name="botName"> bot name / имя бота </param>
        private TelegramMessage TelegramPayload(LogMessage message, string botName)
        {
            return new TelegramMessage()
            {
                Text = $"<b>{botName}</b>" +
                       $"\n\n<pre>{FormattedMessage(message)}</pre>" +
                       $"\n\n<i>{MessageEntityCode(message)} {message.Type}</i>",
                Parse_mode = "HTML",
                Disable_notification = false
            };
        }

        public sealed class TelegramMessage
        {
            [JsonProperty("text")] public string Text { get; set; }

            [JsonProperty("parse_mode")] public string Parse_mode { get; set; }

            [JsonProperty("disable_notification")] public bool Disable_notification { get; set; }
        }
        #endregion

        #region Raw messaging

        /// <summary>
        /// send raw message to custom webhook
        /// отправить сырое сообщение на кастомный вебхук
        /// </summary>
        /// <param name="webhook"> webhook / вебхук </param>
        /// <param name="message"> log message / сообщение из лога </param>
        /// <param name="botName"> bot name / имя бота </param>
        private void SendRawMessage(string webhook, LogMessage message, string botName)
        {
            PostJson(webhook, SerializedString(RawPayload(message, botName)));
        }

        /// <summary>
        /// serialize message
        /// сериализовать сообщение
        /// </summary>
        /// <param name="message"> log message / сообщение из лога </param>
        /// <param name="botName"> bot name / имя бота </param>
        private RawMessage RawPayload(LogMessage message, string botName)
        {
            return new RawMessage()
            {
                Bot = botName,
                Message = message.Message,
                Type = message.Type.ToString(),
                Time = message.Time.ToString(),
                Color = MessageColor(message)
            };
        }

        public sealed class RawMessage
        {
            [JsonProperty("bot")] public string Bot { get; set; }

            [JsonProperty("message")] public string Message { get; set; }

            [JsonProperty("type")] public string Type { get; set; }

            [JsonProperty("time")] public string Time { get; set; }

            [JsonProperty("color")] public string Color { get; set; }
        }
        #endregion

        #region Helpers

        private void PostJson(string webhook, string payload)
        {
            var request = new RestRequest(Method.POST);
            request.AddParameter("application/json", payload, ParameterType.RequestBody);
            var response = new RestClient(webhook).Execute(request);
        }

        private string SerializedString(object payload)
        {
            return JsonConvert.SerializeObject(payload, new JsonSerializerSettings
            {
                StringEscapeHandling = StringEscapeHandling.EscapeNonAscii
            });
        }

        private string MessageColor(LogMessage message, string format = "hexadecimal")
        {
            string hexColor;

            switch (message.Type)
            {
                case LogMessageType.Trade:
                    hexColor = "55aa00";
                    break;
                case LogMessageType.Signal:
                    hexColor = "ffaa00";
                    break;
                case LogMessageType.Connect:
                case LogMessageType.Error:
                    hexColor = "f44242";
                    break;
                default:
                    hexColor = "edefff";
                    break;
            }

            switch (format)
            {
                case "decimal":
                    return Convert.ToInt32(hexColor, 16).ToString();
                case "hexadecimal":
                    return hexColor;
                default:
                    return hexColor;
            }
        }
        
        private string MessageEntityCode(LogMessage message)
        {
            string entityCode;

            switch (message.Type)
            {
                case LogMessageType.Trade:
                    entityCode = "&#9989;";
                    break;
                case LogMessageType.Signal:
                    entityCode = "&#9889;";
                    break;
                case LogMessageType.Error:
                    entityCode = "&#10060;";
                    break;
                case LogMessageType.User:
                    entityCode = "&#129333;";
                    break;
                case LogMessageType.Connect:
                    entityCode = "&#128279;";
                    break;
                default:
                    entityCode = "&#9881;";
                    break;
            }

            return entityCode;
        }

        private string FormattedBotName(string nameBot)
        {
            return ":robot_face: " + nameBot;
        }

        private string FormattedMessage(LogMessage message)
        {
            return message.Message;
        }

        private string FormattedMessageType(LogMessage message)
        {
            return ":gear: " + message.Type;
        }

        private string WebUtcTime(DateTime time)
        {
            return time.ToUniversalTime().ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
        }

        private string UnixEpochTimestamp(DateTime time)
        {
            return (time.ToUniversalTime() - new DateTime(1970, 1, 1)).TotalSeconds.ToString();
        }
        #endregion
    }
}
