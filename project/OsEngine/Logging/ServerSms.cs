/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.IO;
using System.Net;
using System.Text;

namespace OsEngine.Logging
{
    /// <summary>
    /// сервер рассылки смс сообщений
    /// </summary>
    public class ServerSms
    {
        // синглетон

        private static ServerSms _server; // сервер в одном экземпляре

        public static ServerSms GetSmsServer() // синглетон
        {
            if (_server == null)
            {
                _server = new ServerSms();
            }
            return _server;
        }

        // сервис

        private ServerSms() // конструктор
        {
            Load();
        }

        private void Load() // загрузить
        {
            if (File.Exists(@"Engine\smsSet.txt"))
            {
                StreamReader reader = new StreamReader(@"Engine\smsSet.txt");

                SmscLogin = reader.ReadLine();
                SmscPassword = reader.ReadLine();
                Phones = reader.ReadLine();

                reader.Close();
            }

        }

        public void Save() // сохранить
        {
            StreamWriter writer = new StreamWriter(@"Engine\smsSet.txt");
            writer.WriteLine(SmscLogin);
            writer.WriteLine(SmscPassword);
            writer.WriteLine(Phones);

            writer.Close();
        }

        public void ShowDialog() // показать меню
        {
            ServerSmsUi ui = new ServerSmsUi();
            ui.ShowDialog();
        }


        // Параметры отправки
        public string SmscLogin = "login";		    // логин клиента
        public string SmscPassword = "password";	// пароль или MD5-хеш пароля в нижнем регистре
        public bool SmscPost;				        // использовать метод POST
        public bool SmscHttps = false;				// использовать HTTPS протокол
        public string SmscCharset = "utf-8";        // кодировка сообщения (windows-1251 или koi8-r), по умолчанию используется utf-8
        public bool SmscDebug = false;				// флаг отладки
        public string[][] D2Res;

        public string Phones;

        /// <summary>
        /// отправить сообщение
        /// </summary>
        /// <param name="message"></param>
        public void Send(string message)
        {
            if (string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(Phones))
            {
                return;
            }
            send_sms(Phones, message, 0, "", 0, 0, "", "", null);
        }

        // Метод отправки SMS
        //
        // обязательные параметры:
        //
        // phones - список телефонов через запятую или точку с запятой
        // message - отправляемое сообщение
        //
        // необязательные параметры:
        //
        // translit - переводить или нет в транслит
        // time - необходимое время доставки в виде строки (DDMMYYhhmm, h1-h2, 0ts, +m)
        // id - идентификатор сообщения. Представляет собой 32-битное число в диапазоне от 1 до 2147483647.
        // format - формат сообщения (0 - обычное sms, 1 - flash-sms, 2 - wap-push, 3 - hlr, 4 - bin, 5 - bin-hex, 6 - ping-sms, 7 - mms, 8 - mail, 9 - call)
        // sender - имя отправителя (Sender ID). Для отключения Sender ID по умолчанию необходимо в качестве имени
        // передать пустую строку или точку.
        // query - строка дополнительных параметров, добавляемая в URL-запрос ("valid=01:00&maxsms=3")
        //
        // возвращает массив строк (<id>, <количество sms>, <стоимость>, <баланс>) в случае успешной отправки
        // либо массив строк (<id>, -<код ошибки>) в случае ошибки

        private string[] send_sms(string phones, string message, int translit = 0, string time = "", int id = 0, int format = 0, string sender = "", string query = "", string[] files = null)
        {
            if (files != null)
            {
                SmscPost = true;
            }
            else
            {
                SmscPost = false;
            }
                

            string[] formats = {"flash=1", "push=1", "hlr=1", "bin=1", "bin=2", "ping=1", "mms=1", "mail=1", "call=1"};

            string[] m = _smsc_send_cmd("send", "cost=3&phones=" + _urlencode(phones)
                                                + "&mes=" + _urlencode(message) + "&id=" + id.ToString() + "&translit=" + translit.ToString()
                                                + (format > 0 ? "&" + formats[format-1] : "") + (sender != "" ? "&sender=" + _urlencode(sender) : "")
                                                + (time != "" ? "&time=" + _urlencode(time) : "") + (query != "" ? "&" + query : ""), files);

            // (id, cnt, cost, balance) или (id, -error)

            if (SmscDebug) 
            {
                if (Convert.ToInt32(m[1]) > 0)
                    _print_debug("Сообщение отправлено успешно. ID: " + m[0] + ", всего SMS: " + m[1] + ", стоимость: " + m[2] + ", баланс: " + m[3]);
                else
                    _print_debug("Ошибка №" + m[1].Substring(1, 1) + (m[0] != "0" ? ", ID: " + m[0] : ""));
            }

            return m;
        }

