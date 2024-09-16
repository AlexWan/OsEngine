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
using OsEngine.Market.Servers.InteractiveBrokers;
using OsEngine.Market.Servers.Kraken;
using OsEngine.Market.Servers.Lmax;
using OsEngine.Market.Servers.NinjaTrader;
using OsEngine.Market.Servers.Optimizer;
using OsEngine.Market.Servers.Plaza;
using OsEngine.Market.Servers.Quik;
using OsEngine.Market.Servers.QuikLua;
using OsEngine.Market.Servers.Tester;
using OsEngine.Market.Servers.Transaq;
using OsEngine.Market.Servers.ZB;
using OsEngine.Market.Servers.Hitbtc;
using OsEngine.Market.Servers.MFD;
using OsEngine.Market.Servers.MOEX;
using OsEngine.Market.Servers.TinkoffInvestments;
using MessageBox = System.Windows.MessageBox;
using OsEngine.Market.Servers.Bybit;
using OsEngine.Market.Servers.OKX;
using OsEngine.Market.Servers.BitMaxFutures;
using OsEngine.Market.Servers.BitGet.BitGetSpot;
using OsEngine.Market.Servers.BitGet.BitGetFutures;
using OsEngine.Market.Servers.Alor;
using OsEngine.Market.Servers.GateIo.GateIoSpot;
using OsEngine.Market.Servers.GateIo.GateIoFutures;
using OsEngine.Market.Servers.KuCoin.KuCoinSpot;
using OsEngine.Market.Servers.KuCoin.KuCoinFutures;
using OsEngine.Market.Servers.BinGxSpot;
using OsEngine.Market.Servers.BingX.BingXSpot;
using OsEngine.Market.Servers.BingX.BingXFutures;
using OsEngine.Market.Servers.Deribit;
using OsEngine.Market.Servers.XT.XTSpot;
using OsEngine.Market.Servers.Pionex;
using OsEngine.Market.Servers.Woo;
using OsEngine.Market.Servers.MoexAlgopack;
using OsEngine.Market.Servers.HTX.Spot;
using OsEngine.Market.Servers.HTX.Futures;
using OsEngine.Market.Servers.HTX.Swap;
using OsEngine.Market.Servers.MoexFixFastSpot;
using OsEngine.Market.Servers.BitMart;
using OsEngine.Market.Servers.MoexFixFastCurrency;

namespace OsEngine.Market
{
    /// <summary>
    /// class exchange server manager
    /// класс менеджер серверов подключения к бирже
    /// </summary>
    public class ServerMaster
    {

        #region Service

        /// <summary>
        /// show settings
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
        /// save settings
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

        #endregion

        #region Creating and storing servers

        /// <summary>
        /// array of deployed servers
        /// </summary>
        private static List<IServer> _servers;

