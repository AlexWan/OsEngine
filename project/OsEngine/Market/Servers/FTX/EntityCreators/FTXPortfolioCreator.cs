using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.FTX.EntityCreators
{
    public class FTXPortfolioCreator
    {
        private const string ResultPath = "result";
        private const string PositionsPath = "positions";
        private const string NamePath = "future";
        private const string CostPath = "cost";
        private const string PnlPricePath = "realizedPnl";
        private const string CollateralUsedPath = "collateralUsed";

        private const string FreeCollateralPath = "freeCollateral";
        private const string CollateralPath = "collateral";
        private const string TotalAccountValuePath = "totalAccountValue";

        private Portfolio _headPortfolio;
        private List<Portfolio> _portfolios;

        public FTXPortfolioCreator(string portfolioName)
        {
            _headPortfolio = new Portfolio() { Number = portfolioName };
            _portfolios = new List<Portfolio> { _headPortfolio };
        }

        public List<Portfolio> Create(JToken jt)
        {
            var result = jt.SelectToken(ResultPath);

            var collateral = result.SelectToken(CollateralPath).Value<decimal>();
            var freeCollateral = result.SelectToken(FreeCollateralPath).Value<decimal>();

            _headPortfolio.ValueCurrent = result.SelectToken(TotalAccountValuePath).Value<decimal>();
            _headPortfolio.ValueBlocked = collateral - freeCollateral;

            var positions = result.SelectTokens(PositionsPath).Children();

            foreach (var jtPosition in positions)
            {
                PositionOnBoard pos = new PositionOnBoard();

                pos.PortfolioName = _headPortfolio.Number;
                pos.SecurityNameCode = jtPosition.SelectToken(NamePath).ToString();
                var cost = jtPosition.SelectToken(CostPath).Value<decimal>();
                var realizedPnl = jtPosition.SelectToken(PnlPricePath).Value<decimal>();
                pos.ValueBegin = cost;
                pos.ValueCurrent = cost + realizedPnl;
                pos.ValueBlocked = jtPosition.SelectToken(CollateralUsedPath).Value<decimal>();

                if (pos.ValueCurrent > 0 || pos.ValueBlocked > 0)
                {
                    _portfolios[0].SetNewPosition(pos);
                }
            }

            return _portfolios;
        }
    }
}