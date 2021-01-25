using System;
using System.Xml.Serialization;
using System.Collections.Generic;
namespace OsEngine.Market.Servers.Transaq.TransaqEntity
{
    [XmlRoot(ElementName = "markets")]
    public class Markets
    {
        [XmlElement(ElementName = "market")]
        public List<string> Market { get; set; }
    }

    [XmlRoot(ElementName = "money_position")]
    public class Money_position
    {
        [XmlElement(ElementName = "client")]
        public List<string> Client { get; set; }
        [XmlElement(ElementName = "markets")]
        public Markets Markets { get; set; }
        [XmlElement(ElementName = "register")]
        public string Register { get; set; }
        [XmlElement(ElementName = "asset")]
        public string Asset { get; set; }
        [XmlElement(ElementName = "union")]
        public string Union { get; set; }
        [XmlElement(ElementName = "shortname")]
        public string Shortname { get; set; }
        [XmlElement(ElementName = "saldoin")]
        public string Saldoin { get; set; }
        [XmlElement(ElementName = "bought")]
        public string Bought { get; set; }
        [XmlElement(ElementName = "sold")]
        public string Sold { get; set; }
        [XmlElement(ElementName = "saldo")]
        public string Saldo { get; set; }
        [XmlElement(ElementName = "ordbuy")]
        public string Ordbuy { get; set; }
        [XmlElement(ElementName = "ordbuycond")]
        public string Ordbuycond { get; set; }
        [XmlElement(ElementName = "comission")]
        public string Comission { get; set; }
    }

    [XmlRoot(ElementName = "sec_position")]
    public class Sec_position
    {
        [XmlElement(ElementName = "secid")]
        public string Secid { get; set; }
        [XmlElement(ElementName = "market")]
        public string Market { get; set; }
        [XmlElement(ElementName = "seccode")]
        public string Seccode { get; set; }
        [XmlElement(ElementName = "register")]
        public string Register { get; set; }
        [XmlElement(ElementName = "client")]
        public string Client { get; set; }
        [XmlElement(ElementName = "union")]
        public string Union { get; set; }
        [XmlElement(ElementName = "shortname")]
        public string Shortname { get; set; }
        [XmlElement(ElementName = "saldoin")]
        public string Saldoin { get; set; }
        [XmlElement(ElementName = "saldomin")]
        public string Saldomin { get; set; }
        [XmlElement(ElementName = "bought")]
        public string Bought { get; set; }
        [XmlElement(ElementName = "sold")]
        public string Sold { get; set; }
        [XmlElement(ElementName = "saldo")]
        public string Saldo { get; set; }
        [XmlElement(ElementName = "ordbuy")]
        public string Ordbuy { get; set; }
        [XmlElement(ElementName = "ordsell")]
        public string Ordsell { get; set; }
        [XmlElement(ElementName = "amount")]
        public string Amount { get; set; }
        [XmlElement(ElementName = "equity")]
        public string Equity { get; set; }
    }

    [XmlRoot(ElementName = "forts_position")]
    public class Forts_position
    {
        [XmlElement(ElementName = "secid")]
        public string Secid { get; set; }
        [XmlElement(ElementName = "markets")]
        public Markets Markets { get; set; }
        [XmlElement(ElementName = "seccode")]
        public string Seccode { get; set; }
        [XmlElement(ElementName = "client")]
        public string Client { get; set; }
        [XmlElement(ElementName = "union")]
        public string Union { get; set; }
        [XmlElement(ElementName = "startnet")]
        public string Startnet { get; set; }
        [XmlElement(ElementName = "openbuys")]
        public string Openbuys { get; set; }
        [XmlElement(ElementName = "opensells")]
        public string Opensells { get; set; }
        [XmlElement(ElementName = "totalnet")]
        public string Totalnet { get; set; }
        [XmlElement(ElementName = "todaybuy")]
        public string Todaybuy { get; set; }
        [XmlElement(ElementName = "todaysell")]
        public string Todaysell { get; set; }
        [XmlElement(ElementName = "optmargin")]
        public string Optmargin { get; set; }
        [XmlElement(ElementName = "varmargin")]
        public string Varmargin { get; set; }
        [XmlElement(ElementName = "expirationpos")]
        public string Expirationpos { get; set; }
        [XmlElement(ElementName = "usedsellspotlimit")]
        public string Usedsellspotlimit { get; set; }
        [XmlElement(ElementName = "sellspotlimit")]
        public string Sellspotlimit { get; set; }
        [XmlElement(ElementName = "netto")]
        public string Netto { get; set; }
        [XmlElement(ElementName = "kgo")]
        public string Kgo { get; set; }
    }

