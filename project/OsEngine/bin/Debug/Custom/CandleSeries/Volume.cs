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
    public class Volume : ACandlesSeriesRealization
    {
        public CandlesParameterDecimal VolumeToCloseCandle;

        public override void OnStateChange(CandleSeriesState state)
        {
            if (state == CandleSeriesState.Configure)
            {
                VolumeToCloseCandle = CreateParameterDecimal("VolumeToCloseCandle", "Volume to close candle", 10000m);
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
            {// ���� ������ ������ ������
                return;
            }

            if (CandlesAll == null
                || CandlesAll.Count == 0)
            {
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

            if (CandlesAll != null &&
                CandlesAll[CandlesAll.Count - 1].Volume >= VolumeToCloseCandle.ValueDecimal)
            {
                // ���� ������ ������ �� ����� ������

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