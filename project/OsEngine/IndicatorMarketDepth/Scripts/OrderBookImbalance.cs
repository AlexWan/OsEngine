/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using System.Collections.Generic;
using System.Drawing;

namespace OsEngine.IndicatorMarketDepth.Scripts
{
    /// <summary>
    /// Дисбаланс стакана: перевес покупателей/продавцов на ближайших уровнях.
    /// </summary>
    public class OrderBookImbalance : AMarketDepth
    {
        public override string Description
        {
            get
            {
                string eng = "Order Book Imbalance (OBI) is the advantage of buyers or sellers on the nearest levels. Calculation. (sum of bid volume minus sum of ask volume) divided by (sum of bid plus ask) on the first 'Levels' levels. Value is from -1 to +1. +1 means only bids (strong buyer pressure), -1 means only asks (strong seller pressure), 0 means balance. Positive values usually mean upward pressure, negative mean downward. Parameter 'Levels' is how many nearest levels to use. Parameter 'Mode' selects Simple or Weighted. In Weighted mode farther levels fade exponentially (each level has half the weight of the previous one).";
                string ru = "Дисбаланс стакана (Order Book Imbalance, OBI) — перевес покупателей или продавцов на ближайших уровнях. Расчёт. (сумма объёма Bid − сумма объёма Ask) делить на (сумма Bid + Ask) на первых «Levels» уровнях. Значение от −1 до +1. +1 — только заявки на покупку (сильное давление покупателей), −1 — только на продажу, 0 — равенство. Положительное значение обычно означает давление вверх, отрицательное — вниз. Параметр «Levels» — сколько ближайших уровней учитывать. Параметр «Mode» выбирает Simple или Weighted. В режиме Weighted дальние уровни затухают экспоненциально (вес каждого следующего уровня вдвое меньше предыдущего).";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        private ParameterMarketDepthInt _levels;

        private ParameterMarketDepthString _mode;

        private IndicatorDataSeries _series;

        public override void OnStateChange(MarketDepthIndicatorState state)
        {
            if (state == MarketDepthIndicatorState.Configure)
            {
                _mode = CreateParameterStringCollection("Mode", "Simple", new List<string> { "Simple", "Weighted" });
                _levels = CreateParameterInt("Levels", 5);
                _series = CreateSeries("Imbalance", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(MarketDepth depth)
        {
            int levels = _levels.ValueInt;

            if (levels <= 0)
            {
                levels = 5;
            }

            if (_mode.ValueString == "Weighted")
            {
                decimal weightedBid = 0;
                decimal weightedAsk = 0;
                decimal weight = 1m;

                for (int i = 0; i < depth.Bids.Count && i < levels; i++)
                {
                    weightedBid += (decimal)depth.Bids[i].Bid * weight;
                    weight *= 0.5m;
                }

                weight = 1m;

                for (int i = 0; i < depth.Asks.Count && i < levels; i++)
                {
                    weightedAsk += (decimal)depth.Asks[i].Ask * weight;
                    weight *= 0.5m;
                }

                decimal weightedTotal = weightedBid + weightedAsk;
                decimal weightedImbalance = 0;

                if (weightedTotal != 0)
                {
                    weightedImbalance = (weightedBid - weightedAsk) / weightedTotal;
                }

                SetValue(_series, weightedImbalance);
                return;
            }

            decimal sumBid = 0;
            decimal sumAsk = 0;

            for (int i = 0; i < depth.Bids.Count && i < levels; i++)
            {
                sumBid += (decimal)depth.Bids[i].Bid;
            }

            for (int i = 0; i < depth.Asks.Count && i < levels; i++)
            {
                sumAsk += (decimal)depth.Asks[i].Ask;
            }

            decimal total = sumBid + sumAsk;

            decimal imbalance = 0;

            if (total != 0)
            {
                imbalance = (sumBid - sumAsk) / total;
            }

            SetValue(_series, imbalance);
        }
    }
}
