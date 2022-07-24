using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;
using OsEngine.Charts.CandleChart.Indicators;
using System.Drawing;
using System;

namespace OsEngine.Robots.Trend
{
    /// <summary>
    /// тестовая стратегия по EMA
    /// </summary>

    public class EmaTest : BotPanel
    {
        public EmaTest(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
            Volume = CreateParameter("Volume", 0.001m, 0.0005m, 100, 0.0005m);
            //CloseAtFastLine = CreateParameter("Close At Fast Line", 0, 1, 1, 1);

            IgnoreFast = CreateParameter("IgnoreFast", 0, 1, 1, 1);
            IgnoreCandelCrossSlow = CreateParameter("IgnoreCandelCrossSlow", 0, 1, 1, 1);
            MaxDropDownPercentCrossSlow = CreateParameter("MaxDropDownPercentCrossSlow", 0.25m, 0.0m, 2, 0.01m);

            EmaSignalLength = CreateParameter("EmaSignal Length", 11, 3, 66, 1);
            EmaSignalType = CreateParameter("EmaSignal Type", 1, 0, 6, 1);

            EmaFastLength = CreateParameter("EmaFast Length", 33, 18, 99, 1);
            EmaFastType = CreateParameter("EmaFast Type", 1, 0, 6, 1);

            EmaSlowLength = CreateParameter("EmaSlow Length", 88, 33, 133, 1);
            EmaSlowType = CreateParameter("EmaSlow Type", 1, 0, 6, 1);



            _emaSignal = new MovingAverage(name + "emaSignal", false)
            {
                Lenght = EmaSignalLength.ValueInt,
                TypeCalculationAverage = (MovingAverageTypeCalculation) EmaSignalType.ValueInt, //MovingAverageTypeCalculation.Exponential,
                ColorBase = Color.DeepSkyBlue
            };
            _emaSignal = _tab.CreateIndicator(_emaSignal);

            _emaFast = new MovingAverage(name + "emaFast", false)
            {
                Lenght = EmaFastLength.ValueInt,
                TypeCalculationAverage = (MovingAverageTypeCalculation)EmaFastType.ValueInt, //MovingAverageTypeCalculation.Exponential,
                ColorBase = Color.Blue
            };
            _emaFast = _tab.CreateIndicator(_emaFast);

            _emaSlow = new MovingAverage(name + "emaSlow", false)
            {
                Lenght = EmaSlowLength.ValueInt,
                TypeCalculationAverage = (MovingAverageTypeCalculation)EmaSlowType.ValueInt, //MovingAverageTypeCalculation.Exponential,
                ColorBase = Color.Violet
            };
            _emaSlow = _tab.CreateIndicator(_emaSlow);

            ParametrsChangeByUser += EnvelopTrend_ParametersChangeByUser;

            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
            _tab.PositionClosingFailEvent += _tab_PositionClosingFailEvent;
        }

        private void EnvelopTrend_ParametersChangeByUser()
        {
            _emaSignal.Lenght = EmaSignalLength.ValueInt;
            _emaSignal.TypeCalculationAverage = (MovingAverageTypeCalculation)EmaSignalType.ValueInt;
            _emaSignal.Reload();

            _emaFast.Lenght = EmaFastLength.ValueInt;
            _emaFast.TypeCalculationAverage = (MovingAverageTypeCalculation)EmaFastType.ValueInt;
            _emaFast.Reload();

            _emaSlow.Lenght = EmaSlowLength.ValueInt;
            _emaSlow.TypeCalculationAverage = (MovingAverageTypeCalculation)EmaSlowType.ValueInt;
            _emaSlow.Reload();

        }

        /// <summary>
        /// bot name
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "EmaTest";
        }

        /// <summary>
        /// strategy name
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {

        }

        /// <summary>
        /// trade tab
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        //indicators индикаторы

        /// <summary>
        /// ema сигнальная
        /// </summary>
        private MovingAverage _emaSignal;

        /// <summary>
        /// ema быстрая
        /// </summary>
        private MovingAverage _emaFast;

        /// <summary>
        /// ema медленная
        /// </summary>
        private MovingAverage _emaSlow;

        //settings настройки публичные

        public StrategyParameterInt CloseAtFastLine;

        public StrategyParameterDecimal Volume;

        public StrategyParameterString Regime;

        public StrategyParameterDecimal BollingerDeviation;

        public StrategyParameterInt IgnoreFast;
        public StrategyParameterInt IgnoreCandelCrossSlow;
        public StrategyParameterDecimal MaxDropDownPercentCrossSlow;

        public StrategyParameterInt EmaSignalLength;
        public StrategyParameterInt EmaFastLength;
        public StrategyParameterInt EmaSlowLength;

        public StrategyParameterInt EmaSignalType;
        public StrategyParameterInt EmaFastType;
        public StrategyParameterInt EmaSlowType;


        private struct SimpleLine
        {
            public decimal from;
            public decimal to;
        }

        private enum SimpleLinesCrossResult
        {
            None,
            Up,
            Down,
        }

        private SimpleLinesCrossResult GetLinesCrossResult(SimpleLine signalLine, SimpleLine secondLine)
        {
            SimpleLinesCrossResult res = SimpleLinesCrossResult.None;

            if (signalLine.from < secondLine.from && signalLine.to > secondLine.to)
            {
                res = SimpleLinesCrossResult.Up;
            }
            else if (signalLine.from > secondLine.from && signalLine.to < secondLine.to)
            {
                res = SimpleLinesCrossResult.Down;
            }

            return res;
        }

        private SimpleLine GetLineFromLastValues(List<decimal> Values)
        {
            return new SimpleLine
            {
                from = Values[_emaSignal.Values.Count - 2],
                to = Values[_emaSignal.Values.Count - 1]
            };
        }

        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if (Regime.ValueString == "Off")
            {
                return;
            }

