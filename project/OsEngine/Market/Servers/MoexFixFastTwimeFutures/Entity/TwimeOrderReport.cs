using System;
using System.Collections.Generic;
using OsEngine.Entity;

namespace OsEngine.Market.Servers.MoexFixFastTwimeFutures.Entity
{
    public class TwimeOrderReport
    {
        private ushort _messageID;
        private byte[] _msgBytes;
        private ulong ClOrdId;
        private ulong Timestamp;
        private ulong ExpireDate;
        private long OrderId;
        private ulong Flags;
        private ulong Flags2;
        private long Price;
        private int SecurityId;
        private uint OrderQty;
        private int ClOrdLinkID;
        private byte _side;
        private int TradingSessionID;
        private char ComplianceID;
        private long DisplayOrderID;
        private uint DisplayQty;
        private uint DisplayVarianceQty;
        private long PrevOrderID;
        private int TotalAffectedOrders;
        private long TrdMatchID;
        private long LastPx;
        private uint LastQty;
        private DateTime _transactTime;
        private bool parseError = false;

        public TwimeOrderReport(ushort messageID, byte[] msgBytes)
        {
            _messageID = messageID;
            _msgBytes = msgBytes;
        }

        public Order GetOrderReport()
        {
            ParseBytes();

            if (parseError)
                return null;

            Order order = new Order();

            if (_messageID == 7015)
            {
                order.State = OrderStateType.Active;
                order.Price = (decimal)Price / 100000;
                order.NumberMarket = OrderId.ToString();
                order.NumberUser = ClOrdLinkID;
                order.SecurityNameCode = SecurityId.ToString();
                order.Volume = OrderQty;
                order.Side = _side == 1 ? Side.Buy : Side.Sell;
                order.TimeCreate = _transactTime;
                order.TimeCallBack = _transactTime;
            }
            else if (_messageID == 7017)
            {
                order.State = OrderStateType.Cancel;
                order.NumberMarket = OrderId.ToString();
                order.NumberUser = ClOrdLinkID;
                order.Volume = OrderQty;
                order.TimeCancel = _transactTime;
                order.TimeCallBack = _transactTime;
            }
            else if (_messageID == 7018)
            {
                order.State = OrderStateType.Active;
                order.Price = (decimal)Price / 100000;
                order.NumberMarket = OrderId.ToString();
                order.NumberUser = ClOrdLinkID;
                order.SecurityNameCode = SecurityId.ToString();
                order.Volume = OrderQty;
                order.TimeCallBack = _transactTime;
            }

            return order;
        }

        public Order GetOrderReport(out MyTrade trade)
        {
            ParseBytes();

            trade = null;

            if (parseError)
                return null;

            Order order = new Order();

            order.State = OrderStateType.Done;
            order.Price = (decimal)LastPx / 100000;
            order.NumberMarket = OrderId.ToString();
            order.NumberUser = ClOrdLinkID;
            order.SecurityNameCode = SecurityId.ToString();
            order.Volume = LastQty;
            order.Side = _side == 1 ? Side.Buy : Side.Sell;
            order.TimeCallBack = _transactTime;
            order.TimeDone = _transactTime;

            trade = new MyTrade();
            trade.NumberTrade = TrdMatchID.ToString();
            trade.Time = _transactTime;
            trade.Price = (decimal)LastPx / 100000;
            trade.Volume = LastQty;
            trade.Side = _side == 1 ? Side.Buy : Side.Sell;
            trade.NumberOrderParent = OrderId.ToString();

            return order;
        }

