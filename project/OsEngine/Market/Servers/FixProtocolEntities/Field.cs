namespace OsEngine.Market.Servers.FixProtocolEntities
{
    public class Field
    {
        public int Tag;
        public string Value;

        public Field(int tag, string value)
        {
            Tag = tag;
            Value = value;
        }
    }
}
