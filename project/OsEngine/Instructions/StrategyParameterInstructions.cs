/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Language;
using System.Collections.Generic;
using System.Reflection;
using static OsEngine.Language.OsLocalization;

namespace OsEngine.Instructions
{
    public class StrategyParameterInstructions
    {
        public string AllInstructionsInClassDescription
        {
            get
            {
                OsLocalType currentLanguage = OsLocalization.CurLocalization;

                if (currentLanguage == OsLocalType.Ru)
                {
                    return "Параметры робота. Сборник статей";
                }
                else if (currentLanguage == OsLocalType.Eng)
                {
                    return "Strategy parameters. A collection of posts";
                }

                return "";
            }
        }

        public List<Instruction> AllInstructionsInClass
        {
            get
            {
                List<Instruction> result = new List<Instruction>();
                return result;
            }
        }

        public List<Instruction> GetInstructionsForRobot(string robotName)
        {
            if (string.IsNullOrEmpty(robotName))
            {
                return null;
            }

            Dictionary<string, Instruction> cache = GetRobotInstructionCache();

            if (cache.TryGetValue(robotName, out Instruction specific) == false)
            {
                return null;
            }

            List<Instruction> result = new List<Instruction>();
            result.Add(specific);

            return result;
        }

        private Dictionary<string, Instruction> _robotInstructionCache;

        private Dictionary<string, Instruction> GetRobotInstructionCache()
        {
            if (_robotInstructionCache != null)
            {
                return _robotInstructionCache;
            }

            _robotInstructionCache = new Dictionary<string, Instruction>();

            FieldInfo[] fields = GetType().GetFields();

            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];

                if (field.FieldType != typeof(Instruction))
                {
                    continue;
                }

                if (field.Name.StartsWith("Link") == false)
                {
                    continue;
                }

                string robotName = field.Name.Substring(4); // убираем префикс "Link"

                if (string.IsNullOrEmpty(robotName))
                {
                    continue;
                }

                Instruction instruction = (Instruction)field.GetValue(this);

                if (instruction != null)
                {
                    _robotInstructionCache[robotName] = instruction;
                }
            }

