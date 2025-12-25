/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Candles.Factory;
using OsEngine.Entity;
using System;
using System.Collections.Generic;

namespace OsEngine.Candles.Series
{
    public class Renko : ACandlesSeriesRealization
    {
        public CandlesParameterString ValueType;

        public CandlesParameterDecimal RencoCandlesPoints;

        public CandlesParameterBool RencoIsBuildShadows;

        public override void OnStateChange(CandleSeriesState state)
        {
            if (state == CandleSeriesState.Configure)
            {
                ValueType
                    = CreateParameterStringCollection("valueType", "Value type",
                    "Percent", new List<string> { "Absolute", "Percent" });

                RencoCandlesPoints = CreateParameterDecimal("MinMove", "Min move", 0.2m);

                RencoIsBuildShadows = CreateParameterBool("RencoIsBuildShadows", "Build shadows", false);
            }
            else if (state == CandleSeriesState.ParametersChange)
            {
                if (CandlesAll != null)
                {
                    CandlesAll.Clear();
                }
            }
        }

        private decimal _rencoStartPrice;

        private Side _rencoLastSide;

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
                _rencoStartPrice = price;
                _rencoLastSide = Side.None;
                // пришла первая сделка
                CandlesAll = new List<Candle>();

                DateTime timeNextCandle = time;

                while (timeNextCandle.Millisecond != 0)
                {
                    timeNextCandle = timeNextCandle.AddMilliseconds(-1);
                }

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

                if (CandlesAll.Count > 0)
                {
                    candle.OpenInterest = CandlesAll[^1].OpenInterest;
                }

                CandlesAll.Add(candle);

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }

