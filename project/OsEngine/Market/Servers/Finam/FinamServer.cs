/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.Finam.Entity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using Action = System.Action;

namespace OsEngine.Market.Servers.Finam
{

    public class FinamServer : AServer
    {
        public FinamServer()
        {
            FinamServerRealization realization = new FinamServerRealization();
            ServerRealization = realization;
            NeedToHideParameters = true;
        }
    }

    public class FinamServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public FinamServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            if (!Directory.Exists(@"Data\Temp\FinamTempFiles\"))
            {
                Directory.CreateDirectory(@"Data\Temp\FinamTempFiles\");
            }
        }

        public void Connect(WebProxy proxy)
        {
            HttpResponseMessage response = CheckFinamServer();

            if (response.StatusCode == HttpStatusCode.OK)
            {
                ServerStatus = ServerConnectStatus.Connect;
                ConnectEvent();
            }
            else
            {
                SendLogMessage($"Connect server error: {response.StatusCode}", LogMessageType.Error);
            }

            response.Dispose();
        }

        public void Dispose()
        {
            string[] files = Directory.GetFiles(@"Data\Temp\FinamTempFiles\");

            if (files.Length > 0)
            {
                for (int i = 0; i < files.Length; i++)
                {
                    File.Delete(files[i]);
                }
            }

            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        private HttpResponseMessage CheckFinamServer()
        {
            string url = "https://www.finam.ru/profile/moex-akcii/sberbank/export/old/";

            HttpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36");

            try
            {
                HttpResponseMessage response = HttpClient.GetAsync(url).Result;

                return response;

            }
            catch (Exception ex)
            {
                SendLogMessage($"Connect server error: {ex.Message}", LogMessageType.Error);
                return null;
            }
        }

        public ServerType ServerType
        {
            get { return ServerType.Finam; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

        #endregion

        #region 2 Properties

        public HttpClient HttpClient { get; set; } = new HttpClient();

        public DateTime ServerTime { get; set; }

        public List<IServerParameter> ServerParameters { get; set; }

        #endregion

        #region 3 Securities

        List<Security> _securities = new List<Security>();

        private List<FinamSecurity> _finamSecurities;

        private Dictionary<string, string> _rusIndexNames = new Dictionary<string, string>()
         {
             {"420450", "Индекс МосБиржи"},
             {"420453", "Индекс МосБиржи широкого рынка"},
             {"420446", "Индекс МосБиржи голубых фишек"},
             {"420451", "Индекс МосБиржи 10"},
             {"420486", "Индекс МосБиржи инноваций"},
             {"420445", "Индекс РТС"},
             {"420470", "Индекс РТС металлов и добычи"},
             {"420471", "Индекс РТС нефти и газа"},
             {"420466", "Индекс РТС потреб. сектора"},
             {"420472", "Индекс РТС телекоммуникаций"},
             {"420473", "Индекс РТС транспорта"},
             {"420468", "Индекс РТС финансов"},
             {"420465", "Индекс РТС химии и нефтехимии"},
             {"420474", "Индекс РТС широкого рынка"},
             {"420467", "Индекс РТС электроэнергетики"},
             {"420478", "Индекс гос обл RGBI"},
             {"420479", "Индекс гос обл RGBI TR"},
             {"420457", "Индекс металлов и добычи"},
             {"420458", "Индекс нефти и газа"},
             {"420454", "Индекс потребит сектора"},
             {"420461", "Индекс телекоммуникаций"},
             {"420475", "Индекс транспорта"},
             {"420456", "Индекс финансов"},
             {"420455", "Индекс химии и нефтехимии"},
             {"420459", "Индекс электроэнергетики"}
         };

        public void GetSecurities()
        {
            SendLogMessage("Downloading the list of securities...", LogMessageType.System);

            string response = GetSecFromFile();

            string[] arra = response.Split('=');
            string[] arr = arra[1].Split('[')[1].Split(']')[0].Split(',');

            string[] arraySets = response.Split('=');
            string[] arrayIds = arraySets[1].Split('[')[1].Split(']')[0].Split(',');

            for (int i = 0; i < arrayIds.Length; i++)
            {
                arrayIds[i] = arrayIds[i].Replace(" ", "");
            }

            string names = arraySets[2].Split('[')[1].Split(']')[0];

            List<string> arrayNames = new List<string>();

            string name = "";

            for (int i = 1; i < names.Length; i++)
            {
                if ((names[i] == '\'' && i + 1 == names.Length)
                    || i + 1 == names.Length
                    ||
                    (names[i] == '\'' && names[i + 1] == ',' && names[i + 3] == '\''))
                {
                    arrayNames.Add(name);
                    name = "";
                    i += 3;
                }
                else
                {
                    name += names[i];
                }
            }
            string[] arrayCodes = arraySets[3].Split('[')[1].Split(']')[0].Split(',');
            arrayCodes[0] = arrayCodes[0].Substring(1, arrayCodes[0].Length - 1);
            for (int i = 1; i < arrayCodes.Length; i++)
            {
                arrayCodes[i] = arrayCodes[i].Substring(2, arrayCodes[i].Length - 2);
            }

            string[] arrayMarkets = arraySets[4].Split('[')[1].Split(']')[0].Split(',');

            for (int i = 1; i < arrayMarkets.Length; i++)
            {
                arrayMarkets[i] = arrayMarkets[i].Substring(1, arrayMarkets[i].Length - 1);
            }

            string[] arrayDecp = arraySets[5].Split('{')[1].Split('}')[0].Split(',');

            for (int i = 0; i < arrayDecp.Length; i++)
            {
                arrayDecp[i] = arrayDecp[i].Replace(" ", "");
            }

            string[] arrayFormatStrs = arraySets[6].Split('[')[1].Split(']')[0].Split(',');
            string[] arrayEmitentChild = arraySets[7].Split('[')[1].Split(']')[0].Split(',');

            for (int i = 0; i < arrayEmitentChild.Length; i++)
            {
                arrayEmitentChild[i] = arrayEmitentChild[i].Replace(" ", "");
            }

            string[] arrayEmitentUrls = arraySets[8].Split('{')[1].Split('}')[0].Split(',');

            string[] unavailableSecurities = arraySets[9].Split(':');

            HashSet<string> unavailableSecHashSet = new HashSet<string>(unavailableSecurities);

            _finamSecurities = new List<FinamSecurity>();

            for (int i = 0; i < arrayIds.Length; i++)
            {
                if (unavailableSecHashSet.Contains(arrayIds[i]))
                {
                    continue;
                }
                string url = arrayEmitentUrls[i].Split(':')[1];

                if (url.Contains("-smal")
                    || url.Contains("-fqbr")
                    || url.Contains("-tqbd"))
                {
                    continue;
                }

                FinamSecurity finamSecurity = new FinamSecurity();

                finamSecurity.Code = arrayCodes[i].TrimStart('\'').TrimEnd('\'');
                finamSecurity.Decp = arrayDecp[i].Split(':')[1];
                finamSecurity.EmitentChild = arrayEmitentChild[i];
                finamSecurity.Id = arrayIds[i];
                finamSecurity.Name = arrayNames[i];
                finamSecurity.Url = url;

                finamSecurity.MarketId = arrayMarkets[i];

                if (finamSecurity.MarketId == "7")
                {
                    finamSecurity.Name =
                        finamSecurity.Name.Replace("*", "")
                            .Replace("-", "")
                            .Replace("_", "")
                            .ToUpper();

                    finamSecurity.Code = finamSecurity.Name;

                    if (finamSecurity.Name == "MINI D&JFUT")
                    {
                        finamSecurity.Code = "DANDI.MINIFUT";
                    }

                }

                if (Convert.ToInt32(arrayMarkets[i]) == 200)
                {
                    finamSecurity.Market = "МосБиржа топ";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 1)
                {
                    finamSecurity.Market = "МосБиржа акции";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 14)
                {
                    finamSecurity.Market = "МосБиржа фьючерсы";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 41)
                {
                    finamSecurity.Market = "Курс рубля";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 45)
                {
                    finamSecurity.Market = "МосБиржа валютный рынок";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 2)
                {
                    finamSecurity.Market = "МосБиржа облигации";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 12)
                {
                    finamSecurity.Market = "МосБиржа внесписочные облигации";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 29)
                {
                    finamSecurity.Market = "МосБиржа пифы";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 515)
                {
                    finamSecurity.Market = "Мосбиржа ETF";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 8)
                {
                    finamSecurity.Market = "Расписки";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 519)
                {
                    finamSecurity.Market = "Еврооблигации";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 517)
                {
                    finamSecurity.Market = "Санкт-Петербургская биржа";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 6)
                {
                    if (finamSecurity.Name == "Индекс МосБиржи, вечер. сессия")
                    {
                        finamSecurity.Code = "RI." + finamSecurity.Code;
                        finamSecurity.Market = "Индексы Россия";
                        finamSecurity.MarketId = "14";
                        finamSecurity.Id = "1900253";
                    }
                    else
                    {
                        finamSecurity.Market = "Индексы мировые";
                    }
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 24)
                {
                    finamSecurity.Market = "Товары";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 5)
                {
                    finamSecurity.Market = "Мировые валюты";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 25)
                {
                    finamSecurity.Market = "Акции США(BATS)";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 7)
                {
                    finamSecurity.Market = "Фьючерсы США";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 27)
                {
                    finamSecurity.Market = "Отрасли экономики США";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 26)
                {
                    finamSecurity.Market = "Гособлигации США";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 28)
                {
                    finamSecurity.Market = "ETF";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 30)
                {
                    finamSecurity.Market = "Индексы мировой экономики";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 91)
                {
                    if (_rusIndexNames.TryGetValue(finamSecurity.Id, out string fullName))
                    {
                        finamSecurity.Name = fullName;
                        finamSecurity.Market = "Индексы Россия";
                    }
                    else
                    {
                        finamSecurity.Market = "Российские индексы";
                    }
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 3)
                {
                    finamSecurity.Market = "РТС";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 20)
                {
                    finamSecurity.Market = "RTS Board";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 10)
                {
                    finamSecurity.Market = "РТС-GAZ";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 17)
                {
                    finamSecurity.Market = "ФОРТС Архив";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 31)
                {
                    finamSecurity.Market = "Сырье Архив";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 38)
                {
                    finamSecurity.Market = "RTS Standard Архив";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 16)
                {
                    finamSecurity.Market = "ММВБ Архив";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 18)
                {
                    finamSecurity.Market = "РТС Архив";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 9)
                {
                    finamSecurity.Market = "СПФБ Архив";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 32)
                {
                    finamSecurity.Market = "РТС-BOARD Архив";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 39)
                {
                    finamSecurity.Market = "Расписки Архив";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == -1)
                {
                    finamSecurity.Market = "Отрасли";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 520)
                {
                    finamSecurity.Market = "Криптовалюты";
                }

                if (finamSecurity.Market == null)
                {
                    continue;
                }

                bool isInArray = false;


                for (int j = 0; j < _finamSecurities.Count; j++)
                {
                    FinamSecurity secInArray = _finamSecurities[j];

                    if (secInArray.Id == finamSecurity.Id
                        && secInArray.Market == finamSecurity.Market)
                    {
                        isInArray = true;
                        break;
                    }

                    if (secInArray.Code == finamSecurity.Code
                       && secInArray.Market == finamSecurity.Market)
                    {
                        isInArray = true;
                        break;
                    }
                }

                if (isInArray == false)
                {
                    _finamSecurities.Add(finamSecurity);
                }
            }

            _securities = new List<Security>();

            for (int i = 0; i < _finamSecurities.Count; i++)
            {
                if (_finamSecurities[i].Name == "")
                {
                    continue;
                }

                Security sec = new Security();
                sec.NameFull = _finamSecurities[i].Code;
                sec.Name = _finamSecurities[i].Name;
                sec.NameId = _finamSecurities[i].Id;
                sec.NameClass = _finamSecurities[i].Market;
                sec.PriceStep = 0;
                sec.PriceStepCost = 0;

                _securities.Add(sec);
            }

            if (_securities.Count > 0)
            {
                SendLogMessage($"{_securities.Count} securities loaded", LogMessageType.System);
            }

            SecurityEvent(_securities);
        }

        private static string GetSecFromFile()
        {
            if (!File.Exists(@"FinamSecurities.txt"))
            {
                return "";
            }

            // Как обновить данные по бумагам
            // 1. Идём на сайт Финам: https://www.finam.ru/profile/moex-akcii/sberbank/export/old/
            // 2. Заходим в источники страницы, через инструменты разработчика
            // 3. В кэше находим файл icharts.js
            // 4. Копируем содержимое этого файла в текстовик FinamSecurities.txt, который рядом с exe файлом OsEngine до строки, которая начинается с НЕТ НА СЕРВЕРЕ

            string result = "";

            try
            {
                using (StreamReader reader = new StreamReader(@"FinamSecurities.txt"))
                {
                    result = reader.ReadToEnd();
                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }

            if (result != null)
            {
                result = result.Replace("\n", "");
                result = result.Replace("\r", "");
            }

            return result;
        }

        public event Action<List<Security>> SecurityEvent;
        #endregion

        #region 4 Portfolios

        private List<Portfolio> _myPortfolios = new List<Portfolio>();

        public void GetPortfolios()
        {
            Portfolio newPortfolio = new Portfolio();
            newPortfolio.Number = "Finam Virtual Portfolio";
            newPortfolio.ValueCurrent = 1;
            _myPortfolios.Add(newPortfolio);

            if (_myPortfolios.Count != 0)
            {
                PortfolioEvent(_myPortfolios);
            }
        }

        public event Action<List<Portfolio>> PortfolioEvent;
        #endregion

        #region 5 Data

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            try
            {
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    return null;
                }

                FinamDataSeries finamDataSeries = new FinamDataSeries(this);

                finamDataSeries.TimeActual = actualTime;
                finamDataSeries.Security = security;
                finamDataSeries.SecurityFinam = _finamSecurities.Find(s => s.Id == security.NameId);
                finamDataSeries.Candles = new List<Candle>();
                finamDataSeries.TimeEnd = endTime;
                finamDataSeries.TimeStart = startTime;
                finamDataSeries.TimeFrame = timeFrameBuilder.TimeFrame;

                finamDataSeries.Process();

                if (finamDataSeries.Candles != null)
                {
                    return finamDataSeries.Candles;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception error)
            {
                SendLogMessage($"Candles data downloading error: {error}", LogMessageType.Error);
                return null;
            }
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            try
            {
                if (ServerStatus != ServerConnectStatus.Connect)
                {

                    return null;
                }

                FinamDataSeries finamDataSeries = new FinamDataSeries(this);

                finamDataSeries.TimeActual = actualTime;
                finamDataSeries.Security = security;
                finamDataSeries.SecurityFinam = _finamSecurities.Find(s => s.Id == security.NameId);
                finamDataSeries.TimeEnd = endTime;
                finamDataSeries.TimeStart = startTime;
                finamDataSeries.IsTick = true;

                finamDataSeries.Process();

                if (finamDataSeries.Trades != null)
                {
                    return finamDataSeries.Trades;
                }
                else
                {
                    return null;
                }

            }
            catch (Exception error)
            {
                SendLogMessage($"Trades data downloading error: {error}", LogMessageType.Error);
                return null;
            }
        }

        #endregion

        #region 6 Log

        public void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent(message, messageType);
        }

        public event Action<string, LogMessageType> LogMessageEvent;
        #endregion

        #region 7 Unused methods

        public void Subscribe(Security security) { }

        public void SendOrder(Order order) { }

        public void CancelAllOrders() { }

        public void CancelAllOrdersToSecurity(Security security) { }

        public bool CancelOrder(Order order) { return false; }

        public void ChangeOrderPrice(Order order, decimal newPrice) { }

        public void GetAllActivOrders() { }

        public OrderStateType GetOrderStatus(Order order) { return OrderStateType.None; }

        public bool SubscribeNews() { return false; }

        public List<Order> GetActiveOrders(int startIndex, int count) { return null; }

        public List<Order> GetHistoricalOrders(int startIndex, int count) { return null; }

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount) { return null; }

        public event Action<News> NewsEvent { add { } remove { } }
        public event Action<MarketDepth> MarketDepthEvent { add { } remove { } }
        public event Action<Trade> NewTradesEvent { add { } remove { } }
        public event Action<Order> MyOrderEvent { add { } remove { } }
        public event Action<MyTrade> MyTradeEvent { add { } remove { } }
        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }
        public event Action<Funding> FundingUpdateEvent { add { } remove { } }
        public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }

        #endregion

    }
}