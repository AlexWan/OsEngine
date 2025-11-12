using System.IO;

namespace OsEngine.OsData.BinaryEntity
{
    public class DataBinaryReader : BinaryReader
    {
        public DataBinaryReader(Stream stream) : base(stream) { }

        public long ReadGrowing(long lastValue)
        {
            uint offset = ULeb128.Read(BaseStream);

            if (offset == ULeb128.Max4BValue)
                return lastValue + Leb128.Read(BaseStream);
            else
                return lastValue + offset;
        }

        public long ReadLeb128() { return Leb128.Read(BaseStream); }
    }
}
