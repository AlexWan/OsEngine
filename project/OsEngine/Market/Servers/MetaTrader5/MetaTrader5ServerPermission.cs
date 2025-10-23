﻿namespace OsEngine.Market.Servers.MetaTrader5
{
    public class MetaTrader5ServerPermission : IServerPermission
    {
        public ServerType ServerType
        {
            get { return ServerType.MetaTrader5; }
        }

        #region DataFeedPermissions

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

        public int WaitTimeSecondsAfterFirstStartToSendOrders
        {
            get { return 10; }
        }

        public bool IsCanChangeOrderPrice
        {
            get { return true; }
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

        private TimeFramePermission _tradeTimeFramePermission = new TimeFramePermission()
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
            TimeFrameMin3IsOn = false,
            TimeFrameMin5IsOn = true,
            TimeFrameMin10IsOn = false,
            TimeFrameMin15IsOn = true,
            TimeFrameMin20IsOn = false,
            TimeFrameMin30IsOn = true,
            TimeFrameMin45IsOn = false,
            TimeFrameHour1IsOn = true,
            TimeFrameHour2IsOn = false,
            TimeFrameHour4IsOn = true,
            TimeFrameDayIsOn = true
        };

        public bool ManuallyClosePositionOnBoard_IsOn
        {
            get { return false; }
        }

        public string[] ManuallyClosePositionOnBoard_ValuesForTrimmingName
        {
            get
            {
                string[] values = new string[]
                {
                    "_LONG",
                    "_SHORT"
                };

                return values;
            }
        }

        public string[] ManuallyClosePositionOnBoard_ExceptionPositionNames
        {
            get
            {
                string[] values = new string[]
                {
                    "RUB",
                    "USD",
                    "EUR"
                };

                return values;
            }
        }

        public bool CanQueryOrdersAfterReconnect
        {
            get { return true; }
        }

        public bool CanQueryOrderStatus
        {
            get { return true; }
        }

        public bool CanGetOrderLists
        {
            get { return true; }
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

        public bool HaveOnlyMakerLimitsRealization
        {
            get { return false; }
        }

        #endregion
    }
}