﻿
namespace OsEngine.Market.Servers.Binance.Spot
{
    public class BinanceSpotServerPermission : IServerPermission
    {
        public ServerType ServerType
        {
            get { return ServerType.Binance; }
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
            get { return true; }
        }

        #endregion

        #region Trade permission

        public bool MarketOrdersIsSupport
        {
            get { return true; }
        }

        public bool IsTradeServer
        {
            get { return true; }
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
            get { return 1; }
        }

        private TimeFramePermission _tradeTimeFramePermission
            = new TimeFramePermission()
            {
                TimeFrameSec1IsOn = true,
                TimeFrameSec2IsOn = true,
                TimeFrameSec5IsOn = true,
                TimeFrameSec10IsOn = true,
                TimeFrameSec15IsOn = true,
                TimeFrameSec20IsOn = true,
                TimeFrameSec30IsOn = true,
                TimeFrameMin1IsOn = true,
                TimeFrameMin2IsOn = true,
                TimeFrameMin3IsOn = true,
                TimeFrameMin5IsOn = true,
                TimeFrameMin10IsOn = true,
                TimeFrameMin15IsOn = true,
                TimeFrameMin20IsOn = true,
                TimeFrameMin30IsOn = true,
                TimeFrameMin45IsOn = true,
                TimeFrameHour1IsOn = true,
                TimeFrameHour2IsOn = true,
                TimeFrameHour4IsOn = true,
                TimeFrameDayIsOn = true
            };

        public bool UseStandartCandlesStarter
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
            get { return true; }
        }

        public bool CanQueryOrderStatus
        {
            get { return true; }
        }

        #endregion
    }
}
