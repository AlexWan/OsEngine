/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;

namespace OsEngine.Robots.TechSamples
{
    // example showing blocking of indicators for calculating
    // useful when optimizing robots that do not need all created indicators 

    [Bot("BlockIndicatorsSample")]
    public class BlockIndicatorsSample : BotPanel
    {
        private BotTabSimple _tab;

        private Aindicator _bollinger;
        private Aindicator _sma;
        private Aindicator _atr;

        private StrategyParameterBool _bollingerIsOn;
        private StrategyParameterBool _smaIsOn;
        private StrategyParameterBool _atrIsOn;

        public BlockIndicatorsSample(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _bollinger = IndicatorsFactory.CreateIndicatorByName("Bollinger", name + "Bollinger", false);
            _bollinger = (Aindicator)_tab.CreateCandleIndicator(_bollinger, "Prime");

            _sma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "sma", false);
            _sma = (Aindicator)_tab.CreateCandleIndicator(_sma, "Prime");

            _atr = IndicatorsFactory.CreateIndicatorByName("ATR", name + "atr", false);
            _atr = (Aindicator)_tab.CreateCandleIndicator(_atr, "AtrArea");

            _bollingerIsOn = CreateParameter("Bollinger is ON", true);

            _smaIsOn = CreateParameter("Sma is ON", true);

            _atrIsOn = CreateParameter("Atr is ON", true);

            StopOrActivateIndicators();

            ParametrsChangeByUser += BlockIndicatorsSample_ParametrsChangeByUser;

            Description = "Example showing the blocking of indicators for calculation";
        }

        private void BlockIndicatorsSample_ParametrsChangeByUser()
        {
            StopOrActivateIndicators();
        }

        private void StopOrActivateIndicators()
        {
            if(_bollingerIsOn.ValueBool 
                != _bollinger.IsOn)
            {
                _bollinger.IsOn = _bollingerIsOn.ValueBool;
                _bollinger.Reload();
            }

            if (_smaIsOn.ValueBool
               != _sma.IsOn)
            {
                _sma.IsOn = _smaIsOn.ValueBool;
                _sma.Reload();
            }

            if (_atrIsOn.ValueBool
                != _atr.IsOn)
            {
                _atr.IsOn = _atrIsOn.ValueBool;
                _atr.Reload();
            }
        }

        public override string GetNameStrategyType()
        {
            return "BlockIndicatorsSample";
        }

        public override void ShowIndividualSettingsDialog()
        {
          
        }
    }
}