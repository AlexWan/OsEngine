using OsEngine.Logging;
using OsEngine.Market.Servers.Transaq.TransaqEntity;
using RestSharp;
using RestSharp.Deserializers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using OsEngine.Market.Servers.Entity;
using Order = OsEngine.Market.Servers.Transaq.TransaqEntity.Order;


namespace OsEngine.Market.Servers.Transaq
{
    public class TransaqClient
    {
        public string Login; // user login for Transaq server / логин пользователя для сервера Transaq
        public string Password; // user password for Transaq server / пароль пользователя для сервера Transaq
        public string NewPassword;
        public string ServerIp; // IP-address of Transaq server / IP адрес сервера Transaq
        public string ServerPort; // port number of Transaq server / номер порта сервера Transaq
        public string LogPath;

        delegate bool CallBackDelegate(IntPtr pData);

        private CallBackDelegate _myCallbackDelegate;

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        public TransaqClient(string login, string password, string serverIp, string serverPort, string logPath)
        {
            Login = login;
            Password = password;
            ServerIp = serverIp;
            ServerPort = serverPort;
            LogPath = logPath;

            _deserializer = new XmlDeserializer();

            _myCallbackDelegate = new CallBackDelegate(CallBackDataHandler);

            SetCallback(_myCallbackDelegate);

            Thread converter = new Thread(Converter);
            converter.CurrentCulture = new CultureInfo("ru-RU");
            converter.IsBackground = true;
            converter.Start();
        }

        private bool _loadSecInfoUpdate = false;

        /// <summary>
        /// connecto to the exchange
        /// установить соединение с биржей 
        /// </summary>
        public void Connect(bool loadSecInfoUpdate)
        {
            _loadSecInfoUpdate = loadSecInfoUpdate;

            ConnectorInitialize();
            Thread.Sleep(1000);

            // formation of the command text / формирование текста команды
            string cmd = "<command id=\"connect\">";
            cmd = cmd + "<login>" + Login + "</login>";
            cmd = cmd + "<password>" + Password + "</password>";
            cmd = cmd + "<host>" + ServerIp + "</host>";
            cmd = cmd + "<port>" + ServerPort + "</port>";
            cmd = cmd + "<push_pos_equity>" + 3 + "</push_pos_equity>";
            cmd = cmd + "<rqdelay>100</rqdelay>";
            cmd = cmd + "</command>";

            // sending the command / отправка команды
            ConnectorSendCommand(cmd);
        }

        /// <summary>
        /// disconnect to exchange
        /// разорвать соединение с биржей 
        /// </summary>
        public void Disconnect()
        {
            // formation of the command text / формирование текста команды
            string cmd = "<command id=\"disconnect\">";
            cmd = cmd + "</command>";

            // sending the command / отправка команды
            ConnectorSendCommand(cmd);
        }

        /// <summary>
        /// is connection working
        /// работает ли соединение
        /// </summary>
        public bool IsConnected;

        /// <summary>
        /// there was a request to clean up the object
        /// произошёл запрос на очистку объекта
        /// </summary>
        private bool _isDisposed;

        /// <summary>
        /// bring the program to the start time. Clear all objects involved in connecting to the server
        /// привести программу к моменту запуска. Очистить все объекты участвующие в подключении к серверу
        /// </summary>
        public void Dispose()
        {
            Disconnect();
            Thread.Sleep(2000);
            var res = ConnectorUnInitialize();

            if (res)
            {
                IsConnected = false;

                if (Disconnected != null)
                {
                    Disconnected();
                }

                _isDisposed = true;
            }
        }

        /// <summary>
        /// Initializes the library: starts the callback queue processing thread
        /// Выполняет инициализацию библиотеки: запускает поток обработки очереди обратных вызовов
        /// </summary>
        public bool ConnectorInitialize()
        {
            IntPtr pResult = Initialize(MarshalUtf8.StringToHGlobalUtf8(LogPath), 1);

            if (!pResult.Equals(IntPtr.Zero))
            {
                MarshalUtf8.PtrToStringUtf8(pResult);

                FreeMemory(pResult);

                return false;
            }
            else
            {
                FreeMemory(pResult);
                return true;
            }
        }

        /// <summary>
        /// Shuts down the internal threads of the library, including completing thread queue callbacks
        /// Выполняет остановку внутренних потоков библиотеки, в том числе завершает поток обработки очереди обратных вызовов
        /// </summary>
        public bool ConnectorUnInitialize()
        {
            IntPtr pResult = UnInitialize();

            if (!pResult.Equals(IntPtr.Zero))
            {
                MarshalUtf8.PtrToStringUtf8(pResult);
                FreeMemory(pResult);
                return false;
            }
            else
            {
                FreeMemory(pResult);
                return true;
            }
        }

