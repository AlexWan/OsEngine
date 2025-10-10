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
using OsEngine.Market.Servers.BitMex;
using OsEngine.Market.Servers.Finam;
using OsEngine.Market.Servers.InteractiveBrokers;
using OsEngine.Market.Servers.NinjaTrader;
using OsEngine.Market.Servers.Optimizer;
using OsEngine.Market.Servers.Plaza;
using OsEngine.Market.Servers.QuikLua;
using OsEngine.Market.Servers.Tester;
using OsEngine.Market.Servers.Transaq;
using OsEngine.Market.Servers.MFD;
using OsEngine.Market.Servers.MOEX;
using OsEngine.Market.Servers.TInvest;
using MessageBox = System.Windows.MessageBox;
using OsEngine.Market.Servers.Bybit;
using OsEngine.Market.Servers.OKX;
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
using OsEngine.Market.Servers.BitMartFutures;
using OsEngine.Market.Servers.MoexFixFastCurrency;
using OsEngine.Market.Servers.MoexFixFastTwimeFutures;
using OsEngine.Market.Servers.TraderNet;
using OsEngine.Market.Servers.Mexc;
using OsEngine.Market.Servers.KiteConnect;
using OsEngine.Market.Servers.YahooFinance;
using OsEngine.Market.Servers.Atp;
using OsEngine.Market.Servers.Polygon;
using OsEngine.Market.Servers.CoinEx.Spot;
using OsEngine.Market.Servers.CoinEx.Futures;
using OsEngine.Market.Servers.RSSNews;
using OsEngine.Market.Servers.SmartLabNews;
using OsEngine.Market.Servers.AE;
using OsEngine.Market.Proxy;
using System.Net;
using OsEngine.Market.Servers.BloFin;
using OsEngine.Market.Servers.TelegramNews;
using OsEngine.Market.Servers.Bitfinex.BitfinexFutures;
using OsEngine.Market.Servers.FinamGrpc;
using OsEngine.Market.Servers.BinanceData;
using OsEngine.Market.AutoFollow;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader;
using System.Linq;
using OsEngine.Market.Servers.AscendexSpot;
using OsEngine.Market.Servers.OKXData;
using System.Windows.Controls;
using OsEngine.Market.Servers.ExMo.ExmoSpot;
using OsEngine.Market.Servers.BybitData;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.GateIoData;

namespace OsEngine.Market
{
    /// <summary>
    /// class exchange server manager
    /// класс менеджер серверов подключения к бирже
    /// </summary>
    public class ServerMaster
    {

        #region Service

        public static void Activate()
        {
            ActivateLogging();
        }

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
                if (_ui.WindowState == System.Windows.WindowState.Minimized)
                {
                    _ui.WindowState = System.Windows.WindowState.Normal;
                }

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
                    writer.WriteLine(NeedToConnectAuto);
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
                    NeedToConnectAuto = Convert.ToBoolean(reader.ReadLine());
                    reader.Close();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private static List<ServerType> _loadServerInstance = new List<ServerType>();

        public static void TryLoadServerInstance(ServerType serverType)
        {
            IServerPermission serverPermission = GetServerPermission(serverType);

            if (serverPermission == null
                || serverPermission.IsSupports_MultipleInstances == false)
            {
                return;
            }

            for (int i = 0; i < _loadServerInstance.Count; i++)
            {
                if (_loadServerInstance[i] == serverType)
                {
                    return;
                }
            }

            _loadServerInstance.Add(serverType);

            if (!File.Exists(@"Engine\" + serverType + @"ServerInstanceNumbers.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + serverType + @"ServerInstanceNumbers.txt"))
                {
                    while (reader.EndOfStream == false)
                    {
                        int currentNumber = Convert.ToInt32(reader.ReadLine());
                        CreateServer(serverType, false, currentNumber);
                    }

                    reader.Close();
                }
            }
            catch
            {
                // ignore
            }
        }

