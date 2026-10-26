/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace OsEngine.IndicatorMarketDepth.Scripts
{
    public class Delta : AMarketDepth
    {
        public override string Description
        {
            get
            {
                string eng = "Delta / CVD (Cumulative Volume Delta) is market buys minus market sells by trades. Buy trades are positive, sell trades are negative. Series 'Delta' is the value for the current second, series 'Cvd' is the running sum since the start of the trading day. Positive Delta/CVD means buyers are more aggressive, negative means sellers. Parameter 'Aggregation'. Volume sums trade volume, Trades counts trades. CVD is usually compared with price. Price rising while CVD falls may indicate a weak move.";
                string ru = "Дельта / CVD (Cumulative Volume Delta) — рыночные покупки минус рыночные продажи по сделкам. Сделки на покупку считаются с плюсом, на продажу — с минусом. Серия «Delta» — значение за текущую секунду, серия «Cvd» — накопленная сумма с начала торгового дня. Положительная Delta/CVD — покупатели агрессивнее, отрицательная — продавцы. Параметр «Aggregation». Volume суммирует объём сделок, Trades считает количество сделок. CVD обычно сравнивают с ценой. Рост цены при падающем CVD может указывать на слабость движения.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        public override MarketDepthIndicatorDataSource DataSource
        {
            get { return MarketDepthIndicatorDataSource.Trades; }
        }

        private ParameterMarketDepthString _aggregation;

        private IndicatorDataSeries _delta;

        private IndicatorDataSeries _cvd;

        private int _lastCount = -1;

        private decimal _perSecondSigned;

        private decimal _cumulative;

        private DateTime _cumDate = DateTime.MinValue;

        public override void OnStateChange(MarketDepthIndicatorState state)
        {
            if (state == MarketDepthIndicatorState.Configure)
            {
                _aggregation = CreateParameterStringCollection("Aggregation", "Volume", new List<string> { "Volume", "Trades" });
                _delta = CreateSeries("Delta", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
                _cvd = CreateSeries("Cvd", Color.DarkOrange, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(MarketDepth depth)
        {
        }

        public override void OnProcessTrade(Trade trade)
        {
            bool newSecond = _delta.Values.Count != _lastCount;
            _lastCount = _delta.Values.Count;

            if (newSecond)
            {
                _perSecondSigned = 0;
            }

            decimal coefficient = _aggregation.ValueString == "Volume" ? trade.Volume : 1m;
            decimal signed = (trade.Side == Side.Buy ? 1m : -1m) * coefficient;

            _perSecondSigned += signed;

            if (_cumDate == DateTime.MinValue || _cumDate != trade.Time.Date)
            {
                _cumDate = trade.Time.Date;
                _cumulative = 0;
            }

            _cumulative += signed;

            SetValue(_delta, _perSecondSigned);
            SetValue(_cvd, _cumulative);
        }
    }
}
