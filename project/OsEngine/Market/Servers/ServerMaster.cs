/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Forms.Integration;
using OsEngine.Logging;
using OsEngine.Market.Servers.AstsBridge;
using OsEngine.Market.Servers.Binance;
using OsEngine.Market.Servers.Bitfinex;
using OsEngine.Market.Servers.BitMex;
using OsEngine.Market.Servers.BitStamp;
using OsEngine.Market.Servers.Finam;
using OsEngine.Market.Servers.InteractivBrokers;
using OsEngine.Market.Servers.Kraken;
using OsEngine.Market.Servers.NinjaTrader;
using OsEngine.Market.Servers.Oanda;
using OsEngine.Market.Servers.Optimizer;
using OsEngine.Market.Servers.Plaza;
using OsEngine.Market.Servers.Quik;
using OsEngine.Market.Servers.QuikLua;
using OsEngine.Market.Servers.SmartCom;
using OsEngine.Market.Servers.Tester;
using MessageBox = System.Windows.MessageBox;

namespace OsEngine.Market.Servers
{
    /// <summary>
    /// класс менеджер серверов подключения к бирже
    /// </summary>
    public class ServerMaster
    {

// сервис

        /// <summary>
        /// массив развёрнутых серверов
        /// </summary>
        private static List<IServer> _servers;

        /// <summary>
        /// какая программа запустила мастер серверов
        /// </summary>
        public static ServerStartProgramm StartProgram;

        /// <summary>
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
        /// показать настройки
        /// </summary>
        public static void ShowDialog() 
        {
            if (StartProgram == ServerStartProgramm.IsTester)
            {
                ServerMasterUi ui = new ServerMasterUi();
            }
            else
            {
                ServerMasterUi ui = new ServerMasterUi();
                ui.ShowDialog();
            }
        }

