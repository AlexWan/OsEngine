using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using OsEngine.Entity;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;


namespace OsEngine.Robots.GreyCardinal
{
    [Bot("MarketProfileBot")]
    
    internal class MarketProfileBot : BotPanel
    {

        private BotTabSimple _tabToTrade;
        private BotTabCluster _tabCluster;
        private StrategyParameterBool isOnParam;
        private StrategyParameterDecimal volumeParam;
        private StrategyParameterInt pointsSL, pointsTP;

        public MarketProfileBot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tabToTrade = TabsSimple[0];
            TabCreate(BotTabType.Cluster);
            _tabCluster = TabsCluster[0];



            // Events
            _tabToTrade.CandleFinishedEvent += _tab_CandleFinishedEvent;
            _tabToTrade.PositionOpeningSuccesEvent += _tab_PositionOpeningSuccesEvent;
            _tabToTrade.MarketDepthUpdateEvent += _tab_MarketDepthUpdateEvent;
            _tabToTrade.NewTickEvent += _tab_NewTickEvent;

            // Params
            isOnParam = CreateParameter("Is On", true);
            volumeParam = CreateParameter("Volume", 1.0m, 1.0m, 10.0m, 1.0m);
            pointsSL = CreateParameter("Points to StopLost", 50, 10, 1000, 1);
            pointsTP = CreateParameter("Points to TakeProfit", 150, 10, 1000, 1);

        }

        public override string GetNameStrategyType()
        {
            return "MarketProfileBot";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        private void _tab_CandleFinishedEvent(List<Candle> candels)
        {
            if (!_tabToTrade.IsConnected|| isOnParam.ValueBool == false) { return; }

            if(_tabCluster.VolumeClusters.Count<6) { return; }

            List<Position> positions = _tabToTrade.PositionsOpenAll;

            if (positions.Count == 0)
            {

                HorizontalVolumeCluster cluster = _tabCluster.FindMaxVolumeCluster(_tabCluster.VolumeClusters.Count - 6
                    , _tabCluster.VolumeClusters.Count - 1, ClusterType.SummVolume);
                HorizontalVolumeLine line = cluster.MaxSummVolumeLine;
                if ((candels[candels.Count - 1].Close > line.Price))
                {
                    _tabToTrade.BuyAtMarket(volumeParam.ValueDecimal);
                }
                else if ((candels[candels.Count - 1].Close < line.Price))
                {
                    _tabToTrade.SellAtMarket(volumeParam.ValueDecimal);
                }
            }
            else if (positions.Count > 0)
            {
                Position position = positions[0];
                trallingPosition(position);
            }
        }

        private void trallingPosition(Position position) 
        {
            decimal _newStopPrice=0,_newProfitPrice=0;
            if (position.Direction == Side.Buy)
            {
                _newStopPrice = _tabToTrade.PriceBestAsk - pointsSL.ValueInt * _tabToTrade.Securiti.PriceStep;
                if (_newStopPrice > position.StopOrderPrice)
                {
                    _tabToTrade.CloseAtStop(position, _newStopPrice, _newStopPrice);
                }
                _newProfitPrice = _tabToTrade.PriceBestBid + pointsTP.ValueInt * _tabToTrade.Securiti.PriceStep;
                if (_newProfitPrice > position.ProfitOrderPrice)
                {
                    _tabToTrade.CloseAtProfit(position, _newProfitPrice, _newProfitPrice);
                }
                //               _tabToTrade.CloseAtTrailingStop(position, position.EntryPrice + pointsSL.ValueInt * _tabToTrade.Securiti.PriceStep, position.EntryPrice + pointsSL.ValueInt * _tabToTrade.Securiti.PriceStep);
            }
            if (position.Direction == Side.Sell)
            {
                _newStopPrice = _tabToTrade.PriceBestBid + pointsSL.ValueInt * _tabToTrade.Securiti.PriceStep;
                if (_newStopPrice < position.StopOrderPrice)
                    _tabToTrade.CloseAtStop(position, _newStopPrice, _newStopPrice);
                _newProfitPrice = _tabToTrade.PriceBestAsk - pointsTP.ValueInt * _tabToTrade.Securiti.PriceStep;
                if (_newProfitPrice < position.ProfitOrderPrice)
                {
                    _tabToTrade.CloseAtProfit(position, _newProfitPrice, _newProfitPrice);
                }
                //                _tabToTrade.CloseAtStop(position, _tabToTrade.PriceBestBid + pointsSL.ValueInt * _tabToTrade.Securiti.PriceStep, _tabToTrade.PriceBestBid - pointsSL.ValueInt * _tabToTrade.Securiti.PriceStep);
                //_tab.CloseAtProfit(position, position.EntryPrice + 200 * _tab.Securiti.PriceStep, position.EntryPrice + 200 * _tab.Securiti.PriceStep);
                //             _tabToTrade.CloseAtTrailingStop(position, position.EntryPrice + pointsSL.ValueInt * _tabToTrade.Securiti.PriceStep, position.EntryPrice + pointsSL.ValueInt * _tabToTrade.Securiti.PriceStep);
            }
        }

        private void _tab_PositionOpeningSuccesEvent(Position position)
        {
            trallingPosition(position);
 //           if (position.Direction == Side.Buy)
 //           {
 //               _tabToTrade.CloseAtStop(position, position.EntryPrice - pointsSL.ValueInt * _tabToTrade.Securiti.PriceStep, position.EntryPrice - pointsSL.ValueInt * _tabToTrade.Securiti.PriceStep);
 //               //_tab.CloseAtProfit(position, position.EntryPrice + 200 * _tab.Securiti.PriceStep, position.EntryPrice + 200 * _tab.Securiti.PriceStep);
 ////               _tabToTrade.CloseAtTrailingStop(position, position.EntryPrice + pointsSL.ValueInt * _tabToTrade.Securiti.PriceStep, position.EntryPrice + pointsSL.ValueInt * _tabToTrade.Securiti.PriceStep);
 //           }
 //           if (position.Direction == Side.Sell)
 //           {
 //               _tabToTrade.CloseAtStop(position, position.EntryPrice - pointsSL.ValueInt * _tabToTrade.Securiti.PriceStep, position.EntryPrice - pointsSL.ValueInt * _tabToTrade.Securiti.PriceStep);
 //               //_tab.CloseAtProfit(position, position.EntryPrice + 200 * _tab.Securiti.PriceStep, position.EntryPrice + 200 * _tab.Securiti.PriceStep);
 ////               _tabToTrade.CloseAtTrailingStop(position, position.EntryPrice + pointsSL.ValueInt * _tabToTrade.Securiti.PriceStep, position.EntryPrice + pointsSL.ValueInt * _tabToTrade.Securiti.PriceStep);
 //           }
        }

        private void _tab_MarketDepthUpdateEvent(MarketDepth marketDepth)
        {
            if (!_tabToTrade.IsConnected)
            {
                return;
            }

        }

        private void _tab_NewTickEvent(Trade trade)
        {
            if (!_tabToTrade.IsConnected)
            {
                return;
            }
        }

    }
}

