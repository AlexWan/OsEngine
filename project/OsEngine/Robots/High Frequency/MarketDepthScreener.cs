/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Robots.High_Frequency
{
    [Bot("MarketDepthScreener")]
    public class MarketDepthScreener : BotPanel
    {
        BotTabScreener _tabScreener;

        public StrategyParameterString Regime;
        public StrategyParameterInt MaxPositions;
        public StrategyParameterInt MomentumLen;
        public StrategyParameterString VolumeType;
        public StrategyParameterDecimal Volume;
        public StrategyParameterString TradeAssetInPortfolio;
        public StrategyParameterDecimal MinMomentumValue;
        public StrategyParameterDecimal BestBidMinRatioToAll;
        public StrategyParameterDecimal ProfitPercent;
        public StrategyParameterDecimal StopPercent;
        public StrategyParameterInt OrderLifeTime;

        public MarketDepthScreener(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);

            _tabScreener = TabsScreener[0];
            _tabScreener.CreateCandleIndicator(2, "Momentum", new List<string>() { "15" }, "Second");

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyClosePosition" });
            MaxPositions = CreateParameter("Max positions", 5, 0, 20, 1);
            MinMomentumValue = CreateParameter("Min momentum value", 95m, 0, 20, 1m);
            MomentumLen = CreateParameter("Momentum length", 50, 0, 20, 1);
            VolumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            Volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            TradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            BestBidMinRatioToAll = CreateParameter("Best bid min ratio", 5m, 0, 20, 1m);
            ProfitPercent = CreateParameter("Profit percent", 0.05m, 0, 20, 1m);
            StopPercent = CreateParameter("Stop percent", 0.05m, 0, 20, 1m);
            OrderLifeTime = CreateParameter("Order life time milliseconds", 2000, 0, 20, 1);

            DeleteEvent += MarketDepthScreener_DeleteEvent;

            Thread worker = new Thread(WorkerPlace);
            worker.Start();
        }

        public override string GetNameStrategyType()
        {
            return "MarketDepthScreener";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        // logic

        private void MarketDepthScreener_DeleteEvent()
        {
            _botIsDelete = true;
        }

        private bool _botIsDelete = false;

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

            decimal bestBidVolume = md.Bids[0].Bid;

            for(int i = 1; i < md.Bids.Count; i++)
            {
                // если отношение объёма в лучшем биде к любому другому биду
                // больше чем указанное значение - то не входим
                // нам нужна плита

                decimal curVolume = md.Bids[i].Bid;
                decimal ratio = bestBidVolume / curVolume;

                if(ratio < BestBidMinRatioToAll.ValueDecimal)
                {
                    return;
                }
            }

            decimal openOrderPrice = md.Bids[0].Price + tab.Securiti.PriceStep;
            decimal volume = GetVolume(tab);

            tab.ManualPositionSupport.DisableManualSupport();
            tab.BuyAtLimit(volume, openOrderPrice);
        }

        private void LogicClosePosition(BotTabSimple tab, Position position)
        {
            if (position.OpenOrders[0].State == OrderStateType.Activ)
            { // ордер ещё не исполнен на открытие
                Order order = position.OpenOrders[0];

                if(position.SignalTypeOpen == "Canceled")
                { 
                    // уже отозвали открывающий орде по позиции в прошлые заходы в метод
                    // ждём пока коннектор отработает и отзовёт ордер
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
                    // трейды по ордеру ещё не пришли. Рано обрабатывать позицию
                    return;
                }

                if(position.StopOrderRedLine == 0)
                {
                    // 1 рассчитываем стоп, один раз. И выставляем закрывающий ордер.
                    // это значит что с этой позицией в этой ветке логики мы впервые

                    decimal stopPrice = 
                        position.EntryPrice - position.EntryPrice * (StopPercent.ValueDecimal / 100);
                    position.StopOrderRedLine = stopPrice;

                    // выставляем ордер на закрытие позиции
                    decimal profitOrderPrice = 
                        position.EntryPrice + position.EntryPrice * (ProfitPercent.ValueDecimal / 100);
                    tab.CloseAtLimit(position, profitOrderPrice, position.OpenVolume);
                }

                // 2 выход по стопу

                decimal bestSellPrice = tab.PriceBestAsk;

                if(bestSellPrice <= position.StopOrderRedLine
                    || position.SignalTypeClose == "Stop")
                {
                    // отзываем ордер на закрытие по профиту
                    if (position.CloseActiv == true 
                        && position.SignalTypeClose != "Stop")
                    {
                        tab.CloseAllOrderToPosition(position);
                    }

                    position.SignalTypeClose = "Stop";

                    if (position.CloseActiv == true)
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
                    tab.Securiti.Lot != 0 &&
                        tab.Securiti.Lot > 1)
                    {
                        volume = Volume.ValueDecimal / (contractPrice * tab.Securiti.Lot);
                    }

                    volume = Math.Round(volume, tab.Securiti.DecimalsVolume);
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

                decimal qty = moneyOnPosition / tab.PriceBestAsk / tab.Securiti.Lot;

                if (tab.StartProgram == StartProgram.IsOsTrader)
                {
                    qty = Math.Round(qty, tab.Securiti.DecimalsVolume);
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