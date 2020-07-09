using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using System;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.FTX.EntityCreators
{
    public class FTXSecurityCreator
    {
        private const string SearchPath = "result";
        private const string PathForType = "type";
        private const string PathForState = "enabled";
        private const string PathForPriceStep = "priceIncrement";
        private const string PathForName = "name";
        private const string PathForLot = "minProvideSize";

        private List<Security> _securities;

        public FTXSecurityCreator()
        {
            _securities = new List<Security>();
        }

        public List<Security> Create(JToken jt)
        {
            var jProperties = jt.SelectToken(SearchPath).Children();
            foreach (var jProperty in jProperties)
            {
                try
                {
                    var security = new Security();
                    bool isFuture = jProperty.SelectToken(PathForType).ToString() == "future";
                    var name = jProperty.SelectToken(PathForName).ToString();
                    security.NameId = name;
                    security.NameFull = name;
                    security.Name = name;
                    security.NameClass = name.Split(isFuture ? '-' : '/')[0];
                    security.SecurityType = isFuture ? SecurityType.Futures : SecurityType.CurrencyPair;
                    security.PriceStep = jProperty.SelectToken(PathForPriceStep).Value<decimal>();
                    security.State = jProperty.SelectToken(PathForState).Value<bool>() ?
                        SecurityStateType.Activ :
                        SecurityStateType.Close;
                    security.Lot = jProperty.SelectToken(PathForLot).Value<decimal>();

                    _securities.Add(security);
                }
                catch (Exception error)
                {
                    throw new Exception("Security creation error \n" + error.ToString());
                }
            }

            return _securities;
        }
    }
}
