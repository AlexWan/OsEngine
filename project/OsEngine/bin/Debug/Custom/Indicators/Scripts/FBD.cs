using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    /// <summary>
    /// индикатор Fund Balance Divergenсe
    /// тест идеи вот от сюда https://smart-lab.ru/blog/610172.php
    /// </summary>
    public class FBD : Aindicator
    {

        private IndicatorParameterInt _lookBack;

        private IndicatorParameterInt _lookUp;

        private IndicatorDataSeries _series;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _lookUp = CreateParameterInt("Look Up", 10);
                _lookBack = CreateParameterInt("Look Back", 10);

                _series = CreateSeries("FBD value", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            decimal priceInter = GetMeadlePriceInter(candles, index);
            if (priceInter == 0)
            {
                return;
            }
            _series.Values[index] = (candles[index].Close - priceInter) / priceInter * 100;
        }

        private decimal _fundsMeadlePriceInter;
        private int _quarterNum;
        private bool _fullCheckPriceInter;

        private decimal GetMeadlePriceInter(List<Candle> candles, int index)
        {
            if ((candles[index].TimeStart - candles[0].TimeStart).TotalDays < 90)
            {
                return 0;
            }

            int curQuarter = GetQuarterNum(candles[index].TimeStart);

            if (_quarterNum == curQuarter &&
                _fullCheckPriceInter == true)
            {
                return _fundsMeadlePriceInter;
            }

            _fullCheckPriceInter = false;
            _quarterNum = curQuarter;

            int startMonth = candles[index].TimeStart.Month;

            int myMonth = 0;

            if (startMonth > 9)
            {
                myMonth = 9;
            }
            else if (startMonth > 6)
            {
                myMonth = 6;
            }
            else if (startMonth > 3)
            {
                myMonth = 3;
            }

            List<Candle> candlesUp = new List<Candle>();
            List<Candle> candlesBack = new List<Candle>();

            for (int i = index; i > 0; i--)
            {
                if (candles[i].TimeStart.Month == myMonth)
                {
                    candlesUp.Insert(0, candles[i]);
                }
                if ((myMonth != 0 && candles[i].TimeStart.Month + 1 == myMonth) ||
                    myMonth == 0 && candles[i].TimeStart.Month == 12)
                {
                    candlesBack.Insert(0, candles[i]);
                }

                if ((myMonth == 9 && candles[i].TimeStart.Month == 7) ||
                    (myMonth == 6 && candles[i].TimeStart.Month == 4) ||
                    (myMonth == 3 && candles[i].TimeStart.Month == 1) ||
                    (myMonth == 0 && candles[i].TimeStart.Month == 11))
                {
                    break;
                }
            }

            candlesUp = Cut(candlesUp, _lookUp.ValueInt, true);
            candlesBack = Cut(candlesBack, _lookBack.ValueInt, false);

            candlesUp.AddRange(candlesBack);

            decimal meadlePriceInter = 0;

            if (candlesUp.Count == 0)
            {
                return 0;
            }

            for (int i = 0; i < candlesUp.Count; i++)
            {
                meadlePriceInter += candlesUp[i].Close;
            }

            meadlePriceInter = meadlePriceInter / candlesUp.Count;

            _fundsMeadlePriceInter = meadlePriceInter;

            if (candles[index].TimeStart.Month == 2 ||
                candles[index].TimeStart.Month == 4 ||
                candles[index].TimeStart.Month == 7 ||
                    candles[index].TimeStart.Month == 10)
            {
                _fullCheckPriceInter = true;
            }

            return _fundsMeadlePriceInter;
        }

        private List<Candle> Cut(List<Candle> candles, int dayCount, bool fromStart)
        {
            if (candles.Count == 0)
            {
                return candles;
            }

            List<Candle> result = new List<Candle>();

            int days = 0;
            int curDay = 0;

            if (fromStart)
            {
                for (int i = 0; i < candles.Count; i++)
                {
                    if (candles[i].TimeStart.Day != curDay)
                    {
                        curDay = candles[i].TimeStart.Day;
                        days++;
                    }
                    if (days > dayCount)
                    {
                        break;
                    }
                    result.Add(candles[i]);
                }
            }
            else
            {
                for (int i = candles.Count - 1; i < candles.Count; i--)
                {
                    if (candles[i].TimeStart.Day != curDay)
                    {
                        curDay = candles[i].TimeStart.Day;
                        days++;
                    }
                    if (days > dayCount)
                    {
                        break;
                    }
                    result.Add(candles[i]);
                }
            }

            return result;
        }

        private int GetQuarterNum(DateTime time)
        {
            if (time.Month < 4)
            {
                return 1;
            }
            if (time.Month > 3 && time.Month < 7)
            {
                return 2;
            }
            if (time.Month > 6 && time.Month < 10)
            {
                return 3;
            }
            if (time.Month > 9)
            {
                return 4;
            }
            return 0;
        }
    }
}