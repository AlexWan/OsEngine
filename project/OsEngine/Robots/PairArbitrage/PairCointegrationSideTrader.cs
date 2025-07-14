/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/*
Description

Trading robot for osengine

The robot trades on a chart of deviations of one instrument from another, 
calculated through their difference with the multiplier. 
Two lines, calculated from the standard deviation multiplied by the multiplier, 
are superimposed on this graph (Cointegration). Above and below zero.

When the current deviation is higher than the upper line on the deviation chart - 
we enter a position expecting the instruments to converge. We close the previous position.
When the current deviation is below the bottom line on the Deviation chart - 
enter the position, counting on the instruments convergence. Closing the previous position.
*/

namespace OsEngine.Robots.PairArbitrage
{
    [Bot("PairCointegrationSideTrader")] //We create an attribute so that we don't write anything in the Boot factory
    public class PairCointegrationSideTrader : BotPanel
    {
        BotTabPair _pairTrader;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterInt _maxPositionsCount;

        public PairCointegrationSideTrader(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Pair);
            _pairTrader = this.TabsPair[0];

            // Subscribe to the cointegration position side change event
            _pairTrader.CointegrationPositionSideChangeEvent += _pairTrader_CointegrationPositionSideChangeEvent;

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });
            _maxPositionsCount = CreateParameter("max poses count", 5, 5, 5, 5);

            Description = OsLocalization.Description.DescriptionLabel69;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "PairCointegrationSideTrader";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Logic
        private void _pairTrader_CointegrationPositionSideChangeEvent(CointegrationLineSide side, PairToTrade pair)
        {
            if(_regime.ValueString == "Off")
            {
                return;
            }

            if(pair.HavePositions)
            {
                ClosePositionLogic(pair);
                OpenPositionLogic(pair);
            }
            else
            {
                OpenPositionLogic(pair);
            }
        }

        // Logic close position 
        private void ClosePositionLogic(PairToTrade pair)
        {
            if (pair.SideCointegrationValue == CointegrationLineSide.Up 
                && pair.LastEntryCointegrationSide == CointegrationLineSide.Down)
            {
                pair.ClosePositions();
            }
            else if (pair.SideCointegrationValue == CointegrationLineSide.Down
                && pair.LastEntryCointegrationSide == CointegrationLineSide.Up)
            {
                pair.ClosePositions();
            }
        }

        // Position opening logic
        private void OpenPositionLogic(PairToTrade pair)
        {
            if(_pairTrader.PairsWithPositionsCount >= _maxPositionsCount.ValueInt)
            {
                return;
            }

            if(pair.SideCointegrationValue == pair.LastEntryCointegrationSide)
            {
                return;
            }

            if(pair.SideCointegrationValue == CointegrationLineSide.Up)
            {
                pair.SellSec1BuySec2();
            }
            else if(pair.SideCointegrationValue == CointegrationLineSide.Down)
            {
                pair.BuySec1SellSec2();
            }
        }
    }
}