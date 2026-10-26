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
    public class TradeSizeAnalytics : AMarketDepth
    {
        public override string Description
        {
            get
            {
                string eng = "Trade Size Analytics describes the size of trades. Series 'AvgTradeSize' is the average trade volume over the last 'Period' seconds. Series 'LargeTradeShare' is the share of volume coming from large trades (volume at least 'LargeThreshold') over the last 'Period' seconds, from 0 to 1. A high large trade share means big players are active.";
                string ru = "Аналитика размера сделок (Trade Size Analytics) описывает размер сделок. Серия «AvgTradeSize» — средний объём сделки за последние «Period» секунд. Серия «LargeTradeShare» — доля объёма от крупных сделок (объём не меньше «LargeThreshold») за последние «Period» секунд, от 0 до 1. Высокая доля крупных сделок означает активность крупных игроков.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        public override MarketDepthIndicatorDataSource DataSource
        {
            get { return MarketDepthIndicatorDataSource.Trades; }
        }

        private ParameterMarketDepthInt _period;

        private ParameterMarketDepthDecimal _largeThreshold;

        private IndicatorDataSeries _avgTradeSize;

        private IndicatorDataSeries _largeTradeShare;

        private int _lastCount = -1;

        private decimal _secVolume;

        private decimal _secCount;

        private decimal _secLargeVolume;

        private List<decimal> _volumeHistory = new List<decimal>();

        private List<decimal> _countHistory = new List<decimal>();

        private List<decimal> _largeVolumeHistory = new List<decimal>();

        private bool _hasPrev;

        public override void OnStateChange(MarketDepthIndicatorState state)
        {
            if (state == MarketDepthIndicatorState.Configure)
            {
                _period = CreateParameterInt("Period", 10);
                _largeThreshold = CreateParameterDecimal("LargeThreshold", 1000);
                _avgTradeSize = CreateSeries("AvgTradeSize", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
                _largeTradeShare = CreateSeries("LargeTradeShare", Color.Orange, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(MarketDepth depth)
        {
        }

        public override void OnProcessTrade(Trade trade)
        {
            bool newSecond = _avgTradeSize.Values.Count != _lastCount;
            _lastCount = _avgTradeSize.Values.Count;

            if (newSecond)
            {
                if (_hasPrev)
                {
                    int period = _period.ValueInt;

                    if (period < 1)
                    {
                        period = 10;
                    }

                    _volumeHistory.Add(_secVolume);
                    _countHistory.Add(_secCount);
                    _largeVolumeHistory.Add(_secLargeVolume);

                    while (_volumeHistory.Count > period)
                    {
                        _volumeHistory.RemoveAt(0);
                        _countHistory.RemoveAt(0);
                        _largeVolumeHistory.RemoveAt(0);
                    }
                }

                _secVolume = 0;
                _secCount = 0;
                _secLargeVolume = 0;
                _hasPrev = true;
            }

            _secVolume += trade.Volume;
            _secCount += 1;

            decimal threshold = _largeThreshold.ValueDecimal;

            if (trade.Volume >= threshold)
            {
                _secLargeVolume += trade.Volume;
            }

            decimal totalVolume = Sum(_volumeHistory);
            decimal totalCount = Sum(_countHistory);
            decimal totalLarge = Sum(_largeVolumeHistory);

            decimal avgTradeSize = totalCount == 0 ? 0 : totalVolume / totalCount;
            decimal largeShare = totalVolume == 0 ? 0 : totalLarge / totalVolume;

            SetValue(_avgTradeSize, avgTradeSize);
            SetValue(_largeTradeShare, largeShare);
        }

        private static decimal Sum(List<decimal> values)
        {
            decimal sum = 0;

            for (int i = 0; i < values.Count; i++)
            {
                sum += values[i];
            }

            return sum;
        }
    }
}
