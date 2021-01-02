using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Market.Servers.Huobi.Entities;

namespace OsEngine.Market.Servers.Huobi.Spot
{
    public class HuobiSpotSecurityCreator
    {
        private  List<Security> _securities;

        public List<Security> Create(string data)
        {
            _securities = new List<Security>();

            GetSymbolsResponse symbols = JsonConvert.DeserializeObject<GetSymbolsResponse>(data);

            foreach (var symbol in symbols.data)
            {
                try
                {
                    var security = new Security();

                    security.Name = symbol.symbol;
                    security.NameFull = security.Name;
                    security.NameClass = symbol.quoteCurrency;
                    security.NameId = symbol.symbol;
                    security.SecurityType = SecurityType.CurrencyPair;
                    security.Decimals = symbol.pricePrecision;
                    security.PriceStep = security.Decimals.GetValueByDecimals();
                    security.PriceStepCost = security.PriceStep;
                    security.State = symbol.state == "online" ? SecurityStateType.Activ : SecurityStateType.Close;
                    security.Lot = 1;

                    _securities.Add(security);
                }
                catch 
                {
                    throw new Exception("Ошибка создания инструмента");
                }
            }

            return _securities;
        }
    }
}
