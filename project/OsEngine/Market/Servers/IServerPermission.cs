

using System.Collections.Generic;

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

        bool DataFeedTfMarketDepthHistoryCanLoad
        {
            get { return false; }

        }

        #endregion

        #region Trade Permissions

        bool MarketOrdersIsSupport { get; }

        bool IsCanChangeOrderPrice { get; }

        bool IsUseLotToCalculateProfit { get; }

        TimeFramePermission TradeTimeFramePermission { get; }

        int WaitTimeSecondsAfterFirstStartToSendOrders { get; }

        bool UseStandardCandlesStarter { get; }

        bool ManuallyClosePositionOnBoard_IsOn { get; }

        string[] ManuallyClosePositionOnBoard_ValuesForTrimmingName { get; }

        string[] ManuallyClosePositionOnBoard_ExceptionPositionNames { get; }

        bool CanQueryOrdersAfterReconnect { get; }

        bool CanQueryOrderStatus { get; }

        bool CanGetOrderLists { get; }

        bool HaveOnlyMakerLimitsRealization { get; }

        #endregion

        #region Other Permissions

        bool IsNewsServer { get; }

        bool IsSupports_CheckDataFeedLogic { get; }

        string[] CheckDataFeedLogic_ExceptionSecuritiesClass { get; }

        int CheckDataFeedLogic_NoDataMinutesToDisconnect { get; }

        bool IsSupports_MultipleInstances { get; }

        bool IsSupports_ProxyFor_MultipleInstances { get; }

        bool IsSupports_AsyncOrderSending { get; }

        int AsyncOrderSending_RateGateLimitMls { get; }

        bool IsSupports_AsyncCandlesStarter { get; }

        int AsyncCandlesStarter_RateGateLimitMls { get; }

        string[] IpAddressServer { get; }
                
        bool CanChangeOrderMarketNumber { get; }

        #endregion

        #region Leverage Permission 

        bool Leverage_IsSupports { get; }

        Dictionary<string, LeveragePermission> Leverage_Permission { get; }

        bool HedgeMode_IsSupports { get; }

        Dictionary<string, HedgeModePermission> HedgeMode_Permission { get; }

        bool MarginMode_IsSupports { get; }

        Dictionary<string, MarginModePermission> MarginMode_Permission { get; }

        #endregion
    }

    public class LeveragePermission
    {
        public decimal Leverage_StandardValue { get; set; }

        public bool Leverage_CommonMode { get; set; }

        public bool Leverage_IndividualLongShort { get; set; }

        public bool Leverage_CheckOpenPosition { get; set; }

        public string[] Leverage_SupportClassesIndividualLongShort { get; set; }

        public string[] Leverage_CantBeLeverage { get; set; }
    }

    public class HedgeModePermission
    {
        public string HedgeMode_StandardValue { get; set; }

        public bool HedgeMode_CommonMode { get; set; }

        public bool HedgeMode_CheckOpenPosition { get; set; }

        public string[] HedgeMode_SupportMode { get; set; }

        public string[] HedgeMode_CantBeHedgeMode { get; set; }
    }

    public class MarginModePermission
    {
        public string MarginMode_StandardValue { get; set; }

        public bool MarginMode_CommonMode { get; set; }

        public bool MarginMode_CheckOpenPosition { get; set; }

        public string[] MarginMode_SupportMode { get; set; }
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