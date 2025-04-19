/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;

namespace OsEngine.Robots.CurrencyArbitrage
{
    [Bot("CurrencyArbitrageClassic")]
    public class CurrencyArbitrageClassic : BotPanel
    {
        public CurrencyArbitrageClassic(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Polygon);
            _tabPolygon = this.TabsPolygon[0];
            _tabPolygon.ProfitGreaterThanSignalValueEvent += _tabPolygon_ProfitGreaterThanSignalValueEvent;

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });
            Volume = CreateParameter("Volume", 0.1m, 0.1m, 50, 0.1m);

            OrderType = CreateParameter("Orders type", OrderPriceType.Limit.ToString(), new[] 
            { OrderPriceType.Limit.ToString(), OrderPriceType.Market.ToString()});

            CommissionType = CreateParameter("Commission type", CommissionPolygonType.Percent.ToString(), new[] 
            { CommissionPolygonType.None.ToString(), CommissionPolygonType.Percent.ToString() });

            CommissionValue = CreateParameter("Commission value %", 0.1m, 0.1m, 50, 0.1m);

            SubstractCommission = CreateParameter("SubstractCommission", true);

            DelayType = CreateParameter("Delay Type", DelayPolygonType.ByExecution.ToString() , new[] 
            { DelayPolygonType.ByExecution.ToString(), DelayPolygonType.InMLS.ToString(), DelayPolygonType.Instantly.ToString() });

            DelayMls = CreateParameter("Delay MLS", 200, 200, 2000, 100);

            Description = "A robot for classic currency arbitrage";
        }

        public override string GetNameStrategyType()
        {
            return "CurrencyArbitrageClassic";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        private BotTabPolygon _tabPolygon;

        public StrategyParameterString Regime;

        public StrategyParameterDecimal Volume;

        public StrategyParameterString OrderType;

        public StrategyParameterString CommissionType;

        public StrategyParameterDecimal CommissionValue;

        public StrategyParameterBool SubstractCommission;

        public StrategyParameterString DelayType;

        public StrategyParameterInt DelayMls;

        private void _tabPolygon_ProfitGreaterThanSignalValueEvent(decimal profit, PolygonToTrade sequence)
        {
            if (sequence.HavePositions == true)
            {
                return;
            }

            if (Regime.ValueString == "Off")
            {
                return;
            }

            sequence.QtyStart = Volume.ValueDecimal;
            sequence.DelayMls = DelayMls.ValueInt;
            sequence.CommissionValue = CommissionValue.ValueDecimal;
            sequence.CommissionIsSubstract = SubstractCommission.ValueBool;

            OrderPriceType orderType;

            if (Enum.TryParse(OrderType.ValueString, out orderType))
            {
                sequence.OrderPriceType = orderType;
            }

            CommissionPolygonType cType;

            if (Enum.TryParse(CommissionType.ValueString, out cType))
            {
                sequence.CommissionType= cType;
            }

            DelayPolygonType delayType;

            if (Enum.TryParse(DelayType.ValueString, out delayType))
            {
                sequence.DelayType = delayType;
            }

            sequence.TradeLogic();
        }
    }
}