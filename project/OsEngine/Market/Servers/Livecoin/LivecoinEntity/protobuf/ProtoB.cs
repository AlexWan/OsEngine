using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace protobuf.ws
{

    [global::ProtoBuf.ProtoContract()]
    public partial class SubscribeTickerChannelRequest : ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"currency_pair", IsRequired = true)]
        public string CurrencyPair { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"frequency")]
        public float Frequency
        {
            get { return __pbn__Frequency.GetValueOrDefault(); }
            set { __pbn__Frequency = value; }
        }
        public bool ShouldSerializeFrequency() => __pbn__Frequency != null;
        public void ResetFrequency() => __pbn__Frequency = null;
        private float? __pbn__Frequency;

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class SubscribeOrderBookRawChannelRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"currency_pair", IsRequired = true)]
        public string CurrencyPair { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"depth")]
        public int Depth
        {
            get { return __pbn__Depth.GetValueOrDefault(); }
            set { __pbn__Depth = value; }
        }
        public bool ShouldSerializeDepth() => __pbn__Depth != null;
        public void ResetDepth() => __pbn__Depth = null;
        private int? __pbn__Depth;

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class SubscribeOrderBookChannelRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"currency_pair", IsRequired = true)]
        public string CurrencyPair { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"depth")]
        public int Depth
        {
            get { return __pbn__Depth.GetValueOrDefault(); }
            set { __pbn__Depth = value; }
        }
        public bool ShouldSerializeDepth() => __pbn__Depth != null;
        public void ResetDepth() => __pbn__Depth = null;
        private int? __pbn__Depth;

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class SubscribeTradeChannelRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"currency_pair", IsRequired = true)]
        public string CurrencyPair { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class SubscribeCandleChannelRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"currency_pair", IsRequired = true)]
        public string CurrencyPair { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"interval", IsRequired = true)]
        public CandleInterval Interval { get; set; } = CandleInterval.Candle1Minute;

        [global::ProtoBuf.ProtoMember(3, Name = @"depth")]
        [global::System.ComponentModel.DefaultValue(0)]
        public int Depth
        {
            get { return __pbn__Depth ?? 0; }
            set { __pbn__Depth = value; }
        }
        public bool ShouldSerializeDepth() => __pbn__Depth != null;
        public void ResetDepth() => __pbn__Depth = null;
        private int? __pbn__Depth;

        [global::ProtoBuf.ProtoContract()]
        public enum CandleInterval
        {
            [global::ProtoBuf.ProtoEnum(Name = @"CANDLE_1_MINUTE")]
            Candle1Minute = 1,
        }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class UnsubscribeRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, IsRequired = true)]
        public ChannelType channel_type { get; set; } = ChannelType.Ticker;

        [global::ProtoBuf.ProtoMember(2, Name = @"currency_pair", IsRequired = true)]
        public string CurrencyPair { get; set; }

        [global::ProtoBuf.ProtoContract()]
        public enum ChannelType
        {
            [global::ProtoBuf.ProtoEnum(Name = @"TICKER")]
            Ticker = 1,
            [global::ProtoBuf.ProtoEnum(Name = @"ORDER_BOOK_RAW")]
            OrderBookRaw = 2,
            [global::ProtoBuf.ProtoEnum(Name = @"ORDER_BOOK")]
            OrderBook = 3,
            [global::ProtoBuf.ProtoEnum(Name = @"TRADE")]
            Trade = 4,
            [global::ProtoBuf.ProtoEnum(Name = @"CANDLE")]
            Candle = 5,
        }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class RequestExpired : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"now", IsRequired = true)]
        public long Now { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"ttl", IsRequired = true)]
        public int Ttl { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class LoginRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"expire_control", IsRequired = true)]
        public RequestExpired ExpireControl { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"api_key", IsRequired = true)]
        public string ApiKey { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class PutLimitOrderRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"expire_control", IsRequired = true)]
        public RequestExpired ExpireControl { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"currency_pair", IsRequired = true)]
        public string CurrencyPair { get; set; }

        [global::ProtoBuf.ProtoMember(3, IsRequired = true)]
        public OrderType order_type { get; set; } = OrderType.Bid;

        [global::ProtoBuf.ProtoMember(4, Name = @"amount", IsRequired = true)]
        public string Amount { get; set; }

        [global::ProtoBuf.ProtoMember(5, Name = @"price", IsRequired = true)]
        public string Price { get; set; }

        [global::ProtoBuf.ProtoContract()]
        public enum OrderType
        {
            [global::ProtoBuf.ProtoEnum(Name = @"BID")]
            Bid = 1,
            [global::ProtoBuf.ProtoEnum(Name = @"ASK")]
            Ask = 2,
        }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class CancelLimitOrderRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"expire_control", IsRequired = true)]
        public RequestExpired ExpireControl { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"currency_pair", IsRequired = true)]
        public string CurrencyPair { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"id", IsRequired = true)]
        public long Id { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class BalanceRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"expire_control", IsRequired = true)]
        public RequestExpired ExpireControl { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"currency", IsRequired = true)]
        public string Currency { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class BalancesRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"expire_control", IsRequired = true)]
        public RequestExpired ExpireControl { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"currency")]
        [global::System.ComponentModel.DefaultValue("")]
        public string Currency
        {
            get { return __pbn__Currency ?? ""; }
            set { __pbn__Currency = value; }
        }
        public bool ShouldSerializeCurrency() => __pbn__Currency != null;
        public void ResetCurrency() => __pbn__Currency = null;
        private string __pbn__Currency;

        [global::ProtoBuf.ProtoMember(3, Name = @"only_not_zero")]
        public bool OnlyNotZero
        {
            get { return __pbn__OnlyNotZero.GetValueOrDefault(); }
            set { __pbn__OnlyNotZero = value; }
        }
        public bool ShouldSerializeOnlyNotZero() => __pbn__OnlyNotZero != null;
        public void ResetOnlyNotZero() => __pbn__OnlyNotZero = null;
        private bool? __pbn__OnlyNotZero;

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class LastTradesRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"expire_control", IsRequired = true)]
        public RequestExpired ExpireControl { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"currency_pair", IsRequired = true)]
        public string CurrencyPair { get; set; }

        [global::ProtoBuf.ProtoMember(3)]
        [global::System.ComponentModel.DefaultValue(Interval.Minute)]
        public Interval interval
        {
            get { return __pbn__interval ?? Interval.Minute; }
            set { __pbn__interval = value; }
        }
        public bool ShouldSerializeinterval() => __pbn__interval != null;
        public void Resetinterval() => __pbn__interval = null;
        private Interval? __pbn__interval;

        [global::ProtoBuf.ProtoMember(4)]
        [global::System.ComponentModel.DefaultValue(TradeType.Sell)]
        public TradeType trade_type
        {
            get { return __pbn__trade_type ?? TradeType.Sell; }
            set { __pbn__trade_type = value; }
        }
        public bool ShouldSerializetrade_type() => __pbn__trade_type != null;
        public void Resettrade_type() => __pbn__trade_type = null;
        private TradeType? __pbn__trade_type;

        [global::ProtoBuf.ProtoContract()]
        public enum TradeType
        {
            [global::ProtoBuf.ProtoEnum(Name = @"SELL")]
            Sell = 1,
            [global::ProtoBuf.ProtoEnum(Name = @"BUY")]
            Buy = 2,
        }

        [global::ProtoBuf.ProtoContract()]
        public enum Interval
        {
            [global::ProtoBuf.ProtoEnum(Name = @"MINUTE")]
            Minute = 1,
            [global::ProtoBuf.ProtoEnum(Name = @"HOUR")]
            Hour = 2,
        }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class TradesRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"expire_control", IsRequired = true)]
        public RequestExpired ExpireControl { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"currency_pair")]
        [global::System.ComponentModel.DefaultValue("")]
        public string CurrencyPair
        {
            get { return __pbn__CurrencyPair ?? ""; }
            set { __pbn__CurrencyPair = value; }
        }
        public bool ShouldSerializeCurrencyPair() => __pbn__CurrencyPair != null;
        public void ResetCurrencyPair() => __pbn__CurrencyPair = null;
        private string __pbn__CurrencyPair;

        [global::ProtoBuf.ProtoMember(3)]
        [global::System.ComponentModel.DefaultValue(Direction.Asc)]
        public Direction direction
        {
            get { return __pbn__direction ?? Direction.Asc; }
            set { __pbn__direction = value; }
        }
        public bool ShouldSerializedirection() => __pbn__direction != null;
        public void Resetdirection() => __pbn__direction = null;
        private Direction? __pbn__direction;

        [global::ProtoBuf.ProtoMember(4, Name = @"offset")]
        public int Offset
        {
            get { return __pbn__Offset.GetValueOrDefault(); }
            set { __pbn__Offset = value; }
        }
        public bool ShouldSerializeOffset() => __pbn__Offset != null;
        public void ResetOffset() => __pbn__Offset = null;
        private int? __pbn__Offset;

        [global::ProtoBuf.ProtoMember(5, Name = @"limit")]
        public int Limit
        {
            get { return __pbn__Limit.GetValueOrDefault(); }
            set { __pbn__Limit = value; }
        }
        public bool ShouldSerializeLimit() => __pbn__Limit != null;
        public void ResetLimit() => __pbn__Limit = null;
        private int? __pbn__Limit;

        [global::ProtoBuf.ProtoContract()]
        public enum Direction
        {
            [global::ProtoBuf.ProtoEnum(Name = @"ASC")]
            Asc = 1,
            [global::ProtoBuf.ProtoEnum(Name = @"DESC")]
            Desc = 2,
        }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class ClientOrdersRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"expire_control", IsRequired = true)]
        public RequestExpired ExpireControl { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"currency_pair")]
        [global::System.ComponentModel.DefaultValue("")]
        public string CurrencyPair
        {
            get { return __pbn__CurrencyPair ?? ""; }
            set { __pbn__CurrencyPair = value; }
        }
        public bool ShouldSerializeCurrencyPair() => __pbn__CurrencyPair != null;
        public void ResetCurrencyPair() => __pbn__CurrencyPair = null;
        private string __pbn__CurrencyPair;

        [global::ProtoBuf.ProtoMember(3, Name = @"status")]
        [global::System.ComponentModel.DefaultValue(OrderStatus.Open)]
        public OrderStatus Status
        {
            get { return __pbn__Status ?? OrderStatus.Open; }
            set { __pbn__Status = value; }
        }
        public bool ShouldSerializeStatus() => __pbn__Status != null;
        public void ResetStatus() => __pbn__Status = null;
        private OrderStatus? __pbn__Status;

        [global::ProtoBuf.ProtoMember(4, Name = @"issued_from")]
        public long IssuedFrom
        {
            get { return __pbn__IssuedFrom.GetValueOrDefault(); }
            set { __pbn__IssuedFrom = value; }
        }
        public bool ShouldSerializeIssuedFrom() => __pbn__IssuedFrom != null;
        public void ResetIssuedFrom() => __pbn__IssuedFrom = null;
        private long? __pbn__IssuedFrom;

        [global::ProtoBuf.ProtoMember(5, Name = @"issued_to")]
        public long IssuedTo
        {
            get { return __pbn__IssuedTo.GetValueOrDefault(); }
            set { __pbn__IssuedTo = value; }
        }
        public bool ShouldSerializeIssuedTo() => __pbn__IssuedTo != null;
        public void ResetIssuedTo() => __pbn__IssuedTo = null;
        private long? __pbn__IssuedTo;

        [global::ProtoBuf.ProtoMember(6)]
        [global::System.ComponentModel.DefaultValue(OrderType.Bid)]
        public OrderType order_type
        {
            get { return __pbn__order_type ?? OrderType.Bid; }
            set { __pbn__order_type = value; }
        }
        public bool ShouldSerializeorder_type() => __pbn__order_type != null;
        public void Resetorder_type() => __pbn__order_type = null;
        private OrderType? __pbn__order_type;

        [global::ProtoBuf.ProtoMember(7, Name = @"start_row")]
        public int StartRow
        {
            get { return __pbn__StartRow.GetValueOrDefault(); }
            set { __pbn__StartRow = value; }
        }
        public bool ShouldSerializeStartRow() => __pbn__StartRow != null;
        public void ResetStartRow() => __pbn__StartRow = null;
        private int? __pbn__StartRow;

        [global::ProtoBuf.ProtoMember(8, Name = @"end_row")]
        public int EndRow
        {
            get { return __pbn__EndRow.GetValueOrDefault(); }
            set { __pbn__EndRow = value; }
        }
        public bool ShouldSerializeEndRow() => __pbn__EndRow != null;
        public void ResetEndRow() => __pbn__EndRow = null;
        private int? __pbn__EndRow;

        [global::ProtoBuf.ProtoContract()]
        public enum OrderStatus
        {
            [global::ProtoBuf.ProtoEnum(Name = @"OPEN")]
            Open = 1,
            [global::ProtoBuf.ProtoEnum(Name = @"CLOSED")]
            Closed = 2,
            [global::ProtoBuf.ProtoEnum(Name = @"CANCELLED")]
            Cancelled = 4,
            [global::ProtoBuf.ProtoEnum(Name = @"PARTIALLY")]
            Partially = 5,
            [global::ProtoBuf.ProtoEnum(Name = @"NOT_CANCELLED")]
            NotCancelled = 6,
        }

        [global::ProtoBuf.ProtoContract()]
        public enum OrderType
        {
            [global::ProtoBuf.ProtoEnum(Name = @"BID")]
            Bid = 1,
            [global::ProtoBuf.ProtoEnum(Name = @"ASK")]
            Ask = 2,
        }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class ClientOrderRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"expire_control", IsRequired = true)]
        public RequestExpired ExpireControl { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"order_id", IsRequired = true)]
        public long OrderId { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"currency_pair", IsRequired = true)]
        public string CurrencyPair { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class CommissionRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"expire_control", IsRequired = true)]
        public RequestExpired ExpireControl { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class CommissionCommonInfoRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"expire_control", IsRequired = true)]
        public RequestExpired ExpireControl { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class TradeHistoryRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"expire_control", IsRequired = true)]
        public RequestExpired ExpireControl { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"start", IsRequired = true)]
        public long Start { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"end", IsRequired = true)]
        public long End { get; set; }

        [global::ProtoBuf.ProtoMember(4)]
        public global::System.Collections.Generic.List<Types> types { get; } = new global::System.Collections.Generic.List<Types>();

        [global::ProtoBuf.ProtoMember(5, Name = @"limit")]
        public int Limit
        {
            get { return __pbn__Limit.GetValueOrDefault(); }
            set { __pbn__Limit = value; }
        }
        public bool ShouldSerializeLimit() => __pbn__Limit != null;
        public void ResetLimit() => __pbn__Limit = null;
        private int? __pbn__Limit;

        [global::ProtoBuf.ProtoMember(6, Name = @"offset")]
        public int Offset
        {
            get { return __pbn__Offset.GetValueOrDefault(); }
            set { __pbn__Offset = value; }
        }
        public bool ShouldSerializeOffset() => __pbn__Offset != null;
        public void ResetOffset() => __pbn__Offset = null;
        private int? __pbn__Offset;

        [global::ProtoBuf.ProtoContract()]
        public enum Types
        {
            [global::ProtoBuf.ProtoEnum(Name = @"BUY")]
            Buy = 1,
            [global::ProtoBuf.ProtoEnum(Name = @"SELL")]
            Sell = 2,
            [global::ProtoBuf.ProtoEnum(Name = @"DEPOSIT")]
            Deposit = 3,
            [global::ProtoBuf.ProtoEnum(Name = @"WITHDRAWAL")]
            Withdrawal = 4,
            [global::ProtoBuf.ProtoEnum(Name = @"BET")]
            Bet = 5,
            [global::ProtoBuf.ProtoEnum(Name = @"RETRIEVE")]
            Retrieve = 6,
            [global::ProtoBuf.ProtoEnum(Name = @"PRIZE")]
            Prize = 7,
            [global::ProtoBuf.ProtoEnum(Name = @"REFERRAL_BET")]
            ReferralBet = 8,
            [global::ProtoBuf.ProtoEnum(Name = @"REFERRAL")]
            Referral = 9,
            [global::ProtoBuf.ProtoEnum(Name = @"DEPOSIT_VOUCHER")]
            DepositVoucher = 10,
            [global::ProtoBuf.ProtoEnum(Name = @"WITHDRAWAL_VOUCHER")]
            WithdrawalVoucher = 11,
        }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class MarkerOrderRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"expire_control", IsRequired = true)]
        public RequestExpired ExpireControl { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"currency_pair", IsRequired = true)]
        public string CurrencyPair { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"amount", IsRequired = true)]
        public string Amount { get; set; }

        [global::ProtoBuf.ProtoMember(4, IsRequired = true)]
        public ClientOrdersRequest.OrderType orderType { get; set; } = ClientOrdersRequest.OrderType.Bid;

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class WalletAddressRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"expire_control", IsRequired = true)]
        public RequestExpired ExpireControl { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"currency", IsRequired = true)]
        public string Currency { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class WithdrawalCoinRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"expire_control", IsRequired = true)]
        public RequestExpired ExpireControl { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"wallet", IsRequired = true)]
        public string Wallet { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"currency", IsRequired = true)]
        public string Currency { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"amount", IsRequired = true)]
        public string Amount { get; set; }

        [global::ProtoBuf.ProtoMember(5, Name = @"description")]
        [global::System.ComponentModel.DefaultValue("")]
        public string Description
        {
            get { return __pbn__Description ?? ""; }
            set { __pbn__Description = value; }
        }
        public bool ShouldSerializeDescription() => __pbn__Description != null;
        public void ResetDescription() => __pbn__Description = null;
        private string __pbn__Description;

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class WithdrawalPayeerRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"expire_control", IsRequired = true)]
        public RequestExpired ExpireControl { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"wallet", IsRequired = true)]
        public string Wallet { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"currency", IsRequired = true)]
        public string Currency { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"amount", IsRequired = true)]
        public string Amount { get; set; }

        [global::ProtoBuf.ProtoMember(5, Name = @"protect")]
        [global::System.ComponentModel.DefaultValue("")]
        public string Protect
        {
            get { return __pbn__Protect ?? ""; }
            set { __pbn__Protect = value; }
        }
        public bool ShouldSerializeProtect() => __pbn__Protect != null;
        public void ResetProtect() => __pbn__Protect = null;
        private string __pbn__Protect;

        [global::ProtoBuf.ProtoMember(6)]
        public int protectPeriod
        {
            get { return __pbn__protectPeriod.GetValueOrDefault(); }
            set { __pbn__protectPeriod = value; }
        }
        public bool ShouldSerializeprotectPeriod() => __pbn__protectPeriod != null;
        public void ResetprotectPeriod() => __pbn__protectPeriod = null;
        private int? __pbn__protectPeriod;

        [global::ProtoBuf.ProtoMember(7)]
        [global::System.ComponentModel.DefaultValue("")]
        public string protectCode
        {
            get { return __pbn__protectCode ?? ""; }
            set { __pbn__protectCode = value; }
        }
        public bool ShouldSerializeprotectCode() => __pbn__protectCode != null;
        public void ResetprotectCode() => __pbn__protectCode = null;
        private string __pbn__protectCode;

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class WithdrawalCapitalistRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"expire_control", IsRequired = true)]
        public RequestExpired ExpireControl { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"wallet", IsRequired = true)]
        public string Wallet { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"currency", IsRequired = true)]
        public string Currency { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"amount", IsRequired = true)]
        public string Amount { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class WithdrawalAdvcashRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"expire_control", IsRequired = true)]
        public RequestExpired ExpireControl { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"wallet", IsRequired = true)]
        public string Wallet { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"currency", IsRequired = true)]
        public string Currency { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"amount", IsRequired = true)]
        public string Amount { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class WithdrawalYandexRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"expire_control", IsRequired = true)]
        public RequestExpired ExpireControl { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"wallet", IsRequired = true)]
        public string Wallet { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"currency", IsRequired = true)]
        public string Currency { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"amount", IsRequired = true)]
        public string Amount { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class WithdrawalQiwiRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"expire_control", IsRequired = true)]
        public RequestExpired ExpireControl { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"wallet", IsRequired = true)]
        public string Wallet { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"currency", IsRequired = true)]
        public string Currency { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"amount", IsRequired = true)]
        public string Amount { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class WithdrawalCardRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"expire_control", IsRequired = true)]
        public RequestExpired ExpireControl { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"account", IsRequired = true)]
        public string Account { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"currency", IsRequired = true)]
        public string Currency { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"amount", IsRequired = true)]
        public string Amount { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class WithdrawalMastercardRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"expire_control", IsRequired = true)]
        public RequestExpired ExpireControl { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"currency", IsRequired = true)]
        public string Currency { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"amount", IsRequired = true)]
        public string Amount { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"card_number", IsRequired = true)]
        public string CardNumber { get; set; }

        [global::ProtoBuf.ProtoMember(5, Name = @"card_holder", IsRequired = true)]
        public string CardHolder { get; set; }

        [global::ProtoBuf.ProtoMember(6, Name = @"card_holder_country", IsRequired = true)]
        public string CardHolderCountry { get; set; }

        [global::ProtoBuf.ProtoMember(7, Name = @"card_holder_city", IsRequired = true)]
        public string CardHolderCity { get; set; }

        [global::ProtoBuf.ProtoMember(8, Name = @"card_holder_dob", IsRequired = true)]
        public string CardHolderDob { get; set; }

        [global::ProtoBuf.ProtoMember(9, Name = @"card_holder_mobile_phone", IsRequired = true)]
        public string CardHolderMobilePhone { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class WithdrawalPerfectMoneyRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"expire_control", IsRequired = true)]
        public RequestExpired ExpireControl { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"wallet", IsRequired = true)]
        public string Wallet { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"currency", IsRequired = true)]
        public string Currency { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"amount", IsRequired = true)]
        public string Amount { get; set; }

        [global::ProtoBuf.ProtoMember(5, Name = @"protect_code")]
        [global::System.ComponentModel.DefaultValue("")]
        public string ProtectCode
        {
            get { return __pbn__ProtectCode ?? ""; }
            set { __pbn__ProtectCode = value; }
        }
        public bool ShouldSerializeProtectCode() => __pbn__ProtectCode != null;
        public void ResetProtectCode() => __pbn__ProtectCode = null;
        private string __pbn__ProtectCode;

        [global::ProtoBuf.ProtoMember(6, Name = @"protect_period")]
        public int ProtectPeriod
        {
            get { return __pbn__ProtectPeriod.GetValueOrDefault(); }
            set { __pbn__ProtectPeriod = value; }
        }
        public bool ShouldSerializeProtectPeriod() => __pbn__ProtectPeriod != null;
        public void ResetProtectPeriod() => __pbn__ProtectPeriod = null;
        private int? __pbn__ProtectPeriod;

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class VoucherMakeRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"expire_control", IsRequired = true)]
        public RequestExpired ExpireControl { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"description", IsRequired = true)]
        public string Description { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"currency", IsRequired = true)]
        public string Currency { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"amount", IsRequired = true)]
        public string Amount { get; set; }

        [global::ProtoBuf.ProtoMember(5, Name = @"for_user")]
        [global::System.ComponentModel.DefaultValue("")]
        public string ForUser
        {
            get { return __pbn__ForUser ?? ""; }
            set { __pbn__ForUser = value; }
        }
        public bool ShouldSerializeForUser() => __pbn__ForUser != null;
        public void ResetForUser() => __pbn__ForUser = null;
        private string __pbn__ForUser;

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class VoucherAmountRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"expire_control", IsRequired = true)]
        public RequestExpired ExpireControl { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"voucher_code", IsRequired = true)]
        public string VoucherCode { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class VoucherRedeemRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"expire_control", IsRequired = true)]
        public RequestExpired ExpireControl { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"voucher_code", IsRequired = true)]
        public string VoucherCode { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class CancelOrdersRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"expire_control", IsRequired = true)]
        public RequestExpired ExpireControl { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"currency_pairs")]
        public global::System.Collections.Generic.List<string> CurrencyPairs { get; } = new global::System.Collections.Generic.List<string>();

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class PingRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class TickerEvent : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"timestamp", IsRequired = true)]
        public long Timestamp { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"last")]
        [global::System.ComponentModel.DefaultValue("")]
        public string Last
        {
            get { return __pbn__Last ?? ""; }
            set { __pbn__Last = value; }
        }
        public bool ShouldSerializeLast() => __pbn__Last != null;
        public void ResetLast() => __pbn__Last = null;
        private string __pbn__Last;

        [global::ProtoBuf.ProtoMember(3, Name = @"high")]
        [global::System.ComponentModel.DefaultValue("")]
        public string High
        {
            get { return __pbn__High ?? ""; }
            set { __pbn__High = value; }
        }
        public bool ShouldSerializeHigh() => __pbn__High != null;
        public void ResetHigh() => __pbn__High = null;
        private string __pbn__High;

        [global::ProtoBuf.ProtoMember(4, Name = @"low")]
        [global::System.ComponentModel.DefaultValue("")]
        public string Low
        {
            get { return __pbn__Low ?? ""; }
            set { __pbn__Low = value; }
        }
        public bool ShouldSerializeLow() => __pbn__Low != null;
        public void ResetLow() => __pbn__Low = null;
        private string __pbn__Low;

        [global::ProtoBuf.ProtoMember(5, Name = @"volume")]
        [global::System.ComponentModel.DefaultValue("")]
        public string Volume
        {
            get { return __pbn__Volume ?? ""; }
            set { __pbn__Volume = value; }
        }
        public bool ShouldSerializeVolume() => __pbn__Volume != null;
        public void ResetVolume() => __pbn__Volume = null;
        private string __pbn__Volume;

        [global::ProtoBuf.ProtoMember(6, Name = @"vwap")]
        [global::System.ComponentModel.DefaultValue("")]
        public string Vwap
        {
            get { return __pbn__Vwap ?? ""; }
            set { __pbn__Vwap = value; }
        }
        public bool ShouldSerializeVwap() => __pbn__Vwap != null;
        public void ResetVwap() => __pbn__Vwap = null;
        private string __pbn__Vwap;

        [global::ProtoBuf.ProtoMember(7, Name = @"max_bid")]
        [global::System.ComponentModel.DefaultValue("")]
        public string MaxBid
        {
            get { return __pbn__MaxBid ?? ""; }
            set { __pbn__MaxBid = value; }
        }
        public bool ShouldSerializeMaxBid() => __pbn__MaxBid != null;
        public void ResetMaxBid() => __pbn__MaxBid = null;
        private string __pbn__MaxBid;

        [global::ProtoBuf.ProtoMember(8, Name = @"min_ask")]
        [global::System.ComponentModel.DefaultValue("")]
        public string MinAsk
        {
            get { return __pbn__MinAsk ?? ""; }
            set { __pbn__MinAsk = value; }
        }
        public bool ShouldSerializeMinAsk() => __pbn__MinAsk != null;
        public void ResetMinAsk() => __pbn__MinAsk = null;
        private string __pbn__MinAsk;

        [global::ProtoBuf.ProtoMember(9, Name = @"best_bid")]
        [global::System.ComponentModel.DefaultValue("")]
        public string BestBid
        {
            get { return __pbn__BestBid ?? ""; }
            set { __pbn__BestBid = value; }
        }
        public bool ShouldSerializeBestBid() => __pbn__BestBid != null;
        public void ResetBestBid() => __pbn__BestBid = null;
        private string __pbn__BestBid;

        [global::ProtoBuf.ProtoMember(10, Name = @"best_ask")]
        [global::System.ComponentModel.DefaultValue("")]
        public string BestAsk
        {
            get { return __pbn__BestAsk ?? ""; }
            set { __pbn__BestAsk = value; }
        }
        public bool ShouldSerializeBestAsk() => __pbn__BestAsk != null;
        public void ResetBestAsk() => __pbn__BestAsk = null;
        private string __pbn__BestAsk;

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class TickerChannelSubscribedResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"currency_pair", IsRequired = true)]
        public string CurrencyPair { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"data")]
        public global::System.Collections.Generic.List<TickerEvent> Datas { get; } = new global::System.Collections.Generic.List<TickerEvent>();

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class OrderBookRawEvent : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, IsRequired = true)]
        public OrderType order_type { get; set; } = OrderType.Bid;

        [global::ProtoBuf.ProtoMember(2, Name = @"id", IsRequired = true)]
        public long Id { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"timestamp", IsRequired = true)]
        public long Timestamp { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"price")]
        [global::System.ComponentModel.DefaultValue("")]
        public string Price
        {
            get { return __pbn__Price ?? ""; }
            set { __pbn__Price = value; }
        }
        public bool ShouldSerializePrice() => __pbn__Price != null;
        public void ResetPrice() => __pbn__Price = null;
        private string __pbn__Price;

        [global::ProtoBuf.ProtoMember(5, Name = @"quantity", IsRequired = true)]
        public string Quantity { get; set; }

        [global::ProtoBuf.ProtoContract()]
        public enum OrderType
        {
            [global::ProtoBuf.ProtoEnum(Name = @"BID")]
            Bid = 1,
            [global::ProtoBuf.ProtoEnum(Name = @"ASK")]
            Ask = 2,
        }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class OrderBookRawChannelSubscribedResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"currency_pair", IsRequired = true)]
        public string CurrencyPair { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"data")]
        public global::System.Collections.Generic.List<OrderBookRawEvent> Datas { get; } = new global::System.Collections.Generic.List<OrderBookRawEvent>();

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class OrderBookEvent : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, IsRequired = true)]
        public OrderType order_type { get; set; } = OrderType.Bid;

        [global::ProtoBuf.ProtoMember(2, Name = @"timestamp", IsRequired = true)]
        public long Timestamp { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"price", IsRequired = true)]
        public string Price { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"quantity", IsRequired = true)]
        public string Quantity { get; set; }

        [global::ProtoBuf.ProtoContract()]
        public enum OrderType
        {
            [global::ProtoBuf.ProtoEnum(Name = @"BID")]
            Bid = 1,
            [global::ProtoBuf.ProtoEnum(Name = @"ASK")]
            Ask = 2,
        }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class OrderBookChannelSubscribedResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"currency_pair", IsRequired = true)]
        public string CurrencyPair { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"data")]
        public global::System.Collections.Generic.List<OrderBookEvent> Datas { get; } = new global::System.Collections.Generic.List<OrderBookEvent>();

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class TradeEvent : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"id", IsRequired = true)]
        public long Id { get; set; }

        [global::ProtoBuf.ProtoMember(2, IsRequired = true)]
        public TradeType trade_type { get; set; } = TradeType.Buy;

        [global::ProtoBuf.ProtoMember(3, Name = @"timestamp", IsRequired = true)]
        public long Timestamp { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"price", IsRequired = true)]
        public string Price { get; set; }

        [global::ProtoBuf.ProtoMember(5, Name = @"quantity", IsRequired = true)]
        public string Quantity { get; set; }

        [global::ProtoBuf.ProtoMember(6, Name = @"order_buy_id")]
        public long OrderBuyId
        {
            get { return __pbn__OrderBuyId.GetValueOrDefault(); }
            set { __pbn__OrderBuyId = value; }
        }
        public bool ShouldSerializeOrderBuyId() => __pbn__OrderBuyId != null;
        public void ResetOrderBuyId() => __pbn__OrderBuyId = null;
        private long? __pbn__OrderBuyId;

        [global::ProtoBuf.ProtoMember(7, Name = @"order_sell_id")]
        public long OrderSellId
        {
            get { return __pbn__OrderSellId.GetValueOrDefault(); }
            set { __pbn__OrderSellId = value; }
        }
        public bool ShouldSerializeOrderSellId() => __pbn__OrderSellId != null;
        public void ResetOrderSellId() => __pbn__OrderSellId = null;
        private long? __pbn__OrderSellId;

        [global::ProtoBuf.ProtoContract()]
        public enum TradeType
        {
            [global::ProtoBuf.ProtoEnum(Name = @"BUY")]
            Buy = 1,
            [global::ProtoBuf.ProtoEnum(Name = @"SELL")]
            Sell = 2,
        }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class TradeChannelSubscribedResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"currency_pair", IsRequired = true)]
        public string CurrencyPair { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"data")]
        public global::System.Collections.Generic.List<TradeEvent> Datas { get; } = new global::System.Collections.Generic.List<TradeEvent>();

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class CandleEvent : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"timestamp", IsRequired = true)]
        public long Timestamp { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"open_price", IsRequired = true)]
        public string OpenPrice { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"close_price", IsRequired = true)]
        public string ClosePrice { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"high_price", IsRequired = true)]
        public string HighPrice { get; set; }

        [global::ProtoBuf.ProtoMember(5, Name = @"low_price", IsRequired = true)]
        public string LowPrice { get; set; }

        [global::ProtoBuf.ProtoMember(6, Name = @"volume", IsRequired = true)]
        public string Volume { get; set; }

        [global::ProtoBuf.ProtoMember(7, Name = @"quoted_volume", IsRequired = true)]
        public string QuotedVolume { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class CandleChannelSubscribedResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"currency_pair", IsRequired = true)]
        public string CurrencyPair { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"interval", IsRequired = true)]
        public SubscribeCandleChannelRequest.CandleInterval Interval { get; set; } = SubscribeCandleChannelRequest.CandleInterval.Candle1Minute;

        [global::ProtoBuf.ProtoMember(3, Name = @"data")]
        public global::System.Collections.Generic.List<CandleEvent> Datas { get; } = new global::System.Collections.Generic.List<CandleEvent>();

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class ChannelUnsubscribedResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"type", IsRequired = true)]
        public UnsubscribeRequest.ChannelType Type { get; set; } = UnsubscribeRequest.ChannelType.Ticker;

        [global::ProtoBuf.ProtoMember(2, Name = @"currency_pair", IsRequired = true)]
        public string CurrencyPair { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class ErrorResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"code", IsRequired = true)]
        public int Code { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"message", IsRequired = true)]
        public string Message { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class TickerNotification : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"currency_pair", IsRequired = true)]
        public string CurrencyPair { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"data")]
        public global::System.Collections.Generic.List<TickerEvent> Datas { get; } = new global::System.Collections.Generic.List<TickerEvent>();

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class OrderBookRawNotification : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"currency_pair", IsRequired = true)]
        public string CurrencyPair { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"data")]
        public global::System.Collections.Generic.List<OrderBookRawEvent> Datas { get; } = new global::System.Collections.Generic.List<OrderBookRawEvent>();

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class OrderBookNotification : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"currency_pair", IsRequired = true)]
        public string CurrencyPair { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"data")]
        public global::System.Collections.Generic.List<OrderBookEvent> Datas { get; } = new global::System.Collections.Generic.List<OrderBookEvent>();

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class TradeNotification : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"currency_pair", IsRequired = true)]
        public string CurrencyPair { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"data")]
        public global::System.Collections.Generic.List<TradeEvent> Datas { get; } = new global::System.Collections.Generic.List<TradeEvent>();

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class CandleNotification : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"currency_pair", IsRequired = true)]
        public string CurrencyPair { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"interval", IsRequired = true)]
        public SubscribeCandleChannelRequest.CandleInterval Interval { get; set; } = SubscribeCandleChannelRequest.CandleInterval.Candle1Minute;

        [global::ProtoBuf.ProtoMember(3, Name = @"data")]
        public global::System.Collections.Generic.List<CandleEvent> Datas { get; } = new global::System.Collections.Generic.List<CandleEvent>();

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class LoginResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class PutLimitOrderResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"order_id", IsRequired = true)]
        public long OrderId { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"amount_left", IsRequired = true)]
        public string AmountLeft { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class CancelLimitOrderResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"order_id", IsRequired = true)]
        public long OrderId { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"amount_left", IsRequired = true)]
        public string AmountLeft { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class BalanceResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"type", IsRequired = true)]
        public BalanceType Type { get; set; } = BalanceType.Total;

        [global::ProtoBuf.ProtoMember(2, Name = @"currency", IsRequired = true)]
        public string Currency { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"value", IsRequired = true)]
        public string Value { get; set; }

        [global::ProtoBuf.ProtoContract()]
        public enum BalanceType
        {
            [global::ProtoBuf.ProtoEnum(Name = @"TOTAL")]
            Total = 1,
            [global::ProtoBuf.ProtoEnum(Name = @"AVAILABLE")]
            Available = 2,
            [global::ProtoBuf.ProtoEnum(Name = @"AVAILABLE_WITHDRAWAL")]
            AvailableWithdrawal = 3,
            [global::ProtoBuf.ProtoEnum(Name = @"TRADE")]
            Trade = 4,
        }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class BalancesResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"balances")]
        public global::System.Collections.Generic.List<BalanceResponse> Balances { get; } = new global::System.Collections.Generic.List<BalanceResponse>();

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class LastTradesResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"trades")]
        public global::System.Collections.Generic.List<TradeEvent> Trades { get; } = new global::System.Collections.Generic.List<TradeEvent>();

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class Trade : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"id", IsRequired = true)]
        public long Id { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"trade_type", IsRequired = true)]
        public TradeEvent.TradeType TradeType { get; set; } = TradeEvent.TradeType.Buy;

        [global::ProtoBuf.ProtoMember(3, Name = @"timestamp", IsRequired = true)]
        public long Timestamp { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"currency_pair", IsRequired = true)]
        public string CurrencyPair { get; set; }

        [global::ProtoBuf.ProtoMember(5, Name = @"price", IsRequired = true)]
        public string Price { get; set; }

        [global::ProtoBuf.ProtoMember(6, Name = @"quantity", IsRequired = true)]
        public string Quantity { get; set; }

        [global::ProtoBuf.ProtoMember(7, Name = @"commission", IsRequired = true)]
        public string Commission { get; set; }

        [global::ProtoBuf.ProtoMember(8, Name = @"bonus", IsRequired = true)]
        public string Bonus { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class TradesResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"trades")]
        public global::System.Collections.Generic.List<Trade> Trades { get; } = new global::System.Collections.Generic.List<Trade>();

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class Order : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"id", IsRequired = true)]
        public long Id { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"currency_pair", IsRequired = true)]
        public string CurrencyPair { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"good_until_time", IsRequired = true)]
        public long GoodUntilTime { get; set; }

        [global::ProtoBuf.ProtoMember(4, IsRequired = true)]
        public OrderType order_type { get; set; } = OrderType.MarketBuy;

        [global::ProtoBuf.ProtoMember(5, IsRequired = true)]
        public OrderStatus order_status { get; set; } = OrderStatus.New;

        [global::ProtoBuf.ProtoMember(6, Name = @"issue_time", IsRequired = true)]
        public long IssueTime { get; set; }

        [global::ProtoBuf.ProtoMember(7, Name = @"price")]
        [global::System.ComponentModel.DefaultValue("")]
        public string Price
        {
            get { return __pbn__Price ?? ""; }
            set { __pbn__Price = value; }
        }
        public bool ShouldSerializePrice() => __pbn__Price != null;
        public void ResetPrice() => __pbn__Price = null;
        private string __pbn__Price;

        [global::ProtoBuf.ProtoMember(8, Name = @"quantity", IsRequired = true)]
        public string Quantity { get; set; }

        [global::ProtoBuf.ProtoMember(9, Name = @"remaining_quantity", IsRequired = true)]
        public string RemainingQuantity { get; set; }

        [global::ProtoBuf.ProtoMember(10, Name = @"commission_by_trade", IsRequired = true)]
        public string CommissionByTrade { get; set; }

        [global::ProtoBuf.ProtoMember(11, Name = @"bonus_by_trade", IsRequired = true)]
        public string BonusByTrade { get; set; }

        [global::ProtoBuf.ProtoMember(12, Name = @"bonus_rate", IsRequired = true)]
        public string BonusRate { get; set; }

        [global::ProtoBuf.ProtoMember(13, Name = @"commission_rate", IsRequired = true)]
        public string CommissionRate { get; set; }

        [global::ProtoBuf.ProtoMember(14, Name = @"last_modification_time", IsRequired = true)]
        public long LastModificationTime { get; set; }

        [global::ProtoBuf.ProtoContract()]
        public enum OrderType
        {
            [global::ProtoBuf.ProtoEnum(Name = @"MARKET_BUY")]
            MarketBuy = 1,
            [global::ProtoBuf.ProtoEnum(Name = @"MARKET_SELL")]
            MarketSell = 2,
            [global::ProtoBuf.ProtoEnum(Name = @"LIMIT_BUY")]
            LimitBuy = 3,
            [global::ProtoBuf.ProtoEnum(Name = @"LIMIT_SELL")]
            LimitSell = 4,
            [global::ProtoBuf.ProtoEnum(Name = @"UNKNOWN_TYPE")]
            UnknownType = 5,
            [global::ProtoBuf.ProtoEnum(Name = @"MARKET_BUY_IN_FULL_AMOUNT")]
            MarketBuyInFullAmount = 6,
        }

        [global::ProtoBuf.ProtoContract()]
        public enum OrderStatus
        {
            [global::ProtoBuf.ProtoEnum(Name = @"NEW")]
            New = 1,
            [global::ProtoBuf.ProtoEnum(Name = @"OPEN")]
            Open = 2,
            [global::ProtoBuf.ProtoEnum(Name = @"EXPIRED")]
            Expired = 3,
            [global::ProtoBuf.ProtoEnum(Name = @"CANCELLED")]
            Cancelled = 4,
            [global::ProtoBuf.ProtoEnum(Name = @"EXECUTED")]
            Executed = 5,
            [global::ProtoBuf.ProtoEnum(Name = @"PARTIALLY_FILLED")]
            PartiallyFilled = 6,
            [global::ProtoBuf.ProtoEnum(Name = @"PARTIALLY_FILLED_AND_CANCELLED")]
            PartiallyFilledAndCancelled = 7,
            [global::ProtoBuf.ProtoEnum(Name = @"PARTIALLY_FILLED_AND_EXPIRED")]
            PartiallyFilledAndExpired = 8,
            [global::ProtoBuf.ProtoEnum(Name = @"UNKNOWN_STATUS")]
            UnknownStatus = 9,
        }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class ClientOrdersResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, IsRequired = true)]
        public int totalRows { get; set; }

        [global::ProtoBuf.ProtoMember(2, IsRequired = true)]
        public int startRow { get; set; }

        [global::ProtoBuf.ProtoMember(3, IsRequired = true)]
        public int endRow { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"orders")]
        public global::System.Collections.Generic.List<Order> Orders { get; } = new global::System.Collections.Generic.List<Order>();

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class Trades : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, IsRequired = true)]
        public int trades { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"amount", IsRequired = true)]
        public string Amount { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"quantity", IsRequired = true)]
        public string Quantity { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"avg_price", IsRequired = true)]
        public string AvgPrice { get; set; }

        [global::ProtoBuf.ProtoMember(5, Name = @"commission", IsRequired = true)]
        public string Commission { get; set; }

        [global::ProtoBuf.ProtoMember(6, Name = @"bonus", IsRequired = true)]
        public string Bonus { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class ClientOrderResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"id", IsRequired = true)]
        public long Id { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"order_status", IsRequired = true)]
        public Order.OrderStatus OrderStatus { get; set; } = Order.OrderStatus.New;

        [global::ProtoBuf.ProtoMember(3, IsRequired = true)]
        public string currencyPair { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"price", IsRequired = true)]
        public string Price { get; set; }

        [global::ProtoBuf.ProtoMember(5, Name = @"quantity", IsRequired = true)]
        public string Quantity { get; set; }

        [global::ProtoBuf.ProtoMember(6, Name = @"remaining_quantity", IsRequired = true)]
        public string RemainingQuantity { get; set; }

        [global::ProtoBuf.ProtoMember(7, Name = @"blocked", IsRequired = true)]
        public string Blocked { get; set; }

        [global::ProtoBuf.ProtoMember(8, Name = @"blocked_remain", IsRequired = true)]
        public string BlockedRemain { get; set; }

        [global::ProtoBuf.ProtoMember(9, Name = @"commission_rate", IsRequired = true)]
        public string CommissionRate { get; set; }

        [global::ProtoBuf.ProtoMember(10, Name = @"trades")]
        public Trades Trades { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class CommissionResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"value", IsRequired = true)]
        public string Value { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class CommissionCommonInfoResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"commission", IsRequired = true)]
        public string Commission { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"last30_days_amount_as_usd", IsRequired = true)]
        public string Last30DaysAmountAsUsd { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class TradeHistory : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"id", IsRequired = true)]
        public string Id { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"trade_type", IsRequired = true)]
        public TradeHistoryRequest.Types TradeType { get; set; } = TradeHistoryRequest.Types.Buy;

        [global::ProtoBuf.ProtoMember(3, Name = @"date", IsRequired = true)]
        public long Date { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"amount", IsRequired = true)]
        public string Amount { get; set; }

        [global::ProtoBuf.ProtoMember(5, Name = @"fee", IsRequired = true)]
        public string Fee { get; set; }

        [global::ProtoBuf.ProtoMember(6, Name = @"fixed_currency", IsRequired = true)]
        public string FixedCurrency { get; set; }

        [global::ProtoBuf.ProtoMember(7, Name = @"tax_currency", IsRequired = true)]
        public string TaxCurrency { get; set; }

        [global::ProtoBuf.ProtoMember(8, Name = @"variable_amount", IsRequired = true)]
        public string VariableAmount { get; set; }

        [global::ProtoBuf.ProtoMember(9, Name = @"variable_currency", IsRequired = true)]
        public string VariableCurrency { get; set; }

        [global::ProtoBuf.ProtoMember(10, Name = @"external")]
        [global::System.ComponentModel.DefaultValue("")]
        public string External
        {
            get { return __pbn__External ?? ""; }
            set { __pbn__External = value; }
        }
        public bool ShouldSerializeExternal() => __pbn__External != null;
        public void ResetExternal() => __pbn__External = null;
        private string __pbn__External;

        [global::ProtoBuf.ProtoMember(11, Name = @"login", IsRequired = true)]
        public string Login { get; set; }

        [global::ProtoBuf.ProtoMember(12, Name = @"external_key", IsRequired = true)]
        public string ExternalKey { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class TradeHistoryResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"data")]
        public global::System.Collections.Generic.List<TradeHistory> Datas { get; } = new global::System.Collections.Generic.List<TradeHistory>();

        [global::ProtoBuf.ProtoMember(2, Name = @"total", IsRequired = true)]
        public long Total { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class MarkerOrderResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"order_id", IsRequired = true)]
        public long OrderId { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"amount_left", IsRequired = true)]
        public string AmountLeft { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class WalletAddressResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"wallet_address", IsRequired = true)]
        public string WalletAddress { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"wallet_currency", IsRequired = true)]
        public string WalletCurrency { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class WithdrawalCoinResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"fault")]
        [global::System.ComponentModel.DefaultValue("")]
        public string Fault
        {
            get { return __pbn__Fault ?? ""; }
            set { __pbn__Fault = value; }
        }
        public bool ShouldSerializeFault() => __pbn__Fault != null;
        public void ResetFault() => __pbn__Fault = null;
        private string __pbn__Fault;

        [global::ProtoBuf.ProtoMember(2, Name = @"user_id", IsRequired = true)]
        public long UserId { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"user_name", IsRequired = true)]
        public string UserName { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"id", IsRequired = true)]
        public long Id { get; set; }

        [global::ProtoBuf.ProtoMember(5, IsRequired = true)]
        public State state { get; set; } = State.New;

        [global::ProtoBuf.ProtoMember(6, Name = @"create_date", IsRequired = true)]
        public long CreateDate { get; set; }

        [global::ProtoBuf.ProtoMember(7, Name = @"last_modify_date", IsRequired = true)]
        public long LastModifyDate { get; set; }

        [global::ProtoBuf.ProtoMember(8, Name = @"verification_type")]
        [global::System.ComponentModel.DefaultValue("")]
        public string VerificationType
        {
            get { return __pbn__VerificationType ?? ""; }
            set { __pbn__VerificationType = value; }
        }
        public bool ShouldSerializeVerificationType() => __pbn__VerificationType != null;
        public void ResetVerificationType() => __pbn__VerificationType = null;
        private string __pbn__VerificationType;

        [global::ProtoBuf.ProtoMember(9, Name = @"verification_data")]
        public long VerificationData
        {
            get { return __pbn__VerificationData.GetValueOrDefault(); }
            set { __pbn__VerificationData = value; }
        }
        public bool ShouldSerializeVerificationData() => __pbn__VerificationData != null;
        public void ResetVerificationData() => __pbn__VerificationData = null;
        private long? __pbn__VerificationData;

        [global::ProtoBuf.ProtoMember(10, Name = @"comment")]
        [global::System.ComponentModel.DefaultValue("")]
        public string Comment
        {
            get { return __pbn__Comment ?? ""; }
            set { __pbn__Comment = value; }
        }
        public bool ShouldSerializeComment() => __pbn__Comment != null;
        public void ResetComment() => __pbn__Comment = null;
        private string __pbn__Comment;

        [global::ProtoBuf.ProtoMember(11, Name = @"description", IsRequired = true)]
        public string Description { get; set; }

        [global::ProtoBuf.ProtoMember(12, Name = @"amount", IsRequired = true)]
        public string Amount { get; set; }

        [global::ProtoBuf.ProtoMember(13, Name = @"currency", IsRequired = true)]
        public string Currency { get; set; }

        [global::ProtoBuf.ProtoMember(14, Name = @"wallet", IsRequired = true)]
        public string Wallet { get; set; }

        [global::ProtoBuf.ProtoContract()]
        public enum State
        {
            [global::ProtoBuf.ProtoEnum(Name = @"NEW")]
            New = 1,
            [global::ProtoBuf.ProtoEnum(Name = @"SAVED")]
            Saved = 2,
            [global::ProtoBuf.ProtoEnum(Name = @"RECEIVED")]
            Received = 3,
            [global::ProtoBuf.ProtoEnum(Name = @"VERIFIED")]
            Verified = 4,
            [global::ProtoBuf.ProtoEnum(Name = @"INPROCESS")]
            Inprocess = 5,
            [global::ProtoBuf.ProtoEnum(Name = @"PREAPPROVED")]
            Preapproved = 6,
            [global::ProtoBuf.ProtoEnum(Name = @"APPROVED")]
            Approved = 7,
            [global::ProtoBuf.ProtoEnum(Name = @"READY_FOR_EXECUTION")]
            ReadyForExecution = 8,
            [global::ProtoBuf.ProtoEnum(Name = @"ONEXECUTION")]
            Onexecution = 9,
            [global::ProtoBuf.ProtoEnum(Name = @"EXECUTED")]
            Executed = 10,
            [global::ProtoBuf.ProtoEnum(Name = @"DECLINED")]
            Declined = 11,
            [global::ProtoBuf.ProtoEnum(Name = @"POSTPONED")]
            Postponed = 12,
        }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class WithdrawalPayeerResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"fault")]
        [global::System.ComponentModel.DefaultValue("")]
        public string Fault
        {
            get { return __pbn__Fault ?? ""; }
            set { __pbn__Fault = value; }
        }
        public bool ShouldSerializeFault() => __pbn__Fault != null;
        public void ResetFault() => __pbn__Fault = null;
        private string __pbn__Fault;

        [global::ProtoBuf.ProtoMember(2, Name = @"user_id", IsRequired = true)]
        public long UserId { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"user_name", IsRequired = true)]
        public string UserName { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"id", IsRequired = true)]
        public long Id { get; set; }

        [global::ProtoBuf.ProtoMember(5, Name = @"state", IsRequired = true)]
        public WithdrawalCoinResponse.State State { get; set; } = WithdrawalCoinResponse.State.New;

        [global::ProtoBuf.ProtoMember(6, Name = @"create_date", IsRequired = true)]
        public long CreateDate { get; set; }

        [global::ProtoBuf.ProtoMember(7, Name = @"last_modify_date", IsRequired = true)]
        public long LastModifyDate { get; set; }

        [global::ProtoBuf.ProtoMember(8, Name = @"verification_type")]
        [global::System.ComponentModel.DefaultValue("")]
        public string VerificationType
        {
            get { return __pbn__VerificationType ?? ""; }
            set { __pbn__VerificationType = value; }
        }
        public bool ShouldSerializeVerificationType() => __pbn__VerificationType != null;
        public void ResetVerificationType() => __pbn__VerificationType = null;
        private string __pbn__VerificationType;

        [global::ProtoBuf.ProtoMember(9, Name = @"verification_data")]
        [global::System.ComponentModel.DefaultValue("")]
        public string VerificationData
        {
            get { return __pbn__VerificationData ?? ""; }
            set { __pbn__VerificationData = value; }
        }
        public bool ShouldSerializeVerificationData() => __pbn__VerificationData != null;
        public void ResetVerificationData() => __pbn__VerificationData = null;
        private string __pbn__VerificationData;

        [global::ProtoBuf.ProtoMember(10, Name = @"comment")]
        [global::System.ComponentModel.DefaultValue("")]
        public string Comment
        {
            get { return __pbn__Comment ?? ""; }
            set { __pbn__Comment = value; }
        }
        public bool ShouldSerializeComment() => __pbn__Comment != null;
        public void ResetComment() => __pbn__Comment = null;
        private string __pbn__Comment;

        [global::ProtoBuf.ProtoMember(11, Name = @"description", IsRequired = true)]
        public string Description { get; set; }

        [global::ProtoBuf.ProtoMember(12, Name = @"amount", IsRequired = true)]
        public string Amount { get; set; }

        [global::ProtoBuf.ProtoMember(13, Name = @"currency", IsRequired = true)]
        public string Currency { get; set; }

        [global::ProtoBuf.ProtoMember(14, Name = @"account_to", IsRequired = true)]
        public string AccountTo { get; set; }

        [global::ProtoBuf.ProtoMember(15)]
        public long acceptDate
        {
            get { return __pbn__acceptDate.GetValueOrDefault(); }
            set { __pbn__acceptDate = value; }
        }
        public bool ShouldSerializeacceptDate() => __pbn__acceptDate != null;
        public void ResetacceptDate() => __pbn__acceptDate = null;
        private long? __pbn__acceptDate;

        [global::ProtoBuf.ProtoMember(16)]
        public long valueDate
        {
            get { return __pbn__valueDate.GetValueOrDefault(); }
            set { __pbn__valueDate = value; }
        }
        public bool ShouldSerializevalueDate() => __pbn__valueDate != null;
        public void ResetvalueDate() => __pbn__valueDate = null;
        private long? __pbn__valueDate;

        [global::ProtoBuf.ProtoMember(17, IsRequired = true)]
        public long docDate { get; set; }

        [global::ProtoBuf.ProtoMember(18, IsRequired = true)]
        public string docNumber { get; set; }

        [global::ProtoBuf.ProtoMember(19)]
        [global::System.ComponentModel.DefaultValue("")]
        public string correspondentDetails
        {
            get { return __pbn__correspondentDetails ?? ""; }
            set { __pbn__correspondentDetails = value; }
        }
        public bool ShouldSerializecorrespondentDetails() => __pbn__correspondentDetails != null;
        public void ResetcorrespondentDetails() => __pbn__correspondentDetails = null;
        private string __pbn__correspondentDetails;

        [global::ProtoBuf.ProtoMember(20, IsRequired = true)]
        public string accountFrom { get; set; }

        [global::ProtoBuf.ProtoMember(21, Name = @"outcome", IsRequired = true)]
        public bool Outcome { get; set; }

        [global::ProtoBuf.ProtoMember(22, Name = @"external")]
        [global::System.ComponentModel.DefaultValue("")]
        public string External
        {
            get { return __pbn__External ?? ""; }
            set { __pbn__External = value; }
        }
        public bool ShouldSerializeExternal() => __pbn__External != null;
        public void ResetExternal() => __pbn__External = null;
        private string __pbn__External;

        [global::ProtoBuf.ProtoMember(23, IsRequired = true)]
        public string externalKey { get; set; }

        [global::ProtoBuf.ProtoMember(24, IsRequired = true)]
        public long externalSystemId { get; set; }

        [global::ProtoBuf.ProtoMember(25)]
        public long externalServiceId
        {
            get { return __pbn__externalServiceId.GetValueOrDefault(); }
            set { __pbn__externalServiceId = value; }
        }
        public bool ShouldSerializeexternalServiceId() => __pbn__externalServiceId != null;
        public void ResetexternalServiceId() => __pbn__externalServiceId = null;
        private long? __pbn__externalServiceId;

        [global::ProtoBuf.ProtoMember(26, Name = @"to", IsRequired = true)]
        public string To { get; set; }

        [global::ProtoBuf.ProtoMember(27, Name = @"protect")]
        [global::System.ComponentModel.DefaultValue("")]
        public string Protect
        {
            get { return __pbn__Protect ?? ""; }
            set { __pbn__Protect = value; }
        }
        public bool ShouldSerializeProtect() => __pbn__Protect != null;
        public void ResetProtect() => __pbn__Protect = null;
        private string __pbn__Protect;

        [global::ProtoBuf.ProtoMember(28)]
        public int protectPeriod
        {
            get { return __pbn__protectPeriod.GetValueOrDefault(); }
            set { __pbn__protectPeriod = value; }
        }
        public bool ShouldSerializeprotectPeriod() => __pbn__protectPeriod != null;
        public void ResetprotectPeriod() => __pbn__protectPeriod = null;
        private int? __pbn__protectPeriod;

        [global::ProtoBuf.ProtoMember(29)]
        [global::System.ComponentModel.DefaultValue("")]
        public string protectCode
        {
            get { return __pbn__protectCode ?? ""; }
            set { __pbn__protectCode = value; }
        }
        public bool ShouldSerializeprotectCode() => __pbn__protectCode != null;
        public void ResetprotectCode() => __pbn__protectCode = null;
        private string __pbn__protectCode;

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class WithdrawalCapitalistResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"fault")]
        [global::System.ComponentModel.DefaultValue("")]
        public string Fault
        {
            get { return __pbn__Fault ?? ""; }
            set { __pbn__Fault = value; }
        }
        public bool ShouldSerializeFault() => __pbn__Fault != null;
        public void ResetFault() => __pbn__Fault = null;
        private string __pbn__Fault;

        [global::ProtoBuf.ProtoMember(2, Name = @"user_id", IsRequired = true)]
        public long UserId { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"user_name", IsRequired = true)]
        public string UserName { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"id", IsRequired = true)]
        public long Id { get; set; }

        [global::ProtoBuf.ProtoMember(5, Name = @"state", IsRequired = true)]
        public WithdrawalCoinResponse.State State { get; set; } = WithdrawalCoinResponse.State.New;

        [global::ProtoBuf.ProtoMember(6, Name = @"create_date", IsRequired = true)]
        public long CreateDate { get; set; }

        [global::ProtoBuf.ProtoMember(7, Name = @"last_modify_date", IsRequired = true)]
        public long LastModifyDate { get; set; }

        [global::ProtoBuf.ProtoMember(8, Name = @"verification_type")]
        [global::System.ComponentModel.DefaultValue("")]
        public string VerificationType
        {
            get { return __pbn__VerificationType ?? ""; }
            set { __pbn__VerificationType = value; }
        }
        public bool ShouldSerializeVerificationType() => __pbn__VerificationType != null;
        public void ResetVerificationType() => __pbn__VerificationType = null;
        private string __pbn__VerificationType;

        [global::ProtoBuf.ProtoMember(9, Name = @"verification_data")]
        [global::System.ComponentModel.DefaultValue("")]
        public string VerificationData
        {
            get { return __pbn__VerificationData ?? ""; }
            set { __pbn__VerificationData = value; }
        }
        public bool ShouldSerializeVerificationData() => __pbn__VerificationData != null;
        public void ResetVerificationData() => __pbn__VerificationData = null;
        private string __pbn__VerificationData;

        [global::ProtoBuf.ProtoMember(10, Name = @"comment")]
        [global::System.ComponentModel.DefaultValue("")]
        public string Comment
        {
            get { return __pbn__Comment ?? ""; }
            set { __pbn__Comment = value; }
        }
        public bool ShouldSerializeComment() => __pbn__Comment != null;
        public void ResetComment() => __pbn__Comment = null;
        private string __pbn__Comment;

        [global::ProtoBuf.ProtoMember(11, Name = @"description", IsRequired = true)]
        public string Description { get; set; }

        [global::ProtoBuf.ProtoMember(12, Name = @"amount", IsRequired = true)]
        public string Amount { get; set; }

        [global::ProtoBuf.ProtoMember(13, Name = @"currency", IsRequired = true)]
        public string Currency { get; set; }

        [global::ProtoBuf.ProtoMember(14, Name = @"account_to", IsRequired = true)]
        public string AccountTo { get; set; }

        [global::ProtoBuf.ProtoMember(15)]
        public long acceptDate
        {
            get { return __pbn__acceptDate.GetValueOrDefault(); }
            set { __pbn__acceptDate = value; }
        }
        public bool ShouldSerializeacceptDate() => __pbn__acceptDate != null;
        public void ResetacceptDate() => __pbn__acceptDate = null;
        private long? __pbn__acceptDate;

        [global::ProtoBuf.ProtoMember(16)]
        public long valueDate
        {
            get { return __pbn__valueDate.GetValueOrDefault(); }
            set { __pbn__valueDate = value; }
        }
        public bool ShouldSerializevalueDate() => __pbn__valueDate != null;
        public void ResetvalueDate() => __pbn__valueDate = null;
        private long? __pbn__valueDate;

        [global::ProtoBuf.ProtoMember(17, IsRequired = true)]
        public long docDate { get; set; }

        [global::ProtoBuf.ProtoMember(18, IsRequired = true)]
        public string docNumber { get; set; }

        [global::ProtoBuf.ProtoMember(19)]
        [global::System.ComponentModel.DefaultValue("")]
        public string correspondentDetails
        {
            get { return __pbn__correspondentDetails ?? ""; }
            set { __pbn__correspondentDetails = value; }
        }
        public bool ShouldSerializecorrespondentDetails() => __pbn__correspondentDetails != null;
        public void ResetcorrespondentDetails() => __pbn__correspondentDetails = null;
        private string __pbn__correspondentDetails;

        [global::ProtoBuf.ProtoMember(20, IsRequired = true)]
        public string accountFrom { get; set; }

        [global::ProtoBuf.ProtoMember(21, Name = @"outcome", IsRequired = true)]
        public bool Outcome { get; set; }

        [global::ProtoBuf.ProtoMember(22, Name = @"external")]
        [global::System.ComponentModel.DefaultValue("")]
        public string External
        {
            get { return __pbn__External ?? ""; }
            set { __pbn__External = value; }
        }
        public bool ShouldSerializeExternal() => __pbn__External != null;
        public void ResetExternal() => __pbn__External = null;
        private string __pbn__External;

        [global::ProtoBuf.ProtoMember(23, IsRequired = true)]
        public string externalKey { get; set; }

        [global::ProtoBuf.ProtoMember(24, IsRequired = true)]
        public long externalSystemId { get; set; }

        [global::ProtoBuf.ProtoMember(25)]
        public long externalServiceId
        {
            get { return __pbn__externalServiceId.GetValueOrDefault(); }
            set { __pbn__externalServiceId = value; }
        }
        public bool ShouldSerializeexternalServiceId() => __pbn__externalServiceId != null;
        public void ResetexternalServiceId() => __pbn__externalServiceId = null;
        private long? __pbn__externalServiceId;

        [global::ProtoBuf.ProtoMember(26, IsRequired = true)]
        public string toAccount { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class WithdrawalAdvcashResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"fault")]
        [global::System.ComponentModel.DefaultValue("")]
        public string Fault
        {
            get { return __pbn__Fault ?? ""; }
            set { __pbn__Fault = value; }
        }
        public bool ShouldSerializeFault() => __pbn__Fault != null;
        public void ResetFault() => __pbn__Fault = null;
        private string __pbn__Fault;

        [global::ProtoBuf.ProtoMember(2, Name = @"user_id", IsRequired = true)]
        public long UserId { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"user_name", IsRequired = true)]
        public string UserName { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"id", IsRequired = true)]
        public long Id { get; set; }

        [global::ProtoBuf.ProtoMember(5, Name = @"state", IsRequired = true)]
        public WithdrawalCoinResponse.State State { get; set; } = WithdrawalCoinResponse.State.New;

        [global::ProtoBuf.ProtoMember(6, Name = @"create_date", IsRequired = true)]
        public long CreateDate { get; set; }

        [global::ProtoBuf.ProtoMember(7, Name = @"last_modify_date", IsRequired = true)]
        public long LastModifyDate { get; set; }

        [global::ProtoBuf.ProtoMember(8, Name = @"verification_type")]
        [global::System.ComponentModel.DefaultValue("")]
        public string VerificationType
        {
            get { return __pbn__VerificationType ?? ""; }
            set { __pbn__VerificationType = value; }
        }
        public bool ShouldSerializeVerificationType() => __pbn__VerificationType != null;
        public void ResetVerificationType() => __pbn__VerificationType = null;
        private string __pbn__VerificationType;

        [global::ProtoBuf.ProtoMember(9, Name = @"verification_data")]
        [global::System.ComponentModel.DefaultValue("")]
        public string VerificationData
        {
            get { return __pbn__VerificationData ?? ""; }
            set { __pbn__VerificationData = value; }
        }
        public bool ShouldSerializeVerificationData() => __pbn__VerificationData != null;
        public void ResetVerificationData() => __pbn__VerificationData = null;
        private string __pbn__VerificationData;

        [global::ProtoBuf.ProtoMember(10, Name = @"comment")]
        [global::System.ComponentModel.DefaultValue("")]
        public string Comment
        {
            get { return __pbn__Comment ?? ""; }
            set { __pbn__Comment = value; }
        }
        public bool ShouldSerializeComment() => __pbn__Comment != null;
        public void ResetComment() => __pbn__Comment = null;
        private string __pbn__Comment;

        [global::ProtoBuf.ProtoMember(11, Name = @"description", IsRequired = true)]
        public string Description { get; set; }

        [global::ProtoBuf.ProtoMember(12, Name = @"amount", IsRequired = true)]
        public string Amount { get; set; }

        [global::ProtoBuf.ProtoMember(13, Name = @"currency", IsRequired = true)]
        public string Currency { get; set; }

        [global::ProtoBuf.ProtoMember(14, Name = @"account_to", IsRequired = true)]
        public string AccountTo { get; set; }

        [global::ProtoBuf.ProtoMember(15)]
        public long acceptDate
        {
            get { return __pbn__acceptDate.GetValueOrDefault(); }
            set { __pbn__acceptDate = value; }
        }
        public bool ShouldSerializeacceptDate() => __pbn__acceptDate != null;
        public void ResetacceptDate() => __pbn__acceptDate = null;
        private long? __pbn__acceptDate;

        [global::ProtoBuf.ProtoMember(16)]
        public long valueDate
        {
            get { return __pbn__valueDate.GetValueOrDefault(); }
            set { __pbn__valueDate = value; }
        }
        public bool ShouldSerializevalueDate() => __pbn__valueDate != null;
        public void ResetvalueDate() => __pbn__valueDate = null;
        private long? __pbn__valueDate;

        [global::ProtoBuf.ProtoMember(17, IsRequired = true)]
        public long docDate { get; set; }

        [global::ProtoBuf.ProtoMember(18, IsRequired = true)]
        public string docNumber { get; set; }

        [global::ProtoBuf.ProtoMember(19)]
        [global::System.ComponentModel.DefaultValue("")]
        public string correspondentDetails
        {
            get { return __pbn__correspondentDetails ?? ""; }
            set { __pbn__correspondentDetails = value; }
        }
        public bool ShouldSerializecorrespondentDetails() => __pbn__correspondentDetails != null;
        public void ResetcorrespondentDetails() => __pbn__correspondentDetails = null;
        private string __pbn__correspondentDetails;

        [global::ProtoBuf.ProtoMember(20, IsRequired = true)]
        public string accountFrom { get; set; }

        [global::ProtoBuf.ProtoMember(21, Name = @"outcome", IsRequired = true)]
        public bool Outcome { get; set; }

        [global::ProtoBuf.ProtoMember(22, Name = @"external")]
        [global::System.ComponentModel.DefaultValue("")]
        public string External
        {
            get { return __pbn__External ?? ""; }
            set { __pbn__External = value; }
        }
        public bool ShouldSerializeExternal() => __pbn__External != null;
        public void ResetExternal() => __pbn__External = null;
        private string __pbn__External;

        [global::ProtoBuf.ProtoMember(23, IsRequired = true)]
        public string externalKey { get; set; }

        [global::ProtoBuf.ProtoMember(24, IsRequired = true)]
        public long externalSystemId { get; set; }

        [global::ProtoBuf.ProtoMember(25)]
        public long externalServiceId
        {
            get { return __pbn__externalServiceId.GetValueOrDefault(); }
            set { __pbn__externalServiceId = value; }
        }
        public bool ShouldSerializeexternalServiceId() => __pbn__externalServiceId != null;
        public void ResetexternalServiceId() => __pbn__externalServiceId = null;
        private long? __pbn__externalServiceId;

        [global::ProtoBuf.ProtoMember(26, IsRequired = true)]
        public string toAccount { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class WithdrawalYandexResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"fault")]
        [global::System.ComponentModel.DefaultValue("")]
        public string Fault
        {
            get { return __pbn__Fault ?? ""; }
            set { __pbn__Fault = value; }
        }
        public bool ShouldSerializeFault() => __pbn__Fault != null;
        public void ResetFault() => __pbn__Fault = null;
        private string __pbn__Fault;

        [global::ProtoBuf.ProtoMember(2, Name = @"user_id", IsRequired = true)]
        public long UserId { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"user_name", IsRequired = true)]
        public string UserName { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"id", IsRequired = true)]
        public long Id { get; set; }

        [global::ProtoBuf.ProtoMember(5, Name = @"state", IsRequired = true)]
        public WithdrawalCoinResponse.State State { get; set; } = WithdrawalCoinResponse.State.New;

        [global::ProtoBuf.ProtoMember(6, Name = @"create_date", IsRequired = true)]
        public long CreateDate { get; set; }

        [global::ProtoBuf.ProtoMember(7, Name = @"last_modify_date", IsRequired = true)]
        public long LastModifyDate { get; set; }

        [global::ProtoBuf.ProtoMember(8, Name = @"verification_type")]
        [global::System.ComponentModel.DefaultValue("")]
        public string VerificationType
        {
            get { return __pbn__VerificationType ?? ""; }
            set { __pbn__VerificationType = value; }
        }
        public bool ShouldSerializeVerificationType() => __pbn__VerificationType != null;
        public void ResetVerificationType() => __pbn__VerificationType = null;
        private string __pbn__VerificationType;

        [global::ProtoBuf.ProtoMember(9, Name = @"verification_data")]
        [global::System.ComponentModel.DefaultValue("")]
        public string VerificationData
        {
            get { return __pbn__VerificationData ?? ""; }
            set { __pbn__VerificationData = value; }
        }
        public bool ShouldSerializeVerificationData() => __pbn__VerificationData != null;
        public void ResetVerificationData() => __pbn__VerificationData = null;
        private string __pbn__VerificationData;

        [global::ProtoBuf.ProtoMember(10, Name = @"comment")]
        [global::System.ComponentModel.DefaultValue("")]
        public string Comment
        {
            get { return __pbn__Comment ?? ""; }
            set { __pbn__Comment = value; }
        }
        public bool ShouldSerializeComment() => __pbn__Comment != null;
        public void ResetComment() => __pbn__Comment = null;
        private string __pbn__Comment;

        [global::ProtoBuf.ProtoMember(11, Name = @"description", IsRequired = true)]
        public string Description { get; set; }

        [global::ProtoBuf.ProtoMember(12, Name = @"amount", IsRequired = true)]
        public string Amount { get; set; }

        [global::ProtoBuf.ProtoMember(13, Name = @"currency", IsRequired = true)]
        public string Currency { get; set; }

        [global::ProtoBuf.ProtoMember(14, Name = @"account_to", IsRequired = true)]
        public string AccountTo { get; set; }

        [global::ProtoBuf.ProtoMember(15)]
        public long acceptDate
        {
            get { return __pbn__acceptDate.GetValueOrDefault(); }
            set { __pbn__acceptDate = value; }
        }
        public bool ShouldSerializeacceptDate() => __pbn__acceptDate != null;
        public void ResetacceptDate() => __pbn__acceptDate = null;
        private long? __pbn__acceptDate;

        [global::ProtoBuf.ProtoMember(16)]
        public long valueDate
        {
            get { return __pbn__valueDate.GetValueOrDefault(); }
            set { __pbn__valueDate = value; }
        }
        public bool ShouldSerializevalueDate() => __pbn__valueDate != null;
        public void ResetvalueDate() => __pbn__valueDate = null;
        private long? __pbn__valueDate;

        [global::ProtoBuf.ProtoMember(17, IsRequired = true)]
        public long docDate { get; set; }

        [global::ProtoBuf.ProtoMember(18, IsRequired = true)]
        public string docNumber { get; set; }

        [global::ProtoBuf.ProtoMember(19)]
        [global::System.ComponentModel.DefaultValue("")]
        public string correspondentDetails
        {
            get { return __pbn__correspondentDetails ?? ""; }
            set { __pbn__correspondentDetails = value; }
        }
        public bool ShouldSerializecorrespondentDetails() => __pbn__correspondentDetails != null;
        public void ResetcorrespondentDetails() => __pbn__correspondentDetails = null;
        private string __pbn__correspondentDetails;

        [global::ProtoBuf.ProtoMember(20, IsRequired = true)]
        public string accountFrom { get; set; }

        [global::ProtoBuf.ProtoMember(21, Name = @"outcome", IsRequired = true)]
        public bool Outcome { get; set; }

        [global::ProtoBuf.ProtoMember(22, Name = @"external")]
        [global::System.ComponentModel.DefaultValue("")]
        public string External
        {
            get { return __pbn__External ?? ""; }
            set { __pbn__External = value; }
        }
        public bool ShouldSerializeExternal() => __pbn__External != null;
        public void ResetExternal() => __pbn__External = null;
        private string __pbn__External;

        [global::ProtoBuf.ProtoMember(23, IsRequired = true)]
        public string externalKey { get; set; }

        [global::ProtoBuf.ProtoMember(24, IsRequired = true)]
        public long externalSystemId { get; set; }

        [global::ProtoBuf.ProtoMember(25)]
        public long externalServiceId
        {
            get { return __pbn__externalServiceId.GetValueOrDefault(); }
            set { __pbn__externalServiceId = value; }
        }
        public bool ShouldSerializeexternalServiceId() => __pbn__externalServiceId != null;
        public void ResetexternalServiceId() => __pbn__externalServiceId = null;
        private long? __pbn__externalServiceId;

        [global::ProtoBuf.ProtoMember(26)]
        [global::System.ComponentModel.DefaultValue("")]
        public string txId
        {
            get { return __pbn__txId ?? ""; }
            set { __pbn__txId = value; }
        }
        public bool ShouldSerializetxId() => __pbn__txId != null;
        public void ResettxId() => __pbn__txId = null;
        private string __pbn__txId;

        [global::ProtoBuf.ProtoMember(27, IsRequired = true)]
        public string toAccount { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class WithdrawalQiwiResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"fault")]
        [global::System.ComponentModel.DefaultValue("")]
        public string Fault
        {
            get { return __pbn__Fault ?? ""; }
            set { __pbn__Fault = value; }
        }
        public bool ShouldSerializeFault() => __pbn__Fault != null;
        public void ResetFault() => __pbn__Fault = null;
        private string __pbn__Fault;

        [global::ProtoBuf.ProtoMember(2, Name = @"user_id", IsRequired = true)]
        public long UserId { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"user_name", IsRequired = true)]
        public string UserName { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"id", IsRequired = true)]
        public long Id { get; set; }

        [global::ProtoBuf.ProtoMember(5, Name = @"state", IsRequired = true)]
        public WithdrawalCoinResponse.State State { get; set; } = WithdrawalCoinResponse.State.New;

        [global::ProtoBuf.ProtoMember(6, Name = @"create_date", IsRequired = true)]
        public long CreateDate { get; set; }

        [global::ProtoBuf.ProtoMember(7, Name = @"last_modify_date", IsRequired = true)]
        public long LastModifyDate { get; set; }

        [global::ProtoBuf.ProtoMember(8, Name = @"verification_type")]
        [global::System.ComponentModel.DefaultValue("")]
        public string VerificationType
        {
            get { return __pbn__VerificationType ?? ""; }
            set { __pbn__VerificationType = value; }
        }
        public bool ShouldSerializeVerificationType() => __pbn__VerificationType != null;
        public void ResetVerificationType() => __pbn__VerificationType = null;
        private string __pbn__VerificationType;

        [global::ProtoBuf.ProtoMember(9, Name = @"verification_data")]
        [global::System.ComponentModel.DefaultValue("")]
        public string VerificationData
        {
            get { return __pbn__VerificationData ?? ""; }
            set { __pbn__VerificationData = value; }
        }
        public bool ShouldSerializeVerificationData() => __pbn__VerificationData != null;
        public void ResetVerificationData() => __pbn__VerificationData = null;
        private string __pbn__VerificationData;

        [global::ProtoBuf.ProtoMember(10, Name = @"comment")]
        [global::System.ComponentModel.DefaultValue("")]
        public string Comment
        {
            get { return __pbn__Comment ?? ""; }
            set { __pbn__Comment = value; }
        }
        public bool ShouldSerializeComment() => __pbn__Comment != null;
        public void ResetComment() => __pbn__Comment = null;
        private string __pbn__Comment;

        [global::ProtoBuf.ProtoMember(11, Name = @"description", IsRequired = true)]
        public string Description { get; set; }

        [global::ProtoBuf.ProtoMember(12, Name = @"amount", IsRequired = true)]
        public string Amount { get; set; }

        [global::ProtoBuf.ProtoMember(13, Name = @"currency", IsRequired = true)]
        public string Currency { get; set; }

        [global::ProtoBuf.ProtoMember(14, Name = @"account_to", IsRequired = true)]
        public string AccountTo { get; set; }

        [global::ProtoBuf.ProtoMember(15)]
        public long acceptDate
        {
            get { return __pbn__acceptDate.GetValueOrDefault(); }
            set { __pbn__acceptDate = value; }
        }
        public bool ShouldSerializeacceptDate() => __pbn__acceptDate != null;
        public void ResetacceptDate() => __pbn__acceptDate = null;
        private long? __pbn__acceptDate;

        [global::ProtoBuf.ProtoMember(16)]
        public long valueDate
        {
            get { return __pbn__valueDate.GetValueOrDefault(); }
            set { __pbn__valueDate = value; }
        }
        public bool ShouldSerializevalueDate() => __pbn__valueDate != null;
        public void ResetvalueDate() => __pbn__valueDate = null;
        private long? __pbn__valueDate;

        [global::ProtoBuf.ProtoMember(17, IsRequired = true)]
        public long docDate { get; set; }

        [global::ProtoBuf.ProtoMember(18, IsRequired = true)]
        public string docNumber { get; set; }

        [global::ProtoBuf.ProtoMember(19)]
        [global::System.ComponentModel.DefaultValue("")]
        public string correspondentDetails
        {
            get { return __pbn__correspondentDetails ?? ""; }
            set { __pbn__correspondentDetails = value; }
        }
        public bool ShouldSerializecorrespondentDetails() => __pbn__correspondentDetails != null;
        public void ResetcorrespondentDetails() => __pbn__correspondentDetails = null;
        private string __pbn__correspondentDetails;

        [global::ProtoBuf.ProtoMember(20, IsRequired = true)]
        public string accountFrom { get; set; }

        [global::ProtoBuf.ProtoMember(21, Name = @"outcome", IsRequired = true)]
        public bool Outcome { get; set; }

        [global::ProtoBuf.ProtoMember(22, Name = @"external")]
        [global::System.ComponentModel.DefaultValue("")]
        public string External
        {
            get { return __pbn__External ?? ""; }
            set { __pbn__External = value; }
        }
        public bool ShouldSerializeExternal() => __pbn__External != null;
        public void ResetExternal() => __pbn__External = null;
        private string __pbn__External;

        [global::ProtoBuf.ProtoMember(23, IsRequired = true)]
        public string externalKey { get; set; }

        [global::ProtoBuf.ProtoMember(24, IsRequired = true)]
        public long externalSystemId { get; set; }

        [global::ProtoBuf.ProtoMember(25)]
        public long externalServiceId
        {
            get { return __pbn__externalServiceId.GetValueOrDefault(); }
            set { __pbn__externalServiceId = value; }
        }
        public bool ShouldSerializeexternalServiceId() => __pbn__externalServiceId != null;
        public void ResetexternalServiceId() => __pbn__externalServiceId = null;
        private long? __pbn__externalServiceId;

        [global::ProtoBuf.ProtoMember(26)]
        [global::System.ComponentModel.DefaultValue("")]
        public string txId
        {
            get { return __pbn__txId ?? ""; }
            set { __pbn__txId = value; }
        }
        public bool ShouldSerializetxId() => __pbn__txId != null;
        public void ResettxId() => __pbn__txId = null;
        private string __pbn__txId;

        [global::ProtoBuf.ProtoMember(27, IsRequired = true)]
        public string toAccount { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class WithdrawalCardResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"fault")]
        [global::System.ComponentModel.DefaultValue("")]
        public string Fault
        {
            get { return __pbn__Fault ?? ""; }
            set { __pbn__Fault = value; }
        }
        public bool ShouldSerializeFault() => __pbn__Fault != null;
        public void ResetFault() => __pbn__Fault = null;
        private string __pbn__Fault;

        [global::ProtoBuf.ProtoMember(2, Name = @"user_id", IsRequired = true)]
        public long UserId { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"user_name", IsRequired = true)]
        public string UserName { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"id", IsRequired = true)]
        public long Id { get; set; }

        [global::ProtoBuf.ProtoMember(5, Name = @"state", IsRequired = true)]
        public WithdrawalCoinResponse.State State { get; set; } = WithdrawalCoinResponse.State.New;

        [global::ProtoBuf.ProtoMember(6, Name = @"create_date", IsRequired = true)]
        public long CreateDate { get; set; }

        [global::ProtoBuf.ProtoMember(7, Name = @"last_modify_date", IsRequired = true)]
        public long LastModifyDate { get; set; }

        [global::ProtoBuf.ProtoMember(8, Name = @"verification_type")]
        [global::System.ComponentModel.DefaultValue("")]
        public string VerificationType
        {
            get { return __pbn__VerificationType ?? ""; }
            set { __pbn__VerificationType = value; }
        }
        public bool ShouldSerializeVerificationType() => __pbn__VerificationType != null;
        public void ResetVerificationType() => __pbn__VerificationType = null;
        private string __pbn__VerificationType;

        [global::ProtoBuf.ProtoMember(9, Name = @"verification_data")]
        [global::System.ComponentModel.DefaultValue("")]
        public string VerificationData
        {
            get { return __pbn__VerificationData ?? ""; }
            set { __pbn__VerificationData = value; }
        }
        public bool ShouldSerializeVerificationData() => __pbn__VerificationData != null;
        public void ResetVerificationData() => __pbn__VerificationData = null;
        private string __pbn__VerificationData;

        [global::ProtoBuf.ProtoMember(10, Name = @"comment")]
        [global::System.ComponentModel.DefaultValue("")]
        public string Comment
        {
            get { return __pbn__Comment ?? ""; }
            set { __pbn__Comment = value; }
        }
        public bool ShouldSerializeComment() => __pbn__Comment != null;
        public void ResetComment() => __pbn__Comment = null;
        private string __pbn__Comment;

        [global::ProtoBuf.ProtoMember(11, Name = @"description", IsRequired = true)]
        public string Description { get; set; }

        [global::ProtoBuf.ProtoMember(12, Name = @"amount", IsRequired = true)]
        public string Amount { get; set; }

        [global::ProtoBuf.ProtoMember(13, Name = @"currency", IsRequired = true)]
        public string Currency { get; set; }

        [global::ProtoBuf.ProtoMember(14, Name = @"account_to", IsRequired = true)]
        public string AccountTo { get; set; }

        [global::ProtoBuf.ProtoMember(15)]
        public long acceptDate
        {
            get { return __pbn__acceptDate.GetValueOrDefault(); }
            set { __pbn__acceptDate = value; }
        }
        public bool ShouldSerializeacceptDate() => __pbn__acceptDate != null;
        public void ResetacceptDate() => __pbn__acceptDate = null;
        private long? __pbn__acceptDate;

        [global::ProtoBuf.ProtoMember(16)]
        public long valueDate
        {
            get { return __pbn__valueDate.GetValueOrDefault(); }
            set { __pbn__valueDate = value; }
        }
        public bool ShouldSerializevalueDate() => __pbn__valueDate != null;
        public void ResetvalueDate() => __pbn__valueDate = null;
        private long? __pbn__valueDate;

        [global::ProtoBuf.ProtoMember(17, IsRequired = true)]
        public long docDate { get; set; }

        [global::ProtoBuf.ProtoMember(18, IsRequired = true)]
        public string docNumber { get; set; }

        [global::ProtoBuf.ProtoMember(19)]
        [global::System.ComponentModel.DefaultValue("")]
        public string correspondentDetails
        {
            get { return __pbn__correspondentDetails ?? ""; }
            set { __pbn__correspondentDetails = value; }
        }
        public bool ShouldSerializecorrespondentDetails() => __pbn__correspondentDetails != null;
        public void ResetcorrespondentDetails() => __pbn__correspondentDetails = null;
        private string __pbn__correspondentDetails;

        [global::ProtoBuf.ProtoMember(20, IsRequired = true)]
        public string accountFrom { get; set; }

        [global::ProtoBuf.ProtoMember(21, Name = @"outcome", IsRequired = true)]
        public bool Outcome { get; set; }

        [global::ProtoBuf.ProtoMember(22, Name = @"external")]
        [global::System.ComponentModel.DefaultValue("")]
        public string External
        {
            get { return __pbn__External ?? ""; }
            set { __pbn__External = value; }
        }
        public bool ShouldSerializeExternal() => __pbn__External != null;
        public void ResetExternal() => __pbn__External = null;
        private string __pbn__External;

        [global::ProtoBuf.ProtoMember(23, IsRequired = true)]
        public string externalKey { get; set; }

        [global::ProtoBuf.ProtoMember(24, IsRequired = true)]
        public long externalSystemId { get; set; }

        [global::ProtoBuf.ProtoMember(25)]
        public long externalServiceId
        {
            get { return __pbn__externalServiceId.GetValueOrDefault(); }
            set { __pbn__externalServiceId = value; }
        }
        public bool ShouldSerializeexternalServiceId() => __pbn__externalServiceId != null;
        public void ResetexternalServiceId() => __pbn__externalServiceId = null;
        private long? __pbn__externalServiceId;

        [global::ProtoBuf.ProtoMember(26)]
        [global::System.ComponentModel.DefaultValue("")]
        public string txId
        {
            get { return __pbn__txId ?? ""; }
            set { __pbn__txId = value; }
        }
        public bool ShouldSerializetxId() => __pbn__txId != null;
        public void ResettxId() => __pbn__txId = null;
        private string __pbn__txId;

        [global::ProtoBuf.ProtoMember(27, IsRequired = true)]
        public string toAccount { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class WithdrawalMastercardResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"fault")]
        [global::System.ComponentModel.DefaultValue("")]
        public string Fault
        {
            get { return __pbn__Fault ?? ""; }
            set { __pbn__Fault = value; }
        }
        public bool ShouldSerializeFault() => __pbn__Fault != null;
        public void ResetFault() => __pbn__Fault = null;
        private string __pbn__Fault;

        [global::ProtoBuf.ProtoMember(2, Name = @"user_id", IsRequired = true)]
        public long UserId { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"user_name", IsRequired = true)]
        public string UserName { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"id", IsRequired = true)]
        public long Id { get; set; }

        [global::ProtoBuf.ProtoMember(5, Name = @"state", IsRequired = true)]
        public WithdrawalCoinResponse.State State { get; set; } = WithdrawalCoinResponse.State.New;

        [global::ProtoBuf.ProtoMember(6, Name = @"create_date", IsRequired = true)]
        public long CreateDate { get; set; }

        [global::ProtoBuf.ProtoMember(7, Name = @"last_modify_date", IsRequired = true)]
        public long LastModifyDate { get; set; }

        [global::ProtoBuf.ProtoMember(8, Name = @"verification_type")]
        [global::System.ComponentModel.DefaultValue("")]
        public string VerificationType
        {
            get { return __pbn__VerificationType ?? ""; }
            set { __pbn__VerificationType = value; }
        }
        public bool ShouldSerializeVerificationType() => __pbn__VerificationType != null;
        public void ResetVerificationType() => __pbn__VerificationType = null;
        private string __pbn__VerificationType;

        [global::ProtoBuf.ProtoMember(9, Name = @"verification_data")]
        [global::System.ComponentModel.DefaultValue("")]
        public string VerificationData
        {
            get { return __pbn__VerificationData ?? ""; }
            set { __pbn__VerificationData = value; }
        }
        public bool ShouldSerializeVerificationData() => __pbn__VerificationData != null;
        public void ResetVerificationData() => __pbn__VerificationData = null;
        private string __pbn__VerificationData;

        [global::ProtoBuf.ProtoMember(10, Name = @"comment")]
        [global::System.ComponentModel.DefaultValue("")]
        public string Comment
        {
            get { return __pbn__Comment ?? ""; }
            set { __pbn__Comment = value; }
        }
        public bool ShouldSerializeComment() => __pbn__Comment != null;
        public void ResetComment() => __pbn__Comment = null;
        private string __pbn__Comment;

        [global::ProtoBuf.ProtoMember(11, Name = @"description", IsRequired = true)]
        public string Description { get; set; }

        [global::ProtoBuf.ProtoMember(12, Name = @"amount", IsRequired = true)]
        public string Amount { get; set; }

        [global::ProtoBuf.ProtoMember(13, Name = @"currency", IsRequired = true)]
        public string Currency { get; set; }

        [global::ProtoBuf.ProtoMember(14, Name = @"account_to", IsRequired = true)]
        public string AccountTo { get; set; }

        [global::ProtoBuf.ProtoMember(15)]
        public long acceptDate
        {
            get { return __pbn__acceptDate.GetValueOrDefault(); }
            set { __pbn__acceptDate = value; }
        }
        public bool ShouldSerializeacceptDate() => __pbn__acceptDate != null;
        public void ResetacceptDate() => __pbn__acceptDate = null;
        private long? __pbn__acceptDate;

        [global::ProtoBuf.ProtoMember(16)]
        public long valueDate
        {
            get { return __pbn__valueDate.GetValueOrDefault(); }
            set { __pbn__valueDate = value; }
        }
        public bool ShouldSerializevalueDate() => __pbn__valueDate != null;
        public void ResetvalueDate() => __pbn__valueDate = null;
        private long? __pbn__valueDate;

        [global::ProtoBuf.ProtoMember(17, IsRequired = true)]
        public long docDate { get; set; }

        [global::ProtoBuf.ProtoMember(18, IsRequired = true)]
        public string docNumber { get; set; }

        [global::ProtoBuf.ProtoMember(19)]
        [global::System.ComponentModel.DefaultValue("")]
        public string correspondentDetails
        {
            get { return __pbn__correspondentDetails ?? ""; }
            set { __pbn__correspondentDetails = value; }
        }
        public bool ShouldSerializecorrespondentDetails() => __pbn__correspondentDetails != null;
        public void ResetcorrespondentDetails() => __pbn__correspondentDetails = null;
        private string __pbn__correspondentDetails;

        [global::ProtoBuf.ProtoMember(20, IsRequired = true)]
        public string accountFrom { get; set; }

        [global::ProtoBuf.ProtoMember(21, Name = @"outcome", IsRequired = true)]
        public bool Outcome { get; set; }

        [global::ProtoBuf.ProtoMember(22, Name = @"external")]
        [global::System.ComponentModel.DefaultValue("")]
        public string External
        {
            get { return __pbn__External ?? ""; }
            set { __pbn__External = value; }
        }
        public bool ShouldSerializeExternal() => __pbn__External != null;
        public void ResetExternal() => __pbn__External = null;
        private string __pbn__External;

        [global::ProtoBuf.ProtoMember(23, IsRequired = true)]
        public string externalKey { get; set; }

        [global::ProtoBuf.ProtoMember(24, IsRequired = true)]
        public long externalSystemId { get; set; }

        [global::ProtoBuf.ProtoMember(25)]
        public long externalServiceId
        {
            get { return __pbn__externalServiceId.GetValueOrDefault(); }
            set { __pbn__externalServiceId = value; }
        }
        public bool ShouldSerializeexternalServiceId() => __pbn__externalServiceId != null;
        public void ResetexternalServiceId() => __pbn__externalServiceId = null;
        private long? __pbn__externalServiceId;

        [global::ProtoBuf.ProtoMember(26)]
        [global::System.ComponentModel.DefaultValue("")]
        public string txId
        {
            get { return __pbn__txId ?? ""; }
            set { __pbn__txId = value; }
        }
        public bool ShouldSerializetxId() => __pbn__txId != null;
        public void ResettxId() => __pbn__txId = null;
        private string __pbn__txId;

        [global::ProtoBuf.ProtoMember(27, IsRequired = true)]
        public string toAccount { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class WithdrawalPerfectMoneyResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"fault")]
        [global::System.ComponentModel.DefaultValue("")]
        public string Fault
        {
            get { return __pbn__Fault ?? ""; }
            set { __pbn__Fault = value; }
        }
        public bool ShouldSerializeFault() => __pbn__Fault != null;
        public void ResetFault() => __pbn__Fault = null;
        private string __pbn__Fault;

        [global::ProtoBuf.ProtoMember(2, Name = @"user_id", IsRequired = true)]
        public long UserId { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"user_name", IsRequired = true)]
        public string UserName { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"id", IsRequired = true)]
        public long Id { get; set; }

        [global::ProtoBuf.ProtoMember(5, Name = @"state", IsRequired = true)]
        public WithdrawalCoinResponse.State State { get; set; } = WithdrawalCoinResponse.State.New;

        [global::ProtoBuf.ProtoMember(6, Name = @"create_date", IsRequired = true)]
        public long CreateDate { get; set; }

        [global::ProtoBuf.ProtoMember(7, Name = @"last_modify_date", IsRequired = true)]
        public long LastModifyDate { get; set; }

        [global::ProtoBuf.ProtoMember(8, Name = @"verification_type")]
        [global::System.ComponentModel.DefaultValue("")]
        public string VerificationType
        {
            get { return __pbn__VerificationType ?? ""; }
            set { __pbn__VerificationType = value; }
        }
        public bool ShouldSerializeVerificationType() => __pbn__VerificationType != null;
        public void ResetVerificationType() => __pbn__VerificationType = null;
        private string __pbn__VerificationType;

        [global::ProtoBuf.ProtoMember(9, Name = @"verification_data")]
        [global::System.ComponentModel.DefaultValue("")]
        public string VerificationData
        {
            get { return __pbn__VerificationData ?? ""; }
            set { __pbn__VerificationData = value; }
        }
        public bool ShouldSerializeVerificationData() => __pbn__VerificationData != null;
        public void ResetVerificationData() => __pbn__VerificationData = null;
        private string __pbn__VerificationData;

        [global::ProtoBuf.ProtoMember(10, Name = @"comment")]
        [global::System.ComponentModel.DefaultValue("")]
        public string Comment
        {
            get { return __pbn__Comment ?? ""; }
            set { __pbn__Comment = value; }
        }
        public bool ShouldSerializeComment() => __pbn__Comment != null;
        public void ResetComment() => __pbn__Comment = null;
        private string __pbn__Comment;

        [global::ProtoBuf.ProtoMember(11, Name = @"description", IsRequired = true)]
        public string Description { get; set; }

        [global::ProtoBuf.ProtoMember(12, Name = @"amount", IsRequired = true)]
        public string Amount { get; set; }

        [global::ProtoBuf.ProtoMember(13, Name = @"currency", IsRequired = true)]
        public string Currency { get; set; }

        [global::ProtoBuf.ProtoMember(14, Name = @"account_to", IsRequired = true)]
        public string AccountTo { get; set; }

        [global::ProtoBuf.ProtoMember(15)]
        public long acceptDate
        {
            get { return __pbn__acceptDate.GetValueOrDefault(); }
            set { __pbn__acceptDate = value; }
        }
        public bool ShouldSerializeacceptDate() => __pbn__acceptDate != null;
        public void ResetacceptDate() => __pbn__acceptDate = null;
        private long? __pbn__acceptDate;

        [global::ProtoBuf.ProtoMember(16)]
        public long valueDate
        {
            get { return __pbn__valueDate.GetValueOrDefault(); }
            set { __pbn__valueDate = value; }
        }
        public bool ShouldSerializevalueDate() => __pbn__valueDate != null;
        public void ResetvalueDate() => __pbn__valueDate = null;
        private long? __pbn__valueDate;

        [global::ProtoBuf.ProtoMember(17, IsRequired = true)]
        public long docDate { get; set; }

        [global::ProtoBuf.ProtoMember(18, IsRequired = true)]
        public string docNumber { get; set; }

        [global::ProtoBuf.ProtoMember(19)]
        [global::System.ComponentModel.DefaultValue("")]
        public string correspondentDetails
        {
            get { return __pbn__correspondentDetails ?? ""; }
            set { __pbn__correspondentDetails = value; }
        }
        public bool ShouldSerializecorrespondentDetails() => __pbn__correspondentDetails != null;
        public void ResetcorrespondentDetails() => __pbn__correspondentDetails = null;
        private string __pbn__correspondentDetails;

        [global::ProtoBuf.ProtoMember(20, IsRequired = true)]
        public string accountFrom { get; set; }

        [global::ProtoBuf.ProtoMember(21, Name = @"outcome", IsRequired = true)]
        public bool Outcome { get; set; }

        [global::ProtoBuf.ProtoMember(22, Name = @"external")]
        [global::System.ComponentModel.DefaultValue("")]
        public string External
        {
            get { return __pbn__External ?? ""; }
            set { __pbn__External = value; }
        }
        public bool ShouldSerializeExternal() => __pbn__External != null;
        public void ResetExternal() => __pbn__External = null;
        private string __pbn__External;

        [global::ProtoBuf.ProtoMember(23, IsRequired = true)]
        public string externalKey { get; set; }

        [global::ProtoBuf.ProtoMember(24, IsRequired = true)]
        public long externalSystemId { get; set; }

        [global::ProtoBuf.ProtoMember(25)]
        public long externalServiceId
        {
            get { return __pbn__externalServiceId.GetValueOrDefault(); }
            set { __pbn__externalServiceId = value; }
        }
        public bool ShouldSerializeexternalServiceId() => __pbn__externalServiceId != null;
        public void ResetexternalServiceId() => __pbn__externalServiceId = null;
        private long? __pbn__externalServiceId;

        [global::ProtoBuf.ProtoMember(26, Name = @"receiver", IsRequired = true)]
        public string Receiver { get; set; }

        [global::ProtoBuf.ProtoMember(27, Name = @"code")]
        [global::System.ComponentModel.DefaultValue("")]
        public string Code
        {
            get { return __pbn__Code ?? ""; }
            set { __pbn__Code = value; }
        }
        public bool ShouldSerializeCode() => __pbn__Code != null;
        public void ResetCode() => __pbn__Code = null;
        private string __pbn__Code;

        [global::ProtoBuf.ProtoMember(28, Name = @"period")]
        public int Period
        {
            get { return __pbn__Period.GetValueOrDefault(); }
            set { __pbn__Period = value; }
        }
        public bool ShouldSerializePeriod() => __pbn__Period != null;
        public void ResetPeriod() => __pbn__Period = null;
        private int? __pbn__Period;

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class VoucherMakeResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"voucher_code", IsRequired = true)]
        public string VoucherCode { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class VoucherAmountResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"voucher_amount", IsRequired = true)]
        public string VoucherAmount { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class VoucherRedeemResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"fault")]
        [global::System.ComponentModel.DefaultValue("")]
        public string Fault
        {
            get { return __pbn__Fault ?? ""; }
            set { __pbn__Fault = value; }
        }
        public bool ShouldSerializeFault() => __pbn__Fault != null;
        public void ResetFault() => __pbn__Fault = null;
        private string __pbn__Fault;

        [global::ProtoBuf.ProtoMember(2, Name = @"user_id", IsRequired = true)]
        public long UserId { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"user_name", IsRequired = true)]
        public string UserName { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"id", IsRequired = true)]
        public long Id { get; set; }

        [global::ProtoBuf.ProtoMember(5, Name = @"state", IsRequired = true)]
        public WithdrawalCoinResponse.State State { get; set; } = WithdrawalCoinResponse.State.New;

        [global::ProtoBuf.ProtoMember(6, Name = @"create_date", IsRequired = true)]
        public long CreateDate { get; set; }

        [global::ProtoBuf.ProtoMember(7, Name = @"last_modify_date", IsRequired = true)]
        public long LastModifyDate { get; set; }

        [global::ProtoBuf.ProtoMember(8, Name = @"verification_type")]
        [global::System.ComponentModel.DefaultValue("")]
        public string VerificationType
        {
            get { return __pbn__VerificationType ?? ""; }
            set { __pbn__VerificationType = value; }
        }
        public bool ShouldSerializeVerificationType() => __pbn__VerificationType != null;
        public void ResetVerificationType() => __pbn__VerificationType = null;
        private string __pbn__VerificationType;

        [global::ProtoBuf.ProtoMember(9, Name = @"verification_data")]
        [global::System.ComponentModel.DefaultValue("")]
        public string VerificationData
        {
            get { return __pbn__VerificationData ?? ""; }
            set { __pbn__VerificationData = value; }
        }
        public bool ShouldSerializeVerificationData() => __pbn__VerificationData != null;
        public void ResetVerificationData() => __pbn__VerificationData = null;
        private string __pbn__VerificationData;

        [global::ProtoBuf.ProtoMember(10, Name = @"comment")]
        [global::System.ComponentModel.DefaultValue("")]
        public string Comment
        {
            get { return __pbn__Comment ?? ""; }
            set { __pbn__Comment = value; }
        }
        public bool ShouldSerializeComment() => __pbn__Comment != null;
        public void ResetComment() => __pbn__Comment = null;
        private string __pbn__Comment;

        [global::ProtoBuf.ProtoMember(11, Name = @"description", IsRequired = true)]
        public string Description { get; set; }

        [global::ProtoBuf.ProtoMember(12, Name = @"amount", IsRequired = true)]
        public string Amount { get; set; }

        [global::ProtoBuf.ProtoMember(13, Name = @"currency", IsRequired = true)]
        public string Currency { get; set; }

        [global::ProtoBuf.ProtoMember(14, Name = @"account_to", IsRequired = true)]
        public string AccountTo { get; set; }

        [global::ProtoBuf.ProtoMember(15)]
        public long acceptDate
        {
            get { return __pbn__acceptDate.GetValueOrDefault(); }
            set { __pbn__acceptDate = value; }
        }
        public bool ShouldSerializeacceptDate() => __pbn__acceptDate != null;
        public void ResetacceptDate() => __pbn__acceptDate = null;
        private long? __pbn__acceptDate;

        [global::ProtoBuf.ProtoMember(16)]
        public long valueDate
        {
            get { return __pbn__valueDate.GetValueOrDefault(); }
            set { __pbn__valueDate = value; }
        }
        public bool ShouldSerializevalueDate() => __pbn__valueDate != null;
        public void ResetvalueDate() => __pbn__valueDate = null;
        private long? __pbn__valueDate;

        [global::ProtoBuf.ProtoMember(17, IsRequired = true)]
        public long docDate { get; set; }

        [global::ProtoBuf.ProtoMember(18, IsRequired = true)]
        public string docNumber { get; set; }

        [global::ProtoBuf.ProtoMember(19)]
        [global::System.ComponentModel.DefaultValue("")]
        public string correspondentDetails
        {
            get { return __pbn__correspondentDetails ?? ""; }
            set { __pbn__correspondentDetails = value; }
        }
        public bool ShouldSerializecorrespondentDetails() => __pbn__correspondentDetails != null;
        public void ResetcorrespondentDetails() => __pbn__correspondentDetails = null;
        private string __pbn__correspondentDetails;

        [global::ProtoBuf.ProtoMember(20, IsRequired = true)]
        public string accountFrom { get; set; }

        [global::ProtoBuf.ProtoMember(21, Name = @"outcome", IsRequired = true)]
        public bool Outcome { get; set; }

        [global::ProtoBuf.ProtoMember(22, Name = @"external")]
        [global::System.ComponentModel.DefaultValue("")]
        public string External
        {
            get { return __pbn__External ?? ""; }
            set { __pbn__External = value; }
        }
        public bool ShouldSerializeExternal() => __pbn__External != null;
        public void ResetExternal() => __pbn__External = null;
        private string __pbn__External;

        [global::ProtoBuf.ProtoMember(23, IsRequired = true)]
        public string externalKey { get; set; }

        [global::ProtoBuf.ProtoMember(24)]
        public long externalSystemId
        {
            get { return __pbn__externalSystemId.GetValueOrDefault(); }
            set { __pbn__externalSystemId = value; }
        }
        public bool ShouldSerializeexternalSystemId() => __pbn__externalSystemId != null;
        public void ResetexternalSystemId() => __pbn__externalSystemId = null;
        private long? __pbn__externalSystemId;

        [global::ProtoBuf.ProtoMember(25)]
        public long externalServiceId
        {
            get { return __pbn__externalServiceId.GetValueOrDefault(); }
            set { __pbn__externalServiceId = value; }
        }
        public bool ShouldSerializeexternalServiceId() => __pbn__externalServiceId != null;
        public void ResetexternalServiceId() => __pbn__externalServiceId = null;
        private long? __pbn__externalServiceId;

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class OrderCancelled : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"id", IsRequired = true)]
        public long Id { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"oq", IsRequired = true)]
        public string Oq { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"tq", IsRequired = true)]
        public string Tq { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class CancelOrdersResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"orders")]
        public global::System.Collections.Generic.List<OrderCancelled> Orders { get; } = new global::System.Collections.Generic.List<OrderCancelled>();

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class PrivateSubscribeOrderRawChannelRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"expire_control", IsRequired = true)]
        public RequestExpired ExpireControl { get; set; }

        [global::ProtoBuf.ProtoMember(2, IsRequired = true)]
        public SubscribeType subscribe_type { get; set; } = SubscribeType.OnlyEvents;

        [global::ProtoBuf.ProtoContract()]
        public enum SubscribeType
        {
            [global::ProtoBuf.ProtoEnum(Name = @"ONLY_EVENTS")]
            OnlyEvents = 1,
            [global::ProtoBuf.ProtoEnum(Name = @"WITH_INITIAL_STATE")]
            WithInitialState = 2,
        }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class PrivateOrderRawNotification : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"data")]
        public global::System.Collections.Generic.List<PrivateOrderRawEvent> Datas { get; } = new global::System.Collections.Generic.List<PrivateOrderRawEvent>();

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class PrivateOrderRawChannelSubscribedResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"data")]
        public global::System.Collections.Generic.List<PrivateOrderRawEvent> Datas { get; } = new global::System.Collections.Generic.List<PrivateOrderRawEvent>();

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class PrivateOrderRawEvent : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, IsRequired = true)]
        public OrderType order_type { get; set; } = OrderType.Bid;

        [global::ProtoBuf.ProtoMember(2, Name = @"id", IsRequired = true)]
        public long Id { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"timestamp", IsRequired = true)]
        public long Timestamp { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"price")]
        [global::System.ComponentModel.DefaultValue("")]
        public string Price
        {
            get { return __pbn__Price ?? ""; }
            set { __pbn__Price = value; }
        }
        public bool ShouldSerializePrice() => __pbn__Price != null;
        public void ResetPrice() => __pbn__Price = null;
        private string __pbn__Price;

        [global::ProtoBuf.ProtoMember(5, Name = @"quantity", IsRequired = true)]
        public string Quantity { get; set; }

        [global::ProtoBuf.ProtoMember(6, Name = @"currency_pair", IsRequired = true)]
        public string CurrencyPair { get; set; }

        [global::ProtoBuf.ProtoMember(7, Name = @"is_market", IsRequired = true)]
        public bool IsMarket { get; set; }

        [global::ProtoBuf.ProtoMember(8, Name = @"quantity_left_before_cancellation", IsRequired = true)]
        public string QuantityLeftBeforeCancellation { get; set; }

        [global::ProtoBuf.ProtoContract()]
        public enum OrderType
        {
            [global::ProtoBuf.ProtoEnum(Name = @"BID")]
            Bid = 1,
            [global::ProtoBuf.ProtoEnum(Name = @"ASK")]
            Ask = 2,
        }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class PrivateSubscribeTradeChannelRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"expire_control", IsRequired = true)]
        public RequestExpired ExpireControl { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class PrivateTradeChannelSubscribedResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"data")]
        public global::System.Collections.Generic.List<PrivateTradeEvent> Datas { get; } = new global::System.Collections.Generic.List<PrivateTradeEvent>();

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class PrivateTradeNotification : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"data")]
        public global::System.Collections.Generic.List<PrivateTradeEvent> Datas { get; } = new global::System.Collections.Generic.List<PrivateTradeEvent>();

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class PrivateTradeEvent : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"id", IsRequired = true)]
        public long Id { get; set; }

        [global::ProtoBuf.ProtoMember(2, IsRequired = true)]
        public TradeType trade_type { get; set; } = TradeType.Buy;

        [global::ProtoBuf.ProtoMember(3, Name = @"timestamp", IsRequired = true)]
        public long Timestamp { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"price", IsRequired = true)]
        public string Price { get; set; }

        [global::ProtoBuf.ProtoMember(5, Name = @"quantity", IsRequired = true)]
        public string Quantity { get; set; }

        [global::ProtoBuf.ProtoMember(6, Name = @"order_buy_id")]
        public long OrderBuyId
        {
            get { return __pbn__OrderBuyId.GetValueOrDefault(); }
            set { __pbn__OrderBuyId = value; }
        }
        public bool ShouldSerializeOrderBuyId() => __pbn__OrderBuyId != null;
        public void ResetOrderBuyId() => __pbn__OrderBuyId = null;
        private long? __pbn__OrderBuyId;

        [global::ProtoBuf.ProtoMember(7, Name = @"order_sell_id")]
        public long OrderSellId
        {
            get { return __pbn__OrderSellId.GetValueOrDefault(); }
            set { __pbn__OrderSellId = value; }
        }
        public bool ShouldSerializeOrderSellId() => __pbn__OrderSellId != null;
        public void ResetOrderSellId() => __pbn__OrderSellId = null;
        private long? __pbn__OrderSellId;

        [global::ProtoBuf.ProtoMember(8, Name = @"currency_pair", IsRequired = true)]
        public string CurrencyPair { get; set; }

        [global::ProtoBuf.ProtoContract()]
        public enum TradeType
        {
            [global::ProtoBuf.ProtoEnum(Name = @"BUY")]
            Buy = 1,
            [global::ProtoBuf.ProtoEnum(Name = @"SELL")]
            Sell = 2,
        }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class PrivateUnsubscribeRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"expire_control", IsRequired = true)]
        public RequestExpired ExpireControl { get; set; }

        [global::ProtoBuf.ProtoMember(2, IsRequired = true)]
        public ChannelType channel_type { get; set; } = ChannelType.PrivateOrderRaw;

        [global::ProtoBuf.ProtoContract()]
        public enum ChannelType
        {
            [global::ProtoBuf.ProtoEnum(Name = @"PRIVATE_ORDER_RAW")]
            PrivateOrderRaw = 1,
            [global::ProtoBuf.ProtoEnum(Name = @"PRIVATE_TRADE")]
            PrivateTrade = 2,
            [global::ProtoBuf.ProtoEnum(Name = @"PRIVATE_CHANGE_BALANCE")]
            PrivateChangeBalance = 3,
        }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class PrivateChannelUnsubscribedResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"type", IsRequired = true)]
        public PrivateUnsubscribeRequest.ChannelType Type { get; set; } = PrivateUnsubscribeRequest.ChannelType.PrivateOrderRaw;

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class PrivateSubscribeBalanceChangeChannelRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);
        [global::ProtoBuf.ProtoMember(1, Name = @"expire_control", IsRequired = true)]
        public RequestExpired ExpireControl { get; set; }
    }

    [global::ProtoBuf.ProtoContract()]
    public partial class PrivateChangeBalanceChannelSubscribedResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"data")]
        public global::System.Collections.Generic.List<BalanceResponse> Datas { get; } = new global::System.Collections.Generic.List<BalanceResponse>();
    }

    [global::ProtoBuf.ProtoContract()]
    public partial class PrivateChangeBalanceNotification : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"data")]
        public global::System.Collections.Generic.List<BalanceResponse> Datas { get; } = new global::System.Collections.Generic.List<BalanceResponse>();
    }

    [global::ProtoBuf.ProtoContract()]
    public partial class PongResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"ping_time", IsRequired = true)]
        public long PingTime { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class WsRequestMetaData : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"request_type", IsRequired = true)]
        public WsRequestMsgType RequestType { get; set; } = WsRequestMsgType.SubscribeTicker;

        [global::ProtoBuf.ProtoMember(2, Name = @"token")]
        [global::System.ComponentModel.DefaultValue("")]
        public string Token
        {
            get { return __pbn__Token ?? ""; }
            set { __pbn__Token = value; }
        }
        public bool ShouldSerializeToken() => __pbn__Token != null;
        public void ResetToken() => __pbn__Token = null;
        private string __pbn__Token;

        [global::ProtoBuf.ProtoMember(3)]
        [global::System.ComponentModel.DefaultValue("")]
        public string deprecatedSign
        {
            get { return __pbn__deprecatedSign ?? ""; }
            set { __pbn__deprecatedSign = value; }
        }
        public bool ShouldSerializedeprecatedSign() => __pbn__deprecatedSign != null;
        public void ResetdeprecatedSign() => __pbn__deprecatedSign = null;
        private string __pbn__deprecatedSign;

        [global::ProtoBuf.ProtoMember(4, Name = @"sign")]
        public byte[] Sign
        {
            get { return __pbn__Sign; }
            set { __pbn__Sign = value; }
        }
        public bool ShouldSerializeSign() => __pbn__Sign != null;
        public void ResetSign() => __pbn__Sign = null;
        private byte[] __pbn__Sign;

        [global::ProtoBuf.ProtoContract()]
        public enum WsRequestMsgType
        {
            [global::ProtoBuf.ProtoEnum(Name = @"SUBSCRIBE_TICKER")]
            SubscribeTicker = 1,
            [global::ProtoBuf.ProtoEnum(Name = @"SUBSCRIBE_ORDER_BOOK_RAW")]
            SubscribeOrderBookRaw = 2,
            [global::ProtoBuf.ProtoEnum(Name = @"SUBSCRIBE_ORDER_BOOK")]
            SubscribeOrderBook = 3,
            [global::ProtoBuf.ProtoEnum(Name = @"SUBSCRIBE_TRADE")]
            SubscribeTrade = 4,
            [global::ProtoBuf.ProtoEnum(Name = @"SUBSCRIBE_CANDLE")]
            SubscribeCandle = 5,
            [global::ProtoBuf.ProtoEnum(Name = @"UNSUBSCRIBE")]
            Unsubscribe = 6,
            [global::ProtoBuf.ProtoEnum(Name = @"LOGIN")]
            Login = 7,
            [global::ProtoBuf.ProtoEnum(Name = @"PUT_LIMIT_ORDER")]
            PutLimitOrder = 8,
            [global::ProtoBuf.ProtoEnum(Name = @"CANCEL_LIMIT_ORDER")]
            CancelLimitOrder = 9,
            [global::ProtoBuf.ProtoEnum(Name = @"BALANCE")]
            Balance = 10,
            [global::ProtoBuf.ProtoEnum(Name = @"BALANCES")]
            Balances = 11,
            [global::ProtoBuf.ProtoEnum(Name = @"LAST_TRADES")]
            LastTrades = 12,
            [global::ProtoBuf.ProtoEnum(Name = @"TRADES")]
            Trades = 13,
            [global::ProtoBuf.ProtoEnum(Name = @"CLIENT_ORDERS")]
            ClientOrders = 14,
            [global::ProtoBuf.ProtoEnum(Name = @"CLIENT_ORDER")]
            ClientOrder = 15,
            [global::ProtoBuf.ProtoEnum(Name = @"COMMISSION")]
            Commission = 16,
            [global::ProtoBuf.ProtoEnum(Name = @"COMMISSION_COMMON_INFO")]
            CommissionCommonInfo = 17,
            [global::ProtoBuf.ProtoEnum(Name = @"TRADE_HISTORY")]
            TradeHistory = 18,
            [global::ProtoBuf.ProtoEnum(Name = @"MARKET_ORDER")]
            MarketOrder = 19,
            [global::ProtoBuf.ProtoEnum(Name = @"WALLET_ADDRESS")]
            WalletAddress = 20,
            [global::ProtoBuf.ProtoEnum(Name = @"WITHDRAWAL_COIN")]
            WithdrawalCoin = 21,
            [global::ProtoBuf.ProtoEnum(Name = @"WITHDRAWAL_PAYEER")]
            WithdrawalPayeer = 22,
            [global::ProtoBuf.ProtoEnum(Name = @"WITHDRAWAL_CAPITALIST")]
            WithdrawalCapitalist = 23,
            [global::ProtoBuf.ProtoEnum(Name = @"WITHDRAWAL_ADVCASH")]
            WithdrawalAdvcash = 24,
            [global::ProtoBuf.ProtoEnum(Name = @"PRIVATE_SUBSCRIBE_ORDER_RAW")]
            PrivateSubscribeOrderRaw = 25,
            [global::ProtoBuf.ProtoEnum(Name = @"PRIVATE_SUBSCRIBE_TRADE")]
            PrivateSubscribeTrade = 26,
            [global::ProtoBuf.ProtoEnum(Name = @"PRIVATE_UNSUBSCRIBE")]
            PrivateUnsubscribe = 27,
            [global::ProtoBuf.ProtoEnum(Name = @"WITHDRAWAL_YANDEX")]
            WithdrawalYandex = 28,
            [global::ProtoBuf.ProtoEnum(Name = @"WITHDRAWAL_QIWI")]
            WithdrawalQiwi = 29,
            [global::ProtoBuf.ProtoEnum(Name = @"WITHDRAWAL_CARD")]
            WithdrawalCard = 30,
            [global::ProtoBuf.ProtoEnum(Name = @"WITHDRAWAL_MASTERCARD")]
            WithdrawalMastercard = 31,
            [global::ProtoBuf.ProtoEnum(Name = @"WITHDRAWAL_PERFECTMONEY")]
            WithdrawalPerfectmoney = 32,
            [global::ProtoBuf.ProtoEnum(Name = @"VOUCHER_MAKE")]
            VoucherMake = 33,
            [global::ProtoBuf.ProtoEnum(Name = @"VOUCHER_AMOUNT")]
            VoucherAmount = 34,
            [global::ProtoBuf.ProtoEnum(Name = @"VOUCHER_REDEEM")]
            VoucherRedeem = 35,
            [global::ProtoBuf.ProtoEnum(Name = @"CANCEL_ORDERS")]
            CancelOrders = 36,
            [global::ProtoBuf.ProtoEnum(Name = @"PING_REQUEST")]
            PingRequest = 37,
            [global::ProtoBuf.ProtoEnum(Name = @"SUBSCRIBE_BALANCE_CHANGE")]
            SubscribeBalanceChange = 38,
        }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class WsResponseMetaData : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"response_type", IsRequired = true)]
        public WsResponseMsgType ResponseType { get; set; } = WsResponseMsgType.TickerChannelSubscribed;

        [global::ProtoBuf.ProtoMember(2, Name = @"token")]
        [global::System.ComponentModel.DefaultValue("")]
        public string Token
        {
            get { return __pbn__Token ?? ""; }
            set { __pbn__Token = value; }
        }
        public bool ShouldSerializeToken() => __pbn__Token != null;
        public void ResetToken() => __pbn__Token = null;
        private string __pbn__Token;

        [global::ProtoBuf.ProtoContract()]
        public enum WsResponseMsgType
        {
            [global::ProtoBuf.ProtoEnum(Name = @"TICKER_CHANNEL_SUBSCRIBED")]
            TickerChannelSubscribed = 1,
            [global::ProtoBuf.ProtoEnum(Name = @"ORDER_BOOK_RAW_CHANNEL_SUBSCRIBED")]
            OrderBookRawChannelSubscribed = 2,
            [global::ProtoBuf.ProtoEnum(Name = @"ORDER_BOOK_CHANNEL_SUBSCRIBED")]
            OrderBookChannelSubscribed = 3,
            [global::ProtoBuf.ProtoEnum(Name = @"TRADE_CHANNEL_SUBSCRIBED")]
            TradeChannelSubscribed = 4,
            [global::ProtoBuf.ProtoEnum(Name = @"CANDLE_CHANNEL_SUBSCRIBED")]
            CandleChannelSubscribed = 5,
            [global::ProtoBuf.ProtoEnum(Name = @"CHANNEL_UNSUBSCRIBED")]
            ChannelUnsubscribed = 6,
            [global::ProtoBuf.ProtoEnum(Name = @"ERROR")]
            Error = 7,
            [global::ProtoBuf.ProtoEnum(Name = @"TICKER_NOTIFY")]
            TickerNotify = 8,
            [global::ProtoBuf.ProtoEnum(Name = @"ORDER_BOOK_RAW_NOTIFY")]
            OrderBookRawNotify = 9,
            [global::ProtoBuf.ProtoEnum(Name = @"ORDER_BOOK_NOTIFY")]
            OrderBookNotify = 10,
            [global::ProtoBuf.ProtoEnum(Name = @"TRADE_NOTIFY")]
            TradeNotify = 11,
            [global::ProtoBuf.ProtoEnum(Name = @"CANDLE_NOTIFY")]
            CandleNotify = 12,
            [global::ProtoBuf.ProtoEnum(Name = @"LOGIN_RESPONSE")]
            LoginResponse = 13,
            [global::ProtoBuf.ProtoEnum(Name = @"PUT_LIMIT_ORDER_RESPONSE")]
            PutLimitOrderResponse = 14,
            [global::ProtoBuf.ProtoEnum(Name = @"CANCEL_LIMIT_ORDER_RESPONSE")]
            CancelLimitOrderResponse = 15,
            [global::ProtoBuf.ProtoEnum(Name = @"BALANCE_RESPONSE")]
            BalanceResponse = 16,
            [global::ProtoBuf.ProtoEnum(Name = @"BALANCES_RESPONSE")]
            BalancesResponse = 17,
            [global::ProtoBuf.ProtoEnum(Name = @"LAST_TRADES_RESPONSE")]
            LastTradesResponse = 18,
            [global::ProtoBuf.ProtoEnum(Name = @"TRADES_RESPONSE")]
            TradesResponse = 19,
            [global::ProtoBuf.ProtoEnum(Name = @"CLIENT_ORDERS_RESPONSE")]
            ClientOrdersResponse = 20,
            [global::ProtoBuf.ProtoEnum(Name = @"CLIENT_ORDER_RESPONSE")]
            ClientOrderResponse = 21,
            [global::ProtoBuf.ProtoEnum(Name = @"COMMISSION_RESPONSE")]
            CommissionResponse = 22,
            [global::ProtoBuf.ProtoEnum(Name = @"COMMISSION_COMMON_INFO_RESPONSE")]
            CommissionCommonInfoResponse = 23,
            [global::ProtoBuf.ProtoEnum(Name = @"TRADE_HISTORY_RESPONSE")]
            TradeHistoryResponse = 24,
            [global::ProtoBuf.ProtoEnum(Name = @"MARKET_ORDER_RESPONSE")]
            MarketOrderResponse = 25,
            [global::ProtoBuf.ProtoEnum(Name = @"WALLET_ADDRESS_RESPONSE")]
            WalletAddressResponse = 26,
            [global::ProtoBuf.ProtoEnum(Name = @"WITHDRAWAL_COIN_RESPONSE")]
            WithdrawalCoinResponse = 27,
            [global::ProtoBuf.ProtoEnum(Name = @"WITHDRAWAL_PAYEER_RESPONSE")]
            WithdrawalPayeerResponse = 28,
            [global::ProtoBuf.ProtoEnum(Name = @"WITHDRAWAL_CAPITALIST_RESPONSE")]
            WithdrawalCapitalistResponse = 29,
            [global::ProtoBuf.ProtoEnum(Name = @"WITHDRAWAL_ADVCASH_RESPONSE")]
            WithdrawalAdvcashResponse = 30,
            [global::ProtoBuf.ProtoEnum(Name = @"PRIVATE_ORDER_RAW_CHANNEL_SUBSCRIBED")]
            PrivateOrderRawChannelSubscribed = 31,
            [global::ProtoBuf.ProtoEnum(Name = @"PRIVATE_TRADE_CHANNEL_SUBSCRIBED")]
            PrivateTradeChannelSubscribed = 32,
            [global::ProtoBuf.ProtoEnum(Name = @"PRIVATE_ORDER_RAW_NOTIFY")]
            PrivateOrderRawNotify = 33,
            [global::ProtoBuf.ProtoEnum(Name = @"PRIVATE_TRADE_NOTIFY")]
            PrivateTradeNotify = 34,
            [global::ProtoBuf.ProtoEnum(Name = @"PRIVATE_CHANNEL_UNSUBSCRIBED")]
            PrivateChannelUnsubscribed = 35,
            [global::ProtoBuf.ProtoEnum(Name = @"WITHDRAWAL_YANDEX_RESPONSE")]
            WithdrawalYandexResponse = 36,
            [global::ProtoBuf.ProtoEnum(Name = @"WITHDRAWAL_QIWI_RESPONSE")]
            WithdrawalQiwiResponse = 37,
            [global::ProtoBuf.ProtoEnum(Name = @"WITHDRAWAL_CARD_RESPONSE")]
            WithdrawalCardResponse = 38,
            [global::ProtoBuf.ProtoEnum(Name = @"WITHDRAWAL_MASTERCARD_RESPONSE")]
            WithdrawalMastercardResponse = 39,
            [global::ProtoBuf.ProtoEnum(Name = @"WITHDRAWAL_PERFECTMONEY_RESPONSE")]
            WithdrawalPerfectmoneyResponse = 40,
            [global::ProtoBuf.ProtoEnum(Name = @"VOUCHER_MAKE_RESPONSE")]
            VoucherMakeResponse = 41,
            [global::ProtoBuf.ProtoEnum(Name = @"VOUCHER_AMOUNT_RESPONSE")]
            VoucherAmountResponse = 42,
            [global::ProtoBuf.ProtoEnum(Name = @"VOUCHER_REDEEM_RESPONSE")]
            VoucherRedeemResponse = 43,
            [global::ProtoBuf.ProtoEnum(Name = @"CANCEL_ORDERS_RESPONSE")]
            CancelOrdersResponse = 44,
            [global::ProtoBuf.ProtoEnum(Name = @"PONG_RESPONSE")]
            PongResponse = 45,
            [global::ProtoBuf.ProtoEnum(Name = @"BALANCE_CHANGE_CHANNEL_SUBSCRIBED")]
            BalanceChangeChannelSubscribed = 46,
            [global::ProtoBuf.ProtoEnum(Name = @"BALANCE_CHANGE_NOTIFY")]
            BalanceChangeNotify = 47,
        }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class WsRequest : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"meta", IsRequired = true)]
        public WsRequestMetaData Meta { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"msg", IsRequired = true)]
        public byte[] Msg { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class WsResponse : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"meta", IsRequired = true)]
        public WsResponseMetaData Meta { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"msg", IsRequired = true)]
        public byte[] Msg { get; set; }

    }

}

