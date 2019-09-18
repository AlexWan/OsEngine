using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.ZB.EntityCreators
{
    public class ZbPortfolioCreator
    {
        private const string SearchPath = "data.coins";
        private const string PathForName = "showName";
        private const string PathForValueCurrent = "available";
        private const string PathForValueBlocked = "freez";

        private Portfolio _headPortfolio;
        private List<Portfolio> _portfolios;

        public ZbPortfolioCreator(string portfolioName)
        {
            _headPortfolio = new Portfolio() { Number = portfolioName };
            _portfolios = new List<Portfolio> { _headPortfolio };
        }

        public List<Portfolio> Create(string data)
        {
            var jt = JToken.Parse(data);
            var needLevel = jt.SelectTokens(SearchPath).Children();

            foreach (var jtPosition in needLevel)
            {
                PositionOnBoard pos = new PositionOnBoard();

                pos.PortfolioName = _headPortfolio.Number;
                pos.SecurityNameCode = jtPosition.SelectToken(PathForName).ToString();
                pos.ValueCurrent = jtPosition.SelectToken(PathForValueCurrent).Value<decimal>();
                pos.ValueBlocked = jtPosition.SelectToken(PathForValueBlocked).Value<decimal>();

                if (pos.ValueCurrent > 0 || pos.ValueBlocked > 0)
                {
                    _portfolios[0].SetNewPosition(pos);
                }
            }

            return _portfolios;
        }
    }
}