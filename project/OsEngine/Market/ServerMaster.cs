/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Forms.Integration;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.AstsBridge;
using OsEngine.Market.Servers.Binance;
using OsEngine.Market.Servers.Bitfinex;
using OsEngine.Market.Servers.BitMax;
using OsEngine.Market.Servers.BitMex;
using OsEngine.Market.Servers.BitStamp;
using OsEngine.Market.Servers.ExMo;
using OsEngine.Market.Servers.Finam;
using OsEngine.Market.Servers.GateIo;
using OsEngine.Market.Servers.InteractivBrokers;
using OsEngine.Market.Servers.Kraken;
using OsEngine.Market.Servers.Livecoin;
using OsEngine.Market.Servers.Lmax;
using OsEngine.Market.Servers.NinjaTrader;
using OsEngine.Market.Servers.Oanda;
using OsEngine.Market.Servers.Optimizer;
using OsEngine.Market.Servers.Plaza;
using OsEngine.Market.Servers.Quik;
using OsEngine.Market.Servers.QuikLua;
using OsEngine.Market.Servers.SmartCom;
using OsEngine.Market.Servers.Tester;
using OsEngine.Market.Servers.Transaq;
using OsEngine.Market.Servers.ZB;
using MessageBox = System.Windows.MessageBox;

namespace OsEngine.Market
{
    /// <summary>
    /// class exchange server manager
    /// класс менеджер серверов подключения к бирже
    /// </summary>
    public class ServerMaster
    {

// service
// сервис

        /// <summary>
        /// array of deployed servers
        /// массив развёрнутых серверов
        /// </summary>
        private static List<IServer> _servers;

        /// <summary>
        /// take trade server typre from system
        /// взять типы торговых серверов в системе
        /// </summary>
        public static List<ServerType> ServersTypes
        {
            get
            {
                List<ServerType> serverTypes = new List<ServerType>();

                serverTypes.Add(ServerType.GateIo);

                serverTypes.Add(ServerType.QuikDde);
                serverTypes.Add(ServerType.QuikLua);
                serverTypes.Add(ServerType.SmartCom);
                serverTypes.Add(ServerType.Plaza);
                serverTypes.Add(ServerType.Transaq);

                serverTypes.Add(ServerType.BitMax);
                serverTypes.Add(ServerType.Binance);
                serverTypes.Add(ServerType.BitMex);
                serverTypes.Add(ServerType.BitStamp);
                serverTypes.Add(ServerType.Bitfinex);
                serverTypes.Add(ServerType.Kraken);
                serverTypes.Add(ServerType.Livecoin);
                serverTypes.Add(ServerType.Exmo);
                serverTypes.Add(ServerType.Zb);

                serverTypes.Add(ServerType.InteractivBrokers);
                serverTypes.Add(ServerType.NinjaTrader);
                serverTypes.Add(ServerType.Lmax);
                serverTypes.Add(ServerType.Oanda);

                serverTypes.Add(ServerType.Finam);
                serverTypes.Add(ServerType.AstsBridge);

                return serverTypes;
            }
        }

        /// <summary>
        /// disable all servers
        /// отключить все сервера
        /// </summary>
        public static void AbortAll()
        {
            try
            {
                for (int i = 0; _servers != null && i < _servers.Count; i++)
                {
                    _servers[i].StopServer();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(),LogMessageType.Error);
            }
        }

        /// <summary>
        /// show settings
        /// показать настройки
        /// </summary>
        public static void ShowDialog(bool isTester)
        {
            if (_ui == null)
            {
                _ui = new ServerMasterUi(isTester);

                try
                {
                    _ui.Show();
                    _ui.Closing += (sender, args) => { _ui = null; };
                }
                catch
                {
                    _ui = null;
                }

            }
            else
            {
                _ui.Activate();
            }
        }

        private static ServerMasterUi _ui;

