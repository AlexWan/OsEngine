using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.GateIo.EntityCreators
{
    public class GatePortfolioCreator
    {
        private Portfolio _headPortfolio;
        private List<Portfolio> _portfolios;

        public GatePortfolioCreator(string portfolioName)
        {
            _headPortfolio = new Portfolio { Number = portfolioName };
            _portfolios = new List<Portfolio> { _headPortfolio };
        }

        //{"result":"true",
        //"datas":[{"asset":"LTC","available":"0.308261","freeze":"0.000000","lent":"0.000000","total_lend":"0.000000"},
        //{"asset":"USDT","available":"0.934611","freeze":"0.000000","lent":"0.000000","total_lend":"0.000000"}]}

        public List<Portfolio> CreatePortfolio(string data)
        {
            var jt = JToken.Parse(data)["datas"].Children();
            
            foreach (var jtPosition in jt)
            {
                PositionOnBoard pos = new PositionOnBoard();

                pos.PortfolioName = _headPortfolio.Number;
                pos.SecurityNameCode = jtPosition["asset"].Value<string>();
                pos.ValueCurrent = jtPosition["available"].Value<decimal>();
                pos.ValueBlocked = jtPosition["freeze"].Value<decimal>();

                if (pos.ValueCurrent > 0 || pos.ValueBlocked > 0)
                {
                    _portfolios[0].SetNewPosition(pos);
                }
            }

            return _portfolios;
        }

        //{"method": "balance.update", "params": [{"LTC": {"available": "0.2082608", "freeze": "0.1"}}], "id": null}

        public List<Portfolio> UpdatePortfolio(string data)
        {
            var jt = JObject.Parse(data)["params"].Children<JObject>().Properties();

            foreach (var jtPosition in jt)
            {
                PositionOnBoard pos = new PositionOnBoard();

                pos.PortfolioName = _headPortfolio.Number;
                pos.SecurityNameCode = jtPosition.Name;
                pos.ValueCurrent = jtPosition.Value["available"].Value<decimal>();
                pos.ValueBlocked = jtPosition.Value["freeze"].Value<decimal>();

                if (pos.ValueCurrent > 0 || pos.ValueBlocked > 0)
                {
                    _portfolios[0].SetNewPosition(pos);
                }
            }
            return _portfolios;
        }
    }
}
