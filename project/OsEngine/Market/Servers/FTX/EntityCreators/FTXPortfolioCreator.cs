﻿using Newtonsoft.Json.Linq;
using OsEngine.Entity;

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

        public Portfolio Create(JToken jt, string portfolioName)
        {
            var portfolio = new Portfolio() { Number = portfolioName };

            var result = jt.SelectToken(ResultPath);
            var collateral = result.SelectToken(CollateralPath).Value<decimal>();
            var freeCollateral = result.SelectToken(FreeCollateralPath).Value<decimal>();
            var positions = result.SelectTokens(PositionsPath).Children();

            portfolio.ValueCurrent = result.SelectToken(TotalAccountValuePath).Value<decimal>();
            portfolio.ValueBlocked = collateral - freeCollateral;


            foreach (var jtPosition in positions)
            {
                PositionOnBoard pos = new PositionOnBoard();

                pos.PortfolioName = portfolio.Number;
                pos.SecurityNameCode = jtPosition.SelectToken(NamePath).ToString();
                var cost = jtPosition.SelectToken(CostPath).Value<decimal>();
                var realizedPnl = jtPosition.SelectToken(PnlPricePath).Value<decimal>();
                pos.ValueBegin = cost;
                pos.ValueCurrent = cost + realizedPnl;
                pos.ValueBlocked = jtPosition.SelectToken(CollateralUsedPath).Value<decimal>();

                if (pos.ValueCurrent > 0 || pos.ValueBlocked > 0)
                {
                    portfolio.SetNewPosition(pos);
                }
            }

            return portfolio;
        }

        public Portfolio Create(string portfolioName)
        {
            return new Portfolio { Number = portfolioName };
        }
    }
}