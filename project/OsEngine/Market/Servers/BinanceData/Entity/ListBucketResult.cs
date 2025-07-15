using System.Collections.Generic;
using System.Xml.Serialization;

namespace OsEngine.Market.Servers.BinanceData.Entity
{
    [XmlRoot("ListBucketResult", Namespace = "http://s3.amazonaws.com/doc/2006-03-01/")]
    public class ListBucketResult
    {
        [XmlElement("Name")]
        public string Name { get; set; }

        [XmlElement("Prefix")]
        public string Prefix { get; set; }

        [XmlElement("Marker")]
        public string Marker { get; set; }

        [XmlElement("NextMarker")]
        public string NextMarker { get; set; }

        [XmlElement("MaxKeys")]
        public int MaxKeys { get; set; }

        [XmlElement("Delimiter")]
        public string Delimiter { get; set; }

        [XmlElement("IsTruncated")]
        public bool IsTruncated { get; set; }

        [XmlElement("Contents")]
        public List<S3Object> Contents { get; set; }
    }

    public class S3Object
    {
        [XmlElement("Key")]
        public string Key { get; set; }

        [XmlElement("LastModified")]
        public string LastModified { get; set; }

        [XmlElement("Size")]
        public long Size { get; set; }
    }
}
