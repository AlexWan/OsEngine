using System;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.MoexFixFastSpot.FIX
{
    class FIXMessage
    {
        public static Dictionary<string, string> fixDict = new Dictionary<string, string>()
        {
            { "1", "Account" },
            { "6", "AvgPx" },
            { "7", "BeginSeqNo" },
            { "8", "BeginString" },
            { "9", "BodyLength"},
            { "10", "CheckSum"},
            { "11", "ClOrdID"},
            { "12", "Commission" },
            { "13", "CommType" },
            { "14", "CumQty" },
            { "16", "EndSeqNo" },
            { "17", "ExecID" },
            { "31", "LastPx" },
            { "32", "LastQty" },
            { "34", "MsgSeqNum"},
            { "35", "MsgType"},
            { "36", "NewSeqNo" },
            { "37", "OrderID" },
            { "38", "OrderQty" },
            { "39", "OrdStatus" },
            { "40", "OrdType" },
            { "41", "OrigClOrdID" },
            { "44", "Price" },
            { "49", "SenderCompID"},
            { "52", "SendingTime" },
            { "54", "Side" },
            { "55", "Symbol" },
            { "56", "TargetCompID" },
            { "58", "Text" },
            { "60", "TransactTime" },
            { "63", "SettlType" },
            { "64", "SettlDate" },
            { "75", "TradeDate" },
            { "97", "PossResend" },
            { "98", "EncryptMethod"},
            { "103", "OrdRejReason" },
            { "108", "HeartBtInt"},
            { "112", "TestReqID" },
            { "114", "ResetSeqNumFlag"},
            { "123", "GapFillFlag" },
            { "134", "MsgSeqNum"},
            { "136", "NoMiscFees" },
            { "137", "MiscFeeAmt" },
            { "139", "MiscFeeType" },
            { "141", "ResetSeqNumFlag" },
            { "149", "SenderCompID"},
            { "150", "ExecType" },
            { "151", "LeavesQty" },
            { "152", "SendingTime"},
            { "155", "Password"},
            { "156", "TargetCompID"},
            { "159", "AccruedInterestAmt" },
            { "236", "Yield" },
            { "278", "MDEntryID" },
            { "336", "TradingSessionID" },
            { "340", "TradSesStatus" },
            { "369", "LastMsgSeqNumProcessed" },
            { "371", "RefTagID" },
            { "372", "RefMsgType" },
            { "373", "SessionRejectReason" },
            { "378", "ExecRestatementReason" },
            { "381", "GrossTradeAmt" },
            { "447", "PartyIDSource" },
            { "448", "PartyID" },
            { "452", "PartyRole" },
            { "453", "NoPartyID" },
            { "526", "SecondaryClOrdID"},
            { "530", "MassCancelRequestType" },
            { "531", "MassCancelResponse" },
            { "532", "MassCancelRejectReason" },
            { "552", "NoSides" },
            { "553", "Username" },
            { "554", "Password" },
            { "570", "PreviouslyReported" },
            { "571", "TradeReportID" },
            { "625", "TradingSessionSubID" },
            { "828", "TrdType" },
            { "851", "LastLiquidityInd" },
            { "925", "NewPassword" },
            { "1056", "CalculatedCcyLastQty" },
            { "1137", "DefaultApplVerID" },
            { "1180", "ApplID" },
            { "1182", "ApplBeginSeqNo" },
            { "1183", "ApplEndSeqNo" },
            { "1409", "SessionStatus" },
            { "5020", "OptionSettlDate" },
            { "5155", "InstitutionID" },
            { "5459", "OptionSettlType" },
            { "5979", "RequestTime" },
            { "6029", "CurrencyCode" },
            { "6636", "StipulationValue" },
            { "6867", "CancelOnDisconnect" },
            { "6936", "LanguageID" },
            { "7693", "ClientAccID" },
            { "9412", "OrigTime" },
            { "9945", "OrigOrderID" },
            { "18181", "PreMatchedCumQty" },
        };

        public string MessageType { get; set; }
        public long MsgSeqNum { get; set; }
        public Dictionary<string, string> Fields { get; set; }

        public string rawMessage = "";

        public FIXMessage()
        {
            Fields = new Dictionary<string, string>();
        }

        public override string ToString()
        {
            string output = "";
            // Split the message into header, body, and trailer
            string[] parts = rawMessage.Split(new[] { '\u0001' }); // "\u0001" is the start of a new field in FIX
            // Parse the body fields
            for (int i = 0; i < parts.Length - 1; i++)
            {
                string part = parts[i];
                string[] field = part.Split('=');
                if (field.Length == 2)
                {
                    string name = field[0];
                    string value = field[1];
                    name = fixDict.ContainsKey(name) ? fixDict[name] : name;
                    output += name + "=" + value + ", ";
                }
            }
                    
            return $"{MessageType} (output)";
        }

        public static FIXMessage ParseFIXMessage(string message)
        {                   
            // Split the message into header, body, and trailer
            string[] parts = message.Split(new[] { '\u0001' }); // "\u0001" is the start of a new field in FIX

            FIXMessage FIXMessage = new FIXMessage();

            // Parse the body fields
            for (int i = 0; i < parts.Length - 1; i++)
            {
                string part = parts[i];

                int equalsPosition = part.IndexOf('=');

                // Extracting the key and value based on the known maximum key length
                string name = part.Substring(0, equalsPosition);
                string value = part.Substring(equalsPosition + 1);

                name = fixDict.ContainsKey(name) ? fixDict[name] : name;
                if (name == "MsgSeqNum")
                {
                    FIXMessage.MsgSeqNum = long.Parse(value);
                }

                if (name == "MsgType")
                {
                    if (value == "A")
                    {
                        value = "Logon";
                    }

                    if (value == "0")
                    {
                        value = "Heartbeat";
                    }

                    if (value == "1")
                    {
                        value = "TestRequest";
                    }

                    if (value == "2")
                    {
                        value = "ResendRequest";
                    }

                    if (value == "3")
                    {
                        value = "Reject";
                    }

                    if (value == "4")
                    {
                        value = "SequenceReset";
                    }

                    if (value == "5")
                    {
                        value = "Logout";
                    }

                    if (value == "h")
                    {
                        value = "TradingSessionStatus";
                    }

                    if (value == "8")
                    {
                        value = "ExecutionReport";
                    }

                    if (value == "9")
                    {
                        value = "OrderCancelReject";
                    }

                    if (value == "AE")
                    {
                        value = "TradeCaptureReport";
                    }

                    if (value == "V")
                    {
                        value = "MarketDataRequest";
                    }

                    if (value == "F")
                    {
                        value = "OrderCancelRequest";
                    }

                    if (value == "G")
                    {
                        value = "OrderCancelReplaceRequest";
                    }

                    if (value == "r")
                    {
                        value = "OrderMassCancelReport";
                    }

                    FIXMessage.MessageType = value;
                }

                FIXMessage.Fields[name] = value;
            }

            FIXMessage.rawMessage = message;

            return FIXMessage;
        }
    }        
}