        private void ParseBytes()
        {
            try
            {
                ClOrdId = BitConverter.ToUInt64(_msgBytes, 8);
                Timestamp = BitConverter.ToUInt64(_msgBytes, 16);

                ulong unixTimeInSeconds = Timestamp / 1_000_000_000;
                _transactTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixTimeInSeconds);

                if (_messageID == 7015)
                {
                    ExpireDate = BitConverter.ToUInt64(_msgBytes, 24);
                    OrderId = BitConverter.ToInt64(_msgBytes, 32);
                    Flags = BitConverter.ToUInt64(_msgBytes, 40);
                    Flags2 = BitConverter.ToUInt64(_msgBytes, 48);
                    Price = BitConverter.ToInt64(_msgBytes, 56);
                    SecurityId = BitConverter.ToInt32(_msgBytes, 64);
                    OrderQty = BitConverter.ToUInt32(_msgBytes, 68);
                    TradingSessionID = BitConverter.ToInt32(_msgBytes, 72);
                    ClOrdLinkID = BitConverter.ToInt32(_msgBytes, 76);
                    _side = _msgBytes[80];
                    ComplianceID = BitConverter.ToChar(_msgBytes, 81);
                }
                else if (_messageID == 7016)
                {
                    ExpireDate = BitConverter.ToUInt64(_msgBytes, 24);
                    OrderId = BitConverter.ToInt64(_msgBytes, 32);
                    DisplayOrderID = BitConverter.ToInt64(_msgBytes, 40);
                    Flags = BitConverter.ToUInt64(_msgBytes, 48);
                    Flags2 = BitConverter.ToUInt64(_msgBytes, 56);
                    Price = BitConverter.ToInt64(_msgBytes, 64);
                    SecurityId = BitConverter.ToInt32(_msgBytes, 68);
                    OrderQty = BitConverter.ToUInt32(_msgBytes, 72);
                    DisplayQty = BitConverter.ToUInt32(_msgBytes, 76);
                    DisplayVarianceQty = BitConverter.ToUInt32(_msgBytes, 80);
                    TradingSessionID = BitConverter.ToInt32(_msgBytes, 84);
                    ClOrdLinkID = BitConverter.ToInt32(_msgBytes, 88);
                    _side = _msgBytes[92];
                    ComplianceID = BitConverter.ToChar(_msgBytes, 93);
                }
                else if (_messageID == 7017)
                {
                    OrderId = BitConverter.ToInt64(_msgBytes, 24);
                    Flags = BitConverter.ToUInt64(_msgBytes, 32);
                    Flags2 = BitConverter.ToUInt64(_msgBytes, 40);
                    OrderQty = BitConverter.ToUInt32(_msgBytes, 48);
                    TradingSessionID = BitConverter.ToInt32(_msgBytes, 52);
                    ClOrdLinkID = BitConverter.ToInt32(_msgBytes, 56);
                }
                else if (_messageID == 7018)
                {
                    OrderId = BitConverter.ToInt64(_msgBytes, 24);
                    PrevOrderID = BitConverter.ToInt64(_msgBytes, 32);
                    Flags = BitConverter.ToUInt64(_msgBytes, 40);
                    Flags2 = BitConverter.ToUInt64(_msgBytes, 48);
                    Price = BitConverter.ToInt64(_msgBytes, 56);
                    OrderQty = BitConverter.ToUInt32(_msgBytes, 64);
                    TradingSessionID = BitConverter.ToInt32(_msgBytes, 68);
                    ClOrdLinkID = BitConverter.ToInt32(_msgBytes, 72);
                    ComplianceID = BitConverter.ToChar(_msgBytes, 76);
                }
                else if (_messageID == 7019)
                {
                    OrderId = BitConverter.ToInt64(_msgBytes, 24);
                    TrdMatchID = BitConverter.ToInt64(_msgBytes, 32);
                    Flags = BitConverter.ToUInt64(_msgBytes, 40);
                    Flags2 = BitConverter.ToUInt64(_msgBytes, 48);
                    LastPx = BitConverter.ToInt64(_msgBytes, 56);
                    LastQty = BitConverter.ToUInt32(_msgBytes, 64);
                    OrderQty = BitConverter.ToUInt32(_msgBytes, 68);
                    TradingSessionID = BitConverter.ToInt32(_msgBytes, 72);
                    ClOrdLinkID = BitConverter.ToInt32(_msgBytes, 76);
                    SecurityId = BitConverter.ToInt32(_msgBytes, 80);
                    _side = _msgBytes[84];
                }
                else
                {
                    parseError = true;
                }
            }
            catch (Exception)
            {
                parseError = true;
            }
        }
    }
}