        /// <summary>
        /// create server
        /// создать сервер
        /// </summary>
        /// <param name="type"> server type / тип сервера </param>
        /// <param name="neadLoadTicks"> shows whether upload ticks from storage. this is need for bots with QUIK or Plaza2 servers / нужно ли подгружать тики из хранилища. Актуально в режиме робота для серверов Квик, Плаза 2 </param>
        public static void CreateServer(ServerType type, bool neadLoadTicks)
        {
            try
            {
                if (_servers == null)
                {
                    _servers = new List<IServer>();
                }

                if (_servers.Find(server => server.ServerType == type) != null)
                {
                    return;
                }

                IServer newServer = null;

                if (type == ServerType.GateIo)
                {
                    newServer = new GateIoServer();
                }
                if (type == ServerType.Zb)
                {
                    newServer = new ZbServer();
                }
                if (type == ServerType.Exmo)
                {
                    newServer = new ExmoServer();
                }
                if (type == ServerType.Livecoin)
                {
                    newServer = new LivecoinServer();
                }
                if (type == ServerType.BitMax)
                {
                    newServer = new BitMaxServer();
                }
                if (type == ServerType.Transaq)
                {
                    newServer = new TransaqServer();
                }
                if (type == ServerType.Lmax)
                {
                    newServer = new LmaxServer();
                }
                if (type == ServerType.Bitfinex)
                {
                    newServer = new BitfinexServer();
                }
                if (type == ServerType.Binance)
                {
                    newServer = new BinanceServer();
                }
                if (type == ServerType.NinjaTrader)
                {
                    newServer = new NinjaTraderServer();
                }
                if (type == ServerType.BitStamp)
                {
                    newServer = new BitStampServer();
                }
                if (type == ServerType.Kraken)
                {
                    newServer = new KrakenServer(neadLoadTicks);
                }
                if (type == ServerType.Oanda)
                {
                    newServer = new OandaServer();
                }
                if (type == ServerType.BitMex)
                {
                    newServer = new BitMexServer();
                }
                if (type == ServerType.QuikLua)
                {
                    newServer = new QuikLuaServer();
                }
                if (type == ServerType.QuikDde)
                {
                    newServer = new QuikServer();
                }
                if (type == ServerType.InteractivBrokers)
                {
                    newServer = new InteractivBrokersServer();
                }
                else if (type == ServerType.SmartCom)
                {
                    newServer = new SmartComServer();
                }
                else if (type == ServerType.Plaza)
                {
                    newServer = new PlazaServer();
                }
                else if (type == ServerType.AstsBridge)
                {
                    newServer = new AstsBridgeServer(neadLoadTicks);
                }
                else if (type == ServerType.Tester)
                {
                    newServer = new TesterServer();
                }
                else if (type == ServerType.Finam)
                {
                    newServer = new FinamServer();
                }

                if (newServer == null)
                {
                    return;
                }

                _servers.Add(newServer);

                if (ServerCreateEvent != null)
                {
                    ServerCreateEvent(newServer);
                }

                SendNewLogMessage(OsLocalization.Market.Message3 + _servers[_servers.Count - 1].ServerType, LogMessageType.System);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// create a new optimization server
        /// создать новый сервер оптимизации
        /// </summary>
        public static OptimizerServer CreateNextOptimizerServer(OptimizerDataStorage storage, int num, decimal portfolioStartVal)
        {
            OptimizerServer serv = new OptimizerServer(storage, num, portfolioStartVal);

            bool isInArray = false;

            if (_servers == null)
            {
                _servers = new List<IServer>();
            }

            for (int i = 0; i < _servers.Count; i++)
            {
                if (_servers[i].ServerType == ServerType.Optimizer)
                {
                    _servers[i] = serv;
                    isInArray = true;
                }
            }

            if (isInArray == false)
            {
                _servers.Add(serv);
            }
            
            if (ServerCreateEvent != null)
            {
                ServerCreateEvent(serv);
            }
            return serv;
        }

        /// <summary>
        /// take the server
        /// взять сервер
        /// </summary>
        public static List<IServer> GetServers()
        {
            return _servers;
        }

        /// <summary>
        /// new server created
        /// создан новый сервер
        /// </summary>
        public static event Action<IServer> ServerCreateEvent;

// creating servers automatically 
// создание серверов автоматически

        /// <summary>
        /// upload server settings
        /// загрузить настройки сервера
        /// </summary>
        public static void ActivateAutoConnection()
        {
            Load();
            Thread starterThread = new Thread(ThreadStarterWorkArea);
            starterThread.IsBackground = true;
            starterThread.Name = "SeverMasterAutoStartThread";
            starterThread.Start();
        }

        private static ServerMasterPortfoliosPainter _painter;

        /// <summary>
        /// save settings
        /// сохранить настройки
        /// </summary>
        public static void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + @"ServerMaster.txt", false))
                {
                    writer.WriteLine(NeadToConnectAuto);
                    writer.Close();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// upload settings
        /// загрузить настройки
        /// </summary>
        public static void Load()
        {
            if (!File.Exists(@"Engine\" + @"ServerMaster.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + @"ServerMaster.txt"))
                {
                    NeadToConnectAuto = Convert.ToBoolean(reader.ReadLine());
                    reader.Close();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// shows whether the server-master can be deployed in automatic mode  
        /// можно ли сервер мастеру разворачивать сервера в автоматическом режиме
        /// </summary>
        public static bool NeadToConnectAuto;

        /// <summary>
        /// select a specific server type for connection
        /// заказать на подключение определённый тип сервера
        /// </summary>
        public static void SetNeedServer(ServerType type)
        {
            if (_needServerTypes == null)
            {
                _needServerTypes = new List<ServerType>();
            }

            for (int i = 0; i < _needServerTypes.Count; i++)
            {
                if (_needServerTypes[i] == type)
                {
                    return;
                }
            }

            _needServerTypes.Add(type);
        }

        /// <summary>
        /// selected bot servers
        /// сервера, которые заказали роботы
        /// </summary>
        private static List<ServerType> _needServerTypes;

        /// <summary>
        /// servers that we have already treid to connect
        /// серверы которые мы уже пытались подключить
        /// </summary>
        private static List<ServerType> _tryActivateServerTypes;

        /// <summary>
        /// work place of the thread that connects our servers in auto mode
        /// место работы потока который подключает наши сервера в авто режиме
        /// </summary>
        private static void ThreadStarterWorkArea()
        {
            Thread.Sleep(20000);
            while (true)
            {
                Thread.Sleep(5000);

                if (!MainWindow.ProccesIsWorked)
                {
                    return;
                }

                if (NeadToConnectAuto == false)
                {
                    continue;
                }

                if (_tryActivateServerTypes == null)
                {
                    _tryActivateServerTypes = new List<ServerType>();
                }

                for (int i = 0; _needServerTypes != null && i < _needServerTypes.Count; i++)
                {
                    if (_needServerTypes[i] == ServerType.Tester ||
                        _needServerTypes[i] == ServerType.Optimizer ||
                        _needServerTypes[i] == ServerType.Miner)
                    {
                        continue;
                    }
                    TryStartThisSevrverInAutoType(_needServerTypes[i]);
                }
            }
        }
        
        /// <summary>
        /// try running this server
        /// Попробовать запустить данный сервер
        /// </summary>
        private static void TryStartThisSevrverInAutoType(ServerType type)
        {
            for (int i = 0; i < _tryActivateServerTypes.Count; i++)
            {
                if (_tryActivateServerTypes[i] == type)
                {
                    return;
                }
            }

            _tryActivateServerTypes.Add(type);

            if (GetServers() == null || GetServers().Find(server1 => server1.ServerType == type) == null)
            { // if we don't have our server, create a new one / если у нас нашего сервера нет - создаём его
                CreateServer(type,true);
            }

            List<IServer> servers = GetServers();

            if (servers == null)
            { // something went wrong / что-то пошло не так
                return;
            }

            IServer server = servers.Find(server1 => server1.ServerType == type);

            if (server == null)
            {
                return;
            }

            if (server.ServerStatus != ServerConnectStatus.Connect)
            {
                server.StartServer();
            }
        }

// access to the portfolio and its drawing
// доступ к портфелю и его прорисовка

        /// <summary>
        /// start to draw class controls
        /// начать прорисовывать контролы класса 
        /// </summary>
        public static void StartPaint()
        {
             _painter.StartPaint();
        }

        /// <summary>
        /// stop to draw class controls
        /// остановить прорисовку контролов класса 
        /// </summary>
        public static void StopPaint()
        {
            _painter.StopPaint();
        }

        /// <summary>
        /// clear the order list in the table
        /// очистить список ордеров в таблицах
        /// </summary>
        public static void ClearOrders()
        {
            if (_painter == null)
            {
                return;
            }
            _painter.ClearOrders();
        }

        /// <summary>
        /// add items on which portfolios and orders will be drawn
        /// добавить элементы, на котором будут прорисовываться портфели и ордера
        /// </summary>
        public static void SetHostTable(WindowsFormsHost hostPortfolio, WindowsFormsHost hostOrders)
        {
            _painter = new ServerMasterPortfoliosPainter();
            _painter.LogMessageEvent += SendNewLogMessage;
            _painter.SetHostTable(hostPortfolio, hostOrders);
        }

// log messages
// сообщения в лог

        public static void ActivateLogging()
        {
            if (Log == null)
            {
                Log = new Log("ServerMaster",StartProgram.IsOsTrader);
                Log.ListenServerMaster();
            }
        }

        public static Log Log;

        /// <summary>
        /// send new message to up
        /// выслать новое сообщение на верх
        /// </summary>
        private static void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            { // if nobody is subscribled to us and there is a log error / если на нас никто не подписан и в логе ошибка
                MessageBox.Show(message);
            }
        }

        /// <summary>
        /// outgoing log message
        /// исходящее сообщение для лога
        /// </summary>
        public static event Action<string, LogMessageType> LogMessageEvent;

    }

    /// <summary>
    /// what program start the class
    /// какая программа запустила класс
    /// </summary>
    public enum StartProgram
    {
        /// <summary>
        /// tester
        /// тестер
        /// </summary>
        IsTester,

        /// <summary>
        /// optimizator
        /// оптимизатор
        /// </summary>
        IsOsOptimizer,

        /// <summary>
        /// data downloading
        /// качалка данных
        /// </summary>
        IsOsData,

        /// <summary>
        /// terminal
        /// терминал
        /// </summary>
        IsOsTrader,

        /// <summary>
        /// ticks to candles converter
        /// конвертер тиков в свечи
        /// </summary>
        IsOsConverter,

        /// <summary>
        /// pattern miner
        /// майнер паттернов
        /// </summary>
        IsOsMiner
    }

    /// <summary>
    /// type of connection to trading. Server type
    /// тип подключения к торгам. Тип сервера
    /// </summary>
    public enum ServerType
    {
        /// <summary>
        /// server type not defined
        /// Тип сервера не назначен
        /// </summary>
        None,

        /// <summary>
        /// cryptocurrency exchange Gate.io
        /// биржа криптовалют Gate.io
        /// </summary>
        GateIo,

        /// <summary>
        /// cryptocurrency exchange ZB
        /// биржа криптовалют ZB
        /// </summary>
        Zb,

        /// <summary>
        /// Livecoin exchange
        /// биржа Livecoin
        /// </summary>
        Livecoin,

        /// <summary>
        /// BitMax exchange
        /// биржа BitMax
        /// </summary>
        BitMax,

        /// <summary>
        /// transaq
        /// транзак
        /// </summary>
        Transaq,

        /// <summary>
        /// LMax exchange
        /// биржа LMax
        /// </summary>
        Lmax,

        /// <summary>
        /// cryptocurrency exchange Bitfinex
        /// биржа криптовалют Bitfinex
        /// </summary>
        Bitfinex,

        /// <summary>
        /// cryptocurrency exchange Binance
        /// биржа криптовалют Binance
        /// </summary>
        Binance,

        /// <summary>
        /// cryptocurrency exchange Exmo
        /// биржа криптовалют Exmo
        /// </summary>
        Exmo,

        /// <summary>
        /// terminal Ninja Trader
        /// нинзя трейдер
        /// </summary>
        NinjaTrader,

        /// <summary>
        /// cryptocurrency exchange Kraken
        /// биржа криптовалют Kraken
        /// </summary>
        Kraken,

        /// <summary>
        /// forex broker Oanda
        /// форекс брокер Oanda
        /// </summary>
        Oanda,

        /// <summary>
        /// cryptocurrency exchange BitMEX
        /// биржа криптовалют BitMEX
        /// </summary>
        BitMex,

        /// <summary>
        /// cryptocurrency exchange BitStamp
        /// биржа криптовалют BitStamp
        /// </summary>
        BitStamp,

        /// <summary>
        /// optimizer
        /// Оптимизатор
        /// </summary>
        Optimizer,

        /// <summary>
        /// miner
        /// Майнер
        /// </summary>
        Miner,

        /// <summary>
        /// connection to terminal Quik by LUA
        /// Квик луа
        /// </summary>
        QuikLua,

        /// <summary>
        /// connection to terminal Quik by DDE
        /// Квик
        /// </summary>
        QuikDde,

        /// <summary>
        /// SmartCom
        /// Смарт-Ком
        /// </summary>
        SmartCom,

        /// <summary>
        /// Plaza 2
        /// Плаза 2
        /// </summary>
        Plaza,

        /// <summary>
        /// Tester
        /// Тестер
        /// </summary>
        Tester,

        /// <summary>
        /// IB
        /// </summary>
        InteractivBrokers,

        /// <summary>
        /// Finam
        /// Финам
        /// </summary>
        Finam,

        /// <summary>
        /// AstsBridge, he is also the gateway or TEAP
        /// AstsBridge, он же ШЛЮЗ, он же TEAP 
        /// </summary>
        AstsBridge
    }
}
