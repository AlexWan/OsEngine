/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.IO;
using OsEngine.Entity;

namespace OsEngine.Logging
{
    /// <summary>
    /// distribution manager
    /// менеджер рассылки
    /// </summary>
    public class MessageSender
    {
 // distribution settings
 // настройки рассылки

        public bool WebhookSendOn;
        
        public bool WebhookSystemSendOn;
        public bool WebhookSignalSendOn;
        public bool WebhookErrorSendOn;
        public bool WebhookConnectSendOn;
        public bool WebhookTradeSendOn;
        public bool WebhookNoNameSendOn;

        public bool TelegramSendOn;

        public bool TelegramSystemSendOn;
        public bool TelegramSignalSendOn;
        public bool TelegramErrorSendOn;
        public bool TelegramConnectSendOn;
        public bool TelegramTradeSendOn;
        public bool TelegramNoNameSendOn;
        public bool TelegramUserSendOn;

        public bool SmsSendOn;

        public bool SmsSystemSendOn;
        public bool SmsSignalSendOn;
        public bool SmsErrorSendOn;
        public bool SmsConnectSendOn;
        public bool SmsTradeSendOn;
        public bool SmsNoNameSendOn;

        public bool VkSendOn;

        public bool VkSystemSendOn;
        public bool VkSignalSendOn;
        public bool VkErrorSendOn;
        public bool VkConnectSendOn;
        public bool VkTradeSendOn;
        public bool VkNoNameSendOn;
        public bool VkUserSendOn;

        private string _name; // name / имя

        /// <summary>
        /// program that created the object
        /// программа создавшая объект
        /// </summary>
        private StartProgram _startProgram;

        public MessageSender(string name, StartProgram startProgram)
        {
            _startProgram = startProgram;
            _name = name;
            Load();
        }

        /// <summary>
        /// show settings window
        /// показать окно настроек
        /// </summary>
        public void ShowDialog()
        {
            MessageSenderUi ui = new MessageSenderUi(this);
            ui.Show();
        }

        /// <summary>
        /// download
        /// загрузить
        /// </summary>
        private void Load() 
        {
            if (!File.Exists(@"Engine\" + _name + @"MessageSender.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + _name + @"MessageSender.txt"))
                {
                    SmsSendOn = Convert.ToBoolean(reader.ReadLine());

                    SmsSystemSendOn = Convert.ToBoolean(reader.ReadLine());
                    SmsSignalSendOn = Convert.ToBoolean(reader.ReadLine());
                    SmsErrorSendOn = Convert.ToBoolean(reader.ReadLine());
                    SmsConnectSendOn = Convert.ToBoolean(reader.ReadLine());
                    SmsTradeSendOn = Convert.ToBoolean(reader.ReadLine());
                    SmsNoNameSendOn = Convert.ToBoolean(reader.ReadLine());

                    WebhookSendOn = Convert.ToBoolean(reader.ReadLine());

                    WebhookSystemSendOn = Convert.ToBoolean(reader.ReadLine());
                    WebhookSignalSendOn = Convert.ToBoolean(reader.ReadLine());
                    WebhookErrorSendOn = Convert.ToBoolean(reader.ReadLine());
                    WebhookConnectSendOn = Convert.ToBoolean(reader.ReadLine());
                    WebhookTradeSendOn = Convert.ToBoolean(reader.ReadLine());
                    WebhookNoNameSendOn = Convert.ToBoolean(reader.ReadLine());

                    TelegramSendOn = Convert.ToBoolean(reader.ReadLine());

                    TelegramSystemSendOn = Convert.ToBoolean(reader.ReadLine());
                    TelegramSignalSendOn = Convert.ToBoolean(reader.ReadLine());
                    TelegramErrorSendOn = Convert.ToBoolean(reader.ReadLine());
                    TelegramConnectSendOn = Convert.ToBoolean(reader.ReadLine());
                    TelegramTradeSendOn = Convert.ToBoolean(reader.ReadLine());
                    TelegramNoNameSendOn = Convert.ToBoolean(reader.ReadLine());
                    TelegramUserSendOn = Convert.ToBoolean(reader.ReadLine());

                    VkSendOn = Convert.ToBoolean(reader.ReadLine());

                    VkSystemSendOn = Convert.ToBoolean(reader.ReadLine());
                    VkSignalSendOn = Convert.ToBoolean(reader.ReadLine());
                    VkErrorSendOn = Convert.ToBoolean(reader.ReadLine());
                    VkConnectSendOn = Convert.ToBoolean(reader.ReadLine());
                    VkTradeSendOn = Convert.ToBoolean(reader.ReadLine());
                    VkNoNameSendOn = Convert.ToBoolean(reader.ReadLine());
                    VkUserSendOn = Convert.ToBoolean(reader.ReadLine());

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }

        }

