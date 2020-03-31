/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.IO;
using OsEngine.Entity;
using OsEngine.Market;

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

        public bool MailSendOn;

        public bool MailSystemSendOn;
        public bool MailSignalSendOn;
        public bool MailErrorSendOn;
        public bool MailConnectSendOn;
        public bool MailTradeSendOn;
        public bool MailNoNameSendOn;

        public bool SmsSendOn;

        public bool SmsSystemSendOn;
        public bool SmsSignalSendOn;
        public bool SmsErrorSendOn;
        public bool SmsConnectSendOn;
        public bool SmsTradeSendOn;
        public bool SmsNoNameSendOn;

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
            ui.ShowDialog();
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

                    MailSendOn =  Convert.ToBoolean(reader.ReadLine());

                    MailSystemSendOn = Convert.ToBoolean(reader.ReadLine());
                    MailSignalSendOn = Convert.ToBoolean(reader.ReadLine());
                    MailErrorSendOn = Convert.ToBoolean(reader.ReadLine());
                    MailConnectSendOn = Convert.ToBoolean(reader.ReadLine());
                    MailTradeSendOn = Convert.ToBoolean(reader.ReadLine());
                    MailNoNameSendOn = Convert.ToBoolean(reader.ReadLine());

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
                    writer.WriteLine(MailSendOn);

                    writer.WriteLine(MailSystemSendOn);
                    writer.WriteLine(MailSignalSendOn);
                    writer.WriteLine(MailErrorSendOn);
                    writer.WriteLine(MailConnectSendOn);
                    writer.WriteLine(MailTradeSendOn);
                    writer.WriteLine(MailNoNameSendOn);

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

            if (MailSendOn)
            {
                if (message.Type == LogMessageType.Connect &&
                    MailConnectSendOn)
                {
                    ServerMail.GetServer().Send(message, _name);
                }
                if (message.Type == LogMessageType.Error &&
                MailErrorSendOn)
                {
                    ServerMail.GetServer().Send(message, _name);
                }
                if (message.Type == LogMessageType.Signal &&
                    MailSignalSendOn)
                {
                    ServerMail.GetServer().Send(message, _name);
                }
                if (message.Type == LogMessageType.System &&
                    MailSystemSendOn)
                {
                    ServerMail.GetServer().Send(message, _name);
                }
                if (message.Type == LogMessageType.Trade &&
                    MailTradeSendOn)
                {
                    ServerMail.GetServer().Send(message, _name);
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
