/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;

namespace OsEngine.Market.Servers.Bybit
{
    public class BybitServerPermission : IServerPermission
    {
        public ServerType ServerType
        {
            get { return ServerType.Bybit; }
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
            get
            {
                string[] values = new string[]
                {
                    "_LONG",
                    "_SHORT",
                    "_BOTH"
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
                    "USDT",
                    "USDC",
                    "BTC",
                    "ETH"
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
            get { return true; }
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
            get { return true; }
        }

        public int AsyncOrderSending_RateGateLimitMls
        {
            get { return 10; }
        }

        public bool IsSupports_AsyncCandlesStarter
        {
            get { return true; }
        }

        public int AsyncCandlesStarter_RateGateLimitMls
        {
            get { return 50; }
        }

        public string[] IpAddressServer
        {
            get
            {
                string[] pingIpDomens = new string[]
                {
                    "api.bybit.com",
                    "api-demo.bybit.com",
                    "api.bybit.nl",
                    "api.byhkbit.com",
                    "api.bybit-tr.com",
                    "api.bybit.kz"
                };

                return pingIpDomens;
            }
        }

        public bool CanChangeOrderMarketNumber
        {
            get { return false; }
        }

        #endregion

        #region Leverage, HedgeMode, MarginMode Permissions

        public bool Leverage_IsSupports
        {
            get { return true; }
        }

        public Dictionary<string, LeveragePermission> Leverage_Permission
        {
            get
            {
                return new Dictionary<string, LeveragePermission>()
                {
                    ["LinearPerpetual"] = new LeveragePermission
                    {
                        Leverage_StandardValue = 10,
                        Leverage_CommonMode = false,
                        Leverage_IndividualLongShort = true,
                        Leverage_CheckOpenPosition = false,
                        Leverage_SupportClassesIndividualLongShort = new[] { "Isolated" },
                        Leverage_CantBeLeverage = new[] { "Portfolio" }
                    },

                    ["LinearPerpetual_PERP"] = new LeveragePermission
                    {
                        Leverage_StandardValue = 10,
                        Leverage_CommonMode = false,
                        Leverage_IndividualLongShort = true,
                        Leverage_CheckOpenPosition = false,
                        Leverage_SupportClassesIndividualLongShort = new[] { "Isolated" },
                        Leverage_CantBeLeverage = new[] { "Portfolio" }
                    },

                    ["InversePerpetual"] = new LeveragePermission
                    {
                        Leverage_StandardValue = 10,
                        Leverage_CommonMode = false,
                        Leverage_IndividualLongShort = true,
                        Leverage_CheckOpenPosition = false,
                        Leverage_SupportClassesIndividualLongShort = new[] { "Isolated" },
                        Leverage_CantBeLeverage = new[] { "Portfolio" }
                    },

                    ["LinearFutures"] = new LeveragePermission
                    {
                        Leverage_StandardValue = 10,
                        Leverage_CommonMode = false,
                        Leverage_IndividualLongShort = true,
                        Leverage_CheckOpenPosition = false,
                        Leverage_SupportClassesIndividualLongShort = new[] { "Isolated" },
                        Leverage_CantBeLeverage = new[] { "Portfolio" }
                    },

                    ["InverseFutures"] = new LeveragePermission
                    {
                        Leverage_StandardValue = 10,
                        Leverage_CommonMode = false,
                        Leverage_IndividualLongShort = true,
                        Leverage_CheckOpenPosition = false,
                        Leverage_SupportClassesIndividualLongShort = new[] { "Isolated" },
                        Leverage_CantBeLeverage = new[] { "Portfolio" }
                    }
                };
            }
        }

        public bool HedgeMode_IsSupports
        {
            get { return true; }
        }

        public Dictionary<string, HedgeModePermission> HedgeMode_Permission
        {
            get
            {
                return new Dictionary<string, HedgeModePermission>()
                {
                    ["LinearPerpetual"] = new HedgeModePermission
                    {
                        HedgeMode_StandardValue = "On",
                        HedgeMode_CommonMode = true,
                        HedgeMode_CheckOpenPosition = true,
                        HedgeMode_SupportMode = new[] { "Off", "On" },
                        HedgeMode_CantBeHedgeMode = new[] { "Portfolio" }
                    },

                    ["LinearPerpetual_PERP"] = new HedgeModePermission
                    {
                        HedgeMode_StandardValue = "On",
                        HedgeMode_CommonMode = true,
                        HedgeMode_CheckOpenPosition = true,
                        HedgeMode_SupportMode = new[] { "Off", "On" },
                        HedgeMode_CantBeHedgeMode = new[] { "Isolated", "Cross", "Portfolio" }
                    },

                    ["InversePerpetual"] = new HedgeModePermission
                    {
                        HedgeMode_StandardValue = "On",
                        HedgeMode_CommonMode = true,
                        HedgeMode_CheckOpenPosition = true,
                        HedgeMode_SupportMode = new[] { "Off", "On" },
                        HedgeMode_CantBeHedgeMode = new[] { "Isolated", "Cross", "Portfolio" }
                    },

                    ["LinearFutures"] = new HedgeModePermission
                    {
                        HedgeMode_StandardValue = "On",
                        HedgeMode_CommonMode = true,
                        HedgeMode_CheckOpenPosition = true,
                        HedgeMode_SupportMode = new[] { "Off", "On" },
                        HedgeMode_CantBeHedgeMode = new[] { "Isolated", "Cross", "Portfolio" }
                    },

                    ["InverseFutures"] = new HedgeModePermission
                    {
                        HedgeMode_StandardValue = "On",
                        HedgeMode_CommonMode = true,
                        HedgeMode_CheckOpenPosition = true,
                        HedgeMode_SupportMode = new[] { "Off", "On" },
                        HedgeMode_CantBeHedgeMode = new[] { "Isolated", "Cross", "Portfolio" }
                    },

                    ["USDT_Options"] = new HedgeModePermission
                    {
                        HedgeMode_StandardValue = "On",
                        HedgeMode_CommonMode = true,
                        HedgeMode_CheckOpenPosition = true,
                        HedgeMode_SupportMode = new[] { "Off", "On" },
                        HedgeMode_CantBeHedgeMode = new[] { "Isolated", "Cross", "Portfolio" }
                    }
                };
            }
        }

        public bool MarginMode_IsSupports
        {
            get { return true; }
        }

        public Dictionary<string, MarginModePermission> MarginMode_Permission
        {
            get
            {
                return new Dictionary<string, MarginModePermission>()
                {
                    ["LinearPerpetual"] = new MarginModePermission
                    {
                        MarginMode_StandardValue = "Cross",
                        MarginMode_CommonMode = true,
                        MarginMode_CheckOpenPosition = false,
                        MarginMode_SupportMode = new[] { "Isolated", "Cross", "Portfolio" }
                    },

                    ["LinearPerpetual_PERP"] = new MarginModePermission
                    {
                        MarginMode_StandardValue = "Cross",
                        MarginMode_CommonMode = true,
                        MarginMode_CheckOpenPosition = false,
                        MarginMode_SupportMode = new[] { "Isolated", "Cross", "Portfolio" }
                    },

                    ["InversePerpetual"] = new MarginModePermission
                    {
                        MarginMode_StandardValue = "Cross",
                        MarginMode_CommonMode = true,
                        MarginMode_CheckOpenPosition = false,
                        MarginMode_SupportMode = new[] { "Isolated", "Cross", "Portfolio" }
                    },

                    ["LinearFutures"] = new MarginModePermission
                    {
                        MarginMode_StandardValue = "Cross",
                        MarginMode_CommonMode = true,
                        MarginMode_CheckOpenPosition = false,
                        MarginMode_SupportMode = new[] { "Isolated", "Cross", "Portfolio" }
                    },

                    ["InverseFutures"] = new MarginModePermission
                    {
                        MarginMode_StandardValue = "Cross",
                        MarginMode_CommonMode = true,
                        MarginMode_CheckOpenPosition = false,
                        MarginMode_SupportMode = new[] { "Isolated", "Cross", "Portfolio" }
                    },

                    ["USDT_Options"] = new MarginModePermission
                    {
                        MarginMode_StandardValue = "Cross",
                        MarginMode_CommonMode = true,
                        MarginMode_CheckOpenPosition = false,
                        MarginMode_SupportMode = new[] { "Isolated", "Cross", "Portfolio" }
                    }
                };
            }
        }

        #endregion
    }
}