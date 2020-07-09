using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.FTX.EntityCreators
{
    public class FTXPortfolioCreator
    {
        private const string SearchPath = "result.positions";
        private const string PathForName = "future";
        private const string PathForEntryPrice = "entryPrice";
        private const string PathForPnl = "realizedPnl";

        private const string PathForValueBlocked = "size";

        private Portfolio _headPortfolio;
        private List<Portfolio> _portfolios;

        public FTXPortfolioCreator(string portfolioName)
        {
            _headPortfolio = new Portfolio() { Number = portfolioName };
            _portfolios = new List<Portfolio> { _headPortfolio };
        }

        public List<Portfolio> Create(JToken jt)
        {
            var needLevel = jt.SelectTokens(SearchPath).Children();

            foreach (var jtPosition in needLevel)
            {
                PositionOnBoard pos = new PositionOnBoard();

                pos.PortfolioName = _headPortfolio.Number;
                pos.SecurityNameCode = jtPosition.SelectToken(PathForName).ToString();
                var entryPrice = jtPosition.SelectToken(PathForEntryPrice).Value<decimal>();
                var realizedPnl = jtPosition.SelectToken(PathForPnl).Value<decimal>();
                pos.ValueBegin = entryPrice;
                pos.ValueCurrent = entryPrice + realizedPnl;
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