        private readonly XmlDeserializer _deserializer;

        /// <summary>
        /// queue of new messages from server
        /// очередь новых сообщений, пришедших с сервера биржи
        /// </summary>
        private readonly ConcurrentQueue<string> _newMessage = new ConcurrentQueue<string>();

        /// <summary>
        /// processor of data from callbacks 
        /// обработчик данных пришедших через каллбек
        /// </summary>
        /// <param name="pData">data from Transaq / данные, поступившие от транзака</param>
        bool CallBackDataHandler(IntPtr pData)
        {
            string data = MarshalUtf8.PtrToStringUtf8(pData);

            _newMessage.Enqueue(data);

            FreeMemory(pData);

            return true;
        }

        /// <summary>
        /// sent the command
        /// отправить команду
        /// </summary>
        /// <param name="command">command as a XML document / команда в виде XML документа</param>
        /// <returns>result of sending command/результат отправки команды</returns>
        public string ConnectorSendCommand(string command)
        {
            IntPtr pData = MarshalUtf8.StringToHGlobalUtf8(command);
            IntPtr pResult = SendCommand(pData);

            string result = MarshalUtf8.PtrToStringUtf8(pResult);

            Marshal.FreeHGlobal(pData);
            FreeMemory(pResult);

            return result;
        }

        private List<string> _securityInfos = new List<string>();

        /// <summary>
        /// takes messages from the shared queue, converts them to C# classes, and sends them to up
        /// берет сообщения из общей очереди, конвертирует их в классы C# и отправляет на верх
        /// </summary>
        public void Converter()
        {
            while (true)
            {
                try
                {
                    if (_isDisposed)
                    {
                        return;
                    }

                    if (!_newMessage.IsEmpty)
                    {
                        string data;

                        if (_newMessage.TryDequeue(out data))
                        {
                            if (data.StartsWith("<pits>"))
                            {
                                continue;
                            }

                            if (data.StartsWith("<sec_info_upd>"))
                            {
                                if (!_loadSecInfoUpdate)
                                {
                                    continue;
                                }
                                _securityInfos.Add(data);
                                continue;
                            }
                            else if (data.StartsWith("<securities>"))
                            {
                                UpdatePairs?.Invoke(data);
                            }
                            else if (data.StartsWith("<quotes>"))
                            {
                                var quotes = _deserializer.Deserialize<List<Quote>>(new RestResponse() { Content = data });

                                UpdateMarketDepth?.Invoke(quotes);
                            }
                            else if (data.StartsWith("<trades>"))
                            {
                                var myTrades = _deserializer.Deserialize<List<Trade>>(new RestResponse() { Content = data });

                                MyTradeEvent?.Invoke(myTrades);
                            }
                            else if (data.StartsWith("<alltrades>"))
                            {
                                var allTrades = _deserializer.Deserialize<List<Trade>>(new RestResponse() { Content = data });

                                NewTradesEvent?.Invoke(allTrades);
                            }
                            else if (data.StartsWith("<mc_portfolio"))
                            {
                                UpdatePortfolio?.Invoke(data);
                            }
                            else if (data.StartsWith("<positions"))
                            {
                                var positions = Deserialize<TransaqPositions>(data);

                                UpdatePositions?.Invoke(positions);
                            }
                            else if (data.StartsWith("<clientlimits"))
                            {
                                var limits = Deserialize<ClientLimits>(data);

                                UpdateClientLimits?.Invoke(limits);
                            }
                            else if (data.StartsWith("<client"))
                            {
                                var clientInfo = _deserializer.Deserialize<Client>(new RestResponse() { Content = data });

                                ClientsInfo?.Invoke(clientInfo);
                            }
                            else if (data.StartsWith("<orders>"))
                            {
                                var orders = _deserializer.Deserialize<List<Order>>(new RestResponse() { Content = data });

                                MyOrderEvent?.Invoke(orders);
                            }
                            else if (data.StartsWith("<candles"))
                            {
                                Candles newCandles = Deserialize<Candles>(data);

                                NewCandles?.Invoke(newCandles);
                            }
                            else if (data.StartsWith("<messages>"))
                            {
                                if (data.Contains("Время действия Вашего пароля истекло"))
                                {
                                    NeedChangePassword?.Invoke();
                                }
                            }
                            else if (data.StartsWith("<server_status"))
                            {
                                UpdateSecurity?.Invoke(_securityInfos);

                                ServerStatus status = Deserialize<ServerStatus>(data);

                                if (status.Connected == "true")
                                {
                                    IsConnected = true;
                                    Connected?.Invoke();
                                }
                                else if (status.Connected == "false")
                                {
                                    IsConnected = false;
                                    Disconnected?.Invoke();
                                }
                                else if (status.Connected == "error")
                                {
                                    SendLogMessage(status.Text, LogMessageType.Error);
                                }
                            }
                            else if (data.StartsWith("<error>"))
                            {
                                SendLogMessage(data, LogMessageType.Error);
                            }
                        }
                    }
                }

                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                }
                Thread.Sleep(1);
            }
        }

