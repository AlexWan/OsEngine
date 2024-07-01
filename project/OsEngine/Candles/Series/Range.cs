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
    [Candle("Range")]
    public class Range : ACandlesSeriesRealization
    {
        public CandlesParameterString ValueType;

        public CandlesParameterDecimal RangeCandlesPoints;

        public override void OnStateChange(CandleSeriesState state)
        {
            if (state == CandleSeriesState.Configure)
            {
                ValueType
                    = CreateParameterStringCollection("valueType", OsLocalization.Market.Label122,
                    "Percent", new List<string> { "Absolute", "Percent" });

                RangeCandlesPoints = CreateParameterDecimal("MinMove", OsLocalization.Market.Label18, 0.2m);
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

            bool isNewCandle = false;

            if (ValueType.ValueString == "Absolute")
            {
                if (CandlesAll[CandlesAll.Count - 1].High - CandlesAll[CandlesAll.Count - 1].Low >= RangeCandlesPoints.ValueDecimal)
                {
                    isNewCandle = true;
                }
            }
            else if (ValueType.ValueString == "Percent")
            {
                decimal distance = CandlesAll[CandlesAll.Count - 1].High - CandlesAll[CandlesAll.Count - 1].Low;

                decimal movePercent = distance / (CandlesAll[CandlesAll.Count - 1].Low / 100);

                if (distance != 0
                    && movePercent != 0
                    && movePercent > RangeCandlesPoints.ValueDecimal)
                {
                    isNewCandle = true;
                }
            }

            if (isNewCandle)
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
            else
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
