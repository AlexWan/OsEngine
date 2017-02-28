/*
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
    /// сервер почтновой рассылки
    /// </summary>
    public class ServerMail
    {
// синглетон
        private static ServerMail _server; // сервер рассылки

        /// <summary>
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

        private ServerMail() // конструктор
        {
            Smtp = "smtp.yandex.ru";
            Load();
        }

        /// <summary>
        /// адрес почты с которой будет происходить рассылка
        /// </summary>
        public string MyAdress;

        /// <summary>
        /// пароль от почты с которой будет происходить рассылка
        /// </summary>
        public string MyPassword;

        /// <summary>
        /// протокол SMTP почты отправителя
        /// </summary>
        public string Smtp; // "smtp.yandex.ru" "smtp.gmail.com"

        /// <summary>
        /// список адресатов
        /// </summary>
        public string[] Adress;

        /// <summary>
        /// готов ли сервер к работе
        /// </summary>
        public static bool IsReady;

        /// <summary>
        /// локер многопоточного доступа к серверу
        /// </summary>
        public object LokerMessanger = new object();

        /// <summary>
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
        /// показать окно настроек
        /// </summary>
        public void ShowDialog()
        {
            ServerMailDeliveryUi ui = new ServerMailDeliveryUi();
            ui.ShowDialog();
        }

        /// <summary>
        /// Отправить сообщение. Если сервер рассылки настроен, сообщение будет отправлено
        /// </summary>
        /// <param name="message"> сообщение </param>
        /// <param name="nameBot">имя робота, отправившего сообщение</param>
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
    /// отправщик сообщений
    /// </summary>
    public class MailThreadSaveSender
    {
        /// <summary>
        /// письмо
        /// </summary>
        public string Letter;

        /// <summary>
        /// бот
        /// </summary>
        public string NameBot;

        /// <summary>
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
        /// отправить 
        /// </summary>
        /// <param name="letter">письмо</param>
        /// <param name="nameBot">имя бота</param>
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