                return;
            }

            if (CandlesAll != null
               && CandlesAll.Count > 0
               && _rencoStartPrice == 0)
            {
                _rencoStartPrice = CandlesAll[CandlesAll.Count - 1].Close;

                if (CandlesAll[CandlesAll.Count - 1].Close > CandlesAll[CandlesAll.Count - 1].Open)
                {
                    _rencoLastSide = Side.Buy;
                }
                else
                {
                    _rencoLastSide = Side.Sell;
                }
            }

            decimal renDist = RencoCandlesPoints.ValueDecimal;
            RenkoNewCandleType newCandleType = RenkoNewCandleType.None;

            bool isNewCandle = false;

            if (ValueType.ValueString == "Absolute")
            {
                if ((_rencoLastSide == Side.None && Math.Abs(_rencoStartPrice - price) >= renDist))
                {
                    newCandleType = RenkoNewCandleType.None;
                    isNewCandle = true;
                }
                else if (_rencoLastSide == Side.Buy && price - _rencoStartPrice >= renDist)
                {
                    newCandleType = RenkoNewCandleType.NewUpCandle;
                    isNewCandle = true;
                }
                else if (_rencoLastSide == Side.Buy && _rencoStartPrice - price >= renDist * 2)
                {
                    newCandleType = RenkoNewCandleType.Revers;
                    isNewCandle = true;
                }
                else if (_rencoLastSide == Side.Sell && _rencoStartPrice - price >= renDist)
                {
                    newCandleType = RenkoNewCandleType.NewDownCandle;
                    isNewCandle = true;
                }
                else if (_rencoLastSide == Side.Sell && price - _rencoStartPrice >= renDist * 2)
                {
                    newCandleType = RenkoNewCandleType.Revers;
                    isNewCandle = true;
                }
            }
            else if (ValueType.ValueString == "Percent")
            {
                decimal distance = CandlesAll[CandlesAll.Count - 1].Close - CandlesAll[CandlesAll.Count - 1].Open;

                decimal movePercent = distance / (price / 100);

                if (_rencoLastSide == Side.None && Math.Abs(movePercent) >= renDist)
                {
                    newCandleType = RenkoNewCandleType.None;
                    renDist = Math.Abs(distance);
                    isNewCandle = true;
                }
                else if (_rencoLastSide == Side.Buy && movePercent >= renDist)
                {
                    newCandleType = RenkoNewCandleType.NewUpCandle;
                    renDist = Math.Abs(distance);
                    isNewCandle = true;
                }
                else if (_rencoLastSide == Side.Buy && movePercent <= -(renDist * 2))
                {
                    newCandleType = RenkoNewCandleType.Revers;
                    renDist = Math.Abs(distance);
                    isNewCandle = true;
                }
                else if (_rencoLastSide == Side.Sell && movePercent <= -renDist)
                {
                    newCandleType = RenkoNewCandleType.NewDownCandle;
                    renDist = Math.Abs(distance);
                    isNewCandle = true;
                }
                else if (_rencoLastSide == Side.Sell && movePercent >= renDist * 2)
                {
                    newCandleType = RenkoNewCandleType.Revers;
                    renDist = Math.Abs(distance);
                    isNewCandle = true;
                }
            }

            if (isNewCandle == true)
            {
                // если пришли данные из новой свечки

                Candle lastCandle = CandlesAll[^1];

                if (
                    (_rencoLastSide == Side.None && price - _rencoStartPrice >= 0)
                    ||
                    (_rencoLastSide == Side.Buy && newCandleType == RenkoNewCandleType.NewUpCandle)
                    )
                {
                    _rencoLastSide = Side.Buy;
                    _rencoStartPrice = _rencoStartPrice + renDist;
                    lastCandle.High = _rencoStartPrice;
                }
                else if (
                (_rencoLastSide == Side.None && price - _rencoStartPrice < 0)
                ||
                (_rencoLastSide == Side.Sell && newCandleType == RenkoNewCandleType.NewDownCandle)
                )
                {
                    _rencoLastSide = Side.Sell;
                    _rencoStartPrice = _rencoStartPrice - renDist;
                    lastCandle.Low = _rencoStartPrice;
                }
                else if (
                    _rencoLastSide == Side.Buy && newCandleType == RenkoNewCandleType.Revers)
                {
                    _rencoLastSide = Side.Sell;
                    if (CandlesAll.Count > 2
                        && ValueType.ValueString == "Percent")
                    {
                        lastCandle.Open = CandlesAll[^2].Open;
                        _rencoStartPrice = price;
                        lastCandle.Low = _rencoStartPrice;
                    }
                    else
                    {
                        lastCandle.Open = _rencoStartPrice - renDist;
                        _rencoStartPrice = _rencoStartPrice - renDist * 2;
                        lastCandle.Low = _rencoStartPrice;
                    }
                }
                else if (
                    _rencoLastSide == Side.Sell && newCandleType == RenkoNewCandleType.Revers)
                {
                    if (CandlesAll.Count > 2
                        && ValueType.ValueString == "Percent")
                    {
                        _rencoLastSide = Side.Buy;
                        lastCandle.Open = CandlesAll[^2].Open;
                        _rencoStartPrice = price;
                        lastCandle.High = _rencoStartPrice;
                    }
                    else
                    {
                        _rencoLastSide = Side.Buy;
                        lastCandle.Open = _rencoStartPrice + renDist;
                        _rencoStartPrice = _rencoStartPrice + renDist * 2;
                        lastCandle.High = _rencoStartPrice;
                    }
                }

                lastCandle.Close = _rencoStartPrice;

                if (RencoIsBuildShadows.ValueBool == false)
                {
                    if (lastCandle.IsUp)
                    {
                        lastCandle.Low = lastCandle.Open;
                        lastCandle.High = lastCandle.Close;
                    }
                    else
                    {
                        lastCandle.High = lastCandle.Open;
                        lastCandle.Low = lastCandle.Close;
                    }
                }

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


                Candle newCandle = new Candle()
                {
                    Close = _rencoStartPrice,
                    High = _rencoStartPrice,
                    Low = _rencoStartPrice,
                    Open = _rencoStartPrice,
                    State = CandleState.Started,
                    TimeStart = timeNextCandle,
                    Volume = volume
                };

                if (CandlesAll.Count > 0)
                {
                    newCandle.OpenInterest = CandlesAll[^1].OpenInterest;
                }

                CandlesAll.Add(newCandle);

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }

                if (newCandle.TimeStart.Day == CandlesAll[CandlesAll.Count - 1].TimeStart.Day)
                {
                    // recursion. When intraday gaps happen
                    UpDateCandle(time, price, volume, canPushUp, side);
                }

                return;
            }

            if (CandlesAll != null)
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

    public enum RenkoNewCandleType
    {
        None,
        NewUpCandle,
        NewDownCandle,
        Revers
    }
}