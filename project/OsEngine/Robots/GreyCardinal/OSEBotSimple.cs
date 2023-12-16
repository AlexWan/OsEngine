using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


using OsEngine.Entity;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;

/*

namespace OsEngine.Robots.GreyCardinal
{
    internal class UniversalBot
    {
    }
}
*/

namespace OsEngine.Robots.GreyCardinal
{
    [Bot("OSEBotSimple")]

    internal class OSEBotSimple : BotPanel
    {

        private BotTabSimple _tabSimple;
        private StrategyParameterBool isActive;
        private StrategyParameterDecimal volumeParam;
        private StrategyParameterInt pointsSL, pointsTP;

        // Common procedures
        public override string GetNameStrategyType()
        {
            return "OSEBotSimple";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        // Constructor
        public OSEBotSimple(string name, StartProgram startProgram) : base(name, startProgram)
        {
            Description = "This is bot description";
            
            TabCreate(BotTabType.Simple);
            _tabSimple = TabsSimple[0];

            // Events
            _tabSimple.CandleFinishedEvent += _tab_CandleFinishedEvent;
            _tabSimple.PositionOpeningSuccesEvent += _tab_PositionOpeningSuccesEvent;
            _tabSimple.MarketDepthUpdateEvent += _tab_MarketDepthUpdateEvent;
            _tabSimple.NewTickEvent += _tab_NewTickEvent;

            // Params
            isActive = CreateParameter("Is Active", true);
            volumeParam = CreateParameter("Volume", 1.0m, 1.0m, 10.0m, 1.0m);
            pointsSL = CreateParameter("Points to StopLost", 50, 10, 1000, 1);
            pointsTP = CreateParameter("Points to TakeProfit", 150, 10, 1000, 1);

        }

        // Follow opening positions
        private void _tab_PositionOpeningSuccesEvent(Position position)
        {
            trallingPosition(position, _tabSimple);
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
//                tab.CloseAtTrailingStop(position, position.EntryPrice + pointsSL.ValueInt * _tabToTrade.Securiti.PriceStep, position.EntryPrice + pointsSL.ValueInt * _tabToTrade.Securiti.PriceStep);
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
                //  tab.CloseAtTrailingStop(position, position.EntryPrice + pointsSL.ValueInt * _tabToTrade.Securiti.PriceStep, position.EntryPrice + pointsSL.ValueInt * _tabToTrade.Securiti.PriceStep);
            }
        }
        
        // Logics for enter position
        private void _tab_CandleFinishedEvent(List<Candle> candels)
        {
            if (isActive.ValueBool == false) { return; }


            List<Position> positions = _tabSimple.PositionsOpenAll;

            if (positions.Count == 0)
            {

            }
            else
            {
                foreach(var position in positions)
                {
                    trallingPosition(position, _tabSimple);
                }
            }
        }

        private void _tab_MarketDepthUpdateEvent(MarketDepth marketDepth)
        {
            if (!_tabSimple.IsConnected)
            {
                return;
            }

        }

        private void _tab_NewTickEvent(Trade trade)
        {
            if (!_tabSimple.IsConnected)
            {
                return;
            }
        }

    }
}


