using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.Bybit.EntityCreators
{
    public static class BybitPortfolioCreator
    {
        public static Portfolio Create(JToken data, string portfolioName)
        {
            var portfolio = Create(portfolioName);

            portfolio.ValueCurrent = 1;
            portfolio.ValueBlocked = 1;
            portfolio.ValueBegin = 1;


            foreach (var jtPosition in data)
            {
                PositionOnBoard pos = new PositionOnBoard();

                pos.PortfolioName = portfolio.Number;
                pos.SecurityNameCode = jtPosition.Path.Replace("result.", "");

                var wallet_balace = jtPosition.First.SelectToken("wallet_balance").Value<decimal>();
                var realizedPnl = jtPosition.First.SelectToken("realised_pnl").Value<decimal>();
                pos.ValueBegin = wallet_balace;
                pos.ValueCurrent = wallet_balace + realizedPnl;
                pos.ValueBlocked = jtPosition.First.SelectToken("used_margin").Value<decimal>();

                if (pos.ValueCurrent > 0 || pos.ValueBlocked > 0)
                {
                    portfolio.SetNewPosition(pos);
                }
            }

            return portfolio;
        }

        public static List<PositionOnBoard> CreatePosOnBoard(JToken data)
        {
            List<PositionOnBoard> poses = new List<PositionOnBoard>();

            foreach (var jtPosition in data)
            {
                JToken posJson = jtPosition.SelectToken("data");


                PositionOnBoard pos = new PositionOnBoard();

                pos.PortfolioName = "BybitPortfolio";
                pos.SecurityNameCode 
                    = posJson.SelectToken("symbol").ToString() 
                    + "_" + posJson.SelectToken("side").ToString();

                pos.ValueBegin = posJson.SelectToken("size").Value<decimal>();
                pos.ValueCurrent = pos.ValueBegin;
                poses.Add(pos);
            }

            return poses;
        }

        public static Portfolio Create(string portfolioName)
        {
            return new Portfolio { Number = portfolioName };
        }
    }
}