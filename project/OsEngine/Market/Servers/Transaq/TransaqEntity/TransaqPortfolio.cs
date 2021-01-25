using System.Collections.Generic;
using System.Xml.Serialization;

namespace OsEngine.Market.Servers.Transaq.TransaqEntity
{
    [XmlRoot(ElementName = "security")]
    public class Securit
    {
        [XmlElement(ElementName = "market")]
        public string Market { get; set; }
        [XmlElement(ElementName = "seccode")]
        public string Seccode { get; set; }
        [XmlElement(ElementName = "price")]
        public string Price { get; set; }
        [XmlElement(ElementName = "open_balance")]
        public string Open_balance { get; set; }
        [XmlElement(ElementName = "bought")]
        public string Bought { get; set; }
        [XmlElement(ElementName = "sold")]
        public string Sold { get; set; }
        [XmlElement(ElementName = "balance")]
        public string Balance { get; set; }
        [XmlElement(ElementName = "buying")]
        public string Buying { get; set; }
        [XmlElement(ElementName = "selling")]
        public string Selling { get; set; }
        [XmlElement(ElementName = "equity")]
        public string Equity { get; set; }
        [XmlElement(ElementName = "reg_equity")]
        public string Reg_equity { get; set; }
        [XmlElement(ElementName = "riskrate_long")]
        public string Riskrate_long { get; set; }
        [XmlElement(ElementName = "riskrate_short")]
        public string Riskrate_short { get; set; }
        [XmlElement(ElementName = "reserate_long")]
        public string Reserate_long { get; set; }
        [XmlElement(ElementName = "reserate_short")]
        public string Reserate_short { get; set; }
        [XmlElement(ElementName = "pl")]
        public string Pl { get; set; }
        [XmlElement(ElementName = "pnl_income")]
        public string Pnl_income { get; set; }
        [XmlElement(ElementName = "pnl_intraday")]
        public string Pnl_intraday { get; set; }
        [XmlElement(ElementName = "maxbuy")]
        public string Maxbuy { get; set; }
        [XmlElement(ElementName = "maxsell")]
        public string Maxsell { get; set; }
        [XmlElement(ElementName = "value_part")]
        public Value_part Value_part { get; set; }
        [XmlAttribute(AttributeName = "secid")]
        public string Secid { get; set; }
    }

    [XmlRoot(ElementName = "asset")]
    public class Asset
    {
        [XmlElement(ElementName = "setoff_rate")]
        public string Setoff_rate { get; set; }
        [XmlElement(ElementName = "init_req")]
        public string Init_req { get; set; }
        [XmlElement(ElementName = "maint_req")]
        public string Maint_req { get; set; }
        [XmlElement(ElementName = "security")]
        public List<Securit> Security { get; set; }
        [XmlAttribute(AttributeName = "code")]
        public string Code { get; set; }
        [XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }
    }

    [XmlRoot(ElementName = "portfolio_mct")]
    public class PortfolioMct
    {
        [XmlElement(ElementName = "portfolio_currency")]
        public string Portfolio_currency { get; set; }
        [XmlElement(ElementName = "capital")]
        public string Capital { get; set; }
        [XmlElement(ElementName = "utilization_fact")]
        public string Utilization_fact { get; set; }
        [XmlElement(ElementName = "utilization_plan")]
        public string Utilization_plan { get; set; }
        [XmlElement(ElementName = "coverage_fact")]
        public string Coverage_fact { get; set; }
        [XmlElement(ElementName = "coverage_plan")]
        public string Coverage_plan { get; set; }
        [XmlElement(ElementName = "open_balance")]
        public string Open_balance { get; set; }
        [XmlElement(ElementName = "tax")]
        public string Tax { get; set; }
        [XmlElement(ElementName = "pnl_income")]
        public string Pnl_income { get; set; }
        [XmlElement(ElementName = "pnl_intraday")]
        public string Pnl_intraday { get; set; }
        [XmlAttribute(AttributeName = "client")]
        public string Client { get; set; }
    }