        /// <summary>
        /// converts a string of data to needed format
        /// преобразует строку с данными в нужный формат
        /// </summary>
        /// <typeparam name="T">type for converting / тип, в который нужно преобразовать данные</typeparam>
        /// <param name="data">data string / строка с данными</param>
        /// <returns>nessesary object / объект нужного типа</returns>
        public T Deserialize<T>(string data)
        {
            T newData;
            var formatter = new XmlSerializer(typeof(T));
            using (StringReader fs = new StringReader(data))
            {
                newData = (T)formatter.Deserialize(fs);
            }
            return newData;
        }

        public List<Security> DeserializeSecurities(string data)
        {
            return _deserializer.Deserialize<List<Security>>(new RestResponse() { Content = data }); ;
        }

        #region outgoing events / Исходящие события

        /// <summary>
        /// customer data came in
        /// пришли данные о клиентах
        /// </summary>
        public event Action<Client> ClientsInfo;

        /// <summary>
        /// API connection established
        /// соединение с API установлено
        /// </summary>
        public event Action Connected;

        /// <summary>
        /// API connection lost
        /// соединение с API разорвано
        /// </summary>
        public event Action Disconnected;

        /// <summary>
        /// new security in the system
        /// новые бумаги в системе
        /// </summary>
        public event Action<string> UpdatePairs;

        /// <summary>
        /// updated portfolios
        /// обновились портфели
        /// </summary>
        public event Action<string> UpdatePortfolio;

        /// <summary>
        /// updated portfolios
        /// обновились портфели
        /// </summary>
        public event Action<ClientLimits> UpdateClientLimits;

        /// <summary>
        /// updated positions
        /// обновились позиции
        /// </summary>
        public event Action<TransaqPositions> UpdatePositions;

        /// <summary>
        /// updated ticks
        /// обновились тики
        /// </summary>
        public event Action<List<Trade>> NewTradesEvent;

        /// <summary>
        /// updated depth
        /// обновился стакан
        /// </summary>
        public event Action<List<Quote>> UpdateMarketDepth;

        /// <summary>
        /// my new orders
        /// новые мои ордера
        /// </summary>
        public event Action<List<Order>> MyOrderEvent;

        /// <summary>
        /// my new trades
        /// новые мои сделки
        /// </summary>
        public event Action<List<Trade>> MyTradeEvent;

        /// <summary>
        /// got candles
        /// пришли свечи
        /// </summary>
        public event Action<Candles> NewCandles;

        /// <summary>
        /// need to change password
        /// нужно изменить пароль
        /// </summary>
        public event Action NeedChangePassword;

        /// <summary>
        /// updated security
        /// обновились данные по инструменту
        /// </summary>
        public event Action<List<string>> UpdateSecurity;


        #endregion

        #region log message / сообщения для лога

        /// <summary>
        /// add a new log message
        /// добавить в лог новое сообщение
        /// </summary>
        void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        /// <summary>
        /// send exeptions
        /// отправляет исключения
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion


        //--------------------------------------------------------------------------------
        // file of library TXmlConnector.the dll must be in the same folder as the program
        // файл библиотеки TXmlConnector.dll должен находиться в одной папке с программой

        [DllImport("txmlconnector64.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern bool SetCallback(CallBackDelegate pCallback);

        [DllImport("txmlconnector64.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr SendCommand(IntPtr pData);

        [DllImport("txmlconnector64.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern bool FreeMemory(IntPtr pData);

        [DllImport("TXmlConnector64.dll", CallingConvention = CallingConvention.Winapi)]
        private static extern IntPtr Initialize(IntPtr pPath, Int32 logLevel);

        [DllImport("TXmlConnector64.dll", CallingConvention = CallingConvention.Winapi)]
        private static extern IntPtr UnInitialize();

        [DllImport("TXmlConnector64.dll", CallingConvention = CallingConvention.Winapi)]
        private static extern IntPtr SetLogLevel(Int32 logLevel);
        //--------------------------------------------------------------------------------
    }
}
