/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

namespace OsEngine.Market.Servers.Finam
{
    public class FinamServerPermission : IServerPermission
    {
        public ServerType ServerType
        {
            get { return ServerType.Finam; }
        }

        #region DataFeedPermissions

        public bool DataFeedTf1SecondCanLoad
        {
            get { return true; }
        }
        public bool DataFeedTf2SecondCanLoad
        {
            get { return true; }
        }
        public bool DataFeedTf5SecondCanLoad
        {
            get { return true; }
        }
        public bool DataFeedTf10SecondCanLoad
        {
            get { return true; }
        }
        public bool DataFeedTf15SecondCanLoad
        {
            get { return true; }
        }
        public bool DataFeedTf20SecondCanLoad
        {
            get { return true; }
        }
        public bool DataFeedTf30SecondCanLoad
        {
            get { return true; }
        }
        public bool DataFeedTf1MinuteCanLoad
        {
            get { return true; }
        }
        public bool DataFeedTf2MinuteCanLoad
        {
            get { return false; }
        }
        public bool DataFeedTf5MinuteCanLoad
        {
            get { return true; }
        }
        public bool DataFeedTf10MinuteCanLoad
        {
            get { return true; }
        }
        public bool DataFeedTf15MinuteCanLoad
        {
            get { return true; }
        }
        public bool DataFeedTf30MinuteCanLoad
        {
            get { return true; }
        }
        public bool DataFeedTf1HourCanLoad
        {
            get { return true; }
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
            get { return true; }
        }
        public bool DataFeedTfTickCanLoad
        {
            get { return true; }
        }
        public bool DataFeedTfMarketDepthCanLoad
        {
            get { return false; }
        }

        #endregion

        #region Trade permission

        public bool MarketOrdersIsSupport
        {
            get { return false; }
        }

        public bool IsTradeServer
        {
            get { return false; }
        }

        public bool IsCanChangeOrderPrice
        {
            get { return false; }
        }

        public TimeFramePermission TradeTimeFramePermission
        {
            get { return _tradeTimeFramePermission; }
        }

        public int WaitTimeSecondsAfterFirstStartToSendOrders
        {
            get { return 60; }
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
                TimeFrameMin1IsOn = false,
                TimeFrameMin2IsOn = false,
                TimeFrameMin3IsOn = false,
                TimeFrameMin5IsOn = false,
                TimeFrameMin10IsOn = false,
                TimeFrameMin15IsOn = false,
                TimeFrameMin20IsOn = false,
                TimeFrameMin30IsOn = false,
                TimeFrameMin45IsOn = false,
                TimeFrameHour1IsOn = false,
                TimeFrameHour2IsOn = false,
                TimeFrameHour4IsOn = false,
                TimeFrameDayIsOn = false
            };

        public bool UseStandardCandlesStarter
        {
            get { return false; }
        }

        public bool IsUseLotToCalculateProfit
        {
            get { return false; }
        }

        public bool ManuallyClosePositionOnBoard_IsOn
        {
            get { return false; }
        }

        public string[] ManuallyClosePositionOnBoard_ValuesForTrimmingName
        {
            get { return null; }
        }

        public string[] ManuallyClosePositionOnBoard_ExceptionPositionNames
        {
            get { return null; }
        }

        public bool CanQueryOrdersAfterReconnect
        {
            get { return false; }
        }

        public bool CanQueryOrderStatus
        {
            get { return false; }
        }

        public bool CanGetOrderLists
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
            get { return false; }
        }

        public bool IsSupports_ProxyFor_MultipleInstances
        {
            get { return false; }
        }

        public bool IsSupports_AsyncOrderSending
        {
            get { return false; }
        }

        public int AsyncOrderSending_RateGateLimitMls
        {
            get { return 10; }
        }

        public bool IsSupports_AsyncCandlesStarter
        {
            get { return false; }
        }

        public int AsyncCandlesStarter_RateGateLimitMls
        {
            get { return 10; }
        }

        public string[] IpAddresServer
        {
            get { return null; }
        }

        #endregion
    }
}