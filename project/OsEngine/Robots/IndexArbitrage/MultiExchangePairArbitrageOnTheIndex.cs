/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;

/* Description
Index Arbitrage robot for OsEngine.

Arbitrage of several currency pairs on the index.
*/

namespace OsEngine.Robots.IndexArbitrage
{
    [Bot("MultiExchangePairArbitrageOnTheIndex")] // We create an attribute so that we don't write anything to the BotFactory
    public class MultiExchangePairArbitrageOnTheIndex : BotPanel
    {
        // Index tabs
        private BotTabIndex _index;

        // Simple tabs
        private BotTabSimple _sec1Tab;
        private BotTabSimple _sec2Tab;
        private BotTabSimple _sec3Tab;
        private BotTabSimple _sec4Tab;
        private BotTabSimple _sec5Tab;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _moneyPercentFromDepoOnPosition;

        // GetVolume setting
        private StrategyParameterString _tradeAssetInPortfolio;

        // Deviation settings
        private StrategyParameterDecimal _minDeviationSecToIndexToEntry;
        private StrategyParameterDecimal _minDeviationSecToSecToEntry;
        private StrategyParameterDecimal _minDeviationToExit;

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

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });
            _moneyPercentFromDepoOnPosition = CreateParameter("Percent depo on position", 25m, 0.1m, 50, 0.1m);

            // GetVolume setting
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Deviation settings
            _minDeviationSecToIndexToEntry = CreateParameter("Min Deviation SecToIndex To Entry", 0.15m, 0.15m, 5, 0.1m);
            _minDeviationSecToSecToEntry = CreateParameter("Min Deviation SecToSec ToEntry", 0.5m, 0.1m, 5, 0.1m);
            _minDeviationToExit = CreateParameter("Min Deviation To Exit", 0.1m, 0.1m, 5, 0.1m);

            Description = OsLocalization.Description.DescriptionLabel46;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "MultiExchangePairArbitrageOnTheIndex";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        { 

        }

        // Logic open position
        private void _index_SpreadChangeEvent(List<Candle> index)
        {
            if (_regime.ValueString == "Off")
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

        // Logic open position
        private void TryOpenPositions(List<Candle> index)
        {
            // we take a list of security from the top and bottom of the index.

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

            // we choose the highest ones from the top

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

            // we select the lowest ones from the bottom

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

            if(percentMove >= _minDeviationSecToSecToEntry.ValueDecimal)
            {
                decimal volumeOnUpperSec = GetVolume(upSec);
                decimal volumeOnLowerSec = GetVolume(downSec);

                upSec.SellAtMarket(volumeOnUpperSec);
                downSec.BuyAtMarket(volumeOnLowerSec);
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

            if(diffPercent < _minDeviationSecToIndexToEntry.ValueDecimal)
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

            if (diffPercent < _minDeviationSecToIndexToEntry.ValueDecimal)
            {
                return false;
            }

            return true;
        }

        // Method for calculating the volume of entry into a position
        private decimal GetVolume(BotTabSimple tab)
        {
            Portfolio myPortfolio = tab.Portfolio;

            if (myPortfolio == null)
            {
                return 0;
            }

            decimal portfolioPrimeAsset = 0;

            if (_tradeAssetInPortfolio.ValueString == "Prime")
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
                    if (positionOnBoard[i].SecurityNameCode == _tradeAssetInPortfolio.ValueString)
                    {
                        portfolioPrimeAsset = positionOnBoard[i].ValueCurrent;
                        break;
                    }
                }
            }

            if (portfolioPrimeAsset == 0)
            {
                SendNewLogMessage("Can`t found portfolio " + _tradeAssetInPortfolio.ValueString, Logging.LogMessageType.Error);
                return 0;
            }

            decimal moneyOnPosition = portfolioPrimeAsset * (_moneyPercentFromDepoOnPosition.ValueDecimal / 100);

            decimal qty = moneyOnPosition / tab.PriceBestAsk;

            if (tab.StartProgram == StartProgram.IsOsTrader)
            {
                qty = Math.Round(qty, tab.Security.DecimalsVolume);
            }
            else
            {
                qty = Math.Round(qty, 7);
            }

            return qty;
        }

        // Get opening position
        private List<OpenPosByTab> GetOpenPoses()
        {
            List<OpenPosByTab> openPoses = new List<OpenPosByTab>();

            if(_sec1Tab.PositionsOpenAll.Count > 0)
            {
                OpenPosByTab newPos = new OpenPosByTab();
                newPos.Position = _sec1Tab.PositionsOpenAll[0];
                newPos.Tab = _sec1Tab;
                openPoses.Add(newPos);
            }

            if (_sec2Tab.PositionsOpenAll.Count > 0)
            {
                OpenPosByTab newPos = new OpenPosByTab();
                newPos.Position = _sec2Tab.PositionsOpenAll[0];
                newPos.Tab = _sec2Tab;
                openPoses.Add(newPos);
            }

            if (_sec3Tab.PositionsOpenAll.Count > 0)
            {
                OpenPosByTab newPos = new OpenPosByTab();
                newPos.Position = _sec3Tab.PositionsOpenAll[0];
                newPos.Tab = _sec3Tab;
                openPoses.Add(newPos);
            }

            if (_sec4Tab.PositionsOpenAll.Count > 0)
            {
                OpenPosByTab newPos = new OpenPosByTab();
                newPos.Position = _sec4Tab.PositionsOpenAll[0];
                newPos.Tab = _sec4Tab;
                openPoses.Add(newPos);
            }

            if (_sec5Tab.PositionsOpenAll.Count > 0)
            {
                OpenPosByTab newPos = new OpenPosByTab();
                newPos.Position = _sec5Tab.PositionsOpenAll[0];
                newPos.Tab = _sec5Tab;
                openPoses.Add(newPos);
            }

            return openPoses;
        }

        // logic close position

        private void TryClosePositions(List<OpenPosByTab> openPoses)
        {
            if (openPoses.Count == 1)
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

            if (percentMove <= _minDeviationToExit.ValueDecimal)
            {
                CloseAtMarket(upPosition);
                CloseAtMarket(downPosition);
            }
        }

        // Close at market
        private void CloseAtMarket(OpenPosByTab pos)
        {
            if (pos.Position.State != PositionStateType.Open)
            {
                return;
            }

            pos.Tab.CloseAtMarket(pos.Position, pos.Position.OpenVolume);
        }
    }

    public class OpenPosByTab
    {
        public Position Position;

        public BotTabSimple Tab;
    }
}