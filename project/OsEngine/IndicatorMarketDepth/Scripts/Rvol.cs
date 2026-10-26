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
    public class Rvol : AMarketDepth
    {
        public override string Description
        {
            get
            {
                string eng = "RVOL (Relative Volume) is how much the current trade volume is above or below the norm. Calculation. Current second trade volume divided by the average volume of the previous 'Period' seconds. RVOL = 1 means normal activity, above 1 means higher-than-normal activity (a burst), below 1 means a lull. Parameter 'Period' is how many previous seconds are used as the base. Useful for spotting moments of abnormal activity.";
                string ru = "Относительный объём (RVOL, Relative Volume) — насколько текущий объём сделок выше или ниже обычного. Расчёт. Объём сделок текущей секунды делится на средний объём за предыдущие «Period» секунд. RVOL = 1 — обычная активность, больше 1 — активность выше нормы (всплеск), меньше 1 — затишье. Параметр «Period» — сколько предыдущих секунд брать за базу. Полезен для поиска моментов аномальной активности.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        public override MarketDepthIndicatorDataSource DataSource
        {
            get { return MarketDepthIndicatorDataSource.Trades; }
        }

        private ParameterMarketDepthInt _period;

        private MarketDepthDataSeries _rvol;

        private int _lastCount = -1;

        private decimal _perSecondVolume;

        private List<decimal> _buffer = new List<decimal>();

        private bool _hasPrev;

        public override void OnStateChange(MarketDepthIndicatorState state)
        {
            if (state == MarketDepthIndicatorState.Configure)
            {
                _period = CreateParameterInt("Period", 10);
                _rvol = CreateSeries("Rvol", Color.MediumPurple, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(MarketDepth depth)
        {
        }

        public override void OnProcessTrade(Trade trade)
        {
            bool newSecond = _rvol.Values.Count != _lastCount;
            _lastCount = _rvol.Values.Count;

            if (newSecond)
            {
                if (_hasPrev)
                {
                    _buffer.Add(_perSecondVolume);

                    int period = _period.ValueInt;

                    if (period < 1)
                    {
                        period = 10;
                    }

                    while (_buffer.Count > period)
                    {
                        _buffer.RemoveAt(0);
                    }
                }

                _perSecondVolume = 0;
                _hasPrev = true;
            }

            _perSecondVolume += trade.Volume;

            decimal average = 0;

            for (int i = 0; i < _buffer.Count; i++)
            {
                average += _buffer[i];
            }

            if (_buffer.Count > 0)
            {
                average = average / _buffer.Count;
            }

            decimal rvol = average == 0 ? 0m : _perSecondVolume / average;

            SetValue(_rvol, rvol);
        }
    }
}
