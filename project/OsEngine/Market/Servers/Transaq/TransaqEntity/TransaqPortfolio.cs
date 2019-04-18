using System.Collections.Generic;
using System.Xml.Serialization;

namespace OsEngine.Market.Servers.Transaq.TransaqEntity
{
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
        [XmlElement(ElementName = "settled")]
        public string Settled { get; set; }
        [XmlAttribute(AttributeName = "register")]
        public string Register { get; set; }
        [XmlElement(ElementName = "buying")]
        public string Buying { get; set; }
        [XmlElement(ElementName = "selling")]
        public string Selling { get; set; }
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
        [XmlElement(ElementName = "settled")]
        public string Settled { get; set; }
        [XmlElement(ElementName = "tax")]
        public string Tax { get; set; }
        [XmlElement(ElementName = "value_part")]
        public Value_part Value_part { get; set; }
        [XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }
    }

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

    [XmlRoot(ElementName = "united_portfolio")]
    public class UnitedPortfolio
    {
        [XmlElement(ElementName = "open_equity")]
        public string Open_equity { get; set; }
        [XmlElement(ElementName = "equity")]
        public string Equity { get; set; }
        [XmlElement(ElementName = "chrgoff_ir")]
        public string Chrgoff_ir { get; set; }
        [XmlElement(ElementName = "init_req")]
        public string Init_req { get; set; }
        [XmlElement(ElementName = "chrgoff_mr")]
        public string Chrgoff_mr { get; set; }
        [XmlElement(ElementName = "maint_req")]
        public string Maint_req { get; set; }
        [XmlElement(ElementName = "reg_equity")]
        public string Reg_equity { get; set; }
        [XmlElement(ElementName = "reg_ir")]
        public string Reg_ir { get; set; }
        [XmlElement(ElementName = "reg_mr")]
        public string Reg_mr { get; set; }
        [XmlElement(ElementName = "vm")]
        public string Vm { get; set; }
        [XmlElement(ElementName = "finres")]
        public string Finres { get; set; }
        [XmlElement(ElementName = "go")]
        public string Go { get; set; }
        [XmlElement(ElementName = "vm_mma")]
        public string Vm_mma { get; set; }
        [XmlElement(ElementName = "money")]
        public Money Money { get; set; }
        [XmlElement(ElementName = "asset")]
        public List<Asset> Asset { get; set; }
        [XmlAttribute(AttributeName = "union")]
        public string Union { get; set; }
        [XmlAttribute(AttributeName = "client")]
        public string Client { get; set; }
    }

    [XmlRoot(ElementName = "portfolio_tplus")]
    public class Portfolio_tplus
    {
        [XmlElement(ElementName = "coverage_fact")]
        public string Coverage_fact { get; set; }
        [XmlElement(ElementName = "coverage_plan")]
        public string Coverage_plan { get; set; }
        [XmlElement(ElementName = "coverage_crit")]
        public string Coverage_crit { get; set; }
        [XmlElement(ElementName = "open_equity")]
        public string Open_equity { get; set; }
        [XmlElement(ElementName = "equity")]
        public string Equity { get; set; }
        [XmlElement(ElementName = "cover")]
        public string Cover { get; set; }
        [XmlElement(ElementName = "init_margin")]
        public string Init_margin { get; set; }
        [XmlElement(ElementName = "pnl_income")]
        public string Pnl_income { get; set; }
        [XmlElement(ElementName = "pnl_intraday")]
        public string Pnl_intraday { get; set; }
        [XmlElement(ElementName = "leverage")]
        public string Leverage { get; set; }
        [XmlElement(ElementName = "margin_level")]
        public string Margin_level { get; set; }
        [XmlAttribute(AttributeName = "client")]
        public string Client { get; set; }
    }
}



