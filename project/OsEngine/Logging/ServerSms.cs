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
                if (Convert.ToInt32(m[1]) <= 0)
                    //_print_debug("Сообщение отправлено успешно. ID: " + m[0] + ", всего SMS: " + m[1] + ", стоимость: " + m[2] + ", баланс: " + m[3]);
                    _print_debug("Send SMS Error №" + m[1].Substring(1, 1) + (m[0] != "0" ? ", ID: " + m[0] : ""));
            }

            return m;
        }


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