        // Метод получения стоимости SMS
        //
        // обязательные параметры:
        //
        // phones - список телефонов через запятую или точку с запятой
        // message - отправляемое сообщение 
        //
        // необязательные параметры:
        //
        // translit - переводить или нет в транслит
        // format - формат сообщения (0 - обычное sms, 1 - flash-sms, 2 - wap-push, 3 - hlr, 4 - bin, 5 - bin-hex, 6 - ping-sms, 7 - mms, 8 - mail, 9 - call)
        // sender - имя отправителя (Sender ID)
        // query - строка дополнительных параметров, добавляемая в URL-запрос ("list=79999999999:Ваш пароль: 123\n78888888888:Ваш пароль: 456")
        //
        // возвращает массив (<стоимость>, <количество sms>) либо массив (0, -<код ошибки>) в случае ошибки

        public string[] get_sms_cost(string phones, string message, int translit = 0, int format = 0, string sender = "", string query = "")
        {
            string[] formats = {"flash=1", "push=1", "hlr=1", "bin=1", "bin=2", "ping=1", "mms=1", "mail=1", "call=1"};

            string[] m = _smsc_send_cmd("send", "cost=1&phones=" + _urlencode(phones)
                                                + "&mes=" + _urlencode(message) + translit.ToString() + (format > 0 ? "&" + formats[format-1] : "")
                                                + (sender != "" ? "&sender=" + _urlencode(sender) : "") + (query != "" ? "&query" : ""));

            // (cost, cnt) или (0, -error)

            if (SmscDebug) {
                if (Convert.ToInt32(m[1]) > 0)
                    _print_debug("Стоимость рассылки: " + m[0] + ". Всего SMS: " + m[1]);
                else
                    _print_debug("Ошибка №" + m[1].Substring(1, 1));
            }

            return m;
        }

        // Метод проверки статуса отправленного SMS или HLR-запроса
        //
        // id - ID cообщения или список ID через запятую
        // phone - номер телефона или список номеров через запятую
        // all - вернуть все данные отправленного SMS, включая текст сообщения (0,1 или 2)
        //
        // возвращает массив (для множественного запроса возвращается массив с единственным элементом, равным 1. В этом случае статусы сохраняются в
        //					двумерном динамическом массиве класса D2Res):
        //
        // для одиночного SMS-сообщения:
        // (<статус>, <время изменения>, <код ошибки доставки>)
        //
        // для HLR-запроса:
        // (<статус>, <время изменения>, <код ошибки sms>, <код IMSI SIM-карты>, <номер сервис-центра>, <код страны регистрации>, <код оператора>,
        // <название страны регистрации>, <название оператора>, <название роуминговой страны>, <название роумингового оператора>)
        //
        // при all = 1 дополнительно возвращаются элементы в конце массива:
        // (<время отправки>, <номер телефона>, <стоимость>, <sender id>, <название статуса>, <текст сообщения>)
        //
        // при all = 2 дополнительно возвращаются элементы <страна>, <оператор> и <регион>
        //
        // при множественном запросе (данные по статусам сохраняются в двумерном массиве D2Res):
        // если all = 0, то для каждого сообщения или HLR-запроса дополнительно возвращается <ID сообщения> и <номер телефона>
        //
        // если all = 1 или all = 2, то в ответ добавляется <ID сообщения>
        //
        // либо массив (0, -<код ошибки>) в случае ошибки

