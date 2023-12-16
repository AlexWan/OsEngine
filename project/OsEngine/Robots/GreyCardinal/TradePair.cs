using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;


namespace OsEngine.Robots.GreyCardinal
{
    [Bot("TradePair")]

    
    internal class TradePair : BotPanel
    {
        private BotTabPair _pairTrader;

        private StrategyParameterInt _maxPositionCount;
        public StrategyParameterString _regime;

        public TradePair(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Pair);
            _pairTrader = this.TabsPair[0];
            _regime = CreateParameter("Regime", "On", new[] { "Off", "On" });
            _maxPositionCount = CreateParameter("Max position count", 5, 5, 5, 5);

            _pairTrader.CorrelationChangeEvent += _pairTrader_CorrelationChangeEvent;
        }

        private void _pairTrader_CorrelationChangeEvent(List<PairIndicatorValue> correlation, PairToTrade pair)
        {
            if(_regime.ValueString=="Off")
            {
                return;
            }
            if (pair.HavePositions)
            {
                ClosePositionLogic(pair);

            }
            else
            {
                OpenPositionLogic(pair);
            }
        }

        private void OpenPositionLogic(PairToTrade pair)
        {
            if (pair.CorrelationLast > 0.3m)
            {
                return;
            }
            if(_pairTrader.PairsWithPositionsCount>=_maxPositionCount.ValueInt)
            {
                return;
            }
            if (pair.SideCointegrationValue == CointegrationLineSide.Up)
            {
                pair.SellSec1BuySec2();
            }
            else if(pair.SideCointegrationValue == CointegrationLineSide.Down)
            {
                pair.BuySec1SellSec2();
            }
        }

        private void ClosePositionLogic(PairToTrade pair)
        {
            decimal cointegrationLast = pair.CorrelationLast;
            decimal cointegratuinPrew = pair.Cointegration[pair.Cointegration.Count - 2].Value;
            if(cointegrationLast>0 && cointegratuinPrew > 0)
            {
                pair.ClosePositions();
            }
            if(cointegrationLast>0 && cointegratuinPrew<0)
            {
                pair.ClosePositions();
            }
        }

        public override string GetNameStrategyType()
        {
            return "TradePair"; 
        }

        public override void ShowIndividualSettingsDialog()
        {
 
        }
    }
}
