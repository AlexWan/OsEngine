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
    public class Spoofing : AMarketDepth
    {
        public override string Description
        {
            get
            {
                string eng = "Spoofing is a simple detector of large orders that appear and quickly disappear (a possible fake wall). Series 'SpoofScore' is 1 when a level with volume at least 'Threshold' vanishes between two updates on the nearest 'Levels' levels, otherwise 0. It does not prove spoofing, it only marks rapid removal of a large level.";
                string ru = "Спуфинг (Spoofing) — простой детектор крупных заявок, которые появляются и быстро исчезают (возможная фейковая стена). Серия «SpoofScore» равна 1, когда уровень с объёмом не меньше «Threshold» исчезает между двумя обновлениями на ближайших «Levels» уровнях, иначе 0. Это не доказательство спуфинга, а лишь отметка быстрого снятия крупного уровня.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        public override bool RecalculateByLastDepth
        {
            get { return false; }
        }

        private ParameterMarketDepthDecimal _threshold;

        private ParameterMarketDepthInt _levels;

        private IndicatorDataSeries _score;

        private List<decimal> _lastBidVols = new List<decimal>();

        private List<decimal> _lastAskVols = new List<decimal>();

        private bool _hasPrev;

        public override void OnStateChange(MarketDepthIndicatorState state)
        {
            if (state == MarketDepthIndicatorState.Configure)
            {
                _threshold = CreateParameterDecimal("Threshold", 10000);
                _levels = CreateParameterInt("Levels", 1);
                _score = CreateSeries("SpoofScore", Color.Orange, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(MarketDepth depth)
        {
            int levels = _levels.ValueInt;

            if (levels <= 0)
            {
                levels = 1;
            }

            decimal threshold = _threshold.ValueDecimal;
            bool spoof = false;

            for (int i = 0; i < depth.Bids.Count && i < levels; i++)
            {
                decimal current = (decimal)depth.Bids[i].Bid;

                if (_hasPrev && i < _lastBidVols.Count && _lastBidVols[i] >= threshold && current < threshold)
                {
                    spoof = true;
                }
            }

            for (int i = 0; i < depth.Asks.Count && i < levels; i++)
            {
                decimal current = (decimal)depth.Asks[i].Ask;

                if (_hasPrev && i < _lastAskVols.Count && _lastAskVols[i] >= threshold && current < threshold)
                {
                    spoof = true;
                }
            }

            _lastBidVols.Clear();

            for (int i = 0; i < depth.Bids.Count && i < levels; i++)
            {
                _lastBidVols.Add((decimal)depth.Bids[i].Bid);
            }

            _lastAskVols.Clear();

            for (int i = 0; i < depth.Asks.Count && i < levels; i++)
            {
                _lastAskVols.Add((decimal)depth.Asks[i].Ask);
            }

            _hasPrev = true;

            SetValue(_score, spoof ? 1m : 0m);
        }
    }
}
