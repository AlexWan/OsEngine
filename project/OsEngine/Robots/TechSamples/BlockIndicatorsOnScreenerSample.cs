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
    [Bot("BlockIndicatorsOnScreenerSample")]
    public class BlockIndicatorsOnScreenerSample : BotPanel
    {
        private BotTabScreener _screenerSource;

        private StrategyParameterBool _bollingerIsOn;
        private StrategyParameterBool _smaIsOn;
        private StrategyParameterBool _atrIsOn;

        public BlockIndicatorsOnScreenerSample(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);
            _screenerSource = TabsScreener[0];
            _screenerSource.CandleFinishedEvent += _screenerSource_CandleFinishedEvent;

            _screenerSource.CreateCandleIndicator(1, "Bollinger", null, "Prime");
            _screenerSource.CreateCandleIndicator(2, "Sma", null, "Prime");
            _screenerSource.CreateCandleIndicator(3, "ATR", null, "Second");

            _bollingerIsOn = CreateParameter("Bollinger is ON", true);

            _smaIsOn = CreateParameter("Sma is ON", true);

            _atrIsOn = CreateParameter("Atr is ON", true);

            Description = "Example showing the blocking of indicators for calculation on BotTabScreener source";
        }

        private void _screenerSource_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
        {
            Aindicator bollinger = (Aindicator)tab.Indicators[0];
            Aindicator sma = (Aindicator)tab.Indicators[1];
            Aindicator atr = (Aindicator)tab.Indicators[2];

            if (_bollingerIsOn.ValueBool
               != bollinger.IsOn)
           {
               bollinger.IsOn = _bollingerIsOn.ValueBool;
               bollinger.Reload();
           }

           if (_smaIsOn.ValueBool
              != sma.IsOn)
           {
               sma.IsOn = _smaIsOn.ValueBool;
               sma.Reload();
           }

           if (_atrIsOn.ValueBool
               != atr.IsOn)
           {
               atr.IsOn = _atrIsOn.ValueBool;
               atr.Reload();
           }
        }

        public override string GetNameStrategyType()
        {
            return "BlockIndicatorsOnScreenerSample";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }
    }
}