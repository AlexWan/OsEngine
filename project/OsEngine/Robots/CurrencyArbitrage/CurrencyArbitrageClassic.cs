/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;

/* Description
Arbitrage robot for OsEngine.

A robot for classic currency arbitrage.
*/

namespace OsEngine.Robots.CurrencyArbitrage
{
    [Bot("CurrencyArbitrageClassic")] // We create an attribute so that we don't write anything to the BotFactory
    public class CurrencyArbitrageClassic : BotPanel
    {
        private BotTabPolygon _tabPolygon;

        // Basic setting
        private StrategyParameterString _regime;

        // GetVolume setting
        private StrategyParameterDecimal _volume;

        // Order setting
        private StrategyParameterString _orderType;

        // Commission settings
        private StrategyParameterString _commissionType;
        private StrategyParameterDecimal _commissionValue;
        private StrategyParameterBool _substractCommission;

        // Delay settings
        private StrategyParameterString _delayType;
        private StrategyParameterInt _delayMls;

        public CurrencyArbitrageClassic(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Polygon);
            _tabPolygon = this.TabsPolygon[0];
            _tabPolygon.ProfitGreaterThanSignalValueEvent += _tabPolygon_ProfitGreaterThanSignalValueEvent;

            // Basic setting
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });

            // GetVolume setting
            _volume = CreateParameter("Volume", 0.1m, 0.1m, 50, 0.1m);

            // Order setting
            _orderType = CreateParameter("Orders type", OrderPriceType.Limit.ToString(), new[] 
            { OrderPriceType.Limit.ToString(), OrderPriceType.Market.ToString()});

            // Commission settings
            _commissionType = CreateParameter("Commission type", CommissionPolygonType.Percent.ToString(), new[] 
            { CommissionPolygonType.None.ToString(), CommissionPolygonType.Percent.ToString() });
            _commissionValue = CreateParameter("Commission value %", 0.1m, 0.1m, 50, 0.1m);
            _substractCommission = CreateParameter("SubstractCommission", true);

            // Delay settings
            _delayType = CreateParameter("Delay Type", DelayPolygonType.ByExecution.ToString() , new[] 
            { DelayPolygonType.ByExecution.ToString(), DelayPolygonType.InMLS.ToString(), DelayPolygonType.Instantly.ToString() });
            _delayMls = CreateParameter("Delay MLS", 200, 200, 2000, 100);

            Description = OsLocalization.Description.DescriptionLabel26;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "CurrencyArbitrageClassic";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Logic
        private void _tabPolygon_ProfitGreaterThanSignalValueEvent(decimal profit, PolygonToTrade sequence)
        {
            if (sequence.HavePositions == true)
            {
                return;
            }

            if (_regime.ValueString == "Off")
            {
                return;
            }

            sequence.QtyStart = _volume.ValueDecimal;
            sequence.DelayMls = _delayMls.ValueInt;
            sequence.CommissionValue = _commissionValue.ValueDecimal;
            sequence.CommissionIsSubstract = _substractCommission.ValueBool;

            OrderPriceType orderType;

            if (Enum.TryParse(_orderType.ValueString, out orderType))
            {
                sequence.OrderPriceType = orderType;
            }

            CommissionPolygonType cType;

            if (Enum.TryParse(_commissionType.ValueString, out cType))
            {
                sequence.CommissionType= cType;
            }

            DelayPolygonType delayType;

            if (Enum.TryParse(_delayType.ValueString, out delayType))
            {
                sequence.DelayType = delayType;
            }

            sequence.TradeLogic();
        }
    }
}