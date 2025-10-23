/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
Trading robot for osengine.

The trend robot on MarketDepth Screener.

Buy:
1. Step First: Analyze the latest Momentum value: if it is below the minimum 
acceptable value (MinMomentumValue), proceed to the next entry step.

2. Step Second: Check the volume ratio of the bids: if the volume of the best bid is too 
small compared to other bids (ratio below the threshold BestBidMinRatioToAll), do not enter.

Exit: by stop and profit.
*/

namespace OsEngine.Robots.High_Frequency
{
    [Bot("MarketDepthScreener")] // We create an attribute so that we don't write anything to the BotFactory
    public class MarketDepthScreener : BotPanel
    {
        BotTabScreener _tabScreener;
        
        // Basic settings
        private StrategyParameterString Regime;
        public StrategyParameterInt MaxPositions; 
        public StrategyParameterDecimal MinMomentumValue;
        public StrategyParameterDecimal BestBidMinRatioToAll;

        // Indicator settings
        public StrategyParameterInt MomentumLen;

        // GetVolume settings
        public StrategyParameterString VolumeType;
        public StrategyParameterDecimal Volume;
        public StrategyParameterString TradeAssetInPortfolio;

        // Exit settings
        public StrategyParameterDecimal ProfitPercent;
        public StrategyParameterDecimal StopPercent;
        public StrategyParameterInt OrderLifeTime;

        public MarketDepthScreener(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);

            _tabScreener = TabsScreener[0];
            _tabScreener.CreateCandleIndicator(2, "Momentum", new List<string>() { "15" }, "Second");

            // Basic settings
            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyClosePosition" });
            MaxPositions = CreateParameter("Max positions", 5, 0, 20, 1);
            MinMomentumValue = CreateParameter("Min momentum value", 95m, 0, 20, 1m);
            BestBidMinRatioToAll = CreateParameter("Best bid min ratio", 5m, 0, 20, 1m);
            
            // GetVolume settings
            VolumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            Volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            TradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator settings
            MomentumLen = CreateParameter("Momentum length", 50, 0, 20, 1);

            // Exit settings
            ProfitPercent = CreateParameter("Profit percent", 0.05m, 0, 20, 1m);
            StopPercent = CreateParameter("Stop percent", 0.05m, 0, 20, 1m);
            OrderLifeTime = CreateParameter("Order life time milliseconds", 2000, 0, 20, 1);

            // Create worker area
            Thread worker = new Thread(WorkerPlace);
            worker.Start();
            
            DeleteEvent += MarketDepthScreener_DeleteEvent;

            Description = OsLocalization.Description.DescriptionLabel44;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "MarketDepthScreener";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Logic
        private void MarketDepthScreener_DeleteEvent()
        {
            _botIsDelete = true;
        }

        private bool _botIsDelete = false;

