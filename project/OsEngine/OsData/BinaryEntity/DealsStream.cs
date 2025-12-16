using OsEngine.Entity;
using OsEngine.Market.Servers.Entity;
using System;

namespace OsEngine.OsData.BinaryEntity
{
    public class DealsStream
    {
        public long lastMilliseconds;
        public long lastId;
        public long lastOrderId;
        public long lastPrice;
        public long lastVolume;
        public long lastOI;

        public Trade Read(DataBinaryReader dataReader, decimal priceStep, decimal volumeStep)
        {
            DealFlags flags = (DealFlags)dataReader.ReadByte();

            if ((flags & DealFlags.DateTime) != 0)
                lastMilliseconds = dataReader.ReadGrowing(lastMilliseconds);

            if ((flags & DealFlags.Id) != 0)
                lastId = dataReader.ReadGrowing(lastId);

            if ((flags & DealFlags.OrderId) != 0)
                lastOrderId += dataReader.ReadLeb128();

            if ((flags & DealFlags.Price) != 0)
                lastPrice += dataReader.ReadLeb128();

            if ((flags & DealFlags.Volume) != 0)
                lastVolume = dataReader.ReadLeb128();

            if ((flags & DealFlags.OI) != 0)
                lastOI += dataReader.ReadLeb128();

            Trade trade = new Trade();
            trade.Id = lastId.ToString();
            trade.Price = lastPrice * priceStep;
            trade.Side = (Side)(flags & DealFlags.Type);
            trade.Volume = lastVolume * volumeStep;

            if (lastMilliseconds == 0)
                trade.Time = DateTime.MinValue;
            else
                trade.Time = TimeManager.GetDateTimeFromStartTimeMilliseconds(lastMilliseconds);

                trade.OpenInterest = lastOI;

            return trade;
        }

        public void Write(DataBinaryWriter binaryWriter, Trade trade, decimal priceStep, decimal volumeStep)
        {
            DealFlags flags = DealFlags.None;

            if (trade.Side == Side.None)
            {
                flags |= (DealFlags)0;
            }
            else if (trade.Side == Side.Buy)
            {
                flags |= (DealFlags)1;
            }
            else if (trade.Side == Side.Sell)
            {
                flags |= (DealFlags)2;
            }

            long currentTime = TimeManager.GetTimeStampMillisecondsFromStartTime(trade.Time);
            long deltaTime = currentTime - lastMilliseconds;
            if (deltaTime != 0)
            {
                lastMilliseconds = currentTime;
                flags |= DealFlags.DateTime;
            }

            long deltaId = 0;
            if (long.TryParse(trade.Id, out long numericId))
            {
                deltaId = numericId - lastId;

                if (deltaId != 0)
                {
                    lastId = numericId;
                    flags |= DealFlags.Id;
                }
            }

            long priceInTicks = (long)(trade.Price / priceStep);
            long deltaPrice = priceInTicks - lastPrice;
            if (deltaPrice != 0 || lastPrice == 0)
            {
                lastPrice = priceInTicks;
                flags |= DealFlags.Price;
            }

            long volumeInSteps = (long)(trade.Volume / volumeStep);
            if (volumeInSteps != 0 || lastVolume == 0)
            {
                lastVolume = volumeInSteps;
                flags |= DealFlags.Volume;
            }

            long deltaOI = 0;
            if (trade.OpenInterest != 0)
            {
                long oiInSteps = (long)(trade.OpenInterest / volumeStep);
                deltaOI = oiInSteps - lastOI;
                if (deltaOI != 0 || lastOI == 0)
                {
                    lastOI = oiInSteps;
                    flags |= DealFlags.OI;
                }
            }

            binaryWriter.Write((byte)flags);

            if ((flags & DealFlags.DateTime) != 0)
                binaryWriter.WriteGrowing(deltaTime);

            if ((flags & DealFlags.Id) != 0)
                binaryWriter.WriteGrowing(deltaId);

            if ((flags & DealFlags.OrderId) != 0)
                binaryWriter.WriteLeb128(lastOrderId);

            if ((flags & DealFlags.Price) != 0)
                binaryWriter.WriteLeb128(deltaPrice);

            if ((flags & DealFlags.Volume) != 0)
                binaryWriter.WriteLeb128(volumeInSteps);

            if ((flags & DealFlags.OI) != 0)
                binaryWriter.WriteLeb128(deltaOI);
        }
    }
}
