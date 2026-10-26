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
    public class MicroPrice : AMarketDepth
    {
        public override string Description
        {
            get
            {
                string eng = "Micro Price is a volume-weighted estimate of the fair price on the best levels. It is more accurate than the simple midpoint. Calculation. (best bid price * best ask volume + best ask price * best bid volume) divided by (best bid volume + best ask volume). When the price is above the micro price, sellers dominate, when below, buyers dominate.";
                string ru = "Микро-цена (Micro Price) — взвешенная по объёму оценка справедливой цены на лучших уровнях. Точнее, чем простая середина спреда. Расчёт. (цена лучшего Bid × объём лучшего Ask + цена лучшего Ask × объём лучшего Bid) делить на (объём Bid + объём Ask). Когда цена выше микро-цены, доминируют продавцы, когда ниже — покупатели.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        private MarketDepthDataSeries _series;

        public override void OnStateChange(MarketDepthIndicatorState state)
        {
            if (state == MarketDepthIndicatorState.Configure)
            {
                _series = CreateSeries("MicroPrice", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
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
            double askPrice = depth.Asks[0].Price;
            double bidVolume = depth.Bids[0].Bid;
            double askVolume = depth.Asks[0].Ask;

            if (bidPrice <= 0 || askPrice <= 0 || bidVolume + askVolume <= 0)
            {
                SetValue(_series, 0);
                return;
            }

            decimal microPrice = (decimal)((bidPrice * askVolume + askPrice * bidVolume) / (bidVolume + askVolume));

            SetValue(_series, microPrice);
        }
    }
}
