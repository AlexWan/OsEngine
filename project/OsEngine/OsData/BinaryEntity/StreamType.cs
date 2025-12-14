using System;

namespace OsEngine.OsData.BinaryEntity
{
    enum StreamType
    {
        Quotes = 0x10,
        Deals = 0x20,
        OwnOrders = 0x30,
        OwnTrades = 0x40,
        Messages = 0x50,
        AuxInfo = 0x60,
        OrdLog = 0x70,
        None = 0
    }

    [Flags]
    enum DealFlags
    {
        Type = 0x03,
        DateTime = 0x04,
        Id = 0x08,
        OrderId = 0x10,
        Price = 0x20,
        Volume = 0x40,
        OI = 0x80,
        None = 0
    }
}
