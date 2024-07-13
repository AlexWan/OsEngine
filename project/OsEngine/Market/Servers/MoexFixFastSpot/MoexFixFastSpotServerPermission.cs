﻿/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

namespace OsEngine.Market.Servers.MoexFixFastSpot
{
    public class MoexFixFastSpotServerPermission : IServerPermission
    {
        public ServerType ServerType
        {
            get { return ServerType.MoexFixFastSpot; }
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

        public bool UseStandartCandlesStarter
        {
            get { return false; }
        }

        public bool IsUseLotToCalculateProfit
        {
            get { return true; }
        }

        public bool ManuallyClosePositionOnBoard_IsOn
        {
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
                string[] values = new string[]
                {
                    "RUB",
                    "USD",
                    "EUR",
                    "CNY"
                };

                return values;
            }
        }

        public bool CanQueryOrdersAfterReconnect
        {
            get { return false; }
        }

        public bool CanQueryOrderStatus
        {
            get { return false; }
        }

        public TimeFramePermission TradeTimeFramePermission
        {
            get { return _tradeTimeFramePermission; }
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

        #endregion
    }
}