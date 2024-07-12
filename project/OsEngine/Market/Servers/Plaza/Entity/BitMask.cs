namespace OsEngine.Market.Servers.Plaza.Entity
{
    public enum BitMask : long
    {
        // Типы рыночных заявок

        Auction = 0x1,
        Opposite = 0x2,
        FOK = 0x80000,
        BOC = 0x1000000000000000,

        // Типы клиринговых сделок

        NonQuote = 0x4,
        Exec = 0x20,
        Expiration = 0x80,
        DUFlow = 0x800,
        TASSettlement = 0x10000,
        OptionLapse = 0x800000,
        ClearingTrade = 0x2000000,
        FuturesExecution = 0x40000000,
        CollateralInstrument = 0x400000000,
        PerpetualFuturesExecutionVoluntary = 0x10000000000000,
        PerpetualFuturesExecutionForced = 0x400000000000000,
        PerpetualFuturesExecution = 0x800000000000000,

        // Адресные заявки и сделки

        TransferClientPosition = 0x8,
        Address = 0x4000000,
        NegotiatedMatchByRef = 0x80000000,
        TransferSource = 0x200000000,

        // Операции над связками

        REPOBack = 0x4000,
        Strategy = 0x8000000,

        // Другое

        DontCheckMoney = 0x10,
        ExternalUseEveningExecution = 0x100,
        DontCheckLimits = 0x200,
        Charge = 0x400,
        LastRec = 0x1000,
        DueToCrossCancel = 0x2000,
        MoveOperation = 0x100000,
        DeleteOperation = 0x200000,
        BulkDeleteOperation = 0x400000,
        OppositeOrderTailDeleteDueToCrossTrade = 0x20000000,
        CODBulkDeleteOperation = 0x100000000,
        FineOperation = 0x1000000000,
        UKSBulkDeleteOperation = 0x2000000000,
        NCCRequest = 0x4000000000,
        NCCBulkDeleteOperation = 0x8000000000,
        LiqNettingRF = 0x10000000000,
        ActiveSide = 0x20000000000,
        PassiveSide = 0x40000000000,
        Synthetic = 0x200000000000,
        RFSOrder = 0x400000000000,
        Iceberg = 0x800000000000,
        OperatorInputSA = 0x1000000000000,
        DontFineRF = 0x80000000000000,
        MorningSession = 0x100000000000000,
        SyntheticPassive = 0x200000000000000,
        DuringDiscreteAuction = 0x4000000000000000
    }
}