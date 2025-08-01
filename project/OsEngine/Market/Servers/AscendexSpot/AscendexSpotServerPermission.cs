﻿

namespace OsEngine.Market.Servers.AscendexSpot
{
  
    public class AscendexSpotServerPermission : IServerPermission
    {
            public ServerType ServerType
            {
                get { return ServerType.AscendexSpot; }
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
                get { return false; }
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

            public bool DataFeedTfTickCanLoad
            {
                get { return false; }
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
                get { return 10; }
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
                    TimeFrameMin2IsOn = false,
                    TimeFrameMin3IsOn = false,
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
                get { return true; }
            }

            public bool CanQueryOrderStatus
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
                get { return true; }
            }

            public bool IsSupports_ProxyFor_MultipleInstances
            {
                get { return true; }
            }

            #endregion
    }
}