        public static void TrySaveServerInstance(List<AServer> servers)
        {
            try
            {
                if (servers == null
                    || servers.Count == 0)
                {
                    return;
                }

                ServerType serverType = servers[0].ServerType;

                using (StreamWriter writer = new StreamWriter(@"Engine\" + serverType + @"ServerInstanceNumbers.txt", false))
                {
                    for (int i = 0; i < servers.Count; i++)
                    {
                        writer.WriteLine(servers[i].ServerNum);
                    }

                    writer.Close();
                }
            }
            catch
            {
                // ignore
            }
        }

        public static void SaveServerInstanceByType(ServerType serverType)
        {
            try
            {
                List<AServer> serversArray = new List<AServer>();

                List<IServer> servers = ServerMaster.GetServers();

                for (int i = 0; i < servers.Count; i++)
                {
                    if (servers[i].ServerType == serverType)
                    {
                        serversArray.Add((AServer)servers[i]);
                    }
                }

                TrySaveServerInstance(serversArray);
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Creating and storing servers

        /// <summary>
        /// array of deployed servers
        /// </summary>
        private static List<IServer> _servers;

        /// <summary>
        /// get trade server types from system
        /// </summary>
        public static List<ServerType> ServersTypes
        {
            get
            {
                List<ServerType> serverTypes = new List<ServerType>();


                serverTypes.Add(ServerType.Alor);
                serverTypes.Add(ServerType.QuikLua);
                serverTypes.Add(ServerType.Plaza);
                serverTypes.Add(ServerType.Transaq);
                serverTypes.Add(ServerType.TInvest);
                serverTypes.Add(ServerType.Finam);
                serverTypes.Add(ServerType.FinamGrpc);
                serverTypes.Add(ServerType.MoexDataServer);
                serverTypes.Add(ServerType.MfdWeb);
                serverTypes.Add(ServerType.MoexAlgopack);
                serverTypes.Add(ServerType.MoexFixFastSpot);
                serverTypes.Add(ServerType.Atp);
                serverTypes.Add(ServerType.KiteConnect);
                serverTypes.Add(ServerType.TraderNet);
                serverTypes.Add(ServerType.InteractiveBrokers);
                serverTypes.Add(ServerType.NinjaTrader);
                serverTypes.Add(ServerType.GateIoSpot);
                serverTypes.Add(ServerType.GateIoFutures);
                serverTypes.Add(ServerType.Deribit);
                serverTypes.Add(ServerType.Binance);
                serverTypes.Add(ServerType.BinanceFutures);
                serverTypes.Add(ServerType.BitMex);
                serverTypes.Add(ServerType.BitfinexSpot);
                serverTypes.Add(ServerType.BitfinexFutures);
                serverTypes.Add(ServerType.KuCoinSpot);
                serverTypes.Add(ServerType.KuCoinFutures);
                serverTypes.Add(ServerType.ExmoSpot);
                serverTypes.Add(ServerType.HTXSpot);
                serverTypes.Add(ServerType.HTXFutures);
                serverTypes.Add(ServerType.HTXSwap);
                serverTypes.Add(ServerType.Bybit);
                serverTypes.Add(ServerType.OKX);
                serverTypes.Add(ServerType.BitGetSpot);
                serverTypes.Add(ServerType.BitGetFutures);
                serverTypes.Add(ServerType.BingXSpot);
                serverTypes.Add(ServerType.BingXFutures);
                serverTypes.Add(ServerType.XTSpot);
                serverTypes.Add(ServerType.PionexSpot);
                serverTypes.Add(ServerType.Woo);
                serverTypes.Add(ServerType.BitMartSpot);
                serverTypes.Add(ServerType.BitMartFutures);
                serverTypes.Add(ServerType.MoexFixFastCurrency);
                serverTypes.Add(ServerType.MoexFixFastTwimeFutures);
                serverTypes.Add(ServerType.MexcSpot);
                serverTypes.Add(ServerType.AstsBridge);
                serverTypes.Add(ServerType.CoinExSpot);
                serverTypes.Add(ServerType.CoinExFutures);
                serverTypes.Add(ServerType.RSSNews);
                serverTypes.Add(ServerType.SmartLabNews);
                serverTypes.Add(ServerType.AExchange);
                serverTypes.Add(ServerType.BloFinFutures);
                serverTypes.Add(ServerType.TelegramNews);
                serverTypes.Add(ServerType.BinanceData);
                serverTypes.Add(ServerType. AscendexSpot);

                // а теперь сортируем в зависимости от предпочтений пользователя

                List<ServerPop> popularity = LoadMostPopularServersWithCount();

                for (int i = 0; i < popularity.Count; i++)
                {
                    for (int i2 = 0; i2 < serverTypes.Count; i2++)
                    {
                        if (serverTypes[i2] == popularity[i].ServerType)
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
                        if (serverTypes[i2].ToString() == popularity[i].ServerType.ToString())
                        {
                            isInArray = true;
                            break;
                        }
                    }

                    if (isInArray)
                    {
                        continue;
                    }

                    serverTypes.Insert(0, popularity[i].ServerType);
                }

                for (int i = 0; i < serverTypes.Count; i++)
                {
                    if (serverTypes[i].ToString() == "None")
                    {
                        serverTypes.RemoveAt(i);
                        break;
                    }
                }

                return serverTypes;
            }
        }

        /// <summary>
        /// get trade server types from system. In string array. Sorted by name
        /// </summary>
        public static List<string> ServersTypesStringSorted
        {
            get
            {
                List<ServerType> serverTypes = ServersTypes;

                List<string> result = new List<string>();

                for(int i = 0;i < serverTypes.Count;i++)
                {
                    result.Add(serverTypes[i].ToString());
                }

                result.Sort();

                return result;
            }
        }

        /// <summary>
        /// get data server types
        /// </summary>
        public static List<ServerType> ServersTypesToOsData
        {
            get
            {
                List<ServerType> serverTypes = new List<ServerType>();

                serverTypes.Add(ServerType.TInvest);
                serverTypes.Add(ServerType.XTSpot);
                serverTypes.Add(ServerType.Deribit);
                serverTypes.Add(ServerType.KuCoinSpot);
                serverTypes.Add(ServerType.Alor);
                serverTypes.Add(ServerType.Finam);
                serverTypes.Add(ServerType.FinamGrpc);
                serverTypes.Add(ServerType.MoexDataServer);
                serverTypes.Add(ServerType.MfdWeb);
                serverTypes.Add(ServerType.MoexAlgopack);
                serverTypes.Add(ServerType.MoexFixFastSpot);
                serverTypes.Add(ServerType.Binance);
                serverTypes.Add(ServerType.BinanceFutures);
                serverTypes.Add(ServerType.BingXFutures);
                serverTypes.Add(ServerType.BitMex);
                serverTypes.Add(ServerType.BitfinexSpot);
                serverTypes.Add(ServerType.BitfinexFutures);
                serverTypes.Add(ServerType.ExmoSpot);
                serverTypes.Add(ServerType.HTXFutures);
                serverTypes.Add(ServerType.HTXSwap);
                serverTypes.Add(ServerType.Bybit);
                serverTypes.Add(ServerType.Woo);
                serverTypes.Add(ServerType.BitGetSpot);
                serverTypes.Add(ServerType.BitGetFutures);
                serverTypes.Add(ServerType.TraderNet);
                serverTypes.Add(ServerType.KiteConnect);
                serverTypes.Add(ServerType.YahooFinance);
                serverTypes.Add(ServerType.Polygon);
                serverTypes.Add(ServerType.CoinExSpot);
                serverTypes.Add(ServerType.CoinExFutures);
                serverTypes.Add(ServerType.BinanceData);
                serverTypes.Add(ServerType.AscendexSpot);
                serverTypes.Add(ServerType.OKXData);
                serverTypes.Add(ServerType.BybitData);
                serverTypes.Add(ServerType.GateIoData);

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
        /// array of active servers type of IServer
        /// </summary>
        public static List<IServer> GetServers()
        {
            return _servers;
        }

        /// <summary>
        /// array of active servers type of AServer
        /// </summary>
        public static List<AServer> GetAServers()
        {
            return _servers != null
                ? _servers.OfType<AServer>().ToList()
                : new();
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
        /// array of active servers names
        /// </summary>
        public static List<string> ActiveServersUniqueNames
        {
            get
            {
                List<string> types = new List<string>();

                for (int i = 0; _servers != null && i < _servers.Count; i++)
                {
                    string name = _servers[i].ServerNameAndPrefix;

                    if(name.Split('_').Length == 3)
                    {
                        string shortName = name.Split("_")[0] + "_" + name.Split("_")[1];
                        name = shortName;
                    }

                    types.Add(name);
                }

                return types;
            }
        }

        private static string _serversArrayLocker = "_serversArrayLocker";

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
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// create server
        /// </summary>
        /// <param name="type"> server type / тип сервера </param>
        /// <param name="needLoadTicks"> shows whether upload ticks from storage. this is need for bots with QUIK or Plaza2 servers / нужно ли подгружать тики из хранилища. Актуально в режиме робота для серверов Квик, Плаза 2 </param>
        public static void CreateServer(ServerType type, bool needLoadTicks, int uniqueNum = 0)
        {
            try
            {
                IServer newServer = null;

                lock (_serversArrayLocker)
                {
                    if (_servers == null)
                    {
                        _servers = new List<IServer>();
                    }

                    TryLoadServerInstance(type);

                    for (int i = 0; i < _servers.Count; i++)
                    {
                        if (_servers[i].GetType().BaseType.Name.ToString() == "AServer")
                        {
                            AServer serverCurrent = (AServer)_servers[i];

                            if (serverCurrent.ServerType == type
                                && serverCurrent.ServerNum == uniqueNum)
                            {
                                return;
                            }

                        }
                        else if (_servers[i].ServerType == type)
                        {
                            return;
                        }
                    }

                    SaveMostPopularServers(type);

                    if (type == ServerType.GateIoData)
                    {
                        newServer = new GateIoDataServer();
                    }
                    if (type == ServerType.BybitData)
                    {
                        newServer = new BybitDataServer();
                    }
                    else if (type == ServerType.OKXData)
                    {
                        newServer = new OKXDataServer();
                    }
                    else if (type == ServerType.BinanceData)
                    {
                        newServer = new BinanceDataServer();
                    }
                    else if (type == ServerType.TelegramNews)
                    {
                        newServer = new TelegramNewsServer();
                    }
                    else if (type == ServerType.AExchange)
                    {
                        newServer = new AExchangeServer();
                    }
                    else if (type == ServerType.SmartLabNews)
                    {
                        newServer = new SmartLabNewsServer();
                    }
                    else if (type == ServerType.RSSNews)
                    {
                        newServer = new RSSNewsServer(uniqueNum);
                    }
                    else if (type == ServerType.MoexFixFastTwimeFutures)
                    {
                        newServer = new MoexFixFastTwimeFuturesServer();
                    }
                    else if (type == ServerType.Atp)
                    {
                        newServer = new AtpServer();
                    }
                    else if (type == ServerType.MoexFixFastCurrency)
                    {
                        newServer = new MoexFixFastCurrencyServer();
                    }
                    else if (type == ServerType.BingXSpot)
                    {
                        newServer = new BingXServerSpot(uniqueNum);
                    }
                    else if (type == ServerType.MoexAlgopack)
                    {
                        newServer = new MoexAlgopackServer();
                    }
                    else if (type == ServerType.MoexFixFastSpot)
                    {
                        newServer = new MoexFixFastSpotServer();
                    }
                    else if (type == ServerType.XTSpot)
                    {
                        newServer = new XTServerSpot(uniqueNum);
                    }
                    else if (type == ServerType.BingXFutures)
                    {
                        newServer = new BingXServerFutures(uniqueNum);
                    }
                    else if (type == ServerType.KuCoinFutures)
                    {
                        newServer = new KuCoinFuturesServer(uniqueNum);
                    }
                    else if (type == ServerType.KuCoinSpot)
                    {
                        newServer = new KuCoinSpotServer(uniqueNum);
                    }
                    else if (type == ServerType.Alor)
                    {
                        newServer = new AlorServer(uniqueNum);
                    }
                    else if (type == ServerType.BitGetFutures)
                    {
                        newServer = new BitGetServerFutures(uniqueNum);
                    }
                    else if (type == ServerType.BitGetSpot)
                    {
                        newServer = new BitGetServerSpot(uniqueNum);
                    }
                    else if (type == ServerType.OKX)
                    {
                        newServer = new OkxServer(uniqueNum);
                    }
                    else if (type == ServerType.MfdWeb)
                    {
                        newServer = new MfdServer();
                    }
                    else if (type == ServerType.MoexDataServer)
                    {
                        newServer = new MoexDataServer();
                    }
                    else if (type == ServerType.TInvest)
                    {
                        newServer = new TInvestServer(uniqueNum);
                    }
                    else if (type == ServerType.GateIoSpot)
                    {
                        newServer = new GateIoServerSpot(uniqueNum);
                    }
                    else if (type == ServerType.GateIoFutures)
                    {
                        newServer = new GateIoServerFutures(uniqueNum);
                    }
                    else if (type == ServerType.Bybit)
                    {
                        newServer = new BybitServer(uniqueNum);
                    }
                    else if (type == ServerType.ExmoSpot)
                    {
                        newServer = new ExmoSpotServer();
                    }
                    else if (type == ServerType.Transaq)
                    {
                        newServer = new TransaqServer();
                    }
                    else if (type == ServerType.BitfinexSpot)
                    {
                        newServer = new BitfinexSpotServer(uniqueNum);
                    }
                    else if (type == ServerType.BitfinexFutures)
                    {
                        newServer = new BitfinexFuturesServer(uniqueNum);
                    }
                    else if (type == ServerType.Binance)
                    {
                        newServer = new BinanceServerSpot(uniqueNum);
                    }
                    else if (type == ServerType.BinanceFutures)
                    {
                        newServer = new BinanceServerFutures(uniqueNum);
                    }
                    else if (type == ServerType.NinjaTrader)
                    {
                        newServer = new NinjaTraderServer();
                    }
                    else if (type == ServerType.BitMex)
                    {
                        newServer = new BitMexServer();
                    }
                    else if (type == ServerType.QuikLua)
                    {
                        newServer = new QuikLuaServer();
                    }
                    else if (type == ServerType.InteractiveBrokers)
                    {
                        newServer = new InteractiveBrokersServer();
                    }
                    else if (type == ServerType.Plaza)
                    {
                        newServer = new PlazaServer();
                    }
                    else if (type == ServerType.AstsBridge)
                    {
                        newServer = new AstsBridgeServer(needLoadTicks);
                    }
                    else if (type == ServerType.Tester)
                    {
                        newServer = new TesterServer();
                    }
                    else if (type == ServerType.Finam)
                    {
                        newServer = new FinamServer();
                    }
                    else if (type == ServerType.FinamGrpc)
                    {
                        newServer = new FinamGrpcServer(uniqueNum);
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
                        newServer = new HTXSpotServer(uniqueNum);
                    }
                    else if (type == ServerType.HTXFutures)
                    {
                        newServer = new HTXFuturesServer(uniqueNum);
                    }
                    else if (type == ServerType.HTXSwap)
                    {
                        newServer = new HTXSwapServer(uniqueNum);
                    }
                    else if (type == ServerType.BitMartSpot)
                    {
                        newServer = new BitMartSpotServer();
                    }
                    else if (type == ServerType.BitMartFutures)
                    {
                        newServer = new BitMartFuturesServer();
                    }
                    else if (type == ServerType.TraderNet)
                    {
                        newServer = new TraderNetServer();
                    }
                    else if (type == ServerType.MexcSpot)
                    {
                        newServer = new MexcSpotServer(uniqueNum);
                    }
                    else if (type == ServerType.KiteConnect)
                    {
                        newServer = new KiteConnectServer();
                    }
                    else if (type == ServerType.YahooFinance)
                    {
                        newServer = new YahooServer();
                    }
                    else if (type == ServerType.Polygon)
                    {
                        newServer = new PolygonServer();
                    }
                    else if (type == ServerType.CoinExSpot)
                    {
                        newServer = new CoinExServerSpot();
                    }
                    else if (type == ServerType.CoinExFutures)
                    {
                        newServer = new CoinExServerFutures();
                    }
                    else if (type == ServerType.BloFinFutures)
                    {
                        newServer = new BloFinFuturesServer(uniqueNum);
                    }
                    else if (type == ServerType.AscendexSpot)
                    {
                        newServer = new AscendexSpotServer(uniqueNum);
                    }

                    if (newServer == null)
                    {
                        return;
                    }

                    _servers.Add(newServer);
                }
               
                if (ServerCreateEvent != null)
                {
                    try
                    {
                        ServerCreateEvent(newServer);
                    }
                    catch (Exception ex)
                    {
                        SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                    }
                }

                SendNewLogMessage(OsLocalization.Market.Message3 + _servers[_servers.Count - 1].ServerNameAndPrefix, LogMessageType.System);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// delete server
        /// </summary>
        /// <param name="type"> server type </param>
        /// <param name="uniqueNum"> server number </param>
        public static void DeleteServer(ServerType type, int uniqueNum)
        {
            try
            {
                lock (_serversArrayLocker)
                {
                    for (int i = 0; i < _servers.Count; i++)
                    {
                        if (_servers[i].GetType().BaseType.Name.ToString() == "AServer")
                        {
                            AServer serverCurrent = (AServer)_servers[i];

                            if (serverCurrent.ServerType == type
                                && serverCurrent.ServerNum == uniqueNum)
                            {
                                if (uniqueNum < 1)
                                {
                                    // стандартный сервер под номером 0. Удалять нельзя
                                    // отключаем
                                    serverCurrent.StopServer();
                                    return;
                                }

                                serverCurrent.StopServer();
                                serverCurrent.Delete();

                                _servers.RemoveAt(i);

                                if (ServerDeleteEvent != null)
                                {
                                    try
                                    {
                                        ServerDeleteEvent(serverCurrent);
                                    }
                                    catch (Exception ex)
                                    {
                                        SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                                    }
                                }

                                SendNewLogMessage(OsLocalization.Market.Label245 + ": " + serverCurrent.ServerNameAndPrefix, LogMessageType.System);

                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// save the types of servers that are most often run by the user
        /// </summary>
        private static void SaveMostPopularServers(ServerType type)
        {
            List<ServerPop> servers = LoadMostPopularServersWithCount();

            bool isInArray = false;

            for (int i = 0; i < servers.Count; i++)
            {
                if (servers[i].ServerType == type)
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

                    for (int i = 0; i < servers.Count; i++)
                    {
                        bool isSaved = false;
                        for (int i2 = 0; i2 < alreadySaveServers.Count; i2++)
                        {
                            if (alreadySaveServers[i2] == servers[i].ServerType)
                            {
                                isSaved = true;
                                break;
                            }
                        }

                        if (isSaved)
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
                    while (reader.EndOfStream == false)
                    {
                        string res = reader.ReadLine();

                        if (res.Split('&').Length <= 1)
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

            if (servers.Count > 1)
            {

                for (int i = 0; i < servers.Count; i++)
                {
                    ServerPop curServ = servers[i];

                    for (int i2 = i; i2 < servers.Count; i2++)
                    {
                        if (servers[i2].CountOfCreation < curServ.CountOfCreation)
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

        /// <summary>
        /// server deleted
        /// </summary>
        public static event Action<IServer> ServerDeleteEvent;

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
        public static bool NeedToConnectAuto;

        private static string _startServerLocker = "startServerLocker";

        /// <summary>
        /// select a specific server type for auto connection
        /// </summary>
        public static void SetServerToAutoConnection(ServerType type, string serverName)
        {
            lock (_startServerLocker)
            {
                try
                {
                    bool isInArray = false;

                    if (string.IsNullOrEmpty(serverName))
                    {
                        serverName = type.ToString();
                    }

                    for (int i = 0; i < _needServerNames.Count; i++)
                    {
                        if (_needServerNames[i] == serverName)
                        {
                            isInArray = true;
                            break;
                        }
                    }
                    if (isInArray == false)
                    {
                        _needServerNames.Add(serverName);
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
                catch (Exception error)
                {
                    LogMessageEvent(error.ToString(), LogMessageType.Error);
                }
            }
        }

        /// <summary>
        /// selected bot servers for auto connection
        /// </summary>
        private static List<ServerType> _needServerTypes = new List<ServerType>();

        private static List<string> _needServerNames = new List<string>();

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

                    if (NeedToConnectAuto == false)
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
                catch (Exception ex)
                {
                    SendNewLogMessage(ex.ToString(), LogMessageType.Error);
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

                if (GetServers() == null
                    || GetServers().Find(server1 => server1.ServerType == type) == null)
                { // if we don't have our server, create a new one / если у нас нашего сервера нет - создаём его
                    CreateServer(type, true);
                }

                List<IServer> servers = GetServers();

                if (servers == null)
                { // something went wrong / что-то пошло не так
                    return;
                }

                for (int i = 0; i < servers.Count; i++)
                {
                    IServer currentServer = servers[i];

                    if (currentServer.ServerType != type)
                    {
                        continue;
                    }

                    bool isInArray = false;

                    for (int j = 0; j < _needServerNames.Count; j++)
                    {
                        if (_needServerNames[j] == currentServer.ServerNameAndPrefix)
                        {
                            isInArray = true;
                            break;
                        }
                    }

                    if (isInArray == false)
                    {
                        continue;
                    }

                    if (currentServer.ServerStatus != ServerConnectStatus.Connect)
                    {
                        currentServer.StartServer();
                    }
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
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
            for (int i = 0; i < _serversPermissions.Count; i++)
            {
                if (_serversPermissions[i] == null)
                {
                    continue;
                }
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

            lock (_serverPermissionGeterLocker)
            {
                for (int i = 0; i < _serversPermissions.Count; i++)
                {
                    if (_serversPermissions[i] == null)
                    {
                        continue;
                    }
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
                else if (type == ServerType.Atp)
                {
                    serverPermission = new AtpServerPermission();
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
                else if (type == ServerType.BitfinexSpot)
                {
                    serverPermission = new BitfinexSpotServerPermission();
                }
                else if (type == ServerType.BitfinexFutures)
                {
                    serverPermission = new BitfinexFuturesServerPermission();
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
                else if (type == ServerType.FinamGrpc)
                {
                    serverPermission = new FinamGrpcServerPermission();
                }
                else if (type == ServerType.TInvest)
                {
                    serverPermission = new TInvestServerPermission();
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
                else if (type == ServerType.BitMartSpot)
                {
                    serverPermission = new BitMartSpotServerPermission();
                }
                else if (type == ServerType.BitMartFutures)
                {
                    serverPermission = new BitMartFuturesServerPermission();
                }
                else if (type == ServerType.MoexFixFastCurrency)
                {
                    serverPermission = new MoexFixFastCurrencyServerPermission();
                }
                else if (type == ServerType.MoexFixFastTwimeFutures)
                {
                    serverPermission = new MoexFixFastTwimeFuturesServerPermission();
                }
                else if (type == ServerType.BitMex)
                {
                    serverPermission = new BitMexServerPermission();
                }
                else if (type == ServerType.TraderNet)
                {
                    serverPermission = new TraderNetServerPermission();
                }
                else if (type == ServerType.MexcSpot)
                {
                    serverPermission = new MexcSpotServerPermission();
                }
                else if (type == ServerType.KiteConnect)
                {
                    serverPermission = new KiteConnectServerPermission();
                }
                else if (type == ServerType.YahooFinance)
                {
                    serverPermission = new YahooServerPermission();
                }
                else if (type == ServerType.Polygon)
                {
                    serverPermission = new PolygonServerPermission();
                }
                else if (type == ServerType.CoinExSpot)
                {
                    serverPermission = new CoinExServerSpotPermission();
                }
                else if (type == ServerType.CoinExFutures)
                {
                    serverPermission = new CoinExServerFuturesPermission();
                }
                else if (type == ServerType.RSSNews)
                {
                    serverPermission = new RSSNewsServerPermission();
                }
                else if (type == ServerType.SmartLabNews)
                {
                    serverPermission = new SmartLabNewsServerPermission();
                }
                else if (type == ServerType.AExchange)
                {
                    serverPermission = new AExchangeServerPermission();
                }
                else if (type == ServerType.BloFinFutures)
                {
                    serverPermission = new BloFinFuturesServerPermission();
                }
                else if (type == ServerType.TelegramNews)
                {
                    serverPermission = new TelegramNewsServerPermission();
                }
                else if (type == ServerType.BinanceData)
                {
                    serverPermission = new BinanceDataServerPermission();
                }
                else if (type == ServerType.AscendexSpot)
                {
                    serverPermission = new AscendexSpotServerPermission();
                }
                else if (type == ServerType.OKXData)
                {
                    serverPermission = new OKXDataServerPermission();
                }
                else if (type == ServerType.ExmoSpot)
                {
                    serverPermission = new ExmoSpotServerPermission();
                }
                else if (type == ServerType.BybitData)
                {
                    serverPermission = new BybitDataServerPermission();
                }
                else if (type == ServerType.GateIoData)
                {
                    serverPermission = new GateIoDataServerPermission();
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

        #region Proxy 

        private static ProxyMaster _proxyMaster;

        public static void ActivateProxy()
        {
            if (_proxyMaster == null)
            {
                _proxyMaster = new ProxyMaster();
                _proxyMaster.LogMessageEvent += SendNewLogMessage;
                _proxyMaster.Activate();
            }
        }

        public static WebProxy GetProxyAutoRegime(ServerType serverType, string serverName)
        {
            try
            {
                return _proxyMaster.GetProxyAutoRegime(serverType, serverName);
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        public static WebProxy GetProxyManualRegime(string userValue)
        {
            try
            {
                return _proxyMaster.GetProxyManualRegime(userValue);
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        public static void ShowProxyDialog()
        {
            try
            {
                _proxyMaster.ShowDialog();
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        public static List<ProxyOsa> GetAllProxies()
        {
            try
            {
                return _proxyMaster.Proxies;
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(),LogMessageType.Error);
                return null;
            }
        }

        public static bool AddNewProxy(
            bool isOn, string ip, int port, 
            string login, string password, 
            string pingWebAddress)
        {
            try
            {
                for (int i = 0; i < _proxyMaster.Proxies.Count; i++)
                {
                    ProxyOsa proxyCurrent = _proxyMaster.Proxies[i];

                    if (proxyCurrent.Ip == ip
                        && proxyCurrent.Port == port
                        && proxyCurrent.Login == login
                        && proxyCurrent.UserPassword == password)
                    {
                        return false;
                    }
                }

                ProxyOsa newProxy = _proxyMaster.CreateNewProxy();
                newProxy.IsOn = isOn;
                newProxy.Ip = ip;
                newProxy.Port = port;
                newProxy.Login = login;
                newProxy.UserPassword = password;
                newProxy.PingWebAddress = pingWebAddress;

                _proxyMaster.SaveProxy();

                return true;
            }
            catch(Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
            return false;
        }

        public static bool RemoveProxy(int number)
        {
            try
            {
                int proxiesCount = _proxyMaster.Proxies.Count;

                _proxyMaster.RemoveProxy(number);
                

                if(proxiesCount != _proxyMaster.Proxies.Count)
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
            return false;
        }

        public static ProxyOsa GetOneProxyAt(int number)
        {
            try
            {
                List<ProxyOsa> proxies = _proxyMaster.Proxies;

                for(int i = 0;i < proxies.Count;i++)
                {
                    if(proxies[i].Number == number)
                    {
                        return proxies[i];
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        public static ProxyOsa UpdateStatusProxyAt(int number)
        {
            try
            {
                List<ProxyOsa> proxies = _proxyMaster.Proxies;

                ProxyOsa proxy = null;

                for (int i = 0; i < proxies.Count; i++)
                {
                    if (proxies[i].Number == number)
                    {
                        proxy = proxies[i];
                    }
                }

                if(proxy != null)
                {
                    _proxyMaster.PingProxy(proxy);
                    return proxy;
                }

                return null;
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                return null;
            }
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

        public static ServerMasterOrdersPainter _ordersStorage;

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
        public static void SetHostTable(WindowsFormsHost hostPortfolio, 
            WindowsFormsHost hostActiveOrders, 
            WindowsFormsHost hostHistoricalOrders, 
            StartUiToPainter startUi,
            ComboBox comboBoxActiveOrders,
            Button buttonLeftActiveOrders,
            Button buttonRightActiveOrders,
            ComboBox comboBoxHistoryOrders,
            Button buttonLeftHistoryOrders,
            Button buttonRightHistoryOrders
            )
        {
            if (_painterPortfolios == null)
            {
                _painterPortfolios = new ServerMasterPortfoliosPainter();
                _painterPortfolios.LogMessageEvent += SendNewLogMessage;
                _painterPortfolios.ClearPositionOnBoardEvent += _painterPortfolios_ClearPositionOnBoardEvent;
                _painterPortfolios.SetHostTable(hostPortfolio);
            }

            if (_ordersStorage == null)
            {
                _ordersStorage = new ServerMasterOrdersPainter();
                _ordersStorage.LogMessageEvent += SendNewLogMessage;
                _ordersStorage.SetHostTable(hostActiveOrders, hostHistoricalOrders, startUi,
                comboBoxActiveOrders,buttonLeftActiveOrders,buttonRightActiveOrders,
                comboBoxHistoryOrders,buttonLeftHistoryOrders, buttonRightHistoryOrders);
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
            try
            {
                if (ClearPositionOnBoardEvent != null)
                {
                    ClearPositionOnBoardEvent(sec, server, fullName);
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(),LogMessageType.Error);
            }
        }

        private static void _ordersStorage_RevokeOrderToEmulatorEvent(Order order)
        {
            try
            {
                if (RevokeOrderToEmulatorEvent != null)
                {
                    RevokeOrderToEmulatorEvent(order);
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        public static event Action<Order> RevokeOrderToEmulatorEvent;

        public static event Action<string, IServer, string> ClearPositionOnBoardEvent;

        #endregion

        #region Auto Follow. CopyMaster

        private static CopyMaster _copyMaster;

        public static void SaveCopyMaster()
        {
            _copyMaster.SaveCopyTraders();
        }

        public static void ActivateCopyMaster()
        {
            if (_copyMaster == null)
            {
                _copyMaster = new CopyMaster();
                _copyMaster.LogMessageEvent += SendNewLogMessage;
                _copyMaster.Activate();
            }
        }

        public static void ShowCopyMasterDialog()
        {
            try
            {
                _copyMaster.ShowDialog();
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        public static void ShowApiDialog()
        {
            if(ShowApiDialogEvent != null)
            {
                ShowApiDialogEvent();
            }
        }

        public static void ShowClientManagerDialog()
        {
            if (ShowClientManagerDialogEvent != null)
            {
                ShowClientManagerDialogEvent();
            }
        }

        public static event Action ShowApiDialogEvent;

        public static event Action ShowClientManagerDialogEvent;

        #endregion

        #region Robots in bot station access

        public static List<BotPanel> GetAllBotsFromBotStation()
        {
            try
            {
                if(OsTraderMaster.Master == null)
                {
                    return null;
                }

                List<BotPanel> result = OsTraderMaster.Master.PanelsArray;

                return result;
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }

            return null;
        }

        #endregion

        #region Log

        /// <summary>
        /// enable object logging
        /// </summary>
        private static void ActivateLogging()
        {
            if (Log == null)
            {
                Log = new Log("ServerMaster", StartProgram.IsOsTrader);
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
            { // if nobody is subscribed to us and there is a log error
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
        /// connection to Russian broker T-Invest
        /// подключение к Т-Инвестициям (версия 3 коннектора)
        /// </summary>
        TInvest,

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
        /// transaq
        /// транзак
        /// </summary>
        Transaq,

        /// <summary>
        /// cryptocurrency exchange BitfinexSpot
        /// биржа криптовалют BitfinexSpot
        /// </summary>
        BitfinexSpot,

        /// <summary>
        /// cryptocurrency exchange BitfinexFutures
        /// биржа криптовалют BitfinexFutures
        /// </summary>
        BitfinexFutures,

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
        ExmoSpot,

        /// <summary>
        /// terminal Ninja Trader
        /// нинзя трейдер
        /// </summary>
        NinjaTrader,

        /// <summary>
        /// cryptocurrency exchange BitMEX
        /// биржа криптовалют BitMEX
        /// </summary>
        BitMex,

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
        /// Finam gRPC
        /// Финам gRPC
        /// </summary>
        FinamGrpc,

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

        /// BitMartSpot Spot exchange
        /// </summary>
        BitMartSpot,

        /// BitMartSpot Futures exchange
        /// </summary>
        BitMartFutures,

        /// <summary>
        /// FIX/FAST for MOEX Currency
        /// </summary>
        MoexFixFastCurrency,

        /// <summary>
        /// FIX/FAST/TWIME for MOEX Futures
        /// </summary>
        MoexFixFastTwimeFutures,

        /// <summary>
        /// TraderNet
        /// </summary>
        TraderNet,

        /// <summary>
        /// Mexc Spot
        /// </summary>
        MexcSpot,

        /// <summary>
        /// Mexc Spot
        /// </summary>
        KiteConnect,

        /// <summary>
        /// Yahoo Finance
        /// </summary>
        YahooFinance,

        /// <summary>
        /// ATPlatform
        /// </summary>
        Atp,

        /// <summary>
        /// Polygon.io
        /// </summary>
        Polygon,

        /// <summary>
        /// Spot for cryptocurrency exchange CoinEx.com
        /// Спот биржи криптовалют CoinEx.com
        /// </summary>
        CoinExSpot,

        /// <summary>
        /// Futures for cryptocurrency exchange CoinEx.com
        /// Фьюючерсы биржи криптовалют CoinEx.com
        /// </summary>
        CoinExFutures,

        /// <summary>
        /// Reading news from RSS feeds
        /// Чтение новостей с RSS лент
        /// </summary>
        RSSNews,

        /// <summary>
        /// Reading news from smart-lab.ru
        /// Чтение новостей с сайта smart-lab.ru
        /// </summary>
        SmartLabNews,

        /// <summary>
        /// Options exchange Alternative Exchange ae.exchange
        /// Опционная биржа Alternative Exchange ae.exchange
        /// </summary>
        AExchange,

        /// <summary>
        /// BloFinFutures exchange
        /// </summary>
        BloFinFutures,

        /// <summary>
        /// Reading news from Telegram channels
        /// Чтение новостей с Telegram каналов
        /// </summary>
        TelegramNews,

        /// <summary>
        /// downloading historical data from exchange Binance
        /// скачивание исторических данных с биржи Binance
        /// </summary>
        BinanceData,

        /// <summary>
        ///  AscendexSpot exchange
        /// </summary>
        AscendexSpot,

        /// <summary>
        /// downloading historical data from exchange OKX
        /// скачивание исторических данных с биржи OKX
        /// </summary>
        OKXData,

        /// <summary>
        /// downloading historical data from exchange Bybit
        /// скачивание исторических данных с биржи Bybit
        /// </summary>
        BybitData,

        /// <summary>
        /// downloading historical data from exchange GateIo
        /// скачивание исторических данных с биржи GateIo
        /// </summary>
        GateIoData
    }
}