        public string[] get_status(string id, string phone, int all = 0)
        {
            string[] m = _smsc_send_cmd("status", "phone=" + _urlencode(phone) + "&id=" + _urlencode(id) + "&all=" + all.ToString());

            // (status, time, err, ...) или (0, -error)

            if (id.IndexOf(',') == -1)
            {
                if (SmscDebug)
                {
                    if (m[1] != "" && Convert.ToInt32(m[1]) >= 0)
                    {
                        int timestamp = Convert.ToInt32(m[1]);
                        DateTime offset = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                        DateTime date = offset.AddSeconds(timestamp);

                        _print_debug("Статус SMS = " + m[0] + (timestamp > 0 ? ", время изменения статуса - " + date.ToLocalTime() : ""));
                    }
                    else
                        _print_debug("Ошибка №" + m[1].Substring(1, 1));
                }

                int idx = all == 1 ? 9 : 12;

                if (all > 0 && m.Length > idx && (m.Length < idx + 5 || m[idx + 5] != "HLR"))
                    m = String.Join(",", m).Split(",".ToCharArray(), idx);
            }
            else
            {
                if (m.Length == 1 && m[0].IndexOf('-') == 2)
                    return m[0].Split(',');

                Array.Resize(ref D2Res, 0);
                Array.Resize(ref D2Res, m.Length);

                for (int i = 0; i < D2Res.Length; i++)
                    D2Res[i] = m[i].Split(',');

                Array.Resize(ref m, 1);
                m[0] = "1";
            }

            return m;
        }

        // Метод получения баланса
        //
        // без параметров
        //
        // возвращает баланс в виде строки или пустую строку в случае ошибки

        public string get_balance()
        {
            string[] m = _smsc_send_cmd("balance", ""); // (balance) или (0, -error)

            if (SmscDebug) {
                if (m.Length == 1)
                    _print_debug("Сумма на счете: " + m[0]);
                else
                    _print_debug("Ошибка №" + m[1].Substring(1, 1));
            }

            return m.Length == 1 ? m[0] : "";
        }

        // ПРИВАТНЫЕ МЕТОДЫ

        // Метод вызова запроса. Формирует URL и делает 3 попытки чтения

        private string[] _smsc_send_cmd(string cmd, string arg, string[] files = null)
        {
            arg = "login=" + _urlencode(SmscLogin) + "&psw=" + _urlencode(SmscPassword) + "&fmt=1&charset=" + SmscCharset + "&" + arg;

            string url = (SmscHttps ? "https" : "http") + "://smsc.ru/sys/" + cmd + ".php" + (SmscPost ? "" : "?" + arg);

            string ret;
            int i = 0;
            HttpWebRequest request;
            StreamReader sr;
            HttpWebResponse response;

            do
            {
                if (i > 0)
                    System.Threading.Thread.Sleep(2000 + 1000 * i);

                if (i == 2)
                    url = url.Replace("://smsc.ru/", "://www2.smsc.ru/");

                request = (HttpWebRequest)WebRequest.Create(url);

                if (SmscPost) {
                    request.Method = "POST";

                    string postHeader, boundary = "----------" + DateTime.Now.Ticks.ToString("x");
                    byte[] postHeaderBytes, boundaryBytes = Encoding.ASCII.GetBytes("--" + boundary + "--\r\n"), tbuf;
                    StringBuilder sb = new StringBuilder();
                    int bytesRead;

                    byte[] output = new byte[0];

                    if (files == null) {
                        request.ContentType = "application/x-www-form-urlencoded";
                        output = Encoding.UTF8.GetBytes(arg);
                        request.ContentLength = output.Length;
                    }
                    else {
                        request.ContentType = "multipart/form-data; boundary=" + boundary;

                        string[] par = arg.Split('&');
                        int fl = files.Length;

                        for (int pcnt = 0; pcnt < par.Length + fl; pcnt++)
                        {
                            sb.Clear();

                            sb.Append("--");
                            sb.Append(boundary);
                            sb.Append("\r\n");
                            sb.Append("Content-Disposition: form-data; name=\"");

                            bool pof = pcnt < fl;
                            String[] nv = new String[0];

                            if (pof)
                            {
                                sb.Append("File" + (pcnt + 1));
                                sb.Append("\"; filename=\"");
                                sb.Append(Path.GetFileName(files[pcnt]));
                            }
                            else {
                                nv = par[pcnt - fl].Split('=');
                                sb.Append(nv[0]);
                            }

                            sb.Append("\"");
                            sb.Append("\r\n");
                            sb.Append("Content-Type: ");
                            sb.Append(pof ? "application/octet-stream" : "text/plain; charset=\"" + SmscCharset + "\"");
                            sb.Append("\r\n");
                            sb.Append("Content-Transfer-Encoding: binary");
                            sb.Append("\r\n");
                            sb.Append("\r\n");

                            postHeader = sb.ToString();
                            postHeaderBytes = Encoding.UTF8.GetBytes(postHeader);

                            output = _concatb(output, postHeaderBytes);

                            if (pof)
                            {
                                FileStream fileStream = new FileStream(files[pcnt], FileMode.Open, FileAccess.Read);

                                // Write out the file contents
                                byte[] buffer = new Byte[checked((uint)Math.Min(4096, (int)fileStream.Length))];

                                bytesRead = 0;
                                while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) != 0)
                                {
                                    tbuf = buffer;
                                    Array.Resize(ref tbuf, bytesRead);

                                    output = _concatb(output, tbuf);
                                }
                            }
                            else {
                                byte[] vl = Encoding.UTF8.GetBytes(nv[1]);
                                output = _concatb(output, vl);
                            }

                            output = _concatb(output, Encoding.UTF8.GetBytes("\r\n"));
                        }
                        output = _concatb(output, boundaryBytes);

                        request.ContentLength = output.Length;
                    }

                    Stream requestStream = request.GetRequestStream();
                    requestStream.Write(output, 0, output.Length);
                }

