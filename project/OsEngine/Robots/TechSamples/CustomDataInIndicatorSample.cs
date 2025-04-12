/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using System.Collections.Generic;

namespace OsEngine.Robots.TechSamples
{
    [Bot("CustomDataInIndicatorSample")]
    public class CustomDataInIndicatorSample : BotPanel
    {
        private BotTabSimple _tab;

        private Aindicator _indicatorEmpty;

        public CustomDataInIndicatorSample(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            _indicatorEmpty = IndicatorsFactory.CreateIndicatorByName("EmptyIndicator", name + "EmptyIndicator", false);
            _indicatorEmpty = (Aindicator)_tab.CreateCandleIndicator(_indicatorEmpty, "SecondArea");

            Description = "An example of drawing a series of indicator data calculated in the robot.";
        }

        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {

            decimal dataPoint = candles[candles.Count - 1].Close / 2;

            _indicatorEmpty.DataSeries[0].Values[_indicatorEmpty.DataSeries[0].Values.Count-1] = dataPoint;
            _indicatorEmpty.RePaint();
        }

        public override string GetNameStrategyType()
        {
            return "CustomDataInIndicatorSample";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }
    }
}