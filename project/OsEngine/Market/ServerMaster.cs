/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms.Integration;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.AstsBridge;
using OsEngine.Market.Servers.Binance.Futures;
using OsEngine.Market.Servers.Binance.Spot;
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
using OsEngine.Market.Servers.Hitbtc;
using OsEngine.Market.Servers.Huobi.Futures;
using OsEngine.Market.Servers.Huobi.Spot;
using OsEngine.Market.Servers.Huobi.FuturesSwap;
using OsEngine.Market.Servers.MFD;
using OsEngine.Market.Servers.MOEX;
using OsEngine.Market.Servers.Tinkoff;
using MessageBox = System.Windows.MessageBox;
using OsEngine.Market.Servers.GateIo.Futures;
using OsEngine.Market.Servers.FTX;
using OsEngine.Market.Servers.Bybit;

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

                serverTypes.Add(ServerType.QuikDde);
                serverTypes.Add(ServerType.QuikLua);
                serverTypes.Add(ServerType.SmartCom);
                serverTypes.Add(ServerType.Plaza);
                serverTypes.Add(ServerType.Transaq);
                serverTypes.Add(ServerType.Tinkoff);
                serverTypes.Add(ServerType.Finam);
                serverTypes.Add(ServerType.MoexDataServer);
                serverTypes.Add(ServerType.MfdWeb);

                serverTypes.Add(ServerType.GateIo);
                serverTypes.Add(ServerType.GateIoFutures);
                serverTypes.Add(ServerType.BitMax);
                serverTypes.Add(ServerType.Binance);
                serverTypes.Add(ServerType.BinanceFutures);
                serverTypes.Add(ServerType.BitMex);
                serverTypes.Add(ServerType.BitStamp);
                serverTypes.Add(ServerType.Bitfinex);
                serverTypes.Add(ServerType.Kraken);
                serverTypes.Add(ServerType.Livecoin);
                serverTypes.Add(ServerType.Exmo);
                serverTypes.Add(ServerType.Zb);
                serverTypes.Add(ServerType.Hitbtc);
                serverTypes.Add(ServerType.HuobiSpot);
                serverTypes.Add(ServerType.HuobiFutures);
                serverTypes.Add(ServerType.HuobiFuturesSwap);
                serverTypes.Add(ServerType.FTX);
                serverTypes.Add(ServerType.Bybit);

                serverTypes.Add(ServerType.InteractivBrokers);
                serverTypes.Add(ServerType.NinjaTrader);
                serverTypes.Add(ServerType.Lmax);
                serverTypes.Add(ServerType.Oanda);

                serverTypes.Add(ServerType.AstsBridge);

                return serverTypes;
            }
        }

        public static List<ServerType> ActiveServersTypes
        {
            get
            {
                List<ServerType> types = new List<ServerType>();

                for (int i = 0; _servers != null && i < _servers.Count; i++)
                {
                    types.Add(_servers[i].ServerType);
                }

                return types;
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
                if (type == ServerType.FTX)
                {
                    newServer = new FTXServer();
                }
                if (type == ServerType.HuobiFuturesSwap)
                {
                    newServer = new HuobiFuturesSwapServer();
                }
                if (type == ServerType.HuobiFutures)
                {
                    newServer = new HuobiFuturesServer();
                }
                if (type == ServerType.HuobiSpot)
                {
                    newServer = new HuobiSpotServer();
                }
                if (type == ServerType.MfdWeb)
                {
                    newServer = new MfdServer();
                }
                if (type == ServerType.MoexDataServer)
                {
                    newServer = new MoexDataServer();
                }
                if (type == ServerType.Tinkoff)
                {
                    newServer = new TinkoffServer();
                }
                if (type == ServerType.Hitbtc)
                {
                    newServer = new HitbtcServer();
                }
                if (type == ServerType.GateIo)
                {
                    newServer = new GateIoServer();
                }
                if (type == ServerType.GateIoFutures)
                {
                    newServer = new GateIoFuturesServer();
                }
                if (type == ServerType.Bybit)
                {
                    newServer = new BybitServer();
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
                    newServer = new BitMaxProServer();
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
                if (type == ServerType.BinanceFutures)
                {
                    newServer = new BinanceServerFutures();
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
                    newServer = new InteractiveBrokersServer();
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

        private static object _optimizerGeneratorLocker = new object();

        /// <summary>
        /// create a new optimization server
        /// создать новый сервер оптимизации
        /// </summary>
        public static OptimizerServer CreateNextOptimizerServer(OptimizerDataStorage storage, int num, decimal portfolioStartVal)
        {
            lock (_optimizerGeneratorLocker)
            {
                OptimizerServer serv = new OptimizerServer(storage, num, portfolioStartVal);

                if (serv == null)
                {
                    return null;
                }

                bool isInArray = false;

                if (_servers == null)
                {
                    _servers = new List<IServer>();
                }

                for (int i = 0; i < _servers.Count; i++)
                {
                    IServer ser = _servers[i];

                    if (ser == null)
                    {
                        continue;
                    }

                    if (ser.ServerType == ServerType.Optimizer &&
                        ((OptimizerServer)ser).NumberServer == serv.NumberServer)
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
        }

        public static void RemoveOptimizerServer(OptimizerServer server)
        {
            for (int i = 0; _servers != null && i < _servers.Count; i++)
            {
                if (_servers[i] == null)
                {
                    _servers.RemoveAt(i);
                    i--;
                }
                if (_servers[i].ServerType == ServerType.Optimizer &&
                    ((OptimizerServer)_servers[i]).NumberServer == server.NumberServer)
                {
                    _servers.RemoveAt(i);
                    break;
                }
            }
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

        // доступ к разрешениям для серверов

        private static List<IServerPermission> _serversPermissions = new List<IServerPermission>();

        public static IServerPermission GetServerPermission(ServerType type)
        {
            IServerPermission serverPermission = null;


            if (type == ServerType.Bitfinex)
            {
                serverPermission = _serversPermissions.Find(s => s.ServerType == type);

                if (serverPermission == null)
                {
                    serverPermission = new BitFinexServerPermission();
                    _serversPermissions.Add(serverPermission);
                }

                return serverPermission;
            }

            if (type == ServerType.MoexDataServer)
            {
                serverPermission = _serversPermissions.Find(s => s.ServerType == type);

                if (serverPermission == null)
                {
                    serverPermission = new MoexIssPermission();
                    _serversPermissions.Add(serverPermission);
                }

                return serverPermission;
            }
            if (type == ServerType.MfdWeb)
            {
                serverPermission = _serversPermissions.Find(s => s.ServerType == type);

                if (serverPermission == null)
                {
                    serverPermission = new MfdServerPermission();
                    _serversPermissions.Add(serverPermission);
                }

                return serverPermission;
            }
            if (type == ServerType.Finam)
            {
                serverPermission = _serversPermissions.Find(s => s.ServerType == type);

                if (serverPermission == null)
                {
                    serverPermission = new FinamServerPermission();
                    _serversPermissions.Add(serverPermission);
                }

                return serverPermission;
            }
            if (type == ServerType.Tinkoff)
            {
                serverPermission = _serversPermissions.Find(s => s.ServerType == type);

                if (serverPermission == null)
                {
                    serverPermission = new TinkoffServerPermission();
                    _serversPermissions.Add(serverPermission);
                }

                return serverPermission;
            }
            if (type == ServerType.HuobiSpot)
            {
                serverPermission = _serversPermissions.Find(s => s.ServerType == type);

                if (serverPermission == null)
                {
                    serverPermission = new HuobiSpotServerPermission();
                    _serversPermissions.Add(serverPermission);
                }

                return serverPermission;
            }
            if (type == ServerType.HuobiFutures)
            {
                serverPermission = _serversPermissions.Find(s => s.ServerType == type);

                if (serverPermission == null)
                {
                    serverPermission = new HuobiFuturesServerPermission();
                    _serversPermissions.Add(serverPermission);
                }

                return serverPermission;
            }
            if (type == ServerType.HuobiFuturesSwap)
            {
                serverPermission = _serversPermissions.Find(s => s.ServerType == type);

                if (serverPermission == null)
                {
                    serverPermission = new HuobiFuturesSwapServerPermission();
                    _serversPermissions.Add(serverPermission);
                }

                return serverPermission;
            }
            if (type == ServerType.GateIoFutures)
            {
                serverPermission = _serversPermissions.Find(s => s.ServerType == type);

                if (serverPermission == null)
                {
                    serverPermission = new GateIoFuturesServerPermission();
                    _serversPermissions.Add(serverPermission);
                }

                return serverPermission;
            }
            if (type == ServerType.Bybit)
            {
                serverPermission = _serversPermissions.Find(s => s.ServerType == type);

                if (serverPermission == null)
                {
                    serverPermission = new BybitServerPermission();
                    _serversPermissions.Add(serverPermission);
                }

                return serverPermission;
            }
            if (type == ServerType.InteractivBrokers)
            {
                serverPermission = _serversPermissions.Find(s => s.ServerType == type);

                if (serverPermission == null)
                {
                    serverPermission = new InteractiveBrokersServerPermission();
                    _serversPermissions.Add(serverPermission);
                }

                return serverPermission;
            }

            

            return null;
        }

        
        // создание серверов автоматически creating servers automatically 

        /// <summary>
        /// upload server settings
        /// загрузить настройки сервера
        /// </summary>
        public static void ActivateAutoConnection()
        {
            Load();

            Task task = new Task(ThreadStarterWorkArea);
            task.Start();
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

            try
            {
                for (int i = 0; i < _needServerTypes.Count; i++)
                {
                    if (_needServerTypes[i] == type)
                    {
                        return;
                    }
                }
            }
            catch
            {
                // ignore
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
        private static async void ThreadStarterWorkArea()
        {
            await Task.Delay(20000);

            while (true)
            {
                await Task.Delay(5000);

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
        /// connection to Russian broker Tinkoff Invest
        /// подключение к Тинькофф Инвест (выдающих кредиты под 70% годовых)
        /// </summary>
        Tinkoff,

        /// <summary>
        /// cryptocurrency exchange Hitbtc
        /// биржа криптовалют Hitbtc
        /// </summary>
        Hitbtc,

        /// <summary>
        /// cryptocurrency exchange FTX
        /// биржа криптовалют FTX
        /// </summary>
        FTX,

        /// <summary>
        /// cryptocurrency exchange Gate.io
        /// биржа криптовалют Gate.io
        /// </summary>
        GateIo,

        /// <summary>
        /// Futures of cryptocurrency exchange Gate.io
        /// Фьючерсы биржи криптовалют Gate.io
        /// </summary>
        GateIoFutures,

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
        /// cryptocurrency exchange Binance Futures
        /// биржа криптовалют Binance, секция фьючеры
        /// </summary>
        BinanceFutures,

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
        AstsBridge,

        /// <summary>
        /// Дата сервер московской биржи
        /// </summary>
        MoexDataServer,

        /// <summary>
        /// MFD web server
        /// </summary>
        MfdWeb,

        /// <summary>
        /// Huobi Spot
        /// </summary>
        HuobiSpot,

        /// <summary>
        /// Huobi Futures
        /// </summary>
        HuobiFutures,

        /// <summary>
        /// Huobi Futures Swap
        /// </summary>
        HuobiFuturesSwap,

        /// <summary>
        /// Bybit exchange
        /// </summary>
        Bybit
    }
}
