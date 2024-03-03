using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;

namespace OsEngine.Robots.IndexArbitrage
{
    [Bot("MultiExchangePairArbitrageOnTheIndex")]
    public class MultiExchangePairArbitrageOnTheIndex : BotPanel
    {
        public MultiExchangePairArbitrageOnTheIndex(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Index);
            _index = TabsIndex[0];
            _index.SpreadChangeEvent += _index_SpreadChangeEvent;

            TabCreate(BotTabType.Simple);
            TabCreate(BotTabType.Simple);
            TabCreate(BotTabType.Simple);
            TabCreate(BotTabType.Simple);
            TabCreate(BotTabType.Simple);

            _sec1Tab = TabsSimple[0];
            _sec2Tab = TabsSimple[1];
            _sec3Tab = TabsSimple[2];
            _sec4Tab = TabsSimple[3];
            _sec5Tab = TabsSimple[4];

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort" });

            MaxPositionsCount = CreateParameter("Max poses count", 3, 1, 50, 4);

            MoneyPercentFromDepoOnPosition = CreateParameter("Percent depo on position", 25m, 0.1m, 50, 0.1m);

            TradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            MinDeviationSecToIndexToEntry = CreateParameter("Min Deviation SecToIndex To Entry", 0.15m, 0.15m, 5, 0.1m);

            MinDeviationSecToSecToEntry = CreateParameter("Min Deviation SecToSec ToEntry", 0.5m, 0.1m, 5, 0.1m);

            MinDeviationToExit = CreateParameter("Min Deviation To Exit", 0.1m, 0.1m, 5, 0.1m);

        }

        public override string GetNameStrategyType()
        {
            return "MultiExchangePairArbitrageOnTheIndex";
        }

        public override void ShowIndividualSettingsDialog()
        {
            

        }

        private BotTabIndex _index;

        private BotTabSimple _sec1Tab;

        private BotTabSimple _sec2Tab;

        private BotTabSimple _sec3Tab;

        private BotTabSimple _sec4Tab;

        private BotTabSimple _sec5Tab;

        public StrategyParameterString Regime;

        public StrategyParameterInt MaxPositionsCount;

        public StrategyParameterDecimal MoneyPercentFromDepoOnPosition;

        public StrategyParameterString TradeAssetInPortfolio;

        public StrategyParameterDecimal MinDeviationSecToIndexToEntry;

        public StrategyParameterDecimal MinDeviationSecToSecToEntry;

        public StrategyParameterDecimal MinDeviationToExit;

        // logic open poses

        private void _index_SpreadChangeEvent(List<Candle> index)
        {
            if (Regime.ValueString == "Off")
            {
                return;
            }

            // Close positions

            List<OpenPosByTab> openPoses = GetOpenPoses();

            if(openPoses == null 
                || openPoses.Count == 0)
            {
                TryOpenPositions(index);
            }
            else
            {
                TryClosePositions(openPoses);
            }
        }

        private void TryOpenPositions(List<Candle> index)
        {
            // берём список бумаг сверху и снизу индекса. 

            List<BotTabSimple> secsUpperIndex = new List<BotTabSimple>();

            List<BotTabSimple> secLowerIndex = new List<BotTabSimple>();

            decimal lastIndexPrice = index[index.Count - 1].Close;

            if(IsUpperThenIndex(lastIndexPrice,_sec1Tab))
            {
                secsUpperIndex.Add(_sec1Tab);
            }
            if (IsUpperThenIndex(lastIndexPrice, _sec2Tab))
            {
                secsUpperIndex.Add(_sec2Tab);
            }
            if (IsUpperThenIndex(lastIndexPrice, _sec3Tab))
            {
                secsUpperIndex.Add(_sec3Tab);
            }
            if (IsUpperThenIndex(lastIndexPrice, _sec4Tab))
            {
                secsUpperIndex.Add(_sec4Tab);
            }
            if (IsUpperThenIndex(lastIndexPrice, _sec5Tab))
            {
                secsUpperIndex.Add(_sec5Tab);
            }

            if (IsLowerThenIndex(lastIndexPrice, _sec1Tab))
            {
                secLowerIndex.Add(_sec1Tab);
            }
            if (IsLowerThenIndex(lastIndexPrice, _sec2Tab))
            {
                secLowerIndex.Add(_sec2Tab);
            }
            if (IsLowerThenIndex(lastIndexPrice, _sec3Tab))
            {
                secLowerIndex.Add(_sec3Tab);
            }
            if (IsLowerThenIndex(lastIndexPrice, _sec4Tab))
            {
                secLowerIndex.Add(_sec4Tab);
            }
            if (IsLowerThenIndex(lastIndexPrice, _sec5Tab))
            {
                secLowerIndex.Add(_sec5Tab);
            }

            if(secsUpperIndex.Count == 0 ||
               secLowerIndex.Count == 0)
            {
                return;
            }

            // выбираем самые высокие сверху

            BotTabSimple upSec = null;

            for(int i = 0;i < secsUpperIndex.Count;i++)
            {
                if(upSec == null)
                {
                    upSec = secsUpperIndex[i];
                    continue;
                }
                if (secsUpperIndex[i].PriceBestBid > upSec.PriceBestBid)
                {
                    upSec = secsUpperIndex[i];
                }
            }

            // выбираем самые низкие снизу

            BotTabSimple downSec = null;

            for (int i = 0; i < secLowerIndex.Count; i++)
            {
                if (downSec == null)
                {
                    downSec = secLowerIndex[i];
                    continue;
                }
                if (secLowerIndex[i].PriceBestAsk < downSec.PriceBestAsk)
                {
                    downSec = secLowerIndex[i];
                }
            }

            if(upSec == null 
                || downSec == null)
            {
                return;
            }

            if(upSec.PriceBestBid < downSec.PriceBestAsk)
            {
                SendNewLogMessage("The upper security is lower then lower security. Error", Logging.LogMessageType.Error);
                return;
            }

            decimal lowPrice = downSec.PriceBestAsk;
            decimal highPrice = upSec.PriceBestBid;

            decimal absDiff = highPrice - lowPrice;

            decimal percentMove = absDiff / (lowPrice / 100);

            if(percentMove >= MinDeviationSecToSecToEntry.ValueDecimal)
            {
                decimal volumeOnUpperSec = GetVolume(upSec);
                decimal volumeOnLowerSec = GetVolume(downSec);
                upSec.SellAtMarket(volumeOnUpperSec);
                downSec.SellAtMarket(volumeOnLowerSec);
            }
        }

