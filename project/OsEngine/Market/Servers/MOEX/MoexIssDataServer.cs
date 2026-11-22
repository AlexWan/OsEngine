/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
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
            NeedToHideParameters = false;
            NeedToHideStandardParameters = true;

            CreateParameterEnum(MoexDataServerRealization.TimeZoneParamName,
                MoexDataServerRealization.TimeZoneDefault,
                MoexDataServerRealization.GetTimeZones());
        }
    }

    public class MoexDataServerRealization : IServerRealization
    {
        public const string TimeZoneParamName = "Time zone of candles";

        public static readonly string TimeZoneDefault = "UTC+3 (Moscow, St. Petersburg, Minsk)";

        public static List<string> GetTimeZones()
        {
            return new List<string>
            {
                "UTC+0 (Accra, Dakar)",
                TimeZoneDefault
            };
        }

        private int GetTimeShiftHours()
        {
            for (int i = 0; i < ServerParameters.Count; i++)
            {
                if (ServerParameters[i].Name == TimeZoneParamName
                    && ServerParameters[i] is ServerParameterEnum enumParam)
                {
                    if (enumParam.Value == "UTC+0 (Accra, Dakar)")
                    {
                        return -3;
                    }

                    return 0;
                }
            }

            return 0;
        }

        /// <summary>
        /// Точка инъекции «сейчас» для юнит-тестов (bug-0619). Возвращает текущий момент в UTC,
        /// по умолчанию — системный UTC.
        /// </summary>
        internal static Func<DateTime> NowProviderUtc { get; set; } = delegate { return DateTime.UtcNow; };

        /// <summary>
        /// «Сейчас» по бирже. Анкетные даты ISS датированы Москвой (UTC+3, без летнего времени),
        /// локальные сутки хоста вне MSK наступают раньше или позже биржевых (bug-0619).
        /// </summary>
        private static DateTime ExchangeNow()
        {
            Func<DateTime> provider = NowProviderUtc;
            DateTime utcNow = provider == null ? DateTime.UtcNow : provider();

            // Kind=Utc здесь физически сдвинут на +3 часа и врал бы; сравниваем как Unspecified с Unspecified
            return DateTime.SpecifyKind(utcNow.AddMilliseconds(TimeManager.MoscowUtcOffsetMs),
                DateTimeKind.Unspecified);
        }

        private static DateTime ExchangeToday()
        {
            return ExchangeNow().Date;
        }

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
            string result = GetRequest("https://iss.moex.com/iss/engines/");

            if (string.IsNullOrEmpty(result))
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

        public bool IsCompletelyDeleted { get; set; }

        #endregion

        #region 2 Securities

        public void GetSecurities()
        {
            try
            {
                List<Security> securities = new List<Security>();

                SendLogMessage("Securities downloading...", LogMessageType.System);

                List<string> engines = GetEngines();

                List<string> markets = GetMarkets(engines);

                List<string> classes = GetClasses(markets);

                securities = GetSecurities(classes);

                securities = CreateFuturesSection(securities);

                HashSet<string> seenSecurities = new HashSet<string>();

                for (int i = securities.Count - 1; i >= 0; i--)
                {
                    string key = securities[i].Name + "|" + securities[i].NameClass;

                    if (seenSecurities.Add(key) == false)
                    {
                        securities.RemoveAt(i);
                    }
                }

                SecurityEvent?.Invoke(securities);

                SendLogMessage("Securities downloaded. Count: " + securities.Count, LogMessageType.System);
            }
            catch (Exception ex)
            {
                SendLogMessage("Securities downloading error: " + ex.ToString(), LogMessageType.Error);
            }
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

            // F3: анкету синтетики истёкших берём у фьючерса той же базы, шаблон — первое вхождение базы
            Dictionary<string, Security> templates = new Dictionary<string, Security>();

            for (int i = 0; i < allFutures.Count; i++)
            {
                if (allFutures[i].Name.Length == 0
                    || char.IsDigit(allFutures[i].Name[allFutures[i].Name.Length - 1]) == false)
                {
                    continue; // бессрочный фьючерс (тикер заканчивается маркером F, а не годом) — историю не генерируем
                }

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
                templates[name] = allFutures[i];
            }

            for (int i = 0; i < names.Count; i++)
            {
                Security template;
                if (templates.TryGetValue(names[i], out template) == false)
                {
                    template = allFutures[0]; // шаблона базы нет — явный фолбэк на первый фьючерс
                }

                List<Security> synthSecurities = GetFutNamesForTenYears(names[i], template.NameId);

                foreach (Security synth in synthSecurities)
                {
                    synth.PriceStep = template.PriceStep;
                    synth.PriceStepCost = template.PriceStepCost;
                    synth.Decimals = template.Decimals;
                    synth.Lot = template.Lot;
                    synth.VolumeStep = 1m;
                    synth.SecurityType = template.SecurityType;
                    synth.Exchange = template.Exchange;
                    synth.State = SecurityStateType.Close; // истёкший контракт — Close безусловно
                }

                securities.AddRange(synthSecurities);
            }

            return securities;
        }

        private List<Security> GetFutNamesForTenYears(string futName, string idEnding)
        {
            string[] id = idEnding.Split('#');
            idEnding = id[1] + "#" + id[2] + "#" + id[3];

            List<Security> newSecurities = new List<Security>();

            // bug-0619: база года — от биржевого «сейчас», не от даты хоста
            int currentYear = ExchangeNow().Year;

            for (int i = 0; i < 8; i++)
            {
                if(futName == "BR"
                    || futName == "NG"
                    || futName == "BT"
                    || futName == "CC"
                    || futName == "KC"
                    || futName == "W4")
                {
                    newSecurities.AddRange(GetFuturesForOneYearOncePerMonth(futName, idEnding, currentYear - i - 2000));
                }
                else
                {
                    newSecurities.AddRange(GetFuturesForOneYearOncePerQuarter(futName, idEnding, currentYear - i - 2000));
                }
             }

            return newSecurities;
        }

        private List<Security> GetFuturesForOneYearOncePerMonth(string futName, string idEnding, int year)
        {
            int futEnd = Convert.ToInt32(year.ToString().Substring(1, 1));

            List<Security> sec = new List<Security>();

            // bug-0619: стражи читают биржевое «сейчас» (дата хоста вне MSK даёт не те сутки).
            // shortcut: снимок по одному на метод — один в GetFutNamesForTenYears и по снимку в каждом
            // PerMonth/PerQuarter, то есть 9 вызовов ExchangeNow() на выгрузку анкет; биржевая полночь
            // между ними даёт перечень от разных суток (±1 строка до следующей выгрузки). Единый снимок
            // на выгрузку = вариант B (параметр DateTime из CreateFuturesSection).
            // Страж перед декабрём (Month < 11) сломан отдельно — BUG-0601, здесь не правится.
            DateTime exchangeNow = ExchangeNow();

            // 1 BRF6
            // 2 BRG6
            // 3 BRH6
            // 4 BRJ6
            // 5 BRK6
            // 6 BRM6
            // 7 BRN6
            // 8 BRQ6
            // 9 BRU6
            // 10 BRV6
            // 11 BRX6
            // 12 BRZ6

            // 1 BRF6
            Security month1 = new Security();
            month1.Name = futName + "F" + futEnd;
            month1.NameId = month1.Name + "#" + idEnding;
            month1.NameClass = "Фьючерсы истёкшие#RFUD";
            month1.NameFull = futName + "-1." + year;
            sec.Add(month1);

            if (exchangeNow.Year == 2000 + year &&
                exchangeNow.Month < 2)
            {
                return sec;
            }

            // 2 BRG6
            Security month2 = new Security();
            month2.Name = futName + "G" + futEnd;
            month2.NameId = month2.Name + "#" + idEnding;
            month2.NameClass = "Фьючерсы истёкшие#RFUD";
            month2.NameFull = futName + "-2." + year;
            sec.Add(month2);

            if (exchangeNow.Year == 2000 + year &&
                exchangeNow.Month < 3)
            {
                return sec;
            }

            // 3 BRH6
            Security month3 = new Security();
            month3.Name = futName + "H" + futEnd;
            month3.NameId = month3.Name + "#" + idEnding;
            month3.NameClass = "Фьючерсы истёкшие#RFUD";
            month3.NameFull = futName + "-3." + year;
            sec.Add(month3);

            if (exchangeNow.Year == 2000 + year &&
                exchangeNow.Month < 4)
            {
                return sec;
            }

            // 4 BRJ6
            Security month4 = new Security();
            month4.Name = futName + "J" + futEnd;
            month4.NameId = month4.Name + "#" + idEnding;
            month4.NameClass = "Фьючерсы истёкшие#RFUD";
            month4.NameFull = futName + "-4." + year;
            sec.Add(month4);

            if (exchangeNow.Year == 2000 + year &&
                exchangeNow.Month < 5)
            {
                return sec;
            }

            // 5 BRK6
            Security month5 = new Security();
            month5.Name = futName + "K" + futEnd;
            month5.NameId = month5.Name + "#" + idEnding;
            month5.NameClass = "Фьючерсы истёкшие#RFUD";
            month5.NameFull = futName + "-5." + year;
            sec.Add(month5);

            if (exchangeNow.Year == 2000 + year &&
                exchangeNow.Month < 6)
            {
                return sec;
            }

            // 6 BRM6
            Security month6 = new Security();
            month6.Name = futName + "M" + futEnd;
            month6.NameId = month6.Name + "#" + idEnding;
            month6.NameClass = "Фьючерсы истёкшие#RFUD";
            month6.NameFull = futName + "-6." + year;
            sec.Add(month6);

            if (exchangeNow.Year == 2000 + year &&
                exchangeNow.Month < 7)
            {
                return sec;
            }

            // 7 BRN6
            Security month7 = new Security();
            month7.Name = futName + "N" + futEnd;
            month7.NameId = month7.Name + "#" + idEnding;
            month7.NameClass = "Фьючерсы истёкшие#RFUD";
            month7.NameFull = futName + "-7." + year;
            sec.Add(month7);

            if (exchangeNow.Year == 2000 + year &&
                exchangeNow.Month < 8)
            {
                return sec;
            }

            // 8 BRQ6
            Security month8 = new Security();
            month8.Name = futName + "Q" + futEnd;
            month8.NameId = month8.Name + "#" + idEnding;
            month8.NameClass = "Фьючерсы истёкшие#RFUD";
            month8.NameFull = futName + "-8." + year;
            sec.Add(month8);

            if (exchangeNow.Year == 2000 + year &&
                exchangeNow.Month < 9)
            {
                return sec;
            }

            // 9 BRU6
            Security month9 = new Security();
            month9.Name = futName + "U" + futEnd;
            month9.NameId = month9.Name + "#" + idEnding;
            month9.NameClass = "Фьючерсы истёкшие#RFUD";
            month9.NameFull = futName + "-9." + year;
            sec.Add(month9);

            if (exchangeNow.Year == 2000 + year &&
                exchangeNow.Month < 10)
            {
                return sec;
            }

            // 10 BRV6
            Security month10 = new Security();
            month10.Name = futName + "V" + futEnd;
            month10.NameId = month10.Name + "#" + idEnding;
            month10.NameClass = "Фьючерсы истёкшие#RFUD";
            month10.NameFull = futName + "-10." + year;
            sec.Add(month10);

            if (exchangeNow.Year == 2000 + year &&
                exchangeNow.Month < 11)
            {
                return sec;
            }

            // 11 BRX6
            Security month11 = new Security();
            month11.Name = futName + "X" + futEnd;
            month11.NameId = month11.Name + "#" + idEnding;
            month11.NameClass = "Фьючерсы истёкшие#RFUD";
            month11.NameFull = futName + "-11." + year;
            sec.Add(month11);

            if (exchangeNow.Year == 2000 + year &&
                exchangeNow.Month < 11)
            {
                return sec;
            }

            // 12 BRZ6
            Security month12 = new Security();
            month12.Name = futName + "Z" + futEnd;
            month12.NameId = month12.Name + "#" + idEnding;
            month12.NameClass = "Фьючерсы истёкшие#RFUD";
            month12.NameFull = futName + "-12." + year;
            sec.Add(month12);

            return sec;
        }

        private List<Security> GetFuturesForOneYearOncePerQuarter(string futName, string idEnding, int year)
        {
            int futEnd = Convert.ToInt32(year.ToString().Substring(1, 1));

            // TATN-9.20

            List<Security> sec = new List<Security>();

            // bug-0619: стражи читают биржевое «сейчас» (дата хоста вне MSK даёт не те сутки);
            // снимок по одному на метод, см. shortcut в GetFuturesForOneYearOncePerMonth
            DateTime exchangeNow = ExchangeNow();

            if (exchangeNow.Year == 2000 + year &&
                exchangeNow.Month < 3)
            {
                return sec;
            }

            Security one = new Security();
            one.Name = futName + "H" + futEnd;
            one.NameId = one.Name + "#" + idEnding;
            one.NameClass = "Фьючерсы истёкшие#RFUD";
            one.NameFull = futName + "-3." + year;
            sec.Add(one);

            if (exchangeNow.Year == 2000 + year &&
                exchangeNow.Month < 6)
            {
                return sec;
            }

            Security two = new Security();
            two.Name = futName + "M" + futEnd;
            two.NameId = two.Name + "#" + idEnding;
            two.NameClass = "Фьючерсы истёкшие#RFUD";
            two.NameFull = futName + "-6." + year;
            sec.Add(two);

            if (exchangeNow.Year == 2000 + year &&
                exchangeNow.Month < 9)
            {
                return sec;
            }

            Security three = new Security();
            three.Name = futName + "U" + futEnd;
            three.NameId = three.Name + "#" + idEnding;
            three.NameClass = "Фьючерсы истёкшие#RFUD";
            three.NameFull = futName + "-9." + year;
            sec.Add(three);

            if (exchangeNow.Year == 2000 + year &&
                exchangeNow.Month < 12)
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
            // запрос типов площадок https://iss.moex.com/iss/engines.json

            string request = GetRequest("https://iss.moex.com/iss/engines.json");

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
                    str == "commodity" ||
                    str == "agro" ||
                    str == "otc" ||
                    str == "quotes" ||
                    str == "money")
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
                string str = "https://iss.moex.com/iss/engines/";
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

            // запрос типов торгов по площадке https://iss.moex.com/iss/engines/stock/markets.json
        }

        private List<string> GetClasses(List<string> markets)
        {
            List<string> requests = new List<string>();

            for (int i = 0; i < markets.Count; i++)
            {
                // запрос классов бумаг по площадке и типу торгов https://iss.moex.com/iss/engines/stock/markets/shares/boards.json

                string str = "https://iss.moex.com/iss/engines/";
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
                    // bug-0596: дефектная строка boards.data — пропуск с логом:
                    // падение здесь гасит весь поток анкет (catch в GetSecurities :125)
                    JArray innerArray = dataArray[j] as JArray;

                    if (innerArray == null || innerArray.Count < 3)
                    {
                        SendLogMessage("GetClasses: skip malformed row in " + markets[i]
                            + ", row index " + j + ", cells: " + (innerArray == null ? 0 : innerArray.Count),
                            LogMessageType.Error);
                        continue;
                    }

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
                // запрос всех бумаг по классу https://iss.moex.com/iss/engines/stock/markets/shares/boards/TQBR/securities.json
                string str = "https://iss.moex.com/iss/engines/";
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

                JArray columnsArray = json["securities"]["columns"] as JArray;
                JArray dataArray = (JArray)json["securities"]["data"];

                for (int j = 0; j < dataArray.Count; j++)
                {
                    // bug-0596: короткая/скалярная строка securities.data — пропуск с логом,
                    // позиционные [0]/[2] ниже иначе роняют весь поток анкет
                    JArray innerArray = dataArray[j] as JArray;

                    if (innerArray == null || innerArray.Count < 3)
                    {
                        SendLogMessage("GetSecurities: skip malformed row in " + classes[i]
                            + ", row index " + j + ", cells: " + (innerArray == null ? 0 : innerArray.Count),
                            LogMessageType.Error);
                        continue;
                    }

                    Security newSec = MapIssSecurity(columnsArray, innerArray, classes[i]);
                    newSec.Name = innerArray[0].ToString();
                    newSec.NameId = newSec.Name + "#" + classes[i];

                    string boardCode = classes[i].Split('#')[2];

                    if (boardCode == "RFUD"
                        && newSec.Name.Length > 0
                        && char.IsDigit(newSec.Name[newSec.Name.Length - 1]) == false)
                    {
                        newSec.NameClass = "Фьючерсы бессрочные#RFUD";
                    }
                    else
                    {
                        newSec.NameClass = classes[i].Split('#')[3] + "#" + boardCode;
                    }

                    newSec.NameFull = innerArray[2].ToString();

                    result.Add(newSec);
                }
            }

            return result;
        }

        // bug-0530 (BUG-0529): анкетные поля строки store `securities`. Маппинг по ИМЕНАМ колонок
        // из `securities.columns` — состав колонок различен по рынкам, позиции использовать нельзя.
        // Дефолты по ТЗ-ред.2 (F1/F2/F4); бросать запрещено: нет колонки (ColIndex -1) -> дефолт.
        private static Security MapIssSecurity(JArray columns, JArray row, string classParam)
        {
            Security sec = new Security();
            sec.Exchange = "MOEX";

            string board = "";
            string[] classParts = classParam == null ? new string[0] : classParam.Split('#');
            if (classParts.Length > 2)
            {
                board = classParts[2];
            }

            int minStepIndex = ColIndex(columns, "MINSTEP");
            string minStepValue = IssCell(row, minStepIndex);
            decimal priceStep = 0m;
            // NumberStyles.Float, не Number: JValue-токен может отдать шаг с экспонентой
            // ("1E-05") или ведущей точкой (".5") — Number молча сливал в фолбэк (U-13/U-15)
            bool stepFromColumn = minStepValue != null
                && decimal.TryParse(minStepValue, NumberStyles.Float, CultureInfo.InvariantCulture, out priceStep)
                && priceStep > 0;

            if (stepFromColumn == false)
            {
                priceStep = 1m; // индексы/CETS и пропавшая колонка — фолбэк шаг
            }

            sec.PriceStep = priceStep;

            // F1 (ТЗ-ред.3b, R-08): V1 Error 16 — точное равенство Decimals и числа
            // дробных знаков PriceStep (Var_1_Securities.IsCompairDecimalsAndStep),
            // S-01/U-03 пинят оракул. Decimals при шаге из колонки — всегда знаки шага:
            // сеттер Security (Security.cs:68-93) не совпадает с оракулом на шаге >=1
            // ("1.5" -> 0) и на длинных дробях. Колонка DECIMALS ISS не используется:
            // на фактических данных она согласована со знаком (tr293), а при рассогласовании
            // (39 бумаг CETS, tr270) оракул требует именно знаки шага. Фолбэк шаг >=1 -> 0.
            if (stepFromColumn)
            {
                sec.Decimals = StepFractionDigits(priceStep);
            }

            if (board == "TQBR")
            {
                sec.State = IssCell(row, ColIndex(columns, "STATUS")) == "A"
                    ? SecurityStateType.Activ : SecurityStateType.Close;
            }
            else if (board == "RFUD")
            {
                DateTime lastTradeDate;
                string lastTradeDateValue = IssCell(row, ColIndex(columns, "LASTTRADEDATE"));
                sec.State = lastTradeDateValue != null
                    && DateTime.TryParse(lastTradeDateValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out lastTradeDate)
                    && lastTradeDate.Date >= ExchangeToday()
                    ? SecurityStateType.Activ : SecurityStateType.Close;
            }
            else
            {
                sec.State = SecurityStateType.Activ; // индексы и валюта — доска не протухает
            }

            sec.PriceStepCost = priceStep;

            if (board == "RFUD")
            {
                // F2: у фьючерсов стоимость шага — колонка STEPPRICE, нет/ноль — сам шаг
                decimal stepCost;
                string stepCostValue = IssCell(row, ColIndex(columns, "STEPPRICE"));
                if (stepCostValue != null
                    && decimal.TryParse(stepCostValue, NumberStyles.Float, CultureInfo.InvariantCulture, out stepCost)
                    && stepCost > 0)
                {
                    sec.PriceStepCost = stepCost;
                }
            }

            // LOTSIZE читаем, где колонка есть в columns ответа (TQBR, CETS — факт дампа runs#350);
            // нет/ноль/null -> 1
            sec.Lot = 1m;

            decimal lotSize;
            string lotSizeValue = IssCell(row, ColIndex(columns, "LOTSIZE"));
            if (lotSizeValue != null
                && decimal.TryParse(lotSizeValue, NumberStyles.Float, CultureInfo.InvariantCulture, out lotSize)
                && lotSize > 0)
            {
                sec.Lot = lotSize;
            }

            sec.VolumeStep = 1m;

            if (board == "RFUD")
            {
                // SECTYPE в RFUD — код базового актива ("BR"), тип из него брать нельзя
                sec.SecurityType = SecurityType.Futures;
            }
            else if (board == "CETS")
            {
                sec.SecurityType = SecurityType.CurrencyPair;
            }
            else if (board == "TQBR")
            {
                sec.SecurityType = SecurityType.Stock;
                string secType = IssCell(row, ColIndex(columns, "SECTYPE"));
                // F4: ветка Fund работает, только пока в columns ответа есть SECTYPE; без колонки мертва
                if (secType == "ETF" || secType == "J")
                {
                    sec.SecurityType = SecurityType.Fund;
                }
            }
            else
            {
                sec.SecurityType = SecurityType.Index; // RTSI/SNDX и прочие индексные доски
            }

            return sec;
        }

        // Число дробных знаков шага без хвостовых нулей — та же мерка, что у V1
        // Error 16 (Var_1_Securities.IsCompairDecimalsAndStep, tr270).
        private static int StepFractionDigits(decimal step)
        {
            string stepText = step.ToString(CultureInfo.InvariantCulture).TrimEnd('0');
            int dotIndex = stepText.IndexOf('.');
            return dotIndex < 0 ? 0 : stepText.Length - dotIndex - 1;
        }

        // Индекс колонки по имени в securities.columns; колонки нет — -1 (вызывающий даёт дефолт).
        private static int ColIndex(JArray columns, string columnName)
        {
            if (columns == null)
            {
                return -1;
            }

            for (int i = 0; i < columns.Count; i++)
            {
                if (string.Equals(IssCell(columns, i), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        // Ячейка строки/колонок в инвариантном формате; за границами или JSON null — null.
        private static string IssCell(JArray array, int index)
        {
            if (array == null || index < 0 || index >= array.Count)
            {
                return null;
            }

            JToken token = array[index];

            if (token == null || token.Type == JTokenType.Null)
            {
                return null;
            }

            return token is IFormattable formattable
                ? formattable.ToString(null, CultureInfo.InvariantCulture)
                : token.ToString();
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
            else if (minutesInTf >= 60 &&
                     minutesInTf < 1440)
            {
                List<Candle> sourseCandle = GetAllCandles(security, startTime, 60, endTime);
                candles = ConcateCandles(sourseCandle, 60, minutesInTf);
            }
            else if (minutesInTf == 1440)
            {
                List<Candle> sourseCandle = GetAllCandles(security, startTime, 24, endTime);
                candles = ConcateCandles(sourseCandle, 1440, minutesInTf);
            }

            Candle previousCandle = new Candle();

            for (int i = 0; i < candles.Count; i++)
            {
                Candle newCandle = candles[i];

                if (newCandle.TimeStart.TimeOfDay == TimeSpan.Zero && minutesInTf < 1440)
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
                        newCandle.TimeStart = newCandle.TimeStart.AddMinutes(-1);
                    }
                }
                if (startTf == 10
                    && (endTf == 30))
                {
                    while (newCandle.TimeStart.Minute != 0
                        && newCandle.TimeStart.Minute != 30)
                    {
                        newCandle.TimeStart = newCandle.TimeStart.AddMinutes(-1);
                    }
                }

                if (startTf == 1
                    && (endTf == 45))
                {
                    while (newCandle.TimeStart.Minute != 0
                        && newCandle.TimeStart.Minute % 5 != 0)
                    {
                        newCandle.TimeStart = newCandle.TimeStart.AddMinutes(-1);
                    }
                }

                newCandle.Open = candlesOld[i].Open;
                newCandle.High = candlesOld[i].High;
                newCandle.Low = candlesOld[i].Low;
                newCandle.Close = candlesOld[i].Close;
                newCandle.Volume += candlesOld[i].Volume;
                newCandle.State = CandleState.Finished;
                i++;

                DateTime EndCandleTime = newCandle.TimeStart.Add(candleMinuteLen);

                while (true)
                {
                    if(i >= candlesOld.Count)
                    {
                        break;
                    }

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

                    i++;
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

            string str = "https://iss.moex.com/iss/engines/";
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

            if (string.IsNullOrEmpty(response))
            {
                return null;
            }

            List<Candle> result = new List<Candle>();

            JObject json = JObject.Parse(response);

            JArray dataArray = (JArray)json["candles"]["data"];

            int timeShift = GetTimeShiftHours();

            for (int j = 0; j < dataArray.Count; j++)
            {
                JArray innerArray = (JArray)dataArray[j];

                Candle candle = new Candle();
                candle.Open = innerArray[0].ToString().ToDecimal();
                candle.Close = innerArray[1].ToString().ToDecimal();
                candle.High = innerArray[2].ToString().ToDecimal();
                candle.Low = innerArray[3].ToString().ToDecimal();
                candle.Volume = innerArray[5].ToString().ToDecimal();
                candle.TimeStart = Convert.ToDateTime(innerArray[6].ToString()).AddHours(timeShift);

                result.Add(candle);
            }

            return result;

            // ТФ которые есть: 1 минута, 10 минут, 60 минут
            // за раз 500 свечек

            // "https://iss.moex.com/iss/engines/stock/markets/shares/boards/TQBR/securities/SBER/candles.json?from=2014-01-01&interval=60&start=0";
            //  https://iss.moex.com/iss/engines/stock/markets/shares/boards/TQBR/securities/LKOH/candles.json?from=2014-04-01-&interval=1&start=500
        }

        #endregion

        #region 4 Queries

        // bug-0596: шов для юнит-стенда (Artifacts/Tests/bug-0596) — моки ISS-ответов без сети
        protected virtual string GetRequest(string url)
        {
            try
            {
                RestRequest requestRest = new RestRequest(Method.GET);
                RestClient client = new RestClient(url);

                string responseMessage = client.Execute(requestRest).Content;
                return responseMessage;
            }
            catch (Exception error)
            {
                SendLogMessage("GetRequest error. Url: " + url + ". " + error.ToString(), LogMessageType.Error);
                return string.Empty;
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
                ServerMaster.SendNewLogMessage(message, type);
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

        public void SetLeverage(Security security, decimal leverage) { }

        #endregion
    }
}