        /// <summary>
        /// take trade server typre from system
        /// </summary>
        public static List<ServerType> ServersTypes
        {
            get
            {
                List<ServerType> serverTypes = new List<ServerType>();

                
                serverTypes.Add(ServerType.Alor);
                serverTypes.Add(ServerType.QuikDde);
                serverTypes.Add(ServerType.QuikLua);
                serverTypes.Add(ServerType.Plaza);
                serverTypes.Add(ServerType.Transaq);
                serverTypes.Add(ServerType.TinkoffInvestments);
                serverTypes.Add(ServerType.Finam);
                serverTypes.Add(ServerType.MoexDataServer);
                serverTypes.Add(ServerType.MfdWeb);

                serverTypes.Add(ServerType.GateIoSpot);
                serverTypes.Add(ServerType.GateIoFutures);
                serverTypes.Add(ServerType.AscendEx_BitMax);
                serverTypes.Add(ServerType.Deribit);
                serverTypes.Add(ServerType.Binance);
                serverTypes.Add(ServerType.BinanceFutures);
                serverTypes.Add(ServerType.BitMex);
                serverTypes.Add(ServerType.BitStamp);
                serverTypes.Add(ServerType.Bitfinex);
                serverTypes.Add(ServerType.Kraken);
                serverTypes.Add(ServerType.KuCoinSpot);
                serverTypes.Add(ServerType.KuCoinFutures);
                serverTypes.Add(ServerType.Exmo);
                serverTypes.Add(ServerType.Zb);
                serverTypes.Add(ServerType.Hitbtc);
                serverTypes.Add(ServerType.HTXSpot);
                serverTypes.Add(ServerType.HTXFutures);
                serverTypes.Add(ServerType.HTXSwap);
                serverTypes.Add(ServerType.Bybit);
                serverTypes.Add(ServerType.OKX);
                serverTypes.Add(ServerType.Bitmax_AscendexFutures);
                serverTypes.Add(ServerType.BitGetSpot);
                serverTypes.Add(ServerType.BitGetFutures);
                serverTypes.Add(ServerType.BingXSpot);
                serverTypes.Add(ServerType.BingXFutures);
                serverTypes.Add(ServerType.XTSpot);
                serverTypes.Add(ServerType.PionexSpot);
                serverTypes.Add(ServerType.Woo);
                serverTypes.Add(ServerType.MoexAlgopack);
                serverTypes.Add(ServerType.MoexFixFastSpot);
                serverTypes.Add(ServerType.InteractiveBrokers);
                serverTypes.Add(ServerType.NinjaTrader);
                serverTypes.Add(ServerType.Lmax);
                serverTypes.Add(ServerType.BitMart);
                serverTypes.Add(ServerType.MoexFixFastCurrency);

                serverTypes.Add(ServerType.AstsBridge);


                // а теперь сортируем в зависимости от предпочтений пользователя

                List<ServerPop> popularity = LoadMostPopularServersWithCount();

                for (int i = 0; i < popularity.Count; i++)
                {
                    for(int i2 = 0;i2 < serverTypes.Count;i2++)
                    {
                        if(serverTypes[i2] == popularity[i].ServerType)
                        {
                            serverTypes.RemoveAt(i2);
                            i2--;
                            break;
                        }
                    }
                }

                for (int i = 0; i < popularity.Count; i++)
                {
                    if (popularity[i].ServerType == ServerType.Tester)
                    {
                        continue;
                    }

                    if (popularity[i].ServerType == ServerType.Finam)
                    {
                        continue;
                    }

                    bool isInArray = false;

                    for (int i2 = 0; i2 < serverTypes.Count; i2++)
                    {
                        if(serverTypes[i2].ToString() == popularity[i].ServerType.ToString())
                        {
                            isInArray = true;
                            break;
                        }
                    }

                    if(isInArray)
                    {
                        continue;
                    }

                    serverTypes.Insert(0, popularity[i].ServerType);
                }

                for (int i = 0; i < serverTypes.Count; i++)
                {
                    if(serverTypes[i].ToString() == "None")
                    {
                        serverTypes.RemoveAt(i);
                        break;
                    }
                }

                return serverTypes;
            }
        }

        /// <summary>
        /// take trade server typre from system
        /// </summary>
        public static List<ServerType> ServersTypesToOsData
        {
            get
            {
                List<ServerType> serverTypes = new List<ServerType>();

                serverTypes.Add(ServerType.TinkoffInvestments);
                serverTypes.Add(ServerType.XTSpot);
                serverTypes.Add(ServerType.Deribit);
                serverTypes.Add(ServerType.KuCoinSpot);
                serverTypes.Add(ServerType.Alor);
                serverTypes.Add(ServerType.Finam);
                serverTypes.Add(ServerType.MoexDataServer);
                serverTypes.Add(ServerType.MfdWeb);
                serverTypes.Add(ServerType.MoexAlgopack);
                serverTypes.Add(ServerType.MoexFixFastSpot);
                serverTypes.Add(ServerType.AscendEx_BitMax);
                serverTypes.Add(ServerType.Binance);
                serverTypes.Add(ServerType.BinanceFutures);
                serverTypes.Add(ServerType.BingXFutures);
                serverTypes.Add(ServerType.BitMex);
                serverTypes.Add(ServerType.BitStamp);
                serverTypes.Add(ServerType.Bitfinex);
                serverTypes.Add(ServerType.Kraken);
                serverTypes.Add(ServerType.Exmo);
                serverTypes.Add(ServerType.HTXSpot);
                serverTypes.Add(ServerType.HTXFutures);
                serverTypes.Add(ServerType.HTXSwap);
                serverTypes.Add(ServerType.Bybit);
                serverTypes.Add(ServerType.OKX);
                serverTypes.Add(ServerType.Woo);

                return serverTypes;
            }
        }

