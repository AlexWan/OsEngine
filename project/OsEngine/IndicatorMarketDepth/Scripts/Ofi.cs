/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using System.Drawing;

namespace OsEngine.IndicatorMarketDepth.Scripts
{
    public class Ofi : AMarketDepth
    {
        public override string Description
        {
            get
            {
                string eng = "Order Flow Imbalance (OFI) is the net change in liquidity at the best bid and ask between two order book updates (Cont, Kukanov, Stoikov, 2014). It measures the imbalance between supply and demand at the touch. Positive OFI means buying pressure is growing, negative means selling pressure is growing.";
                string ru = "Дисбаланс потока заявок (Order Flow Imbalance, OFI) — чистое изменение ликвидности на лучших биде и аске между двумя обновлениями стакана (Cont, Kukanov, Stoikov, 2014). Показывает баланс спроса и предложения на лучших ценах. Положительный OFI — давление покупателей растёт, отрицательный — продавцов.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        public override bool RecalculateByLastDepth
        {
            get { return false; }
        }

        private MarketDepthDataSeries _series;

        private bool _hasPrev;

        private double _prevBidPrice;

        private double _prevBidSize;

        private double _prevAskPrice;

        private double _prevAskSize;

        public override void OnStateChange(MarketDepthIndicatorState state)
        {
            if (state == MarketDepthIndicatorState.Configure)
            {
                _series = CreateSeries("Ofi", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(MarketDepth depth)
        {
            if (depth.Bids == null || depth.Bids.Count == 0
                || depth.Asks == null || depth.Asks.Count == 0)
            {
                SetValue(_series, 0);
                return;
            }

            double bidPrice = depth.Bids[0].Price;
            double bidSize = depth.Bids[0].Bid;
            double askPrice = depth.Asks[0].Price;
            double askSize = depth.Asks[0].Ask;

            decimal ofi = 0;

            if (_hasPrev)
            {
                decimal eBid = 0;

                if (bidPrice >= _prevBidPrice)
                {
                    eBid += (decimal)bidSize;
                }

                if (bidPrice <= _prevBidPrice)
                {
                    eBid -= (decimal)_prevBidSize;
                }

                decimal eAsk = 0;

                if (askPrice <= _prevAskPrice)
                {
                    eAsk -= (decimal)askSize;
                }

                if (askPrice >= _prevAskPrice)
                {
                    eAsk += (decimal)_prevAskSize;
                }

                ofi = eBid + eAsk;
            }

            _prevBidPrice = bidPrice;
            _prevBidSize = bidSize;
            _prevAskPrice = askPrice;
            _prevAskSize = askSize;
            _hasPrev = true;

            SetValue(_series, ofi);
        }
    }
}
