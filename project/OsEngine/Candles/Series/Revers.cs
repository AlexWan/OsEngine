/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Candles.Factory;
using OsEngine.Entity;
using OsEngine.Language;
using System;
using System.Collections.Generic;

namespace OsEngine.Candles.Series
{
    [Candle("Revers")]
    public class Revers : ACandlesSeriesRealization
    {
        public CandlesParameterString ValueType;

        public CandlesParameterDecimal ReversCandlesPointsMinMove;

        public CandlesParameterDecimal ReversCandlesPointsBackMove;

        public override void OnStateChange(CandleSeriesState state)
        {
            if (state == CandleSeriesState.Configure)
            {
                ValueType
                    = CreateParameterStringCollection("valueType", OsLocalization.Market.Label122,
                    "Percent", new List<string> { "Absolute", "Percent" });

                ReversCandlesPointsMinMove = CreateParameterDecimal("MinMove", OsLocalization.Market.Label18, 0.2m);
                ReversCandlesPointsBackMove = CreateParameterDecimal("BackMove", OsLocalization.Market.Label19, 0.1m);
            }
            else if (state == CandleSeriesState.ParametersChange)
            {
                if (CandlesAll != null)
                {
                    CandlesAll.Clear();
                }
            }
        }

        public override void UpDateCandle(DateTime time, decimal price, decimal volume, bool canPushUp, Side side)
        {
            if (CandlesAll != null && CandlesAll.Count > 0 && CandlesAll[CandlesAll.Count - 1] != null &&
            CandlesAll[CandlesAll.Count - 1].TimeStart > time)
            {// если пришли старые данные
                return;
            }

            if (CandlesAll == null
                || CandlesAll.Count == 0)
            {
                // пришла первая сделка
                CandlesAll = new List<Candle>();

                DateTime timeNextCandle = time;

                while (timeNextCandle.Second % 1 != 0)
                {
                    timeNextCandle = timeNextCandle.AddSeconds(-1);
                }


                while (timeNextCandle.Millisecond != 0)
                {
                    timeNextCandle = timeNextCandle.AddMilliseconds(-1);
                }

                timeNextCandle = new DateTime(timeNextCandle.Year, timeNextCandle.Month, timeNextCandle.Day,
                    timeNextCandle.Hour, timeNextCandle.Minute, timeNextCandle.Second, timeNextCandle.Millisecond);

                Candle candle = new Candle()
                {
                    Close = price,
                    High = price,
                    Low = price,
                    Open = price,
                    State = CandleState.Started,
                    TimeStart = timeNextCandle,
                    Volume = volume
                };

                CandlesAll.Add(candle);

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }

                return;
            }

            bool candleReady = false;

            Candle lastCandle = CandlesAll[CandlesAll.Count - 1];

            if (ValueType.ValueString == "Absolute")
            {
                if (lastCandle.High - lastCandle.Open >= ReversCandlesPointsMinMove.ValueDecimal
                    &&
                    lastCandle.High - lastCandle.Close >= ReversCandlesPointsBackMove.ValueDecimal)
                { // есть откат от хая
                    candleReady = true;
                }

                if (lastCandle.Open - lastCandle.Low >= ReversCandlesPointsMinMove.ValueDecimal
                    &&
                    lastCandle.Close - lastCandle.Low >= ReversCandlesPointsBackMove.ValueDecimal)
                { // есть откат от лоя
                    candleReady = true;
                }
            }
            else if (ValueType.ValueString == "Percent")
            {
                if (lastCandle.High - lastCandle.Open > 0
                    && lastCandle.High - lastCandle.Close > 0)
                {
                    decimal moveUpPercent = (lastCandle.High - lastCandle.Open) / (lastCandle.Open / 100);
                    decimal backMoveFromHighPercent = (lastCandle.High - lastCandle.Close) / (lastCandle.Close / 100);

                    if (moveUpPercent >= ReversCandlesPointsMinMove.ValueDecimal
                    &&
                    backMoveFromHighPercent >= ReversCandlesPointsBackMove.ValueDecimal)
                    {// есть откат от хая
                        candleReady = true;
                    }
                }

                if (lastCandle.Open - lastCandle.Low > 0
                    && lastCandle.Close - lastCandle.Low > 0)
                {
                    decimal moveDownPercent = (lastCandle.Open - lastCandle.Low) / (lastCandle.Low / 100);

                    decimal backMoveFromLowPercent = (lastCandle.Close - lastCandle.Low) / (lastCandle.Low / 100);

                    if (moveDownPercent >= ReversCandlesPointsMinMove.ValueDecimal &&
                        backMoveFromLowPercent >= ReversCandlesPointsBackMove.ValueDecimal)
                    { // есть откат от лоя
                        candleReady = true;
                    }
                }
            }

            if (CandlesAll != null &&
                candleReady)
            {
                // если пришли данные из новой свечки

                if (CandlesAll[CandlesAll.Count - 1].State != CandleState.Finished)
                {
                    // если последнюю свечку ещё не закрыли и не отправили
                    CandlesAll[CandlesAll.Count - 1].State = CandleState.Finished;

                    if (canPushUp)
                    {
                        UpdateFinishCandle();
                    }
                }

                DateTime timeNextCandle = time;

                while (timeNextCandle.Millisecond != 0)
                {
                    timeNextCandle = timeNextCandle.AddMilliseconds(-1);
                }

                timeNextCandle = new DateTime(timeNextCandle.Year, timeNextCandle.Month, timeNextCandle.Day,
                    timeNextCandle.Hour, timeNextCandle.Minute, timeNextCandle.Second, timeNextCandle.Millisecond);

                Candle newCandle = new Candle()
                {
                    Close = price,
                    High = price,
                    Low = price,
                    Open = price,
                    State = CandleState.Started,
                    TimeStart = timeNextCandle,
                    Volume = volume
                };

                CandlesAll.Add(newCandle);

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }

                return;
            }

            if (CandlesAll != null &&
                candleReady == false)
            {
                // если пришли данные внутри свечи

                CandlesAll[CandlesAll.Count - 1].Volume += volume;
                CandlesAll[CandlesAll.Count - 1].Close = price;

                if (CandlesAll[CandlesAll.Count - 1].High < price)
                {
                    CandlesAll[CandlesAll.Count - 1].High = price;
                }

                if (CandlesAll[CandlesAll.Count - 1].Low > price)
                {
                    CandlesAll[CandlesAll.Count - 1].Low = price;
                }

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }
            }
        }
    }
}