        /// <summary>
        /// save
        /// сохранить
        /// </summary>
        public void Save() 
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + _name + @"MessageSender.txt", false))
                {
                    writer.WriteLine(SmsSendOn);

                    writer.WriteLine(SmsSystemSendOn);
                    writer.WriteLine(SmsSignalSendOn);
                    writer.WriteLine(SmsErrorSendOn);
                    writer.WriteLine(SmsConnectSendOn);
                    writer.WriteLine(SmsTradeSendOn);
                    writer.WriteLine(SmsNoNameSendOn);

                    writer.WriteLine(WebhookSendOn);

                    writer.WriteLine(WebhookSystemSendOn);
                    writer.WriteLine(WebhookSignalSendOn);
                    writer.WriteLine(WebhookErrorSendOn);
                    writer.WriteLine(WebhookConnectSendOn);
                    writer.WriteLine(WebhookTradeSendOn);
                    writer.WriteLine(WebhookNoNameSendOn);

                    writer.WriteLine(TelegramSendOn);

                    writer.WriteLine(TelegramSystemSendOn);
                    writer.WriteLine(TelegramSignalSendOn);
                    writer.WriteLine(TelegramErrorSendOn);
                    writer.WriteLine(TelegramConnectSendOn);
                    writer.WriteLine(TelegramTradeSendOn);
                    writer.WriteLine(TelegramNoNameSendOn);
                    writer.WriteLine(TelegramUserSendOn);

                    writer.WriteLine(VkSendOn);

                    writer.WriteLine(VkSystemSendOn);
                    writer.WriteLine(VkSignalSendOn);
                    writer.WriteLine(VkErrorSendOn);
                    writer.WriteLine(VkConnectSendOn);
                    writer.WriteLine(VkTradeSendOn);
                    writer.WriteLine(VkNoNameSendOn);
                    writer.WriteLine(VkUserSendOn);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        /// <summary>
        /// delete
        /// удалить
        /// </summary>
        public void Delete() 
        {
            if (File.Exists(@"Engine\" + _name + @"MessageSender.txt"))
            {
                File.Delete(@"Engine\" + _name + @"MessageSender.txt");
            }
        }

        /// <summary>
        /// Send message. If this message type is subscribed and distribution servers are configured, the message will be sent
        /// If test server is enabled, the message will not be sent
        /// Отправить сообщение. Если такой тип сообщений подписан на рассылку и сервера рассылки настроены, сообщение будет отправлено
        /// Если включен тестовый сервер - сообщение не будет отправленно
        /// </summary>
        public void AddNewMessage(LogMessage message)
        {
            if (_startProgram != StartProgram.IsOsTrader)
            {
                return;
            }

            if (TelegramSendOn)
            {
                if (message.Type == LogMessageType.Connect &&
                    TelegramConnectSendOn)
                {
                    ServerTelegram.GetServer().Send(message, _name);
                }
                if (message.Type == LogMessageType.Error &&
                    TelegramErrorSendOn)
                {
                    ServerTelegram.GetServer().Send(message, _name);
                }
                if (message.Type == LogMessageType.Signal &&
                    TelegramSignalSendOn)
                {
                    ServerTelegram.GetServer().Send(message, _name);
                }
                if (message.Type == LogMessageType.System &&
                    TelegramSystemSendOn)
                {
                    ServerTelegram.GetServer().Send(message, _name);
                }
                if (message.Type == LogMessageType.Trade &&
                    TelegramTradeSendOn)
                {
                    ServerTelegram.GetServer().Send(message, _name);
                }
                if (message.Type == LogMessageType.User &&
                    TelegramUserSendOn)
                {
                    ServerTelegram.GetServer().Send(message, _name);
                }
            }

            if (VkSendOn)
            {
                if (message.Type == LogMessageType.Connect 
                    && VkConnectSendOn)
                {
                    ServerVk.GetServer().Send(message, _name);
                }
                if (message.Type == LogMessageType.Error 
                    && VkErrorSendOn)
                {
                    ServerVk.GetServer().Send(message, _name);
                }
                if (message.Type == LogMessageType.Signal 
                    && VkSignalSendOn)
                {
                    ServerVk.GetServer().Send(message, _name);
                }
                if (message.Type == LogMessageType.System 
                    && VkSystemSendOn)
                {
                    ServerVk.GetServer().Send(message, _name);
                }
                if (message.Type == LogMessageType.Trade 
                    && VkTradeSendOn)
                {
                    ServerVk.GetServer().Send(message, _name);
                }
                if (message.Type == LogMessageType.User 
                    && VkUserSendOn)
                {
                    ServerVk.GetServer().Send(message, _name);
                }
            }

            if (WebhookSendOn)
            {
                if (message.Type == LogMessageType.Connect &&
                    WebhookConnectSendOn)
                {
                    ServerWebhook.GetServer().Send(message, _name);
                }
                if (message.Type == LogMessageType.Error &&
                    WebhookErrorSendOn)
                {
                    ServerWebhook.GetServer().Send(message, _name);
                }
                if (message.Type == LogMessageType.Signal &&
                    WebhookSignalSendOn)
                {
                    ServerWebhook.GetServer().Send(message, _name);
                }
                if (message.Type == LogMessageType.System &&
                    WebhookSystemSendOn)
                {
                    ServerWebhook.GetServer().Send(message, _name);
                }
                if (message.Type == LogMessageType.Trade &&
                    WebhookTradeSendOn)
                {
                    ServerWebhook.GetServer().Send(message, _name);
                }
            }

            if (SmsSendOn)
            {
                if (message.Type == LogMessageType.Connect &&
                    SmsConnectSendOn)
                {
                    ServerSms.GetSmsServer().Send(message.GetString());
                }
                if (message.Type == LogMessageType.Error && 
                    SmsErrorSendOn)
                {
                    ServerSms.GetSmsServer().Send(message.GetString());
                }
                if (message.Type == LogMessageType.Signal &&
                    SmsSignalSendOn)
                {
                    ServerSms.GetSmsServer().Send(message.GetString());
                }
                if (message.Type == LogMessageType.System &&
                    SmsSystemSendOn)
                {
                    ServerSms.GetSmsServer().Send(message.GetString());
                }
                if (message.Type == LogMessageType.Trade &&
                    SmsTradeSendOn)
                {
                    ServerSms.GetSmsServer().Send(message.GetString());
                }
            }
        }
    }
}
