using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using System.Windows.Documents;

namespace OsEngine.Market.Servers
{
    public interface IServerPermission
    {
        ServerType ServerType { get; }

        #region Data Feed Permissions

        bool DataFeedTf1SecondCanLoad { get; }

        bool DataFeedTf2SecondCanLoad { get; }

        bool DataFeedTf5SecondCanLoad { get; }

        bool DataFeedTf10SecondCanLoad { get; }

        bool DataFeedTf15SecondCanLoad { get; }

        bool DataFeedTf20SecondCanLoad { get; }

        bool DataFeedTf30SecondCanLoad { get; }

        bool DataFeedTf1MinuteCanLoad { get; }

        bool DataFeedTf2MinuteCanLoad { get; }

        bool DataFeedTf5MinuteCanLoad { get; }

        bool DataFeedTf10MinuteCanLoad { get; }

        bool DataFeedTf15MinuteCanLoad { get; }

        bool DataFeedTf30MinuteCanLoad { get; }

        bool DataFeedTf1HourCanLoad { get; }

        bool DataFeedTf2HourCanLoad { get; }

        bool DataFeedTf4HourCanLoad { get; }

        bool DataFeedTfDayCanLoad { get; }

        bool DataFeedTfTickCanLoad { get; }

        bool DataFeedTfMarketDepthCanLoad { get; }

        #endregion

        #region Trade Permissions

        bool MarketOrdersIsSupport { get; }

        bool IsCanChangeOrderPrice { get; }

        bool IsUseLotToCalculateProfit { get; }

        TimeFramePermission TradeTimeFramePermission { get; }

        int WaitTimeSecondsAfterFirstStartToSendOrders { get; }

        bool UseStandartCandlesStarter { get; }

        bool ManuallyClosePositionOnBoard_IsOn { get; }

        string[] ManuallyClosePositionOnBoard_ValuesForTrimmingName { get; }

        string[] ManuallyClosePositionOnBoard_ExceptionPositionNames { get; }

        bool CanQueryOrdersAfterReconnect { get; }

        bool CanQueryOrderStatus { get; }

        #endregion

    }

    public class TimeFramePermission
    {
        public bool TimeFrameSec1IsOn = true;

        public bool TimeFrameSec2IsOn = true;

        public bool TimeFrameSec5IsOn = true;

        public bool TimeFrameSec10IsOn = true;

        public bool TimeFrameSec15IsOn = true;

        public bool TimeFrameSec20IsOn = true;

        public bool TimeFrameSec30IsOn = true;

        public bool TimeFrameMin1IsOn = true;

        public bool TimeFrameMin2IsOn = true;

        public bool TimeFrameMin3IsOn = true;

        public bool TimeFrameMin5IsOn = true;

        public bool TimeFrameMin10IsOn = true;

        public bool TimeFrameMin15IsOn = true;

        public bool TimeFrameMin20IsOn = true;

        public bool TimeFrameMin30IsOn = true;

        public bool TimeFrameMin45IsOn = true;

        public bool TimeFrameHour1IsOn = true;

        public bool TimeFrameHour2IsOn = true;

        public bool TimeFrameHour4IsOn = true;

        public bool TimeFrameDayIsOn = true;

    }
}
