using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Net;
using System.Windows;

namespace OsEngine.Market.Servers.MOEX
{
    public class MoexDataServer : AServer
    {
        public MoexDataServer()
        {
            MoexDataServerRealization realization = new MoexDataServerRealization();
            ServerRealization = realization;
            NeedToHideParameters = true;
        }
    }

    public class MoexDataServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public MoexDataServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        public ServerType ServerType
        {
            get
            {
                return ServerType.MoexDataServer;
            }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public List<IServerParameter> ServerParameters { get; set; }

        public DateTime ServerTime { get; set; }

        public void Connect(WebProxy proxy)
        {
            string result = GetRequest("http://iss.moex.com/iss/engines/");

            if (result == null)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
            }
            else
            {
                ServerStatus = ServerConnectStatus.Connect;
                ConnectEvent?.Invoke();
            }
        }

        public void Dispose()
        {
            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

        #endregion

        #region 2 Securities

        public void GetSecurities()
        {
            List<Security> securities = new List<Security>();

            SendLogMessage("Securities downloading...", LogMessageType.System);

            List<string> engines = GetEngines();

            List<string> markets = GetMarkets(engines);

            List<string> classes = GetClasses(markets);

            securities = GetSecurities(classes);

            securities = CreateFuturesSection(securities);

            SecurityEvent?.Invoke(securities);

            SendLogMessage("Securities downloaded. Count: " + securities.Count, LogMessageType.System);
        }

        private List<Security> CreateFuturesSection(List<Security> securities)
        {
            List<Security> allFutures = new List<Security>();

            for (int i = 0; i < securities.Count; i++)
            {
                if (securities[i].NameClass.EndsWith("RFUD"))
                {
                    allFutures.Add(securities[i]);
                }
            }

            List<string> names = new List<string>();

            for (int i = 0; i < allFutures.Count; i++)
            {
                string name = allFutures[i].Name.Substring(0, allFutures[i].Name.Length - 2);

                bool isInArray = false;
                for (int i2 = 0; i2 < names.Count; i2++)
                {
                    if (names[i2] == name)
                    {
                        isInArray = true;
                        break;
                    }
                }

                if (isInArray)
                {
                    continue;
                }

                names.Add(name);
            }

            for (int i = 0; i < names.Count; i++)
            {
                securities.AddRange(GetFutNamesForTenYears(names[i], allFutures[0].NameId));
            }

            return securities;
        }

        private List<Security> GetFutNamesForTenYears(string futName, string idEnding)
        {
            string[] id = idEnding.Split('#');
            idEnding = id[1] + "#" + id[2] + "#" + id[3];

            List<Security> newSecurities = new List<Security>();

            for (int i = 0; i < 3; i++)
            {
                newSecurities.AddRange(GetFuturesForOneYear(futName, idEnding, DateTime.Now.Year - i - 2000));
            }

            return newSecurities;
        }

        private List<Security> GetFuturesForOneYear(string futName, string idEnding, int year)
        {
            int futEnd = Convert.ToInt32(year.ToString().Substring(1, 1));

            // TATN-9.20

            List<Security> sec = new List<Security>();

            if (DateTime.Now.Year == 2000 + year &&
                DateTime.Now.Month < 3)
            {
                return sec;
            }

            Security one = new Security();
            one.Name = futName + "H" + futEnd;
            one.NameId = one.Name + "#" + idEnding;
            one.NameClass = "Фьючерсы истёкшие#RFUD";
            one.NameFull = futName + "-3." + year;
            sec.Add(one);

            if (DateTime.Now.Year == 2000 + year &&
                DateTime.Now.Month < 6)
            {
                return sec;
            }

            Security two = new Security();
            two.Name = futName + "M" + futEnd;
            two.NameId = two.Name + "#" + idEnding;
            two.NameClass = "Фьючерсы истёкшие#RFUD";
            two.NameFull = futName + "-6." + year;
            sec.Add(two);

            if (DateTime.Now.Year == 2000 + year &&
                DateTime.Now.Month < 9)
            {
                return sec;
            }

            Security three = new Security();
            three.Name = futName + "U" + futEnd;
            three.NameId = three.Name + "#" + idEnding;
            three.NameClass = "Фьючерсы истёкшие#RFUD";
            three.NameFull = futName + "-9." + year;
            sec.Add(three);

            if (DateTime.Now.Year == 2000 + year &&
                DateTime.Now.Month < 12)
            {
                return sec;
            }

            Security four = new Security();
            four.Name = futName + "Z" + futEnd;
            four.NameId = four.Name + "#" + idEnding;
            four.NameClass = "Фьючерсы истёкшие#RFUD";
            four.NameFull = futName + "-12." + year;
            sec.Add(four);

            return sec;
        }

        private List<string> GetEngines()
        {
            // запрос типов площадок http://iss.moex.com/iss/engines.json

            string request = GetRequest("http://iss.moex.com/iss/engines.json");

            List<string> result = new List<string>();

            JObject json = JObject.Parse(request);

            JArray dataArray = (JArray)json["engines"]["data"];

            for (int i = 0; i < dataArray.Count; i++)
            {
                JArray innerArray = (JArray)dataArray[i];
                string str = innerArray[1].ToString();

                if (str == "state" ||
                    str == "interventions" ||
                    str == "offboard" ||
                    str == "commodity")
                {
                    continue;
                }

                result.Add(str);
            }

            return result;
        }

        private List<string> GetMarkets(List<string> engines)
        {
            List<string> requests = new List<string>();

            for (int i = 0; i < engines.Count; i++)
            {
                string str = "http://iss.moex.com/iss/engines/";
                str += engines[i];
                str += "/markets.json";
                requests.Add(str);
            }

            List<string> responses = new List<string>();

            for (int i = 0; i < requests.Count; i++)
            {
                string response = GetRequest(requests[i]);
                responses.Add(response);
            }

            List<string> result = new List<string>();

            for (int i = 0; i < responses.Count; i++)
            {
                JObject json = JObject.Parse(responses[i]);

                JArray dataArray = (JArray)json["markets"]["data"];

                for (int j = 0; j < dataArray.Count; j++)
                {
                    JArray innerArray = (JArray)dataArray[j];
                    string str = engines[i] + "#" + innerArray[1].ToString();
                    result.Add(str);
                }
            }

            return result;

            // запрос типов торгов по площадке http://iss.moex.com/iss/engines/stock/markets.json
        }

        private List<string> GetClasses(List<string> markets)
        {
            List<string> requests = new List<string>();

            for (int i = 0; i < markets.Count; i++)
            {
                // запрос классов бумаг по площадке и типу торгов http://iss.moex.com/iss/engines/stock/markets/shares/boards.json

                string str = "http://iss.moex.com/iss/engines/";
                str += markets[i].Split('#')[0];
                str += "/markets/";
                str += markets[i].Split('#')[1];
                str += "/boards.json";

                requests.Add(str);
            }

            List<string> responses = new List<string>();

            for (int i = 0; i < requests.Count; i++)
            {
                string response = GetRequest(requests[i]);
                responses.Add(response);
            }

            List<string> result = new List<string>();

            for (int i = 0; i < responses.Count; i++)
            {
                JObject json = JObject.Parse(responses[i]);

                JArray dataArray = (JArray)json["boards"]["data"];

                for (int j = 0; j < dataArray.Count; j++)
                {
                    JArray innerArray = (JArray)dataArray[j];

                    string code = innerArray[2].ToString();

                    if (code != "TQBR" && // акции
                       code != "RFUD" && // фьючерсы торгующиеся
                       code != "RTSI" &&// индексы RTS
                       code != "CETS" && // валютная секция
                       code != "SNDX" // Индексы фондового рынка
                       )
                    {
                        continue;
                    }

                    string fullName = "";

                    if (code == "TQBR")
                    {
                        fullName = "Акции";
                    }
                    if (code == "RFUD")
                    {
                        fullName = "Фьючерсы активные";
                    }
                    if (code == "RTSI")
                    {
                        fullName = "Индексы РТС";
                    }
                    if (code == "CETS")
                    {
                        fullName = "Валютная секция";
                    }
                    if (code == "SNDX")
                    {
                        fullName = "Индексы фондового рынка";
                    }

                    string str = markets[i] + "#" + innerArray[2].ToString() + "#" + fullName;

                    result.Add(str);
                }
            }

            return result;
        }

        private List<Security> GetSecurities(List<string> classes)
        {
            List<string> requests = new List<string>();

            for (int i = 0; i < classes.Count; i++)
            {
                // запрос всех бумаг по классу http://iss.moex.com/iss/engines/stock/markets/shares/boards/TQBR/securities.json
                string str = "http://iss.moex.com/iss/engines/";
                str += classes[i].Split('#')[0];
                str += "/markets/";
                str += classes[i].Split('#')[1];
                str += "/boards/";
                str += classes[i].Split('#')[2];
                str += "/securities.json";
                requests.Add(str);
            }

            List<string> responses = new List<string>();

            for (int i = 0; i < requests.Count; i++)
            {
                string response = GetRequest(requests[i]);
                responses.Add(response);
            }

            List<Security> result = new List<Security>();

            for (int i = 0; i < responses.Count; i++)
            {
                JObject json = JObject.Parse(responses[i]);

                JArray dataArray = (JArray)json["securities"]["data"];

                for (int j = 0; j < dataArray.Count; j++)
                {
                    JArray innerArray = (JArray)dataArray[j];

                    Security newSec = new Security();
                    newSec.Name = innerArray[0].ToString();
                    newSec.NameId = newSec.Name + "#" + classes[i];
                    newSec.NameClass = classes[i].Split('#')[3] + "#" + classes[i].Split('#')[2];
                    newSec.NameFull = innerArray[2].ToString();

                    result.Add(newSec);
                }
            }

            return result;
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 3 Data

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime,
            DateTime actualTime)
        {
            List<Candle> candles = new List<Candle>();

            int minutesInTf = Convert.ToInt32(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes);

            if (minutesInTf >= 1 &&
                minutesInTf < 10)
            {
                List<Candle> sourseCandle = GetAllCandles(security, startTime, 1, endTime);
                candles = ConcateCandles(sourseCandle, 1, minutesInTf);
            }
            else if (minutesInTf == 15 ||
                     minutesInTf == 45)
            {
                List<Candle> sourseCandle = GetAllCandles(security, startTime, 1, endTime);
                candles = ConcateCandles(sourseCandle, 1, minutesInTf);
            }
            else if (minutesInTf >= 10 &&
                     minutesInTf < 60)
            {
                List<Candle> sourseCandle = GetAllCandles(security, startTime, 10, endTime);
                candles = ConcateCandles(sourseCandle, 10, minutesInTf);
            }
            else if (minutesInTf >= 60)
            {
                List<Candle> sourseCandle = GetAllCandles(security, startTime, 60, endTime);
                candles = ConcateCandles(sourseCandle, 60, minutesInTf);
            }

            Candle previousCandle = new Candle();

            for (int i = 0; i < candles.Count; i++)
            {
                Candle newCandle = candles[i];

                if (newCandle.TimeStart.TimeOfDay == TimeSpan.Zero)
                {
                    candles.RemoveAt(i);
                    i--;
                    continue;
                }
                else if (previousCandle.TimeStart == DateTime.MinValue)
                {
                    previousCandle = newCandle;
                    continue;
                }
                else if (newCandle.TimeStart == previousCandle.TimeStart)
                {
                    candles.RemoveAt(i);
                    i--;
                    continue;
                }
                else
                {
                    previousCandle = newCandle;
                }
            }

            return candles;
        }

        private List<Candle> ConcateCandles(List<Candle> candlesOld, int startTf, int endTf)
        {
            if (startTf == endTf)
            {
                return candlesOld;
            }

            TimeSpan candleMinuteLen = new TimeSpan(0, endTf, 0);

            int countOldCandlesInOneNew = endTf / startTf;

            List<Candle> candlesNew = new List<Candle>();

            for (int i = 0; i < candlesOld.Count; i++)
            {
                Candle newCandle = new Candle();
                newCandle.TimeStart = candlesOld[i].TimeStart;

                if (startTf == 1
                    && (endTf == 5 || endTf == 10 || endTf == 15))
                {
                    while (newCandle.TimeStart.Minute != 0
                        && newCandle.TimeStart.Minute % endTf != 0)
                    {
                        newCandle.TimeStart = newCandle.TimeStart.AddMinutes(1);
                    }
                }
                if (startTf == 10
                    && (endTf == 30))
                {
                    while (newCandle.TimeStart.Minute != 0
                        && newCandle.TimeStart.Minute != 30)
                    {
                        newCandle.TimeStart = newCandle.TimeStart.AddMinutes(1);
                    }
                }

                if (startTf == 1
                    && (endTf == 45))
                {
                    while (newCandle.TimeStart.Minute != 0
                        && newCandle.TimeStart.Minute % 5 != 0)
                    {
                        newCandle.TimeStart = newCandle.TimeStart.AddMinutes(1);
                    }
                }

                newCandle.Open = candlesOld[i].Open;
                newCandle.High = candlesOld[i].High;
                newCandle.Low = candlesOld[i].Low;
                newCandle.Close = candlesOld[i].Close;
                newCandle.Volume += candlesOld[i].Volume;
                newCandle.State = CandleState.Finished;
                i++;

                for (int i2 = 0; i2 < countOldCandlesInOneNew - 1 && i < candlesOld.Count; i2++)
                {
                    if (newCandle.TimeStart.Hour != 10 &&
                        newCandle.TimeStart.Minute != 0 &&
                        candlesOld[i].TimeStart.Hour == 10 &&
                        candlesOld[i].TimeStart.Minute == 0)
                    {
                        i--;
                        break;
                    }
                    if (newCandle.TimeStart.Day != candlesOld[i].TimeStart.Day)
                    {
                        i--;
                        break;
                    }

                    DateTime EndCandleTime = newCandle.TimeStart.Add(candleMinuteLen);

                    if (candlesOld[i].TimeStart >= EndCandleTime)
                    {
                        i--;
                        break;
                    }

                    if (candlesOld[i].High > newCandle.High)
                    {
                        newCandle.High = candlesOld[i].High;
                    }
                    if (candlesOld[i].Low < newCandle.Low)
                    {
                        newCandle.Low = candlesOld[i].Low;
                    }

                    newCandle.Close = candlesOld[i].Close;
                    newCandle.Volume += candlesOld[i].Volume;

                    if (i2 + 1 < countOldCandlesInOneNew - 1)
                    {
                        i++;
                    }
                }

                candlesNew.Add(newCandle);
            }

            return candlesNew;
        }

        public List<Candle> GetAllCandles(Security security, DateTime startTime, int minutesCount, DateTime endTime)
        {
            int startCandle = 0;

            DateTime lastTime = startTime;

            List<Candle> candles = new List<Candle>();

            while (lastTime < endTime)
            {
                List<Candle> curCandles = Get500LastCandles(security, startTime, startCandle, minutesCount);
                startCandle += 500;

                if (curCandles == null ||
                    curCandles.Count == 0)
                {
                    break;
                }

                candles.AddRange(curCandles);
                lastTime = curCandles[curCandles.Count - 1].TimeStart;
            }

            return candles;
        }

        public List<Candle> Get500LastCandles(Security security, DateTime startTime, int startCandle, int minutesCount)
        {
            string[] classes = security.NameId.Split('#');

            string str = "http://iss.moex.com/iss/engines/";
            str += classes[1];
            str += "/markets/";
            str += classes[2];
            str += "/boards/";
            str += classes[3];
            str += "/securities/";
            str += security.Name;
            str += "/candles.json?from=";
            str += startTime.Year + "-";

            if (startTime.Month < 10)
            {
                str += "0" + startTime.Month + "-";
            }
            else
            {
                str += startTime.Month + "-";
            }

            if (startTime.Day < 10)
            {
                str += "0" + startTime.Day + "-";
            }
            else
            {
                str += startTime.Day;
            }
            str += "&interval=";
            str += minutesCount;
            str += "&start=";
            str += startCandle;

            string response = GetRequest(str);

            List<Candle> result = new List<Candle>();

            JObject json = JObject.Parse(response);

            JArray dataArray = (JArray)json["candles"]["data"];

            for (int j = 0; j < dataArray.Count; j++)
            {
                JArray innerArray = (JArray)dataArray[j];

                Candle candle = new Candle();
                candle.Open = innerArray[0].ToString().ToDecimal();
                candle.Close = innerArray[1].ToString().ToDecimal();
                candle.High = innerArray[2].ToString().ToDecimal();
                candle.Low = innerArray[3].ToString().ToDecimal();
                candle.Volume = innerArray[5].ToString().ToDecimal();
                candle.TimeStart = Convert.ToDateTime(innerArray[6].ToString());

                result.Add(candle);
            }

            return result;

            // ТФ которые есть: 1 минута, 10 минут, 60 минут
            // за раз 500 свечек

            // "http://iss.moex.com/iss/engines/stock/markets/shares/boards/TQBR/securities/SBER/candles.json?from=2014-01-01&interval=60&start=0";
            //  http://iss.moex.com/iss/engines/stock/markets/shares/boards/TQBR/securities/LKOH/candles.json?from=2014-04-01-&interval=1&start=500
        }

        #endregion

        #region 4 Queries

        private string GetRequest(string url)
        {
            try
            {
                RestRequest requestRest = new RestRequest(Method.GET);
                RestClient client = new RestClient(url);
                var responseMessage = client.Execute(requestRest).Content;
                return responseMessage;
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
                return null;
            }
        }

        #endregion

        #region 5 Log

        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else
            {
                MessageBox.Show(message);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion

        #region 6 Unused methods

        public void GetPortfolios()
        {
            Portfolio portfolio = new Portfolio();
            portfolio.ValueCurrent = 1;
            portfolio.Number = "MoexISS fake portfolio";

            PortfolioEvent?.Invoke(new List<Portfolio>() { portfolio });
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime) { return null; }

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
        public event Action<Order> MyOrderEvent { add { } remove { } }
        public event Action<MyTrade> MyTradeEvent { add { } remove { } }
        public event Action<List<Portfolio>> PortfolioEvent;
        public event Action<MarketDepth> MarketDepthEvent { add { } remove { } }
        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }
        public event Action<Trade> NewTradesEvent { add { } remove { } }

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }

        #endregion
    }
}