                try
                {
                    response = (HttpWebResponse)request.GetResponse();

                    sr = new StreamReader(response.GetResponseStream());
                    ret = sr.ReadToEnd();
                }
                catch (WebException) {
                    ret = "";
                }
            }
            while (ret == "" && ++i < 4);

            if (ret == "") {
                if (SmscDebug)
                    _print_debug("Ошибка чтения адреса: " + url);

                ret = ","; // фиктивный ответ
            }

            char delim = ',';

            if (cmd == "status")
            {
                string[] par = arg.Split('&');

                for (i = 0; i < par.Length; i++)
                {
                    string[] lr = par[i].Split("=".ToCharArray(), 2);

                    if (lr[0] == "id" && lr[1].IndexOf("%2c") > 0) // запятая в id - множественный запрос
                        delim = '\n';
                }
            }

            return ret.Split(delim);
        }

        // кодирование параметра в http-запросе
        private string _urlencode(string str) {
            if (SmscPost) return str;

            return WebUtility.UrlEncode(str);
        }

        // объединение байтовых массивов
        private byte[] _concatb(byte[] farr, byte[] sarr)
        {
            int opl = farr.Length;

            Array.Resize(ref farr, farr.Length + sarr.Length);
            Array.Copy(sarr, 0, farr, opl, sarr.Length);

            return farr;
        }

        // вывод отладочной информации
        private void _print_debug(string str) {
            System.Windows.Forms.MessageBox.Show(str);
        }
    }
}

// Examples:
// SMSC smsc = new SMSC();
// string[] r = smsc.send_sms("79999999999", "Ваш пароль: 123", 2);
// string[] r = smsc.send_sms("79999999999", "http://smsc.ru\nSMSC.RU", 0, "", 0, 0, "", "maxsms=3");
// string[] r = smsc.send_sms("79999999999", "0605040B8423F0DC0601AE02056A0045C60C036D79736974652E72750001036D7973697465000101", 0, "", 0, 5);
// string[] r = smsc.send_sms("79999999999", "", 0, "", 0, 3);
// string[] r = smsc.send_sms("dest@mysite.com", "Ваш пароль: 123", 0, 0, 0, 8, "source@mysite.com", "subj=Confirmation");
// string[] r = smsc.get_sms_cost("79999999999", "Вы успешно зарегистрированы!");
// smsc.send_sms_mail("79999999999", "Ваш пароль: 123", 0, "0101121000");
// string[] r = smsc.get_status(12345, "79999999999");
// string balance = smsc.get_balance();
