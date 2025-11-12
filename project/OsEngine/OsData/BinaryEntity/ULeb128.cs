using System.IO;

namespace OsEngine.OsData.BinaryEntity
{
    public static class ULeb128
    {
        public const uint Max1BValue = 127;
        public const uint Max2BValue = 16383;
        public const uint Max3BValue = 2097151;
        public const uint Max4BValue = 268435455;

        public static byte WriteULeb128(BinaryWriter writer, ulong value)
        {
            do
            {
                byte b = (byte)(value & 0x7F);
                value >>= 7;
                if (value != 0) b |= 0x80;
                writer.Write(b);
            } while (value != 0);

            return (byte)value;
        }

        public static uint Read(Stream stream)
        {
            uint value = 0;
            int shift = 0;

            for (; ; )
            {
                uint b = (uint)stream.ReadByte();

                if (b == 0xffffffff)
                    throw new EndOfStreamException();

                value |= (b & 0x7f) << shift;

                if ((b & 0x80) == 0)
                    return value;

                shift += 7;
            }
        }
    }
}
