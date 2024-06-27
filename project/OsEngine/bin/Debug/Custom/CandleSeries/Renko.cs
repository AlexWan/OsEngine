/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *���� ����� �� ������������� ���� ������������ ������ ��������� http://o-s-a.net/doc/license_simple_engine.pdf
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
            {// ���� ������ ������ ������
                return;
            }

            if (CandlesAll == null
                || CandlesAll.Count == 0)
            {
                _rencoStartPrice = price;
                _rencoLastSide = Side.None;
                // ������ ������ ������
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

                CandlesAll.Add(candle);

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }

                return;
            }

            decimal renDist = RencoCandlesPoints.ValueDecimal;

            if (
                (_rencoLastSide == Side.None && Math.Abs(_rencoStartPrice - price) >= renDist)
                ||
                (_rencoLastSide == Side.Buy && price - _rencoStartPrice >= renDist)
                ||
                (_rencoLastSide == Side.Buy && _rencoStartPrice - price >= renDist * 2)
                ||
                (_rencoLastSide == Side.Sell && _rencoStartPrice - price >= renDist)
                ||
                (_rencoLastSide == Side.Sell && price - _rencoStartPrice >= renDist * 2)
                )
            {
                // ���� ������ ������ �� ����� ������

                Candle lastCandle = CandlesAll[CandlesAll.Count - 1];



                if (
                    (_rencoLastSide == Side.None && price - _rencoStartPrice >= renDist)
                    ||
                    (_rencoLastSide == Side.Buy && price - _rencoStartPrice >= renDist)
                    )
                {
                    _rencoLastSide = Side.Buy;
                    _rencoStartPrice = _rencoStartPrice + renDist;
                    lastCandle.High = _rencoStartPrice;
                }
                else if (
                (_rencoLastSide == Side.None && _rencoStartPrice - price >= renDist)
                ||
                (_rencoLastSide == Side.Sell && _rencoStartPrice - price >= renDist)
                )
                {
                    _rencoLastSide = Side.Sell;
                    _rencoStartPrice = _rencoStartPrice - renDist;
                    lastCandle.Low = _rencoStartPrice;
                }
                else if (
                    _rencoLastSide == Side.Buy && _rencoStartPrice - price >= renDist * 2)
                {
                    _rencoLastSide = Side.Sell;
                    lastCandle.Open = _rencoStartPrice - renDist;
                    _rencoStartPrice = _rencoStartPrice - renDist * 2;
                    lastCandle.Low = _rencoStartPrice;
                }
                else if (
                    _rencoLastSide == Side.Sell && price - _rencoStartPrice >= renDist * 2)
                {
                    _rencoLastSide = Side.Buy;
                    lastCandle.Open = _rencoStartPrice + renDist;
                    _rencoStartPrice = _rencoStartPrice + renDist * 2;
                    lastCandle.High = _rencoStartPrice;
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
                    // ���� ��������� ������ ��� �� ������� � �� ���������
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

                CandlesAll.Add(newCandle);

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }

                return;
            }

            if (CandlesAll != null)
            {
                // ���� ������ ������ ������ �����

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