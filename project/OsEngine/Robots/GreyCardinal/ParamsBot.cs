using OsEngine.Entity;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OsEngine.Market.Servers.Bitfinex.BitfitnexEntity;

namespace OsEngine.Robots.GreyCardinal
{
    [Bot("ParamsBot")]
    
    internal class ParamsBot : BotPanel
    {
        private BotTabSimple _tab;
        private StrategyParameterBool isOnParam;
        private StrategyParameterDecimal volumeParam;
        private StrategyParameterInt pointsSL, pointsTP;

        public ParamsBot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];
            
            // Events
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
            _tab.PositionOpeningSuccesEvent += _tab_PositionOpeningSuccesEvent;
            _tab.MarketDepthUpdateEvent += _tab_MarketDepthUpdateEvent;
            _tab.NewTickEvent += _tab_NewTickEvent;
            
            // Params
            isOnParam = CreateParameter("Is On", false);
            volumeParam = CreateParameter("Volume", 1.0m, 1.0m, 10.0m, 1.0m);
            pointsSL = CreateParameter("Points to StopLost", 50, 10, 1000, 1);
            pointsTP = CreateParameter("Points to TakeProfit", 150, 10, 1000, 1);

        }

        public override string GetNameStrategyType()
        {
            return "ParamsBot";//throw new NotImplementedException();
        }

        public override void ShowIndividualSettingsDialog()
        {
            //throw new NotImplementedException();
        }

        private void _tab_CandleFinishedEvent(List<Candle> candels)
        {
            if (isOnParam.ValueBool == false) { return; }
            
            
            List<Position> positions = _tab.PositionsOpenAll;
            
            if (positions.Count ==0) { 
 
            }
            
 //               _tab.BuyAtLimit(1, lastCandle.Close);
        }
        private void _tab_PositionOpeningSuccesEvent(Position position)
        {
            if (position.Direction == Side.Buy)
            {
                _tab.CloseAtStop(position, position.EntryPrice - pointsSL.ValueInt * _tab.Securiti.PriceStep, position.EntryPrice - pointsSL.ValueInt * _tab.Securiti.PriceStep);
                //_tab.CloseAtProfit(position, position.EntryPrice + 200 * _tab.Securiti.PriceStep, position.EntryPrice + 200 * _tab.Securiti.PriceStep);
                _tab.CloseAtTrailingStop(position, position.EntryPrice + pointsSL.ValueInt * _tab.Securiti.PriceStep, position.EntryPrice + pointsSL.ValueInt * _tab.Securiti.PriceStep);
            }
            if (position.Direction == Side.Sell)
            {
                _tab.CloseAtStop(position, position.EntryPrice - pointsSL.ValueInt * _tab.Securiti.PriceStep, position.EntryPrice - pointsSL.ValueInt * _tab.Securiti.PriceStep);
                //_tab.CloseAtProfit(position, position.EntryPrice + 200 * _tab.Securiti.PriceStep, position.EntryPrice + 200 * _tab.Securiti.PriceStep);
                _tab.CloseAtTrailingStop(position, position.EntryPrice + pointsSL.ValueInt * _tab.Securiti.PriceStep, position.EntryPrice + pointsSL.ValueInt * _tab.Securiti.PriceStep);
            }
        }

        private void _tab_MarketDepthUpdateEvent(MarketDepth marketDepth)
        {
            if (!_tab.IsConnected) {
                return; 
            }

        }
        
        private void _tab_NewTickEvent(Trade trade)
        {
            if (!_tab.IsConnected)
            {
                return;
            }
        }


    }
}

