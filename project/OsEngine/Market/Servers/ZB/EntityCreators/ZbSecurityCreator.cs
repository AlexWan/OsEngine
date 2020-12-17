using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using System;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.ZB.EntityCreators
{
    public class ZbSecurityCreator
    {
        private const string SearchPath = "data";
        private const string PathForDecimalsVolume = "amountScale";
        private const string PathForDecimalsPrice = "priceScale";
        private const char Separator = '_';

        private List<Security> _securities;

        public ZbSecurityCreator()
        {
            _securities = new List<Security>();
        }

        public List<Security> Create(string data)
        {
            var jProperties = JToken.Parse(data).SelectToken(SearchPath).Children<JProperty>();

            foreach (var jProperty in jProperties)
            {
                try
                {
                    Security security = new Security();

                    var name = jProperty.Name;
                    security.Name = name.Replace("_", string.Empty);
                    security.NameFull = security.Name;
                    security.NameId = security.Name;
                    security.NameClass = name.Split(Separator)[1];
                    security.SecurityType = SecurityType.CurrencyPair;
                    security.Decimals = jProperty.Value.SelectToken(PathForDecimalsPrice).Value<int>();
                    security.PriceStep = security.Decimals.GetValueByDecimals();
                    security.PriceStepCost = security.PriceStep;
                    security.State = SecurityStateType.Activ;
                    int volumeScale = jProperty.Value.SelectToken(PathForDecimalsVolume).Value<int>();
                    security.Lot = 1;

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