        // Worker place
        public void WorkerPlace()
        {
            while(true)
            {
                try
                {
                    if (_botIsDelete == true)
                    { // exit
                        return;
                    }

                    if (Regime.ValueString == "Off")
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    List<BotTabSimple> tabsToTrade = _tabScreener.Tabs;

                    for(int i = 0;tabsToTrade != null && i < tabsToTrade.Count;i++)
                    {
                        BotTabSimple tab = tabsToTrade[i];
                        TradeLogicEntry(tab);
                    }

                    Thread.Sleep(100);
                }
                catch(Exception e)
                {
                    SendNewLogMessage(e.ToString(),Logging.LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        // Trade logic Entry
        private void TradeLogicEntry(BotTabSimple tab)
        {
            if (Regime.ValueString == "Off")
            {
                return;
            }

            if(tab.IsConnected == false
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
                LogicEntryFirstStep(tab);
            }
            else
            {
                for(int i = 0;i < openPositions.Count;i++)
                {
                    LogicClosePosition(tab, openPositions[i]);
                }
            }
        }

        // Trade logic entry first Step
        private void LogicEntryFirstStep(BotTabSimple tab)
        {
            if (_tabScreener.PositionsOpenAll.Count >= MaxPositions.ValueInt)
            {
                return;
            }

            Aindicator momentum = (Aindicator)tab.Indicators[0];

            if (momentum.ParametersDigit[0].Value != MomentumLen.ValueInt)
            {
                momentum.ParametersDigit[0].Value = MomentumLen.ValueInt;
                momentum.Save();
                momentum.Reload();
            }

            if (momentum.DataSeries[0].Values.Count == 0 ||
                momentum.DataSeries[0].Last == 0)
            {
                return;
            }

            decimal lastMomentum = momentum.DataSeries[0].Last;

            if (lastMomentum < MinMomentumValue.ValueDecimal)
            {
                LogicEntrySecondStep(tab);
            }
        }

        // Trade logic Entry second step
        private void LogicEntrySecondStep(BotTabSimple tab)
        {
            if(tab.MarketDepth == null)
            {
                return;
            }

            MarketDepth md = tab.MarketDepth.GetCopy();

            if(md.Bids == null ||
                md.Bids.Count == 0
                || md.Bids.Count < 10)
            {
                return;
            }

            decimal bestBidVolume = md.Bids[0].Bid.ToDecimal();

            for(int i = 1; i < md.Bids.Count; i++)
            {
                // if the ratio of the volume in the best bid to any other bid
                // more than the specified value - then we do not enter
                // we need a stove

                decimal curVolume = md.Bids[i].Bid.ToDecimal();
                decimal ratio = bestBidVolume / curVolume;

                if(ratio < BestBidMinRatioToAll.ValueDecimal)
                {
                    return;
                }
            }

            decimal openOrderPrice = md.Bids[0].Price.ToDecimal() + tab.Security.PriceStep;
            decimal volume = GetVolume(tab);

            tab.ManualPositionSupport.DisableManualSupport();

            tab.BuyAtLimit(volume, openOrderPrice);
        }

        // Close position logic
        private void LogicClosePosition(BotTabSimple tab, Position position)
        {
            if (position.OpenOrders[0].State == OrderStateType.Active)
            { // the order has not yet been executed for opening
                Order order = position.OpenOrders[0];

                if(position.SignalTypeOpen == "Canceled")
                { 
                    return;
                }

                if(order.TimeCreate.AddMilliseconds(OrderLifeTime.ValueInt) < tab.TimeServerCurrent)
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
                    // trades on the order have not arrived yet. It is too early to process the position
                    return;
                }

                if(position.StopOrderRedLine == 0)
                {
                    // 1 We calculate the stop once. And we place a closing order.
                    // this means that with this position in this branch of logic we are for the first time

                    decimal stopPrice = 
                        position.EntryPrice - position.EntryPrice * (StopPercent.ValueDecimal / 100);
                    position.StopOrderRedLine = stopPrice;

                    // we place an order to close the position
                    decimal profitOrderPrice = 
                        position.EntryPrice + position.EntryPrice * (ProfitPercent.ValueDecimal / 100);
                    tab.CloseAtLimit(position, profitOrderPrice, position.OpenVolume);
                }

                // 2 exit by stop

                decimal bestSellPrice = tab.PriceBestAsk;

                if(bestSellPrice <= position.StopOrderRedLine
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

                    if(position.CloseOrders.Count == 1)
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
                    if (tab.Security.UsePriceStepCostToCalculateVolume == true
                     && tab.Security.PriceStep != tab.Security.PriceStepCost
                     && tab.PriceBestAsk != 0
                     && tab.Security.PriceStep != 0
                     && tab.Security.PriceStepCost != 0)
                    {// расчёт количества контрактов для фьючерсов и опционов на Мосбирже
                        qty = moneyOnPosition / (tab.PriceBestAsk / tab.Security.PriceStep * tab.Security.PriceStepCost);
                    }

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