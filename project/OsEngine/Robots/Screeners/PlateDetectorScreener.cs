/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
trading robot for osengine

The trend robot on Plate Detector Screener.

Buy:
1. If the Bid ratio is higher than the specified minimum value BestBidMinRatioToAll.

Exit: based on stop and profit.
 */

namespace OsEngine.Robots.Screeners
{
    [Bot("PlateDetectorScreener")] // We create an attribute so that we don't write anything to the BotFactory
    public class PlateDetectorScreener : BotPanel
    {
        private BotTabScreener _tabScreener;

        // Basic settings
        private StrategyParameterString Regime;
        private StrategyParameterInt MaxPositions;
        private StrategyParameterDecimal BestBidMinRatioToAll;

        // GetVolume settings
        private StrategyParameterString VolumeType;
        private StrategyParameterDecimal Volume;
        private StrategyParameterString TradeAssetInPortfolio;

        // Exit settings
        private StrategyParameterDecimal ProfitPercent;
        private StrategyParameterDecimal StopPercent;
        private StrategyParameterInt OrderLifeTime;

        public PlateDetectorScreener(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);
            _tabScreener = TabsScreener[0];

            // Subscribe to the candle finished event
            _tabScreener.MarketDepthUpdateEvent += _tabScreener_MarketDepthUpdateEvent;

            // Basic settings
            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });
            MaxPositions = CreateParameter("Max positions", 5, 0, 20, 1);
            BestBidMinRatioToAll = CreateParameter("Best bid min ratio", 5m, 0, 20, 1m);

            // GetVolume settings
            VolumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            Volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            TradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Exit settings
            ProfitPercent = CreateParameter("Profit percent", 1.5m, 0, 20, 1m);
            StopPercent = CreateParameter("Stop percent", 0.5m, 0, 20, 1m);
            OrderLifeTime = CreateParameter("Order life time milliseconds", 2000, 0, 20, 1);

            Description = "The trend robot on Plate Detector Screener. " +
                "Buy: " +
                "1. If the Bid ratio is higher than the specified minimum value BestBidMinRatioToAll. " +
                "Exit: based on stop and profit.";
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "PlateDetectorScreener";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Trade logic
        private void _tabScreener_MarketDepthUpdateEvent(MarketDepth marketDepth, BotTabSimple tab)
        {
            if (Regime.ValueString == "Off")
            {
                return;
            }

            if (tab.IsConnected == false
                || tab.IsReadyToTrade == false)
            {
                return;
            }

            List<Position> openPositions = tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                if (Regime.ValueString == "OnlyClosePosition")
                {
                    return;
                }
                LogicEntry(marketDepth, tab);
            }
            else
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    LogicClosePosition(tab, openPositions[i]);
                }
            }
        }

        // Opening logic
        private void LogicEntry(MarketDepth marketDepth, BotTabSimple tab)
        {
            if (marketDepth == null)
            {
                return;
            }

            MarketDepth md = marketDepth.GetCopy();

            if (md.Bids == null ||
                md.Bids.Count == 0
                || md.Bids.Count < 10)
            {
                return;
            }

            decimal bestBidVolume = md.Bids[0].Bid;

            for (int i = 1; i < md.Bids.Count; i++)
            {
                // если отношение объёма в лучшем биде к любому другому биду
                // больше чем указанное значение - то не входим
                // нам нужна плита

                decimal curVolume = md.Bids[i].Bid;
                decimal ratio = bestBidVolume / curVolume;

                if (ratio < BestBidMinRatioToAll.ValueDecimal)
                {
                    return;
                }
            }

            decimal openOrderPrice = md.Bids[0].Price + tab.Security.PriceStep;
            decimal volume = GetVolume(tab);

            tab.ManualPositionSupport.DisableManualSupport();
            tab.BuyAtLimit(volume, openOrderPrice);
        }

        // Logic close position
        private void LogicClosePosition(BotTabSimple tab, Position position)
        {
            if (position.OpenOrders[0].State == OrderStateType.Active) // the order has not yet been executed for opening
            { 
                Order order = position.OpenOrders[0];

                if (position.SignalTypeOpen == "Canceled")
                {
                    return;
                }

                if (order.TimeCreate.AddMilliseconds(OrderLifeTime.ValueInt) < tab.TimeServerCurrent)
                {
                    position.SignalTypeOpen = "Canceled";
                    tab.CloseAllOrderToPosition(position);
                }
            }
            else
            {
                if (position.OpenOrders[0].MyTrades == null
                    || position.OpenOrders[0].MyTrades.Count == 0)
                {
                    // Trades on the order have not arrived yet. It is too early to process the position
                    return;
                }

                if (position.StopOrderRedLine == 0)
                {
                    // 1 we calculate the stop, once. And we place a closing order.
                    // This means that with this position in this branch of logic we are for the first time

                    decimal stopPrice =
                        position.EntryPrice - position.EntryPrice * (StopPercent.ValueDecimal / 100);
                    position.StopOrderRedLine = stopPrice;

                    // we place an order to close the position
                    decimal profitOrderPrice =
                        position.EntryPrice + position.EntryPrice * (ProfitPercent.ValueDecimal / 100);
                    tab.CloseAtLimit(position, profitOrderPrice, position.OpenVolume);
                }

                // 2 exit at stop

                decimal bestSellPrice = tab.PriceBestAsk;

                if (bestSellPrice <= position.StopOrderRedLine
                    || position.SignalTypeClose == "Stop")
                {
                    // we recall the order to close at profit
                    if (position.CloseActive == true
                        && position.SignalTypeClose != "Stop")
                    {
                        tab.CloseAllOrderToPosition(position);
                    }

                    position.SignalTypeClose = "Stop";

                    if (position.CloseActive == true)
                    {
                        return;
                    }

                    if (position.CloseOrders.Count == 1)
                    {
                        tab.CloseAtMarket(position, position.OpenVolume);
                    }
                }
            }
        }

        // Method for calculating the volume of entry into a position
        private decimal GetVolume(BotTabSimple tab)
        {
            decimal volume = 0;

            if (VolumeType.ValueString == "Contracts")
            {
                volume = Volume.ValueDecimal;
            }
            else if (VolumeType.ValueString == "Contract currency")
            {
                decimal contractPrice = tab.PriceBestAsk;
                volume = Volume.ValueDecimal / contractPrice;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);

                    if (serverPermission != null &&
                        serverPermission.IsUseLotToCalculateProfit &&
                    tab.Security.Lot != 0 &&
                        tab.Security.Lot > 1)
                    {
                        volume = Volume.ValueDecimal / (contractPrice * tab.Security.Lot);
                    }

                    volume = Math.Round(volume, tab.Security.DecimalsVolume);
                }
                else // Tester or Optimizer
                {
                    volume = Math.Round(volume, 6);
                }
            }
            else if (VolumeType.ValueString == "Deposit percent")
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
                        if (positionOnBoard[i].SecurityNameCode == TradeAssetInPortfolio.ValueString)
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

                decimal moneyOnPosition = portfolioPrimeAsset * (Volume.ValueDecimal / 100);

                decimal qty = moneyOnPosition / tab.PriceBestAsk / tab.Security.Lot;

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

            return volume;
        }
    }
}