        /// <summary>
        /// создать сервер
        /// </summary>
        /// <param name="type"> тип сервера</param>
        /// <param name="neadLoadTicks">нужно ли подгружать тики из хранилища. Актуально в режиме робота для серверов Квик, Плаза 2</param>
        public static void CreateServer(ServerType type, bool neadLoadTicks)
        {
            try
            {
                if (_servers == null)
                {
                    _servers = new List<IServer>();
                }
                if (type == ServerType.Bitfinex)
                {
                    if (_servers.Find(server => server.ServerType == ServerType.Bitfinex) != null)
                    {
                        return;
                    }

                    BitfinexServer serv = new BitfinexServer(neadLoadTicks);
                    _servers.Add(serv);

                    if (ServerCreateEvent != null)
                    {
                        ServerCreateEvent();
                    }
                }
                if (type == ServerType.Binance)
                {
                    if (_servers.Find(server => server.ServerType == ServerType.Binance) != null)
                    {
                        return;
                    }

                    BinanceServer serv = new BinanceServer(neadLoadTicks);
                    _servers.Add(serv);

                    if (ServerCreateEvent != null)
                    {
                        ServerCreateEvent();
                    }
                }
                if (type == ServerType.NinjaTrader)
                {
                    if (_servers.Find(server => server.ServerType == ServerType.NinjaTrader) != null)
                    {
                        return;
                    }

                    NinjaTraderServer serv = new NinjaTraderServer(neadLoadTicks);
                    _servers.Add(serv);

                    if (ServerCreateEvent != null)
                    {
                        ServerCreateEvent();
                    }
                }
                if (type == ServerType.BitStamp)
                {
                    if (_servers.Find(server => server.ServerType == ServerType.BitStamp) != null)
                    {
                        return;
                    }

                    BitStampServer serv = new BitStampServer(neadLoadTicks);
                    _servers.Add(serv);

                    if (ServerCreateEvent != null)
                    {
                        ServerCreateEvent();
                    }
                }
                if (type == ServerType.Kraken)
                {
                    if (_servers.Find(server => server.ServerType == ServerType.Kraken) != null)
                    {
                        return;
                    }

                    KrakenServer serv = new KrakenServer(neadLoadTicks);
                    _servers.Add(serv);

                    if (ServerCreateEvent != null)
                    {
                        ServerCreateEvent();
                    }
                }
                if (type == ServerType.Oanda)
                {
                    if (_servers.Find(server => server.ServerType == ServerType.Oanda) != null)
                    {
                        return;
                    }

                    OandaServer serv = new OandaServer(neadLoadTicks);
                    _servers.Add(serv);

                    if (ServerCreateEvent != null)
                    {
                        ServerCreateEvent();
                    }
                }
                if (type == ServerType.BitMex)
                {
                    if (_servers.Find(server => server.ServerType == ServerType.BitMex) != null)
                    {
                        return;
                    }

                    BitMexServer serv = new BitMexServer(neadLoadTicks);
                    _servers.Add(serv);

                    if (ServerCreateEvent != null)
                    {
                        ServerCreateEvent();
                    }
                }
                if (type == ServerType.QuikLua)
                {
                    if (_servers.Find(server => server.ServerType == ServerType.QuikLua) != null)
                    {
                        return;
                    }
                    QuikLuaServer serv = new QuikLuaServer(neadLoadTicks);
                    _servers.Add(serv);

                    if (ServerCreateEvent != null)
                    {
                        ServerCreateEvent();
                    }
                }

                if (type == ServerType.QuikDde)
                {
                    if (_servers.Find(server => server.ServerType == ServerType.QuikDde) != null)
                    {
                        return;
                    }

                    QuikServer serv = new QuikServer(neadLoadTicks);
                    _servers.Add(serv);

                    if (ServerCreateEvent != null)
                    {
                        ServerCreateEvent();
                    }
                }
                if (type == ServerType.InteractivBrokers)
                {
                    if (_servers.Find(server => server.ServerType == ServerType.InteractivBrokers) != null)
                    {
                        return;
                    }

                    InteractivBrokersServer serv = new InteractivBrokersServer();
                    _servers.Add(serv);

                    if (ServerCreateEvent != null)
                    {
                        ServerCreateEvent();
                    }
                }
                else if (type == ServerType.SmartCom)
                {
                    if (_servers.Find(server => server.ServerType == ServerType.SmartCom) != null)
                    {
                        return;
                    }
                    try
                    {
                        SmartComServer serv = new SmartComServer();
                        _servers.Add(serv);
                    }
                    catch (Exception)
                    {
                        MessageBox.Show(
                            "Произошла ошибка создания сервера СмартКом. Вероятно у Вас не установлена соответствующая программа. SmartCOM_Setup_3.0.146.msi ");
                        return;
                    }

                    if (ServerCreateEvent != null)
                    {
                        ServerCreateEvent();
                    }
                }
                else if (type == ServerType.Plaza)
                {
                    if (_servers.Find(server => server.ServerType == ServerType.Plaza) != null)
                    {
                        return;
                    }
                    try
                    {
                        PlazaServer serv = new PlazaServer(neadLoadTicks);
                        _servers.Add(serv);
                    }
                    catch (Exception)
                    {
                        MessageBox.Show(
                            "Произошла ошибка создания сервера Плаза. Вероятно у Вас не установлено соответствующее программное обеспечение.");
                        return;
                    }

                    if (ServerCreateEvent != null)
                    {
                        ServerCreateEvent();
                    }
                }
                else if (type == ServerType.AstsBridge)
                {
                    if (_servers.Find(server => server.ServerType == ServerType.AstsBridge) != null)
                    {
                        return;
                    }
                    try
                    {
                        AstsBridgeServer serv = new AstsBridgeServer(neadLoadTicks);
                        _servers.Add(serv);
                    }
                    catch (Exception)
                    {
                        MessageBox.Show(
                            "Произошла ошибка создания сервера Плаза. Вероятно у Вас не установлено соответствующее программное обеспечение.");
                        return;
                    }

                    if (ServerCreateEvent != null)
                    {
                        ServerCreateEvent();
                    }
                }
                else if (type == ServerType.Tester)
                {
                    try
                    {
                        TesterServer serv = new TesterServer();
                        _servers.Add(serv);
                    }
                    catch (Exception)
                    {
                        MessageBox.Show(
                            "Произошла ошибка создания тестового сервера.");
                        return;
                    }

                    if (ServerCreateEvent != null)
                    {
                        ServerCreateEvent();
                    }
                }
                else if (type == ServerType.Finam)
                {
                    try
                    {
                        FinamServer serv = new FinamServer();
                        _servers.Add(serv);
                    }
                    catch (Exception)
                    {
                        MessageBox.Show(
                            "Произошла ошибка создания тестового сервера.");
                        return;
                    }

                    if (ServerCreateEvent != null)
                    {
                        ServerCreateEvent();
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// создать новый сервер оптимизации
        /// </summary>
        public static OptimizerServer CreateNextOptimizerServer(OptimizerDataStorage storage, int num, decimal portfolioStartVal)
        {
            _servers = new List<IServer>();

            OptimizerServer serv = new OptimizerServer(storage, num, portfolioStartVal);
            _servers.Add(serv);

            if (ServerCreateEvent != null)
            {
                ServerCreateEvent();
            }
            return serv;
        }

        /// <summary>
        /// взять сервер
        /// </summary>
        public static List<IServer> GetServers()
        {
            return _servers;
        }

        /// <summary>
        /// создан новый сервер
        /// </summary>
        public static event Action ServerCreateEvent;

// создание серверов автоматически

        /// <summary>
        /// загрузить настройки сервера
        /// </summary>
        public static void Activate()
        {
            Load();
            Thread starterThread = new Thread(ThreadStarterWorkArea);
            starterThread.IsBackground = true;
            starterThread.Name = "SeverMasterAutoStartThread";
            starterThread.Start();
        }

        private static ServerMasterPortfoliosPainter _painter;

        /// <summary>
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
        /// можно ли сервер мастеру разворачивать сервера в автоматическом режиме
        /// </summary>
        public static bool NeadToConnectAuto;

        /// <summary>
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
        /// сервера, которые заказали роботы
        /// </summary>
        private static List<ServerType> _needServerTypes;

        /// <summary>
        /// серверы которые мы уже пытались подключить
        /// </summary>
        private static List<ServerType> _tryActivateServerTypes;

        /// <summary>
        /// место работы потока который подключает наши сервера в авто режиме
        /// </summary>
        private static void ThreadStarterWorkArea()
        {
            if (StartProgram == ServerStartProgramm.IsTester)
            {
                return;
            }
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
                    TryStartThisSevrverInAutoType(_needServerTypes[i]);
                }
            }
        }
        
        /// <summary>
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
            { // если у нас нашего сервера нет - создаём его
                CreateServer(type,true);
            }

            List<IServer> servers = GetServers();

            if (servers == null)
            { // что-то пошло не так
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

// доступ к портфелю и его прорисовка

        /// <summary>
        /// начать прорисовывать контролы класса 
        /// </summary>
        public static void StartPaint()
        {
             _painter.StartPaint();
        }

        /// <summary>
        /// остановить прорисовку контролов класса 
        /// </summary>
        public static void StopPaint()
        {
            _painter.StopPaint();
        }

        /// <summary>
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
        /// добавить элементы, на котором будут прорисовываться портфели и ордера
        /// </summary>
        public static void SetHostTable(WindowsFormsHost hostPortfolio, WindowsFormsHost hostOrders)
        {
            _painter = new ServerMasterPortfoliosPainter();
            _painter.LogMessageEvent += SendNewLogMessage;
            _painter.SetHostTable(hostPortfolio, hostOrders);
        }

        // сообщения в лог

        /// <summary>
        /// выслать новое сообщение на верх
        /// </summary>
        private static void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            { // если на нас никто не подписан и в логе ошибка
                MessageBox.Show(message);
            }
        }

        /// <summary>
        /// исходящее сообщение для лога
        /// </summary>
        public static event Action<string, LogMessageType> LogMessageEvent;

    }

    /// <summary>
    /// какая программа запустила мастер серверов
    /// </summary>
    public enum ServerStartProgramm
    {
        /// <summary>
        /// тестер
        /// </summary>
        IsTester,

        /// <summary>
        /// оптимизатор
        /// </summary>
        IsOsOptimizer,

        /// <summary>
        /// качалка данных
        /// </summary>
        IsOsData,

        /// <summary>
        /// терминал
        /// </summary>
        IsOsTrader,

        /// <summary>
        /// конвертер тиков в свечи
        /// </summary>
        IsOsConverter,

        /// <summary>
        /// майнер паттернов
        /// </summary>
        IsOsMiner
    }

    /// <summary>
    /// тип подключения к торгам. Тип сервера
    /// </summary>
    public enum ServerType
    {
        /// <summary>
        /// биржа криптовалют Bitfinex
        /// </summary>
        Bitfinex,

        /// <summary>
        /// биржа криптовалют Binance
        /// </summary>
        Binance,

        /// <summary>
        /// нинзя трейдер
        /// </summary>
        NinjaTrader,

        /// <summary>
        /// биржа криптовалют Kraken
        /// </summary>
        Kraken,

        /// <summary>
        /// форекс брокер Oanda
        /// </summary>
        Oanda,

        /// <summary>
        /// биржа криптовалют BitMEX
        /// </summary>
        BitMex,

        /// <summary>
        /// биржа криптовалют BitStamp
        /// </summary>
        BitStamp,

        /// <summary>
        /// Оптимизатор
        /// </summary>
        Optimizer,

        /// <summary>
        /// Майнер
        /// </summary>
        Miner,

        /// <summary>
        /// Квик луа
        /// </summary>
        QuikLua,

        /// <summary>
        /// Квик
        /// </summary>
        QuikDde,

        /// <summary>
        /// Смарт-Ком
        /// </summary>
        SmartCom,

        /// <summary>
        /// Плаза 2
        /// </summary>
        Plaza,

        /// <summary>
        /// Тестер
        /// </summary>
        Tester,

        /// <summary>
        /// IB
        /// </summary>
        InteractivBrokers,

        /// <summary>
        /// Финам
        /// </summary>
        Finam,

        /// <summary>
        /// AstsBridge, он же ШЛЮЗ, он же TEAP 
        /// </summary>
        AstsBridge,

        /// <summary>
        /// Тип сервера не назначен
        /// </summary>
        Unknown
    }
}
