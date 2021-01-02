using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using System;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.GateIo.EntityCreators
{
    class GateSecurityCreator
    {
        private List<Security> _securities;

        public GateSecurityCreator()
        {
            _securities = new List<Security>();
        }

        public List<Security> Create(string data)
        {
            var jProperties = JToken.Parse(data).SelectToken("pairs").Children<JObject>().Properties();
            
            foreach (var jProperty in jProperties)
            {
                try
                {
                    Security security = new Security();

                    var name = jProperty.Name.ToUpper();

                    security.Name = name;
                    security.NameFull = security.Name;
                    security.NameId = security.Name;
                    security.NameClass = name.Split('_')[1];
                    security.SecurityType = SecurityType.CurrencyPair;
                    security.Decimals = jProperty.Value["decimal_places"].Value<int>();
                    security.PriceStep = security.Decimals.GetValueByDecimals();
                    security.PriceStepCost = security.PriceStep;
                    security.State = SecurityStateType.Activ;
                    security.Lot = 1;
                    security.Go = jProperty.Value.SelectToken("min_amount_b").Value<decimal>();

                    _securities.Add(security);
                }
                catch (ArgumentNullException)
                {
                    // invalid data in row, ignore
                }
            }

            return _securities;
        }
    }
}