        /// <summary>
        /// are there any active servers
        /// </summary>
        public static bool HasActiveServers()
        {
            return _servers != null && _servers.Count > 0;
        }

        /// <summary>
        /// array of active servers
        /// </summary>
        public static List<IServer> GetServers()
        {
            return _servers;
        }

        /// <summary>
        /// array of active servers types
        /// </summary>
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
        /// create server
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

                SaveMostPopularServers(type);

                IServer newServer = null;

                if (type == ServerType.MoexFixFastCurrency)
                {
                    newServer = new MoexFixFastCurrencyServer();
                }
                if (type == ServerType.BingXSpot)
                {
                    newServer = new BingXServerSpot();
                }
                if (type == ServerType.MoexAlgopack)
                {
                    newServer = new MoexAlgopackServer();
                }
                if (type == ServerType.MoexFixFastSpot)
                {
                    newServer = new MoexFixFastSpotServer();
                }
                if (type == ServerType.XTSpot)
                {
                    newServer = new XTServerSpot();
                }
                if (type == ServerType.BingXFutures)
                {
                    newServer = new BingXServerFutures();
                }
                if (type == ServerType.KuCoinFutures)
                {
                    newServer = new KuCoinFuturesServer();
                }
                if (type == ServerType.KuCoinSpot)
                {
                    newServer = new KuCoinSpotServer();
                }
                if (type == ServerType.Alor)
                {
                    newServer = new AlorServer();
                }
                if (type == ServerType.BitGetFutures)
                {
                    newServer = new BitGetServerFutures();
                }
                if (type == ServerType.BitGetSpot)
                {
                    newServer = new BitGetServerSpot();
                }
                if (type == ServerType.Bitmax_AscendexFutures)
                {
                    newServer = new BitMaxFuturesServer();
                }
                if (type == ServerType.OKX)
                {
                    newServer = new OkxServer();
                }
                if (type == ServerType.MfdWeb)
                {
                    newServer = new MfdServer();
                }
                if (type == ServerType.MoexDataServer)
                {
                    newServer = new MoexDataServer();
                }
                if (type == ServerType.TinkoffInvestments)
                {
                    newServer = new TinkoffInvestmentsServer();
                }
                if (type == ServerType.Hitbtc)
                {
                    newServer = new HitbtcServer();
                }
                if (type == ServerType.GateIoSpot)
                {
                    newServer = new GateIoServerSpot();
                }
                if (type == ServerType.GateIoFutures)
                {
                    newServer = new GateIoServerFutures();
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
                if (type == ServerType.AscendEx_BitMax)
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
                    newServer = new BinanceServerSpot();
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
                    newServer = new KrakenServer();
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
                if (type == ServerType.InteractiveBrokers)
                {
                    newServer = new InteractiveBrokersServer();
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
                else if (type == ServerType.Deribit)
                {
                    newServer = new DeribitServer();
                }
                else if (type == ServerType.PionexSpot)
                {
                    newServer = new PionexServerSpot();
                }
                else if (type == ServerType.Woo)
                {
                    newServer = new WooServer();
                }
                else if (type == ServerType.HTXSpot)
                {
                    newServer = new HTXSpotServer();
                }
                else if (type == ServerType.HTXFutures)
                {
                    newServer = new HTXFuturesServer();
                }
                else if (type == ServerType.HTXSwap)
                {
                    newServer = new HTXSwapServer();
                }
                else if (type == ServerType.BitMart)
                {
                    newServer = new BitMartServer();
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
        /// save the types of servers that are most often run by the user
        /// </summary>
        private static void SaveMostPopularServers(ServerType type)
        {
            List<ServerPop> servers = LoadMostPopularServersWithCount();

            bool isInArray = false;

            for(int i = 0;i < servers.Count;i++)
            {
                if(servers[i].ServerType == type)
                {
                    servers[i].CountOfCreation += 1;
                    isInArray = true;
                    break;
                }
            }

            if (isInArray == false)
            {
                ServerPop curServ = new ServerPop();
                curServ.ServerType = type;
                curServ.CountOfCreation = 1;
                servers.Add(curServ);
            }

            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + @"MostPopularServers.txt", false))
                {
                    List<ServerType> alreadySaveServers = new List<ServerType>();

                    for(int i = 0;i < servers.Count;i++)
                    {
                        bool isSaved = false;
                        for(int i2 = 0; i2 < alreadySaveServers.Count;i2++)
                        {
                            if(alreadySaveServers[i2] == servers[i].ServerType)
                            {
                                isSaved = true;
                                break;
                            }
                        }

                        if(isSaved)
                        {
                            continue;
                        }

                        alreadySaveServers.Add(servers[i].ServerType);
                        string saveStr = servers[i].ServerType + "&" + servers[i].CountOfCreation;
                        writer.WriteLine(saveStr);
                    }

                    writer.Close();
                }
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>
        /// load the types of servers that are most often run by the user
        /// </summary>
        public static List<ServerPop> LoadMostPopularServersWithCount()
        {
            List<ServerPop> servers = new List<ServerPop>();

            if (!File.Exists(@"Engine\" + @"MostPopularServers.txt"))
            {
                return servers;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + @"MostPopularServers.txt"))
                {
                    while(reader.EndOfStream == false)
                    {
                        string res = reader.ReadLine();

                        if(res.Split('&').Length <= 1)
                        {
                            continue;
                        }

                        string[] saveInStr = res.Split('&');

                        ServerPop curServ = new ServerPop();

                        Enum.TryParse(saveInStr[0], out curServ.ServerType);
                        curServ.CountOfCreation = Convert.ToInt32(saveInStr[1]);

                        servers.Add(curServ);
                    }

                    reader.Close();
                }
            }
            catch
            {
                // ignore
            }

            if(servers.Count > 1)
            {

                for(int i = 0;i < servers.Count;i++)
                {
                    ServerPop curServ = servers[i];

                    for(int i2 = i;i2 < servers.Count;i2++)
                    {
                        if(servers[i2].CountOfCreation < curServ.CountOfCreation)
                        {
                            servers[i] = servers[i2];
                            servers[i2] = curServ;
                        }
                    }
                }
            }

            return servers;
        }

        private static object _optimizerGeneratorLocker = new object();

        /// <summary>
        /// create a new optimization server
        /// </summary>
        public static OptimizerServer CreateNextOptimizerServer(OptimizerDataStorage storage,
            int num, decimal portfolioStartVal)
        {

            OptimizerServer serv = new OptimizerServer(storage, num, portfolioStartVal);

            if (serv == null)
            {
                return null;
            }

            bool isInArray = false;

            lock (_optimizerGeneratorLocker)
            {
                if (_servers == null)
                {
                    _servers = new List<IServer>();
                }

                for (int i = 0; i < _servers.Count; i++)
                {
                    IServer ser = _servers[i];

                    if (ser == null)
                    {
                        _servers.RemoveAt(i);
                        i--;
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

                return serv;
            }
        }

        /// <summary>
        /// delete server to optimize by number
        /// </summary>
        public static void RemoveOptimizerServer(OptimizerServer server)
        {
            server.ClearDelete();

            lock (_optimizerGeneratorLocker)
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
        }

        /// <summary>
        /// new server created
        /// </summary>
        public static event Action<IServer> ServerCreateEvent;

        #endregion

        #region Automatic creation servers

        /// <summary>
        /// activate automatic server deployment
        /// </summary>
        public static void ActivateAutoConnection()
        {
            Load();

            Task task = new Task(ThreadStarterWorkArea);
            task.Start();
        }

        /// <summary>
        /// shows whether the server-master can be deployed in automatic mode  
        /// </summary>
        public static bool NeadToConnectAuto;

        private static string _startServerLocker = "startServLocker";

        /// <summary>
        /// select a specific server type for auto connection
        /// </summary>
        public static void SetServerToAutoConnection(ServerType type)
        {
            lock (_startServerLocker)
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

                    _needServerTypes.Add(type);
                }
                catch (Exception error)
                {
                    LogMessageEvent(error.ToString(), LogMessageType.Error);
                }
            }
        }

        /// <summary>
        /// selected bot servers for auto connection
        /// </summary>
        private static List<ServerType> _needServerTypes;

        /// <summary>
        /// servers that we have already treid to connect
        /// </summary>
        private static List<ServerType> _tryActivateServerTypes;

        /// <summary>
        /// work place of the thread that connects our servers in auto mode
        /// </summary>
        private static async void ThreadStarterWorkArea()
        {
            await Task.Delay(20000);

            while (true)
            {
                try
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
                        TryStartThisServerInAutoType(_needServerTypes[i]);
                    }
                }
                catch(Exception ex)
                {
                    SendNewLogMessage(ex.ToString(),LogMessageType.Error);
                }
            }
        }

