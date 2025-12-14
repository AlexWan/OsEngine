using System.IO;

namespace OsEngine.OsData.BinaryEntity
{
    public class DataBinaryWriter : BinaryWriter
    {
        public DataBinaryWriter(Stream stream) : base(stream) { }

        public void WriteGrowing(long value)
        {
            if (value >= 0 && value <= 268435454)
            {
                ULeb128.WriteULeb128(this, ((ulong)value));
            }
            else
            {
                ULeb128.WriteULeb128(this, 268435455);
                Leb128.WriteLeb128(this, value);
            }
        }

        public void WriteLeb128(long value) { Leb128.WriteLeb128(this, value); }
    }
}
