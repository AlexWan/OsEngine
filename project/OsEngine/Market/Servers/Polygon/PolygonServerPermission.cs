﻿
namespace OsEngine.Market.Servers.Polygon
{
    class PolygonServerPermission : IServerPermission
    {
        public ServerType ServerType
        {
            get { return ServerType.Polygon; }
        }

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
            get { return true; ; }
        }
        public bool DataFeedTf30SecondCanLoad
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

        public bool DataFeedTf1MinuteCanLoad
        {
            get { return true; }
        }
        public bool DataFeedTf2MinuteCanLoad
        {
            get { return true; }
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
            get { return true; }
        }
        public bool DataFeedTf4HourCanLoad
        {
            get { return true; }
        }
        public bool DataFeedTfDayCanLoad
        {
            get { return true; }
        }
        
        public int WaitTimeSecondsAfterFirstStartToSendOrders
        {
            get { return 1; }
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
                TimeFrameMin2IsOn = false,
                TimeFrameMin3IsOn = true,
                TimeFrameMin5IsOn = true,
                TimeFrameMin10IsOn = false,
                TimeFrameMin15IsOn = true,
                TimeFrameMin20IsOn = false,
                TimeFrameMin30IsOn = true,
                TimeFrameMin45IsOn = false,
                TimeFrameHour1IsOn = true,
                TimeFrameHour2IsOn = true,
                TimeFrameHour4IsOn = true,
                TimeFrameDayIsOn = true
            };

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

        public bool UseStandardCandlesStarter
        {
            get { return true; }
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

        public bool IsNewsServer
        {
            get { return false; }
        }
    }
}
