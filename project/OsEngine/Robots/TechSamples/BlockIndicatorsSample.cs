using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;

namespace OsEngine.Robots.TechSamples
{
    // пример показывающий блокировку индикаторов для расчёта
    // пригодиться при оптимизации роботов, в которых нужны не все индикаторы

    [Bot("BlockIndicatorsSample")]
    public class BlockIndicatorsSample : BotPanel
    {
        public BlockIndicatorsSample(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _bollinger = IndicatorsFactory.CreateIndicatorByName("Bollinger", name + "Bollinger", false);
            _bollinger = (Aindicator)_tab.CreateCandleIndicator(_bollinger, "Prime");

            _sma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "sma", false);
            _sma = (Aindicator)_tab.CreateCandleIndicator(_sma, "Prime");

            _priceChannel = IndicatorsFactory.CreateIndicatorByName("PriceChannel", name + "Pc", false);
            _priceChannel = (Aindicator)_tab.CreateCandleIndicator(_priceChannel, "Prime");

            _bollingerIsOn = CreateParameter("вкл расчёт Bollinger", true);

            _smaIsOn = CreateParameter("вкл расчёт Sma", true);

            _priceChannelIsOn = CreateParameter("вкл расчёт PriceChannel", true);

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

            if (_priceChannelIsOn.ValueBool
                != _priceChannel.IsOn)
            {
                _priceChannel.IsOn = _priceChannelIsOn.ValueBool;
                _priceChannel.Reload();
            }
        }

        private BotTabSimple _tab;

        private Aindicator _bollinger;
        StrategyParameterBool _bollingerIsOn;

        private Aindicator _sma;
        StrategyParameterBool _smaIsOn;

        private Aindicator _priceChannel;
        StrategyParameterBool _priceChannelIsOn;

        public override string GetNameStrategyType()
        {
            return "BlockIndicatorsSample";
        }

        public override void ShowIndividualSettingsDialog()
        {
          
        }
    }
}