    [XmlRoot(ElementName = "portfolio_currency")]
    public class Portfolio_currency
    {
        [XmlElement(ElementName = "cross_rate")]
        public string Cross_rate { get; set; }
        [XmlElement(ElementName = "open_balance")]
        public string Open_balance { get; set; }
        [XmlElement(ElementName = "balance")]
        public string Balance { get; set; }
        [XmlElement(ElementName = "equity")]
        public string Equity { get; set; }
        [XmlElement(ElementName = "cover")]
        public string Cover { get; set; }
        [XmlElement(ElementName = "init_req")]
        public string Init_req { get; set; }
        [XmlElement(ElementName = "maint_req")]
        public string Maint_req { get; set; }
        [XmlElement(ElementName = "unrealized_pnl")]
        public string Unrealized_pnl { get; set; }
        [XmlAttribute(AttributeName = "currency")]
        public string Currency { get; set; }
    }

    [XmlRoot(ElementName = "value_part")]
    public class Value_part
    {
        [XmlElement(ElementName = "open_balance")]
        public string Open_balance { get; set; }
        [XmlElement(ElementName = "bought")]
        public string Bought { get; set; }
        [XmlElement(ElementName = "sold")]
        public string Sold { get; set; }
        [XmlElement(ElementName = "balance")]
        public string Balance { get; set; }
        [XmlElement(ElementName = "blocked")]
        public string Blocked { get; set; }
        [XmlElement(ElementName = "estimated")]
        public string Estimated { get; set; }
        [XmlAttribute(AttributeName = "register")]
        public string Register { get; set; }
    }

    [XmlRoot(ElementName = "money")]
    public class Money
    {
        [XmlElement(ElementName = "open_balance")]
        public string Open_balance { get; set; }
        [XmlElement(ElementName = "bought")]
        public string Bought { get; set; }
        [XmlElement(ElementName = "sold")]
        public string Sold { get; set; }
        [XmlElement(ElementName = "balance")]
        public string Balance { get; set; }
        [XmlElement(ElementName = "blocked")]
        public string Blocked { get; set; }
        [XmlElement(ElementName = "estimated")]
        public string Estimated { get; set; }
        [XmlElement(ElementName = "fee")]
        public string Fee { get; set; }
        [XmlElement(ElementName = "vm")]
        public string Vm { get; set; }
        [XmlElement(ElementName = "finres")]
        public string Finres { get; set; }
        [XmlElement(ElementName = "cover")]
        public string Cover { get; set; }
        [XmlElement(ElementName = "value_part")]
        public Value_part Value_part { get; set; }
        [XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }
        [XmlAttribute(AttributeName = "currency")]
        public string Currency { get; set; }
    }

    [XmlRoot(ElementName = "mc_portfolio")]
    public class McPortfolio
    {
        [XmlElement(ElementName = "open_equity")]
        public string Open_equity { get; set; }
        [XmlElement(ElementName = "equity")]
        public string Equity { get; set; }
        [XmlElement(ElementName = "pl")]
        public string Pl { get; set; }
        [XmlElement(ElementName = "go")]
        public string Go { get; set; }
        [XmlElement(ElementName = "cover")]
        public string Cover { get; set; }
        [XmlElement(ElementName = "init_req")]
        public string Init_req { get; set; }
        [XmlElement(ElementName = "maint_req")]
        public string Maint_req { get; set; }
        [XmlElement(ElementName = "unrealized_pnl")]
        public string Unrealized_pnl { get; set; }
        [XmlElement(ElementName = "portfolio_currency")]
        public Portfolio_currency Portfolio_currency { get; set; }
        [XmlElement(ElementName = "money")]
        public Money Money { get; set; }
        [XmlAttribute(AttributeName = "union")]
        public string Union { get; set; }
        [XmlAttribute(AttributeName = "client")]
        public string Client { get; set; }
    }

}