            if (((_emaSignal.Values == null)
                || (_emaFast.Values == null)
                || (_emaSlow.Values == null))
                && (_emaSlow.Values.Count < 2))
            {
                return;
            }

            var signalLine = GetLineFromLastValues(_emaSignal.Values);
            var fastLine = GetLineFromLastValues(_emaFast.Values);
            var slowLine = GetLineFromLastValues(_emaSlow.Values);

            var crossToFast = GetLinesCrossResult(signalLine, fastLine);
            var crossToSlow = GetLinesCrossResult(signalLine, slowLine);

            List<Position> positions = _tab.PositionsOpenAll;

            decimal lastCandleBodyMedian = candles[candles.Count - 1].Open + (candles[candles.Count - 1].Close - candles[candles.Count - 1].Open) / 2;

            decimal lastClose = candles[candles.Count - 1].Close;


            if (positions.Count == 0)
            { // open logic

                if (Regime.ValueString == "OnlyClosePosition")
                {
                    return;
                }

                if ((crossToSlow == SimpleLinesCrossResult.Up) && (Regime.ValueString != "OnlyShort"))
                    _tab.BuyAtMarket(Volume.ValueDecimal);
                else if ((crossToSlow == SimpleLinesCrossResult.Down) && (Regime.ValueString != "OnlyLong"))
                    _tab.SellAtMarket(Volume.ValueDecimal);
            }
            else
            { // stop logic

                var position = positions[0];

                if (position.State != PositionStateType.Open)
                {
                    return;
                }

                var openPositionCandleIndex = GetCandleIndexByTime(position.TimeOpen, candles);
                var deltaCandlesCount = (candles.Count - 1) - openPositionCandleIndex;

                //var deltaLines = GetDeltaLines();

                var minDownValue = position.EntryPrice * MaxDropDownPercentCrossSlow.ValueDecimal/100m; // до минимальной просадки не выходим

                if (lastClose < position.EntryPrice + minDownValue
                    && lastClose > position.EntryPrice - minDownValue
                    && deltaCandlesCount < 20
                    )
                    return;

                if (position.Direction == Side.Buy)
                {
                    if (
                        // пересекли медленную
                        (crossToSlow == SimpleLinesCrossResult.Down)
                        // пересекли быструю и надо учитывать быструю
                        || (crossToFast == SimpleLinesCrossResult.Down && IgnoreFast.ValueInt == 0)
                        // свеча открылась уже после медленной и закрылась ниже минимально допустимой /2
                        || (candles[candles.Count - 1].Open < slowLine.to && lastClose < slowLine.to - minDownValue / 2)
                        // середина свечи ниже медленной и двигалась свеча не в нашу пользу и тело свечи больше минимально допустимого
                        || (lastCandleBodyMedian < slowLine.to && candles[candles.Count - 1].IsDown && candles[candles.Count - 1].Body > minDownValue)
                        )
                    {
                        _tab.CloseAtMarket(position, position.OpenVolume);
                    }

                }
                if (position.Direction == Side.Sell)
                {
                    if (
                        // пересекли медленную
                        (crossToSlow == SimpleLinesCrossResult.Up)
                        // пересекли быструю и надо учитывать быструю
                        || (crossToFast == SimpleLinesCrossResult.Up && IgnoreFast.ValueInt == 0)
                        // свеча открылась уже после медленной и закрылась ниже минимально допустимой /2
                        || (candles[candles.Count - 1].Open > slowLine.to && lastClose > slowLine.to - minDownValue / 2)
                        // середина свечи ниже медленной и двигалась свеча не в нашу пользу и тело свечи больше минимально допустимого
                        || (lastCandleBodyMedian > slowLine.to && candles[candles.Count - 1].IsUp && candles[candles.Count - 1].Body > minDownValue)
                        
                        )
                    {
                        _tab.CloseAtMarket(position, position.OpenVolume);
                    }
                }
            }
        }

        private bool isGoodPrevCandles(List<Candle> candles)
        {
            var lastCandleBody = candles[candles.Count - 1].Body;
            var fewCandlesBody = candles[candles.Count - 2].Body + candles[candles.Count - 3].Body + candles[candles.Count - 4].Body;
            var res = (fewCandlesBody * 4 > lastCandleBody);
            return res;
        }

        private int GetCandleIndexByTime(DateTime tagetTime, List<Candle> candles)
        {
            int res = 0;

            if (tagetTime < DateTime.Now.AddDays(-100))
                return candles.Count - 1;

            var duration = candles[1].TimeStart - candles[0].TimeStart;

            for (int i = candles.Count - 1; i > 0; i--)
            {
                if (tagetTime >= candles[i].TimeStart && tagetTime < candles[i].TimeStart.Add(duration))
                {
                    res = i;
                    break;
                }
            }

            return res;
        }

        private decimal GetDeltaLines()
        {
            decimal emaFastValue = _emaFast.Values[_emaFast.Values.Count - 1];
            decimal emaSignalValue = _emaSignal.Values[_emaSignal.Values.Count - 1];
            decimal emaSlowValue = _emaSlow.Values[_emaSlow.Values.Count - 1];

            decimal high = emaFastValue;
            decimal low = emaFastValue;

            if (high < emaSignalValue)
                high = emaSignalValue;
            if (high < emaSlowValue)
                high = emaSlowValue;

            if (low > emaSignalValue)
                low = emaSignalValue;
            if (low > emaSlowValue)
                low = emaSlowValue;

            return high-low;
        }




        private void _tab_PositionClosingFailEvent(Position position)
        {
            if (position.CloseActiv)
            {
                _tab.CloseAtMarket(position, position.OpenVolume);
            }
        }

    }
}