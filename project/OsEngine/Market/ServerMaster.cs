﻿/*
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
using OsEngine.Market.Servers.BitMex;
using OsEngine.Market.Servers.BitStamp;
using OsEngine.Market.Servers.Finam;
using OsEngine.Market.Servers.InteractivBrokers;
using OsEngine.Market.Servers.Kraken;
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
using MessageBox = System.Windows.MessageBox;

namespace OsEngine.Market
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
        /// взять типы торговых серверов в системе
        /// </summary>
        public static List<ServerType> ServersTypes
        {
            get
            {
                List<ServerType> serverTypes = new List<ServerType>
                {
                    ServerType.QuikDde,
                    ServerType.QuikLua,
                    ServerType.SmartCom,
                    ServerType.Plaza,
                    ServerType.Transaq,

                    ServerType.Binance,
                    ServerType.BitMex,
                    ServerType.BitStamp,
                    ServerType.Bitfinex,
                    ServerType.Kraken,

                    ServerType.InteractivBrokers,
                    ServerType.NinjaTrader,
                    ServerType.Lmax,
                    ServerType.Oanda,

                    ServerType.Finam,
                    ServerType.AstsBridge
                };

                return serverTypes;
            }
        }

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

                if (_servers.Find(server => server.ServerType == type) != null)
                {
                    return;
                }

                IServer newServer = null;

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

                ServerCreateEvent?.Invoke(newServer);

                SendNewLogMessage(OsLocalization.Market.Message3 + _servers[_servers.Count - 1].ServerType, LogMessageType.System);
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

            ServerCreateEvent?.Invoke(serv);
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
        public static event Action<IServer> ServerCreateEvent;

        /// <summary>
        /// взять список существующих подключений
        /// </summary>
        /// <returns></returns>
        public static List<ServerType> GetServerTypes()
        {
            List<ServerType> types = new List<ServerType>
            {
                ServerType.AstsBridge,
                ServerType.Binance,
                ServerType.BitMex,
                ServerType.BitStamp,
                ServerType.Bitfinex,
                ServerType.Finam,
                ServerType.InteractivBrokers,
                ServerType.Kraken,
                ServerType.Lmax,
                ServerType.NinjaTrader,
                ServerType.Oanda,
                ServerType.Plaza,
                ServerType.QuikDde,
                ServerType.QuikLua,
                ServerType.SmartCom
            };

            return types;
        }

// создание серверов автоматически

        /// <summary>
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
    /// какая программа запустила класс
    /// </summary>
    public enum StartProgram
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
        /// Тип сервера не назначен
        /// </summary>
        None,

        /// <summary>
        /// транзак
        /// </summary>
        Transaq,

        /// <summary>
        /// биржа LMax
        /// </summary>
        Lmax,

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
        AstsBridge
    }
}