        private bool IsUpperThenIndex(decimal lastIndexPrice, BotTabSimple tab)
        {
            if(tab.IsConnected == false
                || tab.IsReadyToTrade == false)
            {
                return false;
            }

            decimal lastBid = tab.PriceBestBid;

            if(lastBid == 0)
            {
                return false;
            }

            if(lastBid < lastIndexPrice )
            {
                return false;
            }

            decimal diff = lastBid - lastIndexPrice;

            decimal diffPercent = diff / (lastIndexPrice / 100);

            if(diffPercent < MinDeviationSecToIndexToEntry.ValueDecimal)
            {
                return false;
            }

            return true;
        }

        private bool IsLowerThenIndex(decimal lastIndexPrice, BotTabSimple tab)
        {
            if (tab.IsConnected == false
                || tab.IsReadyToTrade == false)
            {
                return false;
            }

            decimal lastAsk = tab.PriceBestAsk;

            if (lastAsk == 0)
            {
                return false;
            }

            if (lastAsk > lastIndexPrice)
            {
                return false;
            }

            decimal diff = lastIndexPrice - lastAsk;

            decimal diffPercent = diff / (lastAsk / 100);

            if (diffPercent < MinDeviationSecToIndexToEntry.ValueDecimal)
            {
                return false;
            }

            return true;
        }

        private void TryClosePositions(List<OpenPosByTab> openPoses)
        {
            if(openPoses.Count == 1)
            { // error position

                CloseAtMarket(openPoses[0]);
                return;
            }

            OpenPosByTab upPosition = null;
            OpenPosByTab downPosition = null;

            if (openPoses[0].Position.Direction == Side.Sell)
            {
                upPosition = openPoses[0];
                downPosition = openPoses[1];
            }
            else
            {
                upPosition = openPoses[1];
                downPosition = openPoses[0];
            }

            decimal lowPrice = downPosition.Tab.PriceBestAsk;
            decimal highPrice = upPosition.Tab.PriceBestBid;

            decimal absDiff = highPrice - lowPrice;

            decimal percentMove = absDiff / (lowPrice / 100);

            if (percentMove <= MinDeviationToExit.ValueDecimal)
            {
                CloseAtMarket(upPosition);
                CloseAtMarket(downPosition);
            }

        }

        private void CloseAtMarket(OpenPosByTab pos)
        { 
            if(pos.Position.State != PositionStateType.Open)
            {
                return;
            }

            pos.Tab.CloseAtMarket(pos.Position, pos.Position.OpenVolume);
        }

        private decimal GetVolume(BotTabSimple tab)
        {
            Portfolio myPortfolio = tab.Portfolio;

            if (myPortfolio == null)
            {
                return 0;
            }

            decimal portfolioPrimeAsset = 0;

            if (TradeAssetInPortfolio.ValueString == "Prime")
            {
                portfolioPrimeAsset = myPortfolio.ValueCurrent;
            }
            else
            {
                List<PositionOnBoard> positionOnBoard = myPortfolio.GetPositionOnBoard();

                if (positionOnBoard == null)
                {
                    return 0;
                }

                for (int i = 0; i < positionOnBoard.Count; i++)
                {
                    if (positionOnBoard[i].PortfolioName == TradeAssetInPortfolio.ValueString)
                    {
                        portfolioPrimeAsset = positionOnBoard[i].ValueCurrent;
                        break;
                    }
                }
            }

            if (portfolioPrimeAsset == 0)
            {
                SendNewLogMessage("Can`t found portfolio " + TradeAssetInPortfolio.ValueString, Logging.LogMessageType.Error);
                return 0;
            }

            decimal moneyOnPosition = portfolioPrimeAsset * (MoneyPercentFromDepoOnPosition.ValueDecimal / 100);

            decimal qty = Math.Round(moneyOnPosition / tab.PriceBestAsk, tab.Securiti.DecimalsVolume);

            return qty;
        }

        private List<OpenPosByTab> GetOpenPoses()
        {

            return null;
        }
    }

    public class OpenPosByTab
    {
        public Position Position;

        public BotTabSimple Tab;
    }
}