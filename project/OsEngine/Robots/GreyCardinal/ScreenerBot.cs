using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using OsEngine.Entity;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;
using OsEngine.Indicators;
using System.Data.SqlTypes;
using OsEngine.Market.Servers.GateIo.Futures.Response;


namespace OsEngine.Robots.GreyCardinal
{
    [Bot("ScreenerBot")]

    internal class ScreenerBot : BotPanel
    {

        private BotTabScreener _tabScreener;
        private StrategyParameterBool isActive;
        private StrategyParameterDecimal volumeParam;
        private StrategyParameterInt MaxPosition;
        private StrategyParameterInt pointsSL, pointsTP;

        public override string GetNameStrategyType()
        {
            return "ScreenerBot";
        }


        public override void ShowIndividualSettingsDialog()
        {

        }


        // Constructor
        public ScreenerBot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            Description = "This is bot description";

            TabCreate(BotTabType.Screener);
            _tabScreener = TabsScreener[0];

            // Events

            // Params
            isActive = CreateParameter("Is Active", true);
            volumeParam = CreateParameter("Volume", 1.0m, 1.0m, 10.0m, 1.0m);
            MaxPosition = CreateParameter("Max Position", 1, 1, 10, 1);
            pointsSL = CreateParameter("Percent to StopLost", 1, 1, 10, 1);
            pointsTP = CreateParameter("Percent to TakeProfit", 3, 3, 20, 1);
            _tabScreener.CreateCandleIndicator(1, "PriceChannel", new List<string> { "40", "40" });
            _tabScreener.CreateCandleIndicator(1, "Sma", new List<string> { "200" });

            _tabScreener.CandleFinishedEvent += _tabScreener_CandleFinishedEvent;
        }

        private void _tabScreener_CandleFinishedEvent(List<Candle> candels, BotTabSimple tab)
        {
            tab.BuyAtStopCancel();
            tab.SellAtStopCancel();
            if(candels.Count<100|| !isActive.ValueBool)
            {
                return;
            }
            bool canOpenBuyPoses = true;
            List<Position> positionsAll = _tabScreener.PositionsOpenAll;
            if (positionsAll.Count >= MaxPosition.ValueInt)
            {
                canOpenBuyPoses = false;
            }
            bool canOpenBuyThisTab = true;

            List<Position> positionsTab = tab.PositionsAll;
            if (positionsTab.Count > 0)
            {
                canOpenBuyThisTab = false;
            }
            if (canOpenBuyPoses && canOpenBuyThisTab)
            {
                EntryLogic(candels, tab);
            }
            if(positionsTab.Count>0)
            {
                ExitLogic(candels, tab, positionsTab[0]);
            }

            
        }

        private void ExitLogic(List<Candle> candels, BotTabSimple tab, Position position)
        {
            if (position.State != PositionStateType.Open)
            {
                return;
            }
            decimal stopPrice = 0;
            decimal low = candels[candels.Count - 1].Low;
            decimal high = candels[candels.Count - 1].High;
            if(position.Direction == Side.Buy)
            {
                stopPrice = low - low * pointsSL.ValueInt / 100;
            }
            if (position.Direction == Side.Sell)
            {
                stopPrice = high +high * pointsSL.ValueInt / 100;
            }
            if (stopPrice != 0)
            {
                tab.CloseAtTrailingStop(position, stopPrice, stopPrice);
            }
        }

        private void EntryLogic(List<Candle> candels, BotTabSimple tab)
        {
            Aindicator pc = (Aindicator)tab.Indicators[0],ma = (Aindicator)tab.Indicators[1];
            decimal lastma = ma.DataSeries[0].Last;
            decimal previousma = ma.DataSeries[0].Values[ma.DataSeries[0].Values.Count - 2];

            decimal upChannel = pc.DataSeries[0].Last;
            decimal downChannel = pc.DataSeries[1].Last;
            if(lastma==0 || upChannel==0 || downChannel == 0)
            {
                return;
            }
            if (lastma > previousma)
            {
                tab.BuyAtStop(1, upChannel, upChannel, StopActivateType.HigherOrEqual);

            }
            if (lastma < previousma)
            {
                tab.SellAtStop(1, downChannel, downChannel, StopActivateType.LowerOrEqyal);
            }
        }

        // Follow opening positions
        private void _tab_PositionOpeningSuccesEvent(Position position)
        {
        }

        private void trallingPosition(Position position, BotTabSimple tab)
        {
            decimal _newStopPrice = 0, _newProfitPrice = 0;
            if (position.Direction == Side.Buy)
            {
                _newStopPrice = tab.PriceBestAsk - pointsSL.ValueInt * tab.Securiti.PriceStep;
                if (_newStopPrice > position.StopOrderPrice)
                {
                    tab.CloseAtStop(position, _newStopPrice, _newStopPrice);
                }
                _newProfitPrice = tab.PriceBestBid + pointsTP.ValueInt * tab.Securiti.PriceStep;
                if (_newProfitPrice > position.ProfitOrderPrice)
                {
                    tab.CloseAtProfit(position, _newProfitPrice, _newProfitPrice);
                }
                //                tab.CloseAtTrailingStop(position, position.EntryPrice + pointsSL.ValueInt * tab.Securiti.PriceStep, position.EntryPrice + pointsSL.ValueInt * tab.Securiti.PriceStep);
            }
            if (position.Direction == Side.Sell)
            {
                _newStopPrice = tab.PriceBestBid + pointsSL.ValueInt * tab.Securiti.PriceStep;
                if (_newStopPrice < position.StopOrderPrice)
                    tab.CloseAtStop(position, _newStopPrice, _newStopPrice);
                _newProfitPrice = tab.PriceBestAsk - pointsTP.ValueInt * tab.Securiti.PriceStep;
                if (_newProfitPrice < position.ProfitOrderPrice)
                {
                    tab.CloseAtProfit(position, _newProfitPrice, _newProfitPrice);
                }
                //  tab.CloseAtTrailingStop(position, position.EntryPrice + pointsSL.ValueInt * tab.Securiti.PriceStep, position.EntryPrice + pointsSL.ValueInt * tab.Securiti.PriceStep);
            }
        }

        // Logics for enter position

    }
}