        /// <summary>
        /// try running this server
        /// </summary>
        private static void TryStartThisServerInAutoType(ServerType type)
        {
            try
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
                    CreateServer(type, true);
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
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(),LogMessageType.Error);
            }
        }

        #endregion

        #region Access to servers permissions

        /// <summary>
        /// array of previously created permissions for servers
        /// </summary>
        private static List<IServerPermission> _serversPermissions = new List<IServerPermission>();

        /// <summary>
        /// array of servers types for which there are no implementations IserverPermission
        /// </summary>
        private static List<ServerType> _noServerPermissionServers = new List<ServerType>();

        /// <summary>
        /// object blocking multithreaded access to the functions of creating permission objects.
        /// </summary>
        private static string _serverPermissionGeterLocker = "serverPermissionLocker";

        /// <summary>
        /// request server permissions of the type
        /// </summary>
        public static IServerPermission GetServerPermission(ServerType type)
        {
            for(int i = 0;i < _serversPermissions.Count;i++)
            {
                if (_serversPermissions[i].ServerType == type)
                {
                    return _serversPermissions[i];
                }
            }

            for(int i = 0;i < _noServerPermissionServers.Count;i++)
            {
                if (_noServerPermissionServers[i] == type)
                {
                    return null;
                }
            }

            lock(_serverPermissionGeterLocker)
            {
                for (int i = 0; i < _serversPermissions.Count; i++)
                {
                    if (_serversPermissions[i].ServerType == type)
                    {
                        return _serversPermissions[i];
                    }
                }

                for (int i = 0; i < _noServerPermissionServers.Count; i++)
                {
                    if (_noServerPermissionServers[i] == type)
                    {
                        return null;
                    }
                }

                IServerPermission serverPermission = null;

                if (type == ServerType.KuCoinSpot)
                {
                    serverPermission = new KuCoinSpotServerPermission();
                }
                else if (type == ServerType.QuikLua)
                {
                    serverPermission = new QuikLuaServerPermission();
                }
                else if (type == ServerType.KuCoinFutures)
                {
                    serverPermission = new KuCoinFuturesServerPermission();
                }
                else if (type == ServerType.MoexAlgopack)
                {
                    serverPermission = new MoexAlgopackServerPermission();
                }
                else if (type == ServerType.MoexFixFastSpot)
                {
                    serverPermission = new MoexFixFastSpotServerPermission();
                }
                else if (type == ServerType.XTSpot)
                {
                    serverPermission = new XTSpotServerPermission();
                }
                else if (type == ServerType.Transaq)
                {
                    serverPermission = new TransaqServerPermission();
                }
                else if (type == ServerType.BingXSpot)
                {
                    serverPermission = new BingXSpotServerPermission();
                }
                else if (type == ServerType.BingXFutures)
                {
                    serverPermission = new BingXFuturesServerPermission();
                }
                else if (type == ServerType.Alor)
                {
                    serverPermission = new AlorServerPermission();
                }
                else if (type == ServerType.BitGetSpot)
                {
                    serverPermission = new BitGetSpotServerPermission();
                }
                else if (type == ServerType.BitGetFutures)
                {
                    serverPermission = new BitGetFuturesServerPermission();
                }
                else if (type == ServerType.AscendEx_BitMax)
                {
                    serverPermission = new BitmaxServerPermission();
                }
                else if (type == ServerType.OKX)
                {
                    serverPermission = new OkxServerPermission();
                }
                else if (type == ServerType.Binance)
                {
                    serverPermission = new BinanceSpotServerPermission();
                }
                else if (type == ServerType.BinanceFutures)
                {
                    serverPermission = new BinanceFuturesServerPermission();
                }
                else if (type == ServerType.Bitfinex)
                {
                    serverPermission = new BitFinexServerPermission();
                }
                else if (type == ServerType.Kraken)
                {
                    serverPermission = new KrakenServerPermission();
                }
                else if (type == ServerType.MoexDataServer)
                {
                    serverPermission = new MoexIssPermission();
                }
                else if (type == ServerType.MfdWeb)
                {
                    serverPermission = new MfdServerPermission();
                }
                else if (type == ServerType.Finam)
                {
                    serverPermission = new FinamServerPermission();
                }
                else if (type == ServerType.TinkoffInvestments)
                {
                    serverPermission = new TinkoffInvestmentsServerPermission();
                }
                else if (type == ServerType.GateIoFutures)
                {
                    serverPermission = new GateIoServerFuturesPermission();
                }
                else if (type == ServerType.GateIoSpot)
                {
                    serverPermission = new GateIoSpotServerPermission();
                }
                else if (type == ServerType.Bybit)
                {
                    serverPermission = new BybitServerPermission();
                }
                else if (type == ServerType.InteractiveBrokers)
                {
                    serverPermission = new InteractiveBrokersServerPermission();
                }
                else if (type == ServerType.Deribit)
                {
                    serverPermission = new DeribitServerPermission();
                }
                else if (type == ServerType.PionexSpot)
                {
                    serverPermission = new PionexServerSpotPermission();
                }
                else if (type == ServerType.Woo)
                {
                    serverPermission = new WooServerPermission();
                }
                else if (type == ServerType.HTXSpot)
                {
                    serverPermission = new HTXSpotServerPermission();
                }
                else if (type == ServerType.HTXFutures)
                {
                    serverPermission = new HTXFuturesServerPermission();
                }
                else if (type == ServerType.HTXSwap)
                {
                    serverPermission = new HTXSwapServerPermission();
                }
                else if (type == ServerType.Plaza)
                {
                    serverPermission = new PlazaServerPermission();
                }
                else if (type == ServerType.BitMart)
                {
                    serverPermission = new BitMartServerPermission();
                }
                else if (type == ServerType.MoexFixFastCurrency)
                {
                    serverPermission = new MoexFixFastCurrencyServerPermission();
                }

                if (serverPermission != null)
                {
                    _serversPermissions.Add(serverPermission);
                    return serverPermission;
                }
                else
                {
                    _noServerPermissionServers.Add(type);
                }
            }

            return null;
        }

        #endregion

        #region Access to portfolio, orders and its drawing

        /// <summary>
        /// start to draw class controls
        /// </summary>
        public static void StartPaint()
        {
             _painterPortfolios.StartPaint();
            _ordersStorage.StartPaint();
        }

        /// <summary>
        /// stop to draw class controls
        /// </summary>
        public static void StopPaint()
        {
            _painterPortfolios.StopPaint();
            _ordersStorage.StopPaint();
        }

        private static ServerMasterPortfoliosPainter _painterPortfolios;

        private static ServerMasterOrdersPainter _ordersStorage;

        /// <summary>
        /// clear the order list in the table
        /// </summary>
        public static void ClearOrders()
        {
            if (_painterPortfolios == null)
            {
                return;
            }
            _ordersStorage.ClearOrders();
        }

        /// <summary>
        /// add items on which portfolios and orders will be drawn
        /// </summary>
        public static void SetHostTable(WindowsFormsHost hostPortfolio, WindowsFormsHost hostActiveOrders, 
            WindowsFormsHost hostHistoricalOrders)
        {
            if (_painterPortfolios == null)
            {
                _painterPortfolios = new ServerMasterPortfoliosPainter();
                _painterPortfolios.LogMessageEvent += SendNewLogMessage;
                _painterPortfolios.ClearPositionOnBoardEvent += _painterPortfolios_ClearPositionOnBoardEvent;
                _painterPortfolios.SetHostTable(hostPortfolio);
            }

            if(_ordersStorage == null)
            {
                _ordersStorage = new ServerMasterOrdersPainter();
                _ordersStorage.LogMessageEvent += SendNewLogMessage;
                _ordersStorage.SetHostTable(hostActiveOrders, hostHistoricalOrders);
                _ordersStorage.RevokeOrderToEmulatorEvent += _ordersStorage_RevokeOrderToEmulatorEvent;
            }
        }

        /// <summary>
        /// add a draw order 
        /// </summary>
        public static void InsertOrder(Order order)
        {
            if (_ordersStorage != null)
            {
                _ordersStorage.InsertOrder(order);
            }
        }

        private static void _painterPortfolios_ClearPositionOnBoardEvent(string sec, IServer server, string fullName)
        {
            if(ClearPositionOnBoardEvent != null)
            {
                ClearPositionOnBoardEvent(sec, server, fullName);
            }
        }

        private static void _ordersStorage_RevokeOrderToEmulatorEvent(Order order)
        {
            if(RevokeOrderToEmulatorEvent != null)
            {
                RevokeOrderToEmulatorEvent(order);
            }
        }

        public static event Action<Order> RevokeOrderToEmulatorEvent;

        public static event Action<string, IServer, string> ClearPositionOnBoardEvent;

        #endregion

        #region Log

        /// <summary>
        /// enable object logging
        /// </summary>
        public static void ActivateLogging()
        {
            if (Log == null)
            {
                Log = new Log("ServerMaster",StartProgram.IsOsTrader);
                Log.ListenServerMaster();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public static Log Log;

        /// <summary>
        /// send new message to up
        /// </summary>
        public static void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            { // if nobody is subscribled to us and there is a log error
              // если на нас никто не подписан и в логе ошибка
                MessageBox.Show(message);
            }
        }

        /// <summary>
        /// outgoing log message event
        /// </summary>
        public static event Action<string, LogMessageType> LogMessageEvent;

        #endregion

    }

    public class ServerPop
    {
        public ServerType ServerType;

        public int CountOfCreation;
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
        /// connection to Russian broker Tinkoff Investments
        /// подключение к Тинькофф Инвестициям (версия 3 коннектора)
        /// </summary>
        TinkoffInvestments,

        /// <summary>
        /// cryptocurrency exchange Hitbtc
        /// биржа криптовалют Hitbtc
        /// </summary>
        Hitbtc,

        /// <summary>
        /// cryptocurrency exchange Gate.io
        /// биржа криптовалют Gate.io
        /// </summary>
        GateIoSpot,

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
        /// BitMax exchange
        /// биржа BitMax
        /// </summary>
        AscendEx_BitMax,

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
        InteractiveBrokers,

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
        /// Bybit exchange
        /// </summary>
        Bybit,

        /// <summary>
        /// OKX exchange
        /// </summary>
        OKX,

        /// <summary>
        /// Ascendex exchange
        /// </summary>
        Bitmax_AscendexFutures,

        /// <summary>
        /// BitGetSpot exchange
        /// </summary>
        BitGetSpot,

        /// <summary>
        /// BitGetFutures exchange
        /// </summary>
        BitGetFutures,
        
        /// <summary>
        /// Alor OpenAPI & Websocket
        /// </summary>
        Alor,

        /// <summary>
        /// KuCoinSpot exchange
        /// </summary>
        KuCoinSpot,

        /// <summary>
        /// KuCoinSpot exchange
        /// </summary>
        KuCoinFutures,

        /// <summary>
        /// BingXSpot exchange
        /// </summary>
        BingXSpot,

        /// <summary>
        /// BingXFutures exchange
        /// </summary>
        BingXFutures,

        /// <summary>
        /// Deribit exchange
        /// </summary>
        Deribit,

        /// <summary>
        /// XT Spot exchange
        /// </summary>
        XTSpot,

        /// <summary>
        /// Pionex exchange
        /// </summary>
        PionexSpot,

        /// <summary>
        /// Woo exchange
        /// </summary>
        Woo,

        /// <summary>
        /// MoexAlgopack data-server
        /// </summary>
        MoexAlgopack,

        /// <summary>
        /// HTXSpot exchange
        /// </summary>
        HTXSpot,

        /// <summary>
        /// HTXFutures exchange
        /// </summary>
        HTXFutures,

        /// <summary>
        /// HTXSwap exchange
        /// </summary>
        HTXSwap,

        /// <summary>
        /// FIX/FAST for MOEX Spot
        /// </summary>
        MoexFixFastSpot,

        /// BitMart exchange
        /// </summary>
        BitMart,

        /// <summary>
        /// FIX/FAST for MOEX Currency
        /// </summary>
        MoexFixFastCurrency,
    }
}