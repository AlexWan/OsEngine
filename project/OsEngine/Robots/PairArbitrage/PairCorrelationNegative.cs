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

trading robot for osengine

Bot - trading pairs in the trend
If the correlation is below -0.8 and we are on some side of the cointegration - enter counting on a further spread

Exit - when correlation rises above 0.8
*/

namespace OsEngine.Robots.PairArbitrage
{
    [Bot("PairCorrelationNegative")] //We create an attribute so that we don't write anything in the Boot factory
    public class PairCorrelationNegative : BotPanel
    {
        BotTabPair _pairTrader;

        // Basic settings
        private StrategyParameterInt _maxPositionsCount;
        private StrategyParameterDecimal _maxCorrelationToEntry;
        private StrategyParameterDecimal _minCorrelationToExit;
        private StrategyParameterString _regime;

        public PairCorrelationNegative(string name, StartProgram startProgram)
          : base(name, startProgram)
        {
            TabCreate(BotTabType.Pair);
            _pairTrader = this.TabsPair[0];

            // Subscribe to the cointegration change event
            _pairTrader.CorrelationChangeEvent += _pairTrader_CorrelationChangeEvent;

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });
            _maxPositionsCount = CreateParameter("Max poses count", 5, 5, 5, 5);
            _maxCorrelationToEntry = CreateParameter("Max Correlation To Entry", -0.8m, -0.1m, 5,0.1m);
            _minCorrelationToExit = CreateParameter("Min Correlation To Exit", 0.8m, 0.1m, 1, 0.1m);

            Description = OsLocalization.Description.DescriptionLabel70;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "PairCorrelationNegative";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Logic
        private void _pairTrader_CorrelationChangeEvent(System.Collections.Generic.List<PairIndicatorValue> correlation, PairToTrade pair)
        {
            if (_regime.ValueString == "Off")
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

        // Position close logic
        private void ClosePositionLogic(PairToTrade pair)
        {
            if (pair.CorrelationLast > _minCorrelationToExit.ValueDecimal)
            {
                pair.ClosePositions();
            }
        }

        // Position open logic
        private void OpenPositionLogic(PairToTrade pair)
        {
            if (pair.CorrelationLast > _maxCorrelationToEntry.ValueDecimal)
            {
                return;
            }

            if (_pairTrader.PairsWithPositionsCount >= _maxPositionsCount.ValueInt)
            {
                return;
            }

            if (pair.SideCointegrationValue == CointegrationLineSide.Up)
            {
                pair.BuySec1SellSec2();
            }
            else if (pair.SideCointegrationValue == CointegrationLineSide.Down)
            {
                pair.SellSec1BuySec2();
            }
        }
    }
}