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
    public class SpeedOfTape : AMarketDepth
    {
        public override string Description
        {
            get
            {
                string eng = "Speed of Tape: number of trades per second. It shows market activity: spikes in the trade count mean a burst of interest (news, a large player, a level breakout), a drop means a lull. High tape speed together with a price move confirms the move; high tape speed without a price move may indicate volume absorption. The series 'Speed' is the number of trades in the current second.";
                string ru = "Скорость ленты (Speed of Tape) — сколько сделок проходит в секунду. Показывает активность рынка: всплеск числа сделок говорит о вспышке интереса (новости, крупный игрок, пробой уровня), спад — о затишье. Высокая скорость ленты вместе с движением цены подтверждает движение; высокая скорость без движения цены может указывать на поглощение объёма. Серия «Speed» — число сделок за текущую секунду.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        public override MarketDepthIndicatorDataSource DataSource
        {
            get { return MarketDepthIndicatorDataSource.Trades; }
        }

        private IndicatorDataSeries _speed;

        private int _lastCount = -1;

        private int _perSecondCount;

        public override void OnStateChange(MarketDepthIndicatorState state)
        {
            if (state == MarketDepthIndicatorState.Configure)
            {
                _speed = CreateSeries("Speed", Color.Green, IndicatorChartPaintType.Column, true);
            }
        }

        public override void OnProcess(MarketDepth depth)
        {
        }

        public override void OnProcessTrade(Trade trade)
        {
            bool newSecond = _speed.Values.Count != _lastCount;
            _lastCount = _speed.Values.Count;

            if (newSecond)
            {
                _perSecondCount = 0;
            }

            _perSecondCount++;

            SetValue(_speed, _perSecondCount);
        }
    }
}
