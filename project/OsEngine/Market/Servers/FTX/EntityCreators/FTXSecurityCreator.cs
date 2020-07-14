using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using System;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.FTX.EntityCreators
{
    public class FTXSecurityCreator
    {
        private const string ResultPath = "result";
        private const string TypePath = "type";
        private const string StatePath = "enabled";
        private const string PriceStepPath = "priceIncrement";
        private const string NamePath = "name";
        private const string LotPath = "minProvideSize";

        public List<Security> Create(JToken jt)
        {
            var securities = new List<Security>();
            var jProperties = jt.SelectToken(ResultPath).Children();

            foreach (var jProperty in jProperties)
            {
                try
                {
                    var security = new Security();
                    bool isFuture = jProperty.SelectToken(TypePath).ToString() == "future";
                    var name = jProperty.SelectToken(NamePath).ToString();
                    security.NameId = name;
                    security.NameFull = name;
                    security.Name = name;
                    security.NameClass = name.Split(isFuture ? '-' : '/')[0];
                    security.SecurityType = isFuture ? SecurityType.Futures : SecurityType.CurrencyPair;
                    security.PriceStep = jProperty.SelectToken(PriceStepPath).Value<decimal>();
                    security.State = jProperty.SelectToken(StatePath).Value<bool>() ?
                        SecurityStateType.Activ :
                        SecurityStateType.Close;
                    security.Lot = jProperty.SelectToken(LotPath).Value<decimal>();

                    securities.Add(security);
                }
                catch (Exception error)
                {
                    throw new Exception("Security creation error \n" + error.ToString());
                }
            }

            return securities;
        }
    }
}