            return _robotInstructionCache;
        }

        // Инструкции для конкретных роботов.
        // Соглашение об именовании: Link{ИмяКласса робота}
        // Пример: для робота AlgoStart1LinearRegression -> поле LinkAlgoStart1LinearRegression

        public Instruction LinkAlgoStart1LinearRegression = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "AlgoStart 1. Трендовый робот-скринер на индикаторе LinearRegression",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab76&subcats%5B%5D=sub118&items%5B%5D=item873"
            }
        };

        public Instruction LinkAlgoStart2Soldiers = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "AlgoStart 2. Трендовый робот-скринер на паттерне «Три солдата»",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab76&subcats%5B%5D=sub118&items%5B%5D=item874"
            }
        };

        public Instruction LinkAlgoStart3PriceChannel = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "AlgoStart 3. Трендовый робот-скринер на адаптивном ценовом канале",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab76&subcats%5B%5D=sub118&items%5B%5D=item875"
            }
        };

        public Instruction LinkAlgoStart4Railway = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "AlgoStart 4. Трендовый робот-скринер «Железная дорога ZigZag»",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab76&subcats%5B%5D=sub118&items%5B%5D=item876"
            }
        };

        public Instruction LinkFuturesStart1Bollinger = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "FuturesStart 1. Трендовый робот на Bollinger",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab76&subcats%5B%5D=sub119&items%5B%5D=item877"
            }
        };

        public Instruction LinkFuturesStart2Keltner = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "FuturesStart 2. Трендовый робот на Keltner",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab76&subcats%5B%5D=sub119&items%5B%5D=item878"
            }
        };

        public Instruction LinkTelegramSample = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Робот для рассылки сообщений",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub34&items%5B%5D=item295"
            }
        };

        public Instruction LinkTMONRebalancer = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Робот-ребалансер для Tmon",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub34&items%5B%5D=item835"
            }
        };

        public Instruction LinkPayOfMarginBot = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Тестер. Робот для уплаты комиссий брокеру за маржинальную торговлю",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub28&items%5B%5D=item833"
            }
        };

        public Instruction LinkTaxPayer = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Тестер. Робот для уплаты налогов в тестере",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub28&items%5B%5D=item831"
            }
        };

        public Instruction LinkPairCointegrationSideTrader = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Робот для парного арбитража на схождение",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub49&items%5B%5D=item686"
            }
        };

        public Instruction LinkPairCorrelationNegative = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Робот для парного арбитража на разрыв спреда",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub49&items%5B%5D=item689"
            }
        };

        public Instruction LinkPairCorrelationTrader = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Робот для классического парного стат арбитража на разрыв с несколькими фильтрами",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub49&items%5B%5D=item690"
            }
        };

        public Instruction LinkMultiOneLegArbitrageMeanReversion = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Индексный одноногий арбитраж на возврат к среднему",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub55&items%5B%5D=item722"
            }
        };

        public Instruction LinkMultiOneLegArbitrageInTrend = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Индексный одноногий арбитраж в тренд на стадиях волатильности",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub55&items%5B%5D=item730"
            }
        };

        public Instruction LinkMultiExchangePairArbitrageOnTheIndex = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Парный межбиржевой арбитраж на индексе",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub55&items%5B%5D=item804"
            }
        };

        public Instruction LinkIndexArbitrageClassic = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Робот для классического индексного арбитража",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub55&items%5B%5D=item731"
            }
        };

        public Instruction LinkGridLinearRegression = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Автосетка по пробою канала линейной регрессии",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub101&items%5B%5D=item769"
            }
        };

        public Instruction LinkGridBollinger = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Автосетка по каналу Bollinger. Лонг и Шорт. Контртренд",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub101&items%5B%5D=item771"
            }
        };

        public Instruction LinkGridTwoSides = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Автосетка в обе стороны по падению волатильности по ATR",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub101&items%5B%5D=item775"
            }
        };

        public Instruction LinkGridTwoSignals = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Две сетки по двум разным сигналам",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub101&items%5B%5D=item776"
            }
        };

        public Instruction LinkGridBollingerScreener = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Скринер на сетках. Bollinger по волатильности",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub101&items%5B%5D=item777"
            }
        };

        public Instruction LinkGridScreenerAdaptiveSoldiers = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Скринер на сетках по взрыву волатильности в тренд",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub101&items%5B%5D=item778"
            }
        };

        public Instruction LinkGridPair = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Торговля раздвижек пар через Маркет-Мейкерскую сетку",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub101&items%5B%5D=item780"
            }
        };

        public Instruction LinkGridVolumeBollingerRankingScreener = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Автосетка с фильтром щитков и ранжированием общего направления рынка",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub101&items%5B%5D=item862"
            }
        };

        public Instruction LinkOpenInterestBotSample = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Пример робота, запрашивающего в своей логике OI",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub102&items%5B%5D=item783"
            }
        };

        public Instruction LinkNewsAIBot = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Робот для торговли по новостям при помощи ИИ",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub103&items%5B%5D=item788"
            }
        };

        public Instruction LinkTelegramCryptoXBot = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Робот для торговли по сигналам из Телеграм",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub103&items%5B%5D=item789"
            }
        };

        public Instruction LinkLiquidityAnalyzer = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Скринер для анализа мгновенной ликвидности по большому числу инструментов",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub117&items%5B%5D=item844"
            }
        };

        public Instruction LinkTwoEntrySample = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Контроль позиций по разным типам входов при помощи SignalTypeOpen и SignalTypeClose",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab11&cats%5B%5D=tab88&subcats%5B%5D=sub61&items%5B%5D=item382"
            }
        };

        public Instruction LinkPriceChannelCounterTrend = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Выход из позиции в несколько ордеров одновременно через множество открытий",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab11&cats%5B%5D=tab88&subcats%5B%5D=sub61&items%5B%5D=item392"
            }
        };

        public Instruction LinkEnvelopsCountertrend = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Усреднение позиций через открытие новых позиций с пересчётом тейк-профита по средней цене входа",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab11&cats%5B%5D=tab88&subcats%5B%5D=sub61&items%5B%5D=item488"
            }
        };

        public Instruction LinkAlligatorTrendAverage = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Пирамидинг по движению и усреднение на откате",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab11&cats%5B%5D=tab88&subcats%5B%5D=sub61&items%5B%5D=item491"
            }
        };

        public Instruction LinkCandlesTurnaroundPattern = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Последовательный выход из позиций лимитками, ожидающими в рынке",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab11&cats%5B%5D=tab88&subcats%5B%5D=sub61&items%5B%5D=item495"
            }
        };

        public Instruction LinkCustomIcebergSample = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Вход в позицию через кастомный айсберг для реала",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab11&cats%5B%5D=tab88&subcats%5B%5D=sub61&items%5B%5D=item497"
            }
        };

        public Instruction LinkUnsafeLimitsClosingSample = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Одновременный выход из позиций лимитками, ожидающими в рынке",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab11&cats%5B%5D=tab88&subcats%5B%5D=sub61&items%5B%5D=item498"
            }
        };

        public Instruction LinkUnsafeAveragePosition = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Усреднение двумя лимитками, ожидающими в рынке",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab11&cats%5B%5D=tab88&subcats%5B%5D=sub61&items%5B%5D=item499"
            }
        };

        public Instruction LinkStopByTradeFeedSample = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Стандартный вход на свечках. Трейлинг стоп по ленте сделок",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab11&cats%5B%5D=tab88&subcats%5B%5D=sub61&items%5B%5D=item521"
            }
        };

        public Instruction LinkElementsOnChartSampleBot = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "«Кастомные элементы чарта» для OsEngine",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab76&subcats%5B%5D=sub78&items%5B%5D=item534"
            }
        };

        public Instruction LinkCustomParamsUseBotSamplet = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Таблица в окне параметров",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab76&subcats%5B%5D=sub78&items%5B%5D=item535"
            }
        };

        public Instruction LinkCustomChartInParamWindowSample = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Робот-пример «Чарт в окне параметров»",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab76&subcats%5B%5D=sub78&items%5B%5D=item536"
            }
        };

        public Instruction LinkCustomTableInTheParamWindowSample = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Пример «Таблица в окне параметров 2»",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab76&subcats%5B%5D=sub78&items%5B%5D=item538"
            }
        };

        public Instruction LinkVolatilityAdaptiveCandlesTrader = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Робот для торговли кастомных свечей на ускорении к усреднённой внутридневной волатильности",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&cats%5B%5D=tab112&cats%5B%5D=tab80&subcats%5B%5D=sub56&items%5B%5D=item369"
            }
        };

        public Instruction LinkCustomCandlesImpulseTrader = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Импульсный робот на кастомных свечках, адаптирующихся под волатильность",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&cats%5B%5D=tab112&cats%5B%5D=tab80&subcats%5B%5D=sub56&items%5B%5D=item370"
            }
        };

        public Instruction LinkSmaScreener = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Самый простой скринер на скользящей средней",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&cats%5B%5D=tab112&cats%5B%5D=tab80&subcats%5B%5D=sub99&items%5B%5D=item747"
            }
        };

        public Instruction LinkPinBarVolatilityScreener = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Скринер ложного пробоя на PinBar, привязанном к внутридневной волатильности",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&cats%5B%5D=tab112&cats%5B%5D=tab80&subcats%5B%5D=sub99&items%5B%5D=item751"
            }
        };

        public Instruction LinkPriceChannelAdaptiveRsiScreener = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Скринер на RSI и адаптивном ценовом канале",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&cats%5B%5D=tab112&cats%5B%5D=tab80&subcats%5B%5D=sub99&items%5B%5D=item753"
            }
        };

        public Instruction LinkPumpDetectorScreener = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Скринер, анализирующий ленту сделок",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&cats%5B%5D=tab112&cats%5B%5D=tab80&subcats%5B%5D=sub99&items%5B%5D=item755"
            }
        };

        public Instruction LinkPlateDetectorScreener = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Скринер, анализирующий стакан котировок",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&cats%5B%5D=tab112&cats%5B%5D=tab80&subcats%5B%5D=sub99&items%5B%5D=item756"
            }
        };
    }
}
