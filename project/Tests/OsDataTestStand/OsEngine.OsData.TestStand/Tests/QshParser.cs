/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace OsEngine.OsData.TestStand.Tests
{
    /// <summary>
    /// Парсер бинарного формата .qsh («QScalp History Data», версия 4) для проверки
    /// стаканов (исторических и онлайн). Возвращает сводку по срезам и нарушениям.
    /// </summary>
    public static class QshParser
    {
        private const string Prefix = "QScalp History Data";
        private const ulong GrowingMarker = 268435455; // ULeb128.Max4BValue

        public static QshStats Parse(string filePath)
        {
            QshStats stats = new QshStats();
            bool headerParsed = false;

            try
            {
                using (FileStream fs = File.OpenRead(filePath))
                using (Stream stream = OpenStream(fs))
                {
                    if (stream == null)
                    {
                        return null;
                    }

                    QshReader reader = new QshReader(stream);

                    int version = reader.ReadByte();

                    if (version != 4)
                    {
                        return null;
                    }

                    reader.ReadString(); // appName
                    reader.ReadString(); // comment (VolumeStep)
                    reader.ReadInt64(); // time
                    int streamCount = reader.ReadByte();

                    if (streamCount < 1 || streamCount > 32)
                    {
                        return null;
                    }

                    stats.StreamCount = streamCount;

                    byte[] streamTypes = new byte[streamCount];

                    for (int i = 0; i < streamCount; i++)
                    {
                        streamTypes[i] = (byte)reader.ReadByte();

                        if (streamTypes[i] != 0x50) // Messages без securityName
                        {
                            reader.ReadString(); // securityName
                        }
                    }

                    headerParsed = true;

                    long lastTimeStamp = 0;
                    long lastPrice = 0;
                    long dealsLastMs = 0;
                    long dealsLastId = 0;
                    long dealsLastPrice = 0;
                    long prevFrameTime = long.MinValue;
                    long prevDealMs = long.MinValue;
                    long maxBid = long.MinValue;
                    long minAsk = long.MaxValue;
                    Dictionary<long, long> bids = new Dictionary<long, long>();
                    Dictionary<long, long> asks = new Dictionary<long, long>();

                    while (true)
                    {
                        lastTimeStamp = reader.ReadGrowing(lastTimeStamp);

                        // глобальная хронология: время кадра не должно откатываться назад
                        if (lastTimeStamp < prevFrameTime)
                        {
                            stats.NonMonotonic++;
                        }
                        prevFrameTime = lastTimeStamp;

                        int streamIndex = 0;

                        if (streamCount > 1)
                        {
                            streamIndex = reader.ReadByte();
                        }

                        if (streamIndex < 0 || streamIndex >= streamCount)
                        {
                            return null;
                        }

                        byte streamType = streamTypes[streamIndex];

                        if (streamType == 0x10) // Quotes
                        {
                            long count = reader.ReadLeb128();

                            for (long i = 0; i < count; i++)
                            {
                                long priceDelta = reader.ReadLeb128();
                                lastPrice += priceDelta;
                                long volume = reader.ReadLeb128();

                                // Сброс аккумулятора цены (delete с ценой 0) — легитимный маркер Qscalp,
                                // а не реальный уровень. Проверяем цену только для реальных уровней.
                                if (lastPrice <= 0 && volume != 0)
                                {
                                    stats.BadPrice++;
                                }

                                if (volume == 0)
                                {
                                    if (bids.Remove(lastPrice) && lastPrice == maxBid)
                                    {
                                        maxBid = FindMax(bids);
                                    }

                                    if (asks.Remove(lastPrice) && lastPrice == minAsk)
                                    {
                                        minAsk = FindMin(asks);
                                    }
                                }
                                else if (volume > 0)
                                {
                                    asks[lastPrice] = volume;

                                    if (bids.Remove(lastPrice) && lastPrice == maxBid)
                                    {
                                        maxBid = FindMax(bids);
                                    }

                                    if (lastPrice < minAsk)
                                    {
                                        minAsk = lastPrice;
                                    }
                                }
                                else
                                {
                                    bids[lastPrice] = -volume;

                                    if (asks.Remove(lastPrice) && lastPrice == minAsk)
                                    {
                                        minAsk = FindMin(asks);
                                    }

                                    if (lastPrice > maxBid)
                                    {
                                        maxBid = lastPrice;
                                    }
                                }
                            }

                            stats.Frames++;
                            stats.Changes += count;

                            if (bids.Count > 0 && asks.Count > 0 && maxBid > minAsk)
                            {
                                stats.Crossed++;
                            }
                        }
                        else if (streamType == 0x20) // Deals
                        {
                            byte flags = (byte)reader.ReadByte();

                            if ((flags & 0x04) != 0) dealsLastMs = reader.ReadGrowing(dealsLastMs);
                            if ((flags & 0x08) != 0) dealsLastId = reader.ReadGrowing(dealsLastId);
                            if ((flags & 0x10) != 0) reader.ReadLeb128();
                            if ((flags & 0x20) != 0) dealsLastPrice += reader.ReadLeb128();
                            if ((flags & 0x40) != 0) reader.ReadLeb128();
                            if ((flags & 0x80) != 0) reader.ReadLeb128();

                            // монотонность биржевого времени сделки (DATETIME)
                            if (dealsLastMs < prevDealMs)
                            {
                                stats.NonMonotonicDeals++;
                            }
                            prevDealMs = dealsLastMs;

                            // цена сделки не должна уходить далеко от текущего стакана
                            if (maxBid != long.MinValue && minAsk != long.MaxValue)
                            {
                                if (dealsLastPrice * 1000 > minAsk * 1003
                                    || dealsLastPrice * 1000 < maxBid * 997)
                                {
                                    stats.PriceOutOfRange++;
                                }
                            }

                            stats.Deals++;
                        }
                        else
                        {
                            return null;
                        }
                    }
                }
            }
            catch (EndOfStreamException)
            {
                return headerParsed ? stats : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static long FindMax(Dictionary<long, long> map)
        {
            long max = long.MinValue;

            foreach (long key in map.Keys)
            {
                if (key > max)
                {
                    max = key;
                }
            }

            return max;
        }

        private static long FindMin(Dictionary<long, long> map)
        {
            long min = long.MaxValue;

            foreach (long key in map.Keys)
            {
                if (key < min)
                {
                    min = key;
                }
            }

            return min;
        }

        private static Stream OpenStream(FileStream fs)
        {
            byte[] prefix = Encoding.UTF8.GetBytes(Prefix);

            if (HasPrefix(fs, prefix))
            {
                return fs;
            }

            fs.Position = 0;

            try
            {
                GZipStream gzip = new GZipStream(fs, CompressionMode.Decompress, true);

                if (HasPrefix(gzip, prefix))
                {
                    return gzip;
                }

                gzip.Dispose();
            }
            catch
            {
                // не gzip
            }

            fs.Position = 0;

            try
            {
                DeflateStream deflate = new DeflateStream(fs, CompressionMode.Decompress, true);

                if (HasPrefix(deflate, prefix))
                {
                    return deflate;
                }

                deflate.Dispose();
            }
            catch
            {
                // не deflate
            }

            return null;
        }

        private static bool HasPrefix(Stream stream, byte[] prefix)
        {
            for (int i = 0; i < prefix.Length; i++)
            {
                int b = stream.ReadByte();

                if (b < 0 || b != prefix[i])
                {
                    return false;
                }
            }

            return true;
        }

        private sealed class QshReader
        {
            private readonly Stream _stream;

            public QshReader(Stream stream)
            {
                _stream = stream;
            }

            public int ReadByte()
            {
                return _stream.ReadByte();
            }

            public long ReadInt64()
            {
                byte[] buffer = new byte[8];

                for (int i = 0; i < buffer.Length; i++)
                {
                    int b = _stream.ReadByte();

                    if (b < 0)
                    {
                        throw new EndOfStreamException();
                    }

                    buffer[i] = (byte)b;
                }

                return BitConverter.ToInt64(buffer, 0);
            }

            public ulong ReadUleb128()
            {
                ulong value = 0;
                int shift = 0;

                while (true)
                {
                    int b = _stream.ReadByte();

                    if (b < 0)
                    {
                        throw new EndOfStreamException();
                    }

                    value |= (ulong)(b & 0x7f) << shift;

                    if ((b & 0x80) == 0)
                    {
                        return value;
                    }

                    shift += 7;
                }
            }

            public long ReadLeb128()
            {
                long value = 0;
                int shift = 0;

                while (true)
                {
                    int b = _stream.ReadByte();

                    if (b < 0)
                    {
                        throw new EndOfStreamException();
                    }

                    value |= (long)(b & 0x7f) << shift;
                    shift += 7;

                    if ((b & 0x80) == 0)
                    {
                        if (shift < 64 && (b & 0x40) != 0)
                        {
                            value |= -(1L << shift);
                        }

                        return value;
                    }
                }
            }

            public long ReadGrowing(long lastValue)
            {
                ulong offset = ReadUleb128();

                if (offset == GrowingMarker)
                {
                    return lastValue + ReadLeb128();
                }

                return lastValue + (long)offset;
            }

            public string ReadString()
            {
                ulong length = ReadUleb128();

                if (length > int.MaxValue)
                {
                    throw new InvalidDataException("Строка в .qsh слишком длинная");
                }

                byte[] buffer = new byte[(int)length];
                int read = 0;

                while (read < buffer.Length)
                {
                    int r = _stream.Read(buffer, read, buffer.Length - read);

                    if (r <= 0)
                    {
                        throw new EndOfStreamException();
                    }

                    read += r;
                }

                return Encoding.UTF8.GetString(buffer);
            }
        }
    }

    public class QshStats
    {
        public int StreamCount;

        public long Frames;

        public long Changes;

        public long Crossed;

        public long BadPrice;

        public long BadVolume;

        public long Deals;

        public long NonMonotonic;

        public long NonMonotonicDeals;

        public long PriceOutOfRange;
    }
}
