/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using OsEngine.Market.Servers.Entity;
using OsEngine.OsData.BinaryEntity;

namespace OsEngine.Market.Servers.QscalpMarketDepth
{
    /// <summary>
    /// Сливает два одиночных QSH-файла (Quotes.qsh + Deals.qsh) в один
    /// мульти-стрим файл (streamCount = 2) с чередующимися по времени кадрами.
    /// Кадры копируются побайтово (payload не перекодируется), пересчитывается только
    /// глобальное время кадра и добавляется байт stream_index.
    /// </summary>
    public static class QshMerger
    {
        private static readonly byte[] Signature = Encoding.UTF8.GetBytes("QScalp History Data");

        public static void Merge(string quotesFilePath, string dealsFilePath, string outputFilePath)
        {
            string quotesComment;
            string quotesSecurityName;
            List<QshFrame> frames = ReadFrames(quotesFilePath, 0, out quotesComment, out quotesSecurityName);

            string dealsComment;
            string dealsSecurityName;
            List<QshFrame> dealsFrames = ReadFrames(dealsFilePath, 1, out dealsComment, out dealsSecurityName);

            frames.AddRange(dealsFrames);

            if (frames.Count == 0)
            {
                throw new InvalidDataException("Нет кадров для слияния");
            }

            frames = frames.OrderBy(f => f.TimeMs).ToList();

            long baseMs = frames[0].TimeMs;

            Write(frames, quotesComment, quotesSecurityName, dealsSecurityName, baseMs, outputFilePath);
        }

        private static void Write(List<QshFrame> frames, string comment, string quotesSecurityName,
            string dealsSecurityName, long baseMs, string outputFilePath)
        {
            using (FileStream fs = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write))
            using (DataBinaryWriter writer = new DataBinaryWriter(fs))
            {
                writer.Write(Signature);
                writer.Write((byte)4);
                WriteString(writer, "OsEngine");
                WriteString(writer, string.IsNullOrEmpty(comment) ? "VolumeStep:1" : comment);

                DateTime baseTime = TimeManager.GetDateTimeFromStartTimeMilliseconds(baseMs);
                writer.Write(baseTime.Ticks);

                writer.Write((byte)2);

                writer.Write((byte)StreamType.Quotes);
                WriteString(writer, quotesSecurityName ?? string.Empty);

                writer.Write((byte)StreamType.Deals);
                WriteString(writer, dealsSecurityName ?? string.Empty);

                long globalLastMs = baseMs;

                foreach (QshFrame frame in frames)
                {
                    writer.WriteGrowing(frame.TimeMs - globalLastMs);
                    globalLastMs = frame.TimeMs;

                    writer.Write(frame.StreamIndex);
                    writer.Write(frame.Payload);
                }

                writer.Flush();
            }
        }

        private static List<QshFrame> ReadFrames(string filePath, byte streamIndex, out string comment, out string securityName)
        {
            comment = null;
            securityName = null;

            byte[] data = DecompressIfNeeded(File.ReadAllBytes(filePath));

            List<QshFrame> frames = new List<QshFrame>();

            using (MemoryStream ms = new MemoryStream(data))
            using (DataBinaryReader reader = new DataBinaryReader(ms))
            {
                reader.ReadBytes(Signature.Length);

                int version = reader.ReadByte();
                if (version != 4)
                {
                    throw new InvalidDataException("Unsupported QSH version: " + version);
                }

                reader.ReadString(); // appName
                comment = reader.ReadString();

                long createdTicks = reader.ReadInt64();
                long createdMs = TimeManager.GetTimeStampMillisecondsFromStartTime(new DateTime(createdTicks, DateTimeKind.Utc));

                reader.ReadByte(); // streamCount (игнорируем, читаем один поток)

                byte streamType = reader.ReadByte();
                securityName = streamType == (byte)StreamType.Messages ? null : reader.ReadString();

                bool isDeals = streamType == (byte)StreamType.Deals;

                long lastFrameMs = createdMs;

                // frame header в Qscalp в UTC, а DATETIME сделки — в Москве; переводим кадры в Москву
                lastFrameMs += TimeManager.MoscowUtcOffsetMs;

                long lastDealMs = 0;
                long lastDealId = 0;

                while (ms.Position < ms.Length)
                {
                    long payloadStart;

                    try
                    {
                        lastFrameMs = reader.ReadGrowing(lastFrameMs);

                        payloadStart = ms.Position;

                        if (streamType == (byte)StreamType.Quotes)
                        {
                            long count = reader.ReadLeb128();
                            for (long i = 0; i < count; i++)
                            {
                                reader.ReadLeb128(); // price delta
                                reader.ReadLeb128(); // volume
                            }
                        }
                        else if (streamType == (byte)StreamType.Deals)
                        {
                            byte flags = reader.ReadByte();

                            if ((flags & (byte)DealFlags.DateTime) != 0) lastDealMs = reader.ReadGrowing(lastDealMs);
                            if ((flags & (byte)DealFlags.Id) != 0) lastDealId = reader.ReadGrowing(lastDealId);
                            if ((flags & (byte)DealFlags.OrderId) != 0) reader.ReadLeb128();
                            if ((flags & (byte)DealFlags.Price) != 0) reader.ReadLeb128();
                            if ((flags & (byte)DealFlags.Volume) != 0) reader.ReadLeb128();
                            if ((flags & (byte)DealFlags.OI) != 0) reader.ReadLeb128();
                        }
                        else
                        {
                            throw new InvalidDataException("Unsupported stream type: " + streamType);
                        }
                    }
                    catch (EndOfStreamException)
                    {
                        break;
                    }

                    long payloadEnd = ms.Position;
                    byte[] payload = new byte[payloadEnd - payloadStart];
                    Array.Copy(data, payloadStart, payload, 0, payload.Length);

                    // сделки сортируем по биржевому времени DATETIME (точнее грубого frame header),
                    // стакан — по frame header. У первой сделки DATETIME уже задан (DateTime=true).
                    long sortTimeMs = isDeals && lastDealMs != 0 ? lastDealMs : lastFrameMs;

                    frames.Add(new QshFrame { TimeMs = sortTimeMs, StreamIndex = streamIndex, Payload = payload });
                }
            }

            return frames;
        }

        private static void WriteString(BinaryWriter writer, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            ULeb128.WriteULeb128(writer, (ulong)bytes.Length);
            writer.Write(bytes);
        }

        private static byte[] DecompressIfNeeded(byte[] data)
        {
            if (StartsWithSignature(data))
            {
                return data;
            }

            try
            {
                using (MemoryStream input = new MemoryStream(data))
                using (GZipStream gzip = new GZipStream(input, CompressionMode.Decompress))
                using (MemoryStream output = new MemoryStream())
                {
                    gzip.CopyTo(output);
                    return output.ToArray();
                }
            }
            catch
            {
                // не gzip
            }

            try
            {
                using (MemoryStream input = new MemoryStream(data))
                using (DeflateStream deflate = new DeflateStream(input, CompressionMode.Decompress))
                using (MemoryStream output = new MemoryStream())
                {
                    deflate.CopyTo(output);
                    return output.ToArray();
                }
            }
            catch
            {
                // не deflate
            }

            return data;
        }

        private static bool StartsWithSignature(byte[] data)
        {
            if (data.Length < Signature.Length)
            {
                return false;
            }

            for (int i = 0; i < Signature.Length; i++)
            {
                if (data[i] != Signature[i])
                {
                    return false;
                }
            }

            return true;
        }

        private class QshFrame
        {
            public long TimeMs;

            public byte StreamIndex;

            public byte[] Payload;
        }
    }
}
