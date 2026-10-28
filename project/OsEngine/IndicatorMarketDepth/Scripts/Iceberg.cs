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
    /// <summary>
    /// Айсберг / Поглощение (Iceberg / Absorption): сделки идут по лучшей цене,
    /// но видимый объём уровня почти не уменьшается — признак скрытой ликвидности.
    /// </summary>
    public class Iceberg : AMarketDepth
    {
        public override string Description
        {
            get
            {
                string eng = "Iceberg / Absorption detects hidden liquidity. Trades hit the best price but the visible volume of the level barely decreases, meaning a hidden order replenishes it. Calculation. Over a 'Window' of seconds, for the ask side 'eatenAsk' is the total decrease of the best ask volume and 'tradedAsk' is the total volume of aggressive buy trades (volume at least 'TradeThreshold'); for the bid side mirror. Absorption = 1 - eaten / traded, clamped to 0..1. A value near 1 means trades were absorbed without reducing the visible level (iceberg), near 0 means the visible volume really melted. 'MinTrades' is the minimum number of aggressive trades in the window to consider the value valid.";
                string ru = "Айсберг / Поглощение (Iceberg / Absorption) ловит скрытую ликвидность. Сделки идут по лучшей цене, но видимый объём уровня почти не уменьшается — значит, скрытая заявка его пополняет. Расчёт. За окно «Window» секунд на стороне аска «eatenAsk» — суммарное уменьшение объёма лучшего аска, а «tradedAsk» — суммарный объём агрессивных покупок (объём не меньше «TradeThreshold»); на стороне бида зеркально. Absorption = 1 − eaten / traded, ограничено от 0 до 1. Значение около 1 — сделки поглощены без уменьшения видимого уровня (айсберг), около 0 — видимый объём реально тает. «MinTrades» — минимальное число агрессивных сделок в окне, чтобы считать значение достоверным.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        public override MarketDepthIndicatorDataSource DataSource
        {
            get { return MarketDepthIndicatorDataSource.Both; }
        }

        private ParameterMarketDepthInt _window;

        private ParameterMarketDepthDecimal _tradeThreshold;

        private ParameterMarketDepthInt _minTrades;

        private MarketDepthDataSeries _absorptionAsk;

        private MarketDepthDataSeries _absorptionBid;

        private readonly List<Bucket> _buffer = new List<Bucket>();

        private Bucket _current = new Bucket();

        private bool _hasStarted;

        private bool _hasPrevDepth;

        private decimal _lastAskPrice;

        private decimal _lastAskVol;

        private decimal _lastBidPrice;

        private decimal _lastBidVol;

        public override void OnStateChange(MarketDepthIndicatorState state)
        {
            if (state == MarketDepthIndicatorState.Configure)
            {
                _window = CreateParameterInt("Window", 10);
                _tradeThreshold = CreateParameterDecimal("TradeThreshold", 0);
                _minTrades = CreateParameterInt("MinTrades", 3);

                _absorptionAsk = CreateSeries("AbsorptionAsk", Color.OrangeRed, IndicatorChartPaintType.Line, true);
                _absorptionBid = CreateSeries("AbsorptionBid", Color.SeaGreen, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(MarketDepth depth)
        {
            if (depth.Asks == null || depth.Asks.Count == 0
                || depth.Bids == null || depth.Bids.Count == 0)
            {
                return;
            }

            decimal askPrice = (decimal)depth.Asks[0].Price;
            decimal askVol = (decimal)depth.Asks[0].Ask;
            decimal bidPrice = (decimal)depth.Bids[0].Price;
            decimal bidVol = (decimal)depth.Bids[0].Bid;

            if (_hasPrevDepth)
            {
                if (askPrice == _lastAskPrice && askVol < _lastAskVol)
                {
                    _current.EatenAsk += _lastAskVol - askVol;
                }

                if (bidPrice == _lastBidPrice && bidVol < _lastBidVol)
                {
                    _current.EatenBid += _lastBidVol - bidVol;
                }
            }

            _lastAskPrice = askPrice;
            _lastAskVol = askVol;
            _lastBidPrice = bidPrice;
            _lastBidVol = bidVol;
            _hasPrevDepth = true;

            if (_hasStarted)
            {
                _buffer.Add(_current);

                int window = _window.ValueInt;

                if (window < 1)
                {
                    window = 10;
                }

                while (_buffer.Count > window)
                {
                    _buffer.RemoveAt(0);
                }
            }

            _current = new Bucket();
            _hasStarted = true;

            decimal tradedAsk = 0;
            decimal tradedBid = 0;
            decimal eatenAsk = 0;
            decimal eatenBid = 0;
            int buyCount = 0;
            int sellCount = 0;

            for (int i = 0; i < _buffer.Count; i++)
            {
                Bucket bucket = _buffer[i];

                tradedAsk += bucket.TradedAsk;
                tradedBid += bucket.TradedBid;
                eatenAsk += bucket.EatenAsk;
                eatenBid += bucket.EatenBid;
                buyCount += bucket.BuyCount;
                sellCount += bucket.SellCount;
            }

            int minTrades = _minTrades.ValueInt;

            if (minTrades < 0)
            {
                minTrades = 0;
            }

            decimal absorptionAsk = 0;
            decimal absorptionBid = 0;

            if (buyCount >= minTrades && tradedAsk > 0)
            {
                absorptionAsk = Clamp01(1m - eatenAsk / tradedAsk);
            }

            if (sellCount >= minTrades && tradedBid > 0)
            {
                absorptionBid = Clamp01(1m - eatenBid / tradedBid);
            }

            SetValue(_absorptionAsk, absorptionAsk);
            SetValue(_absorptionBid, absorptionBid);
        }

        public override void OnProcessTrade(Trade trade)
        {
            decimal threshold = _tradeThreshold.ValueDecimal;

            if (trade.Volume < threshold)
            {
                return;
            }

            if (trade.Side == Side.Buy)
            {
                _current.TradedAsk += trade.Volume;
                _current.BuyCount++;
            }
            else if (trade.Side == Side.Sell)
            {
                _current.TradedBid += trade.Volume;
                _current.SellCount++;
            }
        }

        private static decimal Clamp01(decimal value)
        {
            if (value < 0)
            {
                return 0;
            }

            if (value > 1)
            {
                return 1;
            }

            return value;
        }

        private class Bucket
        {
            public decimal TradedAsk;

            public decimal TradedBid;

            public decimal EatenAsk;

            public decimal EatenBid;

            public int BuyCount;

            public int SellCount;
        }
    }
}