    [XmlRoot(ElementName = "forts_money")]
    public class Forts_money
    {
        [XmlElement(ElementName = "client")]
        public string Client { get; set; }
        [XmlElement(ElementName = "union")]
        public string Union { get; set; }
        [XmlElement(ElementName = "markets")]
        public Markets Markets { get; set; }
        [XmlElement(ElementName = "shortname")]
        public string Shortname { get; set; }
        [XmlElement(ElementName = "current")]
        public string Current { get; set; }
        [XmlElement(ElementName = "blocked")]
        public string Blocked { get; set; }
        [XmlElement(ElementName = "free")]
        public string Free { get; set; }
        [XmlElement(ElementName = "varmargin")]
        public string Varmargin { get; set; }
    }

    [XmlRoot(ElementName = "forts_collaterals")]
    public class Forts_collaterals
    {
        [XmlElement(ElementName = "client")]
        public string Client { get; set; }
        [XmlElement(ElementName = "union")]
        public string Union { get; set; }
        [XmlElement(ElementName = "markets")]
        public Markets Markets { get; set; }
        [XmlElement(ElementName = "shortname")]
        public string Shortname { get; set; }
        [XmlElement(ElementName = "current")]
        public string Current { get; set; }
        [XmlElement(ElementName = "blocked")]
        public string Blocked { get; set; }
        [XmlElement(ElementName = "free")]
        public string Free { get; set; }
    }

    [XmlRoot(ElementName = "spot_limit")]
    public class Spot_limit
    {
        [XmlElement(ElementName = "client")]
        public string Client { get; set; }
        [XmlElement(ElementName = "union")]
        public string Union { get; set; }
        [XmlElement(ElementName = "markets")]
        public Markets Markets { get; set; }
        [XmlElement(ElementName = "shortname")]
        public string Shortname { get; set; }
        [XmlElement(ElementName = "buylimit")]
        public string Buylimit { get; set; }
        [XmlElement(ElementName = "buylimitused")]
        public string Buylimitused { get; set; }
    }

    [XmlRoot(ElementName = "positions")]
    public class TransaqPositions
    {
        [XmlElement(ElementName = "money_position")]
        public Money_position Money_position { get; set; }
        [XmlElement(ElementName = "sec_position")]
        public Sec_position Sec_position { get; set; }
        [XmlElement(ElementName = "forts_position")]
        public List<Forts_position> Forts_position { get; set; }
        [XmlElement(ElementName = "forts_money")]
        public Forts_money Forts_money { get; set; }
        [XmlElement(ElementName = "forts_collaterals")]
        public Forts_collaterals Forts_collaterals { get; set; }
        [XmlElement(ElementName = "spot_limit")]
        public Spot_limit Spot_limit { get; set; }
    }

    [XmlRoot(ElementName = "clientlimits")]
    public class ClientLimits
    {
        [XmlElement(ElementName = "coverage")]
        public string Coverage { get; set; }

        [XmlElement(ElementName = "money_current")]
        public string MoneyCurrent { get; set; }

        [XmlElement(ElementName = "money_free")]
        public string MoneyFree { get; set; }

        [XmlElement(ElementName = "money_reserve")]
        public string MoneyReserve { get; set; }

        [XmlAttribute(AttributeName = "client")]
        public string Client { get; set; }

        [XmlElement(ElementName = "exchange_fee")]
        public string ExchangeFee { get; set; }

        [XmlElement(ElementName = "varmargin")]
        public string VarMargin { get; set; }

        [XmlElement(ElementName = "forts_varmargin")]
        public string FortsVarMargin { get; set; }

        [XmlElement(ElementName = "profit")]
        public string Profit { get; set; }
    }
}
