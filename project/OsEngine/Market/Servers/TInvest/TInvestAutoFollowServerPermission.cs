/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

namespace OsEngine.Market.Servers.TInvest
{
    public class TInvestAutoFollowServerPermission : IServerPermission
    {
        public ServerType ServerType
        {
            get { return ServerType.TInvestAutoFollow; }
        }

        #region DataFeedPermissions

        // Осознанное решение: коннектор не качает данные для OsData (все false,
        // сервера нет в ServersTypesToOsData). Свечи через gRPC
        // (GetCandleDataToSecurity / GetLastCandleHistory) существуют только
        // для живой торговли роботов (UseStandardCandlesStarter = true)

        public bool DataFeedTf1SecondCanLoad
        {
            get { return false; }
        }

        public bool DataFeedTf2SecondCanLoad
        {
            get { return false; }
        }

        public bool DataFeedTf5SecondCanLoad
        {
            get { return false; }
        }

        public bool DataFeedTf10SecondCanLoad
        {
            get { return false; }
        }

        public bool DataFeedTf15SecondCanLoad
        {
            get { return false; }
        }

        public bool DataFeedTf20SecondCanLoad
        {
            get { return false; }
        }

        public bool DataFeedTf30SecondCanLoad
        {
            get { return false; }
        }

        public bool DataFeedTfTickCanLoad
        {
            get { return false; }
        }

        public bool DataFeedTfMarketDepthCanLoad
        {
            get { return false; }
        }

        public bool DataFeedTf1MinuteCanLoad
        {
            get { return false; }
        }

        public bool DataFeedTf2MinuteCanLoad
        {
            get { return false; }
        }

        public bool DataFeedTf5MinuteCanLoad
        {
            get { return false; }
        }

        public bool DataFeedTf10MinuteCanLoad
        {
            get { return false; }
        }

        public bool DataFeedTf15MinuteCanLoad
        {
            get { return false; }
        }

        public bool DataFeedTf30MinuteCanLoad
        {
            get { return false; }
        }

        public bool DataFeedTf1HourCanLoad
        {
            get { return false; }
        }

        public bool DataFeedTf2HourCanLoad
        {
            get { return false; }
        }

        public bool DataFeedTf4HourCanLoad
        {
            get { return false; }
        }

        public bool DataFeedTfDayCanLoad
        {
            get { return false; }
        }

        #endregion

        #region Trade permission

        public bool MarketOrdersIsSupport
        {
            get { return true; }
        }

        public bool StopOrdersIsSupport
        {
            get { return true; }
        }

        public int WaitTimeSecondsAfterFirstStartToSendOrders
        {
            get { return 10; }
        }

        public bool IsCanChangeOrderPrice
        {
            // API автоследования не умеет изменение цены сигнала: менять можно
            // было бы только локальные несработавшие лимитки — частичная
            // поддержка ввела бы роботов в заблуждение, поэтому честный false
            get { return false; }
        }

        public bool UseStandardCandlesStarter
        {
            get { return true; }
        }

        public bool IsUseLotToCalculateProfit
        {
            get { return true; }
        }

        public TimeFramePermission TradeTimeFramePermission
        {
            get { return _tradeTimeFramePermission; }
        }
        private TimeFramePermission _tradeTimeFramePermission
            = new TimeFramePermission()
            {
                TimeFrameSec1IsOn = false,
                TimeFrameSec2IsOn = false,
                TimeFrameSec5IsOn = false,
                TimeFrameSec10IsOn = false,
                TimeFrameSec15IsOn = false,
                TimeFrameSec20IsOn = false,
                TimeFrameSec30IsOn = false,
                TimeFrameMin1IsOn = true,
                TimeFrameMin2IsOn = true,
                TimeFrameMin3IsOn = true,
                TimeFrameMin5IsOn = true,
                TimeFrameMin10IsOn = true,
                TimeFrameMin15IsOn = true,
                TimeFrameMin20IsOn = false,
                TimeFrameMin30IsOn = true,
                TimeFrameMin45IsOn = false,
                TimeFrameHour1IsOn = true,
                TimeFrameHour2IsOn = true,
                TimeFrameHour4IsOn = true,
                TimeFrameDayIsOn = true
            };

        public bool ManuallyClosePositionOnBoard_IsOn
        {
            // ручное закрытие позиции работает: движок шлёт встречный
            // маркет-ордер на полный объём — он уходит маркет-сигналом
            // и закрывает виртуальную позицию стратегии
            get { return true; }
        }

        public string[] ManuallyClosePositionOnBoard_ValuesForTrimmingName
        {
            get { return null; }
        }

        public string[] ManuallyClosePositionOnBoard_ExceptionPositionNames
        {
            get
            {
                // строки денежных остатков закрывать нельзя. Сравнение в движке
                // регистрозависимое, регистр currency из API не документирован —
                // держим оба варианта
                string[] values = new string[]
                {
                    "rub", "RUB",
                    "usd", "USD",
                    "eur", "EUR",
                    "hkd", "HKD",
                    "cny", "CNY"
                };

                return values;
            }
        }

        public bool CanQueryOrdersAfterReconnect
        {
            // локальные лимитки живут только в памяти коннектора и по контракту
            // Dispose обнуляются — переизлучить после реконнекта нечего.
            // Биржевые сигналы восстанавливаются своим опросом, без этого флага
            get { return false; }
        }

        public bool CanQueryOrderStatus
        {
            get { return true; }
        }

        public bool CanGetOrderLists
        {
            get { return true; }
        }

        public bool HaveOnlyMakerLimitsRealization
        {
            get { return false; }
        }

        #endregion

        #region Other Permissions

        public bool IsNewsServer
        {
            get { return false; }
        }

        public bool IsSupports_CheckDataFeedLogic
        {
            get { return false; }
        }

        public string[] CheckDataFeedLogic_ExceptionSecuritiesClass
        {
            get { return null; }
        }

        public int CheckDataFeedLogic_NoDataMinutesToDisconnect
        {
            get { return 10; }
        }

        public bool IsSupports_MultipleInstances
        {
            get { return true; }
        }

        public bool IsSupports_ProxyFor_MultipleInstances
        {
            get { return true; }
        }

        public bool IsSupports_AsyncOrderSending
        {
            get { return false; }
        }

        public int AsyncOrderSending_RateGateLimitMls
        {
            get { return 15000; }
        }

        public bool IsSupports_AsyncCandlesStarter
        {
            get { return false; }
        }

        public int AsyncCandlesStarter_RateGateLimitMls
        {
            get { return 10; }
        }

        public string[] IpAddressServer
        {
            get { return null; }
        }

        public bool Leverage_IsSupports
        {
            get { return false; }
        }

        public decimal Leverage_StandardValue
        {
            get { return 10; }
        }

        public string[] Leverage_SupportClasses { get; }

        public bool CanChangeOrderMarketNumber
        {
            get { return false; }
        }

        public OrderLifeTimePermission OrdersLifeTimeRealization
        {
            get
            {
                return new OrderLifeTimePermission
                {
                    GtcIsReady = false,
                    SpecifiedIsReady = true,
                    DayIsReady = true
                };
            }
        }

        #endregion
    }
}
