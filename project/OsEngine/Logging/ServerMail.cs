/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading;

namespace OsEngine.Logging
{
    /// <summary>
    /// mailing server
    /// сервер почтновой рассылки
    /// </summary>
    public class ServerMail
    {
// singleton
// синглетон
        private static ServerMail _server; // mailing server / сервер рассылки

        /// <summary>
        /// get access to server
        /// получить доступ к серверу 
        /// </summary>
        /// <returns></returns>
        public static ServerMail GetServer()
        {
            if (_server == null)
            {
                _server = new ServerMail();
            }
            return _server;
        }

        private ServerMail() // constructor / конструктор
        {
            Smtp = "smtp.yandex.ru";
            Load();
        }

        /// <summary>
        /// e-mail address to send out
        /// адрес почты с которой будет происходить рассылка
        /// </summary>
        public string MyAdress;

        /// <summary>
        /// password from the e-mail to send out
        /// пароль от почты с которой будет происходить рассылка
        /// </summary>
        public string MyPassword;

        /// <summary>
        /// sender's SMTP protocol
        /// протокол SMTP почты отправителя
        /// </summary>
        public string Smtp; // "smtp.yandex.ru" "smtp.gmail.com"

        /// <summary>
        /// mailing list
        /// список адресатов
        /// </summary>
        public string[] Adress;

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
            if (File.Exists(@"Engine\mailSet.txt"))
            {
                StreamReader reader = new StreamReader(@"Engine\mailSet.txt");

                MyAdress = reader.ReadLine();
                MyPassword = reader.ReadLine();
                Smtp = reader.ReadLine();
                IsReady = false;
                for (int i = 0; !reader.EndOfStream; i++)
                {
                    if (Adress == null || Adress[0] == null)
                    {
                        Adress = new string[1];
                        Adress[0] = reader.ReadLine();
                        IsReady = true;
                    }
                    else
                    {
                        string[] newAdress = new string[Adress.Length+1];

                        for (int ii = 0; ii < Adress.Length; ii++)
                        {
                            newAdress[ii] = Adress[ii];
                        }
                        
                        newAdress[newAdress.Length -1] = reader.ReadLine();
                        Adress = newAdress;
                        IsReady = true;
                    }

                }

                reader.Close();

            }
            else
            {
                MyAdress = string.Empty;
                MyPassword = string.Empty;
                Smtp = string.Empty;
                IsReady = false;
            }
        }

        /// <summary>
        /// save
        /// сохранить
        /// </summary>
        public void Save()
        {
            StreamWriter writer = new StreamWriter(@"Engine\mailSet.txt");
            writer.WriteLine(MyAdress);
            writer.WriteLine(MyPassword);
            writer.WriteLine(Smtp);
            IsReady = false;
            if (Adress != null && Adress[0] != null)
            {
                for (int i = 0; i < Adress.Length; i++)
                {
                    IsReady = true;
                    writer.WriteLine(Adress[i]);
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
            ServerMailDeliveryUi ui = new ServerMailDeliveryUi();
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

            MailThreadSaveSender sender = new MailThreadSaveSender();
            sender.Letter = message.GetString();
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
    public class MailThreadSaveSender
    {
        /// <summary>
        /// letter
        /// письмо
        /// </summary>
        public string Letter;

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
            lock (ServerMail.GetServer().LokerMessanger)
            {
                for (int i = 0; i < ServerMail.GetServer().Adress.Length; i++)
                {
                    Send(Letter, NameBot, ServerMail.GetServer().Adress[i]);
                }
            }
        }

        /// <summary>
        /// send
        /// отправить 
        /// </summary>
        /// <param name="letter"> letter / письмо </param>
        /// <param name="nameBot"> bot name / имя бота</param>
        /// <param name="adress">адрес</param>
        public void Send(string letter, string nameBot, string adress)
        {
            try
            {
                MailMessage mail = new MailMessage();

                mail.From = new MailAddress(ServerMail.GetServer().MyAdress);
                mail.To.Add(new MailAddress(adress));
                mail.Subject = "Bot_" + nameBot;
                mail.Body = letter;

                SmtpClient client = new SmtpClient();
                client.Host = ServerMail.GetServer().Smtp;
                client.Port = 587;
                client.EnableSsl = true;
                client.Credentials = new NetworkCredential(ServerMail.GetServer().MyAdress.Split('@')[0], ServerMail.GetServer().MyPassword);
                client.DeliveryMethod = SmtpDeliveryMethod.Network;
                client.Send(mail);
                mail.Dispose();
            }
            catch
            {
                 // ingored
            }
        }
    }

}
